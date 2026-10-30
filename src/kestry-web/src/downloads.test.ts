import type { Download } from './api/types';
import { countByFilter, diffSnapshots, filterDownloads, summarize } from './downloads';

const d = (gid: string, status: Download['status'], extra: Partial<Download> = {}): Download => ({
  gid, name: `${gid}.bin`, path: `/d/${gid}.bin`, status, source: 'https', fileType: 'file', size: 1, downloaded: 0, speed: 0,
  uploadSpeed: 0, eta: null, connections: 0, seeders: 0, fetchingMetadata: false, seeding: false, errorCode: 0, error: null, ...extra,
});

const list = [d('a', 'downloading'), d('b', 'queued'), d('c', 'paused'), d('d', 'completed'), d('e', 'failed')];

describe('filters', () => {
  it('counts per filter, with paused inside "queued"', () => {
    expect(countByFilter(list)).toEqual({ all: 5, downloading: 1, queued: 2, completed: 1, failed: 1 });
  });

  it('filters by status', () => {
    expect(filterDownloads(list, 'queued', '').map((x) => x.gid)).toEqual(['b', 'c']);
    expect(filterDownloads(list, 'all', '')).toHaveLength(5);
  });

  it('searches names case-insensitively and combines with the filter', () => {
    expect(filterDownloads(list, 'all', ' A.BIN ').map((x) => x.gid)).toEqual(['a']);
    expect(filterDownloads(list, 'failed', 'a')).toEqual([]);
  });

  it('summarizes running and waiting downloads', () => {
    expect(summarize(list)).toEqual({ active: 1, queued: 2 });
  });
});

describe('diffSnapshots', () => {
  it('never toasts for the first snapshot', () => {
    expect(diffSnapshots(null, [d('a', 'completed')])).toEqual([]);
  });

  it('toasts when a download completes, with a copy-path action', () => {
    const out = diffSnapshots([d('a', 'downloading')], [d('a', 'completed')]);
    expect(out).toEqual([{ tone: 'success', name: 'a.bin', gid: 'a', action: 'copyPath' }]);
  });

  it('toasts an error with retry when a download fails', () => {
    const out = diffSnapshots([d('a', 'downloading')], [d('a', 'failed')]);
    expect(out).toEqual([{ tone: 'error', name: 'a.bin', gid: 'a', action: 'retry' }]);
  });

  it('does not repeat for unchanged or already-finished downloads', () => {
    expect(diffSnapshots([d('a', 'completed')], [d('a', 'completed')])).toEqual([]);
  });

  it('ignores downloads that first appear already finished (e.g. after a restart)', () => {
    expect(diffSnapshots([], [d('a', 'completed')])).toEqual([]);
  });

  it('announces a torrent once when it finishes downloading, not again when seeding ends', () => {
    const seeding = d('a', 'completed', { seeding: true });
    expect(diffSnapshots([d('a', 'downloading')], [seeding])).toHaveLength(1);
    expect(diffSnapshots([seeding], [d('a', 'completed')])).toEqual([]);
  });
});
