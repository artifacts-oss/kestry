export const locales = ['en', 'es', 'de', 'fr'] as const;

export type AppLocale = (typeof locales)[number];

export const defaultLocale: AppLocale = 'en';
export const localeStorageKey = 'kestry-locale';

export const isValidLocale = (locale: string): locale is AppLocale => locales.includes(locale as AppLocale);

/** Picks the best supported locale from an Accept-Language style list ("es-ES,es;q=0.9,en;q=0.8"). */
export function getPreferredLocale(acceptLanguage?: string | null): AppLocale {
  const candidates = (acceptLanguage ?? '')
    .split(',')
    .map((part) => {
      const [tag, ...params] = part.trim().split(';');
      const q = params.find((p) => p.trim().startsWith('q='));
      const quality = q ? Number(q.trim().slice(2)) : 1;
      return { tag: tag.toLowerCase(), quality: Number.isFinite(quality) ? quality : 0 };
    })
    .filter((c) => c.tag.length > 0)
    .sort((a, b) => b.quality - a.quality);

  for (const { tag } of candidates) {
    if (isValidLocale(tag)) return tag;
    const base = tag.split('-')[0];
    if (isValidLocale(base)) return base;
  }
  return defaultLocale;
}

/** A stored, valid choice wins; otherwise the browser's languages decide. */
export function resolveLocale(stored?: string | null, acceptLanguage?: string | null): AppLocale {
  return stored && isValidLocale(stored) ? stored : getPreferredLocale(acceptLanguage);
}
