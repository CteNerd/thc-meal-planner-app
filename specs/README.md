# THC Family Meal Planner App

A secure, cost-effective family meal planning web application for a small household. Replaces the current GitHub-based markdown meal planning workflow with a full-stack web experience featuring AI-powered meal generation, shared grocery lists, and a digital family cookbook.

---

## Architecture Overview

```
┌──────────────────────┐
│   React 19 + TS SPA  │  ← Vite + TailwindCSS (PWA)
│   (CloudFront + S3)  │
└──────────┬───────────┘
           │ HTTPS
┌──────────▼───────────┐
│   API Gateway (REST) │  ← Throttling, CORS
└──────────┬───────────┘
           │
┌──────────▼───────────┐     ┌─────────────────┐
│  ASP.NET Core 9      │────▶│  OpenAI API      │
│  Lambda (.NET AOT)   │     │  (Function Call)  │
└──────────┬───────────┘     └─────────────────┘
           │
    ┌──────┼──────────┬─────────────┐
    ▼      ▼          ▼             ▼
┌───────┐┌────────┐┌──────────┐┌────────┐
│DynamoDB││Cognito ││S3 Images ││  SES   │
│(6 tbl) ││(Auth)  ││(Recipes) ││(Email) │
└───────┘└────────┘└──────────┘└────────┘
```

## Tech Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| Frontend | React 19, TypeScript, Vite, TailwindCSS | PWA, mobile-first responsive |
| Backend | ASP.NET Core 9 (.NET 9) | Lambda with AOT compilation |
| Database | Amazon DynamoDB | On-demand pricing, 6 tables |
| Auth | Amazon Cognito | TOTP 2FA, JWT, 2 users |
| AI | OpenAI API | Function calling for CRUD chatbot |
| IaC | AWS CDK (TypeScript) | Multi-stack, parameterized env |
| Hosting | CloudFront + S3 (OAC) | Private bucket, no public access |
| Email | Amazon SES | Notifications and security alerts |
| Images | Amazon S3 | Recipe photos, pre-signed URLs |
| CI/CD | GitHub Actions | Copilot coding agents, 80% coverage |

## Target Users

- **Adult 1** — Head of household, works from home
- **Adult 2** — Primary meal planner, family of 4
- **Child 1** (preschool age) and **Child 2** (elementary age) — Future user access

> **Note**: Real family profiles with names, ages, dietary details, and medical conditions are stored locally in `.local/profiles/` (gitignored). See `.local/profiles/README.md` for the generic-to-real label mapping.

## Estimated Monthly Cost

**~$5–15/month** for a 2-person household on AWS free tier + on-demand pricing.

## Repository Structure (Target)

```
thc-meal-planner-app/
├── frontend/                  # React 19 + TypeScript + Vite
│   ├── src/
│   │   ├── components/        # Shared UI components
│   │   ├── pages/             # Route-level pages
│   │   ├── hooks/             # Custom React hooks
│   │   ├── services/          # API client, auth service
│   │   ├── types/             # TypeScript interfaces
│   │   └── utils/             # Helpers, constants
│   ├── public/                # Static assets, PWA manifest
│   ├── e2e/                   # Playwright E2E tests
│   └── package.json
├── backend/
│   ├── ThcMealPlanner.Api/          # Lambda entry, controllers
│   ├── ThcMealPlanner.Core/         # Domain models, interfaces
│   ├── ThcMealPlanner.Infrastructure/ # DynamoDB, OpenAI, SES
│   └── ThcMealPlanner.Tests/        # xUnit tests
├── infra/                     # AWS CDK (TypeScript)
│   ├── lib/                   # Stack definitions
│   └── bin/                   # CDK app entry
├── docs/                      # Architecture docs, ADRs
├── scripts/                   # Migration, seed data
├── .github/
│   ├── agents/                # Copilot agent definitions
│   ├── instructions/          # Copilot coding instructions
│   ├── workflows/             # GitHub Actions CI/CD
│   ├── ISSUE_TEMPLATE/        # Issue templates
│   └── copilot-instructions.md
└── specs/                     # These spec documents (reference)
```

## Spec Documents

| Document | Purpose |
|----------|---------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | System architecture, data flow, component interactions |
| [PRD.md](PRD.md) | Product requirements, user stories, acceptance criteria |
| [API_SPEC.md](API_SPEC.md) | Full REST API specification with schemas |
| [DATA_MODEL.md](DATA_MODEL.md) | DynamoDB tables, GSIs, access patterns, TTL policies |
| [AUTH_SPEC.md](AUTH_SPEC.md) | Cognito config, 2FA flow, JWT validation, authorization |
| [CHATBOT_SPEC.md](CHATBOT_SPEC.md) | OpenAI integration, function definitions, safety guardrails |
| [FRONTEND_SPEC.md](FRONTEND_SPEC.md) | Component hierarchy, responsive design, PWA config |
| [INFRASTRUCTURE.md](INFRASTRUCTURE.md) | CDK stacks, CloudFront+S3, deployment pipeline |
| [SECURITY.md](SECURITY.md) | OWASP compliance, CORS, CSP, secrets management |
| [MIGRATION.md](MIGRATION.md) | Data migration from markdown repo to DynamoDB |
| [COPILOT_CONFIG.md](COPILOT_CONFIG.md) | Agent definitions, instructions, skills, coding agent workflow |
| [COST_ANALYSIS.md](COST_ANALYSIS.md) | Per-service AWS cost estimates |
| [MILESTONES.md](MILESTONES.md) | Phased delivery plan with milestone criteria |
| [PHASE0_CHECKLIST.md](PHASE0_CHECKLIST.md) | Living checklist and backlog plan for Phase 0 setup/validation |
| [PHASE1_CHECKLIST.md](PHASE1_CHECKLIST.md) | Living checklist and backlog plan for Phase 1 foundation work |
| [PHASE2_CHECKLIST.md](PHASE2_CHECKLIST.md) | Living checklist for Phase 2 data layer/profile work across parallel lanes |
| [COOKBOOK_SPEC.md](COOKBOOK_SPEC.md) | Recipe management, URL import, image upload, favorites |
| [TESTING_SPEC.md](TESTING_SPEC.md) | Testing pyramid, 80% coverage, tools, CI gates |

## Source Repository

This application is the successor to `thc-meal-prep-planner` — a markdown-first meal planning system with GitHub Copilot agents. All existing profiles, recipes, constraints, and history data will be migrated to the new application.

> **Important**: The new repository does **not** have access to the old `thc-meal-prep-planner` repo. All source data needed for migration is embedded directly in these spec documents (see [DATA_MODEL.md](DATA_MODEL.md) § Seed Data and [MIGRATION.md](MIGRATION.md) § Embedded Source Data). No external repo access is required. Real family PII (names, medical conditions, ages) is stored locally in `.local/profiles/` (gitignored) and referenced by generic labels (Adult 1, Adult 2, Child 1, Child 2) throughout these specs.

## Implementation Verification Policy

> **All profile data, recipes, constraints, and configuration in these specs are DRAFT and must be verified with the user before committing during implementation.** The user has identified known issues in the spec data that will be corrected during implementation. Agents must present data to the user for confirmation before writing it to code or database — do not blindly trust spec values.
>
> This applies to:
> - User profiles (names, dietary restrictions, allergies, macro targets, family members)
> - Recipe data (ingredients, nutritional info, tags, categorization)
> - Constraint engine configuration (thresholds, rules, scoring weights)
> - Store preferences and section mappings
> - Any data originating from the old `thc-meal-prep-planner` repo
