# Execution Report — P2-06 (Take a quiz) — BACKEND

> **Template scaffolded by qc-test-designer. Results are filled by `api-tester` after running the cases in `backend-test-cases.md`. Do not fill results before execution.**

## Run metadata

| Field | Value |
|---|---|
| Executed by | _(api-tester)_ |
| Date | _(yyyy-mm-dd)_ |
| API base URL | _(e.g. http://localhost:5000)_ |
| Build SHA / branch | _(fill)_ |
| Seed method | _(how lessons/subjects/quiz questions were seeded — API vs direct SQL into `learning` schema)_ |
| Overall verdict | _(PASS / FAIL / PARTIAL)_ |

## Results — start-quiz cases

| ID | Title | Priority | Result (Pass/Fail/Blocked/Skipped) | Observed (status code / notes) | Defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Start attempt returns attemptId + questions | P0 | | | |
| BE-TC-02 | BaseResponse envelope + `Successed` spelling | P0 | | | |
| BE-TC-03 | Student role accepted (200) | P0 | | | |
| BE-TC-04 | Same-language lesson passes guard (200) | P1 | | | |
| BE-TC-05 | All 4 question types returned with discriminator | P0 | | | |
| BE-TC-06 | Per-type Options shape preserved | P1 | | | |
| BE-TC-07 | CorrectAnswer never in start payload | P0 | | | |
| BE-TC-08 | StudentId from JWT, not request | P0 | | | |
| BE-TC-09 | Parent role rejected (403) | P0 | | | |
| BE-TC-10 | Admin/non-Student rejected (403) | P1 | | | |
| BE-TC-11 | Anonymous rejected (401) | P0 | | | |
| BE-TC-12 | Cross-language lesson rejected (403) | P1 | | | |
| BE-TC-13 | Attempt row persisted with correct fields | P0 | | | |
| BE-TC-14 | Quiz entities full forward-compatible schema | P2 | | | |
| BE-TC-15 | Re-start resumes, no duplicate Attempt | P0 | | | |
| BE-TC-16 | Non-existent lesson → 404 | P0 | | | |
| BE-TC-17 | lessonId = 0 → 422 | P1 | | | |
| BE-TC-18 | Negative lessonId → 422 | P2 | | | |
| BE-TC-19 | Non-numeric lessonId → 404 (routing) | P2 | | | |
| BE-TC-20 | Empty lesson → 200 + empty list | P1 | | | |
| BE-TC-21 | No teacher role can start | P1 | | | |
| BE-TC-22 | 4-subject scope (no Social Studies) | P2 | | | |
| BE-TC-23 | [PROBE] Locked skill NOT rejected (gap) | P1 | | | |
| BE-TC-24 | [BLOCKED] Concurrent starts — one Attempt | P2 | Blocked | Pending Open Q2 ruling | |

## Regression smoke

| ID | Title | Result | Observed | Defect ref |
|---|---|---|---|---|
| REG-1 | SubmitAnswer happy path (P2-07) | | | |
| REG-2 | Complete / Abandon idempotent (P2-08) | | | |
| REG-3 | SubmitAnswer to other student's attempt → 401 | | | |
| REG-4 | Existing Learning skills endpoint still 200 | | | |

## Summary

| Metric | Count |
|---|---|
| Passed | |
| Failed | |
| Blocked | |
| Skipped | |
| **Total** | 24 (+4 regression) |

## Defects found

_(api-tester: list each defect with ID, severity, affected case, repro, expected vs actual.)_

## Open-question outcomes

- **Open Q1 (locked-skill, BE-TC-23):** _(lead ruling + result)_
- **Open Q2 (concurrency, BE-TC-24):** _(lead ruling + result)_
