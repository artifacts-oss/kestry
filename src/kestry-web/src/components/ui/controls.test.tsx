import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button, IconButton } from './Button';
import { Checkbox, Select, TextArea, TextField } from './fields';
import { Segmented } from './Segmented';
import { Switch } from './Switch';

describe('Button', () => {
  it('fires onClick and renders its icon', async () => {
    const onClick = vi.fn();
    render(<Button variant="primary" icon="plus" onClick={onClick}>Añadir</Button>);
    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('does not fire when disabled', async () => {
    const onClick = vi.fn();
    render(<Button disabled onClick={onClick}>Añadir</Button>);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
  });

  it('IconButton exposes its label as the accessible name', () => {
    render(<IconButton icon="pause" label="Pausar" />);
    expect(screen.getByRole('button', { name: 'Pausar' })).toBeInTheDocument();
  });
});

describe('fields', () => {
  it('TextField links label, hint and error to the input', () => {
    render(<TextField label="Carpeta" hint="Ruta absoluta" error="No existe" />);
    const input = screen.getByLabelText('Carpeta');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('No existe');
  });

  it('TextField describes itself with the hint when there is no error', () => {
    render(<TextField label="Carpeta" hint="Ruta absoluta" />);
    expect(screen.getByLabelText('Carpeta')).toHaveAccessibleDescription('Ruta absoluta');
  });

  it('TextArea is editable', async () => {
    render(<TextArea label="Enlaces" />);
    await userEvent.type(screen.getByLabelText('Enlaces'), 'hola');
    expect(screen.getByLabelText('Enlaces')).toHaveValue('hola');
  });

  it('Select accepts string and object options', async () => {
    render(<Select label="Unidad" options={['KB/s', { value: 'mb', label: 'MB/s' }]} />);
    await userEvent.selectOptions(screen.getByLabelText('Unidad'), 'MB/s');
    expect(screen.getByLabelText('Unidad')).toHaveValue('mb');
  });

  it('Checkbox toggles', async () => {
    render(<Checkbox label="Añadir en pausa" />);
    await userEvent.click(screen.getByLabelText('Añadir en pausa'));
    expect(screen.getByLabelText('Añadir en pausa')).toBeChecked();
  });
});

describe('Switch', () => {
  it('reports the new value on click', async () => {
    const onChange = vi.fn();
    render(<Switch checked={false} onChange={onChange} label="Apagar" />);
    await userEvent.click(screen.getByRole('switch', { name: 'Apagar' }));
    expect(onChange).toHaveBeenCalledWith(true);
  });

  it('exposes checked state and ignores clicks when disabled', async () => {
    const onChange = vi.fn();
    render(<Switch checked disabled onChange={onChange} label="Apagar" />);
    const sw = screen.getByRole('switch', { name: 'Apagar' });
    expect(sw).toBeChecked();
    await userEvent.click(sw);
    expect(onChange).not.toHaveBeenCalled();
  });
});

describe('Segmented', () => {
  it('selects an option', async () => {
    function Harness() {
      const [v, setV] = useState('system');
      return (
        <Segmented
          label="Tema"
          value={v}
          onChange={setV}
          options={[{ value: 'light', label: 'Claro' }, { value: 'dark', label: 'Oscuro' }, { value: 'system', label: 'Sistema' }]}
        />
      );
    }
    render(<Harness />);
    expect(screen.getByRole('radio', { name: 'Sistema' })).toBeChecked();
    await userEvent.click(screen.getByText('Oscuro'));
    expect(screen.getByRole('radio', { name: 'Oscuro' })).toBeChecked();
  });
});
