# Phase 1 Checklist and Backlog

Living tracker for foundation work: scaffolding, CI/CD, infra baseline, and authentication.

## Status

| Item | Description | Status | Evidence |
|---|---|---|---|
| 1.1 | Initialize monorepo structure (`frontend`, `backend`, `infra`) | Done | Directories present in repository |
| 1.2 | Scaffold ASP.NET Core 9 API | Done | `backend/ThcMealPlanner.sln`, API project, `/api/health`, Lambda + AOT config |
| 1.3 | Scaffold React 19 + Vite + TailwindCSS | Done | `frontend/package.json`, Vite config, router shell, Tailwind styles, starter test |
| 1.4 | Create CDK project with 6 stacks | Done | `infra/package.json`, `bin/app.ts`, config files, shared constructs, 6 stack skeletons |
| 1.5 | Deploy AuthStack | Not Started | Pending AWS auth/infrastructure setup |
| 1.6 | Implement login flow with TOTP | In Progress | Backend JWT/auth + protected session route, auth tests (unauthorized/authenticated), frontend auth context/service, TOTP flow, protected routes, API auth header + 401 refresh handling |
| 1.7 | Add CI build/test/lint pipeline | In Progress | `.github/workflows/ci.yml` added for backend/frontend/infra jobs; runtime execution blocked in current environment |
| 1.8 | Deploy to dev | Not Started | Pending stack implementation and CI/CD |

## Notes

- Pipeline and cloud deployment tasks may be backlogged when environment permissions are limited.
- Keep each step small and test-backed; update this file as each item becomes verifiable.

## Environment-Blocked Backlog

Track items that require capabilities outside the current Codespaces session:

| Item | Why Blocked Here | Remediation Later |
|---|---|---|
| 0.9 | GitHub issue pickup and workflow execution require repository-side automation and validation outside this session | Run tagged test issues and workflow checks from a fully authorized environment |
| 1.5 | Cognito provisioning requires AWS credentials, account context, and verified deployment environment | Deploy `AuthStack` with AWS access and confirm pool/client outputs |
| 1.6 | End-to-end Cognito SRP + TOTP verification requires deployed User Pool, app client, and real token issuance | Replace placeholder frontend auth service with live Cognito integration and verify protected API calls |
| 1.7 | CI workflow execution and local build/test commands are blocked in this session by repository file-provider limitations (`ENOPRO`) and unavailable Actions runtime | Re-run backend/frontend/infra builds and workflow jobs from a fully functional local or GitHub-hosted environment |
| 1.8 | CDK deploy, S3 sync, and CloudFront invalidation require AWS credentials and runtime outputs | Deploy from a trusted local or cloud-enabled environment |

## Session Checkpoint (2026-03-22)

Completed in this session:

- Phase 1.2 backend scaffold, including Lambda-ready API project and `/api/health` endpoint.
- Phase 1.3 frontend scaffold, including routing, Tailwind, auth placeholders, and initial tests.
- Phase 1.4 CDK scaffold, including stack skeletons, constructs, and env config.
- Phase 1.6 hardening pass:
	- Backend JWT/Cognito auth pipeline scaffolded.
	- Protected `/api/session` endpoint added.
	- Test auth handler and auth-focused endpoint tests added.
	- Frontend auth context/service expanded for session and refresh semantics.
	- API client now injects bearer token and handles 401 refresh/retry path.
- Phase 1.7 scaffold started with `.github/workflows/ci.yml`.

Still blocked in this environment:

- Running terminal commands and VS Code tasks (`ENOPRO`), so build/test execution could not be validated here.
- GitHub Actions execution and AWS deployment actions.

Laptop pickup plan:

1. Run backend tests and build (`dotnet restore/build/test` on `backend/ThcMealPlanner.sln`).
2. Run frontend install/lint/build/test with coverage in `frontend/`.
3. Run infra install and `npx cdk diff --context env=dev` in `infra/`.
4. Trigger CI workflow and reconcile any runtime-only issues.
5. Finish 1.6 by swapping placeholder frontend auth service for live Cognito SRP/TOTP once AuthStack is deployed.
