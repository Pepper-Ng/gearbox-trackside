from __future__ import annotations

import copy
import json
import time
from pathlib import Path
from typing import Any, Protocol

from .rf2_shared_memory import SharedMemoryScoringReader, SharedMemoryUnavailable


Snapshot = dict[str, Any]


class ScoringSource(Protocol):
    def read(self) -> Snapshot:
        """Return a normalized live-session snapshot."""

    def history(self) -> Snapshot:
        """Return in-memory session history collected by the PoC."""


class MockScoringSource:
    def __init__(self, fixture_path: Path):
        self.fixture_path = fixture_path
        self._base_snapshot = json.loads(fixture_path.read_text(encoding="utf-8"))
        self._started_at = time.monotonic()
        self._updates = 0

    def read(self) -> Snapshot:
        self._updates += 1
        elapsed = time.monotonic() - self._started_at
        snapshot = copy.deepcopy(self._base_snapshot)
        snapshot["source"] = "mock"
        snapshot["status"] = "fixture replay"
        snapshot["timestamp"] = time.time()
        snapshot["update_counter"] = self._updates
        snapshot["decode_offset"] = None
        snapshot.setdefault(
            "telemetry",
            {
                "status": "fixture replay",
                "memory_map": None,
                "decode_offset": None,
                "scope": "fixture",
                "vehicle_count": sum(1 for driver in snapshot.get("drivers", []) if driver.get("telemetry")),
                "joined_vehicle_count": sum(1 for driver in snapshot.get("drivers", []) if driver.get("telemetry")),
                "vehicles": [
                    driver["telemetry"]
                    for driver in snapshot.get("drivers", [])
                    if driver.get("telemetry")
                ],
            },
        )

        session = snapshot.setdefault("session", {})
        session["raw_vehicle_count"] = len(snapshot.get("drivers", []))
        session["vehicle_count"] = len(snapshot.get("drivers", []))
        session["current_time"] = round(float(session.get("current_time") or 0.0) + elapsed, 3)

        lap_distance = float(session.get("lap_distance") or 5300.0)
        for index, driver in enumerate(snapshot.get("drivers", [])):
            driver["current_lap_time"] = round(
                float(driver.get("current_lap_time") or 0.0) + elapsed,
                3,
            )
            distance = float(driver.get("lap_distance") or 0.0) + elapsed * (80 + index * 9)
            if lap_distance > 0:
                completed_extra_laps = int(distance // lap_distance)
                driver["laps"] = int(driver.get("laps") or 0) + completed_extra_laps
                distance = distance % lap_distance
            driver["lap_distance"] = round(distance, 1)
            driver["track_position_percent"] = round(distance / lap_distance * 100.0, 2) if lap_distance > 0 else None

        return snapshot

    def history(self) -> Snapshot:
        return {"current_session": None, "completed_sessions": [], "completed_session_count": 0}


class SharedMemoryScoringSource:
    def __init__(
        self,
        map_name: str | None = None,
        pid: int | None = None,
        telemetry_map_name: str | None = None,
    ):
        self._reader = SharedMemoryScoringReader(
            map_name=map_name,
            pid=pid,
            telemetry_map_name=telemetry_map_name,
        )

    def read(self) -> Snapshot:
        return self._reader.read_snapshot()

    def history(self) -> Snapshot:
        return {"current_session": None, "completed_sessions": [], "completed_session_count": 0}


class AutoScoringSource:
    def __init__(
        self,
        fixture_path: Path,
        map_name: str | None = None,
        pid: int | None = None,
        telemetry_map_name: str | None = None,
    ):
        self._fallback = MockScoringSource(fixture_path)
        try:
            self._live: SharedMemoryScoringSource | None = SharedMemoryScoringSource(
                map_name=map_name,
                pid=pid,
                telemetry_map_name=telemetry_map_name,
            )
            self._live.read()
        except SharedMemoryUnavailable:
            self._live = None

    def read(self) -> Snapshot:
        if self._live is not None:
            try:
                return self._live.read()
            except SharedMemoryUnavailable:
                self._live = None

        snapshot = self._fallback.read()
        snapshot["status"] = "shared memory unavailable; using fixture replay"
        return snapshot

    def history(self) -> Snapshot:
        if self._live is not None:
            return self._live.history()
        return self._fallback.history()


class RecordingScoringSource:
    def __init__(self, source: ScoringSource):
        self._source = source
        self._recorder = SessionRecorder()

    def read(self) -> Snapshot:
        snapshot = self._source.read()
        self._recorder.record(snapshot)
        history = self._recorder.history()
        enrich_snapshot(snapshot, history)
        return snapshot

    def history(self) -> Snapshot:
        return self._recorder.history()


class SessionRecorder:
    def __init__(self) -> None:
        self._current: Snapshot | None = None
        self._completed: list[Snapshot] = []

    def record(self, snapshot: Snapshot) -> None:
        key = session_key(snapshot)
        if self._current is None or self._current.get("key") != key:
            if self._current is not None and self._current.get("drivers"):
                self._finalize_current("session changed")
            self._current = new_session_record(snapshot, key)

        update_session_record(self._current, snapshot)
        if is_session_complete(snapshot) and not self._current.get("finalized"):
            self._finalize_current("game phase/session finish")

    def history(self) -> Snapshot:
        return {
            "current_session": public_session_record(self._current) if self._current else None,
            "completed_sessions": [public_session_record(record) for record in self._completed],
            "completed_session_count": len(self._completed),
        }

    def _finalize_current(self, reason: str) -> None:
        if self._current is None or self._current.get("finalized"):
            return
        self._current["finalized"] = True
        self._current["completion_reason"] = reason
        drivers = list(self._current.get("drivers", {}).values())
        self._current["finishing_order"] = [
            {
                "place": driver.get("last_place"),
                "driver_id": driver.get("id"),
                "driver_name": driver.get("driver_name"),
                "laps": driver.get("laps"),
                "best_lap_time": driver.get("best_lap_time"),
                "finish_status": driver.get("finish_status_name"),
            }
            for driver in sorted(drivers, key=lambda value: value.get("last_place") or 999)
        ]
        self._completed.insert(0, copy.deepcopy(self._current))
        self._completed = self._completed[:20]


def enrich_snapshot(snapshot: Snapshot, history: Snapshot) -> None:
    snapshot["highlights"] = build_highlights(snapshot)
    snapshot["history"] = history
    snapshot["field_coverage"] = build_field_coverage(snapshot, history)


def build_highlights(snapshot: Snapshot) -> Snapshot:
    drivers = snapshot.get("drivers", [])
    return {
        "fastest_lap": best_driver_metric(drivers, "best_lap_time"),
        "fastest_sector_1": best_driver_metric(drivers, "best_sector_1"),
        "fastest_sector_2_split": best_driver_metric(drivers, "best_sector_2_split"),
        "fastest_best_lap_sector_3": best_driver_metric(drivers, "best_lap_sector_3"),
        "highest_speed": best_driver_metric(drivers, "speed_kph", reverse=True),
    }


def build_field_coverage(snapshot: Snapshot, history: Snapshot) -> list[Snapshot]:
    drivers = snapshot.get("drivers", [])
    telemetry = snapshot.get("telemetry", {})
    return [
        coverage_item("session.state", "Session state", "scoring", bool(snapshot.get("session"))),
        coverage_item("session.weather", "Weather and temperatures", "scoring", any_driver_or_session(snapshot, ["ambient_temp", "track_temp", "raining"])),
        coverage_item("drivers.current", "Current driver rows", "scoring", bool(drivers), f"{len(drivers)} scored rows"),
        coverage_item("drivers.fastest_lap", "Fastest lap", "scoring", metric_available(drivers, "best_lap_time")),
        coverage_item("drivers.sectors", "Sector timing", "scoring", metric_available(drivers, "best_sector_1") or metric_available(drivers, "last_sector_1")),
        coverage_item("drivers.gaps", "Race gaps", "scoring", metric_available(drivers, "time_behind_leader") or metric_available(drivers, "laps_behind_leader")),
        coverage_item("drivers.finish", "Finish status/order", "scoring", metric_available(drivers, "finish_status")),
        coverage_item("track.distance", "Track position distance/percent", "scoring", metric_available(drivers, "lap_distance") or metric_available(drivers, "track_position_percent")),
        coverage_item("track.coordinates", "World coordinates", "scoring/telemetry", nested_metric_available(drivers, "position")),
        coverage_item("telemetry.map", "Telemetry memory map", "telemetry", telemetry.get("status") == "connected", telemetry.get("scope") or telemetry.get("status")),
        coverage_item("telemetry.all_cars", "All-car telemetry scope", "telemetry", telemetry.get("scope") == "all scoring vehicles", telemetry.get("scope")),
        coverage_item("telemetry.throttle", "Throttle position", "telemetry", telemetry_metric_available(drivers, "throttle_percent")),
        coverage_item("telemetry.brake", "Brake position", "telemetry", telemetry_metric_available(drivers, "brake_percent")),
        coverage_item("telemetry.steering", "Steering wheel position", "telemetry", telemetry_metric_available(drivers, "steering_percent")),
        coverage_item("telemetry.gear", "Selected gear", "telemetry", telemetry_metric_available(drivers, "gear")),
        coverage_item("telemetry.g_force", "G-forces", "telemetry", telemetry_metric_available(drivers, "g_force")),
        coverage_item("history.live_laps", "Observed lap/sector history", "recorder", bool((history.get("current_session") or {}).get("drivers"))),
        coverage_item("history.completed", "Completed sessions", "recorder", int(history.get("completed_session_count") or 0) > 0, f"{history.get('completed_session_count', 0)} completed"),
    ]


def coverage_item(key: str, label: str, source: str, available: bool, detail: str | None = None) -> Snapshot:
    return {"key": key, "label": label, "source": source, "available": bool(available), "detail": detail}


def best_driver_metric(drivers: list[Snapshot], metric: str, reverse: bool = False) -> Snapshot | None:
    candidates = [driver for driver in drivers if isinstance(driver.get(metric), (int, float))]
    if not candidates:
        return None
    driver = sorted(candidates, key=lambda value: value[metric], reverse=reverse)[0]
    return {
        "driver_id": driver.get("id"),
        "driver_name": driver.get("driver_name"),
        "value": driver.get(metric),
        "metric": metric,
    }


def metric_available(drivers: list[Snapshot], metric: str) -> bool:
    return any(driver.get(metric) not in (None, "", []) for driver in drivers)


def nested_metric_available(drivers: list[Snapshot], metric: str) -> bool:
    return any(bool(driver.get(metric)) for driver in drivers)


def telemetry_metric_available(drivers: list[Snapshot], metric: str) -> bool:
    return any((driver.get("telemetry") or {}).get(metric) not in (None, "", []) for driver in drivers)


def any_driver_or_session(snapshot: Snapshot, metrics: list[str]) -> bool:
    session = snapshot.get("session", {})
    return any(session.get(metric) not in (None, "", []) for metric in metrics)


def session_key(snapshot: Snapshot) -> str:
    session = snapshot.get("session", {})
    track = session.get("track") or "unknown-track"
    session_code = session.get("session_code") or "unknown-session"
    start_time = session.get("start_time") or session.get("end_time") or "unknown-start"
    return f"{track}|{session_code}|{start_time}"


def new_session_record(snapshot: Snapshot, key: str) -> Snapshot:
    session = snapshot.get("session", {})
    return {
        "key": key,
        "track": session.get("track"),
        "session_type": session.get("session_type"),
        "session_code": session.get("session_code"),
        "started_at": snapshot.get("timestamp") or time.time(),
        "last_seen_at": snapshot.get("timestamp") or time.time(),
        "finalized": False,
        "completion_reason": None,
        "drivers": {},
        "fastest_lap": None,
        "fastest_sectors": {},
        "finishing_order": [],
    }


def update_session_record(record: Snapshot, snapshot: Snapshot) -> None:
    record["last_seen_at"] = snapshot.get("timestamp") or time.time()
    record["fastest_lap"] = best_driver_metric(snapshot.get("drivers", []), "best_lap_time")
    fastest_sectors = {}
    for key in ("best_sector_1", "best_sector_2_split", "best_lap_sector_3"):
        metric = best_driver_metric(snapshot.get("drivers", []), key)
        if metric:
            fastest_sectors[key] = metric
    record["fastest_sectors"] = fastest_sectors

    drivers = record.setdefault("drivers", {})
    for driver in snapshot.get("drivers", []):
        driver_id = str(driver.get("id"))
        driver_record = drivers.setdefault(
            driver_id,
            {
                "id": driver.get("id"),
                "driver_name": driver.get("driver_name"),
                "vehicle_name": driver.get("vehicle_name"),
                "laps": 0,
                "lap_history": [],
            },
        )
        driver_record.update(
            {
                "driver_name": driver.get("driver_name"),
                "vehicle_name": driver.get("vehicle_name"),
                "vehicle_class": driver.get("vehicle_class"),
                "laps": driver.get("laps"),
                "last_place": driver.get("place"),
                "finish_status": driver.get("finish_status"),
                "finish_status_name": driver.get("finish_status_name"),
                "best_lap_time": driver.get("best_lap_time"),
                "best_sector_1": driver.get("best_sector_1"),
                "best_sector_2_split": driver.get("best_sector_2_split"),
                "best_lap_sector_3": driver.get("best_lap_sector_3"),
            }
        )
        append_observed_lap(driver_record, driver, snapshot)


def append_observed_lap(driver_record: Snapshot, driver: Snapshot, snapshot: Snapshot) -> None:
    lap_number = driver.get("laps")
    lap_time = driver.get("last_lap_time")
    if not lap_number or not lap_time:
        return
    lap_key = (lap_number, lap_time)
    if any((lap.get("lap_number"), lap.get("lap_time")) == lap_key for lap in driver_record.get("lap_history", [])):
        return
    driver_record.setdefault("lap_history", []).append(
        {
            "lap_number": lap_number,
            "lap_time": lap_time,
            "sector_1": driver.get("last_sector_1"),
            "sector_2": driver.get("last_sector_2_split"),
            "sector_3": driver.get("last_sector_3"),
            "observed_at": snapshot.get("timestamp"),
        }
    )


def is_session_complete(snapshot: Snapshot) -> bool:
    session = snapshot.get("session", {})
    drivers = snapshot.get("drivers", [])
    if session.get("game_phase") == 8:
        return True
    if session.get("session_type") == "Race" and drivers:
        return all(driver.get("finish_status") in (1, 2, 3) for driver in drivers)
    return False


def public_session_record(record: Snapshot | None) -> Snapshot | None:
    if record is None:
        return None
    public = copy.deepcopy(record)
    public["drivers"] = sorted(public.get("drivers", {}).values(), key=lambda driver: driver.get("last_place") or 999)
    return public


def build_source(
    source_kind: str,
    fixture_path: Path,
    map_name: str | None = None,
    pid: int | None = None,
    telemetry_map_name: str | None = None,
) -> ScoringSource:
    if source_kind == "mock":
        return RecordingScoringSource(MockScoringSource(fixture_path))
    if source_kind == "shared-memory":
        return RecordingScoringSource(
            SharedMemoryScoringSource(
                map_name=map_name,
                pid=pid,
                telemetry_map_name=telemetry_map_name,
            )
        )
    if source_kind == "auto":
        return RecordingScoringSource(
            AutoScoringSource(
                fixture_path=fixture_path,
                map_name=map_name,
                pid=pid,
                telemetry_map_name=telemetry_map_name,
            )
        )
    raise ValueError(f"Unsupported source kind: {source_kind}")