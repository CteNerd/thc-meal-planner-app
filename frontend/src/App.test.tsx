import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import { AuthProvider } from './contexts/AuthContext';

describe('App', () => {
  it('renders login page by default when unauthenticated', () => {
    window.history.pushState({}, '', '/login');

    render(
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { name: /sign in to your planner/i })).toBeInTheDocument();
  });

  it('redirects protected routes to login when unauthenticated', () => {
    window.history.pushState({}, '', '/profile');

    render(
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { name: /sign in to your planner/i })).toBeInTheDocument();
  });
});
