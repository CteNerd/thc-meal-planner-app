# Phase 7 Deployment Runbook (Dev/Prod)

## Purpose

Standardized deployment and rollback procedure for Phase 7 closeout.

## Preconditions

- CI workflow [ci.yml](../.github/workflows/ci.yml) passes (backend, frontend, infra, e2e smoke).
- Security workflows [codeql.yml](../.github/workflows/codeql.yml) and [secret-scan.yml](../.github/workflows/secret-scan.yml) are green.
- Migration payloads (if any) are explicitly user-confirmed before execution.

## Deployment Workflows

- Dev: [deploy-dev.yml](../.github/workflows/deploy-dev.yml)
- Prod: [deploy-prod.yml](../.github/workflows/deploy-prod.yml)

## Automated Smoke Validation

Use [validate-deployment.sh](../scripts/validate-deployment.sh):

```bash
./scripts/validate-deployment.sh https://dev-thc-mealplanner.tomlin.life dev
./scripts/validate-deployment.sh https://thc-mealplanner.tomlin.life prod
```

Current active domains:

- Dev: `https://dev-thc-mealplanner.tomlin.life`
- Prod: `https://thc-mealplanner.tomlin.life`
- CloudFront fallback domains remain valid for infrastructure troubleshooting and direct smoke checks.

Checks:

- `GET /` returns 200
- `GET /api/health` returns 200
- `GET /api/profile` returns 401 when unauthenticated
- `OPTIONS /api/health` returns 204

## Rollback

1. Re-deploy previous known-good commit with the same workflow.
2. Re-run automated smoke checks.
3. If frontend-only regression: sync previous frontend artifact to S3 and invalidate CloudFront.
4. If API/runtime regression: re-deploy previous API stack artifact and confirm `/api/health` and `/api/profile` behavior.

## Production Handoff Notes

Capture these after each run:

- Stack outputs (distribution domain, bucket, API URL, auth IDs).
- Custom domain outputs and any Route 53 / ACM changes.
- Workflow run URL and commit SHA.
- Smoke validation output.
- Any contract changes (env vars, routes, table/index names).
