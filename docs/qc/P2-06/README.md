# QC Test Plan & Coverage Report — P2-06 (Take a quiz, 4 question types) — BACKEND ONLY

- **Story:** [user-stories/Phase-2-Learning-Core/P2-06-take-a-quiz.md](../../../user-stories/Phase-2-Learning-Core/P2-06-take-a-quiz.md)
- **Brief / Plan:** [docs/briefs/P2-06.md](../../briefs/P2-06.md) · [docs/plans/P2-06.md](../../plans/P2-06.md)
- **Task file:** [tasks/Backend/Phase-2-Learning-Core/P2-06-BE.md](../../../tasks/Backend/Phase-2-Learning-Core/P2-06-BE.md)
- **Scope of this pass:** Backend API surface only. No `frontend-test-cases.md` (FE is P2-06-FE, separate story/stack).
- **Design owner:** qc-test-designer (Opus). Design only — no test code, no execution, no feature edits.

## 1. Summary

P2-06's quiz engine was implemented **inside the Learning module** (schema `learning`), **not** a separate `Assessment` module/schema — a deliberate "ask-before-new-modules" deviation recorded in the task file. **All routes are therefore `api/Learning/Quizzes/...`, not `api/Assessment/Quizzes/...`.** The brief/plan still describe the abandoned Assessment design; the test cases below target the **as-built** Learning surface verified from source.

The story's testable backend slice is the **start-quiz** endpoint plus its supporting entities. The controller also exposes SubmitAnswer / Complete / Abandon actions (those belong to P2-07/P2-08); they are covered here only as **regression/context smoke**, not as the P2-06 acceptance surface.

### Surface under test (verified from source)

| Action | Route | Auth | P2-06? |
|---|---|---|---|
| Start attempt | `POST /api/Learning/Quizzes/{lessonId}/Attempt` | `[Authorize(Roles="Student")]` | **Yes — primary** |
| Submit answer | `POST /api/Learning/Quizzes/{attemptId}/Answers` | `[Authorize(Roles="Student")]` | No (P2-07) — regression only |
| Complete attempt | `POST /api/Learning/Quizzes/{attemptId}/Complete` | `[Authorize(Roles="Student")]` | No (P2-08) — regression only |
| Abandon attempt | `POST /api/Learning/Quizzes/{attemptId}/Abandon` | `[Authorize(Roles="Student")]` | No (P2-08) — regression only |

### Confirmed facts (answer to the dispatch questions)

- **4th question type = `FillInBlank`.** Full enum: `QuestionType { MCQ=1, TrueFalse=2, Matching=3, FillInBlank=4 }` (`Domain/Enums/QuestionType.cs`).
- **Correct answer IS withheld from the start payload.** `QuizQuestionDto` has **no** `CorrectAnswer` field; `QuizProfile` explicitly `ForSourceMember(... CorrectAnswer ...).DoNotValidate()` and the DTO carries only `Id`, `QuestionType`, `QuestionText`, `Options`. This is a deliberate, commented security control — high-value to assert (FE-TC parity not in scope; BE asserts the JSON shape).
- **Start-quiz returns the question set** for the lesson: `StartAttemptResponse { AttemptId, List<QuizQuestionDto> Questions }`. Empty lesson → 200 with empty `Questions`.
- **Role gate is Student-only.** A parent/admin JWT is rejected by ASP.NET role authorization → **403** (not 401, since they are authenticated). Anonymous → **401**.
- **Cross-language guard returns 403.** The handler walks Lesson → Unit → Subject and compares `Subject.Language` to the student's resolved language (`learning_language` claim); mismatch → `Forbidden<T>()` → HTTP 403. The Learning `AppControllerBase.NewResult` default arm maps `HttpStatusCode.Forbidden` (and `UnprocessableEntity`/`FailedDependency`) via `(int)response.StatusCode`, so 403/422/424 surface correctly.
- **Resume, not duplicate.** Starting a quiz the student already has `InProgress` on the same lesson **resumes** (returns the existing `AttemptId` + questions, message `AttemptResumedSuccessfully`) instead of creating a second `Attempt`.
- **Persistence.** `IAttemptService.StartNewAsync` commits immediately (`Status=InProgress`, `StartedAt=UtcNow`, `StudentId` from JWT) so the DB-generated `Id` is returned. `Attempt`/`StudentAnswer` carry the full P2-07/P2-08 field set.
- **Status mapping:** `Unauthorized→401`, `NotFound→404`, `BusinessValidation→424 (FailedDependency)`, `UnprocessableEntity→422`, `Forbidden→403`, validation failures from `ValidationBehavior→422`. Envelope success flag spelled **`Successed`**.

### Counts

| Bucket | Count |
|---|---|
| **Total cases** | **24** |
| Backend (all) | 24 |
| Frontend | 0 (out of scope) |
| P0 | 11 |
| P1 | 9 |
| P2 | 4 |
| Blocked / not-testable-yet | 2 (BE-TC-23, BE-TC-24 — see notes) |

## 2. Coverage matrix (acceptance criterion → case IDs)

Story acceptance criteria (from the user story + the backend slice in the brief):

| Acceptance criterion | Covered by | Verdict |
|---|---|---|
| AC-S1: Quiz supports MCQ, True/False, Matching, Fill-in-the-blank | BE-TC-05, BE-TC-06 (all 4 types render in payload with per-type `Options` shape) | Covered |
| AC-S2: Starting a quiz creates an Attempt | BE-TC-01, BE-TC-13 (persistence), BE-TC-15 (resume = no duplicate) | Covered |
| AC-S3 (brief AC-4): `POST .../{lessonId}/Attempt` returns `attemptId` in `BaseResponse<T>` | BE-TC-01, BE-TC-02 (envelope shape, `Successed`) | Covered |
| AC-S4: QuizQuestion / Attempt / StudentAnswer persist the session | BE-TC-13, BE-TC-14 (DB assertions) | Covered |
| Brief AC: question DTO never leaks `CorrectAnswer` | BE-TC-07 (P0 — payload has no CorrectAnswer field) | Covered |
| Brief AC: StudentId from JWT, never client-supplied | BE-TC-08, BE-TC-13 (StudentId in row == JWT user) | Covered |
| Role gate (Student-only) | BE-TC-03 (Student 200), BE-TC-09 (Parent 403), BE-TC-10 (Admin 403), BE-TC-11 (anonymous 401) | Covered |
| Cross-language guard (P8) | BE-TC-12 (mismatch 403), BE-TC-04 (match 200) | Covered |
| Invalid / missing lesson | BE-TC-16 (lesson not found 404), BE-TC-17/18/19 (validation 422) | Covered |
| Empty lesson (no questions) | BE-TC-20 (200 + empty list) | Covered |
| Product overrides (no Social Studies / no teacher role / no self-register) | BE-TC-21 (no teacher role → no Teacher JWT can start), BE-TC-22 (4-subject scope, context) | Covered (negative) |
| Locked-skill rejection (P2-04, hinted in dispatch) | BE-TC-23 | **GAP — not implemented** (see Risk + Open Q1) |
| Concurrency: two simultaneous starts → one Attempt | BE-TC-24 | **Blocked** — race-condition design note (see Open Q2) |

**Coverage verdict:** Every story acceptance criterion has at least one P0/P1 case. **One dispatch-hinted behavior (locked-skill rejection) is NOT implemented in the start-quiz path** — BE-TC-23 is written as an expected-behavior probe and flagged as a likely product gap for the lead, not a pass. No silently-dropped criteria.

## 3. Risk notes (where cases are weighted)

1. **Answer leakage (highest).** The entire quiz's integrity hinges on `CorrectAnswer` never reaching the client in the start payload. One mapping regression breaks it silently. BE-TC-07 is P0 and asserts on the raw JSON (no `correctAnswer` key, case-insensitive) for every type — not just object-shape.
2. **Role/authz boundary.** Student-only is enforced by `[Authorize(Roles="Student")]`. The risky case is Parent/Admin getting **403 not 401** (they are authenticated) and anonymous getting **401**. Mis-seeding role claims is the common test defect — BE-TC-03/09/10/11 pin all four principals.
3. **Cross-tenant / cross-child isolation.** StudentId is JWT-derived, so a start cannot be made on behalf of another child via this endpoint. The resume guard filters by `StudentId == currentUser`. BE-TC-08/13/15 lock this; the SubmitAnswer ownership guard (regression context) is noted but not re-tested here.
4. **Cross-language tree (P8 guard).** Starting an attempt in the wrong-language subject tree must 403. Default-Arabic fallback on a missing `learning_language` claim is a subtle path — BE-TC-12 needs the seed Subject's `Language` to actually differ from the student's resolved language.
5. **Resume vs duplicate.** Re-starting must not spawn a second InProgress Attempt. BE-TC-15 asserts the returned `AttemptId` is identical and only one row exists.
6. **Locked-skill gap.** Dispatch expected a P2-04 lock guard; the code has none. Weighted as an open question, not a failing assertion, to avoid a false defect.

## 4. Open questions / assumptions (lead decisions needed before implementation)

1. **Locked-skill rejection (BE-TC-23).** The dispatch brief and P2-04 imply that starting a quiz on a **locked** skill/lesson must be rejected. `StartAttemptCommandHandler` performs **no lock check** — it validates lesson existence + language only. **Is this an intended P2-06 gap (lock enforced elsewhere / later), or a missing guard?** Until the lead rules, BE-TC-23 is a probe documenting current behavior (start succeeds on a "locked" lesson) and flags the discrepancy.
2. **Concurrency (BE-TC-24).** The resume guard reads InProgress then writes a new Attempt with no DB unique constraint on `(StudentId, LessonId, Status=InProgress)`. Two near-simultaneous starts could create two InProgress attempts. **Is a unique index / serialization required?** Marked Blocked pending a design decision; do not implement the race test until the lead confirms the intended contract.
3. **`{lessonId}` semantics confirmed.** Route param is a **LessonId** (no `Quiz` entity). Questions are grouped by `LessonId`. Tests seed questions with the matching `LessonId`. Confirm no `Quiz` resource is expected.
4. **Module/route reality.** Confirm the as-built `api/Learning/Quizzes/...` routes are the contract of record (brief still says `api/Assessment/...`). `api-tester` must hit the Learning routes; the brief's Assessment paths will 404.
5. **Resume message contract.** Resume returns `Successed=true` + message `AttemptResumedSuccessfully` (HTTP 200), not a 409/Created. Confirm the FE/contract expects 200-resume rather than a distinct status.

## 5. Handoff

- **`backend-test-cases.md` → `api-tester`.** Implement all 24 cases as integration tests against the running API (Learning routes). Seed via API where possible; seed quiz questions + lesson/subject language directly in `learning` schema where no admin endpoint exists (note the seed method used).
- **`execution-report.md`** — template scaffolded here (empty results). `api-tester` fills pass/fail per case + defects after running. **qc-test-designer does not fill results.**
- **No `frontend-test-cases.md`** — backend-only pass by request.
- **Before implementing:** get lead rulings on Open Q1 (locked-skill) and Q2 (concurrency); BE-TC-23/24 stay blocked until then.

---

Test cases ready — `api-tester` to implement `backend-test-cases.md`; results go into `execution-report.md`. (No frontend pass this run.)
