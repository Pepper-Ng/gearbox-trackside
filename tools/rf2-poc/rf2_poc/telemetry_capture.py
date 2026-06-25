from __future__ import annotations

import bisect
import ctypes
import json
import logging
import math
import os
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, TextIO

from .reports import append_sample_to_record, numeric
from .rf2_shared_memory import (
    MAX_MAPPED_VEHICLES,
    STANDARD_GRAVITY,
    SharedMemoryTelemetryReader,
    c_string,
    gear_label,
    is_probable_telemetry_vehicle,
    percent_01,
    percent_signed,
    rF2Telemetry,
)


logger = logging.getLogger(__name__)

RAW_TELEMETRY_SCHEMA = "gearbox-trackside.telemetry-raw.v1"
RAW_TELEMETRY_FILENAME = "telemetry_raw.jsonl"
TELEMETRY_WORKER_DIRNAME = "_telemetry-worker"
RAW_VEHICLE_FIELDS = [
    "id",
    "lap_number",
    "elapsed_time",
    "lap_start_time",
    "delta_time",
    "gear",
    "throttle",
    "brake",
    "steering",
    "local_velocity_x",
    "local_velocity_y",
    "local_velocity_z",
    "local_accel_x",
    "local_accel_y",
    "local_accel_z",
    "vehicle_name",
    "track_name",
]

Snapshot = dict[str, Any]


class CompactTelemetryWriter:
    def __init__(self, path: Path, flush_frames: int = 50) -> None:
        self.path = path
        self.flush_frames = max(1, flush_frames)
        self._handle: TextIO | None = None
        self._pending: list[str] = []

    def __enter__(self) -> "CompactTelemetryWriter":
        self.open()
        return self

    def __exit__(self, exc_type, exc, tb) -> None:  # type: ignore[no-untyped-def]
        self.close()

    def open(self) -> None:
        if self._handle is not None:
            return
        self.path.parent.mkdir(parents=True, exist_ok=True)
        is_new = not self.path.exists() or self.path.stat().st_size == 0
        self._handle = self.path.open("a", encoding="utf-8", buffering=1024 * 1024)
        if is_new:
            self.write_header()

    def write_header(self) -> None:
        self.write(
            {
                "type": "header",
                "schema": RAW_TELEMETRY_SCHEMA,
                "vehicle_fields": RAW_VEHICLE_FIELDS,
                "created_at": time.time(),
            }
        )
        self.flush()

    def write(self, payload: Snapshot) -> None:
        if self._handle is None:
            self.open()
        self._pending.append(json.dumps(payload, separators=(",", ":")))
        if len(self._pending) >= self.flush_frames:
            self.flush()

    def flush(self) -> None:
        if self._handle is None or not self._pending:
            return
        self._handle.write("\n".join(self._pending) + "\n")
        self._handle.flush()
        self._pending.clear()

    def close(self) -> None:
        try:
            self.flush()
        finally:
            if self._handle is not None:
                self._handle.close()
                self._handle = None


@dataclass
class TelemetryWorkerOptions:
    output_file: Path
    target_hz: float
    map_name: str | None = None
    pid: int | None = None
    duration_seconds: float | None = None
    flush_frames: int = 10
    status_file: Path | None = None
    status_interval_seconds: float = 2.0
    stop_file: Path | None = None
    priority: str = "normal"
    affinity_mask: int | None = None
    write_repeated_updates: bool = False
    stable_attempts: int = 2


def run_telemetry_worker(options: TelemetryWorkerOptions) -> int:
    set_process_tuning(options.priority, options.affinity_mask)
    reader = SharedMemoryTelemetryReader(map_name=options.map_name, pid=options.pid)
    target_interval = 1.0 / options.target_hz if options.target_hz > 0 else 0.0
    started_perf = time.perf_counter()
    next_deadline = time.perf_counter()
    last_status_at = 0.0
    last_update_counter: int | None = None
    stats: Snapshot = {
        "schema": RAW_TELEMETRY_SCHEMA,
        "status": "running",
        "started_at": time.time(),
        "output_file": str(options.output_file),
        "target_hz": options.target_hz,
        "map_name": None,
        "loops": 0,
        "frames_written": 0,
        "vehicle_samples_written": 0,
        "duplicate_update_counter_reads": 0,
        "update_counter_gap_count": 0,
        "late_loop_count": 0,
        "torn_read_count": 0,
        "last_update_counter": None,
        "last_error": None,
    }
    try:
        with CompactTelemetryWriter(options.output_file, flush_frames=options.flush_frames) as writer:
            while True:
                loop_started = time.perf_counter()
                if options.duration_seconds is not None and loop_started - started_perf >= options.duration_seconds:
                    break
                if options.stop_file is not None and options.stop_file.exists():
                    break
                if target_interval > 0 and loop_started > next_deadline + target_interval:
                    stats["late_loop_count"] = int(stats["late_loop_count"] or 0) + 1

                timestamp = time.time()
                telemetry, decode_offset, torn_read = reader.read_raw_payload(stable_attempts=options.stable_attempts)
                update_counter = int(telemetry.mVersionUpdateEnd)
                stats["loops"] = int(stats["loops"] or 0) + 1
                stats["map_name"] = reader.map_name
                stats["last_update_counter"] = update_counter
                if torn_read:
                    stats["torn_read_count"] = int(stats["torn_read_count"] or 0) + 1

                should_write = True
                if last_update_counter is not None:
                    delta = update_counter - last_update_counter
                    if delta == 0:
                        stats["duplicate_update_counter_reads"] = int(stats["duplicate_update_counter_reads"] or 0) + 1
                        should_write = options.write_repeated_updates
                    elif delta > 1:
                        stats["update_counter_gap_count"] = int(stats["update_counter_gap_count"] or 0) + 1
                last_update_counter = update_counter

                if should_write:
                    frame = compact_frame_from_payload(
                        telemetry,
                        timestamp=timestamp,
                        read_duration_seconds=time.perf_counter() - loop_started,
                        decode_offset=decode_offset,
                        map_name=reader.map_name,
                        torn_read=torn_read,
                    )
                    writer.write(frame)
                    stats["frames_written"] = int(stats["frames_written"] or 0) + 1
                    stats["vehicle_samples_written"] = int(stats["vehicle_samples_written"] or 0) + len(frame.get("v") or [])

                now = time.perf_counter()
                if options.status_file is not None and now - last_status_at >= options.status_interval_seconds:
                    write_worker_status(options.status_file, stats, started_perf)
                    last_status_at = now
                if target_interval > 0:
                    next_deadline += target_interval
                    sleep_seconds = next_deadline - time.perf_counter()
                    if sleep_seconds > 0:
                        time.sleep(sleep_seconds)
                    elif sleep_seconds < -target_interval:
                        next_deadline = time.perf_counter()
    except KeyboardInterrupt:
        stats["status"] = "stopped"
    except Exception as exc:
        stats["status"] = "error"
        stats["last_error"] = str(exc)
        logger.exception("Telemetry worker failed")
        if options.status_file is not None:
            write_worker_status(options.status_file, stats, started_perf)
        return 1
    finally:
        reader.close()
    stats["status"] = "stopped"
    if options.status_file is not None:
        write_worker_status(options.status_file, stats, started_perf)
    return 0


def set_process_tuning(priority: str, affinity_mask: int | None) -> None:
    if os.name != "nt":
        return
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    process = kernel32.GetCurrentProcess()
    if priority == "high":
        high_priority_class = 0x00000080
        kernel32.SetPriorityClass(process, high_priority_class)
    if affinity_mask is not None:
        kernel32.SetProcessAffinityMask(process, ctypes.c_size_t(affinity_mask))


def write_worker_status(path: Path, stats: Snapshot, started_perf: float) -> None:
    payload = dict(stats)
    elapsed = max(0.0, time.perf_counter() - started_perf)
    frames_written = int(payload.get("frames_written") or 0)
    payload["elapsed_seconds"] = round(elapsed, 3)
    payload["effective_frame_hz"] = round(frames_written / elapsed, 3) if elapsed > 0 else None
    payload["updated_at"] = time.time()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def compact_frame_from_payload(
    telemetry: rF2Telemetry,
    timestamp: float,
    read_duration_seconds: float,
    decode_offset: int,
    map_name: str,
    torn_read: bool,
) -> Snapshot:
    raw_vehicle_count = int(telemetry.mNumVehicles)
    scan_limit = raw_vehicle_count if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES else MAX_MAPPED_VEHICLES
    vehicles = []
    for index in range(scan_limit):
        vehicle = telemetry.mVehicles[index]
        if not is_probable_telemetry_vehicle(vehicle):
            continue
        vehicles.append(compact_vehicle_row(vehicle))
    return {
        "type": "frame",
        "t": timestamp,
        "u": int(telemetry.mVersionUpdateEnd),
        "b": int(telemetry.mVersionUpdateBegin),
        "e": int(telemetry.mVersionUpdateEnd),
        "h": int(telemetry.mBytesUpdatedHint),
        "n": raw_vehicle_count,
        "m": map_name,
        "o": decode_offset,
        "r": round(read_duration_seconds, 6),
        "x": bool(torn_read),
        "v": vehicles,
    }


def compact_vehicle_row(vehicle: Any) -> list[Any]:
    return [
        int(vehicle.mID),
        int(vehicle.mLapNumber),
        none_if_negative_raw(float(vehicle.mElapsedTime)),
        none_if_negative_raw(float(vehicle.mLapStartET)),
        none_if_negative_raw(float(vehicle.mDeltaTime)),
        int(vehicle.mGear),
        float(vehicle.mUnfilteredThrottle),
        float(vehicle.mUnfilteredBrake),
        float(vehicle.mUnfilteredSteering),
        float(vehicle.mLocalVel.x),
        float(vehicle.mLocalVel.y),
        float(vehicle.mLocalVel.z),
        float(vehicle.mLocalAccel.x),
        float(vehicle.mLocalAccel.y),
        float(vehicle.mLocalAccel.z),
        c_string(vehicle.mVehicleName),
        c_string(vehicle.mTrackName),
    ]


def none_if_negative_raw(value: float) -> float | None:
    return None if value < 0 else value


class TelemetryWorkerProcess:
    def __init__(
        self,
        output_dir: Path,
        target_hz: float,
        map_name: str | None = None,
        pid: int | None = None,
        priority: str = "high",
        affinity_mask: int | None = None,
        flush_frames: int = 10,
    ) -> None:
        self.output_dir = output_dir
        self.target_hz = target_hz
        self.map_name = map_name
        self.pid = pid
        self.priority = priority
        self.affinity_mask = affinity_mask
        self.flush_frames = flush_frames
        self.run_id = f"run-{int(time.time())}-{uuid.uuid4().hex[:8]}"
        self.run_dir = output_dir / TELEMETRY_WORKER_DIRNAME / self.run_id
        self.raw_file = self.run_dir / RAW_TELEMETRY_FILENAME
        self.status_file = self.run_dir / "telemetry_worker_status.json"
        self.stop_file = self.run_dir / "telemetry_worker.stop"
        self.log_file = self.run_dir / "telemetry_worker.log"
        self.process: subprocess.Popen[str] | None = None
        self._log_handle: Any | None = None

    def start(self) -> None:
        if self.target_hz <= 0 or self.process is not None:
            return
        self.run_dir.mkdir(parents=True, exist_ok=True)
        script = Path(__file__).resolve().parents[1] / "run_telemetry_recorder.py"
        command = [
            sys.executable,
            str(script),
            "--output-file",
            str(self.raw_file),
            "--target-hz",
            str(self.target_hz),
            "--status-file",
            str(self.status_file),
            "--stop-file",
            str(self.stop_file),
            "--flush-frames",
            str(self.flush_frames),
            "--priority",
            self.priority,
        ]
        if self.map_name:
            command.extend(["--map-name", self.map_name])
        if self.pid is not None:
            command.extend(["--pid", str(self.pid)])
        if self.affinity_mask is not None:
            command.extend(["--affinity-mask", str(self.affinity_mask)])
        creationflags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
        self._log_handle = self.log_file.open("a", encoding="utf-8")
        self.process = subprocess.Popen(
            command,
            cwd=str(Path(__file__).resolve().parents[1]),
            creationflags=creationflags,
            stdout=self._log_handle,
            stderr=subprocess.STDOUT,
            text=True,
        )
        logger.info(
            "Telemetry worker process started: pid=%s target_hz=%.2f raw=%s",
            self.process.pid,
            self.target_hz,
            self.raw_file,
        )

    def status(self) -> Snapshot:
        payload: Snapshot = {
            "mode": "process",
            "run_id": self.run_id,
            "pid": self.process.pid if self.process is not None else None,
            "raw_file": str(self.raw_file),
            "status_file": str(self.status_file),
            "log_file": str(self.log_file),
            "target_hz": self.target_hz,
        }
        if self.process is not None:
            return_code = self.process.poll()
            payload["process_status"] = "running" if return_code is None else f"exited {return_code}"
        if self.status_file.exists():
            try:
                payload.update(json.loads(self.status_file.read_text(encoding="utf-8")))
            except Exception as exc:
                payload["status_error"] = str(exc)
        return payload

    def enrich_record(self, record: Snapshot) -> Snapshot:
        importer = TelemetryRawSessionImporter(self.raw_file)
        return importer.enrich_record(record)

    def close(self) -> None:
        if self.process is None:
            return
        if self.process.poll() is None:
            try:
                self.stop_file.write_text("stop\n", encoding="utf-8")
                self.process.wait(timeout=5.0)
            except subprocess.TimeoutExpired:
                self.process.terminate()
                try:
                    self.process.wait(timeout=3.0)
                except subprocess.TimeoutExpired:
                    self.process.kill()
                    self.process.wait(timeout=2.0)
        if self._log_handle is not None:
            self._log_handle.close()
            self._log_handle = None
        logger.info("Telemetry worker process stopped: pid=%s", self.process.pid)


class TelemetryRawSessionImporter:
    def __init__(self, raw_file: Path) -> None:
        self.raw_file = raw_file

    def enrich_record(self, record: Snapshot) -> Snapshot:
        record = dict(record)
        sample_file_value = record.get("telemetry_samples_file")
        if not sample_file_value:
            record["telemetry_import_error"] = "record has no telemetry_samples_file"
            return record
        if not self.raw_file.exists():
            record["telemetry_import_error"] = f"raw telemetry file does not exist: {self.raw_file}"
            return record

        sample_file = Path(sample_file_value)
        session_raw_file = sample_file.parent / RAW_TELEMETRY_FILENAME
        sample_file.parent.mkdir(parents=True, exist_ok=True)
        start_time = numeric(record.get("started_at"))
        end_time = numeric(record.get("finalized_at")) or numeric(record.get("last_seen_at"))
        if start_time is None or end_time is None:
            record["telemetry_import_error"] = "record does not have start/end timestamps"
            return record

        scoring_index = ScoringSampleIndex(record.get("_scoring_samples") or [])
        raw_frames = 0
        raw_vehicle_samples = 0
        imported_samples = 0
        duplicate_samples = 0
        with CompactTelemetryWriter(session_raw_file, flush_frames=100) as raw_writer:
            with sample_file.open("w", encoding="utf-8", buffering=1024 * 1024) as sample_handle:
                for frame in load_compact_frames(self.raw_file, start_time=start_time, end_time=end_time):
                    raw_frames += 1
                    raw_writer.write(frame)
                    for sample in samples_from_compact_frame(frame, scoring_index, record):
                        raw_vehicle_samples += 1
                        if append_sample_to_record(record, sample):
                            imported_samples += 1
                            sample_handle.write(json.dumps(sample, separators=(",", ":")) + "\n")
                        else:
                            duplicate_samples += 1

        record["telemetry_sample_count"] = imported_samples
        record["telemetry_samples_file"] = str(sample_file)
        record["telemetry_raw_file"] = str(session_raw_file)
        record["telemetry_import_stats"] = {
            "source_raw_file": str(self.raw_file),
            "raw_frames": raw_frames,
            "raw_vehicle_samples": raw_vehicle_samples,
            "imported_samples": imported_samples,
            "duplicate_samples": duplicate_samples,
            "scoring_sample_count": len(record.get("_scoring_samples") or []),
        }
        logger.info(
            "Telemetry raw import finished: id=%s frames=%s samples=%s duplicates=%s",
            record.get("id"),
            raw_frames,
            imported_samples,
            duplicate_samples,
        )
        return record


class ScoringSampleIndex:
    def __init__(self, scoring_samples: list[Snapshot]) -> None:
        self.by_driver: dict[str, list[Snapshot]] = {}
        self.timestamps_by_driver: dict[str, list[float]] = {}
        for sample in sorted(scoring_samples, key=lambda item: float(item.get("timestamp") or 0.0)):
            driver_id = str(sample.get("driver_id"))
            if not driver_id or driver_id == "None":
                continue
            self.by_driver.setdefault(driver_id, []).append(sample)
        for driver_id, samples in self.by_driver.items():
            self.timestamps_by_driver[driver_id] = [float(sample.get("timestamp") or 0.0) for sample in samples]

    def lookup(self, driver_id: Any, timestamp: float, lap_number: int | None) -> Snapshot:
        samples = self.by_driver.get(str(driver_id)) or []
        timestamps = self.timestamps_by_driver.get(str(driver_id)) or []
        if not samples:
            return {}
        index = bisect.bisect_left(timestamps, timestamp)
        before = samples[index - 1] if index > 0 else None
        after = samples[index] if index < len(samples) else None
        nearest = nearest_sample(before, after, timestamp) or before or after or {}
        return {
            "nearest": nearest,
            "lap_distance": interpolated_field(before, after, timestamp, lap_number, "lap_distance"),
            "lap_percent": interpolated_field(before, after, timestamp, lap_number, "lap_percent"),
            "session_time": interpolated_field(before, after, timestamp, None, "session_time"),
            "time_seconds": interpolated_field(before, after, timestamp, lap_number, "time_seconds"),
        }


def nearest_sample(before: Snapshot | None, after: Snapshot | None, timestamp: float) -> Snapshot | None:
    if before is None:
        return after
    if after is None:
        return before
    before_delta = abs(timestamp - float(before.get("timestamp") or 0.0))
    after_delta = abs(float(after.get("timestamp") or 0.0) - timestamp)
    return before if before_delta <= after_delta else after


def interpolated_field(
    before: Snapshot | None,
    after: Snapshot | None,
    timestamp: float,
    lap_number: int | None,
    field: str,
) -> float | None:
    if before is None and after is None:
        return None
    nearest = nearest_sample(before, after, timestamp)
    if before is None or after is None:
        return numeric((nearest or {}).get(field))
    left_time = numeric(before.get("timestamp"))
    right_time = numeric(after.get("timestamp"))
    left_value = numeric(before.get(field))
    right_value = numeric(after.get(field))
    if left_time is None or right_time is None or right_time <= left_time:
        return numeric((nearest or {}).get(field))
    if left_value is None or right_value is None:
        return numeric((nearest or {}).get(field))
    if lap_number is not None:
        left_lap = before.get("lap_number")
        right_lap = after.get("lap_number")
        if left_lap != lap_number or right_lap != lap_number:
            return numeric((nearest or {}).get(field))
    if field in {"lap_distance", "lap_percent"} and right_value + 8.0 < left_value:
        return numeric((nearest or {}).get(field))
    ratio = max(0.0, min(1.0, (timestamp - left_time) / (right_time - left_time)))
    return round(left_value + (right_value - left_value) * ratio, 4)


def load_compact_frames(path: Path, start_time: float | None = None, end_time: float | None = None) -> Iterable[Snapshot]:
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                payload = json.loads(stripped)
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSON on {path}:{line_number}: {exc}") from exc
            if payload.get("type") != "frame":
                continue
            timestamp = numeric(payload.get("t"))
            if timestamp is None:
                continue
            if start_time is not None and timestamp < start_time:
                continue
            if end_time is not None and timestamp > end_time:
                continue
            yield payload


def samples_from_compact_frame(frame: Snapshot, scoring_index: ScoringSampleIndex, record: Snapshot) -> Iterable[Snapshot]:
    timestamp = numeric(frame.get("t")) or time.time()
    update_counter = frame.get("u")
    for vehicle in frame.get("v") or []:
        raw_sample = sample_from_compact_vehicle(frame, vehicle)
        driver_id = raw_sample.get("driver_id")
        lap_number = raw_sample.get("lap_number") if isinstance(raw_sample.get("lap_number"), int) else None
        scoring = scoring_index.lookup(driver_id, timestamp, lap_number)
        nearest = scoring.get("nearest") or {}
        raw_sample.update(
            {
                "timestamp": timestamp,
                "session_id": record.get("id"),
                "session_key": record.get("key"),
                "session_track": nearest.get("session_track") or record.get("track"),
                "session_type": nearest.get("session_type") or record.get("session_type"),
                "session_game_phase": nearest.get("session_game_phase"),
                "session_game_phase_name": nearest.get("session_game_phase_name"),
                "telemetry_update_counter": update_counter,
                "driver_name": nearest.get("driver_name") or raw_sample.get("driver_name"),
                "vehicle_name": nearest.get("vehicle_name") or raw_sample.get("vehicle_name"),
                "lap_distance": scoring.get("lap_distance"),
                "lap_percent": scoring.get("lap_percent"),
                "session_time": scoring.get("session_time"),
                "count_lap_flag": nearest.get("count_lap_flag"),
                "count_lap_flag_name": nearest.get("count_lap_flag_name"),
                "in_pits": nearest.get("in_pits"),
                "pit_state": nearest.get("pit_state"),
                "pit_state_name": nearest.get("pit_state_name"),
                "finish_status": nearest.get("finish_status"),
                "finish_status_name": nearest.get("finish_status_name"),
                "telemetry_torn_read": frame.get("x"),
                "telemetry_read_duration_seconds": frame.get("r"),
            }
        )
        if raw_sample.get("time_seconds") is None:
            raw_sample["time_seconds"] = scoring.get("time_seconds")
        yield raw_sample


def sample_from_compact_vehicle(frame: Snapshot, row: list[Any]) -> Snapshot:
    values = {field: row[index] if index < len(row) else None for index, field in enumerate(RAW_VEHICLE_FIELDS)}
    accel_x = numeric(values.get("local_accel_x")) or 0.0
    accel_y = numeric(values.get("local_accel_y")) or 0.0
    accel_z = numeric(values.get("local_accel_z")) or 0.0
    lateral_g = accel_x / STANDARD_GRAVITY
    vertical_g = accel_y / STANDARD_GRAVITY
    longitudinal_g = accel_z / STANDARD_GRAVITY
    elapsed_time = numeric(values.get("elapsed_time"))
    lap_start_time = numeric(values.get("lap_start_time"))
    time_seconds = None
    if elapsed_time is not None and lap_start_time is not None and elapsed_time >= lap_start_time:
        time_seconds = round(elapsed_time - lap_start_time, 4)
    gear = values.get("gear")
    return {
        "driver_id": values.get("id"),
        "driver_name": None,
        "vehicle_name": values.get("vehicle_name"),
        "lap_number": values.get("lap_number") if isinstance(values.get("lap_number"), int) else None,
        "time_seconds": time_seconds,
        "speed_kph": speed_kph_from_components(
            numeric(values.get("local_velocity_x")) or 0.0,
            numeric(values.get("local_velocity_y")) or 0.0,
            numeric(values.get("local_velocity_z")) or 0.0,
        ),
        "throttle_percent": percent_01(float(values.get("throttle") or 0.0)),
        "brake_percent": percent_01(float(values.get("brake") or 0.0)),
        "gear": gear,
        "gear_label": gear_label(int(gear)) if isinstance(gear, int) else None,
        "steering_percent": percent_signed(float(values.get("steering") or 0.0)),
        "lateral_g": round(lateral_g, 3),
        "longitudinal_g": round(longitudinal_g, 3),
        "vertical_g": round(vertical_g, 3),
        "g_magnitude": round(math.sqrt(lateral_g * lateral_g + vertical_g * vertical_g + longitudinal_g * longitudinal_g), 3),
        "raw_telemetry_elapsed_time": elapsed_time,
        "raw_telemetry_delta_time": values.get("delta_time"),
        "raw_telemetry_track_name": values.get("track_name"),
        "raw_telemetry_frame_update_counter": frame.get("u"),
    }


def speed_kph_from_components(x: float, y: float, z: float) -> float:
    return round(math.sqrt(x * x + y * y + z * z) * 3.6, 1)


def scoring_trace_sample(snapshot: Snapshot, driver: Snapshot) -> Snapshot:
    session = snapshot.get("session") or {}
    laps = driver.get("laps")
    lap_number = laps + 1 if isinstance(laps, int) else None
    return {
        "timestamp": snapshot.get("timestamp"),
        "session_time": session.get("current_time"),
        "session_track": session.get("track"),
        "session_type": session.get("session_type"),
        "session_game_phase": session.get("game_phase"),
        "session_game_phase_name": session.get("game_phase_name"),
        "driver_id": driver.get("id"),
        "driver_name": driver.get("driver_name"),
        "vehicle_name": driver.get("vehicle_name"),
        "lap_number": lap_number,
        "lap_distance": driver.get("lap_distance"),
        "lap_percent": driver.get("track_position_percent"),
        "time_seconds": driver.get("current_lap_time"),
        "count_lap_flag": driver.get("count_lap_flag"),
        "count_lap_flag_name": driver.get("count_lap_flag_name"),
        "in_pits": driver.get("in_pits"),
        "pit_state": driver.get("pit_state"),
        "pit_state_name": driver.get("pit_state_name"),
        "finish_status": driver.get("finish_status"),
        "finish_status_name": driver.get("finish_status_name"),
    }