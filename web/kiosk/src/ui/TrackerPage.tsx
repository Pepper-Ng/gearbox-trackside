import { type CSSProperties, useEffect, useMemo, useRef, useState } from 'react';
import { type DriverSnapshot, type LiveSessionSnapshot, type TrackGeometryBounds, type TrackGeometryResponse } from '../tracksideApi';

interface TrackerPageProps {
  snapshot: LiveSessionSnapshot | null;
  geometry: TrackGeometryResponse | null;
  clientRefreshHz: number | null | undefined;
}

const mapWidth = 1000;
const minMapHeight = 360;
const maxMapHeight = 1000;
const mapPadding = 56;

export function TrackerPage({ snapshot, geometry, clientRefreshHz }: TrackerPageProps) {
  const [trackerSnapshot, setTrackerSnapshot] = useState<LiveSessionSnapshot | null>(snapshot);
  const latestSnapshot = useRef<LiveSessionSnapshot | null>(snapshot);
  const refreshHz = clampRefreshHz(clientRefreshHz);

  useEffect(() => {
    latestSnapshot.current = snapshot;
  }, [snapshot]);

  useEffect(() => {
    let cancelled = false;
    let timer = 0;

    function tick() {
      setTrackerSnapshot(latestSnapshot.current);
      if (!cancelled) {
        timer = window.setTimeout(tick, 1000 / refreshHz);
      }
    }

    tick();
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [refreshHz]);

  const mapMetrics = useMemo(() => buildMapMetrics(geometry?.bounds), [geometry?.bounds]);
  const pathPoints = useMemo(() => (geometry?.points ?? [])
    .map(point => toSvgPoint(point.x, point.y, mapMetrics))
    .map(point => `${point.x},${point.y}`)
    .join(' '), [geometry?.points, mapMetrics]);
  const markers = useMemo(() => buildDriverMarkers(trackerSnapshot?.drivers ?? [], geometry?.bounds, mapMetrics), [trackerSnapshot?.drivers, geometry?.bounds, mapMetrics]);

  return (
    <section className="trackerPage" aria-label="Driver tracker">
      <svg className="trackerMap" viewBox={`0 0 ${mapMetrics.width} ${mapMetrics.height}`} role="img" aria-label={trackerSnapshot?.session.trackName ?? 'Track map'}>
        <rect className="trackerMapBackground" x="0" y="0" width={mapMetrics.width} height={mapMetrics.height} rx="18" />
        {geometry?.isAvailable && pathPoints ? <polyline className="trackGeometryLine" points={pathPoints} /> : null}
        {markers.map((marker, index) => (
          <g key={marker.driverId} className="driverMarker" style={{ '--marker-color': markerColor(index) } as CSSProperties} transform={`translate(${marker.x} ${marker.y})`}>
            <circle r="12" />
            <text y="4">{marker.rank}</text>
            <title>{marker.label}</title>
          </g>
        ))}
      </svg>

      <div className="trackerRoster" aria-label="Driver positions">
        {markers.map((marker, index) => (
          <div key={marker.driverId} className="trackerRosterItem" style={{ '--marker-color': markerColor(index) } as CSSProperties}>
            <span>{marker.rank}</span>
            <strong>{marker.label}</strong>
            <small>{marker.rigName}</small>
          </div>
        ))}
      </div>
    </section>
  );
}

interface MapMetrics {
  width: number;
  height: number;
}

interface DriverMarker {
  driverId: string;
  rigName: string;
  label: string;
  rank: number;
  x: number;
  y: number;
}

function buildMapMetrics(bounds: TrackGeometryBounds | null | undefined): MapMetrics {
  if (!bounds) {
    return { width: mapWidth, height: 640 };
  }

  const worldWidth = Math.max(1, bounds.maxWorldX - bounds.minWorldX);
  const worldHeight = Math.max(1, bounds.maxWorldZ - bounds.minWorldZ);
  return {
    width: mapWidth,
    height: Math.min(maxMapHeight, Math.max(minMapHeight, Math.round(mapWidth * (worldHeight / worldWidth)))),
  };
}

function buildDriverMarkers(drivers: DriverSnapshot[], bounds: TrackGeometryBounds | null | undefined, metrics: MapMetrics): DriverMarker[] {
  if (!bounds) {
    return [];
  }

  const worldWidth = bounds.maxWorldX - bounds.minWorldX;
  const worldHeight = bounds.maxWorldZ - bounds.minWorldZ;
  if (worldWidth <= 0 || worldHeight <= 0) {
    return [];
  }

  return drivers
    .filter(driver => isFiniteNumber(driver.posX) && isFiniteNumber(driver.posZ))
    .map((driver, index) => {
      const worldX = driver.posX!;
      const worldZ = driver.posZ!;
      const normalizedX = (worldX - bounds.minWorldX) / worldWidth;
      const normalizedY = (bounds.maxWorldZ - worldZ) / worldHeight;
      const point = toSvgPoint(normalizedX, normalizedY, metrics);
      return {
        driverId: driver.driverId,
        rigName: driver.rigName,
        label: driver.displayName || driver.rigName,
        rank: driver.leaderboardRank || driver.position || index + 1,
        x: point.x,
        y: point.y,
      };
    });
}

function toSvgPoint(normalizedX: number, normalizedY: number, metrics: MapMetrics): { x: number; y: number } {
  return {
    x: mapPadding + (Math.min(1, Math.max(0, normalizedX)) * (metrics.width - (mapPadding * 2))),
    y: mapPadding + (Math.min(1, Math.max(0, normalizedY)) * (metrics.height - (mapPadding * 2))),
  };
}

function clampRefreshHz(value: number | null | undefined): number {
  if (!isFiniteNumber(value)) {
    return 50;
  }

  return Math.min(60, Math.max(1, value));
}

function isFiniteNumber(value: number | null | undefined): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}

function markerColor(index: number): string {
  const colors = ['#00b0ff', '#ff202d', '#ff8a00', '#5ff08a', '#d86bff', '#f1c40f'];
  return colors[index % colors.length];
}
