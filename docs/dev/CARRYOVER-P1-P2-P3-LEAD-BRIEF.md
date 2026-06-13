# Lead hand-off — clear the Phase 1 / 2 / 3 carryover backlog (sequenced)

> Drop this whole file into a fresh Claude Code lead session as the opening prompt. It is self-contained.

## Your mission
Drive the **complete Phase 1 / 2 / 3 carryover backlog to done**, in dependency order, wave by wave, using the standard multi-agent pipeline. Backend for these phases is essentially complete and merged — **this work is ~90% frontend** (the gamification screens were never built) plus a few small full-stack items and three product decisions.

## Read first (mandatory, in this order)
1. `CLAUDE.md` — the shared rulebook (module isolation, `BaseResponse`/`Successed`, no UoW, design-patterns-ask-first, no teacher role, 4 subjects, parent-driven onboarding).
2. `docs/dev/HANDOFF.md` — the shared dev memory. **Update it before each PR.**
3. The carryover source docs (these define the items below precisely):
   - `tasks/Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md`
   - `tasks/Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md`
   - `docs/briefs/phase-1-2-3-gap-after-p7.md` (the post-Phase-7 gap map — the master view)
   - `docs/briefs/phase-1-2-fe-gap-analysis.md`, `docs/briefs/phase-1-design-gap-analysis.md`, `docs/briefs/phase-1-backend-gap-analysis.md`
4. The relevant `user-stories/` files for each story ID below (source of truth for acceptance criteria).

## Environment
- The live repo is the **WSL2 checkout at `~/projects/learnexia`** — run Claude Code inside WSL. The Windows checkout (`e:\Wrokspace\Learnexia`) is abandoned; Ubuntu CI is the source of truth.
- Frontend = Turborepo monorepo, Expo universal student app (web PWA + native), Tamagui, TanStack Query + Zustand. Build/run the web PWA + backend API for the e2e stage.
- **Before starting:** there are 6 uncommitted modified `P7_*` integration-test files on `qc/phase-7-backend`. Stash or land those first so you start from a clean tree on `main`.

## Pipeline to use (per CLAUDE.md)
For each story/batch: `analyzer → planner → (designer for any UI surface) → implementers (db-migration / backend-feature / frontend) → api-tester (if HTTP) / frontend-e2e-tester (if student-app UI) → security-auditor (if security-sensitive) → reviewer → committer`. One `feat/<StoryID>` branch per story; committer opens the PR; never merge to `main` yourself unless told. Run independent batches in parallel (Mode A); independent sibling stories may use git worktrees per `docs/dev/PARALLELISM.md`.

---

## DECIDE THESE THREE FIRST (they gate downstream work)
Ask the user/product before building — do not guess:
1. **Matching-question answer-payload shape** (gates P2-06/07 BE comparator, seed, and the FE pairing UI). Propose e.g. `{ pairs: [{ leftId, rightId }] }` and get sign-off.
2. **Attempt-history scope** (G5 / P2-09) — does it belong to Phase 2 now, or defer to Phase 5 parent analytics? If deferred, drop it from this backlog.
3. **Marketing landing ar/RTL** (CO-FE-4) — confirm it is not intentionally EN-first-only before building the Arabic/RTL layout.

---

## SEQUENCED WAVES

### Wave A — Phase 1/2 FE polish (small, high-value, all backend-ready; start here)
- **P1-10-FE-6** — map `LoginTooManyFailedAttempts` + `LoginAccountDeactivated` to distinct sign-in UI messages (backend already returns both).
- **P1-11-FE-15/16** — Register CAPTCHA UI: send `captchaToken` when the server advertises the requirement (backend `RegisterParentCommand.CaptchaToken` + verifier already merged, P1-13-BE-6).
- **P1-11-FE-9** — Reports page: chart-less KPIs + subject-mastery bars + date-range selector + "Send Report" action (charts defer to P5-05; backend endpoints exist).
- **CO-FE-4** — marketing landing Arabic + RTL layout (only if decision #3 says yes).
- Finish P1-12-FE if still open: **Batch 3** (Google OAuth + RTL/dark QA) on `feat/P1-12-FE` — confirm merged; set backend `GoogleAuth__ClientId`. Note the lead-accepted implicit-grant→PKCE pre-production debt (do not re-architect now).

### Wave B — Phase-3 gamification frontend (the bulk of the work)
First **rebase the stale P4-08 motion branch** (`feat/P4-08-gamification-screens-motion`, ~97 commits behind `main`, predates P7/P8) onto `main`, or re-cut it — it carries the Reanimated motion system + confetti + `useReduceMotion`. Then build the screens (each backend is merged ✅):
- **P4-02-FE** — XP + level-up moment screen.
- **P4-03-FE** — daily streak calendar / milestone screen.
- **P4-04-FE** — hearts + Practice Mode regain flow.
- **P4-05-FE** — badge collection screens (consume `GET /api/Gamification/Badges/Me`; zero UI today).
- **P4-07-FE** — weekly league standings screen (only inline `LeaguePreviewRow` exists today).
- **P4-09-FE** — re-engagement notification / in-app nudge surface (Notifications.Inbox backend merged).
- **P4-11-FE** — streak-freeze + timed-events + weekly-challenges screens.
- **P4-06-FE** — daily/weekly mission list + progress UI. **BLOCKED** until **P4-06-BE** lands; finish that backend first, then build.
- P4-10-FE — none (Redis realtime is a server-side perf layer, no FE contract by design).

### Wave C — Matching questions, full-stack (only after decision #1)
- **CO-BE-1** — define the Matching payload (decision #1).
- **CO-BE-2** — `AnswerComparator` order-independent pair-mapping equality (replace the current `OrdinalIgnoreCase` fall-through at `Learning.Domain/Services/AnswerComparator.cs`).
- **CO-BE-3** — seed ≥1 Matching question in the demo curriculum (today only MCQ is seeded).
- **CO-BE-4** — api-tester: submit Matching answer end-to-end (correct / wrong / malformed→422).
- **CO-FE-5 (P2-06-FE-2)** — real tap/drag pairing UI (replace the "coming soon" stub that submits an empty payload).

### Wave D — E2E + QA close-out
- **CO-FE-6** — extend the Playwright harness (`tests/e2e/`) to cover: Reports, locked-login messaging, register-with-CAPTCHA, landing ar/RTL, and the Matching quiz flow. RTL/ar + en, auth/role routing, happy + error paths.

---

## Definition of done for the whole backlog
Every story: acceptance criteria met, reviewer PASS, api-tester/frontend-e2e green where applicable, security-auditor clear on sensitive items, PR opened. `docs/dev/HANDOFF.md` updated in the same PR. The three decisions recorded in HANDOFF.md. Anything still deferred (e.g. attempt-history if pushed to Phase 5) explicitly logged as carry-forward, not silently dropped.

## Out of scope (do not pull in)
P7-09/10/11 (blocked on unbuilt P3-01/02, P5-03, P6-02), the OAuth PKCE re-architecture, native avatar picker / native OAuth client IDs, and the frontend app-side i18n phase unless the user adds them.
