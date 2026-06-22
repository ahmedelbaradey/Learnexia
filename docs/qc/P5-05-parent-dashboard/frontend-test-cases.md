# P5-05 Parent Dashboard — Frontend (Web E2E) Test-Case Catalog

> **QC backfill.** The story is already built and an `execution-report.md` exists (21 PASS / 3 SKIP).
> This catalog is the **formal, traceable** test-case set the `frontend-e2e-tester` should converge
> the spec onto: it documents what `tests/e2e/specs/P5-05-parent-dashboard.spec.ts` **already covers**
> and the **NEW gaps** that should be added.
>
> **Surface under test:** `apps/student-app` (Expo universal RN-Web + Tamagui), the parent area
> `app/(parent)/{overview,reports,children,…}`. Web PWA driven by Playwright.
>
> **Design only.** No executable test code here. The `frontend-e2e-tester` implements the NEW cases
> and writes results into `execution-report.md`.

---

## 0. Ground truth — what the running code actually does (read before writing tests)

The **implementation diverges from the design spec** in load-bearing ways. Tests MUST assert the
*real* shapes below, not the design-spec shapes:

| Concern | Design spec said | **Real implementation (authoritative)** |
|---|---|---|
| Chart endpoints | 3 separate routes (`weekly-activity`, `twenty-day-activity`, `time-of-day`) | **ONE** route `GET api/Parent/Children/{id}/Reports` → `useChildReports`; feeds all 3 charts (`dailyXpSeries`, `xpTrend20Day`, `timeOfDayBuckets`). |
| KPI route | `…/weekly-kpis` | `GET api/Parent/Children/{id}/WeeklyKpis` → `useWeeklyKpis` |
| Mastery fields | `subjects[].subjectId` + `masteryPercent` + `lessonsCount` | `bySubject[].subjectCode` (0-indexed) + `percent`; **no `lessonsCount`** (mastery rows show `%` only). |
| Daily/20-day fields | `{ date, xpEarned }` | `{ day, xp }` |
| Time-of-day | 8 hourly bars (`hour` int, `minutes`) + "peak insight 4–5pm" | **4 named buckets** `{ bucket: "Morning"\|"Afternoon"\|"Evening"\|"Night", totalXp }`; insight tip names the **bucket**, not an hour range. |
| Envelope wire flag | `Successed` | lowercase **`successed`** on the wire (client unwraps both `get` and paginated). |
| Subject map | `SUBJECT_ID_MAP` (1-indexed) | `PARENT_SUBJECT_CODE_MAP` / `SUBJECT_CODE_MAP` (0-indexed: 0=Math,1=Science,2=Arabic,3=English). |

**testIDs in the shipped code** (confirmed in source):
`overview-root`, `overview-kpi-region`, `overview-mastery-region`, `overview-focus-recommendations-row`,
`daily-activity-card`, `daily-activity-chart`, `recommendations-card`,
`reports-root`, `reports-kpi-region`, `reports-mastery-panel`, `reports-loading`, `reports-error-strip`,
`reports-first-week-band`, `reports-add-child-band`,
`reports-chart-20day`, `reports-chart-20day-bars`, `reports-chart-tod`, `reports-chart-tod-bars`,
`tod-peak-insight`, `child-switcher-pill`, `child-switcher-dropdown`, `sidebar-child-selector` (sidebar),
`parent-home` (the `(parent)/index` placeholder), `sign-out-button`, `login-username`/`login-password`/`login-submit`.

> **DEF-P5-01 (open):** the report claims `daily-activity-card` is missing from the DOM (Tamagui `Stack`
> with `height="100%"` dropping `data-testid`). Source declares it. **Re-verify on a clean bundle** — see TC-26.
> **DEF-P5-02/DEF-P5-03** were stale-bundle / startup-locale artifacts, not code defects.

**Seed reality:** a freshly-registered parent + child has **zero activity** → every analytics endpoint
returns a **200 zero-state** (not 404). To exercise *populated* charts/insight you must generate attempts
(see "Seeding for populated data" at the end). The current spec SKIPs populated cases (TC-20/21) for this reason.

---

## 1. Conventions for this catalog

- **ID:** `P5-05-TC-NN` (stable; the legacy spec's `TC-NN` are reconciled in the mapping table at the end).
- **Status tag per case:** `[EXISTING]` already in `P5-05-parent-dashboard.spec.ts` · `[ENHANCE]` exists but
  the assertion is soft/weakened and should be tightened · `[NEW]` not covered — implement.
- **Auth/seed default:** register a parent + ≥1 child via API in `beforeAll` (hermetic per run), then log in
  through the real `/login?role=parent` UI flow. Cross-family cases seed a *second* parent+child.
- **Selectors:** `getByTestId` first → `getByRole`/`getByLabel` fallback. Never brittle class/text selectors.
- **Locale:** default app locale is **ar/RTL**. EN requires `localStorage['@learnexia/locale']='en'` set
  **before app init** then reload (DEF-P5-03 — setting it after startup does not re-apply `html[lang]`).

---

## 2. Auth / role routing

### P5-05-TC-01 — Parent login routes into the parent area `[EXISTING]`
- **Type:** auth-routing · **Priority:** P0 · **Traces:** AC "parent dashboard" / brief role guard.
- **Preconditions:** seeded parent + child.
- **Steps:** log in via `/login?role=parent` with seeded creds.
- **Expected:** URL is no longer `/login` or `role-select`; lands on a `(parent)` route
  (`/children` default, or `/overview`/`/reports`/`/settings`).

### P5-05-TC-02 — Unauthenticated parent routes redirect away `[EXISTING]`
- **Type:** auth-authz · **Priority:** P0 · **Traces:** brief "signed-out redirect".
- **Steps:** without logging in, navigate directly to `/overview`.
- **Expected:** not left on `/overview`; redirected to `/login` / role-select / root (the `useGroupGuard('(parent)')`
  renders nothing while resolving, then redirects).

### P5-05-TC-03 — Reports route also guarded when signed out `[NEW]`
- **Type:** auth-authz · **Priority:** P1 · **Traces:** brief role guard.
- **Steps:** signed out, navigate directly to `/reports`.
- **Expected:** redirected away from `/reports` (same guard as overview). *Gap: legacy spec only guards `/overview`.*

### P5-05-TC-04 — Product override: no teacher role / no self-register surface `[NEW]`
- **Type:** negative / product-override · **Priority:** P2 · **Traces:** CLAUDE product decisions.
- **Steps:** load `/login?role=parent`; inspect the role options reachable from the login/role-select screen.
- **Expected:** there is **no teacher** role option and **no student self-register** path; parent is the only
  self-service registration. (Documents the product invariant at the UI boundary.)

---

## 3. Overview page — panels render (zero-state, fresh child)

### P5-05-TC-05 — Overview root + KPI region render (zero-state) `[EXISTING]`
- **Type:** functional / state(empty) · **Priority:** P0 · **Traces:** AC "shows latest weekly report (KPIs)".
- **Steps:** login → `/overview`; wait for `overview-root`, then `overview-kpi-region`.
- **Expected:** both visible, non-zero height. KPI region shows 4 tiles (Time / XP / Lessons / Streak) with
  zero-state values, no raw i18n keys, no crash.

### P5-05-TC-06 — KPI deltas are ABSOLUTE, never a `%` `[NEW]`
- **Type:** functional / regression(GAP-8) · **Priority:** P1 · **Traces:** AC KPIs; brief `xpDelta` divergence.
- **Preconditions:** seed a child with attempts so `WeeklyKpisDto.xpDelta != 0` (see seeding note) **OR** intercept
  `…/WeeklyKpis` with a fulfilled 200 body carrying `{ xpEarned: 320, xpDelta: 120, lessonsDone: 8, lessonsDelta: 2,
  timeLearningMinutes: 95, timeLearningDeltaMinutes: 30, streakDays: 4, streakDelta: 1 }` (wire flag `successed:true`).
- **Steps:** login (EN) → `/overview`; read the XP tile delta text.
- **Expected:** XP delta reads "+120 XP this week" (contains `XP`, not `%`). The Overview KPI region text contains
  **no** `%` character in any delta line. Positive delta → success color. *This is the GAP-8 regression that the
  current spec does not assert.*

### P5-05-TC-07 — Daily-activity card renders (zero-state) `[EXISTING / ENHANCE]`
- **Type:** functional / state(empty) · **Priority:** P0 · **Traces:** AC "progress charts" / daily activity.
- **Steps:** login → `/overview`; locate `daily-activity-card` (fallback: Export-CSV button text / title).
- **Expected:** card visible, non-zero height; the daily `BarChart` (`daily-activity-chart`) is present; for an
  all-zero series the empty caption `parent.overview.dailyActivity.empty` text shows. *ENHANCE: tighten to assert
  the `daily-activity-chart` testID and 7 bars once DEF-P5-01 is confirmed fixed (TC-26).*

### P5-05-TC-08 — Subject mastery region renders 4 subjects, zero bars `[EXISTING / ENHANCE]`
- **Type:** functional / product-override · **Priority:** P0 · **Traces:** AC "weak areas/mastery"; 4-subjects rule.
- **Steps:** login → `/overview`; wait for `overview-mastery-region`.
- **Expected:** exactly 4 mastery rows in order Math → Science → Arabic → English, each at 0% in zero-state
  (rows **not hidden**); empty caption `parent.overview.subjectMastery.empty` shows. *ENHANCE: assert the row
  count is 4 and the order, not just region visibility.*

### P5-05-TC-09 — Overview "Areas to focus on" empty state `[NEW]`
- **Type:** state(empty) · **Priority:** P1 · **Traces:** AC "weak areas … empty list → graceful empty state".
- **Steps:** login → `/overview`; locate the focus-areas card within `overview-focus-recommendations-row`.
- **Expected:** with an empty `WeakAreas.areas` list, the card shows the empty text
  `parent.overview.focusAreas.empty` (not an error, not a crash, no skeleton stuck).

### P5-05-TC-10 — Overview "Recommendations" empty state `[NEW]`
- **Type:** state(empty) · **Priority:** P1 · **Traces:** AC recommendations empty state.
- **Steps:** login → `/overview`; locate `recommendations-card`.
- **Expected:** with empty `Recommendations.items`, shows `parent.overview.recommendations.empty`; no raw keys.

### P5-05-TC-11 — Family summary strip renders (My Children) `[NEW]`
- **Type:** functional · **Priority:** P2 · **Traces:** brief Family/Summary panel.
- **Steps:** login → `/children`; locate the family "this week" hero strip.
- **Expected:** the 4 stats (XP / lessons / best streak / badges) render with localized numerals; eyebrow +
  headline present; no raw i18n keys. *Note: per the hooks file, FamilySummary wiring is partially deferred —
  if the strip still consumes `FamilyTotalsStub`, assert it renders without crash and flag as a wiring gap.*

---

## 4. Reports page — panels & charts

### P5-05-TC-12 — Reports root, KPI row, both chart panels render `[EXISTING]`
- **Type:** functional · **Priority:** P0 · **Traces:** AC "progress charts" + reports view.
- **Steps:** login → `/reports`; wait for `reports-root`, `reports-kpi-region`, `reports-chart-20day`, `reports-chart-tod`.
- **Expected:** all four visible, non-zero height. *Drop the old `reports-chart-slot-*` fallbacks — those were the
  stale-bundle path and should no longer exist (DEF-P5-02 resolved).*

### P5-05-TC-13 — Reports mastery panel renders 4 subjects `[EXISTING / ENHANCE]`
- **Type:** functional · **Priority:** P1 · **Traces:** AC mastery in reports.
- **Steps:** login → `/reports`; wait for `reports-mastery-panel`.
- **Expected:** 4 subject rows (Math/Science/Arabic/English) with `percent` values (real, not all hard-zero when
  populated). *ENHANCE: when seeded with attempts, assert at least one row > 0% (resolves the G-1 "all value={0}" gap).*

### P5-05-TC-14 — Reports XP KPI tile shows honest "—" placeholder `[EXISTING]`
- **Type:** functional / known-gap(G-2) · **Priority:** P1 · **Traces:** brief G-2 (no parent-readable child XP).
- **Steps:** login (EN) → `/reports`; read `reports-kpi-region`.
- **Expected:** the XP tile value is `—` with sub-copy `parent.reports.kpi.xpComingSoon`. Region contains `—`.

### P5-05-TC-15 — 20-day chart: exactly 20 bars when populated `[NEW]`
- **Type:** boundary · **Priority:** P1 · **Traces:** AC charts; brief "`xpTrend20Day` exactly 20".
- **Preconditions:** intercept `…/Reports` 200 with `xpTrend20Day` = 20 entries (varied `xp`, one `day` == today),
  `dailyXpSeries` = 7, `timeOfDayBuckets` = 4.
- **Steps:** login → `/reports`; count bars under `reports-chart-20day-bars`.
- **Expected:** exactly **20** bars; the entry whose `day` equals today is the active bar; chart shows no value
  labels (Shape B). *Gap: legacy spec never asserts the 20-count or the active bar.*

### P5-05-TC-16 — 20-day chart zero-state caption, no error `[EXISTING]`
- **Type:** state(empty) · **Priority:** P1 · **Traces:** AC "empty/first-week … no chart errors".
- **Steps:** login → `/reports` (fresh child); inspect `reports-chart-20day`.
- **Expected:** panel non-zero height; **no** `reports-error-strip` inside it; when 20 zero entries, the
  `parent.reports.charts.noData` caption shows; no crash.

### P5-05-TC-17 — Time-of-day chart: 4 named buckets `[NEW]`
- **Type:** functional / regression(spec-divergence) · **Priority:** P1 · **Traces:** AC charts.
- **Preconditions:** intercept `…/Reports` 200 with `timeOfDayBuckets` = the 4 named buckets, varied `totalXp`.
- **Steps:** login → `/reports`; inspect `reports-chart-tod-bars`.
- **Expected:** exactly **4** bars labelled with the resolved bucket i18n labels
  (`parent.reports.charts.tod.{morning,afternoon,evening,night}`) — **not** 8 hourly bars, **not** raw `Morning`.
  *Gap: legacy spec only checks panel visibility; the design spec wrongly assumed 8 hours.*

### P5-05-TC-18 — Time-of-day peak insight tip (populated) `[NEW — was SKIP TC-21]`
- **Type:** functional · **Priority:** P2 · **Traces:** AC recommendations/insight.
- **Preconditions:** `…/Reports` with one bucket clearly highest `totalXp` (e.g. Afternoon=120, others <72).
- **Steps:** login → `/reports`; inspect `tod-peak-insight`.
- **Expected:** the insight tip renders, names the **peak bucket label** (e.g. "Afternoon"), via key
  `parent.reports.charts.peakBucketInsight`. *Replaces the SKIPPED TC-21; the real tip names a bucket, not "4–5pm".*

### P5-05-TC-19 — Time-of-day chart zero-state: no insight tip, caption shown `[NEW]`
- **Type:** state(empty) / boundary · **Priority:** P1 · **Traces:** AC empty states.
- **Steps:** login → `/reports` (fresh child / all `totalXp=0`).
- **Expected:** `reports-chart-tod` visible; `tod-peak-insight` **absent**; `parent.reports.charts.todEmpty`
  caption shows; no crash. *Boundary complement to TC-18.*

### P5-05-TC-20 — Reports first-week band renders (no prior attempts) `[EXISTING]`
- **Type:** state(empty) · **Priority:** P2 · **Traces:** AC "empty/first-week states".
- **Steps:** login → `/reports` (fresh child).
- **Expected:** `reports-first-week-band` shows non-empty copy; `reports-root` stays visible (no crash). If the
  attempts query hasn't settled, the band may be absent — assert no-crash either way.

### P5-05-TC-21 — Reports "add child" band when family has no children `[NEW]`
- **Type:** state(empty) · **Priority:** P2 · **Traces:** brief empty-family handling.
- **Preconditions:** seed a parent with **no** child.
- **Steps:** login → `/reports`.
- **Expected:** `reports-add-child-band` shows `parent.reports.empty.addChild` + an Add-Child CTA; no chart panels
  render; no crash. *Gap: current spec always seeds a child, never tests the no-child family.*

---

## 5. Populated charts (data-driven) — closes the SKIP gaps

### P5-05-TC-22 — Daily-activity bars reflect a populated series `[NEW — was SKIP TC-20]`
- **Type:** functional · **Priority:** P1 · **Traces:** AC progress charts (real data).
- **Preconditions:** intercept `…/Reports` 200 with `dailyXpSeries` = 7 entries, mixed `xp` (one day = today, xp>0).
- **Steps:** login → `/overview`; inspect `daily-activity-chart`.
- **Expected:** 7 bars; the today entry has the active styling; value labels show localized integers (Shape A,
  `showValueLabels`); zero days render as the 4px stub, not 0px. *Replaces SKIPPED TC-20 by mocking the series so
  the harness no longer depends on generating real attempts.*

### P5-05-TC-23 — Daily-activity Export CSV button present & accessible `[EXISTING]`
- **Type:** functional / a11y · **Priority:** P1 · **Traces:** AC "Export CSV".
- **Steps:** login → `/overview`; find the Export-CSV pill (testID `daily-activity-export-csv` if present, else
  role=button name "Export CSV" / "تصدير CSV", else any button containing "CSV").
- **Expected:** present and visible; `accessibilityRole=button`; min target ≥44px width. (Label keeps "CSV" Latin in AR.)

### P5-05-TC-24 — Daily-activity Export CSV downloads a valid file `[NEW / ENHANCE]`
- **Type:** functional · **Priority:** P1 · **Traces:** AC "Export CSV produces a real CSV".
- **Preconditions:** populated `dailyXpSeries` (intercept as TC-22).
- **Steps:** login → `/overview`; click Export CSV; capture the Playwright `download` event.
- **Expected:** a download fires with filename `daily-activity.csv`; the content's first line is the header
  (`Date,XP` EN / `التاريخ,النقاط` AR) and there are 7 data rows `day,xp`. *Gap: legacy spec only asserts the
  button exists, never that a CSV is produced.*

### P5-05-TC-25 — 20-day chart Export CSV downloads a valid file `[NEW / ENHANCE — was soft TC-23]`
- **Type:** functional · **Priority:** P2 · **Traces:** AC "Export CSV" (reports).
- **Preconditions:** populated `xpTrend20Day` (intercept as TC-15).
- **Steps:** login → `/reports`; click the 20-day panel Export-CSV pill; capture the download.
- **Expected:** filename `xp-trend-20day.csv`; header line + **20** data rows. *Replaces the soft-pass TC-23.*

### P5-05-TC-26 — `daily-activity-card` testID is emitted to the DOM (DEF-P5-01 regression) `[NEW]`
- **Type:** regression · **Priority:** P2 · **Traces:** DEF-P5-01.
- **Steps:** on a clean bundle, login → `/overview`; query `getByTestId('daily-activity-card')`.
- **Expected:** the element is attached to the DOM (no fallback needed). If still absent, file/keep DEF-P5-01 with
  the `height="100%"` Tamagui-Stack root cause. *Turns the current fallback into an explicit assertion.*

---

## 6. Multi-child / child switcher / data isolation

### P5-05-TC-27 — Child switcher visible in the parent shell `[EXISTING]`
- **Type:** functional · **Priority:** P0 · **Traces:** AC "parent with multiple children can switch".
- **Steps:** login → `/overview`; locate `child-switcher-pill` (fallback `sidebar-child-selector`).
- **Expected:** one is visible; shows the active child's name.

### P5-05-TC-28 — Child switcher opens its dropdown (multi-child) `[ENHANCE — was SKIP TC-10]`
- **Type:** functional · **Priority:** P1 · **Traces:** AC multi-child switching.
- **Preconditions:** seed a parent with **2** children (the legacy single-child seed collapses the dropdown → SKIP).
- **Steps:** login → `/overview`; click `child-switcher-pill`.
- **Expected:** `child-switcher-dropdown` (or a role=menu/listbox) opens, listing both children + the "Add child"
  footer; the active row is marked selected (✓). *Resolves the skipped TC-10 by seeding ≥2 children.*

### P5-05-TC-29 — Switching child re-fetches every panel for the new child only `[NEW]`
- **Type:** functional / data-isolation · **Priority:** P0 · **Traces:** AC "each shows only that child's data";
  "every panel re-fetches for the active child".
- **Preconditions:** parent with 2 children A & B; intercept `…/Children/{idA}/*` and `…/Children/{idB}/*` with
  distinct identifiable payloads (e.g. A: lessonsDone=8; B: lessonsDone=3).
- **Steps:** login → `/overview` (shows A's data); open switcher → select child B; observe the KPI/charts.
- **Expected:** after switch, every per-child query (`WeeklyKpis`, `Reports`, `SubjectMastery`, `WeakAreas`,
  `Recommendations`) is requested with **B's** id; the UI shows B's distinct values; **no A data lingers**. The
  query keys are childId-scoped, so each panel keys independently. *Critical gap — legacy spec never switches.*

### P5-05-TC-30 — Active child persists across Overview ↔ Reports navigation `[NEW]`
- **Type:** state · **Priority:** P2 · **Traces:** AC multi-child consistency (`activeChildStore`).
- **Steps:** parent with 2 children; select child B on `/overview`; navigate to `/reports`.
- **Expected:** Reports loads for child B (the store-held active child), not A / not children[0].

---

## 7. Cross-family / IDOR / authz

### P5-05-TC-31 — Cross-family child analytics returns 403 (API) `[EXISTING]`
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Traces:** AC "only a parent linked to that child"; brief
  "403 never leaks an IDOR oracle".
- **Preconditions:** seed parent1+childA and parent2+childB.
- **Steps:** with parent1's token, `GET …/Children/{childB}/WeeklyKpis`.
- **Expected:** HTTP **403**; envelope `successed:false`; `data` is null (no child data leaked). *Note: this is a
  pure-API assertion — keep it, and add the UI-surface complement below.*

### P5-05-TC-32 — IDOR also 403s across the other analytics endpoints `[NEW]`
- **Type:** auth-authz / IDOR · **Priority:** P1 · **Traces:** brief 403 path.
- **Steps:** with parent1's token, hit `…/Children/{childB}/{Reports,SubjectMastery,WeakAreas,Recommendations}`.
- **Expected:** every one returns **403**, `successed:false`, null data — and never **404** (404 would leak that the
  id exists vs not). *Broadens the single-endpoint IDOR check to the whole surface.*

### P5-05-TC-33 — UI surface of a 403 shows generic error, no oracle `[NEW]`
- **Type:** auth-authz / state(error) · **Priority:** P1 · **Traces:** brief "403 leaks no IDOR oracle".
- **Preconditions:** force the active-child id to childB while logged in as parent1 (intercept `…/Children/{childB}/*`
  → real 403 envelope, or drive via a crafted store state).
- **Steps:** load `/overview` in that state.
- **Expected:** panels render their generic error strip + Retry (the `ReportsWeb` ErrorStrip / per-card error),
  showing only generic copy (`parent.overview.loadError` / `parent.reports.loadError`) — never the child's name,
  id, or any data; no app crash.

---

## 8. Per-panel error + retry states

### P5-05-TC-34 — `…/WeeklyKpis` 500 → KPI region error + retry `[EXISTING / ENHANCE]`
- **Type:** state(error) · **Priority:** P0 · **Traces:** AC "error per panel → generic error strip with retry".
- **Steps:** intercept `…/WeeklyKpis` → 500; login → `/overview`.
- **Expected:** `overview-kpi-region` shows the error block (`parent.overview.loadError`) + a Retry button
  (`common.retry`). *ENHANCE: assert the Retry button is present specifically, not just non-empty text.*

### P5-05-TC-35 — Retry button refetches and recovers `[NEW]`
- **Type:** state(error→success) · **Priority:** P1 · **Traces:** AC retry.
- **Steps:** intercept `…/WeeklyKpis` → 500 once; login → `/overview` (error shows); change the intercept to a 200
  populated body; click Retry.
- **Expected:** the KPI region re-fetches and renders the 4 tiles with data; the error block disappears. *Gap: no
  test currently verifies retry actually recovers.*

### P5-05-TC-36 — `…/Reports` 500 → both chart panels show error strip + retry `[EXISTING / ENHANCE]`
- **Type:** state(error) · **Priority:** P1 · **Traces:** AC error per panel.
- **Steps:** intercept `…/Reports` → 500; login → `/reports`.
- **Expected:** `reports-chart-20day` and `reports-chart-tod` each render `reports-error-strip` + Retry; the KPI
  row (driven by `WeeklyKpis`, not `Reports`) still renders. *ENHANCE: drop the stale-bundle conditional; assert
  the error strips appear in both chart panels.*

### P5-05-TC-37 — `…/SubjectMastery` 500 → mastery card error + retry `[NEW]`
- **Type:** state(error) · **Priority:** P2 · **Traces:** AC error per panel.
- **Steps:** intercept `…/SubjectMastery` → 500; login → `/overview`.
- **Expected:** the Overview mastery card shows `parent.overview.loadError` + Retry (per-card isolation — other
  panels unaffected).

### P5-05-TC-38 — `…/WeakAreas` 500 → focus-areas card error + retry `[NEW]`
- **Type:** state(error) · **Priority:** P2 · **Traces:** AC error per panel.
- **Steps:** intercept `…/WeakAreas` → 500; login → `/overview`.
- **Expected:** FocusAreasCard shows error + Retry; distinct from its empty state (TC-09).

### P5-05-TC-39 — `…/Recommendations` 500 → recommendations card error + retry `[NEW]`
- **Type:** state(error) · **Priority:** P2 · **Traces:** AC error per panel.
- **Steps:** intercept `…/Recommendations` → 500; login → `/overview`.
- **Expected:** RecommendationsCard shows error + Retry; distinct from its empty state (TC-10).

### P5-05-TC-40 — Children list (`useMyChildren`) load error → Reports error strip `[NEW]`
- **Type:** state(error) · **Priority:** P2 · **Traces:** AC error handling.
- **Steps:** intercept the children-list endpoint → 500; login → `/reports`.
- **Expected:** `reports-error-strip` (top-level) shows with Retry; no chart panels attempt to render with an
  undefined child.

---

## 9. Loading skeletons

### P5-05-TC-41 — Overview KPI loading skeletons render before data `[NEW]`
- **Type:** state(loading) · **Priority:** P2 · **Traces:** AC progress; design §3.1 loading state.
- **Steps:** intercept `…/WeeklyKpis` with a delayed (~2s) 200; login → `/overview`; assert during the delay.
- **Expected:** 4 skeleton tiles (`$cardSoft`, height ~110) inside `overview-kpi-region` while loading; replaced by
  real tiles after the response. *Gap: no loading-state assertion exists.*

### P5-05-TC-42 — Reports page loading skeleton renders before data `[NEW]`
- **Type:** state(loading) · **Priority:** P2 · **Traces:** design §3.4 / `reports-loading`.
- **Steps:** delay the children/attempts queries; login → `/reports`; assert during the delay.
- **Expected:** `reports-loading` skeleton is visible during initial load; chart panels appear after settle.

---

## 10. RTL (Arabic) + LTR (English) / i18n

### P5-05-TC-43 — Overview renders in Arabic without raw i18n key leaks `[EXISTING]`
- **Type:** RTL-i18n · **Priority:** P0 · **Traces:** AC "renders in Arabic (RTL) and English".
- **Steps:** AR context; set `@learnexia/locale=ar`; login → `/overview`.
- **Expected:** `html[lang]` is `ar`/`ar-EG`; no dotted `parent.*`/`common.*` keys visible (esp. `.kpi.`, `.charts.`,
  `.overview.`, `.reports.`).

### P5-05-TC-44 — Reports renders in Arabic without raw i18n key leaks `[EXISTING]`
- **Type:** RTL-i18n · **Priority:** P0 · **Traces:** AC Arabic.
- **Steps:** AR; login → `/reports`.
- **Expected:** `reports-root` visible; no raw key leaks across KPIs, charts, mastery, focus, recommendations.

### P5-05-TC-45 — Overview renders in English without raw key leaks `[EXISTING / ENHANCE]`
- **Type:** RTL-i18n · **Priority:** P1 · **Traces:** AC English.
- **Steps:** set `@learnexia/locale=en` **before init**, reload; login → `/overview`.
- **Expected:** when EN propagates, `html[lang]='en'` and LTR; no raw key leaks. *ENHANCE: make the EN propagation
  deterministic (set storage on the root page before any app mount, then reload) so the assertion can be hard, not
  soft (DEF-P5-03).*

### P5-05-TC-46 — Charts stay LTR in Arabic (bar order + axis), value labels LTR-safe `[NEW]`
- **Type:** RTL-i18n · **Priority:** P1 · **Traces:** AC "charts direction-aware … value labels stay LTR".
- **Steps:** AR; login → `/overview` and `/reports`; inspect the bar containers.
- **Expected:** the BarChart bar layout container is `direction: ltr` (bars grow left→right even in AR); daily
  axis labels are Latin day abbreviations (`Mon…Sun`); 20-day axis labels are Latin 1..20; value labels are not
  reversed/garbled. *Gap: no chart-direction assertion exists.*

### P5-05-TC-47 — Arabic numerals in KPI values; "XP"/"CSV" stay Latin `[NEW]`
- **Type:** RTL-i18n · **Priority:** P2 · **Traces:** AC Eastern-Arabic numerals; design §4.
- **Preconditions:** populated `WeeklyKpis` (intercept as TC-06) under AR locale.
- **Steps:** AR; login → `/overview`; read a numeric KPI value and the XP delta and the Export-CSV label.
- **Expected:** numeric KPI values use Eastern-Arabic digits (`٠-٩`); the XP delta uses `نقطة` (not Latin "XP");
  the Export-CSV button label is `تصدير CSV` (CSV stays Latin). *Gap: numeral localization unverified.*

### P5-05-TC-48 — No "Social Studies" subject anywhere (4-subjects override) `[EXISTING]`
- **Type:** product-override / negative · **Priority:** P0 · **Traces:** CLAUDE "4 subjects, no Social Studies".
- **Steps:** EN; login → `/overview`; read `overview-mastery-region` text.
- **Expected:** text does not contain "social studies"; only Math/Science/Arabic/English appear.

---

## 11. Envelope / pagination normalization edge

### P5-05-TC-49 — `successed:false` 200 envelope is treated as an error `[NEW]`
- **Type:** negative / envelope · **Priority:** P1 · **Traces:** brief envelope shape; `apiClient.handleResponse`.
- **Steps:** intercept `…/WeeklyKpis` → **HTTP 200** but body `{ successed:false, data:null, message:"x" }`; login → `/overview`.
- **Expected:** the KPI region shows the **error** state (not a populated-with-nulls render) — the client honors the
  `successed` flag even on a 2xx. *Edge the existing spec never exercises.*

### P5-05-TC-50 — Children/attempts pagination envelope normalizes (flattened vs nested) `[NEW]`
- **Type:** persistence / envelope · **Priority:** P2 · **Traces:** brief "pagination metadata"; `getPaginated`
  dual-shape normalization (Identity flattened vs nested `data`).
- **Steps:** load `/reports` for a seeded parent (uses `useMyChildren` + `useStudentAttempts`, both paginated).
- **Expected:** the child list and attempts resolve correctly regardless of whether the backend returns the
  flattened `PaginatedResult` or `BaseResponse<PaginatedResult>` — the page renders the child and first-week/attempts
  state without error. *Documents the normalization seam at the UI level; if both shapes can't be exercised live,
  cover the live shape and note the normalization in `coverage-report.md`.*

---

## 12. Seeding for populated data (note for the implementer)

Several P1 cases (TC-06, TC-13, TC-15, TC-17, TC-18, TC-22, TC-24, TC-25, TC-29, TC-47) require **non-zero**
analytics. Two viable approaches — pick per harness ergonomics:

1. **Network interception (preferred for determinism):** `page.route('**/api/Parent/Children/*/Reports', …)` and
   `…/WeeklyKpis`, `…/SubjectMastery`, etc., fulfilling 200 with crafted `BaseResponse` bodies (`successed:true`).
   This isolates FE rendering from backend data variance and avoids the OQ-3.5 "silent zero degrade" attribution
   problem. Use the **real field names** from §0 (`day`/`xp`, `bucket`/`totalXp`, `subjectCode`/`percent`, `xpDelta`).
2. **Real attempts seeding:** register parent+child, complete quiz attempts via API so the analytics endpoints
   compute non-zero series. Heavier; subject to the OQ-3.5 caveat (a missing seam silently returns zeros).

For cross-child isolation (TC-29) and IDOR (TC-31/32), real seeding of **two** families is required (interception
alone can't prove server-side 403).
