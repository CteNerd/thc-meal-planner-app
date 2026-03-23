# Phase 2 Codespaces Kickoff Packet

Use this packet to start Codespaces work immediately while Mac Mini handles deploy/integration tasks.

## Scope Owned in Codespaces

- 2.2 Build DynamoDB data access layer
- 2.3 Profile API endpoints (`GET`/`PUT /api/profile`) with FluentValidation
- 2.4 Dependent profile endpoints (`CRUD /api/family/dependents`)
- 2.5 Profile UI and API wiring
- 2.7 Family-scoped authorization enforcement + tests

## Branch Plan

Create short-lived branches from `main`:

1. `cs/p2-2-dynamodb-data-layer`
2. `cs/p2-3-profile-api`
3. `cs/p2-4-dependents-api`
4. `cs/p2-5-profile-ui`
5. `cs/p2-7-family-scope-authz`

Use draft PRs early and add labels:

- `phase:2`
- `lane:codespaces`
- `area:backend`, `area:frontend`, or `area:infra`

## Current Contracts (From Mac Lane)

CDK index contract has been established:

- Users: `FamilyIndex` (`familyId` + `name`)
- MealPlans: `StatusIndex` (`familyId` + `statusCreatedAt`)
- Recipes: `CategoryIndex` (`category` + `name`), `CuisineIndex` (`cuisine` + `name`)

DataStack key + table naming contract (confirmed in infra source):

- Table primary keys: `PK` (partition key), `SK` (sort key)
- Dev table names:
	- `thc-meal-planner-dev-users`
	- `thc-meal-planner-dev-mealplans`
	- `thc-meal-planner-dev-recipes`
	- `thc-meal-planner-dev-favorites`
	- `thc-meal-planner-dev-grocerylists`
	- `thc-meal-planner-dev-chathistory`
- TTL attribute for expiring tables: `TTL`

Reference implementation files:

- `infra/lib/constructs/dynamo-table.ts`
- `infra/lib/stacks/data-stack.ts`

## Backend Target Files

- `backend/ThcMealPlanner.Api/Program.cs`
- `backend/ThcMealPlanner.Core/` (new domain/repository contracts)
- `backend/ThcMealPlanner.Infrastructure/` (new DynamoDB implementations)
- `backend/ThcMealPlanner.Tests/HealthEndpointTests.cs` (expand for profile/dependent authz)

## Frontend Target Files

- `frontend/src/pages/ProfilePage.tsx`
- `frontend/src/services/api.ts`
- `frontend/src/types/` (profile/dependent models)
- `frontend/src/App.test.tsx` (expand tests)

## Definition of Done for Codespaces PRs

1. Compiles and tests pass in branch.
2. Adds or updates tests for each behavior change.
3. Enforces family scoping in service/repository boundaries.
4. Uses Problem Details for API validation/error responses.
5. Updates `specs/PHASE2_CHECKLIST.md` status + check-in log entry.

## Commands to Run in Codespaces

Backend:

```bash
cd backend
dotnet restore
dotnet build
dotnet test
```

Frontend:

```bash
cd frontend
npm ci
npm run lint
npm run build
npm run test
```

Infra validation (if touched):

```bash
cd infra
npm ci
npm run build
npm run synth
```

## Required Sync Check-In Template

When posting updates in `specs/PHASE2_CHECKLIST.md`, use:

- Lane: Codespaces
- Task: 2.x
- Branch: `cs/...`
- Status: In Progress / Blocked / Ready for Review / Done
- Contracts touched: API routes, keys/indexes, env vars
- Blockers or handoff requests: explicit ask for Mac lane if needed

## Blockers to Escalate to Mac Lane

- Any AWS credentialed deploy or stack apply.
- Any migration run against real cloud resources.
- Any OpenAI secret wiring or Secrets Manager updates.
