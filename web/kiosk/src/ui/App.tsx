import { type ReactNode, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { formatGap, formatLapTime, formatNumber } from '../format';
import { BestLapBoardResponse, BestLapRow, BestLapWindow, ClientConfiguration, DriverSnapshot, KioskDisplayMode, LastFinishedSessionResponse, LastFinishedSessionRow, LiveSessionConnection, LiveSessionInfo, LiveSessionSnapshot, SectorSnapshot, startLiveSessionFeed, TrackGeometryResponse, TracksideApiClient } from '../tracksideApi';
import { getConnectionIndicators, getDriverStatus, getRaceLapProgress, getRacePositionDelta, type ConnectionIndicators, type DriverStatus } from './liveBoardLogic';
import { TrackerPage } from './TrackerPage';

type ViewMode = BestLapWindow | 'last' | 'live' | 'tracker';

const supportedPaths: Record<string, ViewMode> = {
  '/monthly': 'monthly',
  '/weekly': 'weekly',
  '/daily': 'daily',
  '/last-session': 'last',
  '/live': 'live',
  '/tracker': 'tracker',
};

export function getViewFromPath(path: string): ViewMode | null {
  const normalized = path.toLowerCase().replace(/\/+$/, '') || '/';
  return supportedPaths[normalized] ?? null;
}

/** Main kiosk application shell. */
export function App() {
  const client = useMemo(() => new TracksideApiClient(), []);
  const [snapshot, setSnapshot] = useState<LiveSessionSnapshot | null>(null);
  const [trackGeometry, setTrackGeometry] = useState<TrackGeometryResponse | null>(null);
  const [board, setBoard] = useState<BestLapBoardResponse | null>(null);
  const [lastSession, setLastSession] = useState<LastFinishedSessionResponse | null>(null);
  const [clientConfiguration, setClientConfiguration] = useState<ClientConfiguration | null>(null);
  const [view, setView] = useState<ViewMode>(() => getViewFromPath(window.location.pathname) ?? 'monthly');
  const [status, setStatus] = useState('Loading configuration...');
  const [boardStatus, setBoardStatus] = useState('Loading best laps...');

  useEffect(() => {
    const currentView = getViewFromPath(window.location.pathname);
    let cancelled = false;
    client.getClientConfiguration()
      .then(configuration => {
        if (!cancelled) {
          setClientConfiguration(configuration);
          if (!currentView) {
            setView(toViewMode(configuration.defaultDisplayMode));
          }
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
      geometry => {
        if (!cancelled) {
          setTrackGeometry(geometry);
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
    if (view === 'live' || view === 'last' || view === 'tracker') {
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
      <ShellHeader status={view === 'live' ? status : view === 'tracker' ? '' : boardStatus} view={view} snapshot={snapshot} />

      {view === 'live'
        ? <LiveBoard snapshot={snapshot} status={status} />
        : view === 'tracker'
          ? <TrackerPage
              snapshot={snapshot}
              geometry={trackGeometry}
              clientRefreshHz={clientConfiguration?.driverTrackerClientRefreshHz}
            />
          : view === 'last'
            ? <LastSessionBoard result={lastSession} />
            : <BestLapBoard board={board} view={view} />}
    </main>
  );
}

interface LiveBoardProps {
  /** Current live-session snapshot. */
  snapshot: LiveSessionSnapshot | null;
  /** Current browser/backend feed status. */
  status: string;
}

function LiveBoard({ snapshot, status }: LiveBoardProps) {
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
  const connectionIndicators = getConnectionIndicators(status, snapshot);

  return (
    <>
      <section className="sessionStrip" aria-label="Session summary">
        <Metric label="Track" value={snapshot?.session.trackName} indicators={<CompactStatusDots indicators={connectionIndicators} />} />
        <Metric label="Session" value={formatSessionValue(snapshot)} />
        <Metric label="Clock" value={formatLapTime(interpolatedClockSeconds)} />
      </section>

      <BoardPanel title="Live Board" meta={`${snapshot?.drivers.length ?? 0} drivers`} metaClassName="liveDriverCount" live>
        <div className="tableFrame liveBoardFrame" style={{ '--flag-color': getFlagColor(snapshot?.session.overallFlag) } as React.CSSProperties}>
          <LeaderboardTable snapshot={snapshot} />
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
  /** Current live-session snapshot. */
  snapshot: LiveSessionSnapshot | null;
}

function LeaderboardTable({ snapshot }: LeaderboardTableProps) {
  const drivers = snapshot?.drivers ?? [];
  const fastestLapFlashes = useFastestLapFlashes(drivers);
  const positionDeltas = useRacePositionDeltas(snapshot);
  const setRowRef = useRowSwapAnimation(drivers);

  return (
    <table className="leaderboardTable">
      <thead>
        <tr>
          <th className="columnRank">Rank</th>
          <th className="columnDriver">Driver</th>
          <th className="columnRig">Rig</th>
          <th className="columnLaps">Laps</th>
          <th className="columnBest">Best</th>
          <th className="columnCurrent">Current</th>
          <th className="columnSector">S1</th>
          <th className="columnSector">S2</th>
          <th className="columnSector">S3</th>
          <th className="columnInterval">Int</th>
          <th className="columnGap">Gap</th>
        </tr>
      </thead>
      <tbody>
        {drivers.map(driver => {
          const driverStatus = getDriverStatus(driver, snapshot?.session.kind);
          return (
            <tr
              key={driver.driverId}
              ref={element => setRowRef(driver.driverId, element)}
              className={classNames('liveBoardRow', driver.isOverallBestLap ? 'bestLapRow' : undefined, fastestLapFlashes.has(driver.driverId) ? 'fastestLapFlash' : undefined)}
            >
              <td className="rankCell columnRank">
                <span className="rankStack">
                  <span className="rankNumber">{driver.leaderboardRank || driver.position || '-'}</span>
                  <PositionDeltaBadge delta={positionDeltas.get(driver.driverId)} />
                </span>
              </td>
              <td className="columnDriver">
                <div className="driverCell">
                  <strong>{driver.displayName}</strong>
                  <DriverMetaLine driver={driver} status={driverStatus} />
                </div>
              </td>
              <td className="columnRig">{driver.rigName}</td>
              <td className="columnLaps">{driver.completedLaps}</td>
              <td className={classNames('columnBest', driver.isOverallBestLap ? 'bestTime' : undefined)}>{formatLapTime(driver.bestLapSeconds)}</td>
              <CurrentLapCell driver={driver} status={driverStatus} />
              {[1, 2, 3].map(number => (
                <SectorCell key={number} sector={driver.sectors.find(candidate => candidate.number === number)} />
              ))}
              <td className="columnInterval">{driver.leaderboardRank === 1 ? '-' : formatGap(driver.gapToNextSeconds)}</td>
              <td className="columnGap">{formatGap(driver.gapToLeaderSeconds, driver.lapsBehindLeader)}</td>
            </tr>
          );
        })}
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
    <td className={classNames('columnSector', 'sectorCell', sector?.isOverallBest ? 'bestTime' : undefined)}>
      <span>{formatLapTime(sector?.bestSeconds)}</span>
      <small>{formatLapTime(sector?.currentSeconds ?? sector?.lastSeconds)}</small>
    </td>
  );
}

function PositionDeltaBadge({ delta }: { delta: number | null | undefined }) {
  if (!delta) {
    return null;
  }

  return (
    <span className={classNames('positionDeltaBadge', delta > 0 ? 'positionDeltaBadge-gained' : 'positionDeltaBadge-lost')}>
      {delta > 0 ? `+${delta}` : delta}
    </span>
  );
}

interface CurrentLapCellProps {
  driver: DriverSnapshot;
  status: DriverStatus | null;
}

function CurrentLapCell({ driver, status }: CurrentLapCellProps) {
  return (
    <td className="columnCurrent">
      {status ? <StatusBadge label={status.label} tone={status.tone} /> : formatLapTime(driver.currentLapSeconds)}
    </td>
  );
}

interface DriverMetaLineProps {
  driver: DriverSnapshot;
  status: DriverStatus | null;
}

function DriverMetaLine({ driver, status }: DriverMetaLineProps) {
  return (
    <span className="driverMetaLine">
      {status ? <StatusBadge label={status.label} tone={status.tone} mobile /> : <SectorBars driver={driver} />}
      <span>{formatPercent(driver.trackPositionPercent)}</span>
    </span>
  );
}

interface StatusBadgeProps extends DriverStatus {
  mobile?: boolean;
}

function StatusBadge({ label, tone, mobile }: StatusBadgeProps) {
  return <span className={classNames('driverStatusBadge', `driverStatusBadge-${tone}`, mobile ? 'mobileStatusBadge' : undefined)}>{label}</span>;
}

function SectorBars({ driver }: { driver: DriverSnapshot }) {
  return (
    <span className="sectorBars" aria-label="Sector progress">
      {[1, 2, 3].map(number => {
        const sector = driver.sectors.find(candidate => candidate.number === number);
        return <span key={number} className={classNames('sectorBar', sectorBarTone(sector, driver.currentSector === number - 1))} />;
      })}
    </span>
  );
}

function sectorBarTone(sector: SectorSnapshot | undefined, isCurrent: boolean): string {
  if (sector?.isOverallBest) {
    return 'sectorBar-fastest';
  }

  if (isCurrent || sector?.currentSeconds != null) {
    return 'sectorBar-active';
  }

  if (sector?.bestSeconds != null || sector?.lastSeconds != null) {
    return 'sectorBar-complete';
  }

  return 'sectorBar-pending';
}

function useFastestLapFlashes(drivers: DriverSnapshot[]): Set<string> {
  const [flashes, setFlashes] = useState<Record<string, number>>({});
  const previousBestRef = useRef<number | null>(null);
  const initializedRef = useRef(false);

  useEffect(() => {
    const best = getBestLapDrivers(drivers);
    const now = Date.now();

    if (!best) {
      previousBestRef.current = null;
      initializedRef.current = false;
      setFlashes({});
      return;
    }

    const previousBest = previousBestRef.current;
    const shouldFlash = initializedRef.current && previousBest !== null && best.seconds < previousBest - 0.0005;
    previousBestRef.current = best.seconds;
    initializedRef.current = true;

    setFlashes(current => {
      const next = pruneExpiredFlashes(current, now);
      if (shouldFlash) {
        const expiresAt = now + 4500;
        for (const driverId of best.driverIds) {
          next[driverId] = expiresAt;
        }
      }

      return next;
    });
  }, [drivers.map(driver => `${driver.driverId}:${driver.bestLapSeconds ?? ''}`).join('|')]);

  useEffect(() => {
    const expiries = Object.values(flashes);
    if (expiries.length === 0) {
      return;
    }

    const timeout = window.setTimeout(() => {
      setFlashes(current => pruneExpiredFlashes(current, Date.now()));
    }, Math.max(0, Math.min(...expiries) - Date.now()) + 50);

    return () => window.clearTimeout(timeout);
  }, [flashes]);

  const now = Date.now();
  return new Set(Object.entries(flashes).filter(([, expiresAt]) => expiresAt > now).map(([driverId]) => driverId));
}

function getBestLapDrivers(drivers: DriverSnapshot[]): { seconds: number; driverIds: string[] } | null {
  const usable = drivers.filter(driver => isFiniteNumber(driver.bestLapSeconds) && driver.bestLapSeconds > 0);
  if (usable.length === 0) {
    return null;
  }

  const seconds = Math.min(...usable.map(driver => driver.bestLapSeconds!));
  return {
    seconds,
    driverIds: usable.filter(driver => Math.abs(driver.bestLapSeconds! - seconds) < 0.0005).map(driver => driver.driverId),
  };
}

function pruneExpiredFlashes(flashes: Record<string, number>, now: number): Record<string, number> {
  return Object.fromEntries(Object.entries(flashes).filter(([, expiresAt]) => expiresAt > now));
}

function useRacePositionDeltas(snapshot: LiveSessionSnapshot | null): Map<string, number> {
  const baselinesRef = useRef<{ sessionKey: string; ranks: Map<string, number> }>({ sessionKey: '', ranks: new Map() });
  const drivers = snapshot?.drivers ?? [];
  const driverRankKey = drivers.map(driver => `${driver.driverId}:${getDriverRank(driver) ?? ''}`).join('|');

  return useMemo(() => {
    if (!snapshot || snapshot.session.kind !== 'Race') {
      baselinesRef.current = { sessionKey: '', ranks: new Map() };
      return new Map<string, number>();
    }

    const sessionKey = `${snapshot.source}:${snapshot.session.trackName}:${snapshot.session.totalLaps ?? ''}`;
    if (baselinesRef.current.sessionKey !== sessionKey) {
      baselinesRef.current = { sessionKey, ranks: new Map() };
    }

    const deltas = new Map<string, number>();
    for (const driver of drivers) {
      const currentRank = getDriverRank(driver);
      if (!currentRank) {
        continue;
      }

      if (!baselinesRef.current.ranks.has(driver.driverId)) {
        baselinesRef.current.ranks.set(driver.driverId, currentRank);
      }

      const delta = getRacePositionDelta(currentRank, baselinesRef.current.ranks.get(driver.driverId));
      if (delta) {
        deltas.set(driver.driverId, delta);
      }
    }

    return deltas;
  }, [snapshot?.source, snapshot?.session.kind, snapshot?.session.trackName, snapshot?.session.totalLaps, driverRankKey]);
}

function getDriverRank(driver: DriverSnapshot): number | null {
  return driver.leaderboardRank || driver.position || null;
}

function useRowSwapAnimation(drivers: DriverSnapshot[]) {
  const rowsRef = useRef(new Map<string, HTMLTableRowElement>());
  const previousTopsRef = useRef<Map<string, number> | null>(null);
  const orderKey = drivers.map(driver => driver.driverId).join('|');

  useLayoutEffect(() => {
    const previousTops = previousTopsRef.current;
    const nextTops = new Map<string, number>();

    for (const driver of drivers) {
      const row = rowsRef.current.get(driver.driverId);
      if (!row) {
        continue;
      }

      const top = row.getBoundingClientRect().top;
      nextTops.set(driver.driverId, top);
      const previousTop = previousTops?.get(driver.driverId);
      if (previousTop !== undefined) {
        animateRowShift(row, previousTop - top);
      }
    }

    previousTopsRef.current = nextTops;
  }, [drivers, orderKey]);

  return (driverId: string, element: HTMLTableRowElement | null) => {
    if (element) {
      rowsRef.current.set(driverId, element);
    } else {
      rowsRef.current.delete(driverId);
    }
  };
}

function animateRowShift(row: HTMLTableRowElement, shift: number) {
  if (Math.abs(shift) < 2 || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    return;
  }

  row.style.transition = 'none';
  row.style.transform = `translateY(${shift}px)`;
  row.style.zIndex = shift > 0 ? '3' : '2';
  row.getBoundingClientRect();
  window.requestAnimationFrame(() => {
    row.classList.add('rowSwapping');
    row.style.transition = '';
    row.style.transform = '';
    window.setTimeout(() => {
      row.classList.remove('rowSwapping');
      row.style.zIndex = '';
    }, 760);
  });
}

function formatPercent(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? '-' : `${formatNumber(value, 1)}%`;
}

function CompactStatusDots({ indicators }: { indicators: ConnectionIndicators }) {
  if (!indicators.backendConnected && !indicators.liveData) {
    return null;
  }

  return (
    <span className="compactStatusDots" aria-label="Connection status">
      {indicators.backendConnected ? <span className="compactStatusDot compactStatusDot-connected" title="Connected to Trackside service" /> : null}
      {indicators.liveData ? <span className="compactStatusDot compactStatusDot-live" title="Receiving shared-memory data" /> : null}
    </span>
  );
}

function formatSessionValue(snapshot: LiveSessionSnapshot | null): string | undefined {
  if (!snapshot) {
    return undefined;
  }

  const lapProgress = getRaceLapProgress(snapshot);
  if (!lapProgress) {
    return snapshot.session.kind;
  }

  return `${snapshot.session.kind} - LAP ${lapProgress.current}/${lapProgress.total}`;
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

export function toViewMode(displayMode: KioskDisplayMode | string): ViewMode {
  switch (displayMode.toLowerCase()) {
    case 'weekly':
      return 'weekly';
    case 'daily':
      return 'daily';
    case 'lastsession':
    case 'last-session':
      return 'last';
    case 'live':
      return 'live';
    case 'tracker':
      return 'tracker';
    default:
      return 'monthly';
  }
}

interface MetricProps {
  /** Short label shown above the value. */
  label: string;
  /** Value rendered for the metric. */
  value: string | number | null | undefined;
  /** Optional compact inline indicators rendered beside the value. */
  indicators?: ReactNode;
  /** Feature important metrics with larger text. */
  featured?: boolean;
}

function Metric({ label, value, indicators, featured }: MetricProps) {
  return (
    <div className={`metric${featured ? ' featured' : ''}`}>
      <span>{label}</span>
      <strong>{value ?? '-'}{indicators}</strong>
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
    case 'local yellow':
    case 'localyellow':
    case 'safety car / full course yellow':
    case 'full course yellow':
    case 'fullcourseyellow':
    case 'caution':
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
    case 'session over':
      return 'linear-gradient(135deg, #ffffff 25%, #000000 25%, #000000 50%, #ffffff 50%, #ffffff 75%, #000000 75%, #000000)';
    default:
      return 'rgba(255, 255, 255, 0.08)';
  }
}

function getFlagDisplayText(session: LiveSessionInfo | null | undefined): string {
  if (!session?.overallFlag) {
    return '-';
  }

  if (isCheckeredFlag(session.overallFlag) || session.phase === 'SessionOver') {
    return 'CHECKERED';
  }

  const yellowSectors = getLocalYellowSectors(session);
  if (yellowSectors.length > 0) {
    return yellowSectors.map(sector => `S${sector}`).join(' ');
  }

  return session.overallFlag;
}

function getLocalYellowSectors(session: LiveSessionInfo): number[] {
  if (!session.overallFlag.toLowerCase().includes('local')) {
    return [];
  }

  return (session.sectorFlags ?? [])
    .map((flag, index) => ({ flag: flag.toLowerCase(), sector: index + 1 }))
    .filter(item => item.flag.includes('yellow') || item.flag.includes('caution'))
    .map(item => item.sector);
}

function isCheckeredFlag(flag: string | null | undefined): boolean {
  if (!flag) {
    return false;
  }

  const normalized = flag.toLowerCase().replace(/\s+/g, '');
  return normalized.includes('checker') || normalized.includes('sessionover');
}

interface ShellHeaderProps {
  /** Current status message shown in the masthead. */
  status: string;
  /** Selected display mode. */
  view: ViewMode;
  /** Current live-session snapshot for live view. */
  snapshot: LiveSessionSnapshot | null;
}

function ShellHeader({ status, view, snapshot }: ShellHeaderProps) {
  const connectionIndicators = getConnectionIndicators(status, view === 'live' ? snapshot : null);
  const session = snapshot?.session;
  const sanitized = sanitizeStatus(status);
  const modeLabel = view === 'tracker' ? 'Tracker' : 'Leaderboard';

  return (
    <header className={classNames('topbar', view === 'live' ? 'topbar-live' : undefined)}>
      <BrandMark />
      <div className="topbarMeta" aria-live="polite">
        <div className="topbarPills">
          {view === 'live' && connectionIndicators.backendConnected ? <span className="statusPill connection">CONNECTED</span> : null}
          {view === 'live' && connectionIndicators.liveData ? <span className="statusPill live">LIVE</span> : null}
          {view !== 'live' ? <span className="statusPill">{modeLabel}</span> : null}
        </div>
        {sanitized ? <p>{sanitized}</p> : null}
      </div>
      {view === 'live' && session?.overallFlag ? (
        <div className="topbarFlag">
          <div className="flagSwatch" style={{ background: getFlagColor(session.overallFlag) }}>
            <span>{getFlagDisplayText(session)}</span>
          </div>
        </div>
      ) : null}
    </header>
  );
}

function sanitizeStatus(status: string): string {
  return status.replace(/\b(?:shared[- ]memory\b.*|Connected through\b.*)$/i, '').trim();
}

function isFiniteNumber(value: number | null | undefined): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

function classNames(...values: Array<string | false | null | undefined>): string {
  return values.filter(Boolean).join(' ');
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
  /** Optional class name for the metadata span. */
  metaClassName?: string;
  /** Panel content. */
  children: ReactNode;
  /** True for live boards to use special styling. */
  live?: boolean;
}

function BoardPanel({ title, meta, metaClassName, children, live }: BoardPanelProps & { live?: boolean }) {
  return (
    <section className={`boardPanel${live ? ' live' : ''}`}>
      <div className="sectionHeader">
        <h2>{title}</h2>
        {meta ? <span className={metaClassName}>{meta}</span> : null}
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