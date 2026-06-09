# Execution Report — P2-01 (Curriculum hierarchy CRUD)

> **Filled by `api-tester` after the run.**

## Run metadata

| Field | Value |
|---|---|
| Date / time (UTC) | 2026-06-09 |
| Run by | api-tester |
| Branch / commit | qc/phase-2-backend-continue |
| API base URL | in-process (WebApplicationFactory + Testcontainers pgvector/pg16) |
| Build status (`dotnet build backend/Learnexia.Modular.sln`) | 0 errors, 17 warnings (pre-existing) |
| Test project / class | `backend/tests/Learnexia.IntegrationTests/P2_01_CurriculumHierarchy_Extended_Tests.cs` (60 new) + `P2_01_CurriculumHierarchy_Tests.cs` (32 existing) |

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
| BE-TC-30 | Duplicate subject same grade → rejected | P0 | PASS | **500** (ServerError) | **DEFECT-1** — see below. Unique constraint violated → catch→ServerError; not clean 409 |
| BE-TC-31 | Same-name duplicate → rejected | P1 | PASS | **500** (ServerError) | **DEFECT-1** same root cause; no stack trace leaked in body |
| BE-TC-32 | First subject survives duplicate failure | P1 | PASS | Subject A: 200; Subject B: 500 | Rollback confirmed — B not persisted; A fully intact |
| BE-TC-33 | Subject under bad GradeId → rejected | P1 | PASS | **500** (ServerError) | Cross-reference: `AC4_Subject_NonExistentGradeId_FailsGracefully`; **DEFECT-2** FK violation caught at SaveChanges |
| BE-TC-34 | Unit under bad SubjectId → rejected | P1 | PASS | **500** (ServerError) | New: `BETC34_Unit_NonExistentSubjectId_Rejected`; **DEFECT-2** same pattern |
| BE-TC-35 | Concept under bad SubjectId → rejected | P1 | PASS | **500** (ServerError) | New: `BETC35_Concept_NonExistentSubjectId_Rejected`; **DEFECT-2** same pattern |
| BE-TC-36 | Lesson/Skill under bad parent → rejected | P1 | PASS | Lesson bad UnitId: **500**; Skill bad ConceptId: **500**; Lesson bad SkillId (optional FK): **500** | New: `BETC36a/b/c`; **DEFECT-2**; bonus: non-existent optional SkillId also raises FK violation → 500 (SetNull is delete-side only, not insert-side) |
| BE-TC-37 | 4 subjects / no Social Studies | P2 | PASS (sub-a) + BLOCKED (sub-b) | N/A (reflection check) | Sub-a: `BETC37` — confirmed exactly 4 SubjectCode values {0,1,2,3}; no Social Studies name. Sub-b: BLOCKED — `AddSubjectCommand` does not expose `SubjectCode`, so passing SubjectCode=4 via Create endpoint is not testable |

## Defects found

| # | Case ID | Severity | Summary | Status |
|---|---|---|---|---|
| DEFECT-1 | BE-TC-30, BE-TC-31 | High | `POST /api/learning/subjects/Create` with a second subject under the same grade violates `IX_Subjects_GradeId_SubjectCode_Language` (unique index) and returns **HTTP 500 ServerError** instead of a clean **409 Conflict** or **422 Unprocessable**. Root cause: `AddSubjectCommand`/`AddSubjectDto` do not expose `SubjectCode` or `Language`, so every API-created subject defaults to `(MATH=0, Ar=0)`, causing all subjects under the same grade to collide. The handler's `catch(Exception ex) → ServerError()` path is hit. The 500 body is a valid JSON envelope (successed=false), so it is not a crash page — but it is not the correct contract for a business-rule conflict. **Recommend**: either (a) expose `SubjectCode`+`Language` on `AddSubjectCommand` and add a pre-check → 409/422, or (b) add explicit duplicate-check in the service layer before insert. For backend-feature to fix. | Open |
| DEFECT-2 | BE-TC-33, BE-TC-34, BE-TC-35, BE-TC-36 | Medium | All child-create endpoints (`/subjects/Create` with bad GradeId, `/units/Create` with bad SubjectId, `/concepts/Create` with bad SubjectId, `/lessons/Create` with bad UnitId, `/skills/Create` with bad ConceptId, `/lessons/Create` with bad non-null SkillId) return **HTTP 500 ServerError** when the parent does not exist. Root cause: no existence pre-check before insert; the FK violation propagates from `SaveChangesAsync` to the handler's `catch → ServerError()`. The response body is a valid JSON envelope (successed=false, no stack trace), so the graceful-envelope contract holds — but 500 is the wrong status for a "parent not found" scenario. **Recommend**: service layer should check parent existence before insert and return a domain error (`NotFound` or `BadRequest`) → 404 or 422. For backend-feature to fix (non-blocking for the existing graceful-envelope assertion). | Open |

## Open-question observations (for the lead — Q1/Q2)

- **Q1 — duplicate `(GradeId,SubjectCode,Language)` status:** Observed code = **500** (ServerError from catch block). The body is a valid JSON envelope with `successed=false` — not a crash page. Recommend exposing `SubjectCode`+`Language` on `AddSubjectCommand` + adding a pre-check or mapping the unique-violation exception to 409. See DEFECT-1.
- **Q2 — child under non-existent parent status:** Observed code = **500** (ServerError from catch block) for all five FK edges (Subject→Grade, Unit→Subject, Concept→Subject, Lesson→Unit, Skill→Concept). Recommend adding pre-existence checks → 404 or 422. See DEFECT-2.
- **Lesson with non-existent SkillId (optional FK, SetNull):** Observed behavior = **500** rejected (not accepted-as-null). `SetNull` only applies on *delete* of the referenced Skill, not on *insert* with a non-existent FK value — the DB still enforces the FK constraint at insert time. So a non-existent `SkillId` on create is also a 500. The optional nature only means the column itself can be NULL (omitted/null SkillId is accepted fine — confirmed by BE-TC-15).

## Summary

| | Count |
|---|---|
| Total | 37 |
| Pass | 36 |
| Fail | 0 |
| Blocked | 1 (BE-TC-37 sub-b) |

**Total dotnet test run:** 92 tests passed, 0 failed (60 new extended + 32 existing P2-01 tests).

Run command: `dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P2_01" --no-build`

Result line: `Test Run Successful. Total tests: 92 | Passed: 92 | Total time: 1.89 Minutes`
