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

// The API's own codes are checked against the backend's ErrorCodes by ErrorCodesTests.cs (xUnit).
describe('aria2 exit codes', () => {
  it('are translated for 1-30 and 32 in every language', () => {
    for (const locale of locales) {
      const keys = Object.keys(messages[locale].Aria2Errors).map(Number).sort((a, b) => a - b);
      expect(keys).toEqual([...Array.from({ length: 30 }, (_, i) => i + 1), 32]);
    }
  });
});
