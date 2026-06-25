from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from rf2_poc.telemetry_capture import TelemetryWorkerOptions, run_telemetry_worker


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the rFactor 2 telemetry-only shared-memory recorder.")
    parser.add_argument("--output-file", type=Path, required=True, help="Compact raw telemetry JSONL output file.")
    parser.add_argument("--target-hz", type=float, default=50.0, help="Telemetry read-loop target frequency.")
    parser.add_argument("--map-name", help="Exact telemetry memory-map name to read.")
    parser.add_argument("--pid", type=int, help="Dedicated.exe process ID for PID-suffixed telemetry maps.")
    parser.add_argument("--duration-seconds", type=float, help="Optional fixed capture duration for diagnostics.")
    parser.add_argument("--flush-frames", type=int, default=10, help="Flush output after this many compact frames.")
    parser.add_argument("--status-file", type=Path, help="Write capture status and cadence metrics to this JSON file.")
    parser.add_argument("--status-interval-seconds", type=float, default=2.0, help="Status JSON update interval.")
    parser.add_argument("--stop-file", type=Path, help="Stop cleanly when this file appears.")
    parser.add_argument("--priority", choices=("normal", "high"), default="high", help="Windows process priority class.")
    parser.add_argument("--affinity-mask", type=lambda value: int(value, 0), help="Optional Windows process affinity mask.")
    parser.add_argument("--write-repeated-updates", action="store_true", help="Store repeated update counters as raw frames.")
    parser.add_argument("--stable-attempts", type=int, default=2, help="Attempts to avoid torn shared-memory reads.")
    return parser.parse_args()


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
        force=True,
    )
    args = parse_args()
    raise SystemExit(
        run_telemetry_worker(
            TelemetryWorkerOptions(
                output_file=args.output_file,
                target_hz=args.target_hz,
                map_name=args.map_name,
                pid=args.pid,
                duration_seconds=args.duration_seconds,
                flush_frames=args.flush_frames,
                status_file=args.status_file,
                status_interval_seconds=args.status_interval_seconds,
                stop_file=args.stop_file,
                priority=args.priority,
                affinity_mask=args.affinity_mask,
                write_repeated_updates=args.write_repeated_updates,
                stable_attempts=args.stable_attempts,
            )
        )
    )


if __name__ == "__main__":
    main()