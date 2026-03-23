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

## Status Board

| Item | Description | Primary Owner | Status | Evidence / PR / Notes |
|---|---|---|---|---|
| 2.1 | Deploy DataStack (6 tables, GSIs, TTL) | Mac Mini | Done | AWS validated: 6 tables deployed, all target GSIs ACTIVE, TTL enabled for `mealplans` and `chathistory` (`TTL` attribute); committed to `main` 2026-03-22 |
| 2.2 | Build DynamoDB data access layer | Codespaces | Not Started | |
| 2.3 | GET/PUT `/api/profile` + FluentValidation | Codespaces | Not Started | |
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
