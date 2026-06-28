import { type ReactNode, useEffect, useMemo, useState } from 'react';
import { formatGap, formatLapTime, formatNumber } from '../format';
import { BestLapBoardResponse, BestLapRow, BestLapWindow, DriverSnapshot, KioskDisplayMode, LastFinishedSessionResponse, LastFinishedSessionRow, LiveSessionConnection, LiveSessionSnapshot, SectorSnapshot, startLiveSessionFeed, TracksideApiClient } from '../tracksideApi';

type ViewMode = BestLapWindow | 'last' | 'live';

const supportedPaths: Record<string, ViewMode> = {
  '/monthly': 'monthly',
  '/weekly': 'weekly',
  '/daily': 'daily',
  '/last-session': 'last',
  '/live': 'live',
};

function getViewFromPath(path: string): ViewMode | null {
  const normalized = path.toLowerCase().replace(/\/+$/, '') || '/';
  return supportedPaths[normalized] ?? null;
}

/** Main kiosk application shell. */
export function App() {
  const client = useMemo(() => new TracksideApiClient(), []);
  const [snapshot, setSnapshot] = useState<LiveSessionSnapshot | null>(null);
  const [board, setBoard] = useState<BestLapBoardResponse | null>(null);
  const [lastSession, setLastSession] = useState<LastFinishedSessionResponse | null>(null);
  const [view, setView] = useState<ViewMode>(() => getViewFromPath(window.location.pathname) ?? 'monthly');
  const [status, setStatus] = useState('Loading configuration...');
  const [boardStatus, setBoardStatus] = useState('Loading best laps...');

  useEffect(() => {
    const currentView = getViewFromPath(window.location.pathname);
    if (currentView) {
      return;
    }

    let cancelled = false;
    client.getClientConfiguration()
      .then(configuration => {
        if (!cancelled) {
          setView(toViewMode(configuration.defaultDisplayMode));
        }
      })
      .catch(() => {
      });

    return () => {
      cancelled = true;
    };
  }, [client]);

  useEffect(() => {
    let connection: LiveSessionConnection | null = null;
    let cancelled = false;

    startLiveSessionFeed(
      client,
      nextSnapshot => {
        if (!cancelled) {
          setSnapshot(nextSnapshot);
        }
      },
      nextStatus => {
        if (!cancelled) {
          setStatus(nextStatus);
        }
      },
    ).then(nextConnection => {
      connection = nextConnection;
      if (cancelled) {
        void connection.stop();
      }
    }).catch(error => {
      if (!cancelled) {
        setStatus(`Unable to connect: ${error instanceof Error ? error.message : String(error)}`);
      }
    });

    return () => {
      cancelled = true;
      void connection?.stop();
    };
  }, [client]);

  useEffect(() => {
    if (view === 'live' || view === 'last') {
      return;
    }

    const boardWindow: BestLapWindow = view;
    let cancelled = false;
    async function loadBoard() {
      try {
        const nextBoard = await client.getBestLaps(boardWindow, 20);
        if (!cancelled) {
          setBoard(nextBoard);
          setBoardStatus('');
        }
      } catch (error) {
        if (!cancelled) {
          setBoard(null);
          setBoardStatus(`Unable to load best laps: ${error instanceof Error ? error.message : String(error)}`);
        }
      }
    }

    void loadBoard();
    const timer = window.setInterval(() => {
      void loadBoard();
    }, 15000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [client, view]);

  useEffect(() => {
    if (view !== 'last') {
      return;
    }

    let cancelled = false;
    async function loadLastSession() {
      try {
        const result = await client.getLastFinishedSession();
        if (!cancelled) {
          setLastSession(result);
          setBoardStatus(result.isAvailable ? '' : 'No finished session yet');
        }
      } catch (error) {
        if (!cancelled) {
          setLastSession(null);
          setBoardStatus(`Unable to load last session: ${error instanceof Error ? error.message : String(error)}`);
        }
      }
    }

    void loadLastSession();
    const timer = window.setInterval(() => {
      void loadLastSession();
    }, 15000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [client, view]);

  return (
    <main className="shell">
      <ShellHeader status={view === 'live' ? status : boardStatus} view={view} flag={snapshot?.session.overallFlag} />

      {view === 'live'
        ? <LiveBoard snapshot={snapshot} />
        : view === 'last'
          ? <LastSessionBoard result={lastSession} />
          : <BestLapBoard board={board} view={view} />}
    </main>
  );
}

interface LiveBoardProps {
  /** Current live-session snapshot. */
  snapshot: LiveSessionSnapshot | null;
}

function LiveBoard({ snapshot }: LiveBoardProps) {
  const [clockAnchor, setClockAnchor] = useState({ seconds: 0, timestamp: Date.now() });
  const [tick, setTick] = useState(Date.now());

  useEffect(() => {
    const currentSeconds = snapshot?.session?.currentSessionSeconds;
    if (currentSeconds == null) {
      return;
    }

    setClockAnchor({
      seconds: currentSeconds,
      timestamp: Date.now(),
    });
  }, [snapshot?.session?.currentSessionSeconds]);

  useEffect(() => {
    if (snapshot?.session?.currentSessionSeconds == null) {
      return;
    }

    const interval = window.setInterval(() => setTick(Date.now()), 50);
    return () => window.clearInterval(interval);
  }, [snapshot?.session?.currentSessionSeconds]);

  const interpolatedClockSeconds = snapshot?.session?.currentSessionSeconds != null
    ? clockAnchor.seconds + Math.max(0, (tick - clockAnchor.timestamp) / 1000)
    : undefined;

  return (
    <>
      <section className="sessionStrip" aria-label="Session summary">
        <Metric label="Track" value={snapshot?.session.trackName} />
        <Metric label="Session" value={snapshot?.session.kind} />
        <Metric label="Clock" value={formatLapTime(interpolatedClockSeconds)} />
      </section>

      <BoardPanel title="Live Board" meta={`${snapshot?.drivers.length ?? 0} drivers`} live>
        <div className="tableFrame liveBoardFrame" style={{ '--flag-color': getFlagColor(snapshot?.session.overallFlag) } as React.CSSProperties}>
          <LeaderboardTable drivers={snapshot?.drivers ?? []} />
        </div>
      </BoardPanel>
    </>
  );
}

interface BestLapBoardProps {
  /** Current board response. */
  board: BestLapBoardResponse | null;
  /** Selected view. */
  view: BestLapWindow;
}

function BestLapBoard({ board, view }: BestLapBoardProps) {
  const title = view === 'monthly' ? 'Monthly Track' : `${capitalize(view)} Bests`;
  const rows = board?.rows ?? [];
  return (
    <>
      <MetricGrid label="Best-lap summary">
        <Metric label="Track" value={board?.trackName ?? (view === 'monthly' ? 'Not set' : 'All tracks')} />
        <Metric label="Since" value={formatDate(board?.fromUtc)} />
      </MetricGrid>

      <BoardPanel title={title} meta="">
        <div className="tableFrame">
          <BestLapTable rows={rows} showTrack={!board?.trackName} />
        </div>
      </BoardPanel>
    </>
  );
}

interface BestLapTableProps {
  /** Counted timed lap rows. */
  rows: BestLapRow[];
  /** True when the track column should be shown. */
  showTrack: boolean;
}

function BestLapTable({ rows, showTrack }: BestLapTableProps) {
  if (rows.length === 0) {
    return <p className="emptyState">No counted timed laps yet.</p>;
  }

  return (
    <table className="bestLapTable">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Driver</th>
          <th>Rig</th>
          {showTrack ? <th>Track</th> : null}
          <th>Lap</th>
          <th>Time</th>
          <th>Set</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(row => (
          <tr key={`${row.rank}-${row.rigName}-${row.lapNumber}-${row.observedUtc}`} className={row.rank === 1 ? 'bestLapRow' : undefined}>
            <td className="rankCell">{row.rank}</td>
            <td>{row.displayName}</td>
            <td>{row.rigName}</td>
            {showTrack ? <td>{row.trackName}</td> : null}
            <td>{row.lapNumber}</td>
            <td className={row.rank === 1 ? 'bestTime' : undefined}>{formatLapTime(row.lapSeconds)}</td>
            <td>{formatDate(row.observedUtc)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

interface LeaderboardTableProps {
  /** Driver rows already sorted by the backend leaderboard rules. */
  drivers: DriverSnapshot[];
}

function LeaderboardTable({ drivers }: LeaderboardTableProps) {
  return (
    <table className="leaderboardTable">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Driver</th>
          <th>Rig</th>
          <th>Laps</th>
          <th>Best</th>
          <th>Current</th>
          <th>S1</th>
          <th>S2</th>
          <th>S3</th>
          <th>Gap</th>
        </tr>
      </thead>
      <tbody>
        {drivers.map(driver => (
          <tr key={driver.driverId} className={driver.isOverallBestLap ? 'bestLapRow' : undefined}>
            <td className="rankCell">{driver.leaderboardRank || driver.position || '-'}</td>
            <td>
              <div className="driverCell">
                <strong>{driver.displayName}</strong>
                <span>{formatPercent(driver.trackPositionPercent)}</span>
              </div>
            </td>
            <td>{driver.rigName}</td>
            <td>{driver.completedLaps}</td>
            <td className={driver.isOverallBestLap ? 'bestTime' : undefined}>{formatLapTime(driver.bestLapSeconds)}</td>
            <td>{formatLapTime(driver.currentLapSeconds)}</td>
            {[1, 2, 3].map(number => (
              <SectorCell key={number} sector={driver.sectors.find(candidate => candidate.number === number)} />
            ))}
            <td>{formatGap(driver.gapToLeaderSeconds, driver.lapsBehindLeader)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

interface SectorCellProps {
  /** Sector timing row for this cell. */
  sector: SectorSnapshot | undefined;
}

function SectorCell({ sector }: SectorCellProps) {
  return (
    <td className={sector?.isOverallBest ? 'bestTime sectorCell' : 'sectorCell'}>
      <span>{formatLapTime(sector?.bestSeconds)}</span>
      <small>{formatLapTime(sector?.currentSeconds ?? sector?.lastSeconds)}</small>
    </td>
  );
}

function formatPercent(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? '-' : `${formatNumber(value, 1)}%`;
}

function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return new Date(value).toLocaleString([], { month: 'short', day: '2-digit', hour: '2-digit', minute: '2-digit' });
}

function capitalize(value: string): string {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}

function toViewMode(displayMode: KioskDisplayMode): ViewMode {
  switch (displayMode) {
    case 'Weekly':
      return 'weekly';
    case 'Daily':
      return 'daily';
    case 'LastSession':
      return 'last';
    case 'Live':
      return 'live';
    default:
      return 'monthly';
  }
}

interface MetricProps {
  /** Short label shown above the value. */
  label: string;
  /** Value rendered for the metric. */
  value: string | number | null | undefined;
  /** Feature important metrics with larger text. */
  featured?: boolean;
}

function Metric({ label, value, featured }: MetricProps) {
  return (
    <div className={`metric${featured ? ' featured' : ''}`}>
      <span>{label}</span>
      <strong>{value ?? '-'}</strong>
    </div>
  );
}

interface SlimMetricProps {
  label: string;
  value: string | number | null | undefined;
}

function SlimMetric({ label, value }: SlimMetricProps) {
  return (
    <div className="slimMetric">
      <span>{label}</span>
      <strong>{value ?? '-'}</strong>
    </div>
  );
}

interface FlagMetricProps {
  flag: string | null | undefined;
}

function FlagMetric({ flag }: FlagMetricProps) {
  return (
    <div className="flagSwatch" aria-label="Flag" role="img" style={{ background: getFlagColor(flag) }}>
      <span>{flag ?? '-'}</span>
    </div>
  );
}

function getFlagColor(flag: string | null | undefined): string {
  if (!flag) {
    return 'rgba(255, 255, 255, 0.08)';
  }

  switch (flag.toLowerCase()) {
    case 'green':
    case 'greenflag':
      return '#2ecc71';
    case 'yellow':
    case 'yellowflag':
      return '#f1c40f';
    case 'red':
    case 'redflag':
      return '#ff202d';
    case 'blue':
    case 'blueflag':
      return '#3498db';
    case 'white':
    case 'whiteflag':
      return '#ecf0f1';
    case 'black':
    case 'blackflag':
      return '#2f3640';
    case 'checker':
    case 'checkered':
    case 'checkeredflag':
      return 'linear-gradient(135deg, #ffffff 25%, #000000 25%, #000000 50%, #ffffff 50%, #ffffff 75%, #000000 75%, #000000)';
    default:
      return 'rgba(255, 255, 255, 0.08)';
  }
}

interface ShellHeaderProps {
  /** Current status message shown in the masthead. */
  status: string;
  /** Selected display mode. */
  view: ViewMode;
  /** Current session flag for live view. */
  flag: string | null | undefined;
}

function ShellHeader({ status, view, flag }: ShellHeaderProps) {
  const isConnected = view === 'live' && /^Connected through\b/i.test(status);
  const sanitized = sanitizeStatus(status);

  return (
    <header className="topbar">
      <BrandMark />
      <div className="topbarMeta" aria-live="polite">
        <div className="topbarPills">
          {isConnected ? <span className="statusPill connection">CONNECTED</span> : null}
          <span className={view === 'live' ? 'statusPill live' : 'statusPill'}>
            {view === 'live' ? 'LIVE' : 'Leaderboard'}
          </span>
        </div>
        {sanitized ? <p>{sanitized}</p> : null}
      </div>
      {view === 'live' && flag ? (
        <div className="topbarFlag">
          <div className="flagSwatch" style={{ background: getFlagColor(flag) }}>
            <span>{flag}</span>
          </div>
        </div>
      ) : null}
    </header>
  );
}

function sanitizeStatus(status: string): string {
  return status.replace(/\b(?:shared[- ]memory\b.*|Connected through\b.*)$/i, '').trim();
}

function BrandMark() {
  return (
    <div className="brandLockup">
      <img className="brandLogo" src="/brand/gearbox-trackside-logo-dark-wordmark.png" alt="Gearbox Trackside" />
      <h1 className="srOnly">Trackside Kiosk</h1>
    </div>
  );
}



interface MetricGridProps {
  /** Accessible label for the metric group. */
  label: string;
  /** Metric cards. */
  children: ReactNode;
}

function MetricGrid({ label, children }: MetricGridProps) {
  return (
    <section className="summary" aria-label={label}>
      {children}
    </section>
  );
}

interface BoardPanelProps {
  /** Panel title. */
  title: string;
  /** Compact panel metadata. */
  meta: string;
  /** Panel content. */
  children: ReactNode;
  /** True for live boards to use special styling. */
  live?: boolean;
}

function BoardPanel({ title, meta, children, live }: BoardPanelProps & { live?: boolean }) {
  return (
    <section className={`boardPanel${live ? ' live' : ''}`}>
      <div className="sectionHeader">
        <h2>{title}</h2>
        {meta ? <span>{meta}</span> : null}
      </div>
      {children}
    </section>
  );
}

interface LastSessionBoardProps {
  /** Last finished session result. */
  result: LastFinishedSessionResponse | null;
}

function LastSessionBoard({ result }: LastSessionBoardProps) {
  const rows = result?.rows ?? [];
  return (
    <>
      <MetricGrid label="Last session summary">
        <Metric label="Track" value={result?.trackName ?? 'No finished session'} />
        <Metric label="Session" value={result?.sessionKind} />
        <Metric label="Finished" value={formatDate(result?.lastSeenUtc)} />
      </MetricGrid>

      <BoardPanel title="Last Session" meta="">
        <div className="tableFrame">
          <LastSessionTable rows={rows} />
        </div>
      </BoardPanel>
    </>
  );
}

interface LastSessionTableProps {
  /** Result rows. */
  rows: LastFinishedSessionRow[];
}

function LastSessionTable({ rows }: LastSessionTableProps) {
  if (rows.length === 0) {
    return <p className="emptyState">No finished session has been observed yet.</p>;
  }

  return (
    <table className="bestLapTable">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Driver</th>
          <th>Rig</th>
          <th>Laps</th>
          <th>Best</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(row => (
          <tr key={`${row.rank}-${row.rigName}`}>
            <td className="rankCell">{row.rank}</td>
            <td>{row.displayName}</td>
            <td>{row.rigName}</td>
            <td>{row.completedLaps}</td>
            <td>{formatLapTime(row.bestLapSeconds)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}