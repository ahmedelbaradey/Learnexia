# Backend Test Cases — P2-02 (Browse subjects & lessons)

**Target agent:** `api-tester` · **Surface:** `SubjectsController` (+ one boundary case on `LessonsController`) in `Learnexia.Modules.Learning.Api`.
**Envelope:** every response is `BaseResponse<T>` serialised camelCase — success flag key is `successed` (C# `Successed`). Assert `statusCode`, `successed`, `data`.

## Shared preconditions / seed
- Target DB freshly migrated (P2-01) **and** seeded (P2-10): per grade, 4 MVP subjects (MATH, SCIENCE, ARABIC, ENGLISH) exist in **both** an Arabic (`Language=Ar`) and English (`Language=En`) tree, keyed by `(GradeId, SubjectCode, Language)`. Math grade-1 = 5 units × 3 lessons; concepts/skills per the seeder.
- **Auth fixtures (mint real JWTs via the login flow):**
  - `STUDENT_AR` — authenticated student whose token carries `learning_language=ar`.
  - `STUDENT_EN` — authenticated student whose token carries `learning_language=en`.
  - `STUDENT_NOCLAIM` — a legacy token with **no** `learning_language` claim (for `BE-TC-29`); if not mintable, mark that case Blocked with the reason.
- **Helper lookups** (resolve once, reuse): `mathG1ArId`, `mathG1EnId`, `scienceG1ArId`, a grade-2 subject id `subjG2Id`, and an `emptySubjectId` (a subject seeded with zero units, or create one via admin POST if none exists; if neither, mark `BE-TC-14` Blocked).
- Resolver facts (`SubjectLanguageResolver`): `ARABIC→Ar` (pinned), `ENGLISH→En` (pinned), `MATH→learnerLang`, `SCIENCE→learnerLang`.

---

## Group A — `GET /api/learning/Subjects/ForGrade?grade={n}` (GetSubjectsForGradeQuery)

### BE-TC-01 — Happy path: subjects for grade 1 (ar student)
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; grade 1 seeded.
- **Steps:** 1. `GET /api/learning/Subjects/ForGrade?grade=1` with `STUDENT_AR` bearer token.
- **Expected:** `200`; `successed=true`; `data` is a non-empty list; every item has `id`, `name`, `gradeNumber=1`, `subjectCode`. Envelope shape valid.
- **Traces to:** AC-S1, AC-B1.

### BE-TC-02 — Exactly 4 MVP subject codes returned (product override)
- **Type:** functional / regression · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; grade 1 fully seeded.
- **Steps:** 1. `GET …/ForGrade?grade=1`. 2. Collect the `subjectCode` values from `data`.
- **Expected:** Exactly **4** items; the set of `subjectCode` is `{MATH, SCIENCE, ARABIC, ENGLISH}` (enum ints `{0,1,2,3}`) — **no Social Studies** / no 5th code. No duplicate `subjectCode` (one subject per code despite two language trees existing).
- **Traces to:** AC-S1, product decision (4 subjects).

### BE-TC-03 — Grade-6 boundary still returns the 4 codes
- **Type:** boundary · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; grade 6 seeded.
- **Steps:** 1. `GET …/ForGrade?grade=6`.
- **Expected:** `200`, `successed=true`; items all have `gradeNumber=6`; subject-code set ⊆ `{MATH,SCIENCE,ARABIC,ENGLISH}` (4 if fully seeded). None carry a different grade number.
- **Traces to:** AC-S1, AC-S3 (grade boundary).

### BE-TC-04 — No cross-grade leakage (grade-2 subject absent from grade-1 result)
- **Type:** auth-authz / functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; grades 1 and 2 seeded; `subjG2Id` resolved.
- **Steps:** 1. `GET …/ForGrade?grade=1`. 2. Assert `data` contains no item whose `id == subjG2Id`. 3. Assert every returned `gradeNumber == 1`.
- **Expected:** Grade-2 subject IDs are absent; all items are grade 1.
- **Traces to:** AC-S3 (grade filtering, no leak), R2.

### BE-TC-05 — `gradeNumber` reflects the requested grade, not GradeId surrogate
- **Type:** regression · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; grade where `Grade.Id != Grade.Number` (verify in seed — surrogate keys typically differ).
- **Steps:** 1. `GET …/ForGrade?grade=3`. 2. Assert all items `gradeNumber=3`.
- **Expected:** `gradeNumber=3` for every item (handler maps `Grade.Number==3 → Grade.Id` before filtering; never echoes `GradeId` as the number).
- **Traces to:** AC-S3, R2 (Grade.Number vs GradeId, plan Q4).

### BE-TC-06 — Learning-language filter: MATH/SCIENCE follow the learner's language
- **Type:** language-filter · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Both `STUDENT_AR` and `STUDENT_EN`; grade 1 seeded with ar+en trees.
- **Steps:** 1. `GET …/ForGrade?grade=1` as `STUDENT_AR`, capture the MATH item's `id` (= an `Ar`-tree subject) and `name`. 2. Repeat as `STUDENT_EN`, capture the MATH item's `id`/`name`.
- **Expected:** The MATH (and SCIENCE) subject `id`/`name` **differ** between the two students (ar student gets the Ar-tree MATH subject; en student gets the En-tree MATH subject). The `subjectCode` is the same (`MATH`) for both. ARABIC item is identical (Ar-pinned) for both; ENGLISH item is identical (En-pinned) for both.
- **Traces to:** Learning-language filter (P8-03).

### BE-TC-07a — Invalid grade (0) → 400
- **Type:** validation / negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`.
- **Steps:** 1. `GET …/ForGrade?grade=0`.
- **Expected:** `400`; `successed=false`; message = the `GradeOutOfRange` localized text; `data` null/default.
- **Traces to:** plan Batch-2 (out-of-range grade → 400).

### BE-TC-07b — Invalid grade (7) → 400
- **Type:** boundary / negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`.
- **Steps:** 1. `GET …/ForGrade?grade=7`.
- **Expected:** `400`; `successed=false` (grade > 6 rejected before any DB hit).
- **Traces to:** plan Batch-2.

### BE-TC-08 — In-range but unseeded grade number (e.g. valid 1–6 with no subjects) → 200 empty; unknown grade → 404
- **Type:** boundary / negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`. Note: handler returns **404** only when no `Grade` row has that `Number`; an existing grade with no subjects returns **200 + empty**.
- **Steps:** 1. If a grade row exists with `Number=N` but zero subjects → `GET …/ForGrade?grade=N`, expect `200` + `successed=true` + empty `data` (EmptyCollection path). 2. Pick a `Number` in 1–6 that has **no `Grade` row** (if seed leaves a gap) → expect `404` `GradeNotFound`. If no gap exists, mark step 2 Blocked (no unseeded in-range grade available).
- **Expected:** Existing-grade-no-subjects → `200` empty (not 404); missing-grade-row → `404`.
- **Traces to:** AC-S4 (empty state ≠ error), AC-B4.

### BE-TC-09 — Anonymous request → 401
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** No bearer token.
- **Steps:** 1. `GET …/ForGrade?grade=1` with **no** Authorization header.
- **Expected:** `401 Unauthorized` (controller `[Authorize]`). No subject data leaked.
- **Traces to:** AC-B7, P8-SEC-1.

---

## Group B — `GET /api/learning/Subjects/{id}/Lessons` (GetSubjectLessonsQuery)

### BE-TC-12 — Happy path: units returned in `SequenceOrder`
- **Type:** functional / ordering · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `mathG1ArId` (5 units × 3 lessons).
- **Steps:** 1. `GET /api/learning/Subjects/{mathG1ArId}/Lessons`.
- **Expected:** `200`, `successed=true`; `data` has 5 units; `data[*].sequenceOrder` is strictly ascending (non-decreasing) in array order. Each unit has `unitId`, `name`, `sequenceOrder`, `lessons[]`.
- **Traces to:** AC-S2, AC-B2.

### BE-TC-13 — Lessons within each unit ordered by `SequenceOrder`; node carries `State`
- **Type:** functional / ordering / state · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `mathG1ArId`.
- **Steps:** 1. `GET …/{mathG1ArId}/Lessons`. 2. For each unit, read `lessons[*].sequenceOrder`.
- **Expected:** Within every unit, `lessons[*].sequenceOrder` is ascending; each lesson has `lessonId`, `name`, `difficulty`, `sequenceOrder`, `skillId` (nullable ok), `isBoss` (bool), and `state` ∈ `{Locked(0), Available(1), Completed(2)}`. Math grade-1 = 3 lessons per unit.
- **Traces to:** AC-S2, AC-B5 (state on lesson node).

### BE-TC-14 — Subject with no units → 200 + empty collection (not 404)
- **Type:** boundary / functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `emptySubjectId` (a subject with zero units). If unavailable, Blocked.
- **Steps:** 1. `GET …/{emptySubjectId}/Lessons`.
- **Expected:** `200`; `successed=true`; `data` is an empty array. **Not** 404, not an error.
- **Traces to:** AC-S4, AC-B4 (empty state).

### BE-TC-15 — Non-existent subject id → 404
- **Type:** negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`.
- **Steps:** 1. `GET /api/learning/Subjects/99999/Lessons`.
- **Expected:** `404`; `successed=false`; `SubjectNotFound` message.
- **Traces to:** AC-B4 (missing resource → 404).

### BE-TC-16 — `State` is engine-derived for an authenticated student (P2-04), not the static placeholder
- **Type:** functional / regression · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR` with **no progress** in the subject; `mathG1ArId`.
- **Steps:** 1. `GET …/{mathG1ArId}/Lessons`. 2. Inspect `state` across all lessons.
- **Expected:** Every lesson `state` is a valid `NodeState`; for a fresh student the first/entry lessons are `Available` while gated lessons may be `Locked` per prerequisite edges (engine output, not all-`Available`). Locked lessons carry a non-empty `missingPrerequisites` array; Available/Completed carry empty. (Assert validity + that `state` is present, not a pinned distribution.)
- **Traces to:** AC-B5, R4.

### BE-TC-17 — Anonymous request → 401
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** No token.
- **Steps:** 1. `GET …/{mathG1ArId}/Lessons` with no Authorization header.
- **Expected:** `401`. (P2-04 tightened this from anonymous — breaking change noted in the controller.)
- **Traces to:** AC-B7.

---

## Group C — `GET /api/learning/Subjects/{id}/SkillTree` (GetSubjectSkillTreeQuery)

### BE-TC-18 — Happy path: concepts → skill nodes, each node carries `State`
- **Type:** functional / state · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `mathG1ArId`.
- **Steps:** 1. `GET /api/learning/Subjects/{mathG1ArId}/SkillTree`.
- **Expected:** `200`, `successed=true`; `data` is a list of concept nodes, each with `conceptId`, `name`, `skills[]`. Each skill has `skillId`, `name`, `masteryThreshold`, `estimatedTimeMinutes`, `lessonIds[]`, and `state` ∈ `{Locked,Available,Completed}`.
- **Traces to:** AC-B5.

### BE-TC-19 — Seeded counts: concepts and skills present
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `mathG1ArId` fully seeded.
- **Steps:** 1. `GET …/{mathG1ArId}/SkillTree`. 2. Count concepts and total skills.
- **Expected:** concepts ≥ 1 (per seeder, Math grade tree ~5 concepts); every concept has ≥ 1 skill; total skill count > 0. (Pin exact counts only if the seed is stable in the target env; otherwise assert ≥ thresholds and record actuals.)
- **Traces to:** AC-B5, R3.

### BE-TC-20 — Concepts ordered by Id; skills ordered by Id (surrogate order)
- **Type:** ordering · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `mathG1ArId`.
- **Steps:** 1. `GET …/{mathG1ArId}/SkillTree`. 2. Read `conceptId` array order and each concept's `skills[*].skillId` order.
- **Expected:** `conceptId` ascending across the list; `skillId` ascending within each concept (no `SequenceOrder` column yet — surrogate order, deferred to P2-11).
- **Traces to:** AC-B5 (stable ordering), plan schema-gap note.

### BE-TC-21 — Learning-language filter on SkillTree (ar vs en)
- **Type:** language-filter · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`, `STUDENT_EN`; `mathG1ArId` (Ar-tree) and `mathG1EnId` (En-tree) resolved.
- **Steps:** 1. `STUDENT_EN` requests `GET …/{mathG1ArId}/SkillTree` (the **Ar**-tree id). 2. `STUDENT_AR` requests `GET …/{mathG1EnId}/SkillTree` (the **En**-tree id). 3. Compare concept names to the correct-language tree.
- **Expected:** **No 403.** Each student is **redirected** to the correct-language tree for the same `SubjectCode`+`Grade`: `STUDENT_EN` gets the En-tree concepts/skills even though they passed the Ar-tree id; `STUDENT_AR` gets the Ar-tree. (If the resolved tree is genuinely absent in seed, the handler serves the requested tree + logs a warning — note in report.) **Documents the silent-redirect contract; see README Q1.**
- **Traces to:** Cross-language access (P8-03), R1.

### BE-TC-22 — Non-existent subject id → 404
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`.
- **Steps:** 1. `GET /api/learning/Subjects/99999/SkillTree`.
- **Expected:** `404`; `successed=false`; `SubjectNotFound`.
- **Traces to:** AC-B4.

### BE-TC-23 — Subject with no concepts → 200 + empty
- **Type:** boundary · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; a subject with zero concepts (the `emptySubjectId` if it also has no concepts). If none, Blocked.
- **Steps:** 1. `GET …/{emptyConceptSubjectId}/SkillTree`.
- **Expected:** `200`, `successed=true`, empty `data` array — not 404/500.
- **Traces to:** AC-S4 (empty state).

### BE-TC-24 — Anonymous request → 401
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** No token.
- **Steps:** 1. `GET …/{mathG1ArId}/SkillTree` with no Authorization header.
- **Expected:** `401`.
- **Traces to:** AC-B7.

---

## Group D — Cross-cutting envelope, language-default, grade-scope, and lesson-403 boundary

### BE-TC-25 — Admin-write actions are not reachable by a student (regression guard)
- **Type:** auth-authz · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR` (Student, not Admin).
- **Steps:** 1. `POST /api/learning/Subjects/Create` with `STUDENT_AR` token and a minimal body.
- **Expected:** `403` (or `401` if policy rejects pre-auth) — `AdminOnly` policy blocks a student. Confirms the new read endpoints did not loosen the admin CRUD guard.
- **Traces to:** module auth integrity (no-teacher / role separation).

### BE-TC-26 — Envelope shape consistent across all three success responses
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; `mathG1ArId`.
- **Steps:** 1. Call all three endpoints (`ForGrade?grade=1`, `{mathG1ArId}/Lessons`, `{mathG1ArId}/SkillTree`).
- **Expected:** Each response body is `{ "successed": true, "statusCode": 200, "data": [...], "message": ... }` — key spelled `successed` (camelCase of `Successed`), `statusCode` 200, `data` an array. No raw entity leakage, no exception text.
- **Traces to:** AC-B6.

### BE-TC-27 — Cross-language LESSON access via direct id → 403 (where 403 actually lives)
- **Type:** auth-authz / language-filter / boundary · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** `STUDENT_EN`; an **Arabic-tree** lesson id `arLessonId` (a lesson under an Ar `MATH`/`SCIENCE` subject — i.e. resolves wrong-language for an en student). Resolve via `GET …/{mathG1ArId}/Lessons` as `STUDENT_AR`.
- **Steps:** 1. `GET /api/learning/Lessons/{arLessonId}` as `STUDENT_EN`.
- **Expected:** `403 Forbidden`; `successed=false`; `LessonLanguageMismatch` message. (`GetLessonQueryHandler` walks Lesson→Unit→Subject and 403s on language mismatch.) **This is the endpoint where the task's "cross-language → 403" requirement is genuinely enforced.**
- **Traces to:** Cross-language access (P8-03), R1.

### BE-TC-28 — Same-language LESSON access succeeds (positive control for the 403 guard)
- **Type:** functional / language-filter · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR`; the same `arLessonId` from BE-TC-27.
- **Steps:** 1. `GET /api/learning/Lessons/{arLessonId}` as `STUDENT_AR`.
- **Expected:** `200`, `successed=true`, lesson returned (Ar tree matches the ar learner). Confirms the 403 in BE-TC-27 is language-specific, not a blanket block. (`CorrectAnswer` must NOT appear in any `quickCheck`.)
- **Traces to:** Learning-language filter (P8-03).

### BE-TC-29 — Legacy token with no `learning_language` claim → defaults to Arabic content, no error
- **Type:** boundary / negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `STUDENT_NOCLAIM` (token without the claim). If not mintable, mark Blocked with reason.
- **Steps:** 1. `GET /api/learning/Subjects/ForGrade?grade=1` as `STUDENT_NOCLAIM`.
- **Expected:** `200`, `successed=true`; MATH/SCIENCE resolve to the **Ar** tree (Arabic-first fallback per `LearningLanguageClaimAccessor`); no 400/500. (Server logs a warning — not observable via API.)
- **Traces to:** README Q2 (claim-absent fallback).

### BE-TC-30 — Grade-scope NOT server-enforced today (documented known gap, expected-today)
- **Type:** auth-authz / negative · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** `STUDENT_AR` (whatever the student's own grade is); grade 3 seeded.
- **Steps:** 1. `GET /api/learning/Subjects/ForGrade?grade=3` as `STUDENT_AR`.
- **Expected:** `200` with grade-3 subjects — i.e. the student **can** query a grade other than their own (`?grade=` is trusted; server-side enforcement deferred to P6-06). **This case documents the current accepted behavior (no 403).** If/when P6-06 enforces grade, this expectation flips to 403 and the case must be updated. Record the observed behavior; do NOT mark it a defect under P2-02.
- **Traces to:** README Q3 (deferred grade enforcement).

---

## Notes for the implementer
- Assert on `state` (engine `NodeState`), **not** the `[Obsolete] isLocked` field (R5).
- For language cases, the discriminator is the subject/concept/lesson **content** (id/name) belonging to the Ar vs En tree — not a flag on the response.
- Where a precondition cannot be satisfied in the target env (no empty subject, no claim-less token, no unseeded in-range grade), mark the case **Blocked** with the specific missing seam in `execution-report.md` — do not silently pass or drop it.
- Record actual seeded counts in the report so R3 seed-ordering flakiness is distinguishable from a real regression.
