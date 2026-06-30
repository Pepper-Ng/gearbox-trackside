import { type DriverSnapshot, type LiveSessionSnapshot } from '../tracksideApi';

export interface DriverStatus {
  label: string;
  tone: 'pit' | 'garage' | 'out';
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

function isFiniteNumber(value: number | null | undefined): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}