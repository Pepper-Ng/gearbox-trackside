from __future__ import annotations

import ctypes
import os
import time
from ctypes import wintypes
from typing import Any


SCORING_MAP_NAME = "$rFactor2SMMP_Scoring$"
TELEMETRY_MAP_NAME = "$rFactor2SMMP_Telemetry$"
MAX_MAPPED_VEHICLES = 128
FILE_MAP_READ = 0x0004
STANDARD_GRAVITY = 9.80665


class SharedMemoryUnavailable(RuntimeError):
    pass


class rF2Vec3(ctypes.Structure):
    _pack_ = 4
    _fields_ = [("x", ctypes.c_double), ("y", ctypes.c_double), ("z", ctypes.c_double)]


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
        ("mYellowFlagState", ctypes.c_byte),
        ("mSectorFlag", ctypes.c_byte * 3),
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
        ("mServerPort", ctypes.c_ushort),
        ("mServerPublicIP", ctypes.c_uint),
        ("mMaxPlayers", ctypes.c_int),
        ("mServerName", ctypes.c_ubyte * 32),
        ("mStartET", ctypes.c_float),
        ("mAvgPathWetness", ctypes.c_double),
        ("mExpansion", ctypes.c_ubyte * 200),
        ("pointer2", ctypes.c_ubyte * 8),
    ]


class rF2MappedBufferVersionBlock(ctypes.Structure):
    _pack_ = 4
    _fields_ = [("mVersionUpdateBegin", ctypes.c_uint), ("mVersionUpdateEnd", ctypes.c_uint)]


class rF2Wheel(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mSuspensionDeflection", ctypes.c_double),
        ("mRideHeight", ctypes.c_double),
        ("mSuspForce", ctypes.c_double),
        ("mBrakeTemp", ctypes.c_double),
        ("mBrakePressure", ctypes.c_double),
        ("mRotation", ctypes.c_double),
        ("mLateralPatchVel", ctypes.c_double),
        ("mLongitudinalPatchVel", ctypes.c_double),
        ("mLateralGroundVel", ctypes.c_double),
        ("mLongitudinalGroundVel", ctypes.c_double),
        ("mCamber", ctypes.c_double),
        ("mLateralForce", ctypes.c_double),
        ("mLongitudinalForce", ctypes.c_double),
        ("mTireLoad", ctypes.c_double),
        ("mGripFract", ctypes.c_double),
        ("mPressure", ctypes.c_double),
        ("mTemperature", ctypes.c_double * 3),
        ("mWear", ctypes.c_double),
        ("mTerrainName", ctypes.c_ubyte * 16),
        ("mSurfaceType", ctypes.c_ubyte),
        ("mFlat", ctypes.c_ubyte),
        ("mDetached", ctypes.c_ubyte),
        ("mStaticUndeflectedRadius", ctypes.c_ubyte),
        ("mVerticalTireDeflection", ctypes.c_double),
        ("mWheelYLocation", ctypes.c_double),
        ("mToe", ctypes.c_double),
        ("mTireCarcassTemperature", ctypes.c_double),
        ("mTireInnerLayerTemperature", ctypes.c_double * 3),
        ("mExpansion", ctypes.c_ubyte * 24),
    ]


class rF2VehicleTelemetry(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mID", ctypes.c_int),
        ("mDeltaTime", ctypes.c_double),
        ("mElapsedTime", ctypes.c_double),
        ("mLapNumber", ctypes.c_int),
        ("mLapStartET", ctypes.c_double),
        ("mVehicleName", ctypes.c_ubyte * 64),
        ("mTrackName", ctypes.c_ubyte * 64),
        ("mPos", rF2Vec3),
        ("mLocalVel", rF2Vec3),
        ("mLocalAccel", rF2Vec3),
        ("mOri", rF2Vec3 * 3),
        ("mLocalRot", rF2Vec3),
        ("mLocalRotAccel", rF2Vec3),
        ("mGear", ctypes.c_int),
        ("mEngineRPM", ctypes.c_double),
        ("mEngineWaterTemp", ctypes.c_double),
        ("mEngineOilTemp", ctypes.c_double),
        ("mClutchRPM", ctypes.c_double),
        ("mUnfilteredThrottle", ctypes.c_double),
        ("mUnfilteredBrake", ctypes.c_double),
        ("mUnfilteredSteering", ctypes.c_double),
        ("mUnfilteredClutch", ctypes.c_double),
        ("mFilteredThrottle", ctypes.c_double),
        ("mFilteredBrake", ctypes.c_double),
        ("mFilteredSteering", ctypes.c_double),
        ("mFilteredClutch", ctypes.c_double),
        ("mSteeringShaftTorque", ctypes.c_double),
        ("mFront3rdDeflection", ctypes.c_double),
        ("mRear3rdDeflection", ctypes.c_double),
        ("mFrontWingHeight", ctypes.c_double),
        ("mFrontRideHeight", ctypes.c_double),
        ("mRearRideHeight", ctypes.c_double),
        ("mDrag", ctypes.c_double),
        ("mFrontDownforce", ctypes.c_double),
        ("mRearDownforce", ctypes.c_double),
        ("mFuel", ctypes.c_double),
        ("mEngineMaxRPM", ctypes.c_double),
        ("mScheduledStops", ctypes.c_ubyte),
        ("mOverheating", ctypes.c_ubyte),
        ("mDetached", ctypes.c_ubyte),
        ("mHeadlights", ctypes.c_ubyte),
        ("mDentSeverity", ctypes.c_ubyte * 8),
        ("mLastImpactET", ctypes.c_double),
        ("mLastImpactMagnitude", ctypes.c_double),
        ("mLastImpactPos", rF2Vec3),
        ("mEngineTorque", ctypes.c_double),
        ("mCurrentSector", ctypes.c_int),
        ("mSpeedLimiter", ctypes.c_ubyte),
        ("mMaxGears", ctypes.c_ubyte),
        ("mFrontTireCompoundIndex", ctypes.c_ubyte),
        ("mRearTireCompoundIndex", ctypes.c_ubyte),
        ("mFuelCapacity", ctypes.c_double),
        ("mFrontFlapActivated", ctypes.c_ubyte),
        ("mRearFlapActivated", ctypes.c_ubyte),
        ("mRearFlapLegalStatus", ctypes.c_ubyte),
        ("mIgnitionStarter", ctypes.c_ubyte),
        ("mFrontTireCompoundName", ctypes.c_ubyte * 18),
        ("mRearTireCompoundName", ctypes.c_ubyte * 18),
        ("mSpeedLimiterAvailable", ctypes.c_ubyte),
        ("mAntiStallActivated", ctypes.c_ubyte),
        ("mUnused", ctypes.c_ubyte * 2),
        ("mVisualSteeringWheelRange", ctypes.c_float),
        ("mRearBrakeBias", ctypes.c_double),
        ("mTurboBoostPressure", ctypes.c_double),
        ("mPhysicsToGraphicsOffset", ctypes.c_float * 3),
        ("mPhysicalSteeringWheelRange", ctypes.c_float),
        ("mBatteryChargeFraction", ctypes.c_double),
        ("mElectricBoostMotorTorque", ctypes.c_double),
        ("mElectricBoostMotorRPM", ctypes.c_double),
        ("mElectricBoostMotorTemperature", ctypes.c_double),
        ("mElectricBoostWaterTemperature", ctypes.c_double),
        ("mElectricBoostMotorState", ctypes.c_ubyte),
        ("mExpansion", ctypes.c_ubyte * 111),
        ("mWheels", rF2Wheel * 4),
    ]


class rF2Telemetry(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mVersionUpdateBegin", ctypes.c_uint),
        ("mVersionUpdateEnd", ctypes.c_uint),
        ("mBytesUpdatedHint", ctypes.c_int),
        ("mNumVehicles", ctypes.c_int),
        ("mVehicles", rF2VehicleTelemetry * MAX_MAPPED_VEHICLES),
    ]


class rF2VehicleScoring(ctypes.Structure):
    _pack_ = 4
    _fields_ = [
        ("mID", ctypes.c_int),
        ("mDriverName", ctypes.c_ubyte * 32),
        ("mVehicleName", ctypes.c_ubyte * 64),
        ("mTotalLaps", ctypes.c_short),
        ("mSector", ctypes.c_byte),
        ("mFinishStatus", ctypes.c_byte),
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
        ("mControl", ctypes.c_byte),
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
        ("mVersionUpdateBegin", ctypes.c_uint),
        ("mVersionUpdateEnd", ctypes.c_uint),
        ("mBytesUpdatedHint", ctypes.c_int),
        ("mScoringInfo", rF2ScoringInfo),
        ("mVehicles", rF2VehicleScoring * MAX_MAPPED_VEHICLES),
    ]


MAPPED_BUFFER_WRAPPER_SIZE = ctypes.sizeof(rF2MappedBufferVersionBlock)


def candidate_map_names(base_name: str, map_name: str | None = None, pid: int | None = None) -> list[str]:
    names: list[str] = []
    if map_name:
        names.append(map_name)
    if pid is not None:
        names.append(f"{base_name}{pid}")
        names.append(f"Global\\{base_name}{pid}")
    names.append(base_name)
    return dedupe(names)


def candidate_scoring_map_names(map_name: str | None = None, pid: int | None = None) -> list[str]:
    return candidate_map_names(SCORING_MAP_NAME, map_name=map_name, pid=pid)


def candidate_telemetry_map_names(map_name: str | None = None, pid: int | None = None) -> list[str]:
    return candidate_map_names(TELEMETRY_MAP_NAME, map_name=map_name, pid=pid)


class SharedMemoryScoringReader:
    def __init__(self, map_name: str | None = None, pid: int | None = None, telemetry_map_name: str | None = None):
        if os.name != "nt":
            raise SharedMemoryUnavailable("rFactor 2 shared-memory reading is Windows-only.")

        configure_win32_api()

        self.map_names = candidate_scoring_map_names(map_name=map_name, pid=pid)
        self.map_name = self._choose_map_name(self.map_names, rF2Scoring, "scoring")
        self.telemetry_map_names = candidate_telemetry_map_names(map_name=telemetry_map_name, pid=pid)
        self.telemetry_map_name, self.telemetry_error = self._try_choose_map_name(
            self.telemetry_map_names, rF2Telemetry, "telemetry"
        )

    def _choose_map_name(self, names: list[str], payload_type: type[ctypes.Structure], label: str) -> str:
        map_name, error = self._try_choose_map_name(names, payload_type, label)
        if map_name is not None:
            return map_name
        raise SharedMemoryUnavailable(f"Could not open any {label} memory map. Tried: {error}")

    def _try_choose_map_name(
        self, names: list[str], payload_type: type[ctypes.Structure], label: str
    ) -> tuple[str | None, str | None]:
        errors: list[str] = []
        for name in names:
            try:
                self._read_map_bytes(name, payload_type)
                return name, None
            except Exception as exc:
                errors.append(f"{name}: {exc}")
        return None, "; ".join(errors) if errors else f"No {label} map names were tried."

    def read_snapshot(self) -> dict[str, Any]:
        try:
            buffer = self._read_map_bytes(self.map_name, rF2Scoring)
        except Exception as exc:
            raise SharedMemoryUnavailable(f"Could not read scoring memory map '{self.map_name}': {exc}") from exc

        scoring, decode_offset = decode_best_scoring_payload(buffer)
        info = scoring.mScoringInfo
        telemetry = self._read_telemetry()
        telemetry_by_id = {vehicle["id"]: vehicle for vehicle in telemetry.get("vehicles", [])}

        raw_vehicle_count = int(info.mNumVehicles)
        session_lap_distance = none_if_negative(float(info.mLapDist))
        sector_flags = [int(value) for value in info.mSectorFlag]
        scan_limit = raw_vehicle_count if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES else MAX_MAPPED_VEHICLES
        drivers: list[dict[str, Any]] = []
        for index in range(scan_limit):
            driver = scoring_vehicle_to_dict(scoring.mVehicles[index], session_lap_distance)
            if driver is None:
                continue
            driver_telemetry = telemetry_by_id.get(driver["id"])
            driver["telemetry"] = driver_telemetry
            driver["telemetry_available"] = driver_telemetry is not None
            drivers.append(driver)

        drivers.sort(key=lambda driver: driver.get("place") or 999)
        joined_vehicle_count = sum(1 for driver in drivers if driver.get("telemetry_available"))
        telemetry["joined_vehicle_count"] = joined_vehicle_count
        telemetry["scope"] = telemetry_scope(
            scoring_vehicle_count=len(drivers),
            telemetry_vehicle_count=int(telemetry.get("vehicle_count") or 0),
            joined_vehicle_count=joined_vehicle_count,
        )

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
            "telemetry": telemetry,
            "session": {
                "track": c_string(info.mTrackName),
                "session_code": int(info.mSession),
                "session_type": session_type_name(int(info.mSession)),
                "current_time": none_if_negative(float(info.mCurrentET)),
                "end_time": none_if_negative(float(info.mEndET)),
                "max_laps": int(info.mMaxLaps),
                "lap_distance": session_lap_distance,
                "vehicle_count": len(drivers),
                "raw_vehicle_count": raw_vehicle_count,
                "game_phase": int(info.mGamePhase),
                "game_phase_name": game_phase_name(int(info.mGamePhase)),
                "yellow_flag_state": int(info.mYellowFlagState),
                "yellow_flag_state_name": yellow_flag_state_name(int(info.mYellowFlagState)),
                "sector_flags": sector_flags,
                "sector_flags_detail": sector_flags_detail(sector_flags),
                "overall_flag": overall_flag_name(int(info.mGamePhase), int(info.mYellowFlagState), sector_flags),
                "start_light": int(info.mStartLight),
                "num_red_lights": int(info.mNumRedLights),
                "in_realtime": bool(info.mInRealtime),
                "player_name": c_string(info.mPlayerName),
                "player_file_name": c_string(info.mPlrFileName),
                "server_name": c_string(info.mServerName),
                "ambient_temp": none_if_unreasonable(float(info.mAmbientTemp)),
                "track_temp": none_if_unreasonable(float(info.mTrackTemp)),
                "raining": clamp_01(float(info.mRaining)),
                "dark_cloud": clamp_01(float(info.mDarkCloud)),
                "wind": vec_to_dict(info.mWind),
                "min_path_wetness": clamp_01(float(info.mMinPathWetness)),
                "max_path_wetness": clamp_01(float(info.mMaxPathWetness)),
                "avg_path_wetness": clamp_01(float(info.mAvgPathWetness)),
                "game_mode": int(info.mGameMode),
                "is_password_protected": bool(info.mIsPasswordProtected),
                "server_port": int(info.mServerPort),
                "server_public_ip": int(info.mServerPublicIP),
                "max_players": int(info.mMaxPlayers),
                "start_time": none_if_negative(float(info.mStartET)),
            },
            "drivers": drivers,
        }

    def _read_telemetry(self) -> dict[str, Any]:
        if self.telemetry_map_name is None:
            return {
                "status": "unavailable",
                "memory_map": None,
                "map_names": self.telemetry_map_names,
                "error": self.telemetry_error,
                "decode_offset": None,
                "update_counter": None,
                "version_begin": None,
                "version_end": None,
                "bytes_updated_hint": None,
                "raw_vehicle_count": None,
                "vehicle_count": 0,
                "vehicles": [],
            }

        try:
            buffer = self._read_map_bytes(self.telemetry_map_name, rF2Telemetry)
            telemetry, decode_offset = decode_best_telemetry_payload(buffer)
        except Exception as exc:
            return {
                "status": "error",
                "memory_map": self.telemetry_map_name,
                "map_names": self.telemetry_map_names,
                "error": str(exc),
                "decode_offset": None,
                "update_counter": None,
                "version_begin": None,
                "version_end": None,
                "bytes_updated_hint": None,
                "raw_vehicle_count": None,
                "vehicle_count": 0,
                "vehicles": [],
            }

        raw_vehicle_count = int(telemetry.mNumVehicles)
        scan_limit = raw_vehicle_count if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES else MAX_MAPPED_VEHICLES
        vehicles = [
            vehicle
            for index in range(scan_limit)
            if (vehicle := telemetry_vehicle_to_dict(telemetry.mVehicles[index])) is not None
        ]
        return {
            "status": "connected",
            "memory_map": self.telemetry_map_name,
            "map_names": self.telemetry_map_names,
            "error": None,
            "decode_offset": decode_offset,
            "update_counter": int(telemetry.mVersionUpdateEnd),
            "version_begin": int(telemetry.mVersionUpdateBegin),
            "version_end": int(telemetry.mVersionUpdateEnd),
            "bytes_updated_hint": int(telemetry.mBytesUpdatedHint),
            "raw_vehicle_count": raw_vehicle_count,
            "vehicle_count": len(vehicles),
            "vehicles": vehicles,
        }

    def _read_map_bytes(self, name: str, payload_type: type[ctypes.Structure]) -> bytes:
        size = ctypes.sizeof(payload_type)
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


def decode_best_telemetry_payload(buffer: bytes) -> tuple[rF2Telemetry, int]:
    size = ctypes.sizeof(rF2Telemetry)
    candidates: list[tuple[int, rF2Telemetry, int]] = []
    for offset in (0, MAPPED_BUFFER_WRAPPER_SIZE):
        if len(buffer) < offset + size:
            continue
        telemetry = rF2Telemetry.from_buffer_copy(buffer[offset : offset + size])
        candidates.append((score_decoded_telemetry(telemetry), telemetry, offset))

    if not candidates:
        raise SharedMemoryUnavailable("Telemetry memory map is smaller than the expected rF2Telemetry payload.")

    _, telemetry, offset = max(candidates, key=lambda item: item[0])
    return telemetry, offset


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
        1 for index in range(scan_limit) if is_probable_scoring_vehicle(scoring.mVehicles[index])
    )
    score += min(valid_driver_count, 16) * 2
    return score


def score_decoded_telemetry(telemetry: rF2Telemetry) -> int:
    score = 0
    raw_vehicle_count = int(telemetry.mNumVehicles)
    if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES:
        score += 5
    if 0 <= int(telemetry.mBytesUpdatedHint) <= ctypes.sizeof(rF2Telemetry):
        score += 1

    scan_limit = raw_vehicle_count if 0 <= raw_vehicle_count <= MAX_MAPPED_VEHICLES else MAX_MAPPED_VEHICLES
    valid_vehicle_count = sum(
        1 for index in range(scan_limit) if is_probable_telemetry_vehicle(telemetry.mVehicles[index])
    )
    score += min(valid_vehicle_count, 16) * 2
    return score


def scoring_vehicle_to_dict(vehicle: rF2VehicleScoring, session_lap_distance: float | None) -> dict[str, Any] | None:
    if not is_probable_scoring_vehicle(vehicle):
        return None

    lap_distance = none_if_negative(float(vehicle.mLapDist))
    best_lap_time = none_if_not_timed(float(vehicle.mBestLapTime))
    best_sector_1 = none_if_not_timed(float(vehicle.mBestSector1))
    best_sector_2 = none_if_not_timed(float(vehicle.mBestSector2))
    last_lap_time = none_if_not_timed(float(vehicle.mLastLapTime))
    last_sector_1 = none_if_not_timed(float(vehicle.mLastSector1))
    last_sector_2 = none_if_not_timed(float(vehicle.mLastSector2))
    current_sector_1 = none_if_not_timed(float(vehicle.mCurSector1))
    current_sector_2 = none_if_not_timed(float(vehicle.mCurSector2))
    best_lap_sector_1 = none_if_not_timed(float(vehicle.mBestLapSector1))
    best_lap_sector_2_cumulative = none_if_not_timed(float(vehicle.mBestLapSector2))

    return {
        "id": int(vehicle.mID),
        "driver_name": c_string(vehicle.mDriverName),
        "vehicle_name": c_string(vehicle.mVehicleName),
        "vehicle_class": c_string(vehicle.mVehicleClass),
        "laps": int(vehicle.mTotalLaps),
        "place": int(vehicle.mPlace) or None,
        "sector": int(vehicle.mSector),
        "finish_status": int(vehicle.mFinishStatus),
        "finish_status_name": finish_status_name(int(vehicle.mFinishStatus)),
        "best_lap_time": best_lap_time,
        "last_lap_time": last_lap_time,
        "current_lap_time": none_if_not_timed(float(vehicle.mTimeIntoLap)),
        "estimated_lap_time": none_if_not_timed(float(vehicle.mEstimatedLapTime)),
        "best_sector_1": best_sector_1,
        "best_sector_2": best_sector_2,
        "best_sector_2_split": sector_delta(best_sector_2, best_sector_1),
        "best_lap_sector_1": best_lap_sector_1,
        "best_lap_sector_2": sector_delta(best_lap_sector_2_cumulative, best_lap_sector_1),
        "best_lap_sector_2_cumulative": best_lap_sector_2_cumulative,
        "best_lap_sector_3": sector_delta(best_lap_time, best_lap_sector_2_cumulative),
        "last_sector_1": last_sector_1,
        "last_sector_2": last_sector_2,
        "last_sector_2_split": sector_delta(last_sector_2, last_sector_1),
        "last_sector_3": sector_delta(last_lap_time, last_sector_2),
        "current_sector_1": current_sector_1,
        "current_sector_2": current_sector_2,
        "current_sector_2_split": sector_delta(current_sector_2, current_sector_1),
        "lap_distance": lap_distance,
        "track_position_percent": percent_of_lap(lap_distance, session_lap_distance),
        "path_lateral": round(float(vehicle.mPathLateral), 3),
        "track_edge": round(float(vehicle.mTrackEdge), 3),
        "pit_lap_distance": none_if_negative(float(vehicle.mPitLapDist)),
        "pit_group": c_string(vehicle.mPitGroup),
        "pit_state": int(vehicle.mPitState),
        "pit_state_name": pit_state_name(int(vehicle.mPitState)),
        "num_pitstops": int(vehicle.mNumPitstops),
        "num_penalties": int(vehicle.mNumPenalties),
        "is_player": bool(vehicle.mIsPlayer),
        "control": int(vehicle.mControl),
        "control_name": control_name(int(vehicle.mControl)),
        "in_pits": bool(vehicle.mInPits),
        "server_scored": bool(vehicle.mServerScored),
        "individual_phase": int(vehicle.mIndividualPhase),
        "qualification": int(vehicle.mQualification),
        "time_behind_next": none_if_not_timed(float(vehicle.mTimeBehindNext)),
        "time_behind_leader": none_if_not_timed(float(vehicle.mTimeBehindLeader)),
        "laps_behind_next": int(vehicle.mLapsBehindNext),
        "laps_behind_leader": int(vehicle.mLapsBehindLeader),
        "lap_start_time": none_if_negative(float(vehicle.mLapStartET)),
        "position": vec_to_dict(vehicle.mPos),
        "local_velocity": vec_to_dict(vehicle.mLocalVel),
        "speed_kph": speed_kph(vehicle.mLocalVel),
        "local_acceleration": vec_to_dict(vehicle.mLocalAccel),
        "local_acceleration_g": g_force_dict(vehicle.mLocalAccel),
        "local_rotation": vec_to_dict(vehicle.mLocalRot),
        "local_rotation_acceleration": vec_to_dict(vehicle.mLocalRotAccel),
        "headlights": bool(vehicle.mHeadlights),
        "flag": int(vehicle.mFlag),
        "flag_name": primary_flag_name(int(vehicle.mFlag)),
        "flag_scope": "primary green/blue only",
        "under_yellow": bool(vehicle.mUnderYellow),
        "count_lap_flag": int(vehicle.mCountLapFlag),
        "count_lap_flag_name": count_lap_flag_name(int(vehicle.mCountLapFlag)),
        "in_garage_stall": bool(vehicle.mInGarageStall),
        "upgrade_pack": list(bytes(vehicle.mUpgradePack)),
    }


def telemetry_vehicle_to_dict(vehicle: rF2VehicleTelemetry) -> dict[str, Any] | None:
    if not is_probable_telemetry_vehicle(vehicle):
        return None

    return {
        "id": int(vehicle.mID),
        "vehicle_name": c_string(vehicle.mVehicleName),
        "track_name": c_string(vehicle.mTrackName),
        "elapsed_time": none_if_negative(float(vehicle.mElapsedTime)),
        "delta_time": none_if_negative(float(vehicle.mDeltaTime)),
        "lap_number": int(vehicle.mLapNumber),
        "lap_start_time": none_if_negative(float(vehicle.mLapStartET)),
        "position": vec_to_dict(vehicle.mPos),
        "local_velocity": vec_to_dict(vehicle.mLocalVel),
        "speed_kph": speed_kph(vehicle.mLocalVel),
        "local_acceleration": vec_to_dict(vehicle.mLocalAccel),
        "g_force": g_force_dict(vehicle.mLocalAccel),
        "lateral_g": round(float(vehicle.mLocalAccel.x) / STANDARD_GRAVITY, 3),
        "vertical_g": round(float(vehicle.mLocalAccel.y) / STANDARD_GRAVITY, 3),
        "longitudinal_g": round(float(vehicle.mLocalAccel.z) / STANDARD_GRAVITY, 3),
        "local_rotation": vec_to_dict(vehicle.mLocalRot),
        "local_rotation_acceleration": vec_to_dict(vehicle.mLocalRotAccel),
        "gear": int(vehicle.mGear),
        "gear_label": gear_label(int(vehicle.mGear)),
        "engine_rpm": round(float(vehicle.mEngineRPM), 1),
        "engine_max_rpm": none_if_unreasonable_high(float(vehicle.mEngineMaxRPM), 50000.0),
        "engine_water_temp": none_if_unreasonable(float(vehicle.mEngineWaterTemp)),
        "engine_oil_temp": none_if_unreasonable(float(vehicle.mEngineOilTemp)),
        "clutch_rpm": round(float(vehicle.mClutchRPM), 1),
        "throttle": clamp_01(float(vehicle.mUnfilteredThrottle)),
        "throttle_percent": percent_01(float(vehicle.mUnfilteredThrottle)),
        "brake": clamp_01(float(vehicle.mUnfilteredBrake)),
        "brake_percent": percent_01(float(vehicle.mUnfilteredBrake)),
        "steering": normalized_steering(float(vehicle.mUnfilteredSteering)),
        "steering_percent": percent_signed(float(vehicle.mUnfilteredSteering)),
        "clutch": clamp_01(float(vehicle.mUnfilteredClutch)),
        "clutch_percent": percent_01(float(vehicle.mUnfilteredClutch)),
        "filtered_throttle": clamp_01(float(vehicle.mFilteredThrottle)),
        "filtered_throttle_percent": percent_01(float(vehicle.mFilteredThrottle)),
        "filtered_brake": clamp_01(float(vehicle.mFilteredBrake)),
        "filtered_brake_percent": percent_01(float(vehicle.mFilteredBrake)),
        "filtered_steering": normalized_steering(float(vehicle.mFilteredSteering)),
        "filtered_steering_percent": percent_signed(float(vehicle.mFilteredSteering)),
        "filtered_clutch": clamp_01(float(vehicle.mFilteredClutch)),
        "filtered_clutch_percent": percent_01(float(vehicle.mFilteredClutch)),
        "steering_shaft_torque": round(float(vehicle.mSteeringShaftTorque), 3),
        "visual_steering_wheel_range": round(float(vehicle.mVisualSteeringWheelRange), 1),
        "physical_steering_wheel_range": round(float(vehicle.mPhysicalSteeringWheelRange), 1),
        "current_sector": int(vehicle.mCurrentSector),
        "fuel": none_if_unreasonable_high(float(vehicle.mFuel), 1000.0),
        "fuel_capacity": none_if_unreasonable_high(float(vehicle.mFuelCapacity), 1000.0),
        "rear_brake_bias": clamp_01(float(vehicle.mRearBrakeBias)),
        "turbo_boost_pressure": none_if_unreasonable_high(float(vehicle.mTurboBoostPressure), 10000.0),
        "front_tire_compound": c_string(vehicle.mFrontTireCompoundName),
        "rear_tire_compound": c_string(vehicle.mRearTireCompoundName),
        "front_tire_compound_index": int(vehicle.mFrontTireCompoundIndex),
        "rear_tire_compound_index": int(vehicle.mRearTireCompoundIndex),
        "speed_limiter": bool(vehicle.mSpeedLimiter),
        "speed_limiter_available": bool(vehicle.mSpeedLimiterAvailable),
        "anti_stall_activated": bool(vehicle.mAntiStallActivated),
        "max_gears": int(vehicle.mMaxGears),
        "headlights": bool(vehicle.mHeadlights),
        "overheating": bool(vehicle.mOverheating),
        "detached": bool(vehicle.mDetached),
        "scheduled_stops": int(vehicle.mScheduledStops),
        "last_impact_time": none_if_negative(float(vehicle.mLastImpactET)),
        "last_impact_magnitude": none_if_negative(float(vehicle.mLastImpactMagnitude)),
        "last_impact_position": vec_to_dict(vehicle.mLastImpactPos),
        "battery_charge_fraction": clamp_01(float(vehicle.mBatteryChargeFraction)),
        "electric_boost_motor_state": int(vehicle.mElectricBoostMotorState),
    }


def is_probable_scoring_vehicle(vehicle: rF2VehicleScoring) -> bool:
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


def is_probable_telemetry_vehicle(vehicle: rF2VehicleTelemetry) -> bool:
    vehicle_name = c_string(vehicle.mVehicleName)
    track_name = c_string(vehicle.mTrackName)
    if not is_plausible_text(vehicle_name) and not is_plausible_text(track_name):
        return False
    vehicle_id = int(vehicle.mID)
    lap_number = int(vehicle.mLapNumber)
    gear = int(vehicle.mGear)
    if vehicle_id < -1 or vehicle_id > 100000:
        return False
    if lap_number < -1 or lap_number > 10000:
        return False
    if gear < -1 or gear > 30:
        return False
    return True


def telemetry_scope(scoring_vehicle_count: int, telemetry_vehicle_count: int, joined_vehicle_count: int) -> str:
    if telemetry_vehicle_count <= 0:
        return "unavailable"
    if scoring_vehicle_count > 0 and joined_vehicle_count >= scoring_vehicle_count:
        return "all scoring vehicles"
    if telemetry_vehicle_count == 1:
        return "single vehicle"
    return "partial vehicle set"


def vec_to_dict(value: rF2Vec3) -> dict[str, float]:
    return {"x": round(float(value.x), 3), "y": round(float(value.y), 3), "z": round(float(value.z), 3)}


def vec_magnitude(value: rF2Vec3) -> float:
    return (float(value.x) ** 2 + float(value.y) ** 2 + float(value.z) ** 2) ** 0.5


def speed_kph(value: rF2Vec3) -> float:
    return round(vec_magnitude(value) * 3.6, 1)


def g_force_dict(value: rF2Vec3) -> dict[str, float]:
    x = float(value.x) / STANDARD_GRAVITY
    y = float(value.y) / STANDARD_GRAVITY
    z = float(value.z) / STANDARD_GRAVITY
    return {
        "x": round(x, 3),
        "y": round(y, 3),
        "z": round(z, 3),
        "lateral": round(x, 3),
        "vertical": round(y, 3),
        "longitudinal": round(z, 3),
        "magnitude": round((x * x + y * y + z * z) ** 0.5, 3),
    }


def sector_delta(cumulative: float | None, previous_cumulative: float | None) -> float | None:
    if cumulative is None or previous_cumulative is None:
        return None
    delta = cumulative - previous_cumulative
    if delta <= 0 or delta > 24 * 60 * 60:
        return None
    return round(delta, 3)


def percent_of_lap(lap_distance: float | None, session_lap_distance: float | None) -> float | None:
    if lap_distance is None or session_lap_distance is None or session_lap_distance <= 0:
        return None
    return round(max(0.0, min(100.0, lap_distance / session_lap_distance * 100.0)), 2)


def is_plausible_text(value: str) -> bool:
    if not value:
        return False
    return sum(1 for char in value if char.isprintable()) == len(value)


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


def game_phase_name(game_phase: int) -> str:
    names = {0: "Garage", 1: "Reconnaissance", 2: "Grid walk", 3: "Formation", 4: "Countdown", 5: "Green flag", 6: "Full course yellow", 7: "Stopped", 8: "Session over", 9: "Paused/heartbeat"}
    return names.get(game_phase, f"Unknown ({game_phase})")


def yellow_flag_state_name(yellow_flag_state: int) -> str:
    names = {
        -1: "invalid",
        0: "none",
        1: "pending",
        2: "pits closed",
        3: "pit lead lap",
        4: "pits open",
        5: "last lap",
        6: "resume",
        7: "race halt",
    }
    return names.get(yellow_flag_state, f"unknown ({yellow_flag_state})")


def sector_flags_detail(sector_flags: list[int]) -> list[dict[str, Any]]:
    return [
        {
            "sector": index + 1,
            "value": value,
            "flag": sector_flag_name(value),
            "is_yellow": sector_flag_is_local_yellow(value),
        }
        for index, value in enumerate(sector_flags)
    ]


def overall_flag_name(game_phase: int, yellow_flag_state: int, sector_flags: list[int]) -> str:
    if game_phase == 8:
        return "SESSION OVER"
    if game_phase == 7 or yellow_flag_state == 7:
        return "RED / RACE HALT"
    if game_phase == 6:
        return "SAFETY CAR / FULL COURSE YELLOW"
    if any(sector_flag_is_local_yellow(value) for value in sector_flags):
        return "LOCAL YELLOW"
    if yellow_flag_state not in (-1, 0):
        return "YELLOW"
    if game_phase == 5:
        return "GREEN"
    return game_phase_name(game_phase).upper()


def sector_flag_name(sector_flag: int) -> str:
    if sector_flag == 0:
        return "clear"
    if sector_flag == 1:
        return "local yellow"
    if sector_flag in (-1, 2, 3, 4, 5, 6, 7):
        return yellow_flag_state_name(sector_flag)
    return "unclassified"


def sector_flag_is_local_yellow(sector_flag: int) -> bool:
    return sector_flag == 1


def finish_status_name(finish_status: int) -> str:
    return {0: "none", 1: "finished", 2: "dnf", 3: "dq"}.get(finish_status, f"unknown ({finish_status})")


def primary_flag_name(flag: int) -> str:
    return {0: "green", 6: "blue"}.get(flag, f"unknown ({flag})")


def count_lap_flag_name(flag: int) -> str:
    names = {0: "do not count lap", 1: "count lap, not time", 2: "count lap and time"}
    return names.get(flag, f"unknown ({flag})")


def control_name(control: int) -> str:
    return {-1: "nobody", 0: "player", 1: "ai", 2: "remote", 3: "replay"}.get(control, f"unknown ({control})")


def pit_state_name(pit_state: int) -> str:
    return {0: "none", 1: "request", 2: "entering", 3: "stopped", 4: "exiting"}.get(pit_state, f"unknown ({pit_state})")


def gear_label(gear: int) -> str:
    if gear == -1:
        return "R"
    if gear == 0:
        return "N"
    return str(gear)


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


def none_if_unreasonable_high(value: float, maximum: float) -> float | None:
    if value < 0 or value > maximum:
        return None
    return round(value, 3)


def clamp_01(value: float) -> float | None:
    if value < 0 or value > 1:
        return None
    return round(value, 3)


def percent_01(value: float) -> float | None:
    clamped = clamp_01(value)
    if clamped is None:
        return None
    return round(clamped * 100.0, 1)


def normalized_steering(value: float) -> float | None:
    if value < -1.5 or value > 1.5:
        return None
    return round(value, 3)


def percent_signed(value: float) -> float | None:
    normalized = normalized_steering(value)
    if normalized is None:
        return None
    return round(normalized * 100.0, 1)


def dedupe(values: list[str]) -> list[str]:
    unique_values: list[str] = []
    for value in values:
        if value not in unique_values:
            unique_values.append(value)
    return unique_values


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
