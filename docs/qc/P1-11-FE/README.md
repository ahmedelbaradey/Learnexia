# QC Test Plan & Coverage Report — P1-11-FE (Web app pages, pixel-perfect)

> **Scope:** student-app web PWA (Expo web) E2E only — frontend run. No backend test cases (per the lead's frontend-only directive).
> **Surfaces:** Splash, Login, Register, My Children (+ add/edit child), Dashboard/Overview, Settings, Landing.
> **Owner of this doc:** qc (test architect). **Implementer:** `frontend-e2e-tester` (Playwright, `tests/e2e/`).
> **Harness:** `tests/e2e/` — `chromium` + `mobile` (Pixel 7) projects, `baseURL` http://localhost:8081 (Playwright owns the Expo web server). Backend at `:5080` is a prerequisite for any data flow. Selector convention: `getByTestId` → `getByRole`/`getByLabel`.

---

## 1. Summary

P1-11-FE is the "canonical screen set" for the parent web experience. Per `docs/dev/HANDOFF.md`, **all seven pages are built** (Login, Register, My Children, Splash, Dashboard/Overview, Settings, Landing). Two parent surfaces are intentional placeholders that must NOT be asserted against as full features:

- **`(parent)/reports.tsx`** — a clean empty-state (title + "coming soon"), **not** the full KPIs/charts Reports build. Cases for the full Reports page are **BLOCKED (placeholder)**.
- **`(parent)/index.tsx`** (parent dashboard landing) — a branded "coming soon" card with sign-out + a link to My-Children; the **per-child charts** (daily-activity / 20-day / time-of-day) are deferred to **P5-05-FE**. The chart-bearing Overview page (`overview.tsx`) is built **chart-less** (the daily-activity chart is a placeholder).

Mock data shown in the captures ("Reading"/"Art" subjects) is **not** built — the app uses the 4 product subjects (Math/Science/Arabic/English). No case asserts mock data.

**Counts**
- **Total cases:** 48 — all `frontend-e2e-tester`.
- **By surface:** Splash 4 · Login 9 · Register 7 · My Children + add/edit 8 · Dashboard/Overview 5 · Settings 7 · Landing 5 · Cross-cutting (routing/RTL/responsive/a11y) 3.
- **By priority:** P0 = 17 · P1 = 21 · P2 = 10.
- **Status flags:** 6 cases marked **BLOCKED** (placeholders / harness gaps) — they are listed, not dropped, each with the blocker reason.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria are drawn from the story's per-child stories (P1-11a…h) + cross-cutting criteria.

| Story / criterion | Case ID(s) | Notes |
|---|---|---|
| **P1-11a shell** — sidebar matches capture (logo, child selector, nav, active state), collapses ≤768 | FE-TC-25, FE-TC-26, FE-TC-46, FE-TC-47 | Sidebar only renders ≥768; nav active-state + child selector covered |
| **P1-11a shell** — theme provider dark default + toggle persists | FE-TC-06, FE-TC-07 | Login theme switch + reload persistence |
| **P1-11a shell** — language switch en↔ar flips dir + fonts app-wide | FE-TC-05, FE-TC-37, FE-TC-45 | Login switch + Settings Language tab + app-wide RTL |
| **P1-11b Landing** — matches capture (hero/features/subjects/CTA) | FE-TC-40, FE-TC-41 | BLOCKED if harness can't reach the Next.js site (see §4) |
| **P1-11b Landing** — primary CTA → Register, secondary → Login | FE-TC-42 | env-driven URLs; BLOCKED-conditional |
| **P1-11b Landing** — 4 subjects (no Social Studies) | FE-TC-43 | negative: asserts no Social Studies tile |
| **P1-11b Landing** — en LTR only (RTL scoped out) | FE-TC-44 | asserts LTR; no RTL case for Landing |
| **P1-11c Login** — split panel, persona toggle, email/pwd, remember, forgot, social, footer | FE-TC-02, FE-TC-03, FE-TC-08, FE-TC-09 | |
| **P1-11c Login** — dark-mode switch present + persists | FE-TC-06, FE-TC-07 | |
| **P1-11c Login** — language switch present + flips dir/fonts | FE-TC-05 | |
| **P1-11c Login** — submits to auth API; inline errors invalid/not-found | FE-TC-04, FE-TC-10 | requires backend `:5080` |
| **P1-11d Register** — two-column form + benefits panel + consent + strength meter | FE-TC-11, FE-TC-12, FE-TC-13 | |
| **P1-11d Register** — parent-only (no student self-register); on success → add-child | FE-TC-14, FE-TC-16 | FE-TC-16 negative (no self-register route) |
| **P1-11d Register** — duplicate-email / weak-password inline | FE-TC-15 | requires backend |
| **P1-11e My Children** — family hero + child cards + add CTA | FE-TC-27, FE-TC-28 | stats are P5 stubs — don't assert values |
| **P1-11e My Children** — Add child creates child | FE-TC-30 | routes to add-child form (P1-04 flow) |
| **P1-11e My Children** — Edit child opens + saves | FE-TC-31 | EditChildSheet via card pencil affordance |
| **P1-11e My Children** — en LTR / ar RTL | FE-TC-45, FE-TC-29 | |
| **P1-11f Dashboard** — header + 4 KPI cards + mastery bars + focus areas | FE-TC-33, FE-TC-34 | daily-activity chart = placeholder (FE-TC-35) |
| **P1-11f Dashboard** — daily-activity chart | FE-TC-35 | **BLOCKED (placeholder → P5-05-FE)** |
| **P1-11f Dashboard** — child selector switches child | FE-TC-36 | only first child wired; documented limitation |
| **P1-11f Dashboard** — 4 product subjects (no mock Reading/Art) | FE-TC-34 | negative on mock subjects |
| **P1-11g Reports** — full KPIs/charts/time-of-day | FE-TC-48 | **BLOCKED (placeholder)** — only empty-state renderable |
| **P1-11g Reports** — empty-state renders (not broken) | FE-TC-32 | the *built* surface |
| **P1-11h Settings** — six-tab bar pixel-perfect; Profile + Language functional | FE-TC-37, FE-TC-38 | |
| **P1-11h Settings** — Profile form (name/email/phone/country/save) | FE-TC-38 | requires backend `/Me` + profile |
| **P1-11h Settings** — Language tab switches app-wide + persists | FE-TC-37 | |
| **P1-11h Settings** — 4 secondary tabs show "coming soon" not broken | FE-TC-39 | |
| **Cross-cutting** — auth/role routing (signed-out → login; parent home) | FE-TC-01, FE-TC-17 | |
| **Cross-cutting** — responsive 390/768/1024 | FE-TC-46, FE-TC-47 | sidebar collapse + form layout |
| **Cross-cutting** — a11y (roles/labels) | FE-TC-09, FE-TC-26 | role=header/link/menuitem checks |

**Gap verdict: every acceptance criterion is covered by at least one P0/P1 case.** The only criteria with **no executable (green) case** are the two deliberate placeholders (full Reports FE-TC-48; daily-activity chart FE-TC-35) and the Landing cases **if** the harness cannot reach the separate Next.js dev server — all are flagged BLOCKED, not gaps.

---

## 3. Risk notes (where the cases are weighted)

1. **Auth + routing guard (`useAuthRoute` on Splash)** — the splash is the single mount point that resolves role and `router.replace`s. Highest blast radius: a regression here strands every user on splash or routes a parent to a child surface. Weighted P0 (FE-TC-01, FE-TC-17).
2. **RTL-default locale.** Arabic is the **default** locale; the whole app boots RTL. Selector strategy must be `testID`/role/label, never visible Arabic copy. RTL flip correctness (dir attribute, fonts, logical layout) is weighted across Login/Register/My Children/Settings (FE-TC-05, 29, 45). The brand wordmark + email/phone fields are forced LTR even in RTL — explicit assertions (FE-TC-18).
3. **`BaseResponse` + i18n error surfacing.** Invalid-credentials must show a **generic** banner (anti-enumeration; no field-level reveal), duplicate-email/weak-password map to localized copy — these are the user-visible contract with the backend (FE-TC-04, 10, 15). Must render i18n text, not raw keys.
4. **Placeholder vs built confusion.** The biggest authoring risk is asserting against `(parent)/index` charts or `reports.tsx` as if they were full. Explicitly fenced: FE-TC-35 and FE-TC-48 are BLOCKED; FE-TC-32 asserts only the empty-state.
5. **Missing testIDs on auth + parent screens.** Login/Register/parent screens currently expose `accessibilityLabel`/`accessibilityRole` (i18n-keyed) but **almost no `testID`** (only `(child)` screens carry testIDs). This forces role/label selectors which, with an Arabic default, are i18n-coupled. See open questions — the tester should request stable testIDs rather than reach into CSS or hard-code Arabic strings.
6. **Landing is a separate stack/server.** Next.js `apps/marketing-site` is NOT served by the Expo `:8081` web server the harness owns; it links to the app via `NEXT_PUBLIC_APP_URL`. The Landing cases are conditionally BLOCKED on harness reachability (see §4 + open questions).

---

## 4. Open questions / assumptions (lead must resolve before implementation)

1. **Landing harness reach (BLOCKER for FE-TC-40..44).** The Playwright config only owns the Expo web server at `:8081`. The Next.js marketing site runs on its own server (default `:3000`). Does the lead want (a) a second `webServer`/project added to `playwright.config.ts` for the marketing site, (b) the Landing cases run against a separately-started Next dev server, or (c) the Landing cases deferred? Until resolved, FE-TC-40..44 are **BLOCKED (harness gap)**.
2. **Missing `testID`s.** Should `frontend-e2e-tester` file a `frontend` ticket to add stable `testID`s to: persona toggle, login/register submit buttons, locale + theme switches, sidebar nav items, settings tabs, child cards + edit pencil, KPI tiles? Recommendation: yes — selectors are currently role/label-only and Arabic-default makes copy selectors fragile. The tester should use role+`accessibilityLabel` (i18n key resolved) in the interim and report the needed testIDs.
3. **Backend availability for auth/profile flows.** FE-TC-04, 10, 15, 38 require a running backend at `:5080` with seedable parent/children. Assumption: the tester seeds a parent via the register/auth API (P1-09) and children via P1-03/P1-04. Confirm seed credentials / whether a fixture parent exists.
4. **Google OAuth env (`EXPO_PUBLIC_GOOGLE_CLIENT_ID`).** When unset, the Google button disables gracefully (no crash). FE-TC-19 asserts the *disabled* state — confirm the test env leaves it unset (recommended) so we test the graceful-degrade path, not a live OAuth round-trip (which is out of scope for E2E).
5. **Child-selector wiring on Dashboard/Overview.** `OverviewWeb` currently always reflects `children[0]` (the sidebar selector routes to My Children rather than re-scoping the page). FE-TC-36 documents this as a **known limitation**, not a defect — confirm that's the intended Phase-1 behavior before the tester files it as a bug.
6. **Theme persistence mechanism.** FE-TC-07 asserts the dark/light choice survives a reload. Confirm persistence is to web storage (localStorage) and not memory-only, so a Playwright `page.reload()` is a valid assertion.

---

## 5. Handoff

- **`frontend-test-cases.md`** → `frontend-e2e-tester`: implement each `FE-TC-*` as one Playwright test (1:1). Honor the BLOCKED markers — scaffold those as `test.skip` with the blocker reason in the title, do not assert against placeholders.
- **`execution-report.md`** → `frontend-e2e-tester` (and `frontend` for any bug it finds): the empty templated results table is in that file. The tester fills **pass/fail per case + defects** after running; qc does **not** fill results.
- Results feed the `reviewer` gate per the CLAUDE.md pipeline (`frontend` → `frontend-e2e-tester` → `reviewer`).
