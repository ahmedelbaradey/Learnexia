# Handoff — Phase 1 web frontend + dev environment

> Living handoff for leads/agents picking up the web frontend + backend work. Last updated 2026-05-29 (**Wave 6 merged; Wave 7 fully merged; Wave 8 in progress — P2-04 merged via PR #63; P2-07 ready for PR**).
> Captures what's done, the decisions, the load-bearing config, and what's next. If you change any of these, update this file.

## Wave 8 — Phase 2 backend (in progress)

### P2-07 — Instant answer feedback ✅ Batches 1–5 complete, PR pending

**What's on branch `feat/P2-07-instant-answer-feedback` (ready for PR):**
- **`AnswerComparator`** ✅ pure static at `Learning.Domain/Services/AnswerComparator.cs` — plain `switch` on `QuestionType` (no design pattern). MCQ: `OrdinalIgnoreCase` (preserves P2-08 behavior); TrueFalse: `bool.TryParse` both sides + equality; FillInBlank: trim + `OrdinalIgnoreCase`; Matching: string-compare fallthrough with `TODO P2-07.b` (no matching questions seeded today). Null/whitespace inputs return `false` (no throw). 12 unit tests in `AnswerComparatorTests.cs`.
- **`SubmitAnswerCommandHandler`** ✅ uses `AnswerComparator.AreEqual(...)` for correctness; injects `IPublisher`; publishes `AnswerSubmittedIntegrationEvent` after `AddAsync` and before return (direct publish per ADR 0002 Option B, NOT outbox). Guarded on `question.SkillId.HasValue` — null skips with `_logger.LogWarn` + `TODO P3-09`. Try/catch around `Publish` is fail-soft (publisher exception is logged via `_logger.LogError(ex, msg)`; user request still succeeds).
- **`CompleteAttemptCommandHandler`** ✅ same pattern. Loads `Lesson.SkillId` via the new `GetLessonSkillIdAsync` repo method; publishes `LessonCompletedIntegrationEvent` (7 fields: `EventId, OccurredOnUtc, StudentId, LessonId, SkillId, AccuracyPercentage:int (rounded from double), CorrectAnswerCount`). Same null-skip + fail-soft pattern. `AbandonAttemptCommandHandler` is **NOT** touched — abandonment is not a completion event.
- **`ILearningRepository` extended** ✅ `GetLessonSkillIdAsync(int lessonId, CT) → Task<int?>` (AsNoTracking, single projection).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_07_InstantAnswerFeedback_Tests.cs` — 13 cases via in-test `INotificationHandler<T>` capture (factory layered with `WithWebHostBuilder` — `LearnexiaWebAppFactory` not modified). Covers MCQ/TrueFalse/FillInBlank correctness, event-captured-on-success-with-SkillId, NO event on null-SkillId, NO event on rejection paths (duplicate/IDOR/state guards), `LessonCompletedIntegrationEvent` happy + null-SkillId, idempotent Complete doesn't re-fire, handler isolation (throwing subscriber doesn't fail the API), envelope still `"successed":` camelCase, Abandon doesn't publish. Full Wave-7+Wave-8 regression suite: 60/60 PASS.
- **Security audit** ✅ `docs/briefs/P2-07-security-audit.md` — PASS, 0 Critical/High. Event payloads carry IDs only (no `CorrectAnswer`/`AnswerPayload`/PII). `ex.Message` not leaked. Log lines contain IDs only. Ghost-event-on-rollback documented as accepted Phase-2 trade-off per ADR 0002.

**Key decisions:** Per-type correctness via plain `switch` (no Strategy). Direct `IPublisher.Publish` inside the UoW transaction (Option B), matching the Identity precedent. Skip event when `SkillId IS NULL` (don't extend the cross-module event contract with a sentinel). Fail-soft try/catch around publish (publisher failure must NOT fail the user request). `CorrectAnswerCount` on `LessonCompletedIntegrationEvent` is the 7th field — initially missed by Batch 3 spec, corrected in implementation. Adjusted FillInBlank integration test to use JSON-encoded strings (`CorrectAnswer` is `jsonb`; bare words are invalid JSON) — whitespace-trim still covered by unit tests.

**Non-blocking follow-ups** (carry forward): switch the 4 new log lines to structured-logging placeholder syntax (`"... {AttemptId}"` instead of `$"...AttemptId={attempt.Id}"`) for observability — security-audit F-01 Low. P2-08 inherited: still no `MaximumLength` validator on `AnswerPayload` (recommended for Phase 3 scale-up).

### P2-04 — Unlock rules / Learning Path Engine ✅ Merged via PR #63

Wave-8 story 1 — `LearningPathEngine` (pure static memoized DFS) + 5 AsNoTracking repo methods + JWT-aware wiring into P2-02 handlers + `[Authorize]` tightening on `Subjects/{id}/{Lessons,SkillTree}`. See git log + `docs/briefs/P2-04.md` + `docs/plans/P2-04.md` for full details. **Breaking change**: those two endpoints now return 401 to unauthenticated callers.

### P2-04 — Unlock rules / Learning Path Engine ✅ Batches 1–4 complete, PR pending

**What's on branch `feat/P2-04-unlock-rules-learning-path-engine` (ready for PR):**
- **Engine** ✅ `Learning.Domain/Services/LearningPathEngine.cs` — pure static, three-color memoized DFS over Prerequisite edges. Caller pre-fetches inputs (no DI, no DB). Inputs: `IReadOnlyList<Lesson>`, `IReadOnlyList<KnowledgeNode>`, `IReadOnlyList<KnowledgeEdge>`, `IReadOnlyDictionary<int, SkillMastery> mastery`, `IReadOnlySet<int> completedLessonIds`, `IReadOnlyDictionary<int, Skill> skillsById` (separate from `SkillMastery` so the 3-param mastery record stays tiny). Returns `IReadOnlyDictionary<int, LessonUnlockStateDto>` keyed by `Lesson.Id`. 12 unit tests cover acyclic / cycle / self-loop / null-SkillId / no-prereqs / partial-mastery / exact-threshold / cross-grade / completed-lesson.
- **DTOs at `Domain/Services/`** (next to engine — not under `Application/Features/.../Dtos/`): `SkillMastery (SkillId, AccuracyPercentage:double, TotalAnswers)`, `LessonUnlockStateDto (LessonId, NodeState, IReadOnlyList<MissingPrerequisiteDto>)`, `MissingPrerequisiteDto (PrereqSkillId, PrereqSkillName, PrereqNodeId, RequiredAccuracy:int, CurrentAccuracy:decimal)`.
- **Repository extension** ✅ `ILearningRepository` + `LearningRepository` got 5 new AsNoTracking methods: `GetSubjectKnowledgeNodesAsync`, `GetSubjectKnowledgeEdgesAsync` (returns edges whose both endpoints are in the subject), `GetSkillMasteryForStudentInSubjectAsync` (returns mastery rows for EVERY skill in the subject — zero-row skills get `TotalAnswers=0` so the engine has the threshold), `GetCompletedLessonIdsForStudentInSubjectAsync`, `GetSubjectLessonsAsync`.
- **Wired into 2 existing P2-02 handlers** ✅ `GetSubjectSkillTreeQueryHandler` + `GetSubjectLessonsQueryHandler` now branch on `_currentUser.UserId.HasValue`: authenticated → run engine + project real `NodeState` + `MissingPrerequisites`; anonymous → fall back to existing placeholder (now never reached after Batch 4). Skill-level `NodeState` aggregated from its lessons (Completed > Available > Locked); Concept-level aggregated from its skills.
- **DTOs extended** ✅ `LessonInUnitDto` got `State : NodeState` (new) + `MissingPrerequisites : IReadOnlyList<MissingPrerequisiteDto>` (defaults to empty). `IsLocked` kept for back-compat, marked `[Obsolete("Replaced by LearningPathEngine in P2-04. Will be removed in P2-09 or P6-06.")]`. `SkillNodeDto.MissingPrerequisites` added as nullable (null when anonymous).
- **Auth tightening** ✅ `[Authorize]` added to `GET /api/learning/Subjects/{id}/SkillTree` AND `GET /api/learning/Subjects/{id}/Lessons`. `GET /api/learning/Subjects/ForGrade` stays anonymous. **BREAKING CHANGE:** any client currently calling the two gated endpoints without a JWT will start getting 401. FE wiring already uses auth.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_04_LearningPath_Tests.cs` — 12 cases (anonymous 401 gate × 2; fresh-student root-Available/downstream-Locked × 2; root-mastery unlocks next-skill; `MissingPrerequisites` shape; completed-lesson state; cross-student isolation; anonymous ForGrade still 200; unknown-subject 404; null-SkillId lesson Available; envelope camelCase). P2-02 tests updated to pass Student JWT on the 7 now-gated cases. All 24 green (~66s, Testcontainers Postgres).
- **2 new localized message keys** in `SharedResources*.resx` + `SharedResourcesKey.cs`: `LearningPathSubjectNotFound`, `LearningPathUnauthorized`.

**Key decisions:** Mastery = `AccuracyPercentage >= MasteryThreshold` (int 0..100) AND `TotalAnswers >= 1`. Completion = ≥1 `Attempt.Status=Completed` for that `(student, lesson)`. Lessons with `SkillId IS NULL` → `Available`. Skills with no prereq edges → `Available` (root nodes). `MissingPrerequisites` = immediate prereqs only (no transitive closure). `Strength` ignored in v1 (kept on schema). Edge of next concern: `Lesson.IsLocked` boolean is deprecated but still in the DB and DTO — removal scheduled for P2-09 or P6-06. P2-07 (sibling Wave-8 story) also touches `ILearningRepository.cs` — ship P2-04 first, rebase P2-07 on top.

## Wave 7 — Phase 2 backend ✅ Fully merged

All 3 stories merged to main (P2-11 via PR #60, P2-08 via PR #61, P2-02 via PR #62). See git log for full details. Original Wave 7 brief and decisions preserved below for historical reference.

### P2-11 — Skill dependency graph ✅ Batches 1–4 complete, PR pending

### P2-11 — Skill dependency graph ✅ Batches 1–4 complete, PR pending

**What's on main (PR #56):**
- `KnowledgeNode` entity — wraps `Skill` via nullable `SkillId?` FK (filtered unique index `UX_KnowledgeNodes_SkillId WHERE SkillId IS NOT NULL`). Fields: Name, NodeType (Skill/Concept/Review enum), SubjectId FK, GradeId FK, Difficulty (int 1–5).
- `KnowledgeEdge` entity — self-referential directed edge. Fields: SourceNodeId, TargetNodeId, RelationshipType (Prerequisite/Related enum), Strength (decimal 0–1, default 1.0). Both FKs `DeleteBehavior.Restrict`; SkillId FK `SetNull`.
- Migration `AddSkillGraphTables` (learning schema).

**What's on branch `feat/P2-11-skill-dependency-graph` (ready for PR):**
- **BE-3** ✅ `SkillGraphValidator.AssertAcyclic` (static, three-color DFS over Prerequisite edges only) at `Learning.Domain/Services/SkillGraphValidator.cs` + 6 unit tests (acyclic / cycle / self-loop / related-excluded / empty / mixed) — all green.
- **BE-5** ✅ `GetPrerequisitesQuery` + `GetUnlockedByQuery` CQRS handlers under `Learning.Application/Features/KnowledgeGraph/` + `KnowledgeNodeDto` + `KnowledgeGraphProfile` (placed in `Application/Mapping/` to match the existing convention, not under `Features/`); `KnowledgeGraphController` exposing `GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId}` + `/UnlockedBy/{nodeId}` (both `[Authorize]`). Repository extended on `ILearningRepository` with `GetPrerequisiteNodesAsync`, `GetUnlockedByNodeAsync`, `KnowledgeNodeExistsAsync`. Localized `KnowledgeNodeNotFound` key added in en-US + ar-EG resources.
- **BE-4** ✅ `LearningSeeder.SeedSkillGraphAsync` — maps every seeded `Skill` → `KnowledgeNode` (idempotent on `SkillId`, Difficulty=3 default); authors 7 Prerequisite edges across Math G1→G6 (skipped chains where a P2-10 skill name doesn't exist, e.g. "Place Value", "Division" — documented inline). Calls `SkillGraphValidator.AssertAcyclic(existing.Concat(@new))` before save; on cycle detection logs error + skips save (does NOT crash startup). Uses `GetService<ILoggerManager>()` (null-tolerant) so existing seeder unit tests keep working with a minimal service provider.
- **BE-6 DESCOPED** — no wiring to P2-04/P3-08/P3-10; the query API IS the integration seam; P2-04 consumes it when built (Wave 8).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_11_KnowledgeGraph_Tests.cs` — 6 tests (Prerequisites happy path, UnlockedBy happy path, unknown nodeId ≠ 500, unauthenticated → 401, seed smoke check, `"successed":` envelope literal) all green against Testcontainers PostgreSQL.

**Key decisions:** KnowledgeNode wraps (not replaces) Skill; within-subject edges only in demo seed; BE-6 seam only. **Skill Name strings must not be renamed** (P2-10 seeder + P2-11 use them as lookup keys). Math prereq chain skips Division (no Division skill seeded in P2-10) — jumps G3 Multiplication → G5 Fractions; revisit when P2-10 fills out Division skills. BL-01..05 deferral now recorded in `user-stories/README.md` (AC-7).

### P2-08 — Record granular answers ✅ Batches 1–4 complete, security PASS, PR pending

**What's on main (PR #58):**
- Migration `AddAttemptQueryIndexes` — composite `(StudentId, Status)` on `learning.Attempts`; `(AttemptId, QuestionId)` on `learning.StudentAnswers`. Schema from P2-06 already had all needed columns (zero gaps).
- `AttemptStatus` has `Abandoned=3`.

**What's on branch `feat/P2-08-record-granular-answers` (ready for PR):**
- **BE-1** ✅ `SubmitAnswerCommand` → `POST /api/Learning/Quizzes/{attemptId}/Answers` `[Authorize(Roles="Student")]`. Cross-lesson injection guard (`question.LessonId == attempt.LessonId`), re-answer guard (duplicate `(AttemptId, QuestionId)` → 424), case-insensitive correctness check, returns `{isCorrect, correctAnswer:null-when-correct, hintAvailable:false}`. TODO comment for P2-07 `AnswerSubmittedIntegrationEvent`.
- **BE-2/3** ✅ `CompleteAttemptCommand` + `AbandonAttemptCommand` → `POST …/Complete` and `POST …/Abandon` `[Authorize(Roles="Student")]`. Both idempotent on terminal state (re-call returns current snapshot); cross-terminal rejected (Complete on Abandoned → 424 and vice versa). `RecomputeAggregates` private helper duplicated in both handlers (plan-authorized; not a shared service). Returns `AttemptSummaryDto`. TODO comment for P2-07 `LessonCompletedIntegrationEvent`.
- **BE-4** ✅ `GetStudentAttemptsQuery` → `GET /api/Learning/Students/{studentId}/Attempts` `[Authorize]` (new `StudentsController`) + `GetSkillStatsQuery` → `GET /api/Learning/Skills/{skillId}/Stats?studentId=` `[Authorize]` (appended to existing `SkillsController`). Both enforce per-student IDOR guard (`studentId == _currentUser.UserId`). `AttemptListItemDto` and `SkillStatsDto` both omit `CorrectAnswer` entirely. Skill-stats zero-data case returns zeroed DTO (not 404/500); questions with null `SkillId` silently excluded (correct behavior).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_08_RecordGranularAnswers_Tests.cs` — 17 test cases (all 6 SubmitAnswer + 3 Complete + 3 Abandon + 2 GetStudentAttempts + 3 GetSkillStats per plan Batch 5) — all green (~30s, Testcontainers Postgres + Student-role JWT via parent→child onboarding flow).
- **Security audit** ✅ `docs/briefs/P2-08-security-audit.md` — 0 Critical/High; all 7 focus areas PASS (JWT-derived StudentId, ownership, IDOR, no `CorrectAnswer` leak, no `ex.Message` leak, `TimeSpentSeconds ≤ 3600`, cross-lesson guard). 2 Low + 4 Info findings documented, none blocking.
- **Bug fix surfaced + applied:** `RecomputeAggregates` was computing negative `DurationSeconds` because Npgsql returns `timestamp with time zone` columns with `Kind == Local`. Fixed by normalizing `attempt.StartedAt.ToUniversalTime()` before subtracting `DateTime.UtcNow` (+ `Math.Max(0, …)` belt-and-suspenders). Comment in both handlers explains the Kind=Local rationale.

**Key decisions:** P2-08 owns `SubmitAnswerCommand`; P2-07 (Wave 8) extends it with feedback. DurationSeconds = server-side `UtcNow - StartedAt.ToUniversalTime()`; per-answer TimeSpentSeconds advisory (validated ≥0, ≤3600). Reject duplicate QuestionId in same attempt. Validators: Submit/Complete/Abandon all enforce `AttemptId > 0`; SubmitAnswer also enforces `AnswerPayload` not-empty + `TimeSpentSeconds` 0..3600 range. 14 new localized message keys (en-US + ar-EG).

### P2-02 — Browse subjects & lessons ✅ Batch 1 merged (PR #57), api-tester PR pending

**What's on main (PR #57):**
- `NodeState` enum at `Domain/Enums/NodeState.cs` — `Locked=0`, `Available=1`, `Completed=2` (placeholder from `Lesson.IsLocked`; P2-03/P2-04 replace the logic)
- `GET /api/learning/Subjects/ForGrade?grade={1-6}` → `GetSubjectsForGradeQuery`
- `GET /api/learning/Subjects/{id}/Lessons` → `GetSubjectLessonsQuery` (nested Units→Lessons, SequenceOrder)
- `GET /api/learning/Subjects/{id}/SkillTree` → `GetSubjectSkillTreeQuery` (Concepts+Skills with placeholder NodeState)
- No migration — P2-01 schema + P2-10 seed already in place

**What's on branch `feat/P2-02-browse-subjects-lessons` (ready for PR):**
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_02_BrowseSubjectsAndLessons_Tests.cs` — 12 cases: ForGrade happy paths (G1 + G6) returning 4 subjects each, out-of-range grade=99 → 400 (handler guards 1..6), missing param → 400, item shape (id/name/gradeNumber); Lessons happy path (5 units × 3 lessons for Math G1), order-by-SequenceOrder, unknown subject → 404; SkillTree happy path (5 concepts × 3 skills for Math G1), `state` field present + value ∈ {0,1,2}, unknown subject → 404; envelope `"successed":` camelCase check. All green (~55s, Testcontainers Postgres).

**Confirmed contract:** `grade` query param validated 1..6 in handler (out-of-range → 400, not empty list). `NodeState` serializes as int (no `JsonStringEnumConverter` registered). `SkillNodeDto.State` JSON key is `"state":` (not `"nodeState":`). Endpoints are anonymous-callable today — no `[Authorize]` yet.

**Deferred follow-ups:** Grade JWT claim seam (P6-06); `Concept/Skill.SequenceOrder` columns (P2-11 follow-up; currently ordered by Id); `[Authorize]` on new actions (hardening wave).

### Cloud-env worktree note
Worktrees at `/home/user/Learnexia.worktrees/{P2-11,P2-08,P2-02}` (branches off `claude/phase2-backend-wave7-U48WT`). **Direct `git commit` from the main session's Bash tool fails inside worktrees** (signing server 400 "missing source"). Workaround: dispatch a background `committer` subagent — background agents sign successfully. Main checkout commits without issue.

## TL;DR
- The repo now runs natively in **WSL2** (`~/projects/learnexia`). Clean install + `dotnet build` + Expo web/native bundling are validated.
- The Expo **student-app web** now boots, translates (ar/en), and talks to the backend end-to-end (register/login → 200 + JWT).
- **P1-11** (parent web pages, pixel-perfect from `design-system/screenshots/`) is planned + two screens built: **Login** and **Register**.
- All **new backend** the design implies is deferred to **P1-12 "Batch 2"** (Identity-scoped, parallel-safe with the Phase 2 BE lead) — see "For the backend lead".


## P2-06 — Take a quiz (folded into Learning module)
> Committed on `feat/P2-06-assessment-quiz`; pending Wave-6 PR. Build green, integration + unit tests pass, reviewer PASS.

**Lead decision:** quiz/assessment functionality lives in the **Learning** module (schema `learning`), NOT a separate Assessment module. A separate Assessment module was scaffolded then deleted per lead instruction. **Ask before creating new modules** — all quiz work goes in Learning going forward.

**New domain entities (Learning.Domain):**
- `QuizQuestion` — polymorphic question record with `QuestionType` (MCQ/TrueFalse/Matching/FillInBlank), `Content` (JSON blob), `CorrectAnswer`, `Order`, and `GeneratedBy` (Human/AI). Linked to a `Lesson`.
- `Attempt` — student quiz attempt record; status `AttemptStatus` (NotStarted/InProgress/Completed/Abandoned); links to a `Lesson` and `StudentId`.
- `StudentAnswer` — per-question answer record inside an attempt.

**Migration:** `AddQuizTables` (learning schema) — creates `quiz_questions`, `attempts`, `student_answers` tables in the `learning` schema.

**New endpoint:**
- `POST /api/Learning/Quizzes/{lessonId}/Attempt` — `[Authorize(Roles="Student")]` — creates a new `InProgress` attempt (or resumes an existing one) and returns the lesson's questions **without** the `CorrectAnswer` field. Enforces: lesson-existence check (404), Student-role-only (403), no-answer-leak.

**4 question types modeled** (MCQ / TrueFalse / Matching / FillInBlank) with a per-type content validator (`QuizQuestionContentValidator` helper) and unit tests in `Modules.Learning.UnitTests/QuizQuestionTypeValidationTests.cs`.

**`AttemptService.StartNewAsync` explicit SaveChangesAsync:** calls `LearningDbContext.SaveChangesAsync` directly (not waiting for UoW) to obtain the DB-generated `AttemptId` before returning questions — mirrors the `LinkParentStudentService` precedent. UoW's later save is a no-op.

**Secret hygiene (no new secrets introduced):**
- Remote dev DB connection string lives ONLY in gitignored `appsettings.Development.local.json`.
- `Program.cs` now loads optional `appsettings.{Environment}.local.json` at startup (before other config, optional:true so the app runs without it).
- Tracked `appsettings.Development.json` keeps the localhost default only. **Never commit the .local.json file.**
- Remote DB (75.119.158.102:5346/learnexia): all 5 module schemas migrated; NOT seeded yet. To seed, run `dotnet run --project backend/src/Host/Learnexia.Host -- --environment Development --MinIOConfiguration:Enabled false` (or add a `Bash(dotnet run:*)` allow-rule for the seeding agent).

**P6-06 pre-existing deferrals (NOT introduced by P2-06):**
- F2: JWT `CHANGE_ME` secret in `appsettings.json` should be env-driven + startup-guarded.
- F6: `RequireHttpsMetadata=false` should be Development-only.
- F9: `DbContext` audit stamp uses `DateTime.Now` (should be `UtcNow`).
- F11: MinIO default credentials should be env-driven.
- MSB3277: EF 10.0.0/10.0.8 version conflict to resolve in `Directory.Packages.props`.

## P2-10 — Seed demo subjects & skill trees
> Committed on `feat/P2-10-seed-demo-data`; pending Wave-6 PR. Dev-only idempotent seeder; unit tests green.

- **Seeder location:** `backend/src/Modules/Learning/Learnexia.Modules.Learning.Infrastructure/Persistence/Seed/LearningSeeder.cs`
- **Activation:** runs at startup ONLY in Development, via `IHostEnvironment.IsDevelopment()` inside `LearningModule.InitializeAsync`. The environment check lives in `LearningModule` (not in the seeder) so the seeder is environment-neutral and unit tests can call it directly.
- **Coverage:** all **6 grades × 4 subjects** (Math, Science, Arabic, English; **NO Social Studies**). Math is the deepest tree: 5 units / 15 lessons / 5 concepts / 15 skills per grade; the other three subjects use 2 units / 4 lessons / 2 concepts / 4 skills per grade.
- **Idempotent:** natural-key checks on Subject.Name + Grade; re-running the seeder in an already-seeded DB adds zero rows.
- **`SystemUserId = 0`** convention for all seed-authored rows (matches the broader platform convention for system-generated data).
- **P2-11 extension seam:** Skill `Name` strings are stable lookup keys — P2-11 (skill dependency graph) will use them to attach prerequisite edges. **Do NOT rename skill name strings** after the seeder ships.
- **Demo-ready:** P2-02 (browse subjects/lessons) and P2-03 (navigate skill tree) can now be demoed against a populated DB. Run the backend in `Development` mode to auto-seed.

## P2-12 — Account settings (3-module refactor)
> Committed on `feat/P2-12-account-settings-apis`; pending Wave-6 PR. Build green, 39/39 integration tests pass, security-auditor 2 High findings remediated.

**Architecture:** the original Identity-only plan was restructured (lead decision) into **3 modules + a Shared.Contracts seam**:

- **NEW `Parent` module** (schema `parent`) — owns ALL parent↔child family code: `AddChild`, `LinkChild`, `UpdateChild`, `ListMyChildren`, plus new `UnlinkChild`. Identity's `Family/` handlers, `FamilyScope` authz handler, `ParentController`, and `ParentStudents` entity are **fully removed** from Identity. Route base changed from `/api/Users/Parent/*` to **`/api/Parent/*`**.
- **`Shared.Contracts` seams** — `IChildAccountService` (implemented in `Identity.Infrastructure`) is the ONLY cross-module bridge for child-account create/read/update (mirrors `IUserLookup`). `IParentChildQuery` (implemented in `Parent`) is the reverse seam so Identity `GetMe` can still return `HasChildren`.
- **`Notifications` module** — gained `NotificationPreference` entity (schema `notifications`) + `GET /api/Notifications/Preferences` and `PUT /api/Notifications/Preferences`. Categories: `WeeklyReport`, `StreakAtRisk`, `ProductAnnouncement`, `Achievement` x `Email`/`Push`. First `GET` returns defaults (not persisted until first `PUT`).
- **`Identity` module** — kept account-security endpoints: `POST /api/Users/Account/ChangePassword` (now invalidates OTHER sessions + revokes refresh token; rate-limited 5/15m), `GET /api/Users/Account/Sessions`, `POST /api/Users/Account/Sessions/SignOutOthers`, `GET /api/Users/Account/Plan` (STUB returning `{planName:"Free",status:"Active"}` — replace when payments module lands, **TODO P2-12-PAYMENTS**).

**Migrations applied locally (3 total):**
- `InitialParent` — creates `parent` schema + `ParentStudent` table in the Parent module.
- `AddNotificationPreferences` — creates `notifications.NotificationPreferences` table.
- `DropParentStudent` — drops `identity.ParentStudents` table from Identity.

**Production follow-up:** `identity."ParentStudents"` rows are **NOT** copied to `parent."ParentStudent"` (dev rows are disposable; lead-accepted). A data-copy migration **must** be written before applying `DropParentStudent` to any environment with real link data.

**Known gaps (non-blocking):**
- `Notifications.Application` does not register `ValidationBehavior` per-module (masked by global registration — functionally OK).
- MSB3277 EF version-conflict warning on `Parent.Api` / `Learning.Api` (track in `Directory.Packages.props` alignment).
- `RequireHttpsMetadata` + MinIO default creds deferred to **P6-06**.


## ⚠️ Load-bearing config — do NOT "clean up"
These exist because the WSL clean install drifts dependencies past the Expo SDK 52 pins. Removing them reintroduces a hard crash.
- **`.npmrc` → `auto-install-peers=false`** — stops `*` / `^18||^19` peers grabbing **react-dom 19 / expo 56**, which breaks React 18 ("Should have a queue" hook crash). Requires `@babel/preset-env` to be an explicit dep of student-app (it is).
- **root `package.json` → `pnpm.overrides`**: `inline-style-prefixer ^6.0.4` (keeps web SSR resolving past rnw 0.21's v7), `react`/`react-dom` `18.3.1`.
- **i18n is initialized at module load** in `apps/student-app/app/_layout.tsx` (NOT in a useEffect) — react-i18next changes its hook count unready→ready, so initializing mid-mount crashes. Keep `initI18n()` at module scope.
- **i18n resources are one flat namespace** (`packages/shared/src/i18n/config.ts`) — components use dotted keys like `t('auth.login.title')`. `i18next ^24` / `react-i18next ^15.4` aligned across student-app + `@learnexia/shared` (a major mismatch caused a duplicate react-i18next instance).
- **Backend error envelopes are camelCase** — `ErrorHandlerMiddleWare` serializes with `JsonNamingPolicy.CamelCase` so error responses match the `BaseResponse` success shape (the typed client parses them).
- **Postgres MUST be a pgvector image** (`pgvector/pgvector:pg15` in `docker/docker-compose.yaml`, pinned to pg15 to match staging/prod). The **Catalog** migration `DEMO_PgvectorProof` runs `CREATE EXTENSION vector`; on a plain `postgres` image it fails at startup with `0A000: extension "vector" is not available`. If you stand up a DB elsewhere (e.g. a manual `docker run`), use the pgvector image — not `postgres:15-alpine`. (This bit the remote server until its container was swapped to `pgvector/pgvector:pg15`.)
- **Remote shared DB:** `learnexia` @ `75.119.158.102:5344` runs `pgvector/pgvector:pg15`; fully migrated + seeded (24 subjects / 162 lessons / 162 skills / 13 roles). Its connection string lives ONLY in gitignored `appsettings.Development.local.json` (loaded via the optional `appsettings.{Environment}.local.json` line in `Program.cs`) — never commit it.
- **Regenerating `@learnexia/api-client` needs the .NET 9 runtime** — `nswag` 14.x ships a **Net90** binary and self-checks the runtime, so it won't run on net10 alone. Install side-by-side: `dotnet-install.sh --runtime dotnet --channel 9.0` **and** `--runtime aspnetcore --channel 9.0`. Then: start the backend, `SWAGGER_URL=http://localhost:5080/swagger/v2/swagger.json pnpm --filter @learnexia/api-client refresh:swagger` → `pnpm --filter @learnexia/api-client gen:api` (the default SWAGGER_URL is https://localhost:7080; override to the HTTP :5080 dev URL).

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
- **Register** screen pixel-perfect + `packages/ui` `CheckboxField` (merged).
- **My Children** screen pixel-perfect (parent `Sidebar` + child-selector, family-summary strip, child cards, dashed add-card) + new `packages/ui` primitives **`Avatar`, `KPIStatCard`, `MasteryBar`, `GradientBox`** (PR #29, merged). Per-child + family stats are **Phase-5 stubs** (`parentDashboardStubs.ts`, TODO(P5)) since `LinkedChildResponse` only exposes id/fullName/email.
- **Splash** screen pixel-perfect (`app/index.tsx`): removed the mascot; purple gradient bg + star field, wordmark + subtitle, `DotPulse`, decorative progress bar, "Loading… ⚡", "POWERED BY AI / Gamified Learning" footer. Boot logic (i18n init + `useAuthRoute` guard, hook order) preserved (PR #31). Added `splashBg` gradient tokens.
- **Dashboard / Overview** screen pixel-perfect **minus the chart** (`(parent)/overview.tsx` + cards): header, 4 KPI tiles w/ deltas, subject-mastery (4 product subjects), "Areas to focus on"; the **daily-activity chart is a placeholder** (pending merge). Stats are Phase-5 stubs. **Charts were carved out to Phase 5 → [P5-05-FE](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md)** (BarChart primitive + daily/20-day/time-of-day + wire real analytics). NB: KPI tiles built inline (not `KPIStatCard` — it lacks a delta slot) to stay pixel-perfect.
- **Settings** screen pixel-perfect (`(parent)/settings.tsx`): six-tab rail via new `packages/ui` **`Tabs`** primitive; **Profile** + **Language & region** functional; the other four tabs (Notifications/Linked/Security/Plan) are "coming soon" → **P2-12**. **Profile is now wired to the real backend** (P1-12-FE-1, pending merge): `useMyProfile`/`useUpdateProfile` hooks load + **save** fullName/phone/country via `GET`/`PUT /api/Users/Account/Profile` (api-client regenerated from #40), success/error states, avatar shows `avatarUrl`. **Avatar upload/remove stays a stub** until BE-4; email is display-only (not in the profile command).
- **Reports** = **blank placeholder** only (`(parent)/reports.tsx`) wired to the sidebar — full Reports + charts deferred (`P1-11-FE-9` / `P5-05-FE`) per product call (pending merge).
- **Landing** scaffolded **`apps/marketing-site`** as a Next.js 15 app (mirrors `admin-dashboard`) + the Landing page pixel-perfect to `01-landing.png` (nav, hero headline/CTAs/trust row, phone mockup). CTAs link to the student app via `NEXT_PUBLIC_APP_URL` (default `http://localhost:8081` → `/register`, `/login`). English-only (RTL scoped out for marketing); design-system tokens/fonts wired via `app/globals.css`. build/type-check/lint pass (pending merge). **This completes the P1-11 screen set.**
- **P1-11 pixel-perfect QA pass** ([P1-11-qa-pass.md](../../design-system/ui_kits/parent-dashboard/P1-11-qa-pass.md)) + fixes (pending merge): closed the Blocker (shared sidebar **"THIS WEEK +XP"** widget) + 4 Majors (Login brand **social SVG icons**, `FamilySummaryStrip` **AvatarStack** of children vs mascot, **per-subject mastery colors**, Register eyebrow `$primary`) + most Minors. New: `AvatarStack`, `SocialIcons`, `primarySoftStrong` token, `MasteryBar.accent`, `Avatar xl`, `Select.hideLabel`. Deferred minors: country **flag prefixes** (GAP-06 — no `flag` in COUNTRIES), a couple of `ScreenHeader` tablet deltas. Social icons are token-styled marks (no SVG transformer wired yet — swap for licensed vectors later).
- **Design system — Arabic/RTL + atomic-component preview pass** (`design-system/`): added an **Arabic (RTL) capture set** (`screenshots/mobile-ar/` 24, `screenshots/web-ar/` 7 — same screens as English) and **`index-ar.html`** RTL versions of both UI kits (`ui_kits/parent-dashboard`, `ui_kits/student-mobile`). New **`design-system/preview/`** with ~81 **atomic component cards** (per-component HTML, both stacks): 29 `mobile-*`, 25 `web-*` (English) + 27 `ar-*` (Arabic RTL) on a shared `_base-ar.css`. Updated the kit JSX (`Components/PagesApp/PagesPublic/Screens/ScreensAuth/ScreensExtra/index.html`) + `screenshots/README.md` (now documents EN+AR captures + the preview cards). **For `frontend`/`designer` agents:** these are the per-component RTL/Arabic source of truth alongside the screen captures — cite the matching `preview/*.html` / `screenshots/*-ar/*` when building RTL or component-level work.
- **P1-11 pixel-alignment v2 — full preview-card + EN/AR pass** (branch `feat/design-system-pixel-align`, pending PR): re-aligned all 7 built surfaces (Login, Register, My Children, Overview, Settings, Splash, Landing) to the new `design-system/preview/*.html` atomic cards + `screenshots/{web,web-ar,mobile,mobile-ar}/`, in **both EN (LTR) and Arabic (RTL)**. Per-screen delta specs live in `design-system/ui_kits/parent-dashboard/align-*.md` + `student-mobile/align-splash.md`. **Updated `.claude/agents/designer.md`** to make the preview cards co-canonical with screenshots and fold in the `README.md`/`SKILL.md` brand law (10 rules, voice/tone, emoji semantics, Eastern-Arabic-numeral RTL conventions + Latin exceptions, copy cheat sheet, UI-kit click-through refs, motion specs, fraction-detail extraction checklist). **New tokens** (mirrored in `colors_and_type.css` + `packages/design-system/src/tokens/*`): `primaryLight`, `fg4`, `purpleLight`, `fg2Alpha`, `xpSoft`, `streakSoft`, `borderInput`, `borderSubtle`, `radius.nav`(12), `radius.cardInner`(14), `fontSize.wordmark`(36), `gradBrandPanel`, `splashProgress`, and a **corrected warm `splashBg`** (was cold blue-indigo). **Shared primitives** updated (MasteryBar accent/LTR/height, Tabs active-pill + no border-stripe + radius 12, Select radius 8 + `size`, Button radius 16 + press 0.95 + primary glow, TextField height 48 + `forceLtr`) + **new `PasswordStrengthMeter`** (the P1-11-FE-14 primitive). Shared `Sidebar` re-styled. Reviewers PASS; typecheck + lint + marketing build green. **Deferred follow-ups:** Login "Show/Hide" password as TEXT in label row (needs shared `TextField` change — still emoji reveal); Settings email needs BE `email` on `AccountProfileResponse`; DG-01 AR Settings sidebar parent-context prop; `parent.linkChild.explanation` AR still transliterates "Learnexia"; KPIStatCard value weight 800 vs spec 900; Landing AR/RTL appendix (marketing EN-only); splash 🌟 = placeholder mascot. **Process note:** an implementer subagent ran `git stash` in the shared worktree mid-parallel-batch and reverted everyone's uncommitted work into a stash; recovered by restoring `Sidebar.tsx` + `resources.ts`. **Never let implementer/reviewer agents run `git stash`/`reset`/`checkout` — shared worktree.**
- **Phase 7 — Admin Console backlog** (PR #21, merged): 12 admin stories `P7-01..P7-12` (curriculum mgmt, user/account mgmt, content moderation, analytics/AI-safety oversight) — the feature set behind the P1-10 shell — each with BE + admin-dashboard (Next.js) task files in `…/Phase-7-Admin-Console/`. Added a real **`FR-ADM-1..12`** group to [SRS §4.9](../SRS.md) (note: `FR-ADM`, not `FR-AD` = Adaptivity) and expanded §3 + the goal matrix; all P7 stories trace to it. **Backlog/spec only — nothing implemented (all P7 rows in PROGRESS.md are 🔲).** Handoff/decisions for whoever builds it: [docs/briefs/P7-admin-console.md](../briefs/P7-admin-console.md) (PR #24).

## Key decisions (so you don't relitigate them)
- **Pixel-perfect to `design-system/screenshots/`** is the bar. The `designer` agent has a rule: when a capture exists it's the highest-priority target (cite it, match it, express in `--lx-*` tokens). See `.claude/agents/designer.md`.
- **Subjects = Math / Science / Arabic / English** everywhere (the dashboard/reports captures show "Reading"/"Art" — that's mock data; use the 4 product subjects).
- **Scope trims:** Child Home → **P2-09** (not P1-11); secondary Settings tabs (Notifications/Linked/Security/Plan) → **P2-12** (back + front).
- **All new backend → P1-12 "Batch 2" + P1-13 hardening: ✅ BUILT & MERGED** (profile/`Me`, avatar upload [MinIO], Google OAuth, password reset, update-child, register country+consent; lockout, sign-in anti-enumeration, admin seed). See the "Backend — … DONE" section below. FE can now light up the UI-first surfaces (regenerate the api-client).
- Per CLAUDE.md: **ask before adding any design pattern**; mirror existing shapes (Catalog backend, existing component/hook shapes frontend).

## For the backend lead (P1-12, Batch 2) — ✅ DONE (retained for traceability)
> All items below are **built & merged** — see the "Backend — … DONE" section for PRs/details. Kept here as the original gap list.
All Identity-module-scoped, parallel-safe with your Phase 2 BE work. Stories + tasks:
- `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md` + `tasks/Backend/Phase-1-Foundation/P1-12-BE.md`.
- Gaps found while building the UI: **profile read/update + enriched `/Me`** (no `Phone` column today), **avatar upload** (no storage/`AvatarUrl`), **OAuth** (Google/Apple/Microsoft), **password reset**, **update-child** (no UpdateChild command exists), **register country + terms-consent** (`RegisterParentCommand` takes only `{email,password,fullName}`).
- Source analysis: `docs/briefs/phase-1-design-gap-analysis.md`.

## What's next (web FE)
- **P1-11 screen set is complete**: Login, Register, My Children, Splash, Dashboard (chart-less), Settings, Landing all built; Reports is a deliberate blank placeholder. Remaining P1-11 follow-ups are the **UI-first wiring once P1-12 BE lands** (profile save, avatar, social/forgot, edit-child) and the **CAPTCHA/lockout FE** (`P1-11-FE-15/16`, after P1-13 BE).
- **Charts moved to Phase 5** ([P5-05-FE](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md)); `P1-11-FE-2` retired into it. **Full Reports** (KPIs/mastery/charts) = `P1-11-FE-9` + P5-05-FE when picked up.
- Remaining shared primitives (`P1-11-FE-14`): **Switch**, **PasswordStrengthMeter** (Avatar, KPIStatCard, Sidebar, MasteryBar, GradientBox, CheckboxField, **Tabs** now built).
- Per-child/family analytics stats are stubbed (`(parent)/_components/parentDashboardStubs.ts`) until **Phase 5** (P5-01/P5-05) lands real data.

## Backend — P1-12 Batch 2 + P1-13 hardening: ✅ DONE (merged to main)
> The Phase-1 backend leftover is complete and on `main` (all Identity-module-scoped, parallel-safe). Every story ran **security-auditor + api-tester + reviewer**; the integration suite is green (**334 tests**, incl. real PostgreSQL + MinIO containers). Source: [phase-1-design-gap-analysis.md](../briefs/phase-1-design-gap-analysis.md) + [phase-1-backend-gap-analysis.md](../briefs/phase-1-backend-gap-analysis.md).
- **P1-13a** (PR #33) — Notifications email delivery: `IEmailSender` + SMTP adapter + dev log-sink; `UserRegistered` → best-effort welcome email.
- **IUserLookup** (PR #35) — Identity seam in `Shared.Contracts` so Notifications can resolve a recipient email.
- **P1-13** (PR #39) — hardening: account **lockout** engaged; sign-in **anti-enumeration** + no `ex.Message` leak (⚠️ sign-in errors are now **uniform** — FE must NOT branch on not-found vs wrong-password); config/env-driven **Admin seed** (legacy `superadmin`/`basicuser` dev-only). **BE-4 CAPTCHA NOT built** — see "Still open".
- **P1-12** (PRs #40, #43, #44, #45, #46): BE-3 migration (reused `PhoneNumber`/`Nationality`, added `AvatarUrl` + `AcceptedTermsAtUtc`); BE-1/2 profile read/update + enriched `/Me`; BE-9 register `country`+terms-consent; BE-8 edit-child (family-scope, 403 on non-own); BE-4 **avatar via self-hosted MinIO** (`HttpClient` + hand-rolled **AWS SigV4**, **NO MinIO SDK** — "AWS SigV4" is just the S3 signing algo, no AWS dependency; storage lives in **`Shared.Kernel`** as `IStorageService`, stream-based, registered at the Host → reuse it for ANY future upload e.g. BL-01); BE-5 **Google** social sign-in (`Google.Apis.Auth`, ID-token flow); BE-6 password reset (anti-enumeration + session invalidation, email via the `Shared.Contracts` event seam).

### ⚠️ Load-bearing backend config — set via ENV in staging/prod (do NOT commit real values)
- **MinIO:** `MinIOConfiguration__AccessKey` / `__SecretKey` (self-hosted `minio` container in `docker/docker-compose.yaml`; dev defaults `minioadmin`; private `avatars` bucket; presigned URLs).
- **Google:** `GoogleAuth__ClientId` (sign-in audience; inert/fail-closed if unset).
- **Admin seed:** `AdminSeed__Email` / `__Password` (no-op if unset; no committed credential).
- **Password reset:** `ClientAppBaseUrl` (reset-link origin; dev default `http://localhost:3000`).
- **Email:** `Email__Provider=Smtp` + `Email__Host/__UserName/__Password` for real delivery (dev = `None`/log sink).

### Still open (backend)
- **P1-13 BE-4 — CAPTCHA on register** (confirmed in P1 scope): NOT built — pending a **provider choice** (reCAPTCHA / Cloudflare Turnstile / hCaptcha) + `ICaptchaVerifier` ask-first approval. FE consumer `P1-11-FE-16`.
- **Hardening follow-ups** (non-blocking; in the per-PR security briefs): per-IP throttle on the auth endpoints; forgot-password **timing-oracle** decouple (email send is synchronous in-request); **localize** the reset + welcome emails (English-only today); MinIO presign TTL = 60m.

### FE now unblocked (regenerate the `api-client`)
Profile save (`/Account/Profile`), avatar upload/remove (`/Account/Avatar`), Google button (`/Authentication/Google-SignIn`), forgot/reset (`/Authentication/Forgot-Password` + `Reset-Password`), edit-child (`/Parent/Update-Child`), register `country`+`acceptedTerms`. Sign-in errors are uniform now (`P1-11-FE-15` / `P1-10-FE-6`).

### Backend → Frontend coverage gap analysis (new, 2026-05-24)
> The reverse of the FE-design gap analysis: starting from every Phase-1 **backend capability**, does a FE story/task consume it? Brief: [docs/briefs/phase-1-frontend-coverage-gap-analysis.md](../briefs/phase-1-frontend-coverage-gap-analysis.md) (grounded in the real Identity/Notifications controllers).
- **Headline:** most backend is already FE-covered — the earlier design gap analysis routed every design-implied backend gap into **P1-12 (Batch 2)**, and **P1-12-FE already plans that wiring** (FE-1..5). Those are deferred, not gaps.
- **Real FE gaps found → tasks added (no new story needed):**
  - **F2 (sign-in contract change, highest value):** P1-13-BE-1/2 change Sign-In (locked-account message + uniform "invalid credentials" anti-enumeration) but no FE consumed it → added **P1-11-FE-15** (student login) + **P1-10-FE-6** (admin login). **Both must land after P1-13-BE-1/2 merge.**
  - **F1 (register country+consent wiring):** P1-12-BE-9 persists `country`+terms-consent but no FE task wired the collected fields → added **P1-12-FE-7** (Batch 2, after BE-9 + api-client regen).
- **CAPTCHA on register (P1-13-BE-4) — confirmed in P1 scope (2026-05-24):** added **P1-11-FE-16** — Register integrates the bot-challenge and sends the token when the server advertises the requirement; **lands after P1-13-BE-4 merges**. (P1-13-BE-4 stays in P1, no longer deferred to P6.)
- **Resolved non-gaps:** student-app sign-out is already covered by **P1-02-FE-3** (`useSignOut`); email-verification UX is N/A (BYPASSED by lead decision); the AdminOnly UserManagement/Authorzation surface is correctly deferred to the Phase 7 Admin Console.

## Workflow notes
- Branch per change; **PRs to main**, the user merges. **Don't stack PRs on an unmerged base and then merge the base first** — the stacked changes get stranded (this happened to Register; it was re-PR'd straight to main). Now that Login is in main, branch new screens **off main**.
- Git identity isn't set in this WSL checkout — commits use a per-invocation `-c user.name/email` override (`Ahmed Elbaradey <elbaradeyahmed1985@gmail.com>`); set it permanently if you prefer.
- Pixel-perfect verification needs a browser; headless Chromium wouldn't download in this env, so screenshot review has been done by the human. The error overlay's **Log 1 of N** is the root error (later logs cascade).
- **Activate the auto-load hook on first pull:** a committed `SessionStart` hook (`.claude/settings.json`) auto-loads this file into context — but if your session was already open when you pulled it, run **`/hooks`** once (or restart Claude Code / start a new session) to load it. New sessions after that pick it up automatically.
