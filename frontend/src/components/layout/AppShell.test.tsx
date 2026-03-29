import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

vi.mock('./Header', () => ({
  Header: () => <div>Header stub</div>
}));

describe('AppShell', () => {
  it('renders header and main content', () => {
    render(
      <AppShell>
        <div>Dashboard content</div>
      </AppShell>
    );

    expect(screen.getByText('Header stub')).toBeInTheDocument();
    expect(screen.getByText('Dashboard content')).toBeInTheDocument();
  });
});
