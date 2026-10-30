export type ThemePreference = 'light' | 'dark' | 'system';

const KEY = 'kestry.theme';

export function getThemePreference(): ThemePreference {
  try {
    const v = localStorage.getItem(KEY);
    return v === 'light' || v === 'dark' ? v : 'system';
  } catch {
    return 'system';
  }
}

/** Toggles the `.dark` class on <html> and remembers the choice in the browser. */
export function applyTheme(pref: ThemePreference, root: HTMLElement = document.documentElement) {
  const dark = pref === 'dark' || (pref === 'system' && matchMedia('(prefers-color-scheme: dark)').matches);
  root.classList.toggle('dark', dark);
  try {
    localStorage.setItem(KEY, pref);
  } catch {
    /* storage unavailable: the choice just won't persist */
  }
}
