# Phase 2 Checklist and Backlog

Living tracker for data layer and profile work with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- **Mac Mini lane**: AWS deploys, environment verification, migration execution.
- **Codespaces lane**: backend/frontend feature code, tests, and PR preparation.

## Working Policy

Both Mac Mini and Codespaces work directly on `main`. No feature branches or PRs. Resolve any conflicts within the IDE.

Check in after each subphase or logical commit unit — push to `main` promptly so the two environments stay in sync.

## Check-In Protocol (Both Brains)

Update this file after each subphase commit and at least daily.

Check-in template:

- Lane: Mac Mini or Codespaces
- Task: Milestone item number(s)
- Status: In Progress / Blocked / Done
- Contracts touched: API routes, data model keys/indexes, env vars, auth assumptions
- Blockers or handoff requests

## Check-In Log

### 2026-03-22 - Mac Mini lane

- Lane: Mac Mini
- Task: 2.1
- Status: Done
- Contracts touched: DynamoDB GSIs (`FamilyIndex`, `StatusIndex`, `CategoryIndex`, `CuisineIndex`)
- Blockers or handoff requests: None. Codespaces lane can begin 2.2 using these index names as active contract.
- Evidence: CloudFormation `ThcMealPlanner-dev-Data` is `UPDATE_COMPLETE`; tables present (`users`, `mealplans`, `recipes`, `favorites`, `grocerylists`, `chathistory`); GSIs active (`FamilyIndex`, `StatusIndex`, `CategoryIndex`, `CuisineIndex`); TTL enabled on `mealplans` + `chathistory` with attribute `TTL`.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.2
- Status: Done
- Contracts touched: Backend DI registration for DynamoDB repository, `DynamoDb` app config section, document-type to table-name mapping contract
- Blockers or handoff requests: None.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.3
- Status: In Progress
- Contracts touched: Added `GET`/`PUT /api/profile`, merge-semantics upsert behavior, FluentValidation request contract, profile key contract (`PK=USER#{sub}`, `SK=PROFILE`)
- Blockers or handoff requests: Align API `DynamoDb` config with deployed contract (`PK`/`SK` key attributes + concrete Users table name mapping).

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.2, 2.3, sync with `main`
- Status: Blocked (local git sync execution unavailable in current Codespaces session)
- Contracts touched: No new contracts; operational handoff for rebase/conflict resolution and DataStack output validation
- Blockers or handoff requests: Mac lane please run fetch/rebase from `main`, resolve conflicts if any, and report back final values for Users table name + key attribute casing used by deployed DataStack.

### 2026-03-23 - Mac Mini lane

- Lane: Mac Mini
- Task: 2.6 prep
- Status: In Progress
- Contracts touched: Added migration console scaffold (`backend/ThcMealPlanner.Migration`) with dry-run/execute modes and users-table write path (`PK`/`SK` keys to `thc-meal-planner-dev-users`)
- Blockers or handoff requests: Awaiting explicit user confirmation of the 4 profile records before committing/running migration payload writes.

### 2026-03-23 - Mac Mini lane

- Lane: Mac Mini
- Task: 2.6 migration dry-run
- Status: In Progress
- Contracts touched: Finalized local profile payload in `.local/seed-data/Users.json` using `PK=USER#{userId}` + `SK=PROFILE` for both adults and dependents; verified 4-record dry-run against `thc-meal-planner-dev-users`.
- Blockers or handoff requests: Awaiting explicit approval for live migration writes (`--execute`).

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.2, 2.3 docs reconciliation
- Status: Done
- Contracts touched: Confirmed DataStack table naming and key schema from infra source of truth (`thc-meal-planner-dev-*`, partition key `PK`, sort key `SK`)
- Blockers or handoff requests: None for contracts; continue implementation alignment in backend config.

### 2026-03-22 - Mac Mini lane

- Lane: Mac Mini
- Task: 2.8 (next)
- Status: Not Started — holding until Codespaces signals readiness for 2.6 handoff
- Contracts touched: None yet
- Blockers or handoff requests: 2.6 blocked on user confirmation of migration records; 2.8 SecretsStack queued as next Mac Mini work item.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.3
- Status: In Progress
- Contracts touched: Aligned `DynamoDb` config to deployed key casing (`PK`/`SK`) and users table mapping (`thc-meal-planner-dev-users`); added profile API tests for partial merge semantics and family claim fallback
- Blockers or handoff requests: Mac lane can proceed with 2.6 prep; only blocker remains explicit user confirmation of migration records before commit/deploy.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.3, 2.4
- Status: In Progress
- Contracts touched: Switched to environment-specific table mapping strategy (base appsettings no table hardcoding; dev appsettings maps `UserProfileDocument` + `DependentProfileDocument` to users table); added repository GSI index query support; scaffolded dependent CRUD endpoints at `/api/family/dependents`
- Blockers or handoff requests: No Mac blocker for coding; Mac 2.6 remains blocked on explicit migration record confirmation.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.4, 2.5, 2.7
- Status: In Progress
- Contracts touched: Added family-scoped dependent service boundary (`IDependentProfileService`), tightened profile role mutation guardrails, added frontend profile/dependent API client touchpoints and shared types
- Blockers or handoff requests: Await integrated runtime build/test execution in an environment where terminal provider is functional.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.4, 2.5, 2.7
- Status: In Progress
- Contracts touched: Centralized user claim parsing via authenticated user context adapter; added member-role denial tests for dependent CRUD routes; wired Profile UI actions for profile save and dependent create/delete via API client
- Blockers or handoff requests: Mac lane still blocked on 2.6 until migration records are explicitly confirmed by user before commit/deploy.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.4, 2.5, 2.7 commit checkpoint + hardening
- Status: In Progress
- Contracts touched: No new route contracts yet; hardening pass in progress for RFC 9457-style authorization failure responses
- Blockers or handoff requests: Local commit execution blocked in this Codespaces session due terminal ENOPRO provider error; Mac lane can run commit/push from synced working tree.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.7 hardening follow-through
- Status: In Progress
- Contracts touched: Dependent authorization failures now consistently return RFC 9457 Problem Details (403) instead of plain forbid; denial-path tests assert forbidden payload content
- Blockers or handoff requests: Attempted targeted backend test run is still blocked in this session by terminal ENOPRO provider error; require Mac lane execution for integrated test confirmation and commit/push.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.7 hardening (ProblemDetails contract)
- Status: In Progress
- Contracts touched: Centralized profile/dependent 401/403/404 ProblemDetails builders and normalized titles/details; endpoint tests now assert ProblemDetails payload fields (`status`, `title`, `detail`) including missing-claims unauthorized paths
- Blockers or handoff requests: Local terminal ENOPRO still blocks integrated `dotnet test` execution and commit commands in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.5 UI/API hardening
- Status: In Progress
- Contracts touched: Frontend API error handling now extracts ProblemDetails `detail`/`title`/validation errors for Profile page actions; added focused unit test coverage for API error-message extraction behavior
- Blockers or handoff requests: Local terminal ENOPRO still blocks integrated frontend test execution in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.7 validation-contract hardening (option 1)
- Status: In Progress
- Contracts touched: Endpoint tests now assert ValidationProblem shape for 400 responses (`status`, `title`, `errors`) for profile and dependent create/update invalid payload paths
- Blockers or handoff requests: Local terminal ENOPRO still blocks integrated backend test execution and commit commands in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.4, 2.7 coverage expansion
- Status: In Progress
- Contracts touched: Added unit coverage for dependent service boundary (`ListByFamilyAsync` index/filter contract, create/update/delete family-scope behavior) plus endpoint tests for nested validation keys and missing-claims unauthorized paths across dependent CRUD operations
- Blockers or handoff requests: Terminal ENOPRO still blocks integrated `dotnet test` execution and commit commands in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.3, 2.4, 2.7 coverage expansion
- Status: In Progress
- Contracts touched: Added dedicated validator unit tests for profile/dependent request contracts (nested property names and constraint messages) and added dependent service tests for not-found update/delete edge paths
- Blockers or handoff requests: Terminal ENOPRO still blocks integrated test execution and commit commands in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.5 coverage expansion
- Status: In Progress
- Contracts touched: Added Profile page component tests for load success, API ProblemDetails error rendering, and dependent add validation/error flows using mocked profile API service calls
- Blockers or handoff requests: Terminal ENOPRO still blocks integrated frontend test execution and commit commands in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.5, 2.7 coverage expansion
- Status: In Progress
- Contracts touched: Added auth claim resolver unit tests (required/fallback/missing claim paths and role semantics) and expanded Profile page interaction tests for save success, dependent create success, and dependent delete API-error rendering
- Blockers or handoff requests: Terminal ENOPRO still blocks integrated backend/frontend test execution and commit commands in this session.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.5, 2.7 coverage expansion
- Status: In Progress
- Contracts touched: Added frontend `apiFetch` retry/unauthorized behavior tests (refresh success/failure and retry-401 callback path) and backend ProblemDetails helper contract tests via internal test access (`InternalsVisibleTo`)
- Blockers or handoff requests: Terminal ENOPRO still blocks integrated backend/frontend test execution and commit commands in this session.

## Status Board

| Item | Description | Primary Owner | Status | Evidence / PR / Notes |
|---|---|---|---|---|
| 2.1 | Deploy DataStack (6 tables, GSIs, TTL) | Mac Mini | Done | AWS validated: 6 tables deployed, all target GSIs ACTIVE, TTL enabled for `mealplans` and `chathistory` (`TTL` attribute); committed to `main` 2026-03-22 |
| 2.2 | Build DynamoDB data access layer | Codespaces | Done | Core generic repository contract + Infrastructure DynamoDB implementation scaffolded |
| 2.3 | GET/PUT `/api/profile` + FluentValidation | Codespaces | In Progress | Profile endpoints + validators + expanded endpoint and validator unit tests; config aligned to `PK`/`SK` + users mapping; pending integrated build/test run |
| 2.4 | CRUD `/api/family/dependents` | Codespaces | In Progress | API endpoint scaffold + validators + expanded endpoint tests + expanded service unit tests (family-scope/index-filter/not-found paths); pending integrated runtime verification |
| 2.5 | Profile UI and API integration | Codespaces | In Progress | Frontend touchpoints + ProblemDetails-aware error messaging + expanded page-level and API-client auth/retry behavior tests; pending integrated frontend test run |
| 2.6 | Run migration script for 4 profiles | Mac Mini | Not Started | Requires explicit user confirmation for records before commit/deploy |
| 2.7 | Family-scoped authorization enforcement | Codespaces | In Progress | Service boundary + standardized 401/403/404 + 400 ValidationProblem contract assertions + auth-claim resolver and ProblemDetails helper contract unit coverage; pending integrated test run |
| 2.8 | Deploy SecretsStack (OpenAI key) | Mac Mini | Not Started | |

## Milestone Criteria Tracking

- [ ] Both user profiles load correctly after login
- [ ] Dependent profiles (Child 1, Child 2) visible in family management UI
- [ ] Profile edits persist to DynamoDB
- [ ] Migration script validates all data correctly (4 profiles total)
- [ ] Adult 1 has no severe allergies
- [ ] Adult 2 has severe food allergy (anaphylaxis-level)

## Backlog / Follow-Ups

1. Define DynamoDB key schema contract in code comments or ADR before repository implementation is merged.
2. Add integration tests for familyId enforcement at repository/service boundaries.
3. Add UI test coverage for profile save and dependents CRUD happy path + validation errors.
4. Confirm migration payload records with user before committing migration data or scripts.
5. Add repository/service integration tests for `FamilyIndex` query behavior against deployed DynamoDB.
6. Add integrated authorization-contract verification pass (401/403/404 payload shape) once terminal test execution is restored.
