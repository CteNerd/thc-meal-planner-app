# Instruction: github-actions-ci-cd

## Scope
Use for workflow design and CI/CD policy.

## Rules
- Keep workflows deterministic and cache-aware.
- Build, lint, and test backend and frontend on pull requests.
- Enforce coverage gates at 80% minimum.
- Run infrastructure validation with CDK synth/diff.
- Keep deploy workflows environment-parameterized.

## AWS Authentication
- Use OIDC with `aws-actions/configure-aws-credentials@v4` and an IAM role — never store static AWS keys as secrets.
- Deploy role is `thc-meal-planner-github-actions-deploy`; ARN stored in `AWS_ROLE_TO_ASSUME` GitHub secret.
- CDK deploy reads CloudFormation stack outputs for downstream env var injection (e.g., Cognito IDs into frontend build).

## Reliability
- Fail fast on quality gate violations.
- Publish clear logs and artifacts for debugging.
