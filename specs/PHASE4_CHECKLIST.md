# Phase 4 Checklist and Backlog

Living tracker for meal planning and constraints delivery with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- Mac Mini lane: OpenAI/runtime verification, deployed environment checks, authenticated smoke tests.
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

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 4.1, 4.2, 4.5, 4.7
- Status: Done
- Contracts touched: Added `MealPlans` backend domain (`MealPlanDocument`, request models, validators, constraint engine, service, endpoints), mapped dev Dynamo table `MealPlanDocument`, registered DI/services/validators in API startup, implemented plan TTL computation for 90-day history retention (+7 day buffer), and added backend tests for constraint engine, service behavior, and meal-plan endpoints.
- Blockers or handoff requests: OpenAI-backed generation verification is still a Mac Mini lane runtime check; current generation path is deterministic and constraint-validated.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 4.3, 4.4, 4.6
- Status: Done
- Contracts touched: Added frontend meal plan types, dedicated meal plan API client service, and implemented weekly grid UI in Meal Plans page with generate action, per-slot swap action, history view, quality score display, nutrition summary tiles, and backend-driven swap suggestions endpoint.
- Blockers or handoff requests: Authenticated end-to-end smoke tests in deployed environment are pending Mac Mini lane confirmation.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 4.4 test hardening
- Status: Done
- Contracts touched: Added `MealPlansPage` frontend tests for load, generate, swap suggestions, swap update, history tab, and no-active-plan fallback state.
- Blockers or handoff requests: None for local implementation; deployed smoke checks remain in Mac Mini lane.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 4.2 OpenAI integration
- Status: Done
- Contracts touched: Added backend OpenAI integration service with Secrets Manager key loading (`OPENAI_SECRET_ARN`), wired AI-assisted meal generation and swap ranking into `MealPlanService`, and preserved deterministic fallback when AI is unavailable or responses are invalid.
- Blockers or handoff requests: Mac Mini lane should validate deployed runtime behavior with live OpenAI responses and confirm expected quality/constraint compliance.

## Validation Status

- Backend local validation: `dotnet build` passed and `dotnet test` passed (`110 passed, 0 failed`).
- Frontend local validation: `npm run test -- --run` passed (`7 files, 30 tests`) and `npm run build` passed.
- Automated deployed validation (complete):
	- Dev `/api/health` returns `200` via API Gateway and CloudFront.
	- Unauthenticated protected routes return `401` (auth gate enforced in deployed runtime).
	- CORS preflight `OPTIONS` behavior is verified for core API paths in deployed runtime.
- Manual validation (pending):
	- Authenticated meal generation against live OpenAI configuration.
	- Authenticated swap/history round-trip in deployed UI with persistence verification.
	- Constraint-quality checks with real profile + recipe data (allergen/prohibited ingredient confirmations).

## Status Board

| Item | Description | Primary Owner | Status | Evidence / Notes |
|---|---|---|---|---|
| 4.1 | ConstraintEngine service | Codespaces | Done | Constraint config + validation/scoring engine implemented and test-covered. |
| 4.2 | Meal plan generation API | Shared | Done | OpenAI-backed generation integration added with deterministic fallback and Secrets Manager key retrieval; deployed runtime verification remains Mac Mini lane responsibility. |
| 4.3 | Weekly meal plan view | Codespaces | Done | Meal plans page renders day-column/meal-row weekly grid and current plan metadata. |
| 4.4 | Meal swap functionality | Shared | Done | Per-slot swap flow implemented and persisted via update API; backend suggestion endpoint and UI suggest flow implemented. |
| 4.5 | Plan history | Codespaces | Done | History endpoint + UI list implemented; backend TTL set for retention lifecycle. |
| 4.6 | Nutrition summary | Codespaces | Done | Daily nutrition summary computed backend-side and displayed in UI tiles. |
| 4.7 | Quality scoring | Codespaces | Done | Quality score fields generated backend-side and displayed in current/history views. |

## Mac Mini Handoff

1. Verify deployed generation runtime behavior
- Confirm Lambda runtime has expected OpenAI secrets/config available.
- Exercise `POST /api/meal-plans/generate` in authenticated session and validate generated plan persistence.

2. Verify constraint behavior with real data
- Confirm Wednesday no-cook constraints and prep-time limits are honored in generated plans.
- Validate no prohibited ingredients/allergen regressions against real profiles and recipe set.

3. Verify UI/API round-trip in deployed app
- Generate current-week plan in UI and refresh page to confirm persistence.
- Swap at least one meal slot and verify saved change appears in current plan and history.
- Validate quality score and nutrition summary values render without API errors.

## Milestone Criteria Tracking

- [~] Meal plan generated respecting all constraints
- [ ] No allergen-containing recipes for Adult 2; no prohibited ingredients for Adult 1
- [x] Wednesday no-cook nights enforced
- [x] Quality score calculated and displayed

`[~]` indicates implementation completed in Codespaces, with deployed runtime verification still pending in Mac Mini lane.
