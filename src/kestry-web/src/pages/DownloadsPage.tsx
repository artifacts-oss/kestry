import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/client';
import type { Download, Snapshot } from '../api/types';
import { ConfirmDialog, type Confirmation } from '../components/ConfirmDialog';
import { Header } from '../components/Header';
import { DownloadsList } from '../components/DownloadsList';
import { Sidebar } from '../components/Sidebar';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/fields';
import { EmptyState, Toolbar } from '../components/ui/layout';
import { useToast } from '../components/ui/Toaster';
import { countByFilter, diffSnapshots, filterDownloads, summarize, type Filter } from '../downloads';
import { strings } from '../strings';

export function DownloadsPage({ snapshot, connected, aria2Version }: { snapshot: Snapshot | null; connected: boolean; aria2Version: string | null }) {
  const toast = useToast();
  const [filter, setFilter] = useState<Filter>('all');
  const [query, setQuery] = useState('');
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const downloads = useMemo(() => snapshot?.downloads ?? [], [snapshot]);

  const previous = useRef<Download[] | null>(null);
  const run = async (action: () => Promise<unknown>, success?: string) => {
    try {
      await action();
      if (success) toast({ tone: 'info', message: success });
    } catch (e) {
      toast({ tone: 'error', message: (e as Error).message || strings.toasts.requestFailed });
    }
  };
  const copyPath = (d: Download) => run(() => navigator.clipboard.writeText(d.path), strings.toasts.pathCopied);
  const retry = (d: Download) => run(() => api.retry(d.gid), strings.toasts.retried);

  useEffect(() => {
    if (!snapshot) return;
    for (const t of diffSnapshots(previous.current, snapshot.downloads)) {
      const d = snapshot.downloads.find((x) => x.gid === t.gid)!;
      const isCopy = t.action === 'copyPath';
      toast({
        tone: t.tone,
        message: t.message,
        actionLabel: isCopy ? strings.actions.copyPath : strings.actions.retry,
        onAction: () => (isCopy ? copyPath(d) : retry(d)),
      });
    }
    previous.current = snapshot.downloads;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only a new snapshot should produce toasts
  }, [snapshot]);

  const visible = filterDownloads(downloads, filter, query);
  const { active, queued } = summarize(downloads);

  return (
    <div className="flex flex-col min-h-screen">
      <Header />
      <div className="flex flex-col flex-1 lg:flex-row">
      <Sidebar filter={filter} onFilter={setFilter} counts={countByFilter(downloads)} snapshot={snapshot} connected={connected} aria2Version={aria2Version} />
      <main className="flex flex-col flex-1 min-w-0 bg-surface-000">
        <Toolbar label={strings.toolbarLabel} className="flex-wrap">
          <TextField icon="search" aria-label={strings.search} placeholder={strings.search} value={query} onChange={(e) => setQuery(e.target.value)} className="w-full sm:w-72" />
          <Toolbar.Spacer />
          <Button variant="quiet" icon="pause" onClick={() => run(api.pauseAll)}>{strings.pauseAll}</Button>
          <Button variant="quiet" icon="play" onClick={() => run(api.resumeAll)}>{strings.resumeAll}</Button>
        </Toolbar>
        <div className="flex flex-col gap-4 p-4 lg:px-12 lg:py-8">
          {downloads.length > 0 && <p className="m-0 text-body-sm text-ink-muted">{strings.summary(active, queued)}</p>}
          {downloads.length === 0 ? (
            <EmptyState title={strings.empty.title} description={strings.empty.description} />
          ) : visible.length === 0 ? (
            <p className="m-0 py-8 text-center text-body-sm text-ink-muted">{strings.noResults}</p>
          ) : (
            <DownloadsList
              downloads={visible}
              actions={{
                onPause: (d) => run(() => api.pause(d.gid)),
                onResume: (d) => run(() => api.resume(d.gid)),
                onCancel: (d) => setConfirmation({ kind: 'cancel', gid: d.gid, name: d.name }),
                onRetry: retry,
                onRemove: (d) => setConfirmation({ kind: 'remove', gid: d.gid, name: d.name }),
                onCopyPath: copyPath,
              }}
            />
          )}
        </div>
      </main>
      </div>
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => setConfirmation(null)}
        onConfirm={(c, deleteFiles) => {
          setConfirmation(null);
          run(() => api.remove(c.gid, deleteFiles), c.kind === 'cancel' ? strings.toasts.cancelled : strings.toasts.removed);
        }}
      />
    </div>
  );
}
