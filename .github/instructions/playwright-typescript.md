# Instruction: playwright-typescript

## Scope
Use for end-to-end browser tests in TypeScript.

## Rules
- Cover high-value user journeys first.
- Use stable selectors and page object abstractions where helpful.
- Keep tests isolated and data-independent.
- Avoid arbitrary sleeps; use explicit waits on state transitions.
- Capture traces/screenshots for flaky or failed tests.

## Coverage Focus
- Auth flow with MFA.
- Meal planning and grocery collaboration flows.
- Chatbot confirmations for destructive actions.
