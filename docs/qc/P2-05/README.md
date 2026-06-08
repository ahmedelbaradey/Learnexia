# QC Test Plan & Coverage Report — P2-05 "Open & complete a lesson" (BACKEND ONLY)

> **Run scope:** Backend API surface only. No frontend test cases (`frontend-test-cases.md` intentionally omitted — the lesson screen UI is `P2-05-FE`, out of scope).
> **Designed by:** `qc-test-designer` (Opus). Design-only — no executable test code, no runs, no feature edits.
> **Story status when designed:** `P2-05-BE` already merged to `main` (audited 2026-06-07). An 11-case integration test already exists at `backend/tests/Learnexia.IntegrationTests/P2_05_OpenAndCompleteLesson_Tests.cs`. **This catalog supersedes and extends it** — it re-traces the existing coverage and adds the gaps the shipped test does not cover (cross-language 403 guard, AdminOnly authz on CRUD, validation→422 on the attempt commands, IDOR ownership guards, attempt idempotency/resume, the R3 locked-lesson gap).

---

## 1. Summary

| Item | Value |
|---|---|
| Story | P2-05 — Open & complete a lesson (FR-LR-4) |
| Batch | Full backend surface (single run) |
| Surfaces under test | `LessonsController` (open/back-compat/CRUD) + `QuizzesController` (start / submit / complete / abandon) |
| Target agent | `api-tester` (all cases) |
| Total cases | **34** |
| By priority | **P0: 18 · P1: 12 · P2: 4** |
| Frontend cases | 0 (out of scope this run) |

### Endpoints in scope

| Method + route | Authz | Handler | Notes |
|---|---|---|---|
| `GET /api/learning/Lessons/{id:int}` | `[Authorize]` | `GetLessonQueryHandler` | **Open lesson** — assembly DTO `{ name, difficulty, sequenceOrder, isLocked, unitId, skillId, explanation, visual, isBoss, quickCheck }`. Query — **not** FluentValidation-validated. Has the **P8 language guard** (403). |
| `GET /api/learning/Lessons?id={id}` | `[Authorize]` (P8-SEC-2) | `GetLessonQueryHandler` | Back-compat query-string route, same handler, same guard. |
| `POST /api/learning/Quizzes/{lessonId}/Attempt` | `[Authorize(Roles="Student")]` | `StartAttemptCommandHandler` | **Start attempt** — creates/resumes; persisted immediately; language guard (403); resume on existing in-progress. |
| `POST /api/learning/Quizzes/{attemptId}/Answers` | `[Authorize(Roles="Student")]` | `SubmitAnswerCommandHandler` | **Submit answer** — body validated (422); ownership guard; state guard; re-answer guard. |
| `POST /api/learning/Quizzes/{attemptId}/Complete` | `[Authorize(Roles="Student")]` | `CompleteAttemptCommandHandler` | **Complete attempt** — records progress, publishes `LessonCompletedIntegrationEvent`, idempotent. |
| `POST /api/learning/Quizzes/{attemptId}/Abandon` | `[Authorize(Roles="Student")]` | `AbandonAttemptCommandHandler` | Terminal alternative — relevant only for negative state-guard coverage. |
| `POST/PUT/DELETE /api/learning/Lessons/*` | `[Authorize(Policy=AdminOnly)]` | CRUD | Product-decision negative coverage (no teacher/student authoring). |

### Verified status-code mapping (ground truth — read from `BaseResponseHandler` + `ErrorHandlerMiddleWare`)

| Handler outcome | HTTP | Where |
|---|---|---|
| `Success` | 200 | `BaseResponseHandler.Success` |
| `NotFound` | 404 | `BaseResponseHandler.NotFound` |
| `Unauthorized` | 401 | `BaseResponseHandler.Unauthorized` |
| `Forbidden` | 403 | `BaseResponseHandler.Forbidden` (language guard) |
| `BusinessValidation` | **424 FailedDependency** | `BaseResponseHandler.BusinessValidation` (e.g. answer to non-in-progress attempt, re-answer, complete-abandoned) |
| `ServerError` | 500 | `BaseResponseHandler.ServerError` — never with `ex.Message` (Q12 fixed) |
| FluentValidation failure on `ICommand` | **422 UnprocessableEntity** | `ErrorHandlerMiddleWare` catch `ValidationException` |
| Malformed JSON body | 400 BadRequest | `ErrorHandlerMiddleWare` catch `ArgumentNullException(request)` |
| Missing/insufficient role → `[Authorize]`/policy | 401 (no token) / 403 (wrong role) | ASP.NET pipeline, before handler |

**Load-bearing:** envelope flag is camelCase `"successed":` (single-s, sic). All asserts use the existing `TryProp` case-insensitive helper.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria are taken verbatim from the story + the brief's testable expansion (AC1, AC3, AC4 are backend; AC2 is frontend, out of scope).

| AC | Criterion (backend portion) | Case IDs | Verdict |
|---|---|---|---|
| **AC1** | Lesson assembly endpoint returns explanation + visual + embedded quick-check | BE-TC-02, BE-TC-03, BE-TC-04, BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-08 | ✅ Covered |
| **AC1 (sec)** | Quick-check never carries `CorrectAnswer` | BE-TC-06 | ✅ Covered |
| **AC1 (404)** | Missing lesson id → 404 (not 500) | BE-TC-09 | ✅ Covered |
| **AC1 (authz)** | Open requires auth | BE-TC-01, BE-TC-10 | ✅ Covered |
| **AC1 (P8 guard)** | Wrong-language lesson via direct id → 403 | BE-TC-11, BE-TC-12 | ✅ Covered |
| **AC3** | Completing the quick check records an attempt + marks lesson progress | BE-TC-20, BE-TC-21, BE-TC-22, BE-TC-23, BE-TC-27, BE-TC-30 | ✅ Covered |
| **AC3 (start)** | Start-attempt creates a persisted, retrievable attempt | BE-TC-14, BE-TC-15, BE-TC-19 | ✅ Covered |
| **AC3 (idempotency)** | Re-open / resume / re-complete are idempotent | BE-TC-16, BE-TC-28, BE-TC-08 | ✅ Covered |
| **AC3 (event contract)** | Lesson-side completion publishes `LessonCompletedIntegrationEvent` | BE-TC-24 | ✅ Covered (lesson-side contract only; gamification internals not asserted) |
| **AC4** | Phase-2 seeded/static explanation; column shape neutral | BE-TC-31 | ✅ Covered |
| **AC2** | Lesson screen visuals (tutor bubble / hearts / streak) | — | ⛔ Out of scope (frontend P2-05-FE) |

**Verdict: every backend acceptance criterion has at least one P0/P1 case. No gaps.**

Additional product-decision & hardening coverage beyond the ACs:

| Concern | Case IDs |
|---|---|
| No teacher / no student authoring (CRUD is AdminOnly) | BE-TC-32, BE-TC-33 |
| Validation → 422 on attempt commands (start/submit/complete) | BE-TC-13, BE-TC-25, BE-TC-26 |
| IDOR / cross-student attempt access | BE-TC-17, BE-TC-22 |
| Start a **locked** lesson is currently NOT rejected (R3 documented gap) | BE-TC-18 |
| Submit / complete state guards (424) | BE-TC-25, BE-TC-29 |
| Malformed body → 400 | BE-TC-34 |

---

## 3. Risk notes (where the cases are weighted, and why)

1. **`CorrectAnswer` leak (highest weight).** The quick-check is the first place a lesson's answer could leak. The DTO reuses `QuizQuestionDto` (which structurally has no `CorrectAnswer` member), and `QuizProfile` excludes it on the map — but a regression in either the DTO or the profile would silently expose answers. BE-TC-06 does a **deep recursive JSON walk** (not just a substring check) and is **P0**.

2. **P8 learning-language guard (high weight, under-tested by the shipped suite).** `GetLessonQueryHandler` and `StartAttemptCommandHandler` both walk Lesson → Unit → Subject and 403 on a language mismatch. The existing 11-case test only exercises the *happy* (en-student → MATH/En) path; it never asserts the **403 reject** path. This is the story's CRITICAL guard per the dispatch. BE-TC-11 (open) and BE-TC-12 (start) close that gap as **P0**. The resolver pins ARABIC→Ar and ENGLISH→En regardless of learner language, and lets MATH/SCIENCE follow the learner — BE-TC-12 must pick a mismatchable pair (e.g. an `ar`-medium student hitting a MATH/En lesson, or any student hitting the opposite-language Arabic/English tree).

3. **Locked-lesson bypass (R3, documented production gap).** `StartAttemptCommandHandler` checks lesson existence + language but **NOT** `IsLocked` / engine-derived `NodeState`. A student can start an attempt on a locked lesson by calling the endpoint directly. BE-TC-18 **documents current behavior** (expects 200 today) and is flagged as a **known gap / characterization test** — it is NOT a pass/fail of intended security, it pins the current contract so the P2-06 hardening follow-up has a baseline. See Open Question Q1.

4. **IDOR on attempts (high weight).** Submit/Complete load the attempt by id and then compare `attempt.StudentId` to the JWT id, returning `Unauthorized` (401) on mismatch — note this is **401, not 403**, an intentional convention quirk worth pinning (BE-TC-17, BE-TC-22).

5. **Idempotency / resume semantics (medium).** Start resumes an in-progress attempt (same `AttemptId`, "resumed" message) rather than creating duplicates; Complete on an already-Completed attempt returns the snapshot without re-publishing the event. Both are correctness-critical for the FE retry/refresh UX.

6. **Validation 422 vs 424 vs 404 confusion (medium).** Three different "rejection" surfaces are easy to conflate: FluentValidation on the command body → **422**; business-state rejection → **424**; missing entity → **404**; the lesson-open *query* is NOT validated so a `0`/negative id flows to the handler and returns **404**, not 422. The cases assert the exact code.

---

## 4. Open questions / assumptions (need a lead decision before / during implementation)

- **Q1 (decision needed) — Locked-lesson behavior for BE-TC-18.** The brief's R3 + HANDOFF record that `StartAttempt` does **not** enforce lock state (a known P2-06 hardening follow-up). Should BE-TC-18 be authored as (a) a **characterization test** asserting today's 200 (so the gap is pinned and visible), or (b) a **skipped/blocked** test asserting the *intended* 403/424 once the hardening lands? **Default assumption: (a) characterization, marked "KNOWN GAP".** If the lead wants the hardening pulled into this QC pass, flip it to a blocked-pending test.

- **Q2 (assumption) — mismatchable language fixture for BE-TC-11/12.** The seeder seeds bilingual subject trees (MATH/En + MATH/Ar, plus ARABIC/Ar and ENGLISH/En). I assume a student created with `LearningLanguage="en"` resolves MATH→En, so a **MATH/Ar** lesson (or the opposite-medium Science tree) is a valid 403 fixture; and an `en`-student hitting the **ARABIC** subject's lessons is NOT a mismatch (ARABIC is pinned to Ar regardless). The api-tester must resolve the *Ar*-tree lesson id by `SubjectCode + Language=Ar` (mirrors the existing test's `FirstOrDefaultAsync(SubjectCode.MATH && Language==En && Grade==1)`). Confirm the Ar-tree demo lesson exists in the seed before authoring.

- **Q3 (assumption) — 401 vs 403 for IDOR.** Submit/Complete return **401 Unauthorized** (not 403) when the attempt belongs to another student (the handler calls `Unauthorized<T>`). BE-TC-17/22 assert **401** to match the shipped code. If the lead considers this a convention bug (cross-user access is arguably 403/404), raise it — but the test pins current behavior.

- **Q4 (assumption) — abandon is in-scope only as a negative-state fixture.** `Abandon` is not a P2-05 acceptance criterion; BE-TC-29 uses it solely to create an Abandoned attempt and prove `Complete` on it returns 424. No standalone abandon happy-path cases are designed (covered by P2-08's suite).

- **Q5 (assumption) — Parent role on lesson-open.** `GET Lessons/{id}` is `[Authorize]` (any authenticated role), so a Parent JWT should get 200 (subject to the language guard, which keys off the `learning_language` claim — parents may not carry one, falling back to Ar). BE-TC-10 asserts a Parent token is accepted (200/403, not 401). If parent tokens lack the claim and you want a strict assertion, see the note on BE-TC-10.

---

## 5. Handoff

- **`backend-test-cases.md` → `api-tester`.** All 34 cases are HTTP/integration cases for the running API (xUnit + Testcontainers Postgres + `LearnexiaWebAppFactory`). The shipped `P2_05_OpenAndCompleteLesson_Tests.cs` already implements equivalents of BE-TC-01..09 + 14 + 20..24 + 31; the api-tester should **extend that class (or add a sibling class)** to cover the new cases (language-guard 403, AdminOnly authz, validation 422, IDOR, resume/idempotency, state guards, locked-lesson characterization) rather than re-implement the existing ones. Each case maps 1:1 to a `[Fact]`.
- **No `frontend-test-cases.md`** this run — backend-only scope.
- **`execution-report.md`** — templated and empty in this folder. After the api-tester runs, it fills pass/fail per case ID + defects. `qc-test-designer` never fills results.

**Definition-of-done for this QC run:** folder `docs/qc/P2-05/` created with `README.md`, `backend-test-cases.md`, `execution-report.md`; 34 backend cases; every backend AC covered (no gaps); top open questions Q1–Q5 surfaced for the lead.
