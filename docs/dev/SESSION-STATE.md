# Session State — Phase 2 Backend build (resume pointer)

> **Purpose:** hand off an in-flight multi-wave build to a fresh Claude Code session on another device (web/mobile). Local auto-memory + the conversation transcript do **not** sync across machines — this file + `CLAUDE.md` + `docs/dev/HANDOFF.md` + the committed `docs/briefs/*` & `docs/plans/*` are what travel. Read those first.
> Last updated: 2026-05-25.

## Where we are
Driving the **Phase 2 (Learning Core) backend** to completion, wave by wave, through the full agent pipeline (analyzer → planner → implementers → security-auditor/api-tester → reviewer → committer). One PR per wave.

### Wave plan (dependency-ordered)
| Wave | Stories | Status |
|---|---|---|
| **6** | P2-12 (account settings, 3-module refactor), P2-10 (seed), P2-06 (take-a-quiz, folded into Learning) | ✅ done → **PR #54** (open, awaiting merge) |
| **7** | P2-11 (skill dependency graph), P2-08 (record granular answers), P2-02 (browse subjects/lessons queries) | ⏭️ next |
| **8** | P2-04 (Learning Path unlock engine), P2-07 (instant answer feedback) | pending |
| **9** | P2-05 (open/complete lesson), P2-03 (skill-tree node state), P2-09 (home dashboard) | pending |

P2-01 (curriculum model) was already done pre-session.

## Open PRs
- **#54** `feat/wave-6` → main — Wave 6 backend. Full integration suite **417/417 green**. Merge this first.
- **#55** `chore/agent-model-tiers` → main — per-agent model frontmatter (execution→sonnet, designer→opus).

## Immediate next action (after #54 merges)
1. `git fetch && git checkout main && git merge --ff-only origin/main` (sync local main).
2. Create `feat/wave-7` off main; per story cut a branch off the wave branch.
3. Run analyzer → planner for **P2-11, P2-08, P2-02** (briefs/plans not yet written for these). Then implement.
   - **P2-11 depends on** P2-10 seed (stable Skill names are the prerequisite-edge seam) + wires into P2-04 (Wave 8).
   - **P2-08 depends on** the quiz entities from P2-06 (already in the Learning module).
   - **P2-02** = query endpoints over the P2-01 curriculum model + P2-10 seed.

## Environment / build (works natively on Windows here)
- Repo: `E:\Wrokspace\Learnexia` (Windows). .NET 10 (`dotnet 10.0.201`). Local Postgres = Docker `pgvector` on `localhost:5432`, DB `Learnexia`, `postgres/admin`.
- Build: `dotnet build backend/Learnexia.Modular.sln`. Integration tests: `dotnet test backend/tests/Learnexia.IntegrationTests/...` (Testcontainers Postgres). Unit tests under `backend/tests/Modules.*.UnitTests`.
- A fresh **cloud** session (claude.ai/code) starts without local Postgres/Docker — Testcontainers needs Docker; verify availability before relying on integration tests there.

## Remote shared DB (migrated, NOT seeded)
- A remote Postgres (`learnexia`) had **all 5 module schemas migrated** this session (identity, catalog, learning incl. quiz tables, notifications, parent). It is **not seeded** (roles + curriculum) — seeding needs a Host run in Development that was permission-blocked.
- **The connection string (incl. credentials) is intentionally NOT in the repo.** It lives only in the gitignored `backend/src/Host/Learnexia.Host/appsettings.Development.local.json` (loaded via the optional `appsettings.{Environment}.local.json` line in `Program.cs`). To use the remote DB elsewhere, re-add that file or set the `ConnectionStrings__Default` env var (the user holds the credentials). To seed: `dotnet run --project backend/src/Host/Learnexia.Host -- --environment Development --MinIOConfiguration:Enabled false` (needs a `Bash(dotnet run:*)` allow-rule under the safety classifier).

## Working agreements (distilled from session memory — these are how the user wants the build run)
- **Cadence:** wave by wave; **PAUSE for user check-in after each wave** before starting the next. User reviews & merges PRs on GitHub (don't self-merge unless told).
- **One PR per wave:** `feat/wave-<N>` off main; cut each story's branch off the wave branch; merge reviewed story branches into the wave branch with `--no-ff` + a real multi-line body; build/test the wave branch; then the wave PR → main. Every commit has a multi-line body; every PR a full description (write to `docs/pr/wave-<N>.md`).
- **Ask before creating any new backend module** (mirror existing modules instead; quiz/assessment lives in the **Learning** module, not a separate one). Ask before introducing any **design pattern**.
- **Module isolation (CLAUDE.md rule 1):** cross-module only via `Shared.Contracts` seams (e.g. `IUserLookup`, `IChildAccountService`, `IParentChildQuery`) or integration events — no cross-module project refs or FKs.
- **Complexity-aware model tiers:** architecture/domain-boundary reasoning → Opus; feature/CRUD/tests/migrations/verification → Sonnet; trivial/localized → Haiku. (PR #55 sets the subagent defaults.)
- **Secrets:** never commit real credentials; use gitignored `appsettings.*.local.json` or env vars.

## Deferred hardening → P6-06 (pre-existing, not from Wave 6)
JWT `CHANGE_ME` default secret (env-drive + startup guard), `RequireHttpsMetadata=false` (make Development-only), DbContext audit stamp `DateTime.Now`→`UtcNow`, MinIO default creds, and the MSB3277 EF 10.0.0/10.0.8 version conflict in `Directory.Packages.props`.

## Key docs to read on resume
`CLAUDE.md` (rulebook) · `docs/dev/HANDOFF.md` (shared memory, updated with P2-12/P2-10/P2-06) · `tasks/PROGRESS.md` (status board) · `docs/briefs/P2-*.md` + `docs/plans/P2-*.md` (per-story specs, written for P2-01/06/10/12) · `user-stories/Phase-2-Learning-Core/` + `tasks/Backend/Phase-2-Learning-Core/` (source of truth for scope).
