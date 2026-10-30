import type { Download } from './api/types';

export type Filter = 'all' | 'downloading' | 'queued' | 'completed' | 'failed';
export const FILTERS: Filter[] = ['all', 'downloading', 'queued', 'completed', 'failed'];

/** "En cola" also contains paused downloads, like aria2's tellWaiting. */
const matches = (d: Download, f: Filter) =>
  f === 'all' || (f === 'queued' ? d.status === 'queued' || d.status === 'paused' : d.status === f);

export const countByFilter = (list: Download[]) =>
  Object.fromEntries(FILTERS.map((f) => [f, list.filter((d) => matches(d, f)).length])) as Record<Filter, number>;

export function filterDownloads(list: Download[], filter: Filter, query: string): Download[] {
  const q = query.trim().toLowerCase();
  return list.filter((d) => matches(d, filter) && (!q || d.name.toLowerCase().includes(q)));
}

export interface ToastSpec {
  tone: 'success' | 'error';
  name: string;
  gid: string;
  action: 'copyPath' | 'retry';
}

/** Detects downloads that finished or failed between two snapshots. The first snapshot never toasts. */
export function diffSnapshots(prev: Download[] | null, next: Download[]): ToastSpec[] {
  if (!prev) return [];
  const before = new Map(prev.map((d) => [d.gid, d.status]));
  const out: ToastSpec[] = [];
  for (const d of next) {
    const was = before.get(d.gid);
    if (!was || was === d.status) continue;
    if (d.status === 'completed') out.push({ tone: 'success', name: d.name, gid: d.gid, action: 'copyPath' });
    if (d.status === 'failed') out.push({ tone: 'error', name: d.name, gid: d.gid, action: 'retry' });
  }
  return out;
}

export function summarize(list: Download[]) {
  return {
    active: list.filter((d) => d.status === 'downloading').length,
    queued: list.filter((d) => d.status === 'queued' || d.status === 'paused').length,
  };
}
