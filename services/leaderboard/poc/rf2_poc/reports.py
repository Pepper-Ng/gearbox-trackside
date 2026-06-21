from __future__ import annotations

import hashlib
import re
import time
from typing import Any


Snapshot = dict[str, Any]
DEFAULT_AXIS = [float(value) for value in range(101)]
TICK_AXIS = [float(value) for value in range(0, 101, 10)]
MIN_PROPER_LAP_SAMPLES = 2
PROPER_LAP_START_MAX_PERCENT = 5.0
PROPER_LAP_END_MIN_PERCENT = 95.0
PROPER_LAP_MIN_SPAN_PERCENT = 90.0
SERIES_FIELDS = [
    "lap_percent",
    "time_seconds",
    "timestamp",
    "session_time",
    "speed_kph",
    "throttle_percent",
    "brake_percent",
    "steering_percent",
    "gear",
    "lateral_g",
    "longitudinal_g",
    "vertical_g",
    "g_magnitude",
]
CHANNELS = [
    {"key": "speed_kph", "label": "Speed", "unit": "km/h", "kind": "line"},
    {"key": "throttle_percent", "label": "Throttle", "unit": "%", "kind": "line"},
    {"key": "brake_percent", "label": "Brake", "unit": "%", "kind": "line"},
    {"key": "steering_percent", "label": "Steering", "unit": "%", "kind": "line"},
    {"key": "gear", "label": "Gear", "unit": "", "kind": "step"},
    {"key": "lateral_g", "label": "Lateral G", "unit": "g", "kind": "line"},
    {"key": "longitudinal_g", "label": "Longitudinal G", "unit": "g", "kind": "line"},
    {"key": "vertical_g", "label": "Vertical G", "unit": "g", "kind": "line"},
    {"key": "delta_time", "label": "Delta To Fastest Lap", "unit": "s", "kind": "line"},
]


def make_session_id(snapshot: Snapshot, key: str) -> str:
    session = snapshot.get("session", {})
    base = f"{session.get('track') or 'unknown'}-{session.get('session_type') or session.get('session_code') or 'session'}"
    slug = re.sub(r"[^a-zA-Z0-9]+", "-", base).strip("-").lower()[:48] or "session"
    digest = hashlib.sha1(key.encode("utf-8", errors="ignore")).hexdigest()[:10]
    return f"{slug}-{digest}"


def sample_from_driver(snapshot: Snapshot, driver: Snapshot) -> Snapshot | None:
    telemetry = driver.get("telemetry") or {}
    lap_distance = numeric(driver.get("lap_distance"))
    lap_percent = numeric(driver.get("track_position_percent"))
    if lap_distance is None or lap_percent is None or not telemetry:
        return None

    g_force = telemetry.get("g_force") or driver.get("local_acceleration_g") or {}
    elapsed_time = numeric(telemetry.get("elapsed_time"))
    lap_start_time = numeric(telemetry.get("lap_start_time")) or numeric(driver.get("lap_start_time"))
    current_lap_time = numeric(driver.get("current_lap_time"))
    time_from_lap_start = None
    if elapsed_time is not None and lap_start_time is not None and elapsed_time >= lap_start_time:
        time_from_lap_start = round(elapsed_time - lap_start_time, 4)
    elif current_lap_time is not None:
        time_from_lap_start = current_lap_time
    lap_number = current_lap_number(driver, telemetry)

    return {
        "timestamp": snapshot.get("timestamp"),
        "session_time": (snapshot.get("session") or {}).get("current_time"),
        "session_track": (snapshot.get("session") or {}).get("track"),
        "session_type": (snapshot.get("session") or {}).get("session_type"),
        "session_game_phase": (snapshot.get("session") or {}).get("game_phase"),
        "session_game_phase_name": (snapshot.get("session") or {}).get("game_phase_name"),
        "telemetry_update_counter": (snapshot.get("telemetry") or {}).get("update_counter"),
        "driver_id": driver.get("id"),
        "driver_name": driver.get("driver_name"),
        "vehicle_name": driver.get("vehicle_name"),
        "lap_number": lap_number,
        "lap_distance": lap_distance,
        "lap_percent": lap_percent,
        "time_seconds": time_from_lap_start,
        "speed_kph": numeric(telemetry.get("speed_kph") or driver.get("speed_kph")),
        "throttle_percent": numeric(telemetry.get("throttle_percent")),
        "brake_percent": numeric(telemetry.get("brake_percent")),
        "gear": numeric(telemetry.get("gear")),
        "gear_label": telemetry.get("gear_label"),
        "steering_percent": numeric(telemetry.get("steering_percent")),
        "lateral_g": numeric(g_force.get("lateral") if isinstance(g_force, dict) else None) or numeric(g_force.get("x") if isinstance(g_force, dict) else None),
        "longitudinal_g": numeric(g_force.get("longitudinal") if isinstance(g_force, dict) else None) or numeric(g_force.get("z") if isinstance(g_force, dict) else None),
        "vertical_g": numeric(g_force.get("vertical") if isinstance(g_force, dict) else None) or numeric(g_force.get("y") if isinstance(g_force, dict) else None),
        "g_magnitude": numeric(g_force.get("magnitude") if isinstance(g_force, dict) else None),
        "count_lap_flag": driver.get("count_lap_flag"),
        "count_lap_flag_name": driver.get("count_lap_flag_name"),
        "in_pits": driver.get("in_pits"),
        "pit_state": driver.get("pit_state"),
        "pit_state_name": driver.get("pit_state_name"),
        "finish_status": driver.get("finish_status"),
        "finish_status_name": driver.get("finish_status_name"),
    }


def inferred_current_lap(driver: Snapshot) -> int | None:
    laps = driver.get("laps")
    if isinstance(laps, int):
        return laps + 1
    return None


def current_lap_number(driver: Snapshot, telemetry: Snapshot) -> int | None:
    candidates = []
    telemetry_lap = telemetry.get("lap_number")
    if isinstance(telemetry_lap, int) and telemetry_lap > 0:
        candidates.append(telemetry_lap)
    inferred_lap = inferred_current_lap(driver)
    if inferred_lap is not None and inferred_lap > 0:
        candidates.append(inferred_lap)
    if not candidates:
        return None
    return max(candidates)


def append_sample_to_record(record: Snapshot, sample: Snapshot) -> bool:
    driver_id = str(sample.get("driver_id"))
    if not driver_id or driver_id == "None":
        return False

    drivers = record.setdefault("drivers", {})
    driver_record = drivers.setdefault(
        driver_id,
        {
            "id": sample.get("driver_id"),
            "driver_name": sample.get("driver_name"),
            "vehicle_name": sample.get("vehicle_name"),
            "laps": 0,
            "lap_history": [],
            "telemetry_laps": {},
            "last_sample_signature": None,
            "last_lap_number": None,
            "last_lap_key": None,
            "lap_instance": 0,
        },
    )

    signature = (
        sample.get("telemetry_update_counter"),
        sample.get("lap_number"),
        sample.get("lap_distance"),
        sample.get("speed_kph"),
        sample.get("throttle_percent"),
        sample.get("brake_percent"),
        sample.get("steering_percent"),
        sample.get("gear"),
    )
    if signature == driver_record.get("last_sample_signature"):
        return False
    driver_record["last_sample_signature"] = signature

    lap_number = sample.get("lap_number")
    if lap_number is None:
        return False
    lap_instance = int(driver_record.get("lap_instance") or 0)
    previous_lap_number = driver_record.get("last_lap_number")
    previous_lap_key = driver_record.get("last_lap_key")
    if previous_lap_number is not None and previous_lap_number != lap_number:
        previous_lap = driver_record.get("telemetry_laps", {}).get(str(previous_lap_key or previous_lap_number))
        if previous_lap:
            previous_lap["completed"] = True
        lap_instance = 0
        driver_record["lap_instance"] = lap_instance
    driver_record["last_lap_number"] = lap_number

    lap_key = f"{lap_number}:{lap_instance}"
    lap = driver_record.setdefault("telemetry_laps", {}).setdefault(
        lap_key,
        {"lap_number": lap_number + lap_instance, "samples": [], "completed": False, "lap_time": None},
    )
    samples = lap.setdefault("samples", [])
    if samples and sample.get("lap_percent") is not None and samples[-1].get("lap_percent") is not None:
        if sample["lap_percent"] + 8.0 < samples[-1]["lap_percent"]:
            lap["completed"] = True
            lap_instance += 1
            driver_record["lap_instance"] = lap_instance
            lap_key = f"{lap_number}:{lap_instance}"
            lap = driver_record.setdefault("telemetry_laps", {}).setdefault(
                lap_key,
                {"lap_number": lap_number + lap_instance, "samples": [], "completed": False, "lap_time": None},
            )
            samples = lap.setdefault("samples", [])
    driver_record["last_lap_key"] = lap_key
    samples.append(sample)
    return True


def assign_lap_times_from_history(driver_record: Snapshot) -> None:
    lap_times = {
        str(lap.get("lap_number")): lap.get("lap_time")
        for lap in driver_record.get("lap_history", [])
        if lap.get("lap_number") is not None and lap.get("lap_time") is not None
    }
    for lap in driver_record.get("telemetry_laps", {}).values():
        lap_history_key = str(lap.get("lap_number"))
        if lap.get("lap_time") is None and lap_history_key in lap_times:
            lap["lap_time"] = lap_times[lap_history_key]
            lap["completed"] = True
        elif lap.get("lap_time") is None:
            samples = lap.get("samples") or []
            start = first_numeric(samples, "time_seconds")
            end = last_numeric(samples, "time_seconds")
            if start is not None and end is not None and end > start:
                lap["lap_time"] = round(end - start, 3)


def build_report(record: Snapshot) -> Snapshot | None:
    if not record:
        return None
    started_at = time.perf_counter()

    all_laps = []
    for driver in record.get("drivers", {}).values():
        assign_lap_times_from_history(driver)
        all_laps.extend(report_laps_for_driver(driver))

    proper_laps = [lap for lap in all_laps if lap.get("eligible_for_report")]
    mark_fastest_laps(all_laps, proper_laps)

    if not all_laps:
        return {
            "session_id": record.get("id"),
            "track": record.get("track"),
            "session_type": record.get("session_type"),
            "status": "no telemetry laps",
            "axis": DEFAULT_AXIS,
            "axis_strategy": "raw samples",
            "axis_sample_count": len(DEFAULT_AXIS),
            "build_seconds": round(time.perf_counter() - started_at, 4),
            "channels": CHANNELS,
            "laps": [],
            "all_laps": [],
            "proper_lap_count": 0,
            "excluded_lap_count": 0,
            "telemetry_sample_count": int(record.get("telemetry_sample_count") or 0),
            "reference_lap": None,
        }

    reference = min(proper_laps, key=lambda lap: lap.get("lap_time") or 999999.0) if proper_laps else None
    if reference is not None:
        reference["is_reference"] = True
    sorted_proper_laps = sorted(proper_laps, key=lambda lap: lap.get("lap_time") or 999999.0)
    sorted_all_laps = sorted(all_laps, key=lap_sort_key)
    status = "ready" if proper_laps else "no proper telemetry laps"

    return {
        "session_id": record.get("id"),
        "track": record.get("track"),
        "session_type": record.get("session_type"),
        "started_at": record.get("started_at"),
        "finalized": record.get("finalized"),
        "completion_reason": record.get("completion_reason"),
        "status": status,
        "axis": [],
        "axis_strategy": "raw recorded samples at collector frequency",
        "axis_sample_count": 0,
        "build_seconds": round(time.perf_counter() - started_at, 4),
        "channels": CHANNELS,
        "proper_lap_count": len(proper_laps),
        "excluded_lap_count": len(all_laps) - len(proper_laps),
        "telemetry_sample_count": int(record.get("telemetry_sample_count") or 0),
        "reference_lap": {
            "driver_id": reference.get("driver_id"),
            "driver_name": reference.get("driver_name"),
            "lap_number": reference.get("lap_number"),
            "lap_time": reference.get("lap_time"),
        } if reference is not None else None,
        "laps": sorted_proper_laps,
        "all_laps": sorted_all_laps,
    }


def placeholder_report(session_id: str, status: str, error: str | None = None) -> Snapshot:
    report = {
        "session_id": session_id,
        "track": None,
        "session_type": None,
        "status": status,
        "axis": DEFAULT_AXIS,
        "axis_strategy": "placeholder",
        "axis_sample_count": len(DEFAULT_AXIS),
        "channels": CHANNELS,
        "laps": [],
        "all_laps": [],
        "proper_lap_count": 0,
        "excluded_lap_count": 0,
        "telemetry_sample_count": 0,
        "reference_lap": None,
    }
    if error:
        report["error"] = error
    return report


def report_laps_for_driver(driver: Snapshot) -> list[Snapshot]:
    laps = []
    for lap_key, lap in driver.get("telemetry_laps", {}).items():
        samples = [sample for sample in lap.get("samples", []) if sample.get("lap_percent") is not None]
        classification = classify_lap(lap, samples)
        laps.append(
            {
                "lap_id": f"{driver.get('id')}:{lap_key}",
                "driver_id": driver.get("id"),
                "driver_name": driver.get("driver_name"),
                "vehicle_name": driver.get("vehicle_name"),
                "lap_number": lap.get("lap_number"),
                "lap_time": lap.get("lap_time"),
                "sample_count": len(samples),
                "coverage": lap_coverage(samples),
                "raw_lap_percents": raw_lap_percents(samples),
                "lap_classification": classification["classification"],
                "classification_reasons": classification["reasons"],
                "eligible_for_report": classification["eligible_for_report"],
                "is_reference": False,
                "is_fastest_personal": False,
                "is_fastest_overall": False,
                "series": raw_series_from_samples(samples),
            }
        )
    return laps


def classify_lap(lap: Snapshot, samples: list[Snapshot]) -> Snapshot:
    sample_count = len(samples)
    coverage = lap_coverage(samples)
    min_percent = numeric(coverage.get("min_percent"))
    max_percent = numeric(coverage.get("max_percent"))
    span = (max_percent - min_percent) if min_percent is not None and max_percent is not None else None
    lap_time = numeric(lap.get("lap_time"))
    reasons = []
    count_flags = int_values(samples, "count_lap_flag")
    game_phases = int_values(samples, "session_game_phase")
    pit_type = lap_pit_type(samples)

    if not samples:
        return lap_classification("partial", False, ["no telemetry samples"])
    if any(phase in {1, 2, 3, 4} for phase in game_phases):
        return lap_classification("formation", False, ["session was not green flag"])
    if count_flags and 2 not in count_flags and lap_time is None:
        return lap_classification("formation", False, ["lap was not timed by rFactor 2"])
    if pit_type is not None:
        return lap_classification(pit_type, False, ["pit state/in-pits was observed"])
    if lap_time is None:
        reasons.append("no counted lap time was observed")
    if sample_count < MIN_PROPER_LAP_SAMPLES:
        reasons.append(f"only {sample_count} telemetry samples")
    if min_percent is None or max_percent is None or span is None:
        reasons.append("lap-percent coverage is unavailable")
    else:
        if min_percent > PROPER_LAP_START_MAX_PERCENT:
            reasons.append(f"missing lap start before {min_percent:.1f}%")
        if max_percent < PROPER_LAP_END_MIN_PERCENT:
            reasons.append(f"missing lap end after {max_percent:.1f}%")
        if span < PROPER_LAP_MIN_SPAN_PERCENT:
            reasons.append(f"only {span:.1f}% lap coverage")
    if count_flags and 2 not in count_flags:
        reasons.append("lap was not marked count lap and time")
    if reasons:
        return lap_classification("partial", False, reasons)
    return lap_classification("proper", True, [])


def lap_classification(name: str, eligible: bool, reasons: list[str]) -> Snapshot:
    return {"classification": name, "eligible_for_report": eligible, "reasons": reasons}


def lap_pit_type(samples: list[Snapshot]) -> str | None:
    if not any(sample_in_pit(sample) for sample in samples):
        return None
    edge_count = max(1, min(10, len(samples) // 10 or 1))
    if any(sample_in_pit(sample) for sample in samples[:edge_count]):
        return "outlap"
    if any(sample_in_pit(sample) for sample in samples[-edge_count:]):
        return "inlap"
    return "inlap"


def sample_in_pit(sample: Snapshot) -> bool:
    pit_state = sample.get("pit_state")
    return bool(sample.get("in_pits")) or pit_state in (2, 3, 4)


def int_values(samples: list[Snapshot], key: str) -> set[int]:
    values = set()
    for sample in samples:
        value = sample.get(key)
        if isinstance(value, bool):
            continue
        if isinstance(value, int):
            values.add(value)
    return values


def mark_fastest_laps(all_laps: list[Snapshot], proper_laps: list[Snapshot]) -> None:
    if not proper_laps:
        return
    overall = min(proper_laps, key=lambda lap: lap.get("lap_time") or 999999.0)
    overall["is_fastest_overall"] = True
    by_driver: dict[str, list[Snapshot]] = {}
    for lap in proper_laps:
        by_driver.setdefault(str(lap.get("driver_id")), []).append(lap)
    for laps in by_driver.values():
        fastest = min(laps, key=lambda lap: lap.get("lap_time") or 999999.0)
        fastest["is_fastest_personal"] = True


def lap_sort_key(lap: Snapshot) -> tuple[str, float, float]:
    return (
        str(lap.get("driver_name") or lap.get("driver_id") or ""),
        numeric(lap.get("lap_number")) or 0.0,
        numeric(lap.get("lap_time")) or 999999.0,
    )


def build_adaptive_axis(laps: list[Snapshot]) -> list[float]:
    axis_values = set(TICK_AXIS)
    for lap in laps:
        for sample in lap.get("samples") or []:
            lap_percent = numeric(sample.get("lap_percent"))
            if lap_percent is None:
                continue
            if 0.0 <= lap_percent <= 100.0:
                axis_values.add(round(lap_percent, 4))
    if len(axis_values) < 2:
        return DEFAULT_AXIS
    return sorted(axis_values)


def raw_lap_percents(samples: list[Snapshot]) -> list[float]:
    values = []
    for sample in samples:
        lap_percent = numeric(sample.get("lap_percent"))
        if lap_percent is not None:
            values.append(round(lap_percent, 4))
    return values


def raw_series_from_samples(samples: list[Snapshot]) -> Snapshot:
    output = {}
    for field in SERIES_FIELDS:
        output[field] = [numeric(sample.get(field)) for sample in samples]
    return output


def resample_lap(lap: Snapshot, axis: list[float]) -> Snapshot:
    samples = sorted(lap.get("samples") or [], key=lambda sample: sample.get("lap_percent") or 0.0)
    output = {}
    for field in SERIES_FIELDS:
        values = resample_field(samples, axis, field, step=field == "gear")
        if field == "time_seconds" and all(value is None for value in values):
            lap_time = numeric(lap.get("lap_time"))
            values = [round(point / 100.0 * lap_time, 3) if lap_time is not None else None for point in axis]
        output[field] = values
    return output


def resample_field(samples: list[Snapshot], axis: list[float], field: str, step: bool = False) -> list[float | None]:
    points = [(numeric(sample.get("lap_percent")), numeric(sample.get(field))) for sample in samples]
    points = [(x, y) for x, y in points if x is not None and y is not None]
    if not points:
        return [None for _ in axis]
    points = dedupe_points(points)
    values = []
    point_index = 1
    for target in axis:
        if target <= points[0][0]:
            values.append(points[0][1])
            continue
        if target >= points[-1][0]:
            values.append(points[-1][1])
            continue
        while point_index < len(points) and points[point_index][0] < target:
            point_index += 1
        left_x, left_y = points[point_index - 1]
        right_x, right_y = points[point_index]
        if step or right_x == left_x:
            values.append(left_y)
        else:
            ratio = (target - left_x) / (right_x - left_x)
            values.append(round(left_y + ratio * (right_y - left_y), 4))
    return values


def dedupe_points(points: list[tuple[float, float]]) -> list[tuple[float, float]]:
    deduped: list[tuple[float, float]] = []
    for x, y in points:
        if deduped and deduped[-1][0] == x:
            deduped[-1] = (x, y)
        else:
            deduped.append((x, y))
    return deduped


def delta_series(selected: list[float | None] | None, reference: list[float | None] | None) -> list[float | None]:
    if not selected or not reference:
        return []
    values = []
    for left, right in zip(selected, reference):
        if left is None or right is None:
            values.append(None)
        else:
            values.append(round(left - right, 4))
    return values


def lap_coverage(samples: list[Snapshot]) -> Snapshot:
    percents = [value for sample in samples if (value := numeric(sample.get("lap_percent"))) is not None]
    if not percents:
        return {"min_percent": None, "max_percent": None}
    return {"min_percent": round(min(percents), 2), "max_percent": round(max(percents), 2)}


def first_numeric(items: list[Snapshot], key: str) -> float | None:
    for item in items:
        value = numeric(item.get(key))
        if value is not None:
            return value
    return None


def last_numeric(items: list[Snapshot], key: str) -> float | None:
    for item in reversed(items):
        value = numeric(item.get(key))
        if value is not None:
            return value
    return None


def numeric(value: Any) -> float | None:
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    return None
