# THC Family Meal Planner

A secure, cost-effective family meal planning web application for a small household.

**Stack**: React 19 + TypeScript + TailwindCSS | ASP.NET Core 9 on AWS Lambda | DynamoDB | Cognito | OpenAI | CDK

## Getting Started

See [specs/](specs/) for full project specifications.

| Start Here | Description |
|-----------|-------------|
| [specs/README.md](specs/README.md) | Project overview, architecture, and spec index |
| [specs/MILESTONES.md](specs/MILESTONES.md) | 8-phase delivery plan (start with Phase 0) |
| [specs/COPILOT_CONFIG.md](specs/COPILOT_CONFIG.md) | Agent, instruction, and skill configuration |

## Implementation Verification Policy

All profile data, recipes, constraints, and configuration in the spec documents are **DRAFT** and must be verified with the user before committing during implementation. See [specs/README.md](specs/README.md) for details.

## Project Structure (Target)

```
thc-meal-planner-app/
├── .github/                   # Copilot agents, instructions, skills, CI/CD
├── frontend/                  # React 19 + TypeScript + Vite + TailwindCSS
├── backend/                   # ASP.NET Core 9 (.NET AOT Lambda)
├── infra/                     # AWS CDK (TypeScript)
├── specs/                     # Project specifications (16 docs)
└── README.md
```
