# QC Test Plan & Coverage Report — P2-03 Navigate the skill tree (BACKEND ONLY)

> **Run scope:** Backend API surface only. No `frontend-test-cases.md` in this run.
> **Designed by:** `qc-test-designer` (Opus). Design only — `api-tester` implements `backend-test-cases.md`.
> **Story:** [P2-03 Navigate the skill tree](../../../user-stories/Phase-2-Learning-Core/P2-03-navigate-skill-tree.md) · FR-LR-2
> **Brief / Plan:** [`docs/briefs/P2-03.md`](../../briefs/P2-03.md) · [`docs/plans/P2-03.md`](../../plans/P2-03.md)

---

## 1. Summary

This story is the **skill-tree READ + node-status** surface. The student reads their subject as a
concept→skill graph, each skill carrying an engine-derived `NodeState` (Locked / Available /
Completed), missing-prerequisite reasons for locked skills, and — added by this story — a per-lesson
`IsBoss` flag (the fourth FR-LR-2 rendering category). The unlock *logic* itself lives in **P2-04**
(`LearningPathEngine`); this story consumes it. Cases below test the READ contract and node status;
P2-04-specific unlock-rule depth is cross-referenced, not re-derived.

### Endpoints in scope (the skill-tree READ surface)

| # | Endpoint | Auth | Returns | Role here |
|---|---|---|---|---|
| E1 | `GET /api/learning/Subjects/{id}/SkillTree` | `[Authorize]` | `BaseResponse<List<ConceptNodeDto>>` | **Primary** — concept→skill tree with per-skill `State` + `MissingPrerequisites`. |
| E2 | `GET /api/learning/Subjects/{id}/Lessons` | `[Authorize]` | `BaseResponse<List<UnitWithLessonsDto>>` | **Primary** — unit→lesson leaves with per-lesson `State`, `MissingPrerequisites`, **`IsBoss`** (the story's net-new field). |
| E3 | `GET /api/learning/Lessons/{id}` | `[Authorize]` | `BaseResponse<SingleLessonResponse>` | **Supporting** — single lesson read; carries `IsBoss`; the **only** surface here that returns **403** on cross-language. |

> The story brief frames "the skill tree" as both the concept→skill rollup (E1) and the unit→lesson
> detail (E2) — the FE calls both. Both are JWT-aware and run the same engine output. `IsBoss` lands on
> E2/E3 leaves only (per brief Q8 — `SkillNodeDto.HasBoss` was descoped).

### Case counts

| Bucket | Count |
|---|---|
| **Total backend cases** | **24** |
| Frontend cases | 0 (out of scope this run) |
| P0 | 11 |
| P1 | 9 |
| P2 | 4 |
| By endpoint | E1 SkillTree: 12 · E2 Lessons: 8 · E3 single-lesson: 4 |
| Blocked / behaviour-discrepancy flagged | 1 (BE-TC-09 — see Risk R1 + Open Question Q1) |

---

## 2. Coverage matrix (every acceptance criterion → case IDs)

Acceptance criteria from the user story, with the BE-relevant decomposition from the brief.

| AC | Criterion (BE interpretation) | Covering cases | Gap? |
|---|---|---|---|
| **AC1** | Tree renders nodes in the four states locked / unlocked / completed / **boss**. (BE: `NodeState` enum (3) + orthogonal `IsBoss`.) | BE-TC-01, 02, 04, 05, 06, 07, 11, 16, 17, 18, 19, 21, 22 | No |
| **AC1-boss** | The 4th category — boss — is carried as `IsBoss` on lesson-bearing DTOs; exactly one boss (highest `SequenceOrder`) per unit; non-boss = false. | BE-TC-16, 17, 18, 19, 21, 22, 23 | No |
| **AC2** | Tapping a locked node shows *why* it's locked (missing prerequisite). (BE: `MissingPrerequisites` populated for Locked skills/lessons — P2-04 behaviour, regression-guarded here.) | BE-TC-06, 07, 08, 20 | No |
| **AC3** | Node states reflect the student's current mastery/progress. (BE: state derived from the engine + the student's attempts; fresh vs progressed student differ; cross-student isolation.) | BE-TC-04, 05, 06, 11, 12, 13, 20 | No |
| **AC4** | RTL/LTR render. | — (Frontend concern, P2-03-FE — out of scope; BE returns IDs + names only.) | N/A (correctly out of scope) |

**Supporting (not an AC line, but load-bearing for the READ contract):**

| Concern | Covering cases |
|---|---|
| Auth — anonymous → 401 | BE-TC-03, 14, 24 |
| `BaseResponse<T>` envelope + `Successed` spelling | BE-TC-02, 16 |
| Grade scoping (tree is the student's grade's curriculum) | BE-TC-10, 12 |
| Learning-language filter (En vs Ar tree resolution) | BE-TC-09, 10, 11, 23 |
| Cross-language access — actual handler behaviour vs the "403" framing | BE-TC-09 (E1 silent-redirect), BE-TC-23 (E3 → 403) |
| Subject not found → 404 | BE-TC-15 |
| Tree shape matches the seeded graph (concept/skill counts, edges) | BE-TC-01, 04, 05, 08 |
| Fresh student — only root unlocked, rest locked | BE-TC-04, 05 |
| Product overrides (4 subjects, no Social Studies, no student self-register) | BE-TC-12, 13 |

**Coverage verdict: every backend-relevant acceptance criterion has at least one P0/P1 case. No gap.**
AC4 is correctly out of scope for a backend run (no UI surface).

---

## 3. Risk notes (where cases are weighted, and why)

- **R1 — "Cross-language → 403" is FALSE for the skill-tree endpoints (E1 + E2). [HIGHEST RISK]**
  The task brief and the dispatch instruction both say "cross-language → 403" for the skill tree.
  The **actual handler** (`GetSubjectSkillTreeQueryHandler.cs:74-97` and
  `GetSubjectLessonsQueryHandler.cs:74-95`) does **NOT** forbid — it **silently redirects** to the
  correct-language tree for the same `SubjectCode`+`GradeId`, and only logs a warning (still 200) when
  the resolved tree is absent. Only the **single-lesson** endpoint
  (`GetLessonQueryHandler.cs:86-90`) returns **403** on a language mismatch. A test that naively
  asserts 403 on `GET /Subjects/{wrongLangId}/SkillTree` **will fail against correct code** — the bug
  would be in the test, not the product. BE-TC-09 is written to assert the **real** behaviour
  (silent redirect → 200, content is the resolved-language tree) and is flagged as a discrepancy
  needing a lead ruling (Q1). This is the single most important thing for `api-tester` to get right.

- **R2 — Prerequisite edges exist only in the MATH tree.** `LearningSeeder` authors `KnowledgeEdge`
  Prerequisite rows for MATH/Ar and MATH/En only (`LearningSeeder.cs:590-633`). Science, Arabic, and
  English have `KnowledgeNode`s but **no prereq edges** → their skills will not be Locked-by-prereq.
  Edge-correctness and "why-locked" cases must target **Math** to be meaningful. Asserting a locked
  Science skill would be a false expectation.

- **R3 — Fresh-student lock topology depends on the engine, not on `Lesson.IsLocked`.** Post-P2-04 the
  state is engine-derived. For a fresh Grade-1 Math student, the root skill chain
  `Count to 1000 (G1) → Compare and Order Numbers (G1) → Add Single-Digit Numbers (G1)` means only the
  root is Available; downstream skills with unmet prereqs are Locked. Cases assert "root Available,
  prereq-gated locked" rather than a flat "all locked but one."

- **R4 — Boss flag is a static curriculum value, orthogonal to state.** A boss lesson can be Locked,
  Available, or Completed. Tests must not assume boss ⇒ unlocked. The boss is the highest-`SequenceOrder`
  lesson per unit; in Grade-1 Math Unit-1 that lesson is also `IsLocked=true` in the legacy seed, so the
  boss-read case (E3) must confirm 200 is returned regardless of lock (P2-05 preview semantics).

- **R5 — Grade scoping is implicit, not a route parameter.** There is no `grade` parameter on E1/E2/E3.
  The student reaches *their* grade's subjects via the subject IDs resolved for their grade; there is no
  IDOR vector (no client-supplied studentId). Cross-grade access is only reachable by passing another
  grade's `subjectId` — which the endpoint *will* serve (it is curriculum-public). The "grade scoping"
  guarantee here is that **node status** is the student's, not that the curriculum tree is hidden.
  BE-TC-12 tests this nuance explicitly so it is not mistaken for an access-control bug.

- **R6 — Enum serialisation.** `NodeState` serialises as an **int** by default (no global
  `JsonStringEnumConverter`), per the P2-09 test precedent. Cases tolerate both int and string but
  assert the int value primarily.

---

## 4. Open questions / assumptions (lead must resolve before implementation)

1. **[BLOCKER for BE-TC-09] Cross-language skill-tree: silent-redirect vs 403 — which is the intended
   contract?** The dispatch brief says 403; the code does a silent 200 redirect on E1/E2 (only E3
   returns 403). Two outcomes:
   (a) **Code is correct, brief wording is loose** → BE-TC-09 asserts silent-redirect→200 (as written).
   (b) **403 is genuinely intended for E1/E2 too** → that is a *product defect*, not a test gap; raise it
   for `backend-feature` before `api-tester` codes a 403 assertion. **Recommended default: (a)** — the
   code is deliberate and documented (P8-03-BE-4/BE-6 "redirect to the correct-language tree"), and 403
   is reserved for the direct single-lesson read where redirect has no clean target.

2. **Is the skill-tree (E1) expected to surface `IsBoss` at all?** Per brief Q8 it was **descoped** —
   `SkillNodeDto` has no `HasBoss`. So boss assertions live on E2/E3 only. Assumption: E1 boss-less is
   correct; no case asserts boss on E1. Confirm the lead does not want `SkillNodeDto.HasBoss` after all.

3. **Seed boss tally for assertions.** Cases assert "exactly one boss per unit = highest `SequenceOrder`"
   structurally (robust to seed-count drift) rather than hard-coding "66 boss lessons." Confirm the
   structural assertion is acceptable; if the lead wants the absolute 66/162 tally asserted, BE-TC-22 has
   an optional sub-assertion for it.

4. **Empty-tree (concept-less subject) case.** BE-TC-15b assumes a subject with no concepts returns
   `200 + EmptyCollection`. The seeder always seeds concepts for the 4 subjects, so this needs a
   synthesised subject row or is marked **not-testable-without-fixture**. Confirm whether `api-tester`
   should synthesise it or skip.

5. **Assumption — student-auth via parent flow.** All authenticated cases reuse the
   `CreateStudentViaParentFlowAsync` pattern from `P2_09_HomeDashboard_Tests.cs` (Register-Parent →
   Add-Child → Sign-In), with `LearningLanguage="en"` so the En tree resolves. No student self-register
   (product override). Confirmed against existing tests — no decision needed unless the lead wants an
   Arabic-medium (`LearningLanguage="ar"`) variant added (BE-TC-11 covers it).

---

## 5. Handoff

| File | Owner | Goes to |
|---|---|---|
| [`backend-test-cases.md`](./backend-test-cases.md) | qc-test-designer (this run) | **`api-tester`** — implements as `P2_03_SkillTree_Tests.cs` (new) and may extend `P2_09_HomeDashboard_Tests.cs` for the dashboard `IsBoss` assertion already present. |
| [`execution-report.md`](./execution-report.md) | scaffolded empty by this run | **`api-tester`** fills pass/fail per case + defects after running. Never filled by the designer. |
| `frontend-test-cases.md` | — | **Not produced** — backend-only run. |

**Process:** `api-tester` reads `backend-test-cases.md`, implements each `BE-TC-*` 1:1 against the
running API, then records the result for every case ID in `execution-report.md` (status + evidence +
any defect). Resolve Open Question #1 (the 403 vs silent-redirect discrepancy) **before** coding
BE-TC-09's assertion.

> Note overlap with **P2-04**: this story = the tree **READ + status surface**; **P2-04** = the unlock
> **logic** (`LearningPathEngine`). Cases here assert that the READ correctly *surfaces* engine output
> (states, missing-prereqs, edges-as-derived-locks); they do not re-test the engine's internal
> derivation depth — that belongs to the P2-04 suite (`P2_04_LearningPath_Tests.cs`), which these cases
> must not regress.
