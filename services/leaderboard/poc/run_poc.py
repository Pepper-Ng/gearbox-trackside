from __future__ import annotations

import argparse
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
        "--map-name",
        help="Exact Windows memory-map name to read, for example '$rFactor2SMMP_Scoring$' or 'Global\\$rFactor2SMMP_Scoring$12345'.",
    )
    parser.add_argument(
        "--pid",
        type=int,
        help="Dedicated.exe process ID. When supplied, the reader tries dedicated-server scoring map name variants.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    source = build_source(
        source_kind=args.source,
        fixture_path=args.fixture,
        map_name=args.map_name,
        pid=args.pid,
    )
    run_server(
        source=source,
        host=args.host,
        port=args.port,
        poll_seconds=args.poll_seconds,
    )


if __name__ == "__main__":
    main()