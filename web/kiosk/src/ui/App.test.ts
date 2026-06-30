import { describe, expect, it } from 'vitest';
import { type DriverSnapshot, type LiveSessionSnapshot } from '../tracksideApi';
import { getViewFromPath, toViewMode } from './App';
import { stableDriverColor, trackerDriverColorByIndex } from './driverColors';
import { getConnectionIndicators, getDriverStatus, getRaceLapProgress, getRacePositionDelta } from './liveBoardLogic';

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

  it('reports race positions gained or lost from a baseline rank', () => {
    expect(getRacePositionDelta(2, 5)).toBe(3);
    expect(getRacePositionDelta(6, 4)).toBe(-2);
    expect(getRacePositionDelta(4, 4)).toBeNull();
  });

  it('separates backend connection from shared-memory live data', () => {
    expect(getConnectionIndicators('Connected through REST recovery endpoint', makeSnapshot({ sessionKind: 'Practice' }))).toEqual({ backendConnected: true, liveData: false });
    expect(getConnectionIndicators('Connected through SignalR live updates', makeSnapshot({ sessionKind: 'Race', source: 'shared-memory', sourceStatus: 'connected' }))).toEqual({ backendConnected: true, liveData: true });
    expect(getConnectionIndicators('Unable to connect: 503', null)).toEqual({ backendConnected: false, liveData: false });
  });
});

describe('tracker colors', () => {
  it('starts stable tracker assignments with venue colors', () => {
    expect([0, 1, 2].map(trackerDriverColorByIndex)).toEqual(['#ff202d', '#ff8a00', '#00b0ff']);
  });

  it('keeps a driver color stable regardless of row order', () => {
    const niko = stableDriverColor('7', 'Niko');

    expect(stableDriverColor('7', 'Niko')).toBe(niko);
    expect(stableDriverColor('8', 'Antonio')).not.toBe(niko);
  });
});

function makeSnapshot(options: { sessionKind: 'Practice' | 'Qualifying' | 'Race'; totalLaps?: number | null; source?: string; sourceStatus?: string }): LiveSessionSnapshot {
  return {
    source: options.source ?? 'test',
    status: options.sourceStatus ?? 'ready',
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
