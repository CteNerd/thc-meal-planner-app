# Milestones & Delivery Plan

## Overview

8-phase delivery plan (Phase 0–7) for the THC Meal Planner web application. Phase 0 establishes Copilot agents and instructions so all subsequent work benefits from configured AI context. Each phase builds on the previous, with working software deliverable at every milestone.

> **Implementation Verification Policy**: All data from these specs (profiles, recipes, constraints) is DRAFT. Agents must verify each data record with the user before committing to code or database. See [README.md](README.md) § Implementation Verification Policy.

---

## Phase 0: Copilot Agent Setup

**Goal**: Configure GitHub Copilot agents, instructions, skills, and coding agent workflow **before** any application code is written. This ensures every subsequent phase benefits from project-aware AI assistance.

### Steps

| # | Task | Details |
|---|------|---------|
| 0.1 | Initialize repo with `.github/` structure | Create `agents/`, `instructions/`, `skills/`, `workflows/` directories |
| 0.2 | Create `copilot-instructions.md` | Global project context (see [COPILOT_CONFIG.md](COPILOT_CONFIG.md) § Global Instructions) |
| 0.3 | Create adapted agents (4) | `meal-planner`, `recipe-creator`, `grocery-list`, `nutrition-calculator` — upgraded from legacy repo |
| 0.4 | Create new agents (4) | `dotnet-engineer`, `react-engineer`, `devops-engineer`, `governance-reviewer` — from awesome-copilot |
| 0.5 | Add instructions (8) | `csharp`, `aspnet-rest-apis`, `security-and-owasp`, `context-engineering`, `github-actions-ci-cd`, `dotnet-architecture`, `playwright-typescript`, `agent-safety` |
| 0.6 | Add skills (15) | All skills from [COPILOT_CONFIG.md](COPILOT_CONFIG.md) § Skills |
| 0.7 | Create `copilot-setup-steps.yml` | CI workflow for coding agents |
| 0.8 | Copy spec documents | Copy `specs/` folder into new repo for agent reference |
| 0.9 | Verify agent functionality | Test that agents respond with project context, can navigate specs |

### Milestone Criteria
- `copilot-instructions.md` loads correctly and contains project context
- All 8 agents defined and respond to domain prompts
- All 8 instruction files present
- All 15 skill files present
- Copilot coding agent can pick up a test issue
- Spec documents accessible to agents in the repo

---

## Phase 1: Foundation

**Goal**: Project scaffolding, CI/CD pipeline, infrastructure-as-code, and authentication.

### Steps

| # | Task | Details |
|---|------|---------|
| 1.1 | Initialize mono-repo | Create `frontend/`, `backend/`, `infra/` directories alongside existing `.github/` and `specs/` |
| 1.2 | Scaffold ASP.NET Core 9 API | Minimal API with health endpoint, Lambda hosting, AOT config |
| 1.3 | Scaffold React 19 + Vite + TailwindCSS | Create app with routing, TailwindCSS config, component folders |
| 1.4 | Create CDK project | 6 stacks defined (auth, data, api, frontend, notifications, secrets) |
| 1.5 | Deploy AuthStack | Cognito User Pool + Client, provision 2 users |
| 1.6 | Implement login flow | Frontend: native Cognito user pool SDK (SRP + TOTP, no Amplify) → Backend: JWT validation middleware |
| 1.7 | GitHub Actions CI pipeline | Build + test + lint for both .NET and React |
| 1.8 | Deploy to dev | Full pipeline: CDK deploy → S3 sync → CloudFront |

### Milestone Criteria
- Health endpoint returns 200 at CloudFront URL
- Login works with TOTP MFA for both users
- CI pipeline passes: build, test, lint
- Copilot coding agent can pick up issues

---

## Phase 2: Data Layer & Profiles

**Goal**: DynamoDB tables, data access layer, user profile management.

### Steps

| # | Task | Details |
|---|------|---------|
| 2.1 | Deploy DataStack | All 6 DynamoDB tables with GSIs and TTL config |
| 2.2 | Build DynamoDB data access layer | Generic repository pattern, typed document operations |
| 2.3 | Profile API endpoints | GET/PUT `/api/profile` with FluentValidation |
| 2.4 | Dependent profile endpoints | CRUD `/api/family/dependents` for managing child profiles |
| 2.5 | Profile UI | Profile page with dietary prefs, allergies, macro targets, cooking constraints, family member management |
| 2.6 | Run migration script | Migrate Adult 1 + Adult 2 profiles + Child 1 + Child 2 dependent profiles from `.local/seed-data/` |
| 2.7 | Family-scoped authorization | Service layer enforces familyId on all queries |
| 2.8 | SecretsStack deployment | OpenAI API key in Secrets Manager |

### Milestone Criteria
- Both user profiles load correctly after login
- Dependent profiles (Child 1, Child 2) visible in family management UI
- Profile edits persist to DynamoDB
- Migration script validates all data correctly (4 profiles total)
- Adult 1 has no severe allergies; Adult 2 has severe food allergy (anaphylaxis-level)

---

## Phase 3: Cookbook & Recipes

**Goal**: Recipe CRUD, cookbook browsing, favorites system, image upload.

### Steps

| # | Task | Details |
|---|------|---------|
| 3.1 | Recipe API endpoints | Full CRUD for recipes, includes cuisine/proteinSource/cookingMethod fields |
| 3.2 | Recipe migration | 6 existing recipes migrated with inferred fields |
| 3.3 | Cookbook browse UI | Recipe grid with search, category/cuisine filters |
| 3.4 | Recipe detail page | Full recipe with ingredients, instructions, nutrition, variants |
| 3.5 | Add/edit recipe form | Form with all fields, tag input, client-side validation |
| 3.6 | Image upload | Pre-signed S3 URL workflow, CloudFront serving via `/images/*` |
| 3.7 | Favorites API + UI | Toggle favorite, notes, portion override, favorites list |
| 3.8 | Recipe import from URL | Fetch URL → OpenAI parses into recipe structure |

### Milestone Criteria
- 6 migrated recipes visible in cookbook
- New recipes can be created with images
- Favorites toggle works for both users
- URL import produces parseable recipe structure

---

## Phase 4: Meal Planning & Constraints

**Goal**: Meal plan generation with constraint engine, weekly view, history.

### Steps

| # | Task | Details |
|---|------|---------|
| 4.1 | ConstraintEngine service | C# service with config from appsettings.json |
| 4.2 | Meal plan generation API | POST endpoint, OpenAI integration with constraint validation |
| 4.3 | Weekly meal plan view | Day columns × meal rows grid with recipe cards |
| 4.4 | Meal swap functionality | Swap individual meals, AI suggests alternatives |
| 4.5 | Plan history | List of past plans with quality scores, 90-day TTL |
| 4.6 | Nutrition summary | Per-day and per-week nutrition dashboard on plan view |
| 4.7 | Quality scoring | Variety, constraint compliance, preference matching scores |

### Milestone Criteria
- Meal plan generated respecting all constraints
- No allergen-containing recipes for Adult 2; no prohibited ingredients for Adult 1
- Wednesday no-cook nights enforced
- Quality score calculated and displayed

---

## Phase 5: Grocery List & Real-Time Sync

**Goal**: Grocery list generation, live sync, optimistic concurrency.

### Steps

| # | Task | Details |
|---|------|---------|
| 5.1 | Grocery list generation API | Aggregate ingredients from meal plan, group by store section |
| 5.2 | Grocery list UI | Grouped by section, checkboxes, quantity display |
| 5.3 | Optimistic concurrency | Version-based updates, 409 conflict handling |
| 5.4 | Activity-based polling | 5s polling with Page Visibility API, 304 support |
| 5.5 | Add/remove items manually | FAB button for manual adds, swipe-to-remove |
| 5.6 | Completed item TTL | 7-day cleanup for checked items (application-level) |
| 5.7 | Store preferences integration | Primary store section ordering for shopping flow |
| 5.8 | Grocery list reactivity | Auto-recalculate grocery list when meal plan is modified (swap/add/remove meals) |
| 5.9 | Pantry staples management | CRUD for persistent pantry staples list; auto-mark matching items as in-stock |
| 5.10 | In-stock toggle | Per-item in-stock flag on grocery list; in-stock items excluded from "to buy" count |

### Milestone Criteria
- Grocery list generates from active meal plan
- Both users see real-time updates when checking items
- Conflict resolution works (two users check same item simultaneously)
- Completed items auto-clean after 7 days
- Swapping a meal updates the grocery list (old ingredients removed, new ones added)
- Pantry staples auto-flagged as in-stock on new grocery lists
- In-stock items visually distinguished and excluded from shopping count

---

## Phase 6: AI Chatbot

**Goal**: Full CRUD chatbot with function calling, conversation history.

### Steps

| # | Task | Details |
|---|------|---------|
| 6.1 | Chat API endpoint | POST message, streaming response |
| 6.2 | OpenAI function calling | 8 function definitions wired to service layer (includes pantry management) |
| 6.3 | System prompt builder | Dynamic context injection (family, current plan, grocery state) |
| 6.4 | Chat UI | Bubble interface, markdown rendering, typing indicator |
| 6.5 | Confirmation flow | Destructive action confirmation cards |
| 6.6 | Conversation history | Storage in ChatHistory table, 30-day TTL |
| 6.7 | Safety guardrails | Topic restriction, allergy validation, input sanitization |

### Milestone Criteria
- Chatbot can generate meal plan via conversation
- Chatbot can add recipes to cookbook
- Chatbot respects allergy constraints in all suggestions
- Destructive actions require confirmation
- 30-day history accessible and auto-deleted

---

## Phase 7: Polish, Notifications & Production

**Goal**: Email notifications, PWA, responsive polish, production deployment.

### Steps

| # | Task | Details |
|---|------|---------|
| 7.1 | Email notifications | SES integration: meal plan ready, security alerts |
| 7.2 | PWA configuration | Manifest, service worker, offline support, install prompt |
| 7.3 | Responsive polish | Test all pages at mobile/tablet/desktop breakpoints |
| 7.4 | Dashboard page | This week summary, grocery status, quick actions |
| 7.5 | E2E test suite | Playwright tests for critical paths (login, plan, grocery, chat) |
| 7.6 | Coverage validation | Ensure 80% across all projects |
| 7.7 | Security hardening | CSP headers, security response headers, Dependabot, secret scanning |
| 7.8 | Production deployment | CDK deploy to prod, custom domain setup (optional), budget alarm |
| 7.9 | Migration to production | Run migration script against prod tables, validate |

### Milestone Criteria
- Email notifications send for meal plan completion
- PWA installable on mobile
- All responsive breakpoints polished
- 80% code coverage across .NET and React
- Playwright E2E suite passes
- Production deployment live and stable

---

## CI/CD Pipeline (GitHub Actions)

### On Pull Request

```yaml
jobs:
  backend:
    - dotnet restore
    - dotnet build
    - dotnet test --collect:"XPlat Code Coverage"
    - Upload coverage report
    - Fail if < 80%

  frontend:
    - npm ci
    - npm run lint
    - npm run build
    - npm run test -- --coverage
    - Fail if < 80%

  infra:
    - npm ci
    - npx cdk diff --context env=dev

  e2e: (on labeled PRs only)
    - Deploy to dev
    - npx playwright test
```

### On Merge to Main

```yaml
jobs:
  deploy-dev:
    - Build all
    - cdk deploy --all --context env=dev
    - aws s3 sync frontend/dist s3://frontend-bucket
    - aws cloudfront create-invalidation --paths "/*"
```

### On Release Tag

```yaml
jobs:
  deploy-prod:
    - Build all (release config)
    - cdk deploy --all --context env=prod
    - aws s3 sync frontend/dist s3://frontend-bucket-prod
    - aws cloudfront create-invalidation --paths "/*"
    - Run smoke tests
```

---

## Definition of Done

Each feature is considered done when:
1. Code implemented and compiles
2. Unit tests written and passing (80% coverage)
3. FluentValidation on all inputs
4. API returns appropriate HTTP status codes
5. Error responses use RFC 9457 format
6. Responsive at all 3 breakpoints
7. PR reviewed (human or governance agent)
8. CI pipeline passes
9. Deployed to dev and manually verified
