import type { Aria2State, Snapshot } from '../api/types';
import { FILTERS, type Filter } from '../downloads';
import { useTranslations } from '../i18n';
import { formatBytes } from './ui/format';
import type { IconName } from './ui/Icon';
import { NavItem, SpeedStat } from './ui/layout';
import { StatusBadge, type DownloadStatus } from './ui/status';

const icons: Record<Filter, IconName> = { all: 'inbox', downloading: 'download', queued: 'clock', completed: 'check', failed: 'alert' };

const split = (bytes: number | null | undefined) => {
  const [value, unit] = formatBytes(bytes).split(' ');
  return { value, unit };
};

function Connection({ snapshot, connected, aria2Version }: { snapshot: Snapshot | null; connected: boolean; aria2Version: string | null }) {
  const c = useTranslations('Connection');
  const reasons = useTranslations('Aria2Reasons');
  let tone: DownloadStatus;
  let text: string;
  let reason: string | null = null;
  const state: Aria2State | undefined = snapshot?.aria2.state;

  if (!connected || !snapshot) [tone, text] = ['failed', c('offline')];
  else if (state === 'running') [tone, text] = ['completed', c('connected', { version: aria2Version ?? '' })];
  else if (state === 'installing') [tone, text] = ['downloading', c('installing')];
  else if (state === 'restarting') [tone, text] = ['paused', c('restarting')];
  else {
    [tone, text] = ['failed', c('down')];
    const code = snapshot.aria2.reasonCode;
    reason = (code && reasons.has(code) ? reasons(code) : null) || snapshot.aria2.reason;
  }

  return (
    <div className="flex flex-col items-start gap-2">
      <StatusBadge status={tone}>{text}</StatusBadge>
      {reason && <p className="m-0 text-caption tracking-normal text-ink-muted">{reason}</p>}
    </div>
  );
}

export function Sidebar({ filter, onFilter, counts, snapshot, connected, aria2Version }: {
  filter: Filter;
  onFilter: (f: Filter) => void;
  counts: Record<Filter, number>;
  snapshot: Snapshot | null;
  connected: boolean;
  aria2Version: string | null;
}) {
  const f = useTranslations('Filters');
  const st = useTranslations('Stats');
  const down = split(snapshot?.stats.downloadSpeed ?? 0);
  const up = split(snapshot?.stats.uploadSpeed ?? 0);
  const free = split(snapshot?.stats.freeDiskBytes);

  return (
    <aside className="flex flex-col gap-4 p-4 bg-surface-100 border-b border-line lg:w-60 lg:flex-none lg:border-b-0 lg:border-r lg:gap-6">
      <nav aria-label={f('label')} className="flex gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
        {FILTERS.map((name) => (
          <NavItem
            key={name}
            label={f(name)}
            icon={icons[name]}
            count={counts[name]}
            active={filter === name}
            onClick={() => onFilter(name)}
            className="w-auto shrink-0 lg:w-full"
          />
        ))}
      </nav>
      <div className="hidden lg:flex lg:flex-col lg:gap-2">
        <SpeedStat icon="download" label={st('download')} value={down.value} unit={`${down.unit}/s`} className="min-w-0" />
        <SpeedStat icon="upload" label={st('upload')} value={up.value} unit={`${up.unit}/s`} className="min-w-0" />
        <SpeedStat icon="folder" label={st('freeDisk')} value={free.value} unit={free.unit} className="min-w-0" />
      </div>
      <div className="lg:mt-auto">
        <Connection snapshot={snapshot} connected={connected} aria2Version={aria2Version} />
      </div>
    </aside>
  );
}
