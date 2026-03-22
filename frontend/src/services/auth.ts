import { z } from 'zod';
import {
  AuthenticationDetails,
  CognitoRefreshToken,
  CognitoUser,
  CognitoUserSession,
  CognitoUserPool,
  type ICognitoUserPoolData
} from 'amazon-cognito-identity-js';

const loginRequestSchema = z.object({
  email: z.string().email(),
  password: z.string().min(1),
  totpCode: z.string().regex(/^\d{6}$/).optional(),
  newPassword: z.string().min(12).optional(),
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

export class AuthChallengeError extends Error {
  public constructor(
    public readonly code: 'TOTP_REQUIRED' | 'NEW_PASSWORD_REQUIRED' | 'MFA_SETUP_REQUIRED',
    message: string,
    public readonly metadata?: Record<string, string>
  ) {
    super(message);
    this.name = 'AuthChallengeError';
  }
}

type PendingChallengeState = {
  cognitoUser: CognitoUser;
  email: string;
  challenge: 'MFA_SETUP_REQUIRED';
  setupSecretCode?: string;
};

let pendingChallengeState: PendingChallengeState | null = null;

const SESSION_STORAGE_KEY = 'thc-meal-planner-auth-session';
const STORAGE_PREFERENCE_KEY = 'thc-meal-planner-auth-storage';

const cognitoConfigSchema = z.object({
  region: z.string().min(1),
  userPoolId: z.string().min(1),
  clientId: z.string().min(1)
});

type CognitoConfig = z.infer<typeof cognitoConfigSchema>;

function parseJwtPayload(token: string): Record<string, unknown> {
  const payloadSegment = token.split('.')[1];

  if (!payloadSegment) {
    throw new Error('Invalid JWT token payload');
  }

  const normalized = payloadSegment.replace(/-/g, '+').replace(/_/g, '/');
  const padding = '='.repeat((4 - (normalized.length % 4)) % 4);
  const decoded = window.atob(normalized + padding);

  return JSON.parse(decoded) as Record<string, unknown>;
}

function normalizeName(email: string, nameClaim: unknown) {
  if (typeof nameClaim === 'string' && nameClaim.length > 0) {
    return nameClaim;
  }

  return email.split('@')[0] ?? 'Adult User';
}

function toAuthSession(idToken: string, accessToken: string): AuthSession {
  const idPayload = parseJwtPayload(idToken);
  const accessPayload = parseJwtPayload(accessToken);

  const sub = typeof idPayload.sub === 'string' ? idPayload.sub : 'unknown-user';
  const email = typeof idPayload.email === 'string' ? idPayload.email : 'unknown@example.com';
  const exp = typeof accessPayload.exp === 'number' ? accessPayload.exp : Math.floor(Date.now() / 1000) + 3600;

  return {
    user: {
      sub,
      email,
      name: normalizeName(email, idPayload.name)
    },
    accessToken,
    idToken,
    expiresAt: exp * 1000
  };
}

function readCognitoConfig(): CognitoConfig | null {
  const parsed = cognitoConfigSchema.safeParse({
    region: import.meta.env.VITE_COGNITO_REGION,
    userPoolId: import.meta.env.VITE_COGNITO_USER_POOL_ID,
    clientId: import.meta.env.VITE_COGNITO_CLIENT_ID
  });

  if (!parsed.success) {
    return null;
  }

  return parsed.data;
}

function getPreferredStorage() {
  return window.localStorage.getItem(STORAGE_PREFERENCE_KEY) === 'local'
    ? window.localStorage
    : window.sessionStorage;
}

class CognitoAuthService implements AuthService {
  private readonly poolData: ICognitoUserPoolData;

  public constructor(config: CognitoConfig) {
    this.poolData = {
      UserPoolId: config.userPoolId,
      ClientId: config.clientId
    };
  }

  public getMode() {
    return 'cognito' as const;
  }

  public async login(request: LoginRequest): Promise<AuthSession> {
    loginRequestSchema.parse(request);

    if (pendingChallengeState?.challenge === 'MFA_SETUP_REQUIRED') {
      if (!request.totpCode) {
        throw new AuthChallengeError(
          'MFA_SETUP_REQUIRED',
          'Set up your authenticator app and enter a 6-digit code to continue.',
          {
            setupSecretCode: pendingChallengeState.setupSecretCode ?? '',
            email: pendingChallengeState.email
          }
        );
      }

      const totpCode = request.totpCode;

      return await new Promise<AuthSession>((resolve, reject) => {
        pendingChallengeState?.cognitoUser.verifySoftwareToken(
          totpCode,
          'thc-meal-planner-device',
          {
            onSuccess: () => {
              pendingChallengeState?.cognitoUser.sendMFACode(
                totpCode,
                {
                  onSuccess: (session) => {
                    pendingChallengeState = null;
                    resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
                  },
                  onFailure: (error) => {
                    reject(error);
                  }
                },
                'SOFTWARE_TOKEN_MFA'
              );
            },
            onFailure: (error) => {
              reject(error);
            }
          }
        );
      });
    }

    window.localStorage.setItem(STORAGE_PREFERENCE_KEY, request.rememberDevice ? 'local' : 'session');
    const storage = getPreferredStorage();
    const userPool = new CognitoUserPool({ ...this.poolData, Storage: storage });
    const cognitoUser = new CognitoUser({
      Username: request.email,
      Pool: userPool,
      Storage: storage
    });
    const authDetails = new AuthenticationDetails({
      Username: request.email,
      Password: request.password
    });

    return await new Promise<AuthSession>((resolve, reject) => {
      cognitoUser.authenticateUser(authDetails, {
        onSuccess: (session) => {
          resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
        },
        onFailure: (error) => {
          reject(error);
        },
        mfaRequired: () => {
          if (!request.totpCode) {
            reject(new AuthChallengeError('TOTP_REQUIRED', 'A TOTP code is required to complete sign-in.'));
            return;
          }

          cognitoUser.sendMFACode(request.totpCode, {
            onSuccess: (session) => {
              resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
            },
            onFailure: (error) => {
              reject(error);
            }
          }, 'SOFTWARE_TOKEN_MFA');
        },
        totpRequired: () => {
          if (!request.totpCode) {
            reject(new AuthChallengeError('TOTP_REQUIRED', 'A TOTP code is required to complete sign-in.'));
            return;
          }

          cognitoUser.sendMFACode(request.totpCode, {
            onSuccess: (session) => {
              resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
            },
            onFailure: (error) => {
              reject(error);
            }
          }, 'SOFTWARE_TOKEN_MFA');
        },
        mfaSetup: () => {
          cognitoUser.associateSoftwareToken({
            associateSecretCode: (secretCode) => {
              pendingChallengeState = {
                cognitoUser,
                email: request.email,
                challenge: 'MFA_SETUP_REQUIRED',
                setupSecretCode: secretCode
              };

              reject(new AuthChallengeError(
                'MFA_SETUP_REQUIRED',
                'MFA setup is required before first sign-in can complete.',
                {
                  setupSecretCode: secretCode,
                  email: request.email
                }
              ));
            },
            onFailure: (error) => {
              reject(error);
            }
          });
        },
        newPasswordRequired: () => {
          if (!request.newPassword) {
            reject(new AuthChallengeError('NEW_PASSWORD_REQUIRED', 'A new password is required before sign-in can continue.'));
            return;
          }

          cognitoUser.completeNewPasswordChallenge(request.newPassword, {}, {
            onSuccess: (session) => {
              resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
            },
            onFailure: (error) => {
              reject(error);
            },
            mfaRequired: () => {
              if (!request.totpCode) {
                reject(new AuthChallengeError('TOTP_REQUIRED', 'A TOTP code is required to complete sign-in.'));
                return;
              }

              cognitoUser.sendMFACode(request.totpCode, {
                onSuccess: (session) => {
                  resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
                },
                onFailure: (error) => {
                  reject(error);
                }
              }, 'SOFTWARE_TOKEN_MFA');
            },
            mfaSetup: () => {
              cognitoUser.associateSoftwareToken({
                associateSecretCode: (secretCode) => {
                  pendingChallengeState = {
                    cognitoUser,
                    email: request.email,
                    challenge: 'MFA_SETUP_REQUIRED',
                    setupSecretCode: secretCode
                  };

                  reject(new AuthChallengeError(
                    'MFA_SETUP_REQUIRED',
                    'MFA setup is required before first sign-in can complete.',
                    {
                      setupSecretCode: secretCode,
                      email: request.email
                    }
                  ));
                },
                onFailure: (error) => {
                  reject(error);
                }
              });
            }
          });
        }
      });
    });
  }

  public async refreshSession(): Promise<AuthSession | null> {
    const storage = getPreferredStorage();
    const userPool = new CognitoUserPool({ ...this.poolData, Storage: storage });
    const cognitoUser = userPool.getCurrentUser();

    if (!cognitoUser) {
      return null;
    }

    return await new Promise<AuthSession | null>((resolve) => {
      cognitoUser.getSession((sessionError: Error | null, session: CognitoUserSession | null) => {
        if (sessionError || !session) {
          resolve(null);
          return;
        }

        if (session.isValid()) {
          resolve(toAuthSession(session.getIdToken().getJwtToken(), session.getAccessToken().getJwtToken()));
          return;
        }

        const refreshTokenValue = session.getRefreshToken().getToken();

        if (!refreshTokenValue) {
          resolve(null);
          return;
        }

        cognitoUser.refreshSession(new CognitoRefreshToken({ RefreshToken: refreshTokenValue }), (refreshError, refreshedSession) => {
          if (refreshError || !refreshedSession) {
            resolve(null);
            return;
          }

          resolve(toAuthSession(refreshedSession.getIdToken().getJwtToken(), refreshedSession.getAccessToken().getJwtToken()));
        });
      });
    });
  }

  public async logout(): Promise<void> {
    const storage = getPreferredStorage();
    const userPool = new CognitoUserPool({ ...this.poolData, Storage: storage });
    const cognitoUser = userPool.getCurrentUser();

    cognitoUser?.signOut();
    storage.removeItem(SESSION_STORAGE_KEY);
  }
}

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

const cognitoConfig = readCognitoConfig();

export const authService: AuthService = cognitoConfig
  ? new CognitoAuthService(cognitoConfig)
  : new PlaceholderAuthService();