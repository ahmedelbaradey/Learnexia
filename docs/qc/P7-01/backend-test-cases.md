# P7-01 — Subjects & Units admin — Backend API test cases

> Target agent: `api-tester`. Implement the **GAP** cases as new `[Fact]`/`[Theory]` methods, ideally in a new
> `P7_01b_SubjectsUnitsRegression_Tests.cs` collection file (keeps PR #183 regressions grouped), reusing the
> existing helpers in `P7_01_SubjectsUnitsAdmin_Tests.cs` (`SendAsync`, `CreateGradeGetIdAsync`,
> `CreateSubjectGetIdAsync`, `CreateUnitGetIdAsync`, `AssertRejected`, `TryProp`).
>
> Surface under test (all `[Authorize(AdminOnly)]` unless noted):
> - `SubjectsController` — `List`, `GetById`, `Create`, `Update`, `Delete`, `Reorder`, `{id}/Active`, `Coverage`
> - `UnitsController` — `List`, `GetById`, `Create`, `Update`, `Delete`, `Reorder`, `{id}/Active`
> - `GradesController` — `Create`, `Update`, `Delete` (AdminOnly); `List`/`GetById` (any authed user)
>
> Status mapping recap: handler `BadRequest`/`NotFound` → HTTP **200** with `Successed=false` (BaseResponseHandler maps
> the `BaseResponse.StatusCode` into the envelope, controller `NewResult` returns 200 for the soft-fail path used here),
> `ValidationBehavior` on `ICommand` → **422**, auth → **401/403**, uncaught exception → **500** (a defect for these cases).

Legend: **Covered** = an existing integration test already asserts this (file + method cited). **GAP** = no existing test; `api-tester` should implement.

---

## Group A — PR #183 regression (Edit/Delete must be 404/400, never 500, never leak `ex.Message`)  ★ headline gap

These are the regressions PR #183 (P7_01b) closed. The existing suite only covers **Create**-path 404/422 guards
(`P2_01_CurriculumHierarchy_Extended_Tests` BE-TC-30..36) — the **Update** and **Grade-Delete** paths are untested.
Every case here asserts (a) the HTTP/envelope status is the graceful one (404/400 → 200 `Successed=false`, or HTTP 404),
(b) **not 500**, and (c) the response body does **not** leak a raw exception (`message` contains no `"at "` stack frame,
no `"Exception"`, no `"Npgsql"`, no SQL text).

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected result | Covered / GAP |
|----|-------|------|-----|---------------------|-------|-----------------|---------------|
| BE-TC-01 | Subject Update with non-existent Id → 404, no leak | regression/negative | P0 | admin token; pick `id=999999999` (never created) | PUT `/api/learning/Subjects/Update` body `{ Id:999999999, Name:"X", Country:"EG", GradeId:1, SubjectCode:0, Language:0 }` | NOT 500. `Successed=false`; `statusCode` 404 (or HTTP 404). `message` non-empty, human-readable, **no** stack-trace / `Npgsql` / SQL substring. | **GAP** |
| BE-TC-02 | Unit Update with non-existent Id → 404, no leak | regression/negative | P0 | admin token | PUT `/api/learning/Units/Update` body `{ Id:999999999, Name:"X", SequenceOrder:1, SubjectId:<valid> }` | NOT 500. `Successed=false`; 404 mapping; no `ex.Message` leak. | **GAP** |
| BE-TC-03 | Grade Update with non-existent Id → 404, no leak | regression/negative | P0 | admin token | PUT `/api/learning/grades/Update` body `{ Id:999999999, Number:3, DisplayName:"G3" }` | NOT 500. `Successed=false`; 404 mapping; no `ex.Message` leak. | **GAP** |
| BE-TC-04 | Grade Delete that still has subjects → 400 "grade not empty", no leak, no FK-500 | regression/negative | P0 | admin token; create grade `g`, then create a Subject under `g` | DELETE `/api/learning/grades?id=<g>` | NOT 500 (the old FK violation). `Successed=false`; clear "not empty"/has-subjects message; no SQL/FK text leaked. Grade still present in `grades/List`. | **GAP** |
| BE-TC-05 | Grade Delete (empty) → succeeds | regression/functional | P1 | admin token; create grade `g` with no subjects | DELETE `/api/learning/grades?id=<g>` | 200 `Successed=true`; grade absent from `grades/List`. | **GAP** |
| BE-TC-06 | Subject Update to a duplicate `(GradeId,SubjectCode,Language)` → 400/422, no leak, no unique-index-500 | regression/negative | P0 | admin token; create MATH/Ar and SCIENCE/Ar in grade `g` | PUT `/api/learning/Subjects/Update` on SCIENCE/Ar changing `SubjectCode`→MATH (collides with existing MATH/Ar) | NOT 500. `Successed=false` (or 422); informative duplicate-tree message; no unique-index/Npgsql text. Both subjects unchanged on reload. | **GAP** |
| BE-TC-07 | Unit Update moving to non-existent SubjectId → 404, no leak | regression/negative | P1 | admin token; create unit `u` under subject | PUT `/api/learning/Units/Update` body `{ Id:<u>, Name:"X", SequenceOrder:1, SubjectId:999999999 }` | NOT 500. `Successed=false`; 404 mapping; no FK/SQL leak. | **GAP** |
| BE-TC-08 | Subject Delete non-existent Id → 404, no leak | regression/negative | P1 | admin token | DELETE `/api/learning/subjects?id=999999999` | NOT 500. `Successed=false`; 404 mapping; no leak. | **GAP** |
| BE-TC-09 | Unit Delete non-existent Id → 404, no leak | regression/negative | P1 | admin token | DELETE `/api/learning/units?id=999999999` | NOT 500. `Successed=false`; 404 mapping; no leak. | **GAP** |

> **Implementer note for the "no leak" assertion:** parse `message` (and `errors`) and assert it does NOT contain any of:
> `" at "`, `"System."`, `"Npgsql"`, `"DbUpdateException"`, `"violates"`, `"constraint"`, `"SELECT "`. A friendly message
> like "Grade has subjects and cannot be deleted" is the pass condition.

---

## Group B — Update / happy-path round-trips (untested today)

The existing suite never exercises the **Update** (Edit) success path for Subjects, Units, or Grades, nor a Subject
Create→GetById round-trip via the admin route.

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected result | Covered / GAP |
|----|-------|------|-----|---------------------|-------|-----------------|---------------|
| BE-TC-10 | Subject Update happy path persists Name + SequenceOrder | functional/persistence | P1 | admin token; create MATH/Ar subject `s` | PUT `/api/learning/Subjects/Update` changing `Name` and `SequenceOrder`; then GET `/Subjects?id=<s>` (GetById) | 200 `Successed=true`; GetById reflects new Name + SequenceOrder; `SubjectCode`/`Language` unchanged. | **GAP** |
| BE-TC-11 | Unit Update happy path persists Name + SequenceOrder | functional/persistence | P1 | admin token; create unit `u` | PUT `/api/learning/Units/Update`; GET `/Units?id=<u>` | 200 `Successed=true`; GetById reflects new values; SubjectId unchanged. | **GAP** |
| BE-TC-12 | Subject Create→GetById admin round-trip exposes code/language/order/active | functional | P2 | admin token | POST Create MATH/Ar; GET `/Subjects?id=<new>` | 200; DTO has `subjectCode`, `language`, `sequenceOrder`, `isActive=true`. (List variant Covered: `AC1_SubjectList_ExposesP701Fields`.) | **GAP** (GetById path) |
| BE-TC-13 | Subject Update with Number/Name empty (ICommand) → 422 | validation | P2 | admin token; subject `s` | PUT Update with `Name:""` | 422; `Successed=false`. (Grade variant Covered: P2-01 BE-TC-24.) | **GAP** (Subject) |

---

## Group C — Auth matrix (largely covered; named gaps)

| ID | Title | Type | Pri | Steps | Expected result | Covered / GAP |
|----|-------|------|-----|-------|-----------------|---------------|
| BE-TC-14 | Reorder/SetActive/Coverage anonymous→401, basic/parent→403 | auth-authz | P0 | per endpoint | 401 / 403 | **Covered** — `P7_01_SubjectsUnitsAdmin_Tests` AC9_* (Reorder, SetActive, Coverage, Subject+Unit) |
| BE-TC-15 | Subjects/Units `List` + `GetById` anonymous→401, non-admin→403 | auth-authz | P1 | GET `/Subjects/List`, `/Subjects?id=1`, `/Units/List`, `/Units?id=1` with null / basic token | 401 / 403 (admin DTO lockdown) | **GAP** — P2-01 covers Create/Update/Delete auth on the 6 controllers, but the **admin `List`/`GetById` 401/403 lockdown** for Subjects/Units is not asserted (it IS asserted for Skills in P7-03 AC-AUTH-2). |
| BE-TC-16 | Subject/Unit Update + Delete non-admin→403 | auth-authz | P1 | PUT Update / DELETE with basic token | 403 | **Covered** — `P2_01_CurriculumHierarchy_Extended_Tests` BE-TC-29 (PUT Update, DELETE on all six controllers) |
| BE-TC-17 | Grades `List`/`GetById` reachable by non-admin authed user (200, not 403) | auth-authz | P2 | GET `/grades/List` with basic token | 200 (class-level `[Authorize]`, not AdminOnly) | **GAP** — confirms Grades read is intentionally broader than Subjects/Units read. |

---

## Group D — Reorder / SetActive / Delete-guard / Coverage (covered — listed for traceability)

| ID | Title | Pri | Expected result | Covered / GAP |
|----|-------|-----|-----------------|---------------|
| BE-TC-18 | Subject Reorder persists order; scoped to language tree; cross-tree→`Successed=false` | P0 | order persists; En tree untouched; cross-tree rejected | **Covered** — AC3_SubjectReorder_PersistsSequenceOrder / AC3_SubjectReorder_IsScopedToLanguageTree / AC4_SubjectReorder_CrossTree_Rejected |
| BE-TC-19 | Unit Reorder persists order | P1 | order persists | **Covered** — AC3_UnitReorder_PersistsSequenceOrder |
| BE-TC-20 | Reorder validators: empty list→422; id=0→422 (Subjects+Units) | P1 | 422 | **Covered** — AC11_* (4 tests) |
| BE-TC-21 | Reorder single-item allowed; non-existent ids→`Successed=false` | P2 | as titled | **Covered** — AC3_SubjectReorder_SingleItem_Succeeds / AC3_SubjectReorder_NonExistentIds_Rejected |
| BE-TC-22 | SetActive(false) hides Subject from ForGrade; List still shows it; SetActive(true) restores | P0 | as titled | **Covered** — AC5_SubjectDeactivate_HidesFromStudentForGrade / AC5_SubjectDeactivate_AdminListStillReturnsIt / AC6_SubjectReactivate_RestoresStudentVisibility |
| BE-TC-23 | SetActive(false) hides Unit from student Lessons; List still shows it | P0 | as titled | **Covered** — AC5_UnitDeactivate_HidesFromStudentLessons / AC5_UnitDeactivate_AdminListStillReturnsIt |
| BE-TC-24 | SetActive validator: SubjectId=0 in route → 422 | P1 | 422 | **Covered** — AC12_SubjectSetActive_ZeroId_Returns422 |
| BE-TC-25 | Unit SetActive validator: UnitId=0 in route → 422 | P2 | 422 | **GAP** — AC12 covers Subjects only; the equivalent Unit `{id}/Active` 0-id 422 is not asserted. |
| BE-TC-26 | Delete unit with lessons → `Successed=false` "unit not empty"; delete empty unit succeeds | P0 | as titled | **Covered** — AC7_DeleteUnit_WithLessons_IsRejected / AC7c_DeleteEmptyUnit_SoftDeletes_DisappearsFromList |
| BE-TC-27 | Delete subject with units → `Successed=false`; delete empty subject succeeds | P0 | as titled | **Covered** — AC7b_DeleteSubject_WithUnits_IsRejected / AC7d_DeleteEmptySubject_SoftDeletes_DisappearsFromList |
| BE-TC-28 | Create duplicate `(GradeId,SubjectCode,Language)` → `Successed=false` | P0 | rejected, no 500 | **Covered** — AC8_CreateSubject_DuplicateTree_Rejected |
| BE-TC-29 | Create against soft-deleted tree's key → `Successed=false` + restore message (no 500) | P1 | rejected, valid JSON, message non-empty | **Covered** — AC8b_CreateSubject_SoftDeletedKeyExists_ReturnsRestoreMessage |
| BE-TC-30 | Coverage: empty grade→6 gaps; one slot present→5 gaps; non-existent grade→`Successed=false`; admin→200 | P0 | as titled | **Covered** — AC2_Coverage_* (4 tests) + AC9_Coverage_Admin_Returns200 |
| BE-TC-31 | BaseResponse envelope shape on SetActive (statusCode/successed/message/data/errors) | P1 | all 5 keys present | **Covered** — AC10_SetActive_ResponseEnvelopeShape |
| BE-TC-32 | SubjectCode beyond the 4 (=4 / Social Studies) → 422 | validation/product-rule | P0 | POST Create `SubjectCode:4` | 422 | **Covered** — `P2_01_CurriculumHierarchy_Extended_Tests` BE-TC-37(b-ii) |

---

## Group E — Net-new edge cases worth adding

| ID | Title | Type | Pri | Steps | Expected result | Covered / GAP |
|----|-------|------|-----|-------|-----------------|---------------|
| BE-TC-33 | Coverage with `gradeId=0` → graceful `Successed=false` (not 500) | boundary/negative | P2 | GET `/Subjects/Coverage?gradeId=0` admin | NOT 500; `Successed=false`. | **GAP** (non-existent id is Covered; the `0` boundary is not) |
| BE-TC-34 | Subjects `List` pagination metadata present (pageNumber/pageSize/totalCount) | functional | P2 | GET `/Subjects/List?PageNumber=1&PageSize=2&GradeId=<g>` after creating 3 subjects | envelope `data` carries pagination metadata; page1 ≤ 2 items | **GAP** (P2-01 BE-TC-08 covers pagination on the older list shape but not the P7-01 admin `Subjects/List` DTO) |
| BE-TC-35 | Reorder with duplicate ids in the list → graceful reject (no 500/partial write) | boundary/negative | P2 | PUT `/Subjects/Reorder` body `{ subjectIds:[<s>,<s>] }` | NOT 500; either 422 or `Successed=false`; ordering not corrupted. | **GAP** |
