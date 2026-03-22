# Security Specification

## Overview

Security posture designed for a small family application with strong defaults. Follows OWASP Top 10 guidelines and AWS Well-Architected security pillar. The attack surface is intentionally minimal: 2 users, no public registration, no third-party integrations beyond OpenAI.

---

## OWASP Top 10 Mitigations

### 1. Broken Access Control (A01)

| Threat | Mitigation |
|--------|------------|
| Unauthorized API access | All endpoints require valid JWT (except `/api/health`) |
| Cross-family data access | Service layer enforces `familyId` matching on every query |
| Direct object reference | Resource access validated against authenticated user's family |
| Missing function-level access | Authorization middleware applied globally in ASP.NET Core pipeline |

```csharp
// Every service method validates family ownership
public async Task<MealPlan?> GetPlanAsync(string userId, string weekDate)
{
    var user = await GetUserOrThrowAsync(userId);
    var plan = await _table.GetItemAsync(
        $"FAMILY#{user.FamilyId}", $"PLAN#{weekDate}");
    return plan; // PK already scoped to family
}
```

### 2. Cryptographic Failures (A02)

| Area | Implementation |
|------|---------------|
| Data in transit | HTTPS everywhere (CloudFront → viewer, CloudFront → API Gateway, API Gateway → Lambda) |
| Data at rest | DynamoDB default encryption (AWS-managed KMS), S3 SSE-S3 |
| Secrets | Secrets Manager for OpenAI API key (never in environment variables as plaintext) |
| Tokens | JWT signed by Cognito RSA keys, refresh token HttpOnly cookie |
| Password hashing | Cognito handles (SRP protocol — password never sent over wire) |

### 3. Injection (A03)

| Vector | Mitigation |
|--------|------------|
| SQL injection | Not applicable — DynamoDB (no SQL) |
| NoSQL injection | DynamoDB SDK uses parameterized expressions (attribute values are typed) |
| XSS (stored) | React auto-escapes output; DOMPurify for any user-generated markdown rendering |
| XSS (reflected) | CSP headers, no `dangerouslySetInnerHTML` without sanitization |
| Command injection | No shell execution in application code |
| Prompt injection | System prompt boundaries + output validation for AI chatbot |

### 4. Insecure Design (A04)

| Principle | Implementation |
|-----------|---------------|
| Least privilege | Lambda IAM role scoped to specific table ARNs and actions |
| Defense in depth | Cognito → API Gateway → Lambda auth → service-layer auth |
| Fail secure | Default deny; errors don't expose internal details |
| Secure defaults | All resources created with encryption, no public access |

### 5. Security Misconfiguration (A05)

| Area | Configuration |
|------|---------------|
| S3 buckets | Block all public access; OAC for CloudFront |
| API Gateway | Throttling enabled, request validation, no wildcard CORS |
| Lambda | Minimum IAM permissions, no `*` resource ARNs |
| CloudFront | HTTPS only, TLSv1.2 minimum, security headers |
| DynamoDB | No public endpoints (VPC not needed — IAM auth) |
| Error responses | RFC 9457 problem details; no stack traces in production |

### 6. Vulnerable Components (A06)

| Measure | Implementation |
|---------|---------------|
| Dependency scanning | Dependabot enabled on GitHub repo |
| .NET packages | `dotnet list package --vulnerable` in CI |
| npm packages | `npm audit` in CI |
| CDK packages | Locked to specific versions in `package-lock.json` |
| Runtime updates | Lambda managed runtime receives automatic patches |

### 7. Authentication Failures (A07)

| Threat | Mitigation |
|--------|------------|
| Brute force | Cognito built-in lockout after failed attempts |
| Credential stuffing | TOTP MFA required — password alone insufficient |
| Weak passwords | 12+ char policy with complexity requirements |
| Session hijacking | Refresh token in HttpOnly cookie, short-lived access tokens |
| Token replay | Token expiry + `exp` claim validation with zero clock skew |

### 8. Data Integrity Failures (A08)

| Area | Mitigation |
|------|------------|
| CI/CD pipeline | GitHub Actions with pinned action versions (`@sha256:...`) |
| Dependency integrity | `package-lock.json` and `packages.lock.json` committed |
| CDK deployments | `cdk diff` on PR, approval gate for prod |
| Data validation | FluentValidation on all API inputs server-side |

### 9. Logging & Monitoring Failures (A09)

| What | Where |
|------|-------|
| API requests | CloudWatch Logs (API Gateway access logs) |
| Lambda execution | CloudWatch Logs (structured JSON logging) |
| Auth events | Cognito advanced security event logging |
| Failed logins | Cognito → Lambda trigger → SES alert email |
| Error rates | CloudWatch alarm on Lambda error rate > 5% |
| Cost anomalies | AWS Budgets alert at $15/month |

### 10. SSRF (A10)

| Threat | Mitigation |
|--------|------------|
| URL import feature | Allowlist approach: only HTTP/HTTPS schemes, validate hostname against blocklist (localhost, 169.254.x.x, 10.x.x.x, etc.), DNS rebinding protection |
| OpenAI API calls | Fixed endpoint URL, not user-controlled |
| Pre-signed URLs | S3 bucket scoped, time-limited (15 min expiry) |

---

## Security Headers

Applied via CloudFront response headers policy:

```
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self' https://cognito-idp.us-east-1.amazonaws.com; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'
Strict-Transport-Security: max-age=63072000; includeSubDomains; preload
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

---

## CORS Configuration

Strict single-origin CORS on API Gateway:

```json
{
  "AllowOrigins": ["https://d1234567890.cloudfront.net"],
  "AllowMethods": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
  "AllowHeaders": ["Authorization", "Content-Type", "X-Request-Id"],
  "MaxAge": 3600,
  "AllowCredentials": true
}
```

No wildcard origins. Updated when custom domain is configured.

---

## Input Validation

### Server-Side (FluentValidation)

All API request bodies validated before processing:

```csharp
public class UpdateProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MacroTargets.Calories)
            .InclusiveBetween(800, 5000)
            .When(x => x.MacroTargets?.Calories != null);
        RuleFor(x => x.MacroTargets.Sodium)
            .InclusiveBetween(500, 5000)
            .When(x => x.MacroTargets?.Sodium != null);
        RuleForEach(x => x.Allergies).ChildRules(allergy =>
        {
            allergy.RuleFor(a => a.Allergen).NotEmpty().MaximumLength(50);
            allergy.RuleFor(a => a.Severity)
                .IsInEnum(); // mild, moderate, severe
        });
    }
}
```

### Client-Side

Client-side validation mirrors server-side for UX (immediate feedback) but is never trusted — server always re-validates.

---

## Pre-Signed URL Security

Recipe image uploads use time-limited, scoped pre-signed URLs:

```csharp
var presignedUrl = _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
{
    BucketName = _bucketName,
    Key = $"recipes/{recipeId}/{Guid.NewGuid()}.{extension}",
    Verb = HttpVerb.PUT,
    Expires = DateTime.UtcNow.AddMinutes(15),
    ContentType = contentType, // image/jpeg, image/png, image/webp only
});
```

- Expires in 15 minutes
- Scoped to specific key (no wildcard uploads)
- Content-Type restricted to image MIME types
- Max file size enforced via S3 bucket policy (5 MB limit)

---

## Security Alerts (SES)

Triggered events:
1. Failed login attempt (after MFA stage)
2. Password change
3. New device/IP detected (via Cognito advanced security)
4. Lambda error rate spike (via CloudWatch alarm → SNS → Lambda → SES)

Email template:
```
Subject: [THC Meal Planner] Security Alert
Body: A {eventType} was detected on your account at {timestamp}.
      IP: {sourceIp}
      If this was not you, please change your password immediately.
```

---

## Threat Model Summary

| Asset | Threat | Risk | Mitigation |
|-------|--------|------|------------|
| User credentials | Brute force | Low | Cognito lockout + TOTP MFA |
| Dietary data | Unauthorized read | Low | JWT + family-scoped queries |
| OpenAI API key | Exposure | Medium | Secrets Manager, never in code |
| Recipe images | Unauthorized upload | Low | Pre-signed URLs, scoped, time-limited |
| AI chatbot | Prompt injection | Medium | System prompt boundaries, output validation |
| Infrastructure | Misconfiguration | Low | CDK with typed constructs, PR review |
| CI/CD pipeline | Supply chain | Low | Pinned actions, Dependabot, secret scanning |
