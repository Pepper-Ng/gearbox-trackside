import { describe, expect, it, vi } from 'vitest';
import { LiveSessionFeedClient, LiveSessionSnapshot, TrackGeometryResponse, startLiveSessionFeed } from './tracksideApi';

describe('startLiveSessionFeed', () => {
  it('loads the current REST snapshot before connecting live updates', async () => {
    const calls: string[] = [];
    const pushedSnapshots: Array<(snapshot: LiveSessionSnapshot) => void> = [];
    const pushedGeometry: Array<(geometry: TrackGeometryResponse) => void> = [];
    const receivedSnapshots: LiveSessionSnapshot[] = [];
    const receivedGeometry: TrackGeometryResponse[] = [];
    const statuses: string[] = [];
    const currentSnapshot = makeSnapshot(1);
    const liveSnapshot = makeSnapshot(2);
    const initialGeometry = makeGeometry(false);
    const liveGeometry = makeGeometry(true);
    const connection = { stop: vi.fn(async () => undefined) };
    const client: LiveSessionFeedClient = {
      async getClientConfiguration() {
        calls.push('configuration');
        return {
          currentSessionPath: '/api/live-session/current',
          liveSessionHubPath: '/hubs/live-session',
          trackGeometryPath: '/api/track-geometry/current',
          healthPath: '/api/health',
          recommendedReconnectSeconds: 2,
          defaultDisplayMode: 'Monthly',
          driverTrackerClientRefreshHz: 50,
        };
      },
      async getCurrentSession(path) {
        calls.push(`current:${path}`);
        return currentSnapshot;
      },
      async getBestLaps() {
        return {
          window: 'monthly',
          mode: 'per-driver',
          trackName: 'Loch Drummond - Short',
          rows: [],
        };
      },
      async getLastFinishedSession() {
        return { isAvailable: false, rows: [] };
      },
      async getTrackGeometry() {
        calls.push('geometry:/api/track-geometry/current');
        return initialGeometry;
      },
      async connectLiveSession(path, onSnapshot, onTrackGeometry) {
        calls.push(`hub:${path}`);
        pushedSnapshots.push(onSnapshot);
        if (onTrackGeometry) {
          pushedGeometry.push(onTrackGeometry);
        }
        return connection;
      },
    };

    const handle = await startLiveSessionFeed(
      client,
      snapshot => receivedSnapshots.push(snapshot),
      status => statuses.push(status),
      geometry => receivedGeometry.push(geometry),
    );
    pushedSnapshots[0](liveSnapshot);
    pushedGeometry[0](liveGeometry);

    expect(handle).toBe(connection);
    expect(calls).toEqual(['configuration', 'current:/api/live-session/current', 'geometry:/api/track-geometry/current', 'hub:/hubs/live-session']);
    expect(receivedSnapshots.map(snapshot => snapshot.updateSequence)).toEqual([1, 2]);
    expect(receivedGeometry.map(geometry => geometry.isAvailable)).toEqual([false, true]);
    expect(statuses).toEqual(['Connected through REST recovery endpoint', 'Connected through SignalR live updates']);
  });

  it('keeps REST recovery connected when SignalR cannot connect', async () => {
    const statuses: string[] = [];
    const receivedSnapshots: LiveSessionSnapshot[] = [];
    const client = makeFeedClient({
      async connectLiveSession() {
        throw new Error('hub unavailable');
      },
    });

    const handle = await startLiveSessionFeed(
      client,
      snapshot => receivedSnapshots.push(snapshot),
      status => statuses.push(status),
    );

    await expect(handle.stop()).resolves.toBeUndefined();
    expect(receivedSnapshots.map(snapshot => snapshot.updateSequence)).toEqual([1]);
    expect(statuses).toEqual([
      'Connected through REST recovery endpoint',
      'Connected through REST recovery endpoint; SignalR unavailable: hub unavailable',
    ]);
  });
});

function makeFeedClient(overrides: Partial<LiveSessionFeedClient> = {}): LiveSessionFeedClient {
  return {
    async getClientConfiguration() {
      return {
        currentSessionPath: '/api/live-session/current',
        liveSessionHubPath: '/hubs/live-session',
        trackGeometryPath: '/api/track-geometry/current',
        healthPath: '/api/health',
        recommendedReconnectSeconds: 2,
        defaultDisplayMode: 'Monthly',
        driverTrackerClientRefreshHz: 50,
      };
    },
    async getCurrentSession() {
      return makeSnapshot(1);
    },
    async getBestLaps() {
      return { window: 'monthly', mode: 'per-driver', rows: [] };
    },
    async getLastFinishedSession() {
      return { isAvailable: false, rows: [] };
    },
    async getTrackGeometry() {
      return makeGeometry(false);
    },
    async connectLiveSession() {
      return { stop: vi.fn(async () => undefined) };
    },
    ...overrides,
  };
}

function makeGeometry(isAvailable: boolean): TrackGeometryResponse {
  return {
    isAvailable,
    sampleCount: isAvailable ? 180 : 0,
    coveragePercent: isAvailable ? 100 : 0,
    isCompleteLap: isAvailable,
    points: [],
  };
}

function makeSnapshot(updateSequence: number): LiveSessionSnapshot {
  return {
    source: 'test',
    status: 'ready',
    timestampUtc: '2026-06-24T12:00:00+00:00',
    updateSequence,
    session: {
      trackName: 'Loch Drummond - Short',
      kind: 'Practice',
      phase: 'GreenFlag',
      vehicleCount: 0,
      overallFlag: 'GREEN',
    },
    drivers: [],
  };
}