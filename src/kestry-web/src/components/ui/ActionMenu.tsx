import { DropdownMenu } from 'radix-ui';
import { Icon, type IconName } from './Icon';
import { IconButton } from './Button';
import { strings } from '../../strings';

export interface MenuAction {
  icon: IconName;
  label: string;
  onSelect?: () => void;
}

export function ActionMenu({ actions, label = strings.actions.more }: { actions: MenuAction[]; label?: string }) {
  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <IconButton icon="sliders" label={label} size="sm" />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="end"
          sideOffset={4}
          className="z-50 min-w-40 p-1 bg-surface-000 border border-line rounded-md shadow-soft"
        >
          {actions.map((a) => (
            <DropdownMenu.Item
              key={a.label}
              onSelect={a.onSelect}
              className="flex items-center gap-2 h-9 px-3 rounded-sm text-body-sm text-ink cursor-pointer outline-none data-highlighted:bg-surface-200"
            >
              <Icon name={a.icon} size={16} />
              {a.label}
            </DropdownMenu.Item>
          ))}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
