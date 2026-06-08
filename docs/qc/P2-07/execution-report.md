# Execution Report — P2-07 Instant Answer Feedback (Backend)

> **Owner: `api-tester`.** This file is scaffolded empty by `qc-test-designer`. Fill it AFTER implementing and running the cases in `backend-test-cases.md`. One row per BE-TC ID. `qc-test-designer` does not fill results.

## Run metadata

| Field | Value |
|---|---|
| Run date | _TBD_ |
| Executed by | `api-tester` |
| Branch / commit | _TBD_ |
| Test file(s) | _e.g._ `backend/tests/Learnexia.IntegrationTests/P2_07_InstantAnswerFeedback_Tests.cs` |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P2_07"` |
| API/DB state | _PostgreSQL `Learnexia`, migrations applied via `ApplyMigrationsAndSeedAsync`_ |

## Results

| Case ID | Title | Priority | Status (PASS/FAIL/BLOCKED) | Evidence (status code / value observed) | Defect / note |
|---|---|---|---|---|---|
| BE-TC-01 | Correct → isCorrect=true, correctAnswer null, no leak | P0 | | | |
| BE-TC-02 | Wrong → isCorrect=false, correctAnswer populated | P0 | | | |
| BE-TC-03 | Same-screen synchronous feedback | P1 | | | |
| BE-TC-04 | Answer result persisted | P0 | | | |
| BE-TC-05 | MCQ correct (case-insensitive, trimmed) | P0 | | | |
| BE-TC-06 | MCQ wrong | P0 | | | |
| BE-TC-07 | TrueFalse correct | P0 | | | |
| BE-TC-08 | TrueFalse wrong | P0 | | | |
| BE-TC-09 | TrueFalse malformed → wrong, no throw | P0 | | | |
| BE-TC-10 | FillInBlank correct (trim + case-insensitive) | P0 | | | |
| BE-TC-11 | FillInBlank wrong | P1 | | | |
| BE-TC-12 | Whitespace payload grades wrong | P1 | | | |
| BE-TC-13 | Matching string-compare fallback (documentary) | P2 | | | |
| BE-TC-14 | Correct answer never leaked on correct path (sweep) | P0 | | | |
| BE-TC-15 | No ex.Message / answer leak on error path | P1 | | | |
| BE-TC-16 | Forged isCorrect/correctAnswer keys ignored | P0 | | | |
| BE-TC-17 | Inverse: injected IsCorrect:false ignored | P2 | | | |
| BE-TC-18 | IDOR: A submits to B's attempt → 401 | P0 | | | |
| BE-TC-19 | Cross-lesson question injection → 404, not graded | P1 | | | |
| BE-TC-20 | Anonymous (no JWT) → 401 | P0 | | | |
| BE-TC-21 | Parent JWT (wrong role) → 403 | P0 | | | |
| BE-TC-22 | SuperAdmin JWT (wrong role) → 403 | P2 | | | |
| BE-TC-23 | AnswerSubmittedIntegrationEvent fires once (SkillId set) | P0 | | | |
| BE-TC-23-NEG | No event on any rejected SubmitAnswer path | P0 | | | |
| BE-TC-24 | No event when QuizQuestion.SkillId null (+200+persist) | P0 | | | |
| BE-TC-25 | LessonCompletedIntegrationEvent fires once on Complete | P0 | | | |
| BE-TC-26 | No LessonCompleted event when Lesson.SkillId null | P1 | | | |
| BE-TC-27 | LessonCompleted does NOT re-fire on idempotent re-Complete | P0 | | | |
| BE-TC-28 | Handler isolation: throwing subscriber → still 200 | P0 | | | |
| BE-TC-29 | Event payload data-minimization (no PII) | P1 | | | |
| BE-TC-30 | AttemptId ≤ 0 → 422 | P1 | | | |
| BE-TC-31 | QuestionId ≤ 0 → 422 | P1 | | | |
| BE-TC-32 | Empty AnswerPayload → 422 | P1 | | | |
| BE-TC-33 | TimeSpentSeconds boundary (0/3600/3601/-1) | P1 | | | |

## Summary

| Metric | Count |
|---|---|
| Total | 33 |
| Passed | |
| Failed | |
| Blocked | |
| P0 failures (release-blocking) | |

## Defects found

| ID | Case | Severity | Description | Status |
|---|---|---|---|---|
| | | | | |

## Open-question outcomes (confirm against actual runs)

- **OQ-1 (IDOR code):** observed status for BE-TC-18 = _____ (expected 401).
- **OQ-2 (404 vs 401 enumeration):** BE-TC-19 = _____ ; BE-TC-05/missing-attempt = _____.
- **OQ-5 (role gate):** BE-TC-21 parent = _____ (expected 403); BE-TC-20 anonymous = _____ (expected 401).
