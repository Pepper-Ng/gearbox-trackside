from __future__ import annotations

import ctypes
import os
import time
from ctypes import wintypes
from typing import Any


SCORING_MAP_NAME = "$rFactor2SMMP_Scoring$"
MAX_MAPPED_VEHICLES = 128
FILE_MAP_READ = 0x0004


class SharedMemoryUnavailable(RuntimeError):
    pass


class rF2Vec3(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("x", ctypes.c_double),
        ("y", ctypes.c_double),
        ("z", ctypes.c_double),
    ]


class rF2ScoringInfo(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mTrackName", ctypes.c_ubyte * 64),
        ("mSession", ctypes.c_int),
        ("mCurrentET", ctypes.c_double),
        ("mEndET", ctypes.c_double),
        ("mMaxLaps", ctypes.c_int),
        ("mLapDist", ctypes.c_double),
        ("pointer1", ctypes.c_ubyte * 8),
        ("mNumVehicles", ctypes.c_int),
        ("mGamePhase", ctypes.c_ubyte),
        ("mYellowFlagState", ctypes.c_ubyte),
        ("mSectorFlag", ctypes.c_ubyte * 3),
        ("mStartLight", ctypes.c_ubyte),
        ("mNumRedLights", ctypes.c_ubyte),
        ("mInRealtime", ctypes.c_ubyte),
        ("mPlayerName", ctypes.c_ubyte * 32),
        ("mPlrFileName", ctypes.c_ubyte * 64),
        ("mDarkCloud", ctypes.c_double),
        ("mRaining", ctypes.c_double),
        ("mAmbientTemp", ctypes.c_double),
        ("mTrackTemp", ctypes.c_double),
        ("mWind", rF2Vec3),
        ("mMinPathWetness", ctypes.c_double),
        ("mMaxPathWetness", ctypes.c_double),
        ("mGameMode", ctypes.c_ubyte),
        ("mIsPasswordProtected", ctypes.c_ubyte),
        ("mServerPort", ctypes.c_short),
        ("mServerPublicIP", ctypes.c_int),
        ("mMaxPlayers", ctypes.c_int),
        ("mServerName", ctypes.c_ubyte * 32),
        ("mStartET", ctypes.c_float),
        ("mAvgPathWetness", ctypes.c_double),
        ("mExpansion", ctypes.c_ubyte * 200),
        ("pointer2", ctypes.c_ubyte * 8),
    ]


class rF2MappedBufferVersionBlock(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mVersionUpdateBegin", ctypes.c_uint),
        ("mVersionUpdateEnd", ctypes.c_uint),
    ]


class rF2VehicleScoring(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mID", ctypes.c_int),
        ("mDriverName", ctypes.c_ubyte * 32),
        ("mVehicleName", ctypes.c_ubyte * 64),
        ("mTotalLaps", ctypes.c_short),
        ("mSector", ctypes.c_ubyte),
        ("mFinishStatus", ctypes.c_ubyte),
        ("mLapDist", ctypes.c_double),
        ("mPathLateral", ctypes.c_double),
        ("mTrackEdge", ctypes.c_double),
        ("mBestSector1", ctypes.c_double),
        ("mBestSector2", ctypes.c_double),
        ("mBestLapTime", ctypes.c_double),
        ("mLastSector1", ctypes.c_double),
        ("mLastSector2", ctypes.c_double),
        ("mLastLapTime", ctypes.c_double),
        ("mCurSector1", ctypes.c_double),
        ("mCurSector2", ctypes.c_double),
        ("mNumPitstops", ctypes.c_short),
        ("mNumPenalties", ctypes.c_short),
        ("mIsPlayer", ctypes.c_ubyte),
        ("mControl", ctypes.c_ubyte),
        ("mInPits", ctypes.c_ubyte),
        ("mPlace", ctypes.c_ubyte),
        ("mVehicleClass", ctypes.c_ubyte * 32),
        ("mTimeBehindNext", ctypes.c_double),
        ("mLapsBehindNext", ctypes.c_int),
        ("mTimeBehindLeader", ctypes.c_double),
        ("mLapsBehindLeader", ctypes.c_int),
        ("mLapStartET", ctypes.c_double),
        ("mPos", rF2Vec3),
        ("mLocalVel", rF2Vec3),
        ("mLocalAccel", rF2Vec3),
        ("mOri", rF2Vec3 * 3),
        ("mLocalRot", rF2Vec3),
        ("mLocalRotAccel", rF2Vec3),
        ("mHeadlights", ctypes.c_ubyte),
        ("mPitState", ctypes.c_ubyte),
        ("mServerScored", ctypes.c_ubyte),
        ("mIndividualPhase", ctypes.c_ubyte),
        ("mQualification", ctypes.c_int),
        ("mTimeIntoLap", ctypes.c_double),
        ("mEstimatedLapTime", ctypes.c_double),
        ("mPitGroup", ctypes.c_ubyte * 24),
        ("mFlag", ctypes.c_ubyte),
        ("mUnderYellow", ctypes.c_ubyte),
        ("mCountLapFlag", ctypes.c_ubyte),
        ("mInGarageStall", ctypes.c_ubyte),
        ("mUpgradePack", ctypes.c_ubyte * 16),
        ("mPitLapDist", ctypes.c_float),
        ("mBestLapSector1", ctypes.c_float),
        ("mBestLapSector2", ctypes.c_float),
        ("mExpansion", ctypes.c_ubyte * 48),
    ]


class rF2Scoring(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mVersionUpdateBegin", ctypes.c_int),
        ("mVersionUpdateEnd", ctypes.c_int),
        ("mBytesUpdatedHint", ctypes.c_int),
        ("mScoringInfo", rF2ScoringInfo),
        ("mVehicles", rF2VehicleScoring * MAX_MAPPED_VEHICLES),
    ]


MAPPED_BUFFER_WRAPPER_SIZE = ctypes.sizeof(rF2MappedBufferVersionBlock)


def candidate_scoring_map_names(
    map_name: str | None = None,
    pid: int | None = None,
) -> list[str]:
    names: list[str] = []
    if map_name:
        names.append(map_name)
    if pid is not None:
        names.append(f"{SCORING_MAP_NAME}{pid}")
        names.append(f"Global\\{SCORING_MAP_NAME}{pid}")
    names.append(SCORING_MAP_NAME)

    unique_names: list[str] = []
    for name in names:
        if name not in unique_names:
            unique_names.append(name)
    return unique_names


class SharedMemoryScoringReader:
    def __init__(self, map_name: str | None = None, pid: int | None = None):
        if os.name != "nt":
            raise SharedMemoryUnavailable("rFactor 2 shared-memory reading is Windows-only.")

        configure_win32_api()

        self.map_names = candidate_scoring_map_names(map_name=map_name, pid=pid)
        self.map_name = self._choose_map_name()

    def _choose_map_name(self) -> str:
        errors: list[str] = []
        for name in self.map_names:
            try:
                self._read_map_bytes(name)
                return name
            except Exception as exc:
                errors.append(f"{name}: {exc}")
        raise SharedMemoryUnavailable("Could not open any scoring memory map. Tried: " + "; ".join(errors))

    def read_snapshot(self) -> dict[str, Any]:
        try:
            buffer = self._read_map_bytes(self.map_name)
        except Exception as exc:
            raise SharedMemoryUnavailable(f"Could not read scoring memory map '{self.map_name}': {exc}") from exc

        scoring, decode_offset = decode_best_scoring_payload(buffer)
        info = scoring.mScoringInfo
        raw_vehicle_count = int(info.mNumVehicles)
        scan_limit = raw_vehicle_count if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES else MAX_MAPPED_VEHICLES
        drivers = [
            driver
            for index in range(scan_limit)
            if (driver := vehicle_to_dict(scoring.mVehicles[index])) is not None
        ]
        drivers.sort(key=lambda driver: driver.get("place") or 999)

        return {
            "source": "shared-memory",
            "status": "connected",
            "memory_map": self.map_name,
            "decode_offset": decode_offset,
            "timestamp": time.time(),
            "update_counter": int(scoring.mVersionUpdateEnd),
            "version_begin": int(scoring.mVersionUpdateBegin),
            "version_end": int(scoring.mVersionUpdateEnd),
            "bytes_updated_hint": int(scoring.mBytesUpdatedHint),
            "session": {
                "track": c_string(info.mTrackName),
                "session_code": int(info.mSession),
                "session_type": session_type_name(int(info.mSession)),
                "current_time": none_if_negative(float(info.mCurrentET)),
                "end_time": none_if_negative(float(info.mEndET)),
                "max_laps": int(info.mMaxLaps),
                "lap_distance": none_if_negative(float(info.mLapDist)),
                "vehicle_count": len(drivers),
                "raw_vehicle_count": raw_vehicle_count,
                "game_phase": int(info.mGamePhase),
                "in_realtime": bool(info.mInRealtime),
                "player_name": c_string(info.mPlayerName),
                "server_name": c_string(info.mServerName),
                "ambient_temp": none_if_unreasonable(float(info.mAmbientTemp)),
                "track_temp": none_if_unreasonable(float(info.mTrackTemp)),
                "raining": clamp_01(float(info.mRaining)),
                "dark_cloud": clamp_01(float(info.mDarkCloud)),
                "game_mode": int(info.mGameMode),
                "server_port": int(info.mServerPort),
                "max_players": int(info.mMaxPlayers),
            },
            "drivers": drivers,
        }

    def _read_map_bytes(self, name: str) -> bytes:
        size = ctypes.sizeof(rF2Scoring)
        mapped_size = MAPPED_BUFFER_WRAPPER_SIZE + size
        handle = _KERNEL32.OpenFileMappingW(FILE_MAP_READ, False, name)
        if not handle:
            raise SharedMemoryUnavailable(last_win32_error(f"OpenFileMappingW({name!r}) failed"))

        view = None
        try:
            view = _KERNEL32.MapViewOfFile(handle, FILE_MAP_READ, 0, 0, mapped_size)
            read_size = mapped_size
            if not view:
                view = _KERNEL32.MapViewOfFile(handle, FILE_MAP_READ, 0, 0, size)
                read_size = size
            if not view:
                raise SharedMemoryUnavailable(last_win32_error(f"MapViewOfFile({name!r}) failed"))

            buffer = (ctypes.c_ubyte * read_size)()
            ctypes.memmove(buffer, view, read_size)
            return bytes(buffer)
        finally:
            if view:
                _KERNEL32.UnmapViewOfFile(view)
            _KERNEL32.CloseHandle(handle)


def decode_best_scoring_payload(buffer: bytes) -> tuple[rF2Scoring, int]:
    size = ctypes.sizeof(rF2Scoring)
    candidates: list[tuple[int, rF2Scoring, int]] = []
    for offset in (0, MAPPED_BUFFER_WRAPPER_SIZE):
        if len(buffer) < offset + size:
            continue
        scoring = rF2Scoring.from_buffer_copy(buffer[offset : offset + size])
        candidates.append((score_decoded_scoring(scoring), scoring, offset))

    if not candidates:
        raise SharedMemoryUnavailable("Scoring memory map is smaller than the expected rF2Scoring payload.")

    _, scoring, offset = max(candidates, key=lambda item: item[0])
    return scoring, offset


def score_decoded_scoring(scoring: rF2Scoring) -> int:
    info = scoring.mScoringInfo
    score = 0
    raw_vehicle_count = int(info.mNumVehicles)
    session_code = int(info.mSession)
    track_name = c_string(info.mTrackName)

    if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES:
        score += 5
    if 0 <= session_code <= 13:
        score += 5
    if is_plausible_text(track_name):
        score += 3
    if none_if_unreasonable(float(info.mAmbientTemp)) is not None:
        score += 1
    if none_if_unreasonable(float(info.mTrackTemp)) is not None:
        score += 1

    scan_limit = raw_vehicle_count if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES else MAX_MAPPED_VEHICLES
    valid_driver_count = sum(
        1 for index in range(scan_limit) if is_probable_vehicle(scoring.mVehicles[index])
    )
    score += min(valid_driver_count, 16) * 2
    return score


def vehicle_to_dict(vehicle: rF2VehicleScoring) -> dict[str, Any] | None:
    if not is_probable_vehicle(vehicle):
        return None

    return {
        "id": int(vehicle.mID),
        "driver_name": c_string(vehicle.mDriverName),
        "vehicle_name": c_string(vehicle.mVehicleName),
        "vehicle_class": c_string(vehicle.mVehicleClass),
        "laps": int(vehicle.mTotalLaps),
        "place": int(vehicle.mPlace) or None,
        "sector": int(vehicle.mSector),
        "finish_status": int(vehicle.mFinishStatus),
        "best_lap_time": none_if_not_timed(float(vehicle.mBestLapTime)),
        "last_lap_time": none_if_not_timed(float(vehicle.mLastLapTime)),
        "current_lap_time": none_if_not_timed(float(vehicle.mTimeIntoLap)),
        "estimated_lap_time": none_if_not_timed(float(vehicle.mEstimatedLapTime)),
        "best_sector_1": none_if_not_timed(float(vehicle.mBestSector1)),
        "best_sector_2": none_if_not_timed(float(vehicle.mBestSector2)),
        "last_sector_1": none_if_not_timed(float(vehicle.mLastSector1)),
        "last_sector_2": none_if_not_timed(float(vehicle.mLastSector2)),
        "current_sector_1": none_if_not_timed(float(vehicle.mCurSector1)),
        "current_sector_2": none_if_not_timed(float(vehicle.mCurSector2)),
        "lap_distance": none_if_negative(float(vehicle.mLapDist)),
        "is_player": bool(vehicle.mIsPlayer),
        "control": int(vehicle.mControl),
        "in_pits": bool(vehicle.mInPits),
        "server_scored": bool(vehicle.mServerScored),
        "qualification": int(vehicle.mQualification),
        "time_behind_next": none_if_not_timed(float(vehicle.mTimeBehindNext)),
        "time_behind_leader": none_if_not_timed(float(vehicle.mTimeBehindLeader)),
        "laps_behind_next": int(vehicle.mLapsBehindNext),
        "laps_behind_leader": int(vehicle.mLapsBehindLeader),
        "position": {
            "x": round(float(vehicle.mPos.x), 3),
            "y": round(float(vehicle.mPos.y), 3),
            "z": round(float(vehicle.mPos.z), 3),
        },
    }


def is_probable_vehicle(vehicle: rF2VehicleScoring) -> bool:
    driver_name = c_string(vehicle.mDriverName)
    if not is_plausible_text(driver_name):
        return False
    place = int(vehicle.mPlace)
    laps = int(vehicle.mTotalLaps)
    sector = int(vehicle.mSector)
    if place < 0 or place > MAX_MAPPED_VEHICLES:
        return False
    if laps < 0 or laps > 10000:
        return False
    if sector not in (0, 1, 2):
        return False
    return True


def is_plausible_text(value: str) -> bool:
    if not value:
        return False
    printable_count = sum(1 for char in value if char.isprintable())
    return printable_count == len(value)


def c_string(value: Any) -> str:
    raw = bytes(value).split(b"\0", 1)[0]
    for encoding in ("utf-8", "cp1252"):
        try:
            return raw.decode(encoding).strip()
        except UnicodeDecodeError:
            continue
    return raw.decode("utf-8", errors="ignore").strip()


def session_type_name(session_code: int) -> str:
    if session_code == 0:
        return "Test Day"
    if 1 <= session_code <= 4:
        return "Practice"
    if 5 <= session_code <= 8:
        return "Qualifying"
    if session_code == 9:
        return "Warmup"
    if 10 <= session_code <= 13:
        return "Race"
    return f"Unknown ({session_code})"


def none_if_not_timed(value: float) -> float | None:
    if value <= 0 or value > 24 * 60 * 60:
        return None
    return round(value, 3)


def none_if_negative(value: float) -> float | None:
    if value < 0:
        return None
    return round(value, 3)


def none_if_unreasonable(value: float) -> float | None:
    if value < -100 or value > 150:
        return None
    return round(value, 2)


def clamp_01(value: float) -> float | None:
    if value < 0 or value > 1:
        return None
    return round(value, 3)


_KERNEL32 = ctypes.WinDLL("kernel32", use_last_error=True) if os.name == "nt" else None


def configure_win32_api() -> None:
    if _KERNEL32 is None:
        return
    _KERNEL32.OpenFileMappingW.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR]
    _KERNEL32.OpenFileMappingW.restype = wintypes.HANDLE
    _KERNEL32.MapViewOfFile.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.DWORD, wintypes.DWORD, ctypes.c_size_t]
    _KERNEL32.MapViewOfFile.restype = wintypes.LPVOID
    _KERNEL32.UnmapViewOfFile.argtypes = [wintypes.LPCVOID]
    _KERNEL32.UnmapViewOfFile.restype = wintypes.BOOL
    _KERNEL32.CloseHandle.argtypes = [wintypes.HANDLE]
    _KERNEL32.CloseHandle.restype = wintypes.BOOL


def last_win32_error(prefix: str) -> str:
    error_code = ctypes.get_last_error()
    if not error_code:
        return prefix
    return f"{prefix}: Windows error {error_code}"