export const trackerDriverPalette = ['#ef233c', '#ff7a00', '#1684ff', '#ffd60a', '#9b5de5', '#22c55e', '#f8fafc', '#ff00a8', '#00d9ff', '#a3e635', '#800000', '#ff6b6b', '#14b8a6', '#c084fc', '#a16207', '#fde68a', '#1d4ed8', '#94a3b8', '#f9a8d4', '#84cc16'];

export function trackerDriverColorByIndex(index: number): string {
  return trackerDriverPalette[Math.max(0, index) % trackerDriverPalette.length];
}

/**
 * Browser-only fallback for older servers that have not yet supplied a durable colour.
 * Normal kiosk updates use the server-provided join-order assignment instead.
 */
export function stableDriverColor(driverId: string, stableIdentity?: string): string {
  const key = `${stableIdentity || ''}:${driverId || ''}`.trim();
  let hash = 0;
  const normalizedKey = key.toUpperCase();
  for (let index = 0; index < normalizedKey.length; index++) {
    hash = ((hash << 5) - hash + normalizedKey.charCodeAt(index)) | 0;
  }

  return trackerDriverColorByIndex(Math.abs(hash));
}