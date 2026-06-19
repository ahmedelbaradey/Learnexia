# P7-04 — Question authoring admin — Backend API test cases

> Target agent: `api-tester`. **TWO** existing files cover this story:
> `P7_04_QuestionsAdmin_Tests.cs` (~50 facts: CRUD, per-type validation, reorder, activate, auth, the CRITICAL
> correctAnswer-leak guard) and `P7_04_QuestionAuthoring_Tests.cs` (~21 facts: the jsonb CorrectAnswer fix —
> add/edit each type returns 200-not-500, plus authored→student-grades-correct round-trips and Edit double-encode
> regression). This catalog is **gap analysis**.
>
> ★ IMPLEMENTATION REALITY (read first): There is **NO Quiz-level CRUD and NO `Quizzes/{id}/Attach` endpoint.**
> `QuizzesController` is student-facing (Start/Submit/Complete/Abandon). The admin authoring path is the
> **`QuestionsController`** — questions are authored **per lesson** (the lesson is the implicit quiz), each question
> carrying `LessonId` + optional `SkillId`. The story's "Quiz CRUD with ordered question list" maps to per-lesson
> question CRUD + reorder; the story's "attach quiz to a lesson/skill, same-language only" maps to setting
> `LessonId`/`SkillId` on the question. **The cross-language attach-rejection criterion has no dedicated endpoint** —
> see GAP BE-TC-15/16. Flag to the lead whether a Quiz aggregate + Attach endpoint is intended (story vs. impl divergence).
>
> Surface under test (all `[Authorize(AdminOnly)]`):
> - `QuestionsController` — `ByLesson/{id}`, `{id}` (GetById), `POST`, `PUT`, `DELETE/{id}`, `Reorder`, `{id}/SetActive`
> - Question types: MCQ, TrueFalse, Matching, FillInBlank. `correctAnswer` stored as jsonb.

Legend: **Covered** (file + method) / **GAP** (implement).

---

## Group A — CRUD + the 4 question types (covered)

| ID | Title | Type | Pri | Expected result | Covered / GAP |
|----|-------|------|-----|-----------------|---------------|
| BE-TC-01 | Add MCQ/TrueFalse/Matching/FillInBlank → ByLesson returns it (type+correctAnswer), isActive=true | functional | P0 | 200; round-trips | **Covered** — QuestionsAdmin AC-CRUD-1..4 + QuestionAuthoring BE-TC-A1..A4 |
| BE-TC-02 | Add each type → HTTP 200 NOT 500 (jsonb CorrectAnswer fix) | regression | P0 | 200 not 500 | **Covered** — QuestionAuthoring BE-TC-A1..A4 |
| BE-TC-03 | Edit each type → 200 not 500; ByLesson reflects new text/correctAnswer | regression/functional | P0 | as titled | **Covered** — QuestionAuthoring BE-TC-A5..A8 + QuestionsAdmin AC-CRUD-5 |
| BE-TC-04 | GetById returns AdminQuestionDto with correctAnswer | functional | P1 | correctAnswer present | **Covered** — QuestionsAdmin AC-CRUD-6 |
| BE-TC-05 | Soft-delete → absent from admin ByLesson + student StartAttempt | persistence | P0 | gone both | **Covered** — QuestionsAdmin AC-CRUD-7 / AC-CRUD-8 |
| BE-TC-06 | Authored question → student submits correct → isCorrect=true (each gradeable type) | functional/e2e | P0 | grades correct | **Covered** — QuestionAuthoring BE-TC-G1..G4 |
| BE-TC-07 | Edit round-trip: grade still correct after GET→resubmit-to-Edit (no double-encode) | regression | P0 | grade still correct | **Covered** — QuestionAuthoring BE-TC-E1..E3 |

---

## Group B — Per-type validation (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-08 | MCQ: 1 option→422; correctAnswer not in options→422; options not a JSON array→422 | P0 | **Covered** — QuestionsAdmin AC-VAL-MCQ-1..3 + QuestionAuthoring A9/A10 |
| BE-TC-09 | TrueFalse: correctAnswer='yes'→422; empty→422; 'false' lowercase accepted | P0 | **Covered** — QuestionsAdmin AC-VAL-TF-1/2 + "false lowercase accepted" + QuestionAuthoring A11 |
| BE-TC-10 | Matching: unequal left/right→422; missing 'left' key→422 | P0 | **Covered** — QuestionsAdmin AC-VAL-MATCH-1/2 + QuestionAuthoring A13 |
| BE-TC-11 | FillInBlank: empty correctAnswer→422 | P0 | **Covered** — QuestionsAdmin AC-VAL-FIB-1 + QuestionAuthoring A12 |
| BE-TC-12 | Base validators: empty questionText→422; invalid QuestionType=99→422; LessonId=0→422; Edit Id=0→422; QuestionText>4096→422; Options>16384→422 | P1 | **Covered** — QuestionsAdmin AC-VAL-BASE-1..4, AC-VAL-SIZE-1/2 |
| BE-TC-13 | Add to non-existent lesson → `Successed=false` (not 500); GetById non-existent → `Successed=false` | negative | P1 | **Covered** — QuestionsAdmin AC-LESSON-404 + "GetById non-existent" + QuestionAuthoring A14 |
| BE-TC-14 | Matching question authored → graded (round-trip) | functional | P2 | **GAP** — Matching is the one of the 4 types with **no** authored→student-grade round-trip (G1..G4 cover MCQ/TF×2/FIB; Matching add/edit is tested but not an end-to-end grade). Add a Matching grade round-trip. |

---

## Group C — Quiz/attach + language (story-vs-impl divergence) — GAP

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|----|-------|------|-----|-------|----------|---------------|
| BE-TC-15 | Attach question to a lesson in a different language tree than its skill → rejected (same-tree guard) | negative | P1 | Add question with `LessonId` in MATH/Ar and `SkillId` in MATH/En | `Successed=false` cross-language; no persist | **GAP** — the story's "attach must stay within the same `(SubjectCode,Language)` tree" is NOT tested; the question carries both `LessonId` and optional `SkillId`, so a cross-language LessonId↔SkillId pairing is the realizable form of this criterion. Confirm with lead whether the guard exists. |
| BE-TC-16 | Question DTO surfaces resolved (inherited) language read-only | functional | P2 | GetById a question under MATH/Ar | DTO exposes resolved language = Ar | **GAP** |
| BE-TC-17 | Add question with `SkillId` for a skill not under the lesson's subject tree → graceful (not 500) | negative/boundary | P2 | Add with mismatched SkillId | NOT 500; documented behavior | **GAP** |

> If the lead confirms a Quiz aggregate + `Attach` endpoint is **not** built, BE-TC-15/16/17 become the surrogate
> coverage for the story's quiz/attach/language criteria. If a Quiz aggregate **is** intended, raise as a missing
> feature, not just a missing test.

---

## Group D — Reorder + activate (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-18 | Reorder questions persists order; cross-lesson reorder→`Successed=false`; empty→422; id=0→422; non-existent ids→`Successed=false` | P1 | **Covered** — QuestionsAdmin AC-REORDER-1..5 |
| BE-TC-19 | Append semantics: each added question gets next SequenceOrder (0-based) | P2 | **Covered** — QuestionsAdmin "Append semantics" |
| BE-TC-20 | Deactivate question excludes from student StartAttempt; admin ByLesson shows IsActive=false; reactivate restores | P0 | **Covered** — QuestionsAdmin AC-ACTIVE-1..3 |
| BE-TC-21 | SetActive validator: Id=0 → 422 | P1 | **Covered** — QuestionsAdmin AC-ACTIVE-4 |

---

## Group E — Security + auth + envelope (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-22 | ★CRITICAL: student StartAttempt MUST NOT contain correctAnswer in any casing | auth/security | P0 | **Covered** — QuestionsAdmin AC-SEC-1/2 (CRITICAL) |
| BE-TC-23 | All Questions endpoints anonymous→401; non-admin (basic/parent)→403 | auth | P0 | **Covered** — QuestionsAdmin AC-AUTH-1/2 |
| BE-TC-24 | Add question response has full BaseResponse envelope keys | functional | P1 | **Covered** — QuestionsAdmin AC-ENV-1 |
| BE-TC-25 | Edit non-existent questionId → 404 not 500, no `ex.Message` leak | regression/negative | P0 | NOT 500; `Successed=false`; no leak | **GAP** — GetById/Add non-existent are covered; the **Edit** non-existent path is not (PR #183-style). |
| BE-TC-26 | Delete non-existent questionId → `Successed=false` (not 500), no leak | negative | P2 | NOT 500; `Successed=false` | **GAP** |
