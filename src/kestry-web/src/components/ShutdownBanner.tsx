import { Banner } from './ui/layout';
import { Button } from './ui/Button';
import { useTranslations } from '../i18n';

export function ShutdownBanner({ seconds, onCancel }: { seconds: number | null; onCancel: () => void }) {
  const t = useTranslations('ShutdownBanner');
  if (seconds == null) return null;
  return (
    <Banner tone="action">
      <span className="flex-1">{t('message', { seconds })}</span>
      <Button variant="secondary" size="sm" onClick={onCancel}>{t('cancel')}</Button>
    </Banner>
  );
}
