from __future__ import annotations

import json
import sys
import tempfile
import unittest
import ctypes
import copy
import uuid
from ctypes import wintypes
from pathlib import Path

POC_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(POC_ROOT))

from rf2_poc.rf2_shared_memory import (  # noqa: E402
    FILE_MAP_READ,
    MAPPED_BUFFER_WRAPPER_SIZE,
    MAX_MAPPED_VEHICLES,
    SCORING_MAP_NAME,
    TELEMETRY_MAP_NAME,
    SharedMemoryScoringReader,
    SharedMemoryUnavailable,
    candidate_scoring_map_names,
    candidate_telemetry_map_names,
    rF2MappedBufferVersionBlock,
    rF2Scoring,
    rF2Telemetry,
    c_string,
    session_type_name,
)
from rf2_poc.reports import append_sample_to_record, build_report  # noqa: E402
from rf2_poc.server import dashboard_html, is_client_disconnect, read_recordings_safely, read_report_safely, report_html  # noqa: E402
from rf2_poc.sources import MockScoringSource, SessionRecorder, build_source  # noqa: E402
from rf2_poc.viewer import telemetry_viewer_html  # noqa: E402


class MockScoringSourceTests(unittest.TestCase):
    def test_fixture_source_returns_driver_data_and_updates_counter(self) -> None:
        fixture_path = POC_ROOT / "fixtures" / "mock_scoring_snapshot.json"
        source = MockScoringSource(fixture_path)

        first = source.read()
        second = source.read()

        self.assertEqual(first["source"], "mock")
        self.assertEqual(second["update_counter"], first["update_counter"] + 1)
        self.assertGreaterEqual(len(first["drivers"]), 3)
        self.assertEqual(first["session"]["session_type"], "Practice")

    def test_fixture_source_accepts_minimal_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            fixture_path = Path(temp_dir) / "minimal.json"
            fixture_path.write_text(
                json.dumps({"session": {"current_time": 0}, "drivers": []}),
                encoding="utf-8",
            )
            snapshot = MockScoringSource(fixture_path).read()

        self.assertEqual(snapshot["drivers"], [])
        self.assertEqual(snapshot["source"], "mock")

    def test_build_source_enriches_fixture_for_dashboard(self) -> None:
        fixture_path = POC_ROOT / "fixtures" / "mock_scoring_snapshot.json"
        source = build_source("mock", fixture_path)

        snapshot = source.read()

        self.assertIn("field_coverage", snapshot)
        self.assertIn("highlights", snapshot)
        self.assertIn("history", snapshot)
        self.assertEqual(snapshot["telemetry"]["scope"], "fixture")
        self.assertTrue(snapshot["highlights"]["fastest_lap"])
        self.assertTrue(any(item["key"] == "telemetry.throttle" and item["available"] for item in snapshot["field_coverage"]))

    def test_build_source_records_telemetry_samples_to_jsonl(self) -> None:
        fixture_path = POC_ROOT / "fixtures" / "mock_scoring_snapshot.json"
        with tempfile.TemporaryDirectory() as temp_dir:
            source = build_source("mock", fixture_path, telemetry_output_dir=Path(temp_dir))
            snapshot = source.read()
            history = snapshot["history"]["current_session"]
            sample_file = Path(history["telemetry_samples_file"])
            source.close()

            self.assertGreater(history["telemetry_sample_count"], 0)
            self.assertTrue(sample_file.exists())
            first_sample = json.loads(sample_file.read_text(encoding="utf-8").splitlines()[0])

        self.assertIn("lap_distance", first_sample)
        self.assertIn("lap_percent", first_sample)
        self.assertIn("throttle_percent", first_sample)
        self.assertIn("lateral_g", first_sample)

    def test_session_recorder_builds_finalized_report_with_delta_series(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            recorder = SessionRecorder(output_dir=Path(temp_dir), target_hz=50.0)
            for snapshot in build_report_snapshots():
                recorder.record(snapshot)
            recorder.close()

            history = recorder.history()
            completed = history["completed_sessions"][0]
            report = recorder.report(completed["id"])

        self.assertEqual(completed["report_status"], "ready")
        self.assertIsNotNone(report)
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["reference_lap"]["driver_name"], "Setup1")
        self.assertEqual(report["reference_lap"]["lap_time"], 80.0)
        self.assertEqual(report["proper_lap_count"], 2)
        self.assertEqual(report["laps"][0]["lap_classification"], "proper")
        self.assertEqual(len(report["laps"][0]["series"]["speed_kph"]), report["laps"][0]["sample_count"])
        self.assertEqual(report["axis_strategy"], "raw recorded samples at collector frequency")

    def test_report_generation_keeps_full_resolution_samples(self) -> None:
        samples = [
            {
                "lap_percent": round(index * 100.0 / 1999, 4),
                "time_seconds": round(index * 80.0 / 1999, 4),
                "speed_kph": 180.0 + index % 20,
                "throttle_percent": 70.0,
                "count_lap_flag": 2,
            }
            for index in range(2000)
        ]
        record = {
            "id": "long-report",
            "track": "Long Report Track",
            "session_type": "Race",
            "drivers": {
                "1": {
                    "id": 1,
                    "driver_name": "Setup1",
                    "vehicle_name": "Formula Test",
                    "lap_history": [],
                    "telemetry_laps": {
                        "1": {"lap_number": 1, "lap_time": 80.0, "completed": True, "samples": samples}
                    },
                }
            },
        }

        report = build_report(record)

        self.assertIsNotNone(report)
        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["telemetry_sample_count"], 0)
        self.assertEqual(len(report["laps"][0]["raw_lap_percents"]), 2000)
        self.assertEqual(len(report["laps"][0]["series"]["speed_kph"]), 2000)

    def test_partial_fast_lap_is_not_used_as_reference(self) -> None:
        proper_samples = [
            {"lap_percent": 1.0, "time_seconds": 1.0, "speed_kph": 150.0, "count_lap_flag": 2},
            {"lap_percent": 50.0, "time_seconds": 40.0, "speed_kph": 180.0, "count_lap_flag": 2},
            {"lap_percent": 99.0, "time_seconds": 80.0, "speed_kph": 160.0, "count_lap_flag": 2},
        ]
        partial_samples = [
            {"lap_percent": 62.0, "time_seconds": 1.0, "speed_kph": 200.0, "count_lap_flag": 2},
            {"lap_percent": 99.0, "time_seconds": 50.0, "speed_kph": 210.0, "count_lap_flag": 2},
        ]
        record = {
            "id": "partial-fast",
            "track": "Partial Track",
            "session_type": "Race",
            "drivers": {
                "1": {
                    "id": 1,
                    "driver_name": "Setup1",
                    "vehicle_name": "Formula Test",
                    "lap_history": [],
                    "telemetry_laps": {
                        "1:0": {"lap_number": 1, "lap_time": 80.0, "completed": True, "samples": proper_samples},
                        "2:0": {"lap_number": 2, "lap_time": 50.0, "completed": True, "samples": partial_samples},
                    },
                }
            },
        }

        report = build_report(record)

        self.assertEqual(report["status"], "ready")
        self.assertEqual(report["proper_lap_count"], 1)
        self.assertEqual(report["reference_lap"]["lap_time"], 80.0)
        self.assertEqual(len(report["laps"]), 1)
        self.assertEqual(len(report["all_laps"]), 2)
        self.assertEqual(report["all_laps"][1]["lap_classification"], "partial")

    def test_report_with_no_proper_laps_has_no_display_laps(self) -> None:
        record = {
            "id": "no-proper",
            "track": "No Proper Track",
            "session_type": "Practice",
            "drivers": {
                "1": {
                    "id": 1,
                    "driver_name": "Setup1",
                    "vehicle_name": "Formula Test",
                    "lap_history": [],
                    "telemetry_laps": {
                        "1:0": {"lap_number": 1, "lap_time": 60.0, "completed": True, "samples": [{"lap_percent": 60.0, "speed_kph": 180.0}]}
                    },
                }
            },
        }

        report = build_report(record)

        self.assertEqual(report["status"], "no proper telemetry laps")
        self.assertEqual(report["laps"], [])
        self.assertEqual(len(report["all_laps"]), 1)

    def test_append_sample_splits_lap_when_percent_wraps_with_same_lap_number(self) -> None:
        record: dict = {}
        first_sample = {
            "driver_id": 1,
            "driver_name": "Setup1",
            "vehicle_name": "Formula Test",
            "telemetry_update_counter": 1,
            "lap_number": 1,
            "lap_distance": 3000.0,
            "lap_percent": 95.0,
            "speed_kph": 180.0,
        }
        second_sample = dict(first_sample, telemetry_update_counter=2, lap_distance=50.0, lap_percent=1.5, speed_kph=90.0)

        self.assertTrue(append_sample_to_record(record, first_sample))
        self.assertTrue(append_sample_to_record(record, second_sample))

        laps = record["drivers"]["1"]["telemetry_laps"]
        self.assertEqual(len(laps), 2)
        self.assertTrue(laps["1:0"]["completed"])
        self.assertEqual(laps["1:1"]["lap_number"], 2)
        self.assertEqual(laps["1:1"]["samples"][0]["lap_percent"], 1.5)

    def test_recorder_starts_new_folder_for_repeated_finalized_session_key(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            recorder = SessionRecorder(output_dir=Path(temp_dir), target_hz=50.0)
            snapshots = build_report_snapshots()
            for snapshot in snapshots:
                recorder.record(snapshot)
            first_completed = recorder.history()["completed_sessions"][0]

            next_active = copy.deepcopy(snapshots[0])
            next_active["timestamp"] = 4.0
            next_active["session"]["current_time"] = 4.0
            next_active["session"]["game_phase"] = 5
            recorder.record(next_active)
            current = recorder.history()["current_session"]
            recorder.close()

        self.assertFalse(current["finalized"])
        self.assertNotEqual(current["id"], first_completed["id"])
        self.assertNotEqual(
            Path(current["telemetry_samples_file"]).parent,
            Path(first_completed["telemetry_samples_file"]).parent,
        )

    def test_recorder_discards_short_complete_boot_snapshot(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            recorder = SessionRecorder(output_dir=Path(temp_dir), target_hz=50.0)
            snapshot = build_report_snapshots()[0]
            snapshot["session"]["game_phase"] = 8
            snapshot["timestamp"] = 1.0
            recorder.record(snapshot)
            history = recorder.history()
            recorder.close()

            remaining_dirs = [path for path in Path(temp_dir).iterdir() if path.is_dir()]

        self.assertIsNone(history["current_session"])
        self.assertEqual(history["completed_sessions"], [])
        self.assertEqual(remaining_dirs, [])

    def test_recorder_lists_and_loads_stored_reports_after_restart(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            output_dir = Path(temp_dir)
            recorder = SessionRecorder(output_dir=output_dir, target_hz=50.0)
            for snapshot in build_report_snapshots():
                recorder.record(snapshot)
            completed = recorder.history()["completed_sessions"][0]
            recorder.close()

            restarted = SessionRecorder(output_dir=output_dir, target_hz=50.0)
            recordings = restarted.recordings()["recordings"]
            report = restarted.report(completed["id"])
            restarted.close()

        self.assertEqual(recordings[0]["session_id"], completed["id"])
        self.assertIsNotNone(report)
        self.assertEqual(report["session_id"], completed["id"])
        self.assertEqual(report["status"], "ready")

    def test_recordings_api_helper_is_module_scoped(self) -> None:
        source = self.StaticReportSource({})

        self.assertEqual(read_recordings_safely(source), {"recordings": []})

    def test_report_api_helper_is_module_scoped_and_page_selects_driver(self) -> None:
        report = {"session_id": "abc", "status": "ready", "axis": [], "channels": [], "laps": []}
        source = self.StaticReportSource(report)

        self.assertEqual(read_report_safely(source, "abc"), report)
        html = report_html("abc")

        self.assertIn("Driver <select", html)
        self.assertNotIn("Driver lap <select", html)
        self.assertIn("lines.join('\\n')", html)
        self.assertNotIn("lines.join('\n')", html)
        self.assertIn("Waiting for /api/reports response", html)
        self.assertIn("timed out waiting for /api/reports response", html)
        self.assertIn("const timeoutId = setTimeout(() => controller.abort(), 30000)", html)

        dashboard = dashboard_html(1.0, "history")
        self.assertIn("function fmtDateTime", dashboard)
        self.assertIn("Started ${fmtDateTime(session.started_at)}", dashboard)
        self.assertIn("no proper telemetry laps", dashboard)

        viewer = telemetry_viewer_html("abc")
        self.assertIn("const initialSessionId = 'abc'", viewer)
        self.assertIn("/api/recordings", viewer)
        self.assertIn("Fastest Ovrl.", viewer)

    def test_client_disconnect_errors_are_expected(self) -> None:
        self.assertTrue(is_client_disconnect(ConnectionAbortedError()))
        self.assertTrue(is_client_disconnect(ConnectionResetError()))
        self.assertTrue(is_client_disconnect(BrokenPipeError()))
        self.assertFalse(is_client_disconnect(OSError("not a disconnect")))

    class StaticReportSource:
        def __init__(self, report: dict):
            self._report = report

        def read(self) -> dict:
            return {}

        def history(self) -> dict:
            return {}

        def report(self, session_id: str) -> dict:
            return self._report

        def recordings(self) -> dict:
            return {"recordings": []}

        def close(self) -> None:
            return None


class SharedMemoryMappingTests(unittest.TestCase):
    def test_candidate_names_include_dedicated_pid_variants_and_base_name(self) -> None:
        names = candidate_scoring_map_names(pid=12345)

        self.assertEqual(names[0], f"{SCORING_MAP_NAME}12345")
        self.assertIn(f"Global\\{SCORING_MAP_NAME}12345", names)
        self.assertEqual(names[-1], SCORING_MAP_NAME)

    def test_telemetry_candidate_names_include_dedicated_pid_variants_and_base_name(self) -> None:
        names = candidate_telemetry_map_names(pid=12345)

        self.assertEqual(names[0], f"{TELEMETRY_MAP_NAME}12345")
        self.assertIn(f"Global\\{TELEMETRY_MAP_NAME}12345", names)
        self.assertEqual(names[-1], TELEMETRY_MAP_NAME)

    def test_explicit_map_name_is_tried_first(self) -> None:
        names = candidate_scoring_map_names(map_name="CustomMap", pid=42)

        self.assertEqual(names[0], "CustomMap")

    def test_c_string_decodes_null_terminated_bytes(self) -> None:
        raw = (ctypes.c_ubyte * 8)()
        for index, value in enumerate(b"Setup1\0x"):
            raw[index] = value

        self.assertEqual(c_string(raw), "Setup1")

    def test_session_type_mapping(self) -> None:
        self.assertEqual(session_type_name(0), "Test Day")
        self.assertEqual(session_type_name(1), "Practice")
        self.assertEqual(session_type_name(5), "Qualifying")
        self.assertEqual(session_type_name(10), "Race")

    @unittest.skipUnless(sys.platform == "win32", "Windows shared-memory behavior only")
    def test_missing_map_does_not_create_false_live_connection(self) -> None:
        missing_name = f"GearboxTracksideMissing-{uuid.uuid4()}"

        with self.assertRaises(SharedMemoryUnavailable):
            SharedMemoryScoringReader(map_name=missing_name)

    @unittest.skipUnless(sys.platform == "win32", "Windows shared-memory behavior only")
    def test_reader_parses_scoring_memory_map_with_wrapper_offset(self) -> None:
        map_name = f"GearboxTracksideScoring-{uuid.uuid4()}"
        scoring = build_fake_scoring_payload()

        with NamedScoringMap(map_name, scoring, payload_offset=MAPPED_BUFFER_WRAPPER_SIZE):
            snapshot = SharedMemoryScoringReader(map_name=map_name).read_snapshot()

        self.assertEqual(snapshot["source"], "shared-memory")
        self.assertEqual(snapshot["memory_map"], map_name)
        self.assertEqual(snapshot["decode_offset"], MAPPED_BUFFER_WRAPPER_SIZE)
        self.assertEqual(snapshot["session"]["track"], "PoC Test Track")
        self.assertEqual(snapshot["session"]["session_type"], "Practice")
        self.assertEqual(snapshot["session"]["vehicle_count"], 1)
        self.assertEqual(snapshot["drivers"][0]["driver_name"], "Setup9")
        self.assertEqual(snapshot["drivers"][0]["place"], 1)
        self.assertEqual(snapshot["drivers"][0]["best_lap_time"], 81.234)

    @unittest.skipUnless(sys.platform == "win32", "Windows shared-memory behavior only")
    def test_reader_parses_scoring_memory_map_at_zero_offset(self) -> None:
        map_name = f"GearboxTracksideScoring-{uuid.uuid4()}"
        scoring = build_fake_scoring_payload()

        with NamedScoringMap(map_name, scoring, payload_offset=0):
            snapshot = SharedMemoryScoringReader(map_name=map_name).read_snapshot()

        self.assertEqual(snapshot["decode_offset"], 0)
        self.assertEqual(snapshot["session"]["track"], "PoC Test Track")
        self.assertEqual(snapshot["drivers"][0]["driver_name"], "Setup9")

    @unittest.skipUnless(sys.platform == "win32", "Windows shared-memory behavior only")
    def test_reader_joins_telemetry_memory_map_by_vehicle_id(self) -> None:
        scoring_map_name = f"GearboxTracksideScoring-{uuid.uuid4()}"
        telemetry_map_name = f"GearboxTracksideTelemetry-{uuid.uuid4()}"
        scoring = build_fake_scoring_payload()
        telemetry = build_fake_telemetry_payload()

        with NamedScoringMap(scoring_map_name, scoring, payload_offset=MAPPED_BUFFER_WRAPPER_SIZE):
            with NamedTelemetryMap(telemetry_map_name, telemetry, payload_offset=MAPPED_BUFFER_WRAPPER_SIZE):
                snapshot = SharedMemoryScoringReader(
                    map_name=scoring_map_name,
                    telemetry_map_name=telemetry_map_name,
                ).read_snapshot()

        self.assertEqual(snapshot["telemetry"]["status"], "connected")
        self.assertEqual(snapshot["telemetry"]["scope"], "all scoring vehicles")
        self.assertTrue(snapshot["drivers"][0]["telemetry_available"])
        self.assertEqual(snapshot["drivers"][0]["telemetry"]["throttle_percent"], 73.0)
        self.assertEqual(snapshot["drivers"][0]["telemetry"]["brake_percent"], 12.0)
        self.assertEqual(snapshot["drivers"][0]["telemetry"]["gear_label"], "4")


def build_report_snapshots() -> list[dict]:
    snapshots = []
    for timestamp, phase, rows in [
        (1.0, 5, [(1, "Setup1", 0, None, 32.0, 1.0, 1, 1.0), (2, "Setup2", 0, None, 35.0, 1.094, 1, 1.2)]),
        (2.0, 5, [(1, "Setup1", 0, None, 3168.0, 99.0, 1, 78.0), (2, "Setup2", 0, None, 3140.0, 98.125, 1, 82.0)]),
        (3.0, 8, [(1, "Setup1", 1, 80.0, 20.0, 0.625, 2, 1.0), (2, "Setup2", 1, 86.0, 18.0, 0.5625, 2, 1.2)]),
    ]:
        snapshots.append(
            {
                "source": "test",
                "timestamp": timestamp,
                "session": {
                    "track": "Report Test Track",
                    "session_code": 10,
                    "session_type": "Race",
                    "start_time": 100.0,
                    "current_time": timestamp,
                    "game_phase": phase,
                    "lap_distance": 3200.0,
                },
                "telemetry": {"update_counter": int(timestamp * 100)},
                "drivers": [
                    build_report_driver(*row)
                    for row in rows
                ],
            }
        )
    return snapshots


def build_report_driver(
    driver_id: int,
    name: str,
    laps: int,
    last_lap_time: float | None,
    lap_distance: float,
    lap_percent: float,
    telemetry_lap: int,
    current_lap_time: float,
) -> dict:
    return {
        "id": driver_id,
        "driver_name": name,
        "vehicle_name": "Formula Test",
        "laps": laps,
        "place": driver_id,
        "best_lap_time": last_lap_time,
        "last_lap_time": last_lap_time,
        "last_sector_1": 25.0 if last_lap_time else None,
        "last_sector_2_split": 27.0 if last_lap_time else None,
        "last_sector_3": (last_lap_time - 52.0) if last_lap_time else None,
        "current_lap_time": current_lap_time,
        "lap_distance": lap_distance,
        "track_position_percent": lap_percent,
        "finish_status": 1 if laps else 0,
        "finish_status_name": "finished" if laps else "none",
        "count_lap_flag": 2,
        "count_lap_flag_name": "count lap and time",
        "in_pits": False,
        "pit_state": 0,
        "pit_state_name": "none",
        "telemetry": {
            "id": driver_id,
            "lap_number": telemetry_lap,
            "speed_kph": 180.0 + driver_id,
            "throttle_percent": 60.0 + driver_id,
            "brake_percent": 5.0,
            "gear": 4,
            "gear_label": "4",
            "steering_percent": 2.0,
            "g_force": {"lateral": 0.2, "longitudinal": 0.1, "vertical": 1.0, "magnitude": 1.03},
        },
    }


PAGE_READWRITE = 0x0004
FILE_MAP_WRITE = 0x0002
INVALID_HANDLE_VALUE = wintypes.HANDLE(-1).value


class NamedScoringMap:
    def __init__(self, name: str, scoring: rF2Scoring, payload_offset: int):
        self.name = name
        self.scoring = scoring
        self.payload_offset = payload_offset
        self.handle = None
        self.view = None
        self.payload_size = ctypes.sizeof(rF2Scoring)
        self.size = MAPPED_BUFFER_WRAPPER_SIZE + self.payload_size
        self.kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel32.CreateFileMappingW.argtypes = [
            wintypes.HANDLE,
            wintypes.LPVOID,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.LPCWSTR,
        ]
        self.kernel32.CreateFileMappingW.restype = wintypes.HANDLE
        self.kernel32.MapViewOfFile.argtypes = [
            wintypes.HANDLE,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            ctypes.c_size_t,
        ]
        self.kernel32.MapViewOfFile.restype = wintypes.LPVOID
        self.kernel32.UnmapViewOfFile.argtypes = [wintypes.LPCVOID]
        self.kernel32.CloseHandle.argtypes = [wintypes.HANDLE]

    def __enter__(self) -> "NamedScoringMap":
        self.handle = self.kernel32.CreateFileMappingW(
            INVALID_HANDLE_VALUE,
            None,
            PAGE_READWRITE,
            0,
            self.size,
            self.name,
        )
        if not self.handle:
            raise ctypes.WinError(ctypes.get_last_error())

        self.view = self.kernel32.MapViewOfFile(
            self.handle,
            FILE_MAP_READ | FILE_MAP_WRITE,
            0,
            0,
            self.size,
        )
        if not self.view:
            raise ctypes.WinError(ctypes.get_last_error())

        wrapper = rF2MappedBufferVersionBlock()
        wrapper.mVersionUpdateBegin = 7
        wrapper.mVersionUpdateEnd = 7
        ctypes.memmove(int(self.view), ctypes.byref(wrapper), MAPPED_BUFFER_WRAPPER_SIZE)
        ctypes.memmove(
            int(self.view) + self.payload_offset,
            ctypes.byref(self.scoring),
            self.payload_size,
        )
        return self

    def __exit__(self, exc_type, exc, tb) -> None:  # type: ignore[no-untyped-def]
        if self.view:
            self.kernel32.UnmapViewOfFile(self.view)
        if self.handle:
            self.kernel32.CloseHandle(self.handle)


class NamedTelemetryMap(NamedScoringMap):
    def __init__(self, name: str, telemetry: rF2Telemetry, payload_offset: int):
        self.name = name
        self.scoring = telemetry
        self.payload_offset = payload_offset
        self.handle = None
        self.view = None
        self.payload_size = ctypes.sizeof(rF2Telemetry)
        self.size = MAPPED_BUFFER_WRAPPER_SIZE + self.payload_size
        self.kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel32.CreateFileMappingW.argtypes = [
            wintypes.HANDLE,
            wintypes.LPVOID,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.LPCWSTR,
        ]
        self.kernel32.CreateFileMappingW.restype = wintypes.HANDLE
        self.kernel32.MapViewOfFile.argtypes = [
            wintypes.HANDLE,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            ctypes.c_size_t,
        ]
        self.kernel32.MapViewOfFile.restype = wintypes.LPVOID
        self.kernel32.UnmapViewOfFile.argtypes = [wintypes.LPCVOID]
        self.kernel32.CloseHandle.argtypes = [wintypes.HANDLE]


def build_fake_scoring_payload() -> rF2Scoring:
    scoring = rF2Scoring()
    scoring.mVersionUpdateBegin = 11
    scoring.mVersionUpdateEnd = 11
    scoring.mBytesUpdatedHint = ctypes.sizeof(rF2Scoring)
    scoring.mScoringInfo.mSession = 1
    scoring.mScoringInfo.mCurrentET = 123.456
    scoring.mScoringInfo.mEndET = 1800.0
    scoring.mScoringInfo.mLapDist = 3210.0
    scoring.mScoringInfo.mNumVehicles = 1
    scoring.mScoringInfo.mInRealtime = 1
    scoring.mScoringInfo.mAmbientTemp = 21.0
    scoring.mScoringInfo.mTrackTemp = 29.5
    scoring.mScoringInfo.mMaxPlayers = MAX_MAPPED_VEHICLES
    write_c_string(scoring.mScoringInfo.mTrackName, "PoC Test Track")
    write_c_string(scoring.mScoringInfo.mServerName, "PoC Test Server")

    vehicle = scoring.mVehicles[0]
    vehicle.mID = 9
    write_c_string(vehicle.mDriverName, "Setup9")
    write_c_string(vehicle.mVehicleName, "Formula Test")
    write_c_string(vehicle.mVehicleClass, "F1")
    vehicle.mTotalLaps = 4
    vehicle.mPlace = 1
    vehicle.mBestLapTime = 81.234
    vehicle.mLastLapTime = 82.345
    vehicle.mTimeIntoLap = 12.5
    vehicle.mLapDist = 512.0
    vehicle.mServerScored = 1
    return scoring


def build_fake_telemetry_payload() -> rF2Telemetry:
    telemetry = rF2Telemetry()
    telemetry.mVersionUpdateBegin = 13
    telemetry.mVersionUpdateEnd = 13
    telemetry.mBytesUpdatedHint = ctypes.sizeof(rF2Telemetry)
    telemetry.mNumVehicles = 1

    vehicle = telemetry.mVehicles[0]
    vehicle.mID = 9
    vehicle.mElapsedTime = 123.456
    vehicle.mDeltaTime = 0.02
    vehicle.mLapNumber = 4
    vehicle.mLapStartET = 111.0
    write_c_string(vehicle.mVehicleName, "Formula Test")
    write_c_string(vehicle.mTrackName, "PoC Test Track")
    vehicle.mGear = 4
    vehicle.mEngineRPM = 11234.0
    vehicle.mUnfilteredThrottle = 0.73
    vehicle.mUnfilteredBrake = 0.12
    vehicle.mUnfilteredSteering = -0.25
    vehicle.mLocalVel.x = 0.0
    vehicle.mLocalVel.y = 0.0
    vehicle.mLocalVel.z = 58.0
    vehicle.mLocalAccel.x = 3.2
    vehicle.mLocalAccel.y = 9.8
    vehicle.mLocalAccel.z = -1.1
    vehicle.mPos.x = 512.0
    vehicle.mPos.y = 0.0
    vehicle.mPos.z = -128.0
    return telemetry


def write_c_string(buffer, value: str) -> None:  # type: ignore[no-untyped-def]
    encoded = value.encode("utf-8")[: len(buffer) - 1]
    for index, byte in enumerate(encoded):
        buffer[index] = byte
    buffer[len(encoded)] = 0


if __name__ == "__main__":
    unittest.main()