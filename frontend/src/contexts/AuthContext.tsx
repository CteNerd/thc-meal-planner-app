import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode
} from 'react';
import { configureApiClient } from '../services/api';
import { authService, type AuthSession, type AuthUser } from '../services/auth';

type AuthState = {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  accessToken: string | null;
  authMode: 'placeholder' | 'cognito';
  login: (email: string, password: string, totpCode: string, rememberDevice: boolean) => Promise<void>;
  logout: () => Promise<void>;
  refreshSession: () => Promise<void>;
};

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const refreshTimeoutRef = useRef<number | null>(null);

  useEffect(() => {
    void (async () => {
      const currentSession = await authService.refreshSession();
      setSession(currentSession);
      setIsLoading(false);
    })();

    return () => {
      if (refreshTimeoutRef.current !== null) {
        window.clearTimeout(refreshTimeoutRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (refreshTimeoutRef.current !== null) {
      window.clearTimeout(refreshTimeoutRef.current);
      refreshTimeoutRef.current = null;
    }

    if (!session) {
      return;
    }

    const millisecondsUntilRefresh = Math.max(session.expiresAt - Date.now() - 5 * 60 * 1000, 1_000);

    refreshTimeoutRef.current = window.setTimeout(() => {
      void authService.refreshSession().then(setSession);
    }, millisecondsUntilRefresh);
  }, [session]);

  useEffect(() => {
    configureApiClient({
      getAccessToken: () => session?.accessToken ?? null,
      refreshSession: async () => {
        const refreshedSession = await authService.refreshSession();
        setSession(refreshedSession);
      },
      onUnauthorized: async () => {
        await authService.logout();
        setSession(null);
      }
    });
  }, [session]);

  const value = useMemo<AuthState>(() => ({
    isAuthenticated: session !== null,
    isLoading,
    user: session?.user ?? null,
    accessToken: session?.accessToken ?? null,
    authMode: authService.getMode(),
    login: async (email, password, totpCode, rememberDevice) => {
      const nextSession = await authService.login({ email, password, totpCode, rememberDevice });
      setSession(nextSession);
    },
    logout: async () => {
      await authService.logout();
      setSession(null);
    },
    refreshSession: async () => {
      const refreshedSession = await authService.refreshSession();
      setSession(refreshedSession);
    }
  }), [isLoading, session]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }

  return context;
}
