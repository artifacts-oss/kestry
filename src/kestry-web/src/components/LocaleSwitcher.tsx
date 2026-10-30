import { DropdownMenu } from 'radix-ui';
import { locales, setLocale, useLocale, useTranslations } from '../i18n';
import { IconButton } from './ui/Button';
import { Icon } from './ui/Icon';

export function LocaleSwitcher() {
  const locale = useLocale();
  const t = useTranslations('LocaleSwitcher');
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <IconButton icon="globe" label={t('trigger')} />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content align="end" sideOffset={4} className="z-50 min-w-48 p-1 bg-surface-000 border border-line rounded-md shadow-soft">
          <DropdownMenu.Label className="px-3 py-1.5 text-caption text-ink-muted">{t('title')}</DropdownMenu.Label>
          <DropdownMenu.RadioGroup value={locale} onValueChange={setLocale}>
            {locales.map((option) => (
              <DropdownMenu.RadioItem
                key={option}
                value={option}
                className="flex items-center justify-between gap-2 h-9 px-3 rounded-sm text-body-sm text-ink cursor-pointer outline-none data-highlighted:bg-surface-200"
              >
                {t(`options.${option}`)}
                <DropdownMenu.ItemIndicator className="text-slate-600">
                  <Icon name="check" size={16} />
                </DropdownMenu.ItemIndicator>
              </DropdownMenu.RadioItem>
            ))}
          </DropdownMenu.RadioGroup>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
