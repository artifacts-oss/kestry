import { renderHook, waitFor, act } from '@testing-library/react';
import { api, ApiError } from './api/client';
import type { AppSettings } from './api/types';
import { splitLimit, toKBps, useSettings } from './settings';

vi.mock('./api/client', async (orig) => ({
  ...(await orig<typeof import('./api/client')>()),
  api: { getSettings: vi.fn(), saveSettings: vi.fn() },
}));

const s: AppSettings = {
  maxConcurrentDownloads: 3, speedLimitEnabled: false, downloadLimitKBps: 5120, uploadLimitKBps: 1024,
  downloadDir: '/d', shutdownWhenDone: false, seedingEnabled: false, seedRatio: 1,
};

describe('limit helpers', () => {
  it.each([
    [5120, '5', 'MB'],
    [1024, '1', 'MB'],
    [1500, '1500', 'KB'],
    [512, '512', 'KB'],
  ])('splits %s KB/s', (kbps, value, unit) => expect(splitLimit(kbps)).toEqual({ value, unit }));

  it('converts back to KB/s', () => {
    expect(toKBps('5', 'MB')).toBe(5120);
    expect(toKBps('1.5', 'MB')).toBe(1536);
    expect(toKBps('300', 'KB')).toBe(300);
  });
});

describe('useSettings', () => {
  it('does not load before the first snapshot tells it the version', () => {
    renderHook(() => useSettings(undefined));
    expect(api.getSettings).not.toHaveBeenCalled();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.getSettings).mockResolvedValue(s);
  });

  it('loads on mount and reloads when another client changes the version', async () => {
    const { result, rerender } = renderHook(({ v }) => useSettings(v), { initialProps: { v: 0 } });
    await waitFor(() => expect(result.current.settings).toEqual(s));
    expect(api.getSettings).toHaveBeenCalledTimes(1);

    vi.mocked(api.getSettings).mockResolvedValue({ ...s, maxConcurrentDownloads: 9 });
    rerender({ v: 1 });

    await waitFor(() => expect(result.current.settings?.maxConcurrentDownloads).toBe(9));
  });

  it('save stores what the server returned and reports no errors', async () => {
    vi.mocked(api.saveSettings).mockResolvedValue({ ...s, maxConcurrentDownloads: 5 });
    const { result } = renderHook(() => useSettings(0));
    await waitFor(() => expect(result.current.settings).not.toBeNull());

    let errors = {};
    await act(async () => { errors = await result.current.save({ ...s, maxConcurrentDownloads: 5 }); });

    expect(errors).toEqual({});
    expect(result.current.settings?.maxConcurrentDownloads).toBe(5);
  });

  it('save returns field errors from a 400 and keeps the previous settings', async () => {
    vi.mocked(api.saveSettings).mockRejectedValue(new ApiError('Invalid settings', 400, 'settings.invalid', undefined, { downloadDir: { code: 'settings.downloadDir.outsideRoots', error: 'bad' } }));
    const { result } = renderHook(() => useSettings(0));
    await waitFor(() => expect(result.current.settings).not.toBeNull());

    let errors = {};
    await act(async () => { errors = await result.current.save({ ...s, downloadDir: 'x' }); });

    expect(errors).toEqual({ downloadDir: { code: 'settings.downloadDir.outsideRoots', error: 'bad' } });
    expect(result.current.settings).toEqual(s);
  });

  it('save rethrows API errors that carry no field errors (e.g. a 403) instead of swallowing them', async () => {
    const forbidden = new ApiError('Origin not allowed', 403, 'request.originNotAllowed');
    vi.mocked(api.saveSettings).mockRejectedValue(forbidden);
    const { result } = renderHook(() => useSettings(0));
    await waitFor(() => expect(result.current.settings).not.toBeNull());

    await expect(result.current.save(s)).rejects.toBe(forbidden);
  });

  it('save rethrows unexpected failures', async () => {
    vi.mocked(api.saveSettings).mockRejectedValue(new Error('network'));
    const { result } = renderHook(() => useSettings(0));
    await waitFor(() => expect(result.current.settings).not.toBeNull());

    await expect(result.current.save(s)).rejects.toThrow('network');
  });
});
