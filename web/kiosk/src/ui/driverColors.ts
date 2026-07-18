export const trackerDriverPalette = ['#ff202d', '#ff8a00', '#00b0ff', '#5ff08a', '#d86bff', '#f1c40f', '#f6f8fb', '#00d5c8', '#ff5e9f', '#a6ff4d', '#6f8cff', '#ffcf5a'];

export function trackerDriverColorByIndex(index: number): string {
  return trackerDriverPalette[Math.max(0, index) % trackerDriverPalette.length];
}

export function stableDriverColor(driverId: string, stableIdentity?: string): string {
  const key = (stableIdentity || driverId || '').trim();
  const ordinalMatch = /(\d+)$/u.exec(key);
  if (ordinalMatch) {
    const ordinal = Number.parseInt(ordinalMatch[1], 10);
    if (ordinal > 0) {
      // Venue rigs use identities such as Setup1/2/3, which must always begin red/orange/blue.
      return trackerDriverColorByIndex(ordinal - 1);
    }
  }

  let hash = 0;
  const normalizedKey = key.toUpperCase();
  for (let index = 0; index < normalizedKey.length; index++) {
    hash = ((hash << 5) - hash + normalizedKey.charCodeAt(index)) | 0;
  }

  return trackerDriverColorByIndex(Math.abs(hash));
}