import { useCallback, useEffect, useState } from 'react';
import { api, ApiError } from './api/client';
import type { AppSettings, ErrorBody } from './api/types';

export type LimitUnit = 'KB' | 'MB';

/** Shows a KB/s limit in MB/s when it is a whole number of megabytes. */
export function splitLimit(kbps: number): { value: string; unit: LimitUnit } {
  return kbps >= 1024 && kbps % 1024 === 0 ? { value: String(kbps / 1024), unit: 'MB' } : { value: String(kbps), unit: 'KB' };
}

export const toKBps = (value: string, unit: LimitUnit) => Math.round(Number(value) * (unit === 'MB' ? 1024 : 1));

/**
 * Loads settings and reloads them whenever another client saves (the snapshot's settingsVersion changes).
 * `save` resolves to the field errors, or an empty object on success.
 */
export function useSettings(settingsVersion: number | undefined) {
  const [settings, setSettings] = useState<AppSettings | null>(null);

  useEffect(() => {
    if (settingsVersion === undefined) return; // wait for the first snapshot so startup does not fetch twice
    api.getSettings().then(setSettings).catch(() => {});
  }, [settingsVersion]);

  const save = useCallback(async (next: AppSettings): Promise<Record<string, ErrorBody>> => {
    try {
      setSettings(await api.saveSettings(next));
      return {};
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) return e.fieldErrors;
      throw e;
    }
  }, []);

  return { settings, save };
}
