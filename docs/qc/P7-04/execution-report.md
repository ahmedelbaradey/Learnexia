# P7-04 — Question authoring admin — Execution report

> Template scaffolded by `qc-test-designer`. `api-tester` fills the backend rows after running the GAP cases.
> NOTE: resolve the Quiz-aggregate / Attach-endpoint open question with the lead before implementing BE-TC-15/17.

## Run metadata
- Date / runner:
- Branch / commit:
- Environment (Docker + Postgres up?):
- Command:

## Backend results (GAP cases to implement)

| ID | Title | Result (PASS/FAIL/BLOCKED) | Notes / defect link |
|----|-------|----------------------------|---------------------|
| BE-TC-14 | Matching authored → student grades correct | | |
| BE-TC-15 | Cross-language LessonId↔SkillId pairing rejected | | (BLOCKED if no attach/guard exists — escalate) |
| BE-TC-16 | Question DTO resolved language | | |
| BE-TC-17 | Mismatched SkillId → graceful (not 500) | | |
| BE-TC-25 | Edit non-existent questionId → 404, no leak | | |
| BE-TC-26 | Delete non-existent questionId → Successed=false | | |

(Covered cases BE-TC-01..13, 18..24 already pass in `P7_04_QuestionsAdmin_Tests.cs` + `P7_04_QuestionAuthoring_Tests.cs` — re-run for regression.)

## Defects found

| # | Case | Severity | Summary | Status |
|---|------|----------|---------|--------|
| | | | | |
