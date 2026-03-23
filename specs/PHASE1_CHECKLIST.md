# Phase 1 Checklist and Backlog

Living tracker for foundation work: scaffolding, CI/CD, infra baseline, and authentication.

## Status

| Item | Description | Status | Evidence |
|---|---|---|---|
| 1.1 | Initialize monorepo structure (`frontend`, `backend`, `infra`) | Done | Directories present in repository |
| 1.2 | Scaffold ASP.NET Core 9 API | Done | `backend/ThcMealPlanner.sln`, API project, `/api/health`, Lambda + AOT config |
| 1.3 | Scaffold React 19 + Vite + TailwindCSS | Done | `frontend/package.json`, Vite config, router shell, Tailwind styles, starter test |
| 1.4 | Create CDK project with 6 stacks | Done | `infra/package.json`, `infra/bin/app.ts`, config files, shared constructs, 6 stack skeletons — `cdk synth` produces all 6 CF templates cleanly |
| 1.5 | Deploy AuthStack | Done | Deployed with profile `thc`; outputs: UserPoolId `us-east-1_OWufvWke8`, UserPoolClientId `1a0hgiq7vfdc7id09ogv188alg`, UserPoolDomain `thc-meal-planner-dev-auth` |
| 1.6 | Implement login flow with TOTP | Done | Backend JWT/auth + protected session route, auth tests (unauthorized/authenticated), frontend native Cognito user-pool auth service (SRP + TOTP + refresh, no Amplify), TOTP flow, protected routes, API auth header + 401 refresh handling. Cognito users provisioned for both family members. `rtomlin62@gmail.com` completed full first-login flow: temp password → new password → MFA setup (Google Authenticator) → TOTP enrollment verified. Ash login deferred until domain is configured. Deploy `0b095b4` live on CloudFront (`d3ugym4rb87yys.cloudfront.net`). |
| 1.7 | Add CI build/test/lint pipeline | Done | `.github/workflows/ci.yml` now passes in GitHub Actions on `main` (run `23411889262`); backend, frontend, and infra jobs all succeeded |
| 1.8 | Deploy to dev | Done | OIDC-based deploy workflow `.github/workflows/deploy-dev.yml` executed successfully on `main` (run `23412560524`): CDK deploy + frontend publish + CloudFront invalidation |

## Notes

- Pipeline and cloud deployment tasks may be backlogged when environment permissions are limited.
- Keep each step small and test-backed; update this file as each item becomes verifiable.

## Phase 1 Completion Notes (2026-03-22)

All Phase 1 items are complete. Key facts:

- Auth SDK: `amazon-cognito-identity-js` (no Amplify); SRP + TOTP + challenge chain fully implemented
- CDK stacks deployed to `us-east-1` via OIDC GitHub Actions (`deploy-dev.yml`)
- `rtomlin62@gmail.com` completed full first-login: temp password → new password → MFA setup → TOTP enrolled with Google Authenticator
- `ashuah.tomlin@gmail.com` provisioned; first-login deferred until custom domain is in place
- Frontend URL: `https://d3ugym4rb87yys.cloudfront.net`
- CI/CD: OIDC deploy role `thc-meal-planner-github-actions-deploy`; no static credentials in repo

**Phase 2 starting point:** DynamoDB DataStack, data access layer, user profile management, family-scoped auth.
