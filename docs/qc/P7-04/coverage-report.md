# P7-04 — Question authoring admin — Coverage report

## Summary
- Story: P7-04 Manage quizzes & questions (quiz CRUD + 4 question types with per-type validation + attach to lesson/skill + same-language guard).
- Backend cases catalogued: **26** (BE-TC-01..26). Covered by existing tests: **19**. **GAP (to implement): 7.**
- Frontend reference cases: 8 (FE lead).
- Existing test sources: `P7_04_QuestionsAdmin_Tests.cs` (~50 facts) + `P7_04_QuestionAuthoring_Tests.cs` (~21 facts).

## ★ Story-vs-implementation divergence (lead decision needed)
The story is written around a **Quiz aggregate** ("create/edit a quiz with an ordered list of questions", "attach the
quiz to a lesson/skill"). The implementation has **no Quiz CRUD and no `Attach` endpoint** — questions are authored
directly per **lesson** (lesson = implicit quiz) via `QuestionsController`, each question carrying `LessonId` +
optional `SkillId`. Consequences:
- "Quiz CRUD with ordered question list" → realized as per-lesson question CRUD + reorder. **Covered.**
- "Attach quiz to lesson/skill, same-language only" → there is **no attach endpoint**; the realizable surrogate is the
  question's `LessonId`/`SkillId` pairing (BE-TC-15/16/17, all **GAP**). The cross-language guard for that pairing is
  **not tested and may not be implemented.**
- **Action for the lead:** decide whether the Quiz-aggregate + Attach criteria are (a) intentionally collapsed into
  per-lesson question authoring (then BE-TC-15/16/17 are the coverage), or (b) a missing feature to backlog.

## Acceptance criteria → test cases → status

| Acceptance criterion (story) | Test case(s) | Status |
|------------------------------|--------------|--------|
| Quiz persists with ordered question list | BE-TC-01, BE-TC-18, BE-TC-19 | Covered (as per-lesson questions) |
| Add question, choose 1 of 4 types, enforce type's shape | BE-TC-01, BE-TC-08..12 | Covered |
| Invalid per-type shape → clear per-type validation error | BE-TC-08..12 | Covered |
| Quiz/question inherits Subject-tree language | BE-TC-16 | **GAP** |
| Attach stays within same `(SubjectCode,Language)` tree | BE-TC-15, BE-TC-17 | **GAP** (no attach endpoint — surrogate via LessonId/SkillId) |
| Edit/remove question, order updates | BE-TC-03, BE-TC-18, BE-TC-25, BE-TC-26 | Covered + **GAP** (Edit/Delete non-existent regression) |
| Admin-only access; non-admin → 403 | BE-TC-23 | Covered |
| (security) correctAnswer never reaches student | BE-TC-22 | Covered |
| (completeness) all 4 types graded end-to-end | BE-TC-06, BE-TC-14 | Covered (MCQ/TF/FIB) + **GAP** (Matching grade round-trip) |

**One criterion is materially uncovered: the same-language attach guard** (BE-TC-15) — because the underlying endpoint
may not exist. Flagged as the top open question, not silently dropped.

## Prioritized GAP list for `api-tester`

**P0:**
1. BE-TC-25 Edit non-existent questionId → 404 not 500, no `ex.Message` leak (PR #183-style)

**P1:**
2. BE-TC-15 Cross-language LessonId↔SkillId pairing → rejected (or confirm endpoint absent → lead)
3. BE-TC-14 Matching authored → student-grades-correct round-trip

**P2:**
4. BE-TC-16 Question DTO surfaces resolved language
5. BE-TC-17 Mismatched SkillId → graceful (not 500)
6. BE-TC-26 Delete non-existent questionId → `Successed=false`, no leak

## Risk notes
- **Coverage is the strongest of the five stories** — the jsonb CorrectAnswer fix and the CRITICAL correctAnswer-leak
  guard both have dedicated suites. Residual functional risk is low.
- **Top risk is the story-vs-impl gap** (no Quiz aggregate / no Attach): the same-language attach criterion is
  effectively unverified. This is a **specification/scope** risk more than a test risk — surface it to the lead before
  `api-tester` spends effort on BE-TC-15.
- Matching is the only type without an end-to-end grade round-trip (BE-TC-14) — a real functional blind spot for the
  most complex type.
