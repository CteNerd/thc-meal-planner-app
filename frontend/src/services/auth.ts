import { z } from 'zod';

const loginRequestSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1),
  totpCode: z.string().regex(/^\d{6}$/),
  rememberDevice: z.boolean()
});

export type AuthUser = {
  sub: string;
  email: string;
  name: string;
};

export type AuthSession = {
  user: AuthUser;
  accessToken: string;
  idToken: string;
  expiresAt: number;
};

export type LoginRequest = z.infer<typeof loginRequestSchema>;

export type AuthService = {
  login: (request: LoginRequest) => Promise<AuthSession>;
  refreshSession: () => Promise<AuthSession | null>;
  logout: () => Promise<void>;
  getMode: () => 'placeholder' | 'cognito';
};

const SESSION_STORAGE_KEY = 'thc-meal-planner-auth-session';

class PlaceholderAuthService implements AuthService {
  public getMode() {
    return 'placeholder' as const;
  }

  public async login(request: LoginRequest): Promise<AuthSession> {
    loginRequestSchema.parse(request);

    const session: AuthSession = {
      user: {
        sub: 'local-user-1',
        email: request.email,
        name: request.email.split('@')[0] ?? 'Adult User'
      },
      accessToken: 'local-access-token',
      idToken: 'local-id-token',
      expiresAt: Date.now() + 60 * 60 * 1000
    };

    window.sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));

    return session;
  }

  public async refreshSession(): Promise<AuthSession | null> {
    const rawSession = window.sessionStorage.getItem(SESSION_STORAGE_KEY);

    if (!rawSession) {
      return null;
    }

    const session = JSON.parse(rawSession) as AuthSession;

    if (session.expiresAt <= Date.now()) {
      window.sessionStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }

    return session;
  }

  public async logout(): Promise<void> {
    window.sessionStorage.removeItem(SESSION_STORAGE_KEY);
  }
}

export const authService: AuthService = new PlaceholderAuthService();