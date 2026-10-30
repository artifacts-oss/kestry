import type { Translator } from '../../i18n';

export function formatBytes(bytes: number | null | undefined): string {
  if (bytes == null || Number.isNaN(bytes)) return '—';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let i = 0;
  while (bytes >= 1024 && i < units.length - 1) {
    bytes /= 1024;
    i++;
  }
  return `${i === 0 || bytes >= 100 ? Math.round(bytes) : bytes.toFixed(1)} ${units[i]}`;
}

export const formatSpeed = (bytesPerSecond: number) => `${formatBytes(bytesPerSecond)}/s`;

/** `t` is the `Eta` translator. */
export function formatEta(seconds: number | null | undefined, t: Translator): string | null {
  if (seconds == null || !Number.isFinite(seconds)) return null;
  if (seconds < 60) return t('seconds', { n: Math.ceil(seconds) });
  if (seconds < 3600) return t('minutes', { n: Math.ceil(seconds / 60) });
  return t('hoursMinutes', { h: Math.floor(seconds / 3600), m: Math.round((seconds % 3600) / 60) });
}
