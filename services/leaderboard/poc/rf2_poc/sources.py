from __future__ import annotations

import copy
import json
import logging
import re
import shutil
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Protocol, TextIO

from .reports import append_sample_to_record, build_report, make_session_id, placeholder_report, sample_from_driver
from .rf2_shared_memory import SharedMemoryScoringReader, SharedMemoryUnavailable


Snapshot = dict[str, Any]
logger = logging.getLogger(__name__)
MIN_MEANINGFUL_SESSION_SECONDS = 10.0
MIN_MEANINGFUL_SAMPLE_SECONDS = 5.0


class ScoringSource(Protocol):
    def read(self) -> Snapshot:
        """Return a normalized live-session snapshot."""

    def history(self) -> Snapshot:
        """Return in-memory session history collected by the PoC."""

    def report(self, session_id: str) -> Snapshot | None:
        """Return a finalized telemetry report by session ID."""

    def recordings(self) -> Snapshot:
        """Return stored telemetry recordings known to the PoC."""

    def close(self) -> None:
        """Release background resources."""


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

    def report(self, session_id: str) -> Snapshot | None:
        return None

    def recordings(self) -> Snapshot:
        return {"recordings": []}

    def close(self) -> None:
        return None


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

    def report(self, session_id: str) -> Snapshot | None:
        return None

    def recordings(self) -> Snapshot:
        return {"recordings": []}

    def close(self) -> None:
        return None


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

    def report(self, session_id: str) -> Snapshot | None:
        if self._live is not None:
            return self._live.report(session_id)
        return self._fallback.report(session_id)

    def recordings(self) -> Snapshot:
        if self._live is not None:
            return self._live.recordings()
        return self._fallback.recordings()

    def close(self) -> None:
        if self._live is not None:
            self._live.close()
        self._fallback.close()


class RecordingScoringSource:
    def __init__(
        self,
        source: ScoringSource,
        output_dir: Path | None = None,
        telemetry_record_hz: float = 0.0,
    ):
        self._source = source
        self._recorder = SessionRecorder(output_dir=output_dir, target_hz=telemetry_record_hz)
        self._lock = threading.RLock()
        self._latest_snapshot: Snapshot | None = None
        self._last_error: Exception | None = None
        self._consecutive_sample_errors = 0
        self._sample_interval = 1.0 / telemetry_record_hz if telemetry_record_hz > 0 else 0.0
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None
        if self._sample_interval > 0:
            self._thread = threading.Thread(target=self._sample_loop, name="rf2-poc-telemetry-recorder", daemon=True)
            self._thread.start()
            logger.info("Telemetry recorder started at %.2f Hz", telemetry_record_hz)

    def read(self) -> Snapshot:
        with self._lock:
            if self._latest_snapshot is None:
                snapshot = self._read_and_record_locked()
            else:
                snapshot = copy.deepcopy(self._latest_snapshot)
            history = self._recorder.history()
        enrich_snapshot(snapshot, history)
        return snapshot

    def history(self) -> Snapshot:
        with self._lock:
            return self._recorder.history()

    def report(self, session_id: str) -> Snapshot | None:
        return self._recorder.report(session_id)

    def recordings(self) -> Snapshot:
        return self._recorder.recordings()

    def close(self) -> None:
        logger.info("Stopping telemetry recorder")
        self._stop_event.set()
        if self._thread is not None:
            self._thread.join(timeout=2.0)
        self._recorder.close()
        self._source.close()

    def _sample_loop(self) -> None:
        while not self._stop_event.is_set():
            started_at = time.monotonic()
            try:
                with self._lock:
                    self._read_and_record_locked()
            except Exception as exc:
                self._last_error = exc
                self._consecutive_sample_errors += 1
                if self._consecutive_sample_errors == 1 or self._consecutive_sample_errors % 50 == 0:
                    logger.exception(
                        "Telemetry recorder sample failed (%s consecutive failures)",
                        self._consecutive_sample_errors,
                    )
            else:
                if self._consecutive_sample_errors:
                    logger.info("Telemetry recorder recovered after %s failed samples", self._consecutive_sample_errors)
                self._consecutive_sample_errors = 0
            elapsed = time.monotonic() - started_at
            self._stop_event.wait(max(0.0, self._sample_interval - elapsed))

    def _read_and_record_locked(self) -> Snapshot:
        snapshot = self._source.read()
        self._recorder.record(snapshot)
        self._latest_snapshot = copy.deepcopy(snapshot)
        self._last_error = None
        return copy.deepcopy(snapshot)


class SessionRecorder:
    def __init__(self, output_dir: Path | None = None, target_hz: float = 0.0) -> None:
        self._current: Snapshot | None = None
        self._completed: list[Snapshot] = []
        self._reports: dict[str, Snapshot] = {}
        self._report_threads: dict[str, threading.Thread] = {}
        self._report_errors: dict[str, str] = {}
        self._report_lock = threading.RLock()
        self._sample_file_handle: TextIO | None = None
        self._sample_file_path: Path | None = None
        self._session_sequence = 0
        self._output_dir = output_dir
        self._target_hz = target_hz

    def record(self, snapshot: Snapshot) -> None:
        key = session_key(snapshot)
        needs_new_session = self._current is None or self._current.get("key") != key
        if self._current is not None and self._current.get("finalized") and not is_session_complete(snapshot):
            needs_new_session = True
        if needs_new_session:
            if self._current is not None and self._current.get("drivers") and not self._current.get("finalized"):
                self._finalize_current("session changed")
            self._session_sequence += 1
            self._current = new_session_record(
                snapshot,
                key,
                output_dir=self._output_dir,
                target_hz=self._target_hz,
                sequence=self._session_sequence,
            )
            logger.info(
                "Recording session started: id=%s track=%s type=%s samples=%s",
                self._current.get("id"),
                self._current.get("track"),
                self._current.get("session_type"),
                self._current.get("telemetry_samples_file"),
            )

        update_session_record(self._current, snapshot)
        self._record_telemetry_samples(snapshot)
        if is_session_complete(snapshot) and not self._current.get("finalized"):
            self._finalize_current("game phase/session finish")

    def history(self) -> Snapshot:
        return {
            "current_session": public_session_record(self._current) if self._current else None,
            "completed_sessions": [public_session_record(record) for record in self._completed],
            "completed_session_count": len(self._completed),
        }

    def report(self, session_id: str) -> Snapshot | None:
        with self._report_lock:
            report = self._reports.get(session_id)
            if report is not None:
                return copy.deepcopy(report)
            thread = self._report_threads.get(session_id)
            if thread is not None and thread.is_alive():
                return placeholder_report(session_id, "building")
            error = self._report_errors.get(session_id)
            if error:
                return placeholder_report(session_id, "error", error=error)
        return self._read_stored_report(session_id)

    def recordings(self) -> Snapshot:
        return {"recordings": self._list_recordings()}

    def close(self) -> None:
        with self._report_lock:
            threads = list(self._report_threads.values())
        for thread in threads:
            thread.join(timeout=2.0)
        self._close_sample_file()

    def _record_telemetry_samples(self, snapshot: Snapshot) -> None:
        if self._current is None:
            return
        for driver in snapshot.get("drivers", []):
            sample = sample_from_driver(snapshot, driver)
            if sample is None:
                continue
            sample["session_id"] = self._current.get("id")
            sample["session_key"] = self._current.get("key")
            if append_sample_to_record(self._current, sample):
                self._current["telemetry_sample_count"] = int(self._current.get("telemetry_sample_count") or 0) + 1
                try:
                    self._write_sample(sample)
                except Exception as exc:
                    error_count = int(self._current.get("telemetry_write_error_count") or 0) + 1
                    self._current["telemetry_write_error_count"] = error_count
                    self._current["telemetry_write_error"] = str(exc)
                    if error_count == 1 or error_count % 50 == 0:
                        logger.exception(
                            "Failed to write telemetry sample for session %s (%s write failures)",
                            self._current.get("id"),
                            error_count,
                        )
                else:
                    if self._current.get("telemetry_write_error"):
                        logger.info("Telemetry sample writing recovered for session %s", self._current.get("id"))
                    self._current.pop("telemetry_write_error", None)

    def _write_sample(self, sample: Snapshot) -> None:
        sample_file = (self._current or {}).get("telemetry_samples_file")
        if not sample_file:
            return
        path = Path(sample_file)
        if self._sample_file_path != path or self._sample_file_handle is None:
            self._close_sample_file()
            path.parent.mkdir(parents=True, exist_ok=True)
            self._sample_file_handle = path.open("a", encoding="utf-8", buffering=1)
            self._sample_file_path = path
            logger.info("Writing telemetry samples to %s", path)
        try:
            self._sample_file_handle.write(json.dumps(sample, separators=(",", ":")) + "\n")
        except Exception:
            self._close_sample_file()
            raise

    def _close_sample_file(self) -> None:
        if self._sample_file_handle is None:
            self._sample_file_path = None
            return
        try:
            self._sample_file_handle.close()
        finally:
            self._sample_file_handle = None
            self._sample_file_path = None

    def _finalize_current(self, reason: str) -> None:
        if self._current is None or self._current.get("finalized"):
            return
        if not is_meaningful_session_record(self._current):
            self._discard_current(f"discarded {reason}: too short or empty")
            return
        self._current["finalized"] = True
        self._current["completion_reason"] = reason
        self._current["finalized_at"] = time.time()
        self._current["finalized_at_iso"] = iso_timestamp(self._current["finalized_at"])
        session_id = str(self._current.get("id"))
        self._current["report_url"] = f"/telemetry?session={session_id}"
        self._current["report_status"] = "building"
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
        logger.info(
            "Recording session finalized: id=%s reason=%s samples=%s",
            session_id,
            reason,
            self._current.get("telemetry_sample_count"),
        )
        self._start_report_build(session_id, copy.deepcopy(self._current))

    def _discard_current(self, reason: str) -> None:
        if self._current is None:
            return
        session_id = self._current.get("id")
        sample_file = self._current.get("telemetry_samples_file")
        logger.info(
            "Discarding recorded session: id=%s reason=%s samples=%s duration=%.3f",
            session_id,
            reason,
            self._current.get("telemetry_sample_count"),
            session_duration_seconds(self._current),
        )
        self._close_sample_file()
        if sample_file:
            session_dir = Path(sample_file).parent
            if session_dir.exists() and self._output_dir is not None and session_dir.parent == self._output_dir:
                shutil.rmtree(session_dir, ignore_errors=True)
        self._current = None

    def _start_report_build(self, session_id: str, record: Snapshot) -> None:
        thread = threading.Thread(
            target=self._build_report_job,
            args=(session_id, record),
            name=f"rf2-poc-report-{session_id}",
            daemon=True,
        )
        with self._report_lock:
            self._report_threads[session_id] = thread
        thread.start()
        logger.info("Telemetry report build started: id=%s", session_id)

    def _build_report_job(self, session_id: str, record: Snapshot) -> None:
        started_at = time.monotonic()
        try:
            report = build_report(record)
            if report is None:
                report = placeholder_report(session_id, "no telemetry laps")
            with self._report_lock:
                self._reports[session_id] = report
                self._report_errors.pop(session_id, None)
            self._write_report(session_id, report)
            self._set_report_status(session_id, str(report.get("status") or "ready"))
            logger.info(
                "Telemetry report build finished: id=%s status=%s laps=%s seconds=%.3f",
                session_id,
                report.get("status"),
                len(report.get("laps") or []),
                time.monotonic() - started_at,
            )
        except Exception as exc:
            with self._report_lock:
                self._report_errors[session_id] = str(exc)
            self._set_report_status(session_id, "error")
            logger.exception("Telemetry report build failed: id=%s", session_id)

    def _set_report_status(self, session_id: str, status: str) -> None:
        if self._current and self._current.get("id") == session_id:
            self._current["report_status"] = status
        for record in self._completed:
            if record.get("id") == session_id:
                record["report_status"] = status
                break

    def _write_report(self, session_id: str, report: Snapshot) -> None:
        if self._output_dir is None:
            return
        report_file = self._output_dir / session_id / "report.json"
        report_file.parent.mkdir(parents=True, exist_ok=True)
        report_file.write_text(json.dumps(report, indent=2), encoding="utf-8")
        logger.info("Telemetry report written: id=%s file=%s", session_id, report_file)

    def _read_stored_report(self, session_id: str) -> Snapshot | None:
        report_file = self._recording_report_file(session_id)
        if report_file is None or not report_file.exists():
            return None
        try:
            return json.loads(report_file.read_text(encoding="utf-8"))
        except Exception:
            logger.exception("Failed to read stored telemetry report: id=%s file=%s", session_id, report_file)
            return None

    def _list_recordings(self) -> list[Snapshot]:
        if self._output_dir is None or not self._output_dir.exists():
            return []
        recordings = []
        for session_dir in self._output_dir.iterdir():
            if not session_dir.is_dir():
                continue
            sample_file = session_dir / "telemetry_samples.jsonl"
            report_file = session_dir / "report.json"
            if not sample_file.exists() and not report_file.exists():
                continue
            report = None
            if report_file.exists():
                try:
                    report = json.loads(report_file.read_text(encoding="utf-8"))
                except Exception:
                    logger.exception("Failed to read recording summary: %s", report_file)
            recordings.append(recording_summary(session_dir, sample_file, report_file, report))
        return sorted(recordings, key=lambda item: item.get("last_modified") or 0, reverse=True)

    def _recording_report_file(self, session_id: str) -> Path | None:
        if self._output_dir is None or not is_safe_recording_id(session_id):
            return None
        return self._output_dir / session_id / "report.json"


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
        coverage_item("session.flags", "Flag / yellow state", "scoring", bool((snapshot.get("session") or {}).get("overall_flag"))),
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
        coverage_item("reports.telemetry_samples", "Telemetry samples for reports", "recorder", int((history.get("current_session") or {}).get("telemetry_sample_count") or 0) > 0, f"{(history.get('current_session') or {}).get('telemetry_sample_count', 0)} samples"),
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
    start_time = first_present(session.get("start_time"), session.get("end_time"), "unknown-start")
    return f"{track}|{session_code}|{start_time}"


def first_present(*values: Any) -> Any:
    for value in values:
        if value is not None:
            return value
    return None


def new_session_record(
    snapshot: Snapshot,
    key: str,
    output_dir: Path | None = None,
    target_hz: float = 0.0,
    sequence: int = 0,
) -> Snapshot:
    session = snapshot.get("session", {})
    started_at = snapshot.get("timestamp") or time.time()
    session_id = make_session_id(snapshot, f"{key}|{sequence}|{started_at:.3f}")
    session_dir = output_dir / session_id if output_dir is not None else None
    return {
        "id": session_id,
        "key": key,
        "track": session.get("track"),
        "session_type": session.get("session_type"),
        "session_code": session.get("session_code"),
        "started_at": started_at,
        "started_at_iso": iso_timestamp(started_at),
        "last_seen_at": started_at,
        "last_seen_at_iso": iso_timestamp(started_at),
        "finalized_at": None,
        "finalized_at_iso": None,
        "finalized": False,
        "completion_reason": None,
        "drivers": {},
        "fastest_lap": None,
        "fastest_sectors": {},
        "finishing_order": [],
        "report_url": None,
        "report_status": None,
        "telemetry_sample_count": 0,
        "telemetry_target_hz": target_hz,
        "telemetry_samples_file": str(session_dir / "telemetry_samples.jsonl") if session_dir is not None else None,
        "telemetry_write_error": None,
        "telemetry_write_error_count": 0,
    }


def update_session_record(record: Snapshot, snapshot: Snapshot) -> None:
    record["last_seen_at"] = snapshot.get("timestamp") or time.time()
    record["last_seen_at_iso"] = iso_timestamp(record["last_seen_at"])
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


def is_meaningful_session_record(record: Snapshot) -> bool:
    if any((driver.get("lap_history") or []) for driver in record.get("drivers", {}).values()):
        return True
    sample_count = int(record.get("telemetry_sample_count") or 0)
    target_hz = float(record.get("telemetry_target_hz") or 0.0)
    if target_hz > 0 and sample_count >= int(target_hz * MIN_MEANINGFUL_SAMPLE_SECONDS):
        return True
    if target_hz <= 0 and sample_count > 0 and session_duration_seconds(record) >= MIN_MEANINGFUL_SESSION_SECONDS:
        return True
    return session_duration_seconds(record) >= MIN_MEANINGFUL_SESSION_SECONDS and bool(record.get("drivers"))


def session_duration_seconds(record: Snapshot) -> float:
    started = record.get("started_at")
    last_seen = record.get("last_seen_at")
    if not isinstance(started, (int, float)) or not isinstance(last_seen, (int, float)):
        return 0.0
    return max(0.0, float(last_seen) - float(started))


def iso_timestamp(timestamp: Any) -> str | None:
    if not isinstance(timestamp, (int, float)):
        return None
    return datetime.fromtimestamp(float(timestamp), timezone.utc).astimezone().isoformat(timespec="seconds")


def is_safe_recording_id(session_id: str) -> bool:
    return bool(re.fullmatch(r"[A-Za-z0-9_.-]+", session_id))


def recording_summary(session_dir: Path, sample_file: Path, report_file: Path, report: Snapshot | None) -> Snapshot:
    latest_mtime = max(
        path.stat().st_mtime
        for path in (sample_file, report_file)
        if path.exists()
    )
    return {
        "session_id": session_dir.name,
        "track": (report or {}).get("track"),
        "session_type": (report or {}).get("session_type"),
        "status": (report or {}).get("status") or ("samples only" if sample_file.exists() else "report only"),
        "started_at": (report or {}).get("started_at"),
        "proper_lap_count": (report or {}).get("proper_lap_count"),
        "excluded_lap_count": (report or {}).get("excluded_lap_count"),
        "telemetry_sample_count": (report or {}).get("telemetry_sample_count"),
        "sample_file": str(sample_file) if sample_file.exists() else None,
        "report_file": str(report_file) if report_file.exists() else None,
        "last_modified": latest_mtime,
        "last_modified_iso": iso_timestamp(latest_mtime),
        "viewer_url": f"/telemetry?session={session_dir.name}",
    }


def public_session_record(record: Snapshot | None) -> Snapshot | None:
    if record is None:
        return None
    public = copy.deepcopy(record)
    public["drivers"] = [
        public_driver_record(driver)
        for driver in sorted(public.get("drivers", {}).values(), key=lambda driver: driver.get("last_place") or 999)
    ]
    return public


def public_driver_record(driver: Snapshot) -> Snapshot:
    lap_history = copy.deepcopy(driver.get("lap_history") or [])
    telemetry_laps = driver.get("telemetry_laps") or {}
    completed_telemetry_laps = [lap for lap in telemetry_laps.values() if lap.get("completed") or lap.get("lap_time")]
    return {
        "id": driver.get("id"),
        "driver_name": driver.get("driver_name"),
        "vehicle_name": driver.get("vehicle_name"),
        "vehicle_class": driver.get("vehicle_class"),
        "laps": driver.get("laps"),
        "last_place": driver.get("last_place"),
        "finish_status": driver.get("finish_status"),
        "finish_status_name": driver.get("finish_status_name"),
        "best_lap_time": driver.get("best_lap_time"),
        "best_sector_1": driver.get("best_sector_1"),
        "best_sector_2_split": driver.get("best_sector_2_split"),
        "best_lap_sector_3": driver.get("best_lap_sector_3"),
        "lap_history": lap_history,
        "telemetry_lap_count": len(telemetry_laps),
        "completed_telemetry_lap_count": len(completed_telemetry_laps),
        "telemetry_sample_count": sum(len(lap.get("samples") or []) for lap in telemetry_laps.values()),
    }


def build_source(
    source_kind: str,
    fixture_path: Path,
    map_name: str | None = None,
    pid: int | None = None,
    telemetry_map_name: str | None = None,
    telemetry_output_dir: Path | None = None,
    telemetry_record_hz: float = 0.0,
) -> ScoringSource:
    if source_kind == "mock":
        return RecordingScoringSource(
            MockScoringSource(fixture_path),
            output_dir=telemetry_output_dir,
            telemetry_record_hz=telemetry_record_hz,
        )
    if source_kind == "shared-memory":
        return RecordingScoringSource(
            SharedMemoryScoringSource(
                map_name=map_name,
                pid=pid,
                telemetry_map_name=telemetry_map_name,
            ),
            output_dir=telemetry_output_dir,
            telemetry_record_hz=telemetry_record_hz,
        )
    if source_kind == "auto":
        return RecordingScoringSource(
            AutoScoringSource(
                fixture_path=fixture_path,
                map_name=map_name,
                pid=pid,
                telemetry_map_name=telemetry_map_name,
            ),
            output_dir=telemetry_output_dir,
            telemetry_record_hz=telemetry_record_hz,
        )
    raise ValueError(f"Unsupported source kind: {source_kind}")