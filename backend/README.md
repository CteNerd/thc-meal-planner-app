# Backend Scaffold

Current scaffold (Phase 1.2):

- .NET 9 solution at `ThcMealPlanner.sln`
- API project at `ThcMealPlanner.Api/`
- Core project at `ThcMealPlanner.Core/`
- Infrastructure project at `ThcMealPlanner.Infrastructure/`
- Test project at `ThcMealPlanner.Tests/`
- Health endpoint at `GET /api/health`
- Lambda hosting and Native AOT configuration in API project
- Cognito JWT bearer scaffold and protected `GET /api/session` endpoint

Next implementation tasks:

1. Replace placeholder Cognito settings with deployed stack outputs.
2. Add FluentValidation baseline and request validation conventions.
3. Add RFC 9457 error response shaping middleware.
4. Expand tests beyond health/auth baseline into real protected resources.
