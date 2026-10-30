import { Banner } from './ui/layout';
import { Button } from './ui/Button';
import { strings } from '../strings';

export function ShutdownBanner({ seconds, onCancel }: { seconds: number | null; onCancel: () => void }) {
  if (seconds == null) return null;
  return (
    <Banner tone="action">
      <span className="flex-1">{strings.shutdownBanner.message(seconds)}</span>
      <Button variant="secondary" size="sm" onClick={onCancel}>{strings.shutdownBanner.cancel}</Button>
    </Banner>
  );
}
