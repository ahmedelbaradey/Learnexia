# Backend Test Cases — P2-01 (Curriculum hierarchy CRUD)

**Target agent:** `api-tester`
**Implements against:** the live API (Learning module), extending
`backend/tests/Learnexia.IntegrationTests/P2_01_CurriculumHierarchy_Tests.cs`.

## Conventions used by every case below

- **Routes** (observed): per controller `api/learning/{controller}` —
  `GET …/List?PageNumber=&PageSize=[&<ParentFk>=]` · `GET …?id=` · `POST …/Create` ·
  `PUT …/Update` · `DELETE …?id=`. Subjects/Lessons also have student-facing GET routes that are out of scope here.
- **Admin auth:** sign in `POST api/Users/Authentication/Sign-In` with `superadmin / 123Pa$$word!`; use the returned
  `data.accessToken` as `Bearer` for all writes and Grade reads.
- **Non-admin auth (for 403):** sign in `basicuser / 123Pa$$word!` (role Basic). Fallback: mint a Parent token via
  `POST api/Users/Authentication/Register-Parent`.
- **Success envelope:** `BaseResponse<T>` with `statusCode`, `successed` (**spelled `Successed`** — assert the
  property exists and is boolean), `message`, `data`, `errors`. Create/Update/Delete return **HTTP 200** (handlers
  call `Success()`, **not** `Created()`) — **do not assert 201**.
- **List shape:** `BaseResponse<PaginatedResult<T>>` → `root.data.{currentPage,totalCount,totalPages,pageSize}` and
  `root.data.data` = the item array.
- **Validation:** `ICommand` bodies are validated → **HTTP 422** with `successed=false`, `errors[]` of
  `{propertyName, errorMessage}`. Queries are NOT validated.
- **Enum keys (do not change):** `DifficultyLevel { Easy=1, Medium=2, Hard=3 }`; `SubjectCode { MATH=0, SCIENCE=1,
  ARABIC=2, ENGLISH=3 }`; `ContentLanguage { Ar=0, En=1 }`.
- **"Extends/Existing"** column: if a case already exists in `P2_01_CurriculumHierarchy_Tests.cs`, the api-tester
  should **assert it is present and passing** and NOT re-author it; otherwise author it new in an extension class.
- **Seed helpers:** reuse `CreateGradeGetId` / `CreateSubjectGetId` / `CreateUnitGetId` / `CreateConceptGetId` /
  `FindIdInList` from the existing suite.

---

## Group A — CRUD happy paths & hierarchy (AC-1, AC-3)

### BE-TC-01 — Grade CRUD round-trip
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC1_Grade_CrudRoundTrip`. Cross-reference; do not duplicate.
- **Preconditions:** admin token.
- **Steps:** Create grade → List/find id → GetById → Update DisplayName → GetById (changed) → Delete.
- **Expected:** every step HTTP 200, `successed=true`; GetById reflects the update; **no step returns 201**.
- **Traces to:** AC-1.

### BE-TC-02 — Unit CRUD round-trip
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO (existing suite covers Unit only as a hierarchy step, not full update/delete).
- **Preconditions:** admin token; a grade + subject seeded.
- **Steps:** Create unit under subject → List filtered by `SubjectId` → GetById → Update `Name`+`SequenceOrder`
  → GetById (changed) → Delete → GetById of deleted id.
- **Expected:** create/update/delete HTTP 200 `successed=true`; GetById after delete → non-2xx, `successed=false`.
- **Traces to:** AC-1, AC-3.

### BE-TC-03 — Concept CRUD round-trip
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Preconditions:** admin token; grade + subject seeded.
- **Steps:** Create concept (`DifficultyLevel=2`, `Description` set) under subject → List by `SubjectId` → GetById
  → Update `Name`+`Description` → GetById (changed) → Delete.
- **Expected:** 200 `successed=true` each write; `description` and `difficultyLevel` round-trip in GetById.
- **Traces to:** AC-1.

### BE-TC-04 — Skill CRUD round-trip
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Preconditions:** admin token; grade + subject + concept seeded.
- **Steps:** Create skill (`MasteryThreshold=80`,`EstimatedTimeMinutes=30`) under concept → List by `ConceptId`
  → GetById → Update `MasteryThreshold=90` → GetById (changed) → Delete.
- **Expected:** 200 `successed=true`; updated threshold reflected in GetById.
- **Traces to:** AC-1, AC-4.

### BE-TC-05 — Full hierarchy creation Grade→Subject→Unit→Lesson→Concept→Skill
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC1_FullHierarchy_CreationRoundTrip`. Cross-reference.
- **Steps:** Create grade → subject → unit → concept → skill → lesson (no SkillId) → lesson (with SkillId);
  verify both lessons appear in the unit-filtered list.
- **Expected:** each create HTTP 200 `successed=true`; lesson-with-skill links the seeded skill; list count ≥ 2.
- **Traces to:** AC-1, AC-3.

---

## Group B — Envelope & pagination shape (AC-2)

### BE-TC-06 — Success envelope keys + `Successed` spelling
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC2_GradesList_OuterEnvelopeShape`. Cross-reference.
- **Steps:** GET Grades List with admin token.
- **Expected:** `statusCode`, **`successed`** (boolean, true), `message`, `data`, `errors` all present; assert the
  flag property name is `successed`/`Successed` (the project's deliberate spelling), not `succeeded`/`success`.
- **Traces to:** AC-2.

### BE-TC-07 — PaginatedResult metadata shape
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC2_GradesList_PaginatedResultShape`. Cross-reference.
- **Expected:** `root.data` has `currentPage`, `totalCount`, `totalPages`, `pageSize`, and inner `data` array.
- **Traces to:** AC-2.

### BE-TC-08 — Pagination honors PageNumber/PageSize
- **Type:** boundary · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Preconditions:** admin token; seed ≥ 3 grades.
- **Steps:** GET `…/grades/List?PageNumber=1&PageSize=2`; then `PageNumber=2&PageSize=2`.
- **Expected:** page 1 inner `data` length ≤ 2; `pageSize=2`; `currentPage` echoes the request; `totalCount` ≥ 3;
  `totalPages` consistent with `ceil(totalCount/2)`; page 2 returns different items than page 1.
- **Traces to:** AC-2.

### BE-TC-09 — Validation (422) envelope shape with propertyName/errorMessage
- **Type:** validation · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC2_ValidationEnvelope_Has422Shape`. Cross-reference.
- **Expected:** 422; `successed=false`; `errors[]` non-empty; each error has `propertyName` + `errorMessage`.
- **Traces to:** AC-2, AC-5.

---

## Group C — Relationship & field persistence (AC-3, AC-4)

### BE-TC-10 — Subject filtered list scopes to one grade
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC1_Subjects_FilterByGradeId_Works`. Cross-reference.
- **Expected:** GradeA filter includes SubjectA and excludes SubjectB.
- **Traces to:** AC-3.

### BE-TC-11 — Child list filters scope to their parent (Unit/Concept/Skill/Lesson)
- **Type:** functional · **Priority:** P2 · **Agent:** api-tester
- **Extends/Existing:** NO (extends the Subject-only filter test to the other edges).
- **Preconditions:** admin token; two sibling parents each with one child entity, distinct names.
- **Steps:** For each edge — Units by `SubjectId`, Concepts by `SubjectId`, Skills by `ConceptId`, Lessons by
  `UnitId` — create one child under parent X and one under parent Y; list filtered by X.
- **Expected:** each filtered list includes X's child and excludes Y's child.
- **Traces to:** AC-3.

### BE-TC-12 — Lesson `DifficultyLevel` enum round-trips as int
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC5_Lesson_DifficultyLevel_RoundTrips`. Cross-reference.
- **Expected:** lesson created with `Difficulty=3` returns `difficulty=3` (Hard) in list.
- **Traces to:** AC-4.

### BE-TC-13 — Concept `DifficultyLevel` enum round-trips as int
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC5_Concept_DifficultyLevel_RoundTrips`. Cross-reference.
- **Traces to:** AC-4.

### BE-TC-14 — Skill `MasteryThreshold` + `EstimatedTimeMinutes` round-trip
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC1_Skill_MasteryAndTimeFields_PresentInResponse`. Cross-reference.
- **Traces to:** AC-4.

### BE-TC-15 — Lesson nullable SkillId (omitted AND explicit null) accepted
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC1_Lesson_WithoutSkillId_IsAccepted` + `AC1_Lesson_ExplicitNullSkillId_IsAccepted`.
  Cross-reference both.
- **Expected:** both creates HTTP 200 `successed=true`; lesson persists with null skill link.
- **Traces to:** AC-4 (Lesson teaches ≤1 Skill, optional).

---

## Group D — Validation → 422 on every ICommand (AC-5)

> Validators observed: Grade (Number 1–6, DisplayName not empty), Subject (Name not empty, GradeId > 0),
> Unit/Lesson/Concept/Skill (Name not empty; Lesson/Concept difficulty IsInEnum; Skill MasteryThreshold 0–100).
> Send **admin token** so the request clears the auth gate and FluentValidation is the failure source.

### BE-TC-16 — Grade Create empty DisplayName → 422
- **Type:** validation · **Priority:** P0 · **Extends/Existing:** YES — `AC3_Grade_EmptyDisplayName_Returns422`. Cross-reference.

### BE-TC-17 — Grade Number out of 1–6 range → 422 (0 and 7)
- **Type:** boundary · **Priority:** P1 · **Extends/Existing:** YES — `AC3_Grade_InvalidNumber_Zero_Returns422` +
  `AC3_Grade_InvalidNumber_TooLarge_Returns422`. Cross-reference.
- **Additional boundary (NEW):** also assert Number=1 and Number=6 **succeed** (inclusive bounds) — author new if absent.

### BE-TC-18 — Subject Create empty Name → 422
- **Type:** validation · **Priority:** P0 · **Extends/Existing:** YES — `AC3_Subject_EmptyName_Returns422`. Cross-reference.

### BE-TC-19 — Subject Create GradeId=0 → 422 (GreaterThan(0))
- **Type:** validation · **Priority:** P0 · **Extends/Existing:** YES — `AC3_Subject_ZeroGradeId_Returns422`. Cross-reference.

### BE-TC-20 — Unit Create empty Name → 422
- **Type:** validation · **Priority:** P1 · **Extends/Existing:** YES — `AC3_Unit_EmptyName_Returns422`. Cross-reference.

### BE-TC-21 — Lesson Create empty Name → 422; Difficulty out of enum (0, 99) → 422
- **Type:** validation/boundary · **Priority:** P1 · **Extends/Existing:** YES —
  `AC3_Lesson_EmptyName_Returns422` + `AC3_Lesson_InvalidDifficulty_Zero_Returns422`. Cross-reference; **add**
  Difficulty=99 case if absent.

### BE-TC-22 — Concept Create empty Name → 422; DifficultyLevel=99 → 422
- **Type:** validation/boundary · **Priority:** P1 · **Extends/Existing:** YES —
  `AC3_Concept_EmptyName_Returns422` + `AC3_Concept_InvalidDifficultyLevel_Returns422`. Cross-reference.

### BE-TC-23 — Skill Create empty Name → 422; MasteryThreshold=101 → 422
- **Type:** validation/boundary · **Priority:** P1 · **Extends/Existing:** YES —
  `AC3_Skill_EmptyName_Returns422` + `AC3_Skill_MasteryThresholdOutOfRange_Returns422`. Cross-reference; **add**
  MasteryThreshold=-1 (NEW) and assert 0 and 100 (inclusive bounds) **succeed**.

### BE-TC-24 — Update commands are validated too (Edit → 422)
- **Type:** validation · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO (existing suite validates Create only; Edit commands are also `ICommand`).
- **Preconditions:** admin token; a seeded grade id.
- **Steps:** `PUT …/grades/Update` with `{ Id=<seeded>, Number=0, DisplayName="" }`.
- **Expected:** HTTP 422; `successed=false`; `errors[]` non-empty (Edit validators fire on the same rules).
- **Traces to:** AC-5.

---

## Group E — Schema / migration verification (AC-6)

### BE-TC-25 — Six curriculum tables exist in `learning` schema
- **Type:** persistence · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** NO (DB-level assertion; the existing suite asserts via API only).
- **Steps:** Via the test `LearningDbContext` (already used in the suite's `InitializeAsync`) or psql, confirm tables
  `Grades`, `Subjects`, `Units`, `Lessons`, `Concepts`, `Skills` are present under schema `learning`.
- **Expected:** all six present.
- **Traces to:** AC-6.

### BE-TC-26 — Unique index `(GradeId,SubjectCode,Language)` present on Subjects
- **Type:** persistence · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Steps:** Query Postgres catalog (`pg_indexes` / `information_schema`) for index
  `IX_Subjects_GradeId_SubjectCode_Language` on `learning."Subjects"`; assert it exists and is UNIQUE.
- **Expected:** index present and unique; FK index `IX_Subjects_GradeId` also present.
- **Traces to:** AC-6, DataInt-1.

---

## Group F — Authorization (Authz-1/2/3)

### BE-TC-27 — Anonymous Grade reads → 401
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC6_GradesList_RequiresAuth`. Cross-reference; **add** anonymous `GET …/grades?id=1`
  → 401 (class-level `[Authorize]` covers GetById too) if absent.
- **Expected:** 401 Unauthorized without a token.
- **Traces to:** Authz-1.

### BE-TC-28 — Anonymous writes → 401 on all six controllers
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** NO (existing suite never sends an anonymous write).
- **Steps:** Without a token, send `POST …/Create`, `PUT …/Update`, `DELETE …?id=1` for grades, subjects, units,
  lessons, concepts, skills (use a `[Theory]` matrix; minimal/empty bodies are fine — the auth gate fires first).
- **Expected:** every request → **401** (not 422, not 403, not 500) — the auth challenge precedes model binding.
- **Traces to:** Authz-1.

### BE-TC-29 — Non-admin authenticated write → 403 on all six controllers (AdminOnly gate)
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Preconditions:** non-admin token — `basicuser / 123Pa$$word!` (role Basic). Parent token is an acceptable
  alternative.
- **Steps:** With the non-admin bearer, send `POST …/Create`, `PUT …/Update`, `DELETE …?id=1` for all six
  controllers (`[Theory]` over controller × verb).
- **Expected:** every write → **403 Forbidden** (authenticated but lacks AdminOnly). Reads on the five non-grade
  controllers remain reachable for this user; Grade reads return 200 (any authenticated user). A single regression
  that loosens one controller's write attribute must fail this case.
- **Traces to:** Authz-2, Product (no teacher role — even an elevated non-admin cannot author curriculum).

---

## Group G — Unique constraint / duplicate conflict (DataInt-1)

> **Context (see README R1 / Q1):** `AddSubjectCommand` cannot set `SubjectCode`/`Language`, so every API-created
> subject defaults to `(MATH, Ar)`. A second subject under the **same grade** therefore collides on
> `IX_Subjects_GradeId_SubjectCode_Language`. **Do not hard-code 409** — record the actual status; current code
> path is `ServerError` (500). Assert *rejection* + integrity, not a specific code.

### BE-TC-30 — Second subject under same grade violates unique key → rejected
- **Type:** negative/persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions:** admin token; one fresh grade.
- **Steps:** Create subject A under the grade (200). Create subject B (different `Name`, same `GradeId`).
- **Expected:** subject B create is **rejected** — status is **non-2xx**, body is a valid JSON `BaseResponse`
  envelope (parses, not a raw exception page), `successed=false`. **Record the actual status code** in the
  execution report (expected 500 today; flag if 409/422).
- **Traces to:** DataInt-1, AC-6.

### BE-TC-31 — Same-name duplicate under same grade → rejected (envelope, not crash)
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Create subject A; create subject A2 with the *identical* Name under the same grade.
- **Expected:** rejected; valid JSON envelope; `successed=false`; no stack-trace leak in body.
- **Traces to:** DataInt-1.

### BE-TC-32 — First subject still retrievable after the duplicate fails (no partial corruption)
- **Type:** persistence · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Create subject A (capture id via filtered list). Attempt duplicate B (fails per BE-TC-30).
  GET subject A by id; List subjects filtered by the grade.
- **Expected:** subject A still returns 200 with intact fields; the grade's subject list contains exactly A (B was
  not persisted). Demonstrates the failed write rolled back cleanly under the UoW behavior.
- **Traces to:** DataInt-1, persistence integrity.

---

## Group H — FK integrity: child under non-existent parent (DataInt-2)

> Each asserts: non-2xx, valid JSON `BaseResponse` envelope, `successed=false`, no naked exception page. Record the
> actual status code (current path is FK violation at SaveChanges → `ServerError` 500; lead may prefer 404/422 — Q2).
> Use a non-existent parent id (e.g. 999999) that passes the `GreaterThan(0)` validator.

### BE-TC-33 — Subject under non-existent GradeId → rejected
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** YES — `AC4_Subject_NonExistentGradeId_FailsGracefully`. Cross-reference.
- **Traces to:** DataInt-2.

### BE-TC-34 — Unit under non-existent SubjectId → rejected
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Steps:** admin token; `POST …/units/Create` with `SubjectId=999999`, valid Name/SequenceOrder.
- **Expected:** non-2xx, valid envelope, `successed=false`. Record status.
- **Traces to:** DataInt-2 (Unit→Subject Restrict FK).

### BE-TC-35 — Concept under non-existent SubjectId → rejected
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Steps:** admin token; `POST …/concepts/Create` with `SubjectId=999999`, valid Name, DifficultyLevel=1.
- **Expected:** non-2xx, valid envelope, `successed=false`.
- **Traces to:** DataInt-2 (Concept→Subject Restrict FK).

### BE-TC-36 — Lesson under non-existent UnitId, and Skill under non-existent ConceptId → rejected
- **Type:** negative · **Priority:** P1 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Steps:** admin token; (a) `POST …/lessons/Create` `UnitId=999999`, valid other fields, no SkillId;
  (b) `POST …/skills/Create` `ConceptId=999999`, valid Name/threshold/time.
- **Expected:** both non-2xx, valid envelope, `successed=false`. (Bonus: Lesson with valid UnitId but non-existent
  `SkillId=999999` — record whether it is rejected or accepted-as-null, since the FK is SetNull/optional.)
- **Traces to:** DataInt-2 (Lesson→Unit, Skill→Concept Restrict; Lesson→Skill SetNull).

---

## Group I — Product overrides (negative assertions)

### BE-TC-37 — `SubjectCode` enum admits only the 4 product subjects (no Social Studies)
- **Type:** negative/regression · **Priority:** P2 · **Agent:** api-tester
- **Extends/Existing:** NO.
- **Rationale:** the product mandates exactly Math/Science/Arabic/English and **no Social Studies / no 5th subject**.
- **Steps:** (a) Assert `GetSubjectsForGrade` (or the seeded subject set) exposes exactly the four codes
  `{MATH, SCIENCE, ARABIC, ENGLISH}` and never a 5th. (b) If a writable SubjectCode path exists, attempt to create a
  subject with an out-of-range `SubjectCode` (e.g. 4) and assert it is rejected/clamped — **note:** the public
  `AddSubjectCommand` does NOT expose `SubjectCode`, so this sub-step is **documentation-only / not testable via the
  current Create endpoint** (mark blocked with that reason rather than dropping it).
- **Expected:** the four-subject invariant holds; no Social Studies code is producible/observable.
- **Traces to:** Product (4 subjects, no Social Studies). Note: "no teacher role" is covered by BE-TC-29 (no teacher
  principal exists; only admin can author).
