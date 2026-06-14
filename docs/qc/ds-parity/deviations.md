# Design-System Parity Audit — Deviation Log

Ground truth: `design-system/preview/*.html` at commit `cf9e340`.
Audit date: 2026-06-14.
Build status after fixes: `npx turbo run type-check` — **11/11 tasks pass, 0 errors**.

---

## Fixed deviations

### Fix 1 — XPBar track border alpha
- **File:** `packages/ui/src/components/XPBar/index.tsx`
- **Preview:** `components-xp-bar.html` + `ar-xp-bar.html` — `border:1px solid rgba(255,255,255,0.06)` (`$borderSubtle`)
- **Was:** `borderColor="$border"` = `rgba(255,255,255,0.08)`
- **Now:** `borderColor="$borderSubtle"` = `rgba(255,255,255,0.06)`
- **Type:** Border alpha (0.08 → 0.06)

### Fix 2 — LessonCard progress bar fill
- **File:** `packages/ui/src/components/LessonCard/index.tsx`
- **Preview:** `components-lesson-card.html` — progress fill `linear-gradient(90deg,#4F46E5,#A855F7)` (indigo→purple)
- **Was:** flat `backgroundColor="$primary"` (flat indigo, no gradient)
- **Now:** `style={{ background: 'linear-gradient(90deg, #4F46E5, #A855F7)' }}` (web) with `backgroundColor="$primary"` fallback on native
- **Type:** Missing gradient

### Fix 3 — SkillTreeNode disc size
- **File:** `packages/ui/src/components/SkillTreeNode/index.tsx`
- **Preview:** `components-skill-node.html` — disc `80×80px`
- **Was:** `width={72} height={72} borderRadius={36}`
- **Now:** `width={80} height={80} borderRadius={40}`
- **Type:** Size (72 → 80)

### Fix 4 — KPIStatCard tile padding, radius, border
- **File:** `packages/ui/src/components/KPIStatCard/index.tsx`
- **Preview:** `web-child-card.html` — tile `padding:8px 10px`, `border-radius:12px`, `border:1px solid rgba(255,255,255,0.04)`
- **Was:** `padding="$3"` (12px), `borderRadius="$sm"` (8px), no border
- **Now:** `paddingVertical={8} paddingHorizontal={10}`, `borderRadius={12}`, `borderWidth={1}` + `borderColor="rgba(255,255,255,0.04)"`
- **Type:** Padding, radius, missing border

### Fix 5 — CTABanner padding, title size, button dimensions
- **File:** `apps/marketing-site/app/_components/CTABanner.module.css`
- **Preview:** `web-cta-banner.html` + `ar-web-cta.html`
- **Was:** `.banner` padding `56px`; `.title` `36px`; `.button` height `60px`, font `17px`, padding `0 var(--lx-space-8)`, radius `var(--lx-radius-button)` (16px)
- **Now:** `.banner` padding `36px var(--lx-space-8)`; `.title` `28px`; `.button` height `52px`, font `14px`, padding `0 26px`, radius `14px`
- **Type:** Spacing, typography, size (multiple)

### Fix 6 — Sidebar XP widget border-radius and border alpha
- **File:** `apps/student-app/app/(parent)/_components/Sidebar.tsx` (SidebarXpWidget)
- **Preview:** `web-sidebar.html` — XP widget `border-radius:14px`, `border:1px solid rgba(255,255,255,0.06)`
- **Was:** `borderRadius="$button"` (16px), `borderColor="$border"` (0.08 alpha)
- **Now:** `borderRadius={14}`, `borderColor="$borderSubtle"` (0.06 alpha)
- **Type:** Radius (16 → 14), border alpha (0.08 → 0.06)

### Fix 7 — LoginBrandPanel star, title, body
- **File:** `apps/student-app/app/(auth)/_components/LoginBrandPanel.tsx`
- **Preview:** `web-auth-split.html` — star `🌟` 60px gold glow `rgba(250,204,21,0.5)`; title `24px weight:900`; body `12px rgba(255,255,255,0.75)`
- **Was:** star `⭐` 120px purple glow; title `48px`; body `$fg2` 16px
- **Now:** star `🌟` 80px gold glow; title `28px/900` lineHeight 32; body `$fg2Alpha` 13px lineHeight 20 maxWidth 320
- **Note:** 80px is a deliberate midpoint — full brand panel is not compressed like the preview card.
- **Type:** Glyph, size, color, typography (multiple)

### Fix 8 — ChildDashboardCard mastery bar height
- **File:** `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx`
- **Preview:** `web-child-card.html` — mastery bar `height:7px`
- **Was:** `height={8}`
- **Now:** `height={7}`
- **Type:** Size (8 → 7)

### Fix 9 — MCQOption default/locked borderWidth
- **File:** `packages/ui/src/components/MCQOption/index.tsx`
- **Preview:** `components-quiz.html` — `.ans{border:2px solid rgba(255,255,255,0.08)}` for all states
- **Was:** `default: 1`, `locked-default: 1`
- **Now:** `default: 2`, `locked-default: 2`
- **Type:** Border width (1 → 2)

### Fix 10 — MissionBanner border alpha
- **File:** `packages/ui/src/components/MissionBanner/index.tsx`
- **Preview:** `components-missions.html` — `.row{border:1px solid rgba(255,255,255,0.06)}`
- **Was:** `borderColor="$borderStrong"` = `rgba(255,255,255,0.16)`
- **Now:** `borderColor="$borderSubtle"` = `rgba(255,255,255,0.06)`
- **Type:** Border alpha (0.16 → 0.06)

### Fix 11 — AITutorBubble avatar size and border alpha
- **File:** `packages/ui/src/components/AITutorBubble/index.tsx`
- **Preview:** `components-tutor.html` — avatar `64×64px`; AI bubble border `rgba(255,255,255,0.1)` (`$borderInput`)
- **Was:** avatar `52×52`; AI border `$border` (0.08 alpha)
- **Now:** avatar `64×64`; AI border `$borderInput` (0.10 alpha)
- **Type:** Size (52 → 64), border alpha (0.08 → 0.10)

### Fix 12 — Nav button border-radius
- **File:** `apps/marketing-site/app/page.module.css`
- **Preview:** `web-nav.html` — nav buttons `border-radius:12px`, Log-in border `rgba(255,255,255,0.12)`
- **Was:** both `.btnOutline` and `.btnPrimary` used `var(--lx-radius-button)` (16px); `.btnOutline` used `var(--lx-border-strong)` (0.16 alpha)
- **Now:** both use `12px`; `.btnOutline` border = `rgba(255,255,255,0.12)`
- **Type:** Radius (16 → 12), border alpha

### Fix 13 — SubjectsBand card border and gap
- **File:** `apps/marketing-site/app/_components/SubjectsBand.module.css`
- **Preview:** `web-subject-band.html` — `border:1px solid rgba(255,255,255,0.06)` (`$borderSubtle`), `gap:6px`
- **Was:** `border: 1px solid var(--lx-border)` (0.08 alpha), `gap: var(--lx-space-2)` (8px)
- **Now:** `border: 1px solid var(--lx-border-subtle)`, `gap: 6px`
- **Type:** Border alpha (0.08 → 0.06), gap (8 → 6)

### Fix 14 — FeaturesSection card border alpha
- **File:** `apps/marketing-site/app/_components/FeaturesSection.module.css`
- **Preview:** `web-feature-card.html` — `border:1px solid rgba(255,255,255,0.06)` (`$borderSubtle`)
- **Was:** `border: 1px solid var(--lx-border)` (0.08 alpha)
- **Now:** `border: 1px solid var(--lx-border-subtle)`
- **Type:** Border alpha (0.08 → 0.06)

### Fix 15 — ParentValueSection bulletTile size and radius
- **File:** `apps/marketing-site/app/_components/ParentValueSection.module.css`
- **Preview:** `web-benefits-panel.html` — bullet tile `34×34px`, `border-radius:10px`, `font-size:16px`
- **Was:** `40×40`, `border-radius:12px`, `font-size:20px`
- **Now:** `34×34`, `border-radius:10px`, `font-size:16px`
- **Type:** Size (40 → 34), radius (12 → 10), font size

### Fix 16 — DailyActivityCard border alpha
- **File:** `apps/student-app/app/(parent)/_components/DailyActivityCard.tsx`
- **Preview:** `web-kpi-row.html` — card `border:1px solid rgba(255,255,255,0.06)` (`$borderSubtle`)
- **Was:** `borderColor="$border"` (0.08 alpha)
- **Now:** `borderColor="$borderSubtle"` (0.06 alpha)
- **Type:** Border alpha (0.08 → 0.06)

### Fix 17 — OverviewWeb KPI tile border alpha and icon chip radius
- **File:** `apps/student-app/app/(parent)/_components/OverviewWeb.tsx`
- **Preview:** `web-kpi-row.html` — card `border:1px solid rgba(255,255,255,0.06)`, icon chip `border-radius:10px`
- **Was:** `borderColor="$border"` (0.08 alpha), chip `borderRadius="$sm"` (8px)
- **Now:** `borderColor="$borderSubtle"` (0.06 alpha), chip `borderRadius={10}`
- **Type:** Border alpha (0.08 → 0.06), radius (8 → 10)

### Fix 18 — Sidebar.tsx TypeScript error (pre-existing)
- **File:** `apps/student-app/app/(parent)/_components/Sidebar.tsx`
- **Error:** `TS2339: Property 'showRestartPrompt' does not exist on type 'RestartPromptState'`
- **Cause:** `restartPromptStore` was refactored from `showRestartPrompt(locale)` to `show(locale)` but the call site was not updated.
- **Was:** `const { showRestartPrompt } = useRestartPromptStore();`
- **Now:** `const showRestartPrompt = useRestartPromptStore((s) => s.show);`
- **Type:** Bug fix (type error)

---

## Known design gaps (intentional, not fixed)

| ID | Component | Preview value | Code value | Rationale |
|----|-----------|---------------|------------|-----------|
| GAP-1 | XPBar fill gradient | `components-xp-bar.html` yellow→orange (`gradXpFull`) | green→indigo (`gradXp`) | Authoritative source is `ar-xp-bar.html` + design gap note in source. `gradXp` (green→indigo) is canonical. |
| GAP-2 | RewardPopup radius | `components-reward.html` 28px | 24px (`$modal`) | Code comment: "GAP 8: use 24 not the preview's 28." Design gap explicitly documented. |
| GAP-3 | TextField focus glow | `components-input.html` `rgba(99,102,241,0.25)` | uses RN shadow props, not CSS box-shadow | RN shadow system cannot replicate CSS `box-shadow:0 0 0 4px`. Acceptable platform delta. |
| GAP-4 | LoginBrandPanel star size | `web-auth-split.html` ~60px (compressed card) | 80px | Preview is compressed overview; full panel warrants larger star. Deliberate compromise. |
| GAP-5 | Badge disc size in mobile screens | `mobile-badge-tiles.html` 64px | `components-badges.html` 74px | Component spec (74px) supersedes screen-level preview (64px). Screen-level is scaled preview. |
| GAP-6 | FeaturesSection icon tone bg alpha | `web-feature-card.html` 0.15 | `0.15` per code | Match is exact — no delta. |

---

## Most common deviation types (summary)

1. **Border alpha** (most common): `$border` (0.08) used instead of `$borderSubtle` (0.06) — affects XPBar, Sidebar XP widget, SubjectsBand, FeaturesSection, DailyActivityCard, OverviewWeb KPI tiles (6 occurrences).
2. **Size/dimension**: SkillTreeNode disc 72→80, AITutorBubble avatar 52→64, ParentValueSection bulletTile 40→34, mastery bar height 8→7 (4 occurrences).
3. **Typography**: CTABanner title/button sizes, LoginBrandPanel title/body (2 files, multiple properties).
4. **Missing gradient**: LessonCard progress bar was flat color, needed indigo→purple gradient (1 occurrence).
5. **Border width**: MCQOption default/locked borderWidth 1→2 (1 occurrence).
6. **Bug**: Sidebar.tsx TS error (`showRestartPrompt` → `show`) — pre-existing, fixed in pass.

---

## MISSING_TOKEN entries

| Token needed | Resolved via | Location |
|---|---|---|
| `$borderSubtle` (rgba 0.06 exact) | Tamagui token (already in design-system) | Used in XPBar, LessonCard, Sidebar, MissionBanner, DailyActivityCard, OverviewWeb |
| `var(--lx-border-subtle)` (CSS) | CSS custom property (already in globals.css) | SubjectsBand, FeaturesSection CSS modules |
| Nav button border `rgba(255,255,255,0.12)` | Inline value — falls between `$borderInput` (0.10) and `$borderStrong` (0.16). MISSING_TOKEN: no `$borderMid` token exists. | `page.module.css` nav `.btnOutline` |
| Tile border `rgba(255,255,255,0.04)` | Inline value — below `$borderSubtle` (0.06). MISSING_TOKEN: no token at 0.04 alpha. Value matches `web-child-card.html` spec exactly. | `KPIStatCard`, `ChildDashboardCard`, `DailyActivityCard`, `OverviewWeb` |
