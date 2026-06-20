# P7 Curriculum (Admin Dashboard) — Frontend E2E Test Cases

**Surface:** `apps/admin-dashboard` Curriculum (Wave 2: P7-01 subjects/units, P7-02 lessons/content-blocks, P7-03 skills/graph, P7-04 questions, P7-05 lifecycle/publish)
**Target agent:** `frontend-e2e-tester` (Playwright, existing `admin` project, port **3001**)
**Mode:** Implement each case 1:1 as a Playwright test. Design-only — do not edit feature code.
**Run date stamp:** design produced 2026-06-19.

---

## 0. Environment & global preconditions (read first)

| Fact | Value |
|------|-------|
| Admin app | `http://localhost:3001` (Next.js, `NEXT_PUBLIC_API_URL=http://localhost:5080`) |
| Backend | `http://localhost:5080` (Development; CORS allows `:3001`) |
| Admin login | `/login`, form fields `name="userName"` + `name="password"`; creds **`superadmin` / `123Pa$$word!`** (dev seed) |
| Locale | Admin is **build-time `en`** (`ADMIN_LOCALE = 'en'`). Runtime RTL is unreachable → RTL cases = static `dir`/`dir="auto"` attribute checks only; **mark runtime-RTL BLOCKED**. |
| Enums on wire | **INT** (Subject codes MATH=0 SCIENCE=1 ARABIC=2 ENGLISH=3; Language Ar=0 En=1; Lifecycle Draft=1 Published=2 Archived=3; BlockType Text=1 Image=2 Video=3 Callout=4; QuestionType MCQ=1 TrueFalse=2 Matching=3 FillInBlank=4) |
| Seed data | `LearningSeeder` seeds subjects/units/lessons/questions. Where a flow needs deterministic fresh data, create it via the UI under test or seed via API (see `coverage-report.md` → Test Data / Seeding). |
| `data-testid` | The app ships testids throughout — selectors below reference the **actual shipped** testids. Gaps are flagged `[MISSING-TESTID]` and listed in `coverage-report.md`. |

**Shared precondition `LOGIN` (used by every case unless noted):**
1. Go to `/login`. 2. Fill `[name="userName"]` = `superadmin`, `[name="password"]` = `123Pa$$word!`. 3. Submit (click the Sign In button). 4. Wait for redirect off `/login`.
*(Recommended: implement as a Playwright `storageState` fixture so each test starts authenticated. The login form uses Tamagui `TextField` — there is no `data-testid` on the inputs, only `name=`; see handoff note.)*

**No-optimistic-update rule (cross-cutting, applies to all mutations):** mutations are invalidate-and-refetch only (graph add/remove edges, reorder local-state are the only client-side anticipations). After any create/edit/delete/toggle, the row/state must reflect the change **after** the network round-trip, not instantly. Where practical, assert the in-flight `aria-busy`/`…` state, then the settled state.

---

## 1. Subjects list — `/curriculum/subjects` (P7-01-FE-1/2/5)

| Field | Value |
|-------|-------|
| Route | `/curriculum/subjects` |

### CUR-TC-01 — Subjects list renders results table
- **Type:** functional / state · **Priority:** P0
- **Traces:** P7-01-FE-1 (grade-scoped table), AC "four states: loading/empty/error/results"
- **Pre:** LOGIN; seed has ≥1 subject.
- **Steps:** 1. Navigate `/curriculum/subjects`. 2. Wait for `[data-testid="subjects-table"]`.
- **Expected:** Table visible; ≥1 `[data-testid^="subjects-row-"]`; header count chip shows `{totalCount} subjects`; each row shows code badge, name, `LanguageBadge`, grade (`dir="ltr"`), order, `ActiveBadge`, lifecycle slot `[data-testid$="-lifecycle-slot"]`.

### CUR-TC-02 — Loading skeleton shows before data
- **Type:** state · **Priority:** P1 · **Traces:** P7-01-FE-1 loading state
- **Steps:** Throttle/route-intercept the `Subjects/List` call to delay; navigate. **Expected:** `[data-testid="subjects-loading"]` (role=status) appears, then is replaced by `subjects-table`.

### CUR-TC-03 — Empty state when no subjects match
- **Type:** state · **Priority:** P1 · **Traces:** P7-01-FE-1 empty state
- **Steps:** Type a search guaranteed to match nothing (e.g. `zzzqqq___none`) into `[data-testid="subjects-search-input"]`; wait debounce (350ms). **Expected:** `[data-testid="subjects-empty-state"]` visible with "no results" heading + a create-subject CTA; no table.

### CUR-TC-04 — Error state + retry
- **Type:** state / negative · **Priority:** P1 · **Traces:** P7-01-FE-1 error state
- **Steps:** Route-intercept `**/Subjects/List**` → 500; navigate. **Expected:** `[data-testid="subjects-error-banner"]` visible + a Retry button. Remove the intercept, click Retry → table renders.

### CUR-TC-05 — Filter by grade
- **Type:** functional · **Priority:** P0 · **Traces:** P7-01-FE-2 (grade filter)
- **Pre:** LOGIN; seed has subjects across ≥2 grades.
- **Steps:** Select a grade in `[data-testid="subjects-grade-filter"]`. **Expected:** Request fires with `gradeId`; visible rows all belong to that grade (grade column matches); the `LanguageCoveragePanel` appears (only when a grade is selected); page resets to 1.

### CUR-TC-06 — Filter by language (client-side)
- **Type:** functional · **Priority:** P0 · **Traces:** P7-01-FE-2 (language filter is client-side; backend has no Language param)
- **Steps:** With a grade selected, pick AR then EN in the `SubjectLanguageFilter`. **Expected:** Rows filter client-side to matching `LanguageBadge`; switching to EN shows only EN; clearing (null) shows both.

### CUR-TC-07 — Search debounce + reset to page 1
- **Type:** functional · **Priority:** P1 · **Traces:** P7-01-FE-2 (search)
- **Steps:** On page 2 (if pagination available), type into search. **Expected:** A single list request fires ~350ms after last keystroke (debounced); `page` resets to 1.

### CUR-TC-08 — Clear filters
- **Type:** functional · **Priority:** P2 · **Traces:** P7-01-FE-2
- **Steps:** Apply grade + language + search; click the Clear filters button. **Expected:** grade reset to All, language null, search empty, coverage panel hidden.

### CUR-TC-09 — Pagination next/prev
- **Type:** functional / boundary · **Priority:** P1 · **Traces:** P7-01-FE-1 (pagination metadata)
- **Pre:** Seed enough subjects that `totalPages > 1` (create via API if needed).
- **Steps:** Click Next (`subjectsNextPage`), then Prev. **Expected:** "`{page} / {totalPages}`" updates; Prev disabled on page 1; Next disabled on last page. Pagination control hidden when `totalPages <= 1`.

### CUR-TC-10 — Row click navigates to detail
- **Type:** functional · **Priority:** P0 · **Traces:** P7-01-FE-3
- **Steps:** Click a `[data-testid="subjects-row-{id}"]` (not on an action button). **Expected:** Route → `/curriculum/subjects/{id}`. Repeat via keyboard: focus row, press Enter → same nav (row has `role=button`, `tabIndex=0`).

### CUR-TC-11 — Create subject (happy path)
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-01-FE (CRUD create)
- **Steps:** Click `[data-testid="new-subject-btn"]`; fill `subject-form-name`, select `subject-form-grade`, `subject-form-code` (=Math), `subject-form-language`, `subject-form-order`; click `[data-testid="subject-form-save"]`.
- **Expected:** Dialog closes; new subject appears in the table after refetch (use a unique name to locate it). Reload page → still present (persistence).

### CUR-TC-12 — Create subject validation (required fields)
- **Type:** validation / negative · **Priority:** P0 · **Traces:** P7-01-FE create validation
- **Steps:** Open create form; click Save with empty name/grade/code/language. **Expected:** Inline `role="alert"` errors on name, grade, code, language; no request fired; dialog stays open.

### CUR-TC-13 — Pinned-language rule (Arabic/English lock language)
- **Type:** functional / validation · **Priority:** P0 · **Traces:** P7-01 product rule (ARABIC→Ar, ENGLISH→En pinned)
- **Steps:** Open create; select code = Arabic. **Expected:** `[data-testid="subject-form-language"]` becomes disabled and value pinned to Arabic (Ar=0); pinned-language hint visible. Select code = English → pinned to En=1. Select Math/Science → language re-enabled (free choice).

### CUR-TC-14 — Edit subject
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-01-FE (CRUD edit)
- **Steps:** Click `[data-testid="subject-{id}-edit"]`; change name; save. **Expected:** Form pre-filled with current values; after save the row name updates post-refetch; reload confirms persistence.

### CUR-TC-15 — Toggle subject IsActive
- **Type:** functional / state · **Priority:** P0 · **Traces:** P7-01-FE-5 (IsActive)
- **Steps:** Click `[data-testid="subject-{id}-toggle-active"]` on an active subject. **Expected:** `aria-busy` true in-flight; after refetch `ActiveBadge` flips to inactive and row opacity dims (0.55). Toggle back → active. On forced 500 → `toggleError` banner shown.

### CUR-TC-16 — Delete subject (confirm dialog)
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-01-FE (CRUD delete)
- **Pre:** Create a throwaway subject via UI first.
- **Steps:** Click `[data-testid="subject-{id}-delete"]`; in `CurriculumDeleteDialog` click `[data-testid="curriculum-delete-confirm"]`. **Expected:** Dialog closes; success notice (`curriculumDeleteSuccessSubject`) appears; row gone after refetch; reload confirms removal.

### CUR-TC-17 — Single-tree keyboard reorder (enabled only when grade + language chosen)
- **Type:** functional / a11y · **Priority:** P0 · **Traces:** P7-01-FE-5 (reorder), AC "reorder within a (grade, language) tree only"
- **Pre:** Select a grade AND a language with ≥2 subjects in that tree.
- **Steps:** Click `[data-testid="subject-{id}-move-down"]` on the first row. **Expected:** Row order swaps locally; aria-live announcement fires (sr-only "polite"); `[data-testid="subjects-save-order"]` appears. Click Save → reorder request posts ONLY the filtered subset ids; on success the save button disappears and order persists after reload.

### CUR-TC-18 — Reorder controls disabled without grade+language scope
- **Type:** negative / boundary · **Priority:** P1 · **Traces:** P7-01-FE-5 (reorder guard)
- **Steps:** With no grade or no language selected, inspect a row's move buttons. **Expected:** move-up/move-down have `aria-disabled=true` and are not actionable; the `reorderDisabledHint` is shown when language is null.

### CUR-TC-19 — Subject lifecycle badge per row (DG-2 fetch via Preview)
- **Type:** functional / integration · **Priority:** P1 · **Traces:** P7-05 (lifecycle slot in list)
- **Steps:** Load list. **Expected:** Each row's `[data-testid="subject-{id}-lifecycle-slot"]` resolves to `[data-testid="subject-{id}-lifecycle-badge"]` showing a Draft/Published/Archived state (shimmer placeholder first, then badge). (Badge may be null/empty if entity never versioned — acceptable.)

### CUR-TC-20 — No Social Studies / only 4 subject codes
- **Type:** negative / product-rule · **Priority:** P0 · **Traces:** Product decision "4 subjects, no Social Studies"
- **Steps:** Open create form; open `[data-testid="subject-form-code"]`. **Expected:** Exactly four options — Math, Science, Arabic, English. No "Social Studies" / 5th option.

---

## 2. Subject detail + Units — `/curriculum/subjects/[id]` (P7-01-FE-3/4/5)

### CUR-TC-21 — Subject header card + breadcrumb
- **Type:** functional · **Priority:** P0 · **Traces:** P7-01-FE-3
- **Steps:** Navigate to a valid subject detail. **Expected:** `[data-testid="subject-header-{id}"]` shows code/language/active/lifecycle badges, name, grade (`dir="ltr"`), order; breadcrumb back link to `/curriculum/subjects`.

### CUR-TC-22 — Not-found state for bad subject id
- **Type:** negative / state · **Priority:** P1 · **Traces:** P7-01-FE-3 not-found
- **Steps:** Navigate `/curriculum/subjects/99999999` (id not in list). **Expected:** `[data-testid="subject-not-found"]` with a "back to list" button. *(Note: detail resolves subject from page-1 of the list hook — a real subject on page ≥2 may falsely show not-found; record as a known limitation, not a defect, if observed.)*

### CUR-TC-23 — Subject lifecycle panel (control + version history) mounts
- **Type:** functional / integration · **Priority:** P0 · **Traces:** P7-05-FE (control + history)
- **Steps:** Open subject detail. **Expected:** `[data-testid="subject-{id}-lifecycle-panel"]` present; when `currentState` known, `[data-testid="subject-{id}-lifecycle-control"]` (`lifecycle-control`) renders state-appropriate buttons; `[data-testid="subject-{id}-version-history"]` (`version-history-panel`) renders (history rows or `version-history-empty`).

### CUR-TC-24 — Units table under subject
- **Type:** functional / state · **Priority:** P0 · **Traces:** P7-01-FE-4 (units list)
- **Steps:** Open a subject with units. **Expected:** `[data-testid="units-table"]` with `[data-testid^="units-row-"]`; order/name/active/lifecycle columns. Loading → `units-loading`; empty → `units-empty-state`; error (intercept 500) → `units-error-banner` + retry.

### CUR-TC-25 — Create unit
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-01-FE-4 (unit CRUD create)
- **Steps:** Click `[data-testid="new-unit-btn"]`; fill `unit-form-name`, `unit-form-order`; save `[data-testid="unit-form-save"]`. **Expected:** Dialog closes; unit appears after refetch; reload persists.

### CUR-TC-26 — Edit unit
- **Type:** functional · **Priority:** P1 · **Traces:** P7-01-FE-4
- **Steps:** `[data-testid="unit-{id}-edit"]` → change name → save. **Expected:** Form pre-filled; row name updates post-refetch.

### CUR-TC-27 — Toggle unit IsActive
- **Type:** functional / state · **Priority:** P1 · **Traces:** P7-01-FE-5
- **Steps:** `[data-testid="unit-{id}-toggle-active"]`. **Expected:** `aria-busy`; badge flips after refetch; opacity dims when inactive.

### CUR-TC-28 — Delete unit
- **Type:** functional / persistence · **Priority:** P1 · **Traces:** P7-01-FE-4 (delete)
- **Pre:** Create a throwaway unit.
- **Steps:** `[data-testid="unit-{id}-delete"]` → `curriculum-delete-confirm`. **Expected:** Success notice (`curriculumDeleteSuccessUnit`); row gone; reload persists.

### CUR-TC-29 — Units keyboard reorder + save
- **Type:** functional / a11y · **Priority:** P0 · **Traces:** P7-01-FE-4/5 (unit reorder)
- **Pre:** Subject with ≥2 units.
- **Steps:** `[data-testid="unit-{id}-move-down"]` on row 1 → `[data-testid="units-save-order"]`. **Expected:** Local swap + aria-live; Save posts `unitIds` (full local order) with `subjectId`; order persists after reload; move-up disabled on first row, move-down on last.

### CUR-TC-30 — Unit lifecycle badge per row
- **Type:** functional / integration · **Priority:** P2 · **Traces:** P7-05 (unit badge)
- **Steps:** Inspect `[data-testid="unit-{id}-lifecycle-slot"]`. **Expected:** Resolves to `[data-testid="unit-{id}-lifecycle-badge"]` (or empty if unversioned).

---

## 3. Lessons list + detail — `…/units/[unitId]/lessons[/lessonId]` (P7-02-FE)

### CUR-TC-31 — Lessons list four states
- **Type:** functional / state · **Priority:** P0 · **Traces:** P7-02-FE (lessons list)
- **Steps:** Navigate lessons list for a unit with lessons. **Expected:** `[data-testid="lessons-table"]`; rows `[data-testid^="lesson-row-"]` with order/title/difficulty/duration/lock/active. Loading → `lessons-loading`; empty → `lessons-empty`; error (intercept) → `lessons-error` + retry. Breadcrumb shows subject/unit/Lessons; `InheritedLanguageBadge` shown.

### CUR-TC-32 — Lesson detail link navigation
- **Type:** functional · **Priority:** P0 · **Traces:** P7-02-FE (detail)
- **Steps:** Click `[data-testid="lesson-{id}-detail-link"]`. **Expected:** Route → lesson detail; `[data-testid="lesson-header-{id}"]` shows badges + meta (Order, Blocks count); breadcrumb `breadcrumb-lesson` present.

### CUR-TC-33 — Lesson not-found
- **Type:** negative / state · **Priority:** P1 · **Traces:** P7-02-FE not-found
- **Steps:** Navigate to lessons/99999999 detail. **Expected:** `[data-testid="lesson-not-found"]` + back button.

### CUR-TC-34 — Create lesson
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-02-FE (lesson CRUD create)
- **Steps:** `[data-testid="new-lesson-btn"]`; fill `lesson-form-name`, `lesson-form-difficulty`, `lesson-form-minutes`, toggles `lesson-form-locked`/`lesson-form-active`; save `[data-testid="lesson-form-save"]`. **Expected:** Dialog closes; lesson appears after refetch; reload persists.

### CUR-TC-35 — Edit lesson
- **Type:** functional · **Priority:** P1 · **Traces:** P7-02-FE (edit)
- **Steps:** `[data-testid="lesson-{id}-edit"]` → change name → save. **Expected:** Pre-filled; name updates post-refetch.

### CUR-TC-36 — Toggle lesson active (list + detail)
- **Type:** functional / state · **Priority:** P1 · **Traces:** P7-02-FE (active)
- **Steps:** `[data-testid="lesson-{id}-toggle-active"]` on list, then on detail header. **Expected:** `aria-busy`; success banner (`lessonActivateSuccess`/`lessonDeactivateSuccess`); badge flips after refetch.

### CUR-TC-37 — Delete lesson (soft delete)
- **Type:** functional / persistence · **Priority:** P1 · **Traces:** P7-02-FE (delete)
- **Pre:** Throwaway lesson.
- **Steps:** `[data-testid="lesson-{id}-delete"]` → `delete-lesson-confirm-btn`. **Expected:** Row removed after refetch; from detail page, deletion navigates back to lessons list (`backHref`).

### CUR-TC-38 — Lessons keyboard reorder + save
- **Type:** functional / a11y · **Priority:** P0 · **Traces:** P7-02-FE (reorder)
- **Pre:** Unit with ≥2 lessons.
- **Steps:** `[data-testid="lesson-{id}-move-up/-down"]` → `[data-testid="lessons-save-order"]`. **Expected:** Local reorder + aria-live (`lessonReorderPosition`); Save posts `{unitId, lessonIds}`; persists after reload; endpoint move buttons `disabled` at ends.

---

## 4. Content-block editor — lesson detail (P7-02-FE §8)

> Editor root `[data-testid="content-block-editor"]`; list `[data-testid="block-list"]`; cards `[data-testid="block-card-{id}"]`.

### CUR-TC-39 — Block editor four states
- **Type:** state · **Priority:** P0 · **Traces:** P7-02-FE §8
- **Steps:** Open lesson detail. **Expected:** Loading → `block-editor-loading`; empty → `block-editor-empty` + hint; error (intercept lesson) → `block-editor-error`; with blocks → `block-list` of `block-card-{id}`. Count label "`N blocks`".

### CUR-TC-40 — Add Text block (markdown) + sanitized preview
- **Type:** functional / persistence / security · **Priority:** P0 · **Traces:** P7-02-FE §8 (Text block), §9 (sanitized preview)
- **Steps:** Click `[data-testid="block-editor-add-btn"]` → `[data-testid="pick-type-1"]`. In `BlockForm` fill `[data-testid="block-text-markdown"]` with markdown incl. a script payload, e.g. `# Hello\n\n<script>alert(1)</script>**bold**`. Save `[data-testid="block-form-save"]`.
- **Expected:** Block created after refetch; its `[data-testid="block-card-{id}-preview"]` renders sanitized HTML — `**bold**`→`<strong>`, heading rendered, **no executable `<script>`** present in DOM (DOMPurify). Reload persists.

### CUR-TC-41 — Add Image block + HTTPS URL guard + preview fallback
- **Type:** functional / validation / security · **Priority:** P0 · **Traces:** P7-02-FE §8 (Image), SEC-3 URL guard
- **Steps:** Add via `[data-testid="pick-type-2"]`. (a) Enter non-HTTPS `http://example.com/x.png` in `[data-testid="block-image-url"]` → Save. **Expected:** inline `role="alert"` URL error (`blockFormUrlHttpsRequired`); no create. (b) Enter a private host `https://192.168.0.1/a.png` → Save → `blockFormUrlPrivateNotAllowed`. (c) Enter valid `https://cdn.example.com/pic.png` + alt text → Save → block created. (d) For a created image whose URL is non-image (no allowed extension), preview shows `blockPreviewCannotPreview` fallback chip; for valid image-extension URL, preview renders `<img>`.

### CUR-TC-42 — Add Video block
- **Type:** functional / validation · **Priority:** P1 · **Traces:** P7-02-FE §8 (Video)
- **Steps:** `[data-testid="pick-type-3"]`; URL `[data-testid="block-video-url"]` https + optional caption; Save. **Expected:** Block created; preview shows play icon + url; HTTPS guard same as image (no extension requirement); "Open link" present only for secure URL.

### CUR-TC-43 — Add Callout block (variant required)
- **Type:** functional / validation · **Priority:** P1 · **Traces:** P7-02-FE §8 (Callout)
- **Steps:** `[data-testid="pick-type-4"]`; Save with empty variant → `blockFormVariantRequired` + `blockFormMarkdownRequired` alerts. Then pick `[data-testid="block-callout-variant"]`=warning, fill `[data-testid="block-callout-markdown"]`; Save. **Expected:** Block created; preview renders warning-variant callout (border/label) with sanitized markdown.

### CUR-TC-44 — Edit block + type-change warning
- **Type:** functional · **Priority:** P1 · **Traces:** P7-02-FE §8.5 (edit)
- **Steps:** `[data-testid="block-card-{id}-edit"]`; change block type via the edit-mode type `<select>`. **Expected:** `blockFormTypeChangeWarning` shown; fields reset to new type's shape; save persists new type/payload after refetch.

### CUR-TC-45 — Reorder blocks + save order
- **Type:** functional / a11y · **Priority:** P0 · **Traces:** P7-02-FE §8 (reorder)
- **Pre:** Lesson with ≥2 blocks.
- **Steps:** `[data-testid="block-card-{id}-move-up/-down"]` → `[data-testid="block-editor-save-order"]`. **Expected:** Local reorder; save button appears; aria-live region announces reorder; Save posts `{lessonId, contentBlockIds}`; order persists after reload.

### CUR-TC-46 — Delete block
- **Type:** functional / persistence · **Priority:** P1 · **Traces:** P7-02-FE §8 (delete)
- **Pre:** Throwaway block.
- **Steps:** `[data-testid="block-card-{id}-delete"]` → `delete-block-confirm-btn`. **Expected:** Block removed after refetch; reload persists.

### CUR-TC-47 — Payload cap validation (≤ 65536)
- **Type:** boundary / validation · **Priority:** P2 · **Traces:** P7-SEC-2 (payload cap)
- **Steps:** In a Text block paste markdown long enough that the serialized payload > 65536 chars; Save. **Expected:** `blockFormPayloadTooLarge` alert; no create. (Markdown counter warns past 32768.)

### CUR-TC-48 — Block payload parse-error fallback (preview)
- **Type:** negative / state · **Priority:** P2 · **Traces:** P7-02-FE §9 (parse guard)
- **Steps:** Seed (via API) a content block with malformed `payload` JSON; open the lesson. **Expected:** `BlockPreview` shows `blockPreviewParseError` with a truncated raw dump (no crash). *(If API seeding of malformed payload is not feasible, mark BLOCKED.)*

---

## 5. Questions per lesson — `…/lessons/[lessonId]/questions` (P7-04-FE)

> Editor dialog `[data-testid="question-editor-dialog"]`; table `[data-testid="questions-table"]`; rows `[data-testid="question-row-{id}"]`.

### CUR-TC-49 — Questions list four states
- **Type:** state · **Priority:** P0 · **Traces:** P7-04-FE (list)
- **Steps:** Open questions page. **Expected:** loading `questions-loading`; empty `questions-empty` + create CTA; error (intercept) `questions-error` + `questions-retry`; results `questions-table` with order/question/type/difficulty/active columns; count chip.

### CUR-TC-50 — Create MCQ + CorrectAnswer/Options round-trip
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-04-FE (MCQ contract)
- **Steps:** `[data-testid="new-question-btn"]`; `[data-testid="question-type-select"]`=MCQ; fill `[data-testid="question-text-input"]`, `[data-testid="question-difficulty-select"]`; fill `[data-testid="mcq-option-input-0/1]`, add a 3rd via `[data-testid="mcq-add-option"]`, select correct via `[data-testid="mcq-correct-radio-1"]`; Save `[data-testid="question-editor-save"]`.
- **Expected:** Created after refetch; row shows MCQ badge + question text. **Round-trip:** re-open via edit → options decoded back into inputs, correct radio reflects the saved literal (options=`JSON.stringify(string[])`, correctAnswer=raw literal). Reload persists.

### CUR-TC-51 — Create TrueFalse + round-trip (lowercase contract)
- **Type:** functional · **Priority:** P0 · **Traces:** P7-04-FE (TrueFalse contract)
- **Steps:** Type=TrueFalse; pick `[data-testid="tf-option-true"]`; Save. **Expected:** Created; edit re-opens with True selected. Wire contract: `correctAnswer="true"` lowercase, `options=["true","false"]` — assert via the round-trip (decode normalises both legacy `True` and `true`).

### CUR-TC-52 — Create FillInBlank + round-trip
- **Type:** functional · **Priority:** P0 · **Traces:** P7-04-FE (FillInBlank contract)
- **Steps:** Type=FillInBlank; `[data-testid="fib-answer-input"]`="42"; Save. **Expected:** Created; edit re-opens with answer "42" (correctAnswer=raw scalar, options=`[]`).

### CUR-TC-53 — Create Matching + grades-correctly end-to-end (MUST-HAVE)
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-04-FE (Matching contract + correct grading)
- **Steps:** Type=Matching; add 2 left via `[data-testid="match-add-left"]` (`match-left-input-0/1`), 2 right via `match-add-right` (`match-right-input-0/1`); set pairs via `[data-testid="match-pair-select-0/1"]`; Save.
- **Expected:** Created. **Round-trip:** edit re-opens with left/right items + pair selects exactly as entered (options=`{left,right}`, correctAnswer=`{pairs:[{leftId,rightId}]}`). **Grades-correctly assertion:** the saved `correctAnswer.pairs` map each `leftId` to the chosen `rightId` such that re-decoding reproduces the intended mapping — verify by reading the persisted question (API GET or edit-form state) and confirming each leftId→rightId is the one selected (so the student grader would mark a matching submission correct).

### CUR-TC-54 — Matching validation rules
- **Type:** validation / negative · **Priority:** P1 · **Traces:** P7-04-FE (matching validation)
- **Steps:** Type=Matching; trigger each: (a) unequal left/right counts → `questionErrMatchEqualCount`; (b) empty item text → `questionErrMatchEmptyLeft/Right`; (c) a left without a paired right → `questionErrMatchAllPaired`; (d) two lefts mapped to same right → `questionErrMatchDuplicatePair`. **Expected:** Corresponding `AdminErrorBanner` error(s); Save blocked.

### CUR-TC-55 — MCQ validation rules
- **Type:** validation / negative · **Priority:** P1 · **Traces:** P7-04-FE (MCQ validation)
- **Steps:** MCQ with: empty question text → `questionErrTextRequired`; fewer than 2 options (cannot remove below 2 — verify remove button hidden at 2); an empty option string → `questionErrMcqEmptyOptions`. **Expected:** Errors shown; Save blocked.

### CUR-TC-56 — Required type + difficulty
- **Type:** validation · **Priority:** P1 · **Traces:** P7-04-FE
- **Steps:** Open create; Save with no type → `questionErrTypeRequired`. Select type, no difficulty → `questionErrDiffRequired`.

### CUR-TC-57 — Question type locked on edit
- **Type:** functional · **Priority:** P1 · **Traces:** P7-04-FE (type immutable on edit)
- **Steps:** Edit an existing question. **Expected:** Type shown as read-only field with `questionFieldTypeLockedHint`; no `question-type-select` present.

### CUR-TC-58 — Edit question persists
- **Type:** functional / persistence · **Priority:** P1 · **Traces:** P7-04-FE (edit)
- **Steps:** Edit MCQ → change a correct option / question text → Save. **Expected:** Updated after refetch; reload persists.

### CUR-TC-59 — Delete question
- **Type:** functional · **Priority:** P1 · **Traces:** P7-04-FE (delete)
- **Pre:** Throwaway question.
- **Steps:** `[data-testid="question-{id}-delete"]` → `delete-question-confirm-btn`. **Expected:** Row removed after refetch.

### CUR-TC-60 — Activate / Deactivate question
- **Type:** functional / state · **Priority:** P1 · **Traces:** P7-04-FE (active toggle)
- **Steps:** On active question click `[data-testid="question-{id}-toggle-active"]` → `deactivate-question-confirm-btn`; then re-toggle → `activate-question-confirm-btn`. **Expected:** `ActiveBadge` flips after refetch; inactive rows dim (opacity 0.6).

### CUR-TC-61 — Questions reorder + save
- **Type:** functional / a11y · **Priority:** P1 · **Traces:** P7-04-FE (reorder)
- **Pre:** ≥2 questions.
- **Steps:** `[data-testid="question-{id}-move-up/-down"]` → `[data-testid="questions-save-order"]`. **Expected:** Local reorder + aria-live (`questionMovedAnnouncement`); posts `{lessonId, questionIds}`; `questionsOrderSaved` success banner; persists after reload.

### CUR-TC-62 — Open editor by clicking question text
- **Type:** functional · **Priority:** P2 · **Traces:** P7-04-FE
- **Steps:** Click `[data-testid="question-{id}-text-btn"]`. **Expected:** Opens editor in edit mode for that question.

### CUR-TC-63 — Matching parse-error guard
- **Type:** negative / state · **Priority:** P2 · **Traces:** P7-04-FE (matching decode guard)
- **Steps:** Seed (API) a Matching question with malformed `options`/`correctAnswer`; open its editor. **Expected:** `questionMatchParseError` banner; Save disabled; sub-form hidden. *(If API seeding infeasible, mark BLOCKED.)*

---

## 6. Skills + prerequisite graph — `/curriculum/skills` (P7-03-FE)

### CUR-TC-64 — No-subject-selected default state
- **Type:** state · **Priority:** P1 · **Traces:** P7-03-FE-1
- **Steps:** Navigate `/curriculum/skills` without selecting a subject. **Expected:** Left = `[data-testid="skills-empty-state"]` (select-a-subject); right = `[data-testid="graph-no-subject"]`.

### CUR-TC-65 — Select subject → skills table + graph load
- **Type:** functional / state · **Priority:** P0 · **Traces:** P7-03-FE-1/4
- **Steps:** Pick a subject in `[data-testid="skill-subject-picker"]`. **Expected:** `[data-testid="skills-table"]` rows `skills-row-{id}`; graph `[data-testid="graph-node-listbox"]` populates with `graph-node-{id}`; loading states `skills-loading`/`graph-loading` precede; error (intercept) → `graph-error-banner` + retry.

### CUR-TC-66 — Skills filters (concept + search) + pagination
- **Type:** functional · **Priority:** P1 · **Traces:** P7-03-FE-1
- **Steps:** With a subject selected, filter `[data-testid="skill-concept-filter"]` and type in `[data-testid="skills-search-input"]` (debounced). **Expected:** Table updates; page resets to 1; pagination prev/next when `totalPages>1`; empty → `skills-empty-state`.

### CUR-TC-67 — Create skill
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-03-FE (CRUD create)
- **Steps:** `[data-testid="new-skill-btn"]`; fill `skill-form-name`, `skill-form-threshold`, `skill-form-time`, `skill-form-concept`; Save `[data-testid="skill-form-save"]`. **Expected:** Created after refetch; appears in table; reload persists.

### CUR-TC-68 — Edit skill (table + graph detail link)
- **Type:** functional · **Priority:** P1 · **Traces:** P7-03-FE (edit)
- **Steps:** `[data-testid="skill-{id}-edit"]` (and `[data-testid="skill-detail-edit-{id}"]` from graph panel) → change threshold → Save. **Expected:** Pre-filled; updates post-refetch.

### CUR-TC-69 — Delete skill
- **Type:** functional · **Priority:** P1 · **Traces:** P7-03-FE (delete)
- **Pre:** Throwaway skill.
- **Steps:** `[data-testid="skill-{id}-delete"]` → `skill-delete-confirm`. **Expected:** Removed after refetch.

### CUR-TC-70 — Bi-directional row↔graph selection
- **Type:** functional / a11y · **Priority:** P1 · **Traces:** P7-03-FE-4 (wiring)
- **Steps:** Click a `skills-row-{id}` → corresponding `graph-node-{nodeId}` becomes `aria-selected` + focused; prerequisites/unlocks panel reflects it. Click row again → deselects.

### CUR-TC-71 — Graph keyboard navigation (listbox)
- **Type:** a11y · **Priority:** P0 · **Traces:** P7-03-FE-4 (accessible graph; D1 list/adjacency editor)
- **Steps:** Focus `[data-testid="graph-node-listbox"]`; press ArrowDown/ArrowUp/Home/End/Enter. **Expected:** Roving selection moves accordingly (`aria-selected` follows, focus moves); `role=listbox` + `role=option` present; `aria-multiselectable=false`.

### CUR-TC-72 — Add prerequisite edge
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-03-FE-4 (add edge)
- **Steps:** Select a node; pick a source in `[data-testid="prerequisite-picker"]`; click `[data-testid="add-prerequisite-btn"]`. **Expected:** Edge added after refetch (`prerequisite-item-{edgeId}` appears); polite aria-live announcement; picker resets; mutation controls disabled while in-flight (no optimistic — list updates only after success).

### CUR-TC-73 — Remove prerequisite edge
- **Type:** functional · **Priority:** P1 · **Traces:** P7-03-FE-4 (remove edge)
- **Pre:** A node with ≥1 prerequisite edge.
- **Steps:** Click `[data-testid="remove-prerequisite-{edgeId}"]`. **Expected:** Edge removed after refetch; aria-live announcement; if none left → `prerequisites-empty`.

### CUR-TC-74 — Cycle rejection surfaced
- **Type:** negative · **Priority:** P0 · **Traces:** P7-03-FE-4 (cycle guard)
- **Steps:** Construct A→B then attempt B→A (or intercept add edge → 400 with `KnowledgeEdgeWouldCreateCycle`). **Expected:** `[data-testid="graph-edge-error"]` (role=alert, assertive) shows `skillGraphErrCycle`; no edge added.

### CUR-TC-75 — Duplicate edge rejection surfaced
- **Type:** negative · **Priority:** P1 · **Traces:** P7-03-FE-4 (duplicate guard)
- **Steps:** Add an edge that already exists (or intercept → 400 `KnowledgeEdgeDuplicate`). **Expected:** `skillGraphErrDuplicate` error. *(Note: already-prerequisite nodes are excluded from the picker, so reproduce via intercept.)*

### CUR-TC-76 — Cross-language edge rejection surfaced
- **Type:** negative · **Priority:** P1 · **Traces:** P7-03-FE-4 (cross-language guard)
- **Steps:** Intercept add edge → 400 `KnowledgeEdgeCrossLanguageForbidden`. **Expected:** `skillGraphErrCrossLanguage` error; no edge.

### CUR-TC-77 — Picker excludes self + existing prerequisites; all-added message
- **Type:** functional / boundary · **Priority:** P2 · **Traces:** P7-03-FE-4
- **Steps:** Select a node; inspect picker options. **Expected:** Self not listed; already-prerequisite nodes not listed; when none remain, the empty option reads `skillGraphPickerAllAdded` and Add is disabled.

---

## 7. Lifecycle / publish / preview / coverage (P7-05-FE)

### CUR-TC-78 — Publish a Draft subject
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-05-FE (publish)
- **Pre:** A subject currently in Draft (create fresh → defaults Draft).
- **Steps:** On subject detail, `[data-testid="lifecycle-publish-btn"]` → `[data-testid="publish-confirm-btn"]`. **Expected:** Dialog closes; success banner (`clLifecycleSuccessBannerTransitioned`); after refetch state badge → Published; control now shows Unpublish + Archive. Reload persists.

### CUR-TC-79 — Unpublish + Archive available only when Published
- **Type:** functional / state · **Priority:** P0 · **Traces:** P7-05-FE (transition table)
- **Steps:** Inspect control at each state. **Expected:** Draft → only `lifecycle-publish-btn`; Published → `lifecycle-unpublish-btn` + `lifecycle-archive-btn`; Archived → only `lifecycle-restore-btn`. (Assert illegal buttons absent.)

### CUR-TC-80 — Unpublish a Published subject
- **Type:** functional · **Priority:** P1 · **Traces:** P7-05-FE (unpublish)
- **Steps:** `[data-testid="lifecycle-unpublish-btn"]` → confirm. **Expected:** State → Draft after refetch; success banner.

### CUR-TC-81 — Archive then Restore
- **Type:** functional · **Priority:** P1 · **Traces:** P7-05-FE (archive/restore)
- **Steps:** From Published → Archive → confirm (state Archived; control shows Restore only). Then Restore → confirm. **Expected:** Each transition surfaces success banner + state badge updates after refetch; reload persists.

### CUR-TC-82 — Version history list + Latest indicator
- **Type:** functional / state · **Priority:** P1 · **Traces:** P7-05-FE (version history)
- **Pre:** Subject published ≥1 time.
- **Steps:** Open `version-history-panel`. **Expected:** Rows show `v{n}`, formatted date (`dir="ltr"`), "Published by ID {N}", AR/EN chip; first row carries a Published `LifecycleBadge` (Latest); count chip shows total. Never-published → `version-history-empty`. Error (intercept) → error + retry.

### CUR-TC-83 — Rollback to a prior version
- **Type:** functional / persistence · **Priority:** P0 · **Traces:** P7-05-FE (rollback)
- **Pre:** Subject with ≥2 versions.
- **Steps:** On a non-latest row click `[data-testid="rollback-btn-v{n}"]` → `[data-testid="rollback-confirm-btn"]`. **Expected:** Dialog closes; history refetches; rolled-back version becomes a new latest version (a new top row). Latest row has no rollback button. Reload persists.

### CUR-TC-84 — Preview page renders read-only snapshot
- **Type:** functional · **Priority:** P1 · **Traces:** P7-05-FE (preview)
- **Steps:** From control click `[data-testid="lifecycle-preview-link"]` (→ `/curriculum/preview/subject/{id}`). **Expected:** `[data-testid="curriculum-preview-page"]` renders the entity snapshot; back breadcrumb `[data-testid="preview-breadcrumb"]`.

### CUR-TC-85 — Preview invalid type / id → 404
- **Type:** negative / boundary · **Priority:** P1 · **Traces:** P7-05-FE (preview validation)
- **Steps:** Navigate `/curriculum/preview/teacher/5` (unknown type) and `/curriculum/preview/subject/abc` and `/curriculum/preview/subject/0`. **Expected:** Next.js `notFound()` (404 page) for each.

### CUR-TC-86 — Curriculum landing = Publication Coverage
- **Type:** functional / state · **Priority:** P1 · **Traces:** P7-05-FE (coverage landing)
- **Steps:** Navigate `/curriculum`. **Expected:** `[data-testid="curriculum-landing-page"]`; `[data-testid="publication-coverage"]`; `[data-testid="curriculum-go-to-subjects"]` link → `/curriculum/subjects`.

### CUR-TC-87 — Publication coverage matrix by grade
- **Type:** functional / state · **Priority:** P0 · **Traces:** P7-05-FE (coverage matrix)
- **Steps:** Select a grade in `[data-testid="coverage-grade-select"]`. **Expected:** `[data-testid="coverage-table"]` with exactly 4 subject rows (`coverage-row-math|science|arabic|english`), AR + EN columns. Each cell = `LifecycleBadge` + `coverage-warning-chip` (⚠) when not published, or `coverage-slot-not-created` chip when slot absent. Loading → skeleton; error (intercept) → error + retry. No grade → hint shown.

### CUR-TC-88 — Coverage all-published success banner
- **Type:** functional · **Priority:** P2 · **Traces:** P7-05-FE (coverage success)
- **Pre:** A grade where all 8 slots (4 subjects × AR/EN) are Published (seed/publish via API).
- **Steps:** Select that grade. **Expected:** `[data-testid="coverage-all-published"]` banner shown; no ⚠ chips. *(If achieving full coverage is impractical, mark BLOCKED.)*

### CUR-TC-89 — Coverage rows = 4 subjects (no Social Studies)
- **Type:** negative / product-rule · **Priority:** P1 · **Traces:** Product decision "4 subjects"
- **Steps:** Inspect coverage table rows. **Expected:** Exactly Math/Science/Arabic/English; no 5th/Social Studies row.

---

## 8. Cross-cutting — RTL / a11y / auth / no-optimistic

### CUR-TC-90 — Auth redirect when signed out
- **Type:** auth · **Priority:** P0 · **Traces:** admin auth routing
- **Steps:** With NO auth state, navigate `/curriculum/subjects`. **Expected:** Redirect to `/login` (or admin guard). After LOGIN, the deep link is reachable.

### CUR-TC-91 — RTL static-string check (BLOCKED at runtime)
- **Type:** RTL-i18n · **Priority:** P2 · **Traces:** NFR (RTL); admin locale is build-time `en`
- **Steps:** Assert `dir`/`dir="auto"` attributes exist where Arabic-capable content renders: question text inputs (`dir="auto"`), markdown textareas (`dir="auto"`), block image/video URL inputs (`dir="ltr"`), grade/order numbers (`dir="ltr"`), version dates (`dir="ltr"`). **Expected:** attributes present. **Mark runtime-RTL (full ar layout) BLOCKED** — admin ships `en` only; no runtime locale switch.

### CUR-TC-92 — Accessible names / roles on key controls
- **Type:** a11y · **Priority:** P1 · **Traces:** NFR (a11y)
- **Steps:** Run axe (or assert) on subjects list, skills graph, question editor, coverage table. **Expected:** Tables have `<caption>` (sr-only) + `scope="col"` headers; reorder buttons have `aria-label`; graph listbox roles correct; dialogs `role="dialog" aria-modal="true"` with `aria-labelledby`/`aria-label`; no critical axe violations on these surfaces.

### CUR-TC-93 — Modal focus trap + ESC + focus return
- **Type:** a11y · **Priority:** P1 · **Traces:** NFR (a11y); SubjectForm/BlockForm focus trap
- **Steps:** Open SubjectForm; Tab cycles within dialog (wraps first↔last); press Escape → closes; focus returns to the trigger button. Repeat for BlockForm. **Expected:** Trap + ESC + focus-return all hold.

### CUR-TC-94 — No optimistic update on a forced-failure mutation
- **Type:** state / negative · **Priority:** P1 · **Traces:** cross-cutting no-optimistic rule
- **Steps:** Intercept a toggle-active (or create) call → 500; perform the action. **Expected:** UI does NOT show the changed state; an error banner appears; on a subsequent successful retry the state changes only after the round-trip.

---

## Test-ID quick reference (shipped)

- Login: `[name="userName"]`, `[name="password"]` (no testid on inputs).
- Subjects: `new-subject-btn`, `subjects-grade-filter`, `subjects-search-input`, `subjects-table`, `subjects-loading`, `subjects-empty-state`, `subjects-error-banner`, `subjects-row-{id}`, `subject-{id}-edit|toggle-active|delete|move-up|move-down|lifecycle-slot|lifecycle-badge`, `subjects-save-order`.
- SubjectForm: `subject-form-name|grade|code|language|order|active|cancel|save`.
- Subject detail: `subject-header-{id}`, `subject-not-found`, `subject-{id}-lifecycle-panel|lifecycle-control|version-history`.
- Units: `new-unit-btn`, `units-table|loading|empty-state|error-banner`, `units-row-{id}`, `unit-{id}-edit|toggle-active|delete|move-up|move-down|lifecycle-slot|lifecycle-badge`, `units-save-order`; UnitForm `unit-form-name|order|active|cancel|save`.
- Delete dialog: `curriculum-delete-confirm`.
- Lessons: `new-lesson-btn`, `lessons-table|loading|empty|error`, `lesson-row-{id}`, `lesson-{id}-detail-link|edit|toggle-active|delete|move-up|move-down`, `lessons-save-order`, `lesson-header-{id}`, `lesson-not-found`; LessonForm `lesson-form-name|difficulty|minutes|locked|active|cancel|save`; delete `delete-lesson-confirm-btn`.
- Blocks: `content-block-editor`, `block-editor-loading|error|empty|add-btn|save-order`, `block-list`, `block-card-{id}` (+ `-number|-move-up|-move-down|-edit|-delete|-preview|-expand`), picker `pick-type-1|2|3|4`; BlockForm `block-text-markdown`, `block-image-url|alt`, `block-video-url|caption`, `block-callout-variant|markdown`, `block-form-cancel|save`; delete `delete-block-confirm-btn`.
- Questions: `new-question-btn`, `questions-table|loading|empty|error|retry`, `question-row-{id}`, `question-{id}-text-btn|edit|toggle-active|delete|move-up|move-down`, `questions-save-order`; editor `question-editor-dialog|close|cancel|save`, `question-type-select`, `question-text-input`, `question-difficulty-select`, MCQ `mcq-option-input-{i}|mcq-correct-radio-{i}|mcq-add-option|mcq-remove-option-{i}`, TF `tf-option-true|tf-option-false`, FIB `fib-answer-input`, Matching `match-add-left|match-add-right|match-left-input-{i}|match-right-input-{i}|match-remove-left-{i}|match-remove-right-{i}|match-pair-select-{i}`; dialogs `delete-question-confirm-btn|deactivate-question-confirm-btn|activate-question-confirm-btn`.
- Skills/graph: `skill-subject-picker`, `skill-concept-filter`, `skills-search-input`, `skills-table|loading|empty-state`, `skills-row-{id}`, `skill-{id}-edit|delete`, `new-skill-btn`; SkillForm `skill-form-dialog|name|threshold|time|concept|cancel|save`; delete `skill-delete-confirm`; graph `graph-no-subject|loading|error-banner|node-listbox|node-{id}|nodes-empty|deselect-node`, `prerequisite-picker`, `add-prerequisite-btn`, `prerequisite-item-{edgeId}|remove-prerequisite-{edgeId}|prerequisites-empty`, `unlock-item-{edgeId}|unlocks-empty`, `graph-edge-error`, `skill-detail-edit-{id}`.
- Lifecycle/preview/coverage: `lifecycle-control|lifecycle-badge|lifecycle-preview-link`, `lifecycle-publish-btn|unpublish-btn|archive-btn|restore-btn`, dialog confirms `publish-confirm-btn|rollback-confirm-btn`, `version-history-panel|version-history-empty`, `rollback-btn-v{n}`, `curriculum-preview-page|preview-breadcrumb`, `curriculum-landing-page|curriculum-go-to-subjects`, `publication-coverage|coverage-grade-select|coverage-table|coverage-row-{subject}|coverage-slot-not-created|coverage-warning-chip|coverage-all-published`.
