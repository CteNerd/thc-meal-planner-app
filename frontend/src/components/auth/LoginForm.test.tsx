import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LoginForm } from './LoginForm';
import { AuthChallengeError } from '../../services/auth';

const mockNavigate = vi.fn();
const mockLogin = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useLocation: () => ({ state: { from: '/dashboard' } })
  };
});

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({
    authMode: 'placeholder',
    isLoading: false,
    login: mockLogin
  })
}));

describe('LoginForm', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockLogin.mockReset();
  });

  function renderForm() {
    render(
      <MemoryRouter>
        <LoginForm />
      </MemoryRouter>
    );
  }

  it('submits credentials and navigates on successful login', async () => {
    mockLogin.mockResolvedValue(undefined);
    renderForm();

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'adult1@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('adult1@example.com', 'password123456', undefined, false, undefined);
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard', { replace: true });
    });
  });

  it('handles TOTP challenge and validates code format', async () => {
    mockLogin.mockRejectedValueOnce(new AuthChallengeError('TOTP_REQUIRED', 'Code required'));
    renderForm();

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'adult1@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByText('Enter your 6-digit TOTP code to continue.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Verify code' }));
    expect(await screen.findByText('Enter a valid 6-digit TOTP code.')).toBeTruthy();
  });

  it('handles new-password and mfa-setup challenges', async () => {
    mockLogin
      .mockRejectedValueOnce(new AuthChallengeError('NEW_PASSWORD_REQUIRED', 'New password required'))
      .mockRejectedValueOnce(
        new AuthChallengeError('MFA_SETUP_REQUIRED', 'Setup required', { setupSecretCode: 'SECRET123', email: 'adult1@example.com' })
      );

    renderForm();

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'adult1@example.com' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'password123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByLabelText('New password')).toBeTruthy();

    fireEvent.change(screen.getByLabelText('New password'), { target: { value: 'my-secure-password-123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Set password and continue' }));

    expect(await screen.findByText('Set up your authenticator app first')).toBeTruthy();
    expect(screen.getByText('SECRET123')).toBeTruthy();
  });
});
