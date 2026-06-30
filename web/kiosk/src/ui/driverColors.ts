export const trackerDriverPalette = ['#ff202d', '#ff8a00', '#00b0ff', '#5ff08a', '#d86bff', '#f1c40f', '#f6f8fb', '#00d5c8', '#ff5e9f', '#a6ff4d', '#6f8cff', '#ffcf5a'];

export function trackerDriverColorByIndex(index: number): string {
  return trackerDriverPalette[Math.max(0, index) % trackerDriverPalette.length];
}

export function stableDriverColor(driverId: string, label?: string): string {
  const numericId = Number.parseInt(driverId, 10);
  if (Number.isFinite(numericId) && numericId > 0) {
    return trackerDriverColorByIndex(numericId - 1);
  }

  const key = `${driverId || ''}:${label || ''}`;
  let hash = 0;
  for (let index = 0; index < key.length; index++) {
    hash = ((hash << 5) - hash + key.charCodeAt(index)) | 0;
  }

  return trackerDriverColorByIndex(Math.abs(hash));
}