# Backend Test Cases — P2-03 Navigate the skill tree (READ + node status)

> **Target agent:** `api-tester`. Implement each case 1:1 (suggested file: `backend/tests/Learnexia.IntegrationTests/P2_03_SkillTree_Tests.cs`).
> **Reference harness:** mirror `P2_09_HomeDashboard_Tests.cs` — `LearnexiaWebAppFactory`, `ApplyMigrationsAndSeedAsync()` + `LearningSeeder.SeedAsync(...)` in `InitializeAsync`, `CreateStudentViaParentFlowAsync` (Register-Parent → Add-Child → Sign-In), `TryProp` case-insensitive JSON lookup.
> **Record results in** `execution-report.md` (one row per `BE-TC-*`).

## Endpoints under test

| Tag | Method + route | Auth | Envelope `data` type |
|---|---|---|---|
| **E1** | `GET /api/learning/Subjects/{id}/SkillTree` | `[Authorize]` | `List<ConceptNodeDto>` — `{ conceptId, name, skills:[{ skillId, name, masteryThreshold, estimatedTimeMinutes, state, lessonIds, missingPrerequisites? }] }` |
| **E2** | `GET /api/learning/Subjects/{id}/Lessons` | `[Authorize]` | `List<UnitWithLessonsDto>` — `{ unitId, name, sequenceOrder, lessons:[{ lessonId, name, difficulty, sequenceOrder, isLocked, skillId, isBoss, state, missingPrerequisites }] }` |
| **E3** | `GET /api/learning/Lessons/{id}` | `[Authorize]` | `SingleLessonResponse` — `{ ..., isBoss, quickCheck? }` |

## Seed facts (grounding — confirmed in `LearningSeeder.cs`)

- Each grade seeds **6 subject roots**: MATH/Ar, MATH/En, SCIENCE/Ar, SCIENCE/En, ARABIC/Ar, ENGLISH/En.
- Resolution: ARABIC→Ar pinned, ENGLISH→En pinned, MATH/SCIENCE→student's `learning_language`. So an **English-medium** (`LearningLanguage="en"`) student sees MATH/En, SCIENCE/En, ARABIC/Ar, ENGLISH/En.
- **Math** tree per grade: **5 concepts × 3 skills = 15 skills** (En tree skill names: `Count to 1000 (G1)`, `Compare and Order Numbers (G1)`, `Add Single-Digit Numbers (G1)`, … ). Units 1–5, lessons per unit (Math = 3).
- **Prerequisite edges exist for MATH only** (Ar + En). G1 En chain: `Count to 1000 (G1)` → `Compare and Order Numbers (G1)` → `Add Single-Digit Numbers (G1)` (intra-G1), then `Add Single-Digit Numbers (G1)` → `Subtract Within 100 (G2)` (cross-grade). Science/Arabic/English have nodes but **no prereq edges**.
- **`IsBoss`** = the highest-`SequenceOrder` lesson of each unit (seeder `MarkBossLessonsAsync`). Default `false` on all other lessons.
- `NodeState` serialises as **int** by default (0=Locked, 1=Available, 2=Completed). Tolerate string too.
- Resolve subject IDs in `InitializeAsync` by querying `Subjects` for `(SubjectCode, Language, Grade.Number)` — exactly as P2-09 does for `_mathG1SubjectId` / `_scienceG1SubjectId`. Also resolve a Grade-2 Math/En id (`_mathG2SubjectId`) and the **Math/Ar** G1 id (`_mathG1ArSubjectId`) for the language cases.

---

## Group A — Skill-tree happy path, shape & node status (E1)

### BE-TC-01 — Skill tree happy path: full concept→skill shape for an authenticated student
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student via parent flow (`LearningLanguage="en"`). `_mathG1SubjectId` resolved.
- **Steps:**
  1. `GET /api/learning/Subjects/{_mathG1SubjectId}/SkillTree` with the student bearer token.
  2. Parse the `data` array.
- **Expected:** HTTP **200**; `data` is a non-empty array of concept nodes. Math G1 has **5 concepts**; each concept has **3 skills** (15 skills total). Each skill object has `skillId>0`, non-empty `name`, `masteryThreshold` in 0..100, `estimatedTimeMinutes>=0`, a numeric `state`, and a `lessonIds` array. Concepts are ordered by `conceptId` ascending.
- **Traces to:** AC1, "tree shape matches the seeded graph".

### BE-TC-02 — Envelope shape + `Successed` spelling on the skill-tree response
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** As BE-TC-01.
- **Steps:** `GET .../SkillTree`; inspect the raw response body and root envelope.
- **Expected:** Body contains the literal `"successed":` (camelCase, single-`s`-then-`successed`, the load-bearing CONVENTIONS spelling); `successed == true`; envelope has `statusCode`, `message`, `data`, `errors` keys. `statusCode` == 200.
- **Traces to:** Response-envelope contract (CLAUDE.md rule 2).

### BE-TC-03 — Anonymous skill-tree request → 401
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** `_mathG1SubjectId` resolved. No bearer token.
- **Steps:** `GET /api/learning/Subjects/{_mathG1SubjectId}/SkillTree` with **no** Authorization header.
- **Expected:** HTTP **401 Unauthorized** (controller action has `[Authorize]`). No `data` payload leaked.
- **Traces to:** Auth — 401 without JWT.

### BE-TC-04 — Fresh student: root skill Available, prereq-gated skills Locked
- **Type:** state · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student (no attempts). `_mathG1SubjectId` resolved.
- **Steps:**
  1. `GET .../SkillTree`. Flatten all skills with their `name` + `state`.
  2. Locate the root skill `Count to 1000 (G1)` and the downstream `Add Single-Digit Numbers (G1)`.
- **Expected:** HTTP 200. `Count to 1000 (G1)` (graph root, no incoming prereq) → `state == 1` (Available). `Add Single-Digit Numbers (G1)` (has an unmet prereq via the G1 chain) → `state == 0` (Locked). At least one skill is Available and at least one is Locked (the tree is not uniformly one state for a fresh student).
- **Traces to:** AC1, AC3, "fresh student — only root unlocked, rest locked".

### BE-TC-05 — Fresh student: no skill is Completed
- **Type:** state · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student (no attempts). `_mathG1SubjectId`.
- **Steps:** `GET .../SkillTree`; collect all skill `state` values.
- **Expected:** HTTP 200. **No** skill has `state == 2` (Completed) — a student with zero attempts cannot have completed anything.
- **Traces to:** AC3 (states reflect progress).

### BE-TC-06 — Locked skill carries `missingPrerequisites` (why-locked reason)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId`.
- **Steps:**
  1. `GET .../SkillTree`. Find a skill with `state == 0` (Locked) — e.g. `Add Single-Digit Numbers (G1)`.
  2. Inspect its `missingPrerequisites` array.
- **Expected:** HTTP 200. The Locked skill's `missingPrerequisites` is a **non-null, non-empty** array. Each entry has `prereqSkillId>0`, non-empty `prereqSkillName`, `requiredAccuracy` (0..100), and `currentAccuracy` (>= 0). The named prereq matches the seeded edge source (e.g. `Compare and Order Numbers (G1)`).
- **Traces to:** AC2 (why-locked reason). Cross-ref: P2-04 populates this; this case is the READ regression guard.

### BE-TC-07 — Available/Completed skill has empty-or-absent `missingPrerequisites`
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** As BE-TC-06.
- **Steps:** `GET .../SkillTree`; for every skill with `state == 1` (Available), inspect `missingPrerequisites`.
- **Expected:** HTTP 200. Available skills have `missingPrerequisites` either `null` or empty (no "why-locked" reason when not locked). No Available skill exposes a non-empty prereq list.
- **Traces to:** AC2 (reason only when locked).

### BE-TC-08 — Prerequisite-edge correctness: the named prereq matches the seeded graph
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId`. Seeded G1 En edge `Compare and Order Numbers (G1)` → `Add Single-Digit Numbers (G1)`.
- **Steps:**
  1. `GET .../SkillTree`. For the Locked skill `Add Single-Digit Numbers (G1)`, read `missingPrerequisites[].prereqSkillName`.
- **Expected:** HTTP 200. The missing-prereq for `Add Single-Digit Numbers (G1)` includes `Compare and Order Numbers (G1)` (the seeded direct predecessor). The edge shape exposed by the READ matches the seeded `KnowledgeEdge` topology — not an arbitrary or cross-language skill name.
- **Traces to:** AC2, "tree shape / edges match the seeded graph".

### BE-TC-09 — Cross-language skill tree: handler SILENTLY REDIRECTS to the correct-language tree (NOT 403)
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **⚠ Behaviour-discrepancy — resolve Open Question #1 in README before coding the assertion.** The dispatch brief says "cross-language → 403"; the actual `GetSubjectSkillTreeQueryHandler` (lines 74-97) does a silent redirect to the same `SubjectCode`+`GradeId` correct-language tree and returns 200. Only the single-lesson endpoint (BE-TC-23) returns 403.
- **Preconditions / seed:** **English-medium** student (`LearningLanguage="en"`). Resolve `_mathG1ArSubjectId` = the **Math/Ar** Grade-1 subject (the "wrong language" tree for this student).
- **Steps:**
  1. `GET /api/learning/Subjects/{_mathG1ArSubjectId}/SkillTree` with the English-medium student token.
  2. Parse `data`; capture the skill names returned.
- **Expected (default per Open Question #1, option a — code is correct):** HTTP **200** (NOT 403). The returned tree is the **English** Math G1 tree (skills resolved to `*(G1)` English names like `Count to 1000 (G1)`), **not** the Arabic `*(ص1)` names — proving the handler redirected the wrong-language `subjectId` to the student's resolved-language tree. `successed == true`.
- **Traces to:** Learning-language filter; cross-language behaviour. **If the lead rules 403 is intended for E1, this becomes a product-defect ticket, not a test change.**

### BE-TC-10 — Grade & language scoping: English-medium student gets the En tree for their grade
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId` (Math/En G1).
- **Steps:** `GET /api/learning/Subjects/{_mathG1SubjectId}/SkillTree`; read all skill names.
- **Expected:** HTTP 200. All skill names carry the **English** grade suffix `(G1)` (e.g. `Count to 1000 (G1)`); none carry the Arabic `(ص1)` suffix or a different grade number. The tree is the student's grade + resolved language.
- **Traces to:** Grade scoping + learning-language filter.

### BE-TC-11 — Arabic-medium student gets the Ar Math tree (mirror of BE-TC-10)
- **Type:** functional / RTL-i18n · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Create a Grade-1 student via parent flow with `LearningLanguage="ar"`. Resolve `_mathG1ArSubjectId` (Math/Ar G1).
- **Steps:** `GET /api/learning/Subjects/{_mathG1ArSubjectId}/SkillTree` with the Arabic-medium token.
- **Expected:** HTTP 200. Skill names carry the **Arabic** grade suffix `(ص1)` (e.g. `العد حتى 1000 (ص1)`). Engine-derived states present (root Available, prereq-gated Locked, mirroring BE-TC-04 in the Ar tree). Confirms language filtering follows the JWT `learning_language` claim, not the URL.
- **Traces to:** Learning-language filter (both directions).

### BE-TC-12 — Grade scoping nuance: node status is the student's; cross-grade subject still served (curriculum-public)
- **Type:** auth-authz / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium **Grade-1** student. Resolve `_mathG2SubjectId` (Math/En **Grade-2**).
- **Steps:** `GET /api/learning/Subjects/{_mathG2SubjectId}/SkillTree` with the Grade-1 student token.
- **Expected:** HTTP **200** (the curriculum tree is not access-restricted by grade — there is no IDOR vector and no `grade` route param). The returned states are computed for **this** student (a Grade-1 student with no G2 progress → G2 root `Count to 1000 (G2)` Available, downstream Locked). This documents that grade scoping governs *which subject the student normally reaches*, not a hard 403 on other grades. **No PII / no other student's data is exposed.**
- **Traces to:** Grade scoping (R5), AC3.

### BE-TC-13 — Product override: exactly the 4 product subjects, no Social Studies
- **Type:** negative / functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. Use `GET /api/learning/Subjects/ForGrade?grade=1` (sibling browse endpoint) to enumerate visible subjects.
- **Steps:**
  1. `GET /api/learning/Subjects/ForGrade?grade=1` with the student token.
  2. Collect the subject names/codes returned.
- **Expected:** HTTP 200. The visible subjects map to the 4 product subjects **Math, Science, Arabic, English**. **No** "Social Studies" subject appears anywhere. (Product override: 4 subjects, no Social Studies.)
- **Traces to:** Product override — 4 subjects, no Social Studies.

### BE-TC-14 — Concept-less / empty-tree subject → 200 + empty collection (conditional)
- **Type:** boundary · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** A subject row with **no concepts**. *Not naturally seeded* — see Open Question #4. If `api-tester` can synthesise a bare subject row (insert via DbContext) for the student's grade+language, use it; otherwise **mark NOT-TESTABLE-WITHOUT-FIXTURE** in the report with the blocker.
- **Steps:** `GET /api/learning/Subjects/{bareSubjectId}/SkillTree`.
- **Expected:** HTTP 200; `data` is an **empty array** (handler returns `EmptyCollection`); `successed == true`.
- **Traces to:** Empty-state handling. **Blocker:** needs a fixture not in the standard seed.

### BE-TC-15 — Non-existent subject id → 404
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Authenticated student. Choose a `subjectId` guaranteed absent (e.g. `2_000_000_000`).
- **Steps:** `GET /api/learning/Subjects/2000000000/SkillTree` with the student token.
- **Expected:** HTTP **404 Not Found**; envelope `successed == false`; message is the localized `SubjectNotFound` (not a raw exception/stack). No `ex.Message` leakage.
- **Traces to:** Status mapping (404); negative path.

---

## Group B — Unit→lesson leaves & the boss flag (E2)

### BE-TC-16 — Lessons endpoint happy path: unit→lesson shape + envelope
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId`.
- **Steps:** `GET /api/learning/Subjects/{_mathG1SubjectId}/Lessons` with the student token.
- **Expected:** HTTP 200. `data` is a non-empty array of units ordered by `sequenceOrder`. Each unit's `lessons` are ordered by `sequenceOrder`; each lesson has `lessonId>0`, non-empty `name`, numeric `difficulty`, numeric `state`, an `isBoss` boolean, and a `missingPrerequisites` array. Envelope `successed == true`.
- **Traces to:** AC1, AC1-boss, envelope.

### BE-TC-17 — Boss flag: exactly one boss per unit = highest `sequenceOrder`; all others false
- **Type:** functional / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId` (Math G1 = 5 units).
- **Steps:**
  1. `GET .../Lessons`. For each unit, collect `lessons[].{sequenceOrder, isBoss}`.
- **Expected:** HTTP 200. For **every** unit: exactly **one** lesson has `isBoss == true`, and that lesson is the one with the **highest `sequenceOrder`** in the unit; every other lesson has `isBoss == false`.
- **Traces to:** AC1-boss; "exactly one boss per unit."

### BE-TC-18 — Boss flag works across a second subject (Science)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_scienceG1SubjectId` (Science G1).
- **Steps:** `GET /api/learning/Subjects/{_scienceG1SubjectId}/Lessons`; apply the BE-TC-17 assertion.
- **Expected:** HTTP 200. Each Science unit has exactly one highest-`sequenceOrder` boss; all others false. Proves the boss mark is subject-agnostic (not Math-only).
- **Traces to:** AC1-boss (cross-subject).

### BE-TC-19 — Boss lesson can be Locked (boss flag is orthogonal to state)
- **Type:** state / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId`.
- **Steps:** `GET .../Lessons`. Find a lesson with `isBoss == true` whose `state == 0` (Locked).
- **Expected:** HTTP 200. At least one lesson is **both** `isBoss == true` **and** `state == 0` (Locked) for a fresh student (the end-of-unit boss of a not-yet-reachable unit). Confirms boss-ness does not imply unlocked.
- **Traces to:** AC1-boss orthogonality (R4).

### BE-TC-20 — Lesson-level "why locked": Locked lesson carries `missingPrerequisites`
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Fresh English-medium Grade-1 student. `_mathG1SubjectId`.
- **Steps:** `GET .../Lessons`. Find a lesson with `state == 0` (Locked) that is skill-linked to a prereq-gated skill; read `missingPrerequisites`.
- **Expected:** HTTP 200. The Locked lesson's `missingPrerequisites` is non-empty with valid `prereqSkillName`/`requiredAccuracy`/`currentAccuracy`. (Mirrors BE-TC-06 at the lesson granularity — regression guard for P2-04.)
- **Traces to:** AC2 (lesson-level reason).

### BE-TC-21 — Progressed student: completing the root lesson flips its state to Completed and unlocks the next
- **Type:** state / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** English-medium Grade-1 student. Seed a **Completed** `Attempt` directly in the DB for the first lesson of Math G1 Unit-1 (use the `SeedCompletedAttemptAsync` + `GetFirstLessonInSubjectAsync` helpers from P2-09 tests).
- **Steps:**
  1. Seed the Completed attempt on the root lesson.
  2. `GET /api/learning/Subjects/{_mathG1SubjectId}/Lessons`.
  3. Inspect the completed lesson's `state` and a downstream lesson's `state`.
- **Expected:** HTTP 200. The completed lesson has `state == 2` (Completed). At least one previously-Locked downstream lesson/skill now has `state == 1` (Available) — node status reflects the recorded progress. `isBoss` values unchanged by progress (still structural).
- **Traces to:** AC3 (states reflect mastery/progress); persistence (recorded attempt is reflected on re-read).

### BE-TC-22 — Boss tally is stable and structurally one-per-unit across the seeded DB (idempotency-adjacent)
- **Type:** persistence / regression · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** Standard seed applied. Direct DB access via `LearningDbContext`.
- **Steps:**
  1. Query `Lessons` grouped by `UnitId`; for each unit assert exactly one `IsBoss == true` row and it is the max-`SequenceOrder` row.
  2. Assert `Count(Lessons WHERE IsBoss==true) == Count(distinct UnitId)` (one boss per unit).
  3. *(Optional, per Open Question #3)* assert the absolute tally `IsBoss==true` count equals the documented 66 (skip if seed drift makes this brittle).
- **Expected:** Boss count equals the number of units; no unit has 0 or 2+ bosses. Re-running `LearningSeeder.SeedAsync` does not change the count (idempotent mark step).
- **Traces to:** AC1-boss; seeder idempotency (cross-ref brief Q3/Q12).

### BE-TC-23 — Cross-language SINGLE-LESSON read → 403 Forbidden (the one true 403 surface)
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** **English-medium** student. Resolve a lesson id that belongs to the **Math/Ar** G1 tree (the wrong-language tree for this student) — query `Lessons` joined to `Units.Subject` where `Subject == _mathG1ArSubjectId`.
- **Steps:** `GET /api/learning/Lessons/{arLessonId}` with the English-medium student token.
- **Expected:** HTTP **403 Forbidden** (`GetLessonQueryHandler` language guard, lines 86-90); envelope `successed == false`; message is localized `LessonLanguageMismatch`. **Contrast BE-TC-09:** the skill-tree/lessons endpoints redirect; only this direct single-lesson read forbids.
- **Traces to:** Learning-language guard → 403 (the genuine 403 case for this story).

### BE-TC-24 — Anonymous single-lesson + anonymous lessons-list → 401
- **Type:** auth-authz · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** A valid lesson id and `_mathG1SubjectId`. No bearer token.
- **Steps:**
  1. `GET /api/learning/Lessons/{anyLessonId}` with no Authorization header.
  2. `GET /api/learning/Subjects/{_mathG1SubjectId}/Lessons` with no Authorization header.
- **Expected:** Both return HTTP **401 Unauthorized** (both actions are `[Authorize]`). No lesson/tree data leaked to an anonymous caller.
- **Traces to:** Auth — 401 without JWT (E2 + E3).

---

## Implementation notes for `api-tester`

- **Resolve Open Question #1 first.** BE-TC-09 asserts **silent-redirect → 200** per the real code; do
  not code a 403 assertion for E1 unless the lead reclassifies the behaviour as a defect.
- Reuse the P2-09 helpers verbatim: `CreateStudentViaParentFlowAsync` (pass `LearningLanguage="en"` or
  `"ar"`), `SeedCompletedAttemptAsync`, `GetFirstLessonInSubjectAsync`, `TryProp`, `SendAsync`.
- `NodeState` is an int in JSON (0/1/2). Use a tolerant read (int or `"Locked"/"Available"/"Completed"`).
- Do **not** assert `SkillNodeDto.HasBoss` — it does not exist (brief Q8). Boss assertions are E2/E3 only.
- Do **not** regress `P2_04_LearningPath_Tests.cs` or `P2_09_HomeDashboard_Tests.cs` (the dashboard
  `isBoss == false` assertion in P2-09 C03 is already present).
- For BE-TC-14, if no bare-subject fixture is available, record **NOT-TESTABLE-WITHOUT-FIXTURE** with the
  blocker rather than dropping the case.
