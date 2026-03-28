# Phase 0 Checklist and Backlog

Status board for Copilot setup work in Phase 0. Keep this file updated over time as work is validated.

## Labels (Target Taxonomy)

Use these labels for Phase 0 tracking issues:

- `copilot`
- `copilot:backend`
- `copilot:frontend`
- `copilot:infra`
- `copilot:docs`

## Phase 0 Task Checklist

| Item | Description | Status | Evidence |
|---|---|---|---|
| 0.1 | Initialize `.github/` structure (`agents`, `instructions`, `skills`, `workflows`) | Done | Directories present in repo |
| 0.2 | Create `copilot-instructions.md` | Done | `.github/copilot-instructions.md` |
| 0.3 | Add adapted agents (4) | Done | `meal-planner`, `recipe-creator`, `grocery-list`, `nutrition-calculator` |
| 0.4 | Add new agents (4) | Done | `dotnet-engineer`, `react-engineer`, `devops-engineer`, `governance-reviewer` |
| 0.5 | Add instruction files (8) | Done | Files under `.github/instructions/` |
| 0.6 | Add skills (15) | Done | Files under `.github/skills/` |
| 0.7 | Add `copilot-setup-steps.yml` workflow | Done | `.github/workflows/copilot-setup-steps.yml` |
| 0.8 | Ensure `specs/` docs are available to agents | Done | `specs/` folder in repository |
| 0.9 | Verify agent functionality in practice | In Progress | Phase 1 scaffolding exists; CI workflow run `23411889262` passed after scaffolding fixes. Domain and engineering prompt validation completed on `2026-03-28`; coding-agent issue pickup/resolution validation still pending. |

## Milestone Criteria Validation

- [x] `copilot-instructions.md` exists and contains project context.
- [x] All 8 agents are defined.
- [x] All 8 instruction files are present.
- [x] All 15 skill files are present.
- [ ] Copilot coding agent test issue picked up and resolved.
- [x] Agent behavior validated with domain prompts.

## Backlog Issue Plan

Create and keep these issues open until validated:

1. **Phase 0.9: Validate domain agent responses**
   - Labels: `copilot`, `copilot:docs`
   - Acceptance:
     - Meal planning prompt uses constraints and flags conflicts.
     - Recipe creation prompt outputs structured fields and assumptions.
     - Grocery list prompt handles recalculation and conflict notes.
     - Nutrition prompt reports target variance clearly.

2. **Phase 0.9: Validate engineering agent outputs**
   - Labels: `copilot`, `copilot:backend`, `copilot:frontend`, `copilot:infra`
   - Acceptance:
     - `.NET` guidance matches API standards and validation policy.
     - `React` guidance matches app stack and responsive constraints.
     - `DevOps` guidance matches CDK/GitHub Actions plans.
     - `Governance` review catches missing tests and security gaps.

3. **Phase 0.9: Run Copilot setup workflow after Phase 1 scaffolding**
   - Labels: `copilot`, `copilot:infra`
   - Acceptance:
     - Workflow executes successfully after `backend/`, `frontend/`, `infra/` are scaffolded.
     - Build/test/lint steps are green or failures are actionable.

## Checkpoint (2026-03-22)

- Workflow validation portion of 0.9 is now satisfied: GitHub Actions CI run `23411889262` completed successfully on `main` after Phase 1 scaffolding.
- Remaining work for 0.9 is prompt-based validation of domain and engineering agents against real tasks.

## Notes

- This document is intended to be a living checklist.
- Do not mark 0.9 complete until tests are run against real prompts and project scaffolding.