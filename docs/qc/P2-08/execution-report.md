# P2-08 — Execution Report (Backend)

> **Owner: `api-tester`.** The `qc-test-designer` scaffolds this template; the tester fills results AFTER running.
> Do not edit the test-case definitions here — they live in `backend-test-cases.md`. Record only outcomes.

## Run metadata

| Field | Value |
|---|---|
| Run by (agent) | _api-tester_ |
| Date | _yyyy-mm-dd_ |
| Branch / commit | _…_ |
| API base URL | _http://localhost:5080 (or test host)_ |
| DB | _real PostgreSQL (Testcontainers / local stack)_ |
| Test project | _backend/tests/Learnexia.IntegrationTests/_ |
| Build status | _pass / fail_ |

## Result summary

| Metric | Count |
|---|---|
| Total cases | 51 |
| Passed | _…_ |
| Failed | _…_ |
| Blocked (with reason) | _…_ |
| Not run | _…_ |

## Per-case results

> Status = PASS / FAIL / BLOCKED / N-A. For FAIL, give the observed status code + body shape and a one-line defect.
> For BLOCKED, give the blocker (e.g. "no fault-injection seam for 500").

| Case | Title (short) | Priority | xUnit test method | Status | Notes / defect |
|---|---|---|---|---|---|
| BE-TC-01 | Submit correct → persisted | P0 | _…_ | _…_ | _…_ |
| BE-TC-02 | Submit wrong → correctAnswer disclosed | P0 | _…_ | _…_ | _…_ |
| BE-TC-03 | HintUsed round-trips | P1 | _…_ | _…_ | _…_ |
| BE-TC-04 | Correctness computed server-side | P1 | _…_ | _…_ | _…_ |
| BE-TC-05 | Duplicate answer → 424 | P0 | _…_ | _…_ | _…_ |
| BE-TC-06 | Submit to other's attempt → 401 (IDOR) | P0 | _…_ | _…_ | _…_ |
| BE-TC-07 | Submit non-existent attempt → 404 | P1 | _…_ | _…_ | _…_ |
| BE-TC-08 | Submit to non-InProgress → 424 | P0 | _…_ | _…_ | _…_ |
| BE-TC-09 | Cross-lesson question → 404 | P0 | _…_ | _…_ | _…_ |
| BE-TC-10 | Submit no JWT → 401 | P0 | _…_ | _…_ | _…_ |
| BE-TC-11 | Submit Parent/Admin JWT → 403 | P0 | _…_ | _…_ | _…_ |
| BE-TC-12 | Empty AnswerPayload → 422 | P0 | _…_ | _…_ | _…_ |
| BE-TC-13 | QuestionId<=0 → 422 | P1 | _…_ | _…_ | _…_ |
| BE-TC-14 | TimeSpentSeconds boundaries → 422 | P0 | _…_ | _…_ | _…_ |
| BE-TC-15 | AttemptId<=0 → 422 | P2 | _…_ | _…_ | _…_ |
| BE-TC-16 | Oversized AnswerPayload (F-01) | P2 | _…_ | _…_ | _…_ |
| BE-TC-17 | Complete mixed → aggregates | P0 | _…_ | _…_ | _…_ |
| BE-TC-18 | Complete zero answers → accuracy 0 | P0 | _…_ | _…_ | _…_ |
| BE-TC-19 | Accuracy rounding 2dp | P1 | _…_ | _…_ | _…_ |
| BE-TC-20 | Complete idempotent | P0 | _…_ | _…_ | _…_ |
| BE-TC-21 | Complete already-Abandoned → 424 | P0 | _…_ | _…_ | _…_ |
| BE-TC-22 | Complete other's attempt → 401 (IDOR) | P0 | _…_ | _…_ | _…_ |
| BE-TC-23 | Complete non-existent → 404 | P1 | _…_ | _…_ | _…_ |
| BE-TC-24 | Complete no JWT → 401 / Parent → 403 | P0 | _…_ | _…_ | _…_ |
| BE-TC-25 | Complete AttemptId<=0 → 422 | P2 | _…_ | _…_ | _…_ |
| BE-TC-26 | Answers survive completion | P1 | _…_ | _…_ | _…_ |
| BE-TC-27 | Abandon partial → status+aggregates+answers | P0 | _…_ | _…_ | _…_ |
| BE-TC-28 | Abandon zero answers → accuracy 0 | P0 | _…_ | _…_ | _…_ |
| BE-TC-29 | Abandon idempotent | P0 | _…_ | _…_ | _…_ |
| BE-TC-30 | Abandon already-Completed → 424 | P0 | _…_ | _…_ | _…_ |
| BE-TC-31 | Abandon other's attempt → 401 (IDOR) | P0 | _…_ | _…_ | _…_ |
| BE-TC-32 | Abandon non-existent → 404 | P1 | _…_ | _…_ | _…_ |
| BE-TC-33 | Abandon no JWT/Parent/AttemptId<=0 | P1 | _…_ | _…_ | _…_ |
| BE-TC-34 | Abandoned answers retrievable+accurate | P1 | _…_ | _…_ | _…_ |
| BE-TC-35 | Self read attempts, newest first | P0 | _…_ | _…_ | _…_ |
| BE-TC-36 | CorrectAnswer never leaked in list | P0 | _…_ | _…_ | _…_ |
| BE-TC-37 | Read other's attempts → 401 (IDOR) | P0 | _…_ | _…_ | _…_ |
| BE-TC-38 | No attempts → 200 empty list | P1 | _…_ | _…_ | _…_ |
| BE-TC-39 | studentId<=0 → 400 | P1 | _…_ | _…_ | _…_ |
| BE-TC-40 | E4 no JWT → 401 / Parent IDOR | P1 | _…_ | _…_ | _…_ |
| BE-TC-41 | Skill stats with data | P0 | _…_ | _…_ | _…_ |
| BE-TC-42 | Null-SkillId answers excluded | P0 | _…_ | _…_ | _…_ |
| BE-TC-43 | Skill no answers → zeroed (not 500) | P0 | _…_ | _…_ | _…_ |
| BE-TC-44 | Stats scoped to requester (no bleed) | P0 | _…_ | _…_ | _…_ |
| BE-TC-45 | Other's skill stats → 401 (IDOR) | P0 | _…_ | _…_ | _…_ |
| BE-TC-46 | skillId/studentId<=0 → 400 | P1 | _…_ | _…_ | _…_ |
| BE-TC-47 | E5 no JWT → 401 / Parent IDOR | P1 | _…_ | _…_ | _…_ |
| BE-TC-48 | Envelope shape + Successed spelling | P0 | _…_ | _…_ | _…_ |
| BE-TC-49 | ServerError no ex.Message leak | P1 | _…_ | _…_ | _…_ |
| BE-TC-50 | DurationSeconds server-side, non-neg | P1 | _…_ | _…_ | _…_ |
| BE-TC-51 | Mass-assignment StudentId/IsCorrect guard | P1 | _…_ | _…_ | _…_ |

## Defects found (for `backend-feature` / lead)

| # | Severity | Case(s) | Endpoint | Observed | Expected | Notes |
|---|---|---|---|---|---|---|
| _D1_ | _…_ | _…_ | _…_ | _…_ | _…_ | _…_ |

## Open-question outcomes (resolved during the run, if any)

- _401-vs-403 ownership contract: …_
- _424-vs-409 business-state contract: …_
- _F-01 oversized payload decision: …_
- _BE-TC-49 500 fault seam available? …_

## Verdict

_PASS / PASS-WITH-DEFECTS / FAIL — one line, plus blocked-case count and any RED that is a real defect vs. a documented contract gap._
