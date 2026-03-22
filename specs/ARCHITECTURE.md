# Architecture Specification

## System Overview

The THC Family Meal Planner is a serverless web application built on AWS, designed for a 2-person household with cost-efficiency as a primary constraint (~$5–15/month). The system replaces a GitHub-based markdown meal planning workflow with a full-stack web experience.

---

## Architecture Diagram

```
                           ┌─────────────┐
                           │   Browser    │
                           │  (React SPA) │
                           └──────┬───────┘
                                  │ HTTPS
                           ┌──────▼───────┐
                           │  CloudFront  │ ← CDN + SSL + OAC
                           │ Distribution │
                           └──────┬───────┘
                                  │
                    ┌─────────────┼─────────────┐
                    │             │              │
             ┌──────▼──────┐ ┌───▼────┐  ┌──────▼──────┐
             │  S3 Bucket  │ │  API   │  │  S3 Bucket  │
             │  (Frontend) │ │Gateway │  │  (Images)   │
             │  Private    │ │ REST   │  │  Private    │
             └─────────────┘ └───┬────┘  └─────────────┘
                                 │
                          ┌──────▼──────┐
                          │   Lambda    │
                          │ ASP.NET 9   │
                          │  (.NET AOT) │
                          └──────┬──────┘
                                 │
          ┌──────────┬───────────┼───────────┬──────────┐
          │          │           │           │          │
    ┌─────▼────┐┌────▼────┐┌────▼────┐┌─────▼───┐┌────▼────┐
    │ DynamoDB ││ Cognito ││ OpenAI  ││   SES   ││ Secrets │
    │ (6 tbl)  ││  (Auth) ││  API    ││ (Email) ││ Manager │
    └──────────┘└─────────┘└─────────┘└─────────┘└─────────┘
```

---

## Component Architecture

### Frontend (React SPA)

```
frontend/src/
├── App.tsx                    # Root component, routing
├── main.tsx                   # Vite entry point
├── components/
│   ├── layout/                # Shell, Nav, Sidebar, Footer
│   ├── auth/                  # LoginForm, MFAChallenge, ProtectedRoute
│   ├── chat/                  # ChatPanel, MessageBubble, ActionConfirm
│   ├── meals/                 # MealGrid, DayCard, MealSlot
│   ├── recipes/               # RecipeCard, RecipeBrowser, RecipeDetail
│   ├── grocery/               # GroceryList, GroceryItem, SectionGroup
│   ├── profile/               # ProfileForm, DietaryPrefs, AllergenManager
│   └── shared/                # Button, Modal, LoadingSpinner, ErrorBoundary
├── pages/
│   ├── DashboardPage.tsx      # Current week overview
│   ├── MealPlanPage.tsx       # Weekly meal plan view
│   ├── CookbookPage.tsx       # Recipe library / cookbook
│   ├── RecipeDetailPage.tsx   # Single recipe view
│   ├── GroceryListPage.tsx    # Shared grocery list
│   ├── ProfilePage.tsx        # User profile editor
│   ├── LoginPage.tsx          # Auth flow
│   └── NotFoundPage.tsx       # 404
├── hooks/
│   ├── useAuth.ts             # Cognito auth state
│   ├── usePolling.ts          # Activity-based polling
│   ├── useChat.ts             # Chat panel state
│   └── useApi.ts              # API client wrapper
├── services/
│   ├── api.ts                 # Axios/fetch client with JWT
│   ├── auth.ts                # Cognito auth operations
│   └── polling.ts             # Page Visibility API integration
├── types/
│   ├── models.ts              # Domain type interfaces
│   └── api.ts                 # Request/response types
└── utils/
    ├── constants.ts           # Config values
    └── formatters.ts          # Date, nutrition formatting
```

### Backend (ASP.NET Core 9)

```
backend/
├── ThcMealPlanner.Api/              # Presentation layer
│   ├── Program.cs                   # Lambda bootstrap, DI config
│   ├── Controllers/
│   │   ├── ProfileController.cs     # User profile CRUD
│   │   ├── MealPlanController.cs    # Meal plan CRUD
│   │   ├── RecipeController.cs      # Recipe/Cookbook CRUD
│   │   ├── GroceryListController.cs # Grocery list operations
│   │   ├── ChatController.cs        # AI chat endpoint
│   │   └── HealthController.cs      # Health check
│   ├── Middleware/
│   │   ├── JwtValidationMiddleware.cs
│   │   ├── ErrorHandlingMiddleware.cs
│   │   └── RequestLoggingMiddleware.cs
│   └── Filters/
│       └── ValidateModelFilter.cs
│
├── ThcMealPlanner.Core/            # Domain layer
│   ├── Models/
│   │   ├── UserProfile.cs
│   │   ├── MealPlan.cs
│   │   ├── Recipe.cs
│   │   ├── GroceryList.cs
│   │   ├── ChatMessage.cs
│   │   └── Favorite.cs
│   ├── Interfaces/
│   │   ├── IUserProfileRepository.cs
│   │   ├── IMealPlanRepository.cs
│   │   ├── IRecipeRepository.cs
│   │   ├── IGroceryListRepository.cs
│   │   ├── IChatHistoryRepository.cs
│   │   ├── IConstraintEngine.cs
│   │   └── IChatService.cs
│   ├── Services/
│   │   ├── MealPlanService.cs
│   │   ├── ConstraintEngine.cs
│   │   ├── IngredientAggregationService.cs
│   │   └── NutritionService.cs
│   └── Validators/
│       ├── UserProfileValidator.cs
│       ├── MealPlanValidator.cs
│       └── RecipeValidator.cs
│
├── ThcMealPlanner.Infrastructure/   # Infrastructure layer
│   ├── DynamoDb/
│   │   ├── UserProfileRepository.cs
│   │   ├── MealPlanRepository.cs
│   │   ├── RecipeRepository.cs
│   │   ├── GroceryListRepository.cs
│   │   └── ChatHistoryRepository.cs
│   ├── OpenAI/
│   │   ├── ChatService.cs
│   │   ├── FunctionDefinitions.cs
│   │   └── SystemPromptBuilder.cs
│   ├── S3/
│   │   └── ImageStorageService.cs
│   ├── SES/
│   │   └── EmailNotificationService.cs
│   └── Secrets/
│       └── SecretsManagerService.cs
│
└── ThcMealPlanner.Tests/           # Test project
    ├── Unit/
    ├── Integration/
    └── TestHelpers/
```

---

## Data Flow

### Meal Plan Generation (AI)

```
User → Chat UI → POST /api/chat
  → Lambda → ChatService → OpenAI API
    → Function call: generate_meal_plan(week, constraints, profiles)
    → ConstraintEngine validates constraints
    → MealPlanService creates plan
    → RecipeRepository resolves recipes
    → NutritionService calculates summaries
    → MealPlanRepository saves to DynamoDB
  → Response with plan summary + confirmation
  → EmailNotificationService sends completion email (optional)
```

### Grocery List Sync

```
User A checks off item → PUT /api/grocery-lists/{id}/items/{itemId}/toggle
  → Lambda → GroceryListRepository → DynamoDB conditional update (version check)
  → Success → updated version returned

User B's browser (grocery page active):
  → Page Visibility API detects tab is visible
  → Polling every 5s → GET /api/grocery-lists/{id}/poll?since={lastUpdate}
  → Returns only changed items since timestamp
  → UI updates with changes, shows "checked by [name]" indicator
```

### Recipe Image Upload

```
User → Upload button → POST /api/recipes/{id}/image
  → Lambda → S3 → generate pre-signed PUT URL (5-min expiry)
  → Return pre-signed URL to frontend
  → Frontend uploads directly to S3 via pre-signed URL
  → Frontend calls PUT /api/recipes/{id} with S3 key
  → Recipe record updated with imageKey
```

---

## Key Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Compute | Lambda (.NET AOT) | $0 idle, pay-per-request, AOT for <3s cold starts |
| Database | DynamoDB on-demand | $0 idle vs $43/month Aurora, key-based access patterns |
| Frontend hosting | CloudFront + S3 (OAC) | CDK learning, private bucket, global CDN |
| Auth | Cognito only | 2 users, no ASP.NET Identity duplication needed |
| Real-time sync | REST polling (5s) | No WebSocket cost, Page Visibility API pauses when hidden |
| AI integration | OpenAI function calling | Structured CRUD via chat, streaming chunked responses |
| IaC | CDK (TypeScript) | Professional AWS learning, better than raw CloudFormation |
| Secrets | AWS Secrets Manager | Runtime secrets (OpenAI key); GitHub Secrets for CI/CD |

---

## Network Architecture

### CloudFront Behaviors

| Path Pattern | Origin | Cache Policy |
|-------------|--------|-------------|
| `/` and `/index.html` | S3 (frontend) | No cache (SPA routing) |
| `/assets/*` | S3 (frontend) | 1 year (hashed filenames) |
| `/api/*` | API Gateway | No cache (pass-through) |

### CORS Configuration

- **Allowed Origin**: `https://{cloudfront-domain}` (single origin, no wildcards)
- **Allowed Methods**: GET, POST, PUT, DELETE, OPTIONS
- **Allowed Headers**: Authorization, Content-Type, X-Requested-With
- **Max Age**: 3600s

### API Gateway

- **Type**: REST API (not HTTP API — need request validation, usage plans)
- **Stage**: `v1`
- **Throttling**: 100 req/s burst, 50 req/s sustained
- **Lambda integration**: Proxy integration (all routing handled by ASP.NET)

---

## Scalability Considerations

This is a 2-user family application. The architecture is deliberately simple:

- **No auto-scaling configuration needed** — Lambda scales automatically
- **No read replicas** — DynamoDB on-demand handles the load
- **No caching layer** — Response times are fast enough without ElastiCache
- **No queue/async processing** — All operations are synchronous except email
- **Single-region deployment** — us-east-1 (Southeast US, closest to household)

If the application ever grew beyond the family, the serverless architecture would scale naturally — but that's not the design goal.

---

## Error Handling Strategy

- **API responses**: RFC 9457 Problem Details format
- **Lambda errors**: Structured JSON logging to CloudWatch
- **DynamoDB throttling**: Exponential backoff with jitter (AWS SDK built-in)
- **OpenAI failures**: Graceful degradation — return "AI unavailable" message, all UI operations still work
- **Frontend**: React Error Boundary at route level, toast notifications for API errors
- **Optimistic concurrency**: Version field on GroceryList prevents lost updates; conflict returns 409

---

## Environments

| Environment | Purpose | Infrastructure |
|-------------|---------|---------------|
| `dev` | Development and testing | Separate DynamoDB tables, API Gateway stage |
| `prod` | Production | Full CloudFront + S3 + monitoring |

Both environments are deployed via CDK with parameterized stack names. No local DynamoDB for development — use `dev` environment against real AWS services (cost-effective at this scale).
