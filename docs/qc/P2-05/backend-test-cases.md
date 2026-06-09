# P2-05 — Backend Test Cases (for `api-tester`)

Integration tests against the running API (xUnit + Testcontainers Postgres + `LearnexiaWebAppFactory`). Mirror the harness already in `backend/tests/Learnexia.IntegrationTests/P2_05_OpenAndCompleteLesson_Tests.cs` (helpers: `UniqueEmail`, `TryProp`, `SendAsync`, `CreateStudentViaParentFlowAsync`, `StartAttemptViaApiAsync`, `SubmitAnswerAsync`, `CompleteAttemptAsync`, `LearningSeeder.SeedAsync`). Many BE-TCs already have a shipped equivalent — those are marked **[shipped: P205-Cxx]**; re-trace/keep them. The **NEW** cases close the language-guard, authz, validation, IDOR, idempotency, and state-guard gaps.

**Conventions for every case**
- Student JWT via `CreateStudentViaParentFlowAsync`. The child is created with `LearningLanguage="en"` (resolves MATH/SCIENCE → En tree); for Arabic-medium fixtures create a child with `LearningLanguage="ar"`.
- Envelope: assert camelCase `"successed":` where relevant. Use `TryProp` (case-insensitive) for payload fields.
- Status-code source of truth: 200 Success, 404 NotFound, 401 Unauthorized, 403 Forbidden (language guard), **424** BusinessValidation (FailedDependency), **422** FluentValidation failure, 400 malformed body, 500 ServerError.
- Demo fixtures: Math G1 root lesson `"Introduction to Counting (G1)"` (Explanation+Visual+1 MCQ "What number comes after 5?" → correct `"6"` → JSON `"\"6\""`). Non-demo lesson `"Place Value: Tens and Hundreds (G1)"` (null content, no questions).

---

## Group A — Open lesson: `GET /api/learning/Lessons/{id}` (assembly + authz + content)

### BE-TC-01 — Anonymous open → 401  **[shipped: P205-C01]**
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** demo lesson id resolved; no Authorization header.
- **Steps:** 1. `GET api/learning/Lessons/{demoLessonId}` with no bearer token.
- **Expected:** HTTP **401**. Body envelope `successed=false`.
- **Traces to:** AC1 (authz) / brief Q5.

### BE-TC-02 — Authenticated open of seeded demo lesson returns full assembly DTO  **[shipped: P205-C02]**
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** en-student JWT; Math G1 demo lesson.
- **Steps:** 1. `GET api/learning/Lessons/{demoLessonId}` with Student JWT.
- **Expected:** **200**, `successed=true`. `data` has non-null `explanation` (non-empty string), non-null `visual` (non-empty string), and non-null `quickCheck` object with `id>0`, non-empty `questionText`, non-empty `options`. `data` also exposes `name`, `difficulty`, `sequenceOrder`, `isLocked`, `unitId`, `skillId`, `isBoss`.
- **Traces to:** AC1.

### BE-TC-03 — QuickCheck shape: id + questionType + questionText + options present  **[shipped: P205-C02]**
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. GET demo lesson; inspect `data.quickCheck`.
- **Expected:** `quickCheck` carries exactly `id`, `questionType`, `questionText`, `options` (4-option JSON array string). No extra answer-bearing field.
- **Traces to:** AC1.

### BE-TC-04 — `options` is the lesson's first QuizQuestion by Id ASC
- **Type:** functional · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** demo lesson with exactly one seeded question (so "first" is unambiguous); resolve the expected question id via DB by `LessonId` ordered by `Id`.
- **Steps:** 1. GET demo lesson; 2. read `quickCheck.id`; 3. query DB for `min(Id)` QuizQuestion where `LessonId == demoLessonId`.
- **Expected:** `quickCheck.id` == DB min-Id question id (Q2 first-by-Id rule).
- **Traces to:** AC1 / brief Q2.

### BE-TC-05 — Non-demo lesson → 200 with explanation/visual/quickCheck all null  **[shipped: P205-C05]**
- **Type:** boundary / state(empty) · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** `"Place Value: Tens and Hundreds (G1)"` (no content, no questions). Must be in the **same en-medium tree** so the language guard passes.
- **Steps:** 1. `GET api/learning/Lessons/{nonDemoLessonId}` with en-student JWT.
- **Expected:** **200**, `successed=true`; `data.explanation==null`, `data.visual==null`, `data.quickCheck==null` (keys present, values JSON null). Content-absence is not an error.
- **Traces to:** AC1 (graceful null) / AC4.

### BE-TC-06 — `CorrectAnswer` NEVER appears in the open-lesson response (deep walk)  **[shipped: P205-C03]**
- **Type:** auth-authz / regression · **Priority:** P0 · **Agent:** api-tester
- **Steps:** 1. GET demo lesson (has a quick-check); 2. lowercase the raw body, assert it does NOT contain `"correctanswer"`; 3. recursively walk the JSON document and assert no object key equals `correctAnswer` (case-insensitive) at any depth.
- **Expected:** No `correctAnswer` key anywhere. (`QuizQuestionDto` has no such member; `QuizProfile` excludes it.)
- **Traces to:** AC1 security guard / brief Q4.

### BE-TC-07 — Back-compat query-string route returns same assembly DTO  **[shipped: P205-C07]**
- **Type:** functional / regression · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. `GET api/learning/Lessons?id={demoLessonId}` with en-student JWT.
- **Expected:** **200**, `successed=true`; `explanation`/`visual`/`quickCheck` populated identically to BE-TC-02 (same handler). Confirms P8-SEC-2 `[Authorize]` is on the old route too.
- **Traces to:** AC1 / brief Q10.

### BE-TC-08 — Re-open idempotency: two GETs are field-identical (no state mutation)
- **Type:** persistence / state · **Priority:** P2 · **Agent:** api-tester
- **Steps:** 1. GET demo lesson twice with the same JWT, no intervening writes.
- **Expected:** Both responses have identical `explanation`, `visual`, `quickCheck.id`, `quickCheck.questionText`. Open is read-only — it must NOT create an attempt (Q7). Optionally assert `db.Attempts` count for that student/lesson is 0 after two GETs.
- **Traces to:** AC1 / brief Q7 (open does not auto-create attempt).

### BE-TC-09 — Non-existent lesson id → 404 (not 500)  **[shipped: P205-C04]**
- **Type:** negative · **Priority:** P0 · **Agent:** api-tester
- **Steps:** 1. `GET api/learning/Lessons/999999` with Student JWT.
- **Expected:** **404** (NOT 500), `successed=false`. Confirms `NotFound(LessonNotFound)` path + the Q12 `ex.Message`-leak fix (message is the localized key text, not an exception message).
- **Traces to:** AC1 (404 path).

### BE-TC-10 — Open accepts a Parent JWT (any authenticated role), not just Student
- **Type:** auth-authz · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** register a Parent, sign in, use the parent token directly (do not add a child).
- **Steps:** 1. `GET api/learning/Lessons/{demoLessonId}` with the **Parent** JWT.
- **Expected:** **NOT 401** — the action is `[Authorize]` (any role), so a parent is authenticated. Accept **200** if the parent passes the language guard, or **403** if the parent's (absent) `learning_language` claim resolves to Ar and the demo lesson is an En lesson. Assert `statusCode ∈ {200, 403}` and explicitly `!= 401` and `!= 403`-due-to-role. (See README Q5 — if parents lack the claim, the 403 is the language guard, not an authz failure.)
- **Traces to:** brief Q5 (Authorize = any authenticated user).

---

## Group B — Learning-language guard (P8) — the CRITICAL story guard

### BE-TC-11 — Open a wrong-language lesson by direct id → 403 (NEW — closes shipped gap)
- **Type:** auth-authz / negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** an **`en`-medium** student (`LearningLanguage="en"`). Resolve a lesson in the **Ar-medium** MATH (or SCIENCE) tree: `db.Subjects.First(SubjectCode==MATH && Language==Ar && Grade.Number==1)` → its unit's lesson; OR any lesson whose owning subject language != the student's resolved language for that SubjectCode.
- **Steps:** 1. `GET api/learning/Lessons/{arTreeLessonId}` with the en-student JWT.
- **Expected:** **403 Forbidden**, `successed=false`, message = `LessonLanguageMismatch`. The handler walks Lesson→Unit→Subject and rejects the cross-language read. **Not** 200, **not** 404.
- **Traces to:** P8-03-BE-4 language guard (dispatch CRITICAL) / AC1.

### BE-TC-12 — Start an attempt on a wrong-language lesson → 403 (NEW)
- **Type:** auth-authz / negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** same en-student; same Ar-tree MATH/SCIENCE lesson id as BE-TC-11.
- **Steps:** 1. `POST api/learning/Quizzes/{arTreeLessonId}/Attempt` with the en-student JWT.
- **Expected:** **403 Forbidden**, message `LessonLanguageMismatch`. `StartAttemptCommandHandler` enforces the same guard **before** creating an attempt; assert no `Attempt` row is created for that student/lesson (DB count 0).
- **Traces to:** P8-03-BE-4 guard at start / AC3.

### BE-TC-13 — Start-attempt with a non-positive lessonId → 422 (validation)
- **Type:** validation / boundary · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** Student JWT.
- **Steps:** 1. `POST api/learning/Quizzes/0/Attempt` (and a second call with a negative route value if the `int` route binds it) with the Student JWT.
- **Expected:** **422 UnprocessableEntity** — `StartAttemptValidation` requires `LessonId > 0` and `StartAttemptCommand` is an `ICommand`, so `ValidationBehavior` throws `ValidationException` → middleware maps to 422 with `errors[]`. (Note: a route `{lessonId}` of `0` reaches the command; confirm the route accepts it. If the framework 404s a `0` route before binding, document and use the smallest value that binds.)
- **Traces to:** validation→422 on `ICommand`.

---

## Group C — Start attempt: creation, persistence, resume, IDOR

### BE-TC-14 — Start attempt on demo lesson creates a persisted attempt + returns questions  **[shipped: P205-C08 step 2]**
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** api-tester
- **Steps:** 1. `POST api/learning/Quizzes/{demoLessonId}/Attempt` with en-student JWT; 2. capture `data.attemptId`; 3. query DB for that `Attempt`.
- **Expected:** **200**, `successed=true`; `data.attemptId>0`; `data.questions` is a non-empty list with `QuizQuestionDto` shape (and **no** `correctAnswer`). DB has an `Attempt` row with `Status=InProgress`, `StudentId` == JWT student, `LessonId` == demo lesson. Proves the attempt is persisted + retrievable.
- **Traces to:** AC3 (start creates attempt).

### BE-TC-15 — Start-attempt questions carry NO correctAnswer
- **Type:** auth-authz / regression · **Priority:** P0 · **Agent:** api-tester
- **Steps:** 1. start attempt on demo lesson; 2. deep-walk the response JSON.
- **Expected:** No `correctAnswer` key anywhere in `data.questions[*]`.
- **Traces to:** AC3 / security.

### BE-TC-16 — Re-start in-progress attempt resumes the SAME attempt (idempotent, no duplicate)
- **Type:** persistence / state · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. start attempt → capture `attemptId1`; 2. start attempt again on the same lesson with the same JWT (no completion in between) → capture `attemptId2`.
- **Expected:** `attemptId2 == attemptId1`; response `message` reflects "resumed" (`AttemptResumedSuccessfully`); DB has exactly **one** in-progress `Attempt` for that student+lesson (no duplicate row).
- **Traces to:** AC3 idempotency / brief "resume in-progress" semantics.

### BE-TC-17 — Submit/complete another student's attempt → 401 (IDOR guard)
- **Type:** auth-authz / negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** student A starts an attempt (capture `attemptId`, `questionId`); create an independent student B.
- **Steps:** 1. as **B**, `POST api/learning/Quizzes/{attemptId}/Answers` with a valid body; 2. as **B**, `POST api/learning/Quizzes/{attemptId}/Complete`.
- **Expected:** both return **401 Unauthorized** (handler: `attempt.StudentId != studentId` → `Unauthorized`). Assert A's attempt is untouched (still InProgress, no B-authored `StudentAnswer`). Note: code returns **401**, not 403/404 — pin this (README Q3).
- **Traces to:** IDOR / cross-user access.

### BE-TC-18 — Start attempt on a LOCKED lesson — KNOWN GAP characterization
- **Type:** negative / regression · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** a lesson that the engine treats as `Locked` for a fresh student (e.g. a non-root lesson with an unmet prerequisite), in the student's own language tree.
- **Steps:** 1. `POST api/learning/Quizzes/{lockedLessonId}/Attempt` with a fresh student JWT.
- **Expected (CURRENT behavior — characterization):** **200** + an attempt is created. `StartAttemptCommandHandler` does NOT enforce lock/NodeState (brief R3). **Mark this test "KNOWN GAP — R3, P2-06 hardening follow-up".** Do not assert a 403/424 unless/until the hardening lands (README Q1). The test pins today's contract so the gap is visible and the follow-up has a baseline.
- **Traces to:** R3 (lock not enforced at StartAttempt).

### BE-TC-19 — Start attempt on a non-existent lesson → 404
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. `POST api/learning/Quizzes/999999/Attempt` with Student JWT.
- **Expected:** **404**, message `LessonNotFound`. No attempt created.
- **Traces to:** AC3 (start guards).

---

## Group D — Complete the quick check: records progress + event contract (AC3)

### BE-TC-20 — Full happy loop: open → start → submit correct → complete → Status=Completed  **[shipped: P205-C08]**
- **Type:** functional (end-to-end) · **Priority:** P0 · **Agent:** api-tester
- **Steps:** 1. GET demo lesson → capture `quickCheck.id`; 2. start attempt → `attemptId`; 3. `POST .../{attemptId}/Answers` with `{ QuestionId, AnswerPayload="\"6\"", TimeSpentSeconds=5, HintUsed=false }` → `data.isCorrect=true`; 4. `POST .../{attemptId}/Complete` → `data.status="Completed"`.
- **Expected:** every step **200**; complete response `successed=true`, `data.status="Completed"`. DB `Attempt.Status==Completed`.
- **Traces to:** AC3.

### BE-TC-21 — Completion marks the lesson progress signal the engine reads  **[shipped: P205-C08 steps 6-7]**
- **Type:** persistence / functional · **Priority:** P0 · **Agent:** api-tester
- **Steps:** continue BE-TC-20; 1. query DB completed lesson ids for the student in the subject (join Attempt→Lesson→Unit by `Status=Completed`); 2. `GET api/learning/Subjects/{subjectId}/Lessons` with same JWT and locate the demo lesson node.
- **Expected:** the demo lesson id is in the completed set; the lesson's `state == NodeState.Completed` (== 2). Proves the lesson-progress signal is consumed by `LearningPathEngine`.
- **Traces to:** AC3 (marks lesson progress) / FR-LR-2.

### BE-TC-22 — Owner-only completion: only the attempt's student can complete it
- **Type:** auth-authz · **Priority:** P1 · **Agent:** api-tester
- **Steps:** see BE-TC-17 complete leg — assert student B's Complete on A's attempt → **401** and A's attempt remains InProgress until A completes it.
- **Traces to:** AC3 / IDOR (consolidated with BE-TC-17; keep as an explicit completion-leg assertion).

### BE-TC-23 — Wrong answer still completes the lesson (completion ≠ mastery)  **[shipped: P205-C10]**
- **Type:** functional / boundary · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. open→start; 2. submit WRONG answer `"\"4\""` → `data.isCorrect=false`, `data.correctAnswer` non-null (returned only when wrong); 3. complete → `data.status="Completed"`.
- **Expected:** submit **200** `isCorrect=false` with non-null `correctAnswer`; complete **200** `status="Completed"`; DB `Attempt.Status==Completed`, `AccuracyPercentage==0`. Accuracy does not gate completion.
- **Traces to:** AC3 (records attempt regardless of accuracy).

### BE-TC-24 — Completion publishes `LessonCompletedIntegrationEvent` (lesson-side contract only)  **[shipped: P205-C09]**
- **Type:** functional / integration-contract · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** demo lesson has a `SkillId` (event is skipped when null); use the event-capturing fork client.
- **Steps:** 1. open→start→submit correct; 2. clear capture bag; 3. complete.
- **Expected:** exactly **one** `LessonCompletedIntegrationEvent` captured with `StudentId`==JWT, `LessonId`==demo lesson, `SkillId>0`, `CorrectAnswerCount==1`, `AccuracyPercentage∈[0,100]`, non-empty `EventId`, recent `OccurredOnUtc`. **Do NOT assert gamification internals** (XP/badges) — only the lesson-side publish contract.
- **Traces to:** AC3 cross-module contract (Shared.Contracts).

---

## Group E — Validation, state guards, idempotency, malformed body

### BE-TC-25 — Submit-answer with empty AnswerPayload → 422 (validation)
- **Type:** validation · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** active in-progress attempt + valid questionId.
- **Steps:** 1. `POST .../{attemptId}/Answers` with `{ QuestionId, AnswerPayload="", TimeSpentSeconds=5 }`.
- **Expected:** **422** — `SubmitAnswerValidation` requires `AnswerPayload` not-empty (also covers `QuestionId>0`, `AttemptId>0`, `TimeSpentSeconds∈[0,3600]`). Body has `errors[]`. No `StudentAnswer` row written.
- **Traces to:** validation→422 on `ICommand`.

### BE-TC-26 — Submit-answer with TimeSpentSeconds > 3600 → 422 (boundary)
- **Type:** boundary / validation · **Priority:** P2 · **Agent:** api-tester
- **Steps:** 1. submit a valid answer body but `TimeSpentSeconds=99999`.
- **Expected:** **422** (`TimeSpentSecondsExceedsMaximum`). Confirms the upper-bound ceiling. (Also worth a `-1` case → 422 `MustBeNonNegative`.)
- **Traces to:** validation boundary.

### BE-TC-27 — Submit to a question from a different lesson → 404 (cross-lesson injection guard)
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** attempt on demo lesson A; a questionId that belongs to a DIFFERENT lesson B.
- **Steps:** 1. `POST .../{attemptIdForA}/Answers` with `{ QuestionId = questionOfLessonB }`.
- **Expected:** **404** `QuestionNotFound` (handler enforces `q.LessonId == attempt.LessonId`). No answer recorded.
- **Traces to:** AC3 integrity / cross-lesson injection.

### BE-TC-28 — Re-answer the same question in one attempt → 424 (re-answer guard)
- **Type:** negative / state · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. start attempt; 2. submit answer for `questionId` (200); 3. submit again for the SAME `questionId`.
- **Expected:** second submit → **424 FailedDependency** `QuestionAlreadyAnswered`. Only one `StudentAnswer` row exists.
- **Traces to:** AC3 idempotency / re-answer guard.

### BE-TC-29 — Submit/complete on a non-in-progress (completed or abandoned) attempt → 424
- **Type:** negative / state · **Priority:** P1 · **Agent:** api-tester
- **Steps:** (a) complete an attempt, then `POST .../{attemptId}/Answers` → expect **424** `AttemptNotInProgress`. (b) abandon a fresh attempt (`POST .../Abandon`), then `POST .../{attemptId}/Complete` → expect **424** `AttemptAlreadyAbandoned`.
- **Expected:** both **424 FailedDependency**. State guards reject mutation of terminal attempts.
- **Traces to:** AC3 state integrity.

### BE-TC-30 — Re-complete an already-Completed attempt is idempotent (200, snapshot, no second event)
- **Type:** persistence / state · **Priority:** P1 · **Agent:** api-tester
- **Preconditions:** event-capturing fork client; an attempt completed once.
- **Steps:** 1. complete attempt (event fires, capture bag has 1); 2. clear bag; 3. complete the SAME attempt again.
- **Expected:** second complete → **200**, `data.status="Completed"`, same aggregates (TotalAnswers/CorrectAnswers unchanged); capture bag has **0** new events (no re-publish on idempotent re-complete).
- **Traces to:** AC3 idempotency.

### BE-TC-31 — Seeder smoke: exactly the 4 demo lessons carry content + a question  **[shipped: P205-C11]**
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. query DB: count lessons with `Explanation!=null && Visual!=null` ≥ 4; 2. for each of the 4 named demo lessons (Math/Science/Arabic/English G1 roots — note the Arabic lesson name is Arabic-script in the bilingual seeder) assert non-null Explanation+Visual and ≥1 QuizQuestion.
- **Expected:** ≥4 content lessons; each named demo lesson has content + ≥1 question. Re-running the seeder adds zero rows (idempotent).
- **Traces to:** AC4 (Phase-2 seeded static content; column shape neutral).

---

## Group F — Product-decision & contract negatives (CRUD authz, malformed body)

### BE-TC-32 — Lesson CRUD is AdminOnly: a Student cannot create a lesson → 403
- **Type:** auth-authz / negative · **Priority:** P1 · **Agent:** api-tester
- **Steps:** 1. `POST api/learning/Lessons/Create` with a valid `AddLessonCommand` body using a **Student** JWT.
- **Expected:** **403 Forbidden** (policy `AdminOnly`). Enforces "no teacher role / students don't author content". (Also spot-check `PUT Update` and `DELETE` → 403 for Student.)
- **Traces to:** product decisions (no teacher role; parent-driven onboarding; no student authoring).

### BE-TC-33 — Lesson CRUD anonymous → 401
- **Type:** auth-authz / negative · **Priority:** P2 · **Agent:** api-tester
- **Steps:** 1. `POST api/learning/Lessons/Create` (and `PUT Update`, `DELETE`) with no token.
- **Expected:** **401** for each. No content mutation possible unauthenticated.
- **Traces to:** authz hardening.

### BE-TC-34 — Malformed JSON body to submit-answer → 400 (not 500)
- **Type:** negative / boundary · **Priority:** P2 · **Agent:** api-tester
- **Preconditions:** active attempt; Student JWT.
- **Steps:** 1. `POST api/learning/Quizzes/{attemptId}/Answers` with a raw body of `"{ not json"` and `Content-Type: application/json`.
- **Expected:** **400 BadRequest** (middleware maps `ArgumentNullException(request)` from the null-bound command). Must NOT be 500. No `StudentAnswer` written.
- **Traces to:** robustness / status mapping.

---

## Coverage summary

| Priority | Cases |
|---|---|
| **P0** | BE-TC-01, 02, 06, 09, 11, 12, 14, 15, 17, 20, 21 (11) |
| **P1** | BE-TC-03, 05, 07, 13, 16, 18, 19, 22, 23, 24, 25, 27, 28, 29, 30, 31, 32 (17) |
| **P2** | BE-TC-04, 08, 10, 26, 33, 34 (6) |

> Total **34** cases. (README headline counts roll the consolidated IDOR/state cases into P0/P1/P2 buckets; the authoritative per-case priority is the table above.)

Every backend acceptance criterion (AC1, AC3, AC4) maps to ≥1 P0/P1 case; AC2 is frontend (out of scope). Known production gap R3 is pinned by BE-TC-18 as a characterization test pending the P2-06 hardening follow-up.
