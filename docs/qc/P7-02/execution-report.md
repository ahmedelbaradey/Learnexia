# P7-02 — Lessons & Content Blocks admin — Execution report

> Template scaffolded by `qc-test-designer`. `api-tester` fills the backend rows after running the GAP cases. FE rows
> are for the frontend lead's runner.

## Run metadata
- Date / runner:
- Branch / commit:
- Environment (Docker + Postgres up?):
- Command:

## Backend results (GAP cases to implement)

| ID | Title | Result (PASS/FAIL/BLOCKED) | Notes / defect link |
|----|-------|----------------------------|---------------------|
| BE-TC-10 | Add block to non-existent lessonId → Successed=false | | |
| BE-TC-11 | Edit non-existent blockId → Successed=false, no leak | | |
| BE-TC-12 | Delete non-existent blockId → Successed=false | | |
| BE-TC-13 | Malformed payload → 422 not 500 | | |
| BE-TC-14 | Oversized payload handled gracefully | | |
| BE-TC-25 | Lesson Update non-existent → 404, no leak | | |
| BE-TC-30 | Lessons List auth lockdown | | |
| BE-TC-31 | Lesson admin DTO resolved language | | |
| BE-TC-32 | Lesson placement resolves to single language | | |

(Covered cases BE-TC-01..09, 15..24, 26..29 already pass in `P7_02_LessonsContentBlocks_Tests.cs` — re-run for regression.)

## Defects found

| # | Case | Severity | Summary | Status |
|---|------|----------|---------|--------|
| | | | | |
