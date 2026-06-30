export function stableDriverColor(driverId: string, label?: string): string {
  const colors = ['#00d26a', '#ff202d', '#00a8ff', '#f1c40f', '#b65cff', '#ff8a00', '#f6f8fb', '#00d5c8', '#ff5e9f', '#a6ff4d', '#6f8cff', '#ffcf5a'];
  const numericId = Number.parseInt(driverId, 10);
  if (Number.isFinite(numericId) && numericId > 0) {
    return colors[(numericId - 1) % colors.length];
  }

  const key = `${driverId || ''}:${label || ''}`;
  let hash = 0;
  for (let index = 0; index < key.length; index++) {
    hash = ((hash << 5) - hash + key.charCodeAt(index)) | 0;
  }

  return colors[Math.abs(hash) % colors.length];
}