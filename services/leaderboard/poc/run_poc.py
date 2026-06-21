from __future__ import annotations

import argparse
import logging
import sys
from logging.handlers import RotatingFileHandler
from pathlib import Path

from rf2_poc.server import run_server
from rf2_poc.sources import build_source


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run the Phase 0A rFactor 2 live-data browser PoC."
    )
    parser.add_argument(
        "--source",
        choices=("mock", "shared-memory", "auto"),
        default="mock",
        help="Data source. Use 'mock' without rFactor 2, 'shared-memory' on an rF2 host, or 'auto' to try live then fall back to mock.",
    )
    parser.add_argument(
        "--fixture",
        type=Path,
        default=Path(__file__).parent / "fixtures" / "mock_scoring_snapshot.json",
        help="Fixture JSON used by mock mode or auto fallback.",
    )
    parser.add_argument("--host", default="127.0.0.1", help="HTTP bind host.")
    parser.add_argument("--port", type=int, default=8877, help="HTTP bind port.")
    parser.add_argument(
        "--poll-seconds",
        type=float,
        default=1.0,
        help="Browser polling interval for /api/snapshot.",
    )
    parser.add_argument(
        "--telemetry-record-hz",
        type=float,
        default=50.0,
        help="Background telemetry recording target rate. Use 0 to record only when the browser/API asks for a snapshot.",
    )
    parser.add_argument(
        "--telemetry-output-dir",
        type=Path,
        default=Path(__file__).parent / "telemetry-recordings",
        help="Directory for runtime telemetry JSONL samples and finalized report JSON files.",
    )
    parser.add_argument(
        "--log-dir",
        type=Path,
        default=Path(__file__).parent / "logs",
        help="Directory for rotating PoC log files.",
    )
    parser.add_argument(
        "--map-name",
        help="Exact Windows memory-map name to read, for example '$rFactor2SMMP_Scoring$' or 'Global\\$rFactor2SMMP_Scoring$12345'.",
    )
    parser.add_argument(
        "--telemetry-map-name",
        help="Exact telemetry memory-map name to read, for example '$rFactor2SMMP_Telemetry$' or 'Global\\$rFactor2SMMP_Telemetry$12345'.",
    )
    parser.add_argument(
        "--pid",
        type=int,
        help="Dedicated.exe process ID. When supplied, the reader tries dedicated-server scoring map name variants.",
    )
    return parser.parse_args()


def configure_logging(log_dir: Path) -> None:
    log_dir.mkdir(parents=True, exist_ok=True)
    log_file = log_dir / "trackside-poc.log"
    formatter = logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s")
    console = logging.StreamHandler(sys.stdout)
    console.setLevel(logging.INFO)
    console.setFormatter(formatter)
    file_handler = RotatingFileHandler(
        log_file,
        maxBytes=2_000_000,
        backupCount=5,
        encoding="utf-8",
    )
    file_handler.setLevel(logging.INFO)
    file_handler.setFormatter(formatter)
    logging.basicConfig(level=logging.INFO, handlers=[console, file_handler], force=True)
    logging.getLogger("rf2_poc.server").info("Logging to %s", log_file)


def main() -> None:
    args = parse_args()
    configure_logging(args.log_dir)
    source = build_source(
        source_kind=args.source,
        fixture_path=args.fixture,
        map_name=args.map_name,
        pid=args.pid,
        telemetry_map_name=args.telemetry_map_name,
        telemetry_output_dir=args.telemetry_output_dir,
        telemetry_record_hz=args.telemetry_record_hz,
    )
    run_server(
        source=source,
        host=args.host,
        port=args.port,
        poll_seconds=args.poll_seconds,
    )


if __name__ == "__main__":
    main()