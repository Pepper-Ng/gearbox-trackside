from __future__ import annotations

import hashlib
import re
from typing import Any


Snapshot = dict[str, Any]
AXIS = [float(value) for value in range(101)]
SERIES_FIELDS = [
    "time_seconds",
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

    return {
        "timestamp": snapshot.get("timestamp"),
        "session_time": (snapshot.get("session") or {}).get("current_time"),
        "telemetry_update_counter": (snapshot.get("telemetry") or {}).get("update_counter"),
        "driver_id": driver.get("id"),
        "driver_name": driver.get("driver_name"),
        "vehicle_name": driver.get("vehicle_name"),
        "lap_number": telemetry.get("lap_number") or inferred_current_lap(driver),
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
    }


def inferred_current_lap(driver: Snapshot) -> int | None:
    laps = driver.get("laps")
    if isinstance(laps, int):
        return laps + 1
    return None


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
    lap_key = str(lap_number)
    previous_lap_number = driver_record.get("last_lap_number")
    if previous_lap_number is not None and previous_lap_number != lap_number:
        previous_lap = driver_record.get("telemetry_laps", {}).get(str(previous_lap_number))
        if previous_lap:
            previous_lap["completed"] = True
    driver_record["last_lap_number"] = lap_number

    lap = driver_record.setdefault("telemetry_laps", {}).setdefault(
        lap_key,
        {"lap_number": lap_number, "samples": [], "completed": False, "lap_time": None},
    )
    samples = lap.setdefault("samples", [])
    if samples and sample.get("lap_percent") is not None and samples[-1].get("lap_percent") is not None:
        if sample["lap_percent"] + 8.0 < samples[-1]["lap_percent"]:
            lap["completed"] = True
    samples.append(sample)
    return True


def assign_lap_times_from_history(driver_record: Snapshot) -> None:
    lap_times = {
        str(lap.get("lap_number")): lap.get("lap_time")
        for lap in driver_record.get("lap_history", [])
        if lap.get("lap_number") is not None and lap.get("lap_time") is not None
    }
    for lap_key, lap in driver_record.get("telemetry_laps", {}).items():
        if lap.get("lap_time") is None and lap_key in lap_times:
            lap["lap_time"] = lap_times[lap_key]
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

    best_laps = []
    for driver in record.get("drivers", {}).values():
        assign_lap_times_from_history(driver)
        best_lap = best_lap_for_driver(driver)
        if best_lap:
            best_laps.append(best_lap)

    if not best_laps:
        return {
            "session_id": record.get("id"),
            "track": record.get("track"),
            "session_type": record.get("session_type"),
            "status": "no completed telemetry laps",
            "axis": AXIS,
            "channels": CHANNELS,
            "laps": [],
            "reference_lap": None,
        }

    reference = min(best_laps, key=lambda lap: lap.get("lap_time") or 999999.0)
    reference_series = resample_lap(reference, AXIS)
    report_laps = []
    for lap in best_laps:
        series = resample_lap(lap, AXIS)
        series["delta_time"] = delta_series(series.get("time_seconds"), reference_series.get("time_seconds"))
        report_laps.append(
            {
                "driver_id": lap.get("driver_id"),
                "driver_name": lap.get("driver_name"),
                "vehicle_name": lap.get("vehicle_name"),
                "lap_number": lap.get("lap_number"),
                "lap_time": lap.get("lap_time"),
                "sample_count": len(lap.get("samples") or []),
                "coverage": lap_coverage(lap.get("samples") or []),
                "is_reference": lap is reference,
                "series": series,
            }
        )

    return {
        "session_id": record.get("id"),
        "track": record.get("track"),
        "session_type": record.get("session_type"),
        "started_at": record.get("started_at"),
        "finalized": record.get("finalized"),
        "completion_reason": record.get("completion_reason"),
        "status": "ready",
        "axis": AXIS,
        "channels": CHANNELS,
        "reference_lap": {
            "driver_id": reference.get("driver_id"),
            "driver_name": reference.get("driver_name"),
            "lap_number": reference.get("lap_number"),
            "lap_time": reference.get("lap_time"),
        },
        "laps": sorted(report_laps, key=lambda lap: lap.get("lap_time") or 999999.0),
    }


def best_lap_for_driver(driver: Snapshot) -> Snapshot | None:
    candidates = []
    for lap in driver.get("telemetry_laps", {}).values():
        samples = [sample for sample in lap.get("samples", []) if sample.get("lap_percent") is not None]
        if len(samples) < 2:
            continue
        lap_time = lap.get("lap_time")
        if lap_time is None:
            continue
        candidates.append(
            {
                "driver_id": driver.get("id"),
                "driver_name": driver.get("driver_name"),
                "vehicle_name": driver.get("vehicle_name"),
                "lap_number": lap.get("lap_number"),
                "lap_time": lap_time,
                "samples": samples,
            }
        )
    if not candidates:
        return None
    return min(candidates, key=lambda lap: lap.get("lap_time") or 999999.0)


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
    for target in axis:
        if target <= points[0][0]:
            values.append(points[0][1])
            continue
        if target >= points[-1][0]:
            values.append(points[-1][1])
            continue
        for index in range(1, len(points)):
            left_x, left_y = points[index - 1]
            right_x, right_y = points[index]
            if left_x <= target <= right_x:
                if step or right_x == left_x:
                    values.append(left_y)
                else:
                    ratio = (target - left_x) / (right_x - left_x)
                    values.append(round(left_y + ratio * (right_y - left_y), 4))
                break
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
