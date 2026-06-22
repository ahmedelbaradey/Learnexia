# Phase 3 — Gamification · Student-App Frontend (Web E2E) Test Cases

> **Surface:** Expo/Tamagui student-app web PWA (`apps/student-app`), child route group `(child)/*`. Runtime RTL (Arabic-default) **and** LTR (English), light/dark. The `frontend-e2e-tester` implements these with Playwright against the running web build and records pass/fail in `execution-report.md`.
>
> **How to read this doc.** Cases are grouped per screen with stable IDs (`GAM-FE-TC-NN`). Each carries Type, Priority (P0 blocks release / P1 should / P2 nice), Preconditions/seed, Steps, Expected, the **testID** to target, and a **Status** column the runner fills. Seed needs are flagged in the Precondition column and summarized in `coverage-report.md`.
>
> **Grounding facts the implementer must honor** (read from the actual screens):
> - Tabs = Home (`child-tab-index`), Missions (`child-tab-missions`), League (`child-tab-league`), Badges (`child-tab-badges`); bar = `child-tab-bar`. XP / Streak / Hearts / Events are **push screens** (`href: null`) reachable by URL `/(child)/<route>` (try `/<route>` fallback) — the TabBar is **hidden** on them.
> - Default locale is **ar → `document.documentElement.dir === 'rtl'`**; locale switch flips to `ltr`. Set via `getByTestId('locale-switch-{ar|en}')` on the login screen, or clear `localStorage.lx_locale` to reset to ar default. `dir` is set in a `useEffect` — always `waitForFunction` on it, never read synchronously.
> - **Numerals rule (SKILL.md rule 5):** XP / weekly-XP / multiplier counters are **always Latin digits + LTR even in AR**; prose counts (level, streak days, ranks, hearts, freeze, mission counts) use Eastern-Arabic digits in AR. Progress bars are **LTR-locked** (fill grows from visual left) in both locales.
> - **No raw i18n keys** may ever be visible (no text starting `xp.` / `streak.` / `hearts.` / `badges.` / `missions.` / `league.` / `events.` / `common.` / `child.`).
> - RN-Web hydration: after `goto`, wait for the screen root testID `visible`, then a short settle, before asserting child state.
> - Auth: `(child)` layout runs `useGroupGuard('(child)')` — signed-out users and **parents** are redirected away before any child content renders.
> - Login helper: child persona = 2nd radio in `login-persona-toggle`; fields `login-username` / `login-password` / `login-submit`.

---

## 0. Navigation, auth & role routing (shell)

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-01 | TabBar renders 4 tabs after child login | functional | P0 | seeded child (render-only) | login as child → wait | `child-tab-bar` visible; all of `child-tab-index`, `child-tab-missions`, `child-tab-league`, `child-tab-badges` visible | `child-tab-bar` | |
| GAM-FE-TC-02 | Missions tab navigates; bar stays | functional | P0 | child | tap `child-tab-missions` | `missions-screen` renders; `child-tab-bar` still visible | `missions-screen` | |
| GAM-FE-TC-03 | League tab navigates; bar stays | functional | P0 | child | tap `child-tab-league` | `league-screen` renders; bar visible | `league-screen` | |
| GAM-FE-TC-04 | Badges tab navigates; bar stays | functional | P0 | child | tap `child-tab-badges` | `child-badges-screen` renders; bar visible | `child-badges-screen` | |
| GAM-FE-TC-05 | XP push screen reachable; bar hidden | functional | P0 | child | `goto /(child)/xp` | `xp-screen` renders; `child-tab-bar` NOT visible | `xp-screen` | |
| GAM-FE-TC-06 | Streak push screen reachable; bar hidden | functional | P0 | child | `goto /(child)/streak` | `streak-screen` renders; bar hidden | `streak-screen` | |
| GAM-FE-TC-07 | Hearts push screen reachable; bar hidden | functional | P0 | child | `goto /(child)/hearts` | `child-hearts-screen` renders; bar hidden | `child-hearts-screen` | |
| GAM-FE-TC-08 | Events push screen reachable; bar hidden | functional | P0 | child | `goto /(child)/events` | `events-screen` renders; bar hidden | `events-screen` | |
| GAM-FE-TC-09 | Signed-out → redirect away from child screens | auth | P0 | no session | `goto /(child)/xp` | redirected to `/login`; no `xp-screen` content rendered | `login-username` | |
| GAM-FE-TC-10 | Parent role cannot view child gamification | auth-authz | P0 | parent session (no child persona) | login as parent → `goto /(child)/missions` | guard redirects (not parent-home child content); `missions-screen` never shown | n/a | |
| GAM-FE-TC-11 | Back from push screen returns to prior screen | functional | P1 | child | open `/(child)/streak` → tap `streak-back` | navigates away from streak without crash (body still rendered) | `streak-back` | |
| GAM-FE-TC-12 | Android-style back on non-home tab → Home first | navigation | P2 | child | (web) tab to Missions → browser back | returns toward Home tab (`child-tab-index` active) — `backBehavior` default | `child-tab-index` | |

---

## 1. Dashboard preview rows / entry points (`app/(child)/index.tsx`)

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-20 | Dashboard header renders after login | functional | P0 | child (render-only) | login → wait | `dashboard-header` visible; greeting + stats strip present | `dashboard-header` | |
| GAM-FE-TC-21 | Hearts chip → hearts screen | functional | P0 | child | tap `stats-hearts` | navigates to `child-hearts-screen` | `stats-hearts` | |
| GAM-FE-TC-22 | Streak chip → streak screen | functional | P0 | child | tap `stats-streak` | navigates to `streak-screen` | `stats-streak` | |
| GAM-FE-TC-23 | XP bar chip → xp screen | functional | P0 | child | tap `stats-xp` | navigates to `xp-screen` | `stats-xp` | |
| GAM-FE-TC-24 | Freeze chip → events (only when balance>0) | functional | P1 | **seeded child with freezeBalance>0** | tap `stats-freeze` | navigates to `events-screen`; chip ABSENT when balance=0 | `stats-freeze` | |
| GAM-FE-TC-25 | League preview row → league screen | functional | P1 | **seeded child with leaguePreview** (weekly XP placing them) | tap `league-preview` | navigates to `league-screen`; row hidden for brand-new child | `league-preview` | |
| GAM-FE-TC-26 | Timed-event banner → events (when active) | functional | P1 | **seeded active timed event** (admin endpoint) | tap `event-entry` | navigates to `events-screen`; banner absent when no active event | `event-entry` | |
| GAM-FE-TC-27 | "My activity" row → attempts | functional | P2 | child | tap `activity-entry` | navigates to `child-attempts-screen` | `activity-entry` | |
| GAM-FE-TC-28 | Practice-mode pill shows when inPracticeMode | state | P1 | **seeded child at 0 hearts** (inPracticeMode=true) | login → inspect stats strip | practice-mode pill visible with localized text; absent otherwise | (text) `child.home.practiceMode` resolved | |
| GAM-FE-TC-29 | Dashboard error strip + retry | state/error | P1 | child + forced dashboard API failure (route abort) | login | `dashboard-error` strip visible with retry; `dashboard-error-retry` refetches; subjects list still renders | `dashboard-error` | |
| GAM-FE-TC-30 | Dashboard loading skeleton | state | P2 | child + throttled dashboard | login | `dashboard-header` skeleton (loading) renders before data | `dashboard-header` | |
| GAM-FE-TC-31 | No raw i18n keys on dashboard (ar + en) | RTL-i18n | P1 | child | login ar; switch en | no visible text matches `^(child|common)\.`; `dir` flips rtl↔ltr | `dashboard-header` | |

---

## 2. XP & Level screen (`app/(child)/xp.tsx`) — P4-02

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-40 | XP screen renders one of loading/error/populated | state | P0 | child (render-only) | `goto /(child)/xp` | exactly one of `xp-loading` / `xp-error` / `xp-hero`(+`xp-progress-card`,`xp-total-tile`) is shown | `xp-screen` | |
| GAM-FE-TC-41 | First-time child: level hero + total XP render honestly | functional | P1 | brand-new child (xp=0, level=1) | open xp | `xp-hero` shows level 1; `xp-total-tile` shows 0; no fabricated progress | `xp-hero` | |
| GAM-FE-TC-42 | Populated: progress card shows level window + bar | functional | P1 | **seeded child with XP activity** (correct answers / lesson done) | open xp | `xp-progress-card` shows "Level N → N+1" header, counter, fill bar, footer percent/left | `xp-progress-card` | |
| GAM-FE-TC-43 | XP counters are Latin digits + LTR in AR | RTL-i18n | P0 | seeded child w/ XP, locale=ar | open xp in ar | total-XP + "n / m XP" counter render Latin digits (0-9), not Eastern-Arabic; `dir=rtl` on page | `xp-total-tile` | |
| GAM-FE-TC-44 | Level number uses Eastern-Arabic digits in AR | RTL-i18n | P1 | seeded child level≥2, ar | open xp in ar | hero level number renders Eastern-Arabic (٢…) while XP stays Latin | `xp-hero` | |
| GAM-FE-TC-45 | Progress bar is LTR-locked (fill from left) in AR | RTL-i18n | P1 | seeded child mid-level, ar | open xp in ar | fill grows from visual left regardless of RTL page | `xp-progress-card` | |
| GAM-FE-TC-46 | Curve-drift honesty guard: total-only fallback | boundary | P2 | child whose BE total sits outside derived window | open xp | counter shows total-XP-only string; no footer percent/left; bar empty (no fake progress) | `xp-progress-card` | |
| GAM-FE-TC-47 | Error state + retry | state/error | P1 | child + forced dashboard failure | open xp | `xp-error` strip visible; `xp-retry` triggers refetch | `xp-error` | |
| GAM-FE-TC-48 | Back button works | functional | P2 | child | open xp → tap `xp-back` | navigates away without crash | `xp-back` | |
| GAM-FE-TC-49 | Hero a11y label present on level disc | a11y | P1 | child | open xp | `xp-hero` has accessible/aria-label with level; bar has `progressbar` role + value | `xp-hero` | |
| GAM-FE-TC-50 | Level-up celebration popup (RewardPopup level-up) | functional/motion | P1 | **seeded: child gains XP crossing a level threshold between two dashboard refreshes** | open xp → trigger XP gain → refetch | RewardPopup (level-up) appears; dismiss runs count-up; then bar re-fills | (RewardPopup root) | |
| GAM-FE-TC-51 | Reduced motion: no bar fill / count-up animation | a11y/motion | P2 | child + `prefers-reduced-motion` emulated | open xp populated | bar renders at target instantly; level-up count-up jumps to final (no animation) | `xp-progress-card` | |

---

## 3. Streak screen (`app/(child)/streak.tsx`) — P4-03 / P4-11 freeze

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-60 | Streak screen renders loading/error/hero | state | P0 | child (render-only) | `goto /(child)/streak` | one of `streak-loading` / `streak-error` / `streak-hero` shown | `streak-screen` | |
| GAM-FE-TC-61 | Zero-state flame is never-shaming | state | P0 | brand-new child (streak=0) | open streak | flame at reduced opacity (0.4); meta line = encouraging zero-title, NOT a scold; day count = 0 | `streak-hero` | |
| GAM-FE-TC-62 | Populated streak shows day count + glowing flame | functional | P1 | **seeded child with streak≥1** | open streak | `streak-hero` shows N days + flame; milestone markers reflect reached/upcoming | `streak-hero` | |
| GAM-FE-TC-63 | Milestone markers (3/7/14/30) reached vs upcoming | functional | P1 | **seeded child streak between two milestones (e.g. 5)** | open streak | reached markers show ✓ in `$streak`; upcoming show day number; next-target line correct | `streak-milestones` | |
| GAM-FE-TC-64 | Streak-freeze balance pill (earned-only, no spend UI) | functional | P1 | **seeded child with freezeBalance>0** | open streak | `streak-freeze` pill shows ❄️ + count; NO buy/spend button anywhere; explainer text present | `streak-freeze` | |
| GAM-FE-TC-65 | Calendar/history honest placeholder (no fake data) | state | P1 | child | open streak | `streak-history` shows "coming soon" placeholder; NO fabricated per-day calendar | `streak-history` | |
| GAM-FE-TC-66 | Day/freeze counts Eastern-Arabic in AR | RTL-i18n | P1 | seeded streak≥1, ar | open streak in ar | day count + freeze count render Eastern-Arabic digits; `dir=rtl` | `streak-hero` | |
| GAM-FE-TC-67 | LTR layout in English | RTL-i18n | P2 | seeded streak, en | switch en → open streak | `dir=ltr`; row order flips; counts Latin | `streak-screen` | |
| GAM-FE-TC-68 | Error + retry | state/error | P1 | child + forced failure | open streak | `streak-error` + `streak-retry` refetch | `streak-error` | |
| GAM-FE-TC-69 | Flame loop reduced-motion fallback | a11y/motion | P2 | seeded streak≥1 + reduced-motion | open streak | flame static WITH glow (no scale loop) | `streak-hero` | |
| GAM-FE-TC-70 | Hero a11y label + milestone marker a11y | a11y | P1 | child | open streak | `streak-hero` aria-label includes eyebrow+days+meta; each milestone marker has reached/upcoming a11y | `streak-hero` | |

---

## 4. Hearts & Practice Mode screen (`app/(child)/hearts.tsx`) — P4-04

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-80 | Hearts screen renders loading/error/row | state | P0 | child (render-only) | `goto /(child)/hearts` | one of `hearts-loading` / `hearts-error` / `hearts-row` shown | `child-hearts-screen` | |
| GAM-FE-TC-81 | Full hearts (5/5) ready-state sub-line | state | P1 | brand-new child (hearts=5) | open hearts | `hearts-row` shows 5 filled hearts; `hearts-sub` = positive full sub; no refill card | `hearts-row` | |
| GAM-FE-TC-82 | Partial hearts show filled + dimmed slots + refill card | functional | P1 | **seeded child with hearts<5** (e.g. 2 wrong answers) | open hearts | `hearts-row` shows correct filled/dimmed split; `hearts-refill-card` visible (honest static copy, no timer) | `hearts-refill-card` | |
| GAM-FE-TC-83 | Heart-lost card on `?lost=1` arrival | state | P1 | **seeded child hearts<5** | `goto /(child)/hearts?lost=1` | `hearts-lost-card` visible (never-shaming copy); break motion on freshest lost slot | `hearts-lost-card` | |
| GAM-FE-TC-84 | Practice-mode explainer card at 0 hearts | functional | P0 | **seeded child at 0 hearts** (inPracticeMode=true) | open hearts | `hearts-practice-card` visible with encouraging copy; framed as soft regain, NEVER a block/paywall | `hearts-practice-card` | |
| GAM-FE-TC-85 | Practice CTA continues a lesson (never blocks) | functional | P0 | seeded child w/ continue target | tap `hearts-practice-cta` | navigates into a lesson player route; no paywall/hard-block screen | `hearts-practice-cta` | |
| GAM-FE-TC-86 | Practice CTA falls back to Home for net-new child | boundary | P2 | brand-new child (no continue target) | tap `hearts-practice-cta` | routes to Home dashboard (no crash) | `hearts-practice-cta` | |
| GAM-FE-TC-87 | Hearts row a11y (count/max value) | a11y | P1 | child | open hearts | `hearts-row` has aria-label + accessibilityValue {min0,max5,now} | `hearts-row` | |
| GAM-FE-TC-88 | Error + retry | state/error | P1 | child + forced failure | open hearts | `hearts-error` + `hearts-retry` refetch | `hearts-error` | |
| GAM-FE-TC-89 | RTL (ar) + LTR (en) layout | RTL-i18n | P1 | child | open hearts ar then en | `dir` flips; row direction flips; no clipped controls; no raw keys | `child-hearts-screen` | |
| GAM-FE-TC-90 | Reduced motion: instant gray-out on `?lost=1` | a11y/motion | P2 | seeded hearts<5 + reduced-motion | `goto …?lost=1` | lost heart greys instantly; no scale/glyph-swap animation | `hearts-row` | |

---

## 5. Badges screen (`app/(child)/badges.tsx`) — P4-05

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-100 | Badges screen renders loading/error/empty/grid | state | P0 | child (render-only) | `goto /(child)/badges` | one of `badges-loading` / `badges-error` / `badges-empty` / (`badges-stats`+`badges-grid`) shown | `child-badges-screen` | |
| GAM-FE-TC-101 | All-locked first-time gallery (encouraging sub) | state | P0 | brand-new child (0 earned, catalog>0) | open badges | grid renders all locked tiles (dimmed); header sub = encouraging empty-sub; stats strip all 0 | `badges-grid` | |
| GAM-FE-TC-102 | Earned + locked split renders correctly | functional | P1 | **seeded child with ≥1 earned badge** | open badges | earned tile(s) at full opacity w/ rarity variant; locked tiles dimmed w/ how-to-earn hint | `badges-grid` | |
| GAM-FE-TC-103 | Sort: earned (newest first) then locked | functional | P1 | **seeded child ≥2 earned at different times** | open badges | earned ordered newest-first, then locked in catalog order | `badges-grid` | |
| GAM-FE-TC-104 | Stats strip counts per rarity | functional | P1 | seeded child w/ earned badges | open badges | `badges-stats` cells (bronze/silver/gold/legend) show correct earned counts | `badges-stats` | |
| GAM-FE-TC-105 | Earned tile shows date; locked tile shows hint | functional | P2 | seeded child mixed | open badges | earned tile footer = earned-on date; locked footer = how-to-earn hint | `badge-tile-{code}` | |
| GAM-FE-TC-106 | Empty catalog edge (server sent 0 definitions) | boundary | P2 | env where badge catalog is empty | open badges | `badges-empty` shown (medal glyph + copy), not an error | `badges-empty` | |
| GAM-FE-TC-107 | Unknown badge code falls back to code (no raw key) | boundary | P2 | seeded badge with code not in FE i18n map | open badges | tile name = raw server code (technical id), NOT a thrown error / blank | `badge-tile-{code}` | |
| GAM-FE-TC-108 | Badge tile a11y label (name/rarity/state) | a11y | P1 | child | open badges | each `badge-tile-{code}` Badge has aria-label describing earned/locked + rarity | `badge-tile-{code}` | |
| GAM-FE-TC-109 | Error + retry | state/error | P1 | child + forced `useMyBadges` failure | open badges | `badges-error` + `badges-retry` refetch | `badges-error` | |
| GAM-FE-TC-110 | RTL grid (ar) + dates Eastern-Arabic | RTL-i18n | P1 | seeded earned, ar | open badges ar | `dir=rtl`; grid row direction flips; earned date Eastern-Arabic; no raw keys | `badges-grid` | |
| GAM-FE-TC-111 | Badge-unlock celebration (dashboard diff) | functional/motion | P2 | **seeded: child earns a new badge between dashboard refreshes** | on Home, earn badge → refetch | RewardPopup (badge-unlock) fires on Home (NOT on badges tab — celebration is dashboard-driven) | (RewardPopup root) | |

---

## 6. Missions screen (`app/(child)/missions.tsx`) — P4-06

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-120 | Missions screen renders loading/error/empty/data | state | P0 | child (render-only) | `goto /(child)/missions` | one of `missions-loading` / `missions-error` / `missions-empty` / (`missions-hero`/`missions-daily`) shown | `missions-screen` | |
| GAM-FE-TC-121 | Empty state ("new missions at midnight") | state | P1 | child with no missions issued | open missions | `missions-empty` (🎯 + copy), not an error/spinner | `missions-empty` | |
| GAM-FE-TC-122 | Daily list renders rows with progress | functional | P1 | **seeded child with daily missions** | open missions | `missions-daily` lists `mission-row-{code}` rows; each has icon, title, count sub, progress bar, reward pill | `missions-daily` | |
| GAM-FE-TC-123 | Header progress headline (done/total) | functional | P1 | seeded daily missions | open missions | header shows "X / Y" dailies-done; resets line present | `missions-screen` | |
| GAM-FE-TC-124 | Reward hero shown only while dailies incomplete | functional | P1 | seeded with ≥1 incomplete daily | open missions | `missions-hero` shows summed remaining XP; HIDDEN once all dailies complete (no fake reward) | `missions-hero` | |
| GAM-FE-TC-125 | Completed row treatment (✓ tile, green, 100% bar) | functional | P1 | **seeded child with ≥1 completed daily mission** | open missions | completed `mission-row-{code}` shows ✓ tile, solid success bar, "Completed" sub, green reward pill | `mission-row-{code}` | |
| GAM-FE-TC-126 | Expired row muted, non-shaming (never red) | functional/state | P2 | **seeded child with an expired mission** | open missions | expired row at 0.4 opacity, "Time's up" sub; NOT red/shaming | `mission-row-{code}` | |
| GAM-FE-TC-127 | Weekly missions section (regular, excludes CHALLENGE_) | functional | P1 | **seeded child with regular weekly mission(s)** | open missions | `missions-weekly` section renders weekly rows; rows whose code starts `CHALLENGE_` do NOT appear here | `missions-weekly` | |
| GAM-FE-TC-128 | All-dailies-complete celebration (RewardPopup xp) | functional/motion | P1 | **seeded: last incomplete daily flips to complete during a refetch** | complete final daily → refetch | RewardPopup (xp) "Mission Complete!" mounts with total daily XP | (RewardPopup root) | |
| GAM-FE-TC-129 | Mission counts Eastern-Arabic; XP reward Latin in AR | RTL-i18n | P1 | seeded missions, ar | open missions ar | count sub "١ من ٤" Eastern-Arabic; reward pill "+50" Latin + LTR; `dir=rtl` | `mission-row-{code}` | |
| GAM-FE-TC-130 | Unknown titleKey falls back to generic title | boundary | P2 | seeded mission with titleKey not in i18n | open missions | row title = generic localized fallback, never the raw key | `mission-row-{code}` | |
| GAM-FE-TC-131 | Error + retry | state/error | P1 | child + forced `useMyMissions` failure | open missions | `missions-error` + `missions-retry` refetch | `missions-error` | |
| GAM-FE-TC-132 | Mission row a11y + progressbar role | a11y | P1 | seeded missions | open missions | each row accessible with progress/target/xp; bar has `progressbar` role + value | `mission-row-{code}` | |
| GAM-FE-TC-133 | Reduced motion: bars at target, no pulse | a11y/motion | P2 | seeded missions + reduced-motion | open missions | bars render at target instantly; no completed-flip pulse | `missions-daily` | |

---

## 7. League screen (`app/(child)/league.tsx`) — P4-07

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-140 | League screen renders loading/error/empty/standings | state | P0 | child (render-only) | `goto /(child)/league` | one of `league-loading` / `league-error` / `league-empty` / (`league-banner`+`league-standings`) shown | `league-screen` | |
| GAM-FE-TC-141 | Unplaced/empty state (no league this week) | state | P1 | brand-new child (no weekly XP / not placed) | open league | `league-empty` (dimmed trophy + copy), not an error | `league-empty` | |
| GAM-FE-TC-142 | Tier banner shows tier + countdown + promote count | functional | P1 | **seeded child placed in a league w/ standings** | open league | `league-banner` shows tier name, time-left sub, "Top N promote"; `league-countdown` chip present | `league-banner` | |
| GAM-FE-TC-143 | Standings list ranked; you-row highlighted | functional | P0 | **seeded league with multiple members incl. the child** | open league | `league-standings` rows sorted by rank; `league-you-row` highlighted (border/bg) | `league-you-row` | |
| GAM-FE-TC-144 | Anonymization: only "Student #N", no real names | a11y/privacy | P0 | seeded league with other members | open league | every non-you row name is server-anonymized ("Student #N" style); NO real child names/emails/PII | `league-standings` | |
| GAM-FE-TC-145 | Promotion/demotion cutlines render at cutoffs | functional | P1 | seeded league w/ promotion+demotion cutoffs | open league | `league-zone-promotion` after last promoted rank; `league-zone-demotion` before first demoted rank | `league-zone-promotion` | |
| GAM-FE-TC-146 | Single-member week honest hint (no fake rivals) | boundary | P2 | **seeded league with only the child** | open league | `league-alone-hint` shown; no fabricated opponent rows | `league-alone-hint` | |
| GAM-FE-TC-147 | Weekly XP Latin+LTR; rank Eastern-Arabic in AR | RTL-i18n | P1 | seeded standings, ar | open league ar | row weekly XP Latin digits + LTR; rank numbers Eastern-Arabic; `dir=rtl` | `league-you-row` | |
| GAM-FE-TC-148 | Zone arrows (↑/↓) never mirror in RTL | RTL-i18n | P2 | seeded league, ar | open league ar | promotion/demotion arrows keep vertical semantics (not horizontally mirrored) | `league-zone-promotion` | |
| GAM-FE-TC-149 | Auto-scroll brings you-row into view on mount | functional | P2 | **seeded league where child rank is lower down** | open league | you-row visible in viewport after mount (auto-scroll) | `league-you-row` | |
| GAM-FE-TC-150 | Error + retry | state/error | P1 | child + forced `useMyLeague` failure | open league | `league-error` + `league-retry` refetch | `league-error` | |
| GAM-FE-TC-151 | Banner + rows a11y labels | a11y | P1 | seeded standings | open league | `league-banner` aria-label (tier/time/promote); rows have rank/name/xp a11y; you-row distinct a11y | `league-banner` | |

---

## 8. Events: streak-freeze, timed events & weekly challenges (`app/(child)/events.tsx`) — P4-11 / P4-12

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-160 | Events screen renders loading/error/sections | state | P0 | child (render-only) | `goto /(child)/events` | one of `events-loading`/`events-error`/(`events-freeze`+`events-timed`+`events-challenges`) shown | `events-screen` | |
| GAM-FE-TC-161 | Freeze section: balance + earned/zero note (no spend UI) | functional | P1 | child (zero) and **seeded child w/ freezeBalance>0** | open events both | `events-freeze` shows ❄️ count; earned-note when >0, zero-note when 0; NO buy/spend button | `events-freeze` | |
| GAM-FE-TC-162 | Timed-events empty card when none active | state | P0 | child, no active event | open events | `events-timed-empty` card shown (not error/spinner) | `events-timed-empty` | |
| GAM-FE-TC-163 | Active timed-event banner: name + multiplier + countdown | functional | P0 | **seeded active timed event** (admin TimedEvents create+activate) | open events | `event-banner` shows localized name, "×N XP" (Latin+LTR), countdown ends-in text | `event-banner` | |
| GAM-FE-TC-164 | Join-by-playing state (no participation row) | state | P1 | seeded active event, child not yet participating | open events | `event-banner-join` label visible; NO progress bar (`event-banner-progress-bar` absent) | `event-banner-join` | |
| GAM-FE-TC-165 | In-progress state shows progress bar + label | functional | P1 | **seeded: child has participation row status=1** (needs contribution seeding) | open events | `event-banner-progress-bar` visible; `event-banner-progress-label` shows "X of Y" prose | `event-banner-progress-bar` | |
| GAM-FE-TC-166 | Completed state: full bar + completed label | functional | P1 | **seeded: child participation status=2** (needs contribution seeding) | open events | bar at 100% (solid); ✓ disc; completed label | `event-banner-progress-bar` | |
| GAM-FE-TC-167 | Completion celebration (RewardPopup, no "+0 XP") | functional/motion | P1 | **seeded: participation flips to completed across a refetch** | trigger completion → refetch | RewardPopup fires with non-numeric "event complete!" copy; NO "+0 XP" text anywhere on screen | (RewardPopup root) | |
| GAM-FE-TC-168 | Multiple events: overflow "+N more" line | boundary | P2 | **seeded ≥3 active timed events** | open events | first 2 banners shown; `events-timed-more` "+N more" line present | `events-timed-more` | |
| GAM-FE-TC-169 | Ended event drops off (no stale banner) | boundary | P2 | **seeded event with endUtc in the very near past/edge** | open events; wait minute tick | ended event filtered out; timed section falls to empty card or remaining events | `events-timed` | |
| GAM-FE-TC-170 | Weekly-challenge cards (CHALLENGE_ rows only) | functional | P1 | **seeded child with CHALLENGE_ weekly mission** | open events | `events-challenges` shows `weekly-challenge-card`(s) w/ purple chrome, progress + reward; these are the rows missions tab excludes | `weekly-challenge-card` | |
| GAM-FE-TC-171 | Challenges empty state when none active | state | P1 | child, no challenges | open events | `events-challenges-empty` card shown | `events-challenges-empty` | |
| GAM-FE-TC-172 | Challenges section error isolated from freeze/timed | state/error | P1 | child + forced `useMyMissions` failure only | open events | `events-challenges-error` + `events-challenges-retry`; `events-freeze`/`events-timed` still render | `events-challenges-error` | |
| GAM-FE-TC-173 | Whole-screen error (dashboard failure) + retry | state/error | P1 | child + forced dashboard failure | open events | `events-error` + `events-retry` refetch | `events-error` | |
| GAM-FE-TC-174 | RTL (ar): name Arabic, prose Eastern-Arabic, ×N Latin | RTL-i18n | P0 | seeded active event, ar | open events ar | event `nameAr` shown; countdown/progress prose Eastern-Arabic; "×N XP" Latin+LTR; bars LTR-locked; `dir=rtl` | `event-banner` | |
| GAM-FE-TC-175 | LTR (en): name English | RTL-i18n | P2 | seeded active event, en | switch en → open events | event `nameEn` shown; `dir=ltr` | `event-banner` | |
| GAM-FE-TC-176 | No raw i18n keys on events (ar + en) | RTL-i18n | P1 | child | open events ar + en | no visible text matching `^(events|common)\.` | `events-screen` | |
| GAM-FE-TC-177 | Freeze/banner a11y labels | a11y | P1 | seeded event | open events | `events-freeze` aria-label (count+explainer); `event-banner` aria-label state-aware (join/in-progress/completed) | `event-banner` | |

---

## 9. Cross-screen product-rule & global checks

| ID | Title | Type | Pri | Precondition / seed | Steps | Expected | testID | Status |
|---|---|---|---|---|---|---|---|---|
| GAM-FE-TC-190 | RTL default on all gamification screens | RTL-i18n | P0 | child | open each of xp/streak/hearts/missions/badges/league/events in ar | `document.documentElement.dir === 'rtl'` on each; screen root visible | (per screen root) | |
| GAM-FE-TC-191 | Locale switch mid-flow flips every screen to LTR | RTL-i18n | P1 | child | switch en, revisit each screen | `dir=ltr` everywhere; copy in English; no layout breakage | (per screen root) | |
| GAM-FE-TC-192 | No raw i18n keys across all gamification screens | RTL-i18n | P0 | child | visit every screen ar + en | no visible text matching the namespace-key regex on any screen | (per screen root) | |
| GAM-FE-TC-193 | Only one celebration popup at a time (no chain) | state/motion | P2 | **seeded: multiple reward events in one dashboard refresh** | trigger combo (e.g. level-up + badge) | exactly ONE RewardPopup shows (highest priority: level-up→badge→streak→mission) | (RewardPopup root) | |
| GAM-FE-TC-194 | No teacher surfaces anywhere | product-rule | P1 | child | inspect all screens/nav | no teacher tab/role/route exposed | (none) | |
| GAM-FE-TC-195 | Reduced-motion honored globally (confetti off) | a11y/motion | P2 | child + reduced-motion + seeded celebration | trigger any celebration | RewardPopup confetti self-gates off; popup still legible/dismissable | (RewardPopup root) | |
| GAM-FE-TC-196 | Dark mode renders without contrast breakage | visual | P2 | child + dark scheme emulated | visit each screen | screens render in dark tokens; no white-on-white / unreadable text | (per screen root) | |

---

### Implementer notes (how the runner fills this)
- **Status column values:** `PASS` / `FAIL` / `BLOCKED` / `SKIP` — and for FAIL/BLOCKED add a defect id + one-line reason in `execution-report.md`.
- **Reuse existing specs** where they already cover a case (see `coverage-report.md` "overlap" section) — extend rather than rewrite `carryover-d1.spec.ts` (nav + four-state smoke) and `P4-12-timed-event-participation.spec.ts` (timed-event states + seeding helpers).
- **Forced-failure cases** (`*-error`): use Playwright `page.route(...)` to abort/500 the specific endpoint (dashboard / `useMyBadges` / `useMyMissions` / `useMyLeague` / participations) so section-isolation cases are deterministic.
- **Celebration cases** are inherently timing-sensitive (diff across two dashboard/participation refreshes). If not deterministically triggerable in-harness, mark `BLOCKED` with the reason (mirrors the existing `4p` blocked note in `carryover-d1.spec.ts`) rather than dropping them.
