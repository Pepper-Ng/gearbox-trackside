import { describe, expect, it } from 'vitest';
import { type DriverSnapshot, type LiveSessionSnapshot } from '../tracksideApi';
import { getViewFromPath, toViewMode } from './App';
import { stableDriverColor } from './driverColors';
import { getDriverStatus, getRaceLapProgress } from './liveBoardLogic';

describe('kiosk route selection', () => {
  it('maps the tracker route to the tracker view', () => {
    expect(getViewFromPath('/tracker')).toBe('tracker');
    expect(getViewFromPath('/TRACKER/')).toBe('tracker');
  });

  it('maps configured display modes case-insensitively', () => {
    expect(toViewMode('Tracker')).toBe('tracker');
    expect(toViewMode('tracker')).toBe('tracker');
    expect(toViewMode('LastSession')).toBe('last');
    expect(toViewMode('last-session')).toBe('last');
  });
});

describe('live board helpers', () => {
  it('uses the leading race driver to derive the current lap', () => {
    const snapshot = makeSnapshot({ sessionKind: 'Race', totalLaps: 7 });

    expect(getRaceLapProgress(snapshot)).toEqual({ current: 4, total: 7 });
  });

  it('classifies garage, pit, and out-lap row states', () => {
    expect(getDriverStatus(makeDriver({ isInGarageStall: true }), 'Practice')).toEqual({ label: 'GARAGE', tone: 'garage' });
    expect(getDriverStatus(makeDriver({ isInPits: true }), 'Practice')).toEqual({ label: 'PIT', tone: 'pit' });
    expect(getDriverStatus(makeDriver({ completedLaps: 0, bestLapSeconds: null, currentLapSeconds: 18.2 }), 'Qualifying')).toEqual({ label: 'OUT', tone: 'out' });
    expect(getDriverStatus(makeDriver({ completedLaps: 0, bestLapSeconds: null, currentLapSeconds: 18.2 }), 'Race')).toBeNull();
  });
});

describe('tracker colors', () => {
  it('keeps a driver color stable regardless of row order', () => {
    const niko = stableDriverColor('7', 'Niko');

    expect(stableDriverColor('7', 'Niko')).toBe(niko);
    expect(stableDriverColor('8', 'Antonio')).not.toBe(niko);
  });
});

function makeSnapshot(options: { sessionKind: 'Practice' | 'Qualifying' | 'Race'; totalLaps?: number | null }): LiveSessionSnapshot {
  return {
    source: 'test',
    status: 'ready',
    timestampUtc: '2026-06-24T12:00:00+00:00',
    updateSequence: 1,
    session: {
      trackName: 'Loch Drummond - Short',
      kind: options.sessionKind,
      phase: 'GreenFlag',
      vehicleCount: 2,
      totalLaps: options.totalLaps,
      overallFlag: 'GREEN',
    },
    drivers: [
      makeDriver({ driverId: '1', leaderboardRank: 1, completedLaps: 3 }),
      makeDriver({ driverId: '2', leaderboardRank: 2, completedLaps: 3 }),
    ],
  };
}

function makeDriver(overrides: Partial<DriverSnapshot> = {}): DriverSnapshot {
  return {
    leaderboardRank: 1,
    driverId: '1',
    rigName: 'Setup1',
    displayName: 'Niko',
    vehicleName: 'Formula Pro',
    position: 1,
    isOverallBestLap: false,
    completedLaps: 1,
    bestLapSeconds: 82.4,
    lastLapSeconds: 83.1,
    currentLapSeconds: 24.2,
    gapToLeaderSeconds: 0,
    gapToNextSeconds: null,
    lapsBehindLeader: null,
    currentSector: 0,
    trackPositionPercent: 25,
    lapDistanceMeters: 800,
    posX: null,
    posY: null,
    posZ: null,
    isInPits: false,
    isInGarageStall: false,
    sectors: [],
    ...overrides,
  };
}
