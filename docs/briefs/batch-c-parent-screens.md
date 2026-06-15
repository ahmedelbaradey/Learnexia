# Pipeline Brief — Batch C: Port parent screen CONTENT to the parent-mobile design

## Summary & traceability
- **Task (1 line):** Reconcile the four parent *screen contents* (My Children, Child Overview, Reports, Settings) in the `student-app` against the **parent-mobile** design kit, so the same responsive codebase renders the mobile-design content where it currently diverges — without forking native vs web.
- **Decision of record:** Parent Mobile = the SAME `student-app` (one codebase, web PWA + native, responsive). Reconcile, do not fork. The parent SHELL (bottom tab nav, account menu, responsive header, sidebar) is DONE; web screens are responsive (Batch B). Batch C is screen-content parity only.
- **Design source (target):** `design-system/ui_kits/parent-mobile/PMScreens.jsx`, `PMScreensExtra.jsx`, `PMComponents.jsx`; per-component refs `design-system/preview/pm-*.html`; AR `design-system/ui_kits/parent-mobile/index-ar.html`; handoff `design-system/design_handoff_parent_app_and_auth/README.md`. Web look reference: `design-system/ui_kits/parent-dashboard/`.
- **FR / goal:** Parent monitoring surface (SRS parent dashboard / reports FRs); BRD **G3** (parent engagement / transparency into child progress). No backend change — this is a presentation-parity batch on top of P1-04 (`useMyChildren`) + Phase-5 stubs.
- **Phase/epic:** Frontend parent-app polish (parent-dashboard-uiux workstream). Energy IAP + full Energy/Activity screens are **Batch D** (out of scope here).

## Business context & value
- **Who benefits:** Parents (primary) — a single coherent monitoring app that looks/works the same on phone and web. Consistency reduces confusion and lowers the maintenance cost of one codebase serving both.
- **Value:** Visual + structural parity with the approved parent-mobile design; removes the web/mobile divergence that would otherwise force a fork. Most behavior already rides on existing real data (`useMyChildren`, attempts) + clearly-marked Phase-5 stubs, so this is low-risk presentation work.
- **Success measure:** Each parent screen visually matches its parent-mobile reference on narrow widths and stays coherent on web/wide; no regression to existing functional wiring (edit child, attempts-derived KPIs, profile save, language switch); RTL (ar) + en both correct.

## Important finding — the current implementation is already close
The current `_components/` are mature and already mirror most of the parent-mobile design:
- `ChildDashboardCard.tsx` ALREADY has: avatar, name, **Grade pill**, language label, active/inactive dot, a **Level/XP/Streak KPI tile row**, a **mastery bar**, the **weakest-topic line**, an **edit** affordance, and a "View dashboard →" action. This maps almost 1:1 to `PMChildRow` / `pm-child-row`.
- `OverviewWeb.tsx` already has the KPI strip + DailyActivityCard (bar chart deferred) + SubjectMasteryCard + FocusAreas + Recommendations — i.e. the *content* of `PMChildOverview` already exists, but as a dashboard keyed by the ChildSwitcher, NOT as a per-child drill-down route.
- `ReportsWeb.tsx` is real-data driven (attempts) with honest gaps; `SettingsWeb.tsx` is a tabbed profile/preferences surface.

**Consequence:** Batch C is mostly *targeted deltas*, not rebuilds. The two genuinely structural items needing a lead decision are (a) Child Overview drill-down route and (b) cards-vs-rows. Both are flagged in Open Questions.

---

## Acceptance criteria (testable)

### Visual gate (per screen, narrow width + RTL/EN)
- **AC-V1 (My Children):** The child entity shows **4 mini-stats** — Lv / XP / 🔥streak / **⚡energy** — matching `PMChildRow` (today it shows 3). Grade pill + language sit on the name row; active/inactive dot present; mastery bar + weakest line + "View progress/dashboard" present.
- **AC-V2 (My Children):** Family summary hero shows the parent-mobile stat set (Total XP · Lessons · Active) and reads correctly in EN + AR.
- **AC-V3 (Child Overview):** A per-child overview view exists showing: back + avatar header ("{name}'s progress" + Grade/Level/active line), **3 KPI tiles** (Time / XP earned / Lessons), a **daily-activity bar chart**, **subject-mastery bars** (4 product subjects — Math, Science, Arabic, English; NO Reading/Art from the JSX sample), and "Full report" + "Energy" buttons.
- **AC-V4 (Reports):** Areas-to-focus rows (`PMFocusRow`) + Lexi recommendation cards (`PMRecRow`) render per the design, in EN + AR.
- **AC-V5 (Settings):** Profile card (avatar + name + email + edit) and grouped preference rows (Language, Theme, Notifications, Plan & billing, Linked children, Screen-time, Privacy) render per `PMSettingsScreen` shape, in EN + AR.
- **AC-V6 (build contract):** native RTL uses `dir` (no `row-reverse` double-flip), numeric radii (Tamagui radius tokens don't resolve on native), tokens for color; pixel literals only where the design mandates.

### Functional gate (mostly stub-backed)
- **AC-F1:** The ⚡ energy mini-stat renders a **stubbed number** (per-child) and does NOT navigate to / depend on the Energy IAP screen (Batch D). It is display-only.
- **AC-F2:** Existing wiring is preserved with no regression: edit child (AddChildModal), attempts-derived Reports KPIs + honest gaps, profile save (`useUpdateProfile`), avatar upload (web), language switch.
- **AC-F3:** Child identity comes from `useMyChildren` (real); all weekly/per-subject/energy stats remain clearly-marked Phase-5 stubs — never a faked API.
- **AC-F4:** Child Overview's data source is decided (see OQ-1/OQ-3) and the bar chart is wired to the chosen stub OR rendered as the existing deferred slot — not fabricated as if real.
- **AC-F5:** 4-subject rule honored everywhere mastery is shown (no Social Studies; the JSX "Reading"/"Art" sample subjects are design placeholders — use the product subjects).

---

## Affected modules & data

### Files in scope (`apps/student-app/app/(parent)/_components/`)
| Screen | Current file(s) | Design target |
|---|---|---|
| My Children | `MyChildrenWeb.tsx`, `ChildDashboardCard.tsx`, `FamilySummaryStrip.tsx` | `PMChildrenScreen`, `pm-child-row`, `pm-family-summary`, `pm-kpi-tiles` |
| Child Overview | `OverviewWeb.tsx`, `DailyActivityCard.tsx`, `SubjectMasteryCard.tsx`, `FocusAreasCard.tsx`, `RecommendationsCard.tsx` | `PMChildOverview`, `pm-bar-chart`, `pm-mastery-rows` |
| Reports | `ReportsWeb.tsx`, `RecentAttemptsPanel.tsx` | `PMReportsScreen`, `pm-reports-rows` |
| Settings | `SettingsWeb.tsx` + `settings/*` panels | `PMSettingsScreen`, `pm-settings-rows` |
| Routes | `children.tsx`, `overview.tsx`, `index.tsx`, `_layout.tsx` (+ new per-child route if OQ-1=route) | — |
| Stubs | `parentDashboardStubs.ts` | add per-child `energy` stub |
| i18n | `packages/shared/src/i18n/resources.ts` (`parent.*` en + ar) | new keys |

### Data — new vs existing
- **Existing real data:** `useMyChildren` (child id/name/grade/language), `useStudentAttempts` (Reports KPIs), `useMyProfile`/`useUpdateProfile`, locale store.
- **Existing stubs:** `getChildStatsStub` (grade/level/xp/streak/mastery/weakest/active/locale), `getFamilyTotalsStub`, `getOverviewKpiStub`, `getSubjectMasteryStub`, `getFocusAreasStub`.
- **NEW stub field:** per-child `energy` number on `ChildStatsStub` (TODO(Batch D / P-energy)). The design's `PM_CHILDREN` uses values like 180/240/90. Deterministic, derived from child id, clearly marked. **No new entity, no migration, no endpoint.**

---

## Per-screen delta tables

### 1) My Children — `PMChildrenScreen` / `pm-child-row` / `pm-family-summary`
| Aspect | Design (parent-mobile) | Current | Delta / action | File |
|---|---|---|---|---|
| Mini-stats per child | **4 tiles**: 🧠 Lv · ⭐ XP · 🔥 streak · **⚡ energy** | 3 tiles (Lv/XP/streak) | **Add the ⚡ energy mini-stat** (4th tile). Stub value. | `ChildDashboardCard.tsx` |
| Energy stub | `child.energy` (e.g. 180) | none | Add `energy` to `ChildStatsStub` + `getChildStatsStub`. | `parentDashboardStubs.ts` |
| Layout | mobile = vertical **row card** in a single column; entity is a tappable card | web = 3-col card grid; native = wrapping flex | Keep responsive: cards on web grid, single-column stacked cards on narrow (current `MyChildrenWeb` already does grid→flex). Decide cards-vs-rows naming (OQ-2) — content is the same. | `MyChildrenWeb.tsx`, `ChildDashboardCard.tsx` |
| Grade pill + language | name row: name + `Grade N` pill + lang flag | already present (grade pill + lang label) | Minor parity check: design uses a flag glyph (🇬🇧/🇪🇬); current uses a localized language label. Confirm which (OQ-4). | `ChildDashboardCard.tsx` |
| Footer | "Weakest: X" + "View progress →" | "Weakest: X" + "View dashboard →" | Parity OK; the on-tap target depends on OQ-1 (route vs dashboard). | `ChildDashboardCard.tsx` |
| Family summary | THIS WEEK · ALL CHILDREN: Total XP · Lessons · Active (3 stats, dividers) | eyebrow + headline + subline + 4 stats (XP/Lessons/BestStreak/Badges) | Web version is richer (intentional, Batch B). Keep web; ensure narrow reflow reads like `pm-family-summary`. No data change required. | `FamilySummaryStrip.tsx` |
| Add-child CTA | dashed "+ Add a child" card/button | `AddChildCard` + pick-a-child button | Parity OK. | `MyChildrenWeb.tsx`, `AddChildCard.tsx` |

**New components:** none strictly required — extend `ChildDashboardCard` with a 4th mini-stat (and optionally extract a small `MiniStat` if the row gets unwieldy — but per CLAUDE.md rule 8, do NOT introduce a new abstraction without asking; inline is fine).
**Shared web+native:** yes (single component, responsive).

### 2) Child Overview — `PMChildOverview` / `pm-bar-chart` / `pm-mastery-rows`
| Aspect | Design (parent-mobile) | Current | Delta / action | File |
|---|---|---|---|---|
| Entry model | **per-child drill-down**: tap a child row → "{name}'s progress" with a **back button** | `overview.tsx` is a **dashboard** keyed by the ChildSwitcher (no back, no per-child route) | **STRUCTURAL DECISION (OQ-1).** Either (a) new per-child route `(parent)/child/[id].tsx` rendering an overview keyed by route id + back, or (b) reuse `OverviewWeb` and treat the child-row tap as "set active child + go to overview". | `overview.tsx` (+ new route?), `MyChildrenWeb.tsx` |
| Header | back ‹ + avatar + "{name}'s progress" + "Grade N · Level L · Active today" | OverviewWeb header = "{name}'s progress" via ParentHeader (no back, no avatar) | Add the avatar + back header for the drill-down variant. | new/`OverviewWeb.tsx` |
| KPI tiles | **3 tiles**: ⏱️ Time · ⭐ XP earned · ✅ Lessons (with deltas) | **4 tiles** (Time/XP/Lessons/**Streak**) | Reconcile: design shows 3; current shows 4. Decide whether to drop Streak on the drill-down or keep 4 (OQ-5). | `OverviewWeb.tsx` |
| Daily activity | **real bar chart** (`PMBarChart`, XP/day, weekend highlighted) | **deferred slot** (placeholder panel, chart not built) | The design shows actual bars. Decide: render bars from a Phase-5 **stub series**, or keep the deferred slot (OQ-3). If bars: add a `getDailyActivityStub(childId)` + a small bar-chart view. | `DailyActivityCard.tsx`, `parentDashboardStubs.ts` |
| Subject mastery | mastery rows (`PMMasteryRow`) — sample uses Math/Reading/Science/Art | `SubjectMasteryCard` uses the 4 product subjects | Keep 4 product subjects (Math/Science/Arabic/English) — **ignore the JSX sample subjects** (Reading/Art are placeholders). | `SubjectMasteryCard.tsx` |
| Quick links | "📈 Full report" (ghost) + "⚡ Energy" (energy variant) | none on overview | Add two buttons. "Full report" → reports; **"Energy" → energy stub route (Batch D target)** — link only, screen is a stub. | new/`OverviewWeb.tsx` |

**New components:** a `ParentBarChart` view (only if OQ-3 = render stub bars) translating `PMBarChart`. Possibly a per-child route file.
**Shared web+native:** yes.

### 3) Reports — `PMReportsScreen` / `pm-reports-rows`
| Aspect | Design (parent-mobile) | Current | Delta / action | File |
|---|---|---|---|---|
| Weekly summary banner | green "had a strong week 🎉" + "+480 XP · 14 lessons · accuracy up 9 pts" | KPI row from real attempts + honest gaps + first-week band | The narrative banner is stub copy; current uses real KPIs. Decide whether to add the celebratory banner (stub) or keep the honest real-data KPI row (OQ-6). Likely keep real KPIs (no fabrication rule) + optionally a light header line. | `ReportsWeb.tsx` |
| Areas to focus | `PMFocusRow` rows (icon + title + subject + % bar) | FocusAreas exists on Overview, not on Reports | Add a "Areas to focus on" rows section to Reports per `pm-reports-rows` (stub-backed via `getFocusAreasStub`). | `ReportsWeb.tsx` (+ reuse FocusAreasCard pattern) |
| Recommendations from Lexi | `PMRecRow` cards (icon + title + body + CTA) | RecommendationsCard exists on Overview | Add a "Recommendations from Lexi" section to Reports (stub copy). | `ReportsWeb.tsx` (+ reuse RecommendationsCard) |
| Send report | "📤 Send report to email" button | removed from header per Batch B design | Reconcile: design has the button; Batch B removed it. Confirm (OQ-7). |
| Charts | bar chart shown | deferred slots (XP / time-of-day) | Keep deferred slots unless OQ-3 says render stub bars. | `ReportsWeb.tsx` |

**New components:** none — reuse `FocusAreasCard` / `RecommendationsCard` shapes (and the focus/rec stubs already exist).
**Shared web+native:** yes.

### 4) Settings — `PMSettingsScreen` / `pm-settings-rows`
| Aspect | Design (parent-mobile) | Current | Delta / action | File |
|---|---|---|---|---|
| Layout | **profile card on top + grouped preference ROWS** (single scroll) | **left-rail Tabs** (Profile / Notifications / Linked / Security / Billing / Language) + active panel | Reconcile: mobile design is row-list, web is tabbed. Likely keep tabs on web, render the row-list on narrow (responsive), OR unify on rows (OQ-8). | `SettingsWeb.tsx` |
| Profile card | avatar + name + email + ✏️ edit button | full ProfilePanel (avatar upload, name/phone/country, save) | Current is richer + wired. The mobile "card → edit" can open the existing profile panel/modal. | `SettingsWeb.tsx` |
| Preference rows | Language (🇺🇸 EN pill), Theme (Night), Notifications, Plan & billing (Premium badge) | Language panel + Notifications/Plan placeholders | Add the row presentation (with right-side value pills / chevrons) for narrow; keep functional Language + Profile wiring. | `SettingsWeb.tsx`, `settings/*` |
| Children & safety group | Linked children (count), Screen-time limits, Privacy & data | LinkedChildren panel exists; screen-time/privacy = coming soon | Render as rows; keep "coming soon" for unbuilt ones. | `settings/LinkedChildrenPanel.tsx` |
| Log out | danger "↪ Log out" button + "v2.4.0" | logout migrated to AccountMenu (shell) | Reconcile: design has logout in Settings; current has it in the account menu. Confirm no double logout (OQ-9). |

**New components:** a `SettingsRow` presentation (icon + label + right slot) IF unifying to rows — flag as a small presentational helper, not a design pattern.
**Shared web+native:** the row-list is the native/narrow presentation; tabs are the wide presentation. Responsive.

---

## The ⚡ Helper-Energy mini-stat (scoped narrowly)
- **In scope (Batch C):** the **display of a stubbed energy number** on each child entity (My Children mini-stats) and the "⚡ Energy" *link* on Child Overview (links to the existing energy stub route).
- **Out of scope (Batch D):** the full Helper-Energy screen (`PMEnergyScreen` — battery meter, weekly usage, top-up), the IAP/payment flow, any energy mutation, and the Activity screen (`PMActivityScreen`).
- **Dependency flag:** the energy number has **no backend** today. It is a deterministic stub on `ChildStatsStub`. When Batch D / energy backend lands, swap the stub for the real balance. The mini-stat must NOT imply a working balance beyond a number.

## Handoff → db-migration
**None.** No new entities, fields, relationships, or schema. This batch is presentation-only and adds a client-side stub field. (If the lead later wants real energy/analytics, that is a separate backend story — out of scope.)

## Handoff → backend-feature
**None.** No new commands/queries/endpoints/DTOs. Confirmed honest gaps already documented in `ReportsWeb.tsx` (no parent-readable child XP endpoint, no per-subject mastery aggregate) remain gaps — do not fabricate. If the lead wants to close any gap, raise a separate Phase-5 backend story.

## Handoff → frontend
- **Screens/components to touch:** `ChildDashboardCard` (+ energy mini-stat), `OverviewWeb`/Child-Overview drill-down (+ back/avatar header, 3-KPI reconcile, bar chart decision, Full report/Energy buttons), `ReportsWeb` (+ focus rows + Lexi recs + Send-report decision), `SettingsWeb` (+ row presentation for narrow). Stubs: add `energy` to `parentDashboardStubs.ts` (+ optional `getDailyActivityStub`).
- **API shapes:** unchanged — `useMyChildren`, `useStudentAttempts`, `useMyProfile`/`useUpdateProfile`, locale store. All new numbers are client stubs.
- **i18n:** add keys under the existing `parent.*` namespace in `packages/shared/src/i18n/resources.ts` (BOTH `en` and `ar`):
  - `parent.myChildren.statEnergy` (+ short/value form if needed) — mini-stat label.
  - Child Overview: `parent.childOverview.*` (title `{{name}}`, back a11y, KPI labels Time/XP/Lessons if not reusing `parent.overview.kpi.*`, buttons `fullReport` / `energy`).
  - Reports: `parent.reports.focus.*` and `parent.reports.recommendations.*` (titles + row copy) — reuse Overview's focus/rec keys where they exist.
  - Settings: `parent.settings.rows.*` (Theme, Notifications, Plan/Premium badge, Screen-time, Privacy) if rendering the row-list — many already exist under `parent.settings.tabs.*`.
  - AR numerals: reuse the existing `Intl ar-EG` + `statStreakValue`-style patterns; energy value uses tabular-nums + localized digits.
- **Build contract (hard):** native RTL via `dir` on the row root (NO `row-reverse` double-flip — see `_layout.tsx`/`ParentTabBar.tsx`/`ChildDashboardCard.tsx` precedent); **numeric radii** (Tamagui radius tokens render square on native); color via tokens; pixel literals only where the design mandates. Build the UI **from the JSX/HTML kit + `design-system/preview/pm-*.html`**, not from imagination. No new design pattern without asking the lead (CLAUDE.md rule 8).
- **No fabricated data:** stubs must be clearly marked TODO and derived from child id; honest gaps stay honest.

---

## Open questions / assumptions / risks (for the lead → user)

**OQ-1 (structural, highest priority): Child Overview — new per-child route or reuse the dashboard?**
The mobile design's `PMChildOverview` is a **drill-down from a child row** (back button, "{name}'s progress", avatar header). The current `overview.tsx` is a **dashboard keyed by the ChildSwitcher** — no per-child route, no back. Options: (a) add `(parent)/child/[id].tsx` route + render an overview keyed by route id with a back affordance (closest to design, but new routing + nav reconcile with the tab bar/sidebar which have no "child" tab); (b) reuse `OverviewWeb` and make a child-row tap "set active child + navigate to overview" (less work, but no per-child URL / back semantics). **Recommend the lead decide before planning** — this drives whether a new route file + nav wiring is in scope.

**OQ-2: Child CARDS (web) vs ROWS (mobile) — unify or keep both?**
The web uses a 3-col card grid; the mobile design is a single-column stacked card/row. The current component already collapses grid→flex responsively, so the *content* is identical. Confirm: keep the responsive card (grid on web, stacked on narrow) — recommended — or strictly render compact "rows" on native.

**OQ-3: Bar chart data source — Phase-5 stub or keep the deferred slot?**
`PMChildOverview` and `PMReportsScreen` show real bars; current code renders **deferred placeholder slots** (no chart, by design, to avoid faking analytics). Options: (a) render a clearly-labeled **stub** daily-activity series so the screen matches the design visually; (b) keep the deferred slot until Phase-5 analytics. The no-fabrication rule favors (b) but the visual gate favors (a) with an explicit "sample data" treatment. **Lead to decide.**

**OQ-4: Language indicator — flag glyph or localized label?** Design uses 🇬🇧/🇪🇬 flags; current uses a localized language word. Pick one for parity.

**OQ-5: Child Overview KPI count — 3 (design) or 4 (current, includes Streak)?** Drop Streak to match the design's 3 tiles, or keep 4?

**OQ-6: Reports "strong week" banner — add stub narrative or keep real-data KPIs only?** Design shows a celebratory narrative; current shows attempt-derived KPIs + honest gaps. Adding the banner = stub copy.

**OQ-7: Reports "Send report to email" button — restore it?** Batch B removed period/send controls from parent headers per the web design, but `PMReportsScreen` has a "Send report to email" button. Confirm whether it returns (as a Phase-9 stub) or stays removed.

**OQ-8: Settings — keep web Tabs + add narrow row-list (responsive), or unify on the row-list everywhere?** Current is a tab rail; mobile design is a row-list. Recommend responsive (tabs wide, rows narrow); confirm.

**OQ-9: Settings logout — design puts "Log out" in Settings; current has it in the shell AccountMenu.** Avoid a duplicate logout. Confirm placement (likely keep AccountMenu only, or add a Settings logout row that calls the same action).

**Assumptions (proceeding unless told otherwise):**
- 4 product subjects only (Math/Science/Arabic/English) — the JSX sample's Reading/Art are placeholders and are ignored.
- Energy mini-stat is a display-only stub; no Energy IAP/screen work in this batch.
- No backend/db work; honest gaps remain.
- Responsive single-codebase parity (no native fork).

**Risks:**
- Scope creep into Batch D (Energy/Activity) if the "⚡ Energy" link or mini-stat is over-built — keep it display-only.
- The Child Overview route decision (OQ-1) materially changes the size of the batch; planning should not start until it's answered.
- Reconciling design "rows/banner/send" vs Batch-B web decisions risks re-introducing controls Batch B deliberately removed — OQ-6/7/9 guard against that.

## Recommended pipeline order (first cut — `planner` finalizes)
1. **Lead resolves OQ-1, OQ-3, OQ-6/7/8/9** (blocking — they change scope/structure). At minimum OQ-1 + OQ-3 before planning.
2. **`designer`** — produce a Design Spec (`design-system/ui_kits/parent-mobile/<story>.md`) reconciling the four screens against the kit + the lead's OQ answers (this is a UI batch; designer is required).
3. **`frontend` (single stack, batchable internally):**
   - B1 (independent, parallel): `parentDashboardStubs.ts` energy stub + i18n keys (en+ar) + My Children mini-stat (`ChildDashboardCard`).
   - B2: Child Overview (depends on OQ-1 route decision) — header + KPI reconcile + bar-chart decision + buttons.
   - B3 (parallel with B2): Reports focus rows + Lexi recs; Settings row presentation.
4. **`frontend-e2e-tester`** — student-app web PWA flows: parent login → My Children (4 mini-stats incl. ⚡) → child drill-down/overview → Reports → Settings; RTL (ar) + en, validation, routing.
5. **`reviewer`** — gate against the visual + functional ACs above + CONVENTIONS.md (no `row-reverse` double-flip, numeric radii, tokens, no fabricated data, 4-subject rule, no new design pattern).
6. **`committer`** — after PASS, branch `feat/batch-c-parent-screens`, PR.
*(No `db-migration`, `backend-feature`, `api-tester`, or `security-auditor` — no backend/security surface in this batch.)*
