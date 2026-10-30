import { act, renderHook } from '@testing-library/react';
import { defaultLocale, getPreferredLocale, isValidLocale, locales, localeStorageKey, resolveLocale } from './config';
import { createTranslator, format, getLocale, messages, setLocale, useLocale, useTranslations } from './index';

const leaves = (node: unknown, prefix = ''): string[] =>
  typeof node === 'string'
    ? [prefix]
    : Object.entries(node as object).flatMap(([k, v]) => leaves(v, prefix ? `${prefix}.${k}` : k));
const text = (locale: keyof typeof messages, key: string) =>
  key.split('.').reduce<any>((n, p) => n[p], messages[locale]) as string;
const placeholders = (s: string) => [...s.matchAll(/\{(\w+)(?:,|\})/g)].map((m) => m[1]).sort();

describe('locale config', () => {
  it('supports en, es, de and fr, defaulting to English', () => {
    expect([...locales]).toEqual(['en', 'es', 'de', 'fr']);
    expect(defaultLocale).toBe('en');
    expect(isValidLocale('de')).toBe(true);
    expect(isValidLocale('pt')).toBe(false);
  });

  it.each([
    ['es-ES,es;q=0.9,en;q=0.8', 'es'],
    ['fr-CA', 'fr'],
    ['pt-BR,pt;q=0.9,de;q=0.5', 'de'],
    ['en;q=0.1,fr;q=0.9', 'fr'],
    ['xx', 'en'],
    ['', 'en'],
    [null, 'en'],
  ])('prefers %s → %s', (header, expected) => expect(getPreferredLocale(header)).toBe(expected));

  it('a valid stored choice beats the browser languages; an invalid one is ignored', () => {
    expect(resolveLocale('de', 'es')).toBe('de');
    expect(resolveLocale('xx', 'es')).toBe('es');
    expect(resolveLocale(null, null)).toBe('en');
  });
});

describe('message files', () => {
  const keys = leaves(messages.en);

  it.each(locales)('%s has exactly the English keys and placeholders', (locale) => {
    expect(leaves(messages[locale]).sort()).toEqual([...keys].sort());
    for (const key of keys) expect(placeholders(text(locale, key)), `${locale}:${key}`).toEqual(placeholders(text('en', key)));
  });

  it('has no empty messages', () => {
    for (const locale of locales) for (const key of keys) expect(text(locale, key).trim(), `${locale}:${key}`).not.toBe('');
  });
});

describe('format', () => {
  it('interpolates values and leaves unknown placeholders alone', () => {
    expect(format('Hi {name}, {missing}', { name: 'Ana' }, 'en')).toBe('Hi Ana, {missing}');
  });

  it('chooses plural forms with # as the number', () => {
    const msg = '{count, plural, one {# file} other {# files}}';
    expect(format(msg, { count: 1 }, 'en')).toBe('1 file');
    expect(format(msg, { count: 3 }, 'en')).toBe('3 files');
    expect(format(msg, { count: 0 }, 'en')).toBe('0 files');
  });

  it('supports several plurals plus plain values in one message', () => {
    const msg = '{a, plural, one {# x} other {# xs}} · {b, plural, one {# y} other {# ys}} {name}';
    expect(format(msg, { a: 1, b: 2, name: 'z' }, 'en')).toBe('1 x · 2 ys z');
  });
});

describe('createTranslator', () => {
  it('translates per locale', () => {
    expect(createTranslator('es', 'Actions')('pause')).toBe('Pausar');
    expect(createTranslator('de', 'Actions')('pause')).toBe('Pausieren');
    expect(createTranslator('fr', 'Toasts')('added', { count: 2 })).toBe('2 téléchargements ajoutés');
  });

  it('reads nested keys and reports whether a key exists', () => {
    const t = createTranslator('en', 'Settings');
    expect(t('errors.downloadDir')).toMatch(/absolute path/);
    expect(t.has('errors.downloadDir')).toBe(true);
    expect(t.has('errors.nope')).toBe(false);
  });

  it('falls back to the key when nothing matches', () => {
    expect(createTranslator('en', 'Actions')('nope')).toBe('Actions.nope');
  });
});

describe('locale store', () => {
  beforeEach(() => {
    localStorage.clear();
    setLocale('en');
  });

  it('persists the choice, sets <html lang> and ignores unsupported locales', () => {
    setLocale('de');
    expect(getLocale()).toBe('de');
    expect(localStorage.getItem(localeStorageKey)).toBe('de');
    expect(document.documentElement.lang).toBe('de');

    setLocale('pt');
    expect(getLocale()).toBe('de');
  });

  it('re-renders hooks when the language changes', () => {
    const { result } = renderHook(() => ({ locale: useLocale(), t: useTranslations('Actions') }));
    expect(result.current.t('close')).toBe('Close');

    act(() => setLocale('fr'));

    expect(result.current.locale).toBe('fr');
    expect(result.current.t('close')).toBe('Fermer');
  });
});
