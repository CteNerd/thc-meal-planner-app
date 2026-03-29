# Infrastructure Specification

## Overview

All infrastructure is defined as code using AWS CDK (TypeScript). The application targets ~$5-15/month for a 2-user household by leveraging serverless, on-demand pricing, and AWS free-tier benefits.

---

## CDK Project Structure

```
infra/
├── bin/
│   └── app.ts                    # CDK app entry point
├── lib/
│   ├── stacks/
│   │   ├── auth-stack.ts         # Cognito User Pool + App Client
│   │   ├── data-stack.ts         # DynamoDB tables + GSIs
│   │   ├── api-stack.ts          # API Gateway + Lambda
│   │   ├── frontend-stack.ts     # CloudFront + S3
│   │   ├── notifications-stack.ts # SES configuration
│   │   └── secrets-stack.ts      # Secrets Manager
│   ├── constructs/
│   │   ├── lambda-function.ts    # Shared Lambda construct (.NET AOT)
│   │   └── dynamo-table.ts       # Shared table construct with defaults
│   └── config/
│       ├── dev.ts                # Dev environment config
│       └── prod.ts               # Prod environment config
├── cdk.json
├── package.json
└── tsconfig.json
```

---

## Stack Definitions

### 1. AuthStack

**Resources**:
- Cognito User Pool (`thc-meal-planner-{env}-user-pool`)
  - Email sign-in, TOTP MFA required
  - Password policy: 12+ chars, mixed case, number, symbol
  - Advanced security features enabled
- Cognito User Pool Client (`thc-meal-planner-{env}-web-client`)
  - Auth flows: `USER_SRP_AUTH`, `REFRESH_TOKEN_AUTH`
  - Token validity: access 1h, ID 1h, refresh 30d
  - No client secret (public SPA client)

**Outputs**: UserPoolId, UserPoolClientId, UserPoolDomain

### 2. DataStack

**Resources**:
- 6 DynamoDB tables (all on-demand billing):

| Table | PK | SK | GSIs | TTL Field |
|-------|----|----|------|-----------|
| Users | `PK` (String) | `SK` (String) | FamilyIndex | — |
| MealPlans | `PK` (String) | `SK` (String) | StatusIndex | `TTL` |
| Recipes | `PK` (String) | `SK` (String) | CategoryIndex, CuisineIndex | — |
| Favorites | `PK` (String) | `SK` (String) | — | — |
| GroceryLists | `PK` (String) | `SK` (String) | — | — |
| ChatHistory | `PK` (String) | `SK` (String) | — | `TTL` |

- S3 bucket for recipe images (`thc-meal-planner-{env}-recipe-images`)
  - Versioning: disabled (not needed for recipe images)
  - Lifecycle: intelligent-tiering after 30 days
  - CORS: allow PUT from CloudFront domain for pre-signed uploads
  - Public access: blocked (served via CloudFront)

**Outputs**: Table ARNs, S3 bucket ARN

### 3. ApiStack

**Resources**:
- API Gateway REST API (`thc-meal-planner-{env}-api`)
  - Stage: `{env}` (dev or prod)
  - Deploy with stage variables for environment
  - Throttling: 100 requests/second burst, 50 sustained
  - CORS: single origin per environment (custom domain when configured, otherwise CloudFront domain)
  - Custom domain: optional via Route 53
  - Binary media types: `application/json`
  - Request validation: enabled

- Lambda Function (`thc-meal-planner-{env}-api-handler`)
  - Runtime: `dotnet8` (provided.al2023 for AOT)
  - Architecture: `arm64` (Graviton — cheaper)
  - Memory: 512 MB
  - Timeout: 30 seconds
  - Cold start: <3s with .NET AOT
  - Bundling: publish with `-c Release` + AOT output
  - Environment variables:
    - `ASPNETCORE_ENVIRONMENT`: `Development` or `Production`
    - `COGNITO_USER_POOL_ID`: from AuthStack
    - `COGNITO_CLIENT_ID`: from AuthStack
    - `OPENAI_SECRET_ARN`: from SecretsStack
    - `TABLE_PREFIX`: `thc-meal-planner-{env}`
    - `RECIPE_IMAGES_BUCKET`: from DataStack
    - `SES_FROM_EMAIL`: verified sender
  - IAM permissions:
    - DynamoDB: CRUD on all 6 tables + GSIs
    - S3: GetObject, PutObject on recipe images bucket
    - Secrets Manager: GetSecretValue on OpenAI key
    - SES: SendEmail
  - Provisioned concurrency: 0 (on-demand to save costs; cold starts acceptable for 2 users)

- Lambda URL or API Gateway proxy integration:
  - Single Lambda handles all routes via ASP.NET Core routing
  - `{proxy+}` catch-all integration

**Outputs**: API Gateway URL, Lambda function ARN

### 4. FrontendStack

**Resources**:
- S3 Bucket (`thc-meal-planner-{env}-frontend`)
  - Static website hosting: disabled (CloudFront handles routing)
  - Public access: blocked
  - Bucket policy: CloudFront OAC only

- CloudFront Distribution:
  - **Origin 1** (default): S3 bucket via OAC (Origin Access Control)
    - Path pattern: `/*` (default)
    - Viewer protocol: HTTPS only
    - Cache policy: CachingOptimized for static assets
    - Error pages: 403/404 → `/index.html` with 200 (SPA routing)
  - **Origin 2**: API Gateway
    - Path pattern: `/api/*`
    - Cache policy: CachingDisabled
    - Origin request policy: AllViewerExceptHostHeader
    - Viewer protocol: HTTPS only
  - **Origin 3**: S3 recipe images bucket
    - Path pattern: `/images/*`
    - Cache policy: CachingOptimized
  - Price class: PriceClass_100 (US/Canada/Europe only — cheapest)
  - SSL certificate: ACM cert (free) for custom domain if configured
  - Default root object: `index.html`
  - HTTP/2 and HTTP/3 enabled
  - Compression: Gzip + Brotli

**Outputs**: CloudFront distribution URL, distribution ID

### 5. NotificationsStack

**Resources**:
- SES verified identities (email addresses for Adult 1 and Adult 2 — from `.local/profiles/`)
- SES configuration set for tracking
- No need to exit SES sandbox (sending only to verified family emails)

> **Note**: SES stays in sandbox mode. Since we only send to the 2 verified family email addresses, sandbox restrictions are acceptable and avoid the review process.

**Outputs**: SES configuration set name

### 6. SecretsStack

**Resources**:
- Secrets Manager secret: `thc-meal-planner/{env}/openai-api-key`
  - Rotation: manual (no automatic rotation for API keys)
  - Encryption: default AWS KMS key
- Secrets Manager secret: `thc-meal-planner/{env}/app-secrets`
  - Contains any additional configuration secrets

**Outputs**: Secret ARNs

---

## Environment Configuration

### Dev

```typescript
// config/dev.ts
export const devConfig = {
  env: 'dev',
  account: process.env.CDK_DEFAULT_ACCOUNT,
  region: 'us-east-1',
  domainName: 'dev-thc-mealplanner.tomlin.life',
  hostedZoneId: 'Z09770441ES3ZDJB86W5A',
  hostedZoneName: 'tomlin.life',
  lambda: {
    memory: 512,
    timeout: 30,
  },
  apiGateway: {
    throttle: { rateLimit: 50, burstLimit: 100 },
  },
};
```

### Prod

```typescript
// config/prod.ts
export const prodConfig = {
  env: 'prod',
  account: process.env.CDK_DEFAULT_ACCOUNT,
  region: 'us-east-1',
  domainName: 'thc-mealplanner.tomlin.life',
  hostedZoneId: 'Z09770441ES3ZDJB86W5A',
  hostedZoneName: 'tomlin.life',
  lambda: {
    memory: 512,
    timeout: 30,
  },
  apiGateway: {
    throttle: { rateLimit: 50, burstLimit: 100 },
  },
};
```

---

## Deployment

### Prerequisites

```bash
# One-time CDK bootstrap
npx cdk bootstrap aws://<ACCOUNT_ID>/us-east-1
```

### Commands

Current deployed frontends:

- Dev: `https://dev-thc-mealplanner.tomlin.life`
- Prod: `https://thc-mealplanner.tomlin.life`

```bash
# Deploy all stacks to dev
npx cdk deploy --all --context env=dev

# Deploy specific stack
npx cdk deploy ApiStack --context env=prod

# Diff before deploy
npx cdk diff --context env=prod

# Destroy (dev only)
npx cdk destroy --all --context env=dev
```

### Stack Dependencies

```
SecretsStack (no dependencies)
     ↓
AuthStack (no dependencies)
     ↓
DataStack (no dependencies)
     ↓
ApiStack (depends on: AuthStack, DataStack, SecretsStack)
     ↓
FrontendStack (depends on: ApiStack, DataStack)
     ↓
NotificationsStack (no dependencies)
```

---

## CI/CD Integration

See `MILESTONES.md` for the full GitHub Actions pipeline. Key infrastructure-related jobs:

1. **On PR**: `cdk diff` — shows proposed infrastructure changes
2. **On merge to main**: `cdk deploy --all --context env=dev` (auto-deploy to dev)
3. **On release tag**: `cdk deploy --all --context env=prod` (deploy to prod)
4. **Frontend deploy**: Build Vite app → `aws s3 sync` → CloudFront invalidation

---

## AWS MCP Server

For Copilot coding agents working on infrastructure:

- Use the **AWS IaC MCP Server** from `https://github.com/awslabs/mcp`
- The CDK-specific MCP server is deprecated; the AWS IaC MCP replaces it
- Enables agents to understand CDK constructs, CloudFormation mappings, and best practices

---

## Monitoring

- **CloudWatch Logs**: Lambda function logs (auto-created)
- **CloudWatch Metrics**: API Gateway 4xx/5xx, Lambda duration/errors/throttles
- **CloudWatch Alarms**: Alert on Lambda error rate > 5% (sends to SES)
- **X-Ray**: Disabled by default (enable for debugging if needed — adds cost)
- **Cost alerts**: AWS Budgets alarm at $15/month threshold
