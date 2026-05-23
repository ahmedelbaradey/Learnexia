# Handoff — Phase 1 web frontend + dev environment

> Living handoff for leads/agents picking up the web frontend work. Last updated 2026-05-23.
> Captures what's done, the decisions, the load-bearing config, and what's next. If you change any of these, update this file.

## TL;DR
- The repo now runs natively in **WSL2** (`~/projects/learnexia`). Clean install + `dotnet build` + Expo web/native bundling are validated.
- The Expo **student-app web** now boots, translates (ar/en), and talks to the backend end-to-end (register/login → 200 + JWT).
- **P1-11** (parent web pages, pixel-perfect from `design-system/screenshots/`) is planned + two screens built: **Login** and **Register**.
- All **new backend** the design implies is deferred to **P1-12 "Batch 2"** (Identity-scoped, parallel-safe with the Phase 2 BE lead) — see "For the backend lead".

## ⚠️ Load-bearing config — do NOT "clean up"
These exist because the WSL clean install drifts dependencies past the Expo SDK 52 pins. Removing them reintroduces a hard crash.
- **`.npmrc` → `auto-install-peers=false`** — stops `*` / `^18||^19` peers grabbing **react-dom 19 / expo 56**, which breaks React 18 ("Should have a queue" hook crash). Requires `@babel/preset-env` to be an explicit dep of student-app (it is).
- **root `package.json` → `pnpm.overrides`**: `inline-style-prefixer ^6.0.4` (keeps web SSR resolving past rnw 0.21's v7), `react`/`react-dom` `18.3.1`.
- **i18n is initialized at module load** in `apps/student-app/app/_layout.tsx` (NOT in a useEffect) — react-i18next changes its hook count unready→ready, so initializing mid-mount crashes. Keep `initI18n()` at module scope.
- **i18n resources are one flat namespace** (`packages/shared/src/i18n/config.ts`) — components use dotted keys like `t('auth.login.title')`. `i18next ^24` / `react-i18next ^15.4` aligned across student-app + `@learnexia/shared` (a major mismatch caused a duplicate react-i18next instance).
- **Backend error envelopes are camelCase** — `ErrorHandlerMiddleWare` serializes with `JsonNamingPolicy.CamelCase` so error responses match the `BaseResponse` success shape (the typed client parses them).

## How to run the stack (dev)
1. **Postgres (pgvector)** — `docker compose -f docker/docker-compose.yaml up -d postgres` (or an existing pgvector container on `localhost:5432`, DB `Learnexia`, `postgres/admin`). Redis is **not** required for dev (connection string empty).
2. **Backend** — from `backend/src/Host/Learnexia.Host`:
   `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 AllowedOrigins=http://localhost:8081,http://127.0.0.1:8081 dotnet run --no-launch-profile`
   (HTTP avoids the untrusted dev cert in WSL; `AllowedOrigins` must list the web origin because CORS uses `AllowCredentials`.)
3. **Frontend** — from `apps/student-app`: `npx expo start --port 8081`. The API base URL is set via `apps/student-app/.env.local` (`EXPO_PUBLIC_API_BASE_URL=http://localhost:5080`, gitignored). Web at http://localhost:8081; LAN/device via `exp://<lan-ip>:8081`.
4. Default locale is **Arabic** (product is Arabic-first). Default theme is **dark**.

## What's built / merged to main
- Dev-env + bootstrap fixes (deps, i18n, auth error handling) — earlier PRs.
- **P1-11 planning docs** (story, tasks, pixel audit, designer pixel-perfect rule) + **P2-12** (settings tabs) + **P1-12** (Batch-2 BE) + the **gap analysis**.
- **Login** screen pixel-perfect (split layout, persona toggle, social buttons UI-only, theme/lang switches) + shared `SplitFormScaffold`.
- **Register** screen pixel-perfect + `packages/ui` `CheckboxField` (pending merge — see PR list).

## Key decisions (so you don't relitigate them)
- **Pixel-perfect to `design-system/screenshots/`** is the bar. The `designer` agent has a rule: when a capture exists it's the highest-priority target (cite it, match it, express in `--lx-*` tokens). See `.claude/agents/designer.md`.
- **Subjects = Math / Science / Arabic / English** everywhere (the dashboard/reports captures show "Reading"/"Art" — that's mock data; use the 4 product subjects).
- **Scope trims:** Child Home → **P2-09** (not P1-11); secondary Settings tabs (Notifications/Linked/Security/Plan) → **P2-12** (back + front).
- **All new backend → P1-12 "Batch 2"** (deferred): profile/`Me` enrichment, avatar upload, OAuth, password reset, **update-child**, **register country + terms-consent**. FE ships these surfaces **UI-first** (placeholder/disabled) and lights them up when the backend lands.
- Per CLAUDE.md: **ask before adding any design pattern**; mirror existing shapes (Catalog backend, existing component/hook shapes frontend).

## For the backend lead (P1-12, Batch 2)
All Identity-module-scoped, parallel-safe with your Phase 2 BE work. Stories + tasks:
- `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md` + `tasks/Backend/Phase-1-Foundation/P1-12-BE.md`.
- Gaps found while building the UI: **profile read/update + enriched `/Me`** (no `Phone` column today), **avatar upload** (no storage/`AvatarUrl`), **OAuth** (Google/Apple/Microsoft), **password reset**, **update-child** (no UpdateChild command exists), **register country + terms-consent** (`RegisterParentCommand` takes only `{email,password,fullName}`).
- Source analysis: `docs/briefs/phase-1-design-gap-analysis.md`.

## What's next (web FE)
- **My Children** (sidebar + family summary + child cards; needs `Avatar` + `KPIStatCard` primitives — see `P1-11-FE-14`).
- **Splash** polish; then Dashboard / Reports / Settings / Landing.
- Remaining shared primitives (`P1-11-FE-14`): Tabs, Avatar, Switch, Sidebar, KPIStatCard, PasswordStrengthMeter.

## Workflow notes
- Branch per change; **PRs to main**, the user merges. **Don't stack PRs on an unmerged base and then merge the base first** — the stacked changes get stranded (this happened to Register; it was re-PR'd straight to main). Now that Login is in main, branch new screens **off main**.
- Git identity isn't set in this WSL checkout — commits use a per-invocation `-c user.name/email` override (`Ahmed Elbaradey <elbaradeyahmed1985@gmail.com>`); set it permanently if you prefer.
- Pixel-perfect verification needs a browser; headless Chromium wouldn't download in this env, so screenshot review has been done by the human. The error overlay's **Log 1 of N** is the root error (later logs cascade).
