# Execution Report — P2-01 (Curriculum hierarchy CRUD)

> **Template — filled by `api-tester` after the run.** The qc-test-designer scaffolds this file empty; testers
> record results. Do **not** edit the test-case spec to match results — file a defect instead.

## Run metadata

| Field | Value |
|---|---|
| Date / time (UTC) | _TBD_ |
| Run by | _api-tester_ |
| Branch / commit | _TBD_ |
| API base URL | _TBD_ |
| Build status (`dotnet build backend/Learnexia.Modular.sln`) | _TBD_ |
| Test project / class | _e.g. `P2_01_CurriculumHierarchy_Extended_Tests.cs`_ |

## Results — backend (`backend-test-cases.md`)

| Case ID | Title (short) | Priority | Result (PASS/FAIL/BLOCKED) | Observed status code(s) | Notes / defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Grade CRUD round-trip | P0 | | | extends existing |
| BE-TC-02 | Unit CRUD round-trip | P1 | | | |
| BE-TC-03 | Concept CRUD round-trip | P1 | | | |
| BE-TC-04 | Skill CRUD round-trip | P1 | | | |
| BE-TC-05 | Full hierarchy creation | P0 | | | extends existing |
| BE-TC-06 | Success envelope + `Successed` spelling | P0 | | | extends existing |
| BE-TC-07 | PaginatedResult shape | P1 | | | extends existing |
| BE-TC-08 | Pagination PageNumber/PageSize | P1 | | | |
| BE-TC-09 | 422 envelope shape | P0 | | | extends existing |
| BE-TC-10 | Subject filtered by grade | P1 | | | extends existing |
| BE-TC-11 | Child list filters scope to parent | P2 | | | |
| BE-TC-12 | Lesson DifficultyLevel round-trip | P1 | | | extends existing |
| BE-TC-13 | Concept DifficultyLevel round-trip | P1 | | | extends existing |
| BE-TC-14 | Skill mastery/time round-trip | P1 | | | extends existing |
| BE-TC-15 | Lesson nullable SkillId | P1 | | | extends existing |
| BE-TC-16 | Grade empty DisplayName → 422 | P0 | | | extends existing |
| BE-TC-17 | Grade Number range → 422 (+inclusive bounds) | P1 | | | |
| BE-TC-18 | Subject empty Name → 422 | P0 | | | extends existing |
| BE-TC-19 | Subject GradeId=0 → 422 | P0 | | | extends existing |
| BE-TC-20 | Unit empty Name → 422 | P1 | | | extends existing |
| BE-TC-21 | Lesson empty Name / Difficulty enum → 422 | P1 | | | |
| BE-TC-22 | Concept empty Name / DifficultyLevel=99 → 422 | P1 | | | extends existing |
| BE-TC-23 | Skill empty Name / MasteryThreshold range → 422 | P1 | | | +bounds |
| BE-TC-24 | Edit command validated → 422 | P1 | | | |
| BE-TC-25 | Six tables in `learning` schema | P0 | | | |
| BE-TC-26 | Unique index on Subjects present | P0 | | | |
| BE-TC-27 | Anonymous Grade reads → 401 | P0 | | | extends existing |
| BE-TC-28 | Anonymous writes → 401 (all 6) | P0 | | | |
| BE-TC-29 | Non-admin write → 403 (all 6) | P0 | | | |
| BE-TC-30 | Duplicate subject same grade → rejected | P0 | | | **record actual code (Q1)** |
| BE-TC-31 | Same-name duplicate → rejected | P1 | | | |
| BE-TC-32 | First subject survives duplicate failure | P1 | | | |
| BE-TC-33 | Subject under bad GradeId → rejected | P1 | | | extends existing |
| BE-TC-34 | Unit under bad SubjectId → rejected | P1 | | | record code (Q2) |
| BE-TC-35 | Concept under bad SubjectId → rejected | P1 | | | record code (Q2) |
| BE-TC-36 | Lesson/Skill under bad parent → rejected | P1 | | | record Lesson-SetNull behavior |
| BE-TC-37 | 4 subjects only / no Social Studies | P2 | | | sub-step (b) may be BLOCKED (not testable via Create) |

## Defects found

| # | Case ID | Severity | Summary | Status |
|---|---|---|---|---|
| | | | | |

## Open-question observations (for the lead — Q1/Q2)

- **Q1 — duplicate `(GradeId,SubjectCode,Language)` status:** observed code = _TBD_ (expected 500 today). Recommend: _TBD_.
- **Q2 — child under non-existent parent status:** observed code = _TBD_ (expected 500 today). Recommend: _TBD_.
- **Lesson with non-existent SkillId (optional FK):** observed behavior = _TBD_ (rejected vs accepted-as-null).

## Summary

| | Count |
|---|---|
| Total | 37 |
| Pass | _TBD_ |
| Fail | _TBD_ |
| Blocked | _TBD_ |
