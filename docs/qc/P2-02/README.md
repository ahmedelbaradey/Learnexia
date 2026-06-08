# QC Test Plan & Coverage Report — P2-02 (Browse subjects & lessons) — BACKEND ONLY

**Story:** `user-stories/Phase-2-Learning-Core/P2-02-browse-subjects-and-lessons.md`
**Brief:** `docs/briefs/P2-02.md` · **Plan:** `docs/plans/P2-02.md` · **Task:** `tasks/Backend/Phase-2-Learning-Core/P2-02-BE.md`
**Designed by:** `qc-test-designer` (Opus) · **Scope:** backend API surface only (no frontend cases by request)
**Status of feature under test:** Implemented & merged (task file marks P2-02 ✅ Done; endpoints since extended by P2-04 LearningPathEngine and P8-03 language guard).

---

## 1. Summary

P2-02 adds three **student-facing read** endpoints on `SubjectsController` (`api/learning/Subjects`):

| Route | Query handler | Returns |
|---|---|---|
| `GET /api/learning/Subjects/ForGrade?grade={n}` | `GetSubjectsForGradeQueryHandler` | `BaseResponse<List<StudentSubjectDto>>` — the 4 MVP subjects for grade `n`, in the student's learning language |
| `GET /api/learning/Subjects/{id}/Lessons` | `GetSubjectLessonsQueryHandler` | `BaseResponse<List<UnitWithLessonsDto>>` — units→lessons ordered by `SequenceOrder`, with per-student `NodeState` |
| `GET /api/learning/Subjects/{id}/SkillTree` | `GetSubjectSkillTreeQueryHandler` | `BaseResponse<List<ConceptNodeDto>>` — concepts→skills with per-student `NodeState` |

All three are `[Authorize]` (P8/P2-04 tightened from anonymous → 401 when no JWT). They are **not** paginated (Q3: flat `BaseResponse<List<T>>`). They read the student's `learning_language` JWT claim and serve content in the resolved language.

**IMPORTANT — implementation differs from the task framing on cross-language access.** The task brief says "cross-language access → 403". That is **only true for the single-lesson endpoint** `GET /api/learning/Lessons/{id}` (`GetLessonQueryHandler`, which I include as a boundary case). The three P2-02 browse endpoints do **NOT** return 403 for a wrong-language `SubjectId`; they **silently redirect** to the correct-language tree for the same `SubjectCode`+`Grade` (and fall back to the opposite tree + a log warning if the resolved tree is absent). Test cases are written against the **real implemented behavior** (redirect), and the 403 case is asserted on the lesson endpoint where it actually lives. See Risk R1 and Open Question Q1.

### Counts
- **Total cases:** 28 — all backend, all `api-tester`.
- **By surface:** ForGrade (subjects) 9 · Lessons-in-subject 8 · SkillTree 6 · Lesson-by-id language guard (boundary) 2 · cross-cutting envelope/auth 3.
- **By priority:** P0 = 13 · P1 = 11 · P2 = 4.
- **By type:** functional 8 · validation 2 · negative 4 · boundary 4 · auth-authz 4 · persistence/ordering 3 · language-filter 3.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria taken from the story (AC-S*) and the brief's testable list (AC-B*).

| Criterion | Source | Covered by | Gap? |
|---|---|---|---|
| AC-S1 / AC-B1: signed-in student sees the **4 MVP subjects** for their grade (Math, Science, Arabic, English) | story + brief | `BE-TC-01`, `BE-TC-02`, `BE-TC-03` | No |
| AC-S2 / AC-B2: selecting a subject shows **units + lessons in `SequenceOrder`** | story + brief | `BE-TC-12`, `BE-TC-13` | No |
| AC-S3 / AC-B3: queries **filtered by the student's grade** (no cross-grade leak) | story + brief | `BE-TC-04`, `BE-TC-05` | No |
| AC-S4 / AC-B4: subject with **no lessons → empty state (200 empty)**, not error | story + brief | `BE-TC-14`, `BE-TC-08` | No |
| AC-B4 (cont.): non-existent subject id → **404** | brief | `BE-TC-15`, `BE-TC-22` | No |
| AC-B5: **SkillTree** payload — concepts→skill nodes, each node carries a **`State`** | brief | `BE-TC-18`, `BE-TC-19`, `BE-TC-20` | No |
| AC-B6: all responses use **`BaseResponse<T>`** envelope (`Successed`) | brief | `BE-TC-26`, plus every functional case asserts the envelope | No |
| AC-B7 / AC-7: endpoints require **authenticated** student (401 anonymous) | brief + controller | `BE-TC-25`, `BE-TC-09`, `BE-TC-17`, `BE-TC-24` | No |
| **Learning-language filter (P8-03):** ar student sees ar content, en student sees en content | controller + handlers (task "Done" note) | `BE-TC-06`, `BE-TC-10`, `BE-TC-21` | No |
| **Cross-language access (P8-03):** wrong-language `SubjectId` does not leak wrong-language content | handlers | `BE-TC-11` (browse → redirect), `BE-TC-27`/`BE-TC-28` (lesson → 403) | No |
| Invalid grade (0, 7) → **400**; unknown grade number (99) → **404** | plan Batch-2 table | `BE-TC-07a`, `BE-TC-07b`, `BE-TC-08` | No |
| **Product override:** exactly 4 subjects, no Social Studies | CLAUDE.md | `BE-TC-02`, `BE-TC-03` | No |

**Coverage verdict:** Every acceptance criterion is covered by at least one P0/P1 case. **No uncovered criterion.** The only deviation from the task's stated expectation is the cross-language semantics (403 vs silent redirect) — covered, but on the endpoint where each behavior truly lives, and flagged as Open Question Q1 for the lead.

---

## 3. Risk notes (where cases were weighted)

- **R1 — Cross-language semantics mismatch (HIGHEST):** The task and the QC mandate ask for "cross-language → 403". The three browse handlers **do not 403**; they **redirect** to the correct-language tree (`GetSubjectsForGradeQueryHandler` resolves per-code; `GetSubjectLessons`/`GetSubjectSkillTree` swap to the correct-language subject for the same `SubjectCode`+`GradeId`). The actual 403 lives in `GetLessonQueryHandler`. I weighted three cases on the redirect behavior (`BE-TC-06`, `BE-TC-10`, `BE-TC-11`, `BE-TC-21`) and two on the lesson 403 (`BE-TC-27`, `BE-TC-28`) so a tester validates **both** real behaviors and the discrepancy surfaces in the execution report rather than being silently "passed".
- **R2 — Grade vs GradeId mapping:** `?grade=` carries the grade **number** (1–6); `Subject.GradeId` is a surrogate FK to `Grade.Id`. A regression where the handler filters `GradeId == gradeNumber` would still return data for some grades by coincidence. `BE-TC-04`/`BE-TC-05` assert no cross-grade leakage explicitly (grade-2 subject IDs absent from a grade-1 response).
- **R3 — Seed dependence / pre-existing RED:** HANDOFF.md records pre-existing failing cases "P2-02 TC-1" and "ForGrade" tied to **seeder ordering** (`P2_04`/`P2_09` seed-ordering flakiness on full-suite runs). The "4 subjects" assertions (`BE-TC-02`/`BE-TC-03`) and skill counts (`BE-TC-19`) depend on the P2-10 seed being applied to a fresh DB. Tester must run against a freshly migrated+seeded DB and note any seed-ordering RED as environment, not a P2-02 regression.
- **R4 — `NodeState` is now engine-derived, not the P2-02 placeholder:** P2-04 replaced the static `IsLocked`-derived placeholder with `LearningPathEngine.ComputeStates`. For an **authenticated student with no progress**, expect `Available`/`Locked` per prerequisite edges, not the old "all Available". Cases assert the node *carries a valid `NodeState`* (`Locked|Available|Completed`) rather than pinning an exact value, so they stay stable across the P2-04 change.
- **R5 — `IsLocked` is `[Obsolete]`:** `LessonInUnitDto.IsLocked` is retained for back-compat but superseded by `State`. Tests assert against `State`, not `IsLocked`.

---

## 4. Open questions / assumptions (lead must resolve before execution where noted)

- **Q1 (decision needed) — Is the browse-endpoint "silent redirect" the intended cross-language contract, or should it be 403 like the lesson endpoint?** Today: `ForGrade`/`Lessons`/`SkillTree` redirect a wrong-language `SubjectId` to the correct-language tree (`BE-TC-11`); only `Lessons/{id}` 403s (`BE-TC-27`). If the product wants uniform 403, that is a feature change and `BE-TC-11`/`BE-TC-21` must be rewritten. **The QC cases assert the current behavior; flag if the contract should change.**
- **Q2 (assumption) — Absent `learning_language` claim defaults to Arabic.** `LearningLanguageClaimAccessor` falls back to `ContentLanguage.Ar` (and logs) when the claim is missing/unrecognised. `BE-TC-29` asserts a legacy token (no claim) is served Arabic content, not a 400/500. Confirm this fallback is intended (it is documented as the product default).
- **Q3 (assumption) — Server-side grade-scope enforcement is NOT in P2-02.** `?grade=` is trusted (Q2 in the plan; enforcement deferred to P6-06). So a grade-1 student *can* call `?grade=3` and get grade-3 subjects. `BE-TC-30` documents this as **expected-today / known gap** (no 403), not a defect. If the lead wants it enforced now, that's a new story.
- **Q4 (env) — Seed must be applied.** Assumes P2-01 migration + P2-10 seed are present in the target DB (4 subjects × 2 language trees per grade, Math = 5 units × 3 lessons, concepts/skills per seeder). If absent, the "4 subjects"/count cases exercise the empty path instead — tester must record which.
- **Assumption — DTO JSON keys are camelCase**, and `Successed` serialises as `successed` (lowercase). This is the established contract (do not "fix").

---

## 5. Handoff

- **`backend-test-cases.md` → `api-tester`** — implement all 28 cases as integration tests against the running API + freshly seeded PostgreSQL. Two students are needed: one with `learning_language=ar`, one with `=en` (mint via the auth/login flow; see preconditions in each case).
- **No `frontend-test-cases.md`** — backend-only run by request.
- **`execution-report.md`** — scaffolded empty by this agent. `api-tester` fills the result table (Pass/Fail/Blocked per case) + defect log after running. The QC designer never fills results.

**Execution report path:** `docs/qc/P2-02/execution-report.md`
