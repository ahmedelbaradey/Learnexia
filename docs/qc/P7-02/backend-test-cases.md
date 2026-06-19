# P7-02 — Lessons & Content Blocks admin — Backend API test cases

> Target agent: `api-tester`. The existing `P7_02_LessonsContentBlocks_Tests.cs` is **broad and high quality** (52
> facts: full auth matrix, per-type ContentBlock payload validation, lesson + block reorder, soft-delete cascade,
> SetActive student/admin split). This catalog is mostly **gap analysis** — implement only the GAP rows.
>
> Surface under test (all `[Authorize(AdminOnly)]` unless noted):
> - `LessonsController` — `List`, `{id}` (student), `Create`, `Update`, `Delete`, `{id}/Admin`, `{id}/Active`, `Reorder`
> - `ContentBlocksController` — `ByLesson/{id}`, `POST`, `PUT`, `DELETE/{id}`, `Reorder`
> - Content-block types: Text=1, Image=2, Video=3, Callout=4 (payload validated per type)

Legend: **Covered** (existing file + method) / **GAP** (implement).

---

## Group A — ContentBlock CRUD + per-type payload validation (mostly covered)

| ID | Title | Type | Pri | Expected result | Covered / GAP |
|----|-------|------|-----|-----------------|---------------|
| BE-TC-01 | Add Text/Image/Video/Callout block → ByLesson returns it with type+payload | functional | P0 | 200; round-trips | **Covered** — AddTextBlock_RoundTrip / AddImageBlock_RoundTrip / AddVideoBlock_RoundTrip / AddCalloutBlock_RoundTrip |
| BE-TC-02 | Edit block changes type+payload → ByLesson reflects new values | functional | P1 | 200; reflected | **Covered** — EditContentBlock_UpdatesTypeAndPayload |
| BE-TC-03 | Soft-delete block hides it from ByLesson | persistence | P1 | gone from list | **Covered** — DeleteContentBlock_HiddenFromByLesson |
| BE-TC-04 | Reorder blocks within a lesson persists order | functional | P1 | order persisted | **Covered** — ReorderContentBlocks_PersistsSequenceOrder |
| BE-TC-05 | Reorder blocks across two lessons → `Successed=false` | negative | P0 | rejected | **Covered** — ReorderContentBlocks_CrossLesson_Rejected |
| BE-TC-06 | Reorder validators: empty list→422; id=0→422 | validation | P1 | 422 | **Covered** — ReorderContentBlocks_EmptyList_Returns422 / _ZeroId_Returns422 |
| BE-TC-07 | Text missing markdown→422; Image/Video missing url→422; Callout missing variant/invalid variant/missing markdown→422 | validation | P0 | 422 each | **Covered** — AC-CB-10..13 (6 tests) |
| BE-TC-08 | Empty payload→422; invalid BlockType enum (99)→422 | validation | P0 | 422 | **Covered** — AddContentBlock_EmptyPayload / _InvalidBlockType |
| BE-TC-09 | Callout valid variants (info/warning/tip) accepted | functional | P2 | 200 | **Covered** — AddCalloutBlock_ValidVariants_Succeed (Theory) |
| BE-TC-10 | Add block to non-existent lessonId → `Successed=false` (not 500) | negative | P1 | NOT 500; `Successed=false` | **GAP** — no test adds a block to a non-existent lesson; mirrors the P7-04 lesson-404 guard. |
| BE-TC-11 | Edit non-existent blockId → `Successed=false` (not 500), no leak | negative | P1 | NOT 500; `Successed=false`; no `ex.Message` | **GAP** |
| BE-TC-12 | Delete non-existent blockId → `Successed=false` (not 500) | negative | P2 | NOT 500; `Successed=false` | **GAP** |
| BE-TC-13 | Image payload with malformed JSON string → 422 (not 500) | validation/boundary | P2 | 422 | **GAP** — current tests pass well-formed JSON missing a key; a non-JSON `payload` (e.g. `"not json"`) is not tested. |
| BE-TC-14 | Oversized payload (e.g. >64KB markdown) handled gracefully (422 or 200, never 500) | boundary | P2 | NOT 500 | **GAP** |

---

## Group B — Lesson admin lifecycle (mostly covered)

| ID | Title | Type | Pri | Expected result | Covered / GAP |
|----|-------|------|-----|-----------------|---------------|
| BE-TC-15 | Create lesson persists EstimatedMinutes; admin detail reflects it | persistence | P1 | 200; detail=30 | **Covered** — CreateLesson_EstimatedMinutes_PersistedInAdminDetail |
| BE-TC-16 | Edit lesson updates EstimatedMinutes | persistence | P1 | detail=45 | **Covered** — EditLesson_EstimatedMinutes_Updated |
| BE-TC-17 | SetActive(false) hides lesson from student GET `/Lessons/{id}` and Subjects/{id}/Lessons; admin `/Admin` still shows it (IsActive=false) | state | P0 | as titled | **Covered** — DeactivateLesson_HiddenFromStudentGet / _HiddenFromSubjectLessons / _StillVisibleViaAdminRoute |
| BE-TC-18 | SetActive(true) restores student visibility | state | P1 | restored | **Covered** — ReactivateLesson_RestoresStudentVisibility |
| BE-TC-19 | Soft-delete lesson → hidden from student GET + admin `/Admin` (global IsDeleted filter) | persistence | P0 | NotFound both | **Covered** — DeleteLesson_HiddenFromStudentGet / _NotFoundViaAdminRoute |
| BE-TC-20 | Soft-delete lesson cascade-soft-deletes its content blocks atomically | persistence | P0 | ByLesson empty/NotFound | **Covered** — DeleteLesson_CascadeSoftDeletesContentBlocks |
| BE-TC-21 | Lesson Reorder within unit persists order; cross-unit reorder → `Successed=false` | functional/negative | P1 | as titled | **Covered** — ReorderLessons_* (see AC-LS-8) |
| BE-TC-22 | Lesson Reorder validators: empty→422; id=0→422 | validation | P1 | 422 | **Covered** — AC-LS-9 |
| BE-TC-23 | SetActive validator: LessonId=0 in route → 422 | validation | P1 | 422 | **Covered** — AC-LS-12 |
| BE-TC-24 | BaseResponse envelope shape on add/edit/delete | functional | P1 | 5 keys | **Covered** — AC-LS-11 |
| BE-TC-25 | Lesson Update non-existent Id → 404 not 500, no `ex.Message` leak | regression/negative | P0 | NOT 500; `Successed=false`; no leak | **GAP** — PR #183-style; Lesson Update non-existent path untested (parallels P7-01 BE-TC-01). |
| BE-TC-26 | Lesson Create under non-existent UnitId → 404 (pre-existence check) | negative | P1 | 404 | **Covered** — `P2_01_CurriculumHierarchy_Extended_Tests` BE-TC-36a |
| BE-TC-27 | Lesson Create with Difficulty out of enum (99) → 422 | validation | P1 | 422 | **Covered** — P2-01 Extended BE-TC-21 |

---

## Group C — Auth matrix (covered — traceability)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-28 | All ContentBlocks endpoints anonymous→401, non-admin→403 | P0 | **Covered** — AC-CB-16 (10 tests across POST/PUT/DELETE/Reorder/ByLesson) |
| BE-TC-29 | Lessons Admin/SetActive/Reorder anonymous→401, non-admin→403 | P0 | **Covered** — AC-LS-10 |
| BE-TC-30 | Lessons `List` anonymous→401, non-admin→403 (admin DTO lockdown) | P1 | **GAP** — AC-LS-10 covers Admin/SetActive/Reorder but not the `Lessons/List` 401/403 lockdown specifically. |

---

## Group D — Language inheritance (P7-02-BE-6) — under-tested

The story says lesson/content has **no language column** and inherits from the owning Subject tree; placement
validators must reject orphan/cross-tree lessons. The existing suite does not directly exercise the placement guard.

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|----|-------|------|-----|-------|----------|---------------|
| BE-TC-31 | Lesson admin DTO surfaces resolved (read-only) language | functional | P2 | Create lesson under MATH/Ar; GET `/Lessons/{id}/Admin` | DTO exposes resolved language = Ar; no editable language field | **GAP** |
| BE-TC-32 | Lesson Create under a unit whose subject tree is valid resolves to one language | functional | P2 | Create lesson under unit in MATH/En | succeeds; resolved language = En | **GAP** |
