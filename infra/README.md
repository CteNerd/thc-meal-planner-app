# Infrastructure Scaffold

Current scaffold (Phase 1.4):

- AWS CDK TypeScript project metadata and TypeScript config
- CDK app entry at `bin/app.ts`
- Environment configs for `dev` and `prod`
- Shared stack prop types and base constructs
- Stack skeletons for auth, data, api, frontend, notifications, and secrets

Environment-blocked follow-up tasks:

1. Install dependencies and run `npx cdk synth`.
2. Bootstrap CDK in the target AWS account.
3. Deploy `AuthStack` and verify Cognito outputs.
4. Replace placeholder assumptions with real domain, email, and secret values.
