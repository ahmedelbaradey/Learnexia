# P7-01 — Subjects & Units admin — Execution report

> Template scaffolded by `qc-test-designer`. **`api-tester` fills the backend rows after running** the GAP cases from
> `backend-test-cases.md`. The FE lead's runner fills the frontend rows (optional for the backend lead). Do not edit
> the test-case catalog here — record results only.

## Run metadata
- Date / runner:
- Branch / commit:
- Environment (Docker + Postgres up?):
- Command:

## Backend results

| ID | Title | Result (PASS/FAIL/BLOCKED) | Notes / defect link |
|----|-------|----------------------------|---------------------|
| BE-TC-01 | Subject Update non-existent → 404, no leak | | |
| BE-TC-02 | Unit Update non-existent → 404, no leak | | |
| BE-TC-03 | Grade Update non-existent → 404, no leak | | |
| BE-TC-04 | Grade Delete with subjects → 400, no leak | | |
| BE-TC-05 | Grade Delete (empty) succeeds | | |
| BE-TC-06 | Subject Update → duplicate tree → 400/422, no leak | | |
| BE-TC-07 | Unit Update → non-existent SubjectId → 404 | | |
| BE-TC-08 | Subject Delete non-existent → 404 | | |
| BE-TC-09 | Unit Delete non-existent → 404 | | |
| BE-TC-10 | Subject Update happy-path persists | | |
| BE-TC-11 | Unit Update happy-path persists | | |
| BE-TC-12 | Subject GetById admin round-trip | | |
| BE-TC-13 | Subject Update empty Name → 422 | | |
| BE-TC-15 | Subjects/Units List+GetById auth lockdown | | |
| BE-TC-17 | Grades read reachable by non-admin | | |
| BE-TC-25 | Unit SetActive 0-id → 422 | | |
| BE-TC-33 | Coverage gradeId=0 boundary | | |
| BE-TC-34 | Subjects/List pagination metadata | | |
| BE-TC-35 | Reorder duplicate-id edge | | |

(Covered cases BE-TC-14, 16, 18–24, 26–32 already pass in the existing suite — re-run for regression confirmation, no new code.)

## Defects found

| # | Case | Severity | Summary | Status |
|---|------|----------|---------|--------|
| | | | | |
