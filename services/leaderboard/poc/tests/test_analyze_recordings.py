from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

POC_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(POC_ROOT))

from rf2_poc.analysis import analyze_session, analyze_paths  # noqa: E402


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
