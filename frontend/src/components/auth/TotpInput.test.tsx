import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TotpInput } from './TotpInput';

describe('TotpInput', () => {
  it('renders current value and emits change updates', () => {
    const handleChange = vi.fn();

    render(<TotpInput value="123" onChange={handleChange} />);

    const input = screen.getByPlaceholderText('123456');
    expect(input).toHaveValue('123');

    fireEvent.change(input, { target: { value: '654321' } });

    expect(handleChange).toHaveBeenCalledWith('654321');
  });
});
