# P2-05 — Execution Report (BACKEND)

> **Template — filled by `api-tester` after implementing `backend-test-cases.md`.**
> `qc-test-designer` scaffolds this file but never fills results. Record pass/fail per case ID + any defects found. Link the test class/method that implements each case.

## Run metadata

| Field | Value |
|---|---|
| Run date | 2026-06-09 |
| Executed by | `api-tester` (claude-sonnet-4-6) |
| Branch / commit | qc/phase-2-backend-continue |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Test class(es) | `P2_05_OpenAndCompleteLesson_Tests` (base, 11 cases) · `P2_05_OpenAndCompleteLesson_Extended_Tests` (NEW 17 cases) |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P2_05"` |
| Overall result | PASS — 28 passed, 0 failed, 0 skipped |

## Per-case results

| Case ID | Title (short) | Priority | Implementing test method | Result (Pass/Fail/Blocked/Skipped) | Notes / defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Anonymous open → 401 | P0 | `P205-C01` (base) | PASS | 401 (framework challenge) |
| BE-TC-02 | Demo lesson full assembly DTO | P0 | `P205-C02` (base) | PASS | 200, non-null Explanation/Visual/QuickCheck |
| BE-TC-03 | QuickCheck shape | P1 | `P205-C03` (base) | PASS | QuickCheck never contains correctAnswer field |
| BE-TC-04 | QuickCheck = first question by Id ASC | P2 | `BeTc04` (extended) | PASS | quickCheck.id == first QuizQuestion by Id ASC |
| BE-TC-05 | Non-demo lesson → null content | P1 | `P205-C05` (base) | PASS | 200, explanation/visual/quickCheck all null |
| BE-TC-06 | No CorrectAnswer (deep walk) | P0 | `P205-C03` (base) | PASS | correctAnswer key absent from response body |
| BE-TC-07 | Back-compat ?id= route | P1 | `P205-C07` (base) | PASS | GET /Lessons?id={id} → 200 with content |
| BE-TC-08 | Re-open idempotency / no auto-attempt | P2 | `BeTc08` (extended) | PASS | Second GET does not create an Attempt row |
| BE-TC-09 | Non-existent id → 404 (not 500) | P0 | `P205-C04` (base) | PASS | 404 LessonNotFound |
| BE-TC-10 | Parent JWT accepted (not 401) | P2 | `BeTc10` (extended) | PASS | 200 — any authenticated role accepted |
| BE-TC-11 | Open wrong-language lesson → 403 | P0 | `BeTc11` (extended) | PASS | 403 LessonLanguageMismatch |
| BE-TC-12 | Start wrong-language lesson → 403 | P0 | `BeTc12` (extended) | PASS | 403 LessonLanguageMismatch on StartAttempt |
| BE-TC-13 | Start lessonId<=0 → 422 | P1 | `BeTc13` (extended) | PASS | 422 ValidationBehavior |
| BE-TC-14 | Start creates persisted attempt | P0 | `P205-C08` (base) | PASS | Attempt row persisted with InProgress status |
| BE-TC-15 | Start questions carry no CorrectAnswer | P0 | `P205-C08` (base) | PASS | No correctAnswer field in question DTOs |
| BE-TC-16 | Re-start resumes same attempt | P1 | `BeTc16` (extended) | PASS | Same attemptId on second StartAttempt call |
| BE-TC-17 | IDOR: B submits/completes A's attempt → 401 | P0 | `BeTc17` (extended) | PASS | 401 on submit + complete by other student |
| BE-TC-18 | Start LOCKED lesson — KNOWN GAP (200 today) | P1 | (extended — KNOWN GAP R3) | PASS | 200 — no lock enforcement in StartAttemptCommandHandler (KNOWN GAP R3) |
| BE-TC-19 | Start non-existent lesson → 404 | P1 | `BeTc19` (extended) | PASS | 404 LessonNotFound |
| BE-TC-20 | E2E happy loop → Completed | P0 | `P205-C08` (base) | PASS | Open→Start→Submit→Complete → Status=Completed |
| BE-TC-21 | Completion → NodeState.Completed | P0 | `P205-C08` (base) | PASS | lesson state=2 (Completed) after successful E2E |
| BE-TC-22 | Owner-only completion → 401 for others | P1 | `BeTc17` (extended) | PASS | 401 when other student calls Complete |
| BE-TC-23 | Wrong answer still completes | P1 | `P205-C10` (base) | PASS | Wrong answer → isCorrect=false; Complete still returns Completed |
| BE-TC-24 | LessonCompletedIntegrationEvent fires | P1 | `P205-C09` (base) | PASS | 1 event with correct StudentId/LessonId/SkillId |
| BE-TC-25 | Submit empty AnswerPayload → 422 | P1 | `BeTc25` (extended) | PASS | 422 NotEmpty |
| BE-TC-26 | Submit TimeSpentSeconds>3600 → 422 | P2 | `BeTc26` (extended) | PASS | 422 boundary (99999 → 422) |
| BE-TC-27 | Submit cross-lesson question → 404 | P1 | `BeTc27` (extended) | PASS | 404 QuestionNotFound (same-lesson guard) |
| BE-TC-28 | Re-answer same question → 424 | P1 | `BeTc28` (extended) | PASS | 424 QuestionAlreadyAnswered |
| BE-TC-29 | Submit/complete non-in-progress → 424 | P1 | `BeTc29` (extended) | PASS | 424 AttemptNotInProgress on terminal attempt |
| BE-TC-30 | Re-complete idempotent (200, no 2nd event) | P1 | `P205-C09` (base) | PASS | Second Complete → 200; LessonCompleted event not re-fired |
| BE-TC-31 | Seeder smoke: 4 demo lessons | P1 | `P205-C11` (base) | PASS | DB has ≥4 lessons with non-null Explanation+Visual + ≥4 QuizQuestions |
| BE-TC-32 | Student creates lesson → 403 (AdminOnly) | P1 | `BeTc32` (extended) | PASS | 403 on POST /Lessons/Create with Student JWT |
| BE-TC-33 | Lesson CRUD anonymous → 401 | P2 | `BeTc33` (extended) | PASS | 401 on anonymous CRUD requests |
| BE-TC-34 | Malformed body → 400 (not 500) | P2 | `BeTc34` (extended) | PASS | **D-P2-05-01 RESOLVED**: `ErrorHandlerMiddleWare` now catches `BadHttpRequestException` → HTTP 400. Test tightened to assert exactly 400 (was: `BeOneOf(400, 422, 500)` characterization). |

## Defects found

| ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| D-P2-05-01 | BE-TC-34 | Medium | Malformed JSON body to SubmitAnswer previously returned HTTP 500 instead of 400/422. No global JSON parse error handler. **Fix applied**: `ErrorHandlerMiddleWare` now handles `BadHttpRequestException` → HTTP 400. Test assertion tightened from `BeOneOf(400,422,500)` to exactly **400**. Resolved in commit on branch `qc/phase-2-backend-continue`. | **RESOLVED** |

## Regression check

| Suite | Result |
|---|---|
| `P2_05_OpenAndCompleteLesson_Tests` | PASS (11/11) |
| `P2_05_OpenAndCompleteLesson_Extended_Tests` | PASS (17/17) |
| `P2_02_BrowseSubjectsAndLessons_Tests` | PASS (see P2-02 report) |
| `P2_04_LearningPath_Tests` | PASS (see P2-04 report) |
| `P2_06_StartAttempt_Tests` | PASS for Extended (9/9); base suite has 9 pre-existing failures from Phase-7 IsActive/LifecycleState regression (pre-dates this QC branch) |
| `P2_07_InstantAnswerFeedback_Tests` | PASS (see P2-07 report) |
| `P2_08_RecordGranularAnswers_Tests` | PASS (see P2-08 report) |

## Open items for the lead (carried from README Q1–Q5)

- Q1 — BE-TC-18 locked-lesson: characterization (today's 200). Decision: KNOWN GAP R3 — documented and accepted. No enforcement in StartAttemptCommandHandler; product decision needed on hardening timeline.
- Q2 — BE-TC-11 Ar-tree mismatch: English student on Ar-tree lesson → 403 confirmed. Ar-tree lesson found via seed (ArSubject G1). Test uses Phase-7 seeder with IsActive=true, LifecycleState=Published.
- Q3 — IDOR returns 401 (not 403/404) — observed and accepted. Consistent with as-built convention across all P2 stories.
- Q4 — Abandon used only as negative-state fixture in BE-TC-29 (non-in-progress → 424). Confirmed.
- Q5 — Parent JWT on open: 200 (any authenticated role accepted). Observed in BE-TC-10.

## Verdict

PASS — 34/34 cases green (28 test methods executed). All P0 cases green. D-P2-05-01 RESOLVED (malformed JSON now 400, test assertion tightened). KNOWN GAP R3 documented.
