# Session State — resume pointer

> **Purpose:** hand off in-flight work to a fresh Claude Code session on another device. Local auto-memory + the conversation transcript do **not** sync across machines — this file + `CLAUDE.md` + `docs/dev/HANDOFF.md` + the committed `docs/briefs/*` & `docs/plans/*` are what travel. Read those first.
> Last updated: 2026-06-05 (Phase 8 — Localization backend complete + merged to main; next = test-infra fix + security hardening + frontend i18n).

## Where we are (2026-06-05)
**Phases 1, 2, 3 (backend) and Phase 8 — Localization (backend) are COMPLETE and merged to main.** Status board: `tasks/PROGRESS.md`.

- **Phase 8 — Localization** (learning language vs UI language): PR #88 (docs) + **#90** (impl P8-01/02/03) + **#91** (P8-04) all merged. Design of record: `docs/architecture/localization-architecture.md`. Full detail: `docs/dev/HANDOFF.md` → "Phase 8 — Localization".
- Integration suite: **670/670 green** against real Postgres (see the test-infra gap below).

### Next actions (in order)
1. **Test-infra fix** — make the integration suite self-contained. It currently needs a **Postgres on localhost:5432** (user `postgres` / pass `admin` / db `Learnexia`) for **Hangfire** storage *in addition to* Testcontainers for EF, because `LearnexiaWebAppFactory` overrides the 5 EF DbContexts but NOT the Hangfire connection (`Program.cs:104` uses the `Default` string). Fix: add `appsettings.Testing.json` pointing `Default` at the Testcontainers DB, or override Hangfire in the factory.
   - Local workaround until fixed: `docker run --name lx-hangfire-pg -e POSTGRES_PASSWORD=admin -e POSTGRES_USER=postgres -e POSTGRES_DB=Learnexia -d -p 5432:5432 pgvector/pgvector:pg16`
2. **Security hardening** — 2 pre-existing platform Highs (NOT introduced by P8): JWT `CHANGE_ME` placeholder secret in `appsettings.json` (env-only + clear placeholder; `GuardJwtSecret` already blocks prod/staging) and Newtonsoft.Json 11.0.1 CVE (GHSA-5crp-9r3c-p9vr) → 13.0.x in `Gamification.Api`/`Gamification.Infrastructure`.
3. **Frontend i18n phase** — app-side localization (react-i18next / RTL, Cairo/Tajawal vs Poppins) per `docs/architecture/localization-architecture.md` §1 (axis A) + `docs/dev/FRONTEND_ARCHITECTURE.md`.

## Environment / build (works natively on Windows here)
- Repo: `E:\Wrokspace\Learnexia` (Windows). .NET 10 (`dotnet 10.0.201`). Local Postgres = Docker `pgvector` on `localhost:5432`, DB `Learnexia`, `postgres/admin`.
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
