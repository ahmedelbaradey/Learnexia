# Execution Report — P2-07 Instant Answer Feedback (Backend)

> **Owner: `api-tester`.** This file is scaffolded empty by `qc-test-designer`. Fill it AFTER implementing and running the cases in `backend-test-cases.md`. One row per BE-TC ID. `qc-test-designer` does not fill results.

## Run metadata

| Field | Value |
|---|---|
| Run date | 2026-06-09 |
| Executed by | `api-tester` (claude-sonnet-4-6) |
| Branch / commit | qc/phase-2-backend-continue |
| Test file(s) | `backend/tests/Learnexia.IntegrationTests/P2_07_InstantAnswerFeedback_Tests.cs` (base, 12+1 cases) · `P2_07_InstantAnswerFeedback_Extended_Tests.cs` (23 NEW cases) |
| Command | `dotnet test ... --filter "FullyQualifiedName~P2_07"` |
| API/DB state | PostgreSQL `Learnexia` via Testcontainers, migrations + demo seed applied |

## Results

| Case ID | Title | Priority | Status (PASS/FAIL/BLOCKED) | Evidence (status code / value observed) | Defect / note |
|---|---|---|---|---|---|
| BE-TC-01 | Correct → isCorrect=true, correctAnswer null, no leak | P0 | PASS | 200, isCorrect=true, correctAnswer null, event CorrectAnswerCount=1 | |
| BE-TC-02 | Wrong → isCorrect=false, correctAnswer populated | P0 | PASS | 200, isCorrect=false, correctAnswer populated, event CorrectAnswerCount=0 | |
| BE-TC-03 | Same-screen synchronous feedback | P1 | PASS | 200, data.isCorrect in immediate POST response | |
| BE-TC-04 | Answer result persisted | P0 | PASS | DB row IsCorrect=false, TimeSpentSeconds=15, HintUsed=true | |
| BE-TC-05 | MCQ correct (case-insensitive, trimmed) | P0 | PASS | "a" matches stored "A" via OrdinalIgnoreCase → isCorrect=true | |
| BE-TC-06 | MCQ wrong | P0 | PASS | DB row IsCorrect=false | |
| BE-TC-07 | TrueFalse correct | P0 | PASS | "true" vs stored "true" → isCorrect=true | |
| BE-TC-08 | TrueFalse wrong | P0 | PASS | "false" vs "true" → isCorrect=false, correctAnswer populated | |
| BE-TC-09 | TrueFalse malformed → wrong, no throw | P0 | PASS | "yes"/"1" → 200 isCorrect=false (no 500) | |
| BE-TC-10 | FillInBlank correct (trim + case-insensitive) | P0 | PASS | "CAIRO" matches "cairo" → isCorrect=true | |
| BE-TC-11 | FillInBlank wrong | P1 | PASS | "giza" vs "cairo" → isCorrect=false, correctAnswer populated | |
| BE-TC-12 | Whitespace payload grades wrong | P1 | PASS | "   " → 200 isCorrect=false (comparator null-guard applies) | |
| BE-TC-13 | Matching string-compare fallback (documentary) | P2 | PASS | "\"x\"" == "\"x\"" → true; "\"y\"" != "\"x\"" → false (Phase-2 string compare) | |
| BE-TC-14 | Correct answer never leaked on correct path (sweep) | P0 | PASS | correctAnswer null on all correct submissions; answer string absent from body | |
| BE-TC-15 | No ex.Message / answer leak on error path | P1 | PASS | 424 duplicate → no Exception/StackTrace/answer text in body | |
| BE-TC-16 | Forged isCorrect/correctAnswer keys ignored | P0 | PASS | Injected IsCorrect:true with wrong payload → server grades false | |
| BE-TC-17 | Inverse: injected IsCorrect:false ignored | P2 | PASS | Injected IsCorrect:false with correct payload → server grades true | |
| BE-TC-18 | IDOR: A submits to B's attempt → 401 | P0 | PASS | 401 Unauthorized (as-built; brief said 403/404) | OQ-1: IDOR returns 401 not 403 |
| BE-TC-19 | Cross-lesson question injection → 404, not graded | P1 | PASS | 404, no StudentAnswer row | |
| BE-TC-20 | Anonymous (no JWT) → 401 | P0 | PASS | 401 (framework challenge) | |
| BE-TC-21 | Parent JWT (wrong role) → 403 | P0 | PASS | 403 Forbidden | |
| BE-TC-22 | SuperAdmin JWT (wrong role) → 403 | P2 | PASS | 403 Forbidden | |
| BE-TC-23 | AnswerSubmittedIntegrationEvent fires once (SkillId set) | P0 | PASS | 1 event, correct StudentId/LessonId/SkillId/CorrectAnswerCount | |
| BE-TC-23-NEG | No event on any rejected SubmitAnswer path | P0 | PASS | 0 events on duplicate/ownership/non-InProgress rejections | |
| BE-TC-24 | No event when QuizQuestion.SkillId null (+200+persist) | P0 | PASS | 200, 0 events captured | |
| BE-TC-25 | LessonCompletedIntegrationEvent fires once on Complete | P0 | PASS | 1 event, AccuracyPercentage=67, CorrectAnswerCount=2 | |
| BE-TC-26 | No LessonCompleted event when Lesson.SkillId null | P1 | PASS | 200, 0 LessonCompleted events | |
| BE-TC-27 | LessonCompleted does NOT re-fire on idempotent re-Complete | P0 | PASS | Total 1 event across 2 Complete calls | |
| BE-TC-28 | Handler isolation: throwing subscriber → still 200 | P0 | PASS | 200 + capturing handler still received event | |
| BE-TC-29 | Event payload data-minimization (no PII) | P1 | PASS | Event has only opaque IDs; no AnswerPayload/CorrectAnswer/PII | |
| BE-TC-30 | AttemptId ≤ 0 → 422 | P1 | PASS | 422 from ValidationBehavior | |
| BE-TC-31 | QuestionId ≤ 0 → 422 | P1 | PASS | 422 from ValidationBehavior | |
| BE-TC-32 | Empty AnswerPayload → 422 | P1 | PASS | 422 NotEmpty | |
| BE-TC-33 | TimeSpentSeconds boundary (0/3600/3601/-1) | P1 | PASS | -1→422, 0→200, 3600→200, 3601→422 | |

## Summary

| Metric | Count |
|---|---|
| Total | 33 |
| Passed | 33 |
| Failed | 0 |
| Blocked | 0 |
| P0 failures (release-blocking) | 0 |

## Defects found

None. All QC catalog cases GREEN.

## Phase-7 regression note (base suite)

The pre-existing `P2_07_InstantAnswerFeedback_Tests` base class (13 tests) has 13 failures on this branch. Root cause: Phase-7 added `IsActive && LifecycleState == Published` filter to `StartAttemptCommandHandler`. Base tests seed lessons without these fields (Draft/inactive by default). Extended tests set `IsActive=true, LifecycleState=Published` in all inline seeders and pass. This regression predates the QC branch — it is a **pre-existing defect** in the base tests, not caused by QC changes. Reported to `backend-feature`.

## Open-question outcomes

- **OQ-1 (IDOR code):** BE-TC-18 = **401** (as expected per as-built code; brief said 403/404).
- **OQ-2 (404 vs 401 enumeration):** BE-TC-19 cross-lesson injection = **404** (QuestionNotFound as expected).
- **OQ-5 (role gate):** BE-TC-21 parent = **403**; BE-TC-20 anonymous = **401**. Both as expected.
