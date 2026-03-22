# Frontend Scaffold

Current scaffold (Phase 1.3):

- React 19 + TypeScript + Vite baseline
- TailwindCSS v4 wired through Vite
- Router shell with `/login` and `/dashboard`
- Placeholder auth service/context with TOTP login flow and route protection
- UI primitives for button, card, and input
- Starter test with Vitest + React Testing Library

Next implementation tasks:

1. Continue hardening native Cognito user pool SRP + refresh flow (no Amplify dependency).
2. Connect protected routes to real API data and auth headers.
3. Add responsive navigation for desktop and mobile.
4. Wire refresh-token cookie handling once backend/session infrastructure is live.
