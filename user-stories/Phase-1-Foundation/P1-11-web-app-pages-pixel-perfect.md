# Parent web app — all pages, pixel-perfect from design-system screenshots

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Frontend Foundation
- **Issue type:** Epic
- **Story Points:** 34 (sum of children) — split below; reslice during planning.
- **Labels:** `frontend`, `web`, `design-system`, `pixel-perfect`
- **Requirements:** FR-ID-1, FR-ID-2, NFR (localization/RTL), design-system fidelity

## Description
As a parent, I want the full Learnexia web experience — marketing landing, a redesigned login and register, my-children, a per-child dashboard, detailed reports, and settings — built **pixel-perfect to the design-system screenshots**, with the brand fonts, a language switch, and a dark-mode toggle, so that the product looks and feels exactly as designed on the web.

## Design source of truth (read before building)
- **Reference screenshots — `design-system/screenshots/web/`** (the pixel-perfect target). Every page below names its capture.
- **Tokens & fonts** — `design-system/colors_and_type.css` + `design-system/preview/*.html`. Typography roles:
  - Display/headings (en): `--lx-font-display` → **Poppins** (fallback Cairo).
  - Body (en): `--lx-font-body` → **Poppins** (fallback Tajawal).
  - Arabic display: `--lx-font-arabic-display` → **Cairo**; Arabic body: `--lx-font-arabic-body` → **Tajawal**.
  - All families are self-hosted in `design-system/fonts/` and must be wired into the Tamagui font config (no system-font fallback in the happy path).
- **Kit:** `design-system/ui_kits/parent-dashboard/` (web) — designer output target. Reuse `packages/ui` + `packages/design-system` tokens; do not invent a new look.

## Cross-cutting acceptance criteria (apply to every page)
- **Pixel-perfect:** each page matches its `design-system/screenshots/web/<page>.png` — layout, spacing, color tokens, radii, typography (family/size/weight/line-height), and component states. Deviations require an explicit design note.
- **Fonts:** Poppins (Latin) + Tajawal/Cairo (Arabic) load from the self-hosted files and apply per the type roles above; verified in both locales.
- **Language switch:** the UI supports **English (LTR)** and **Arabic (RTL)**; switching flips direction and swaps the Arabic font stack. Reachable from Settings → Language & region and from a header/login affordance.
- **Dark mode:** dark is the default (design is dark-first); a **theme switch is present on the Login page** and persists the choice.
- **Responsive:** laptop (1024+) is the screenshot target; pages stay usable at tablet (768) — sidebar collapses, two-column panels stack.

---

## Child stories

### P1-11a — Web app shell (fonts, theme switch, language switch, nav)
**Issue type:** Technical Enabler · **Points:** 5 — foundation the pages sit on (font wiring, theme provider + toggle, i18n/RTL, dashboard sidebar + child selector).
**Description:** As a developer, I want the web shell — brand fonts wired, a dark/light theme provider with a toggle, the i18n/RTL switch, and the left dashboard navigation (logo, child selector, My Children / Overview / Reports / Activity / Subjects / Settings) — so every page renders in the right look and locale.
**Acceptance Criteria:**
- Poppins + Tajawal + Cairo are registered in the Tamagui font config from `design-system/fonts/` and resolve per the type roles; no FOUT/system fallback in the happy path.
- A theme provider exposes dark (default) + light; the toggle persists across reloads.
- Language switch toggles en↔ar, flips `dir`, and swaps the Arabic font stack app-wide.
- The left sidebar matches `05-dashboard.png` (logo, child selector "Name · Grade · Level", nav items with the active state) and collapses at ≤768px.
**Labels:** `frontend`, `web`, `design-system`

### P1-11b — Landing (marketing) page
**Issue type:** Story · **Points:** 3 — static marketing page, content-heavy but low logic.
**Description:** As a prospective parent, I want a marketing landing page so that I understand the product and can start signing up.
**Acceptance Criteria:**
- Matches `screenshots/web/01-landing.png`: hero, features, subjects (Math/Science/Arabic/English), and CTA.
- Primary CTA routes to Register; secondary to Login.
- Renders correctly in en (LTR) and ar (RTL).
**Labels:** `frontend`, `web`, `marketing`

### P1-11c — Login page (redesigned) + dark-mode & language switch
**Issue type:** Story · **Points:** 5 — split layout, role toggle, social buttons, plus the theme + language affordances.
**Description:** As a parent or student, I want a redesigned split-layout login so that I can sign in, with a theme switch and a language switch on the page.
**Acceptance Criteria:**
- Matches `screenshots/web/02-login.png`: left brand panel (logo, glowing star, "Welcome back to the adventure", "240,000+ kids learning today"); right form ("Welcome back" / "Log in to keep your streak alive 🔥", **Parent/Student toggle**, Email, Password with Show, Remember me, Forgot password?, Log in, "OR CONTINUE WITH" Google/Apple/Microsoft, "New to Learnexia? Create parent account").
- A **dark-mode switch** is present on this page and toggles + persists the theme (addition beyond the screenshot).
- A **language switch** (en/ar) is present and flips direction + fonts (addition beyond the screenshot).
- Submits to the auth API; shows inline errors for invalid credentials / not-found (reuses P1-09 error handling).
**Labels:** `frontend`, `web`, `auth`
**Notes:** dark-mode toggle + language switch are new affordances not shown in the screenshot — design them in the same token language.

### P1-11d — Register page (redesigned)
**Issue type:** Story · **Points:** 3 — two-column form + benefits panel.
**Description:** As a parent, I want a redesigned two-column register page so that I can create a parent account.
**Acceptance Criteria:**
- Matches `screenshots/web/03-register.png`: two-column parent form + benefits panel, consent + password-strength meter.
- Parent-only registration (no student self-register); on success routes to add-child onboarding.
- Submits to the register API; handles duplicate-email / weak-password inline.
**Labels:** `frontend`, `web`, `auth`

### P1-11e — My Children (list, add, edit)
**Issue type:** Story · **Points:** 5 — family hero + child cards + add + **edit** child.
**Description:** As a parent, I want to see my linked children, add a child, and edit a child so that I can manage my family.
**Acceptance Criteria:**
- Matches `screenshots/web/04-my-children.png`: family hero + child cards (per-child stats) + add-child CTA.
- **Add child**: form for name, login email, grade, preferred language, country; creates the child (P1-03/P1-04).
- **Edit child**: opens an existing child for editing the same fields and saves changes.
- Renders in en (LTR) and ar (RTL).
**Labels:** `frontend`, `web`, `onboarding`

### P1-11f — Dashboard / Overview (per-child)
**Issue type:** Story · **Points:** 5 — KPI cards, daily-activity chart, subject mastery, weak areas.
**Description:** As a parent, I want a per-child overview so that I can see progress at a glance.
**Acceptance Criteria:**
- Matches `screenshots/web/05-dashboard.png`: header ("<Name>'s progress" + date range, "This week" selector, "Send Report"), 4 KPI cards (Time learning, XP earned, Lessons done, Day streak, each with vs-last-week delta), Daily activity bar chart (Mon–Sun, Export CSV), Subject mastery bars, "Areas to focus on" list.
- The child selector switches which child the page reflects.
- Renders in en (LTR) and ar (RTL).
**Labels:** `frontend`, `web`, `analytics`
**Notes:** data wiring depends on Phase-5 analytics; this story may render the layout against stubbed/seed data until P5 lands.

### P1-11g — Reports (detailed monthly)
**Issue type:** Story · **Points:** 5 — KPIs, 20-day chart, mastery, time-of-day.
**Description:** As a parent, I want a detailed monthly report so that I can review longer-term progress.
**Acceptance Criteria:**
- Matches `screenshots/web/06-reports.png`: KPIs, 20-day chart, subject mastery, time-of-day breakdown.
- Date-range selector and a "Send Report" action.
- Renders in en (LTR) and ar (RTL).
**Labels:** `frontend`, `web`, `analytics`
**Notes:** see P5-01 (weekly report) / P5-05 (parent dashboard); shares data sources.

### P1-11h — Settings (Profile + Language & region)
**Issue type:** Story · **Points:** 3 — Profile + Language tabs only; the tab bar shows all six but the other four route to P2-12.
**Description:** As a parent, I want a settings page where I can edit my profile and switch language/region so that my account basics and locale are under my control.
**Acceptance Criteria:**
- Matches `screenshots/web/07-settings.png`: the six-tab bar (Profile / Notifications / Linked children / Security / Plan & billing / Language & region) renders pixel-perfect; **Profile** and **Language & region** are functional.
- **Profile** form (avatar upload/remove, Full name, Email, Phone, Country, Cancel / Save changes) works.
- **Language & region** changes the app language (en/ar) + region; applies app-wide and persists.
- The other four tabs are out of scope here (see **P2-12**); selecting one shows a "coming soon" placeholder, not a broken view.
- Renders in en (LTR) and ar (RTL).
**Labels:** `frontend`, `web`, `settings`
**Notes:** Notifications / Linked children / Security / Plan & billing tabs → **P2-12** (full story, back + front).

---

## Reference screenshots
| Page | Screenshot | Story |
|---|---|---|
| Landing | `design-system/screenshots/web/01-landing.png` | P1-11b |
| Login | `design-system/screenshots/web/02-login.png` | P1-11c |
| Register | `design-system/screenshots/web/03-register.png` | P1-11d |
| My Children | `design-system/screenshots/web/04-my-children.png` | P1-11e |
| Dashboard | `design-system/screenshots/web/05-dashboard.png` | P1-11f |
| Reports | `design-system/screenshots/web/06-reports.png` | P1-11g |
| Settings | `design-system/screenshots/web/07-settings.png` | P1-11h |

## Notes
- **Pixel-perfect is the bar.** The `designer` agent must produce specs that match the screenshots exactly and cite the per-screen capture; the `frontend` agent builds to that fidelity (see the new designer rule in `.claude/agents/designer.md`).
- **Product overrides** (per `user-stories/README.md`): parent-driven onboarding (no student self-register), 4 subjects (no Social Studies — ignore the `06-subject-select` mobile capture's "Social"), no teacher role. The login `Parent/Student` toggle selects the *login* persona only; students still don't self-register.
- **Blocked by** P1-08 (design system), P1-09 (auth screens + error handling reused by Login/Register). Dashboard/Reports data depends on Phase 5 — layout-first against seed data is acceptable in Phase 1.
- **New affordances beyond the captures:** dark-mode switch on Login (P1-11c) and the explicit language switch (P1-11c, P1-11h).
- Mobile equivalents live in `design-system/screenshots/mobile/` (18 screens) and map to the existing mobile stories (P1-09, P2-09, P4-08, etc.) — out of scope for this web epic.
- **Scope trims (confirmed):** **Child Home** (`mobile/08-home.png`) is **deferred to P2-09** (home dashboard) — not built here. The four secondary **Settings** tabs (Notifications / Linked children / Security / Plan & billing) move to **P2-12** (new story, back + front); P1-11h keeps Profile + Language only.
