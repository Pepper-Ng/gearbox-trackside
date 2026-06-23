import * as signalR from '@microsoft/signalr';

/** Endpoint configuration returned by the backend for browser clients. */
export interface ClientConfiguration {
  /** REST path for initial live-session load and reconnect recovery. */
  currentSessionPath: string;
  /** SignalR hub path for live-session pushes. */
  liveSessionHubPath: string;
  /** Health endpoint path for diagnostics. */
  healthPath: string;
  /** Recommended reconnect delay in seconds. */
  recommendedReconnectSeconds: number;
}

/** Coarse session category used by the kiosk. */
export type SessionKind = 'Unknown' | 'Practice' | 'Qualifying' | 'Race';

/** Coarse session phase used by the kiosk. */
export type SessionPhase = 'Unknown' | 'Garage' | 'GreenFlag' | 'SessionOver';

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
  /** Ambient air temperature in Celsius. */
  airTemperatureCelsius?: number | null;
  /** Track temperature in Celsius. */
  trackTemperatureCelsius?: number | null;
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
  /** True when this is the overall best sector. */
  isOverallBest: boolean;
}

/** Normalized driver row shown on the kiosk. */
export interface DriverSnapshot {
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

/** Browser API client for REST and SignalR communication with Trackside.Host. */
export class TracksideApiClient {
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

  /** Opens a SignalR connection and invokes the supplied callback for each pushed snapshot. */
  public async connectLiveSession(
    hubPath: string,
    onSnapshot: (snapshot: LiveSessionSnapshot) => void,
  ): Promise<signalR.HubConnection> {
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