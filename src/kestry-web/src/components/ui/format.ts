import { strings } from '../../strings';

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

export const formatSpeed = (bytesPerSecond: number) => formatBytes(bytesPerSecond) + strings.perSecond;

export function formatEta(seconds: number | null | undefined): string | null {
  if (seconds == null || !Number.isFinite(seconds)) return null;
  const { seconds: s, minutes: m, hoursMinutes: hm } = strings.etaLeft;
  if (seconds < 60) return s(Math.ceil(seconds));
  if (seconds < 3600) return m(Math.ceil(seconds / 60));
  return hm(Math.floor(seconds / 3600), Math.round((seconds % 3600) / 60));
}
