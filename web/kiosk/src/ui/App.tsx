import { useEffect, useMemo, useState } from 'react';
import type { HubConnection } from '@microsoft/signalr';
import { formatLapTime, formatNumber } from '../format';
import { LiveSessionSnapshot, TracksideApiClient } from '../tracksideApi';

/** Main kiosk application shell. */
export function App() {
  const client = useMemo(() => new TracksideApiClient(), []);
  const [snapshot, setSnapshot] = useState<LiveSessionSnapshot | null>(null);
  const [status, setStatus] = useState('Loading configuration...');

  useEffect(() => {
    let connection: HubConnection | null = null;
    let cancelled = false;

    async function connect() {
      const configuration = await client.getClientConfiguration();
      const current = await client.getCurrentSession(configuration.currentSessionPath);
      if (!cancelled) {
        setSnapshot(current);
        setStatus('Connected through REST recovery endpoint');
      }
      connection = await client.connectLiveSession(configuration.liveSessionHubPath, pushedSnapshot => {
        setSnapshot(pushedSnapshot);
        setStatus('Connected through SignalR live updates');
      });
    }

    connect().catch(error => {
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
        <h2>Fixture Drivers</h2>
        <div className="tableFrame">
          <table>
            <thead>
              <tr>
                <th>Pos</th>
                <th>Driver</th>
                <th>Rig</th>
                <th>Vehicle</th>
                <th>Laps</th>
                <th>Best</th>
                <th>Last</th>
                <th>Gap</th>
              </tr>
            </thead>
            <tbody>
              {(snapshot?.drivers ?? []).map(driver => (
                <tr key={driver.driverId}>
                  <td>{driver.position ?? '-'}</td>
                  <td>{driver.displayName}</td>
                  <td>{driver.rigName}</td>
                  <td>{driver.vehicleName}</td>
                  <td>{driver.completedLaps}</td>
                  <td>{formatLapTime(driver.bestLapSeconds)}</td>
                  <td>{formatLapTime(driver.lastLapSeconds)}</td>
                  <td>{driver.gapToLeaderSeconds ? `+${formatNumber(driver.gapToLeaderSeconds, 3)}` : '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
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