export function stableDriverColor(driverId: string, label?: string): string {
  const colors = ['#00b0ff', '#ff202d', '#ff8a00', '#5ff08a', '#d86bff', '#f1c40f', '#00d5c8', '#ff5e9f', '#a6ff4d', '#9b8cff'];
  const key = `${driverId || ''}:${label || ''}`;
  let hash = 0;
  for (let index = 0; index < key.length; index++) {
    hash = ((hash << 5) - hash + key.charCodeAt(index)) | 0;
  }

  return colors[Math.abs(hash) % colors.length];
}