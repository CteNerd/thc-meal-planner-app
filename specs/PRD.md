# Product Requirements Document (PRD)

## Product Vision

A secure, cost-effective family meal planning web application that empowers a small household to plan meals, manage dietary constraints, share grocery lists in real-time, and build a digital family cookbook — all through both a traditional UI and an AI-powered chat interface.

---

## Background

The family currently uses a GitHub-based markdown system (`thc-meal-prep-planner`) with Copilot agents for meal planning. While functional for early experimentation, it lacks:

- A proper web UI accessible on phones and tablets
- Real-time shared grocery list (checking off items at the store)
- Persistent recipe storage with images (a family cookbook)
- Multi-user authentication with security (2FA)
- AI chatbot for conversational meal planning

This application replaces that system entirely.

---

## Users

### Primary Users (Now)

| Label | Role | Key Needs |
|-------|------|-----------|
| **Adult 1** | Head of household | Vegetarian diet, ingredient exclusions, high-protein target, multiple cuisine preferences. Works from home. |
| **Adult 2** | Primary meal planner | Severe food allergy (anaphylaxis-level), autoimmune dietary restriction, sodium-limited medical diet, family-sized servings. Multiple cuisine preferences. |

### Future Users

| Label | Age Range | Notes |
|-------|-----------|-------|
| **Child 1** | Preschool | Dependent profile (no login) — considered in meal generation. Will get app access later. |
| **Child 2** | Elementary | Dependent profile (no login) — adventurous eater, will try most things. Will get app access later. |

### User Context

- Region: Southeast Texas
- Primary stores: 1 weekly, 1 bi-weekly bulk, 1 as-needed specialty
- Shopping pattern: Weekly Saturday run at primary store

> **PII Note**: Real names, ages, medical conditions, and store names are in `.local/profiles/` (gitignored). Specs use generic labels: Adult 1, Adult 2, Child 1, Child 2.

---

## Functional Requirements

### FR-1: Authentication & Authorization

| ID | Requirement | Priority |
|----|------------|----------|
| FR-1.1 | Users can log in with email/username and password | Must |
| FR-1.2 | TOTP 2FA is required for all users (Authy/Google Authenticator) | Must |
| FR-1.3 | Password policy: 12+ chars, mixed case, numbers, symbols | Must |
| FR-1.4 | JWT tokens stored in memory only (not localStorage) | Must |
| FR-1.5 | Password reset flow via email | Must |
| FR-1.6 | Role-based access: head_of_household and member roles | Should |

### FR-2: User Profiles

| ID | Requirement | Priority |
|----|------------|----------|
| FR-2.1 | Each user has an editable dietary profile | Must |
| FR-2.2 | Profile includes: dietary preferences, allergies (with severity), excluded ingredients, macro targets, cuisine preferences, cooking constraints | Must |
| FR-2.3 | Profile includes family member info (children with ages, preferences) | Should |
| FR-2.4 | Existing profiles (Adult 1, Adult 2) are pre-seeded from `.local/` seed data | Must |
| FR-2.5 | Profile changes take effect on next meal plan generation | Must |
| FR-2.6 | Dependent profiles: children have full dietary profiles stored in Users table with `role: dependent` (no Cognito login) | Must |
| FR-2.7 | Head-of-household users can create, edit, and delete dependent profiles | Must |
| FR-2.8 | Dependent profiles (Child 1, Child 2) are pre-seeded from `.local/` seed data | Must |
| FR-2.9 | Meal plan generation considers ALL family member profiles (adults + dependents) | Must |

### FR-3: Meal Planning

| ID | Requirement | Priority |
|----|------------|----------|
| FR-3.1 | View current weekly meal plan (day × meal type grid) | Must |
| FR-3.2 | Generate meal plan via AI chatbot with constraint enforcement | Must |
| FR-3.3 | Manually create/edit meal plans | Must |
| FR-3.4 | Each meal links to its recipe (recipe is single source of truth) | Must |
| FR-3.5 | Constraint engine enforces: variety (7-day no-repeat), protein blocking, cuisine blocking, no-cook nights, prep time limits | Must |
| FR-3.6 | Nutritional summary per day and per week against profile targets | Should |
| FR-3.7 | Meal plan history with archival (90-day TTL) | Should |
| FR-3.8 | View meal plan quality score (variety, constraint compliance) | Could |

### FR-4: Cookbook (Recipe Management)

| ID | Requirement | Priority |
|----|------------|----------|
| FR-4.1 | Browse all recipes with filtering by category, tags, dietary compatibility | Must |
| FR-4.2 | View recipe detail page with full cooking/prep information | Must |
| FR-4.3 | Create recipes manually with structured form | Must |
| FR-4.4 | Import recipe from URL (scrape and parse into structured format) | Should |
| FR-4.5 | Upload recipe image (photo from phone) | Should |
| FR-4.6 | Upload photo of physical recipe for OCR/AI parsing | Could |
| FR-4.7 | Favorite recipes with personal notes and portion overrides | Must |
| FR-4.8 | Recipes persist permanently (no TTL) — this is the family cookbook | Must |
| FR-4.9 | Seed 6 existing recipes from current repo | Must |

### FR-5: Grocery List

| ID | Requirement | Priority |
|----|------------|----------|
| FR-5.1 | Auto-generate grocery list from meal plan (aggregate ingredients) | Must |
| FR-5.2 | Group items by store section (produce, dairy, protein, pantry, frozen, bakery) | Must |
| FR-5.3 | Check off items with who/when indicator | Must |
| FR-5.4 | Real-time sync between users via activity-based polling (5s when page visible) | Must |
| FR-5.5 | Grocery list is a living document — no TTL on the list itself | Must |
| FR-5.6 | Completed items auto-expire after 7 days | Should |
| FR-5.7 | Add manual items to grocery list | Must |
| FR-5.8 | Each item shows associated meals/recipes (expandable) | Should |
| FR-5.9 | Optimistic concurrency to prevent lost updates | Must |
| FR-5.10 | Progress bar (completed / total items) | Should |
| FR-5.11 | Filter by: unchecked only, section, meal association | Should |
| FR-5.12 | Grocery list auto-updates when meal plan is modified (meal swap/add/remove triggers recalculation of affected items) | Must |
| FR-5.13 | Pantry/in-stock tracking: items the family already has on hand are flagged and excluded from "to buy" count | Should |
| FR-5.14 | Persistent pantry staples list (e.g., salt, pepper, oil) — auto-applied when generating grocery lists | Should |
| FR-5.15 | Users can toggle `inStock` per-item on the grocery list | Should |

### FR-6: AI Chatbot

| ID | Requirement | Priority |
|----|------------|----------|
| FR-6.1 | Slide-out chat panel accessible from any page | Must |
| FR-6.2 | Natural language commands for all CRUD operations | Must |
| FR-6.3 | AI generates meal plans with constraint awareness | Must |
| FR-6.4 | AI can modify individual meals, update profiles, manage grocery list | Must |
| FR-6.5 | Action confirmations before executing destructive operations | Must |
| FR-6.6 | Streaming responses via chunked transfer encoding | Should |
| FR-6.7 | Chat history stored for 30 days | Should |
| FR-6.8 | Rate limiting to prevent runaway OpenAI costs | Must |

### FR-7: Email Notifications

| ID | Requirement | Priority |
|----|------------|----------|
| FR-7.1 | Email on meal plan generation completion | Should |
| FR-7.2 | Security alerts for autonomous processes (secret rotation, infra changes) | Must |
| FR-7.3 | Weekly meal plan summary email (optional, user-configurable) | Could |

---

## Non-Functional Requirements

### NFR-1: Performance

| ID | Requirement | Target |
|----|------------|--------|
| NFR-1.1 | Lambda cold start | < 3 seconds (AOT compilation) |
| NFR-1.2 | API response time (warm) | < 500ms for CRUD operations |
| NFR-1.3 | Page load time | < 2 seconds on 4G connection |
| NFR-1.4 | Polling interval | 5 seconds when grocery page is active |

### NFR-2: Security

| ID | Requirement |
|----|------------|
| NFR-2.1 | OWASP Top 10 compliance |
| NFR-2.2 | All data encrypted in transit (TLS 1.2+) and at rest (DynamoDB default) |
| NFR-2.3 | TOTP 2FA for all users |
| NFR-2.4 | JWT tokens never stored in localStorage |
| NFR-2.5 | Content Security Policy headers |
| NFR-2.6 | API Gateway throttling |
| NFR-2.7 | Fine-grained IAM — Lambda only accesses required resources |

### NFR-3: Cost

| ID | Requirement | Target |
|----|------------|--------|
| NFR-3.1 | Monthly AWS cost | $5–15/month |
| NFR-3.2 | AWS Budget alert | At $20/month threshold |
| NFR-3.3 | OpenAI monthly cap | Tracked, alerting at $10 |

### NFR-4: Reliability

| ID | Requirement |
|----|------------|
| NFR-4.1 | Graceful degradation when OpenAI is unavailable (UI still works) |
| NFR-4.2 | Retry logic on transient DynamoDB/OpenAI failures |
| NFR-4.3 | Optimistic concurrency on grocery list prevents lost updates |

### NFR-5: Usability

| ID | Requirement |
|----|------------|
| NFR-5.1 | Mobile-first responsive design (grocery list optimized for phone at store) |
| NFR-5.2 | Responsive breakpoints: mobile (<640px), tablet (640-1024px), desktop (>1024px) |
| NFR-5.3 | PWA installable on phone home screen |
| NFR-5.4 | WCAG 2.1 AA accessibility compliance |
| NFR-5.5 | Keyboard navigation support |

### NFR-6: Code Quality

| ID | Requirement | Target |
|----|------------|--------|
| NFR-6.1 | Code coverage (unit + integration) | ≥ 80% |
| NFR-6.2 | Testing pyramid | 60% unit / 25% integration / 15% E2E |
| NFR-6.3 | Coverage enforced in CI | PR blocked if below 80% |

---

## User Stories

### Epic: Authentication

- **US-1**: As a user, I can log in with my email and password so that I can access my family's meal planner.
- **US-2**: As a user, I can set up TOTP 2FA so that my account is secure.
- **US-3**: As a user, I can reset my password via email so that I can recover my account.

### Epic: Meal Planning

- **US-4**: As Adult 2, I can ask the chatbot to "generate a meal plan for next week" so that I get a plan that respects everyone's dietary needs.
- **US-5**: As Adult 1, I can view the current meal plan on my phone so that I know what's for dinner.
- **US-6**: As a user, I can swap a meal by telling the chatbot "change Thursday dinner to something Asian" so that I can adjust the plan.
- **US-7**: As a user, I can click on a meal to see the full recipe so that I have all cooking instructions in one place.

### Epic: Cookbook

- **US-8**: As Adult 2, I can browse recipes filtered by "gluten-free" and "nut-free" so that I only see safe options.
- **US-9**: As Adult 1, I can favorite a recipe and add a note like "double the garlic" so that I remember my preferences.
- **US-10**: As a user, I can import a recipe by pasting a URL so that I can add online recipes to our cookbook.
- **US-11**: As a user, I can take a photo of a recipe card and have it parsed into structured data.

### Epic: Grocery List

- **US-12**: As Adult 2, I can generate a grocery list from the meal plan so that I know exactly what to buy.
- **US-13**: As Adult 2, I can check off items at the store on my phone and Adult 1 sees the updates within 5 seconds.
- **US-14**: As a user, I can see which meals an ingredient is for so that I know if I can skip it.
- **US-15**: As a user, I can add a manual item like "paper towels" to the grocery list.
- **US-16**: As Adult 2, when I swap Thursday's dinner in the meal plan, the grocery list automatically updates — removing ingredients I no longer need and adding new ones.
- **US-17**: As a user, I can mark items as "in stock" so they don't show up in my shopping count.
- **US-18**: As Adult 2, I can maintain a pantry staples list (salt, pepper, olive oil, etc.) so those items are always flagged as in-stock on new grocery lists.

### Epic: Family Profiles

- **US-19**: As a head-of-household, I can create a dependent profile for each child so their preferences are considered during meal planning.
- **US-20**: As Adult 2, I can edit Child 1's profile to update their food preferences as they grow.

### Epic: Notifications

- **US-21**: As Adult 2, I receive an email when the weekly meal plan is generated so I can review it.
- **US-22**: As Adult 1, I receive security alerts when autonomous processes run so I can monitor the system.

---

## Constraints

- **Budget**: ~$5–15/month AWS spend (non-negotiable)
- **Users**: 2 active users initially; 2 children as dependent profiles (no login); children to get app access later
- **Technology**: ASP.NET Core (.NET 9) backend, React 19 frontend, AWS CDK — chosen for professional development growth
- **Timeline**: Phased delivery — see [MILESTONES.md](MILESTONES.md)

---

## Out of Scope (V1)

- Multi-family support / SaaS model
- Native mobile apps (PWA is the mobile strategy)
- Social features (sharing recipes with other families)
- Payment/subscription features
- Calorie counting from food logs (only planned meals are tracked)
- Voice assistant integration
- Third-party grocery delivery integration
