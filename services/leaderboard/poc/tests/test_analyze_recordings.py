from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

POC_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(POC_ROOT))

from rf2_poc.analysis import analyze_paths, analyze_raw_session, analyze_session  # noqa: E402


class AnalyzeRecordingsTests(unittest.TestCase):
    def test_analyze_fixture_session_has_expected_sample_count_and_lap_counts(self) -> None:
        fixture_dir = POC_ROOT / "tests" / "data" / "bahrain-gp-2014-practice-ac00312535"
        analysis = analyze_session(fixture_dir / "telemetry_samples.jsonl")

        self.assertEqual(analysis["session_id"], "bahrain-gp-2014-practice-ac00312535")
        self.assertEqual(analysis["sample_count"], 49060)
        self.assertEqual(analysis["proper_lap_count"], 24)
        self.assertEqual(analysis["excluded_lap_count"], 10)
        self.assertGreaterEqual(len(analysis["driver_summaries"]), 1)
        self.assertIsNotNone(analysis["effective_sample_rate_hz"])

    def test_analyze_paths_accepts_session_directory_and_returns_sessions(self) -> None:
        fixture_dir = POC_ROOT / "tests" / "data" / "bahrain-gp-2014-practice-ac00312535"
        result = analyze_paths([fixture_dir])

        self.assertEqual(len(result["sessions"]), 1)
        self.assertEqual(result["sessions"][0]["session_id"], "bahrain-gp-2014-practice-ac00312535")

    def test_analyze_paths_writes_json_output_when_requested(self) -> None:
        fixture_dir = POC_ROOT / "tests" / "data" / "bahrain-gp-2014-practice-ac00312535"
        analysis = analyze_session(fixture_dir / "telemetry_samples.jsonl")
        with tempfile.TemporaryDirectory() as temp_dir:
            output_file = Path(temp_dir) / "analysis.json"
            output_file.write_text(json.dumps(analysis), encoding="utf-8")
            loaded = json.loads(output_file.read_text(encoding="utf-8"))

        self.assertEqual(loaded["session_id"], analysis["session_id"])

    def test_analyze_raw_telemetry_file_reports_source_cadence(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            raw_file = Path(temp_dir) / "telemetry_raw.jsonl"
            raw_file.write_text(
                "\n".join(
                    json.dumps(item, separators=(",", ":"))
                    for item in [
                        {"type": "header", "schema": "gearbox-trackside.telemetry-raw.v1"},
                        {"type": "frame", "t": 10.0, "u": 1, "v": [raw_vehicle_row(9, 1, 100.0)]},
                        {"type": "frame", "t": 10.02, "u": 2, "v": [raw_vehicle_row(9, 1, 100.02)]},
                        {"type": "frame", "t": 10.04, "u": 3, "v": [raw_vehicle_row(9, 1, 100.04)]},
                    ]
                )
                + "\n",
                encoding="utf-8",
            )

            analysis = analyze_raw_session(raw_file)
            paths_result = analyze_paths([Path(temp_dir)])

        self.assertEqual(analysis["status"], "raw telemetry only")
        self.assertEqual(analysis["raw_frame_count"], 3)
        self.assertEqual(analysis["sample_count"], 3)
        self.assertEqual(analysis["driver_summaries"][0]["min_sample_interval_seconds"], 0.02)
        self.assertEqual(paths_result["sessions"][0]["status"], "raw telemetry only")

    def test_quality_separates_preservation_from_channel_change_cadence(self) -> None:
        frames = [{"type": "header", "schema": "gearbox-trackside.telemetry-raw.v1"}]
        for index in range(10):
            throttle = 0.2 if index < 4 else (0.4 if index < 8 else 0.6)
            frames.append(
                {
                    "type": "frame",
                    "t": round(20.0 + index * 0.02, 4),
                    "u": index + 1,
                    "r": 0.0004,
                    "v": [raw_vehicle_row(9, 1, 200.0 + index * 0.02, throttle=throttle, steering=index / 10.0)],
                }
            )
        with tempfile.TemporaryDirectory() as temp_dir:
            raw_file = Path(temp_dir) / "telemetry_raw.jsonl"
            raw_file.write_text("\n".join(json.dumps(frame, separators=(",", ":")) for frame in frames) + "\n", encoding="utf-8")

            analysis = analyze_raw_session(raw_file, target_hz=50.0, minimum_hz=45.0)

        driver = analysis["driver_summaries"][0]
        self.assertEqual(analysis["effective_sample_rate_hz"], 50.0)
        self.assertEqual(analysis["quality_summary"]["status"], "pass")
        self.assertEqual(analysis["quality_summary"]["basis"], "raw_frames_preservation")
        self.assertEqual(analysis["preservation_summary"], analysis["quality_summary"])
        self.assertEqual(analysis["channel_quality_summary"]["status"], "fail")
        self.assertEqual(analysis["channel_quality_summary"]["basis"], "drivers_observed_channel_changes")
        self.assertEqual(driver["weakest_channel_key"], "throttle_percent")
        self.assertLess(driver["weakest_channel_observed_rate_hz"], 45.0)


def raw_vehicle_row(
    driver_id: int,
    lap_number: int,
    elapsed_time: float,
    throttle: float = 0.8,
    brake: float = 0.1,
    steering: float = -0.2,
) -> list:
    return [
        driver_id,
        lap_number,
        elapsed_time,
        99.0,
        0.02,
        4,
        throttle,
        brake,
        steering,
        0.0,
        0.0,
        50.0,
        3.0,
        9.8,
        -1.0,
        "Formula Test",
        "PoC Track",
    ]
