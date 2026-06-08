# P2-08 — Backend Test Cases (API / HTTP)

**Story:** Record granular per-question answers · **Module:** Learning (`learning` schema) · **Target agent:** `api-tester`
**Surface:** 5 endpoints on `QuizzesController` / `StudentsController` / `SkillsController` (all under `api/Learning/...`).
**Implement each case 1:1 into `backend/tests/Learnexia.IntegrationTests/` (real Postgres) and record pass/fail in `execution-report.md`.**

> Source of truth for behavior: the implemented handlers (already merged). This catalog is grounded in the
> real code, not the plan — where the plan and code differ, the **code wins** (called out inline).

---

## Endpoints under test

| # | Method | Route | Auth attribute | Handler |
|---|--------|-------|----------------|---------|
| E1 | POST | `/api/Learning/Quizzes/{attemptId}/Answers` | `[Authorize(Roles="Student")]` | `SubmitAnswerCommandHandler` |
| E2 | POST | `/api/Learning/Quizzes/{attemptId}/Complete` | `[Authorize(Roles="Student")]` | `CompleteAttemptCommandHandler` |
| E3 | POST | `/api/Learning/Quizzes/{attemptId}/Abandon` | `[Authorize(Roles="Student")]` | `AbandonAttemptCommandHandler` |
| E4 | GET | `/api/Learning/Students/{studentId}/Attempts` | `[Authorize]` | `GetStudentAttemptsQueryHandler` |
| E5 | GET | `/api/Learning/Skills/{skillId}/Stats?studentId=` | `[Authorize]` | `GetSkillStatsQueryHandler` |

## Status-code contract (verified in `BaseResponseHandler` + architecture.md §6)

| Handler helper | HTTP | Used in P2-08 for |
|---|---|---|
| `Success` / `EmptyCollection` | **200** | happy paths, empty attempt list, idempotent terminal returns |
| `BadRequest` | **400** | query inline validation (`studentId<=0`, `skillId<=0`) — E4/E5 only |
| `Unauthorized` | **401** | missing JWT student id **and** ownership/IDOR violations (see note) |
| `NotFound` | **404** | attempt not found; question not found / cross-lesson |
| `BusinessValidation` | **424** (FailedDependency) | not-InProgress, already-answered, already-completed/abandoned |
| `ServerError` | **500** | unhandled exception (no `ex.Message` leaked) |
| `ValidationBehavior` (FluentValidation) | **422** | malformed **command** body (E1/E2/E3 only — queries are NOT auto-validated) |

> **CRITICAL CONTRACT NOTE (assert exactly, do not "fix" to 403):** ownership / IDOR violations on **every**
> P2-08 endpoint return **HTTP 401** via `Unauthorized<T>()`, **not** 403. The brief/plan prose says "403/404";
> the shipped code returns **401**. Assert **401** and flag the prose mismatch as an open question (do not change code).
> The only 403 on this surface is the **framework role gate** (`[Authorize(Roles="Student")]`) rejecting a
> Parent/Admin JWT on E1/E2/E3.

## Envelope contract (assert on representative cases)

Every response body is `BaseResponse<T>`: keys `successed` (camelCase, **double-s spelling** — never `success`/`succeeded`),
`statusCode`, `message`, `data`, plus `errors`/`meta` per the shared shape. On success `successed=true`; on any
4xx/5xx handler return `successed=false`.

## Seeding / auth helper (reuse the P2-06 pattern)

Student JWT flow (see `P2_06_StartAttempt_Tests`): **Register Parent → `POST /api/Parent/Add-Child` → Sign-In as
that child → Student JWT.** Start an attempt via `POST /api/Learning/Quizzes/{lessonId}/Attempt` to obtain a real
`AttemptId` + the lesson's questions. To know the correct answer for a deterministic correct/wrong submit, read
`QuizQuestion.CorrectAnswer` directly from the `LearningDbContext` in the test (it is never returned over the wire).
Two distinct students (two Add-Child calls) are required for the IDOR cases. A skill with `QuizQuestion.SkillId` set
is required for E5 with-data cases — seed inline if the demo seeder does not set `SkillId`.

---

## Group A — SubmitAnswer (E1) · persistence accuracy is the core contract

### BE-TC-01 — Submit correct answer → persisted accurately
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Student S1 with an InProgress attempt A1 on lesson L1; question Q1 (read its `CorrectAnswer` from DB).
- **Steps:**
  1. POST E1 `{attemptId=A1}` body `{ QuestionId=Q1, AnswerPayload=<exact CorrectAnswer>, TimeSpentSeconds=12, HintUsed=false }` with S1 JWT.
  2. Query `learning.StudentAnswers` for `(AttemptId=A1, QuestionId=Q1)`.
- **Expected:** 200, `successed=true`. Response `data.isCorrect=true`, `data.correctAnswer=null`, `data.hintAvailable=false`. DB row exists with `IsCorrect=true`, `TimeSpentSeconds=12`, `HintUsed=false`, `AttemptId=A1`, `QuestionId=Q1`. (Round-trip of all four granular fields.)
- **Traces to:** AC-1 (per-answer detail persisted).

### BE-TC-02 — Submit wrong answer → isCorrect=false + correctAnswer disclosed
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Steps:** POST E1 with `AnswerPayload` that does NOT match `CorrectAnswer`, `TimeSpentSeconds=30`, `HintUsed=true`.
- **Expected:** 200, `data.isCorrect=false`, `data.correctAnswer` is populated (equals the question's `CorrectAnswer`). DB row `IsCorrect=false`, `TimeSpentSeconds=30`, `HintUsed=true`.
- **Traces to:** AC-1; P2-07 feedback contract (conditional disclosure).

### BE-TC-03 — HintUsed=true persists and round-trips
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Submit any answer with `HintUsed=true`; read the DB row.
- **Expected:** DB `HintUsed=true`. (Feeds `HintsUsedCount` aggregate — see BE-TC-17.)
- **Traces to:** AC-1.

### BE-TC-04 — Correctness is computed server-side per QuestionType
- **Type:** functional / security · **Priority:** P1 · **Agent:** api-tester
- **Notes:** `AnswerComparator.AreEqual` dispatches per `QuestionType` (TrueFalse via `bool.TryParse`; FillInBlank trim + OrdinalIgnoreCase; MCQ exact). Client cannot assert correctness — only `AnswerPayload` is honored.
- **Steps:** For a TrueFalse question whose correct value is `true`, submit `AnswerPayload="TRUE"` (different casing).
- **Expected:** `isCorrect=true` (server-side normalization), DB `IsCorrect=true`. Submitting a `isCorrect` field in the body has no effect (not in DTO).
- **Traces to:** AC-1; AC-6 (server authority).

### BE-TC-05 — Duplicate answer for same question → 424 (re-answer guard)
- **Type:** negative / boundary · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Submit Q1 successfully (BE-TC-01), then POST E1 again with the same `QuestionId=Q1`.
- **Expected:** Second call 424 (`BusinessValidation`, `QuestionAlreadyAnswered`), `successed=false`. DB still has exactly ONE row for `(A1,Q1)` — the original `IsCorrect`/timing unchanged (no overwrite, no append).
- **Traces to:** AC-1 (clean accuracy denominator); resolved idempotency decision.

### BE-TC-06 — Submit to another student's attempt → 401 (IDOR write)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Attempt A2 belongs to student S2. S1 holds a valid Student JWT.
- **Steps:** POST E1 `{attemptId=A2}` with S1 JWT and a valid body.
- **Expected:** **401** (`Unauthorized`), `successed=false`, no `StudentAnswer` row created for A2. **No data leak** (response carries no question/answer content).
- **Traces to:** AC-6; security audit FA-2.

### BE-TC-07 — Submit to non-existent attempt → 404
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Steps:** POST E1 `{attemptId=2147483647}` (no such attempt), valid body, S1 JWT.
- **Expected:** 404 (`AttemptNotFound`), `successed=false`.
- **Traces to:** AC-6.

### BE-TC-08 — Submit to a non-InProgress attempt → 424
- **Type:** negative / state · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Attempt A1 owned by S1, transitioned to `Completed` (via E2) or `Abandoned` (via E3).
- **Steps:** POST E1 against A1 with a valid body.
- **Expected:** 424 (`BusinessValidation`, `AttemptNotInProgress`), `successed=false`. No new answer row.
- **Traces to:** AC-6; ordering — answers only accepted while InProgress.

### BE-TC-09 — Cross-lesson question injection → 404
- **Type:** auth-authz / negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Attempt A1 is on lesson L1. Question Qx belongs to a DIFFERENT lesson L2.
- **Steps:** POST E1 `{attemptId=A1}` body `{ QuestionId=Qx, ... }`.
- **Expected:** 404 (`QuestionNotFound` — the handler's `q.LessonId == attempt.LessonId` guard), `successed=false`. No row written.
- **Traces to:** AC-6; security audit FA-7.

### BE-TC-10 — Missing JWT → 401 (framework)
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Steps:** POST E1 with NO `Authorization` header.
- **Expected:** 401 (framework challenge). No row written.
- **Traces to:** AC-6.

### BE-TC-11 — Parent/Admin JWT → 403 (role gate)
- **Type:** auth-authz / product-override · **Priority:** P0 · **Agent:** api-tester
- **Steps:** POST E1 with a Parent JWT (and separately a superadmin JWT) on an existing attempt.
- **Expected:** **403** (role gate — `[Authorize(Roles="Student")]`). Distinguish this from the 401 ownership case (BE-TC-06). No row written.
- **Traces to:** AC-6; product: writes are Student-role only.

### BE-TC-12 — Validation: missing/empty AnswerPayload → 422
- **Type:** validation · **Priority:** P0 · **Agent:** api-tester
- **Steps:** POST E1 with body `{ QuestionId=Q1, AnswerPayload="", TimeSpentSeconds=5, HintUsed=false }` (and a variant with `AnswerPayload` omitted/null), S1 JWT.
- **Expected:** **422** (`ValidationBehavior`, `AnswerPayloadRequired`), `successed=false`, `errors` populated. No row written.
- **Traces to:** AC-1; validation contract.

### BE-TC-13 — Validation: QuestionId <= 0 → 422
- **Type:** validation / boundary · **Priority:** P1 · **Agent:** api-tester
- **Steps:** POST E1 body with `QuestionId=0` (and `-1`).
- **Expected:** 422 (`QuestionIdMustBePositive`).
- **Traces to:** validation contract.

### BE-TC-14 — Validation: TimeSpentSeconds boundaries → 422 below 0 and above 3600
- **Type:** validation / boundary · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Three submits: `TimeSpentSeconds=-1`, `=0`, `=3600`, `=3601`.
- **Expected:** `-1` → 422 (`TimeSpentSecondsMustBeNonNegative`); `=0` → accepted (200); `=3600` → accepted (200, inclusive upper bound); `=3601` → 422 (`TimeSpentSecondsExceedsMaximum`). Asserts the anti-stats-inflation ceiling (security audit FA-6).
- **Traces to:** AC-1; security audit F-01/FA-6.

### BE-TC-15 — AttemptId <= 0 in route/command → 422
- **Type:** validation / boundary · **Priority:** P2 · **Agent:** api-tester
- **Steps:** POST E1 with route `{attemptId=0}` (command `AttemptId` is overwritten from route).
- **Expected:** 422 (`AttemptIdMustBePositive`).
- **Traces to:** validation contract.

### BE-TC-16 — Oversized AnswerPayload (no max-length bound) — documents F-01
- **Type:** boundary / robustness · **Priority:** P2 · **Agent:** api-tester
- **Notes:** Security audit F-01 (Low): `AnswerPayload` has NO `MaximumLength` validator and the column is `text` — a multi-MB payload is accepted and persisted.
- **Steps:** POST E1 with a ~1 MB `AnswerPayload` string against an InProgress attempt.
- **Expected (document actual):** today the handler accepts it → 200, row persisted with the large payload (no 422). **Assert the current behavior and flag F-01** as a hardening follow-up — do NOT mark RED unless the team decides a bound is required. If a bound is added later, this becomes a 422 case.
- **Traces to:** security audit F-01.

---

## Group B — CompleteAttempt (E2) · aggregate accuracy

### BE-TC-17 — Complete with mixed answers → correct aggregates
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** S1 InProgress attempt A1; submit 4 answers — 3 correct, 1 wrong; 2 of them `HintUsed=true`.
- **Steps:** POST E2 `{attemptId=A1}` with S1 JWT. Then re-read the `Attempt` row from DB.
- **Expected:** 200, `data.status="Completed"`, `data.accuracyPercentage=75` (3/4×100), `data.totalAnswers=4`, `data.correctAnswers=3`, `data.hintsUsedCount=2`, `data.completedAt` not null, `data.durationSeconds>=0`. DB `Attempt.Status=Completed (2)`, same aggregate values persisted.
- **Traces to:** AC-2.

### BE-TC-18 — Complete with zero answers → accuracy 0, no divide-by-zero
- **Type:** boundary · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Start A1, immediately POST E2.
- **Expected:** 200 (NOT 500), `accuracyPercentage=0`, `totalAnswers=0`, `correctAnswers=0`, `hintsUsedCount=0`, `status="Completed"`, `completedAt` set.
- **Traces to:** AC-2; divide-by-zero guard.

### BE-TC-19 — AccuracyPercentage rounding (2 dp)
- **Type:** boundary · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Submit 3 answers, 1 correct (→ 33.333…%), then Complete.
- **Expected:** `accuracyPercentage=33.33` (rounded to 2 dp, `Math.Round`).
- **Traces to:** AC-2.

### BE-TC-20 — Complete is idempotent (call twice) → 200 both times
- **Type:** functional / state · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Complete A1, then POST E2 again.
- **Expected:** Second call 200, `status="Completed"`, same aggregates returned (no mutation, no error). NOT 424/500.
- **Traces to:** AC-2; idempotency.

### BE-TC-21 — Complete an already-Abandoned attempt → 424
- **Type:** negative / state · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Abandon A1 (E3), then POST E2 on A1.
- **Expected:** 424 (`BusinessValidation`, `AttemptAlreadyAbandoned`), `successed=false`. Status remains `Abandoned`, aggregates unchanged.
- **Traces to:** AC-2/AC-3; terminal-state transition guard.

### BE-TC-22 — Complete another student's attempt → 401 (IDOR)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Agent:** api-tester
- **Steps:** POST E2 `{attemptId=A2(owned by S2)}` with S1 JWT.
- **Expected:** **401** (`Unauthorized`). A2 status/aggregates unchanged. No leak.
- **Traces to:** AC-6; security audit FA-2.

### BE-TC-23 — Complete non-existent attempt → 404
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Expected:** 404 (`AttemptNotFound`).
- **Traces to:** AC-6.

### BE-TC-24 — Complete: missing JWT → 401; Parent/Admin JWT → 403
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Expected:** No header → 401 (framework); Parent/superadmin JWT → 403 (role gate).
- **Traces to:** AC-6; product role rule.

### BE-TC-25 — Complete: AttemptId<=0 → 422
- **Type:** validation / boundary · **Priority:** P2 · **Agent:** api-tester
- **Steps:** POST E2 route `{attemptId=0}` (and `-1`).
- **Expected:** 422 (`AttemptIdMustBePositive`).
- **Traces to:** validation contract.

### BE-TC-26 — Submitted answers survive completion (not deleted)
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Submit 3 answers, Complete, then re-count `StudentAnswers` for A1.
- **Expected:** All 3 rows still present after completion (Complete only updates the Attempt aggregate, never deletes child rows).
- **Traces to:** AC-1/AC-2.

---

## Group C — AbandonAttempt (E3) · partial signal capture

### BE-TC-27 — Abandon partial attempt → status Abandoned, aggregates over partial set, answers survive
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Lesson L1 has 5 questions; S1 attempt A1; submit only 2 answers (1 correct, 1 wrong).
- **Steps:** POST E3 `{attemptId=A1}` with S1 JWT. Re-read Attempt + count StudentAnswers.
- **Expected:** 200, `status="Abandoned"`, `completedAt` set, `totalAnswers=2`, `correctAnswers=1`, `accuracyPercentage=50` (over the **2** answered, NOT the 5 total questions), `hintsUsedCount` matches. DB still has exactly the 2 `StudentAnswer` rows (none lost). `Attempt.Status=Abandoned (3)`.
- **Traces to:** AC-3 (reliable partial capture — the survivorship-bias guard).

### BE-TC-28 — Abandon with zero answers → accuracy 0, no 500
- **Type:** boundary · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Start A1, immediately POST E3.
- **Expected:** 200 (NOT 500), `accuracyPercentage=0`, `totalAnswers=0`, `hintsUsedCount=0`, `status="Abandoned"`, `completedAt` set, `durationSeconds>=0`.
- **Traces to:** AC-3; divide-by-zero guard.

### BE-TC-29 — Abandon is idempotent (call twice) → 200 both times
- **Type:** functional / state · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Abandon A1, then POST E3 again.
- **Expected:** Second call 200, `status="Abandoned"`, same `totalAnswers`/`correctAnswers` returned (computed from existing rows), no mutation, no error.
- **Traces to:** AC-3; idempotency.

### BE-TC-30 — Abandon an already-Completed attempt → 424
- **Type:** negative / state · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Complete A1, then POST E3.
- **Expected:** 424 (`BusinessValidation`, `AttemptAlreadyCompleted`). Status stays `Completed`.
- **Traces to:** AC-2/AC-3.

### BE-TC-31 — Abandon another student's attempt → 401 (IDOR)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Agent:** api-tester
- **Steps:** POST E3 `{attemptId=A2(S2)}` with S1 JWT.
- **Expected:** **401** (`Unauthorized`). A2 unchanged. No leak.
- **Traces to:** AC-6.

### BE-TC-32 — Abandon non-existent attempt → 404
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Expected:** 404 (`AttemptNotFound`).
- **Traces to:** AC-6.

### BE-TC-33 — Abandon: missing JWT → 401; Parent/Admin JWT → 403; AttemptId<=0 → 422
- **Type:** auth-authz / validation · **Priority:** P1 · **Agent:** api-tester
- **Expected:** No header → 401; Parent/superadmin → 403; route `{attemptId=0}` → 422 (`AttemptIdMustBePositive`).
- **Traces to:** AC-6; validation contract.

### BE-TC-34 — Abandoned answers retrievable afterward (persistence end-to-end)
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Steps:** After BE-TC-27, query `StudentAnswers` for A1 and confirm each row's `IsCorrect`/`TimeSpentSeconds`/`HintUsed` matches what was submitted.
- **Expected:** Every submitted answer is durably retrievable with accurate fields (the core P2-08 contract — abandon does not corrupt the granular rows).
- **Traces to:** AC-1/AC-3.

---

## Group D — GetStudentAttempts (E4) · per-student query + IDOR

### BE-TC-35 — Self read returns attempts with aggregates, newest first
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** S1 has 2 terminal attempts (one Completed, one Abandoned) + their aggregates.
- **Steps:** GET E4 `/Students/{S1}/Attempts` with S1 JWT.
- **Expected:** 200, `data` is a list of 2 `AttemptListItemDto`: each has `id`, `lessonId`, `status`, `accuracyPercentage`, `durationSeconds`, `hintsUsedCount`, `startedAt`, `completedAt`. Ordered by `startedAt` **descending**. Aggregate values match what Complete/Abandon wrote.
- **Traces to:** AC-4.

### BE-TC-36 — CorrectAnswer NEVER leaked in the attempts list
- **Type:** security / regression · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Inspect the raw JSON of every item from BE-TC-35.
- **Expected:** No `correctAnswer` key (case-insensitive) anywhere in the response; no question text/options either (the DTO carries none). `AttemptListItemDto` has no such field.
- **Traces to:** AC-4; security audit FA-4.

### BE-TC-37 — Reading another student's attempts → 401 (IDOR)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Agent:** api-tester
- **Steps:** S1 calls GET E4 `/Students/{S2}/Attempts` with S1 JWT.
- **Expected:** **401** (`Unauthorized` — `request.StudentId != currentUser.UserId`). Response carries **no** attempt data for S2 (empty/null `data`, no leak).
- **Traces to:** AC-6; security audit FA-3.

### BE-TC-38 — New student with no attempts → 200 empty list (not 404)
- **Type:** state / boundary · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Fresh student S3 (no attempts) calls GET E4 `/Students/{S3}/Attempts`.
- **Expected:** 200 (`EmptyCollection`), `data=[]`, `successed=true`. NOT 404, NOT 500.
- **Traces to:** AC-4.

### BE-TC-39 — studentId <= 0 → 400 (query inline validation, NOT 422)
- **Type:** validation / boundary · **Priority:** P1 · **Agent:** api-tester
- **Notes:** Queries are NOT auto-validated; the handler returns `BadRequest` (400), not the `ValidationBehavior` 422.
- **Steps:** GET E4 `/Students/0/Attempts` and `/Students/-1/Attempts` with a valid JWT.
- **Expected:** **400** (`StudentIdMustBePositive`). (Note: ordering — for a positive-but-other-student id the IDOR 401 fires; for `<=0` the 400 fires first.)
- **Traces to:** AC-4; validation contract.

### BE-TC-40 — Missing JWT → 401 (framework). Parent JWT reaching another id → 401 (handler IDOR)
- **Type:** auth-authz · **Priority:** P1 · **Agent:** api-tester
- **Notes:** E4 is `[Authorize]` (any authenticated user), so a Parent JWT is NOT role-rejected; the handler IDOR guard is the only gate.
- **Steps:** (a) No header → expect 401 framework. (b) Parent JWT calling `/Students/{someStudentId}/Attempts` → expect 401 (`Unauthorized`, id mismatch). (c) Parent JWT calling `/Students/{parentOwnUserId}/Attempts` → 200 empty (parents have no attempts) — documents F-05.
- **Expected:** As above. Flag F-05 (generic `[Authorize]`, no Student role) as a documented Phase-5 deferral.
- **Traces to:** AC-6; security audit F-05.

---

## Group E — GetSkillStats (E5) · per-skill query + IDOR

### BE-TC-41 — Skill stats with data → correct aggregation
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Skill K1; questions with `SkillId=K1`; S1 has answered several of them across attempt(s) — e.g. 4 answers, 3 correct, 1 with `HintUsed=true`, time-spent values known.
- **Steps:** GET E5 `/Skills/{K1}/Stats?studentId={S1}` with S1 JWT.
- **Expected:** 200, `data`: `skillId=K1`, `studentId=S1`, `totalAnswers=4`, `correctAnswers=3`, `accuracyPercentage=75`, `avgTimeSpentSeconds` = rounded mean of the 4 times (2 dp), `hintUsageRate=25` (1/4×100, as a 0–100 percentage). No `correctAnswer` field present.
- **Traces to:** AC-5.

### BE-TC-42 — Questions with NULL SkillId are excluded
- **Type:** functional / boundary · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** S1 answered some questions whose `QuizQuestion.SkillId IS NULL` and some with `SkillId=K1`.
- **Steps:** GET E5 `/Skills/{K1}/Stats?studentId={S1}`.
- **Expected:** Only the `SkillId=K1` answers are counted; the null-skill answers do NOT inflate `totalAnswers`/`correctAnswers`. (Documents the nullable-SkillId design — the per-skill path is `StudentAnswer → Question.SkillId`.)
- **Traces to:** AC-5; brief Risks (nullable SkillId).

### BE-TC-43 — Skill with no answers → zeroed stats (not 404/500)
- **Type:** state / boundary · **Priority:** P0 · **Agent:** api-tester
- **Steps:** GET E5 `/Skills/{K_unanswered}/Stats?studentId={S1}` for a skill S1 has never answered.
- **Expected:** 200, all numeric fields `0` (`totalAnswers=0`, `correctAnswers=0`, `accuracyPercentage=0`, `avgTimeSpentSeconds=0`, `hintUsageRate=0`). NOT 404, NOT 500.
- **Traces to:** AC-5.

### BE-TC-44 — Stats are scoped to the requesting student (no cross-student bleed)
- **Type:** functional / IDOR · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** Both S1 and S2 answered questions for skill K1.
- **Steps:** GET E5 `/Skills/{K1}/Stats?studentId={S1}` with S1 JWT.
- **Expected:** Counts reflect ONLY S1's answers (the `sa.Attempt.StudentId == studentId` filter), NOT the combined pool. S2's answers are not included.
- **Traces to:** AC-5; AC-6 (per-student scope).

### BE-TC-45 — Requesting another student's skill stats → 401 (IDOR)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Agent:** api-tester
- **Steps:** S1 calls GET E5 `/Skills/{K1}/Stats?studentId={S2}` with S1 JWT.
- **Expected:** **401** (`Unauthorized` — query `studentId != currentUser.UserId`). No stats for S2 returned.
- **Traces to:** AC-6; security audit FA-3.

### BE-TC-46 — Validation: skillId<=0 or studentId<=0 → 400 (inline, not 422)
- **Type:** validation / boundary · **Priority:** P1 · **Agent:** api-tester
- **Steps:** (a) GET `/Skills/0/Stats?studentId={S1}`. (b) GET `/Skills/{K1}/Stats?studentId=0`. (c) `studentId` query param omitted entirely (binds to 0).
- **Expected:** (a) 400 (`SkillIdMustBePositive`); (b)+(c) 400 (`StudentIdMustBePositive`). All `successed=false`. (Confirms `skillId` is checked before `studentId`.)
- **Traces to:** AC-5; validation contract.

### BE-TC-47 — Missing JWT → 401; Parent JWT → handled by IDOR guard
- **Type:** auth-authz · **Priority:** P1 · **Agent:** api-tester
- **Steps:** (a) No header → 401 framework. (b) Parent JWT calling `?studentId={anyStudent}` → 401 (handler IDOR). Documents F-05 (generic `[Authorize]` on E5).
- **Traces to:** AC-6; security audit F-05.

---

## Group F — Cross-cutting envelope, error-shape, and timing

### BE-TC-48 — BaseResponse envelope shape + `Successed` spelling
- **Type:** functional / regression · **Priority:** P0 · **Agent:** api-tester
- **Steps:** On one success (E1 200) and one failure (E1 424 duplicate) response, parse the raw JSON.
- **Expected:** Keys present: `successed` (camelCase, double-s), `statusCode`, `message`, `data`. `successed=true` on success / `false` on failure. No `success`/`succeeded` key. (Guards the never-rename rule.)
- **Traces to:** API contract; CONVENTIONS.

### BE-TC-49 — ServerError never leaks `ex.Message` / stack trace
- **Type:** security / negative · **Priority:** P1 · **Agent:** api-tester
- **Notes:** Hard to force a 500 without a fault seam; if a deterministic 500 trigger is not reachable, mark **BLOCKED** with the reason (no fault-injection seam) rather than faking it. If reachable, assert the body `message` is the generic `"Internal Server Error."` and contains no exception text/stack.
- **Expected:** 500 body is generic; full exception only server-side. (security audit FA-5.)
- **Traces to:** security audit F-05/FA-5.

### BE-TC-50 — DurationSeconds is server-side elapsed, non-negative, not the sum of client times
- **Type:** functional / boundary · **Priority:** P1 · **Agent:** api-tester
- **Notes:** Handler uses `UtcNow - StartedAt.ToUniversalTime()`, clamped to `Math.Max(0, …)`. This addresses security audit F-02 (the `DateTime.Now`/`UtcNow` mismatch was fixed to normalize to UTC).
- **Steps:** Start A1, submit one answer with `TimeSpentSeconds=3600`, wait a short real interval, Complete.
- **Expected:** `data.durationSeconds` reflects the real wall-clock elapsed since `StartedAt` (small, a few seconds), **not** 3600, and is `>= 0`. Confirms client-reported per-question time does not drive attempt duration, and the UTC-offset bug (F-02) is not present (duration is not inflated by the host UTC offset, e.g. not ~7200).
- **Traces to:** AC-2; security audit F-02; brief Risk (client-reported timing).

### BE-TC-51 — Mass-assignment guard: client cannot set StudentId / IsCorrect via body
- **Type:** security / negative · **Priority:** P1 · **Agent:** api-tester
- **Steps:** POST E1 with extra JSON fields `{ "studentId": <S2>, "isCorrect": true, "answerPayload": <wrong value>, ... }`.
- **Expected:** Extra fields ignored. The row is owned by S1 (JWT), `IsCorrect` reflects the server-side comparison of the wrong payload (false), not the client-supplied `true`.
- **Traces to:** AC-6; security audit FA-1/FA-8.

---

## Coverage summary (this catalog)

| Acceptance criterion | Covered by |
|---|---|
| AC-1 per-answer detail persisted (`IsCorrect`/`TimeSpentSeconds`/`HintUsed`) | BE-TC-01..05, 12-16, 26, 34, 51 |
| AC-2 attempt aggregates on completion | BE-TC-17..26, 50 |
| AC-3 reliable partial capture on abandon | BE-TC-27..34 |
| AC-4 queryable per student; no CorrectAnswer leak | BE-TC-35..40 |
| AC-5 queryable per skill; zero-data safe | BE-TC-41..47 |
| AC-6 auth/ownership; JWT-derived id; no IDOR | BE-TC-06,09,10,11,22,24,31,33,37,40,44,45,47,51 |

**Total: 51 cases — all backend / `api-tester`.** P0: 30 · P1: 16 · P2: 5.
