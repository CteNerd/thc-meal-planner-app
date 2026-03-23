# Phase 3 Checklist and Backlog

Living tracker for cookbook and recipe delivery with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- Mac Mini lane: AWS/image/upload infrastructure verification, migration execution, deployed runtime checks.
- Codespaces lane: backend/frontend feature code, tests, and checklist updates.

## Check-In Protocol

Update this file after each subphase commit and at least daily.

Check-in template:

- Lane: Mac Mini or Codespaces
- Task: Milestone item number(s)
- Status: In Progress / Blocked / Done
- Contracts touched: API routes, data model keys/indexes, env vars, auth assumptions
- Blockers or handoff requests

## Check-In Log

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 3.1, 3.3, 3.7
- Status: Done
- Contracts touched: Added recipe API surface (`GET/POST/PUT/DELETE /api/recipes`), favorites API (`GET /api/recipes/favorites`, `POST/DELETE /api/recipes/{id}/favorite`), DynamoDB dev table mappings for recipe/favorites documents, frontend cookbook browse/favorite UI and service contracts.
- Blockers or handoff requests: None for Codespaces implementation. Runtime and AWS-backed verification remain Mac Mini lane work.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 3.4, 3.5, 3.6, 3.8
- Status: Done
- Contracts touched: Added recipe detail route (`/cookbook/:recipeId`), recipe editor routes (`/cookbook/new`, `/cookbook/:recipeId/edit`), URL import draft API (`POST /api/recipes/import-from-url`), pre-signed upload API (`POST /api/recipes/{id}/upload-url`), recipe source metadata (`sourceType`, `sourceUrl`, `thumbnailKey`), direct-to-S3 upload client flow, and backend/frontend tests for detail/editor/import/upload paths.
- Blockers or handoff requests: Mac Mini lane should validate deployed `RECIPE_IMAGES_BUCKET` wiring, Lambda IAM access to S3 `PutObject/GetObject`, CloudFront `/images/*` serving, and any runtime differences in import/upload behavior. Recipe migration remains blocked pending explicit confirmation of the 6 recipe records.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 3.8 hardening pass
- Status: Done
- Contracts touched: Strengthened recipe URL import parsing by adding JSON-LD schema extraction (Recipe type), improved fallback heading/list parsing, and added dedicated parser tests (`RecipeImportServiceTests`) covering structured JSON-LD, fallback HTML extraction, and localhost SSRF rejection.
- Blockers or handoff requests: None for the Codespaces code path. Remaining work is deployed/runtime verification plus recipe migration confirmation.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: Phase 3 closeout
- Status: Done
- Contracts touched: Finalized frontend recipe page tests, explicit Vitest/jest-dom matcher typing, and Phase 3 documentation alignment for lane handoff.
- Blockers or handoff requests: Codespaces scope is complete. Mac Mini lane still owns deployed image/upload validation, CloudFront image serving checks, real-world URL import smoke tests, and recipe migration after explicit record confirmation.

## Validation Status

- Codespaces local backend validation: `dotnet test` passed.
- Codespaces local frontend validation: `vitest` passed (`6 files, 25 tests`) after final syntax/test harness fixes.
- Codespaces commit status: latest Phase 3 frontend test fixes committed and pushed to `main`.
- Remaining runtime validation: GitHub Actions run, deployed upload URL behavior against real AWS resources, CloudFront image serving, post-deploy recipe import smoke test, and recipe migration after explicit record confirmation.

## Closeout Summary

- Codespaces lane: Complete for Phase 3 scope defined in [MILESTONES.md](MILESTONES.md).
- Mac Mini lane: Still required before Phase 3 can be considered fully complete end-to-end.
- User confirmation still required before any recipe migration payloads are written or executed.

## Status Board

| Item | Description | Primary Owner | Status | Evidence / Notes |
|---|---|---|---|---|
| 3.1 | Recipe API endpoints (CRUD) | Codespaces | Done | Backend CRUD routes, validators, service layer, and endpoint tests implemented locally. |
| 3.2 | Recipe migration (6 existing recipes) | Mac Mini | Not Started | Requires record confirmation and migration execution in AWS-backed lane. |
| 3.3 | Cookbook browse UI | Codespaces | Done | Browse grid, search, category filter, favorites-only toggle, and recipe navigation added. |
| 3.4 | Recipe detail page | Codespaces | Done | Detail route, ingredients checklist, nutrition panel, source metadata, and favorite toggle added. |
| 3.5 | Add/edit recipe form | Codespaces | Done | Create/edit routes, recipe editor form, import draft review, and direct image upload client flow added. |
| 3.6 | Image upload | Shared | In Progress | Codespaces implementation is done; Mac Mini must validate S3 bucket, IAM, and CloudFront `/images/*` runtime behavior before overall Phase 3 signoff. |
| 3.7 | Favorites API + UI | Codespaces | Done | Favorite toggle/list endpoints and browse/detail favorite UI covered by tests. |
| 3.8 | Recipe import from URL | Codespaces | Done | SSRF-protected URL fetch, JSON-LD + fallback parsing, and parser unit tests implemented; Mac Mini should still perform deployed real-world quality smoke tests. |

## Mac Mini Handoff

Run these after syncing the latest Codespaces commit:

1. Verify local and CI parity
	- `cd /workspaces/thc-meal-planner-app/backend && dotnet test`
	- `cd /workspaces/thc-meal-planner-app/frontend && npm ci && npm run test && npm run build`

2. Validate upload/runtime contract in AWS-backed environment
	- Confirm Lambda environment contains `RECIPE_IMAGES_BUCKET`
	- Confirm API runtime role has `s3:PutObject` and `s3:GetObject` on the recipe images bucket
	- Create a recipe in UI, upload `jpeg/png/webp`, confirm `imageKey` persists to recipes table
	- Confirm uploaded image is retrievable through CloudFront path `/images/recipes/{recipeId}/{file}`

3. Validate URL import behavior with live targets
	- Try at least one typical recipe page and one edge-case page with noisy markup
	- Confirm blocked URLs are rejected: `localhost`, private IPs, link-local addresses
	- Record any pages that parse poorly so the next pass can decide whether heuristic parsing is sufficient or OpenAI-backed parsing is required

4. Migration blocker remains explicit
	- Do not write or migrate the 6 recipe records until the user confirms the exact data payloads

## Milestone Criteria Tracking

- [ ] 6 migrated recipes visible in cookbook
- [~] New recipes can be created with images
- [x] Favorites toggle works for both users
- [x] URL import produces parseable recipe structure

`[~]` indicates implementation completed in Codespaces, with deployed runtime verification still pending in Mac Mini lane.
