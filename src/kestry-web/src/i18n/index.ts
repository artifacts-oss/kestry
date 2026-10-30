import { useMemo, useSyncExternalStore } from 'react';
import de from './messages/de.json';
import en from './messages/en.json';
import es from './messages/es.json';
import fr from './messages/fr.json';
import { isValidLocale, localeStorageKey, resolveLocale, type AppLocale } from './config';

export { locales, type AppLocale } from './config';

export const messages: Record<AppLocale, typeof en> = { en, es, de, fr };
export type Namespace = keyof typeof en;
export type Values = Record<string, string | number>;
export type Translator = ((key: string, values?: Values) => string) & { has: (key: string) => boolean };

const read = (): AppLocale => {
  let stored: string | null = null;
  try {
    stored = localStorage.getItem(localeStorageKey);
  } catch {
    /* storage unavailable: fall back to the browser languages */
  }
  return resolveLocale(stored, navigator.languages?.join(',') ?? navigator.language);
};

let current = read();
document.documentElement.lang = current;
const listeners = new Set<() => void>();

export const getLocale = () => current;

/** Switches language everywhere without a reload and remembers it in the browser. */
export function setLocale(next: string) {
  if (!isValidLocale(next) || next === current) return;
  current = next;
  document.documentElement.lang = next;
  try {
    localStorage.setItem(localeStorageKey, next);
  } catch {
    /* the choice just won't persist */
  }
  listeners.forEach((l) => l());
}

const subscribe = (l: () => void) => {
  listeners.add(l);
  return () => listeners.delete(l);
};

export const useLocale = () => useSyncExternalStore(subscribe, getLocale);

const lookup = (locale: AppLocale, path: string): string | undefined => {
  const found = path.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], messages[locale]);
  return typeof found === 'string' ? found : undefined;
};

/**
 * ICU subset: `{name}` and `{n, plural, one {# item} other {# items}}`.
 * shortcut: no select/number formats or nesting, add them when a message needs them.
 */
export function format(message: string, values: Values = {}, locale: AppLocale = current): string {
  const withPlurals = message.replace(/\{(\w+), plural,((?:\s*\w+ \{[^{}]*\})+)\s*\}/g, (_, name: string, options: string) => {
    const n = Number(values[name]);
    const forms = Object.fromEntries([...options.matchAll(/(\w+) \{([^{}]*)\}/g)].map((m) => [m[1], m[2]]));
    return (forms[new Intl.PluralRules(locale).select(n)] ?? forms.other ?? '').replace(/#/g, String(n));
  });
  return withPlurals.replace(/\{(\w+)\}/g, (whole, name: string) => (name in values ? String(values[name]) : whole));
}

/** Translator for one namespace; a missing key falls back to English, then to the key itself. */
export function createTranslator(locale: AppLocale, namespace: Namespace): Translator {
  const find = (key: string) => lookup(locale, `${namespace}.${key}`);
  const t = (key: string, values?: Values) => {
    const message = find(key) ?? lookup('en', `${namespace}.${key}`);
    return message === undefined ? `${namespace}.${key}` : format(message, values, locale);
  };
  return Object.assign(t, { has: (key: string) => find(key) !== undefined });
}

export function useTranslations(namespace: Namespace): Translator {
  const locale = useLocale();
  return useMemo(() => createTranslator(locale, namespace), [locale, namespace]);
}
