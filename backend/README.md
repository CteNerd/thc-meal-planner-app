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

## DynamoDB Environment Contract

Phase 2 backend data access uses the `DynamoDb` configuration section in API settings.

Required keys:

- `DynamoDb:PartitionKeyName` (default: `PK`)
- `DynamoDb:SortKeyName` (default: `SK`)
- `DynamoDb:Tables` dictionary for document-type to table-name mapping

Current document mappings used by profile/dependent APIs:

- `UserProfileDocument` -> users table
- `DependentProfileDocument` -> users table

Environment strategy:

- `appsettings.json` keeps shared defaults and no environment-specific table names.
- `appsettings.Development.json` defines dev table mappings.
- deployed environments should provide table mappings via env-specific config.

Example environment overrides:

- `DynamoDb__PartitionKeyName=PK`
- `DynamoDb__SortKeyName=SK`
- `DynamoDb__Tables__UserProfileDocument=thc-meal-planner-dev-users`
- `DynamoDb__Tables__DependentProfileDocument=thc-meal-planner-dev-users`
