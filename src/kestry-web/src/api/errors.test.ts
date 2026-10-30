import { ApiError } from './client';
import { errorText } from './errors';
import { createTranslator, locales, messages } from '../i18n';

const es = createTranslator('es', 'Errors');

describe('errorText', () => {
  it('translates a known code, filling its params', () => {
    expect(errorText(new ApiError('x', 400, 'links.tooMany', { max: 2000 }), es, '?')).toBe('Como máximo 2000 enlaces por petición.');
    expect(errorText({ code: 'aria2.error', params: { message: 'boom' } }, es, '?')).toBe('aria2 ha devuelto un error: boom');
  });

  it.each([
    ['unknown code', new ApiError('x', 500, 'nope.nope')],
    ['no code', new ApiError('x', 500)],
    ['a network failure', new TypeError('Failed to fetch')],
    ['nothing', undefined],
  ])('falls back for %s', (_name, e) => expect(errorText(e, es, 'fallback')).toBe('fallback'));

  it('uses the language of the translator', () => {
    expect(errorText({ code: 'downloads.notFound' }, createTranslator('de', 'Errors'), '?')).toBe('Der Download existiert nicht mehr.');
  });
});

describe('error codes shared with the backend', () => {
  const backend = [
    'request.hostNotAllowed', 'request.originNotAllowed', 'request.tooLarge',
    'links.empty', 'links.tooLarge', 'links.tooMany',
    'torrents.count', 'torrents.tooLarge', 'torrents.invalid',
    'downloads.invalidSubfolder', 'downloads.noSource', 'downloads.notFound',
    'aria2.error', 'aria2.unavailable',
    'settings.invalid', 'settings.maxConcurrentDownloads.range', 'settings.limit.min', 'settings.seedRatio.range',
    'settings.downloadDir.notAbsolute', 'settings.downloadDir.outsideRoots', 'settings.downloadDir.notWritable',
    'settings.shutdownWhenDone.unavailable',
  ];

  it.each(locales)('%s translates every code the API can return', (locale) => {
    const t = createTranslator(locale, 'Errors');
    for (const code of backend) expect(t.has(code), `${locale}:${code}`).toBe(true);
  });

  it('covers aria2 exit codes 1-30 and 32 in every language', () => {
    for (const locale of locales) {
      const keys = Object.keys(messages[locale].Aria2Errors).map(Number).sort((a, b) => a - b);
      expect(keys).toEqual([...Array.from({ length: 30 }, (_, i) => i + 1), 32]);
    }
  });
});
