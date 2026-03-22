import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Button } from '../ui/Button';

export function Header() {
  const navigate = useNavigate();
  const { logout, user } = useAuth();

  return (
    <header className="flex items-center justify-between gap-4 rounded-[28px] bg-slate-900 px-5 py-4 text-white shadow-lg">
      <div>
        <p className="text-xs uppercase tracking-[0.24em] text-sky-200">THC Meal Planner</p>
        <h1 className="text-lg font-semibold">Foundation Scaffold</h1>
        <p className="text-xs text-slate-300">{user?.email ?? 'No active session'}</p>
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
