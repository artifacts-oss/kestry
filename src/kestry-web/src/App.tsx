import { useEffect, useState } from 'react';
import { api } from './api/client';
import { errorText } from './api/errors';
import type { SystemInfo } from './api/types';
import { useSnapshot } from './api/useSnapshot';
import { Header } from './components/Header';
import { SettingsDrawer } from './components/SettingsDrawer';
import { ToastProvider, useToast } from './components/ui/Toaster';
import { DownloadsPage } from './pages/DownloadsPage';
import { useSettings } from './settings';
import { useTranslations } from './i18n';
import { applyTheme, getThemePreference } from './theme';

function Shell() {
  const toast = useToast();
  const t = useTranslations('Settings');
  const te = useTranslations('Errors');
  const { snapshot, connected } = useSnapshot();
  const { settings, save } = useSettings(snapshot?.settingsVersion);
  const [system, setSystem] = useState<SystemInfo | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const state = snapshot?.aria2.state;

  useEffect(() => applyTheme(getThemePreference()), []);
  // The aria2 version is only known once it is up, so ask again whenever its state changes.
  useEffect(() => {
    if (state === 'running') api.system().then(setSystem).catch(() => {});
  }, [state]);

  const saveSafely = async (next: Parameters<typeof save>[0]) => {
    try {
      return await save(next);
    } catch (e) {
      toast({ tone: 'error', message: errorText(e, te, t('saveFailed')) });
      return {};
    }
  };

  return (
    <div className="flex flex-col min-h-screen">
      <Header
        settings={settings}
        shutdownAvailable={system?.shutdownAvailable ?? false}
        onToggle={(patch) => settings && void saveSafely({ ...settings, ...patch })}
        onOpenSettings={() => setSettingsOpen(true)}
      />
      <DownloadsPage snapshot={snapshot} connected={connected} aria2Version={system?.aria2Version ?? null} />
      <SettingsDrawer open={settingsOpen} onClose={() => setSettingsOpen(false)} settings={settings} system={system} onSave={saveSafely} />
    </div>
  );
}

export function App() {
  return (
    <ToastProvider>
      <Shell />
    </ToastProvider>
  );
}
