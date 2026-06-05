# Execution Plan — P8-Localization (P8-01, P8-02, P8-03)

## Source

| Input | Path |
|---|---|
| Pipeline Brief (authoritative) | `docs/briefs/P8-localization.md` |
| Story P8-01 | `user-stories/Phase-8-Localization/P8-01-set-child-learning-language.md` |
| Story P8-02 | `user-stories/Phase-8-Localization/P8-02-bilingual-curriculum-content.md` |
| Story P8-03 | `user-stories/Phase-8-Localization/P8-03-serve-curriculum-in-learning-language.md` |
| Tasks P8-01-BE | `tasks/Backend/Phase-8-Localization/P8-01-BE.md` |
| Tasks P8-02-BE | `tasks/Backend/Phase-8-Localization/P8-02-BE.md` |
| Tasks P8-03-BE | `tasks/Backend/Phase-8-Localization/P8-03-BE.md` |
| Localization design | `docs/architecture/localization-architecture.md` |
| Rules | `CLAUDE.md`, `docs/dev/CONVENTIONS.md`, `docs/dev/adr/0001-unit-of-work.md`, `docs/dev/adr/0002-domain-events-and-dispatch.md` |

Excluded: P8-04 (parent-only change-learning-language + Math/Science reset — follow-up story, depends on this foundation).

---

## Lead decisions baked in (do not re-open)

1. **Re-seed from scratch** — drop the current 4 single-language demo subjects; the Learning migration + seeder author 6 language-tagged roots per grade (`MATH/ar`, `MATH/en`, `SCIENCE/ar`, `SCIENCE/en`, `ARABIC/ar`, `ENGLISH/en`). The `(GradeId, SubjectCode, Language)` UNIQUE index is created immediately.
2. **Prereq edges per language tree** — Math `KnowledgeNode`/`KnowledgeEdge` prerequisite edges are authored separately inside `MATH/ar` and `MATH/en`; no cross-language edges.
3. **`LearningLanguage` stored as short string** `"ar"`/`"en"`; default `"ar"` for existing rows; claim-absent fallback = `ar` + `LogWarn` (never 500); `SubjectCode` explicitly assigned per seeded root (not parsed from `Name`); expose `SubjectCode` on subject DTOs now.
4. **Language change deferred** — all change-learning-language logic (including wiring into edit-child) is P8-04; do not add a student-facing or edit-child write path in this phase.

No open questions remain; the pipeline is clear to execute.

---

## Task inventory

| ID | Story | Stack | Agent | Summary | Est (h) | Depends on |
|---|---|---|---|---|---|---|
| P8-01-BE-1 | P8-01 | Identity | backend-feature | Add `LearningLanguage` string property to `User` entity; `"ar"`/`"en"`, separate from `PreferredLanguage` | 2 | — |
| P8-01-BE-2 | P8-01 | Identity | db-migration | EF config in `UserEntityConfig` + Npgsql Identity-schema migration; non-null column, default `"ar"` for existing rows | 2 | P8-01-BE-1 |
| P8-01-BE-3 | P8-01 | Parent/Identity | backend-feature | Accept + validate `learningLanguage` on `AddChildCommand` + validator (required, `ar`/`en` rule); thread through `CreateChildRequest` seam → `IdentityChildAccountService.CreateChildAsync`; default `PreferredLanguage` to match at creation | 3 | P8-01-BE-1 |
| P8-01-BE-4 | P8-01 | Identity | backend-feature | Emit `learning_language` JWT claim in `AuthenticationIdentityService.GetClaims`; use a named constant; refresh re-issues automatically via existing flow | 2 | P8-01-BE-1 |
| P8-01-BE-5 | P8-01 | Identity | backend-feature | Surface `learningLanguage` on `MeResponse` + populate in `GetMeQueryHandler` | 2 | P8-01-BE-3 |
| P8-01-BE-6 | P8-01 | Identity | backend-feature | Regenerate api-client Swagger snapshot (add-child + `/Me` contracts changed) | 1 | P8-01-BE-3, P8-01-BE-5 |
| P8-02-BE-1 | P8-02 | Learning | backend-feature | Add `SubjectCode` enum (`MATH`/`SCIENCE`/`ARABIC`/`ENGLISH`) and `ContentLanguage` enum (`ar`/`en`) under `Learning.Domain/Enums/`; stored as int per convention | 1 | — |
| P8-02-BE-2 | P8-02 | Learning | backend-feature | Add `SubjectCode` + `Language` properties to `Subject` entity; language only on `Subject` (no child-entity columns) | 2 | P8-02-BE-1 |
| P8-02-BE-3 | P8-02 | Learning | db-migration | EF config in `SubjectConfig` + Npgsql Learning-schema migration; new int columns + UNIQUE index on `(GradeId, SubjectCode, Language)` | 3 | P8-02-BE-2 |
| P8-02-BE-4 | P8-02 | Learning | backend-feature | Rewrite `LearningSeeder` to author 6 subject roots per grade (idempotent); change `EnsureSubjectAsync` natural key to `(GradeId, SubjectCode, Language)`; assign `SubjectCode` explicitly; author Math prereq edges within each language tree separately; re-seed from scratch (drop legacy 4-root rows) | 5 | P8-02-BE-2 |
| P8-02-BE-5 | P8-02 | Learning | backend-feature | Backfill/cleanup data step — remove orphan/untagged legacy rows via the re-seed-from-scratch approach (seeder handles this); verify no untagged trees remain post-migration+seed | 2 | P8-02-BE-3, P8-02-BE-4 |
| P8-02-BE-6 | P8-02 | Learning | backend-feature | Unit tests: seeder produces exactly 6 roots per grade; `(GradeId, SubjectCode, Language)` unique constraint upheld | 2 | P8-02-BE-4 |
| P8-03-BE-1 | P8-03 | Learning | backend-feature | Typed accessor: read `learning_language` claim from `ICurrentUserService.GetClaimValue("learning_language")`; parse to `ContentLanguage`; fallback to `ar` + `LogWarn` if claim absent | 2 | P8-01-BE-4 (claim emitted), P8-02-BE-1 (enum) |
| P8-03-BE-2 | P8-03 | Learning | backend-feature | Pure static resolver `SubjectLanguageResolver.Resolve(SubjectCode, ContentLanguage)` → `ContentLanguage` under `Learning.Domain/Services/`; mirror `LearningPathEngine`/`SkillGraphValidator` pattern; unit tests for all 4 codes × 2 media | 2 | P8-02-BE-1 |
| P8-03-BE-3 | P8-03 | Learning | backend-feature | Apply resolver in `GetSubjectsForGradeQueryHandler`: return one tree per `SubjectCode` at resolved language; expose `SubjectCode` on `StudentSubjectDto` | 3 | P8-03-BE-1, P8-03-BE-2 |
| P8-03-BE-4 | P8-03 | Learning | backend-feature | Apply resolver in `GetSubjectSkillTreeQueryHandler`, `GetSubjectLessonsQueryHandler`, `GetLessonQueryHandler`, `StartAttemptCommandHandler` — filter/guard by `Subject.Language` (for Lesson/Attempt: resolve the owning Subject and verify language match) | 4 | P8-03-BE-2 |
| P8-03-BE-5 | P8-03 | Learning | backend-feature | Apply resolver in `GetDashboardQueryHandler`: make `FallbackSubjectOrder` language-aware; filter dashboard subjects to the resolved-language tree per code | 2 | P8-03-BE-2 |
| P8-03-BE-6 | P8-03 | Learning | backend-feature | Missing-tree fallback: if resolved tree absent, serve other language tree + `ILoggerManager.LogWarn`; pattern: `GetSubjectLessonsQueryHandler.cs:112` | 1 | P8-03-BE-3 |
| P8-03-BE-7 | P8-03 | Learning | api-tester | Integration tests: ar-medium vs en-medium student in same grade; verify full language matrix across all 6 read endpoints; claim-absent fallback (no 500); refresh re-issues claim | 4 | P8-03-BE-3..BE-5 |

**Total estimate: ~42 h** (P8-01: 12 h, P8-02: 15 h, P8-03: 18 h — inclusive of tests).

---

## Dependency order

```
P8-01-BE-1  (User.LearningLanguage property)
  ├─ P8-01-BE-2  (Identity migration)
  ├─ P8-01-BE-3  (add-child wiring)  ──► P8-01-BE-5 (/Me) ──► P8-01-BE-6 (swagger)
  └─ P8-01-BE-4  (JWT claim)  ──────────────────────────────────────────────────┐

P8-02-BE-1  (enums)                                                             │
  └─ P8-02-BE-2  (Subject props)                                                │
       └─ P8-02-BE-3  (Learning migration + UNIQUE index)                       │
            └─ P8-02-BE-4  (LearningSeeder rewrite, 6 roots, per-lang edges)   │
                 └─ P8-02-BE-5  (orphan cleanup / re-seed verification)         │
                      └─ P8-02-BE-6  (unit tests)                               │
                                                                                 │
     [Wave 1 merge — both P8-01 and P8-02 merged to feat/P8-localization]       │
                                                                                 │
P8-03-BE-1  (claim accessor)  ◄─────────────────────────────────────────────────┘
P8-03-BE-2  (SubjectLanguageResolver + unit tests)  ◄── P8-02-BE-1 (enum)
  [BE-1 and BE-2 can start in parallel]
  └─ P8-03-BE-3  (subjects-for-grade filter + SubjectCode on DTO)
  └─ P8-03-BE-4  (skill-tree, lessons, lesson, start-attempt guards)
  └─ P8-03-BE-5  (dashboard language-aware)
       └─ P8-03-BE-6  (missing-tree fallback)
            └─ P8-03-BE-7  (api-tester integration matrix)
```

Key constraint: the `SubjectCode` enum (P8-02-BE-1) is the compile dependency that P8-03-BE-2 needs. P8-03 cannot build until P8-02-BE-1 is merged. Everything else in P8-03 waits for P8-03-BE-1 and P8-03-BE-2.

---

## Execution batches

### Pre-condition

All batches run on the single branch **`feat/P8-localization`**. This is a backend-only phase (no FE tasks in P8-01/02/03). No design stage.

---

### Wave 1 — P8-01 and P8-02 in parallel (independent modules, independent schemas)

These two stories touch entirely different modules and schemas (Identity schema for P8-01, Learning schema for P8-02). They share no files, no Program.cs registrations, and no `.sln`/`Directory.Packages.props` edits. They run fully in parallel.

**Batch 1a — db-migration (P8-01): Identity schema**

Agent: `db-migration`

Tasks (sequential within batch — property must exist before migration):
1. P8-01-BE-1 — Add `LearningLanguage` string property to `User`; `HasMaxLength(2)`, `HasDefaultValue("ar")`, mirror `PreferredLanguage` mapping in `UserEntityConfig`
2. P8-01-BE-2 — Generate and apply Npgsql Identity-schema migration; verify non-null column + default `"ar"` for existing rows; confirm Identity auto-migrates at startup (per CONVENTIONS §13)

Completion gate: migration runs clean against a local PostgreSQL; `Users` table carries `LearningLanguage nvarchar(2) NOT NULL DEFAULT 'ar'`.

**Batch 1b — backend-feature (P8-01): claim + onboarding + /Me (PARALLEL with 1a, but depends on 1a's entity change being done first)**

Note: in practice 1a and 1b for P8-01 must be sequential within the P8-01 story (entity before feature code). However 1a(P8-01) runs in parallel with 1a+1b(P8-02) across the two worktree sub-streams.

Agent: `backend-feature`

Tasks (sequential within batch):
1. P8-01-BE-3 — Thread `learningLanguage` through `AddChildCommand` → validator → `CreateChildRequest` → `IdentityChildAccountService.CreateChildAsync`; keep `PreferredLanguage = NormalizeLanguage(req.Language)` aligned at creation
2. P8-01-BE-4 — Add `new Claim(ClaimTypes.LearningLanguage, user.LearningLanguage)` to `AuthenticationIdentityService.GetClaims`; define a `ClaimTypes` constant for the string `"learning_language"`
3. P8-01-BE-5 — Add `LearningLanguage` field to `MeResponse`; populate in `GetMeQueryHandler`
4. P8-01-BE-6 — Regenerate Swagger snapshot

**Batch 1c — security-auditor (P8-01)**

Agent: `security-auditor`

Runs after Batch 1b completes. Focus:
- Confirm no student-facing endpoint writes `LearningLanguage` (IDOR / privilege escalation check)
- Confirm claim is server-issued only; no way for a client to inject/override `learning_language`
- Confirm add-child remains family-scoped (parent's own children only, no IDOR via the new field)
- Confirm existing rows get a safe default (not null, not an empty string)

Critical/High findings block the P8-01 reviewer gate.

**Batch 1d — api-tester (P8-01)**

Agent: `api-tester`

Runs after Batch 1b (running API required). Validate:
- `POST /api/parent/children` without `learningLanguage` → 422 (required)
- `POST /api/parent/children` with `learningLanguage: "ar"` → 200; issued JWT decodes to `learning_language=ar`
- Refresh the token; decoded refresh JWT still carries `learning_language=ar`
- `GET /Me` returns `learningLanguage` field populated

**Batch 1e — reviewer gate (P8-01)**

Agent: `reviewer`

Gates against P8-01 acceptance criteria (AC 1–4 in the brief's consolidated list). Must see security-auditor PASS (0 Critical/High) and api-tester PASS before approving.

---

**Batch 2a — db-migration (P8-02): Learning schema (PARALLEL with P8-01 batches)**

Agent: `db-migration`

Tasks (sequential within batch):
1. P8-02-BE-1 — Add `SubjectCode` enum (`MATH=0`, `SCIENCE=1`, `ARABIC=2`, `ENGLISH=3`) and `ContentLanguage` enum (`ar=0`, `en=1`) under `Learning.Domain/Enums/`; enum-as-int convention (`HasConversion<int>`)
2. P8-02-BE-2 — Add `SubjectCode` and `Language` properties to `Subject` entity; no language properties on `Unit`/`Lesson`/`Concept`/`Skill`/`QuizQuestion`
3. P8-02-BE-3 — Configure in `SubjectConfig`: map both columns via `HasConversion<int>`, add UNIQUE index `IX_Subjects_GradeId_SubjectCode_Language` on `(GradeId, SubjectCode, Language)`; generate + apply Npgsql Learning-schema migration (non-Identity modules do NOT auto-migrate — applied manually via `dotnet ef database update`)

Completion gate: migration applies cleanly; `Subjects` table carries `subject_code int NOT NULL` + `language int NOT NULL` + the UNIQUE index.

**Batch 2b — backend-feature (P8-02): seeder rewrite + unit tests**

Agent: `backend-feature`

Depends on: Batch 2a completed (entity + migration must compile before seeder can reference new props).

Tasks (sequential within batch):
1. P8-02-BE-4 — Rewrite `LearningSeeder`:
   - Change `EnsureSubjectAsync` signature to `(GradeId, SubjectCode, ContentLanguage, name)` keyed on `(GradeId, SubjectCode, Language)` UNIQUE triplet
   - Seed 6 roots per grade: `MATH/ar`, `MATH/en`, `SCIENCE/ar`, `SCIENCE/en`, `ARABIC/ar`, `ENGLISH/en`
   - Assign `SubjectCode` explicitly per root (never derive from `Name`)
   - Duplicate Math `KnowledgeNode`/`KnowledgeEdge` skill-graph edges within the `MATH/ar` tree and independently within the `MATH/en` tree; no cross-language edges; `KnowledgeNode.SubjectId` must point to the correct language root
   - Keep seeder idempotent
2. P8-02-BE-5 — Re-seed from scratch: before inserting new roots, drop/replace existing untagged Subject rows (and their child Units/Lessons/Concepts/Skills/QuizQuestions); confirm no orphan trees remain after seed run; since this is a dev-only environment with no real student progress, a clean sweep is safe
3. P8-02-BE-6 — Unit tests in `Modules.Learning.UnitTests`: assert exactly 6 roots per grade are produced; assert no two roots share `(GradeId, SubjectCode, Language)`; assert per-language Math edge sets are non-empty and non-overlapping across trees

**Batch 2c — reviewer gate (P8-02)**

Agent: `reviewer`

Gates against P8-02 acceptance criteria (AC 5–6 in the brief's consolidated list). No security-auditor gate for P8-02 (schema + seed change only; no auth or child data touch-point). No api-tester gate for P8-02 (no new HTTP surface; the seeded data is exercised in P8-03's api-tester pass).

---

### Wave 2 — P8-03 (sequential, after Wave 1 fully merged to feat/P8-localization)

P8-03 is the join point. It depends on:
- The `learning_language` JWT claim (from P8-01-BE-4)
- The `SubjectCode` enum (from P8-02-BE-1)
- The `Subject.Language` column + 6-root seed (from P8-02-BE-3/BE-4)

P8-03 cannot start until both P8-01 and P8-02 reviewer gates pass and their changes are present on the branch.

**Batch 3a — backend-feature (P8-03): resolver + claim accessor + read-path filters**

Agent: `backend-feature`

Tasks — within this batch, BE-1 and BE-2 may be done in parallel (no dependency between them), then BE-3/4/5 in parallel (all depend on BE-1 and BE-2 both done), then BE-6:

1. P8-03-BE-1 — Typed claim accessor: `ICurrentUserService.GetClaimValue("learning_language")` → parse to `ContentLanguage`; fallback to `ContentLanguage.ar` + `ILoggerManager.LogWarn` if claim absent or unrecognised; use the same named constant as P8-01-BE-4
2. P8-03-BE-2 — Pure static `SubjectLanguageResolver.Resolve(SubjectCode code, ContentLanguage learningLanguage) → ContentLanguage` under `Learning.Domain/Services/`; logic: `ARABIC → ar`, `ENGLISH → en`, `MATH → learningLanguage`, `SCIENCE → learningLanguage`; mirror `LearningPathEngine.ComputeStates` static-pure-service pattern; unit tests: 4 codes × 2 media = 8 test cases
3. P8-03-BE-3 — Apply in `GetSubjectsForGradeQueryHandler`: resolve effective language per code; filter `WHERE GradeId = g AND SubjectCode = code AND Language = resolved` for each of the 4 codes; result set = exactly 4 subjects; expose `SubjectCode` on `StudentSubjectDto` (additive change, unblocks FE iconography)
4. P8-03-BE-4 — Apply in `GetSubjectSkillTreeQueryHandler`, `GetSubjectLessonsQueryHandler`, `GetLessonQueryHandler`, `StartAttemptCommandHandler`: for handlers that load by `SubjectId` directly, validate that the subject's `Language` matches the resolved language; for `GetLessonQueryHandler`/`StartAttemptCommandHandler` that currently don't touch Subject, walk up to the owning Subject via `Lesson → Unit → Subject` join and verify `Subject.Language`; serve the resolved-language tree or apply fallback (BE-6)
5. P8-03-BE-5 — Apply in `GetDashboardQueryHandler`: replace hard-coded `FallbackSubjectOrder` name-keyed logic with code-keyed + language-resolved filtering; dashboard subject list must reflect the 4 resolved-language subjects for the authenticated student's grade
6. P8-03-BE-6 — Missing-tree fallback wire-up: in the resolver/handler, if no Subject row matches the resolved `(GradeId, SubjectCode, Language)`, query for the opposite language and serve it; `ILoggerManager.LogWarn("Missing subject tree for SubjectCode={code} Language={lang} GradeId={gradeId}. Falling back to {fallbackLang}.")`

**Batch 3b — api-tester (P8-03)**

Agent: `api-tester`

Runs against the live API after Batch 3a. Full integration matrix:

- Seed two students in the same grade: student A with `learning_language=ar`, student B with `learning_language=en`
- `GET /api/learning/subjects?gradeId=N` as student A → 4 subjects: Math(ar), Science(ar), Arabic(ar), English(en)
- `GET /api/learning/subjects?gradeId=N` as student B → 4 subjects: Math(en), Science(en), Arabic(ar), English(en)
- Confirm Arabic and English subject payloads are byte-identical for A and B
- Drill student A: skill-tree for Math(ar) subject → nodes from ar tree; skill-tree for English(en) subject → nodes from en tree
- Drill student B: skill-tree for Math(en) subject → nodes from en tree
- `GET /api/learning/lessons` for Math unit (student A) → lesson from ar tree; same unit id with student B → lesson from en tree (different subject)
- `POST /api/learning/attempts/start` (student A Math) → attempt against ar-Math lesson
- `GET /api/learning/dashboard` (student A) → Math/Science from ar tree; (student B) → Math/Science from en tree
- Claim-absent test: request with a token lacking `learning_language` → 200 with `ar` fallback (no 500)
- Refresh test: refresh student A's token; decoded refresh token still carries `learning_language=ar`

**Batch 3c — reviewer gate (P8-03 + phase foundation close)**

Agent: `reviewer`

Gates against AC 7–11 in the brief's consolidated list (resolver correctness, full read-path filter, edge-case matrix, fallback + log, conventions). Must see api-tester PASS before approving. This is also the final reviewer gate for the entire P8 localization foundation.

---

## Review gates summary

| Gate | After | Requires | Blocks |
|---|---|---|---|
| Reviewer — P8-01 | Batches 1b + 1c + 1d | security-auditor PASS + api-tester PASS | P8-03 (needs P8-01 merged) |
| Reviewer — P8-02 | Batches 2b | unit tests GREEN | P8-03 (needs P8-02 merged) |
| Reviewer — P8-03 (phase close) | Batches 3a + 3b | api-tester matrix PASS | committer / PR |

---

## Shared-file serialization notes

The two Wave 1 sub-streams are fully independent at the file level:

| File / concern | P8-01 touches | P8-02 touches | Conflict risk |
|---|---|---|---|
| `Program.cs` / `Learnexia.Host.csproj` | No | No | None |
| `Directory.Packages.props` | No | No | None |
| `Learnexia.Modular.sln` | No | No | None |
| `AuthenticationIdentityService.cs` | YES (GetClaims, single claim line) | No | None — only P8-01 writes this file |
| `LearningSeeder.cs` | No | YES (full rewrite) | None — only P8-02 writes this file |
| `Shared.Contracts/Identity/IChildAccountService.cs` | YES (CreateChildRequest) | No | None — only P8-01 writes this file |
| Identity `Migrations/` | YES (new migration file) | No | None — Identity schema only |
| Learning `Migrations/` | No | YES (new migration file) | None — Learning schema only |
| `SubjectConfig.cs` | No | YES | None — only P8-02 touches Learning configs |
| `User.cs` (Identity domain) | YES | No | None |
| `Subject.cs` (Learning domain) | No | YES | None |

P8-03 (Wave 2) writes only to Learning read-path handlers and `Learning.Domain/Services/` — no overlap with Identity files already closed by P8-01.

The single potentially contentious file: if `ICurrentUserService` in `Shared.Kernel` needs a new typed accessor method for `learning_language`, both P8-01 and P8-03 could touch it. Resolution: P8-01-BE-4 uses the **existing** `GetClaimValue(string type)` method (no interface change needed); P8-03-BE-1 adds only a local helper in the Learning infrastructure service — no `Shared.Kernel` edit required. No serialization conflict.

---

## Blockers and risks

| Blocker / risk | Severity | Resolution |
|---|---|---|
| **Re-seed wipes demo data** — all existing Subject rows (and all child Unit/Lesson/Concept/Skill/QuizQuestion rows and associated Attempts/Mastery records) are deleted and re-created with new primary-key values. Any previously issued `SubjectId`/`UnitId`/`LessonId` in test tokens or integration-test fixtures becomes stale. | High | Accepted (lead decision 1). Integration tests must not hard-code ids; they must look up ids from the seeded data at test-setup time. The api-tester and reviewer must be aware of this. |
| **UNIQUE index requires a clean seed** — if any legacy untagged rows survive in `Subjects`, the UNIQUE index on `(GradeId, SubjectCode, Language)` will fail to create (nulls in `SubjectCode`/`Language` break uniqueness). | High | Seeder must drop/replace legacy rows before inserting new roots. The migration itself adds the columns without the unique constraint first, then the seeder cleans data, then a subsequent migration step adds the constraint — OR the re-seed happens before applying the UNIQUE index (migration ordering). db-migration agent must sequence the migration so the UNIQUE index is added only after the data step. Recommended: a two-step migration (add columns non-unique first; seeder runs at startup in Development; subsequent migration adds the unique index). Alternatively, the seeder cleanup runs before `dotnet ef database update` for the unique-index step. The db-migration agent must confirm and document the sequencing. |
| **`GetLessonQueryHandler`/`StartAttemptCommandHandler` do not currently touch Subject** — these handlers load by `LessonId` and have no Subject join. Adding a Subject-language guard requires a new join (`Lesson → Unit → Subject`) and imposes a small DB query cost. | Medium | Accepted as a required change per AC8. backend-feature agent must add the join and guard in P8-03-BE-4. No cross-module FK introduced (stays within Learning schema). |
| **`GetDashboardQueryHandler` hard-codes subject name-based order** — `FallbackSubjectOrder` uses subject names, which are no longer stable keys (ar/en trees have different `Name` values for Math/Science). | Medium | backend-feature agent (P8-03-BE-5) must replace with `SubjectCode`-keyed ordering. |
| **Math skill-graph edge duplication doubles the edge set** — the seeder must wire `KnowledgeNode.SubjectId` to the correct language root for each edge set. If the seeder is not carefully separated, edges may point to the wrong root's `SubjectId`. | Medium | P8-02-BE-4 agent must explicitly pass the per-language `SubjectId` when building each tree's edge set and run the `SkillGraphValidator.AssertAcyclic` check per-language tree after seeding. |
| **Missing-tree fallback (AC 10) should not occur** — once seeded, all 6 roots are present. But if the api-tester or a test environment has a partial seed, fallback logging must fire (not a 500). | Low | P8-03-BE-6 covers this. The api-tester should not trigger it under normal conditions; if it fires in testing, it indicates a seed problem that must be fixed before the reviewer gate. |
| **P8-04 boundary** — do not wire language-change into `UpdateChildRequest`/`UpdateChildAsync` or any student endpoint. The `LearningLanguage` field on `User` should be write-once at creation in this phase. | Low | Enforced by the brief and lead decisions. reviewer must confirm no edit-child change path was introduced. |

---

## Definition of done

### Per batch

| Batch | Done when |
|---|---|
| 1a (Identity migration) | Migration applies cleanly; `Users.LearningLanguage` column exists with default `"ar"`; no Identity compile errors |
| 1b (P8-01 backend-feature) | `AddChildCommand` requires + persists `learningLanguage`; JWT decodes with `learning_language` claim; `/Me` returns `learningLanguage`; Swagger snapshot updated; build green |
| 1c (security-auditor P8-01) | 0 Critical, 0 High findings; Medium findings documented and resolved or accepted with rationale |
| 1d (api-tester P8-01) | All 4 api-tester checks pass against a running API |
| 1e (reviewer P8-01) | Reviewer approves against AC 1–4; security + api passes confirmed |
| 2a (Learning migration) | Migration applies; `Subjects` table carries `subject_code` + `language` int columns + UNIQUE index |
| 2b (P8-02 backend-feature) | Seeder produces 6 roots/grade; no orphan rows; all unit tests green; build green |
| 2c (reviewer P8-02) | Reviewer approves against AC 5–6; unit tests green |
| 3a (P8-03 backend-feature) | Resolver unit tests 8/8 green; all 6 read handlers filter by resolved language; fallback logs warn not 500; build green |
| 3b (api-tester P8-03) | Full ar-medium vs en-medium matrix verified; claim-absent fallback confirmed; refresh token verified |
| 3c (reviewer P8-03 / phase close) | Reviewer approves against AC 7–11; api-tester PASS; phase foundation declared complete |

### Overall (P8 foundation done)

All 11 acceptance criteria from the brief satisfied:
1. `User.LearningLanguage` exists, non-null, `"ar"` default for existing rows, separate from `PreferredLanguage`
2. Add-child requires + persists `learningLanguage`; `PreferredLanguage` defaults to match; no student write path
3. JWT carries `learning_language`; refresh re-issues it
4. `GET /Me` returns `learningLanguage`
5. `Subject` carries `SubjectCode` + `Language`; language only on Subject; UNIQUE index on `(GradeId, SubjectCode, Language)`
6. Seeder authors exactly 6 roots per grade; idempotent; no orphan trees
7. `SubjectLanguageResolver.Resolve` correct for all 4 codes × 2 media; unit-tested
8. Every curriculum read returns the resolved-language tree; language from JWT claim only
9. Integration matrix verified: both ar-medium and en-medium students see all 4 subjects; only Math/Science differ
10. Missing-tree fallback: serve other language + `LogWarn`; never 500
11. Conventions honored throughout: module isolation, `BaseResponse<T>`/`Successed`, `ILoggerManager`, deferred commit per ADR 0001, enums as int, no cross-module FK

Branch: `feat/P8-localization`. committer opens the PR after Batch 3c reviewer PASS. PR must not be merged to `main` by the committer (lead action).
