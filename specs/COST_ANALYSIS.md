# Cost Analysis

## Overview

Target monthly cost: **$5–15** for a 2-person household with moderate daily use. All services use serverless/on-demand pricing to avoid idle costs.

---

## Per-Service Breakdown

### 1. AWS Lambda

| Metric | Estimate |
|--------|----------|
| Requests/month | ~10,000 (API calls + chatbot + polling) |
| Avg duration | 200ms |
| Memory | 512 MB |
| Free tier | 1M requests + 400,000 GB-seconds/month |
| **Monthly cost** | **$0.00** (well within free tier) |

> Free tier is always-free, not 12-month trial.

### 2. API Gateway (REST)

| Metric | Estimate |
|--------|----------|
| Requests/month | ~10,000 |
| Free tier | 1M requests/month (first 12 months) |
| After free tier | $3.50 per million |
| **Monthly cost** | **$0.00** (free tier) → **$0.04** (after) |

### 3. DynamoDB (On-Demand)

| Metric | Estimate |
|--------|----------|
| Write request units/month | ~2,000 |
| Read request units/month | ~40,000 (includes grocery polling) |
| Storage | < 1 GB |
| Free tier | 25 WRU + 25 RRU/sec always-free |
| **Monthly cost** | **$0.00** (well within free tier) |

> On-demand pricing: $1.25/million WRU, $0.25/million RRU. Even without free tier, cost would be ~$0.01/month.

### 4. Amazon Cognito

| Metric | Estimate |
|--------|----------|
| Monthly active users | 2 |
| Free tier | 50,000 MAU (always-free for User Pool) |
| **Monthly cost** | **$0.00** |

### 5. CloudFront

| Metric | Estimate |
|--------|----------|
| Data transfer out/month | ~2 GB |
| Requests/month | ~50,000 |
| Free tier | 1 TB transfer + 10M requests/month (always-free) |
| **Monthly cost** | **$0.00** |

### 6. S3

| Metric | Estimate |
|--------|----------|
| Storage | < 1 GB (frontend + recipe images) |
| PUT requests/month | ~200 |
| GET requests/month | ~5,000 |
| Free tier | 5 GB storage + 20K GET + 2K PUT (first 12 months) |
| After free tier | ~$0.02/GB + request costs |
| **Monthly cost** | **$0.00** → **$0.03** (after free tier) |

### 7. OpenAI API (gpt-4o-mini)

| Metric | Estimate |
|--------|----------|
| Conversations/month | ~60 (2 per day average) |
| Avg input tokens/conversation | ~2,000 (system prompt + history) |
| Avg output tokens/conversation | ~500 |
| Input cost | $0.15 / 1M tokens |
| Output cost | $0.60 / 1M tokens |
| Meal plan generation (4x/month) | ~4,000 input + 2,000 output per generation |
| **Monthly cost** | **$0.02 – $0.05** |

> This is the primary variable cost. Heavy chatbot use could push to $0.50/month but extremely unlikely for 2 users.

### 8. AWS Secrets Manager

| Metric | Estimate |
|--------|----------|
| Secrets stored | 2 |
| API calls/month | ~10,000 (cached in Lambda) |
| Cost | $0.40/secret/month + $0.05/10K calls |
| **Monthly cost** | **$0.85** |

> This is the highest fixed cost. Consider using SSM Parameter Store SecureString ($0.00) if cost sensitivity is high. Trade-off: no automatic rotation capability.

### 9. Amazon SES

| Metric | Estimate |
|--------|----------|
| Emails/month | ~20 (meal plan notifications + security alerts) |
| Free tier | 3,000/month (from Lambda) |
| **Monthly cost** | **$0.00** |

### 10. CloudWatch

| Metric | Estimate |
|--------|----------|
| Log ingestion | < 1 GB/month |
| Metrics | Standard Lambda/API GW metrics (auto) |
| Alarms | 2 (error rate + budget) |
| Free tier | 5 GB ingestion, 10 metrics, 10 alarms |
| **Monthly cost** | **$0.00** |

### 11. ACM (SSL Certificate)

| **Monthly cost** | **$0.00** (always free for public certificates) |

---

## Monthly Summary

| Service | Year 1 (Free Tier) | After Year 1 |
|---------|-------------------|---------------|
| Lambda | $0.00 | $0.00 |
| API Gateway | $0.00 | $0.04 |
| DynamoDB | $0.00 | $0.00 |
| Cognito | $0.00 | $0.00 |
| CloudFront | $0.00 | $0.00 |
| S3 | $0.00 | $0.03 |
| OpenAI | $0.02 – $0.05 | $0.02 – $0.05 |
| Secrets Manager | $0.85 | $0.85 |
| SES | $0.00 | $0.00 |
| CloudWatch | $0.00 | $0.00 |
| ACM | $0.00 | $0.00 |
| **Total** | **$0.87 – $0.90** | **$0.92 – $0.95** |

---

## Cost Scenarios

### Minimal Use (~$1/month)
- 1 meal plan generation per week
- ~10 chatbot conversations/month
- Grocery list polling (5s when active, ~2 hours/week at store)

### Moderate Use (~$2–3/month)
- 1-2 meal plan generations per week
- ~30 chatbot conversations/month
- Daily grocery list usage
- 10-20 recipe additions/month

### Heavy Use (~$5–8/month)
- Multiple meal plan regenerations weekly
- 100+ chatbot conversations/month
- Frequent recipe imports with image uploads
- High grocery list polling

### Absolute Maximum (~$15/month)
- This would require sustained, unrealistic usage volume
- The $15 ceiling provides a generous safety margin
- AWS Budget alarm triggers at $15 as a safety net

---

## Cost Optimization Strategies

| Strategy | Impact | Status |
|----------|--------|--------|
| DynamoDB on-demand (vs. provisioned) | Eliminates idle costs | Default |
| Lambda ARM64 (Graviton) | 20% cheaper than x86 | Default |
| CloudFront Price Class 100 | Cheapest edge locations | Default |
| gpt-4o-mini (vs. gpt-4o) | ~10x cheaper per token | Default |
| SES sandbox mode | No email charges | Default |
| No provisioned concurrency | $0 idle Lambda cost | Default |
| Single Lambda (not per-route) | 1 function to maintain | Default |
| SSM Parameter Store option | Save $0.85/month Secrets Manager | Available if needed |

---

## AWS Budget Alarm

```typescript
// In CDK: set up a budget alarm
new budgets.CfnBudget(this, 'MonthlyBudget', {
  budget: {
    budgetName: 'thc-meal-planner-monthly',
    budgetType: 'COST',
    timeUnit: 'MONTHLY',
    budgetLimit: { amount: 15, unit: 'USD' },
  },
  notificationsWithSubscribers: [{
    notification: {
      comparisonOperator: 'GREATER_THAN',
      notificationType: 'ACTUAL',
      threshold: 80,     // Alert at 80% ($12)
      thresholdType: 'PERCENTAGE',
    },
    subscribers: [{
      address: 'roddy@example.com',
      subscriptionType: 'EMAIL',
    }],
  }],
});
```
