# Instruction: security-and-owasp

## Scope
Use for security-sensitive implementation and review.

## Rules
- Map features against OWASP Top 10 risks.
- Validate and sanitize all user-controlled input.
- Do not log secrets, tokens, passwords, or profile PII.
- Use secure defaults for CORS, CSP, and HTTP headers.
- Validate Cognito JWTs and enforce authorization boundaries.

## Data and Secrets
- Keep API keys in AWS Secrets Manager.
- Use least privilege IAM for all infrastructure roles.
