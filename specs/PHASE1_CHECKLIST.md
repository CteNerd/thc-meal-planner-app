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
| 1.6 | Implement login flow with TOTP | In Progress | Backend JWT/auth + protected session route, auth tests (unauthorized/authenticated), frontend native Cognito user-pool auth service (SRP + TOTP + refresh, no Amplify), TOTP flow, protected routes, API auth header + 401 refresh handling. Cognito users provisioned for `rtomlin62@gmail.com` and `ashuah.tomlin@gmail.com` (both `FORCE_CHANGE_PASSWORD`); pending first login + TOTP enrollment verification. |
| 1.7 | Add CI build/test/lint pipeline | Done | `.github/workflows/ci.yml` now passes in GitHub Actions on `main` (run `23411889262`); backend, frontend, and infra jobs all succeeded |
| 1.8 | Deploy to dev | Done | OIDC-based deploy workflow `.github/workflows/deploy-dev.yml` executed successfully on `main` (run `23412560524`): CDK deploy + frontend publish + CloudFront invalidation |

## Notes

- Pipeline and cloud deployment tasks may be backlogged when environment permissions are limited.
- Keep each step small and test-backed; update this file as each item becomes verifiable.

## Environment-Blocked Backlog

Track items that require capabilities outside the current Codespaces session:

| Item | Why Blocked Here | Remediation Later |
|---|---|---|
| 0.9 | GitHub issue pickup and workflow execution require repository-side automation and validation outside this session | Run tagged test issues and workflow checks from a fully authorized environment |
| 1.6 | End-to-end Cognito SRP + TOTP verification requires deployed User Pool, app client, and real token issuance | Replace placeholder frontend auth service with live Cognito integration and verify protected API calls |

## Session Checkpoint (2026-03-22, Laptop)

Resolved all Codespaces-blocked backlog items:

- Installed `aws-cdk` CLI (v2.1112.0) globally via npm; was the only missing tool.
- AWS CLI v2.28.16, GitHub CLI v2.78.0, dotnet 9.0.305, Node 24.6 all present and authenticated.
- Created missing `infra/bin/app.ts` CDK entry point (Codespaces had not committed it).
- All 6 CDK stacks synthesize cleanly (`cdk synth --context env=dev` produces CloudFormation templates).
- Backend: 3/3 tests passing (added `GlobalUsings.cs` to fix missing Xunit/DI using directives).
- Frontend: 2/2 tests passing; fixed lint error (`vite-env.d.ts` not in tsconfig), fixed `vite.config.ts` to import `defineConfig` from `vitest/config`, fixed test to use `findByRole` for async auth state.
- CI workflow updated: `infra` job now publishes Lambda binary before synthesis, and now uses `cdk synth` in GitHub Actions instead of AWS-backed `cdk diff`.
- Generated `frontend/package-lock.json` and `infra/package-lock.json` (ready to commit).
- Created IAM deploy user/profile `thc`, bootstrapped CDK, and deployed `ThcMealPlanner-dev-Auth` successfully.

**Next steps:**

1. Replace placeholder frontend auth service with live Cognito SRP/TOTP using the deployed User Pool and client.
2. Deploy remaining stacks for the dev environment (data, secrets, api, frontend, notifications).
3. Validate Phase 0.9 domain and engineering agents against real prompts.

## AWS Profile Status

Deployment now uses the dedicated IAM profile `thc` instead of the root account:

1. In AWS Console → IAM → Create user `thc-meal-planner-deployer`
2. Attach `AdministratorAccess` policy (or a scoped CDK deploy policy)
3. Generate access keys → run `aws configure --profile thc`
4. Use `--profile thc` on CDK commands
5. Consider using AWS SSO/IAM Identity Center for longer-term credential management
