# Authentication Specification

## Overview

Authentication uses Amazon Cognito User Pool with TOTP-based two-factor authentication. There are only 2 users (Adult 1 and Adult 2), so the design is intentionally simple — no self-service registration, no federated identity providers, no ASP.NET Identity.

---

## Cognito User Pool Configuration

| Setting | Value |
|---------|-------|
| Pool Name | `thc-meal-planner-{env}-user-pool` |
| Sign-in Aliases | Email only |
| MFA | Required (TOTP only — no SMS costs) |
| Password Policy | 12+ chars, uppercase, lowercase, number, symbol |
| Account Recovery | Email only |
| Email Provider | Cognito default (sufficient for 2 users) |
| User Pool Client | `thc-meal-planner-{env}-web-client` |
| Client Auth Flows | `ALLOW_USER_SRP_AUTH`, `ALLOW_REFRESH_TOKEN_AUTH` |
| Token Validity | Access: 1 hour, ID: 1 hour, Refresh: 30 days |
| Advanced Security | Enabled (compromised credential detection) |

---

## Authentication Flow

### Initial Login (SRP)

```
Client                    Cognito                  Backend
  |                         |                        |
  |-- InitiateAuth(SRP) --->|                        |
  |<-- Challenge: MFA ------|                        |
  |-- RespondToChallenge -->|                        |
  |   (TOTP code)          |                        |
  |<-- Tokens (id,access,refresh)                    |
  |                         |                        |
  |-- API Request + Bearer Token ------------------>|
  |                         |   Validate JWT (kid,   |
  |                         |   iss, aud, exp, sub)  |
  |<-- 200 Response ---------------------------------|
```

### Token Refresh

```
Client                    Cognito
  |                         |
  |-- InitiateAuth -------->|
  |   (REFRESH_TOKEN_AUTH)  |
  |<-- New access + id -----|
```

### TOTP Setup (One-Time)

1. Admin creates user in Cognito (via CDK custom resource or CLI)
2. User receives temporary password via email
3. User logs in → forced password change
4. User associates TOTP: `AssociateSoftwareToken` → gets secret key
5. User scans QR code with authenticator app (Google Authenticator, Authy, etc.)
6. User verifies with TOTP code: `VerifySoftwareToken`
7. MFA is now required on all subsequent logins

---

## JWT Validation (Backend)

The ASP.NET Core backend validates JWTs from Cognito without calling Cognito on every request.

### Configuration

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
    });
```

### Validation Steps

1. **Decode** JWT header, extract `kid`
2. **Fetch** JWKS from `https://cognito-idp.{region}.amazonaws.com/{poolId}/.well-known/jwks.json` (cached)
3. **Verify** signature using RSA public key matching `kid`
4. **Validate** claims:
   - `iss` = Cognito User Pool URL
   - `aud` (for ID token) or `client_id` (for access token) = App Client ID
   - `exp` > current time
   - `token_use` = `id` or `access` as expected
5. **Extract** `sub` claim as the user identifier

### Authorization

All API endpoints require authentication except:
- `GET /api/health` — public health check

Family-scoped authorization (ensuring users only access their own family's data) is enforced at the service layer:

```
1. Extract userId (sub) from JWT
2. Look up user's familyId from Users table
3. Verify requested resource belongs to same familyId
4. Return 403 if mismatch
```

---

## Token Storage (Frontend)

| Token | Storage | Reason |
|-------|---------|--------|
| Access Token | In-memory (React state) | Short-lived, never persisted to disk |
| ID Token | In-memory (React state) | Contains user claims |
| Refresh Token | HttpOnly cookie | Survives page refresh, not accessible to JS |

### Token Lifecycle

```typescript
// AuthContext provides:
interface AuthState {
  isAuthenticated: boolean;
  user: { sub: string; email: string; name: string } | null;
  accessToken: string | null;  // in-memory only
  login: (email: string, password: string, totpCode: string) => Promise<void>;
  logout: () => void;
  refreshSession: () => Promise<void>;
}
```

- On page load → attempt silent refresh using refresh token cookie
- 5 minutes before access token expiry → auto-refresh
- On 401 from API → attempt refresh → if fails, redirect to login
- On logout → revoke refresh token + clear all state

---

## Protected Routes (Frontend)

```typescript
// Route protection
<Route element={<RequireAuth />}>
  <Route path="/dashboard" element={<Dashboard />} />
  <Route path="/meal-plans/*" element={<MealPlans />} />
  <Route path="/cookbook/*" element={<Cookbook />} />
  <Route path="/grocery-list" element={<GroceryList />} />
  <Route path="/chat" element={<Chat />} />
  <Route path="/profile" element={<Profile />} />
</Route>

// Public routes
<Route path="/login" element={<Login />} />
```

---

## User Provisioning

Since there are only 2 users, provisioning is done via CDK custom resource or AWS CLI during initial deployment:

```bash
# Create user (done once during initial setup)
# Use real email addresses from .local/profiles/
aws cognito-idp admin-create-user \
  --user-pool-id $POOL_ID \
  --username <adult1-email> \
  --temporary-password "TempPass123!" \
  --user-attributes Name=email,Value=<adult1-email> Name=name,Value="<Adult 1 Name>"

aws cognito-idp admin-create-user \
  --user-pool-id $POOL_ID \
  --username <adult2-email> \
  --temporary-password "TempPass123!" \
  --user-attributes Name=email,Value=<adult2-email> Name=name,Value="<Adult 2 Name>"
```

After creation, each user:
1. Logs in with temporary password
2. Sets permanent password
3. Configures TOTP device
4. Profile data is seeded into DynamoDB Users table by migration script

---

## Security Alerts

On failed login attempt or new device detection, Cognito triggers a Lambda function that sends an alert email via SES to the account owner. See `SECURITY.md` for details.

---

## Session Management

| Scenario | Behavior |
|----------|----------|
| Idle timeout | None (tokens handle expiry) |
| Access token expired | Auto-refresh via refresh token |
| Refresh token expired | Redirect to login |
| Concurrent sessions | Allowed (both users can be logged in simultaneously) |
| Explicit logout | Revoke refresh token, clear in-memory tokens |
| Password change | All refresh tokens invalidated (Cognito global sign-out) |
