import { cx } from './cx';
import { Icon, type IconName } from './Icon';
import { IconButton } from './Button';
import { ActionMenu, type MenuAction } from './ActionMenu';
import { ProgressBar, StatusBadge, type DownloadStatus } from './status';
import { formatBytes, formatEta, formatSpeed } from './format';
import { useNarrow } from './useNarrow';
import { strings } from '../../strings';

export interface DownloadItemProps {
  name: string;
  path?: string;
  status: DownloadStatus;
  fileType?: Extract<IconName, 'file' | 'video' | 'archive' | 'music' | 'image'>;
  source?: keyof typeof strings.sources;
  size?: number;
  downloaded?: number;
  speed?: number;
  uploadSpeed?: number;
  eta?: number;
  connections?: number;
  seeders?: number;
  fetchingMetadata?: boolean;
  error?: string;
  onPause?(): void;
  onResume?(): void;
  onCancel?(): void;
  onRetry?(): void;
  onRemove?(): void;
  onCopyPath?(): void;
  className?: string;
}

function metaLine(p: DownloadItemProps): string {
  const { status } = p;
  if (status === 'failed') return p.error || strings.downloadFailed;
  if (p.fetchingMetadata) return strings.fetchingMetadata;
  if (status === 'completed') return formatBytes(p.size);
  if (status === 'queued') return `${strings.waiting} · ${formatBytes(p.size)}`;
  const parts = [`${formatBytes(p.downloaded ?? 0)} ${strings.of} ${formatBytes(p.size)}`];
  if (status === 'downloading') {
    if (p.speed) {
      const up = p.source === 'torrent' || p.source === 'magnet' ? ` ↑ ${formatSpeed(p.uploadSpeed ?? 0)}` : '';
      parts.push(`↓ ${formatSpeed(p.speed)}${up}`);
    }
    const eta = formatEta(p.eta);
    if (eta) parts.push(eta);
  }
  return parts.join(' · ');
}

export function DownloadItem(p: DownloadItemProps) {
  const narrow = useNarrow();
  const { status } = p;
  const pct = p.size ? ((p.downloaded ?? 0) / p.size) * 100 : undefined;
  const isTorrent = p.source === 'torrent' || p.source === 'magnet';

  const a = strings.actions;
  const actions: MenuAction[] = [];
  if (status === 'downloading') actions.push({ icon: 'pause', label: a.pause, onSelect: p.onPause });
  if (status === 'paused') actions.push({ icon: 'play', label: a.resume, onSelect: p.onResume });
  if (status === 'failed') actions.push({ icon: 'retry', label: a.retry, onSelect: p.onRetry });
  if (status === 'completed') actions.push({ icon: 'folder', label: a.copyPath, onSelect: p.onCopyPath });
  if (status === 'completed' || status === 'failed') actions.push({ icon: 'trash', label: a.remove, onSelect: p.onRemove });
  else actions.push({ icon: 'x', label: a.cancel, onSelect: p.onCancel });

  const detail = [p.source && strings.sources[p.source], metaLine(p), isTorrent && status === 'downloading' && p.connections != null && strings.peers(p.connections, p.seeders ?? 0)]
    .filter(Boolean)
    .join(' · ');

  return (
    <div
      className={cx(
        'grid grid-cols-[40px_minmax(0,1fr)_auto] gap-4 items-center p-4 bg-surface-100 border border-line rounded-md',
        p.className,
      )}
    >
      <div className="grid place-items-center size-10 rounded-md bg-slate-100 text-slate-700">
        <Icon name={p.fileType ?? 'file'} />
      </div>
      <div className="flex flex-col gap-1.5 min-w-0">
        <div className="flex items-center gap-2 min-w-0">
          <span className="truncate text-body-sm font-semibold text-ink" title={p.path ?? p.name}>{p.name}</span>
          <StatusBadge status={status} />
        </div>
        {status !== 'completed' && (
          <ProgressBar
            status={status}
            value={p.fetchingMetadata ? undefined : status === 'failed' ? (pct ?? 0) : pct}
            label={strings.progressLabel(p.name)}
          />
        )}
        <span className={cx('text-label font-normal tracking-normal tabular-nums', status === 'failed' ? 'text-danger' : 'text-ink-muted')}>
          {detail}
        </span>
      </div>
      <div className="flex gap-1">
        {narrow ? (
          <ActionMenu actions={actions} />
        ) : (
          actions.map((x) => (
            <IconButton key={x.label} icon={x.icon} label={`${x.label} ${p.name}`} size="sm" onClick={x.onSelect} />
          ))
        )}
      </div>
    </div>
  );
}
