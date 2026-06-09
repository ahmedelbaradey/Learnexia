# Execution Report — P2-01 (Curriculum hierarchy CRUD)

> **Filled by `api-tester` after the run.**

## Run metadata

| Field | Value |
|---|---|
| Date / time (UTC) | 2026-06-09 (initial) → 2026-06-09 (defect-fix re-run) |
| Run by | api-tester |
| Branch / commit | qc/phase-2-backend-continue (fixes: DEFECT-1 + DEFECT-2 resolved in same branch) |
| API base URL | in-process (WebApplicationFactory + Testcontainers pgvector/pg16) |
| Build status (`dotnet build backend/Learnexia.Modular.sln`) | 0 errors, 17 warnings (pre-existing) |
| Test project / class | `backend/tests/Learnexia.IntegrationTests/P2_01_CurriculumHierarchy_Extended_Tests.cs` (62 methods — 60 original + 2 new BE-TC-37 sub-b methods) + `P2_01_CurriculumHierarchy_Tests.cs` (32 existing) |

## Results — backend (`backend-test-cases.md`)

| Case ID | Title (short) | Priority | Result | Observed status code(s) | Notes / defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Grade CRUD round-trip | P0 | PASS | 200 | Cross-reference: `AC1_Grade_CrudRoundTrip` |
| BE-TC-02 | Unit CRUD round-trip | P1 | PASS | 200 (create/update/delete); non-2xx after delete | New: `BETC02_Unit_CrudRoundTrip` |
| BE-TC-03 | Concept CRUD round-trip | P1 | PASS | 200 each write | New: `BETC03_Concept_CrudRoundTrip` |
| BE-TC-04 | Skill CRUD round-trip | P1 | PASS | 200 each write; updated masteryThreshold=90 reflected | New: `BETC04_Skill_CrudRoundTrip` |
| BE-TC-05 | Full hierarchy creation | P0 | PASS | 200 each step | Cross-reference: `AC1_FullHierarchy_CreationRoundTrip` |
| BE-TC-06 | Success envelope + `Successed` spelling | P0 | PASS | 200 | Cross-reference: `AC2_GradesList_OuterEnvelopeShape` |
| BE-TC-07 | PaginatedResult shape | P1 | PASS | 200 | Cross-reference: `AC2_GradesList_PaginatedResultShape` |
| BE-TC-08 | Pagination PageNumber/PageSize | P1 | PASS | 200; page 1 ≤ 2 items; page 2 different; totalPages=ceil(n/2) | New: `BETC08_Pagination_HonorsPageNumberAndPageSize` |
| BE-TC-09 | 422 envelope shape | P0 | PASS | 422 | Cross-reference: `AC2_ValidationEnvelope_Has422Shape` |
| BE-TC-10 | Subject filtered by grade | P1 | PASS | 200 | Cross-reference: `AC1_Subjects_FilterByGradeId_Works` |
| BE-TC-11 | Child list filters scope to parent | P2 | PASS | 200; correct isolation per parent for all 4 edges | New: `BETC11_ChildLists_ScopeToParent` |
| BE-TC-12 | Lesson DifficultyLevel round-trip | P1 | PASS | 200 | Cross-reference: `AC5_Lesson_DifficultyLevel_RoundTrips` |
| BE-TC-13 | Concept DifficultyLevel round-trip | P1 | PASS | 200 | Cross-reference: `AC5_Concept_DifficultyLevel_RoundTrips` |
| BE-TC-14 | Skill mastery/time round-trip | P1 | PASS | 200 | Cross-reference: `AC1_Skill_MasteryAndTimeFields_PresentInResponse` |
| BE-TC-15 | Lesson nullable SkillId | P1 | PASS | 200 (both omitted and explicit null) | Cross-reference: `AC1_Lesson_WithoutSkillId_IsAccepted` + `AC1_Lesson_ExplicitNullSkillId_IsAccepted` |
| BE-TC-16 | Grade empty DisplayName → 422 | P0 | PASS | 422 | Cross-reference: `AC3_Grade_EmptyDisplayName_Returns422` |
| BE-TC-17 | Grade Number range → 422 (+inclusive bounds) | P1 | PASS | 422 (0 and 7); 200 (1 and 6) | Cross-ref existing 0/7; new `BETC17_Grade_ValidBoundaryNumbers_Succeed` confirms 1 and 6 succeed |
| BE-TC-18 | Subject empty Name → 422 | P0 | PASS | 422 | Cross-reference: `AC3_Subject_EmptyName_Returns422` |
| BE-TC-19 | Subject GradeId=0 → 422 | P0 | PASS | 422 | Cross-reference: `AC3_Subject_ZeroGradeId_Returns422` |
| BE-TC-20 | Unit empty Name → 422 | P1 | PASS | 422 | Cross-reference: `AC3_Unit_EmptyName_Returns422` |
| BE-TC-21 | Lesson empty Name / Difficulty enum → 422 | P1 | PASS | 422 (Difficulty=0 cross-ref; Difficulty=99 new `BETC21`) | New sub-case: `BETC21_Lesson_InvalidDifficulty_99_Returns422` |
| BE-TC-22 | Concept empty Name / DifficultyLevel=99 → 422 | P1 | PASS | 422 | Cross-reference: `AC3_Concept_EmptyName_Returns422` + `AC3_Concept_InvalidDifficultyLevel_Returns422` |
| BE-TC-23 | Skill empty Name / MasteryThreshold range → 422 | P1 | PASS | 422 (-1 and 101); 200 (0 and 100 inclusive bounds) | New: `BETC23_Skill_NegativeMasteryThreshold_Returns422` + `BETC23_Skill_ValidBoundaryMasteryThreshold_Succeeds` |
| BE-TC-24 | Edit command validated → 422 | P1 | PASS | 422 | New: `BETC24_GradeUpdate_InvalidPayload_Returns422`; EditGradeCommand includes same rules via GradeBaseValidation |
| BE-TC-25 | Six tables in `learning` schema | P0 | PASS | N/A (DB catalog query) | New: `BETC25_SixCurriculumTables_ExistInLearningSchema`; all six tables confirmed present |
| BE-TC-26 | Unique index on Subjects present | P0 | PASS | N/A (pg_indexes query) | New: `BETC26_UniqueIndex_OnSubjects_ExistsAndIsUnique`; index confirmed present and UNIQUE |
| BE-TC-27 | Anonymous Grade reads → 401 | P0 | PASS | 401 (List cross-ref); 401 (GetById new `BETC27_AnonymousGradesGetById_Returns401`) | Class-level `[Authorize]` on GradesController covers both |
| BE-TC-28 | Anonymous writes → 401 (all 6 controllers) | P0 | PASS | 401 on all 18 combinations (6 controllers × POST/PUT/DELETE) | New: 3 `[Theory]` sweeps |
| BE-TC-29 | Non-admin write → 403 (all 6 controllers) | P0 | PASS | 403 on all 18 combinations; `basicuser` (role Basic) used | New: 3 `[Theory]` sweeps; `AdminOnly` policy gate confirmed on all six |
| BE-TC-30 | Duplicate subject same grade → rejected | P0 | PASS | **400** (BadRequest pre-check) | **DEFECT-1 RESOLVED** — `AddSubjectCommandHandler` now pre-checks for live/soft-deleted duplicate before insert; returns `BadRequest<string>()` → HTTP 400. Was: 500 ServerError from unhandled unique constraint. Test: `BETC30_DuplicateSubject_SameGrade_IsRejected` |
| BE-TC-31 | Same-name duplicate → rejected | P1 | PASS | **400** (BadRequest pre-check) | **DEFECT-1 RESOLVED** same fix. Test: `BETC31_SameNameDuplicateSubject_IsRejected` |
| BE-TC-32 | First subject survives duplicate failure | P1 | PASS | Subject A: 200; Subject B: 400 | Rollback confirmed — B not persisted; A fully intact. (Was: B→500; now B→400 with pre-check.) |
| BE-TC-33 | Subject under bad GradeId → rejected | P1 | PASS | **404** (pre-existence check) | **DEFECT-2 RESOLVED** — handler now checks grade existence before insert, returns `NotFound`. Was: 500. Cross-reference: `AC4_Subject_NonExistentGradeId_FailsGracefully` (asserts 404) |
| BE-TC-34 | Unit under bad SubjectId → rejected | P1 | PASS | **404** (pre-existence check) | **DEFECT-2 RESOLVED** — same pattern. Test: `BETC34_Unit_NonExistentSubjectId_Rejected` now asserts 404 |
| BE-TC-35 | Concept under bad SubjectId → rejected | P1 | PASS | **404** (pre-existence check) | **DEFECT-2 RESOLVED**. Test: `BETC35_Concept_NonExistentSubjectId_Rejected` now asserts 404 |
| BE-TC-36 | Lesson/Skill under bad parent → rejected | P1 | PASS | Lesson bad UnitId: **404**; Skill bad ConceptId: **404**; Lesson bad SkillId: **404** | **DEFECT-2 RESOLVED** — all three pre-checks now in place. Tests: `BETC36a/b/c` each assert 404 |
| BE-TC-37 | 4 subjects / no Social Studies; non-MATH/Ar tree creatable | P2 | PASS (sub-a) + PASS (sub-b) | N/A sub-a; 200 + 422 sub-b | Sub-a: `BETC37` — confirmed 4 SubjectCode values {0,1,2,3}; no Social Studies. Sub-b: **UNBLOCKED** — `AddSubjectCommand` inherits `SubjectCode`+`Language` from `SubjectDto`; both ARE settable on Create (immutable on Edit via map ignore). New tests: `BETC37b_NonMathArTree_CanBeCreated` (SCIENCE/Ar + MATH/Ar under new grade → 200 each, distinct rows) + `BETC37b_InvalidSubjectCode4_SocialStudies_Rejected` (SubjectCode=4 → 422 via SubjectBaseValidation) |

## Defects found

| # | Case ID | Severity | Summary | Status |
|---|---|---|---|---|
| DEFECT-1 | BE-TC-30, BE-TC-31 | High | `POST /api/learning/subjects/Create` with a second subject under the same `(GradeId, SubjectCode, Language)` returned **HTTP 500 ServerError** (unhandled unique constraint at SaveChanges). **CORRECTION to original diagnosis**: `SubjectCode` and `Language` ARE exposed on `AddSubjectCommand` (inherited from `AddSubjectDto : SubjectDto`). The original QC report incorrectly stated these were not settable. **Fix applied**: `AddSubjectCommandHandler` now pre-checks for an existing live/soft-deleted subject under the same `(GradeId, SubjectCode, Language)` before insert, returning `BadRequest<string>()` → **HTTP 400**. Additionally, `EditSubjectCommand` → `Subject` mapping now ignores `SubjectCode` and `Language` (immutable on edit). **Resolved in commit on branch `qc/phase-2-backend-continue`**. Tests now assert 400. | **RESOLVED** |
| DEFECT-2 | BE-TC-33, BE-TC-34, BE-TC-35, BE-TC-36 | Medium | All child-create endpoints returned **HTTP 500 ServerError** when the parent entity did not exist (FK violation at SaveChanges). **Fix applied**: each handler now performs a pre-existence check on the parent before insert and returns `NotFound()` → **HTTP 404**. Covers: `/subjects/Create` (GradeId), `/units/Create` (SubjectId), `/concepts/Create` (SubjectId), `/lessons/Create` (UnitId), `/skills/Create` (ConceptId), `/lessons/Create` (optional non-null SkillId). **Resolved in commit on branch `qc/phase-2-backend-continue`**. Tests now assert 404. | **RESOLVED** |

## Open-question observations (for the lead — Q1/Q2)

- **Q1 — duplicate `(GradeId,SubjectCode,Language)` status:** RESOLVED. Was 500, now **400** (BadRequest pre-check in handler). DEFECT-1 marked resolved above.
- **Q2 — child under non-existent parent status:** RESOLVED. Was 500, now **404** (pre-existence check in handler). DEFECT-2 marked resolved above.
- **Lesson with non-existent SkillId (optional FK, SetNull):** RESOLVED as part of DEFECT-2. Handler now pre-checks SkillId existence when provided → **404**. (Explanation: SetNull only fires on *delete* of the referenced Skill, not on *insert* with a non-existent FK value — the pre-check is therefore correct and necessary.)

## Summary

| | Count |
|---|---|
| Total cases | 39 (37 original + 2 new BE-TC-37 sub-b methods) |
| Pass | 39 |
| Fail | 0 |
| Blocked | 0 (BE-TC-37 sub-b UNBLOCKED — SubjectCode/Language ARE settable on Create) |

**Defects:** DEFECT-1 RESOLVED (400 BadRequest pre-check), DEFECT-2 RESOLVED (404 pre-existence check).

**Total dotnet test run (full P2 suite):** 415 passed, 0 failed, 8 skipped.

Run command: `dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P2_01" --no-build`

P2-01-specific result: `Passed! — Failed: 0, Passed: 94, Skipped: 0` (62 extended + 32 base = 94 test methods)
