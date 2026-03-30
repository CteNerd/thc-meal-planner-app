import { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Button } from '../ui/Button';

const navItems = [
  { label: 'Dashboard', to: '/dashboard' },
  { label: 'Meal Plans', to: '/meal-plans' },
  { label: 'Cookbook', to: '/cookbook' },
  { label: 'Grocery', to: '/grocery-list' },
  { label: 'Chat', to: '/chat' },
  { label: 'Profile', to: '/profile' }
];

export function Header() {
  const navigate = useNavigate();
  const { logout, user } = useAuth();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  async function handleSignOut() {
    await logout();
    navigate('/login');
  }

  return (
    <header className="relative flex flex-wrap items-center justify-between gap-4 rounded-[28px] bg-slate-900 px-5 py-4 text-white shadow-lg">
      <div className="space-y-3">
        <p className="text-xs uppercase tracking-[0.24em] text-sky-200">THC Meal Planner</p>
        <h1 className="text-lg font-semibold">Foundation Scaffold</h1>
        <p className="text-xs text-slate-300">{user?.email ?? 'No active session'}</p>
        <nav className="hidden flex-wrap gap-2 md:flex" aria-label="Primary">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `rounded-full px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.12em] transition ${isActive
                  ? 'bg-yellow-300 text-slate-900'
                  : 'bg-slate-800 text-slate-200 hover:bg-slate-700'}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </div>

      <div className="flex items-center gap-2">
        <Button
          type="button"
          variant="secondary"
          className="md:hidden"
          aria-expanded={isMobileMenuOpen}
          aria-controls="mobile-primary-nav"
          onClick={() => setIsMobileMenuOpen((open) => !open)}
        >
          Menu
        </Button>
        <Button
          variant="secondary"
          className="hidden md:inline-flex"
          onClick={handleSignOut}
        >
          Sign out
        </Button>
      </div>

      {isMobileMenuOpen ? (
        <div
          id="mobile-primary-nav"
          className="absolute left-4 right-4 top-[calc(100%+0.5rem)] z-20 rounded-2xl border border-slate-700 bg-slate-950/95 p-3 shadow-2xl md:hidden"
        >
          <nav className="grid gap-2" aria-label="Primary mobile">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={() => setIsMobileMenuOpen(false)}
                className={({ isActive }) =>
                  `rounded-xl px-3 py-2 text-sm font-semibold transition ${isActive
                    ? 'bg-yellow-300 text-slate-900'
                    : 'bg-slate-800 text-slate-200 hover:bg-slate-700'}`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
          <Button
            type="button"
            variant="secondary"
            className="mt-3 w-full"
            onClick={handleSignOut}
          >
            Sign out
          </Button>
        </div>
      ) : null}
    </header>
  );
}
