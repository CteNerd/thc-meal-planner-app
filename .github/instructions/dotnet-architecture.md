# Instruction: dotnet-architecture

## Scope
Use for backend solution structure and dependency boundaries.

## Rules
- Keep API, core/domain, and infrastructure concerns separated.
- Depend inward (outer layers reference inner layers, not vice versa).
- Hide AWS SDK details behind interfaces.
- Use options pattern for configuration binding.
- Keep domain logic independent of transport/storage models.

## Quality
- Prefer small composable services over monolithic handlers.
- Test domain and service logic independently from transport layer.
