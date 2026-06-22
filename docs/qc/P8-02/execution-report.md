# P8-02 — Execution report

> Status: **PASS** — all 10 test cases implemented and green.
> Story: P8-02 bilingual curriculum (parallel trees) · Module: Learning.

## Environment
- Build / commit: main branch, build 2026-06-22
- Test run date: 2026-06-22
- DB: PostgreSQL 16 via Testcontainers (pgvector/pgvector:pg16); schema created from migrations; `LearningSeeder.SeedAsync` applied
- Command: `dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P8_02_BilingualContent" -c Release`
- Result: **Passed — 10/10**

## Results

| Case ID | Title (short) | Priority | Status | Test method | Notes / defect |
|---|---|---|---|---|---|
| BE-TC-01 | each grade has exactly 6 roots | P0 | PASS | `TC01_EachGrade_HasExactlySixSubjectRoots` | Belt-and-suspenders against real PostgreSQL; LearningSeederTests covers this via InMemory |
| BE-TC-02 | Math/Science both langs; Arabic Ar; English En | P0 | PASS | `TC02_MathScience_HaveBothLanguages_ArabicOnlyAr_EnglishOnlyEn` | |
| BE-TC-03 | SubjectCode+Language populated, no untagged | P0 | PASS | `TC03_AllSubjects_HaveValidSubjectCodeAndLanguage` | Asserts no null/zero values |
| BE-TC-04 | no language column on child entities | P1 | PASS | `TC04_LessonEntity_HasNoLanguageProperty_InheritedViaParent` | EF model inspection: `db.Model.FindEntityType(typeof(Lesson))` has no "Language"/"ContentLanguage" property; behavioral walk confirms lesson→subject language |
| BE-TC-05 | unique index on (GradeId,SubjectCode,Language) exists | P1 | PASS | `TC05_UniqueIndex_ExistsOnSubject_GradeSubjectCodeLanguage` | `IX_Subjects_GradeId_SubjectCode_Language` found via `GetIndexes()` |
| BE-TC-06 | duplicate triplet insert rejected | P0 | PASS | `TC06_DuplicateTriplet_ThrowsDbUpdateException` | DbUpdateException thrown when inserting duplicate (GradeId, MATH, En) |
| BE-TC-07 | seeder idempotent | P1 | PASS | `TC07_Seeder_IsIdempotent_SecondRunNoExtraRoots` | Second SeedAsync run: per-grade root counts remain exactly 6 |
| BE-TC-08 | parallel trees differ structurally | P2 | PASS | `TC08_ParallelTrees_HaveDisjointUnits` | MATH/Ar and MATH/En G1 have disjoint UnitId sets with different display names |
| BE-TC-09 | no cross-language KnowledgeEdge | P1 | PASS | `TC09_NoKnowledgeEdge_CrossesLanguageBoundary` | KnowledgeNode→Skill→Concept→Subject walk; zero cross-language prerequisite edges |
| BE-TC-10 | content item resolves per language, no leakage | P1 | PASS | `TC10_Lessons_ResolveToCorrectLanguageTree` | MATH/En and MATH/Ar G1 lessons resolve to different parent Subjects with correct Language/SubjectCode |

## Defects found

None.

## Open items / deviations

- TC01–TC10 are belt-and-suspenders integration coverage against real PostgreSQL. The equivalent logic for TC01–TC07 is already covered at the InMemory-DB layer in `Modules.Learning.UnitTests/LearningSeederTests.cs` (P8-02 BE-TC-01 through BE-TC-07 equivalents).
- TC09: KnowledgeEdge cross-language check scans all Prerequisite edges whose source and target subjects are in the seeded dataset. Zero cross-language edges found in the seeded curriculum.
