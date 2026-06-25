import { describe, expect, it } from 'vitest';
import { formatGap, formatLapTime, formatNumber } from './format';

describe('formatLapTime', () => {
  it('formats seconds as a lap-time string', () => {
    expect(formatLapTime(82.417)).toBe('1:22.417');
  });

  it('shows a dash for missing values', () => {
    expect(formatLapTime(null)).toBe('-');
  });
});

describe('formatNumber', () => {
  it('formats finite numbers', () => {
    expect(formatNumber(22.54, 1)).toBe('22.5');
  });
});

describe('formatGap', () => {
  it('formats leader, timed, and lapped gaps', () => {
    expect(formatGap(0)).toBe('Leader');
    expect(formatGap(0.687)).toBe('+0.687');
    expect(formatGap(null, 2)).toBe('+2L');
  });
});