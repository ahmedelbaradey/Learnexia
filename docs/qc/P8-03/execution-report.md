# P8-03 — Execution report

> Status: **PASS** — all 18 test cases implemented and green.
> Story: P8-03 serve curriculum in learning language · Module: Learning.

## Environment
- Build / commit: main branch, build 2026-06-22
- Test run date: 2026-06-22
- DB: PostgreSQL 16 via Testcontainers (pgvector/pgvector:pg16); schema created from migrations; `LearningSeeder.SeedAsync` applied
- Command: `dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P8_03_ServeInLearningLanguage" -c Release`
- Result: **Passed — 18/18**

## Results

| Case ID | Title (short) | Priority | Status | Test method | Notes / defect |
|---|---|---|---|---|---|
| BE-TC-01 | ForGrade En → MATH/En,SCI/En,ARABIC/Ar,ENG/En | P0 | PASS | `TC01_ForGrade_EnMedium_ReturnsResolvedSet` | DB walk confirms each returned subject's Language |
| BE-TC-02 | ForGrade Ar → MATH/Ar,SCI/Ar,ARABIC/Ar,ENG/En | P0 | PASS | `TC02_ForGrade_ArMedium_ReturnsResolvedSet` | |
| BE-TC-03 | same grade: Math/Sci differ, Arabic/English identical | P0 | PASS | `TC03_SameGrade_ArVsEn_DifferentMathScienceIdenticalLanguageSpecific` | MATH/SCIENCE ids differ; ARABIC/ENGLISH ids are identical |
| BE-TC-04 | Lessons En-MATH serves En tree | P0 | PASS | `TC04_Lessons_EnMathSubjectId_ServesEnTree` | First lesson's subject confirmed Language=En via DB walk |
| BE-TC-05 | SkillTree En-MATH serves En tree | P0 | PASS | `TC05_SkillTree_EnMathSubjectId_ServesEnTree` | Concept `name` field (not `conceptName`) confirmed in English |
| BE-TC-06 | wrong-lang SubjectId Lessons → silent redirect (200) | P0 | PASS | `TC06_Lessons_WrongLanguageSubjectId_SilentlyRedirects` | MATH/Ar id + En token → 200; redirected content confirmed Language=En via DB walk |
| BE-TC-07 | wrong-lang SubjectId SkillTree → silent redirect (200) | P0 | PASS | `TC07_SkillTree_WrongLanguageSubjectId_SilentlyRedirects` | MATH/Ar id + En token → 200; concepts non-empty |
| BE-TC-08 | wrong-lang LessonId Lessons/{id} → 403 | P0 | PASS | `TC08_Lessons_WrongLanguageLessonId_Returns403` | 403 with `successed=false`; confirms the Lessons/SkillTree redirect vs Lessons/{id} 403 asymmetry |
| BE-TC-09 | correct-lang LessonId → 200 | P1 | PASS | `TC09_Lessons_CorrectLanguageLessonId_Returns200` | |
| BE-TC-10 | ARABIC identical for both media (pinned Ar) | P1 | PASS | `TC10_Arabic_IdenticalForBothMedia` | Both En and Ar students get `_arabicArG1SubjectId`; ForGrade confirms same id |
| BE-TC-11 | ENGLISH identical for both media (pinned En) | P1 | PASS | `TC11_English_IdenticalForBothMedia` | Both En and Ar students get `_englishEnG1SubjectId` |
| BE-TC-12 | switching language flips served content (round-trip) | P0 | PASS | `TC12_SwitchLanguage_FlipsServedCurriculum` | En→ar switch + re-sign-in: MATH flips from En id to Ar id; ARABIC/ENGLISH unchanged |
| BE-TC-13 | content from JWT claim, not query param | P0 | PASS | `TC13_QueryParamSpoofIgnored_JwtClaimWins` | `?learningLanguage=ar` ignored; JWT claim `learning_language=en` wins; MATH still En |
| BE-TC-14 | absent claim → Arabic default | P1 | PASS | `TC14_ClaimAbsentFallback_CoveredByUnitTest` | Documentation test (intentional pass). Per G-02 decision: unit test `SubjectLanguageResolverTests.ClaimAccessor_ClaimAbsent_FallsBackToArAndLogsWarn` is the primary coverage. Minting a claimless student token at HTTP layer is not feasible in this harness. |
| BE-TC-15 | missing-tree fallback → serve other + warn | P1 | PASS | `TC15_MissingTreeFallback_ServesOtherLanguage` | Deactivates MATH/En G1 → ForGrade returns 200 (not 500); MATH falls back to MATH/Ar. IsActive restored in finally block. |
| BE-TC-16 | empty-state friendly (200-empty / 400, not 500) | P2 | PASS | `TC16_ForGrade_OutOfRange_Returns400` | ForGrade grade=99 → 400; non-existent subject id on Lessons → 404; SkillTree → 404 |
| BE-TC-17 | dashboard resolves in learning language | P1 | PASS | `TC17_Dashboard_ResolvesLanguageFromClaim` | `GET api/Learning/Dashboard` returns 200 for both Ar and En students |
| BE-TC-18 | start-attempt respects learning language | P1 | PASS | `TC18_StartAttempt_WrongLanguageLesson_Returns403` | G-03: `StartAttemptCommandHandler` confirmed to have the same language guard as `GetLessonQueryHandler`. MATH/Ar lesson + En token → 403. MATH/En lesson + En token → 200. |

## Defects found

None. All behavior confirmed as-built per G-03 decision.

## Observations

**Language guard asymmetry (G-03, confirmed):**
- `GET api/learning/Subjects/{id}/Lessons` — silently redirects wrong-language SubjectId to the resolved tree (200, no error).
- `GET api/learning/Subjects/{id}/SkillTree` — same silent redirect behavior (200, no error).
- `GET api/learning/Lessons/{id}` — returns 403 Forbidden (LessonLanguageMismatch) for wrong-language lesson id.
- `POST api/Learning/Quizzes/{lessonId}/Attempt` — same 403 guard as `GET Lessons/{id}` (confirmed in `StartAttemptCommandHandler`).

**SkillTree response shape:** concept-level objects use `name` (not `conceptName`). Skills within each concept use `name` as well. Confirmed from live response.
