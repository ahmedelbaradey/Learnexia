# Design Spec — W13 Student Home Dashboard (P2-09-FE, Phase 2 FE closer)

> Wave 13 is **P2-09-FE** — turn the bare W11 Subjects list at `apps/student-app/app/(child)/index.tsx` into a personalized **home dashboard**: greeting + mascot + Hearts/Streak/XP strip → optional **Continue Card** to resume → existing 4-subject list rendered below as a section. This is the design spec the `frontend` agent builds verbatim. Token-only, AR/RTL-first, dark-default. Mirrors the structure + grammar of `W11-subjects-tree.md` and `W12-lesson-quiz.md`.

## 0. Source-of-truth pairs

| Surface / piece | LTR capture | RTL capture | Composing preview cards |
|---|---|---|---|
| Dashboard header (chips strip) | `design-system/screenshots/mobile/08-home.png` | `design-system/screenshots/mobile-ar/05-home.png` + `design-system/screenshots/mobile-ar/08-home.png` | `preview/mobile-home-topbar.html`, `preview/ar-home-topbar.html` |
| Continue Card | (derived from the `08-home` "Continue Learning" rail; we ship a single full-width card, not the 2-up rail) | (derived from `mobile-ar/05-home.png` "واصل التعلم" rail) | `preview/components-lesson-card.html`, `preview/ar-lesson-card.html`, `preview/components-skill-node.html` (boss chrome) |
| MissionBanner (built, not rendered W13) | (derived from `08-home` "Daily Quests" tile) | (derived from `mobile-ar/05-home.png` "مهمة اليوم" tile) | derived; chrome mirrors `preview/components-lesson-card.html` |
| Subjects list section (preserved from W11) | `design-system/screenshots/mobile/06-subject-select.png` | `design-system/screenshots/mobile-ar/06-subject-select.png` | `preview/mobile-subject-rows.html` |
| Sign-out (preserved from W11) | (same) | (same) | (ghost text — no card) |

**Captures vs Phase-2 actually-shipping scope (RECONCILE — Pipeline Brief OQ10):**
- `08-home.png` (EN) shows: 4-chip strip (streak / hearts / star-XP `1240` / gem `42`), avatar + "Welcome back, Sami!" greeting, **Continue Learning** rail with two subject tiles (Math 60%, Science 35%) + "See all", **Daily Quests** list with XP rewards + progress bars, and a **bottom tab bar** (Home / Skills / Quests / League / Me). **W13 drops:** gem chip, level pill, the 2-up Continue rail (we ship **one** Continue Card), the Daily Quests list (Mission is `null` in Phase 2 — banner built, not rendered), the bottom tab bar (Phase-4 shell once Skills/Quests/League exist).
- `mobile-ar/05-home.png` + `mobile-ar/08-home.png` (AR) show: level pill "المستوى ٥", days-streak pill "٧ أيام", big XP bar with "٧٨٠ / ١٠٠٠" + "٧٨٪ للوصول للمستوى ٦" caption, a "مهمة اليوم" (today's mission) card with 3 sub-tasks + Start CTA, and a "واصل التعلم" rail. **W13 drops:** the level pill (no level concept until P4-02), the "٧٨٪ للوصول للمستوى ٦" caption + "+٢٢٠ نقطة متبقية" sub-caption (no target until P4-02; XP renders `0 / 100` stub), the mission sub-tasks (Mission `null` in Phase 2). We keep the XP bar chrome from `preview/ar-home-topbar.html` verbatim — just at zero-state values.
- Both captures show a "Welcome back, {name}" framing — we keep that voice. The W11 `child.subjects.title` "Subjects" H1 is **demoted** to a section eyebrow ("Your subjects" / "موادك") because the dashboard header now owns the H1 visual slot.
- The mascot owl in the captures is flagged as a placeholder in `design-system/README.md` — we render the existing `logoMark` asset (W11 precedent) on mobile and reserve the owl swap for when the asset lands.

**Captures vs product overrides (carried from W11):**
- 4 subjects only — Math / Science / Arabic / English. Defensive 4-subject filter from `apps/student-app/app/(child)/_components/subjects.ts` (W11) survives intact in `SubjectsListSection`.
- No teacher role. No Social Studies. Parent-driven onboarding — greeting source is `useMe().data.fullName.split(/\s+/)[0]`.

---

## 1. Surfaces & layout

### Single route — `apps/student-app/app/(child)/index.tsx`

The W11 Subjects list screen is **wrapped**, not replaced. The composition top-to-bottom (logical, top safe-area first):

```
TopBar (preserved from W11 — logoMark on logical leading edge, Sign out on trailing edge)
  ↓ 16px
DashboardHeader (NEW — greeting row + Hearts/StreakFlame/XPBar strip)
  ↓ 24px
ContinueCard (NEW — conditional: only when dashboardQuery.data?.continue !== null)
  ↓ 24px (when ContinueCard present) / 24px (when absent — collapses cleanly)
MissionBanner (NEW — conditional: dashboardQuery.data?.dailyMission !== null. Phase 2 = always null = NOT rendered)
  ↓ 24px (when present)
SubjectsListSection (EXTRACTED from W11 — "Your subjects" eyebrow + 4 SubjectRows + W11 grade/empty/error/loading paths)
  ↓ bottom safe-area + 24px
```

### Layout — 390 portrait (mobile primary)

- Top safe-area + 16px top padding (same as W11/W12).
- Horizontal screen padding: `24px` (`$space-6`) — matches W11.
- `ScrollView`, vertical only, `showsVerticalScrollIndicator={false}`.
- Bottom safe-area + 24px content padding.
- **One primary action per screen** — the Continue Card. When `continue === null`, the primary visual hierarchy falls through to the SubjectsListSection (which is informational, not a single CTA). This is intentional — there is no "Nothing in progress yet" empty-state CTA in W13 (Pipeline Brief OQ5).

### Layout — 768 / 1024 (Expo web responsive, parity with W12)

- Same vertical stack.
- Container `maxWidth: 720`, `marginHorizontal: 'auto'` (centered), 24px horizontal padding preserved.
- At 1024+ the stack stays single-column — this is a student app, not a parent dashboard; multi-column composition is reserved for the parent surface (P5-01).
- Continue Card and Mission Banner are full-width inside the 720 container; the Subjects section keeps the same 4-row vertical stack (no grid promotion).

### Loading composition

While `meQuery.isLoading || dashboardQuery.isLoading || subjectsQuery.isLoading` is true (any of the three), the screen renders shimmer placeholders for each present block:

- **Header skeleton:** greeting line shimmer (40px tall, 60% width) + chips strip shimmer (3 chip-pills, each 64×32, 8px gap).
- **Continue Card skeleton:** full-width card placeholder, 96px tall, `$cardSoft` background, no inner content. Always rendered during loading (we don't know yet whether `continue` will be null).
- **Mission Banner skeleton:** NOT rendered during loading — Phase 2 always returns `null`, so skipping the skeleton avoids a layout shift on `dashboardQuery` resolution.
- **Subjects skeleton:** preserved W11 shimmer (4 SubjectRow placeholders, 10px gap).

### Error composition

When `dashboardQuery.isError`:

- Inline error strip rendered **between** the DashboardHeader (which still renders with zero-state values — XP 0, Streak 0, Hearts 3) and where the ContinueCard would be. Strip chrome mirrors W12's network-error strip (§3.5):
  - Row, padding `12px 16px`, gap 12, radius `$radius-card-inner` 14.
  - Leading-edge bar: 3px `$danger` (logical `borderStartWidth`).
  - Leading glyph: ⚠️ 18px on a 32×32 disc, bg `rgba(239,68,68,0.28)`, fg `$danger`.
  - Body column: title 14/700 `$fg1` = `child.home.errorRetry` ("Couldn't load your dashboard. Try again" / "تعذّر تحميل لوحتك. أعد المحاولة"), no sub-line.
  - Trailing: ghost-button `Button variant="ghost" size="sm"` label "Retry" / "أعد المحاولة" → calls `dashboardQuery.refetch()`.
- The SubjectsListSection underneath **still renders** normally (it owns its own subjectsQuery state). The error is scoped to the dashboard band.
- When the user retries and `dashboardQuery` resolves, the strip unmounts (200ms fade), and the ContinueCard / MissionBanner placeholders run their normal conditional rendering.

### Empty composition (no continue + no subjects)

When `dashboardQuery.data?.continue === null` AND `filteredSubjects.length === 0` (genuinely empty — grade null or seeder returned nothing):

- DashboardHeader still renders (greeting + zero-state chips).
- Continue Card hidden (per AC2 — no empty shell, no "Nothing in progress" placeholder).
- A single **welcome empty-state tile** replaces the SubjectsListSection content:
  - Centered, padding 32 vertical, 24 horizontal.
  - Mascot slot: `logoMark` asset 64×64 (placeholder; final mascot owl will swap in 1:1).
  - Title: 18/700 `$fg2` `child.home.welcomeEmpty` = "Welcome! Tap a subject to start." / "أهلاً! اختر مادة لتبدأ."
  - The Subjects section eyebrow is still rendered ABOVE the empty-state tile so the screen doesn't feel decapitated.
- This empty path is **strictly** for the grade-null OR zero-subjects degenerate case — once W11's grade resolves with the 4 product subjects, this branch never fires.

### Sign-out (preserved from W11)

- Lives in the TopBar's trailing slot — small ghost-style `Text` ("Sign out" / "تسجيل الخروج"), 13/500 `$fg3`, minHeight 48 (a11y tap target floor). Tap → `useSignOutAction()`.
- NOT a primary action; styling is intentionally quiet.
- The DashboardHeader does NOT duplicate sign-out — single source.

---

## 2. State design — composition rules

### DashboardHeader (single rendered state, internal data fan-out)

There is no state-machine for the header chrome. The three child stat-widgets each carry their own visual state from their existing primitive contracts. The Pipeline Brief Phase-2 contract pins:

| Field | Source | Phase 2 value | Phase-4 wire-up |
|---|---|---|---|
| `childName` | `useMe().data.fullName.split(/\s+/)[0]` | live | unchanged |
| `grade` | `useMe().data.grade` | live | unchanged |
| Hearts `current` | hard-coded `3` | always `3` | TODO P4-05 |
| StreakFlame `days` | `dashboardQuery.data?.streak ?? 0` | always `0` | TODO P4-03 |
| XPBar `currentXp` | `dashboardQuery.data?.xp ?? 0` | always `0` | TODO P4-02 |
| XPBar `totalXp` | hard-coded `100` | always `100` | TODO P4-02 (weekly aggregation) |
| XPBar `level` | hard-coded `1` | always `1` | TODO P4-02 |

Inline `// TODO P4-XX` comments are mandatory at each call-site so the Phase-4 wire-up doesn't have to spelunk for them.

### ContinueCard (3 mounted states; Boss orthogonal)

| State | Source | Rendered? | Notes |
|---|---|---|---|
| **hidden** | `dashboardQuery.data?.continue === null` OR `dashboardQuery` not yet resolved as data | NO mount | clean — no empty shell |
| **available** | `continue.nodeState === 1` (Available — from `ContinueTargetDto.NodeState`) | YES — primary chrome | `$primary` border, glow, "Continue" CTA |
| **resumed** | `continue.nodeState === 2` (Completed — defensive; BE may still resolve a Completed lesson when no Available exists) | YES — quiet chrome | 1px `$borderStrong`, no glow, CTA reads "Replay" / "إعادة" |
| **Boss-on-X** (orthogonal) | `continue.isBoss === true` | overlay on the above | adds `BossBadge` pill at trailing-top corner; primary CTA gains 🔥 prefix glyph |

**State chrome map (single-card variant — NOT the 2-up rail from `08-home.png`):**

| Element | available | resumed |
|---|---|---|
| Background | `$card` (`#1E293B`) | `$card` |
| Border | 2px `$primary` | 1px `$borderStrong` |
| Shadow / glow | `$shadow-primary-glow` (`0 8px 24px rgba(99,102,241,0.45)`) | `$shadow-soft` (`0 4px 12px rgba(0,0,0,0.15)`) |
| Eyebrow color | `$primaryLight` (`#A5B4FC`) | `$fg3` |
| Eyebrow copy | `child.home.continue.eyebrow` ("Pick up where you left off" / "أكمل من حيث توقفت") | "Replay last lesson" / "أعد آخر درس" (i18n `child.home.continue.eyebrowReplay`) |
| Title color | `$fg1` | `$fg1` |
| Subject icon tile | tinted per `subjectKey` (W11 map — Math `$primary` / Science `$success` / Arabic `$accent` / English `$purple`) | same |
| CTA label | `child.home.continue.cta` ("Continue" / "متابعة") | `child.home.continue.replayCta` ("Replay" / "إعادة") |
| CTA glyph | logical trailing chevron `→` LTR / `←` RTL (per SKILL.md cheat sheet) | logical trailing chevron |
| Boss badge | top-trailing pill `👑 Boss` / `👑 بوس` (reuse W11 `Badge variant="boss"`) | same — Boss-Completed still shows the badge |

**Locked never appears on the ContinueCard** — the BE only resolves an Available or Completed lesson into `ContinueTargetDto`. If somehow a Locked one arrives, the FE treats it as Available chrome (defensive); the design does NOT add a fourth state.

### MissionBanner (3 declared states; only **hidden** ever renders in W13)

| State | Source | Rendered W13? | Phase-4 chrome |
|---|---|---|---|
| **hidden** | `dashboardQuery.data?.dailyMission === null` (Phase 2 always) | YES — short-circuit, primitive never mounts | n/a |
| **in-progress** | `dailyMission !== null && dailyMission.progress.current < dailyMission.progress.total` | NO (Phase 4) | `$primary` border, progress bar, "Continue Mission" CTA |
| **completed** | `dailyMission !== null && dailyMission.progress.current >= dailyMission.progress.total` | NO (Phase 4) | `$success` border, no CTA, "Mission Complete!" caption |

The primitive ships in `@learnexia/ui` so P4-06 turns it on by deleting the short-circuit check and trusting the contract. **No "Coming soon" placeholder** rendered in W13 — primitive simply isn't mounted.

### SubjectsListSection (preserved from W11)

- Loading: shimmer 4-row stack (W11 path).
- Error: W11 inline retry path, scoped to subjectsQuery.
- Empty (grade null OR 4-filter strips everything): W11 empty path — replaced in W13 by the **welcome empty-state tile** described in §1 "Empty composition" when BOTH dashboardQuery.continue is null AND subjects is empty. When only subjects is empty (continue is non-null), the W11 empty-state survives unchanged (preserves W13 fail-safe).
- Default (loaded): "Your subjects" / "موادك" eyebrow (12/700 uppercase `$fg3`, tracking `0.04em`) + 4 SubjectRow stack, 10px gap. The W11 H1 "Subjects" / "المواد" is **removed** at this surface — the dashboard's greeting line is the new H1.

---

## 3. Net-new primitives — `@learnexia/ui` spec

All under `packages/ui/src/components/<Name>/index.tsx` with the standard barrel re-export under a `// --- W13 home dashboard primitives ---` banner in `packages/ui/src/index.ts`. Tokens only, logical RTL, kid-a11y baseline.

### 3.1 `DashboardHeader`

**Composed from:** `preview/mobile-home-topbar.html` (EN chip strip), `preview/ar-home-topbar.html` (AR chip strip + XP card), `screenshots/mobile/08-home.png` greeting block (kept), `screenshots/mobile-ar/05-home.png` greeting + chip block (kept).

**Reconciliation against `ar-home-topbar.html`:** the preview card composes BOTH a chip row (streak + level) AND an XP card below it (with target + remaining caption). W13 ships the chip-strip layout from the EN card + the XP card chrome from the AR card, MINUS the level pill (no level concept until P4-02) and MINUS the "+٢٢٠ نقطة متبقية" caption (no target until P4-02). The XP card chrome reduces to: 10px-tall bar, radius pill, `$bg` `#0F172A` track bg with `borderSubtle` border, fill = `$gradXp` (`linear-gradient(90deg, #22C55E, #4F46E5)`), `direction: 'ltr'` wrapper (progress L→R per brand law).

**Props (TS):**
```ts
export interface DashboardHeaderProps {
  /** First-name token from useMe — e.g. "Sami". Empty string when unknown — caller pre-derives. */
  childName: string;
  /** Grade from useMe — 1..6. Undefined hides the caption. */
  grade?: number | null;
  /** Hearts widget current count. Phase 2 = always 3. */
  hearts: number;
  /** Hearts max — default 3 (matches W12 lesson intro precedent). */
  heartsMax?: number;
  /** Streak days. Phase 2 = always 0 (StreakFlame still renders with day-zero copy). */
  streakDays: number;
  /** Weekly XP earned. Phase 2 = always 0. */
  weeklyXp: number;
  /** Weekly XP target. Phase 2 = hard-coded 100. */
  weeklyXpTarget: number;
  /** XPBar level — Phase 2 = hard-coded 1. */
  weeklyLevel?: number;
  /** Optional mascot/logo asset on the logical leading edge. Caller passes assets.logoMark. */
  mascotSrc?: ImageSourcePropType;
  /** Already-composed a11y label for the chip strip, e.g. "3 hearts, 5 day streak, 320 XP". Caller pre-localizes. */
  statsAccessibilityLabel: string;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Render the shimmer skeleton variant — greeting line + chip-strip outlines, no content. */
  loading?: boolean;
  testID?: string;
}
```

**Dimensions:**
- Outer block: column, gap 16 (`$space-4`).
- **Greeting row** (row, `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `alignItems: 'center'`, gap 12):
  - Mascot tile (leading): 48×48, radius `$button` 16, `$primarySoft` bg, centered `Image source={mascotSrc}` 32×32 contain. When `mascotSrc` is undefined, render a fallback 🦉 emoji 28px centered (semantic — owl = tutor; mascot-owl is the brand placeholder per `README.md`).
  - Greeting column (flex 1):
    - Greeting H1: `child.home.greeting` ("Hi, {{childName}}!" / "مرحبا {{childName}}!") — 24/800 Poppins/Cairo `$fg1`, `lineHeight: 1.2`, `letterSpacing: -0.01em`, `numberOfLines: 1`. When `childName` is empty, falls back to `child.home.welcomeBack` ("Welcome back" / "أهلاً بعودتك"). The H1 carries `accessibilityRole="header"`.
    - Grade caption: `child.home.gradeCaption` ("Grade {{grade}}" / "الصف {{grade}}") — 13/500 Poppins/Tajawal `$fg3`, `marginTop: 2`. Omitted when `grade` is null/undefined. AR uses Eastern-Arabic numerals (`الصف ١`).
- **Stats strip** (row, `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `alignItems: 'center'`, gap 10, `justifyContent: 'flex-start'`):
  - `<Hearts current={hearts} maxHearts={heartsMax ?? 3} accessibilityLabel={...} />` — uses existing primitive's compact rendering (the W12 lesson intro pattern).
  - `<StreakFlame days={streakDays} size="sm" accessibilityLabel={...} />` — existing primitive. Phase-2 zero-state renders with day-0 chrome (`StreakFlame` already supports days=0 — flame glyph dimmed, count `0`).
  - `<XPBar currentXp={weeklyXp} totalXp={weeklyXpTarget} level={weeklyLevel ?? 1} accessibilityLabel={...} animated={false} />` — existing primitive. `animated={false}` so the bar renders at target on mount (no fill animation for zero-state). Flex 1 to take remaining horizontal space.
  - The whole row is wrapped in `accessibilityLabel={statsAccessibilityLabel}` at the parent `View` level so a single screen-reader pass announces "3 hearts, 0 day streak, 0 XP this week" in one breath; individual widgets remain focusable for granular SR navigation.

**Skeleton (`loading: true`):**
- Greeting row: mascot tile renders as solid `$cardSoft` block. Greeting H1 replaced by a 60%-width × 24px shimmer bar; grade caption omitted.
- Stats strip: three shimmer pills, each 72×32, radius pill, 10px gap.
- No motion on shimmer beyond the W11 sweep (1200ms linear infinite).

**Motion:**
- Mount-only fade-in: `opacity 0 → 1` over **240ms** `$ease-out`. No translate. Honours `prefers-reduced-motion` (opacity unchanged, no transition).
- No hover. No press. The header is informational, not interactive.

**A11y:**
- Greeting H1: `accessibilityRole="header"`.
- Stats strip wrapper: `accessibilityLabel={statsAccessibilityLabel}`, `accessibilityRole="group"` (web); each child widget retains its own role/label (`Hearts`, `StreakFlame`, `XPBar` all already comply).
- Mascot tile: `accessibilityElementsHidden` (decorative; the greeting carries the meaning).

---

### 3.2 `ContinueCard`

**Composed from:** `preview/components-lesson-card.html` chrome (card radius + glow), `preview/components-skill-node.html` boss-pill chrome, `screenshots/mobile/08-home.png` "Continue Learning" rail (composed as a single full-width card, NOT the 2-up).

**Props (TS):**
```ts
export interface ContinueCardProps {
  /** Localized subject name from ContinueTargetDto.subjectName. */
  subjectName: string;
  /** Resolved key for tint — 'math' | 'science' | 'arabic' | 'english'. Caller maps from subjectName via the W11 SUBJECT_NAME_MAP. */
  subjectKey: 'math' | 'science' | 'arabic' | 'english';
  /** Localized lesson name from ContinueTargetDto.lessonName. */
  lessonName: string;
  /** Optional unit name from ContinueTargetDto.unitName — rendered as 2nd-line caption. Undefined hides the line. */
  unitName?: string;
  /** Optional skill name from ContinueTargetDto.skillName — rendered as eyebrow context. Undefined falls back to default eyebrow. */
  skillName?: string;
  /** Render the boss badge + 🔥 prefix on the CTA when true. */
  isBoss?: boolean;
  /** From ContinueTargetDto.nodeState — 1=Available (primary chrome), 2=Completed (quiet chrome). */
  nodeState?: 1 | 2;
  /** Optional 0..100 progress percent. Not exposed by BE in Phase 2 (always undefined). Reserved for Phase 3 P3-08. */
  progressPercent?: number;
  /** Tap handler — caller passes router.push to /(child)/lessons/{lessonId}?subjectId={subjectId}. */
  onPress: () => void;
  /** Already-localized a11y label, e.g. "Continue lesson Compare Bigger and Smaller". */
  accessibilityLabel: string;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions:**
- Outer card: full-width minus 48 horizontal screen padding, padding `18px 20px`, gap 14 (row gap), radius `$radius-modal` 24 (deliberately larger than `$radius-card` 20 — hero affordance, matches W11 LessonCard convention), border per state (§2), bg `$card`, shadow per state (§2). `position: relative` (for boss badge overlay).
- Row layout: `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `alignItems: 'center'`, gap 14.
- **Subject icon tile (leading):** 52×52, radius `$button` 16, centered glyph 26px, color = subject `fg` token, bg = subject `soft` token (reuse W11 subject tint map):

| `subjectKey` | fg | soft | glyph |
|---|---|---|---|
| `math` | `$primary` (`#4F46E5`) | `$primarySoft` (`rgba(79,70,229,0.18)`) | 🧮 |
| `science` | `$success` (`#22C55E`) | `$successSoft` (`rgba(34,197,94,0.18)`) | 🧪 |
| `arabic` | `$accent` (`#F59E0B`) | `$warningSoft` (`rgba(245,158,11,0.18)`) | 📖 |
| `english` | `$purple` (`#A855F7`) | `$purpleSoft` (`rgba(168,85,247,0.18)`) | 🔤 |

- **Body column (flex 1):**
  - **Eyebrow:** 11/700 uppercase, tracking `0.04em`, color per state (`$primaryLight` / `$fg3`). Copy = `child.home.continue.eyebrow` ("Pick up where you left off" / "أكمل من حيث توقفت") for Available, `child.home.continue.eyebrowReplay` for Completed. When `skillName` is non-null, eyebrow becomes "{skillName} · pick up where you left off" (caller composes).
  - **Lesson title:** 16/800 Poppins/Cairo `$fg1`, `lineHeight: 1.2`, `numberOfLines: 2`, `marginTop: 4`.
  - **Unit caption** (when `unitName` set): 12/500 Poppins/Tajawal `$fg3`, `marginTop: 2`, `numberOfLines: 1`.
- **CTA chip (trailing):** pill, padding `8px 14px`, radius `$radius-pill` 9999, height 36 (a11y floor — but the whole card is the tap target, so this is visual only), bg per state:
  - Available: `$primary` (`#4F46E5`) fill, label `$fg1` (`#F8FAFC`) 13/700, `child.home.continue.cta` ("Continue" / "متابعة") + logical trailing chevron `→` LTR / `←` RTL.
  - Completed: transparent bg with 1.5px `$borderStrong` border, label `$fg2` 13/700, `child.home.continue.replayCta` ("Replay" / "إعادة") + chevron.
  - Boss-on-Available: label gains 🔥 prefix glyph (`🔥 Continue` / `🔥 متابعة`) — kid-friendly + factually accurate.
- **Boss badge overlay** (when `isBoss === true`): positioned `top: 14, end: 14` (logical), reuse `Badge variant="boss"` from W11 — pill, padding `4px 10px`, radius pill, 1px `$streak` border, bg `$streakSoft` (`rgba(251,146,60,0.13)`), label 10/800 uppercase `$streak` "👑 Boss" / "👑 بوس".
- **Progress bar (omitted W13):** when `progressPercent` is undefined (always in Phase 2), no bar renders. The primitive accepts the prop for Phase-3 wiring (P3-08); when set, render an 8px tall pill below the body column, bg `$bg`, fill `$gradXp` for in-progress / flat `$success` for `progressPercent === 100`. Wrap fill in `direction: 'ltr'` (brand law).

**Min height:** 80 (the row content lands at ~76; padding 18 × 2 + content brings it to ~88. Comfortable above the 48 floor — the whole card is the tap target).

**Motion:**
- Hover (web only): brighten 8% (bg → `$cardSoft`), scale 1.02, **160ms** `$ease-out` — call this the "hover lift". Never darken. Respects `prefers-reduced-motion` (drop scale; brighten only).
- Press: scale 0.95, 80ms. Same on Completed-variant.
- Focus: `$focus-ring` (2px `$primary` + outer 4px `$primaryGlow`).
- No idle motion. No pulse. The card is a primary CTA but the dashboard mount itself has the celebratory framing — pulsing here would compete with the mascot greeting.

**A11y:**
- `accessibilityRole="button"`.
- `accessibilityLabel` required (caller composes via `child.home.continueA11y` ("Continue lesson {{lesson}}" / "متابعة درس {{lesson}}")). When `isBoss`, caller appends ", boss" / "، بوس".
- `accessibilityHint` (optional): "Opens the lesson player" / "يفتح مشغّل الدروس".
- Touch target: full card ≥ 80px tall × full width — far above the 48 floor.

---

### 3.3 `MissionBanner` (built, NOT rendered in W13)

**Composed from:** derived chrome — mirrors the LessonCard outer (`preview/components-lesson-card.html`) at a quieter intensity, with a Daily Quests-style progress bar from `screenshots/mobile/08-home.png` "Daily Quests" tile + the AR "مهمة اليوم" card from `screenshots/mobile-ar/05-home.png`.

**Props (TS):**
```ts
export interface MissionBannerProps {
  /** Localized mission title — e.g. "Solve 10 math problems" / "حل ١٠ مسائل رياضيات". */
  title: string;
  /** Optional reward XP from DailyMissionDto.rewardXp. When 0 or undefined, hide the badge. */
  rewardXp?: number;
  /** Optional progress object. Caller pre-formats current/total to Eastern numerals when AR. */
  progress?: { current: number; total: number };
  /** Tap handler — caller routes per Phase-4 contract. Omit to render the banner non-interactive. */
  onPress?: () => void;
  /** Already-localized a11y label. */
  accessibilityLabel: string;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions (Phase-4 chrome — captured here so P4-06 ships this primitive unchanged):**
- Outer card: full-width, padding `16px 18px`, gap 12, radius `$radius-card` 20 (one step quieter than ContinueCard's `$radius-modal` 24 — the mission is supportive, not the hero), border 1px `$borderStrong`, bg `$card`, shadow `$shadow-soft`. `position: relative`.
- **Row layout** (row, `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `alignItems: 'center'`, gap 12):
  - Leading 🎯 glyph on a 44×44 disc, bg `$accent` at 0.18 alpha (`rgba(245,158,11,0.18)`), fg `$accent` 24px.
  - Body column (flex 1):
    - Eyebrow 11/700 uppercase tracking `0.04em` `$accent`: `child.home.mission.eyebrow` ("Today's mission" / "مهمة اليوم").
    - Title 16/800 `$fg1`, `lineHeight: 1.2`, `numberOfLines: 2`.
    - Progress bar (when `progress` set): row gap 8, `flexDirection={isRtl ? 'row-reverse' : 'row'}`. Bar fills flex 1, 6px tall, radius pill, `$bg` track, fill `$accent` (linear), wrapped in `direction: 'ltr'` (brand law); caption to trailing side "`{current} / {total}`" 12/700 `$fg2`, `fontVariant: ['tabular-nums']`. AR Eastern numerals.
  - Trailing: XP reward badge (when `rewardXp` set + > 0): `Badge variant="xp"` "+{rewardXp} XP" / "+{rewardXp} نقطة". When `onPress` set, replace badge with `Button variant="primary" size="sm"` "Start Mission" / "ابدأ المهمة" (per SKILL.md AR copy).

**Render path in W13:**
- `dashboardQuery.data?.dailyMission === null` → screen short-circuits BEFORE constructing `<MissionBanner>`. The primitive is never mounted.
- No "Coming soon" placeholder. No layout shim. The 24px gap before SubjectsListSection is anchored to the ContinueCard's bottom (or to the DashboardHeader's bottom when ContinueCard is also hidden).

**Motion (deferred wire-up — Phase 4):**
- Hover/press: same brighten + scale pattern as ContinueCard (160ms / 80ms).
- Optional Phase-4 pulse on the 🎯 disc when `progress.current === 0` (untouched mission of the day) — designer call later.

**A11y:** `accessibilityRole="button"` when `onPress` set, else `accessibilityRole="text"`. Label includes title + progress + reward.

---

### 3.4 `SubjectsListSection` (extracted from W11 — **inline app component, NOT in `@learnexia/ui`**)

**Promotion decision:** keep INLINE under `apps/student-app/app/(child)/_components/SubjectsListSection.tsx`. The component owns app-specific concerns:
- `useMe` grade resolution.
- `useSubjectsForGrade(grade)` query orchestration.
- W11 `SUBJECT_NAME_MAP` defensive 4-subject filter.
- W11 routing — `router.push('/(child)/subjects/' + dto.id)`.

These are not generalizable surfaces (per CLAUDE.md rule 8 — mirror existing shapes, don't generalize prematurely). The `SubjectRow` primitive it composes is already in `@learnexia/ui`.

**Props (TS):**
```ts
export interface SubjectsListSectionProps {
  /** Optional override — caller can short-circuit grade resolution. Default reads from useMe internally. */
  grade?: number | null;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** When true, the whole section renders as shimmer skeletons (parent's loading union). */
  loading?: boolean;
  testID?: string;
}
```

**Layout:**
- Column, gap 12.
- **Section eyebrow:** "Your subjects" / "موادك" (`child.home.yourSubjects`) — 12/700 Cairo/Poppins uppercase, tracking `0.04em`, `$fg3`. Self-align logical start.
- **SubjectRow stack:** vertical, 10px gap, 4 rows (or W11 empty-path empty-state when filter yields 0 + grade is null).

**State paths (all preserved from W11):**
- Loading (parent's union OR internal subjectsQuery.isLoading): 4 shimmer SubjectRow placeholders.
- Error (subjectsQuery.isError): inline "Couldn't load" + Retry CTA (W11 path unchanged).
- Empty (grade null OR 4-filter strips all): W11 empty-state ("Coming soon — no lessons yet" / "قريباً — لا توجد دروس بعد"). NOTE: when ALSO `dashboardQuery.data?.continue === null`, the parent screen replaces this empty path with the W13 welcome empty-state tile (§1).
- Default: 4 `SubjectRow`s.

**Behavior preserved unchanged:** grade resolution, SUBJECT_NAME_MAP normalization (including Arabic + Mathematics aliases), canonical sort order (Math → Science → Arabic → English), routing, `accessibilityLabel` composition per row.

---

## 4. Tokens (full table — every reference, no raw hex)

### 4.1 Colors

| Use | Token | Resolved | Notes |
|---|---|---|---|
| Screen canvas | `$bg` | `#0F172A` | brand-law dark default |
| Cards (DashboardHeader bg is transparent — sits on `$bg`; ContinueCard, MissionBanner outer) | `$card` | `#1E293B` | |
| Hover state (ContinueCard brighten on web) | `$cardSoft` | `#334155` | step lighter, never darker |
| Welcome empty-state tile bg | `$cardSoft` | (reuse) | quietly lighter than card |
| H1 greeting, title, score numbers | `$fg1` | `#F8FAFC` | |
| Mission progress caption, body | `$fg2` | `#CBD5E1` | |
| Grade caption, eyebrows, unit caption, sign-out | `$fg3` | `#94A3B8` | |
| Subject icon tile fg (math), ContinueCard available border + CTA bg | `$primary` | `#4F46E5` | |
| Subject icon tile soft (math), mascot tile bg | `$primarySoft` | `rgba(79,70,229,0.18)` | |
| Available CTA active hover, focus ring inner | `$primary` | (reuse) | |
| Focus ring outer glow | `$primaryGlow` | `rgba(99,102,241,0.45)` | composes into `$shadow-primary-glow` and `$focus-ring` |
| ContinueCard available glow | `$shadow-primary-glow` | `0 8px 24px rgba(99,102,241,0.45)` | brand law: indigo glow on CTA only |
| Eyebrow available, CTA highlight | `$primaryLight` | `#A5B4FC` | |
| Subject icon tile fg (science) | `$success` | `#22C55E` | |
| Subject icon tile soft (science), Completed CTA chip context | `$successSoft` | `rgba(34,197,94,0.18)` | |
| Subject icon tile fg (arabic), MissionBanner accent glyph + bar | `$accent` | `#F59E0B` | |
| Subject icon tile soft (arabic), MissionBanner glyph disc bg | `$warningSoft` | `rgba(245,158,11,0.18)` | |
| Subject icon tile fg (english) | `$purple` | `#A855F7` | |
| Subject icon tile soft (english) | `$purpleSoft` | `rgba(168,85,247,0.18)` | |
| BossBadge border + label | `$streak` | `#FB923C` | reuse from W11 |
| BossBadge fill | `$streakSoft` | `rgba(251,146,60,0.13)` | |
| Error strip chrome (border + glyph) | `$danger` | `#EF4444` | |
| Error strip bg | `$dangerSoft` | `rgba(239,68,68,0.18)` | flagged in W12 — same token reused |
| XPBar fill, summary celebration framing | `$gradXp` | `linear-gradient(90deg, #22C55E 0%, #4F46E5 100%)` | brand-law named gradient |
| Card border | `$border` | `rgba(255,255,255,0.08)` | |
| Completed ContinueCard border | `$borderStrong` | `rgba(255,255,255,0.16)` | |
| Subtle hairline (XPBar track border) | `$borderSubtle` | `rgba(255,255,255,0.06)` | |
| Skeleton shimmer mid-tone | `$cardSoft` | (reuse) | W11 pattern |

### 4.2 Spacing — all from `$space-*`

| Use | Token | Px |
|---|---|---|
| Compact gaps (chip gap, glyph-label gap, eyebrow→title) | `$space-2` | 8 |
| Stats strip gap, eyebrow→body gap | `$space-2-half` (= 10 inline) | 10 |
| ContinueCard body column gap, MissionBanner inner column gap | `$space-3` | 12 |
| Section row gap, mascot→greeting column gap, DashboardHeader greeting row gap | `$space-3-half` (= 14 inline) | 14 |
| ContinueCard padding horizontal, MissionBanner padding | `$space-4` (vertical) / inline 18 (horizontal) | 16/18 |
| ContinueCard padding vertical (18) / DashboardHeader internal gap (16) | `$space-4` | 16 |
| Section-to-section vertical gap (DashboardHeader → ContinueCard, ContinueCard → SubjectsListSection) | `$space-6` | 24 |
| Outer screen horizontal padding | `$space-6` | 24 |
| Empty-state tile vertical padding | `$space-8` | 32 |

(Spacing tokens 10 and 14 are inline-literal where Tamagui doesn't expose a half-step — flagged consistent with W11 / W12 precedent.)

### 4.3 Radii

| Use | Token | Px |
|---|---|---|
| BossBadge pill, ContinueCard CTA chip, XPBar fill, hearts/streak chips | `$radius-pill` | 9999 |
| Error strip, mascot tile, subject icon tile, MissionBanner glyph disc | `$radius-button` | 16 |
| MissionBanner outer card | `$radius-card` | 20 |
| ContinueCard outer card | `$radius-modal` | 24 |

### 4.4 Shadows / glows

| Use | Token | Resolved |
|---|---|---|
| ContinueCard resting (Completed variant), MissionBanner resting | `$shadow-soft` | `0 4px 12px rgba(0,0,0,0.15)` |
| ContinueCard hover (web) | `$shadow-float` | `0 8px 24px rgba(0,0,0,0.25)` |
| ContinueCard Available variant | `$shadow-primary-glow` | `0 8px 24px rgba(99,102,241,0.45)` |
| Focus ring (every interactive primitive) | `$focus-ring` | `0 0 0 2px $primary, 0 0 0 6px rgba(99,102,241,0.30)` |

No new shadow tokens proposed in W13 — all reused from W11/W12.

### 4.5 Typography

| Use | Family (EN / AR) | Size | Weight | LH | Tracking |
|---|---|---|---|---|---|
| Greeting H1 ("Hi, {name}!" / "مرحبا {name}!") | Poppins / Cairo | 24 | 800 | 1.2 | -0.01em |
| Grade caption ("Grade {n}" / "الصف {n}") | Poppins / Tajawal | 13 | 500 | 1.3 | 0 |
| Section eyebrow ("Your subjects" / "موادك"), ContinueCard eyebrow, MissionBanner eyebrow | Poppins / Cairo | 11–12 | 700 | 1.3 | 0.04em uppercase |
| ContinueCard lesson title | Poppins / Cairo | 16 | 800 | 1.2 | 0 |
| ContinueCard unit caption | Poppins / Tajawal | 12 | 500 | 1.3 | 0 |
| ContinueCard CTA chip label | Poppins / Cairo | 13 | 700 | 1.3 | 0 |
| MissionBanner title | Poppins / Cairo | 16 | 800 | 1.2 | 0 |
| MissionBanner progress caption | Poppins / Cairo | 12 | 700 | 1.3 | 0, `fontVariant: ['tabular-nums']` |
| BossBadge label | Poppins / Cairo | 10 | 800 | 1.3 | 0.04em uppercase |
| Welcome empty-state title | Poppins / Cairo | 18 | 700 | 1.3 | 0 |
| Error strip title | Poppins / Cairo | 14 | 700 | 1.3 | 0 |
| Sign-out (preserved W11) | Poppins / Tajawal | 13 | 500 | 1.3 | 0 |
| Hearts/Streak/XP counter text inside the widgets | (per existing primitive — Poppins/Cairo, weight 800, `tabular-nums`) | per primitive | 800 | per primitive | per primitive |

Numbers (XP counter, streak days, hearts count, grade number, progress current/total) all use `fontVariant: ['tabular-nums']` + weight 800 per brand law. AR uses Eastern-Arabic numerals (٠١٢٣٤٥٦٧٨٩) for all in-line reading text. **LTR exception:** XPBar's `820 / 1000` is wrapped in `dir="ltr"` per the AR preview card (`preview/ar-home-topbar.html` line 12) — this is a technical XP counter string and stays Latin per SKILL.md Skill 4. Phase-2 zero-state value `0 / 100` follows the same rule.

---

## 5. Motion

| Element | Trigger | Spec |
|---|---|---|
| Whole dashboard | Mount | fade-in `opacity 0 → 1` over **240ms** `$ease-out`. Single fade on the outer Stack — children don't stagger. |
| DashboardHeader | Idle | static — no shimmer beyond the loading variant, no pulse on the mascot tile. (Phase-4 may add a streak-flame pulse — out of scope W13.) |
| ContinueCard | Hover (web) | brighten 8% (bg → `$cardSoft`) + scale 1.02, **160ms** `$ease-out`. The "hover lift". Never darken. |
| ContinueCard | Press | scale 0.95, 80ms. |
| ContinueCard | Focus | `$focus-ring` (2px `$primary` + 4px `$primaryGlow`). |
| ContinueCard | Idle | static — no pulse, no glow throb. |
| BossBadge | Idle | static. Phase-4 may add a flame-flicker on the 👑 glyph; W13 ships static to match the rest of the dashboard's calm framing. |
| MissionBanner | (not rendered W13) | n/a — primitive specs hover/press as per ContinueCard for the Phase-4 wire-up. |
| SubjectRow | (preserved W11) | hover brighten + scale 1.02 / press scale 0.95 / focus ring. |
| Sign out | Press | scale 0.95, 80ms (preserved W11). |
| Loading skeleton (DashboardHeader, ContinueCard placeholder, SubjectsList) | Idle | shimmer sweep 1200ms linear infinite. `$cardSoft → $border → $cardSoft` (W11 pattern). |
| Error strip | Enter | slide-down `translateY(-4 → 0)` + fade `0 → 1`, **160ms** `$ease-out`. |
| Error strip | Exit (on refetch success) | fade-out **200ms** `$ease-out`. |
| Reduced motion (`prefers-reduced-motion`) | All animations | Replace translates with fade-only. Dashboard mount fade drops to instant. ContinueCard hover keeps brighten, drops scale. Press scale drops. |

All durations ≤ 800ms (brand law). The dashboard is intentionally calm — celebratory motion is reserved for in-lesson moments (W12 Summary trophy rise, W14 confetti). The dashboard's job is "what's next?", not "wow!".

---

## 6. RTL & Arabic

- **Direction:** `dir="rtl"` driven by `useLocale().direction` (the W11 hook). All horizontally-oriented stacks use `flexDirection={isRtl ? 'row-reverse' : 'row'}` — no hardcoded `row-reverse`, no physical `marginLeft` / `marginRight` / `left` / `right` props.
- **Fonts:**
  - Display (Greeting H1, ContinueCard lesson title, MissionBanner title, eyebrows, BossBadge label, CTA chip): **Cairo** in AR, **Poppins** in EN.
  - Body (Grade caption, ContinueCard unit caption, MissionBanner progress caption, welcome empty-state title, sign-out, error strip): **Tajawal** in AR, **Poppins** in EN.
- **Mirroring:**
  - DashboardHeader greeting row: mascot tile sits on the logical leading edge (LTR left / RTL right) via `flexDirection` flip. The greeting H1 + grade caption column always sits next to the mascot, trailing.
  - DashboardHeader stats strip: row flips so the Hearts widget reads from the leading edge in AR. Within each widget (Hearts/StreakFlame/XPBar), existing primitives already handle their internal RTL contracts.
  - ContinueCard: subject icon tile on logical leading edge; CTA chip on logical trailing edge. BossBadge positioned with logical `end` (`top: 14, end: 14`) so it auto-flips.
  - ContinueCard CTA chevron glyph swap: `→` LTR, `←` RTL (per SKILL.md cheat sheet — "واصل التعلم ←").
  - MissionBanner: 🎯 glyph disc on leading edge, XP badge / Start CTA on trailing edge.
  - SubjectsListSection eyebrow: self-align logical start (`alignSelf: 'flex-start'`).
  - Welcome empty-state tile: centered (no mirroring needed).
- **NOT mirrored:**
  - Mascot/owl placeholder glyph (visual identity preserved).
  - Subject icon tile glyphs (🧮, 🧪, 📖, 🔤) — semantic, identity-preserving.
  - BossBadge crown glyph (👑) — semantic.
  - XPBar fill direction — wrapped in `direction: 'ltr'` (brand law: progress reads L→R universally; matches `preview/ar-home-topbar.html` line 14).
  - StreakFlame flame glyph (🔥) — semantic.
  - Hearts heart glyph (❤️) — semantic.
- **Numerals:**
  - Grade caption: Eastern-Arabic in AR (`الصف ١`).
  - Streak days, hearts count (in-line widget rendering): Eastern-Arabic in AR (existing primitive should already handle this; verify in implementation).
  - **LTR exception:** XPBar's `0 / 100` counter wrapped in `dir="ltr"` — keeps Latin digits per the AR preview card. Same convention as W12 `820 / 1000` and `45s` duration.
  - MissionBanner progress caption (when wired in Phase 4): Eastern-Arabic in AR (`٣ / ١٠`).
  - The brand name "Learnexia" stays Latin + `dir="ltr"` per the preview card (line 5).
- **Copy:** see Appendix §8.

---

## 7. Accessibility / kid-UX

- **Touch targets ≥ 48×48.**
  - DashboardHeader: not interactive (no tap targets needed). Sign-out (lives in TopBar) preserved at minHeight 48 (W11).
  - ContinueCard: full card is the tap target — outer card is ≥ 80px tall × full width. The CTA chip is a visual affordance, not a separate tap target.
  - MissionBanner: when rendered (Phase 4), full card ≥ 80px tall × full width. CTA "Start Mission" inside is a `Button size="sm"` — 44px tall, accepted because it's a secondary affordance inside a primary card target.
  - SubjectRow: preserved W11 — ~84px tall × full width.
- **Roles + states:**
  - DashboardHeader Greeting H1: `accessibilityRole="header"`. Caller wraps in `<Text accessibilityRole="header">`.
  - DashboardHeader stats strip: `accessibilityRole="group"` with `accessibilityLabel={statsAccessibilityLabel}` — composed via `child.home.statsA11y` ("{{hearts}} hearts, {{streak}} day streak, {{xp}} XP" / "{{hearts}} قلوب، سلسلة {{streak}} أيام، {{xp}} نقطة"). Each child widget retains its own role/label for granular SR navigation.
  - ContinueCard: `accessibilityRole="button"`, `accessibilityLabel` required (caller composes from `child.home.continueA11y` + ", boss" suffix when applicable). `accessibilityHint`: "Opens the lesson player" / "يفتح مشغّل الدروس".
  - MissionBanner: `accessibilityRole="button"` when `onPress` set, else `accessibilityRole="text"`.
  - SubjectsListSection eyebrow: `accessibilityRole="header"` (level 2).
  - SubjectRow (preserved W11): `accessibilityRole="button"`.
  - Welcome empty-state tile: `accessibilityRole="text"`, `accessibilityLabel` = full title.
  - Error strip: `accessibilityRole="alert"` (web), `accessibilityLiveRegion="polite"` (Android) — screen-readers announce "Couldn't load your dashboard. Try again" when the strip mounts. Retry button: `accessibilityRole="button"`, label = "Retry" / "أعد المحاولة".
- **Focus visibility:** every interactive primitive renders `$focus-ring` on keyboard focus (2px `$primary` + 4px `$primaryGlow` outer). Sign-out and Retry buttons preserved.
- **Color contrast:**
  - Greeting H1 `$fg1` on `$bg`: 16.4:1 (AAA).
  - Grade caption `$fg3` on `$bg`: 4.5:1 (AA, 13/500 borderline — accepted at 13 because the H1 carries the primary identity).
  - Section eyebrow `$fg3` on `$bg`: 4.5:1 (AA at 12/700 uppercase — bold + tracking pushes legibility above the floor).
  - ContinueCard lesson title `$fg1` on `$card`: ≈12:1 (AAA).
  - ContinueCard CTA chip `$fg1` on `$primary` fill: AA pass (the white-on-indigo CTA is the same pairing as the W12 Start button).
- **Reduced motion:** the 240ms dashboard fade-in drops to instant. ContinueCard hover keeps brighten, drops scale. Press scale drops. Error strip slide drops to fade-only.
- **Voice (brand law #8):**
  - Greeting: "Hi, Sami!" / "مرحبا سامي!" — friendly, single exclamation (everyday warm tone, not a fake celebration; the H1 framing makes it feel like a real "older sibling cheering you on" moment).
  - When `childName` is empty: "Welcome back" / "أهلاً بعودتك" — softer, no exclamation (we don't know who they are yet).
  - ContinueCard eyebrow: "Pick up where you left off" / "أكمل من حيث توقفت" — encouraging, no exclamation. Replay variant: "Replay last lesson" / "أعد آخر درس" — matter-of-fact.
  - ContinueCard CTA: "Continue" / "متابعة" — Title Case button label per brand law.
  - Section eyebrow "Your subjects" / "موادك" — possessive, friendly.
  - Welcome empty: "Welcome! Tap a subject to start." / "أهلاً! اختر مادة لتبدأ." — single exclamation (welcoming feels like a genuine warmth moment).
  - Error: "Couldn't load your dashboard. Try again" / "تعذّر تحميل لوحتك. أعد المحاولة" — no exclamation (errors are quiet).

---

## 8. EN + AR copy appendix

Verbatim strings the FE keys on under the existing `child.home.*` namespace (Pipeline Brief §6 confirms 4 placeholder keys from P1-09 — we replace + expand). AR strings follow SKILL.md cheat sheet voice (friendly older-sibling, second-person, encouraging).

| i18n key | EN | AR |
|---|---|---|
| `child.home.greeting` | Hi, {{childName}}! | مرحبا {{childName}}! |
| `child.home.welcomeBack` | Welcome back | أهلاً بعودتك |
| `child.home.gradeCaption` | Grade {{grade}} | الصف {{grade}} |
| `child.home.continueTitle` | (uses lesson `name` from ContinueTargetDto.lessonName; no static key — placeholder for the lesson title slot in tests) | (same) |
| `child.home.continue.eyebrow` | Pick up where you left off | أكمل من حيث توقفت |
| `child.home.continue.eyebrowReplay` | Replay last lesson | أعد آخر درس |
| `child.home.continueCta` | Continue | متابعة |
| `child.home.continue.replayCta` | Replay | إعادة |
| `child.home.continueA11y` | Continue lesson {{lesson}} | متابعة درس {{lesson}} |
| `child.home.mission.eyebrow` | Today's mission | مهمة اليوم |
| `child.home.mission.startCta` | Start Mission | ابدأ المهمة |
| `child.home.yourSubjects` | Your subjects | موادك |
| `child.home.welcomeEmpty` | Welcome! Tap a subject to start. | أهلاً! اختر مادة لتبدأ. |
| `child.home.errorRetry` | Couldn't load your dashboard. Try again | تعذّر تحميل لوحتك. أعد المحاولة |
| `child.home.errorRetryCta` | Retry | أعد المحاولة |
| `child.home.statsA11y` | {{hearts}} hearts, {{streak}} day streak, {{xp}} XP | {{hearts}} قلوب، سلسلة {{streak}} أيام، {{xp}} نقطة |
| `child.home.stats.hearts` | Hearts | قلوب |
| `child.home.stats.streak` | Streak | سلسلة |
| `child.home.stats.xp` | XP this week | نقاط الخبرة هذا الأسبوع |
| `child.home.boss` | Boss | بوس |
| `child.home.continueHintA11y` | Opens the lesson player | يفتح مشغّل الدروس |

**Reused from W11:** `child.subjects.title` is **no longer rendered** at the home dashboard surface (the dashboard greeting H1 replaces it). The key stays in resources for the Subject screen container (W11 Surface 2).

**Reused from W12 / W11 (verify still wired):** `child.subjects.signOut` ("Sign out" / "تسجيل الخروج") preserved in TopBar.

**Placeholder keys to migrate from P1-09 (cleanup):** the existing `child.home.subtitle`, `child.home.mascotMessage`, `child.home.mascotSender` keys from the W11-precedent P1-09 placeholder are **no longer used** in W13 — the FE should leave them in resources (no orphan-removal mandate this wave; deletion is a clean-up follow-up) but may delete them inline if convenient. The `child.home.greeting` key from P1-09 is **redefined verbatim** here.

---

## 9. Implementation handoff

| Piece | Target |
|---|---|
| `DashboardHeader` | `packages/ui/src/components/DashboardHeader/index.tsx` (+ export from `packages/ui/src/index.ts` under a `// --- W13 home dashboard primitives ---` banner) |
| `ContinueCard` | `packages/ui/src/components/ContinueCard/index.tsx` (+ export under same banner) |
| `MissionBanner` | `packages/ui/src/components/MissionBanner/index.tsx` (+ export under same banner) |
| `SubjectsListSection` | `apps/student-app/app/(child)/_components/SubjectsListSection.tsx` (inline — NOT promoted; moves W11 logic from `(child)/index.tsx` intact) |
| `subjects.ts` resolver (W11) | `apps/student-app/app/(child)/_components/subjects.ts` — move the SUBJECT_NAME_MAP + `resolveSubjectKey` + `filterSubjects` helpers from `(child)/index.tsx` to a sibling module so both the existing W11 surfaces and the new `SubjectsListSection` can import. (Pipeline Brief Q2 notes this is a free clean-up.) |
| Home dashboard screen (rewrite) | `apps/student-app/app/(child)/index.tsx` — replace the W11 body with the §1 composition. Preserve TopBar (logoMark + Sign out), `useMe` derivation, `useSignOutAction`, `useLocale`, safe-area handling. |
| `useDashboard` hook | `packages/api-client/src/hooks/useDashboard.ts` — implemented per Pipeline Brief §4 snippet. Exported from `packages/api-client/src/hooks/index.ts`. |
| DashboardDto + sub-DTO re-exports | `packages/api-client/src/schemas.ts` — re-export `DashboardDto`, `ContinueTargetDto`, `DailyMissionDto`, `LeaguePreviewDto`, `DashboardDtoBaseResponse` post-regen. |
| Query key (already exists) | `packages/api-client/src/query/queryKeys.ts` — `queryKeys.learning.dashboard()` already shipped in W12; no work. |
| i18n keys | `packages/shared/src/i18n/resources.ts` — under `child.home.*`, per §8 (EN + AR). |
| Eastern numeral helper wiring | Verify the W11 helper covers `{grade}` interpolation in `child.home.gradeCaption`, and the stats-strip widget internals already handle Eastern numerals for streak / xp / hearts counts. |
| BE annotation patch (prerequisite) | `backend/src/Modules/Learning/Learnexia.Modules.Learning.Api/Controllers/DashboardController.cs` — add `[ProducesResponseType(typeof(BaseResponse<DashboardDto>), 200)]` to `Get`. (Pipeline Brief §4; small + safe + additive.) |

---

## 10. Delta against captures (deliberate deviations the FE should know are intentional)

| Capture / source | Capture shows | Spec ships | Reason |
|---|---|---|---|
| `mobile/08-home.png` | 4-chip strip (streak / hearts / star-XP `1240` / gem `42`) | 3-widget strip (Hearts / StreakFlame / XPBar) | No gem chip (Phase 4 P4-02 territory); XP is the bar, not a star chip, per `ar-home-topbar.html` XP card chrome. |
| `mobile/08-home.png` | Avatar + "Welcome back, Sami!" | Mascot tile (logoMark) + "Hi, Sami!" / "مرحبا سامي!" | Mascot owl is flagged as a placeholder in `README.md`; logoMark is the W11 precedent. Greeting uses "Hi" (cheat sheet) — "Welcome back" reserved for `child.home.welcomeBack` fallback when `childName` is empty. |
| `mobile/08-home.png` | "Continue Learning" 2-up rail (Math 60%, Science 35%) + "See all" | Single full-width ContinueCard, no "See all", no progress percent | BE `ContinueTargetDto` returns ONE Continue (Pipeline Brief §2). Multi-up rail + progress percent both require Phase-3 aggregation (P3-08). |
| `mobile/08-home.png` | "Daily Quests" list with 3 quest rows + XP rewards + progress bars | MissionBanner built but NOT rendered (Phase 2 = `dailyMission === null`) | Phase 4 P4-06 territory. Primitive ships ready for wire-up. |
| `mobile/08-home.png` + `mobile-ar/08-home.png` | Bottom tab bar (Home / Skills / Quests / League / Me) | NO bottom tab bar | P2-09 (W13) does NOT include this — Skills/Quests/League screens don't exist until Phase 4. Same call as W11/W12. |
| `mobile-ar/05-home.png` | Level pill "المستوى ٥" + days-streak pill "٧ أيام" | Streak as `StreakFlame` widget; NO level pill | No level concept until P4-02. StreakFlame is the existing primitive; widget covers the affordance. |
| `mobile-ar/05-home.png` | Big XP card with "٧٨٠ / ١٠٠٠" + "٧٨٪ للوصول للمستوى ٦" + "+٢٢٠ نقطة متبقية" sub-caption | XPBar widget with `0 / 100` zero-state, NO target caption | Phase 2 BE returns `xp: 0`; no target → no "X% للوصول للمستوى" caption. P4-02 wires real values + caption. |
| `mobile-ar/05-home.png` | "مهمة اليوم" tile with 3 sub-tasks + Start CTA | MissionBanner spec captures the chrome but NOT rendered W13 | Same as EN — `dailyMission === null` in Phase 2. |
| `preview/mobile-home-topbar.html` | "Level 5 🧠" pill on the right | Pill omitted | No level concept until P4-02; pill is Phase-4 chrome. |
| `preview/ar-home-topbar.html` | "Learnexia 🌟" branding row | Branding row absent from DashboardHeader | Branding is handled by the screen's TopBar logoMark (W11 precedent); duplicating in DashboardHeader is noise. |
| `screenshots/mobile/06-subject-select.png` (Subjects H1) | "Subjects" H1 owns the top of the screen | "Subjects" demoted to "Your subjects" eyebrow inside SubjectsListSection | Dashboard greeting H1 now owns the H1 visual slot; "Your subjects" is the section eyebrow. |

---

## 11. Open questions (resolved here so FE doesn't have to ask)

1. **Phase-2 zero-state Streak — render `0 days` or hide the chip?** → **Render zero-state.** StreakFlame at `days={0}` shows the flame glyph dimmed with count `0`. Matches the BE contract decision (xp=0, streak=0); avoids primitive churn when P4-03 turns it on. (Pipeline Brief AC5.)
2. **Phase-2 zero-state XP — bar fill at 0% or hide the bar?** → **Render zero-state bar.** Empty fill, target `100`, level `1`. Matches the BE contract decision; Phase-4 turns it on by changing numbers.
3. **Hearts current value — `3` or read from `dashboardQuery`?** → **Hard-coded `3`.** Matches W12 lesson intro precedent (Pipeline Brief OQ8). Inline `// TODO P4-05` comment required at the call site.
4. **ContinueCard when `continue === null` — empty placeholder or hidden?** → **Hidden.** (Pipeline Brief OQ5: AC2.) No "Nothing in progress" placeholder; SubjectsListSection serves as the alternative CTA. Pre-Phase-4 the BE typically returns a fallback (Grade-1 Math) so `null` is rare in practice.
5. **ContinueCard tap route — exact path?** → `/(child)/lessons/{lessonId}?subjectId={subjectId}` (W12 route with the `?subjectId=` query seam added in W12 for the Summary back-stack). (Pipeline Brief AC3.)
6. **ContinueCard with `nodeState === 2` (Completed) — still render?** → **Yes, with "Replay" chrome.** The BE may resolve a Completed lesson as Continue when there's no Available lesson left in the active subject and cross-subject fallback didn't fire. Defensive — quiet chrome, "Replay" CTA. Allows the kid to revisit.
7. **MissionBanner — built or deferred to P4-06?** → **Built.** (Pipeline Brief OQ6.) Primitive ships in `@learnexia/ui` so P4-06 wires data without component churn. NOT rendered W13.
8. **Section eyebrow vs H2 for "Your subjects"?** → **Eyebrow** (12/700 uppercase tracking 0.04em, `$fg3`). The greeting H1 owns the H1 slot; subordinating Subjects to an eyebrow signals "this is the picker, not the lead". Section header `accessibilityRole="header"` keeps SR hierarchy.
9. **Welcome empty-state — when both `continue === null` AND `subjects === []`?** → render the welcome tile (§1 "Empty composition") in place of SubjectsListSection content. Single fallback; preserves the dashboard header above.
10. **Reduced-motion gate — pick up from W12 OQ?** → **Yes.** Apply to ContinueCard hover scale, dashboard mount fade, error strip slide. The W12 reduced-motion retrofit slot is fulfilled here for the primitives this wave ships; W12 primitives' retrofit is still out-of-scope (W14).
11. **Sign-out — duplicate in DashboardHeader or keep in TopBar?** → **Keep in TopBar.** Single source. Mirrors the W11 placement (`(child)/index.tsx` TopBar).
12. **Mascot tile asset — `logoMark` or `mascotOwl` (placeholder)?** → **`logoMark`** for W13. The mascot-owl is the brand placeholder (`README.md` flag); asset swap is a 1-line follow-up when the final mascot lands.

---

## 12. Design gaps logged (for design-system follow-up — NOT W13 blockers)

- **Mascot-owl asset** — final mascot needs to land. W13 ships `logoMark` as the temporary placeholder.
- **`StreakFlame` zero-state copy** — the primitive supports `days={0}` but the rendered label ("0 days" / "٠ أيام") may feel sad. Consider a Phase-4 polish that swaps to "Start your streak" / "ابدأ سلسلتك" when `days === 0`. NOT W13 work.
- **`XPBar` Phase-2 contract** — primitive expects `level` + `currentXp` + `totalXp`. Phase 2 hard-codes `level: 1`, `currentXp: 0`, `totalXp: 100`. P4-02 will need a token clarification: is `level` a per-week target (1–52) or a per-XP-tier level (1–N)? Designer flags but does NOT decide.
- **MissionBanner real renderer** — primitive spec captures the chrome but the data wire-up is P4-06. When that lands, the FE may need a new "completed" variant beyond what §2 captures (e.g. confetti on `progress.current === progress.total`).
- **Bottom tab bar** — captured but explicitly out-of-scope this wave. P4 wave that ships Skills/Quests/League screens will need to design the tab bar primitive end-to-end.
- **Gem chip / level pill** — captured but explicitly out-of-scope. P4-02 designs both.
- **`ContinueCard` progress percent (`progressPercent` prop)** — primitive accepts but BE doesn't expose. Phase-3 (P3-08) wires the per-attempt progress aggregation; spec captures the chrome for that wave's reuse.
- **`useDashboard` query invalidation** — Pipeline Brief reviewer-checklist notes the future invalidation seam (on `LessonCompletedIntegrationEvent` or its FE equivalent, P4-02). NOT W13 work; logged for HANDOFF.

Design spec ready for frontend.
