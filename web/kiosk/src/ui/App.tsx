import { useEffect, useMemo, useState } from 'react';
import { formatGap, formatLapTime, formatNumber } from '../format';
import { BestLapBoardResponse, BestLapRow, BestLapWindow, DriverSnapshot, KioskDisplayMode, LastFinishedSessionResponse, LastFinishedSessionRow, LiveSessionConnection, LiveSessionSnapshot, SectorSnapshot, startLiveSessionFeed, TracksideApiClient } from '../tracksideApi';

type ViewMode = BestLapWindow | 'last' | 'live';

/** Main kiosk application shell. */
export function App() {
  const client = useMemo(() => new TracksideApiClient(), []);
  const [snapshot, setSnapshot] = useState<LiveSessionSnapshot | null>(null);
  const [board, setBoard] = useState<BestLapBoardResponse | null>(null);
  const [lastSession, setLastSession] = useState<LastFinishedSessionResponse | null>(null);
  const [view, setView] = useState<ViewMode>('monthly');
  const [status, setStatus] = useState('Loading configuration...');
  const [boardStatus, setBoardStatus] = useState('Loading best laps...');

  useEffect(() => {
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
          setBoardStatus(`${nextBoard.rows.length} counted timed laps`);
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
          setBoardStatus(result.isAvailable ? `${result.rows.length} result rows` : 'No finished session yet');
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
      <header className="topbar">
        <div>
          <h1>Trackside</h1>
          <p>{view === 'live' ? status : boardStatus}</p>
        </div>
        <div className="topbarActions">
          <a href="/configuration.html">Admin</a>
          <a href="/api/health">Health</a>
        </div>
      </header>

      <nav className="viewTabs" aria-label="Trackside views">
        <button className={view === 'monthly' ? 'active' : undefined} type="button" onClick={() => setView('monthly')}>Monthly</button>
        <button className={view === 'weekly' ? 'active' : undefined} type="button" onClick={() => setView('weekly')}>Weekly</button>
        <button className={view === 'daily' ? 'active' : undefined} type="button" onClick={() => setView('daily')}>Daily</button>
        <button className={view === 'last' ? 'active' : undefined} type="button" onClick={() => setView('last')}>Last Session</button>
        <button className={view === 'live' ? 'active' : undefined} type="button" onClick={() => setView('live')}>Live</button>
      </nav>

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
  return (
    <>
      <section className="summary" aria-label="Session summary">
        <Metric label="Track" value={snapshot?.session.trackName} />
        <Metric label="Session" value={snapshot?.session.kind} />
        <Metric label="Phase" value={snapshot?.session.phase} />
        <Metric label="Flag" value={snapshot?.session.overallFlag} />
        <Metric label="Clock" value={formatLapTime(snapshot?.session.currentSessionSeconds)} />
        <Metric
          label="Air / Track"
          value={`${formatNumber(snapshot?.session.airTemperatureCelsius)}C / ${formatNumber(snapshot?.session.trackTemperatureCelsius)}C`}
        />
      </section>

      <section>
        <h2>Live Board</h2>
        <div className="tableFrame">
          <LeaderboardTable drivers={snapshot?.drivers ?? []} />
        </div>
      </section>
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
      <section className="summary" aria-label="Best-lap summary">
        <Metric label="Board" value={title} />
        <Metric label="Track" value={board?.trackName ?? (view === 'monthly' ? 'Not set' : 'All tracks')} />
        <Metric label="Mode" value={board?.mode === 'all-laps' ? 'All laps' : 'Per driver'} />
        <Metric label="Since" value={formatDate(board?.fromUtc)} />
        <Metric label="Entries" value={rows.length} />
      </section>

      <section>
        <h2>{title}</h2>
        <div className="tableFrame">
          <BestLapTable rows={rows} showTrack={!board?.trackName} />
        </div>
      </section>
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
          <th>Vehicle</th>
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
            <td>{row.vehicleName}</td>
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
          <th>Vehicle</th>
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
            <td>{driver.vehicleName}</td>
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
}

function Metric({ label, value }: MetricProps) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value ?? '-'}</strong>
    </div>
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
      <section className="summary" aria-label="Last session summary">
        <Metric label="Board" value="Last Session" />
        <Metric label="Track" value={result?.trackName ?? 'No finished session'} />
        <Metric label="Session" value={result?.sessionKind} />
        <Metric label="Finished" value={formatDate(result?.lastSeenUtc)} />
        <Metric label="Entries" value={rows.length} />
      </section>

      <section>
        <h2>Last Session</h2>
        <div className="tableFrame">
          <LastSessionTable rows={rows} />
        </div>
      </section>
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
          <th>Vehicle</th>
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
            <td>{row.vehicleName}</td>
            <td>{row.completedLaps}</td>
            <td>{formatLapTime(row.bestLapSeconds)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}