import * as signalR from '@microsoft/signalr';

/** Endpoint configuration returned by the backend for browser clients. */
export interface ClientConfiguration {
  /** REST path for initial live-session load and reconnect recovery. */
  currentSessionPath: string;
  /** SignalR hub path for live-session pushes. */
  liveSessionHubPath: string;
  /** REST path for current track geometry. */
  trackGeometryPath: string;
  /** Health endpoint path for diagnostics. */
  healthPath: string;
  /** Recommended reconnect delay in seconds. */
  recommendedReconnectSeconds: number;
  /** Default display mode a kiosk screen should open with. */
  defaultDisplayMode: KioskDisplayMode;
  /** Browser-side driver tracker refresh rate in Hertz. */
  driverTrackerClientRefreshHz: number;
}

/** Supported backend-configured kiosk display modes. */
export type KioskDisplayMode = 'Monthly' | 'Weekly' | 'Daily' | 'LastSession' | 'Live' | 'Tracker';

/** Coarse session category used by the kiosk. */
export type SessionKind = 'Unknown' | 'Practice' | 'Qualifying' | 'Race';

/** Coarse session phase used by the kiosk. */
export type SessionPhase = 'Unknown' | 'Garage' | 'GreenFlag' | 'SessionOver';

/** Supported best-lap board windows. */
export type BestLapWindow = 'daily' | 'weekly' | 'monthly' | 'all';

/** Supported best-lap ranking modes. */
export type BestLapBoardMode = 'per-driver' | 'all-laps';

/** Session-level metadata delivered by the backend. */
export interface LiveSessionInfo {
  /** Human-readable track name. */
  trackName: string;
  /** Session category used for sorting and display. */
  kind: SessionKind;
  /** Current session phase. */
  phase: SessionPhase;
  /** Current session clock in seconds. */
  currentSessionSeconds?: number | null;
  /** Scheduled session duration in seconds. */
  scheduledDurationSeconds?: number | null;
  /** Full lap distance in meters. */
  lapDistanceMeters?: number | null;
  /** Number of visible driver rows. */
  vehicleCount: number;
  /** Ambient air temperature in Celsius. */
  airTemperatureCelsius?: number | null;
  /** Track temperature in Celsius. */
  trackTemperatureCelsius?: number | null;
  /** Rain intensity normalized between 0 and 1. */
  rainIntensity?: number | null;
  /** Cloud intensity normalized between 0 and 1. */
  cloudIntensity?: number | null;
  /** Track wetness normalized between 0 and 1. */
  trackWetness?: number | null;
  /** Concise overall flag text. */
  overallFlag: string;
}

/** One sector's timing values for a driver. */
export interface SectorSnapshot {
  /** One-based sector number. */
  number: number;
  /** Driver's best sector time in seconds. */
  bestSeconds?: number | null;
  /** Driver's last sector time in seconds. */
  lastSeconds?: number | null;
  /** Driver's current in-progress sector time in seconds. */
  currentSeconds?: number | null;
  /** True when this is the overall best sector. */
  isOverallBest: boolean;
}

/** Normalized driver row shown on the kiosk. */
export interface DriverSnapshot {
  /** One-based leaderboard rank after Trackside ordering rules. */
  leaderboardRank: number;
  /** Stable source-provided driver or scoring identifier. */
  driverId: string;
  /** Underlying fixed rig name. */
  rigName: string;
  /** Staff-facing display name. */
  displayName: string;
  /** Source-reported vehicle name. */
  vehicleName: string;
  /** Current scored position. */
  position?: number | null;
  /** True when this row owns the best known lap. */
  isOverallBestLap: boolean;
  /** Completed lap count. */
  completedLaps: number;
  /** Best lap time in seconds. */
  bestLapSeconds?: number | null;
  /** Last completed lap time in seconds. */
  lastLapSeconds?: number | null;
  /** Current lap time in seconds. */
  currentLapSeconds?: number | null;
  /** Gap to leader in seconds. */
  gapToLeaderSeconds?: number | null;
  /** Gap to next car ahead in seconds. */
  gapToNextSeconds?: number | null;
  /** Laps behind the leader. */
  lapsBehindLeader?: number | null;
  /** Current zero-based rFactor 2 sector index. */
  currentSector?: number | null;
  /** Approximate lap progress percentage. */
  trackPositionPercent?: number | null;
  /** Current lap distance in meters. */
  lapDistanceMeters?: number | null;
  /** Raw X coordinate from the scoring source when available. */
  posX?: number | null;
  /** Raw Y coordinate from the scoring source when available. */
  posY?: number | null;
  /** Raw Z coordinate from the scoring source when available. */
  posZ?: number | null;
  /** True when the source reports the driver between pit entry and pit exit. */
  isInPits: boolean;
  /** True when the source reports the driver in the garage stall. */
  isInGarageStall: boolean;
  /** Lateral distance from the approximate center path in meters. */
  pathLateralMeters?: number | null;
  /** Relevant track edge distance from the approximate center path in meters. */
  trackEdgeMeters?: number | null;
  /** Sector timing rows. */
  sectors: SectorSnapshot[];
}

/** Full browser-facing live-session snapshot. */
export interface LiveSessionSnapshot {
  /** Logical source name, for example fixture. */
  source: string;
  /** Source status for diagnostics. */
  status: string;
  /** UTC timestamp string produced by the backend. */
  timestampUtc: string;
  /** Monotonic update sequence. */
  updateSequence: number;
  /** Session-level metadata. */
  session: LiveSessionInfo;
  /** Current driver rows. */
  drivers: DriverSnapshot[];
}

/** Generated track geometry for the current driver tracker page. */
export interface TrackGeometryResponse {
  /** True when enough geometry points are available to draw a track line. */
  isAvailable: boolean;
  /** Track name associated with the geometry. */
  trackName?: string | null;
  /** Source that last contributed geometry samples. */
  source?: string | null;
  /** UTC timestamp when the geometry cache was last updated. */
  updatedUtc?: string | null;
  /** Number of samples in the generated geometry. */
  sampleCount: number;
  /** Approximate lap coverage represented by geometry samples. */
  coveragePercent: number;
  /** True when enough start-to-finish samples exist for reliable rendering. */
  isCompleteLap: boolean;
  /** Raw world-coordinate bounds for normalizing live driver markers. */
  bounds?: TrackGeometryBounds | null;
  /** Normalized geometry points ordered by lap progress. */
  points: TrackGeometryPoint[];
}

/** Raw X/Z world-coordinate bounds for a generated track geometry. */
export interface TrackGeometryBounds {
  minWorldX: number;
  maxWorldX: number;
  minWorldZ: number;
  maxWorldZ: number;
}

/** One normalized point in a generated track geometry. */
export interface TrackGeometryPoint {
  worldX: number;
  worldZ: number;
  x: number;
  y: number;
  progressPercent: number;
}

/** Active monthly track period returned by the backend. */
export interface MonthlyTrackResponse {
  /** True when a monthly track has been set. */
  isActive: boolean;
  /** Active monthly track name. */
  trackName?: string | null;
  /** UTC timestamp when the current monthly period started. */
  startedUtc?: string | null;
  /** Optional admin reason for the period. */
  reason?: string | null;
}

/** One counted timed lap row for historical boards. */
export interface BestLapRow {
  /** One-based rank in the response. */
  rank: number;
  /** Track name associated with the lap. */
  trackName: string;
  /** Staff-facing display name captured with the lap. */
  displayName: string;
  /** Underlying fixed rig name. */
  rigName: string;
  /** Vehicle name captured with the lap. */
  vehicleName: string;
  /** Completed lap number in the session. */
  lapNumber: number;
  /** Lap time in seconds. */
  lapSeconds: number;
  /** UTC timestamp when Trackside observed the lap. */
  observedUtc: string;
}

/** Public best-lap board response. */
export interface BestLapBoardResponse {
  /** Requested board window. */
  window: BestLapWindow;
  /** Ranking mode used by the board. */
  mode: BestLapBoardMode;
  /** Track filter used by the board. */
  trackName?: string | null;
  /** Vehicle/content filter used by the board. */
  vehicleName?: string | null;
  /** Session-kind filter used by the board. */
  sessionKind?: SessionKind | null;
  /** Inclusive lower UTC bound used by the query. */
  fromUtc?: string | null;
  /** Exclusive upper UTC bound used by the query. */
  toUtc?: string | null;
  /** Active monthly track period, when set. */
  monthlyTrack?: MonthlyTrackResponse | null;
  /** Counted timed laps ordered by lap time. */
  rows: BestLapRow[];
}

/** Last finished session result response. */
export interface LastFinishedSessionResponse {
  /** True when a finished session is available. */
  isAvailable: boolean;
  /** Track name associated with the session. */
  trackName?: string | null;
  /** Session kind associated with the result. */
  sessionKind?: SessionKind | null;
  /** UTC timestamp when Trackside last observed the session. */
  lastSeenUtc?: string | null;
  /** Result rows. */
  rows: LastFinishedSessionRow[];
}

/** One row in a last finished session result. */
export interface LastFinishedSessionRow {
  /** One-based rank. */
  rank: number;
  /** Screen name captured for the participant. */
  displayName: string;
  /** Underlying fixed rig name. */
  rigName: string;
  /** Optional linked driver profile id. */
  driverProfileId?: string | null;
  /** Vehicle name. */
  vehicleName: string;
  /** Completed laps. */
  completedLaps: number;
  /** Best lap in seconds. */
  bestLapSeconds?: number | null;
}

/** Minimal connection handle used by React and tests. */
export interface LiveSessionConnection {
  /** Stops receiving live-session updates. */
  stop(): Promise<void>;
}

/** Client shape needed to start a live-session feed. */
export interface LiveSessionFeedClient {
  /** Fetches endpoint configuration from the backend. */
  getClientConfiguration(): Promise<ClientConfiguration>;
  /** Fetches the current snapshot through the REST recovery endpoint. */
  getCurrentSession(path?: string): Promise<LiveSessionSnapshot>;
  /** Fetches a public best-lap board. */
  getBestLaps(window: BestLapWindow, limit?: number, mode?: BestLapBoardMode): Promise<BestLapBoardResponse>;
  /** Fetches the last finished session result. */
  getLastFinishedSession(): Promise<LastFinishedSessionResponse>;
  /** Fetches generated track geometry for the current track. */
  getTrackGeometry(path?: string): Promise<TrackGeometryResponse>;
  /** Opens the live SignalR feed. */
  connectLiveSession(
    hubPath: string,
    onSnapshot: (snapshot: LiveSessionSnapshot) => void,
  ): Promise<LiveSessionConnection>;
}

/** Loads the current snapshot first, then attaches live SignalR updates. */
export async function startLiveSessionFeed(
  client: LiveSessionFeedClient,
  onSnapshot: (snapshot: LiveSessionSnapshot) => void,
  onStatus: (status: string) => void,
): Promise<LiveSessionConnection> {
  const configuration = await client.getClientConfiguration();
  const current = await client.getCurrentSession(configuration.currentSessionPath);
  onSnapshot(current);
  onStatus('Connected through REST recovery endpoint');

  return client.connectLiveSession(configuration.liveSessionHubPath, pushedSnapshot => {
    onSnapshot(pushedSnapshot);
    onStatus('Connected through SignalR live updates');
  });
}

/** Browser API client for REST and SignalR communication with Trackside.Host. */
export class TracksideApiClient implements LiveSessionFeedClient {
  /** Base URL for the backend; empty string means same-origin. */
  private readonly baseUrl: string;

  /** Creates a client for the current origin or an explicit backend origin. */
  public constructor(baseUrl = '') {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  /** Fetches endpoint configuration from the backend. */
  public async getClientConfiguration(): Promise<ClientConfiguration> {
    return this.getJson<ClientConfiguration>('/api/configuration/client');
  }

  /** Fetches the current snapshot through the REST recovery endpoint. */
  public async getCurrentSession(path = '/api/live-session/current'): Promise<LiveSessionSnapshot> {
    return this.getJson<LiveSessionSnapshot>(path);
  }

  /** Fetches a public best-lap board. */
  public async getBestLaps(window: BestLapWindow, limit = 20, mode: BestLapBoardMode = 'per-driver'): Promise<BestLapBoardResponse> {
    return this.getJson<BestLapBoardResponse>(`/api/leaderboards/best-laps?window=${encodeURIComponent(window)}&mode=${encodeURIComponent(mode)}&limit=${limit}`);
  }

  /** Fetches the last finished session result. */
  public async getLastFinishedSession(): Promise<LastFinishedSessionResponse> {
    return this.getJson<LastFinishedSessionResponse>('/api/leaderboards/last-session');
  }

  /** Fetches generated track geometry for the current track. */
  public async getTrackGeometry(path = '/api/track-geometry/current'): Promise<TrackGeometryResponse> {
    return this.getJson<TrackGeometryResponse>(path);
  }

  /** Fetches the active monthly track. */
  public async getMonthlyTrack(): Promise<MonthlyTrackResponse> {
    return this.getJson<MonthlyTrackResponse>('/api/leaderboards/monthly-track');
  }

  /** Opens a SignalR connection and invokes the supplied callback for each pushed snapshot. */
  public async connectLiveSession(
    hubPath: string,
    onSnapshot: (snapshot: LiveSessionSnapshot) => void,
  ): Promise<LiveSessionConnection> {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(this.toUrl(hubPath))
      .withAutomaticReconnect()
      .build();

    connection.on('SessionUpdated', onSnapshot);
    await connection.start();
    return connection;
  }

  private async getJson<T>(path: string): Promise<T> {
    const response = await fetch(this.toUrl(path), { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }
    return response.json() as Promise<T>;
  }

  private toUrl(path: string): string {
    if (/^https?:\/\//i.test(path)) {
      return path;
    }
    const normalizedPath = path.startsWith('/') ? path : `/${path}`;
    return `${this.baseUrl}${normalizedPath}`;
  }
}