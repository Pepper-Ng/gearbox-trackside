import { useEffect, useMemo, useState } from 'react';
import { formatGap, formatLapTime, formatNumber } from '../format';
import { DriverSnapshot, LiveSessionConnection, LiveSessionSnapshot, SectorSnapshot, startLiveSessionFeed, TracksideApiClient } from '../tracksideApi';

/** Main kiosk application shell. */
export function App() {
  const client = useMemo(() => new TracksideApiClient(), []);
  const [snapshot, setSnapshot] = useState<LiveSessionSnapshot | null>(null);
  const [status, setStatus] = useState('Loading configuration...');

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

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <h1>Trackside</h1>
          <p>{status}</p>
        </div>
        <a href="/api/health">Health</a>
      </header>

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
    </main>
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