# Design Spec — B0-nav · Child app bottom TabBar (the ONE approved new pattern, plan L3)

> Carryover backlog `docs/plans/p1-p2-p3-carryover.md` batch **2b** (B0-nav). This is the app-wide
> navigation shell for `apps/student-app/app/(child)/` that the Wave-B gamification screens hang off.
> **Lead-approved per CLAUDE.md rule 8 (plan decision L3)** — the only new visual pattern in this backlog.
> No app/Tamagui code here; the `frontend` agent builds from this. Default locale **Arabic**, default theme **dark**.
> Final tab wiring (routes to B4/B5/B6 screens) is owned by batch **3c (B-int)**; 2b ships the bar + route stubs.

## 0. Source-of-truth pairs

| Piece | LTR capture | RTL capture | Preview card(s) |
|---|---|---|---|
| TabBar (floating glass pill) | `design-system/screenshots/mobile/08-home.png` (bottom), `mobile/16-league.png`, `mobile/17-badges.png`, `mobile/15-daily-mission.png`, `mobile/18-hearts.png` | `design-system/screenshots/mobile-ar/08-home.png`, `mobile-ar/16-league.png`, `mobile-ar/17-badges.png` | `design-system/preview/mobile-tabbar.html` (authoritative pixel values) |
| Composed reference | `design-system/ui_kits/student-mobile/index.html` (`TabBar` in `MobileComponents.jsx`, `showsTabBar` allowlist) | `ui_kits/student-mobile/index-ar.html` | — |

**Captures vs product scope (overrides applied):** the mock tab set is `Home / Skills / Quests / League / Me`.
"Skills" (a global skill-tree tab) and "Me" (child profile) have **no in-scope screen** in this backlog — the
subjects+tree live inside Home → subject detail (`subjects/[subjectId]`, W11), and a child profile screen is not
scoped. "Quests" is renamed to **Missions** (brand-law tab inventory: Home / Skills / Missions / League / Profile;
the B5 screen is the missions screen). See §1 for the decided tab set and the deviation flags.

---

## 1. Tab inventory (proposal — lead confirms at Gate 2)

**4 tabs**, logical order (first = logical start; in RTL that's the right edge):

| # | key | route (`app/(child)/`) | icon (emoji, per `mobile-tabbar.html` precedent) | label EN | label AR | i18n key |
|---|---|---|---|---|---|---|
| 1 | `home` | `index` (existing dashboard) | 🏠 | Home | الرئيسية | `nav.tabs.home` |
| 2 | `missions` | `missions` (B5 screen; 2b ships a stub) | 🎯 | Missions | المهام | `nav.tabs.missions` |
| 3 | `league` | `league` (B6 screen; stub) | 🏆 | League | الدوري | `nav.tabs.league` |
| 4 | `badges` | `badges` (B4 screen; stub) | 🏅 | Badges | الشارات | `nav.tabs.badges` |

Rationale (grounded in the captures + scope):
- **Home** keeps the W13 dashboard exactly as-is (subjects list lives inside it — that's why there is no "Skills/Learn" tab; a Learn tab would duplicate Home's primary content).
- **Missions / League / Badges** are the three Wave-B screens that need a persistent entry (B5/B6/B4). 🎯 and 🏆 are in the semantic emoji set (mission, trophy/league).
- **Streak (B2), Hearts (B3), XP/Level (B1), My activity (A5-child)** are NOT tabs — they are reached by tapping the corresponding HUD chip / dashboard widget (see P4-08 spec §1). This preserves "one primary action per screen" and keeps the bar ≤5 items.
- **Flagged deviations from the capture:** ① "Skills" tab dropped (absorbed by Home); ② "Me" tab dropped (no child profile screen in scope — reserve the 5th slot; when a profile story lands, `profile` 👤 slots in at position 5 without redesign); ③ "Quests" → "Missions" (brand-law naming + B5 copy); ④ 🏅 for Badges is an addition to the semantic emoji set (the capture's badge screens use medal artwork; 🏆 is taken by League) — **lead/twemoji review**.

## 2. Anatomy & tokens (transcribed from `preview/mobile-tabbar.html`)

Floating glass pill, centered, overlaying the scroll content:

| Property | Value | Token |
|---|---|---|
| Container max-width | 380px, centered (`margin: 0 auto`); side inset 20px from screen edge below 420px | — (card literal) |
| Height | 64px | — (card literal; ≥48px touch target per item) |
| Background | `rgba(15,23,42,0.85)` + `backdrop-filter: blur(20px)` | derived from `$bg` `--lx-bg` at 0.85; glass allowed — floating overlay (brand law 5) |
| Border | 1px `rgba(255,255,255,0.08)` | `$border` / `--lx-border` |
| Radius | **22px** | card literal (sits between `--lx-radius-card` 20 and `--lx-radius-modal` 24 — the preview card is the pixel authority; do not "round" to a bucket) |
| Shadow | `0 8px 32px rgba(0,0,0,0.5)` | card literal (heavier than `--lx-shadow-float` because it floats over content; keep verbatim) |
| Item layout | column, icon over label, `gap: 2px`, items `justify-content: space-around` | — |
| Icon | emoji glyph, 22px | render via Twemoji on web/Android (README iconography) |
| Label | 10px / weight 700 / `font-family: var(--lx-font-display)` (Poppins; **Cairo** in AR) | `--lx-weight-bold` |
| **Active** item | label + icon tint `#A5B4FC`; emoji full-color | `$primaryLight` (token exists; card literal `#A5B4FC`) |
| **Inactive** item | label `#64748B`; emoji `filter: grayscale(0.6) opacity(0.7)` (native fallback: wrap emoji in `opacity 0.55`) | `$fg4` |
| Press | scale **0.95**, 80ms; release spring back | brand law 10; `motion.durations.fast` |
| Hover (web) | brighten label to `$fg2`, scale 1.02 — never darken | brand law 10 |
| Focus (web keyboard) | `--lx-focus-ring` (2px `$primary` + 4px `$primaryGlow`) on the item | `shadows`/`$borderFocus` |
| Active-change motion | icon pop `scale 1 → 1.15 → 1` with `--lx-ease-spring`, ~240ms (`motion.durations.base`); label color cross-fade 120ms. Reduced-motion: color change only, no scale | `motion.easings.easeSpring` |

Badges/notifications dots on tabs: **not in scope** (no unread model this wave).

## 3. Placement, safe-area, and how it wraps the existing stack

- **Pattern:** convert `app/(child)/_layout.tsx` from `<Stack>` to expo-router `<Tabs>` with a **custom `tabBar`**
  component (`apps/student-app/app/(child)/_components/ChildTabBar.tsx`) rendering §2. This is the approved L3
  pattern — build exactly this, no additional nav nesting.
- Non-tab screens stay registered in the same group with `href: null` (not reachable from the bar):
  `subjects/[subjectId]`, `lessons/[lessonId]`, and the Wave-B secondary screens `streak`, `hearts`, `xp`,
  `attempts` (A5-child). Back behavior unchanged (they push on top of the active tab).
- **Visibility rules:**
  - Visible on the 4 tab roots and on `subjects/[subjectId]` (the capture `mobile/09-skill-tree.png` keeps the bar).
  - **Hidden on `lessons/[lessonId]`** (lesson player/quiz = focus mode, one primary action; captures `11-lesson`/`12-quiz` show no bar) and hidden while `RewardPopup` overlays are up (popup scrim covers it anyway).
- **Safe-area:** bar bottom offset = `max(insets.bottom, 12px)`; content ScrollViews on tab screens add
  `paddingBottom: 64 + 24 + insets.bottom` so the last card never hides under the bar (the existing
  `(child)/index.tsx` `paddingBottom: insets.bottom + 24` must grow by the bar height — **B-int applies this on
  Home**; new screens bake it in from day one).
- **Web PWA (≥768 and desktop):** keep the SAME floating bottom bar, centered at max-width 380 — do **not**
  convert to a sidebar (this is the student game world, not the parent dashboard; the kit has no child sidebar).
  `position: fixed; bottom: max(env(safe-area-inset-bottom), 12px); left:0; right:0` with the inner pill centered.
  Keyboard nav: items are buttons in a `tablist` (`role="tablist"`/`tab`, `aria-selected`), arrow-key traversal
  follows logical order.
- **Native:** absolute-positioned over the screen content (RN view, no `position: fixed`); same insets math.
- **Android back:** hardware back on a non-home tab returns to Home tab first (standard Tabs behavior — keep default).

## 4. RTL

- `dir="rtl"` flips the row once — tab #1 (Home) sits at the **right** edge in AR. Do not add `row-reverse` (double-flip; same rule as the parent shell).
- Labels: **Cairo** (display family in AR), same 10px/700.
- Emoji glyphs are **not mirrored** (SKILL.md RTL rule 7).
- AR copy above is final (cheat-sheet vocabulary: المهام per *مهمة اليوم* family, الشارات per *الشارات*).

## 5. States & a11y

| State | Treatment |
|---|---|
| Active tab | §2 active row; `accessibilityState={{ selected: true }}` |
| Inactive | §2 inactive row |
| Stub route (B4/B5/B6 not built yet, batch-2 window) | Tab navigates to a stub screen: centered 🎯/🏆/🏅 glyph 40px + `lx-h3` title (screen name) + `lx-body-sm` `$fg3` "Coming soon" line — reuse the exact MatchingPanel-stub chrome (dashed `$borderStrong` tile on `$cardSoft`). Removed by B-int. |
| Disabled | none (tabs never disable) |
| a11y | each item ≥48×48 hit area (the 64px bar provides it); `accessibilityRole="tab"`, label = localized tab name; active announced via `selected`. Bar itself `accessibilityRole="tablist"`. |

## 6. Data

None — pure navigation. (Future: mission-complete dot would read `useDashboard().dailyMissions` — out of scope.)

## 7. Implementation handoff

| Piece | Target |
|---|---|
| `ChildTabBar` component (custom tabBar) | `apps/student-app/app/(child)/_components/ChildTabBar.tsx` (screen-local; promote to `packages/ui` only if the parent app ever needs it — it won't) |
| Tabs conversion + `href:null` registrations | `apps/student-app/app/(child)/_layout.tsx` (batch 2b is the SOLE owner; 3c finalizes) |
| Route stubs `missions.tsx` / `league.tsx` / `badges.tsx` | `apps/student-app/app/(child)/` (replaced by Wave-B screens in batch 3) |
| i18n keys `nav.tabs.*` (4 keys, en+ar) | `packages/shared/src/i18n/resources.ts` (namespace owner per plan §3) |
| Tokens | all existing — **no new tokens** |

## 8. Design gaps / open questions

1. **Tab set confirmation** (§1): 4 tabs, Skills+Me dropped, 🏅 added for Badges — lead sign-off at the 1d/Gate-1 spec review (cheap to change before 2b builds).
2. The 22px radius and `0 8px 32px rgba(0,0,0,0.5)` shadow are preview-card literals outside the token buckets — kept verbatim (pixel rule). If the team prefers tokenizing, add `--lx-radius-tabbar`/`--lx-shadow-tabbar` later; not required now.
3. Emoji-as-icons substitution (README flag) applies — Twemoji on web/Android for consistency.

Design spec ready for frontend.
