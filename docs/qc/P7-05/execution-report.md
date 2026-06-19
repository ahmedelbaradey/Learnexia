# P7-05 — Content lifecycle — Execution report

> Template scaffolded by `qc-test-designer`. `api-tester` fills the backend rows after running the GAP cases.

## Run metadata
- Date / runner:
- Branch / commit:
- Environment (Docker + Postgres up?):
- Command:

## Backend results (GAP cases to implement)

| ID | Title | Result (PASS/FAIL/BLOCKED) | Notes / defect link |
|----|-------|----------------------------|---------------------|
| BE-TC-08 | Self-transition deterministic, no 500 | | |
| BE-TC-09 | Published→Published re-publish version behavior | | |
| BE-TC-10 | Illegal transition enforced for Subject/Unit/Question | | |
| BE-TC-14 | EntityType/EntityId mismatch → graceful | | |
| BE-TC-21 | ContentVersion publishedBy == acting admin | | |
| BE-TC-22 | Rollback reverts entity content | | |
| BE-TC-23 | Rollback per-(SubjectCode,Language) tree only | | |
| BE-TC-27 | Preview shows pending edit vs live published | | |
| BE-TC-33 | Draft QuizQuestion not in student StartAttempt | | |
| BE-TC-34 | Child Published / ancestor Draft → no leak | | |

(Covered cases BE-TC-01..07, 11..13, 15..20, 24..26, 28..32, 35 already pass in `P7_05_ContentLifecycle_Tests.cs` — re-run for regression.)

## Defects found

| # | Case | Severity | Summary | Status |
|---|------|----------|---------|--------|
| | | | | |
