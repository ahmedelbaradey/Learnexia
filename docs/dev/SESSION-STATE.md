# Session State — Phase 2 Backend build (resume pointer)

> **Purpose:** hand off an in-flight multi-wave build to a fresh Claude Code session on another device (web/mobile). Local auto-memory + the conversation transcript do **not** sync across machines — this file + `CLAUDE.md` + `docs/dev/HANDOFF.md` + the committed `docs/briefs/*` & `docs/plans/*` are what travel. Read those first.
> Last updated: 2026-05-25 (Wave 7 in progress — all Batch 1s done, Batch 2s pending).

## Where we are
Driving the **Phase 2 (Learning Core) backend** to completion, wave by wave, through the full agent pipeline (analyzer → planner → implementers → security-auditor/api-tester → reviewer → committer). One PR per wave.

### Wave plan (dependency-ordered)
| Wave | Stories | Status |
|---|---|---|
| **6** | P2-12 (account settings, 3-module refactor), P2-10 (seed), P2-06 (take-a-quiz, folded into Learning) | ✅ done → **PR #54 merged** |
| **7** | P2-11 (skill dependency graph), P2-08 (record granular answers), P2-02 (browse subjects/lessons queries) | 🟡 **in progress** — Batch 1s complete, Batch 2s next |
| **8** | P2-04 (Learning Path unlock engine), P2-07 (instant answer feedback) | pending |
| **9** | P2-05 (open/complete lesson), P2-03 (skill-tree node state), P2-09 (home dashboard) | pending |

P2-01 (curriculum model) was already done pre-session.

## Open PRs
- *(none — #54 and #55 merged)*

## Wave 7 — current state (branch `claude/phase2-backend-wave7-U48WT`)
Three story branches are in flight off the wave branch. Worktrees at `/home/user/Learnexia.worktrees/{P2-11,P2-08,P2-02}`.

### P2-11 — Skill dependency graph (`feat/P2-11-skill-dependency-graph`)
- ✅ **Batch 1 (db-migration):** `KnowledgeNode` + `KnowledgeEdge` entities, two enums, EF configs, `AddSkillGraphTables` migration
- ⏭️ **Batch 2 (backend-feature, parallel):** BE-3 `SkillGraphValidator` DFS cycle check + unit tests; BE-5 `GetPrerequisitesOf`/`GetUnlockedBy` CQRS queries + `KnowledgeGraphController`
- ⏭️ **Batch 3 (backend-feature):** BE-4 extend `LearningSeeder` — map Skills→KnowledgeNodes + within-subject Math prerequisite edges (depends on Batch 2 cycle validator)
- ⏭️ Batch 4: api-tester · Batch 5: reviewer + committer
- **BE-6 DESCOPED** — no engine wiring to P2-04 (not built yet); the query API is the integration seam; wiring deferred to P2-04 (Wave 8)

### P2-08 — Record granular answers (`feat/P2-08-record-granular-answers`)
- ✅ **Batch 1 (db-migration):** schema verified (zero gaps from P2-06); `AddAttemptQueryIndexes` perf migration (composite indexes on Attempts + StudentAnswers)
- ⏭️ **Batch 2 (backend-feature):** BE-1 `SubmitAnswerCommand` (ownership guard, correctness check, per-answer write)
- ⏭️ **Batch 3:** BE-2/3 `CompleteAttemptCommand` + `AbandonAttemptCommand` + aggregate helper
- ⏭️ **Batch 4:** BE-4 `GetStudentAttemptsQuery` + `GetSkillStatsQuery`
- ⏭️ Batch 5: api-tester · Batch 6: security-auditor · Batch 7: reviewer + committer

### P2-02 — Browse subjects & lessons (`feat/P2-02-browse-subjects-lessons`)
- ✅ **Batch 1 (backend-feature):** `GetSubjectsForGradeQuery`, `GetSubjectLessonsQuery`, `GetSubjectSkillTreeQuery`, `NodeState` enum, 3 controller actions on `SubjectsController`
- ⏭️ **Batch 2:** api-tester · **Batch 3:** reviewer + committer

## Immediate next actions
Dispatch all three Batch 2s in parallel (they touch disjoint files):
1. **P2-11 Batch 2 (parallel BE-3 + BE-5)** in worktree `P2-11`
2. **P2-08 Batch 2 (BE-1 SubmitAnswer)** in worktree `P2-08`
3. **P2-02 Batch 2 (api-tester)** in worktree `P2-02`

Then: P2-11 Batch 3 (seeder); P2-08 Batches 3+4; merge all reviewed stories into wave branch → build/test wave branch → wave PR → main.

## Environment / build (works natively on Windows here)
- Repo: `E:\Wrokspace\Learnexia` (Windows). .NET 10 (`dotnet 10.0.201`). Local Postgres = Docker `pgvector` on `localhost:5432`, DB `Learnexia`, `postgres/admin`.
- Build: `dotnet build backend/Learnexia.Modular.sln`. Integration tests: `dotnet test backend/tests/Learnexia.IntegrationTests/...` (Testcontainers Postgres). Unit tests under `backend/tests/Modules.*.UnitTests`.
- A fresh **cloud** session (claude.ai/code) starts without local Postgres/Docker — Testcontainers needs Docker; verify availability before relying on integration tests there.

## Remote shared DB — ✅ migrated + seeded + pgvector
- Remote Postgres `learnexia` at **`75.119.158.102:5344`** (port moved from 5346 → **5344**). Now runs the **`pgvector/pgvector:pg15`** image (PG 15.18 Debian) so the `vector` extension is available.
- **Why pgvector matters:** the **Catalog** module migration `DEMO_PgvectorProof` runs `CREATE EXTENSION vector`; on a plain `postgres` image it fails with `0A000: extension "vector" is not available`. Fixed by swapping the server container to the pgvector image (kept the data). The repo's `docker/docker-compose.yaml` postgres service is pinned to `pgvector/pgvector:pg15` to match.
- **State verified:** all 5 module schemas migrated (incl. `catalog.EmbeddingDemos` + extension), and **seeded** — `learning.Subjects`=24 (4 subjects × 6 grades), `Lessons`=162, `Skills`=162, `identity.AspNetRoles`=13.
- **Connection (incl. credentials) is intentionally NOT in the repo.** It lives only in the gitignored `backend/src/Host/Learnexia.Host/appsettings.Development.local.json` (loaded via the optional `appsettings.{Environment}.local.json` line in `Program.cs`) — currently points at `:5344`. To use the remote DB elsewhere, re-add that file or set `ConnectionStrings__Default` (credentials held by the user).

## Working agreements (distilled from session memory — these are how the user wants the build run)
- **Cadence:** wave by wave; **PAUSE for user check-in after each wave** before starting the next. User reviews & merges PRs on GitHub (don't self-merge unless told).
- **One PR per wave:** `feat/wave-<N>` off main; cut each story's branch off the wave branch; merge reviewed story branches into the wave branch with `--no-ff` + a real multi-line body; build/test the wave branch; then the wave PR → main. Every commit has a multi-line body; every PR a full description (write to `docs/pr/wave-<N>.md`).
- **Ask before creating any new backend module** (mirror existing modules instead; quiz/assessment lives in the **Learning** module, not a separate one). Ask before introducing any **design pattern**.
- **Module isolation (CLAUDE.md rule 1):** cross-module only via `Shared.Contracts` seams (e.g. `IUserLookup`, `IChildAccountService`, `IParentChildQuery`) or integration events — no cross-module project refs or FKs.
- **Complexity-aware model tiers:** architecture/domain-boundary reasoning → Opus; feature/CRUD/tests/migrations/verification → Sonnet; trivial/localized → Haiku. (PR #55 sets the subagent defaults.)
- **Secrets:** never commit real credentials; use gitignored `appsettings.*.local.json` or env vars.

## Deferred hardening → P6-06 (pre-existing, not from Wave 6)
JWT `CHANGE_ME` default secret (env-drive + startup guard), `RequireHttpsMetadata=false` (make Development-only), DbContext audit stamp `DateTime.Now`→`UtcNow`, MinIO default creds, and the MSB3277 EF 10.0.0/10.0.8 version conflict in `Directory.Packages.props`.

## Cloud-env worktree note
Worktrees at `/home/user/Learnexia.worktrees/{P2-11,P2-08,P2-02}` were created for Mode B parallel implementation. **Direct `git commit` from the main session's Bash tool fails inside worktrees** (signing server returns "missing source" 400). Workaround: dispatch a background `committer` subagent to commit from within the worktree — background agents sign successfully. The main checkout at `/home/user/Learnexia` commits without issue.

## Key docs to read on resume
`CLAUDE.md` (rulebook) · `docs/dev/HANDOFF.md` (shared memory) · `tasks/PROGRESS.md` (status board) · `docs/briefs/P2-{02,08,11}.md` + `docs/plans/P2-{02,08,11}.md` (Wave 7 specs, fully written) · `user-stories/Phase-2-Learning-Core/` + `tasks/Backend/Phase-2-Learning-Core/` (source of truth for scope).
