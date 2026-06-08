# Execution Plan — P7-01..P7-05 Phase 7 Admin Console: Curriculum Wave (Backend Only)

> Planner: 2026-06-08. Branch: `feat/phase-7-backend`. Backend-only wave; no design or frontend stages.
> All decisions baked in per lead session: `AdminActionPerformedEvent` introduced in P7-01; soft-delete everywhere; quiz stays implicit/per-lesson (no new `Quiz` aggregate entity); P7-05 granularity GATED.

---

## Source

| Artifact | Path |
|---|---|
| Pipeline briefs | `docs/briefs/P7-01.md` .. `docs/briefs/P7-05.md` |
| Batch audit | `docs/briefs/phase-7-admin-gap-analysis.md` |
| BE task files | `tasks/Backend/Phase-7-Admin-Console/P7-0{1..5}-BE.md` |
| Reference module | `backend/src/Modules/Learning/**` |
| Conventions | `docs/dev/CONVENTIONS.md` |
| Feature playbook | `docs/dev/FEATURE_PLAYBOOK.md` |
| UoW ADR | `docs/dev/adr/0001-unit-of-work.md` |

---

## Lead Decisions Baked Into This Plan

These were locked during the planning session and must NOT be re-opened by implementing agents:

1. **`AdminActionPerformedEvent`** — introduced in P7-01, in `Shared.Contracts.Admin`. All curriculum admin mutation handlers across P7-01..P7-05 publish it post-commit (best-effort, mirroring existing integration-event publication). The consumer (audit log, P7-12) is deferred. This is a `Shared.Contracts` edit — serialization point; must be the FIRST task in P7-01.

2. **Delete = soft-delete only, everywhere.** No physical deletion in this wave. The existing hard-delete Delete handlers for Subject, Unit, Lesson, Concept, Skill, QuizQuestion must be converted to set `IsActive = false` (or an equivalent `DeletedAt` / `IsDeleted` flag using `FullAuditedEntity`). The "reject delete on non-empty parent" guard still applies — a soft-delete of a unit that has active lessons is blocked (or optionally cascades soft-delete to children; the implementing agent must call this out and get a quick lead sign-off before coding). New columns required on Subject, Unit, Lesson, QuizQuestion (and ContentBlock when introduced by P7-02).

3. **P7-04 quiz model = implicit/per-lesson.** No new `Quiz` aggregate entity. "Quiz" is the set of `QuizQuestion` rows attached to a Lesson/Skill via the existing loose-int reference. The only migration for P7-04 is `QuizQuestion.SequenceOrder int NOT NULL DEFAULT 0` + an `(LessonId, SequenceOrder)` index (if it does not already exist). The attach command stores `QuizQuestion.LessonId`/`SkillId` directly.

4. **P7-05 granularity is OPEN.** P7-05 is a GATED batch. It is listed here for sequencing visibility but MUST NOT be dispatched until the lead decides per-entity vs per-tree versioning (see Blockers section).

5. **AdminOnly policy for all mutations.** The BE-1/BE-2 "apply AdminOnly" tasks across all stories are confirmed ALREADY done (auth hotfix PR #104 shipped them). Agents must verify this during review but must not re-apply or duplicate.

---

## Task Inventory

### P7-01 — Manage subjects & units (SP 5, est. ~28 h)

| ID | Stack | Summary | Est (h) | Depends On |
|---|---|---|---|---|
| P7-01-BE-0 | backend-feature | Introduce `AdminActionPerformedEvent` record in `Shared.Contracts.Admin` (serialization point — run first, alone) | 1 | — |
| P7-01-BE-1 | backend-feature | Verify `[Authorize(AdminOnly)]` on `SubjectsController` Create/Update/Delete — already done per auth hotfix; confirm and document | 1 | auth hotfix PR #104 |
| P7-01-BE-2 | backend-feature | Verify `[Authorize(AdminOnly)]` on `UnitsController` Create/Update/Delete — already done; confirm and document | 1 | P7-01-BE-1 |
| P7-01-BE-3 | db-migration | EF migration: add `Subject.SequenceOrder int NOT NULL DEFAULT 0`, `Subject.IsActive bool NOT NULL DEFAULT true`, `Unit.IsActive bool NOT NULL DEFAULT true`; update `SubjectConfig` + Unit config; add index `(GradeId, SequenceOrder)` on Subject | 3 | P7-01-BE-1, P7-01-BE-2 |
| P7-01-BE-4 | backend-feature | `ReorderSubjectsCommand` + `ReorderUnitsCommand`: batch SequenceOrder update in an explicit transaction (language-tree scoped, not shared across Ar/En siblings); emit `AdminActionPerformedEvent` | 4 | P7-01-BE-3, P7-01-BE-0 |
| P7-01-BE-5 | backend-feature | `SetSubjectActiveCommand` + `SetUnitActiveCommand` (toggle `IsActive`); redefine Delete handlers to soft-deactivate (set `IsActive=false`) instead of hard-delete; student-facing reads filter `IsActive==true`; emit `AdminActionPerformedEvent` | 3 | P7-01-BE-3, P7-01-BE-0 |
| P7-01-BE-6 | backend-feature | Unit delete guard: block soft-delete of a unit that still has active lessons (`Successed=false` + clear message); or define cascade soft-delete policy — MUST call out to lead before coding | 3 | P7-01-BE-5 |
| P7-01-BE-7 | backend-feature | Subject CRUD: expose `SubjectCode`/`Language`/`SequenceOrder`/`IsActive` on `SubjectDto`/`UnitDto`; validator rejects unknown `SubjectCode` or 5th code; wrap unique-index `DbUpdateException` on duplicate `(GradeId,SubjectCode,Language)` → `Successed=false`; emit `AdminActionPerformedEvent` | 4 | P7-01-BE-3, P7-01-BE-0 |
| P7-01-BE-8 | backend-feature | `GetSubjectLanguageCoverageQuery(gradeId)`: returns which `(SubjectCode, Language)` of the 6 expected roots exist/missing; AdminOnly, read-only | 3 | P7-01-BE-7 |

**P7-01 total estimate: ~23 h net (BE-0 is additive; BE-1/BE-2 are verify-only ~2 h combined)**

---

### P7-02 — Manage lessons & lesson content (SP 8, est. ~27 h)

| ID | Stack | Summary | Est (h) | Depends On |
|---|---|---|---|---|
| P7-02-BE-1 | backend-feature | Verify `[Authorize(AdminOnly)]` on `LessonsController`; extend `Lesson` with `EstimatedMinutes int NOT NULL DEFAULT 0` + `IsActive bool NOT NULL DEFAULT true`; EF migration; expose new fields on `LessonDto`; emit `AdminActionPerformedEvent` on create/update/delete | 6 | P7-01-BE-3, P7-01-BE-0 |
| P7-02-BE-2 | db-migration | EF migration: create `ContentBlocks` table (`Id`, `LessonId` FK, `Type int`, `Payload jsonb`, `SequenceOrder int`, audit cols); index `(LessonId, SequenceOrder)`; `DeleteBehavior.Cascade` or handler-managed per atomic-delete decision | 5 | P7-02-BE-1 |
| P7-02-BE-3 | backend-feature | `ContentBlock` entity + `ContentBlockType` enum (Domain); EF config (jsonb mapping for `Payload`); `AddContentBlockCommand` / `EditContentBlockCommand` / `DeleteContentBlockCommand`; per-type payload validators (text/image/video/callout shape); media uploads via `IStorageService` (no module-local MinIO); emit `AdminActionPerformedEvent` | 5 | P7-02-BE-2, P7-01-BE-0 |
| P7-02-BE-4 | backend-feature | `ReorderContentBlocksCommand` (batch, explicit transaction) + `ReorderLessonsCommand` (batch, explicit transaction); language-tree scoped | 4 | P7-02-BE-3 |
| P7-02-BE-5 | backend-feature | Lesson delete: soft-deactivate (`IsActive=false`) + cascade/handle ContentBlock deactivation/removal atomically in one explicit transaction; emit `AdminActionPerformedEvent` | 3 | P7-02-BE-3 |
| P7-02-BE-6 | backend-feature | Language-inheritance: no `Language` column on Lesson/ContentBlock; resolve via `SubjectLanguageResolver`; create/update validators reject orphan/cross-tree placement (`Successed=false`); read DTOs surface resolved language (read-only) | 4 | P7-02-BE-1, P7-01-BE-7 |

**P7-02 total estimate: ~27 h**

---

### P7-03 — Author skills & the skill dependency graph (SP 8, est. ~27 h)

| ID | Stack | Summary | Est (h) | Depends On |
|---|---|---|---|---|
| P7-03-BE-1 | backend-feature | Skill CRUD: verify `[Authorize(AdminOnly)]` on `SkillsController`; confirm existing `MasteryThreshold`/`EstimatedTimeMinutes` props; extend validators; emit `AdminActionPerformedEvent` | 6 | P7-01-BE-0 |
| P7-03-BE-2 | backend-feature | `AddKnowledgeEdgeCommand(SourceNodeId, TargetNodeId, RelationshipType, Strength)` + `RemoveKnowledgeEdgeCommand(EdgeId)`; new controller routes in `SkillsController` or `KnowledgeGraphController` (AdminOnly on mutations); emit `AdminActionPerformedEvent` | 5 | P7-03-BE-1 |
| P7-03-BE-3 | backend-feature | Acyclic check reuse: load all existing Prerequisite edges + proposed edge, call `SkillGraphValidator.AssertAcyclic`; catch `InvalidOperationException` → `Successed=false` + clear message (never 500); do not re-implement the validator | 4 | P7-03-BE-2 |
| P7-03-BE-4 | backend-feature | `GetGraphQuery(subjectId?, language?)` → `SkillGraphDto { nodes[], edges[] }`; reuse existing `GetPrerequisitesQuery`/`GetUnlockedByQuery` for prereq/unlock lists; per-language filter | 4 | P7-03-BE-3 |
| P7-03-BE-5 | db-migration | Migration only if BE-1 surfaces a genuinely new Skill property — verify first; if `MasteryThreshold`/`EstimatedTimeMinutes` already present, this task is a NO-OP (skip migration, note the verify result). Index on `KnowledgeNode(SubjectId)` if absent | 2 | P7-03-BE-1 |
| P7-03-BE-6 | backend-feature | Cross-language edge guard: resolve both nodes' owning-Subject `Language` via `SubjectLanguageResolver`; reject mismatch → `Successed=false`; fail-closed on dangling `SubjectId` (never 500); run alongside acyclic check | 3 | P7-03-BE-2, P7-01-BE-7 |
| P7-03-BE-7 | backend-feature | Per-language graph reads: `GetGraph`/prereq/unlock queries accept optional `language` filter or derive from chosen subject; `[Authorize]` on reads (any authenticated); AdminOnly on mutations | 3 | P7-03-BE-4, P7-03-BE-6 |

**P7-03 total estimate: ~27 h (minus migration if NO-OP)**

---

### P7-04 — Manage quizzes & questions (SP 8, est. ~25 h)

> Quiz model baked-in: implicit/per-lesson (no new `Quiz` entity). "Quiz" = the set of `QuizQuestion` rows for a Lesson/Skill.

| ID | Stack | Summary | Est (h) | Depends On |
|---|---|---|---|---|
| P7-04-BE-1 | backend-feature | Quiz authoring surface on `QuizzesController` (or new admin sub-route): add admin CRUD endpoints (list/get by lessonId, create quiz as a named grouping of questions — see note); `[Authorize(AdminOnly)]` on all authoring endpoints; verify existing student attempt endpoints remain `[Authorize(Roles=Student)]` and untouched; emit `AdminActionPerformedEvent` | 6 | P7-01-BE-0, P7-02-BE-1 |
| P7-04-BE-2 | backend-feature | `AddQuestionCommand` / `EditQuestionCommand` / `DeleteQuestionCommand` over `QuizQuestion`; admin DTO includes `CorrectAnswer` (distinct from student-facing `QuizQuestionDto` which excludes it — keep separation); emit `AdminActionPerformedEvent` | 5 | P7-04-BE-1 |
| P7-04-BE-3 | backend-feature | Wire `QuizQuestionTypeValidation.Validate(type, options, correctAnswer, localizer)` into `AddQuestionCommandValidator` + `EditQuestionCommandValidator` (map null-return = valid, non-null = FluentValidation failure); reuse P2-06 type discriminator; do NOT re-implement the 4 per-type shapes | 5 | P7-04-BE-2 |
| P7-04-BE-4 | backend-feature | Attach command: `AttachQuizToLessonCommand(quizId/lessonId)` / `AttachQuizToSkillCommand(quizId/skillId)`; resolve lesson/skill existence + language tree (via `SubjectLanguageResolver`); reject cross-language attach → `Successed=false`; intra-`Learning` FK allowed; emit `AdminActionPerformedEvent` | 5 | P7-04-BE-1, P7-01-BE-7 |
| P7-04-BE-5 | db-migration | Migration: add `QuizQuestion.SequenceOrder int NOT NULL DEFAULT 0`; add index `(LessonId, SequenceOrder)` if absent; `ReorderQuestionsCommand` (batch, explicit transaction) | 3 | P7-04-BE-2 |
| P7-04-BE-6 | backend-feature | Language inheritance: `QuizQuestion` has no language column; language derived from owning Lesson → Unit → Subject; read DTOs surface resolved language; same-tree attach validator (BE-4) enforces this | 3 | P7-04-BE-4, P7-01-BE-7 |

**P7-04 total estimate: ~27 h**

---

### P7-05 — Publish, version & preview curriculum content (SP 8, est. ~31 h) — GATED

> This batch is BLOCKED on the versioning-granularity decision. Do not dispatch until the lead resolves it (see Blockers section).

| ID | Stack | Summary | Est (h) | Depends On |
|---|---|---|---|---|
| P7-05-BE-1 | db-migration | Add `LifecycleState int NOT NULL DEFAULT <Published>` to Subject, Unit, Lesson, QuizQuestion (backfill = Published so live content stays visible); exact entity scope depends on granularity decision | 5 | P7-01..P7-04 merged, granularity decision |
| P7-05-BE-2 | db-migration | Create `ContentVersions` table: `Id`, `EntityType int`, `EntityId int`, `VersionNumber int`, `PublishedAtUtc timestamptz`, `PublishedBy string`, `Snapshot jsonb`, `Language int (ContentLanguage)`, audit cols; index `(EntityType, EntityId, VersionNumber)`; timestamptz note: call `.ToUniversalTime()` at mapping boundary per HANDOFF P4-11 | 5 | P7-05-BE-1 |
| P7-05-BE-3 | backend-feature | `PublishCommand(entityType, entityId)`: snapshot current draft → new `ContentVersion`, flip `LifecycleState`, atomic explicit transaction; read admin user id from JWT via `IHttpContextAccessor` claim pattern (mirror `LearningLanguageClaimAccessor`); emit `AdminActionPerformedEvent`; AdminOnly | 5 | P7-05-BE-2, P7-01-BE-0 |
| P7-05-BE-4 | backend-feature | Student-facing reads filter `LifecycleState == Published` / latest published `ContentVersion`; admin reads return live + pending draft (`?view=admin`); touches existing P2-* read handlers — change carefully, regression-test P2-02/04/05/09 | 4 | P7-05-BE-3 |
| P7-05-BE-5 | backend-feature | `GetPreviewQuery(entityType, entityId)`: render draft as student would without publishing; media via `IStorageService`; AdminOnly | 3 | P7-05-BE-4 |
| P7-05-BE-6 | backend-feature | `RollbackCommand(entityType, entityId)`: restore previous published `ContentVersion` atomically (explicit transaction); per-language tree scoped; AdminOnly; emit `AdminActionPerformedEvent` | 3 | P7-05-BE-3 |
| P7-05-BE-7 | backend-feature | Per-language tree scoping: publish/rollback/preview operate on a single `(SubjectCode, Language)` tree resolved via `SubjectLanguageResolver`; `ContentVersion.Language` records the resolved language; publishing `ar` never touches `en` sibling | 3 | P7-05-BE-3, P7-01-BE-7 |
| P7-05-BE-8 | backend-feature | `GetPublicationCoverageQuery(gradeId)`: publish state per `(SubjectCode, Language)` tree (complements P7-01-BE-8 existence coverage with publish-state coverage); read-only, AdminOnly | 3 | P7-05-BE-4, P7-01-BE-8 |

**P7-05 total estimate: ~31 h (gated)**

---

## Shared-File Serialization Points

Per `docs/dev/PARALLELISM.md`, edits to these files must be serialized (one story at a time — no concurrent agents touching them):

| Shared File / Resource | Stories That Touch It | Serialization Rule |
|---|---|---|
| `Shared.Contracts` (new `AdminActionPerformedEvent`) | P7-01 (introduces it) | Introduced in P7-01-BE-0 only; P7-02..P7-05 consume it read-only — no further `Shared.Contracts` edits needed |
| `LearningDbContext` (migrations) | P7-01, P7-02, P7-03, P7-04, P7-05 | One migration per story, sequentially; each story's migration must be generated AFTER the prior story's migration has been applied |
| `Program.cs` / `LearningModule.cs` | None in this wave (no new module) | Not a constraint for this wave |
| `Directory.Packages.props` | None expected | Monitor; if a new NuGet package is needed, serialize the edit |

---

## Dependency Order (Story Level)

```
P7-01 (foundation: event contract + schema columns + soft-delete pattern)
  └─► P7-02 (lessons + ContentBlock — depends on P7-01's migration + IsActive pattern + AdminActionPerformedEvent)
  └─► P7-03 (skills + graph — depends on P7-01's event contract + language-tree model; no P7-02 dep)
  └─► P7-04 (quiz authoring — depends on P7-01's event contract + language-tree model; no P7-02/03 dep)
        P7-02 + P7-03 + P7-04 can run in parallel AFTER P7-01 is fully reviewed and committed
  └─► P7-05 (lifecycle/versioning — depends on ALL of P7-01..P7-04 + granularity decision)
```

Key constraint: P7-02, P7-03, and P7-04 each add a separate EF migration to `LearningDbContext`. Although their feature code can be developed in parallel, the migrations themselves must be created sequentially (each one rebased on top of the prior story's migration). The `committer` agent must rebase/reconcile migration files when committing each story to `feat/phase-7-backend`.

---

## Execution Batches

### Batch 0 — P7-01 Serialization Pre-step (sequential, alone)
**Agent:** `backend-feature`
**Tasks:** P7-01-BE-0 only
**What:** Introduce `AdminActionPerformedEvent` record in `Shared.Contracts.Admin`. This is a `Shared.Contracts` edit — must complete and be reviewed before any other story begins, because P7-02..P7-05 all import it.

**Shared-file edit:** `Shared.Contracts` — run alone, not in parallel with any other story.

---

### Batch 1 — P7-01 Migration (sequential, after Batch 0)
**Agent:** `db-migration`
**Tasks:** P7-01-BE-3
**What:** Single EF migration on `LearningDbContext`: `Subject.SequenceOrder`, `Subject.IsActive`, `Unit.IsActive`. Non-destructive defaults.
**Gate:** must apply cleanly before Batch 2.

---

### Batch 2 — P7-01 Feature Implementation (sequential, after Batch 1)
**Agent:** `backend-feature`
**Tasks:** P7-01-BE-1, P7-01-BE-2 (verify-only), P7-01-BE-4, P7-01-BE-5, P7-01-BE-6, P7-01-BE-7, P7-01-BE-8
**What:** All P7-01 feature work. BE-1/BE-2 are verify-only (confirm AdminOnly already applied by auth hotfix). BE-4..BE-8 are net-new handlers, validators, DTOs, guards, coverage query.
**Implementation note for BE-6:** agent must surface the soft-delete cascade policy choice (block vs cascade) to the lead before writing the guard; this is an implementation note, not a blocker on starting the batch.

---

### Batch 3 — P7-01 api-tester (sequential, after Batch 2)
**Agent:** `api-tester`
**Scope:** Integration tests over the running API for P7-01:
- Reorder scoped per language tree (Ar-tree reorder doesn't touch En-tree)
- Soft-deactivate hides items from student reads but preserves them for admin reads
- Duplicate `(GradeId,SubjectCode,Language)` → `Successed=false`
- Unit soft-delete with active lessons → `Successed=false`
- AdminOnly mutations → 403 for non-admin
- Language-coverage report returns correct gaps

---

### Batch 4 — P7-01 security-auditor (sequential, after Batch 3)
**Agent:** `security-auditor`
**Scope:** Admin-write curriculum surface. Confirm AdminOnly on all new mutations; no IDOR on reorder/coverage endpoints; soft-delete cannot be invoked by non-admin; duplicate-tree rejection is server-enforced (not trust-client). Expected finding level: Info. Critical/High findings block.

---

### Batch 5 — P7-01 reviewer + committer (sequential, after Batch 4)
**Agent:** `reviewer` then `committer`
**Gate:** Verify all P7-01 ACs, CONVENTIONS.md compliance (`Successed`, `NewResult`, `ILoggerManager`, `BaseResponse<T>`, no UoW, explicit transaction for multi-writes, language-tree scoping correct, no cross-module FK). On PASS: `committer` commits P7-01 to `feat/phase-7-backend`.

---

### Batch 6 — P7-02 + P7-03 + P7-04 Migrations (parallel within story, sequential across DbContext)
**Agent:** `db-migration` (three sub-tasks, must be sequenced — same DbContext)

Because migrations share `LearningDbContext`, they cannot be generated concurrently. Recommended order:

1. P7-02-BE-2 migration first (ContentBlocks table + Lesson.EstimatedMinutes + Lesson.IsActive)
2. P7-03-BE-5 migration second (Skill props if any; likely NO-OP — agent verifies first)
3. P7-04-BE-5 migration third (QuizQuestion.SequenceOrder + index)

Each migration is generated, applied, and confirmed before the next begins. The feature agents for P7-02/P7-03/P7-04 may begin their non-migration work in parallel (they can code against the schema once the prior story's migration is applied), but EF `Add-Migration` commands are sequential.

**Parallelism note:** P7-02-BE-1, P7-03-BE-1, and P7-04-BE-1 (verify auth + start entity scaffolding) CAN start in parallel with each other and with the migration sequence, since they do not require the new columns to exist for initial scaffolding. However, any handler that queries the new columns must wait on the migration for that story.

---

### Batch 7 — P7-02 + P7-03 + P7-04 Feature Implementation (parallel)
**Agent:** `backend-feature` — three independent sub-batches running in parallel

**P7-02 sub-batch (sequential within P7-02):**
P7-02-BE-1 → P7-02-BE-3 → P7-02-BE-4 → P7-02-BE-5 → P7-02-BE-6

**P7-03 sub-batch (sequential within P7-03):**
P7-03-BE-1 → P7-03-BE-2 → P7-03-BE-3 + P7-03-BE-6 (parallel) → P7-03-BE-4 → P7-03-BE-7

**P7-04 sub-batch (sequential within P7-04):**
P7-04-BE-1 → P7-04-BE-2 → P7-04-BE-3 → P7-04-BE-4 → P7-04-BE-6

P7-02, P7-03, and P7-04 are independent of each other at the feature level. They share only the branch and the migration sequence (handled in Batch 6). The three sub-batches may be dispatched simultaneously.

**File conflict watch:** All three stories write to `LearningDbContext` indirectly via migrations, and all three write to the `Learning` module's Application layer. Watch for namespace collisions in `DependencyInjection.cs` / `LearningModule.cs` — the committer must rebase carefully.

---

### Batch 8 — P7-02 + P7-03 + P7-04 api-tester (parallel, after Batch 7)
**Agent:** `api-tester` — three independent test suites, can run in parallel

**P7-02 integration tests:**
- ContentBlock CRUD + reorder persists correctly
- Atomic lesson delete (blocks handled in same transaction)
- Per-type payload validation → `Successed=false` on bad shape
- Cross-tree lesson placement rejected
- Media stored via `IStorageService` (not raw URL)
- AdminOnly 403

**P7-03 integration tests:**
- Edge add/remove
- Cycle rejected and NOT persisted (verify with 3-node A→B→C then C→A)
- Cross-language edge rejected
- `GetGraph` returns single-language tree
- Prereq/unlock reads scoped per language
- AdminOnly 403

**P7-04 integration tests:**
- Each of 4 question types: valid shape accepted, invalid shape → per-type `Successed=false`
- Question ordering persists
- Attach within same language tree succeeds; cross-language attach → `Successed=false`
- AdminOnly 403
- `CorrectAnswer` NOT present in student-facing `QuizQuestionDto` (confirm no leak)

---

### Batch 9 — P7-02 + P7-03 + P7-04 security-auditor (parallel, after Batch 8)
**Agent:** `security-auditor`

**P7-02 audit:**
- Admin-write + **file-upload** surface (media via `IStorageService`): check upload type/size handling, no path traversal, AdminOnly enforced on block mutations, no IDOR on block operations.
- Confirm `CorrectAnswer` exclusion pattern carries through lesson content surface.

**P7-03 audit:**
- Admin-write authz: confirm AdminOnly on edge add/remove and skill mutations.
- No IDOR on edge operations.
- Fail-closed on dangling node `SubjectId` (must not 500, must return `Successed=false`).

**P7-04 audit:**
- Admin-write + **CorrectAnswer exposure**: confirm admin authoring DTO is AdminOnly-gated and `CorrectAnswer` never appears on student-facing reads (`QuizQuestionDto`).
- No IDOR on question-level operations.
- Same-tree attach enforced server-side.

Expected level: Info to Low. Critical/High findings block.

---

### Batch 10 — P7-02 + P7-03 + P7-04 reviewer + committer (sequential per story, after Batch 9)
**Agent:** `reviewer` then `committer` — review each story independently, commit sequentially to `feat/phase-7-backend`

**Review gates:**
- P7-02: all ACs + CONVENTIONS.md + `IStorageService` used (no module-local MinIO) + ContentBlock entity shape + atomic delete via explicit transaction + no `Quiz` entity introduced.
- P7-03: all ACs + `SkillGraphValidator` REUSED (not duplicated) + no backend design pattern introduced + cross-language guard fail-closed + no `KnowledgeNode`/`KnowledgeEdge` duplication.
- P7-04: all ACs + `QuizQuestionTypeValidation` WIRED (not re-implemented) + no `Quiz` aggregate entity + no `assessment` module created + student DTO `CorrectAnswer` exclusion preserved + implicit quiz model confirmed.

**Commit order:** P7-02 → P7-03 → P7-04 (sequential commits on same branch; migration rebasing required if any conflicts).

---

### Batch 11 — P7-05 (GATED — do not dispatch until decision is resolved)
**Blocked on:** Lead versioning-granularity decision (see Blockers section).

When unblocked, the execution order is:

1. `db-migration`: P7-05-BE-1 → P7-05-BE-2 (sequential, both on `LearningDbContext`)
2. `backend-feature`: P7-05-BE-3 → P7-05-BE-4 → P7-05-BE-5 + P7-05-BE-6 (parallel) → P7-05-BE-7 → P7-05-BE-8
3. `api-tester`: full P7-05 suite (see task file) + **regression of P2-02/P2-04/P2-05/P2-09 student reads** (the published-version filter modifies those paths)
4. `security-auditor`: publish/rollback/preview governance surface (`publishedBy` from JWT not client; no draft leakage to students; AdminOnly on publish/rollback/preview; no IDOR)
5. `reviewer` then `committer`

---

## Review Gates Summary

| Gate | After Batch | Stories Covered | Required Stages |
|---|---|---|---|
| Gate 1 | Batch 0 | P7-01-BE-0 (`AdminActionPerformedEvent`) | reviewer (quick) |
| Gate 2 | Batch 2 | P7-01 feature | api-tester → security-auditor → reviewer |
| Gate 3 | Batch 7 (P7-02) | P7-02 feature | api-tester → security-auditor → reviewer |
| Gate 4 | Batch 7 (P7-03) | P7-03 feature | api-tester → security-auditor → reviewer |
| Gate 5 | Batch 7 (P7-04) | P7-04 feature | api-tester → security-auditor → reviewer |
| Gate 6 (gated) | Batch 11 | P7-05 | api-tester (+ P2 regression) → security-auditor → reviewer |

**Security-auditor is required for every story in this wave** — all involve admin-write surfaces over curriculum content. P7-02 additionally has a file-upload surface (IStorageService media); P7-04 has a CorrectAnswer-exposure surface; P7-05 has publish-governance (draft leakage risk).

---

## Commit / PR Strategy

One shared branch: `feat/phase-7-backend`. The `committer` agent commits each story incrementally after its reviewer PASS:

```
feat(P7-01): manage subjects & units — reorder, soft-delete, IsActive, coverage
feat(P7-02): manage lessons & content blocks — ContentBlock entity, reorder, atomic delete
feat(P7-03): author skills & skill dependency graph — edge CRUD, acyclic reuse, cross-language guard
feat(P7-04): manage quizzes & questions — implicit quiz model, per-type validators, attach guard
feat(P7-05): publish, version & preview curriculum — lifecycle, ContentVersion, rollback    [when unblocked]
```

A **single PR to `main`** is opened at the end of the wave (after all five stories pass review, or after P7-01..P7-04 if P7-05 is deferred). This PR targets `main` and merges AFTER auth hotfix PR #104.

Recommendation: open an **incremental draft PR** immediately after P7-01 is committed (documents the branch and flags the P7-05 gate visually). Promote to ready when the wave is complete.

**Never amend, never force-push, never merge the PR without explicit lead instruction.**

---

## Blockers / Open Questions

### BLOCKER — P7-05 versioning granularity (must resolve before Batch 11)

**Decision required:** Is `LifecycleState`/`ContentVersion` granularity **per-entity** (each subject/unit/lesson/quiz item publishes independently) or **per `(SubjectCode, Language)` tree** (publish the whole tree as one atomic unit)?

- This drives: the `ContentVersion` shape (single-entity ref vs tree-level identifier), the `PublishCommand` signature, the read-filter logic, and the rollback scope.
- AC 2 says "publish acts on one `(SubjectCode, Language)` tree" — but AC 1 says "editing a curriculum item" accumulates as draft, implying per-item state.
- Recommendation: per-entity `LifecycleState` + per-entity `ContentVersion` rows, all published in one transaction when the tree is published (batch of per-entity versions). Lead must confirm.

Also resolve before P7-05 migration:
- **Default `LifecycleState` on migration**: existing seeded content must default to `Published` (so live content stays student-visible). Getting this wrong hides the entire curriculum from students.
- **Snapshot scope**: does one `ContentVersion` snapshot a single entity's payload (jsonb) or the full sub-tree? Atomic rollback fidelity depends on this.

### IMPLEMENTATION NOTE — Soft-delete cascade policy (P7-01-BE-6, P7-02-BE-5)

When an admin soft-deletes a unit that has active lessons, two valid behaviors exist:
- **Block**: reject with `Successed=false` + "unit has active lessons" (safest; matches the existing "unit not empty" guard spirit)
- **Cascade**: soft-delete all active lessons (and their ContentBlocks) transitively

The implementing agent for P7-01-BE-6 and P7-02-BE-5 must surface this choice to the lead before coding the guard, as it determines both the handler logic and the migration `DeleteBehavior`. Recommendation: **Block** for units with active lessons; cascade for ContentBlocks under a lesson (since blocks have no independent lifecycle).

### OPEN QUESTION — ContentBlock type set vs Lesson Renderer (P7-02)

The brief flags risk that the `text/image/video/callout` block types may diverge from what the P2-05 Lesson Renderer (student app) can actually render. This should be confirmed against the P2-05 student-app implementation before the P7-02 per-type validators are frozen. If the type set changes, the enum and validators change with it. This is non-blocking for P7-02 to start, but must be resolved before the P7-02 api-tester signs off.

### OPEN QUESTION — P7-03 Skill→Node relationship for AddEdge (P7-03-BE-2)

The FE contract uses "edges between two skills" but edges physically connect `KnowledgeNode`s (a node optionally wraps a Skill via `SkillId`). The `AddKnowledgeEdgeCommand` should take **node IDs** (recommended — matches the relational model); the FE resolves Skill→Node. Confirm before coding BE-2.

### OPEN QUESTION — P7-03 Auto-create KnowledgeNode on Skill create (P7-03-BE-1)

Should creating a Skill automatically create a wrapping `KnowledgeNode`, or is node creation a separate admin step? Current assumption: node creation is separate/seeded; this story authors edges over existing nodes. Confirm before BE-1 is finalized.

### INFORMATIONAL — `AdminActionPerformedEvent` publish pattern

All mutation handlers in P7-01..P7-05 should publish `AdminActionPerformedEvent` **after commit** (i.e., via the domain-event/integration-event post-commit dispatch path per ADR 0001 §2 and CONVENTIONS.md §8). The event is best-effort (the consumer, P7-12, is deferred). Do NOT let a publish failure roll back the primary mutation — wrap the publish in a try/catch at the handler level.

### INFORMATIONAL — Migration rebasing on shared branch

P7-02, P7-03, and P7-04 all add EF migrations to `LearningDbContext`. The `committer` agent must ensure that when each story is committed to `feat/phase-7-backend`, the new migration's `Up`/`Down` methods do not conflict with the prior story's migration. The safest approach: generate each migration only after the prior story's migration is committed and `dotnet ef database update` has been run.

---

## Definition of Done

### Per-batch (each story)
- All tasks in the batch completed and passing
- `dotnet build` clean (no warnings treated as errors)
- EF migration applies cleanly with `dotnet ef database update` against a local PostgreSQL `Learnexia` database
- All api-tester integration tests pass on the running API
- Security-auditor finds no Critical or High findings (Info/Low accepted with notes)
- Reviewer PASSES against the story's ACs and CONVENTIONS.md checklist
- Committed to `feat/phase-7-backend` by `committer`

### Overall wave (P7-01..P7-04, with P7-05 gated separately)
- All 5 stories' reviewer gates PASSED (or 4 + P7-05 explicitly deferred)
- Student-facing reads for P2-02/P2-04/P2-05/P2-09 still pass (soft-delete + IsActive filter must not break them) — confirmed by api-tester regression at P7-02 latest
- `AdminActionPerformedEvent` published from every admin mutation handler; no consumer yet (deferred to P7-12 wave)
- No physical deletes anywhere in the wave (soft-delete only)
- No `assessment` module created; all quiz/question code lives in `Learning`
- No backend design patterns introduced without lead approval
- `Shared.Contracts` contains exactly one new type: `AdminActionPerformedEvent`
- Single PR to `main` opened (or draft PR promoted to ready) targeting `main`, description covers all 4 (or 5) stories
- `docs/dev/HANDOFF.md` updated in the same PR: new columns, event contract, soft-delete policy, P7-05 gate status, any gotchas

---

**Plan ready — dispatch Batch 0.**
