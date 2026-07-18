import { type CSSProperties, useMemo, useRef } from 'react';
import { type DriverSnapshot, type LiveSessionSnapshot, type TrackGeometryBounds, type TrackGeometryResponse } from '../tracksideApi';
import { stableDriverColor } from './driverColors';

interface TrackerPageProps {
  snapshot: LiveSessionSnapshot | null;
  geometry: TrackGeometryResponse | null;
}

const mapWidth = 1000;
const minMapHeight = 360;
const maxMapHeight = 1000;
const mapPadding = 56;

export function TrackerPage({ snapshot, geometry }: TrackerPageProps) {
  const trackerBounds = useStableTrackerBounds(geometry?.bounds, snapshot?.drivers ?? [], snapshot?.session.trackName);
  const mapMetrics = useMemo(() => buildMapMetrics(trackerBounds), [trackerBounds]);
  // Geometry points are already normalized by the backend, so the browser only adapts them to the current SVG viewport.
  const pathPoints = useMemo(() => (geometry?.points ?? [])
    .map(point => toSvgPoint(point.x, point.y, mapMetrics))
    .map(point => `${point.x},${point.y}`)
    .join(' '), [geometry?.points, mapMetrics]);
  const markers = useMemo(() => buildDriverMarkers(snapshot?.drivers ?? [], trackerBounds, mapMetrics), [snapshot?.drivers, trackerBounds, mapMetrics]);
  const markerColors = useStableDriverColors(markers);

  return (
    <section className="trackerPage" aria-label="Driver tracker">
      <svg className="trackerMap" viewBox={`0 0 ${mapMetrics.width} ${mapMetrics.height}`} role="img" aria-label={snapshot?.session.trackName ?? 'Track map'}>
        <rect className="trackerMapBackground" x="0" y="0" width={mapMetrics.width} height={mapMetrics.height} rx="18" />
        {geometry?.isAvailable && pathPoints ? <polyline className="trackGeometryLine" points={pathPoints} /> : null}
        {markers.map(marker => (
          <g key={marker.driverId} className="driverMarker" style={{ '--marker-color': markerColors.get(marker.driverId) ?? stableDriverColor(marker.driverId, marker.rigName) } as CSSProperties} transform={`translate(${marker.x} ${marker.y})`}>
            <circle r="14" />
            <text y="5">{marker.rank}</text>
            <title>{marker.label}</title>
          </g>
        ))}
      </svg>

      <div className="trackerRoster" aria-label="Driver positions">
        {markers.map(marker => (
          <div key={marker.driverId} className="trackerRosterItem" style={{ '--marker-color': markerColors.get(marker.driverId) ?? stableDriverColor(marker.driverId, marker.rigName) } as CSSProperties}>
            <span>{marker.rank}</span>
            <strong>{marker.label}</strong>
            <small>{marker.rigName}</small>
          </div>
        ))}
      </div>
    </section>
  );
}

export interface MapMetrics {
  width: number;
  height: number;
}

export interface DriverMarker {
  driverId: string;
  rigName: string;
  label: string;
  rank: number;
  x: number;
  y: number;
}

export function useStableDriverColors(markers: DriverMarker[]): Map<string, string> {
  return useMemo(
    () => new Map(markers.map(marker => [marker.driverId, stableDriverColor(marker.driverId, marker.rigName)])),
    [markers],
  );
}

export function buildMapMetrics(bounds: TrackGeometryBounds | null | undefined): MapMetrics {
  if (!bounds) {
    // No geometry and no positioned drivers yet: keep a stable empty viewport instead of resizing around nothing.
    return { width: mapWidth, height: 640 };
  }

  const worldWidth = Math.max(1, bounds.maxWorldX - bounds.minWorldX);
  const worldHeight = Math.max(1, bounds.maxWorldZ - bounds.minWorldZ);
  return {
    width: mapWidth,
    height: Math.min(maxMapHeight, Math.max(minMapHeight, Math.round(mapWidth * (worldHeight / worldWidth)))),
  };
}

export function buildDriverMarkers(drivers: DriverSnapshot[], bounds: TrackGeometryBounds | null | undefined, metrics: MapMetrics): DriverMarker[] {
  if (!bounds) {
    return [];
  }

  const worldWidth = bounds.maxWorldX - bounds.minWorldX;
  const worldHeight = bounds.maxWorldZ - bounds.minWorldZ;
  if (worldWidth <= 0 || worldHeight <= 0) {
    return [];
  }

  return drivers
    // Drivers without world X/Z coordinates cannot be placed on the tracker, but they still remain visible elsewhere.
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

export function resolveTrackerBounds(bounds: TrackGeometryBounds | null | undefined, drivers: DriverSnapshot[]): TrackGeometryBounds | null {
  if (bounds) {
    return bounds;
  }

  // Before a reliable track outline exists, live driver coordinates can still give the tracker a useful
  // provisional viewport. This is only for marker placement; the actual track line remains hidden.
  const positionedDrivers = drivers.filter(driver => isFiniteNumber(driver.posX) && isFiniteNumber(driver.posZ));
  if (positionedDrivers.length === 0) {
    return null;
  }

  const xs = positionedDrivers.map(driver => driver.posX!);
  const zs = positionedDrivers.map(driver => driver.posZ!);
  return expandBounds({
    minWorldX: Math.min(...xs),
    maxWorldX: Math.max(...xs),
    minWorldZ: Math.min(...zs),
    maxWorldZ: Math.max(...zs),
  });
}

export function useStableTrackerBounds(
  geometryBounds: TrackGeometryBounds | null | undefined,
  drivers: DriverSnapshot[],
  trackName: string | null | undefined,
): TrackGeometryBounds | null {
  const provisional = useRef<{ trackName: string; bounds: TrackGeometryBounds } | null>(null);
  return useMemo(() => {
    const normalizedTrackName = trackName?.trim() ?? '';
    if (geometryBounds) {
      provisional.current = { trackName: normalizedTrackName, bounds: geometryBounds };
      return geometryBounds;
    }

    const nextBounds = resolveTrackerBounds(null, drivers);
    if (!nextBounds) {
      return provisional.current?.trackName === normalizedTrackName ? provisional.current.bounds : null;
    }

    const previous = provisional.current;
    const stableBounds = previous?.trackName === normalizedTrackName
      ? unionBounds(previous.bounds, nextBounds)
      : nextBounds;
    provisional.current = { trackName: normalizedTrackName, bounds: stableBounds };
    return stableBounds;
  }, [geometryBounds, drivers, trackName]);
}

function unionBounds(left: TrackGeometryBounds, right: TrackGeometryBounds): TrackGeometryBounds {
  return {
    minWorldX: Math.min(left.minWorldX, right.minWorldX),
    maxWorldX: Math.max(left.maxWorldX, right.maxWorldX),
    minWorldZ: Math.min(left.minWorldZ, right.minWorldZ),
    maxWorldZ: Math.max(left.maxWorldZ, right.maxWorldZ),
  };
}

function expandBounds(bounds: TrackGeometryBounds): TrackGeometryBounds {
  // A single car, or a tight group leaving the pits, would otherwise collapse the SVG scale and make markers jump.
  const minimumSpan = 80;
  const width = Math.max(minimumSpan, bounds.maxWorldX - bounds.minWorldX);
  const height = Math.max(minimumSpan, bounds.maxWorldZ - bounds.minWorldZ);
  const centerX = (bounds.minWorldX + bounds.maxWorldX) / 2;
  const centerZ = (bounds.minWorldZ + bounds.maxWorldZ) / 2;
  return {
    minWorldX: centerX - (width / 2),
    maxWorldX: centerX + (width / 2),
    minWorldZ: centerZ - (height / 2),
    maxWorldZ: centerZ + (height / 2),
  };
}

export function toSvgPoint(normalizedX: number, normalizedY: number, metrics: MapMetrics): { x: number; y: number } {
  return {
    x: mapPadding + (Math.min(1, Math.max(0, normalizedX)) * (metrics.width - (mapPadding * 2))),
    y: mapPadding + (Math.min(1, Math.max(0, normalizedY)) * (metrics.height - (mapPadding * 2))),
  };
}

function isFiniteNumber(value: number | null | undefined): value is number {
  return value !== null && value !== undefined && Number.isFinite(value);
}
