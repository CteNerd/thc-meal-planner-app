# Phase 6 Checklist and Backlog

Living tracker for AI chatbot delivery with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- Mac Mini lane: deployed OpenAI secret/runtime validation, latency/error-path checks, and real-session safety verification.
- Codespaces lane: chat API contracts, function-calling orchestration, chat UI, conversation history support, and automated tests.

## Phase Status Summary

- Codespaces implementation status: Done.
- Mac Mini automated deployed/runtime validation status: Done.
- Remaining Phase 6 work: authenticated real-session behavior verification with Cognito MFA user.

## Check-In Protocol

Update this file after each subphase commit and at least daily.

Check-in template:

- Lane: Mac Mini or Codespaces
- Task: Milestone item number(s)
- Status: In Progress / Blocked / Done
- Contracts touched: API routes, data model keys/indexes, env vars, auth assumptions
- Blockers or handoff requests

## Check-In Log

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.1, 6.4, 6.6, 6.7 (kickoff slice)
- Status: In Progress
- Contracts touched: Added authenticated chat endpoints (`POST /api/chat/message`, `GET /api/chat/history`), chat request validation + message sanitization, baseline domain guardrails and destructive-action confirmation prompts, chat history persistence model (`ChatHistoryMessageDocument`) with 30-day TTL, and frontend chat API/page with message bubbles and typing indicator.
- Blockers or handoff requests: OpenAI function-calling tool orchestration (6.2), dynamic system prompt data injection (6.3), and executable destructive-action confirmations (6.5) are pending implementation. Mac Mini lane should validate deployed OpenAI secret wiring and runtime behavior once those contracts land.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.2 (initial function-calling orchestration)
- Status: In Progress
- Contracts touched: Extended chat service to send OpenAI tool definitions and execute first-pass function calls against service layer (`generate_meal_plan`, `search_recipes`, `create_recipe`, `manage_grocery_list` add/list, `get_nutritional_info`, `manage_pantry` add/list); action execution now records structured chat actions in persisted history.
- Blockers or handoff requests: Tool set is partial versus full Phase 6 target and currently executes a single tool call per turn. Follow-up needed for `modify_meal_plan`, `update_profile`, destructive action execution with explicit confirm/cancel state, and dynamic prompt context from profiles/dependents/current plan/grocery state.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.3 (dynamic prompt context)
- Status: In Progress
- Contracts touched: Chat system prompt now includes runtime context from authenticated user profile, dependent profiles, active meal plan summary, and active grocery list progress before invoking OpenAI.
- Blockers or handoff requests: Prompt context is currently text summary only; follow-up needed to include richer constraint serialization (macro targets/cooking constraints) and explicit allergy-safe recipe filtering in tool execution paths.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.5 (confirmation execution flow) + 6.4 UX increment
- Status: In Progress
- Contracts touched: Added backend confirm/cancel reply handling that resolves pending destructive confirmations per conversation and executes clear-completed grocery cleanup on confirm when applicable; added chat UI confirm/cancel controls on assistant confirmation messages.
- Blockers or handoff requests: Pending-confirmation payload is still intent-inferred for generic destructive messages; follow-up needed for explicit structured pending action payloads and broader destructive operations coverage.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.2 additional tool coverage (`update_profile`)
- Status: In Progress
- Contracts touched: Added `update_profile` tool definition and execution path in chat service with family-scope checks and safe partial updates for profile fields.
- Blockers or handoff requests: Profile tool currently updates a limited subset of fields and does not yet support dependent profile mutation or validator-backed patch semantics.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.2 additional tool coverage (`modify_meal_plan`)
- Status: In Progress
- Contracts touched: Added `modify_meal_plan` tool handler that swaps a meal slot in the active plan (explicit or suggested replacement) and regenerates grocery list afterwards.
- Blockers or handoff requests: Swap path currently updates full plan payload in one write and does not yet expose a dedicated chat-level swap confirmation card for destructive replacements.

### 2026-03-28 - Codespaces lane

- Lane: Codespaces
- Task: 6.1, 6.2, 6.4, 6.5, 6.6, 6.7 completion pass
- Status: Done
- Contracts touched: Added `POST /api/chat/message/stream` SSE endpoint, structured pending-confirmation payloads (`ToolName`, `ArgumentsJson`) with deterministic confirm/cancel execution, multi-tool execution handling per turn, markdown rendering in chat UI, and hard allergy/exclusion filtering in recipe suggestion and meal-plan tool paths.
- Blockers or handoff requests: No implementation blockers in Codespaces lane. Mac Mini lane should execute deployed validation checklist for secrets/runtime/authenticated behavior.

### 2026-03-28 - Mac Mini lane

- Lane: Mac Mini
- Task: Phase 6 deployed/runtime validation
- Status: Done
- Contracts touched: Validated deployed dev stack outputs (`DistributionDomainName=d3ugym4rb87yys.cloudfront.net`, `ApiUrl=https://uryj6zpbfj.execute-api.us-east-1.amazonaws.com/dev/`), CloudFront/API health path behavior, unauthenticated auth challenge on `/api/profile`, CORS preflight for `/api/chat/message`, and Lambda environment/runtime wiring for OpenAI + Cognito + table/image configuration.
- Blockers or handoff requests: Automated validation is complete. Remaining manual verification is an authenticated Cognito MFA chat session because the repo does not contain a non-interactive token acquisition path for the enrolled dev user.

## Status Board

| Item | Description | Primary Owner | Status | Evidence / Notes |
|---|---|---|---|---|
| 6.1 | Chat API endpoint | Codespaces | Done | `POST /api/chat/message` + `POST /api/chat/message/stream` + `GET /api/chat/history` implemented with validation and auth scoping. |
| 6.2 | OpenAI function calling (8 functions) | Codespaces | Done | All eight tool definitions wired with service-layer handlers: meal plan generate/modify, recipe search/create, grocery manage, pantry manage, profile update, nutrition summary. |
| 6.3 | System prompt builder | Codespaces | Done | Runtime context injected from user profile, dependents, active meal plan, and grocery list state. |
| 6.4 | Chat UI | Codespaces | Done | Bubble interface, typing indicator, markdown rendering, and confirmation controls implemented. |
| 6.5 | Confirmation flow | Codespaces | Done | Confirm/cancel flow implemented with structured pending action payloads and executable destructive action path. |
| 6.6 | Conversation history (30-day TTL) | Codespaces | Done | Chat history storage/read endpoint and 30-day TTL persistence field implemented. |
| 6.7 | Safety guardrails | Codespaces | Done | Topic restriction, input sanitization, and allergy/exclusion-safe filtering in tool handlers implemented. |

## Automated Deployed Validation Evidence

- Frontend root via CloudFront: `GET / -> 200`.
- Health endpoint via CloudFront and direct API: `GET /api/health -> 200`.
- Protected profile endpoint via CloudFront and direct API: `GET /api/profile -> 401` when unauthenticated.
- CORS preflight: `OPTIONS /api/chat/message -> 204` and `OPTIONS /api/health -> 204` with wildcard origin, methods, and headers.
- Lambda configuration: `thc-meal-planner-dev-api-handler`, runtime `provided.al2023`, architecture `arm64`, timeout `30`, memory `512`; required env vars present for OpenAI secret ARN, Cognito IDs, table prefix, and recipe images bucket.
- Recent logs: earlier same-day `Runtime.InvalidEntrypoint` and `NullReferenceException` events exist in older streams, but latest stream shows healthy `/api/health -> 200` and `/api/profile -> 401` request handling with no current startup/runtime failure.
- Validation correction: `/api/profiles/me` is not a deployed backend route in this codebase; CloudFront returns SPA HTML `200` for that path, so `/api/profile` is the correct protected-route check.

## Milestone Criteria Tracking

- [x] Chatbot can generate meal plan via conversation
- [x] Chatbot can add recipes to cookbook
- [x] Chatbot respects allergy constraints in all suggestions
- [x] Destructive actions require confirmation
- [x] 30-day history accessible and auto-deleted

Phase 6 implementation and automated deployed validation are complete. Remaining manual validation is a real authenticated Cognito MFA chat session before Phase 7 work begins.
