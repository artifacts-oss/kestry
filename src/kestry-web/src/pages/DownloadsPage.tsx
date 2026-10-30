import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/client';
import type { Download, Snapshot } from '../api/types';
import { AddDownloadsModal } from '../components/AddDownloadsModal';
import { ConfirmDialog, type Confirmation } from '../components/ConfirmDialog';
import { ShutdownBanner } from '../components/ShutdownBanner';
import { DownloadsList } from '../components/DownloadsList';
import { Sidebar } from '../components/Sidebar';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/fields';
import { EmptyState, Toolbar } from '../components/ui/layout';
import { useToast } from '../components/ui/Toaster';
import { countByFilter, diffSnapshots, filterDownloads, summarize, type Filter } from '../downloads';
import { useTranslations } from '../i18n';

export function DownloadsPage({ snapshot, connected, aria2Version }: { snapshot: Snapshot | null; connected: boolean; aria2Version: string | null }) {
  const toast = useToast();
  const t = useTranslations('Dashboard');
  const tt = useTranslations('Toasts');
  const ta = useTranslations('Actions');
  const ts = useTranslations('ShutdownBanner');
  const tc = useTranslations('Add');
  const [filter, setFilter] = useState<Filter>('all');
  const [query, setQuery] = useState('');
  const [adding, setAdding] = useState<string | null>(null); // null = closed, otherwise the initial text
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const downloads = useMemo(() => snapshot?.downloads ?? [], [snapshot]);

  const previous = useRef<Download[] | null>(null);
  const run = async (action: () => Promise<unknown>, success?: string) => {
    try {
      await action();
      if (success) toast({ tone: 'info', message: success });
    } catch (e) {
      toast({ tone: 'error', message: (e as Error).message || tt('requestFailed') });
    }
  };
  const copyPath = (d: Download) => run(() => navigator.clipboard.writeText(d.path), tt('pathCopied'));
  const retry = (d: Download) => run(() => api.retry(d.gid), tt('retried'));

  useEffect(() => {
    if (!snapshot) return;
    for (const spec of diffSnapshots(previous.current, snapshot.downloads)) {
      const d = snapshot.downloads.find((x) => x.gid === spec.gid)!;
      const isCopy = spec.action === 'copyPath';
      toast({
        tone: spec.tone,
        message: tt(spec.tone === 'success' ? 'completed' : 'failed', { name: spec.name }),
        actionLabel: isCopy ? ta('copyPath') : ta('retry'),
        onAction: () => (isCopy ? copyPath(d) : retry(d)),
      });
    }
    previous.current = snapshot.downloads;
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only a new snapshot should produce toasts
  }, [snapshot]);

  // Pasting outside a text field opens the add dialog with the clipboard text.
  useEffect(() => {
    const onPaste = (e: ClipboardEvent) => {
      const target = e.target as HTMLElement | null;
      if (adding !== null || target?.closest('input, textarea, select, [contenteditable="true"], [role="dialog"]')) return;
      const text = e.clipboardData?.getData('text') ?? '';
      if (!text.trim()) return;
      e.preventDefault();
      setAdding(text);
    };
    document.addEventListener('paste', onPaste);
    return () => document.removeEventListener('paste', onPaste);
  }, [adding]);

  const visible = filterDownloads(downloads, filter, query);
  const { active, queued } = summarize(downloads);

  return (
    <div className="flex flex-col flex-1 lg:flex-row">
      <Sidebar filter={filter} onFilter={setFilter} counts={countByFilter(downloads)} snapshot={snapshot} connected={connected} aria2Version={aria2Version} />
      <main className="flex flex-col flex-1 min-w-0 bg-surface-000">
        <Toolbar label={t('toolbar')} className="flex-wrap">
          <TextField icon="search" aria-label={t('search')} placeholder={t('search')} value={query} onChange={(e) => setQuery(e.target.value)} className="w-full sm:w-72" />
          <Toolbar.Spacer />
          <Button variant="quiet" icon="pause" onClick={() => run(api.pauseAll)}>{t('pauseAll')}</Button>
          <Button variant="quiet" icon="play" onClick={() => run(api.resumeAll)}>{t('resumeAll')}</Button>
          <Button variant="primary" icon="plus" onClick={() => setAdding('')}>{tc('button')}</Button>
        </Toolbar>
        <div className="flex flex-col gap-4 p-4 lg:px-12 lg:py-8">
          <ShutdownBanner seconds={snapshot?.shutdown ?? null} onCancel={() => run(api.cancelShutdown, ts('cancelled'))} />
          {downloads.length > 0 && <p className="m-0 text-body-sm text-ink-muted">{t('summary', { active, queued })}</p>}
          {downloads.length === 0 ? (
            <EmptyState
              title={t('emptyTitle')}
              description={t('emptyDescription')}
              action={<Button variant="primary" icon="plus" className="h-12 px-6" onClick={() => setAdding('')}>{tc('pasteHere')}</Button>}
            />
          ) : visible.length === 0 ? (
            <p className="m-0 py-8 text-center text-body-sm text-ink-muted">{t('noResults')}</p>
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
      <AddDownloadsModal open={adding !== null} initialText={adding ?? ''} onClose={() => setAdding(null)} />
      <ConfirmDialog
        confirmation={confirmation}
        onClose={() => setConfirmation(null)}
        onConfirm={(c, deleteFiles) => {
          setConfirmation(null);
          run(() => api.remove(c.gid, deleteFiles), c.kind === 'cancel' ? tt('cancelled') : tt('removed'));
        }}
      />
    </div>
  );
}
