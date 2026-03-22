import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { isTotpCode } from '../../utils/validators';
import { Button } from '../ui/Button';
import { Card } from '../ui/Card';
import { Input } from '../ui/Input';
import { TotpInput } from './TotpInput';

export function LoginForm() {
  const navigate = useNavigate();
  const location = useLocation();
  const { authMode, isLoading, login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [requiresTotp, setRequiresTotp] = useState(false);
  const [totpCode, setTotpCode] = useState('');
  const [rememberDevice, setRememberDevice] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const targetPath = (location.state as { from?: string } | null)?.from ?? '/dashboard';

  return (
    <Card className="w-full max-w-md p-8">
      <div className="space-y-6">
        <div className="space-y-2">
          <p className="text-xs uppercase tracking-[0.24em] text-slate-500">Phase 1.3</p>
          <h1 className="text-3xl font-bold text-slate-900">Sign in to your planner</h1>
          <p className="text-sm text-slate-600">
            Sign in with Cognito user pool credentials and your 6-digit TOTP code.
          </p>
          <p className="text-xs text-slate-500">Auth mode: {authMode}</p>
        </div>

        <form
          className="space-y-4"
          onSubmit={async (event) => {
            event.preventDefault();
            setError(null);

            if (!requiresTotp) {
              setRequiresTotp(true);
              return;
            }

            if (!isTotpCode(totpCode)) {
              setError('Enter a valid 6-digit TOTP code.');
              return;
            }

            try {
              await login(email, password, totpCode, rememberDevice);
              navigate(targetPath, { replace: true });
            } catch {
              setError('Unable to complete sign-in. Verify credentials and TOTP code.');
            }
          }}
        >
          <label className="block space-y-2">
            <span className="text-sm font-medium text-slate-700">Email</span>
            <Input
              type="email"
              placeholder="adult1@example.com"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </label>

          <label className="block space-y-2">
            <span className="text-sm font-medium text-slate-700">Password</span>
            <Input
              type="password"
              placeholder="Enter password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>

          {requiresTotp ? <TotpInput value={totpCode} onChange={setTotpCode} /> : null}

          <label className="flex items-center gap-3 text-sm text-slate-600">
            <input
              className="h-4 w-4 rounded border-slate-300"
              type="checkbox"
              checked={rememberDevice}
              onChange={(event) => setRememberDevice(event.target.checked)}
            />
            Remember this device
          </label>

          {error ? <p className="text-sm text-red-500">{error}</p> : null}

          <Button className="w-full" type="submit" disabled={isLoading}>
            {requiresTotp ? 'Verify code' : 'Continue'}
          </Button>
        </form>
      </div>
    </Card>
  );
}
