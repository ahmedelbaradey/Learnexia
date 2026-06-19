# P7-05 — Content lifecycle (draft → published → archived) — Backend API test cases

> Target agent: `api-tester`. The existing `P7_05_ContentLifecycle_Tests.cs` is **comprehensive** (~50 facts): all
> legal + illegal transitions, versioning + rollback, preview, per-language publication coverage, full auth matrix, and
> the CRITICAL student-leak guards (Draft not served, Published served, Archived hidden). This catalog is **gap analysis**.
>
> Surface under test (all `[Authorize(AdminOnly)]` — class-level on `ContentLifecycleController`):
> - `POST Transition` — Draft↔Published, Published↔Archived (state machine)
> - `POST Rollback` — restore a previous `ContentVersion`
> - `GET VersionHistory` — published versions, newest first
> - `GET Preview` — admin preview of current (possibly Draft) state
> - `GET PublicationCoverage` — per-`(SubjectCode,Language)` publish state for a grade
> - EntityType: Subject=1, Unit=2, Lesson=3, QuizQuestion (and others). TargetState: Draft=1, Published=2, Archived=3.
>
> State machine (from controller doc): **Draft→Published**, **Published→Archived**, **Published→Draft** (unpublish),
> **Archived→Draft** (restore). Illegal: Draft→Archived, Archived→Published (direct).

Legend: **Covered** (file + method) / **GAP** (implement).

---

## Group A — State-machine transitions (legal + illegal) — mostly covered

| ID | Title | Type | Pri | Expected result | Covered / GAP |
|----|-------|------|-----|-----------------|---------------|
| BE-TC-01 | Lesson Draft→Published | state | P0 | `Successed=true` | **Covered** — AC-TC-1 |
| BE-TC-02 | Lesson Published→Archived | state | P0 | `Successed=true` | **Covered** — AC-TC-2 |
| BE-TC-03 | Lesson Published→Draft (unpublish) | state | P0 | `Successed=true` | **Covered** — AC-TC-3 |
| BE-TC-04 | Lesson Archived→Draft (restore) | state | P0 | `Successed=true` | **Covered** — AC-TC-4 |
| BE-TC-05 | Subject / Unit / QuizQuestion Draft→Published | state | P0 | `Successed=true` each | **Covered** — AC-TC-5/6/7 |
| BE-TC-06 | Illegal Draft→Archived → `Successed=false` | negative | P0 | rejected | **Covered** — AC-TC-ILL-1 |
| BE-TC-07 | Illegal Archived→Published (direct) → `Successed=false` | negative | P0 | rejected | **Covered** — AC-TC-ILL-2 |
| BE-TC-08 | Illegal Draft→Draft (no-op self-transition) → documented behavior | negative/boundary | P2 | NOT 500; deterministic (`Successed=false` or idempotent true) | **GAP** — self-transitions (Draft→Draft, Published→Published, Archived→Archived) are not tested; assert deterministic, no 500. |
| BE-TC-09 | Illegal Published→Published — re-publish creates a NEW version vs no-op | boundary | P1 | documented; if it creates a version, VersionNumber increments deterministically | **GAP** — re-publishing an already-Published entity: AC-VER-2 covers a 2nd publish but starts from Drans; the Published→Published edge is not isolated. |
| BE-TC-10 | Subject illegal transition (Draft→Archived) → `Successed=false` | negative | P1 | rejected | **GAP** — illegal transitions are tested only for Lesson (AC-TC-ILL-1/2); assert the state machine is enforced uniformly for Subject/Unit/QuizQuestion, not just Lesson. |

---

## Group B — Transition validators + error paths (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-11 | Transition EntityId=0→422; invalid EntityType=99→422; invalid TargetState=99→422 | P1 | **Covered** — AC-TC-VAL-1/2/3 |
| BE-TC-12 | Transition non-existent EntityId → `Successed=false` (not 500) | P1 | **Covered** — AC-TC-VAL-4 |
| BE-TC-13 | Transition response has full BaseResponse envelope keys | P1 | **Covered** — AC-ENV-1 |
| BE-TC-14 | Transition with EntityType/TargetState mismatch for entity (e.g. EntityType=Subject but EntityId is a Lesson id) → graceful, not 500 | negative/boundary | P2 | **GAP** — type/id confusion is not tested; assert it does not flip the wrong entity and does not 500. |

---

## Group C — Versioning + rollback (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-15 | First publish creates ContentVersion; VersionHistory returns it (timestamp/author) | P0 | **Covered** — AC-VER-1 |
| BE-TC-16 | Second publish increments VersionNumber to 2; history newest-first | P0 | **Covered** — AC-VER-2 |
| BE-TC-17 | Rollback to version 1 → `Successed=true`; new ContentVersion row appended | P0 | **Covered** — AC-VER-3 |
| BE-TC-18 | Rollback validators: VersionNumber=0→422; EntityId=0→422 | P1 | **Covered** — AC-VER-4/5 |
| BE-TC-19 | Rollback to non-existent VersionNumber → `Successed=false` | P1 | **Covered** — AC-VER-6 |
| BE-TC-20 | VersionHistory EntityId=0 → `Successed=false`; no-versions entity → `Successed=true` empty list | P1 | **Covered** — AC-VER-7/8 |
| BE-TC-21 | ContentVersion records publishedBy (author) — author is the acting admin, not null | persistence/audit | P1 | **GAP** — AC-VER-1 asserts a version row exists; it does not assert `publishedBy` is the acting admin's id (the story requires "author" recorded). Assert author = admin. |
| BE-TC-22 | Rollback restores the snapshot's editorial fields (e.g. title) to the entity | persistence | P1 | **GAP** — AC-VER-3 asserts a new row is appended; it does not assert the rolled-back **content** actually reverted. Edit→publish v1, edit→publish v2, rollback→v1, assert entity fields == v1. |
| BE-TC-23 | Rollback is per-`(SubjectCode,Language)` tree — rolling back ar Math does not touch en Math | state/i18n | P1 | **GAP** — P7-05-BE-7 per-language scoping for rollback is not directly asserted (publish-scoping is covered indirectly via coverage). |

---

## Group D — Preview (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-24 | Admin Preview of Draft → CurrentState=Draft; of Published → CurrentState=Published | functional | P1 | **Covered** — AC-PRV-1/2 |
| BE-TC-25 | ★Student/parent token GET Preview → 403 (Draft must not leak) | auth/security | P0 | **Covered** — AC-PRV-3 + AC-AUTH-2 "Preview parent → 403 CRITICAL" |
| BE-TC-26 | Preview EntityId=0 → `Successed=false`; non-existent entity → `Successed=false` (not 500) | negative | P1 | **Covered** — AC-PRV-4/5 |
| BE-TC-27 | Preview renders the DRAFT content (pending edits), distinguishable from the live published version | functional | P1 | **GAP** — story AC: "both the live version and the pending draft are distinguishable". AC-PRV-1 asserts CurrentState only; it does not assert preview shows the *unpublished edit* while student reads still show the old published content. Edit a published entity (now has pending draft), assert Preview shows the new value AND student read shows the old value. |

---

## Group E — Publication coverage + student-leak guards (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-28 | Coverage: Draft slot Exists=true/IsPublished=false; after publish IsPublished=true; one-sided ar-published/en-absent flagged | functional/i18n | P0 | **Covered** — AC-COV-1/2/3 |
| BE-TC-29 | Coverage non-existent gradeId → `Successed=false`; gradeId=0 → `Successed=false` | negative | P1 | **Covered** — AC-COV-4/5 |
| BE-TC-30 | ★Draft Subject NOT on student ForGrade; after Publish IS visible; after Archive hidden again | auth/security | P0 | **Covered** — AC-LEAK-1/2/3 |
| BE-TC-31 | ★Draft Lesson NOT in student Subjects/{id}/Lessons; after Publish IS; ditto GET /Lessons/{id} | auth/security | P0 | **Covered** — AC-LEAK-4/5/6/7 |
| BE-TC-32 | Seeded/backfilled Published content still visible (no regression) | regression | P1 | **Covered** — AC-LEAK-8 |
| BE-TC-33 | ★Draft QuizQuestion NOT in student StartAttempt; after Publish IS | auth/security | P0 | **GAP** — leak guards cover Subject + Lesson but NOT QuizQuestion. A Draft question leaking into a student attempt is a real risk (questions are publishable per AC-TC-7). Add the question-level leak guard. |
| BE-TC-34 | Publishing a Lesson while its parent Subject is still Draft → student still cannot see the lesson | boundary/security | P1 | **GAP** — the hierarchy interaction (child Published but ancestor Draft) is not tested; student reads filter ancestors too. Assert no partial leak. |
| BE-TC-35 | Auth: Transition/Rollback/VersionHistory/Preview/Coverage anonymous→401, basic/parent→403 | auth | P0 | **Covered** — AC-AUTH-1/2 |
