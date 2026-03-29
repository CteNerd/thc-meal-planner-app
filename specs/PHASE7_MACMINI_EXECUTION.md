# Phase 7 Mac Mini Execution Commands

Purpose: run credentialed/deployed validation and production execution tasks with minimal manual handling.

## 0) Preconditions

- AWS profile `thc` is configured.
- Repo is up to date on `main`.
- CI baseline checks are green or acceptable for planned deployment.

## 1) Identity and Stack Output Resolution

```bash
aws sts get-caller-identity --profile thc

aws cloudformation describe-stacks \
  --stack-name ThcMealPlanner-dev-Auth \
  --region us-east-1 \
  --profile thc \
  --query "Stacks[0].Outputs[].[OutputKey,OutputValue]" \
  --output table

aws cloudformation describe-stacks \
  --stack-name ThcMealPlanner-dev-Frontend \
  --region us-east-1 \
  --profile thc \
  --query "Stacks[0].Outputs[].[OutputKey,OutputValue]" \
  --output table
```

## 2) Deploy Dev API/Infra Changes

```bash
dotnet publish backend/ThcMealPlanner.Api/ThcMealPlanner.Api.csproj \
  -c Release -r linux-arm64 -p:PublishAot=false -p:SelfContained=true

cd infra
npm ci
export AWS_PROFILE=thc
export CDK_DEFAULT_REGION=us-east-1
export CDK_DEFAULT_ACCOUNT=425061472162
npx cdk deploy --all --context env=dev --require-approval never
cd ..
```

## 3) Publish Frontend to Dev and Invalidate CloudFront

```bash
# Replace values from stack outputs if they differ.
export DEV_USER_POOL_ID=us-east-1_OWufvWke8
export DEV_USER_POOL_CLIENT_ID=1a0hgiq7vfdc7id09ogv188alg
export DEV_BUCKET=thcmealplanner-dev-frontend-frontendbucketefe2e19c-q3gmyu83uoax
export DEV_DIST_DOMAIN=d3ugym4rb87yys.cloudfront.net

cd frontend
VITE_COGNITO_REGION=us-east-1 \
VITE_COGNITO_USER_POOL_ID="$DEV_USER_POOL_ID" \
VITE_COGNITO_CLIENT_ID="$DEV_USER_POOL_CLIENT_ID" \
npm run build

aws s3 sync dist "s3://$DEV_BUCKET" --delete --profile thc --region us-east-1
cd ..

DEV_DIST_ID=$(aws cloudfront list-distributions \
  --profile thc \
  --query "DistributionList.Items[?DomainName=='$DEV_DIST_DOMAIN'].Id | [0]" \
  --output text)

aws cloudfront create-invalidation \
  --distribution-id "$DEV_DIST_ID" \
  --paths '/*' \
  --profile thc
```

## 4) Automated Dev Validation

```bash
bash scripts/validate-deployment.sh "https://$DEV_DIST_DOMAIN" dev
```

Optional deployed header/PWA check:

```bash
python3 - <<'PY'
import urllib.request

base = 'https://d3ugym4rb87yys.cloudfront.net'
paths = ['/', '/manifest.webmanifest', '/sw.js', '/api/health']
headers_to_check = [
    'content-security-policy',
    'strict-transport-security',
    'x-content-type-options',
    'x-frame-options',
    'referrer-policy',
]

for path in paths:
    req = urllib.request.Request(base + path, method='GET')
    with urllib.request.urlopen(req, timeout=30) as response:
        print(path, response.getcode())
        headers = {k.lower(): v for k, v in response.headers.items()}
        for h in headers_to_check:
            print(' ', h, '=>', headers.get(h, '<missing>'))
PY
```

## 5) SES Runtime Validation (7.1)

Set a verified sender address in AWS SES and app config (`Notifications:FromEmail`) first.

Then validate endpoint with authenticated session:

```bash
# Example payloads for authenticated test calls:
# { "type": "meal-plan-ready", "weekStartDate": "2026-04-06" }
# { "type": "security-alert", "securityMessage": "Unrecognized sign-in attempt detected." }
```

Expected result: HTTP 202 from `/api/notifications/test` and delivery in mailbox.

## 6) Production Deploy (7.8)

Preferred path: run workflow [deploy-prod.yml](../.github/workflows/deploy-prod.yml) from GitHub Actions.

Manual local equivalent (if needed):

```bash
cd infra
export AWS_PROFILE=thc
export CDK_DEFAULT_REGION=us-east-1
export CDK_DEFAULT_ACCOUNT=425061472162
npx cdk deploy --all --context env=prod --require-approval never
cd ..

# Build and publish frontend with prod outputs, then invalidate prod distribution.
# Finally run:
# bash scripts/validate-deployment.sh "https://<prod-distribution-domain>" prod
```

## 7) Migration to Production (7.9)

Blocked until explicit user confirmation of migration records.

After confirmation:

```bash
# Dry run / validation first (no writes)
# Run migration command with prod targets
# Run post-migration integrity checks
```

Record all outcomes in [PHASE7_CHECKLIST.md](PHASE7_CHECKLIST.md).
