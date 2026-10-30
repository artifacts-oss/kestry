import { render } from '@testing-library/react';
import { Icon, iconNames } from './Icon';

describe('Icon', () => {
  it('ports the 23 Kestrel icons plus upload, magnet and globe', () => {
    expect(iconNames).toHaveLength(26);
    expect(iconNames).toEqual(expect.arrayContaining(['upload', 'magnet', 'globe', 'gauge', 'chevronDown']));
  });

  it('is decorative without a label', () => {
    const { container } = render(<Icon name="download" />);
    const svg = container.querySelector('svg')!;
    expect(svg).toHaveAttribute('aria-hidden', 'true');
    expect(svg).not.toHaveAttribute('role');
  });

  it('is exposed as an image with a label', () => {
    const { getByRole } = render(<Icon name="download" label="Descargas" />);
    expect(getByRole('img', { name: 'Descargas' })).toBeInTheDocument();
  });
});
