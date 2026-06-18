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

        session = snapshot.setdefault("session", {})
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

        return snapshot


class SharedMemoryScoringSource:
    def __init__(self, map_name: str | None = None, pid: int | None = None):
        self._reader = SharedMemoryScoringReader(map_name=map_name, pid=pid)

    def read(self) -> Snapshot:
        return self._reader.read_snapshot()


class AutoScoringSource:
    def __init__(self, fixture_path: Path, map_name: str | None = None, pid: int | None = None):
        self._fallback = MockScoringSource(fixture_path)
        try:
            self._live: SharedMemoryScoringSource | None = SharedMemoryScoringSource(
                map_name=map_name,
                pid=pid,
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


def build_source(
    source_kind: str,
    fixture_path: Path,
    map_name: str | None = None,
    pid: int | None = None,
) -> ScoringSource:
    if source_kind == "mock":
        return MockScoringSource(fixture_path)
    if source_kind == "shared-memory":
        return SharedMemoryScoringSource(map_name=map_name, pid=pid)
    if source_kind == "auto":
        return AutoScoringSource(fixture_path=fixture_path, map_name=map_name, pid=pid)
    raise ValueError(f"Unsupported source kind: {source_kind}")