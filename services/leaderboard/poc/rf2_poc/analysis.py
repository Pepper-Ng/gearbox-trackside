from __future__ import annotations

import argparse
import json
import math
import statistics
from pathlib import Path
from typing import Any

from .reports import append_sample_to_record, build_report

SESSION_ANALYSIS_VERSION = 1
TELEMETRY_SAMPLES_FILENAME = "telemetry_samples.jsonl"
REPORT_FILENAME = "report.json"
CHANNEL_FIELDS = [
    "throttle_percent",
    "brake_percent",
    "steering_percent",
    "gear",
    "speed_kph",
    "lap_percent",
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Analyze stored telemetry recordings and summarize cadence, gaps, and sample quality."
    )
    parser.add_argument(
        "paths",
        nargs="+",
        help="One or more session directories or telemetry recording folders to analyze.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Output machine-readable JSON instead of human text.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        help="Write JSON output to a file in addition to stdout.",
    )
    return parser.parse_args()


def load_samples_from_jsonl(path: Path) -> list[dict[str, Any]]:
    samples: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                samples.append(json.loads(stripped))
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSON on {path}:{line_number}: {exc}") from exc
    return samples


def safe_numeric(value: Any) -> float | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        try:
            return float(value)
        except ValueError:
            return None
    return None


def median(values: list[float]) -> float | None:
    if not values:
        return None
    return statistics.median(values)


def p95(values: list[float]) -> float | None:
    if not values:
        return None
    sorted_values = sorted(values)
    index = min(len(sorted_values) - 1, math.ceil(0.95 * len(sorted_values)) - 1)
    return sorted_values[index]


def time_series_deltas(values: list[float]) -> list[float]:
    return [current - previous for previous, current in zip(values, values[1:]) if current >= previous]


def summarize_channel_changes(samples: list[dict[str, Any]]) -> dict[str, float | None]:
    output: dict[str, float | None] = {}
    for field in CHANNEL_FIELDS:
        previous: Any = None
        count = 0
        changed = 0
        for sample in samples:
            current = sample.get(field)
            if current is None:
                continue
            if previous is not None:
                count += 1
                if current != previous:
                    changed += 1
            previous = current
        output[field] = round(changed / count, 4) if count else None
    return output


def analyze_driver_samples(samples: list[dict[str, Any]]) -> dict[str, Any]:
    sample_timestamps = [safe_numeric(sample.get("timestamp")) for sample in samples if safe_numeric(sample.get("timestamp")) is not None]
    intervals = time_series_deltas(sample_timestamps)
    lap_percent_values = [safe_numeric(sample.get("lap_percent")) for sample in samples if safe_numeric(sample.get("lap_percent")) is not None]

    telemetry_update_counters = [int(sample["telemetry_update_counter"]) for sample in samples if isinstance(sample.get("telemetry_update_counter"), int)]
    update_repeats = 0
    update_gaps = 0
    for previous, current in zip(telemetry_update_counters, telemetry_update_counters[1:]):
        delta = current - previous
        if delta == 0:
            update_repeats += 1
        elif delta > 1:
            update_gaps += 1

    lap_percent_repeats = 0
    for previous, current in zip(lap_percent_values, lap_percent_values[1:]):
        if previous == current:
            lap_percent_repeats += 1

    gear_zero_count = sum(1 for sample in samples if safe_numeric(sample.get("gear")) == 0.0)
    sample_count = len(samples)
    return {
        "sample_count": sample_count,
        "sample_duration_seconds": round((max(sample_timestamps) - min(sample_timestamps)), 4) if len(sample_timestamps) >= 2 else 0.0,
        "median_sample_interval_seconds": round(median(intervals), 4) if intervals else None,
        "p95_sample_interval_seconds": round(p95(intervals), 4) if intervals else None,
        "telemetry_update_counter_repeat_count": update_repeats,
        "telemetry_update_counter_gap_count": update_gaps,
        "lap_percent_repeat_count": lap_percent_repeats,
        "gear_zero_share": round(gear_zero_count / sample_count, 4) if sample_count else None,
        "channel_change_fractions": summarize_channel_changes(samples),
    }


def build_session_record(samples: list[dict[str, Any]], session_id: str) -> dict[str, Any]:
    record: dict[str, Any] = {
        "id": session_id,
        "track": None,
        "session_type": None,
        "drivers": {},
    }
    for sample in samples:
        append_sample_to_record(record, sample)
    return record


def load_report_if_exists(samples_path: Path) -> dict[str, Any] | None:
    report_path = samples_path.parent / REPORT_FILENAME
    if not report_path.exists():
        return None
    try:
        with report_path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except Exception:
        return None


def analyze_session(samples_path: Path, session_id: str | None = None) -> dict[str, Any]:
    samples = load_samples_from_jsonl(samples_path)
    if session_id is None:
        session_id = samples[0].get("session_id") if samples else samples_path.parent.name
    record = build_session_record(samples, session_id)
    report = load_report_if_exists(samples_path) or build_report(record) or {}
    drivers = []
    driver_samples: dict[int, list[dict[str, Any]]] = {}
    for sample in samples:
        driver_id = sample.get("driver_id")
        if driver_id is None:
            continue
        driver_samples.setdefault(driver_id, []).append(sample)

    for driver_id, driver_sample_list in driver_samples.items():
        driver_name = driver_sample_list[0].get("driver_name")
        driver_analysis = analyze_driver_samples(driver_sample_list)
        driver_analysis.update(
            {
                "driver_id": driver_id,
                "driver_name": driver_name,
                "proper_lap_count": 0,
                "excluded_lap_count": 0,
            }
        )
        drivers.append(driver_analysis)

    session_sample_timestamps = [safe_numeric(sample.get("timestamp")) for sample in samples if safe_numeric(sample.get("timestamp")) is not None]
    total_duration = round(max(session_sample_timestamps) - min(session_sample_timestamps), 4) if len(session_sample_timestamps) >= 2 else 0.0
    session_rate = round(len(samples) / total_duration, 4) if total_duration else None

    lap_summaries = []
    for lap in report.get("all_laps", []):
        lap_summaries.append(
            {
                "lap_id": lap.get("lap_id"),
                "driver_id": lap.get("driver_id"),
                "driver_name": lap.get("driver_name"),
                "lap_number": lap.get("lap_number"),
                "lap_time": lap.get("lap_time"),
                "sample_count": lap.get("sample_count"),
                "lap_classification": lap.get("lap_classification"),
                "proper": lap.get("eligible_for_report"),
                "coverage": lap.get("coverage"),
            }
        )

    return {
        "analysis_version": SESSION_ANALYSIS_VERSION,
        "session_id": session_id,
        "track": report.get("track"),
        "session_type": report.get("session_type"),
        "status": report.get("status"),
        "sample_count": len(samples),
        "sample_duration_seconds": total_duration,
        "effective_sample_rate_hz": session_rate,
        "proper_lap_count": report.get("proper_lap_count"),
        "excluded_lap_count": report.get("excluded_lap_count"),
        "telemetry_sample_count": report.get("telemetry_sample_count"),
        "driver_summaries": drivers,
        "laps": lap_summaries,
    }


def discover_recording_directories(paths: list[Path]) -> list[Path]:
    directories: list[Path] = []
    for path in paths:
        if path.is_file():
            if path.name == TELEMETRY_SAMPLES_FILENAME:
                directories.append(path.parent)
                continue
            if path.name == REPORT_FILENAME:
                directories.append(path.parent)
                continue
            raise ValueError(f"Unsupported file path: {path}")
        if path.is_dir():
            if (path / TELEMETRY_SAMPLES_FILENAME).exists():
                directories.append(path)
                continue
            for child in sorted(path.iterdir()):
                if child.is_dir() and (child / TELEMETRY_SAMPLES_FILENAME).exists():
                    directories.append(child)
            if not any((path / TELEMETRY_SAMPLES_FILENAME).exists() for path in directories):
                raise ValueError(f"Directory does not contain telemetry recordings: {path}")
    return directories


def format_text_report(analysis: dict[str, Any]) -> str:
    lines: list[str] = []
    lines.append(f"Session: {analysis.get('session_id')}")
    lines.append(f"Track: {analysis.get('track')}")
    lines.append(f"Session type: {analysis.get('session_type')}")
    lines.append(f"Status: {analysis.get('status')}")
    lines.append(f"Sample count: {analysis.get('sample_count')}")
    lines.append(f"Duration: {analysis.get('sample_duration_seconds')} s")
    lines.append(f"Effective sample rate: {analysis.get('effective_sample_rate_hz')} Hz")
    lines.append(f"Proper telemetry laps: {analysis.get('proper_lap_count')}")
    lines.append(f"Excluded telemetry laps: {analysis.get('excluded_lap_count')}")
    lines.append(f"Telemetry sample count: {analysis.get('telemetry_sample_count')}")
    lines.append("")
    for driver in analysis.get("driver_summaries", []):
        lines.append(f"Driver {driver.get('driver_name')} ({driver.get('driver_id')}):")
        lines.append(f"  Samples: {driver.get('sample_count')}")
        lines.append(f"  Median interval: {driver.get('median_sample_interval_seconds')} s")
        lines.append(f"  P95 interval: {driver.get('p95_sample_interval_seconds')} s")
        lines.append(f"  Telemetry update counter repeats: {driver.get('telemetry_update_counter_repeat_count')}")
        lines.append(f"  Telemetry update counter gaps: {driver.get('telemetry_update_counter_gap_count')}")
        lines.append(f"  Lap-percent repeats: {driver.get('lap_percent_repeat_count')}")
        lines.append(f"  Gear-zero share: {driver.get('gear_zero_share')}")
        lines.append(f"  Channel change fractions: {driver.get('channel_change_fractions')}")
        lines.append("")
    return "\n".join(lines)


def analyze_paths(paths: list[Path]) -> dict[str, Any]:
    directories = discover_recording_directories(paths)
    sessions = []
    for directory in directories:
        sample_path = directory / TELEMETRY_SAMPLES_FILENAME
        if not sample_path.exists():
            raise ValueError(f"Telemetry samples file not found in {directory}")
        sessions.append(analyze_session(sample_path))
    return {"sessions": sessions}


def main() -> None:
    args = parse_args()
    analysis = analyze_paths([Path(path) for path in args.paths])
    if args.json:
        payload = json.dumps(analysis, indent=2)
    else:
        payload_lines: list[str] = []
        for session in analysis["sessions"]:
            payload_lines.append(format_text_report(session))
            payload_lines.append("---")
        payload = "\n".join(payload_lines).rstrip("\n-")
    print(payload)
    if args.output:
        args.output.write_text(json.dumps(analysis, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
