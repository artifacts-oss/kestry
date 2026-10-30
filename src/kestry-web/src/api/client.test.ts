import { api, ApiError } from './client';


const reply = (status: number, body?: unknown) =>
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(body === undefined ? null : JSON.stringify(body), { status })));

afterEach(() => vi.unstubAllGlobals());

describe('api errors', () => {
  it('keeps code, params and per-field errors from the server', async () => {
    reply(400, {
      code: 'settings.invalid', error: 'Invalid settings',
      errors: { seedRatio: { code: 'settings.seedRatio.range', error: 'x' } },
    });

    const e = await api.getSettings().catch((x) => x);

    expect(e).toBeInstanceOf(ApiError);
    expect(e).toMatchObject({ status: 400, code: 'settings.invalid', message: 'Invalid settings' });
    expect(e.fieldErrors.seedRatio.code).toBe('settings.seedRatio.range');
  });

  it('exposes params for placeholders', async () => {
    reply(400, { code: 'links.tooMany', error: 'x', params: { max: 2000 } });
    expect(await api.previewLinks('a').catch((x) => x)).toMatchObject({ code: 'links.tooMany', params: { max: 2000 } });
  });

  it('gives the empty 413 from Kestrel its own code', async () => {
    reply(413);
    expect(await api.previewLinks('a').catch((x) => x)).toMatchObject({ status: 413, code: 'request.tooLarge' });
  });

  it('has no code for an unexpected failure', async () => {
    reply(500);
    const e = await api.pause('g').catch((x) => x);
    expect(e.code).toBeUndefined();
    expect(e.fieldErrors).toEqual({});
  });
});
