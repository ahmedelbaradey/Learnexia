# Backend Test Cases — P2-07 Instant Answer Feedback

> Target agent: **`api-tester`**. Implement as integration tests against the running API in `backend/tests/Learnexia.IntegrationTests/`. Reuse the helpers in `P2_08_RecordGranularAnswers_Tests.cs` (`CreateStudentViaParentFlowAsync`, `SeedLessonAsync`, `SeedQuestionsAsync` — extend it to accept `QuestionType`/`CorrectAnswer` per case —, `SeedSkillAsync`, `StartAttemptViaApiAsync`, `SubmitAnswerAsync`, `CompleteAttemptAsync`, `SendAsync`, `TryProp`).
>
> **Grounded contract facts (verified in source):**
> - Endpoints: `POST /api/Learning/Quizzes/{attemptId}/Answers` and `POST /api/Learning/Quizzes/{attemptId}/Complete`, both `[Authorize(Roles="Student")]`.
> - Envelope: `BaseResponse<T>` — success flag spelled **`successed`** (case-insensitive via `TryProp`); body has `data`, `message`, `statusCode`.
> - Status mapping (from `BaseResponseHandler`): `Unauthorized`→**401**, `NotFound`→**404**, `BusinessValidation`→**424** (FailedDependency), `UnprocessableEntity`/`ValidationBehavior`→**422**, `Success`→**200**, `ServerError`→**500**.
> - `SubmitAnswerResponse` = `{ isCorrect, correctAnswer?, hintAvailable }`. `correctAnswer` is `null` when correct, populated when wrong. **No `Explanation` field** (intentional — Phase 3).
> - Grading is server-side via `AnswerComparator.AreEqual(QuestionType, payload, correctAnswer)`. MCQ/FillInBlank/Matching = trim + `OrdinalIgnoreCase`; TrueFalse = `bool.TryParse` both sides; null/empty/whitespace → `false`.
> - `QuestionType` enum: MCQ=1, TrueFalse=2, Matching=3, FillInBlank=4.
> - `CorrectAnswer` is stored as a JSON string (seed values are JSON-quoted, e.g. `"\"4\""`); payloads in tests mirror that storage shape.

**Common precondition (all cases unless stated):** a Student account created via the parent-driven flow (`CreateStudentViaParentFlowAsync`) and its JWT; a seeded Grade→Subject→Unit→Lesson hierarchy (`SeedLessonAsync`); seeded `QuizQuestion`(s); an InProgress attempt started via `StartAttemptViaApiAsync`. `SkillId` is null unless a case says otherwise.

---

## Group A — Core feedback contract (AC-1, AC-2, AC-3, AC-4)

### BE-TC-01 — Correct answer → isCorrect=true, correctAnswer null, no leak
- **Type:** functional | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-1; security invariant "answer revealed only when wrong"
- **Preconditions/seed:** MCQ question `CorrectAnswer="\"4\""`, options `["1","2","3","4"]`, SkillId null. InProgress attempt.
- **Steps:**
  1. `POST /Answers` with `{ QuestionId, AnswerPayload:"\"4\"", TimeSpentSeconds:10, HintUsed:false }` + Student JWT.
- **Expected:** 200. `successed=true`. `data.isCorrect=true`. `data.correctAnswer` is **absent or JSON null** (never the answer value). `data.hintAvailable=false`. Raw body does **not** contain the string `4` inside a `correctAnswer` field.

### BE-TC-02 — Wrong answer → isCorrect=false, correctAnswer populated, hint affordance present
- **Type:** functional | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-2
- **Preconditions/seed:** MCQ question `CorrectAnswer="\"Paris\""`, options include `"Berlin"`. InProgress attempt.
- **Steps:**
  1. `POST /Answers` with `AnswerPayload:"\"Berlin\""`.
- **Expected:** 200. `data.isCorrect=false`. `data.correctAnswer` present, non-null, equals the stored correct value (`"Paris"`). `data.hintAvailable=false` (inert hint affordance present in contract).

### BE-TC-03 — Same-screen feedback: response is synchronous on the POST (AC-3)
- **Type:** functional | **Priority:** P1 | **Target:** api-tester
- **Traces to:** AC-3
- **Preconditions/seed:** any MCQ question, InProgress attempt.
- **Steps:**
  1. `POST /Answers` and observe a single synchronous response.
- **Expected:** 200 returned directly from the same POST (no polling/second call needed). Envelope contains `data.isCorrect` — feedback is in the immediate response body. (No 202/redirect/async pattern.)

### BE-TC-04 — Answer result persisted (AC-4 persistence)
- **Type:** persistence | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-4
- **Preconditions/seed:** MCQ question correct value `"A"`. InProgress attempt.
- **Steps:**
  1. Submit a wrong answer (`"B"`) with `TimeSpentSeconds:15`, `HintUsed:true`.
  2. Query `LearningDbContext.StudentAnswers` for `(AttemptId, QuestionId)`.
- **Expected:** Row exists with `IsCorrect=false`, `TimeSpentSeconds=15`, `HintUsed=true`, `QuestionId`/`AttemptId` matching. Confirms server-computed `IsCorrect` is persisted (not the client's claim).

---

## Group B — Per-QuestionType grading correctness (server-side oracle)

> Each type gets green + red. TrueFalse and FillInBlank also get a malformed/edge case. This is the "grading is correct for all 4 question types" security requirement.

### BE-TC-05 — MCQ correct (case-insensitive, trimmed)
- **Type:** state/grading | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-1; grading-per-type
- **Seed:** MCQ, `CorrectAnswer="\"A\""`.
- **Steps:** Submit `AnswerPayload:"\"a\""` (lowercase).
- **Expected:** 200, `isCorrect=true` (OrdinalIgnoreCase). `correctAnswer` null.

### BE-TC-06 — MCQ wrong
- **Type:** grading | **Priority:** P0 | **Target:** api-tester
- **Seed:** MCQ, `CorrectAnswer="\"A\""`.
- **Steps:** Submit `"\"B\""`.
- **Expected:** 200, `isCorrect=false`, `correctAnswer="\"A\""` (or the stored form). DB row `IsCorrect=false`.

### BE-TC-07 — TrueFalse correct (`"true"` vs stored `"True"`)
- **Type:** grading | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-1; grading-per-type
- **Seed:** `QuestionType=TrueFalse`, `CorrectAnswer="True"`.
- **Steps:** Submit `AnswerPayload:"true"`.
- **Expected:** 200, `isCorrect=true` (`bool.TryParse` both, equal).

### BE-TC-08 — TrueFalse wrong (`"false"` vs `"True"`)
- **Type:** grading | **Priority:** P0 | **Target:** api-tester
- **Seed:** `QuestionType=TrueFalse`, `CorrectAnswer="True"`.
- **Steps:** Submit `"false"`.
- **Expected:** 200, `isCorrect=false`, `correctAnswer="True"`.

### BE-TC-09 — TrueFalse malformed payload grades wrong, does not throw
- **Type:** boundary/negative | **Priority:** P0 | **Target:** api-tester
- **Traces to:** grading robustness; "client can't self-grade via malformed input"
- **Seed:** `QuestionType=TrueFalse`, `CorrectAnswer="True"`.
- **Steps:** Submit `AnswerPayload:"yes"` (then repeat for `"1"`).
- **Expected:** 200 (not 500), `isCorrect=false` for both (`bool.TryParse("yes")` and `bool.TryParse("1")` both fail → false). No 500, no leak of `correctAnswer` swap. (`"1"`/`"yes"` must NOT be treated as true.)

### BE-TC-10 — FillInBlank correct (trim + case-insensitive)
- **Type:** grading | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-1; grading-per-type
- **Seed:** `QuestionType=FillInBlank`, `CorrectAnswer="cairo"`.
- **Steps:** Submit `AnswerPayload:"  Cairo  "` (leading/trailing whitespace + different case).
- **Expected:** 200, `isCorrect=true` (trim + OrdinalIgnoreCase).

### BE-TC-11 — FillInBlank wrong
- **Type:** grading | **Priority:** P1 | **Target:** api-tester
- **Seed:** `QuestionType=FillInBlank`, `CorrectAnswer="cairo"`.
- **Steps:** Submit `"giza"`.
- **Expected:** 200, `isCorrect=false`, `correctAnswer="cairo"`.

### BE-TC-12 — Empty/whitespace payload grades wrong (guard), regardless of type
- **Type:** boundary | **Priority:** P1 | **Target:** api-tester
- **Traces to:** `AnswerComparator` null/empty guard. NOTE: a fully empty payload trips the 422 validator first (`AnswerPayload NotEmpty`) — see BE-TC-32. Use a **whitespace-only** payload to reach the comparator guard.
- **Seed:** MCQ, `CorrectAnswer="\"A\""`.
- **Steps:** Submit `AnswerPayload:"   "` (spaces only — passes `NotEmpty` but `IsNullOrWhiteSpace` in comparator).
- **Expected:** 200, `isCorrect=false` (comparator returns false for whitespace), no 500. (If the framework `NotEmpty` also rejects whitespace → 422; record whichever the API actually returns and flag in execution-report.)

### BE-TC-13 — Matching: documents current string-compare fallback (partial)
- **Type:** grading (documentary) | **Priority:** P2 | **Target:** api-tester
- **Traces to:** grading-per-type (Matching); OQ-4
- **Seed:** `QuestionType=Matching`, `CorrectAnswer="x"`.
- **Steps:** (a) Submit `"x"` → expect `isCorrect=true`. (b) Submit `"y"` → expect `isCorrect=false`.
- **Expected:** Behaves as string OrdinalIgnoreCase compare (the documented Phase-2 fallback). **Mark this case as "documents current behavior, not a final Matching contract"** per the `// TODO P2-07.b` in `AnswerComparator`. Do not assert a real pair-set semantics.

---

## Group C — Correct-answer-leak prevention (security)

### BE-TC-14 — Correct answer never returned on the correct path (multi-type sweep)
- **Type:** security/regression | **Priority:** P0 | **Target:** api-tester
- **Traces to:** "answer revealed ONLY when wrong"
- **Steps:** For each of MCQ, TrueFalse, FillInBlank: submit the *correct* answer; assert `data.correctAnswer` is absent or JSON null and the stored correct value does not appear anywhere in the response body.
- **Expected:** Every correct submission → `correctAnswer` null; the answer string is not leaked in any field.

### BE-TC-15 — No `ex.Message` / answer leak on error path
- **Type:** security/negative | **Priority:** P1 | **Target:** api-tester
- **Traces to:** security audit FA-2 (no `ex.Message` leak)
- **Steps:** Trigger a handled error path (e.g. duplicate submit → 424; non-existent attempt → 404) and inspect the message.
- **Expected:** Error responses carry a localized message only — no stack trace, no exception type, no `CorrectAnswer`, no answer payload echoed. Body never contains a raw .NET exception string.

---

## Group D — Server-side grading authority (client cannot self-grade)

### BE-TC-16 — Forged `isCorrect`/`correctAnswer` keys in request body are ignored
- **Type:** security/negative | **Priority:** P0 | **Target:** api-tester
- **Traces to:** "answer-checking is server-side; client can't self-grade"
- **Seed:** MCQ, `CorrectAnswer="\"A\""`.
- **Steps:** POST a body that includes extra/unexpected fields attempting to coerce the verdict: `{ QuestionId, AnswerPayload:"\"B\"" (wrong), TimeSpentSeconds:5, HintUsed:false, IsCorrect:true, CorrectAnswer:"\"B\"" }`.
- **Expected:** 200, `data.isCorrect=false` (server graded the *wrong* payload as wrong; the injected `IsCorrect:true` is ignored — `SubmitAnswerCommand` has no such bindable field). DB row `IsCorrect=false`. Proves the verdict comes from `AnswerComparator`, not the request.

### BE-TC-17 — Submitting the literal correct value still grades server-side (sanity inverse)
- **Type:** security | **Priority:** P2 | **Target:** api-tester
- **Steps:** Same seed as BE-TC-16; submit the genuinely correct payload `"\"A\""` with `IsCorrect:false` injected.
- **Expected:** 200, `data.isCorrect=true` — the injected `IsCorrect:false` is ignored; server computes truth. (Pairs with BE-TC-16 to prove the client claim is inert in both directions.)

---

## Group E — IDOR / cross-tenant (auth-authz)

### BE-TC-18 — Student A cannot submit to Student B's attempt → 401
- **Type:** auth-authz/IDOR | **Priority:** P0 | **Target:** api-tester
- **Traces to:** "student can't submit for another student's attempt (IDOR)"; OQ-1
- **Preconditions:** Two students A and B (each via parent flow). B starts an attempt for a shared lesson.
- **Steps:** Student A `POST /Answers` to B's `attemptId` with A's JWT.
- **Expected:** **401 Unauthorized** (`attempt.StudentId != studentId.Value`). `successed=false`. No `StudentAnswer` row written for that attempt by A. (See OQ-1: task expected 403/404 — actual contract is 401; assert 401 and flag.)

### BE-TC-19 — Cross-lesson question injection → 404 (and not graded)
- **Type:** auth-authz/negative | **Priority:** P1 | **Target:** api-tester
- **Traces to:** same-lesson guard (`q.LessonId == attempt.LessonId`)
- **Preconditions:** Lesson L1 (attempt started on L1) and a separate Lesson L2 with its own question Q2.
- **Steps:** Submit Q2's `QuestionId` into the L1 attempt.
- **Expected:** **404 NotFound** (QuestionNotFound — the same-lesson filter excludes it). `successed=false`. No `StudentAnswer` row, no event.

---

## Group F — Role gate (Student only)

### BE-TC-20 — Anonymous (no JWT) → 401
- **Type:** auth-authz | **Priority:** P0 | **Target:** api-tester
- **Traces to:** `[Authorize(Roles="Student")]`
- **Steps:** `POST /Answers` with **no** Authorization header.
- **Expected:** **401 Unauthorized** (framework challenge, before handler).

### BE-TC-21 — Parent JWT (wrong role) → 403
- **Type:** auth-authz | **Priority:** P0 | **Target:** api-tester
- **Traces to:** role gate; product has no teacher role → parent is the realistic wrong-role actor; OQ-5
- **Preconditions:** A parent JWT from `RegisterParentAndGetTokenAsync` (role ≠ Student).
- **Steps:** `POST /Answers` with the parent JWT.
- **Expected:** **403 Forbidden** (authenticated but role not Student). No grading, no event.

### BE-TC-22 — SuperAdmin JWT (wrong role) → 403
- **Type:** auth-authz | **Priority:** P2 | **Target:** api-tester
- **Steps:** Sign in as `superadmin`; `POST /Answers` with that token.
- **Expected:** **403 Forbidden**. Confirms the role policy is `Student`-exclusive, not merely "any authenticated".

---

## Group G — Integration-event emission (AC-4 real-time analytics) + isolation

> Implement an in-test `INotificationHandler<AnswerSubmittedIntegrationEvent>` / `INotificationHandler<LessonCompletedIntegrationEvent>` capturing into a per-test collection, registered via `_factory.WithWebHostBuilder(b => b.ConfigureServices(...))`. Clear the collection per test.

### BE-TC-23 — AnswerSubmittedIntegrationEvent fires once on success (SkillId present)
- **Type:** event/functional | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-4
- **Seed:** `SeedSkillAsync()`; MCQ question with `SkillId = <that skill>`; lesson + attempt.
- **Steps:** Submit a correct answer.
- **Expected:** 200; exactly **one** captured event with `StudentId` = caller, `LessonId` = attempt lesson, `SkillId` = the seeded skill, `CorrectAnswerCount = 1` (0 if wrong). Event payload contains **no** answer text / `CorrectAnswer` / PII — only int IDs.

### BE-TC-24 — Event NOT fired when QuizQuestion.SkillId is null (skip + 200 + persist)
- **Type:** event/negative | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-4 (skip rule, brief Q6)
- **Seed:** MCQ question with `SkillId = null`.
- **Steps:** Submit an answer.
- **Expected:** 200, answer persisted, **zero** `AnswerSubmittedIntegrationEvent` captured. (Log warning is emitted but not asserted via HTTP.)

### BE-TC-23-NEG — No event on any rejected SubmitAnswer path
- **Type:** event/negative | **Priority:** P0 | **Target:** api-tester
- **Traces to:** "no event leaks on failure paths" (brief success-measure d)
- **Steps:** For each rejection, assert **zero** captured events:
  1. Duplicate question (submit same `QuestionId` twice) → 2nd is 424, no event on the 2nd call.
  2. Cross-lesson injection → 404, no event (BE-TC-19 scenario).
  3. Not-InProgress (complete then submit) → 424, no event.
  4. Ownership violation → 401, no event (BE-TC-18 scenario).
- **Expected:** All four rejection responses carry the documented status code and emit no integration event. (Note: questions in 1/3/4 may be skill-less; seed them **with** a SkillId so a leak would actually be detectable.)

### BE-TC-25 — LessonCompletedIntegrationEvent fires once on Complete (lesson SkillId present)
- **Type:** event/functional | **Priority:** P0 | **Target:** api-tester
- **Traces to:** AC-4
- **Seed:** `SeedSkillAsync()`; lesson with `SkillId = <that skill>`; 3 questions (2 correct, 1 wrong); attempt; submit all 3.
- **Steps:** `POST /Complete`.
- **Expected:** 200; exactly **one** `LessonCompletedIntegrationEvent` with `StudentId`, `LessonId`, `SkillId`, `AccuracyPercentage = (int)Math.Round(66.67) = 67`, `CorrectAnswerCount = 2`. No PII in payload.

### BE-TC-26 — LessonCompleted event NOT fired when Lesson.SkillId is null
- **Type:** event/negative | **Priority:** P1 | **Target:** api-tester
- **Seed:** Lesson with `SkillId = null` (default `SeedLessonAsync`); attempt; submit ≥1 answer.
- **Steps:** `POST /Complete`.
- **Expected:** 200, attempt Completed, **zero** `LessonCompletedIntegrationEvent`.

### BE-TC-27 — LessonCompleted event does NOT re-fire on idempotent re-Complete
- **Type:** event/negative/state | **Priority:** P0 | **Target:** api-tester
- **Traces to:** brief R5 (idempotent re-Complete must not re-publish)
- **Seed:** Lesson with `SkillId` set (so a re-fire would be detectable); attempt; submit answers.
- **Steps:** `POST /Complete` (1st → 200, fires once), then `POST /Complete` again (2nd → 200, idempotent).
- **Expected:** Total captured `LessonCompletedIntegrationEvent` count = **exactly 1** across both calls. 2nd call returns Completed state without re-publishing.

### BE-TC-28 — Handler isolation: a throwing subscriber does not fail the 200
- **Type:** event/regression | **Priority:** P0 | **Target:** api-tester
- **Traces to:** story AC (no reload / feedback always returns); audit FA-5 (`IsolatedNotificationPublisher`)
- **Steps:** Register a deliberately-throwing `INotificationHandler<AnswerSubmittedIntegrationEvent>` **alongside** the capturing handler; submit a valid answer (skill-linked).
- **Expected:** HTTP **200** with normal feedback body; the capturing handler still received the event (proves per-handler isolation). The student request is unaffected by the failing subscriber.

### BE-TC-29 — Event payload data-minimization (no PII / no answer text)
- **Type:** security | **Priority:** P1 | **Target:** api-tester
- **Traces to:** audit FA-1 (data minimization)
- **Steps:** In the capturing handler from BE-TC-23, inspect the received `AnswerSubmittedIntegrationEvent` fields.
- **Expected:** Only `EventId` (Guid), `OccurredOnUtc`, `StudentId`, `LessonId`, `SkillId`, `CorrectAnswerCount` — all opaque/derived. **No** `AnswerPayload`, no `CorrectAnswer` string, no name/email/DOB. Same check for `LessonCompletedIntegrationEvent` (adds only `AccuracyPercentage`).

---

## Group H — Validation (422) and other negative/boundary inputs

> `SubmitAnswerCommand` is an `ICommand` → `ValidationBehavior` runs `SubmitAnswerValidation`. These map to **422 UnprocessableEntity**.

### BE-TC-30 — AttemptId ≤ 0 → 422
- **Type:** validation | **Priority:** P1 | **Target:** api-tester
- **Steps:** `POST /api/Learning/Quizzes/0/Answers` (route binds `AttemptId=0`) with otherwise-valid body.
- **Expected:** **422**, `successed=false`, localized "AttemptId must be positive" message. No grading, no event. (If routing rejects `0` differently, record actual.)

### BE-TC-31 — QuestionId ≤ 0 → 422
- **Type:** validation | **Priority:** P1 | **Target:** api-tester
- **Steps:** Valid InProgress attempt; submit `{ QuestionId:0, AnswerPayload:"\"A\"", ... }`.
- **Expected:** **422**, "QuestionId must be positive". No row, no event.

### BE-TC-32 — Empty AnswerPayload → 422
- **Type:** validation | **Priority:** P1 | **Target:** api-tester
- **Traces to:** `AnswerPayload NotEmpty`
- **Steps:** Submit `{ QuestionId:<valid>, AnswerPayload:"", ... }`.
- **Expected:** **422**, "AnswerPayload required". Confirms validation precedes grading (empty never reaches the comparator).

### BE-TC-33 — TimeSpentSeconds boundary (security ceiling 3600)
- **Type:** boundary/validation | **Priority:** P1 | **Target:** api-tester
- **Traces to:** security ceiling (prevents client timing inflation)
- **Steps:** (a) `TimeSpentSeconds:3600` → expect **200** (inclusive ceiling). (b) `TimeSpentSeconds:3601` → expect **422** "exceeds maximum". (c) `TimeSpentSeconds:-1` → expect **422** "must be non-negative".
- **Expected:** 3600 accepted; 3601 and -1 rejected with 422. Proves the client cannot inflate per-question time stats.

---

## Implementation notes for `api-tester`
- **Seeder gap:** `SeedQuestionsAsync` in `P2_08_*` hardcodes `QuestionType.MCQ`. Extend it (or add a local seeder) to accept `QuestionType` and arbitrary `CorrectAnswer` for BE-TC-07..13.
- **Skill linkage:** for event cases, set both the question's `SkillId` (Group G answer-event cases) and/or the lesson's `SkillId` (Complete-event cases) — they are distinct nullable columns.
- **Event capture infra:** prefer a test-class-local `WithWebHostBuilder` that registers the capturing/throwing handlers into a `ConcurrentBag`; clear per test to avoid cross-test bleed in the shared `"IntegrationTests"` collection.
- **Status-code asserts:** use the numeric code for 422/424 (`((int)resp.StatusCode).Should().Be(422 / 424)`) — `HttpStatusCode.FailedDependency`/`UnprocessableEntity` also work.
- **OQ-1/OQ-2:** assert the **as-built** codes (401 for IDOR, 404 vs 401 enumeration). If a case fails because the code differs, record it as a contract observation in `execution-report.md`, not a silent re-assertion.
