# P2-08 QC Test Plan + Coverage Report — Record granular per-question answers

**Story:** `user-stories/Phase-2-Learning-Core/P2-08-record-granular-answers.md` · FR-QZ-4
**Scope of this run:** **Backend API surface only** (no frontend — P2-08 has no student-app UI; the task file states "no Frontend tasks").
**Designed by:** `qc-test-designer` (design-only). **Implemented by:** `api-tester` → `backend-test-cases.md` → results into `execution-report.md`.
**Status of feature under test:** implemented + merged + security-audited (PASS-WITH-NOTES). This is a deliberate, traceable QC pass over the shipped code.

---

## 1. Summary

P2-08 is the **signal-capture backbone**: every submitted answer must be persisted granularly (`IsCorrect`,
`TimeSpentSeconds`, `HintUsed`, tied to `AttemptId`+`QuestionId`) and remain durably retrievable and accurate;
terminal attempts (Completed/Abandoned) must carry correct aggregates (accuracy %, duration, hints-used, status);
and the per-student / per-skill read endpoints must be strictly ownership-scoped (no IDOR, no `CorrectAnswer` leak).
This catalog weights heavily toward **persistence accuracy** and the **security-audit IDOR findings**, per the brief.

**Endpoints (5):**

| # | Method | Route | Auth |
|---|--------|-------|------|
| E1 | POST | `/api/Learning/Quizzes/{attemptId}/Answers` | `[Authorize(Roles="Student")]` |
| E2 | POST | `/api/Learning/Quizzes/{attemptId}/Complete` | `[Authorize(Roles="Student")]` |
| E3 | POST | `/api/Learning/Quizzes/{attemptId}/Abandon` | `[Authorize(Roles="Student")]` |
| E4 | GET | `/api/Learning/Students/{studentId}/Attempts` | `[Authorize]` |
| E5 | GET | `/api/Learning/Skills/{skillId}/Stats?studentId=` | `[Authorize]` |

**Case counts:** **51 total · 51 backend · 0 frontend.** By priority: **P0 = 30, P1 = 16, P2 = 5.**
By group: SubmitAnswer 16 (A) · Complete 10 (B) · Abandon 8 (C) · GetStudentAttempts 6 (D) · GetSkillStats 7 (E) · cross-cutting 4 (F).
By type: functional/persistence ~22 · auth-authz/IDOR ~16 · validation/boundary ~10 · negative/state ~13 · security ~6 (cases overlap types).

---

## 2. Coverage matrix (acceptance criterion → case IDs)

| AC (from brief) | Criterion | Covering cases | Verdict |
|---|---|---|---|
| AC-1 | Each `StudentAnswer` stores `IsCorrect`, `TimeSpentSeconds`, `HintUsed`, tied to `AttemptId`+`QuestionId` | BE-TC-01, 02, 03, 04, 05, 12, 13, 14, 15, 16, 26, 34, 51 | **Covered** |
| AC-2 | Attempt aggregates accuracy/duration/hints/status on completion | BE-TC-17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 50 | **Covered** |
| AC-3 | Reliable partial capture on abandon (status, CompletedAt, partial aggregates, answers not lost) | BE-TC-27, 28, 29, 30, 31, 32, 33, 34 | **Covered** |
| AC-4 | Queryable per student (with aggregates + status); `CorrectAnswer` never leaked | BE-TC-35, 36, 37, 38, 39, 40 | **Covered** |
| AC-5 | Queryable per skill; null-SkillId excluded; zero-data → zeroed (not 500) | BE-TC-41, 42, 43, 44, 45, 46, 47 | **Covered** |
| AC-6 | `StudentId` from JWT (never client); own-attempt writes only; read endpoints scoped (no IDOR) | BE-TC-06, 09, 10, 11, 22, 24, 31, 33, 37, 40, 44, 45, 47, 51 | **Covered** |
| (contract) | `BaseResponse<T>` envelope + `Successed` spelling; no `ex.Message` leak | BE-TC-48, 49 | **Covered** |

**Coverage verdict: every one of the 6 acceptance criteria has at least one P0/P1 case. No gaps.**

---

## 3. Risk notes (where cases are weighted, and why)

1. **IDOR / ownership (highest weight).** Per-student/per-skill reads and all writes are ownership-scoped via a
   handler-level `studentId == JWT.UserId` check — there is **no** role/policy narrowing on the reads (E4/E5 are
   generic `[Authorize]`, F-05). The IDOR guard is therefore the *only* gate on reads. Dedicated cross-student
   cases on **every** endpoint: BE-TC-06, 22, 31 (writes), 37, 45 (reads), 44 (scope-bleed in aggregation), 51
   (mass-assignment of `StudentId`).
   - **Load-bearing contract detail:** ownership violations return **HTTP 401** (`Unauthorized<T>()`), **not 403**.
     The brief/plan prose says "403/404"; the **code returns 401**. All IDOR cases assert **401** and the
     mismatch is raised as Open Question #1. The only 403 is the framework role gate on E1/E2/E3 (Parent/Admin JWT).

2. **Persistence accuracy (core contract).** The whole story exists to produce clean signal for Phase-3 adaptivity
   and Phase-5 analytics. Cases assert the full round-trip of all four granular fields (BE-TC-01..04), that
   re-answers don't corrupt the denominator (BE-TC-05), that answers survive both Complete and Abandon
   (BE-TC-26, 34), and that aggregates compute over the *answered* set with correct rounding and divide-by-zero
   guards (BE-TC-17, 18, 19, 27, 28).

3. **Partial-abandon survivorship bias.** AC-3's whole point: abandoned attempts must keep their partial signal.
   BE-TC-27/34 assert aggregates compute over the answered subset (not total questions) and the rows persist.

4. **Client-reported timing is advisory / spoofable (audit F-02 + Risk).** `TimeSpentSeconds` is client-supplied,
   bounded to [0, 3600] (BE-TC-14). Attempt `DurationSeconds` must be server-side wall-clock, UTC-normalized, and
   never the sum of client times nor inflated by the host UTC offset (BE-TC-50 — verifies the F-02 fix).

5. **Status-mapping nuance.** Business-rule failures map to **424** (FailedDependency), not 409 — the plan prose
   says "409-equivalent" but the wire code is 424. Command-body validation is **422**; query inline validation is
   **400** (queries are not auto-validated). Cases pin each exact code (BE-TC-05, 08, 21, 30 → 424; 12–15, 25, 33 →
   422; 39, 46 → 400) so an implementer can't silently accept the wrong one.

6. **Nullable `QuizQuestion.SkillId`.** Per-skill stats silently exclude null-skill answers — correct but a sharp
   edge for seeding. BE-TC-42 asserts exclusion; BE-TC-41/43 need at least one question with `SkillId` set
   (seed inline if the demo seeder doesn't — see Open Questions / blockers).

---

## 4. Open questions / assumptions (lead to resolve before/with implementation)

1. **401-vs-403 on ownership/IDOR (contract clarity, not a code change).** Shipped code returns **401** for
   cross-student writes/reads; brief & plan prose say "403/404". Confirm the intended public contract. The QC
   cases assert the **actual** behavior (401) and flag the doc prose as the thing to reconcile — recommend
   updating the brief/plan wording rather than the code (401 is internally consistent across the module).
2. **424 vs 409 for business-state failures.** "Attempt not in progress / already answered / already
   completed/abandoned" return **424** (the platform's `BusinessValidation` mapping). The plan text calls this a
   "409-equivalent". Confirm 424 is the accepted contract (it is the module-wide convention) so consumers code to it.
3. **F-05 deferral (read-endpoint role).** E4/E5 are generic `[Authorize]` with no `Roles="Student"`; any
   authenticated user (parent/admin) reaching them is gated only by the IDOR `studentId==UserId` check. The audit
   accepts this for Phase 2 (parent/admin scoping is Phase 5/7). Confirm no Phase-2 requirement to role-restrict
   these now. BE-TC-40/47 document the current parent-JWT behavior rather than asserting a 403.
4. **F-01 oversized `AnswerPayload` (Low).** No `MaximumLength` on `AnswerPayload` (column is `text`). BE-TC-16
   documents the current accept-and-persist behavior; decide whether to add a bound (would turn it into a 422
   case). Not blocking.
5. **500 fault-injection seam.** BE-TC-49 (assert `ServerError` leaks no `ex.Message`) needs a deterministic way
   to force an unhandled exception. If no fault seam exists in the integration harness, the case is **BLOCKED**
   (documented, not faked) — same convention as the Phase-1 pass.

**Assumptions:** the P2-06 student-auth seeding flow (Register Parent → Add-Child → Sign-In as child) is the
auth path for all cases; two children are seeded for IDOR pairs; the test reads `QuizQuestion.CorrectAnswer`
straight from `LearningDbContext` to drive deterministic correct/wrong submits; a real Postgres is available
to the integration harness.

---

## 5. Handoff

- **`backend-test-cases.md` → `api-tester`.** Implement all 51 cases 1:1 into
  `backend/tests/Learnexia.IntegrationTests/` against the running API (real Postgres), reusing the
  `P2_06_StartAttempt_Tests` seeding/auth helpers. There is **no** `frontend-test-cases.md` (backend-only story).
- **`execution-report.md`** — templated and empty in this folder. **`api-tester` fills it** after the run:
  pass/fail per `BE-TC-id`, defect notes, and any BLOCKED cases with their blocker. The `qc-test-designer`
  never fills results.
- **Feature code is not changed by the tester.** Any defect (e.g. an unexpected status code, a leaked
  `CorrectAnswer`, an IDOR hole) is reported back for `backend-feature` / the lead, per the standard flow.

**Files in this run:** `docs/qc/P2-08/README.md` (this) · `docs/qc/P2-08/backend-test-cases.md` ·
`docs/qc/P2-08/execution-report.md`.
