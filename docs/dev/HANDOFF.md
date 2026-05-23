# Handoff — Phase 1 web frontend + dev environment

> Living handoff for leads/agents picking up the web frontend + backend work. Last updated 2026-05-24 (added the Phase 1 backend gap analysis + P1-13 hardening story; earlier: Phase 7 Admin Console backlog + P1-12 Batch-2 pickup).
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
- **Phase 7 — Admin Console backlog** (PR #21, merged): 12 admin stories `P7-01..P7-12` (curriculum mgmt, user/account mgmt, content moderation, analytics/AI-safety oversight) — the feature set behind the P1-10 shell — each with BE + admin-dashboard (Next.js) task files in `…/Phase-7-Admin-Console/`. Added a real **`FR-ADM-1..12`** group to [SRS §4.9](../SRS.md) (note: `FR-ADM`, not `FR-AD` = Adaptivity) and expanded §3 + the goal matrix; all P7 stories trace to it. **Backlog/spec only — nothing implemented (all P7 rows in PROGRESS.md are 🔲).** Handoff/decisions for whoever builds it: [docs/briefs/P7-admin-console.md](../briefs/P7-admin-console.md) (PR #24).

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

## What's next (backend — P1-12 "Batch 2", my pickup)
> Owner: backend lead (me). Identity-module-scoped → **parallel-safe with the Phase 2 BE work**. Story: [P1-12](../../user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md) · Tasks: [P1-12-BE](../../tasks/Backend/Phase-1-Foundation/P1-12-BE.md) · Source: [phase-1-design-gap-analysis.md](../briefs/phase-1-design-gap-analysis.md). Mirror **Catalog**; `BaseResponse<T>`/`Successed`; no cross-module FK.
- **Not started yet.** Planned order: (1) **BE-3** migration — add `Phone` (+ `Country`, `AvatarUrl`) to Identity `User`; (2) **BE-1/BE-2** profile read/update + enriched `/Me`; (3) **BE-9** register `country` + terms-consent record (COPPA); (4) **BE-8** update/edit-child with family-scope authz (unblocks P1-11 edit-child); then the heavier, security-gated items: (5) **BE-4** avatar upload + storage abstraction, (6) **BE-5** OAuth (Google/Apple/Microsoft), (7) **BE-6** password reset.
- **`security-auditor` (BE-7)** gates upload/OAuth/reset before the reviewer — Critical/High block.
- **Two design-pattern decisions to raise with the lead first** (CLAUDE.md rule #8): the **file-storage abstraction** (BE-4, dev-local vs object-store) and the **OAuth provider abstraction** (BE-5) — name the pattern and wait for approval; don't introduce unilaterally.
- FE (P1-11) ships these surfaces **UI-first** (placeholder avatar, disabled social/forgot) and lights them up as each task merges; regenerate the `api-client` after BE-8/BE-9.

### P1-13 — Phase 1 backend hardening (new, from the backend gap analysis)
> A code-grounded **backend-only gap analysis of all Phase 1** (excluding P1-12) is at [docs/briefs/phase-1-backend-gap-analysis.md](../briefs/phase-1-backend-gap-analysis.md). It confirmed most suspected gaps are **already covered** (refresh rotation, sign-out revocation, RBAC `[Authorize]` + family/self-scope, JWT secret from env) and found **6 real gaps**, now broken down as **P1-13** ([story](../../user-stories/Phase-1-Foundation/P1-13-backend-hardening.md) · [P1-13-BE](../../tasks/Backend/Phase-1-Foundation/P1-13-BE.md)). Not started.
- **Gaps:** account lockout configured but never engaged (`SignInCommandHandler` passes `lockoutOnFailure:false`); sign-in leaks `ex.Message` + allows email enumeration; **Notifications can't send email** (`SendNotificationCommandHandler` throws — this is the only piece of the P1-12d reset chain P1-12 doesn't build); no env-driven Admin seed (only `superadmin@gmail.com` with committed `123Pa$$word!`); no CAPTCHA on register (tracked debt); no working registration-message send.
- **Sequencing:** email-delivery infra (BE-3) is a shared enabler for P1-12d **and** P5-04 — consider standing it up (possibly as its own story **P1-13a**) before P1-12d. Email-provider abstraction is an **ask-first** design-pattern decision.
- **Open decisions for the lead** (brief §5): email verification at registration in P1 or deferred? COPPA under-13 consent record (distinct from P1-12f terms-consent; **no entity today**)? Is P1-13 a Phase-1 story or a P6 hardening pull (rec: BE-1/2/4/5 in P1, BE-6 CAPTCHA may defer)?

## Workflow notes
- Branch per change; **PRs to main**, the user merges. **Don't stack PRs on an unmerged base and then merge the base first** — the stacked changes get stranded (this happened to Register; it was re-PR'd straight to main). Now that Login is in main, branch new screens **off main**.
- Git identity isn't set in this WSL checkout — commits use a per-invocation `-c user.name/email` override (`Ahmed Elbaradey <elbaradeyahmed1985@gmail.com>`); set it permanently if you prefer.
- Pixel-perfect verification needs a browser; headless Chromium wouldn't download in this env, so screenshot review has been done by the human. The error overlay's **Log 1 of N** is the root error (later logs cascade).
- **Activate the auto-load hook on first pull:** a committed `SessionStart` hook (`.claude/settings.json`) auto-loads this file into context — but if your session was already open when you pulled it, run **`/hooks`** once (or restart Claude Code / start a new session) to load it. New sessions after that pick it up automatically.
