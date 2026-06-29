import { describe, expect, it, vi } from 'vitest';
import { LiveSessionFeedClient, LiveSessionSnapshot, startLiveSessionFeed } from './tracksideApi';

describe('startLiveSessionFeed', () => {
  it('loads the current REST snapshot before connecting live updates', async () => {
    const calls: string[] = [];
    const pushedSnapshots: Array<(snapshot: LiveSessionSnapshot) => void> = [];
    const receivedSnapshots: LiveSessionSnapshot[] = [];
    const statuses: string[] = [];
    const currentSnapshot = makeSnapshot(1);
    const liveSnapshot = makeSnapshot(2);
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
        return { isAvailable: false, sampleCount: 0, coveragePercent: 0, isCompleteLap: false, points: [] };
      },
      async connectLiveSession(path, onSnapshot) {
        calls.push(`hub:${path}`);
        pushedSnapshots.push(onSnapshot);
        return connection;
      },
    };

    const handle = await startLiveSessionFeed(
      client,
      snapshot => receivedSnapshots.push(snapshot),
      status => statuses.push(status),
    );
    pushedSnapshots[0](liveSnapshot);

    expect(handle).toBe(connection);
    expect(calls).toEqual(['configuration', 'current:/api/live-session/current', 'hub:/hubs/live-session']);
    expect(receivedSnapshots.map(snapshot => snapshot.updateSequence)).toEqual([1, 2]);
    expect(statuses).toEqual(['Connected through REST recovery endpoint', 'Connected through SignalR live updates']);
  });
});

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