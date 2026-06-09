# Frontend Test Cases — P2-09-FE (Home Dashboard, student-app web PWA, child surface)

> Target agent: **`frontend-e2e-tester`** (Playwright, Expo web at `:8081`, backend at `:5080`). Implement 1:1 into `tests/e2e/specs/P2-09-FE.spec.ts`. **Arabic is the default locale** — prefer `getByTestId` / `getByRole`, never assert on Arabic copy as a primary selector. Reuse the `registerParent` / `addChildViaForm` / `signInViaUI` / `signOutAndWait` helpers from `tests/e2e/specs/P1-09-FE.spec.ts` (the only way to mint a signed-in child).
>
> **RN Web testID mapping:** `View/Stack testID` → `data-testid` on the outer `<div>`; `TextInput testID` → `data-testid` on the `<input>` directly.
>
> **Existing testIDs available:** `dashboard-header`, `continue-card`, `subjects-list-section`, `sign-out-button`. **Missing testIDs** (request from `frontend`, listed in README §5) are flagged per case with a fallback selector.

## Shared preconditions / seed

- **SEED-A — "fresh child" (default for almost every case):** `registerParent` → `addChildViaForm({ language })` → `signOutAndWait` → `signInViaUI(childEmail, childPassword)`. Lands on `(child)/index` (assert `getByTestId('dashboard-header')` visible). BE returns: `continue` = Grade-1 Math **fallback** (non-null), `xp=0`, `streak=0`, `level=1`, `hearts=5`, `inPracticeMode=false`, `leaguePreview=null` (brand-new), `dailyMissions`/`weeklyMission` not rendered. Use `language:'ar'` unless a case overrides.
- **SEED-B — "progressed child":** NOT producible through the UI in one e2e run (no seeding seam). Cases needing it are **BLOCKED** (FE-TC-09, 25, 26).
- All long-setup cases: `test.setTimeout(120_000)`.

---

## Group A — Dashboard renders for a signed-in child (AC-S1, AC1/AC9)

### FE-TC-01 — Dashboard header renders on child sign-in
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A (ar).
- **Steps:**
  1. Sign in as the fresh child; wait for `getByTestId('dashboard-header')`.
  2. Assert the header is visible and the URL is the child home (not `onboarding`, not `add-child`, not `login`).
- **Expected:** `dashboard-header` visible within 25s; `page.url()` contains neither `onboarding` nor `add-child` nor `login`. Body text contains no `[object Object]` / `undefined` / raw `child.home.` keys.
- **Traces to:** AC-S1, AC1/AC9, story line 15.

### FE-TC-02 — Header shows the Hearts / Streak / XP stat strip
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A.
- **Steps:**
  1. On the child home, locate the header stats strip (the `accessibilityRole="summary"` group inside `dashboard-header`, reachable via `dashboard-header` → its descendant with `aria-label` = the composed stats label).
  2. Assert the stats group is present and its `aria-label` is a resolved string (contains digits, not a raw key).
- **Expected:** stats group exists; `aria-label` matches the resolved `statsA11y` shape (e.g. EN `/\d+ hearts, \d+ day streak, \d+ XP/`, AR contains `قلوب`/`سلسلة`/`نقطة`). XP/Streak/Hearts widgets render (no crash).
- **Traces to:** AC-S1 (XP, streak), AC11.

### FE-TC-03 — Fresh child shows the correct zero/empty state without breaking
- **Type:** state (empty) · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A (a brand-new child = the only seedable state).
- **Steps:**
  1. Sign in as the just-created child; wait for `dashboard-header`.
  2. Read the stats `aria-label`; assert it reflects zero/fresh values (streak `0`, xp `0`).
  3. Assert the screen rendered fully (header + subjects section present) with no error chrome and no `undefined`/`[object Object]` in body text.
- **Expected:** zero-state renders cleanly: stats label shows `0` streak / `0` XP (level 1, hearts default present). No JS error overlay. `subjects-list-section` visible. The screen does not blank or throw on the all-zero `DashboardDto`.
- **Traces to:** AC-S1, fresh/zero-state, brief AC9. **Note:** XP/streak/level zero values are the *expected* fresh-child state; do NOT assert >0 here (that's the BLOCKED progressed-child path).

### FE-TC-04 — Greeting uses the child's first name from `Me`
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A — the child was added with full name "E2E Child".
- **Steps:**
  1. On the child home, read the greeting H1 (the `accessibilityRole="header"` node inside `dashboard-header`).
  2. Assert it contains the first-name token ("E2E", the first whitespace-split token of the full name) and is NOT the raw key `child.home.greeting`.
- **Expected:** greeting H1 visible; in EN matches `/Hi, E2E!/`, in AR contains the name token after `مرحبا`. Falls back to "Welcome back"/"أهلاً بعودتك" only when name is empty (not the case here).
- **Traces to:** AC8, AC-S1.

### FE-TC-05 — XPBar renders the zero-state `0 / 100` counter with Latin digits
- **Type:** boundary / RTL · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A (ar).
- **Steps:**
  1. On the Arabic child home, find the XPBar counter text inside `dashboard-header`.
  2. Assert the technical counter (`0 / 100`) renders with Latin digits even in Arabic (per brand law `dir=ltr` wrap), and the bar does not throw at zero fill.
- **Expected:** XP counter present; counter string uses Latin `0 / 100` (or the primitive's equivalent), wrapped LTR. No crash at `currentXp=0`.
- **Traces to:** AC-S1 (XP), Design Spec §6 numerals. _(If the XPBar exposes no counter text, downgrade to asserting the XPBar node renders — note it in the report.)_

### FE-TC-06 — ContinueCard renders for a fresh child (BE fallback target)
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A. BE resolves a Grade-1 Math fallback `continue` for net-new children → card present.
- **Steps:**
  1. On the child home, assert `getByTestId('continue-card')` is visible.
  2. Assert it has `accessibilityRole="button"` and a resolved `aria-label` (contains the lesson name, not raw `child.home.continueA11y`).
  3. Assert the CTA text resolves ("Continue"/"متابعة"), not a raw key.
- **Expected:** `continue-card` visible, role button, `aria-label` resolved with a lesson name. Eyebrow + CTA copy resolved. **Note:** for the rare `continue === null` BE response the card is absent — if absent, assert the screen still renders header + subjects (do not fail), and log that BE returned null.
- **Traces to:** AC-S1 (continue entry point), AC2.

---

## Group B — Continue navigation (AC-S2, AC3)

### FE-TC-07 — Tapping Continue navigates to the lesson player
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A with a non-null `continue-card`.
- **Steps:**
  1. On the child home, tap `getByTestId('continue-card')`.
  2. Wait for the URL to change to the lesson route.
- **Expected:** URL navigates to `/(child)/lessons/{lessonId}?subjectId={subjectId}` — assert `page.url()` matches `/lessons\/\d+\?subjectId=\d+/` (the encoded `(child)` group may or may not appear in the path). The lesson screen mounts (body non-empty, no `login` redirect). **Open testID:** if `frontend` adds `testID="lesson-screen"`, additionally assert it visible.
- **Traces to:** AC-S2, AC3, story line 16.

### FE-TC-08 — Continue tap carries a valid lessonId AND subjectId (no silent no-op)
- **Type:** negative / robustness · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A with a non-null `continue-card`.
- **Steps:**
  1. Record `page.url()` before tapping.
  2. Tap `continue-card`.
  3. Assert the URL actually changed (the handler no-ops when `lessonId`/`subjectId` are missing — `index.tsx:197`).
  4. Assert both query/path params are present and numeric: path has `/lessons/{digits}` and query has `subjectId={digits}`.
- **Expected:** URL changes; both `lessonId` and `subjectId` are present and numeric. A no-op (URL unchanged) is a **FAIL** — it means the `ContinueTargetDto` was partial.
- **Traces to:** AC-S2, AC3; risk note 1.

### FE-TC-09 — Resume opens the *correct* lesson/subject for a progressed child — **BLOCKED**
- **Type:** functional (resume-correctness) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-B (progressed child with a real, non-fallback most-recent-attempt subject).
- **Steps (intended):** seed a child who has progressed in (e.g.) Science; sign in; tap Continue; assert the resumed lesson belongs to the most-recent-attempt subject (Science), not the Grade-1 Math fallback.
- **Expected:** Continue resumes the *right* subject/lesson.
- **Status:** **BLOCKED** — no UI/API seam to produce a progressed child in one e2e run (README §5 OQ2). FE-TC-07/08 cover that Continue navigates *somewhere valid*; the deeper "right lesson" assertion needs a backend seed. Implement as `test.skip` with this reason.
- **Traces to:** AC-S2, story line 16.

---

## Group C — Phase-4-dependent widgets degrade gracefully (AC-S3, AC6/AC7)

### FE-TC-10 — No MissionBanner / "Daily Quests" UI is rendered (mission null)
- **Type:** negative / graceful-degradation · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A.
- **Steps:**
  1. On the child home, assert there is **no** mission banner element and **no** "Coming soon" mission placeholder.
  2. Assert body text does not contain "Daily Quests" / "Daily Quest" / "مهمة اليوم" mission banner copy, and no `child.home.mission.` raw key.
- **Expected:** mission banner is absent (the primitive is never mounted). No empty shell, no placeholder text. Screen layout is intact (24px gap anchors to ContinueCard/header).
- **Traces to:** AC-S3, AC6, story line 17.

### FE-TC-11 — League preview hidden for a brand-new child (leaguePreview null)
- **Type:** negative / graceful-degradation · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A (brand-new child → `leaguePreview === null`).
- **Steps:**
  1. On the child home, assert the league preview row is absent.
  2. Selector: `getByTestId('league-preview')` (OQ — request from frontend); fallback: assert body text contains no resolved tier label ("Bronze"/"Silver"/"Gold"/"Diamond"/"برونزي"...) and no `child.home.leagueTier.`/`leaguePreview.` raw key.
- **Expected:** no league row rendered; no fallback "Bronze" sentinel. Screen renders fine without it.
- **Traces to:** AC-S3, AC7.

### FE-TC-12 — Dashboard renders fully even with all Phase-4 widgets degraded
- **Type:** state / regression · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A.
- **Steps:**
  1. On the fresh child home (mission null, league null, fresh stats), assert the three *always-present* blocks render: `dashboard-header`, a continue affordance (card or, if BE null, the clean fall-through), and `subjects-list-section`.
  2. Assert no broken layout markers (no `undefined`, no `[object Object]`, no raw key).
- **Expected:** the screen is fully usable with mission + league absent — header + continue + subjects all present, no empty placeholders for the degraded widgets.
- **Traces to:** AC-S3, story line 17.

---

## Group D — RTL (Arabic) vs LTR (English) + i18n (AC-S4)

### FE-TC-13 — Arabic child lands RTL on the dashboard
- **Type:** RTL-i18n · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A with `language:'ar'`.
- **Steps:**
  1. Sign in as the Arabic child; wait for `dashboard-header`.
  2. Assert `html[dir]==='rtl'` and `html[lang]==='ar'`.
- **Expected:** dashboard renders RTL for the Arabic child.
- **Traces to:** AC-S4, story line 18. **Note:** depends on the P1-09 locale-from-`Me` fix (BCP-47 normalization) — if the known `ar-EG` mismatch resurfaces, this fails the same way as P1-09 FE-TC-09; report it as the same root cause, not a P2-09 defect.

### FE-TC-14 — English child lands LTR on the dashboard
- **Type:** RTL-i18n · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A with `language:'en'`.
- **Steps:**
  1. Sign in as the English child; wait for `dashboard-header`.
  2. Assert `html[dir]==='ltr'` and `html[lang]==='en'`.
- **Expected:** dashboard renders LTR for the English child (header greeting row + stats strip + continue card mirror to LTR).
- **Traces to:** AC-S4, story line 18.

### FE-TC-15 — Sign-out is present, quiet, and works from the child dashboard (preserved W11)
- **Type:** functional / auth · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A.
- **Steps:**
  1. On the child home, assert `getByTestId('sign-out-button')` visible and reachable by role button (`name: /sign out|تسجيل الخروج/i`).
  2. Tap it; wait for redirect to `/login`.
- **Expected:** sign-out visible (minHeight ≥ 48 — see FE-TC-20), tap → `/login`, `login-username` visible.
- **Traces to:** AC12, AC-S1.

### FE-TC-16 — No raw i18n keys on the dashboard in either locale
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A — once with `ar`, once with `en`.
- **Steps:**
  1. On the loaded child home, walk visible text nodes; collect any matching `^(child|common|auth|onboarding|parent)\.[a-zA-Z.]+$` or `missingKey`.
  2. Repeat for both locales.
- **Expected:** zero raw keys in both Arabic and English (covers `child.home.greeting`, `continue.*`, `yourSubjects`, `errorRetry`, `statsA11y`, `leagueTier.*`, etc.).
- **Traces to:** AC-S4.

### FE-TC-17 — Stats strip a11y label is a single resolved screen-reader sentence
- **Type:** a11y · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A (ar) and (en).
- **Steps:**
  1. Read the `aria-label` on the stats group (`accessibilityRole="summary"` inside `dashboard-header`).
  2. Assert it is the composed sentence with hearts/streak/xp interpolated (no `{{hearts}}` literal, no raw key).
- **Expected:** EN matches `/\d+ hearts, \d+ day streak, \d+ XP/`; AR contains `قلوب` + `سلسلة` + `نقطة` with digits interpolated. No `{{` placeholders.
- **Traces to:** AC11, AC-S4.

---

## Group E — Loading / error states (AC9/AC10, kid-UX)

### FE-TC-18 — Loading skeleton shows while the dashboard query is in flight
- **Type:** state (loading) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A; intercept `**/api/Learning/Dashboard**` to delay the response ~3–5s (Playwright route + `setTimeout` before `continue()`).
- **Steps:**
  1. With the dashboard request delayed, sign in / navigate to child home.
  2. During the delay, assert the header skeleton variant is present (shimmer placeholders inside `dashboard-header`) and the ContinueCard skeleton placeholder area is shown (the `$cardSoft` 96px block), not the real card.
  3. Release the response; assert the real header + continue card resolve.
- **Expected:** skeleton visible during in-flight; resolves to live content after. No flash of error chrome.
- **Traces to:** AC9. _(If timing is too tight to catch the skeleton deterministically, downgrade to asserting the screen eventually resolves and note the flake in the report.)_

### FE-TC-19 — Dashboard error strip renders (scoped) on a failed dashboard fetch, subjects still render
- **Type:** state (error) · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A. Intercept `**/api/Learning/Dashboard**` → 500 (or `BaseResponse` `successed:false`). Leave `Subjects` endpoint untouched.
- **Steps:**
  1. With the dashboard endpoint forced to fail, navigate to / reload the child home.
  2. Assert the dashboard error strip is shown — selector `getByTestId('dashboard-error')` (OQ; fallback `getByRole('alert')`) — with resolved copy (`child.home.errorRetry` → "Couldn't load your dashboard. Try again" / "تعذّر تحميل لوحتك. أعد المحاولة"), not a raw key.
  3. Assert `dashboard-header` still renders (zero-state values) AND `subjects-list-section` still renders beneath (error is scoped to the dashboard band per AC10).
- **Expected:** error strip visible with resolved copy; header + subjects section still present; ContinueCard absent. No full-screen blank.
- **Traces to:** AC10, AC-S1.

### FE-TC-20 — Retry on the dashboard error refetches and recovers
- **Type:** state (error) / functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** continue from FE-TC-19's failing state.
- **Steps:**
  1. With the error strip shown, remove the route interception (so the next fetch succeeds).
  2. Tap the Retry button — selector `getByTestId('dashboard-error-retry')` (OQ; fallback `getByRole('button', { name: /retry|أعد المحاولة/i })`).
  3. Assert the error strip unmounts and the real dashboard content (continue card / header live values) appears.
- **Expected:** Retry calls `dashboardQuery.refetch()`; on success the strip disappears and the ContinueCard renders. No raw key, no crash.
- **Traces to:** AC10.

---

## Group F — Subjects section: 4 product subjects only (AC4/AC13, kid-UX)

### FE-TC-21 — Subjects section renders beneath the dashboard
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A (a grade-known child; add-child set Grade 1).
- **Steps:**
  1. On the child home, assert `getByTestId('subjects-list-section')` visible.
  2. Assert the "Your subjects" / "موادك" eyebrow is a resolved header (not raw `child.home.yourSubjects`).
- **Expected:** subjects section present with resolved eyebrow; subject rows render (4 expected — see FE-TC-22).
- **Traces to:** AC4, AC13.

### FE-TC-22 — Exactly the 4 product subjects appear (Math / Science / Arabic / English)
- **Type:** functional / validation · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A, Grade 1.
- **Steps:**
  1. Inside `subjects-list-section`, count the subject rows (selector `subject-row-{key}` if added — OQ; fallback: count tappable rows under the section, or assert each of the 4 expected subject names is present in either locale).
  2. Assert exactly 4 rows and that each of math/science/arabic/english is represented (by `subject-row-math|science|arabic|english`, or by localized name match).
- **Expected:** exactly 4 subject rows; the set is {Math, Science, Arabic, English}. Canonical order Math → Science → Arabic → English.
- **Traces to:** AC13, product override "4 subjects."

### FE-TC-23 — No mock / non-product subjects (no Reading, Art, or Social Studies)
- **Type:** negative · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A, Grade 1.
- **Steps:**
  1. On the child home, read all text within `subjects-list-section`.
  2. Assert it contains NONE of: "Reading", "Art", "Social Studies", "Social", "الدراسات الاجتماعية", "القراءة", "الفنون" (the mock-capture / forbidden subjects).
  3. Assert no 5th subject row exists.
- **Expected:** no Reading/Art/Social-Studies anywhere; the defensive `filterSubjects()` 4-key allow-list holds even if the API returns extras.
- **Traces to:** product override "4 subjects, no Social Studies"; mock-capture warning (README §2).

### FE-TC-24 — Tapping a subject row navigates to that subject (W11 routing preserved)
- **Type:** functional · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A, Grade 1.
- **Steps:**
  1. Tap the Math subject row (`subject-row-math`, or the first row).
  2. Assert URL navigates to `/(child)/subjects/{id}` (matches `/subjects\/\d+/`).
- **Expected:** subject navigation works (W11 path preserved). Not a `login` redirect.
- **Traces to:** AC4, AC13.

---

## Group G — Kid-UX + advanced ContinueCard chrome

### FE-TC-25 — ContinueCard Boss / Completed chrome (Boss badge, Replay CTA) — **BLOCKED**
- **Type:** state / functional · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-B — a child whose `continue` resolves to a Completed node (`nodeState===2`, "Replay" CTA) and/or a Boss node (`isBoss===true`, 👑 badge + 🔥 CTA prefix).
- **Steps (intended):** seed each chrome variant; assert the Completed variant shows the Replay CTA + quiet border; assert the Boss variant shows the Boss badge + 🔥 CTA.
- **Expected:** correct chrome per node state.
- **Status:** **BLOCKED** — a fresh child's fallback continue is an Available (`nodeState 1`), non-Boss Grade-1 Math lesson; Completed/Boss states aren't reproducible via the UI seed path (README §5 OQ2). Implement as `test.skip` with this reason.
- **Traces to:** AC5, Design Spec §2 (ContinueCard states).

### FE-TC-26 — Widgets reflect real data after some progress (XP>0, streak>0, real continue) — **BLOCKED**
- **Type:** functional / regression · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-B — a progressed child (completed lessons → XP, streak, league populated).
- **Steps (intended):** sign in as a progressed child; assert the stats strip shows XP>0 / streak>0, the league preview row renders with a real tier + rank, and the ContinueCard targets the most-recent-attempt subject.
- **Expected:** widgets render *live* non-zero data.
- **Status:** **BLOCKED** — no seam to accrue real XP/streak/league/progress in a single e2e run (README §5 OQ2). The fresh-child zero-state is covered by FE-TC-03; this is the positive complement. Implement as `test.skip` with this reason.
- **Traces to:** AC-S1, AC-S3 ("widgets reflect real data after some progress").

### FE-TC-27 — Locale switch mid-session reflects on the dashboard — **BLOCKED (partial)**
- **Type:** RTL-i18n / state · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** SEED-A signed-in child.
- **Steps (intended):** from the child dashboard, switch UI language and assert `html[dir]` flips + dashboard copy re-localizes live.
- **Expected:** instant flip on web (no restart), dashboard re-renders in the new locale.
- **Status:** **BLOCKED** — there is **no in-app language switcher on the child dashboard** (the switcher lives on Login per P1-09; child locale is driven by `Me.preferredLanguage`). Cannot switch locale from within the child surface. Covered indirectly by FE-TC-13/14 (per-child locale at sign-in). Implement as `test.skip` with this reason. _(If a child-surface language control is later added, unblock.)_
- **Traces to:** AC-S4.

### FE-TC-28 — Unset `EXPO_PUBLIC_GOOGLE_CLIENT_ID` effect on dashboard — **BLOCKED (N/A)**
- **Type:** config / negative · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** child dashboard.
- **Status:** **BLOCKED / NOT APPLICABLE** — the Google client ID only affects the Login social button (P1-12), not the child dashboard. No env-coupled surface on this screen. Recorded for completeness; implement as `test.skip` ("no env-coupled surface on the home dashboard").
- **Traces to:** environment edge-case checklist.

### FE-TC-29 — Native RTL restart boundary on the dashboard — **BLOCKED**
- **Type:** RTL-i18n · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Status:** **BLOCKED** — native `I18nManager.forceRTL` + restart is untestable in Playwright web (same boundary as P1-09 FE-TC-22). On web the flip is instant; covered by manual/native QA. Implement as `test.skip` with this reason.
- **Traces to:** AC-S4 (native parity).

---

## Implementation notes for `frontend-e2e-tester`

- Header the spec file with the testID legend (mirror `P1-09-FE.spec.ts`). Note which testIDs were **missing** at implementation time and which fallback you used.
- Group tests with `test.describe` per Group A–G above; keep the FE-TC id in each test name.
- For route-interception cases (FE-TC-18/19/20), match `**/api/Learning/Dashboard**`; restore with `page.unrouteAll({ behavior: 'ignoreErrors' })` in `finally`.
- BLOCKED cases (FE-TC-09, 25, 26, 27, 28, 29) → `test.skip(true, '<reason>')` + an empty body, so they appear in the run as explicitly skipped (not silently missing).
- Record every defect (missing testID, raw key leak, scoped-error regression, continue no-op) in `execution-report.md` and file back to `frontend`.
