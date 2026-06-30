import { type DriverSnapshot, type LiveSessionSnapshot } from '../tracksideApi';

export interface DriverStatus {
  label: string;
  tone: 'pit' | 'garage' | 'out';
}

export interface ConnectionIndicators {
  backendConnected: boolean;
  liveData: boolean;
}

export function getConnectionIndicators(status: string, snapshot: LiveSessionSnapshot | null): ConnectionIndicators {
  const isUnable = /^Unable to connect\b/i.test(status);
  const backendConnected = !isUnable && (/^Connected through\b/i.test(status) || snapshot !== null);
  return {
    backendConnected,
    liveData: isLiveSharedMemorySnapshot(snapshot),
  };
}

function isLiveSharedMemorySnapshot(snapshot: LiveSessionSnapshot | null): boolean {
  if (!snapshot || !stringEquals(snapshot.source, 'shared-memory')) {
    return false;
  }

  const status = snapshot.status.toLowerCase();
  return status.includes('connected')
    && !status.includes('waiting')
    && !status.includes('unavailable')
    && !stringEquals(snapshot.session.trackName, 'No live scoring source');
}

export function getRaceLapProgress(snapshot: LiveSessionSnapshot): { current: number; total: number } | null {
  if (snapshot.session.kind !== 'Race' || !isFiniteNumber(snapshot.session.totalLaps) || snapshot.session.totalLaps <= 0) {
    return null;
  }

  const leader = snapshot.drivers.find(driver => driver.leaderboardRank === 1) ?? snapshot.drivers[0];
  if (!leader) {
    return null;
  }

  const total = Math.max(1, Math.trunc(snapshot.session.totalLaps));
  const completed = Math.max(0, leader.completedLaps);
  const current = snapshot.session.phase === 'SessionOver'
    ? Math.min(total, Math.max(1, completed))
    : Math.min(total, completed + 1);

  return { current, total };
}

export function getDriverStatus(driver: DriverSnapshot, sessionKind: string | null | undefined): DriverStatus | null {
  if (driver.isInGarageStall) {
    return { label: 'GARAGE', tone: 'garage' };
  }

  if (driver.isInPits) {
    return { label: 'PIT', tone: 'pit' };
  }

  const isOutLap = sessionKind !== 'Race'
    && driver.completedLaps === 0
    && driver.bestLapSeconds == null
    && (isFiniteNumber(driver.currentLapSeconds) || isFiniteNumber(driver.trackPositionPercent));
  return isOutLap ? { label: 'OUT', tone: 'out' } : null;
}

export function getRacePositionDelta(currentRank: number | null | undefined, baselineRank: number | null | undefined): number | null {
  if (!isFiniteNumber(currentRank) || !isFiniteNumber(baselineRank) || currentRank <= 0 || baselineRank <= 0) {
    return null;
  }

  const delta = Math.trunc(baselineRank) - Math.trunc(currentRank);
  return delta === 0 ? null : delta;
}

function isFiniteNumber(value: number | null | undefined): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

function stringEquals(left: string | null | undefined, right: string): boolean {
  return (left ?? '').toLowerCase() === right.toLowerCase();
}