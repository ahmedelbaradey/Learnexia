# Session State — resume pointer

> **Purpose:** hand off in-flight work to a fresh Claude Code session on another device. Local auto-memory + the conversation transcript do **not** sync across machines — this file + `CLAUDE.md` + `docs/dev/HANDOFF.md` + the committed `docs/briefs/*` & `docs/plans/*` are what travel. Read those first.
> Last updated: 2026-06-06 (FE state reconciliation: Phase-1 + Phase-2 student FE confirmed merged to main; P8-04 FE corrected to not-started; next FE = Phase-3 gamification screens + app-side localization scoping).

## Where we are (2026-06-06)
**Phases 1, 2, 3 (backend) and Phase 8 — Localization (backend) are COMPLETE and merged to main.** Status board: `tasks/PROGRESS.md`.

### Frontend status (reconciled 2026-06-06 — see the reconciliation note at the top of `docs/dev/HANDOFF.md`)
- **Merged to main:** monorepo foundation + design-system/ui/api-client/shared packages; **Phase-1 student FE** (auth, parent onboarding + add-child, child login/`/Me` routing) + **admin sign-in shell P1-10**; **Phase-2 student FE** (browse subjects/skill-tree, lesson player, 4-type quiz, instant feedback, home dashboard — W11–W13, PRs #70/#71/#72/#74). P1-11 parent web = screen set built, `(parent)/index` + `reports` are intentional blank placeholders (🟡). P1-12 FE (profile save/avatar/OAuth/password-reset) = **not started**.
- **In-flight FE branch (unmerged):** `feat/P4-08-gamification-screens-motion` (gamification screens + motion infra; resumable, rebase onto main first).
- **Stale FE branch (decide):** `feat/design-system-pixel-align` (97 behind; carries brand-font runtime loading + RTL/Switch fixes — relevant to the localization font gap).
- **Localization (app-side, axis A) is PARTIALLY wired**, not greenfield: react-i18next + ar/en resources + RTL helpers + native-restart UX exist. Gaps: brand-font runtime loading (no `expo-font`/`useFonts` in student-app), UI-language switch only on Login (not persisted to `User.PreferredLanguage`), and **no learning-language (axis B) parent UX** — P8-04 FE is genuinely unbuilt.

- **Phase 8 — Localization** (learning language vs UI language): PR #88 (docs) + **#90** (impl P8-01/02/03) + **#91** (P8-04) all merged. Design of record: `docs/architecture/localization-architecture.md`. Full detail: `docs/dev/HANDOFF.md` → "Phase 8 — Localization".
- Integration suite: **670/670 green** against real Postgres (see the test-infra gap below).

### Next actions (in order)
1. **Test-infra fix** — make the integration suite self-contained. It currently needs a **Postgres on localhost:5432** (user `postgres` / pass `admin` / db `Learnexia`) for **Hangfire** storage *in addition to* Testcontainers for EF, because `LearnexiaWebAppFactory` overrides the 5 EF DbContexts but NOT the Hangfire connection (`Program.cs:104` uses the `Default` string). Fix: add `appsettings.Testing.json` pointing `Default` at the Testcontainers DB, or override Hangfire in the factory.
   - Local workaround until fixed: `docker run --name lx-hangfire-pg -e POSTGRES_PASSWORD=admin -e POSTGRES_USER=postgres -e POSTGRES_DB=Learnexia -d -p 5432:5432 pgvector/pgvector:pg16`
2. **Security hardening** — 2 pre-existing platform Highs (NOT introduced by P8): JWT `CHANGE_ME` placeholder secret in `appsettings.json` (env-only + clear placeholder; `GuardJwtSecret` already blocks prod/staging) and Newtonsoft.Json 11.0.1 CVE (GHSA-5crp-9r3c-p9vr) → 13.0.x in `Gamification.Api`/`Gamification.Infrastructure`.
3. **Frontend (next wave):** two unblocked tracks — (a) **Phase-3 gamification screens** (P4-02/03 XP+streak wiring first; then P4-05/06/07 screens; resume `feat/P4-08` for motion as the capstone), and (b) **app-side localization completion** (axis A is partially wired — finish brand-font runtime loading, promote/ persist the UI-language switch to `User.PreferredLanguage`, and build the missing **learning-language parent UX / P8-04 FE**). Localization has **no FE task files yet** — needs an analyzer/planner scoping pass per `docs/architecture/localization-architecture.md` §1/§7.

## Environment / build (WSL2 — primary dev env)
- Repo: `~/projects/learnexia` in **WSL2** (run Claude Code inside WSL; the Windows `E:\Wrokspace\Learnexia` checkout is abandoned — ubuntu CI is source of truth). .NET 10 (`dotnet 10.0.201`). Local Postgres = Docker `pgvector` on `localhost:5432`, DB `Learnexia`, `postgres/admin`. Node/pnpm for FE.
- Build: `dotnet build backend/Learnexia.Modular.sln`. Integration tests: `dotnet test backend/tests/Learnexia.IntegrationTests/...` (Testcontainers Postgres + the localhost:5432 Hangfire DB above). Unit tests under `backend/tests/Modules.*.UnitTests`.
- A fresh **cloud** session (claude.ai/code) starts without local Postgres/Docker — Testcontainers needs Docker; verify availability before relying on integration tests there.

## Remote shared DB
- Remote Postgres `learnexia` at **`75.119.158.102:5344`** (pgvector image). Connection (incl. credentials) is intentionally **NOT in the repo** — it lives only in the gitignored `backend/src/Host/Learnexia.Host/appsettings.Development.local.json`.
- After pulling main, apply any pending module migrations: `dotnet ef database update --context LearningDbContext` from `backend/` (Learning does not auto-migrate; Identity does). **Note:** the P8-02 migration is a **destructive re-seed** of curriculum — only apply to environments whose curriculum is demo data.

## Working agreements (how the user wants the build run)
- **Pipeline:** analyzer → planner → (designer for UI) → implementers (db-migration / backend-feature) → security-auditor/api-tester → reviewer → committer. Don't skip analyzer/planner for non-trivial work.
- **Cadence:** wave/story by story; the user reviews & merges PRs on GitHub (don't self-merge unless told). Handoff/session-state doc updates may be committed directly to main when the user says so.
- **Stacked-PR rule (learned 2026-06-05):** merge stacked PRs **bottom-up** (code PR before its docs/base PR), or target all PRs at `main` independently — otherwise the stacked PR merges into an already-merged base and its changes get stranded off main (this happened with #88/#89 → recovered via #90).
- **Ask before** creating any new backend module (mirror existing — Learning is the reference since Catalog was removed) or introducing any **design pattern**.
- **Module isolation (CLAUDE.md rule 1):** cross-module only via `Shared.Contracts` seams — no cross-module project refs or FKs.
- **Secrets:** never commit real credentials; use gitignored `appsettings.*.local.json` or env vars.
- **Worktree commit signing:** direct `git commit` from the main session's Bash tool can fail in worktrees (signing server 400). Use a background `committer` subagent if it does.

## Deferred hardening → P6-06 (pre-existing)
JWT `CHANGE_ME` default secret, Newtonsoft.Json 11.0.1 CVE (Gamification), `RequireHttpsMetadata=false` (make Development-only), DB-secret fail-fast, DbContext audit stamp `DateTime.Now`→`UtcNow`, avatar `[RequestSizeLimit]`, MinIO default creds, MSB3277 EF version conflict in `Directory.Packages.props`, G2 sign-out token revocation via SessionId per-request validation.

## Key docs to read on resume
`CLAUDE.md` (rulebook) · `docs/dev/HANDOFF.md` (shared memory) · `tasks/PROGRESS.md` (status board) · `docs/architecture/` (the architecture doc set incl. `localization-architecture.md`) · `docs/briefs/*` + `docs/plans/*` (per-story specs) · `user-stories/` + `tasks/` (source of truth).
