import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const root = resolve(__dirname, '../../../..');
const css = readFileSync(resolve(__dirname, 'tokens.css'), 'utf8');
const tokens: { color: { tokens: { name: string }[] } } = JSON.parse(
  readFileSync(resolve(root, 'design/kestrel/tokens.json'), 'utf8'),
);

const block = (selector: string) => css.split(selector)[1].split('}')[0];

describe('tokens.css', () => {
  it.each([':root {', '.dark {'])('defines every Kestrel color token in %s', (selector) => {
    const body = block(selector);
    for (const { name } of tokens.color.tokens) expect(body).toContain(`--${name}:`);
  });

  it('exposes every color token to Tailwind', () => {
    for (const { name } of tokens.color.tokens) expect(css).toContain(`--color-${name}: var(--${name});`);
  });
});
