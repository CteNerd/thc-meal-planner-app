# Phase 2 Checklist and Backlog

Living tracker for data layer and profile work with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- **Mac Mini lane**: AWS deploys, environment verification, migration execution.
- **Codespaces lane**: backend/frontend feature code, tests, and PR preparation.

## Branching Strategy (Phase 2)

1. Start every task from `main`.
2. Use owner-prefixed branch names:
   - `mac/p2-1-datastack`
   - `mac/p2-6-profile-migration`
   - `mac/p2-8-secrets-stack`
   - `cs/p2-2-dynamodb-data-layer`
   - `cs/p2-3-profile-api`
   - `cs/p2-4-dependents-api`
   - `cs/p2-5-profile-ui`
   - `cs/p2-7-family-scope-authz`
3. Open draft PRs as soon as the contract is stable.
4. Use labels on PRs/issues:
   - `phase:2`
   - `lane:mac` or `lane:codespaces`
   - `area:backend`, `area:frontend`, or `area:infra`
5. Rebase from `main` before merge.
6. Use `phase2/integration` only when coordinated cross-lane validation is needed.

## Check-In Protocol (Both Brains)

Update this file at least daily and after each merged PR.

Check-in template:

- Lane: Mac Mini or Codespaces
- Task: Milestone item number(s)
- Branch: branch name
- Status: In Progress / Blocked / Ready for Review / Done
- Contracts touched: API routes, data model keys/indexes, env vars, auth assumptions
- Blockers or handoff requests

## Check-In Log

### 2026-03-22 - Mac Mini lane

- Lane: Mac Mini
- Task: 2.1
- Branch: `mac/p2-1-datastack`
- Status: Done
- Contracts touched: DynamoDB GSIs (`FamilyIndex`, `StatusIndex`, `CategoryIndex`, `CuisineIndex`)
- Blockers or handoff requests: None. Codespaces lane can begin 2.2 using these index names as active contract.
- Evidence: CloudFormation `ThcMealPlanner-dev-Data` is `UPDATE_COMPLETE`; tables present (`users`, `mealplans`, `recipes`, `favorites`, `grocerylists`, `chathistory`); GSIs active (`FamilyIndex`, `StatusIndex`, `CategoryIndex`, `CuisineIndex`); TTL enabled on `mealplans` + `chathistory` with attribute `TTL`.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.2
- Branch: `cs/p2-2-dynamodb-data-layer`
- Status: Done
- Contracts touched: Backend DI registration for DynamoDB repository, `DynamoDb` app config section, document-type to table-name mapping contract
- Blockers or handoff requests: None.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.3
- Branch: `cs/p2-3-profile-api`
- Status: In Progress
- Contracts touched: Added `GET`/`PUT /api/profile`, merge-semantics upsert behavior, FluentValidation request contract, profile key contract (`PK=USER#{sub}`, `SK=PROFILE`)
- Blockers or handoff requests: Align API `DynamoDb` config with deployed contract (`PK`/`SK` key attributes + concrete Users table name mapping).

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.2, 2.3, sync with `main`
- Branch: `cs/p2-3-profile-api`
- Status: Blocked (local git sync execution unavailable in current Codespaces session)
- Contracts touched: No new contracts; operational handoff for rebase/conflict resolution and DataStack output validation
- Blockers or handoff requests: Mac lane please run fetch/rebase from `main`, resolve conflicts if any, and report back final values for Users table name + key attribute casing used by deployed DataStack.

### 2026-03-23 - Codespaces lane

- Lane: Codespaces
- Task: 2.2, 2.3 docs reconciliation
- Branch: `main`
- Status: Done
- Contracts touched: Confirmed DataStack table naming and key schema from infra source of truth (`thc-meal-planner-dev-*`, partition key `PK`, sort key `SK`)
- Blockers or handoff requests: None for contracts; continue implementation alignment in backend config.

## Status Board

| Item | Description | Primary Owner | Status | Evidence / PR / Notes |
|---|---|---|---|---|
| 2.1 | Deploy DataStack (6 tables, GSIs, TTL) | Mac Mini | Done | AWS validated: 6 tables deployed, all target GSIs ACTIVE, TTL enabled for `mealplans` and `chathistory` (`TTL` attribute); deployed from `mac/p2-1-datastack`; PR #1 |
| 2.2 | Build DynamoDB data access layer | Codespaces | Done | Core generic repository contract + Infrastructure DynamoDB implementation scaffolded |
| 2.3 | GET/PUT `/api/profile` + FluentValidation | Codespaces | In Progress | Profile endpoints + validators + tests added; pending backend config alignment to deployed `PK`/`SK` and table mapping |
| 2.4 | CRUD `/api/family/dependents` | Codespaces | Not Started | |
| 2.5 | Profile UI and API integration | Codespaces | Not Started | |
| 2.6 | Run migration script for 4 profiles | Mac Mini | Not Started | Requires explicit user confirmation for records before commit/deploy |
| 2.7 | Family-scoped authorization enforcement | Codespaces | Not Started | |
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
5. Align backend `DynamoDb` app configuration with deployed contract (`PK`/`SK`, users table mapping).
6. Add repository/service integration tests for `FamilyIndex` query behavior before starting 2.4 dependent CRUD.
