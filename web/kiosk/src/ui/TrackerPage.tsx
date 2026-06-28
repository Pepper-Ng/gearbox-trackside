import { type LiveSessionSnapshot } from '../tracksideApi';

interface TrackerPageProps {
  snapshot: LiveSessionSnapshot | null;
}

export function TrackerPage({ snapshot }: TrackerPageProps) {
  return (
    <section className="trackerPage">
      <h1>Tracker</h1>
      <p>Live tracker data will appear here once implemented.</p>
      <pre>{JSON.stringify(snapshot?.drivers?.map(driver => ({
        driverId: driver.driverId,
        rigName: driver.rigName,
        posX: driver.posX,
        posY: driver.posY,
      })), null, 2)}</pre>
    </section>
  );
}
