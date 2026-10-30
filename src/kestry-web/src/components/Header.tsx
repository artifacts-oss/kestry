import type { AppSettings } from '../api/types';
import { useTranslations } from '../i18n';
import { LocaleSwitcher } from './LocaleSwitcher';
import { IconButton } from './ui/Button';
import { Icon } from './ui/Icon';
import { Switch } from './ui/Switch';

export function Header({ settings, shutdownAvailable, onToggle, onOpenSettings }: {
  settings: AppSettings | null;
  shutdownAvailable: boolean;
  onToggle: (patch: Partial<AppSettings>) => void;
  onOpenSettings: () => void;
}) {
  const t = useTranslations('Header');
  const app = useTranslations('App');
  return (
    <header className="flex items-center gap-3 px-4 py-3 bg-surface-000 border-b border-line">
      {/* Reserved slot until the logo exists. */}
      <div aria-hidden className="size-9 rounded-md bg-slate-100" />
      <span className="font-display text-heading-2 font-medium text-ink">{app('name')}</span>
      <span className="flex-1" />
      {settings && (
        <>
          <span className="inline-flex items-center gap-2 text-ink-muted">
            <Icon name="gauge" size={18} />
            <Switch
              label={t('speedLimit')}
              checked={settings.speedLimitEnabled}
              onChange={(v) => onToggle({ speedLimitEnabled: v })}
              className="[&>label]:sr-only sm:[&>label]:not-sr-only"
            />
          </span>
          <Switch
            label={t('shutdown')}
            checked={settings.shutdownWhenDone}
            disabled={!shutdownAvailable && !settings.shutdownWhenDone}
            onChange={(v) => onToggle({ shutdownWhenDone: v })}
            className="[&>label]:sr-only sm:[&>label]:not-sr-only"
          />
        </>
      )}
      <LocaleSwitcher />
      <IconButton icon="sliders" label={t('settings')} onClick={onOpenSettings} />
    </header>
  );
}
