# Session State — Phase 2 Backend build (resume pointer)

> **Purpose:** hand off an in-flight multi-wave build to a fresh Claude Code session on another device (web/mobile). Local auto-memory + the conversation transcript do **not** sync across machines — this file + `CLAUDE.md` + `docs/dev/HANDOFF.md` + the committed `docs/briefs/*` & `docs/plans/*` are what travel. Read those first.
> Last updated: 2026-05-29 (side-track: Phase-1 security follow-up audit → PR #65; Wave 7 Batches 2–4 still in flight in the parallel session).

## Where we are
Driving the **Phase 2 (Learning Core) backend** to completion, wave by wave, through the full agent pipeline (analyzer → planner → implementers → security-auditor/api-tester → reviewer → committer). One PR per wave.

### Wave plan (dependency-ordered)
| Wave | Stories | Status |
|---|---|---|
| **6** | P2-12 (account settings), P2-10 (seed), P2-06 (take-a-quiz) | ✅ merged (PR #54) |
| **7** | P2-11 (skill dependency graph), P2-08 (record granular answers), P2-02 (browse subjects/lessons) | 🟡 **in progress** — Batch 1s merged (#56/#57/#58); Batches 2+ pending |
| **8** | P2-04 (Learning Path unlock engine), P2-07 (instant answer feedback) | pending |
| **9** | P2-05 (open/complete lesson), P2-03 (skill-tree node state), P2-09 (home dashboard) | pending |

P2-01 (curriculum model) was already done pre-session.

## Open PRs
- **#56** `feat/P2-11-skill-dependency-graph` — Draft, Batch 1 merged; continuing Batches 2–3
- **#57** `feat/P2-02-browse-subjects-lessons` — Draft, Batch 1 merged; api-tester + reviewer pending
- **#58** `feat/P2-08-record-granular-answers` — Draft, Batch 1 merged; command/query batches pending

## Wave 7 — immediate next actions
Dispatch all three Batch 2s in parallel (disjoint files, Mode B worktrees ready):

1. **P2-11 Batch 2** (parallel BE-3 + BE-5 in worktree `/home/user/Learnexia.worktrees/P2-11`):
   - BE-3: `SkillGraphValidator.AssertAcyclic` DFS + unit tests
   - BE-5: `GetPrerequisitesOf`/`GetUnlockedBy` queries + `KnowledgeGraphController`
2. **P2-08 Batch 2** (worktree `/home/user/Learnexia.worktrees/P2-08`): BE-1 `SubmitAnswerCommand`
3. **P2-02 Batch 2** (worktree `/home/user/Learnexia.worktrees/P2-02`): api-tester

Then: P2-11 Batch 3 (BE-4 seeder); P2-08 Batches 3+4; reviewer gates; update PRs #56/#57/#58.

## Environment / build (works natively on Windows here)
- Repo: `E:\Wrokspace\Learnexia` (Windows). .NET 10 (`dotnet 10.0.201`). Local Postgres = Docker `pgvector` on `localhost:5432`, DB `Learnexia`, `postgres/admin`.
- Build: `dotnet build backend/Learnexia.Modular.sln`. Integration tests: `dotnet test backend/tests/Learnexia.IntegrationTests/...` (Testcontainers Postgres). Unit tests under `backend/tests/Modules.*.UnitTests`.
- A fresh **cloud** session (claude.ai/code) starts without local Postgres/Docker — Testcontainers needs Docker; verify availability before relying on integration tests there.

## Remote shared DB — ✅ migrated + seeded + pgvector
- Remote Postgres `learnexia` at **`75.119.158.102:5344`** (port moved from 5346 → **5344**). Now runs the **`pgvector/pgvector:pg15`** image (PG 15.18 Debian) so the `vector` extension is available.
- **Why pgvector matters:** the **Catalog** module migration `DEMO_PgvectorProof` runs `CREATE EXTENSION vector`; on a plain `postgres` image it fails with `0A000: extension "vector" is not available`. Fixed by swapping the server container to the pgvector image (kept the data). The repo's `docker/docker-compose.yaml` postgres service is pinned to `pgvector/pgvector:pg15` to match.
- **State verified:** all 5 module schemas migrated (incl. `catalog.EmbeddingDemos` + extension), and **seeded** — `learning.Subjects`=24 (4 subjects × 6 grades), `Lessons`=162, `Skills`=162, `identity.AspNetRoles`=13.
- **Pending DB migrations** (apply after pulling main): `AddAttemptQueryIndexes` + `AddSkillGraphTables` — run `dotnet ef database update --context LearningDbContext` from `backend/`.
- **Connection (incl. credentials) is intentionally NOT in the repo.** It lives only in the gitignored `backend/src/Host/Learnexia.Host/appsettings.Development.local.json` — currently points at `:5344`.

## Working agreements (distilled from session memory — these are how the user wants the build run)
- **Cadence:** wave by wave; **PAUSE for user check-in after each wave** before starting the next. User reviews & merges PRs on GitHub (don't self-merge unless told).
- **Story PRs go directly to main** (Wave 7 pattern — user merges each story PR individually rather than via a wave branch).
- **Ask before creating any new backend module** (mirror existing modules instead; all quiz/assessment/graph work in the **Learning** module). Ask before introducing any **design pattern**.
- **Module isolation (CLAUDE.md rule 1):** cross-module only via `Shared.Contracts` seams — no cross-module project refs or FKs.
- **Complexity-aware model tiers:** architecture/domain-boundary reasoning → Opus; feature/CRUD/tests/migrations → Sonnet; trivial → Haiku.
- **Secrets:** never commit real credentials; use gitignored `appsettings.*.local.json` or env vars.
- **Worktree commit signing:** direct `git commit` from the main session's Bash tool fails in worktrees (signing server 400 "missing source"). Use a background `committer` subagent instead.

## Deferred hardening → P6-06 (pre-existing)
JWT `CHANGE_ME` default secret, `RequireHttpsMetadata=false` (make Development-only), DbContext audit stamp `DateTime.Now`→`UtcNow`, MinIO default creds, MSB3277 EF version conflict in `Directory.Packages.props`.

## Side-track — Phase-1 security follow-up audit (2026-05-29) — separate from the wave build above
Standalone hardening off the wave plan, own branch **`audit/phase-1`** (own worktree). **PR #65 open** (base `main`). Does NOT touch the Wave-7 worktrees/branches.
- Audited every Phase-1 security-audit follow-up vs `main` (all originally PASS / PASS-WITH-FOLLOWUPS; zero Critical/High). ~10/18 already applied.
- **PR #65 (build green; integration suite NOT run — no Docker this session):**
  - **B1** — CAPTCHA prod-guard: `GuardCaptcha` in Identity `DependencyInjection.cs` fail-fasts in Production/Staging unless CAPTCHA enabled + secret set.
  - **G1/B2** — env-gated auth rate limits in `Host/Extensions/ServiceExtensions.cs` (`ConfigureRateLimitingOptions(IConfiguration)`): prod/staging tightened (sign-in 50/5m, register 10/15m, forgot 5/15m, reset 10/15m, google-signin 50/5m); Dev/Testing keep the prior 100/s rules verbatim so integration tests are unaffected.
- **Routed to P6-06 (new AC-7):** **G2** — revoke live access tokens on sign-out/reset via **SessionId per-request validation** (`JwtBearerEvents.OnTokenValidated` against `ISessionManagementService`; NOT security-stamp, which would break P2-12 "keep current session"). Load-bearing auth → full pipeline.
- **Still outstanding (Low/Info, mostly P6-06):** `RequireHttpsMetadata` env-gating, DB-secret fail-fast, avatar `[RequestSizeLimit]`, child-`Email` in Added/Updated/LinkedChildResponse DTOs, Google auto-link/consent, CORS `*`+credentials fallback.
- Full detail: `docs/dev/HANDOFF.md` → "Phase-1 security follow-up audit" section.

## Key docs to read on resume
`CLAUDE.md` (rulebook) · `docs/dev/HANDOFF.md` (shared memory, Wave 7 section at top) · `tasks/PROGRESS.md` (status board) · `docs/briefs/P2-{02,08,11}.md` + `docs/plans/P2-{02,08,11}.md` (Wave 7 specs) · `user-stories/Phase-2-Learning-Core/` + `tasks/Backend/Phase-2-Learning-Core/` (source of truth).
