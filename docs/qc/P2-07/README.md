# QC Test Plan & Coverage Report — P2-07 Instant Answer Feedback (Backend only)

> Designed by `qc-test-designer` (Opus). Design-only artifact. `api-tester` implements `backend-test-cases.md`; results land in `execution-report.md`. No frontend surface in scope for this run.

## 1. Summary

| Item | Value |
|---|---|
| Story | **P2-07 — Instant answer feedback** (`user-stories/Phase-2-Learning-Core/P2-07-instant-answer-feedback.md`) |
| FR-ID | FR-QZ-2 |
| Scope | **Backend API surface only.** `POST /api/Learning/Quizzes/{attemptId}/Answers` (SubmitAnswer) and the adjacent `POST .../Complete` (CompleteAttempt) for the event/persistence ACs. No FE. |
| Module | Learning (`learning` schema) |
| Endpoints under test | `POST /api/Learning/Quizzes/{attemptId}/Answers` · `POST /api/Learning/Quizzes/{attemptId}/Complete` |
| Auth | `[Authorize(Roles = "Student")]` on both (verified in `QuizzesController.cs`). `StudentId` is JWT-derived, never client-supplied. |
| Implementation state | **Already merged** (task file marks P2-07 ✅ Done; security audit PASS dated 2026-05-29). These cases are an independent QC verification + regression pass against the running API. |

### Case counts

| Surface | P0 | P1 | P2 | Total |
|---|---|---|---|---|
| Backend (`backend-test-cases.md`) | 16 | 9 | 4 | **29** |
| Frontend | — | — | — | 0 (out of scope) |
| **Total** | **16** | **9** | **4** | **29** |

By type: functional 6 · grading-per-type 5 · negative 2 · boundary 1 · validation 4 · auth-authz/IDOR 4 · security (no-leak / server-side grading) 3 · persistence 1 · event-emission 3.

## 2. Coverage matrix (every acceptance criterion → case IDs)

Story ACs (verbatim from the user story), plus the brief's testable refinements:

| # | Acceptance criterion | Covered by | Gap? |
|---|---|---|---|
| AC-1 | Correct answer → positive confirmation; recorded as correct | BE-TC-01, BE-TC-05, BE-TC-07, BE-TC-09, BE-TC-11, BE-TC-25 | No |
| AC-2 | Wrong answer → correct/wrong screen + hint affordance; (heart loss later) | BE-TC-02, BE-TC-06, BE-TC-08, BE-TC-10, BE-TC-12, BE-TC-13 | No |
| AC-3 | Feedback appears same-screen, no full reload | BE-TC-03 (synchronous 200 on the POST; envelope shape) | No (server-side: response is synchronous) |
| AC-4 | Result of each answer persisted for later analytics | BE-TC-04 (persistence), BE-TC-23/24 (`AnswerSubmittedIntegrationEvent`), BE-TC-26/27 (`LessonCompletedIntegrationEvent`) | No |

Security-audit-driven invariants (the heart of this run):

| Security invariant (from `docs/briefs/P2-07-security-audit.md`) | Covered by |
|---|---|
| Correct answer revealed ONLY in feedback AFTER submission — never leaked when `isCorrect=true` | BE-TC-01, BE-TC-14, BE-TC-15 |
| Grading is server-side — client cannot self-grade (payload/echo cannot flip the verdict) | BE-TC-16, BE-TC-17 |
| Grading correct for all 4 question types (MCQ / TrueFalse / FillInBlank / Matching) | BE-TC-05–BE-TC-13 |
| Student cannot submit against another student's attempt (IDOR) | BE-TC-18, BE-TC-19 |
| Role gate — Student only (no parent, no anonymous) | BE-TC-20, BE-TC-21, BE-TC-22 |
| Invalid / duplicate submission rejected; no event emitted on failure paths | BE-TC-23-NEG group, BE-TC-28, BE-TC-29 |
| No `ex.Message` / `CorrectAnswer` leak on error or in event payload | BE-TC-15, BE-TC-23, BE-TC-26 |

**Coverage verdict: every acceptance criterion has at least one P0/P1 case. No uncovered AC. No gap.**

## 3. Risk notes (where cases are weighted and why)

1. **Server-side grading authority (highest weight).** The whole story is "instant feedback the client must trust." The risk is a client self-grading or coercing `isCorrect`. The handler computes `isCorrect` via `AnswerComparator.AreEqual(...)` and overwrites the mapped row (`studentAnswer.IsCorrect = isCorrect`) — the command carries no `IsCorrect` field, but QC must prove a forged payload (extra `isCorrect`/`correctAnswer` keys in the JSON body) is ignored. → BE-TC-16, BE-TC-17.
2. **Correct-answer leak.** `CorrectAnswer` is returned only when `isCorrect=false` (`CorrectAnswer = isCorrect ? null : question.CorrectAnswer`). A regression that returns it on the correct path, or via the duplicate/error path, would hand students free answers. → BE-TC-01, BE-TC-14, BE-TC-15.
3. **Per-type grading correctness.** `TrueFalse` uses `bool.TryParse` (so `"yes"`, `"1"`, `""` must grade as wrong, not throw); `FillInBlank`/`MCQ` use trim + `OrdinalIgnoreCase`; `Matching` currently falls through to string compare (documented TODO). Each type needs a green + red + malformed case to lock behavior. → BE-TC-05–BE-TC-13.
4. **IDOR / cross-tenant.** Ownership guard returns **401** (`attempt.StudentId != studentId.Value`). Cross-lesson question injection returns **404** (same-lesson guard). Both must be proven to also emit **no** integration event. → BE-TC-18, BE-TC-19, BE-TC-23-NEG.
5. **Event emission integrity (AC-4 + downstream unblock).** Event must fire exactly once on success, skip silently when `SkillId` is null, never fire on any rejection path, never re-fire on idempotent re-Complete, and a throwing subscriber must not fail the 200. → BE-TC-23 through BE-TC-29.
6. **Validation boundary (422).** `SubmitAnswerCommand` is an `ICommand`, so `ValidationBehavior` runs: `AttemptId>0`, `QuestionId>0`, `AnswerPayload` not-empty, `TimeSpentSeconds ∈ [0,3600]`. The 3600 ceiling is a security control (prevents client-supplied timing inflation) and must be tested at the boundary. → BE-TC-30 group (BE-TC-30..33).

## 4. Open questions / assumptions (lead must confirm before/at implementation)

| # | Question / assumption | Default taken in the cases | Why it matters |
|---|---|---|---|
| OQ-1 | **IDOR returns 401, not 403/404.** The task ask said "submit against an attempt not owned → 403/404", but the code returns **401 Unauthorized** for ownership violation. | Cases assert **401** (the actual contract; matches P2-08 case 4). | If the product wants 403 (authenticated-but-forbidden) for IDOR, that is a contract change, not a test fix. Flagging, not silently asserting 403. |
| OQ-2 | **Non-existent vs unowned attempt both leak existence differently.** Missing attempt → 404; existing-but-unowned → 401. A 401-vs-404 difference is a mild enumeration oracle (a student can distinguish "attempt exists but isn't mine" from "no such attempt"). | Cases assert the as-built codes (404 / 401) and **flag** the oracle as an observation, not a failure. | Lead may want both to return 404 to avoid the oracle. Out of scope to change here; noted for the reviewer/security backlog. |
| OQ-3 | **No `Explanation` in the response.** The task title mentions "the right answer/explanation" but `SubmitAnswerResponse` has only `IsCorrect`, `CorrectAnswer?`, `HintAvailable` (no `Explanation`/`Hint`) — confirmed by brief Q2/Q3 (deferred to Phase 3). | Cases assert the right *answer* is returned (wrong path) and `HintAvailable=false`; **no** `Explanation` assertion. | Prevents a false-fail against a field that intentionally does not exist this cycle. |
| OQ-4 | **No Matching question is seeded in Phase 2** (brief Q1.b). The Matching grading path falls through to string compare with a TODO. | BE-TC-13 is marked **partial / documents current fallback behavior** rather than asserting a real Matching wire-shape. | Avoids encoding an undefined contract as a passing assertion. |
| OQ-5 | **Role gate for non-Student.** A *parent* JWT hitting SubmitAnswer should get **403 Forbidden** (role policy), anonymous → **401**. This is enforced by the framework `[Authorize(Roles="Student")]`, not handler code. | BE-TC-20/21/22 assert 401 (anonymous) and 403 (parent/wrong-role). | Confirms the role decorator is actually wired and not bypassable; the product has **no teacher role**, so parent is the realistic wrong-role actor. |

None of these block implementation — all have a defensible default. They want a lead acknowledgement so the testers don't treat OQ-1/OQ-2 as defects.

## 5. Handoff

| File | Owner | Goes to |
|---|---|---|
| `docs/qc/P2-07/backend-test-cases.md` | this design | **`api-tester`** — implement as integration tests in `backend/tests/Learnexia.IntegrationTests/` (extend `P2_07_InstantAnswerFeedback_Tests.cs` or a new QC file; reuse the `P2_08_RecordGranularAnswers_Tests.cs` helpers: `CreateStudentViaParentFlowAsync`, `SeedLessonAsync`, `SeedQuestionsAsync`, `SeedSkillAsync`, `StartAttemptViaApiAsync`, `SendAsync`, `TryProp`). |
| `docs/qc/P2-07/execution-report.md` | **`api-tester`** fills it | After running, record pass/fail per BE-TC ID + any defects. This design pass leaves it templated and empty. |
| `frontend-test-cases.md` | — | Intentionally omitted (no student-app UI surface in this run). |

How `execution-report.md` gets filled: `api-tester` runs the implemented cases against the running API, then edits `execution-report.md` (one row per case: PASS/FAIL/BLOCKED + evidence + defect link). `qc-test-designer` never fills results.

---

Test cases ready — `api-tester` to implement `backend-test-cases.md`; results written into `execution-report.md`. (No `frontend-test-cases.md` — backend-only run.)
