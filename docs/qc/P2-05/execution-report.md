# P2-05 — Execution Report (BACKEND)

> **Template — to be filled by `api-tester` after implementing `backend-test-cases.md`.**
> `qc-test-designer` scaffolds this file but never fills results. Record pass/fail per case ID + any defects found. Link the test class/method that implements each case.

## Run metadata

| Field | Value |
|---|---|
| Run date | _(fill)_ |
| Executed by | `api-tester` |
| Branch / commit | _(fill)_ |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Test class(es) | `P2_05_OpenAndCompleteLesson_Tests` + _(new class for added cases, if any)_ |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P2_05"` |
| Overall result | _(PASS / FAIL — counts)_ |

## Per-case results

| Case ID | Title (short) | Priority | Implementing test method | Result (Pass/Fail/Blocked/Skipped) | Notes / defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Anonymous open → 401 | P0 | | | |
| BE-TC-02 | Demo lesson full assembly DTO | P0 | | | |
| BE-TC-03 | QuickCheck shape | P1 | | | |
| BE-TC-04 | QuickCheck = first question by Id ASC | P2 | | | |
| BE-TC-05 | Non-demo lesson → null content | P1 | | | |
| BE-TC-06 | No CorrectAnswer (deep walk) | P0 | | | |
| BE-TC-07 | Back-compat ?id= route | P1 | | | |
| BE-TC-08 | Re-open idempotency / no auto-attempt | P2 | | | |
| BE-TC-09 | Non-existent id → 404 (not 500) | P0 | | | |
| BE-TC-10 | Parent JWT accepted (not 401) | P2 | | | |
| BE-TC-11 | Open wrong-language lesson → 403 | P0 | | | |
| BE-TC-12 | Start wrong-language lesson → 403 | P0 | | | |
| BE-TC-13 | Start lessonId<=0 → 422 | P1 | | | |
| BE-TC-14 | Start creates persisted attempt | P0 | | | |
| BE-TC-15 | Start questions carry no CorrectAnswer | P0 | | | |
| BE-TC-16 | Re-start resumes same attempt | P1 | | | |
| BE-TC-17 | IDOR: B submits/completes A's attempt → 401 | P0 | | | |
| BE-TC-18 | Start LOCKED lesson — KNOWN GAP (200 today) | P1 | | | R3 follow-up baseline |
| BE-TC-19 | Start non-existent lesson → 404 | P1 | | | |
| BE-TC-20 | E2E happy loop → Completed | P0 | | | |
| BE-TC-21 | Completion → NodeState.Completed | P0 | | | |
| BE-TC-22 | Owner-only completion → 401 for others | P1 | | | |
| BE-TC-23 | Wrong answer still completes | P1 | | | |
| BE-TC-24 | LessonCompletedIntegrationEvent fires | P1 | | | lesson-side contract only |
| BE-TC-25 | Submit empty AnswerPayload → 422 | P1 | | | |
| BE-TC-26 | Submit TimeSpentSeconds>3600 → 422 | P2 | | | |
| BE-TC-27 | Submit cross-lesson question → 404 | P1 | | | |
| BE-TC-28 | Re-answer same question → 424 | P1 | | | |
| BE-TC-29 | Submit/complete non-in-progress → 424 | P1 | | | |
| BE-TC-30 | Re-complete idempotent (200, no 2nd event) | P1 | | | |
| BE-TC-31 | Seeder smoke: 4 demo lessons | P1 | | | |
| BE-TC-32 | Student creates lesson → 403 (AdminOnly) | P1 | | | |
| BE-TC-33 | Lesson CRUD anonymous → 401 | P2 | | | |
| BE-TC-34 | Malformed body → 400 (not 500) | P2 | | | |

## Defects found

| ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| | | | | |

## Regression check

| Suite | Result |
|---|---|
| `P2_05_OpenAndCompleteLesson_Tests` | _(fill)_ |
| `P2_02_BrowseSubjectsAndLessons_Tests` | _(fill)_ |
| `P2_04_LearningPath_Tests` | _(fill)_ |
| `P2_06_StartAttempt_Tests` | _(fill)_ |
| `P2_07_InstantAnswerFeedback_Tests` | _(fill)_ |
| `P2_08_RecordGranularAnswers_Tests` | _(fill)_ |

## Open items for the lead (carried from README Q1–Q5)

- Q1 — BE-TC-18 locked-lesson: characterization (today's 200) vs blocked-pending hardening? _(record decision)_
- Q2 — confirmed Ar-tree mismatch fixture exists for BE-TC-11/12? _(record lesson id used)_
- Q3 — IDOR returns 401 (not 403/404) — accepted convention or defect? _(record)_
- Q4 — abandon used only as negative-state fixture (BE-TC-29). _(confirm)_
- Q5 — Parent JWT on open: 200 or language-guard 403? _(record observed)_
