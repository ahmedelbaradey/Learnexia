# P2-02-FE — Frontend E2E Test Cases (for `frontend-e2e-tester`)

> Implement into `tests/e2e/specs/P2-02-FE.spec.ts`. One test per case (1:1).
> **Surface:** child learning browse — child home embedded subjects list (S1), subject-detail shell (S2), Lessons tab (S3).
> **Selectors:** `getByTestId` first; fall back to `getByRole('button', { name: <ar|en regex> })` only where no testID exists (note it as a defect to fix). Arabic is the default locale — avoid copy selectors where possible.
> **Read first:** the `README.md` in this folder (§1 surface-drift, §4 BLOCKED, §5 OQ/assumptions). The W11 standalone "Subjects" screen is now the **embedded list on the child home** — there is no `/subjects` route.

---

## Shared preconditions & helpers

**Stack (per HANDOFF run recipe):** backend on `:5080` with a **fresh-seeded** curriculum (Development `dotnet run` migrates + seeds on first boot); Expo web on `:8081` (Node 20, `EXPO_OFFLINE=1`); reuse the running server (no `CI` env).

**Seed pattern (reuse `P1-09-FE.spec.ts` helpers verbatim):**
- `registerParent(page, { email })` → lands on add-child.
- `addChildViaForm(page, { language: 'ar' | 'en' })` → returns `{ email, password }`; **picks Grade 1** today (`selectOption(page, 'add-child-grade', /Grade 1|الصف الأول/i)`).
- `signOutAndWait(page)` then `signInViaUI(page, childEmail, childPassword)` → child lands on the home dashboard (`getByTestId('dashboard-header')` visible).

**Canonical child-home / browse entry:** after child sign-in, the embedded subjects list is under `getByTestId('subjects-list-section')` on `/(child)/`. Scroll into view if needed (it sits below the dashboard header + continue/league rows).

**Subject-name regexes (locale-tolerant) for fallback row selectors:**
- Math → `/^(Math|Mathematics|الرياضيات)$/i`
- Science → `/^(Science|العلوم)$/i`
- Arabic → `/^(Arabic|العربية)$/i`
- English → `/^(English|الإنجليزية)$/i`

**Common assumptions:** A1 (seeded grade-1 curriculum non-empty), A2 (no mastery percent rendered), A4 (Arabic default; child locale from `Me`), A5 (no `/subjects` route). See README §5.

---

## Group A — Browse happy path & subjects list (S1)

### FE-TC-01 — Child lands on home and sees the embedded subjects list
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** seed ar child (grade 1), sign in as child.
- **Steps:**
  1. After sign-in, wait for `getByTestId('dashboard-header')`.
  2. Scroll to / wait for `getByTestId('subjects-list-section')`.
  3. Assert the "Your subjects" eyebrow (role `header`) is present inside the section.
- **Expected:** `subjects-list-section` is visible; no `/subjects` route involved (URL is `/(child)/` / home). The section is the browse entry point.
- **Traces to:** AC1.

### FE-TC-02 — Subjects list renders exactly 4 subject rows (no Social Studies)
- **Type:** product-override / functional · **Priority:** P0
- **Preconditions:** seed ar child (grade 1), sign in.
- **Steps:**
  1. Open child home; wait for `subjects-list-section`.
  2. Within the section, count the rendered subject rows (role `button` rows inside the section, excluding the eyebrow).
  3. Collect each row's accessible name.
- **Expected:** exactly **4** rows; their names match the 4 product subjects (Math/Science/Arabic/English in some locale form); **no** row matches `/Social|الاجتماعية|الدراسات/i`.
- **Traces to:** AC1, Product decision (4 subjects).

### FE-TC-03 — Grade context: child sees their grade's subjects (grade caption present)
- **Type:** functional / grade-filter · **Priority:** P0
- **Preconditions:** seed ar child **grade 1**, sign in.
- **Steps:**
  1. Open child home.
  2. Read the dashboard grade caption (`child.home.gradeCaption` → "Grade 1"/"الصف ١"). Assert it reflects grade 1 (Eastern numeral in ar).
  3. Assert the subjects list is populated (4 rows) — i.e. content is scoped to the signed-in child's grade.
- **Expected:** grade caption shows the child's grade; subjects render for that grade. (The server-side grade filter is backend QC; FE asserts the grade-scoped content the child actually receives.)
- **Traces to:** AC1, AC3.

### FE-TC-04 — Tap a subject → navigates to the subject-detail Lessons tab
- **Type:** functional · **Priority:** P0
- **Preconditions:** seed ar child (grade 1), sign in.
- **Steps:**
  1. Open child home; locate the Math row (testID if added per OQ1, else `getByRole('button', { name: /Math|الرياضيات/i })`).
  2. Tap it.
  3. Wait for URL `**/subjects/**`.
- **Expected:** URL matches `/(child)/subjects/{id}`; the subject-detail shell renders (back chevron + `SegmentedTabs` with Lessons active). Lessons-tab body renders units/lessons OR an empty state — not a crash/blank.
- **Traces to:** AC2.

### FE-TC-05 — Lessons render grouped by unit, units in sequence order
- **Type:** functional · **Priority:** P0
- **Preconditions:** seed ar child (grade 1); a subject with ≥2 units seeded (A1).
- **Steps:**
  1. Open a subject with multiple units (Math expected to be richest from the seeder).
  2. Read the visible unit eyebrow labels in DOM order (`child.subjects.lessons.unitLabel` → "Unit {n}"/"الوحدة {n}").
  3. Extract the `{n}` values (handle Eastern numerals in ar).
- **Expected:** unit numbers appear in **ascending** order (sequence order); each unit header is followed by its lesson cards before the next unit. No unit out of order.
- **Traces to:** AC2.
- **Note:** if the seeded subject has only 1 unit, downgrade to "≥1 unit renders with its lessons" and flag the seeder limitation in the report.

### FE-TC-06 — Subject-detail header shows which subject you opened — **BLOCKED**
- **Type:** functional · **Priority:** P1 · **Status:** BLOCKED
- **Blocker:** `_layout.tsx` header renders `t('child.subjects.title')` ("Subjects"/"المواد"), **not the real subject name**, and has no testID. Cannot assert the opened subject. (README §4, OQ5.)
- **Intended steps (when unblocked):** open the Math subject → assert the header title contains the localized subject name (Math/الرياضيات) via `getByTestId('subject-detail-header')`.
- **Unblock:** render the real subject name + add `testID="subject-detail-header"`.
- **Traces to:** AC2, Design Spec §1 Surface 2.

### FE-TC-07 — Lessons within a unit render in sequence order
- **Type:** functional · **Priority:** P1
- **Preconditions:** seed ar child (grade 1); a unit with ≥2 lessons (A1).
- **Steps:**
  1. Open a subject; pick the first unit with multiple lessons.
  2. Read the lesson titles in DOM order under that unit header.
- **Expected:** lessons appear in the unit's seeded sequence order (the FE sorts by `sequenceOrder` asc — assert the rendered order is stable and matches the order returned, i.e. monotonic, not shuffled across reloads).
- **Traces to:** AC2.
- **Note:** without per-lesson testID/sequence exposure (OQ2), assert order stability across two loads rather than absolute numbers.

---

## Group B — States: loading / empty / error / 404 (S1 + S3)

### FE-TC-08 — Subject with no lessons → "Coming soon" empty state
- **Type:** state (empty) · **Priority:** P0
- **Preconditions:** seed ar child; identify a subject that returns **0 units / all-empty** for the seeded grade (e.g. a thin subject like English/Arabic at grade 1 — confirm against the seeder, A1). If none exists, simulate via route stub returning `{ successed: true, data: [] }` for `**/Subjects/*/Lessons*`.
- **Steps:**
  1. Open the empty subject (or open any subject with the lessons endpoint stubbed to empty).
  2. Wait for the body to settle (no shimmer).
- **Expected:** the empty-state copy renders — `child.subjects.empty` = "Coming soon — no lessons yet" / "قريباً — لا توجد دروس بعد". No crash, no error chrome.
- **Traces to:** AC4.

### FE-TC-09 — Unit with empty lessons → dashed "Coming soon" empty-unit tile
- **Type:** state (empty) · **Priority:** P2
- **Preconditions:** a unit with `lessons: []` (seeder-dependent). If unavailable, stub the lessons response so one unit has `lessons: []`.
- **Steps:**
  1. Open the subject with a partially-empty unit (stub if needed).
  2. Locate the unit header whose lessons are empty.
- **Expected:** the unit header still renders + a single dashed-border tile with `child.subjects.lessons.emptyUnit` = "Coming soon"/"قريباً"; the unit is **not** hidden.
- **Traces to:** AC4, Design Spec §12 Q1.
- **Note:** likely needs a route stub — mark accordingly if the seeder has no empty unit.

### FE-TC-10 — Subjects list shows shimmer skeletons while loading
- **Type:** state (loading) · **Priority:** P1
- **Preconditions:** seed ar child; throttle/delay `**/Subjects/ForGrade*` (and/or `**/Me`) via `page.route` with a delayed fulfill so the loading union holds.
- **Steps:**
  1. Sign in / navigate to home with the subjects (and Me) responses delayed ~2–3s.
  2. Immediately inspect the `subjects-list-section`.
- **Expected:** 4 shimmer skeleton rows render (loading `SubjectRow` chrome, no inner content) before the real rows appear. No raw key, no blank gap.
- **Traces to:** Design Spec §1 Surface 1 / §2 SubjectRow loading.

### FE-TC-11 — Lessons tab shows shimmer skeleton while loading
- **Type:** state (loading) · **Priority:** P2
- **Preconditions:** seed ar child; delay `**/Subjects/*/Lessons*`.
- **Steps:**
  1. Open a subject with the lessons response delayed.
  2. Inspect the lessons-tab body immediately.
- **Expected:** skeleton unit/lesson placeholders render (3 unit blocks × shimmer cards) before content; then real content replaces them.
- **Traces to:** Design Spec §1 Surface 3 (loading).

### FE-TC-12 — Subjects list error + retry recovers
- **Type:** state (error) · **Priority:** P1
- **Preconditions:** seed ar child; `page.route('**/Subjects/ForGrade*', → 500)` for the first call, then unroute.
- **Steps:**
  1. Sign in / load home with `Subjects/ForGrade` forced to 500.
  2. Assert the error copy `child.subjects.errorRetry` ("Couldn't load. Try again" / "تعذّر التحميل…") + a retry button (`common.retry`).
  3. Unroute the failure; tap retry.
- **Expected:** error block shown on failure; after unroute + retry the 4 subject rows render. Retry button height ≥ 44px (kid-UX).
- **Traces to:** Design Spec §1 Surface 1 (error), NFR-6.

### FE-TC-13 — Unknown subject id → "Subject not found" + Back — **BLOCKED**
- **Type:** state (404) · **Priority:** P1 · **Status:** BLOCKED
- **Blocker:** no testID on the 404 block; backend may return empty-200 (→ empty state) rather than 404 for an unknown-but-valid id. (README §4.)
- **Intended steps (when unblocked):** signed-in child deep-links `/(child)/subjects/999999` → assert `child.subjects.subjectNotFound` ("Subject not found"/"المادة غير موجودة") + a "Back to Subjects" ghost button that returns to `/(child)/`.
- **Unblock:** add `testID="subject-not-found"`; confirm the backend 404 contract for unknown subject ids (the FE 404 branch keys on `error.statusCode === 404`).
- **Traces to:** Design Spec §1 Surface 3 (404).

### FE-TC-14 — Lessons tab error + retry recovers
- **Type:** state (error) · **Priority:** P1
- **Preconditions:** seed ar child; `page.route('**/Subjects/*/Lessons*', → 500)`.
- **Steps:**
  1. Open a subject with the lessons endpoint forced to 500 (ensure it is NOT a 404 so the generic-error branch shows, not the 404 branch).
  2. Assert `child.subjects.errorRetry` + retry button.
  3. Unroute; tap retry.
- **Expected:** generic error + retry shown on failure; content renders after retry.
- **Traces to:** Design Spec §1 Surface 3 (error).

---

## Group C — Subject-detail shell & navigation (S2)

### FE-TC-15 — Subject-detail shell: back chevron + SegmentedTabs (Lessons default)
- **Type:** functional · **Priority:** P0
- **Preconditions:** seed ar child; open any subject.
- **Steps:**
  1. Open a subject.
  2. Assert the SegmentedTabs control is present with two segments — Lessons (`child.subjects.tabs.lessons`) + Skill Tree (`child.subjects.tabs.tree`) — and Lessons is active by default (the lessons body is shown, not the tree).
  3. Assert a back control (role `button`, label `child.subjects.backToSubjects`) is present, hit area ≥ 48px.
- **Expected:** both tabs labelled (no raw keys); Lessons active; back control present + large enough.
- **Traces to:** Design Spec §1 Surface 2, AC2.

### FE-TC-16 — No raw i18n keys inside the Lessons tab — **BLOCKED (soft)**
- **Type:** RTL-i18n · **Priority:** P1 · **Status:** BLOCKED (on deterministic subject navigation)
- **Blocker:** reaching a populated lessons tab needs a deterministic subject row (OQ1) and seeded lessons. Without a row testID the open step is locale-brittle.
- **Intended steps (when unblocked):** open a seeded subject → walk all text nodes in the lessons body → assert none match `/^(child|common)\.[a-zA-Z.]+$/` and none contain `missingKey`.
- **Unblock:** add per-`SubjectRow` testID (OQ1).
- **Traces to:** i18n integrity (no raw keys).

### FE-TC-17 — Back from subject-detail returns to child home
- **Type:** functional · **Priority:** P1
- **Preconditions:** seed ar child; open a subject.
- **Steps:**
  1. From the Lessons tab, tap the back control.
  2. Wait for navigation.
- **Expected:** lands back on `/(child)/` with `subjects-list-section` visible (the layout `handleBack` pushes `/(child)/`).
- **Traces to:** AC2 (navigation), Design Spec §1 Surface 2.

---

## Group D — Product overrides & defensive filter

### FE-TC-19 — Social Studies never renders (defensive 4-subject filter)
- **Type:** product-override · **Priority:** P0
- **Preconditions:** seed ar child. (Even if the backend returns a 5th subject, the FE `filterSubjects` drops anything not in the 4 product keys.)
- **Steps:**
  1. Open child home; read all subject row names in the section.
  2. Optionally stub `**/Subjects/ForGrade*` to inject a 5th "Social Studies / الدراسات الاجتماعية" entry and reload.
- **Expected:** no row matches `/Social|الاجتماعية|الدراسات/i`; row count stays 4 even when a 5th subject is injected (defensive filter wins).
- **Traces to:** Product decision (no Social Studies), Design Spec §11.

### FE-TC-20 — Duplicate / unknown subjects deduped + dropped
- **Type:** negative / product-override · **Priority:** P2
- **Preconditions:** seed ar child; stub `**/Subjects/ForGrade*` to return duplicates (two "Math") + an unknown subject ("Coding").
- **Steps:**
  1. Inject the stubbed list and reload home.
  2. Count rendered rows.
- **Expected:** Math appears once (dedup by `SubjectKey`); "Coding" (unknown key) is dropped; ≤4 rows.
- **Traces to:** `filterSubjects` (subjects.ts), product constraint.

### FE-TC-21 — Subjects render in canonical order (Math → Science → Arabic → English)
- **Type:** functional · **Priority:** P2
- **Preconditions:** seed ar child; (optionally) stub `Subjects/ForGrade` returning the 4 in a shuffled order.
- **Steps:**
  1. Read the 4 subject row names in DOM order.
- **Expected:** order is Math, Science, Arabic, English regardless of API order (`ORDER` in subjects.ts). Mastery caption is absent on every row (A2 — not a defect).
- **Traces to:** `filterSubjects` canonical sort, Design Spec §3.1.

---

## Group E — RTL / i18n / learning-language

### FE-TC-22 — Arabic child: RTL layout + Arabic subject names + own-language content
- **Type:** RTL-i18n / learning-language · **Priority:** P0
- **Preconditions:** seed **ar** child (grade 1), sign in.
- **Steps:**
  1. Land on child home. Assert `html[dir=rtl][lang=ar]`.
  2. Assert subject row names render in Arabic (e.g. a row matches `/الرياضيات|العلوم|العربية|الإنجليزية/`).
  3. Open a subject; assert the lessons tab content (unit labels, copy) is Arabic + RTL.
- **Expected:** RTL throughout; Arabic copy; the child sees Arabic-medium content (its `learningLanguage`). No Latin-only fallback for subject names.
- **Traces to:** AC1/AC2, Design Spec §7, learning-language filter (A3).

### FE-TC-23 — English child: LTR layout + English content — **BLOCKED**
- **Type:** RTL-i18n / learning-language · **Priority:** P1 · **Status:** BLOCKED
- **Blocker:** needs a child seeded with `learningLanguage: 'en'` and a reliable sign-in. The single-child UI seed helper can do one `en` child per run, but combining with other cases + the heavy onboarding makes it flaky; a second-child path isn't exercised. (README §4, OQ3.)
- **Intended steps (when unblocked):** seed en child → sign in → assert `html[dir=ltr][lang=en]` + subject names in English + lessons tab English/LTR.
- **Unblock:** API seed helper (OQ3) OR a stable add-child(en) standalone test.
- **Traces to:** AC1/AC2, Design Spec §7, learning-language filter.

### FE-TC-24 — Subject-row mirroring in RTL (chevron + flex direction)
- **Type:** RTL · **Priority:** P2
- **Preconditions:** ar child signed in (RTL).
- **Steps:**
  1. On the home subjects list, inspect a subject row's computed `flex-direction`.
- **Expected:** the row uses `row-reverse` in RTL (icon tile + chevron mirror to the logical leading/trailing edges); the trailing chevron glyph is the RTL variant (`‹`). No hardcoded LTR layout.
- **Traces to:** Design Spec §3.1 logical RTL, §7.

### FE-TC-25 — Different grade → grade-scoped content
- **Type:** grade-filter · **Priority:** P2
- **Preconditions:** seed two children (or re-seed) — one grade 1, one a different grade (e.g. grade 2) — if the add-child grade picker allows it.
- **Steps:**
  1. Sign in as the grade-2 child; read subjects + open one subject's lessons.
  2. Compare against the grade-1 child's content (unit/lesson set differs).
- **Expected:** content reflects the signed-in child's grade (different grade → different units/lessons). Still exactly 4 subjects.
- **Traces to:** AC3.
- **Note:** if seeding a 2nd grade is impractical, downgrade to "grade-1 child sees grade-1-appropriate content" and flag.

### FE-TC-26 — Login UI in English, then browse renders LTR for an English-medium child (cross-check)
- **Type:** RTL · **Priority:** P2
- **Preconditions:** seed ar child; on the login screen switch UI to English (`locale-switch-en`) before sign-in.
- **Steps:**
  1. Switch login UI to English (assert `html[dir=ltr]`).
  2. Sign in as the **ar** child.
- **Expected:** after sign-in the child's `Me.preferredLanguage` (ar) drives direction → flips back to RTL (the P1 fix). Subjects render in Arabic/RTL. (Confirms locale comes from the child profile, not the login-UI choice.)
- **Traces to:** Design Spec §7, A4; cross-ref P1-09-FE FE-TC-09.

### FE-TC-27 — No raw i18n keys on the browse chain (ar + en)
- **Type:** i18n integrity · **Priority:** P1
- **Preconditions:** ar child signed in; then repeat after switching the home/locale to English where feasible (or a second en run).
- **Steps:**
  1. On the child home (subjects list) walk all visible text nodes.
  2. Assert none match `/^(child|common)\.[a-zA-Z.]+$/` and none contain `missingKey`.
- **Expected:** no raw keys on the subjects list in either locale (subject names, eyebrow, empty/error copy all resolved).
- **Traces to:** i18n integrity, NFR.
- **Note:** the lessons-tab variant is FE-TC-16 (BLOCKED on row testID).

---

## Group F — Kid-UX & auth (NFR-6 + guard)

### FE-TC-17b *(numbered FE-TC-17 above is back-nav; this is the kid-UX tap-target case)* — Subject rows meet kid-UX tap-target floor
- **Type:** a11y / kid-UX · **Priority:** P1 · **ID:** FE-TC-18
- **Preconditions:** ar child signed in.
- **Steps:**
  1. On the home subjects list, measure each subject row's bounding box height.
  2. Assert no `[object Object]` / `undefined` text anywhere on the page.
- **Expected:** each subject row ≥ 48px tall (Design Spec §8 says ~84px); role=button + accessible name present; no scary error chrome / leaked objects.
- **Traces to:** NFR-6 kid-UX, Design Spec §8.

### FE-TC-28 — Signed-out deep-link to a subject is bounced to Login — **BLOCKED (cross-ref)**
- **Type:** auth · **Priority:** P1 · **Status:** BLOCKED (redundant with P1 route-guard pass)
- **Blocker:** the `(child)` route-group guard (`useGroupGuard`) was added + verified in the P1 frontend QC pass; re-testing the global guard here is redundant, and `useSubjectLessons` is auth-required (401) so a signed-out deep-link can't render content anyway.
- **Intended steps (if run):** in a clean (signed-out) context `page.goto('/(child)/subjects/1')` → assert redirect to `/login` (`getByTestId('login-username')` visible), no subject content leaked.
- **Unblock / decision:** either keep as a thin smoke assertion here or cross-reference P1-09-FE route-guard cases and drop. Lead call.
- **Traces to:** auth gate; cross-ref P1 `useGroupGuard`.

---

## Implementation notes for the tester

- **Reuse** `tests/e2e/specs/P1-09-FE.spec.ts` helpers (`registerParent`, `addChildViaForm`, `signInViaUI`, `signOutAndWait`, `uniqueEmail`, `selectOption`). Set `test.setTimeout(120_000)` per test (heavy seeding).
- **Scope the section:** anchor row queries inside `getByTestId('subjects-list-section')` to avoid matching the dashboard greeting / continue card.
- **Route stubs** (`page.route`) are the practical way to force empty / error / 5th-subject / shuffled-order states deterministically when the seeder can't guarantee them — prefer stubbing the specific endpoint (`**/Subjects/ForGrade*`, `**/Subjects/*/Lessons*`) and `unrouteAll` after.
- **Eastern numerals:** in ar, "Unit 1" renders "الوحدة ١" — normalize Eastern digits before numeric comparisons.
- **For every BLOCKED case:** file the requested testID/seam back to `frontend` (OQ1–OQ3, OQ5) and mark the case BLOCKED with the reason in `execution-report.md`. Do not reach into CSS to force a pass.
