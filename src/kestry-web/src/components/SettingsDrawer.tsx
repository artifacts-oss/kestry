import { useEffect, useState } from 'react';
import type { AppSettings, SystemInfo } from '../api/types';
import { splitLimit, toKBps, type LimitUnit } from '../settings';
import { strings } from '../strings';
import { applyTheme, getThemePreference, type ThemePreference } from '../theme';
import { Select, TextField } from './ui/fields';
import { Drawer } from './ui/overlays';
import { Segmented } from './ui/Segmented';
import { Switch } from './ui/Switch';

const units: { value: LimitUnit; label: string }[] = [{ value: 'KB', label: 'KB/s' }, { value: 'MB', label: 'MB/s' }];

interface Draft {
  maxConcurrent: string;
  download: string;
  downloadUnit: LimitUnit;
  upload: string;
  uploadUnit: LimitUnit;
  dir: string;
  seedRatio: string;
}

const toDraft = (s: AppSettings): Draft => {
  const d = splitLimit(s.downloadLimitKBps);
  const u = splitLimit(s.uploadLimitKBps);
  return {
    maxConcurrent: String(s.maxConcurrentDownloads),
    download: d.value, downloadUnit: d.unit,
    upload: u.value, uploadUnit: u.unit,
    dir: s.downloadDir,
    seedRatio: String(s.seedRatio),
  };
};

const fromDraft = (d: Draft): Partial<AppSettings> => ({
  maxConcurrentDownloads: Number(d.maxConcurrent),
  downloadLimitKBps: toKBps(d.download, d.downloadUnit),
  uploadLimitKBps: toKBps(d.upload, d.uploadUnit),
  downloadDir: d.dir.trim(),
  seedRatio: Number(d.seedRatio),
});

/** Every control saves as soon as it changes (text fields when they lose focus). */
export function SettingsDrawer({ open, onClose, settings, system, onSave }: {
  open: boolean;
  onClose: () => void;
  settings: AppSettings | null;
  system: SystemInfo | null;
  onSave: (next: AppSettings) => Promise<Record<string, string>>;
}) {
  const [draft, setDraft] = useState<Draft | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [theme, setTheme] = useState<ThemePreference>(getThemePreference);

  useEffect(() => {
    if (settings) setDraft(toDraft(settings));
  }, [settings]);

  if (!settings || !draft) return <Drawer open={open} onClose={onClose} title={strings.settings.title} />;

  const t = strings.settings;
  const commit = async (patch: Partial<AppSettings>) => {
    const changed = Object.entries(patch).some(([k, v]) => settings[k as keyof AppSettings] !== v);
    if (!changed) return;
    setErrors(await onSave({ ...settings, ...patch }));
  };
  const commitDraft = (next: Draft = draft) => commit(fromDraft(next));
  const update = (patch: Partial<Draft>, save = false) => {
    const next = { ...draft, ...patch };
    setDraft(next);
    if (save) void commitDraft(next);
  };
  const err = (field: string) => (errors[field] ? (t.errors[field] ?? errors[field]) : undefined);
  const shutdownReason = system && !system.shutdownAvailable ? t.shutdownUnavailable[system.shutdownUnavailableReason ?? 'unsupported'] : undefined;

  return (
    <Drawer open={open} onClose={onClose} title={t.title}>
      <TextField
        label={t.maxConcurrent} type="number" min={1} max={20} inputMode="numeric"
        value={draft.maxConcurrent} error={err('maxConcurrentDownloads')}
        onChange={(e) => update({ maxConcurrent: e.target.value })} onBlur={() => commitDraft()}
      />

      <Switch label={t.speedLimit} checked={settings.speedLimitEnabled} onChange={(v) => commit({ speedLimitEnabled: v })} />
      <div className="grid grid-cols-[1fr_7rem] gap-2 items-start">
        <TextField
          label={t.downloadLimit} type="number" min={1} inputMode="numeric"
          value={draft.download} error={err('downloadLimitKBps')}
          onChange={(e) => update({ download: e.target.value })} onBlur={() => commitDraft()}
        />
        <Select label={t.unit} options={units} value={draft.downloadUnit} onChange={(e) => update({ downloadUnit: e.target.value as LimitUnit }, true)} />
        <TextField
          label={t.uploadLimit} type="number" min={1} inputMode="numeric"
          value={draft.upload} error={err('uploadLimitKBps')}
          onChange={(e) => update({ upload: e.target.value })} onBlur={() => commitDraft()}
        />
        <Select label={t.unit} options={units} value={draft.uploadUnit} onChange={(e) => update({ uploadUnit: e.target.value as LimitUnit }, true)} />
      </div>

      <TextField
        mono label={t.downloadDir} hint={t.downloadDirHint}
        value={draft.dir} error={err('downloadDir')}
        onChange={(e) => update({ dir: e.target.value })} onBlur={() => commitDraft()}
      />

      <div className="flex flex-col gap-1.5">
        <Switch label={t.shutdown} checked={settings.shutdownWhenDone} disabled={!!shutdownReason && !settings.shutdownWhenDone} onChange={(v) => commit({ shutdownWhenDone: v })} />
        {(shutdownReason || err('shutdownWhenDone')) && (
          <p className="m-0 text-caption tracking-normal text-ink-muted">{err('shutdownWhenDone') ?? shutdownReason}</p>
        )}
      </div>

      <Switch label={t.seeding} checked={settings.seedingEnabled} onChange={(v) => commit({ seedingEnabled: v })} />
      {settings.seedingEnabled && (
        <TextField
          label={t.seedRatio} type="number" min={0.1} step={0.1} inputMode="decimal"
          value={draft.seedRatio} error={err('seedRatio')}
          onChange={(e) => update({ seedRatio: e.target.value })} onBlur={() => commitDraft()}
        />
      )}

      <Segmented
        label={t.theme}
        value={theme}
        onChange={(v) => { setTheme(v); applyTheme(v); }}
        options={(['light', 'dark', 'system'] as const).map((v) => ({ value: v, label: t.themes[v] }))}
      />
    </Drawer>
  );
}
