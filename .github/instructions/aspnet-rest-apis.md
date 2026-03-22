# Instruction: aspnet-rest-apis

## Scope
Use for ASP.NET Core API endpoint and contract design.

## Rules
- Follow resource-oriented routes and HTTP semantics.
- Return RFC 9457 Problem Details for errors.
- Run FluentValidation for every command/request DTO.
- Enforce family-scoped authorization in service layer.
- Use pagination/filtering patterns for list endpoints.

## Response Design
- Keep response contracts deterministic.
- Document intentional breaking changes in PR notes.
