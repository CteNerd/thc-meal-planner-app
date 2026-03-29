import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

vi.mock('./Header', () => ({
  Header: () => <div>Header stub</div>
}));

describe('AppShell', () => {
  it('renders header and main content', () => {
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AppShell>
          <div>Dashboard content</div>
        </AppShell>
      </MemoryRouter>
    );

    expect(screen.getByText('Header stub')).toBeInTheDocument();
    expect(screen.getByText('Dashboard content')).toBeInTheDocument();
  });
});
