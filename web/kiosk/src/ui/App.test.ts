import { describe, expect, it } from 'vitest';
import { type DriverSnapshot, type LiveSessionSnapshot, type SectorSnapshot } from '../tracksideApi';
import { getFlagColor, getFlagDisplayText, getViewFromPath, isCheckeredFlag, shouldShowFlagSwatchText, toViewMode } from './App';
import { stableDriverColor, trackerDriverColorByIndex } from './driverColors';
import { getConnectionIndicators, getDriverStatus, getRaceLapProgress, getRacePositionDelta } from './liveBoardLogic';
import { buildSectorStripeStates, createEmptySectorStripeCache, type SectorStripeState } from './sectorStripeLogic';

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

describe('flag display helpers', () => {
  it('renders checkered flags as a checker pattern without swatch text', () => {
    expect(isCheckeredFlag('Checkered Flag')).toBe(true);
    expect(getFlagColor('Checkered Flag')).toContain('conic-gradient');
    expect(shouldShowFlagSwatchText('Checkered Flag')).toBe(false);
  });

  it('treats session-over as checkered display state', () => {
    const snapshot = makeSnapshot({ sessionKind: 'Practice' });
    const session = { ...snapshot.session, phase: 'SessionOver' as const };

    expect(getFlagDisplayText(session)).toBe('CHECKERED');
    expect(shouldShowFlagSwatchText(session.overallFlag, session.phase)).toBe(false);
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

describe('sector stripe logic', () => {
  it('follows current-lap progress and treats rFactor sector 0 as sector 3', () => {
    let cache = createEmptySectorStripeCache();
    const referenceDriver = makeReferenceDriver();

    let result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [makeDriver({ completedLaps: 0, currentSector: 1, sectors: [sector(1), sector(2), sector(3)] }), referenceDriver],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['pending', 'pending', 'pending']);
    cache = result.cache;

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [makeDriver({
        completedLaps: 0,
        currentSector: 2,
        sectors: [sector(1, { currentSeconds: 25.1, bestSeconds: 25.1 }), sector(2), sector(3)],
      }), referenceDriver],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['personal', 'pending', 'pending']);
    cache = result.cache;

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [makeDriver({
        completedLaps: 0,
        currentSector: 0,
        sectors: [
          sector(1, { currentSeconds: 25.1, bestSeconds: 25.1 }),
          sector(2, { currentSeconds: 30.2, bestSeconds: 30.2 }),
          sector(3),
        ],
      }), referenceDriver],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['personal', 'personal', 'pending']);
    cache = result.cache;

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [makeDriver({
        completedLaps: 1,
        currentSector: 1,
        sectors: [
          sector(1, { lastSeconds: 25.1, bestSeconds: 25.1 }),
          sector(2, { lastSeconds: 30.2, bestSeconds: 30.2 }),
          sector(3, { lastSeconds: 35.3, bestSeconds: 35.3 }),
        ],
      }), referenceDriver],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['personal', 'personal', 'personal']);
    cache = result.cache;

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [makeDriver({
        completedLaps: 1,
        currentSector: 2,
        sectors: [
          sector(1, { currentSeconds: 24.8, lastSeconds: 25.1, bestSeconds: 24.8 }),
          sector(2, { lastSeconds: 30.2, bestSeconds: 30.2 }),
          sector(3, { lastSeconds: 35.3, bestSeconds: 35.3 }),
        ],
      }), referenceDriver],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['personal', 'personal stale', 'personal stale']);
    cache = result.cache;

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [makeDriver({
        completedLaps: 1,
        currentSector: 0,
        sectors: [
          sector(1, { currentSeconds: 24.8, lastSeconds: 25.1, bestSeconds: 24.8 }),
          sector(2, { currentSeconds: 29.8, lastSeconds: 30.2, bestSeconds: 29.8 }),
          sector(3, { lastSeconds: 35.3, bestSeconds: 35.3 }),
        ],
      }), referenceDriver],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['personal', 'personal', 'personal stale']);
  });

  it('recomputes cached stripe colors when another driver takes global best', () => {
    let cache = createEmptySectorStripeCache();
    let result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [
        makeDriver({
          completedLaps: 1,
          currentSector: 1,
          sectors: [sector(1, { lastSeconds: 25, bestSeconds: 25 }), sector(2), sector(3)],
        }),
        makeDriver({ driverId: '2', completedLaps: 0, sectors: [sector(1, { bestSeconds: 26 }), sector(2), sector(3)] }),
      ],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))[0]).toBe('overall');
    cache = result.cache;

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      drivers: [
        makeDriver({
          completedLaps: 1,
          currentSector: 1,
          sectors: [sector(1, { lastSeconds: 25, bestSeconds: 25 }), sector(2), sector(3)],
        }),
        makeDriver({ driverId: '2', completedLaps: 0, sectors: [sector(1, { bestSeconds: 24.5 }), sector(2), sector(3)] }),
      ],
    }), cache);
    expect(summarizeStripes(result.states.get('1'))[0]).toBe('personal');
  });

  it('resets cached stripe state when a new session starts', () => {
    let result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      currentSessionSeconds: 300,
      drivers: [makeDriver({
        completedLaps: 1,
        currentSector: 1,
        sectors: [
          sector(1, { lastSeconds: 25, bestSeconds: 25 }),
          sector(2, { lastSeconds: 30, bestSeconds: 30 }),
          sector(3, { lastSeconds: 35, bestSeconds: 35 }),
        ],
      })],
    }), createEmptySectorStripeCache());
    expect(summarizeStripes(result.states.get('1'))).toEqual(['overall', 'overall', 'overall']);

    result = buildSectorStripeStates(makeSnapshot({
      sessionKind: 'Practice',
      currentSessionSeconds: 5,
      drivers: [makeDriver({ completedLaps: 0, currentSector: 1, sectors: [sector(1), sector(2), sector(3)] })],
    }), result.cache);
    expect(summarizeStripes(result.states.get('1'))).toEqual(['pending', 'pending', 'pending']);
  });
});

function makeSnapshot(options: {
  sessionKind: 'Practice' | 'Qualifying' | 'Race';
  totalLaps?: number | null;
  source?: string;
  sourceStatus?: string;
  currentSessionSeconds?: number | null;
  drivers?: DriverSnapshot[];
}): LiveSessionSnapshot {
  return {
    source: options.source ?? 'test',
    status: options.sourceStatus ?? 'ready',
    timestampUtc: '2026-06-24T12:00:00+00:00',
    updateSequence: 1,
    session: {
      trackName: 'Loch Drummond - Short',
      kind: options.sessionKind,
      phase: 'GreenFlag',
      currentSessionSeconds: options.currentSessionSeconds,
      vehicleCount: 2,
      totalLaps: options.totalLaps,
      overallFlag: 'GREEN',
    },
    drivers: options.drivers ?? [
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

function makeReferenceDriver(): DriverSnapshot {
  return makeDriver({
    driverId: '2',
    displayName: 'Mira',
    sectors: [
      sector(1, { bestSeconds: 24 }),
      sector(2, { bestSeconds: 29 }),
      sector(3, { bestSeconds: 34 }),
    ],
  });
}

function sector(number: number, overrides: Partial<SectorSnapshot> = {}): SectorSnapshot {
  return {
    number,
    bestSeconds: null,
    lastSeconds: null,
    currentSeconds: null,
    isOverallBest: false,
    ...overrides,
  };
}

function summarizeStripes(stripes: SectorStripeState[] | undefined): string[] {
  return (stripes ?? []).map(stripe => `${stripe.tone}${stripe.stale ? ' stale' : ''}`);
}
