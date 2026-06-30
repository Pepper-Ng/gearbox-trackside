import { type DriverSnapshot, type LiveSessionSnapshot, type SectorSnapshot } from '../tracksideApi';

export type SectorStripeTone = 'pending' | 'regular' | 'personal' | 'overall';

export interface SectorStripeState {
  number: number;
  tone: SectorStripeTone;
  stale: boolean;
}

interface SectorStripeDriverCacheEntry {
  completedLaps: number;
  currentLapStarted: boolean;
  currentLapSeconds: Map<number, number>;
  previousLapSeconds: Map<number, number>;
}

export interface SectorStripeCache {
  sessionKey: string;
  sessionClockSeconds: number | null;
  drivers: Map<string, SectorStripeDriverCacheEntry>;
}

interface SectorStripeBuildResult {
  states: Map<string, SectorStripeState[]>;
  cache: SectorStripeCache;
}

const sectorNumbers = [1, 2, 3] as const;

export const defaultSectorStripeStates: SectorStripeState[] = sectorNumbers.map(number => ({
  number,
  tone: 'pending',
  stale: false,
}));

export function createEmptySectorStripeCache(): SectorStripeCache {
  return {
    sessionKey: '',
    sessionClockSeconds: null,
    drivers: new Map(),
  };
}

export function buildSectorStripeStates(
  snapshot: LiveSessionSnapshot | null,
  previousCache: SectorStripeCache,
): SectorStripeBuildResult {
  if (!snapshot) {
    return {
      states: new Map(),
      cache: createEmptySectorStripeCache(),
    };
  }

  const sessionKey = getSectorStripeSessionKey(snapshot);
  const shouldReset = shouldResetCache(snapshot, sessionKey, previousCache);
  const previousDrivers = shouldReset ? new Map<string, SectorStripeDriverCacheEntry>() : previousCache.drivers;
  const globalBestBySector = buildGlobalBestSectors(snapshot.drivers);
  const states = new Map<string, SectorStripeState[]>();
  const nextDrivers = new Map<string, SectorStripeDriverCacheEntry>();

  for (const driver of snapshot.drivers) {
    const previous = previousDrivers.get(driver.driverId);
    const entry = buildDriverCacheEntry(driver, previous);
    states.set(driver.driverId, buildDriverStripeStates(driver, entry, globalBestBySector));
    nextDrivers.set(driver.driverId, entry);
  }

  return {
    states,
    cache: {
      sessionKey,
      sessionClockSeconds: snapshot.session.currentSessionSeconds ?? null,
      drivers: nextDrivers,
    },
  };
}

function getSectorStripeSessionKey(snapshot: LiveSessionSnapshot): string {
  const session = snapshot.session;
  return [
    snapshot.source,
    session.trackName,
    session.kind,
    session.totalLaps ?? '',
    session.scheduledDurationSeconds ?? '',
  ].join('|');
}

function shouldResetCache(snapshot: LiveSessionSnapshot, sessionKey: string, previousCache: SectorStripeCache): boolean {
  if (previousCache.sessionKey !== sessionKey) {
    return true;
  }

  const currentClock = snapshot.session.currentSessionSeconds;
  if (isFiniteNumber(currentClock)
    && isFiniteNumber(previousCache.sessionClockSeconds)
    && currentClock + 1 < previousCache.sessionClockSeconds) {
    return true;
  }

  return snapshot.drivers.some(driver => {
    const previous = previousCache.drivers.get(driver.driverId);
    return previous !== undefined && driver.completedLaps < previous.completedLaps;
  });
}

function buildDriverCacheEntry(driver: DriverSnapshot, previous: SectorStripeDriverCacheEntry | undefined): SectorStripeDriverCacheEntry {
  const previousLapSeconds = getPreviousLapSeconds(driver, previous);
  const completedThrough = getCompletedCurrentLapSectors(driver);
  const sameLapAsPrevious = previous !== undefined && driver.completedLaps === previous.completedLaps;
  const currentLapStarted = completedThrough > 0 || (sameLapAsPrevious && previous.currentLapStarted);
  const currentLapSeconds = getCurrentLapSeconds(driver, previous, completedThrough, sameLapAsPrevious);

  return {
    completedLaps: driver.completedLaps,
    currentLapStarted,
    currentLapSeconds,
    previousLapSeconds,
  };
}

function getPreviousLapSeconds(driver: DriverSnapshot, previous: SectorStripeDriverCacheEntry | undefined): Map<number, number> {
  if (driver.completedLaps > 0) {
    return getLastCompletedLapSeconds(driver);
  }

  return new Map(previous?.previousLapSeconds ?? []);
}

function getLastCompletedLapSeconds(driver: DriverSnapshot): Map<number, number> {
  const seconds = new Map<number, number>();
  for (const sectorNumber of sectorNumbers) {
    const sector = findSector(driver, sectorNumber);
    if (isFiniteNumber(sector?.lastSeconds)) {
      seconds.set(sectorNumber, sector.lastSeconds);
    }
  }

  return seconds;
}

function getCurrentLapSeconds(
  driver: DriverSnapshot,
  previous: SectorStripeDriverCacheEntry | undefined,
  completedThrough: number,
  sameLapAsPrevious: boolean,
): Map<number, number> {
  const currentSeconds = new Map<number, number>();

  for (const sectorNumber of sectorNumbers) {
    if (sectorNumber > completedThrough) {
      continue;
    }

    const sector = findSector(driver, sectorNumber);
    if (isFiniteNumber(sector?.currentSeconds)) {
      currentSeconds.set(sectorNumber, sector.currentSeconds);
    } else if (sameLapAsPrevious && previous !== undefined && previous.currentLapSeconds.has(sectorNumber)) {
      currentSeconds.set(sectorNumber, previous.currentLapSeconds.get(sectorNumber)!);
    }
  }

  return currentSeconds;
}

function getCompletedCurrentLapSectors(driver: DriverSnapshot): number {
  const byCurrentSector = getCompletedSectorsForRf2CurrentSector(driver.currentSector);
  if (byCurrentSector !== null) {
    return byCurrentSector;
  }

  if (isFiniteNumber(findSector(driver, 2)?.currentSeconds)) {
    return 2;
  }

  return isFiniteNumber(findSector(driver, 1)?.currentSeconds) ? 1 : 0;
}

function getCompletedSectorsForRf2CurrentSector(currentSector: number | null | undefined): number | null {
  switch (currentSector) {
    case 1:
      return 0;
    case 2:
      return 1;
    case 0:
    case 3:
      return 2;
    default:
      return null;
  }
}

function buildDriverStripeStates(
  driver: DriverSnapshot,
  entry: SectorStripeDriverCacheEntry,
  globalBestBySector: Map<number, number>,
): SectorStripeState[] {
  // Full color means the current lap's completed sectors, or the last lap until new S1 completes.
  // Stale color means the previous lap after the current lap has a new S1/S2 time.
  return sectorNumbers.map(number => {
    const sector = findSector(driver, number);
    const currentSeconds = entry.currentLapSeconds.get(number);
    if (entry.currentLapStarted && isFiniteNumber(currentSeconds)) {
      return createSectorStripe(number, currentSeconds, sector, globalBestBySector, false);
    }

    const previousSeconds = entry.previousLapSeconds.get(number);
    if (isFiniteNumber(previousSeconds)) {
      return createSectorStripe(number, previousSeconds, sector, globalBestBySector, entry.currentLapStarted);
    }

    return { number, tone: 'pending', stale: false };
  });
}

function createSectorStripe(
  sectorNumber: number,
  seconds: number,
  sector: SectorSnapshot | undefined,
  globalBestBySector: Map<number, number>,
  stale: boolean,
): SectorStripeState {
  return {
    number: sectorNumber,
    tone: getSectorStripeTone(seconds, sector, globalBestBySector.get(sectorNumber)),
    stale,
  };
}

function buildGlobalBestSectors(drivers: DriverSnapshot[]): Map<number, number> {
  const best = new Map<number, number>();
  for (const driver of drivers) {
    for (const sector of driver.sectors) {
      if (!isFiniteNumber(sector.bestSeconds) || sector.bestSeconds <= 0) {
        continue;
      }

      const existing = best.get(sector.number);
      if (existing === undefined || sector.bestSeconds < existing) {
        best.set(sector.number, sector.bestSeconds);
      }
    }
  }

  return best;
}

function getSectorStripeTone(seconds: number, sector: SectorSnapshot | undefined, globalBestSeconds: number | undefined): SectorStripeTone {
  if (isFiniteNumber(globalBestSeconds) && isSameTime(seconds, globalBestSeconds)) {
    return 'overall';
  }

  if (isFiniteNumber(sector?.bestSeconds) && seconds <= sector.bestSeconds + 0.0005) {
    return 'personal';
  }

  return 'regular';
}

function findSector(driver: DriverSnapshot, number: number): SectorSnapshot | undefined {
  return driver.sectors.find(candidate => candidate.number === number);
}

function isFiniteNumber(value: number | null | undefined): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

function isSameTime(left: number, right: number): boolean {
  return Math.abs(left - right) < 0.0005;
}