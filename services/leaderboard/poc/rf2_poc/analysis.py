from __future__ import annotations

import argparse
import json
import math
import statistics
from pathlib import Path
from typing import Any

from .reports import append_sample_to_record, build_report
from .telemetry_capture import RAW_TELEMETRY_FILENAME, load_compact_frames, sample_from_compact_vehicle

SESSION_ANALYSIS_VERSION = 3
DEFAULT_TARGET_HZ = 50.0
DEFAULT_MINIMUM_HZ = 45.0
TELEMETRY_SAMPLES_FILENAME = "telemetry_samples.jsonl"
REPORT_FILENAME = "report.json"
WORKER_STATUS_FILENAME = "telemetry_worker_status.json"
CHANNEL_FIELDS = [
    "throttle_percent",
    "brake_percent",
    "steering_percent",
    "gear",
    "speed_kph",
    "lap_percent",
    "lateral_g",
    "longitudinal_g",
    "vertical_g",
    "g_magnitude",
]
TELEMETRY_CHANNEL_FIELDS = [
    "speed_kph",
    "throttle_percent",
    "brake_percent",
    "steering_percent",
    "lateral_g",
    "longitudinal_g",
    "vertical_g",
    "g_magnitude",
]
MIN_CHANNEL_CHANGES_FOR_RATE = 2


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
    parser.add_argument(
        "--target-hz",
        type=float,
        default=DEFAULT_TARGET_HZ,
        help="Target telemetry cadence used for quality calculations.",
    )
    parser.add_argument(
        "--minimum-hz",
        type=float,
        default=DEFAULT_MINIMUM_HZ,
        help="Minimum acceptable per-car telemetry cadence for pass/warn/fail summaries.",
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


def rate_from_count(sample_count: int, duration_seconds: float) -> float | None:
    if sample_count < 2 or duration_seconds <= 0:
        return None
    return round((sample_count - 1) / duration_seconds, 4)


def rounded(value: float | None, digits: int = 4) -> float | None:
    if value is None:
        return None
    return round(value, digits)


def share(count: int, total: int) -> float | None:
    return round(count / total, 4) if total else None


def summarize_timestamps(
    timestamps: list[float],
    target_hz: float = DEFAULT_TARGET_HZ,
    minimum_hz: float = DEFAULT_MINIMUM_HZ,
) -> dict[str, Any]:
    timestamps = sorted(value for value in timestamps if value is not None)
    intervals = time_series_deltas(timestamps)
    sample_count = len(timestamps)
    duration = round((max(timestamps) - min(timestamps)), 4) if sample_count >= 2 else 0.0
    target_interval = 1.0 / target_hz if target_hz > 0 else None
    minimum_interval = 1.0 / minimum_hz if minimum_hz > 0 else None
    at_target_count = sum(1 for value in intervals if target_interval is not None and value <= target_interval)
    at_minimum_count = sum(1 for value in intervals if minimum_interval is not None and value <= minimum_interval)
    over_2x_target_count = sum(1 for value in intervals if target_interval is not None and value > target_interval * 2.0)
    over_5x_target_count = sum(1 for value in intervals if target_interval is not None and value > target_interval * 5.0)
    late_time_over_minimum = sum(
        max(0.0, value - minimum_interval)
        for value in intervals
        if minimum_interval is not None
    )
    expected_target_samples = int(duration * target_hz) + 1 if duration and target_hz > 0 else None
    expected_minimum_samples = int(duration * minimum_hz) + 1 if duration and minimum_hz > 0 else None
    return {
        "sample_duration_seconds": duration,
        "effective_sample_rate_hz": rate_from_count(sample_count, duration),
        "min_sample_interval_seconds": round(min(intervals), 4) if intervals else None,
        "median_sample_interval_seconds": round(median(intervals), 4) if intervals else None,
        "p95_sample_interval_seconds": round(p95(intervals), 4) if intervals else None,
        "max_sample_interval_seconds": round(max(intervals), 4) if intervals else None,
        "interval_count": len(intervals),
        "target_interval_seconds": rounded(target_interval),
        "minimum_interval_seconds": rounded(minimum_interval),
        "intervals_at_or_above_target_rate_count": at_target_count,
        "intervals_at_or_above_target_rate_share": share(at_target_count, len(intervals)),
        "intervals_at_or_above_minimum_rate_count": at_minimum_count,
        "intervals_at_or_above_minimum_rate_share": share(at_minimum_count, len(intervals)),
        "intervals_over_2x_target_count": over_2x_target_count,
        "intervals_over_5x_target_count": over_5x_target_count,
        "late_time_over_minimum_seconds": round(late_time_over_minimum, 4),
        "expected_samples_at_target_hz": expected_target_samples,
        "expected_samples_at_minimum_hz": expected_minimum_samples,
        "sample_deficit_to_target_hz": max(0, expected_target_samples - sample_count) if expected_target_samples is not None else None,
        "sample_deficit_to_minimum_hz": max(0, expected_minimum_samples - sample_count) if expected_minimum_samples is not None else None,
    }


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


def summarize_channel_presence(samples: list[dict[str, Any]]) -> dict[str, float | None]:
    output: dict[str, float | None] = {}
    total = len(samples)
    for field in CHANNEL_FIELDS:
        present = sum(1 for sample in samples if sample.get(field) is not None)
        output[field] = share(present, total)
    return output


def summarize_channel_activity(
    samples: list[dict[str, Any]],
    fields: list[str] | None = None,
    min_changes_for_rate: int = MIN_CHANNEL_CHANGES_FOR_RATE,
) -> dict[str, Any]:
    fields = fields or TELEMETRY_CHANNEL_FIELDS
    sample_timestamps = [safe_numeric(sample.get("timestamp")) for sample in samples if safe_numeric(sample.get("timestamp")) is not None]
    duration = (max(sample_timestamps) - min(sample_timestamps)) if len(sample_timestamps) >= 2 else 0.0
    channel_metrics: dict[str, Any] = {}
    measured_channels = []
    for field in fields:
        points = [
            (safe_numeric(sample.get("timestamp")), sample.get(field))
            for sample in samples
            if sample.get(field) is not None and safe_numeric(sample.get("timestamp")) is not None
        ]
        points = [(timestamp, value) for timestamp, value in points if timestamp is not None]
        present_count = sum(1 for sample in samples if sample.get(field) is not None)
        change_count = 0
        longest_unchanged_seconds = None
        distinct_values = {value for _, value in points}
        if points:
            previous_value = points[0][1]
            last_change_at = float(points[0][0])
            longest_unchanged_seconds = 0.0
            for timestamp, value in points[1:]:
                timestamp = float(timestamp)
                if value != previous_value:
                    longest_unchanged_seconds = max(longest_unchanged_seconds, timestamp - last_change_at)
                    last_change_at = timestamp
                    previous_value = value
                    change_count += 1
            longest_unchanged_seconds = max(longest_unchanged_seconds, float(points[-1][0]) - last_change_at)
        observed_change_rate = round(change_count / duration, 4) if duration > 0 else None
        measured = observed_change_rate is not None and change_count >= min_changes_for_rate
        metric = {
            "present_count": present_count,
            "presence_share": share(present_count, len(samples)),
            "change_count": change_count,
            "change_share": share(change_count, max(0, present_count - 1)),
            "observed_change_rate_hz": observed_change_rate,
            "longest_unchanged_seconds": round(longest_unchanged_seconds, 4) if longest_unchanged_seconds is not None else None,
            "distinct_value_count": len(distinct_values),
            "measured_for_weakest_link": measured,
        }
        channel_metrics[field] = metric
        if measured:
            measured_channels.append((field, metric))

    weakest = None
    if measured_channels:
        weakest_key, weakest_metric = min(measured_channels, key=lambda item: item[1]["observed_change_rate_hz"] or 0.0)
        weakest = {
            "key": weakest_key,
            "observed_change_rate_hz": weakest_metric.get("observed_change_rate_hz"),
            "change_count": weakest_metric.get("change_count"),
            "longest_unchanged_seconds": weakest_metric.get("longest_unchanged_seconds"),
        }
    return {
        "channels": channel_metrics,
        "measured_channel_count": len(measured_channels),
        "weakest_channel": weakest,
        "weakest_channel_key": (weakest or {}).get("key"),
        "weakest_channel_observed_rate_hz": (weakest or {}).get("observed_change_rate_hz"),
    }


def samples_from_series(series: dict[str, Any]) -> list[dict[str, Any]]:
    lengths = [len(values) for values in series.values() if isinstance(values, list)]
    sample_count = max(lengths, default=0)
    samples = []
    for index in range(sample_count):
        sample = {}
        for field, values in series.items():
            if isinstance(values, list) and index < len(values):
                sample[field] = values[index]
        samples.append(sample)
    return samples


def analyze_driver_samples(
    samples: list[dict[str, Any]],
    target_hz: float = DEFAULT_TARGET_HZ,
    minimum_hz: float = DEFAULT_MINIMUM_HZ,
) -> dict[str, Any]:
    sample_timestamps = [safe_numeric(sample.get("timestamp")) for sample in samples if safe_numeric(sample.get("timestamp")) is not None]
    timestamp_summary = summarize_timestamps(sample_timestamps, target_hz=target_hz, minimum_hz=minimum_hz)
    lap_percent_values = [safe_numeric(sample.get("lap_percent")) for sample in samples if safe_numeric(sample.get("lap_percent")) is not None]

    telemetry_update_counters = [int(sample["telemetry_update_counter"]) for sample in samples if isinstance(sample.get("telemetry_update_counter"), int)]
    update_repeats = 0
    update_gaps = 0
    update_missing = 0
    for previous, current in zip(telemetry_update_counters, telemetry_update_counters[1:]):
        delta = current - previous
        if delta == 0:
            update_repeats += 1
        elif delta > 1:
            update_gaps += 1
            update_missing += delta - 1

    lap_percent_repeats = 0
    for previous, current in zip(lap_percent_values, lap_percent_values[1:]):
        if previous == current:
            lap_percent_repeats += 1

    gear_zero_count = sum(1 for sample in samples if safe_numeric(sample.get("gear")) == 0.0)
    sample_count = len(samples)
    lap_percent_repeats_share = round(lap_percent_repeats / max(1, len(lap_percent_values) - 1), 4) if len(lap_percent_values) >= 2 else None
    update_counter_span = None
    update_counter_rate = None
    unique_update_rate = None
    if telemetry_update_counters and timestamp_summary["sample_duration_seconds"]:
        update_counter_span = max(telemetry_update_counters) - min(telemetry_update_counters)
        update_counter_rate = round(update_counter_span / timestamp_summary["sample_duration_seconds"], 4)
        unique_update_rate = rate_from_count(len(set(telemetry_update_counters)), timestamp_summary["sample_duration_seconds"])
    output = {
        "sample_count": sample_count,
        "telemetry_update_counter_repeat_count": update_repeats,
        "telemetry_update_counter_gap_count": update_gaps,
        "telemetry_update_counter_missing_count": update_missing,
        "telemetry_update_counter_span": update_counter_span,
        "telemetry_update_counter_rate_hz": update_counter_rate,
        "unique_telemetry_update_count": len(set(telemetry_update_counters)),
        "unique_telemetry_update_rate_hz": unique_update_rate,
        "lap_percent_repeat_count": lap_percent_repeats,
        "lap_percent_repeat_share": lap_percent_repeats_share,
        "gear_zero_share": round(gear_zero_count / sample_count, 4) if sample_count else None,
        "channel_presence_fractions": summarize_channel_presence(samples),
        "channel_change_fractions": summarize_channel_changes(samples),
        "channel_activity": summarize_channel_activity(samples),
    }
    output["weakest_channel"] = output["channel_activity"].get("weakest_channel")
    output["weakest_channel_key"] = output["channel_activity"].get("weakest_channel_key")
    output["weakest_channel_observed_rate_hz"] = output["channel_activity"].get("weakest_channel_observed_rate_hz")
    output.update(timestamp_summary)
    return output


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


def analyze_session(
    samples_path: Path,
    session_id: str | None = None,
    target_hz: float = DEFAULT_TARGET_HZ,
    minimum_hz: float = DEFAULT_MINIMUM_HZ,
) -> dict[str, Any]:
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
        driver_analysis = analyze_driver_samples(driver_sample_list, target_hz=target_hz, minimum_hz=minimum_hz)
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
    session_timing = summarize_timestamps(session_sample_timestamps, target_hz=target_hz, minimum_hz=minimum_hz)

    lap_summaries = []
    for lap in report.get("all_laps", []):
        lap_summaries.append(analyze_report_lap(lap, target_hz=target_hz, minimum_hz=minimum_hz))

    preservation_summary = build_quality_summary(
        lap_summaries=lap_summaries,
        driver_summaries=drivers,
        raw_frame_summary=None,
        target_hz=target_hz,
        minimum_hz=minimum_hz,
    )
    channel_quality_summary = build_channel_quality_summary(
        lap_summaries=lap_summaries,
        driver_summaries=drivers,
        target_hz=target_hz,
        minimum_hz=minimum_hz,
    )

    return {
        "analysis_version": SESSION_ANALYSIS_VERSION,
        "session_id": session_id,
        "target_hz": target_hz,
        "minimum_hz": minimum_hz,
        "track": report.get("track"),
        "session_type": report.get("session_type"),
        "status": report.get("status"),
        "sample_count": len(samples),
        "sample_duration_seconds": session_timing["sample_duration_seconds"],
        "effective_sample_rate_hz": session_timing["effective_sample_rate_hz"],
        "proper_lap_count": report.get("proper_lap_count"),
        "excluded_lap_count": report.get("excluded_lap_count"),
        "telemetry_sample_count": report.get("telemetry_sample_count"),
        "telemetry_raw_file": report.get("telemetry_raw_file"),
        "telemetry_import_stats": report.get("telemetry_import_stats"),
        "quality_summary": preservation_summary,
        "preservation_summary": preservation_summary,
        "channel_quality_summary": channel_quality_summary,
        "driver_summaries": drivers,
        "laps": lap_summaries,
    }


def analyze_report_lap(
    lap: dict[str, Any],
    target_hz: float = DEFAULT_TARGET_HZ,
    minimum_hz: float = DEFAULT_MINIMUM_HZ,
) -> dict[str, Any]:
    series = lap.get("series") or {}
    timestamps = [safe_numeric(value) for value in (series.get("timestamp") or []) if safe_numeric(value) is not None]
    timing = summarize_timestamps(timestamps, target_hz=target_hz, minimum_hz=minimum_hz)
    sample_count = int(lap.get("sample_count") or max((len(values) for values in series.values() if isinstance(values, list)), default=0))
    lap_samples = samples_from_series(series)
    channel_activity = summarize_channel_activity(lap_samples)
    weakest_channel_rate = safe_numeric(channel_activity.get("weakest_channel_observed_rate_hz"))
    lap_time = safe_numeric(lap.get("lap_time"))
    lap_time_sample_rate = round(sample_count / lap_time, 4) if lap_time and lap_time > 0 else None
    expected_minimum_samples = int(lap_time * minimum_hz) + 1 if lap_time and minimum_hz > 0 else None
    expected_target_samples = int(lap_time * target_hz) + 1 if lap_time and target_hz > 0 else None
    preservation_rate = lap_time_sample_rate or timing.get("effective_sample_rate_hz")
    if preservation_rate is None:
        quality_status = "unknown"
    elif preservation_rate >= minimum_hz:
        quality_status = "pass"
    else:
        quality_status = "fail"
    if weakest_channel_rate is None:
        channel_quality_status = "unknown"
    elif weakest_channel_rate >= minimum_hz:
        channel_quality_status = "pass"
    else:
        channel_quality_status = "fail"
    output = {
        "lap_id": lap.get("lap_id"),
        "driver_id": lap.get("driver_id"),
        "driver_name": lap.get("driver_name"),
        "lap_number": lap.get("lap_number"),
        "lap_time": lap_time,
        "sample_count": sample_count,
        "lap_time_sample_rate_hz": lap_time_sample_rate,
        "weakest_channel_key": channel_activity.get("weakest_channel_key"),
        "weakest_channel_observed_rate_hz": weakest_channel_rate,
        "effective_weakest_rate_hz": round(min(rate for rate in (preservation_rate, weakest_channel_rate) if rate is not None), 4) if preservation_rate is not None or weakest_channel_rate is not None else None,
        "channel_activity": channel_activity,
        "sample_deficit_to_target_hz_by_lap_time": max(0, expected_target_samples - sample_count) if expected_target_samples is not None else None,
        "sample_deficit_to_minimum_hz_by_lap_time": max(0, expected_minimum_samples - sample_count) if expected_minimum_samples is not None else None,
        "lap_classification": lap.get("lap_classification"),
        "proper": lap.get("eligible_for_report"),
        "coverage": lap.get("coverage"),
        "quality_status": quality_status,
        "preservation_status": quality_status,
        "channel_quality_status": channel_quality_status,
    }
    output.update(timing)
    return output


def build_quality_summary(
    lap_summaries: list[dict[str, Any]],
    driver_summaries: list[dict[str, Any]],
    raw_frame_summary: dict[str, Any] | None,
    target_hz: float,
    minimum_hz: float,
) -> dict[str, Any]:
    proper_laps = [lap for lap in lap_summaries if lap.get("proper")]
    proper_rates = [safe_numeric(lap.get("lap_time_sample_rate_hz") or lap.get("effective_sample_rate_hz")) for lap in proper_laps]
    proper_rates = [rate for rate in proper_rates if rate is not None]
    driver_rates = [safe_numeric(driver.get("effective_sample_rate_hz")) for driver in driver_summaries]
    driver_rates = [rate for rate in driver_rates if rate is not None]
    raw_rate = safe_numeric((raw_frame_summary or {}).get("effective_sample_rate_hz"))
    reasons = []

    if proper_rates:
        below_minimum = sum(1 for rate in proper_rates if rate < minimum_hz)
        status = "pass" if below_minimum == 0 else ("warn" if median(proper_rates) and median(proper_rates) >= minimum_hz else "fail")
        if below_minimum:
            reasons.append(f"{below_minimum} proper laps below {minimum_hz:g} Hz by captured row/sample cadence")
        else:
            reasons.append(f"all proper laps at or above {minimum_hz:g} Hz by captured row/sample cadence")
        return {
            "status": status,
            "basis": "proper_laps_preservation",
            "target_hz": target_hz,
            "minimum_hz": minimum_hz,
            "proper_lap_count": len(proper_rates),
            "proper_lap_rate_min_hz": round(min(proper_rates), 4),
            "proper_lap_rate_median_hz": round(median(proper_rates), 4) if proper_rates else None,
            "proper_lap_rate_max_hz": round(max(proper_rates), 4),
            "proper_laps_below_minimum_count": below_minimum,
            "driver_rate_min_hz": round(min(driver_rates), 4) if driver_rates else None,
            "driver_rate_median_hz": round(median(driver_rates), 4) if driver_rates else None,
            "raw_frame_rate_hz": raw_rate,
            "reasons": reasons,
        }

    if raw_rate is not None:
        status = "pass" if raw_rate >= minimum_hz else "fail"
        reasons.append(f"raw telemetry frame cadence is {raw_rate:g} Hz")
        return {
            "status": status,
            "basis": "raw_frames_preservation",
            "target_hz": target_hz,
            "minimum_hz": minimum_hz,
            "proper_lap_count": 0,
            "proper_lap_rate_min_hz": None,
            "proper_lap_rate_median_hz": None,
            "proper_lap_rate_max_hz": None,
            "proper_laps_below_minimum_count": None,
            "driver_rate_min_hz": round(min(driver_rates), 4) if driver_rates else None,
            "driver_rate_median_hz": round(median(driver_rates), 4) if driver_rates else None,
            "raw_frame_rate_hz": raw_rate,
            "reasons": reasons,
        }

    if driver_rates:
        below_minimum = sum(1 for rate in driver_rates if rate < minimum_hz)
        status = "pass" if below_minimum == 0 else "fail"
        reasons.append(f"{below_minimum} drivers below {minimum_hz:g} Hz by captured row/sample cadence" if below_minimum else f"all drivers at or above {minimum_hz:g} Hz by captured row/sample cadence")
        return {
            "status": status,
            "basis": "drivers_preservation",
            "target_hz": target_hz,
            "minimum_hz": minimum_hz,
            "proper_lap_count": 0,
            "proper_lap_rate_min_hz": None,
            "proper_lap_rate_median_hz": None,
            "proper_lap_rate_max_hz": None,
            "proper_laps_below_minimum_count": None,
            "driver_rate_min_hz": round(min(driver_rates), 4),
            "driver_rate_median_hz": round(median(driver_rates), 4),
            "raw_frame_rate_hz": raw_rate,
            "reasons": reasons,
        }

    return {
        "status": "unknown",
        "basis": "no_rate_data",
        "target_hz": target_hz,
        "minimum_hz": minimum_hz,
        "proper_lap_count": 0,
        "proper_lap_rate_min_hz": None,
        "proper_lap_rate_median_hz": None,
        "proper_lap_rate_max_hz": None,
        "proper_laps_below_minimum_count": None,
        "driver_rate_min_hz": None,
        "driver_rate_median_hz": None,
        "raw_frame_rate_hz": raw_rate,
        "reasons": ["no timestamped telemetry samples were available"],
    }


def build_channel_quality_summary(
    lap_summaries: list[dict[str, Any]],
    driver_summaries: list[dict[str, Any]],
    target_hz: float,
    minimum_hz: float,
) -> dict[str, Any]:
    proper_laps = [lap for lap in lap_summaries if lap.get("proper")]
    proper_weakest_rates = [safe_numeric(lap.get("weakest_channel_observed_rate_hz")) for lap in proper_laps]
    proper_weakest_rates = [rate for rate in proper_weakest_rates if rate is not None]
    proper_weakest_channels = [lap for lap in proper_laps if lap.get("weakest_channel_key")]
    driver_weakest_rates = [safe_numeric(driver.get("weakest_channel_observed_rate_hz")) for driver in driver_summaries]
    driver_weakest_rates = [rate for rate in driver_weakest_rates if rate is not None]
    reasons = []

    if proper_weakest_rates:
        below_minimum = sum(1 for rate in proper_weakest_rates if rate < minimum_hz)
        status = "pass" if below_minimum == 0 else "fail"
        reasons.append(
            f"{below_minimum} proper laps below {minimum_hz:g} Hz by weakest observed channel-change cadence"
            if below_minimum
            else f"all proper laps at or above {minimum_hz:g} Hz by weakest observed channel-change cadence"
        )
        reasons.append("channel-change cadence is secondary evidence; constant inputs can make a healthy channel look static")
        return {
            "status": status,
            "basis": "proper_laps_observed_channel_changes",
            "target_hz": target_hz,
            "minimum_hz": minimum_hz,
            "proper_lap_count": len(proper_weakest_rates),
            "proper_lap_weakest_rate_min_hz": round(min(proper_weakest_rates), 4),
            "proper_lap_weakest_rate_median_hz": round(median(proper_weakest_rates), 4),
            "proper_lap_weakest_rate_max_hz": round(max(proper_weakest_rates), 4),
            "proper_laps_below_minimum_count": below_minimum,
            "driver_weakest_channel_rate_min_hz": round(min(driver_weakest_rates), 4) if driver_weakest_rates else None,
            "driver_weakest_channel_rate_median_hz": round(median(driver_weakest_rates), 4) if driver_weakest_rates else None,
            "proper_lap_weakest_channels": [
                {
                    "lap_id": lap.get("lap_id"),
                    "driver_name": lap.get("driver_name"),
                    "lap_number": lap.get("lap_number"),
                    "weakest_channel_key": lap.get("weakest_channel_key"),
                    "weakest_channel_observed_rate_hz": lap.get("weakest_channel_observed_rate_hz"),
                }
                for lap in proper_weakest_channels
            ],
            "reasons": reasons,
        }

    if driver_weakest_rates:
        below_minimum = sum(1 for rate in driver_weakest_rates if rate < minimum_hz)
        status = "pass" if below_minimum == 0 else "fail"
        reasons.append(
            f"{below_minimum} drivers below {minimum_hz:g} Hz by weakest observed channel-change cadence"
            if below_minimum
            else f"all drivers at or above {minimum_hz:g} Hz by weakest observed channel-change cadence"
        )
        reasons.append("channel-change cadence is secondary evidence; constant inputs can make a healthy channel look static")
        return {
            "status": status,
            "basis": "drivers_observed_channel_changes",
            "target_hz": target_hz,
            "minimum_hz": minimum_hz,
            "proper_lap_count": 0,
            "proper_lap_weakest_rate_min_hz": None,
            "proper_lap_weakest_rate_median_hz": None,
            "proper_lap_weakest_rate_max_hz": None,
            "proper_laps_below_minimum_count": None,
            "driver_weakest_channel_rate_min_hz": round(min(driver_weakest_rates), 4),
            "driver_weakest_channel_rate_median_hz": round(median(driver_weakest_rates), 4),
            "proper_lap_weakest_channels": [],
            "reasons": reasons,
        }

    return {
        "status": "unknown",
        "basis": "no_measured_channel_changes",
        "target_hz": target_hz,
        "minimum_hz": minimum_hz,
        "proper_lap_count": 0,
        "proper_lap_weakest_rate_min_hz": None,
        "proper_lap_weakest_rate_median_hz": None,
        "proper_lap_weakest_rate_max_hz": None,
        "proper_laps_below_minimum_count": None,
        "driver_weakest_channel_rate_min_hz": None,
        "driver_weakest_channel_rate_median_hz": None,
        "proper_lap_weakest_channels": [],
        "reasons": ["no telemetry channel changed often enough to estimate observed channel-change cadence"],
    }


def load_samples_from_raw_telemetry(path: Path) -> list[dict[str, Any]]:
    samples: list[dict[str, Any]] = []
    for frame in load_compact_frames(path):
        timestamp = frame.get("t")
        update_counter = frame.get("u")
        for row in frame.get("v") or []:
            sample = sample_from_compact_vehicle(frame, row)
            sample["timestamp"] = timestamp
            sample["telemetry_update_counter"] = update_counter
            sample["driver_name"] = sample.get("vehicle_name") or sample.get("driver_id")
            samples.append(sample)
    return samples


def analyze_raw_session(
    raw_path: Path,
    session_id: str | None = None,
    target_hz: float = DEFAULT_TARGET_HZ,
    minimum_hz: float = DEFAULT_MINIMUM_HZ,
) -> dict[str, Any]:
    frames = list(load_compact_frames(raw_path))
    samples = samples_from_raw_frames(frames)
    if session_id is None:
        session_id = raw_path.parent.name
    driver_samples: dict[int, list[dict[str, Any]]] = {}
    for sample in samples:
        driver_id = sample.get("driver_id")
        if driver_id is None:
            continue
        driver_samples.setdefault(driver_id, []).append(sample)

    drivers = []
    for driver_id, driver_sample_list in driver_samples.items():
        driver_analysis = analyze_driver_samples(driver_sample_list, target_hz=target_hz, minimum_hz=minimum_hz)
        driver_analysis.update(
            {
                "driver_id": driver_id,
                "driver_name": driver_sample_list[0].get("driver_name"),
                "proper_lap_count": None,
                "excluded_lap_count": None,
            }
        )
        drivers.append(driver_analysis)

    frame_timestamps = [safe_numeric(frame.get("t")) for frame in frames if safe_numeric(frame.get("t")) is not None]
    frame_summary = summarize_timestamps(frame_timestamps, target_hz=target_hz, minimum_hz=minimum_hz)
    frame_count = len(frame_timestamps)
    read_durations = [safe_numeric(frame.get("r")) for frame in frames if safe_numeric(frame.get("r")) is not None]
    raw_update_counters = [int(frame["u"]) for frame in frames if isinstance(frame.get("u"), int)]
    update_repeats, update_gaps, update_missing = summarize_counter_gaps(raw_update_counters)
    raw_frame_summary = {
        "raw_frame_count": frame_count,
        "raw_vehicle_sample_count": len(samples),
        "torn_read_count": sum(1 for frame in frames if frame.get("x")),
        "read_duration_median_seconds": round(median(read_durations), 6) if read_durations else None,
        "read_duration_p95_seconds": round(p95(read_durations), 6) if read_durations else None,
        "read_duration_max_seconds": round(max(read_durations), 6) if read_durations else None,
        "telemetry_update_counter_repeat_count": update_repeats,
        "telemetry_update_counter_gap_count": update_gaps,
        "telemetry_update_counter_missing_count": update_missing,
    }
    raw_frame_summary.update(frame_summary)
    worker_status = load_worker_status(raw_path.parent)
    preservation_summary = build_quality_summary(
        lap_summaries=[],
        driver_summaries=drivers,
        raw_frame_summary=raw_frame_summary,
        target_hz=target_hz,
        minimum_hz=minimum_hz,
    )
    channel_quality_summary = build_channel_quality_summary(
        lap_summaries=[],
        driver_summaries=drivers,
        target_hz=target_hz,
        minimum_hz=minimum_hz,
    )
    return {
        "analysis_version": SESSION_ANALYSIS_VERSION,
        "session_id": session_id,
        "target_hz": target_hz,
        "minimum_hz": minimum_hz,
        "track": None,
        "session_type": None,
        "status": "raw telemetry only",
        "sample_count": len(samples),
        "raw_frame_count": frame_count,
        "sample_duration_seconds": frame_summary["sample_duration_seconds"],
        "effective_sample_rate_hz": frame_summary["effective_sample_rate_hz"],
        "proper_lap_count": None,
        "excluded_lap_count": None,
        "telemetry_sample_count": len(samples),
        "raw_frame_summary": raw_frame_summary,
        "worker_status": worker_status,
        "quality_summary": preservation_summary,
        "preservation_summary": preservation_summary,
        "channel_quality_summary": channel_quality_summary,
        "driver_summaries": drivers,
        "laps": [],
    }


def samples_from_raw_frames(frames: list[dict[str, Any]]) -> list[dict[str, Any]]:
    samples: list[dict[str, Any]] = []
    for frame in frames:
        timestamp = frame.get("t")
        update_counter = frame.get("u")
        for row in frame.get("v") or []:
            sample = sample_from_compact_vehicle(frame, row)
            sample["timestamp"] = timestamp
            sample["telemetry_update_counter"] = update_counter
            sample["driver_name"] = sample.get("vehicle_name") or sample.get("driver_id")
            samples.append(sample)
    return samples


def summarize_counter_gaps(values: list[int]) -> tuple[int, int, int]:
    repeats = 0
    gaps = 0
    missing = 0
    for previous, current in zip(values, values[1:]):
        delta = current - previous
        if delta == 0:
            repeats += 1
        elif delta > 1:
            gaps += 1
            missing += delta - 1
    return repeats, gaps, missing


def load_worker_status(directory: Path) -> dict[str, Any] | None:
    status_path = directory / WORKER_STATUS_FILENAME
    if not status_path.exists():
        return None
    try:
        return json.loads(status_path.read_text(encoding="utf-8"))
    except Exception as exc:
        return {"error": str(exc), "status_file": str(status_path)}


def discover_recording_directories(paths: list[Path]) -> list[Path]:
    directories: list[Path] = []
    for path in paths:
        if path.is_file():
            if path.name == TELEMETRY_SAMPLES_FILENAME:
                directories.append(path.parent)
                continue
            if path.name == RAW_TELEMETRY_FILENAME:
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
            if (path / RAW_TELEMETRY_FILENAME).exists():
                directories.append(path)
                continue
            found_child = False
            for child in sorted(path.iterdir()):
                if child.is_dir() and ((child / TELEMETRY_SAMPLES_FILENAME).exists() or (child / RAW_TELEMETRY_FILENAME).exists()):
                    directories.append(child)
                    found_child = True
            if not found_child:
                raise ValueError(f"Directory does not contain telemetry recordings: {path}")
    return directories


def format_text_report(analysis: dict[str, Any]) -> str:
    lines: list[str] = []
    quality = analysis.get("preservation_summary") or analysis.get("quality_summary") or {}
    channel_quality = analysis.get("channel_quality_summary") or {}
    lines.append(f"Session: {analysis.get('session_id')}")
    lines.append(f"Track: {analysis.get('track')}")
    lines.append(f"Session type: {analysis.get('session_type')}")
    lines.append(f"Status: {analysis.get('status')}")
    lines.append(f"Preservation: {quality.get('status')} basis={quality.get('basis')} target={analysis.get('target_hz')} Hz minimum={analysis.get('minimum_hz')} Hz")
    if quality.get("proper_lap_rate_median_hz") is not None:
        lines.append(f"Proper lap Hz: min={quality.get('proper_lap_rate_min_hz')} median={quality.get('proper_lap_rate_median_hz')} max={quality.get('proper_lap_rate_max_hz')} below_min={quality.get('proper_laps_below_minimum_count')}")
    if quality.get("raw_frame_rate_hz") is not None:
        lines.append(f"Raw frame Hz: {quality.get('raw_frame_rate_hz')}")
    for reason in quality.get("reasons") or []:
        lines.append(f"Preservation reason: {reason}")
    lines.append(f"Channel quality: {channel_quality.get('status')} basis={channel_quality.get('basis')}")
    if channel_quality.get("proper_lap_weakest_rate_median_hz") is not None:
        lines.append(f"Proper lap weakest measured channel Hz: min={channel_quality.get('proper_lap_weakest_rate_min_hz')} median={channel_quality.get('proper_lap_weakest_rate_median_hz')} max={channel_quality.get('proper_lap_weakest_rate_max_hz')}")
    if channel_quality.get("driver_weakest_channel_rate_median_hz") is not None:
        lines.append(f"Driver weakest measured channel Hz: min={channel_quality.get('driver_weakest_channel_rate_min_hz')} median={channel_quality.get('driver_weakest_channel_rate_median_hz')}")
    for reason in channel_quality.get("reasons") or []:
        lines.append(f"Channel quality note: {reason}")
    lines.append(f"Sample count: {analysis.get('sample_count')}")
    lines.append(f"Duration: {analysis.get('sample_duration_seconds')} s")
    lines.append(f"Effective sample rate: {analysis.get('effective_sample_rate_hz')} Hz")
    if analysis.get("raw_frame_summary"):
        raw = analysis["raw_frame_summary"]
        lines.append(f"Raw frames: {raw.get('raw_frame_count')} read p95={raw.get('read_duration_p95_seconds')} s torn={raw.get('torn_read_count')} missing_updates={raw.get('telemetry_update_counter_missing_count')}")
    if analysis.get("telemetry_import_stats"):
        stats = analysis["telemetry_import_stats"]
        lines.append(f"Import: raw_frames={stats.get('raw_frames')} raw_vehicle_samples={stats.get('raw_vehicle_samples')} imported={stats.get('imported_samples')} duplicates={stats.get('duplicate_samples')}")
    lines.append(f"Proper telemetry laps: {analysis.get('proper_lap_count')}")
    lines.append(f"Excluded telemetry laps: {analysis.get('excluded_lap_count')}")
    lines.append(f"Telemetry sample count: {analysis.get('telemetry_sample_count')}")
    lines.append("")
    for driver in analysis.get("driver_summaries", []):
        lines.append(f"Driver {driver.get('driver_name')} ({driver.get('driver_id')}):")
        lines.append(f"  Samples: {driver.get('sample_count')}")
        lines.append(f"  Effective sample rate: {driver.get('effective_sample_rate_hz')} Hz")
        if driver.get("weakest_channel_key"):
            lines.append(f"  Weakest measured channel: {driver.get('weakest_channel_key')} {driver.get('weakest_channel_observed_rate_hz')} Hz")
        lines.append(f"  Min interval: {driver.get('min_sample_interval_seconds')} s")
        lines.append(f"  Median interval: {driver.get('median_sample_interval_seconds')} s")
        lines.append(f"  P95 interval: {driver.get('p95_sample_interval_seconds')} s")
        lines.append(f"  Max interval: {driver.get('max_sample_interval_seconds')} s")
        lines.append(f"  Telemetry update counter repeats: {driver.get('telemetry_update_counter_repeat_count')}")
        lines.append(f"  Telemetry update counter gaps: {driver.get('telemetry_update_counter_gap_count')}")
        lines.append(f"  Telemetry update counter missing: {driver.get('telemetry_update_counter_missing_count')}")
        lines.append(f"  Update-counter rate: {driver.get('telemetry_update_counter_rate_hz')} Hz")
        lines.append(f"  Intervals at/above minimum: {driver.get('intervals_at_or_above_minimum_rate_share')}")
        lines.append(f"  Intervals over 5x target: {driver.get('intervals_over_5x_target_count')}")
        lines.append(f"  Lap-percent repeats: {driver.get('lap_percent_repeat_count')}")
        lines.append(f"  Gear-zero share: {driver.get('gear_zero_share')}")
        lines.append(f"  Channel presence fractions: {driver.get('channel_presence_fractions')}")
        lines.append(f"  Channel change fractions: {driver.get('channel_change_fractions')}")
        lines.append("")
    proper_laps = [lap for lap in analysis.get("laps", []) if lap.get("proper")]
    if proper_laps:
        lines.append("Proper lap effectiveness:")
        for lap in proper_laps:
            lines.append(
                f"  {lap.get('driver_name')} lap {lap.get('lap_number')}: "
                f"samples={lap.get('sample_count')} lap_time={lap.get('lap_time')}s "
                f"rate={lap.get('lap_time_sample_rate_hz')}Hz p95={lap.get('p95_sample_interval_seconds')}s "
                f"weakest={lap.get('weakest_channel_key')}:{lap.get('weakest_channel_observed_rate_hz')}Hz "
                f"effective_weakest={lap.get('effective_weakest_rate_hz')}Hz max_gap={lap.get('max_sample_interval_seconds')}s "
                f"preservation={lap.get('preservation_status')} channel={lap.get('channel_quality_status')}"
            )
        lines.append("")
    return "\n".join(lines)


def analyze_paths(
    paths: list[Path],
    target_hz: float = DEFAULT_TARGET_HZ,
    minimum_hz: float = DEFAULT_MINIMUM_HZ,
) -> dict[str, Any]:
    directories = discover_recording_directories(paths)
    sessions = []
    for directory in directories:
        sample_path = directory / TELEMETRY_SAMPLES_FILENAME
        raw_path = directory / RAW_TELEMETRY_FILENAME
        if sample_path.exists():
            sessions.append(analyze_session(sample_path, target_hz=target_hz, minimum_hz=minimum_hz))
        elif raw_path.exists():
            sessions.append(analyze_raw_session(raw_path, target_hz=target_hz, minimum_hz=minimum_hz))
        else:
            raise ValueError(f"Telemetry samples file not found in {directory}")
    return {"sessions": sessions}


def main() -> None:
    args = parse_args()
    analysis = analyze_paths([Path(path) for path in args.paths], target_hz=args.target_hz, minimum_hz=args.minimum_hz)
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
