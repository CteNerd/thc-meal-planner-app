# THC Meal Planner - Copilot Instructions

## Project Overview
Family meal planning web application for a small household.

- Frontend: React 19 + TypeScript + Vite + TailwindCSS
- Backend: ASP.NET Core 9 on AWS Lambda (.NET AOT)
- Database: Amazon DynamoDB (6 tables, on-demand pricing)
- Auth: Amazon Cognito with TOTP MFA
- Frontend auth implementation: native Cognito user pool SDK (`amazon-cognito-identity-js`), no Amplify dependency
- AI: OpenAI function calling for chatbot and planning tasks
- IaC: AWS CDK (TypeScript)
- Hosting: CloudFront + S3 (OAC)

## Architecture and Delivery Priorities
- Prefer serverless and low-ops solutions.
- Target low monthly cost (roughly $5-15 for current usage profile).
- Use one API surface with clear service boundaries.
- Keep data access family-scoped and authorization-aware.
- Favor deterministic APIs and validation before AI-assisted actions.

## Code Standards
- C# 13 / .NET 9 with nullable reference types enabled.
- FluentValidation for all request and command inputs.
- RFC 9457 Problem Details for API errors.
- Structured logs only, never include secrets or credentials.
- React function components and hooks only.
- TailwindCSS utilities preferred over bespoke styling systems.
- Maintain 80% minimum test coverage.

## Security and Privacy
- Do not log tokens, passwords, secrets, API keys, or profile PII.
- Validate JWTs from Cognito and enforce family-level authorization.
- Use Secrets Manager for API keys and secret values.
- Apply OWASP Top 10 mitigations for all user-facing features.

## Testing Expectations
- Backend: xUnit + FluentAssertions + NSubstitute.
- Frontend: Vitest + React Testing Library.
- E2E: Playwright with realistic user journeys.
- Prefer fast unit tests plus targeted integration and E2E coverage.

## Implementation Verification Policy
All profile data, recipes, constraints, and configuration in specs are draft.

Before writing code that commits, seeds, or migrates data from specs:
1. Present the proposed records to the user.
2. Ask for explicit confirmation.
3. Only then commit code or data changes.

This requirement applies to profiles, allergies, constraints, macros, recipes,
store mappings, and migration payloads.

## Working Rules for Copilot Agents
- Read relevant files under specs/ before proposing implementation details.
- Keep change sets small, test-backed, and reversible.
- Follow the active repository workflow in milestone/checklist docs (currently direct-to-main with frequent checkpoints).
- Prefer explicit assumptions and call out unresolved ambiguity.
- For any DI-activated service (AddScoped/AddSingleton/AddTransient/AddHttpClient), keep exactly one public constructor unless constructor selection is explicitly disambiguated.
- When adding optional dependencies to DI services, prefer nullable parameters on the single constructor over introducing overloaded public constructors.
- For destructive actions, require confirmation in UI and service layers.
- Keep API contracts stable and document intentional breaking changes.
- Run automated deployed validation checks before handing off manual validation items; record automated evidence in living phase checklists.
- Provide a progress checkpoint at least every 20-30 minutes of active work.
- At each checkpoint, update living checklist/backlog docs with completed items, blockers, and next actions.