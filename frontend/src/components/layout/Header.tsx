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

  return (
    <header className="flex flex-wrap items-center justify-between gap-4 rounded-[28px] bg-slate-900 px-5 py-4 text-white shadow-lg">
      <div className="space-y-3">
        <p className="text-xs uppercase tracking-[0.24em] text-sky-200">THC Meal Planner</p>
        <h1 className="text-lg font-semibold">Foundation Scaffold</h1>
        <p className="text-xs text-slate-300">{user?.email ?? 'No active session'}</p>
        <nav className="flex flex-wrap gap-2" aria-label="Primary">
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
      <Button
        variant="secondary"
        onClick={async () => {
          await logout();
          navigate('/login');
        }}
      >
        Sign out
      </Button>
    </header>
  );
}
