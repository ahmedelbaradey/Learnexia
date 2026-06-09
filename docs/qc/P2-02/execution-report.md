# Execution Report — P2-02 (Browse subjects & lessons) — BACKEND

> **Owner of this file:** `api-tester`. The `qc-test-designer` scaffolds this template; it does **not** fill results.
> Fill one row per case from `backend-test-cases.md`. Status ∈ `Pass | Fail | Blocked`.
> For every `Fail`/`Blocked`, add a defect/blocker entry below with reproduction detail.

## Run metadata (fill on execution)
- **Date / time (UTC):** _TBD_
- **Commit / branch under test:** _TBD_
- **API base URL:** _TBD_
- **DB state:** migrated? _Y/N_ · P2-10 seed applied to fresh DB? _Y/N_
- **Auth fixtures minted:** `STUDENT_AR` _Y/N_ · `STUDENT_EN` _Y/N_ · `STUDENT_NOCLAIM` _Y/N_
- **Seeded actuals observed:** subjects/grade = ___ · Math-G1 units×lessons = ___ × ___ · Math-G1 concepts/skills = ___ / ___

## Results

| ID | Title (short) | Priority | Status | Actual / notes |
|----|---------------|----------|--------|----------------|
| BE-TC-01 | Subjects for grade 1 (ar) | P0 | _TBD_ | |
| BE-TC-02 | Exactly 4 MVP codes, no Social Studies | P0 | _TBD_ | |
| BE-TC-03 | Grade-6 boundary returns codes | P1 | _TBD_ | |
| BE-TC-04 | No cross-grade leakage | P0 | _TBD_ | |
| BE-TC-05 | gradeNumber reflects request, not GradeId | P1 | _TBD_ | |
| BE-TC-06 | Lang filter: Math/Science follow learner | P0 | _TBD_ | |
| BE-TC-07a | Grade 0 → 400 | P1 | _TBD_ | |
| BE-TC-07b | Grade 7 → 400 | P1 | _TBD_ | |
| BE-TC-08 | Existing-grade-no-subjects → 200 empty; missing grade → 404 | P1 | _TBD_ | |
| BE-TC-09 | ForGrade anonymous → 401 | P0 | _TBD_ | |
| BE-TC-12 | Units in SequenceOrder | P0 | _TBD_ | |
| BE-TC-13 | Lessons in SequenceOrder + State present | P0 | _TBD_ | |
| BE-TC-14 | Subject no units → 200 empty | P0 | _TBD_ | |
| BE-TC-15 | Lessons: non-existent subject → 404 | P0 | _TBD_ | |
| BE-TC-16 | State engine-derived (P2-04), not placeholder | P1 | _TBD_ | |
| BE-TC-17 | Lessons anonymous → 401 | P0 | _TBD_ | |
| BE-TC-18 | SkillTree concepts→skills, State present | P0 | _TBD_ | |
| BE-TC-19 | SkillTree seeded counts present | P1 | _TBD_ | |
| BE-TC-20 | Concepts/skills ordered by Id | P2 | _TBD_ | |
| BE-TC-21 | SkillTree lang filter (redirect, no 403) | P1 | _TBD_ | |
| BE-TC-22 | SkillTree non-existent subject → 404 | P1 | _TBD_ | |
| BE-TC-23 | SkillTree no concepts → 200 empty | P2 | _TBD_ | |
| BE-TC-24 | SkillTree anonymous → 401 | P0 | _TBD_ | |
| BE-TC-25 | Student blocked from admin Create | P2 | _TBD_ | |
| BE-TC-26 | Envelope shape across all 3 | P0 | _TBD_ | |
| BE-TC-27 | Cross-lang LESSON by id → 403 | P0 | _TBD_ | |
| BE-TC-28 | Same-lang LESSON by id → 200 (control) | P1 | _TBD_ | |
| BE-TC-29 | No learning_language claim → Arabic default, no error | P1 | _TBD_ | |
| BE-TC-30 | Grade-scope not enforced today (no 403) — documented | P2 | _TBD_ | |

## Summary (fill on execution)
- **Pass:** ___ / 28 · **Fail:** ___ · **Blocked:** ___
- **P0 pass rate:** ___ / 13
- **Coverage verdict:** _TBD_

## Defects & blockers (fill on execution)
> One block per Fail/Blocked. Include: case ID, request, expected vs actual, status code + envelope, severity, and whether it is a P2-02 regression or an environment/seed/known-gap issue (see README R1–R5, Q1–Q4).

1. _TBD_
