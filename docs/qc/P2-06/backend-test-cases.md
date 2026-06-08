# Backend Test Cases — P2-06 (Take a quiz) — for `api-tester`

**Target agent:** `api-tester` (integration tests against the running API).
**Surface:** `POST /api/Learning/Quizzes/{lessonId}/Attempt` (start-quiz) — primary. Other Quizzes actions are regression/context only.
**Envelope:** `BaseResponse<T>` — success flag spelled **`Successed`**; `StatusCode`, `Message`, `Data`. Status mapping: `Unauthorized→401`, `NotFound→404`, `BusinessValidation→424`, `UnprocessableEntity→422`, `Forbidden→403`, `ValidationBehavior failure→422`.

## Seed conventions (referenced by cases)

- **`student-A`** — Identity user with role `Student`, valid JWT, `learning_language=ar` (default). Resolves to `ICurrentUserService.UserId`.
- **`student-B`** — second Student (cross-child isolation).
- **`parent-P`** — Identity user with role `Parent`, valid JWT.
- **`admin-X`** — Identity user with an admin/non-Student role, valid JWT.
- **`lesson-AR`** — a Lesson under a Unit under a Subject whose `Language` matches `student-A`'s resolved Arabic tree.
- **`lesson-EN`** — a Lesson whose owning Subject `Language` does NOT match `student-A`'s resolved language (wrong-language tree) — for the cross-language guard.
- **`lesson-EMPTY`** — a valid Lesson (correct language) with **zero** `QuizQuestion` rows.
- **`questions-AllTypes`** — 4 `QuizQuestion` rows on `lesson-AR`, one per `QuestionType`:
  - MCQ: `Options=["A","B","C"]`, `CorrectAnswer="B"`.
  - TrueFalse: `Options=["True","False"]`, `CorrectAnswer="true"`.
  - Matching: `Options={"left":["1","2"],"right":["x","y"]}`, `CorrectAnswer={...}`.
  - FillInBlank: `Options=""` (or null per schema), `CorrectAnswer="cairo"`.
- Quiz questions have no admin create-endpoint in this slice → seed directly into `learning."QuizQuestions"` (record the method in the execution report).

---

## Group A — Happy path & envelope

### BE-TC-01 — Start attempt (Student, valid lesson) returns attemptId + questions
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `student-A` JWT; `lesson-AR` seeded with `questions-AllTypes`.
- **Steps:**
  1. `POST /api/Learning/Quizzes/{lesson-AR.Id}/Attempt` with `student-A` bearer token, empty body.
- **Expected:** HTTP **200**; `Successed=true`; `Data.AttemptId` is a positive int; `Data.Questions` is an array of length 4.
- **Traces to:** AC-S2, AC-S3, brief AC-4.

### BE-TC-02 — Response uses BaseResponse envelope with correct `Successed` spelling
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** same as BE-TC-01.
- **Steps:** Start the attempt; inspect the raw JSON body.
- **Expected:** Body has top-level `Successed` (exact spelling), `StatusCode`, `Message`, `Data`. `Successed=true`, `StatusCode` reflects 200. `Message` is the localized `AttemptStartedSuccessfully` text (not a raw resource key).
- **Traces to:** brief AC-4 / CONVENTIONS envelope rule.

### BE-TC-03 — Student role is accepted (200)
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `student-A` JWT (role `Student`); `lesson-AR`.
- **Steps:** Start the attempt.
- **Expected:** HTTP **200**, `Successed=true`. Confirms `[Authorize(Roles="Student")]` admits the Student role.
- **Traces to:** role gate.

### BE-TC-04 — Same-language lesson passes the language guard (200)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** `student-A` (`learning_language=ar`); `lesson-AR` whose Subject `Language` matches the resolved Arabic tree.
- **Steps:** Start the attempt.
- **Expected:** HTTP **200**, `Successed=true` (no 403). Confirms the Lesson→Unit→Subject walk passes when languages match.
- **Traces to:** cross-language guard (positive).

---

## Group B — Question payload shape (4 types) & answer-leak control

### BE-TC-05 — All four question types are returned with `QuestionType` discriminator
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `lesson-AR` + `questions-AllTypes`.
- **Steps:** Start the attempt; read `Data.Questions`.
- **Expected:** 4 questions; the set of `questionType` values == {1 (MCQ), 2 (TrueFalse), 3 (Matching), 4 (FillInBlank)}. Each has `id`, `questionText`, `options`.
- **Traces to:** AC-S1.

### BE-TC-06 — Per-type `Options` shape is preserved in the payload
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** same as BE-TC-05.
- **Steps:** Inspect each question's `options` string.
- **Expected:** MCQ `options` deserializes to a JSON array of ≥2 strings; TrueFalse `options` is `["True","False"]`; Matching `options` deserializes to an object with `left` + `right` arrays of equal length; FillInBlank `options` is empty/absent per seed. (Options are passed through verbatim as the serialized jsonb string.)
- **Traces to:** AC-S1, FE contract.

### BE-TC-07 — CorrectAnswer is NEVER present in the start payload (any type)
- **Type:** negative (security) · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `questions-AllTypes` each with a non-empty `CorrectAnswer` seeded.
- **Steps:** Start the attempt; serialize the full response body to a string.
- **Expected:** No `correctAnswer` key anywhere in `Data.Questions` (case-insensitive search). The seeded correct-answer values (e.g. `"B"`, `"cairo"`) do not appear in the response. Each question object has exactly `id`, `questionType`, `questionText`, `options` — no fifth field.
- **Traces to:** brief AC (answer withheld) / QuizProfile security control.

---

## Group C — Auth & role boundary (negative)

### BE-TC-08 — StudentId is taken from JWT, not the request
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `student-A` JWT; `lesson-AR`.
- **Steps:** Start the attempt with `student-A`. Attempt to inject a `studentId` via body/query (e.g. `?studentId=<student-B.Id>` and a JSON body `{"studentId": <student-B.Id>}`).
- **Expected:** HTTP 200; the created Attempt's `StudentId` == `student-A`'s user id (verify in DB per BE-TC-13). The injected `studentId` is ignored (command carries only `LessonId`).
- **Traces to:** StudentId-from-JWT rule.

### BE-TC-09 — Parent role is rejected with 403
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `parent-P` JWT (role `Parent`); `lesson-AR`.
- **Steps:** `POST .../{lesson-AR.Id}/Attempt` with `parent-P` token.
- **Expected:** HTTP **403** (authenticated but wrong role — NOT 401). No Attempt row created.
- **Traces to:** role gate (Student-only).

### BE-TC-10 — Admin / non-Student role is rejected with 403
- **Type:** auth-authz · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** `admin-X` JWT (non-Student role); `lesson-AR`.
- **Steps:** Start the attempt with `admin-X` token.
- **Expected:** HTTP **403**. No Attempt row created.
- **Traces to:** role gate.

### BE-TC-11 — Anonymous request is rejected with 401
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none; `lesson-AR`.
- **Steps:** `POST .../{lesson-AR.Id}/Attempt` with **no** Authorization header.
- **Expected:** HTTP **401** (challenge), not 403, not 200. No Attempt row.
- **Traces to:** role gate / auth.

### BE-TC-12 — Cross-language lesson is rejected with 403
- **Type:** auth-authz (P8 guard) · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** `student-A` (`learning_language=ar`); `lesson-EN` whose owning Subject `Language` ≠ student's resolved language.
- **Steps:** `POST .../{lesson-EN.Id}/Attempt` with `student-A` token.
- **Expected:** HTTP **403**; `Successed=false`; `Message` == localized `LessonLanguageMismatch`. No Attempt row created.
- **Traces to:** cross-language guard.

---

## Group D — Persistence & resume

### BE-TC-13 — Attempt row is persisted with correct fields
- **Type:** persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `student-A`; `lesson-AR`; no prior InProgress attempt for this pair.
- **Steps:** Start the attempt; capture `Data.AttemptId`; query `learning."Attempts" WHERE "Id" = <AttemptId>`.
- **Expected:** Row exists. `Status` == 1 (InProgress). `StartedAt` populated (UTC, recent). `StudentId` == `student-A`'s user id. `LessonId` == `lesson-AR.Id`. `CompletedAt` is null. `AccuracyPercentage`/`DurationSeconds`/`HintsUsedCount` default to 0.
- **Traces to:** AC-S2, AC-S4.

### BE-TC-14 — Quiz entities exist with full forward-compatible schema
- **Type:** persistence · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** migrations applied.
- **Steps:** Inspect `learning` schema: `QuizQuestions`, `Attempts`, `StudentAnswers` tables.
- **Expected:** All three tables exist in schema `learning`. `Attempts` has `AccuracyPercentage`, `DurationSeconds`, `HintsUsedCount`, `CompletedAt` (nullable), `Status`, `StartedAt`, `StudentId`, `LessonId`. `StudentAnswers` has `AttemptId`, `QuestionId`, `IsCorrect`, `TimeSpentSeconds`, `HintUsed`. Enum columns (`QuestionType`, `Difficulty`, `GeneratedBy`, `Status`) are integer.
- **Traces to:** AC-S4, brief AC-5/AC-6.

### BE-TC-15 — Re-starting the same lesson resumes (no duplicate Attempt)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `student-A`; `lesson-AR`; one InProgress attempt already created by a prior start (BE-TC-01).
- **Steps:**
  1. Start the attempt again with `student-A`.
  2. Query `SELECT count(*) FROM learning."Attempts" WHERE "StudentId"=<A> AND "LessonId"=<lesson-AR.Id> AND "Status"=1`.
- **Expected:** HTTP 200; `Data.AttemptId` == the original AttemptId; `Message` == localized `AttemptResumedSuccessfully`; count == **1** (no second InProgress row). `Data.Questions` still returned (4).
- **Traces to:** AC-S2 (Attempt lifecycle).

---

## Group E — Invalid input / not-found / validation

### BE-TC-16 — Non-existent lesson returns 404
- **Type:** negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** `student-A`; a `lessonId` (e.g. 999999) that has no Lesson row.
- **Steps:** `POST .../999999/Attempt` with `student-A`.
- **Expected:** HTTP **404**; `Successed=false`; `Message` == localized `LessonNotFound`. No Attempt row.
- **Traces to:** invalid lesson handling.

### BE-TC-17 — lessonId = 0 fails validation (422)
- **Type:** validation · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** `student-A`.
- **Steps:** `POST .../0/Attempt` with `student-A`.
- **Expected:** HTTP **422**; `Successed=false`; validation message == localized `LessonIdMustBePositive` (StartAttemptValidation: `LessonId > 0`).
- **Traces to:** validation (command-only).

### BE-TC-18 — Negative lessonId fails validation (422)
- **Type:** boundary · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** `student-A`.
- **Steps:** `POST .../-5/Attempt` with `student-A`.
- **Expected:** HTTP **422**; validation message `LessonIdMustBePositive`. (If routing rejects the negative segment with 404 instead, record that as the observed behavior.)
- **Traces to:** validation / boundary.

### BE-TC-19 — Non-numeric lessonId is rejected by routing (404)
- **Type:** negative · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** `student-A`.
- **Steps:** `POST /api/Learning/Quizzes/abc/Attempt` with `student-A`.
- **Expected:** HTTP **404** (route constraint — `{lessonId:int}` mismatch / no matching route). No 500.
- **Traces to:** robustness.

### BE-TC-20 — Valid lesson with zero questions returns 200 + empty list
- **Type:** boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** `student-A`; `lesson-EMPTY` (correct language, no QuizQuestion rows).
- **Steps:** Start the attempt on `lesson-EMPTY`.
- **Expected:** HTTP **200**; `Successed=true`; `Data.AttemptId` positive; `Data.Questions` is an empty array (not null, no error). An Attempt row IS still created.
- **Traces to:** edge / empty state.

---

## Group F — Product overrides (negative) & regression

### BE-TC-21 — No teacher role can start a quiz
- **Type:** negative (product override) · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Confirm the Identity role set contains **no** `Teacher` role (product decision: no teacher role).
- **Steps:** Attempt to obtain/forge a JWT with role `Teacher`; call the endpoint. If no Teacher role can be issued by Identity, assert that fact from the role registry.
- **Expected:** No `Teacher` role exists to authenticate with; if a token with role `Teacher` is presented it is rejected with **403** (not in `Roles="Student"`). Documents the no-teacher-role product rule at the API boundary.
- **Traces to:** product override (no teacher role).

### BE-TC-22 — Quiz scope is the 4 supported subjects (context assertion)
- **Type:** functional (product override) · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** Lessons seeded only under Math/Science/Arabic/English subjects (no Social Studies).
- **Steps:** Confirm seed Subjects are within {Math, Science, Arabic, English}; start an attempt under one of them.
- **Expected:** 200; no Social Studies subject exists in the seed/content tree. Documents the 4-subject scope (no functional Social Studies path).
- **Traces to:** product override (4 subjects, no Social Studies).

### BE-TC-23 — [PROBE / likely GAP] Locked skill/lesson is NOT rejected by start-quiz
- **Type:** negative (gap probe) · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** `student-A`; a `lesson-LOCKED` belonging to a skill that is locked for the student per P2-04 lock semantics.
- **Steps:** `POST .../{lesson-LOCKED.Id}/Attempt` with `student-A`.
- **Expected (CURRENT code):** HTTP **200**, attempt created — the handler performs **no lock check** (verified in `StartAttemptCommandHandler`). **Expected (if lead rules lock must be enforced):** 403/424 rejection.
- **Note:** Do **not** mark this fail/pass until lead resolves Open Q1. Run it to document current behavior; raise a defect only if the lead confirms lock enforcement belongs here.
- **Traces to:** dispatch hint (locked skill, P2-04) — coverage gap flagged in README §4.

### BE-TC-24 — [BLOCKED] Concurrent starts on same lesson do not create two InProgress attempts
- **Type:** concurrency · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** `student-A`; `lesson-AR`; no prior attempt; design ruling on Open Q2.
- **Steps:** Fire two near-simultaneous `POST .../{lesson-AR.Id}/Attempt` requests with `student-A`.
- **Expected (target contract):** Exactly **one** InProgress Attempt row exists afterward; both responses return the same `AttemptId`.
- **Note:** **BLOCKED** — there is no DB unique constraint on `(StudentId, LessonId, Status=InProgress)`; the resume guard is read-then-write and may race. Do not implement until the lead confirms the intended concurrency contract (Open Q2). May currently produce two rows.
- **Traces to:** resume integrity / Open Q2.

---

## Regression smoke (context — not P2-06 acceptance; run to confirm no break)

- **REG-1:** `POST /api/Learning/Quizzes/{attemptId}/Answers` (SubmitAnswer) — Student, own InProgress attempt, valid MCQ answer → 200, `IsCorrect` computed server-side; `CorrectAnswer` returned **only when wrong**. (P2-07 surface.)
- **REG-2:** `POST /api/Learning/Quizzes/{attemptId}/Complete` and `/Abandon` — Student, own attempt → 200 `AttemptSummaryDto`; idempotent on repeat. (P2-08 surface.)
- **REG-3:** SubmitAnswer to **another student's** attempt (`student-B` token, `student-A`'s attemptId) → 401/Unauthorized (ownership guard). Confirms cross-child isolation on the answer path.
- **REG-4:** Existing Learning skills endpoint (`GET /api/learning/skills` or equivalent) still 200 — quiz additions didn't regress the module.
