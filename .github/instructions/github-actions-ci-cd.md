# Instruction: github-actions-ci-cd

## Scope
Use for workflow design and CI/CD policy.

## Rules
- Keep workflows deterministic and cache-aware.
- Build, lint, and test backend and frontend on pull requests.
- Enforce coverage gates at 80% minimum.
- Run infrastructure validation with CDK synth/diff.
- Keep deploy workflows environment-parameterized.

## Reliability
- Fail fast on quality gate violations.
- Publish clear logs and artifacts for debugging.
