# P2-04 — Backend Test Cases (Learning Path Unlock Engine — observable API surface)

**Target agent:** `api-tester`
**Surface under test:** Learning module HTTP endpoints that surface engine-derived unlock state, plus the locked-content start path.
**Harness:** mirror `P2_11_KnowledgeGraph_Tests.cs` / `P2_09_HomeDashboard_Tests.cs` — xUnit + Testcontainers PostgreSQL (pg16) + the seeded `LearningSeeder` graph + a real **Student-role JWT** minted via the parent→child onboarding flow (same setup the P2-08/P2-09 suites use). Write results into `execution-report.md`.

> **Read this first — the actual observable surface (verified against code, 2026-06-08):**
> The engine (`LearningPathEngine`) is pure domain logic and is **not** an endpoint. Its observable HTTP surface is **two read endpoints** that now carry engine-derived `NodeState`:
> - `GET /api/learning/Subjects/{id}/SkillTree` → `List<ConceptNodeDto>`; each `SkillNodeDto` has `state` (NodeState) + `missingPrerequisites`.
> - `GET /api/learning/Subjects/{id}/Lessons` → `List<UnitWithLessonsDto>`; each `LessonInUnitDto` has `state` (NodeState) + `missingPrerequisites` (+ deprecated `isLocked`).
> Both are `[Authorize]`. `studentId` is taken **only** from the JWT (`ICurrentUserService.UserId`) — never from route/query (IDOR-proof by construction).
>
> **CRITICAL surface correction — there is NO "start a locked lesson is rejected" guard.**
> The task hypothesized that `QuizzesController.StartAttempt` / `LessonsController` reject starting a **locked** lesson with 403/424. **It does not.** `POST /api/learning/Quizzes/{lessonId}/Attempt` enforces only: `[Authorize(Roles="Student")]`, lesson-exists → 404, and the **P8 language guard → 403** (`LessonLanguageMismatch`). A student can start an attempt on a lesson whose `NodeState == Locked`. This is captured below as **BE-TC-16 (documented gap, expected-to-reveal-no-guard)** — implement it to *document current behavior*, not as a pass/fail of P2-04's stated intent. See README "Risk notes" and "Open questions". The cross-language guard (a real, observable 403) is covered separately.

## NodeState serialization (load-bearing)
`NodeState` serializes as an **int** (no `JsonStringEnumConverter` registered — confirmed P2-02): `Locked = 0`, `Available = 1`, `Completed = 2`. Assert against the integer.

## Envelope (load-bearing)
Success flag key is **`"successed"`** (camelCase in JSON; note the non-standard spelling — do not "fix" it). Error envelopes are also camelCase (`ErrorHandlerMiddleWare` uses `JsonNamingPolicy.CamelCase`). Payload is under `"data"`.

## Seeded prerequisite chain (the fixture P2-04 exercises) — Math G1, English/Latin tree
| Node (Skill) | MasteryThreshold | Inbound Prerequisite | Fresh-student state |
|---|---|---|---|
| `Count to 1000 (G1)` | 70 | (none — **root**) | **Available** |
| `Compare and Order Numbers (G1)` | 75 | `Count to 1000 (G1)` | **Locked** |
| `Add Single-Digit Numbers (G1)` | — | `Compare and Order Numbers (G1)` | **Locked** |

> Resolve the concrete `subjectId` / `skillId` / `lessonId` values at runtime from the seeded graph by name lookup (skill name strings are stable lookup keys per HANDOFF — do **not** hardcode integer IDs). Some Math lessons have `SkillId == null` (3 per grade: "Word Problems: Add and Subtract", "Division as Equal Groups", "Word Problems: Multiply and Divide") — used by BE-TC-09.

## Mastery-seeding helper (used by the "flip" cases)
To mark a skill mastered for the JWT student, drive real attempts through the existing write endpoints (preferred — exercises the true pipeline) **or** seed `Attempt` + `StudentAnswer` rows directly:
1. `POST /api/learning/Quizzes/{lessonId}/Attempt` (lesson whose `SkillId` is the prereq skill) → get `attemptId`.
2. `POST /api/learning/Quizzes/{attemptId}/Answers` N times with correct answers so `AccuracyPercentage >= MasteryThreshold` and `TotalAnswers >= 1`.
3. `POST /api/learning/Quizzes/{attemptId}/Complete`.
Mastery rule (Q2): `AccuracyPercentage >= Skill.MasteryThreshold` **AND** `TotalAnswers >= 1`. Completion (Q5): at least one `Attempt.Status == Completed` for the lesson — independent of accuracy.

---

## Group A — Auth / authz on the unlock surface

### BE-TC-01 — SkillTree requires authentication
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** No `Authorization` header. Seeded subject exists.
- **Steps:** 1. `GET /api/learning/Subjects/{mathG1SubjectId}/SkillTree` with no JWT.
- **Expected:** HTTP **401**. No `data` body leaking skill state. (Mirrors the P2-11/P2-09 anonymous-401 pattern.)
- **Traces to:** Q7 (Option A — `[Authorize]` tightening); AC1 (engine path is the authenticated path).

### BE-TC-02 — Lessons requires authentication
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** No `Authorization` header.
- **Steps:** 1. `GET /api/learning/Subjects/{mathG1SubjectId}/Lessons` with no JWT.
- **Expected:** HTTP **401**. No lesson state in body.
- **Traces to:** Q7; AC1.

### BE-TC-03 — Cross-student isolation (IDOR): each JWT sees only its own progress
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Two distinct Student JWTs (Student A, Student B) from two onboarded children. Student A masters `Count to 1000 (G1)` (see mastery helper); Student B has **zero** answers.
- **Steps:**
  1. As **Student A**: `GET .../Subjects/{id}/SkillTree`.
  2. As **Student B**: `GET .../Subjects/{id}/SkillTree` (same subjectId).
- **Expected:** Student A: `Compare and Order Numbers` → `state == 1` (Available). Student B: same skill → `state == 0` (Locked). The two responses differ purely by JWT; B's progress is unaffected by A's mastery (no `studentId` leakage across users).
- **Traces to:** AC1 (per-student determinism); IDOR (studentId from JWT only).

---

## Group B — Fresh student: root unlocked, dependents locked (AC1)

### BE-TC-04 — Fresh student: root skill is Available
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Student JWT with **no attempts** in Math G1.
- **Steps:** 1. `GET .../Subjects/{mathG1SubjectId}/SkillTree`. 2. Find skill `Count to 1000 (G1)`.
- **Expected:** 200; `state == 1` (Available); `missingPrerequisites` empty/null. Root nodes are entry points (Q4) — a fresh student always has a clear next step.
- **Traces to:** AC1; story "always have a clear next step"; Q4.

### BE-TC-05 — Fresh student: dependent skill is Locked
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Same fresh student.
- **Steps:** 1. `GET .../SkillTree`. 2. Find skill `Compare and Order Numbers (G1)`.
- **Expected:** 200; `state == 0` (Locked); `missingPrerequisites` contains an entry naming `Count to 1000 (G1)`.
- **Traces to:** AC1; AC3.

### BE-TC-06 — Fresh student: lesson states mirror their skill (Lessons endpoint)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Same fresh student.
- **Steps:** 1. `GET .../Subjects/{id}/Lessons`. 2. Inspect lessons under the root skill vs the dependent skill.
- **Expected:** 200; lessons whose `skillId == Count to 1000` → `state == 1`; lessons whose `skillId == Compare and Order Numbers` → `state == 0`. Each locked lesson has a non-empty `missingPrerequisites`.
- **Traces to:** AC1; FR-LR-2 (node states surfaced per lesson).

### BE-TC-07 — Two-hop locking: grandchild skill is Locked while its parent is unmastered
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Fresh student.
- **Steps:** 1. `GET .../SkillTree`. 2. Find `Add Single-Digit Numbers (G1)` (depends on `Compare and Order Numbers`, which depends on root).
- **Expected:** `state == 0` (Locked). `missingPrerequisites` lists its **immediate** prereq `Compare and Order Numbers (G1)` only — **not** the transitive `Count to 1000` (Q9: immediate prereqs only, no transitive closure).
- **Traces to:** AC3; Q9 (immediate-only explanation).

---

## Group C — Mastery flips dependents (AC2)

### BE-TC-08 — Mastering the root flips the next skill from Locked → Available
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Student JWT, fresh. Identify a lesson whose `skillId == Count to 1000 (G1)`.
- **Steps:**
  1. Pre-check: `GET .../SkillTree` → `Compare and Order Numbers` is `state == 0` (Locked).
  2. Master `Count to 1000`: start attempt → submit correct answers until `AccuracyPercentage >= 70` and `TotalAnswers >= 1` → complete.
  3. Post-check: `GET .../SkillTree` again.
- **Expected:** After step 2, `Count to 1000` → `Completed` (2) (it now has a completed attempt) and `Compare and Order Numbers` → `Available` (1) with empty `missingPrerequisites`. Its lessons likewise flip on the Lessons endpoint.
- **Traces to:** **AC2** (complete + meet threshold → dependents unlock); BE-3 recompute-on-read.

### BE-TC-09 — Lesson with `SkillId == null` is always Available
- **Type:** boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Fresh student. Locate a Math lesson with `skillId == null` (e.g. "Division as Equal Groups").
- **Steps:** 1. `GET .../Subjects/{mathSubjectId}/Lessons`. 2. Find the null-skill lesson.
- **Expected:** `skillId == null` AND `state == 1` (Available) even when sibling skill-bearing lessons in the same unit are Locked; `missingPrerequisites` empty (Q3 — no skill to gate on).
- **Traces to:** Q3; AC1 (rule completeness).

### BE-TC-10 — Skill with no inbound Prerequisite edges stays Available pre-completion
- **Type:** boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Fresh student.
- **Steps:** 1. `GET .../SkillTree`. 2. Pick any skill that is a graph root (no inbound Prerequisite edge) — `Count to 1000 (G1)` qualifies.
- **Expected:** `state == 1` (Available), never Locked, before any attempt. (Distinct from BE-TC-04 in intent: asserts the *no-prereq-edges → Available* rule generally, including a skill that has a node but zero inbound edges.)
- **Traces to:** Q4; AC1.

---

## Group D — Missing-prerequisite explanation (AC3)

### BE-TC-11 — Locked lesson exposes a populated `missingPrerequisites` array
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Fresh student.
- **Steps:** 1. `GET .../Subjects/{id}/Lessons`. 2. Find a Locked lesson under `Compare and Order Numbers`.
- **Expected:** `state == 0` AND `missingPrerequisites.length >= 1`.
- **Traces to:** **AC3**.

### BE-TC-12 — `MissingPrerequisiteDto` shape: all five fields present and correct
- **Type:** functional / validation · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Fresh student; locked lesson under `Compare and Order Numbers` (prereq `Count to 1000`, threshold 70).
- **Steps:** 1. `GET .../Lessons`. 2. Read the first `missingPrerequisites` item on the locked lesson.
- **Expected:** Item has all of: `prereqSkillId` (== the `Count to 1000` skillId), `prereqSkillName` (== "Count to 1000 (G1)"), `prereqNodeId` (> 0), `requiredAccuracy` (== 70), `currentAccuracy` (== 0.0 for a fresh student). No field is the type default-unless-correct (e.g. `prereqSkillName` is not empty).
- **Traces to:** AC3; Q9.

### BE-TC-13 — `currentAccuracy` reflects actual partial progress (below-threshold student)
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Student JWT. Seed/produce **partial** progress on `Count to 1000`: enough answers so `AccuracyPercentage ≈ 60` (below the 70 threshold), `TotalAnswers >= 1`, and **complete** the attempt is NOT required (do not let it master). E.g. 3 of 5 correct = 60%.
- **Steps:**
  1. Produce 60% accuracy on the prereq skill (do not reach 70).
  2. `GET .../Lessons` → find the locked dependent lesson.
- **Expected:** Dependent lesson still `state == 0` (Locked) — partial progress below threshold does **not** unlock. The `missingPrerequisites` item shows `currentAccuracy ≈ 60.0` and `requiredAccuracy == 70`. This proves the "attempted but below mastery → still locked" path and the explanation accuracy.
- **Traces to:** AC2 (negative — threshold not met); AC3 (accurate current value).

### BE-TC-14 — Available/Completed lessons carry an empty `missingPrerequisites`
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Fresh student (for Available) + a student who completed a root lesson (for Completed).
- **Steps:** 1. `GET .../Lessons`. 2. Inspect a root (Available) lesson and a completed lesson.
- **Expected:** Both have `missingPrerequisites` empty/`[]`. Explanation is present only when Locked.
- **Traces to:** AC3 (negative side — no noise when not locked).

---

## Group E — Completed-state separation & determinism

### BE-TC-15 — Completed wins over mastery: low-accuracy completion → lesson Completed, dependents stay Locked
- **Type:** functional / state · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Student JWT. On a root-skill lesson, complete an attempt with **low accuracy** (e.g. 1 of 5 correct = 20%, below the 70 threshold), then `Complete`.
- **Steps:**
  1. Start + answer 20% correct + complete the root lesson.
  2. `GET .../Lessons` and `.../SkillTree`.
- **Expected:** The completed lesson → `state == 2` (Completed). The root **skill** mastery is NOT met (20% < 70%), so the **dependent** skill `Compare and Order Numbers` stays `state == 0` (Locked). Confirms Q5 separation: completion ≠ mastery; only mastery drives downstream unlocks.
- **Traces to:** Q5; AC2 (negative — completion alone doesn't flip dependents).

### BE-TC-16 — DOCUMENTED GAP: starting a *locked* lesson is currently NOT rejected
- **Type:** negative / gap-documentation · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Fresh student. Find a lesson whose `state == 0` (Locked) via the Lessons endpoint (e.g. under `Compare and Order Numbers`). Ensure the lesson's subject language matches the student's learning language (so the P8 guard does not fire and confound the result).
- **Steps:**
  1. Confirm the target lesson is `state == 0` (Locked) for this student.
  2. `POST /api/learning/Quizzes/{lockedLessonId}/Attempt`.
- **Expected (current behavior — assert and FLAG, do not treat as P2-04 pass):** HTTP **200** with a new `attemptId` — the start path does **NOT** enforce the unlock engine. There is no 403/424 prerequisite guard. **Record this in `execution-report.md` as a confirmed gap against the task's hypothesized "locked → rejected" behavior**, with the actual status returned. (If, contrary to current code, a 403/424 prerequisite rejection IS returned, record that instead — the assertion documents reality either way.)
- **Traces to:** Task hypothesis ("guard on starting a locked lesson/quiz → 403/422"); **NOT** an acceptance criterion of P2-04 (no AC requires a start-guard). See README Risk notes / Open questions.

---

## Group F — Cross-language guard (P8 — applies to the same surface)

### BE-TC-17 — Starting an attempt in the wrong-language tree is rejected with 403
- **Type:** auth-authz / negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Student JWT whose effective learning language resolves to one language (e.g. Arabic) for a given `SubjectCode`. Identify a lesson belonging to the **other-language** Subject for that same code (e.g. the English/Latin Math tree) so the language does not match the resolved language.
- **Steps:** 1. `POST /api/learning/Quizzes/{wrongLangLessonId}/Attempt`.
- **Expected:** HTTP **403** Forbidden, localized message key `LessonLanguageMismatch`. (This is the real, implemented guard the task referenced — it guards *language*, not *unlock*.)
- **Traces to:** P8-03-BE-4 language guard; brief "Learning-language guard (P8) also applies".

### BE-TC-18 — SkillTree silently serves the correct-language tree for a wrong-language subjectId
- **Type:** functional / state · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** Student JWT resolving to language X. A subject in the wrong language Y exists for the same `SubjectCode`+Grade, and the X-language counterpart also exists.
- **Steps:** 1. `GET .../Subjects/{wrongLangSubjectId}/SkillTree`.
- **Expected:** 200; the returned tree is the **resolved-language (X)** tree (skill names in language X), not the requested Y tree. Engine still runs against the effective subject. No error. (If the resolved tree is absent, fallback serves the requested tree + logs a warning — assert 200 either way; preferred-path assertion is the redirect.)
- **Traces to:** P8-03-BE-4/BE-6 redirect; AC1 (engine runs on the effective subject).

---

## Group G — Determinism & error mapping (AC4 + robustness)

### BE-TC-19 — Reproducible: two identical calls with no state change return identical state
- **Type:** functional / regression · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Any student JWT, no DB writes between the two calls.
- **Steps:** 1. `GET .../Subjects/{id}/SkillTree` → capture body. 2. `GET .../Subjects/{id}/SkillTree` again. 3. Repeat for `.../Lessons`.
- **Expected:** Both responses are **identical** (same per-skill/per-lesson `state`, same `missingPrerequisites` contents and ordering). No clock/random influence. (Pair with the engine's pure-determinism unit test — see "Defer to unit tests".)
- **Traces to:** **AC4** (reproducible for same inputs).

### BE-TC-20 — Unknown subjectId → 404, not 500
- **Type:** negative / error-mapping · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Student JWT. Use a `subjectId` that does not exist (e.g. 999999).
- **Steps:** 1. `GET .../Subjects/999999/SkillTree`. 2. `GET .../Subjects/999999/Lessons`.
- **Expected:** HTTP **404** (`SubjectNotFound`), `successed == false`. Never 500.
- **Traces to:** Status-code mapping (CONVENTIONS); robustness.

### BE-TC-21 — Subject with no concepts/units → 200 + empty collection (not 404/500)
- **Type:** state (empty) · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** Student JWT. A seeded subject that has no concepts (if none exists, mark **blocked — no fixture**).
- **Steps:** 1. `GET .../SkillTree` for that subject. 2. `GET .../Lessons`.
- **Expected:** 200 with an empty `data` array (handlers return `EmptyCollection`). Engine not invoked / returns empty without error.
- **Traces to:** Empty-state handling; robustness. **Note:** if the seed guarantees every subject has concepts, mark this case **blocked (no empty-subject fixture)** in the execution report rather than forcing it.

### BE-TC-22 — Response envelope: `"successed": true` (camelCase) + `data` present
- **Type:** functional / regression · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Student JWT.
- **Steps:** 1. `GET .../SkillTree`. 2. Inspect raw JSON.
- **Expected:** JSON contains literal `"successed":true` (camelCase, boolean) and a `"data":` array. `NodeState` values serialize as integers (0/1/2). (Mirrors the P2-09/P2-11 envelope-literal assertion.)
- **Traces to:** Envelope contract (CLAUDE.md rule 2 `Successed`; HANDOFF camelCase).

---

## Defer to unit tests (pure engine — no HTTP surface)
These are **not** integration cases; they are already (or should be) covered by `LearningPathEngineTests` (`backend/tests/Modules.Learning.UnitTests/LearningPathEngineTests.cs`). `api-tester` should **not** re-implement them over HTTP — note them as "covered by unit tests" in the report:
- **U-1 Cycle defense** — a synthetic cyclic edge set does not infinite-loop; returns deterministic state. *(No HTTP path can construct a cycle — seed-time `SkillGraphValidator` blocks it; only reachable in-memory.)*
- **U-2 Exact-threshold boundary** — `accuracy == threshold` (e.g. 80.0 vs 80) → mastered; `79.99` vs 80 → not mastered. *(HTTP accuracy is computed from integer correct/total fractions, hard to land exactly on a boundary deterministically; the unit test owns this.)*
- **U-3 Zero-answers guard** — `TotalAnswers == 0` → not mastered even if `threshold == 0`. *(Covered indirectly by BE-TC-04/05; the `threshold == 0` edge has no seeded fixture.)*
- **U-4 Pure reproducibility** — same in-memory inputs twice → byte-identical dictionary. *(BE-TC-19 is the HTTP-level analogue; U-4 is the authoritative determinism proof per AC4.)*
- **U-5 Multi-prereq AND, one unmet** — 2 prereqs, 1 mastered + 1 not → Locked, `missingPrerequisites.Count == 1`. *(No clean 2-inbound-edge fixture in the seed; engine-level test owns it.)*
