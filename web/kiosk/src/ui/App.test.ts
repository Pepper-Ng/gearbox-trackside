import { describe, expect, it } from 'vitest';
import { getViewFromPath, toViewMode } from './App';

describe('kiosk route selection', () => {
  it('maps the tracker route to the tracker view', () => {
    expect(getViewFromPath('/tracker')).toBe('tracker');
    expect(getViewFromPath('/TRACKER/')).toBe('tracker');
  });

  it('maps configured display modes case-insensitively', () => {
    expect(toViewMode('Tracker')).toBe('tracker');
    expect(toViewMode('tracker')).toBe('tracker');
    expect(toViewMode('LastSession')).toBe('last');
    expect(toViewMode('last-session')).toBe('last');
  });
});
