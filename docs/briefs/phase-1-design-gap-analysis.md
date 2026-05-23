# Pipeline Brief — Phase 1 Design Gap Analysis

**Date:** 2026-05-23 · **Author:** analyzer agent · **Type:** read-only gap analysis (no code/migrations)

## ✅ Resolution (product-owner decisions, 2026-05-23)
- **All new backend → P1-12 "Phase 1 Batch 2" (deferred):** profile read/update + enriched `/Me`, avatar upload, OAuth (Google/Apple/Microsoft), password reset. Identity-module-scoped to stay **parallel-safe** with the Phase 2 BE lead. → `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md` + `tasks/Backend/Phase-1-Foundation/P1-12-BE.md`.
- **OAuth + Forgot-password:** UI-first in P1-11 (pixel-perfect placeholders/disabled), wired when P1-12 merges.
- **Avatar:** initials/placeholder in P1-11; real upload in P1-12b.
- **Subjects:** use the 4 product subjects (Math/Science/Arabic/English) — the captures' "Reading"/"Art" are mock data.
- **FE shared primitives** (CheckboxField, Tabs, Avatar, Switch, Sidebar, KPIStatCard, PasswordStrengthMeter) → **P1-11-FE-14** (not deferred — FE proceeds now).
- **Deferred-as-correct** unchanged: Child Home→P2-09, dashboard/report data→Phase 5, secondary settings→P2-12.

## Summary & traceability
- **Task:** Compare the design system (web + mobile captures, tokens, the P1-11 pixel audit) against what Phase 1 stories + FE/BE tasks actually cover, and report the gaps.
- **Design ("should exist"):** `design-system/screenshots/web/` (7 pages), `design-system/screenshots/mobile/` (18 screens), `design-system/screenshots/README.md`, tokens (`colors_and_type.css`, `preview/*.html`, `ui_kits/`), and the prior audit `design-system/ui_kits/parent-dashboard/P1-11-pixel-audit.md`.
- **Coverage ("what we planned"):** stories `user-stories/Phase-1-Foundation/P1-01…P1-11` + `P2-12`; tasks under `tasks/Frontend/student-app/Phase-1-Foundation/`, `tasks/Frontend/packages/`, `tasks/Frontend/admin-dashboard/Phase-1-Foundation/`, `tasks/Backend/Phase-1-Foundation/`; built code in `apps/student-app/app/**` and `packages/ui/src/components/*`.
- **Product overrides applied:** parent-driven onboarding (no student self-register; login Parent/Student is login-persona only); 4 subjects (Math/Science/Arabic/English — no Social Studies); no teacher role; grade transition preserves history.

## Method note (what changed since the audit)
The `P1-11-pixel-audit.md` is dated the same day but predates several commits. The built code now **already contains** `PersonaToggle.tsx`, `LoginBrandPanel.tsx`, `LocaleThemeControls.tsx`, and a `ProgressSteps` component — so several audit "BLOCKER/MISSING" items (persona toggle, web split panel, step indicator) are now **partially built**. This analysis focuses on **story/task coverage gaps**, not the audit's per-pixel deltas: a gap here means a screen/feature/component/endpoint the design needs that **no Phase 1 story or task plans**, or that is **under-specified** in the planned tasks.

---

## Summary table (gap · stack · suggested home · priority)

| # | Gap | FE/BE | Suggested home | Priority |
|---|---|---|---|---|
| G1 | **Profile read + update** (name, email, phone, country) backend for Settings → Profile | BE | New: **P1-11-BE** (or extend P1-09-BE) | **Blocker** |
| G2 | **Avatar upload + remove** (file storage + URL) backend | BE | New: **P1-11-BE** | **Blocker** |
| G3 | `Me` query exposes only role/onboarding/language — Profile + Settings need name/phone/country/avatar fields too | BE | Extend P1-09-BE-1 / P1-11-BE | **Blocker** |
| G4 | `CheckboxField` shared component (register Terms consent, Login "Remember me") | FE | **P1-08-FE** (packages/ui) | **Blocker** |
| G5 | `Switch` / toggle control (theme toggle, notification prefs later) | FE | **P1-08-FE** (packages/ui) | Should |
| G6 | `Tabs` / segmented tab bar (Settings 6-tab bar) | FE | **P1-08-FE** + P1-11-FE-10 | **Blocker** |
| G7 | `Avatar` + upload component (Settings profile, child cards, dashboard) | FE | **P1-08-FE** (packages/ui) | **Blocker** |
| G8 | `Sidebar` nav component (parent dashboard shell) — task assumes it, no component story | FE | **P1-08-FE** / P1-11-FE-3 | Should |
| G9 | `KPI stat card` component (dashboard/reports 4-up cards w/ delta) | FE | P1-11-FE-8/9 (or P1-08-FE) | Should |
| G10 | `Chart` primitives (bar chart, horizontal mastery bar) | FE | Planned **P1-11-FE-2** — verify it covers all 3 chart types | Should |
| G11 | `Select`/dropdown for Country + "This week" period selector | FE | `Select` exists — verify country-flag + period variants | Should |
| G12 | `PasswordStrengthMeter` (register, per story P1-11d) | FE | P1-11-FE-5 (under-specified) | Should |
| G13 | `Social-auth button` (Google/Apple/Microsoft) + **no backend OAuth** | FE+BE | P1-11-FE-4 (FE present) / **open question** (BE) | Should |
| G14 | `Forgot password` link + screen + **no backend reset flow** | FE+BE | **New story** (P1-xx) — not planned anywhere | Should |
| G15 | Theme toggle **persistence** (planned) + light-mode pixel parity unverified | FE | P1-11-FE-1 — verify persistence + light captures | Should |
| G16 | RTL coverage for new web pages (dashboard/reports/settings) | FE | P1-11-FE-13 QA — ensure charts/sidebar flip | Should |
| G17 | Dashboard/Reports **analytics data** (KPIs, daily activity, mastery, weak areas, time-of-day) | BE | **Phase 5** (P5-01/P5-05) — layout-first now | Later-phase (correctly deferred) |
| G18 | Design captures show subjects **Reading + Art** (dashboard/reports) — not in 4-subject product | — | **Design-vs-product mismatch** — use Arabic/English, not Reading/Art | Should (flag) |
| G19 | `mobile/04-role-select` shows **Teacher** option | — | **Design-vs-product mismatch** — no teacher role; do not build | N/A (flag) |
| G20 | Child Home (`mobile/08-home`): HUD, subject grid, missions, bottom tab bar | FE | **P2-09** (home dashboard) | Later-phase (correctly deferred) |
| G21 | Mobile profile (`mobile/10-profile`): avatar hero, stats grid, recent badges | FE | **P5-05 / P4-08** (gamification + parent) | Later-phase |
| G22 | Mobile gamification screens 12–18 (quiz/reward/mission/league/badges/hearts) | FE+BE | **Phase 2 (quiz) / Phase 3 gamification** | Later-phase |
| G23 | Skill tree + lesson (`mobile/09`, `11`) | FE+BE | **Phase 2** (P2-03/P2-05) | Later-phase |
| G24 | Grade/Subject selectors (`mobile/05`, `06`) | FE | **P2** (subject browse) / parent add-child (grade) | Mixed |
| G25 | "Send Report" action (dashboard/reports/settings header) | FE+BE | **Phase 5** (P5-04 deliver reports) | Later-phase |
| G26 | Bottom tab bar (child app) shared component | FE | **P2-09** (child shell) | Later-phase |

---

## Frontend gaps (detail)

### Blockers for P1 launch
- **G4 — `CheckboxField`** (`packages/ui`). Needed by Register Terms-consent (web `03-register.png`, mandatory for parent consent / COPPA) and Login "Remember me" (`02-login.png`). No P1-08 task lists Checkbox; the audit flagged it as B-06. **Home: add to P1-08-FE-4 (core components).**
- **G6 — `Tabs` component** for Settings 6-tab bar (`07-settings.png`). P1-11-FE-10 says "full six-tab bar pixel-perfect" but there is no shared tab component in `packages/ui` and no P1-08 task for one. **Home: P1-08-FE-4 + consumed by P1-11-FE-10.**
- **G7 — `Avatar` (+ upload trigger) component**. Settings Profile shows a large avatar with Upload/Remove; child cards and dashboard show avatars. `packages/ui` has no `Avatar`. **Home: P1-08-FE.**

### Should
- **G5 — `Switch`** (theme toggle is implemented ad-hoc via `LocaleThemeControls`; notification toggles arrive in P2-12). No shared `Switch` in `packages/ui`. Home: P1-08-FE.
- **G8 — `Sidebar` nav**: P1-11-FE-3 assumes a `Sidebar` in `packages/ui` but P1-08 never specs one. Either build under P1-11-FE-3 or add to P1-08. Confirm ownership.
- **G9 — `KPI stat card`**: dashboard (`05`) + reports (`06`) use a repeating "label / big number / vs-last-week delta" card. Not a named component anywhere. Suggest extracting under P1-11-FE-8/9 or P1-08-FE.
- **G10 — Charts**: P1-11-FE-2 plans `BarChart` + horizontal mastery bar. **Verify it also covers the reports "time-of-day" chart and the 20-day chart** (3 chart shapes, not 2). Flagged as the highest-uncertainty task — keep it.
- **G11 — `Select` variants**: `Select` exists, but Country (with flag) and the "This week" period selector are distinct uses. Verify P1-11-FE covers both.
- **G12 — `PasswordStrengthMeter`**: story P1-11d explicitly requires a "password-strength meter" on register; the FE task (P1-11-FE-5) only says "consent + password-strength meter" without a component. Under-specified — confirm it's built, not just validation text.
- **G15 — Theme persistence + light parity**: P1-11-FE-1 plans toggle+persistence; ensure (a) persistence survives reload and (b) every page is pixel-checked in **light** mode (captures are dark-only) — currently only dark is the screenshot target.
- **G16 — RTL for new web pages**: P1-11-FE-13 QA must explicitly cover sidebar flip, chart axis/label flip, and KPI delta direction in Arabic. Charts + sidebar are new surfaces the existing RTL infra has never been tested against.
- **G13 — Social-auth buttons**: FE buttons are partially built (`loginParts.tsx`). They must render in **disabled / coming-soon** state because there is **no backend OAuth** (see BE gaps). Confirm with planner.
- **G14 — Forgot-password**: Login captures (web + mobile) show "Forgot password?"; there is **no story, no FE screen, no BE reset flow** anywhere in P1. Genuinely missing — recommend a new story or explicit deferral.

---

## Backend gaps (detail)

### Blockers for P1 launch (Settings → Profile is in-scope per P1-11h)
- **G1 — Profile read + update endpoint.** Settings → Profile (`07-settings.png`, P1-11h, **explicitly in P1 scope**) edits Full name, Email, Phone, Country with Save. **No Phase 1 BE task creates an UpdateProfile command/endpoint.** Phone is not even a `User` field today (P1-03-BE-1 adds Grade/Age/LanguagePreference/Country, not Phone). **Home: new `P1-11-BE` (Profile module/feature), or extend P1-09-BE.**
- **G2 — Avatar upload + remove.** Profile Upload/Remove photo implies file storage (object store/local), an upload endpoint, and an avatar URL on `User`. **No P1 BE task, no file-upload infra planned.** Security-sensitive (file upload). **Home: new P1-11-BE; flag for `security-auditor`.**
- **G3 — `Me` payload too thin.** `GET /Me` (P1-09-BE-1) returns role + onboarding flag + language only. Settings Profile + the dashboard header (`Name · Grade · Level`, "this is how Learnexia knows you") need fullName, email, phone, country, avatarUrl. Either extend `Me` or add a dedicated `GetProfile` query. **Home: extend P1-09-BE-1 or new P1-11-BE.**

### Should / open
- **G13 (BE) — OAuth.** No Google/Apple/Microsoft OAuth endpoints in scope. Either descope the buttons (disabled) or write an OAuth story. Open question for the user.
- **G14 (BE) — Password reset.** "Forgot password?" needs request-reset + confirm-reset endpoints + email. Not planned. Open question.
- **G11 (BE) — Language/region persistence.** Settings Language & region "applies app-wide and persists". `LanguagePreference` exists on `User` (P1-03-BE-1) for **children**; confirm a parent's own preference is persisted and updatable (ties into G1 UpdateProfile).

### Correctly deferred (do NOT flag as P1 gaps)
- **G17 / G25 — Dashboard, Reports, "Send Report" data**: KPIs, daily-activity, subject-mastery, weak-areas, time-of-day, report delivery → **Phase 5** (P5-01 weekly report, P5-02 weak areas, P5-04 deliver, P5-05 parent dashboard). P1-11 stories explicitly say "layout-first against seed/stub data". Covered by Phase 5.
- Notification prefs / Linked-children / Security (sessions list, password change) / Plan & billing tabs → **P2-12** (back + front). Covered.

---

## Shared-component gaps (`packages/ui` — what the captures need and the lib lacks)

| Component | Needed by | In `packages/ui`? | Home | Priority |
|---|---|---|---|---|
| `CheckboxField` | Register consent, Login remember-me | No | P1-08-FE | Blocker |
| `Tabs` / segmented tab bar | Settings 6-tab | No | P1-08-FE | Blocker |
| `Avatar` (+ upload) | Settings, child cards, dashboard | No | P1-08-FE | Blocker |
| `Switch` | theme toggle, notif prefs | No | P1-08-FE | Should |
| `Sidebar` nav | parent dashboard shell | No (assumed by P1-11-FE-3) | P1-08-FE / P1-11-FE-3 | Should |
| `KPI stat card` | dashboard, reports | No | P1-11-FE-8/9 | Should |
| `BarChart` + mastery bar + time-of-day | dashboard, reports | No (planned P1-11-FE-2) | P1-11-FE-2 | Should |
| `PasswordStrengthMeter` | register | No | P1-11-FE-5 | Should |
| `PersonaToggle` | login | **Yes** (in app, not packages/ui) | — | Done (consider promoting to packages/ui) |
| `ProgressSteps` / step indicator | register/onboarding | **Yes** | — | Done |
| `Progress bar` (splash, mastery) | splash, mastery bars | Partial (`XPBar`) | verify reuse | Minor |
| `FAB` (+ add child, mobile) | my-children mobile | No | P1-11 mobile is deferred; needed for mobile P1-09 | Minor |
| `Bottom tab bar` | child app | No | **P2-09** | Later |
| `Social-auth button` | login | Partial (`loginParts.tsx`) | P1-11-FE-4 | Should |

Note: several "existing" components (`PersonaToggle`, `LoginBrandPanel`, `LocaleThemeControls`) live in `apps/student-app/app/(auth)/_components/` rather than `packages/ui`. If the Next.js marketing/admin apps need them, they should be promoted to `packages/ui` — flag for planner, not a build blocker.

---

## Cross-cutting gaps
- **Theme light/dark toggle persistence** — planned (P1-11-FE-1); verify it persists across reload and that **light mode** is pixel-checked (captures are dark-only; no light reference exists — possible design gap).
- **Language switch surfaces** — planned on Login (P1-11-FE-4) and Settings Language tab (P1-11-FE-10). Infra exists. Verify the affordance is reachable on web shell header too.
- **RTL coverage** — existing infra is proven on auth/onboarding only. New web surfaces (sidebar, charts, KPI cards, tabbed settings) are untested in RTL. Must be in the P1-11-FE-13 QA gate.
- **Avatar / file upload** — entirely unplanned end-to-end (FE component G7 + BE storage G2). Security-sensitive.
- **Social auth + forgot password** — UI present/expected, **no backend**. Must be descoped-to-disabled or get a new story.

---

## Open questions / assumptions / risks (for the lead to put to the user)
1. **Settings → Profile backend (G1/G2/G3):** P1-11h is in P1 scope but no BE task exists for profile read/update or avatar upload. Add a **P1-11-BE** story, or descope Profile editing to read-only for P1? (Recommend adding P1-11-BE — Profile + avatar; it's a Blocker as written.)
2. **Phone field:** Profile shows a Phone field; `User` has no Phone column planned. Add to the migration (extends P1-03-BE-1 scope) — confirm.
3. **Social auth (G13):** No OAuth backend. Render Google/Apple/Microsoft as disabled/coming-soon, or write an OAuth story now?
4. **Forgot password (G14):** No screen and no reset flow anywhere. New P1 story, or remove the link for P1?
5. **Subject mismatch (G18):** dashboard/reports captures label subjects "Reading" and "Art". Product = Math/Science/Arabic/English. Confirm we substitute Arabic/English (the captures are illustrative) — do **not** build Reading/Art.
6. **Teacher role (G19):** `mobile/04-role-select` shows a Teacher option. Per product override there is no teacher role — this is a **design-vs-product mismatch**, not a gap to build. Confirm the role-select screen (if ever built) drops Teacher.
7. **Light-mode reference:** captures are dark-only but the product ships a light theme. Is there a light-mode design reference, or is light "best-effort from tokens"?
8. **Sidebar/KPI/Tabs ownership:** do these live in `packages/ui` (reusable for admin/marketing) or stay app-local? Affects P1-08 vs P1-11 task placement.

---

## Correctly deferred to later phases (NOT P1 gaps)
- **Child Home** (`mobile/08-home`): HUD, subject grid, daily quests, bottom tab bar → **P2-09**. Explicitly trimmed from P1-11.
- **Dashboard / Reports analytics data + "Send Report"** (`web/05`, `06`): KPIs, charts, weak areas, report delivery → **Phase 5** (P5-01/02/04/05). Layout-first in P1 is the agreed approach.
- **Secondary Settings tabs** (Notifications / Linked children / Security / Plan & billing) → **P2-12** (back + front).
- **Mobile profile** (`mobile/10`): stats grid, recent badges, level progress → **P5-05 / P4-08** (parent + gamification).
- **Gamification screens** (`mobile/13` reward, `14`/`15` missions, `16` league, `17` badges, `18` hearts) → **Phase 3 Gamification** (P4-xx). The `mobile/12` quiz → **Phase 2** (P2-06).
- **Skill tree + lesson** (`mobile/09`, `11`): → **Phase 2** (P2-03 skill tree, P2-05 lesson).
- **Grade/Subject selectors** (`mobile/05`, `06`): grade selection is folded into parent add-child (P1-03); subject browse → **Phase 2** (P2-02).

---

## Recommended pipeline order (first cut — planner finalizes)
1. **Resolve open questions 1–4 first** (Profile BE scope, OAuth, forgot-password) — they decide whether new stories enter P1.
2. **db-migration** — if P1-11-BE is approved: add `Phone` + `AvatarUrl` to `User`.
3. **backend-feature (new P1-11-BE)** — GetProfile/UpdateProfile + avatar upload → **security-auditor** (file upload) → **api-tester**.
4. **frontend (P1-08-FE additions)** — `CheckboxField`, `Tabs`, `Avatar`, `Switch` shared components (unblock register consent + settings).
5. **frontend (P1-11-FE)** — sidebar/KPI/chart/settings pages consume the above; charts (FE-2) is the long pole.
6. **reviewer gate** per batch against this brief's gap list + the pixel audit.

## Relevant file paths (absolute)
- Audit: `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/design-system/ui_kits/parent-dashboard/P1-11-pixel-audit.md`
- Stories: `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/user-stories/Phase-1-Foundation/`
- FE tasks: `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/tasks/Frontend/student-app/Phase-1-Foundation/P1-11-FE.md`, `P1-09-FE.md`; `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/tasks/Frontend/packages/P1-08-FE.md`
- BE tasks: `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/tasks/Backend/Phase-1-Foundation/` (notably P1-09-BE.md has the thin `Me`; no profile/upload task)
- Built UI: `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/packages/ui/src/components/`, `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/`
- Captures: `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/design-system/screenshots/web/` + `/mobile/`
