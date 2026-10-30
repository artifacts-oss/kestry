import { useSyncExternalStore } from 'react';

const query = '(max-width: 639px)'; // below Tailwind's `sm`

const subscribe = (cb: () => void) => {
  const mq = window.matchMedia?.(query);
  mq?.addEventListener('change', cb);
  return () => mq?.removeEventListener('change', cb);
};

/** True below the `sm` breakpoint; false where matchMedia is unavailable (tests). */
export const useNarrow = () => useSyncExternalStore(subscribe, () => window.matchMedia?.(query).matches ?? false);
