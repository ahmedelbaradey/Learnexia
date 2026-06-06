# E2E tests — student-app web PWA (Playwright)

Browser-level end-to-end tests for the Expo / React Native Web student app. Owned by
the `frontend-e2e-tester` agent ([.claude/agents/frontend-e2e-tester.md](../../.claude/agents/frontend-e2e-tester.md));
runs after the `frontend` batch and feeds the `reviewer` gate.

## Prerequisites
1. **Backend** running at `http://localhost:5080` (it needs the Postgres stack — not
   auto-started here). From `backend/src/Host/Learnexia.Host`:
   ```
   ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 \
   AllowedOrigins=http://localhost:8081,http://127.0.0.1:8081 \
   dotnet run --no-launch-profile
   ```
2. **Browser** installed once: `pnpm --filter @learnexia/e2e install:browser`.

Playwright starts (or reuses) the **Expo web** server itself at `http://localhost:8081`,
reading `apps/student-app/.env.local` (`EXPO_PUBLIC_API_BASE_URL=http://localhost:5080`).

## Run
```
pnpm --filter @learnexia/e2e test          # headless
pnpm --filter @learnexia/e2e test:headed   # watch the browser
pnpm --filter @learnexia/e2e test:ui       # Playwright UI mode
pnpm --filter @learnexia/e2e report        # open the last HTML report
```
Override targets with `WEB_URL` / `API_URL` env vars.

## Conventions
- One spec per story: `specs/<StoryID>.spec.ts`.
- Selectors, in order: `getByTestId` (RN Web maps `testID` → `data-testid`), then
  `getByRole` / `getByLabel` (`accessibilityRole` → `role`, `accessibilityLabel` → `aria-label`).
  Avoid copy-based selectors — Arabic is the default locale. Missing a stable hook?
  Report the needed `testID` back to `frontend`; don't reach into CSS classes.
- Seed state via the API, assert via the UI. Keep specs hermetic (unique emails, no
  cross-spec order dependence).
