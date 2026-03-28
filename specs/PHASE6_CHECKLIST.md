# Phase 6 Checklist and Backlog

Living tracker for AI chatbot delivery with explicit parallel ownership across Mac Mini and Codespaces.

## Parallel Lanes

- Mac Mini lane: deployed OpenAI secret/runtime validation, latency/error-path checks, and real-session safety verification.
- Codespaces lane: chat API contracts, function-calling orchestration, chat UI, conversation history support, and automated tests.

## Phase Status Summary

- Codespaces implementation status: In Progress.
- Remaining Phase 6 work: function-calling orchestration, dynamic system prompt context, destructive action execution + confirmations, and deployed validation.

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

## Status Board

| Item | Description | Primary Owner | Status | Evidence / Notes |
|---|---|---|---|---|
| 6.1 | Chat API endpoint | Codespaces | In Progress | Initial POST/GET endpoints implemented; streaming response still pending. |
| 6.2 | OpenAI function calling (8 functions) | Codespaces | In Progress | Initial tool definitions + execution dispatcher implemented for core actions; remaining functions and multi-step tool loops pending. |
| 6.3 | System prompt builder | Codespaces | In Progress | Static safety/system prompt active; dynamic family/dependent/plan/grocery context injection still pending. |
| 6.4 | Chat UI | Codespaces | In Progress | Bubble-style chat page and send flow implemented; markdown rendering polish pending. |
| 6.5 | Confirmation flow | Codespaces | In Progress | Baseline destructive-intent confirmation prompt added; action cards + execution gating pending. |
| 6.6 | Conversation history (30-day TTL) | Codespaces | In Progress | History document model + read/write support implemented; conversation management endpoints pending. |
| 6.7 | Safety guardrails | Shared | In Progress | Topic restriction and message sanitization baseline implemented; deeper allergy/tool validation pending. |

## Milestone Criteria Tracking

- [ ] Chatbot can generate meal plan via conversation
- [ ] Chatbot can add recipes to cookbook
- [ ] Chatbot respects allergy constraints in all suggestions
- [~] Destructive actions require confirmation
- [~] 30-day history accessible and auto-deleted

`[~]` indicates baseline support exists but full milestone behavior is not complete yet.
