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
- Status: In Progress
- Contracts touched: Added recipe API surface (`GET/POST/PUT/DELETE /api/recipes`), favorites API (`GET /api/recipes/favorites`, `POST/DELETE /api/recipes/{id}/favorite`), DynamoDB dev table mappings for recipe/favorites documents, frontend cookbook browse/favorite UI and service contracts.
- Blockers or handoff requests: Local terminal ENOPRO blocks runtime command execution (`dotnet test`, `npm test`) in this session; require Mac Mini lane or recovered terminal provider for integrated test run and commit/push validation.

## Status Board

| Item | Description | Primary Owner | Status | Evidence / Notes |
|---|---|---|---|---|
| 3.1 | Recipe API endpoints (CRUD) | Codespaces | In Progress | Backend contracts, validators, endpoints, and tests added in workspace. |
| 3.2 | Recipe migration (6 existing recipes) | Mac Mini | Not Started | Requires record confirmation and migration execution in AWS-backed lane. |
| 3.3 | Cookbook browse UI | Codespaces | In Progress | Cookbook page now loads recipes, filters, and renders browse cards. |
| 3.4 | Recipe detail page | Codespaces | Not Started | Pending route and detail view implementation. |
| 3.5 | Add/edit recipe form | Codespaces | Not Started | Pending create/edit UI flow and client-side validation. |
| 3.6 | Image upload | Mac Mini | Not Started | Requires pre-signed URL API + S3/CloudFront runtime verification. |
| 3.7 | Favorites API + UI | Codespaces | In Progress | Favorite toggle/list endpoints + cookbook toggle behavior added. |
| 3.8 | Recipe import from URL | Codespaces | Not Started | Pending SSRF-safe fetch and OpenAI parse endpoint implementation. |

## Milestone Criteria Tracking

- [ ] 6 migrated recipes visible in cookbook
- [ ] New recipes can be created with images
- [ ] Favorites toggle works for both users
- [ ] URL import produces parseable recipe structure
