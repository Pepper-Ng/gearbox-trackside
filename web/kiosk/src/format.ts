/** Formats a duration in seconds as m:ss.mmm for timing tables. */
export function formatLapTime(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) {
    return '-';
  }

  const minutes = Math.floor(seconds / 60).toString();
  const remainder = (seconds % 60).toFixed(3).padStart(6, '0');
  return `${minutes}:${remainder}`;
}

/** Formats an optional numeric value with a fixed number of decimals. */
export function formatNumber(value: number | null | undefined, digits = 1): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '-';
  }
  return value.toFixed(digits);
}

/** Formats a leaderboard gap from leader/next timing values. */
export function formatGap(seconds: number | null | undefined, laps?: number | null): string {
  if (laps !== null && laps !== undefined && laps > 0) {
    return `+${laps}L`;
  }

  if (seconds === 0) {
    return 'Leader';
  }

  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) {
    return '-';
  }

  return `+${seconds.toFixed(3)}`;
}