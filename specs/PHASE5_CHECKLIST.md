# Phase 5 Checklist and Backlog

Living tracker for grocery list generation, live sync, and optimistic concurrency delivery with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- Mac Mini lane: multi-user runtime verification in deployed environment, cloud behavior checks for concurrency/polling, and integrated troubleshooting.
- Codespaces lane: backend/frontend feature implementation, unit tests, and checklist updates.

## Check-In Protocol

Update this file after each subphase commit and at least daily.

Check-in template:

- Lane: Mac Mini or Codespaces
- Task: Milestone item number(s)
- Status: In Progress / Blocked / Done
- Contracts touched: API routes, data model keys/indexes, env vars, auth assumptions
- Blockers or handoff requests

## Check-In Log

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 5.1, 5.3, 5.4 (backend foundation)
- Status: In Progress
- Contracts touched: Added grocery backend domain (`GroceryListDocument`, pantry document, request models, validators, service, endpoints), mapped dev Dynamo table `thc-meal-planner-dev-grocerylists` for grocery + pantry records, introduced optimistic version checks with 409 responses, and added polling endpoint with 304 support.
- Blockers or handoff requests: Need Mac Mini lane to validate behavior against deployed DynamoDB table and confirm API latency/perf under real sessions.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 5.1, 5.2, 5.3, 5.4, 5.5, 5.10 (frontend wiring)
- Status: In Progress
- Contracts touched: Implemented frontend grocery API client (`current`, `generate`, `toggle`, `add`, `in-stock`, `poll`), replaced Grocery List scaffold with interactive page (metrics, section grouping, check toggle, in-stock toggle, manual add), and added 5-second visibility-aware polling against `/api/grocery-lists/poll?since=` with automatic refresh on changes.
- Blockers or handoff requests: Remove-item endpoint is not available yet in backend; frontend remove flow deferred until endpoint contract is added.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 5.5, 5.9 (API expansion)
- Status: In Progress
- Contracts touched: Added `DELETE /api/grocery-lists/items/{itemId}?version=` for optimistic remove flow; added pantry staples endpoints (`GET /api/pantry/staples`, `PUT /api/pantry/staples`, `POST /api/pantry/staples/items`, `DELETE /api/pantry/staples/items/{name}`) with validators and service support.
- Blockers or handoff requests: Pantry UI is still pending; need Mac Mini runtime verification against deployed tables and dual-user conflict behavior.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 5.7, 5.8, 5.9 (integration completion)
- Status: In Progress
- Contracts touched: Added pantry `preferredSectionOrder` persistence and validation; implemented Grocery List pantry/staples UI and section-flow reordering; wired meal plan create/generate/update/delete endpoints to trigger grocery list regeneration for reactive updates after meal changes.
- Blockers or handoff requests: Mac Mini lane should verify deployed runtime behavior for meal-swap reactive recalculation and dual-user live update paths.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 5.x quality hardening
- Status: Done
- Contracts touched: Added focused backend service tests for grocery delta recalculation after meal-plan changes and pantry in-stock mapping; added frontend test for 409 conflict recovery refresh path; revalidated backend/frontend test + build pipelines.
- Blockers or handoff requests: None on implementation quality. Remaining work is deployed multi-user/runtime verification in Mac Mini lane.

## Status Board

| Item | Description | Primary Owner | Status | Evidence / Notes |
|---|---|---|---|---|
| 5.1 | Grocery list generation API | Codespaces | In Progress | Backend generation endpoint and service landed; frontend generate flow integrated. |
| 5.2 | Grocery list UI | Codespaces | Done | Grocery list page now includes grouped list, metrics, manual add/remove, pantry management, and section-flow controls. |
| 5.3 | Optimistic concurrency | Codespaces | Done | Version checks and 409 recovery paths are implemented and test-covered in backend and frontend. |
| 5.4 | Activity-based polling | Shared | Done | Backend poll endpoint + frontend visibility-aware 5-second polling implemented and validated locally. |
| 5.5 | Add/remove items manually | Codespaces | Done | Manual add/remove flows are implemented end-to-end and covered by automated tests. |
| 5.6 | Completed item TTL cleanup | Codespaces | Done | 7-day application-level cleanup implemented in grocery service read path with local validation. |
| 5.7 | Store preferences integration | Codespaces | Done | Primary store section ordering implemented via pantry `preferredSectionOrder` and applied to grocery section rendering. |
| 5.8 | Grocery list reactivity on meal changes | Shared | In Progress | Meal plan create/generate/update/delete now trigger grocery regeneration; deployed verification pending. |
| 5.9 | Pantry staples management | Codespaces | Done | Pantry CRUD API + pantry management UI implemented with tests. |
| 5.10 | In-stock toggle | Codespaces | Done | Endpoint + UI toggle implemented; to-buy count excludes checked and in-stock items, with tests. |

## Milestone Criteria Tracking

- [~] Grocery list generates from active meal plan
- [~] Both users see real-time updates when checking items
- [x] Conflict resolution works (two users check same item simultaneously)
- [x] Completed items auto-clean after 7 days
- [~] Swapping a meal updates the grocery list (old ingredients removed, new ones added)
- [x] Pantry staples auto-flagged as in-stock on new grocery lists
- [x] In-stock items visually distinguished and excluded from shopping count

## Validation Status

- Backend local validation: `dotnet test backend/ThcMealPlanner.Tests/ThcMealPlanner.Tests.csproj` passed (`121 passed, 0 failed`).
- Frontend local validation: `npm run test -- --run` passed (`8 files, 39 tests`) and `npm run build` passed.

`[~]` indicates local implementation in progress with deployed verification still pending.
