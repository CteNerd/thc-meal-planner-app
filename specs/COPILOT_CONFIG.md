# GitHub Copilot Configuration Specification

## Overview

This project heavily leverages GitHub Copilot for development velocity. Configuration includes custom agents, instructions, skills, and a coding agent workflow for automated issue resolution. Resources are curated from the [awesome-copilot](https://github.com/github/awesome-copilot) repository.

---

## Repository Structure

```
.github/
├── copilot-instructions.md       # Global Copilot instructions
├── agents/
│   ├── meal-planner.md           # Adapted from existing repo
│   ├── recipe-creator.md         # Adapted from existing repo
│   ├── grocery-list.md           # Adapted from existing repo
│   ├── nutrition-calculator.md   # Adapted from existing repo
│   ├── dotnet-engineer.md        # From awesome-copilot
│   ├── react-engineer.md         # From awesome-copilot
│   ├── devops-engineer.md        # From awesome-copilot
│   └── governance-reviewer.md    # From awesome-copilot
├── instructions/
│   ├── csharp.md                 # C# coding standards
│   ├── aspnet-rest-apis.md       # ASP.NET Core REST API patterns
│   ├── security-and-owasp.md     # OWASP security guidelines
│   ├── context-engineering.md    # Prompt/context best practices
│   ├── github-actions-ci-cd.md   # CI/CD pipeline standards
│   ├── dotnet-architecture.md    # .NET architecture patterns
│   ├── playwright-typescript.md  # E2E testing guidelines
│   └── agent-safety.md           # Safety guardrails for agents
├── skills/
│   ├── dotnet-best-practices.md
│   ├── architecture-blueprint.md
│   ├── copilot-instructions-blueprint.md
│   ├── folder-structure-blueprint.md
│   ├── create-specification.md
│   ├── create-agents.md
│   ├── webapp-testing.md
│   ├── playwright-generate-test.md
│   ├── polyglot-test-agent.md
│   ├── codeql.md
│   ├── dependabot.md
│   ├── secret-scanning.md
│   ├── cloud-design-patterns.md
│   ├── agent-governance.md
│   └── prd.md
└── workflows/
    └── copilot-setup-steps.yml   # Coding agent CI workflow
```

---

## Global Instructions (copilot-instructions.md)

```markdown
# THC Meal Planner - Copilot Instructions

## Project Overview
Family meal planning web application for a small household.
- Frontend: React 19 + TypeScript + Vite + TailwindCSS
- Backend: ASP.NET Core 9 on AWS Lambda (.NET AOT)
- Database: Amazon DynamoDB (6 tables, on-demand pricing)
- Auth: Amazon Cognito with TOTP 2FA
- AI: OpenAI function calling for chatbot
- IaC: AWS CDK (TypeScript)
- Hosting: CloudFront + S3 (OAC)

## Architecture Principles
- Serverless-first, targeting ~$5-15/month for 2 users
- .NET AOT compilation for Lambda cold start optimization
- Single Lambda function handles all API routes via ASP.NET Core routing
- Activity-based REST polling (not WebSockets) for real-time sync
- Optimistic concurrency for grocery list (version field)

## Code Standards
- C# 13 / .NET 9 with nullable reference types enabled
- FluentValidation for all request validation
- Structured logging with ILogger (JSON format for CloudWatch)
- RFC 9457 Problem Details for error responses
- React functional components with hooks only (no class components)
- TailwindCSS utility classes (no CSS modules or styled-components)
- 80% code coverage minimum

## Family Context (for meal planning agents)
- Family profiles are stored locally in `.local/profiles/` (gitignored, not committed)
- Adult 1: Vegetarian, ingredient exclusions, target macros.
- Adult 2: Autoimmune dietary restriction, severe food allergy, sodium-limited medical diet, target macros.
- Children: Child 1 (preschool, picky), Child 2 (elementary, adventurous).
- Store preferences: 1 primary (weekly), 1 bulk (bi-weekly), 1 as-needed.
- See `.local/profiles/` for real names, ages, medical details, and store names.

## Testing
- Backend: xUnit + FluentAssertions + NSubstitute
- Frontend: Vitest + React Testing Library
- E2E: Playwright (TypeScript)
- Coverage: Coverlet (.NET), @vitest/coverage-v8 (React)
- Always write tests for new features; maintain 80% coverage

## Security
- Never log sensitive data (tokens, API keys, passwords)
- Validate all inputs server-side with FluentValidation
- Use Secrets Manager for OpenAI API key (never environment variables)
- Apply OWASP Top 10 mitigations
```

---

## Agents

### Adapted from Existing Repo (4)

These agents are upgraded from the current `thc-meal-prep-planner` placeholder agents to work with the new web application codebase:

| Agent | Purpose | Key Capabilities |
|-------|---------|-----------------|
| `meal-planner` | Generate and modify weekly meal plans | Constraint engine invocation, OpenAI integration, family-safe recipe selection |
| `recipe-creator` | Create and parse recipes for the cookbook | Markdown-to-DynamoDB mapping, nutritional calculation, image/URL import |
| `grocery-list` | Manage grocery list generation and maintenance | Ingredient aggregation, store section mapping, optimistic concurrency |
| `nutrition-calculator` | Calculate and validate nutritional information | Per-recipe and per-day nutrition, macro target comparison, scoring |

### From awesome-copilot (4)

| Agent | Source | Adaptation |
|-------|--------|------------|
| `dotnet-engineer` | `expert-dotnet-software-engineer` | Configured for ASP.NET Core 9, Lambda, DynamoDB, AOT compilation |
| `react-engineer` | `expert-react-frontend-engineer` | Configured for React 19, Vite, TailwindCSS, PWA patterns |
| `devops-engineer` | `devops-expert` | Configured for AWS CDK (TypeScript), GitHub Actions, CloudFront |
| `governance-reviewer` | `agent-governance-reviewer` | PR review governance, quality gates, security checks |

---

## Instructions (8)

All sourced from `github/awesome-copilot/instructions/`:

| Instruction | Purpose | Customizations |
|------------|---------|---------------|
| `csharp` | C# 13 coding standards | Nullable reference types, record types, pattern matching |
| `aspnet-rest-apis` | REST API patterns | RFC 9457 errors, FluentValidation, DynamoDB patterns |
| `security-and-owasp` | Security guidelines | OWASP Top 10, Cognito JWT validation, pre-signed URLs |
| `context-engineering` | Prompt engineering | System prompt patterns for meal planning chatbot |
| `github-actions-ci-cd` | CI/CD standards | Build/test/deploy pipeline, coverage gates |
| `dotnet-architecture` | .NET architecture patterns | Service layer, DI, options pattern, Lambda hosting |
| `playwright-typescript` | E2E testing | Page object model, test fixtures, auth setup |
| `agent-safety` | Agent guardrails | Destructive action prevention, confirmation flows |

---

## Skills (15)

| Skill | Purpose |
|-------|---------|
| `dotnet-best-practices` | .NET coding patterns and anti-patterns |
| `architecture-blueprint` | Generate architecture documentation |
| `copilot-instructions-blueprint` | Generate/update copilot-instructions.md |
| `folder-structure-blueprint` | Generate project folder structure |
| `create-specification` | Generate technical specifications |
| `create-agents` | Create new Copilot agents |
| `webapp-testing` | Web application testing strategies |
| `playwright-generate-test` | Generate Playwright E2E tests |
| `polyglot-test-agent` | Cross-language test generation |
| `codeql` | Code security analysis setup |
| `dependabot` | Dependency update configuration |
| `secret-scanning` | GitHub secret scanning setup |
| `cloud-design-patterns` | Cloud architecture patterns |
| `agent-governance` | Agent quality and safety governance |
| `prd` | Product requirements generation |

---

## Coding Agent Workflow

Automated issue resolution using GitHub Copilot coding agents:

### Flow

```
1. Create GitHub Issue (feature/bug/task)
         │
2. Add label: "copilot"
         │
3. Copilot coding agent picks up issue
         │
4. Agent creates feature branch from main
         │
5. Agent implements changes
         │
6. Agent runs: copilot-setup-steps.yml
   ├── dotnet build
   ├── dotnet test (80% coverage gate)
   ├── npm run build (frontend)
   ├── npm run test (80% coverage gate)
   └── npm run lint
         │
7. Agent creates Pull Request
         │
8. CI pipeline runs (GitHub Actions)
   ├── Build & test (.NET + React)
   ├── cdk diff (infrastructure changes)
   ├── Playwright E2E (if applicable)
   └── Coverage report
         │
9. Human reviews PR
   ├── governance-reviewer agent assists
   └── Approve or request changes
         │
10. Merge to main → auto-deploy to dev
         │
11. Tag release → deploy to prod
```

### copilot-setup-steps.yml

```yaml
name: Copilot Setup Steps
on: workflow_dispatch

jobs:
  setup:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # .NET setup
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --collect:"XPlat Code Coverage"

      # Node.js setup (frontend + CDK)
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
      - working-directory: ./frontend
        run: npm ci
      - working-directory: ./frontend
        run: npm run build
      - working-directory: ./frontend
        run: npm run test -- --coverage
      - working-directory: ./frontend
        run: npm run lint

      # CDK validation
      - working-directory: ./infra
        run: npm ci
      - working-directory: ./infra
        run: npx cdk synth --context env=dev
```

### Issue Labels for Agents

| Label | Behavior |
|-------|----------|
| `copilot` | Triggers coding agent to work on the issue |
| `copilot:backend` | Guides agent to focus on .NET/API changes |
| `copilot:frontend` | Guides agent to focus on React/UI changes |
| `copilot:infra` | Guides agent to focus on CDK/infrastructure changes |
| `copilot:docs` | Documentation-only changes |

---

## MCP Server Configuration

### AWS IaC MCP Server

For infrastructure-related development, configure the AWS IaC MCP Server from `https://github.com/awslabs/mcp`:

```json
// .vscode/mcp.json (for local Copilot)
{
  "servers": {
    "aws-iac": {
      "type": "stdio",
      "command": "uvx",
      "args": ["awslabs.aws-iac-mcp-server@latest"],
      "env": {
        "AWS_REGION": "us-east-1"
      }
    }
  }
}
```

> **Note**: The CDK-specific MCP server is deprecated. Use the AWS IaC MCP Server which supports CDK, CloudFormation, and other AWS IaC tools.

---

## Excluded Resources

The following `awesome-copilot` resources were evaluated and excluded:

| Resource | Reason |
|----------|--------|
| `ef-core` instruction | Project uses DynamoDB, not Entity Framework |
| `aws-cdk-python-setup` skill | CDK uses TypeScript, not Python |
| All Azure-specific resources | Project is AWS-only |
| All Terraform resources | Project uses AWS CDK |
