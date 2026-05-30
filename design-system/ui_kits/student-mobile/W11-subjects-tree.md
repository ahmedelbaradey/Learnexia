# Design Spec — W11 Browse Subjects + Navigate Skill Tree (student mobile)

> Wave 11 bundles **P2-02-FE** (browse subjects + lessons) and **P2-03-FE** (skill tree). This is the design spec the `frontend` agent builds verbatim. Token-only, AR/RTL-first, dark-default.

## 0. Source-of-truth pairs

| Surface | LTR capture | RTL capture | Composing preview cards |
|---|---|---|---|
| Subjects list (replaces `(child)/index.tsx`) | `design-system/screenshots/mobile/06-subject-select.png` | `design-system/screenshots/mobile-ar/06-subject-select.png` | `preview/mobile-subject-rows.html` |
| Subject screen — Lessons tab | (no dedicated capture — composed) | (no dedicated capture — composed) | `preview/components-lesson-card.html`, `preview/ar-lesson-card.html` |
| Subject screen — Skill Tree tab | `design-system/screenshots/mobile/09-skill-tree.png` | `design-system/screenshots/mobile-ar/09-skill-tree.png` | `preview/components-skill-node.html`, `preview/mobile-skill-path.html` |
| WhyLockedSheet (inline strip + bottom sheet) | (no capture — net-new) | (no capture — net-new) | derived from `preview/components-lesson-card.html` locked variant + `preview/components-tutor.html` for the bubble chrome |
| Boss decoration | derived | derived | `preview/components-skill-node.html` (boss node), `preview/mobile-skill-path.html` (Boss Challenge node) |

**Captures vs product overrides:**
- `06-subject-select.png` (LTR + RTL) shows **5 rows** including "Social Studies / الدراسات الاجتماعية". **Trim to 4** per CLAUDE.md product decision (Math / Science / Arabic / English). Defensive filter on FE.
- `09-skill-tree.png` shows an active-bottom-tab-bar that is **P2-09 (Wave 13)** territory. Wave 11 does NOT render that bar; the Lessons | Skill-Tree segmented control lives **above** the screen body (where the bar would be), not below.
- The Subjects header in `06-*` reads "Choose a Subject / Tap any subject to keep learning". This spec keeps the canonical copy from `SKILL.md` cheat sheet — Welcome back / Continue learning — but anchors the screen title to `child.subjects.title` ("Subjects" / "المواد") to match the brief's i18n key.

---

## 1. Surfaces & navigation flow

### Surface 1 — Subjects list
- **Route:** `apps/student-app/app/(child)/index.tsx` (body replaced; sign-out + `childName` derivation preserved).
- **Layout (390 portrait):**
  - Top safe-area + 24px top padding.
  - Header block:
    - H1 "Subjects" / "المواد" — Poppins/Cairo 32/800, color `$fg1`, `lineHeight: 1.15`, `letterSpacing: -0.02em`.
    - Caption "Grade {n}" / "الصف {n}" — body-sm 14/500, color `$fg3`. Only rendered when `useMe().data?.grade` is non-null. AR: Eastern-Arabic numeral (`الصف ١`).
  - 24px gap to list.
  - Vertical stack of 4 `SubjectRow` items, 10px gap between rows (matches preview `flex-direction:column; gap:10px`).
  - Sign-out button stays top-right (existing pattern from current placeholder). It is NOT a primary action — leave it ghost/icon-button styling.
- **Primary action:** tap any `SubjectRow` → `router.push('/(child)/subjects/' + subjectId)`.
- **States:**
  - Loading: 4 shimmer SubjectRow skeletons (same dims, `$cardSoft` background, no inner content).
  - Empty (grade null OR 0 subjects returned): centered "Coming soon" empty-state block — Cairo/Poppins 18/700 `$fg2` title + body-sm `$fg3` subtitle. No mascot illustration in W11 (it lives in P2-09 Wave 13).
  - Error: friendly "Couldn't load subjects" + "Try again" `Button variant="primary"` (rounded 16, glow).

### Surface 2 — Subject screen container
- **Route:** `apps/student-app/app/(child)/subjects/[subjectId]/_layout.tsx`.
- **Layout:**
  - ScreenHeader (custom — top of layout):
    - Back chevron at the leading edge (`←` LTR, `→` RTL — pointing inward), 24×24 hit area ≥48px, color `$fg2`.
    - Title block (center): subject name `$fg1` 18/700; secondary line `$fg3` 12/500 — Tree tab fills with concept name + "Unit {x} of {y} · Mastery {z}%". Lessons tab fills with `{unit-count} units · {lesson-count} lessons`.
    - Trailing slot reserved for future (empty in W11; sign-out stays on Subjects list only).
  - **Segmented control** (new primitive — see §3.6): two segments "Lessons" / "Skill Tree" — "الدروس" / "شجرة المهارات". Default = Lessons. Sits in the layout shell, persists across tab switches.
  - Outlet (the active tab's body).
- **Primary action:** the segmented control + tapping a card. One primary at a time per body.

### Surface 3 — Lessons tab
- **Route:** `apps/student-app/app/(child)/subjects/[subjectId]/index.tsx`.
- **Layout:**
  - Vertical scroll. 16px horizontal padding.
  - For each `UnitWithLessonsDto` (sorted by `sequenceOrder` asc):
    - Eyebrow row: small uppercase `Unit {sequenceOrder}` (12/700, `$fg3`, tracking 0.04em) + unit name (18/800 `$fg1`, line-height 1.2). 8px gap.
    - Vertical stack of `LessonCard`s (sorted by `sequenceOrder` asc), 12px gap between.
    - 24px gap to next unit.
  - Unit with empty `lessons`: render the unit header + a single `'(empty)' — Coming soon` tile (token-based, 16/700 `$fg3`, dashed `borderStrong`, radius `card` 20). Decision Q open-q #1.
  - Bottom safe-area + 24px padding.
- **Empty subject (no units OR all units empty):** centered "Coming soon — no lessons yet" / "قريباً — لا توجد دروس بعد" (i18n `child.subjects.empty`). Full-bleed, 32px padding.
- **404 (unknown subject id):** "Subject not found" / "المادة غير موجودة" (i18n `child.subjects.subjectNotFound`) + Back to Subjects `Button variant="ghost"`.

### Surface 4 — Skill Tree tab
- **Route:** `apps/student-app/app/(child)/subjects/[subjectId]/tree.tsx`.
- **Layout (from `09-skill-tree.png`):**
  - Background: dark canvas `$bg` with a subtle radial brightening behind the tree column — token: reuse the gradient string `radial-gradient(circle at 50% 50%, #1a2348, #0F172A 70%)` from `preview/components-skill-node.html`. Render via Expo `LinearGradient` proxied as a `RadialGradient` if available, else `GradientBox` `variant="brandPanel"` at reduced opacity. **If RadialGradient is unavailable on web/native parity, fall back to flat `$bg` with no gradient — this is a polish layer, not a blocker.**
  - 24px top padding.
  - Per `ConceptNodeDto` (insertion order from API):
    - Concept eyebrow row: uppercase 12/700 `$primaryLight` (`#A5B4FC`), tracking 0.04em — text "Concept · {conceptName}" / "المفهوم · {conceptName}". Centered. 8px below.
    - Vertical column of `SkillTreeNode`s, centered horizontally. Each node:
      - 72px disc (per `preview/components-skill-node.html` shows 80px — we shrink to 72 for tighter mobile column; flag as a deliberate deviation).
      - 6px gap to label (12/700 `$fg2`).
      - 4px gap to mastery row (3 stars / progress sub) when applicable.
    - **Connector strip below each node except the last in a concept:** a 4px-wide × 24px-tall Tamagui `Stack` centered horizontally; background `$borderStrong` for available/unlocked path, `$border` for locked path (state-aware — see §2). No curve, no Skia.
    - 24px gap between concepts (extra rhythm — concepts feel like sections).
- **Mastery header line** (rendered in the ScreenHeader's secondary line — see Surface 2): `"{subjectName} · {conceptName}"` + `"Unit {x} of {y} · Mastery {z}%"`. `z` = `Math.round((completedCount / totalSkills) * 100)`. AR: `"الوحدة ٢ من ٨ · الإتقان ٤٥٪"` (Eastern numerals, `٪` glyph). LTR exception preserved if the FE decides to surface `Mastery 45%` as a measurement string — designer's call is **use Eastern-Arabic numerals + ٪** to match `mobile-ar/09-skill-tree.png` exactly.
- **Locked node tap:** open `WhyLockedSheet` (Surface 5).
- **Available / Completed tap:** `router.push('/(child)/lessons/' + skill.lessonIds[0])` (defensive no-op when empty).

### Surface 5 — WhyLockedSheet (inline strip + bottom sheet)
- **Spawned by:** tap on any Locked `LessonCard` OR Locked `SkillTreeNode`.
- **Mobile native:** bottom sheet from `$card` surface. 24px top radius (`$modal`). Padding 24. Backdrop `$overlay`. Drag handle: 36×4 `$borderStrong`, radius pill, 8px top margin, 16px bottom gap.
- **Web (Expo web parity):** centered modal dialog. `$modal` radius 24 on all corners. Max-width 480px.
- **Both:** sliding-up motion, 240ms ease-out (`$dur-base`, `$ease-out`). On close: 200ms.
- **Content:**
  - Eyebrow: 🔒 + "Locked" / "مقفل" — 12/700 `$fg3` uppercase, tracking 0.04em.
  - H3 title: "Why is this locked?" / "لماذا هذا مقفل؟" — 18/800 `$fg1`.
  - Body: "Finish these to unlock:" / "أكمل هذه لفتح الدرس:" — 14/500 `$fg2`. Only when prereqs are present.
  - List of prereq rows, 12px gap between:
    - Row layout: row, 12px gap, `$card` background → wait, sheet itself is `$card`; inner row uses `$cardSoft` (`#334155`) so it steps lighter (brand law: lighter, never darker). Radius `$cardInner` 14. Padding 12 horiz × 10 vert.
    - Leading: small skill icon — 32×32 rounded-12 tile, `$primarySoft`, glyph 🎯 (semantic emoji per brand law — "mission/skill target"). Logical leading position.
    - Body column (flex 1):
      - Skill name (`prereqSkillName`) — 14/700 `$fg1`.
      - Sub: "You're at {cur}% — need {req}%" / "أنت عند {cur}٪ — تحتاج {req}٪" — 12/500 `$fg3`. Eastern numerals in AR.
    - Trailing: thin progress bar (use `MasteryBar` primitive, width 72, height 6) showing `Math.round(cur)/req` proportion — colored `$accent` (#F59E0B) since we're warming the student to "almost there" energy. NOT green (green = done).
  - Empty prereqs (defensive): single line "Finish the previous lesson first" / "أكمل الدرس السابق أولاً" — 14/500 `$fg2`.
  - Primary CTA: `Button variant="primary"` "Got it!" / "حسناً!" — full-width, radius `$button` 16, height 48, glow on focus. Voice = friendly older-sibling.
  - Close affordance: tap backdrop OR `×` in top-right (24×24, `$fg3`).
- **Accessibility:** `accessibilityRole="dialog"`, `accessibilityViewIsModal` (native), focus trap. Sheet announces title on open.

### Surface 6 — Locked CTA (non-pressable affordance)
- **What is "locked"?** `LessonCard` with `state === 0` and any `SkillTreeNode` with `state === 0`.
- **Visual:** opacity 0.55 applied to the **content** of the card/disc (not to the boss chrome, see §2). Lock glyph 🔒 rendered as a small badge at the trailing top corner (LTR top-right, RTL top-left) of the card — 18px, color `$fg3`. The disc-version: lock glyph centered inside the disc instead of the state icon.
- **Cursor:** web only — `cursor: not-allowed` (Tamagui prop). Native: no cursor; press handler intercepts and routes to WhyLockedSheet instead of navigation.
- **Press behavior:** still pressable for the WhyLockedSheet open — but `accessibilityState={{ disabled: true }}` to signal screen-readers it cannot be activated as a normal lesson. Screen-reader reads: "Equations, locked, double tap to see what unlocks this".
- **No glow, no hover lift, no scale-on-press.** Brand law #5: never darken on interaction — and locked items should not animate. Motion = zero.

---

## 2. State design — composition rules

### LessonCard (4 states; Boss orthogonal)

| State | Source | Background | Border | Title color | Glow | Hover (web) | Press |
|---|---|---|---|---|---|---|---|
| **Locked** | `state === 0` | `$card` (`#1E293B`) | `$border` (1px) | `$fg3` (`#94A3B8`) | none | none | open WhyLockedSheet, no scale |
| **Available** (active) | `state === 1` | `$card` | **2px solid `$primary`** | `$fg1` | `$shadow-primary-glow` (0 8px 24px `rgba(99,102,241,0.45)`) | brighten 8% + scale 1.02 (160ms) | scale 0.95 80ms |
| **Completed** | `state === 2` | `$card` | `$success` 1.5px (or `$successSoft` fill chip on the tag, see below) | `$fg1` | none | brighten 8%, no scale | scale 0.95 80ms |
| **Boss-on-X** | `isBoss === true` (composed on top) | base of X | base of X | base of X | adds `$shadow-streak-glow` over base | base | base |

**Tag pill** (the eyebrow chip inside each card, e.g. `Math · Numbers`):
- Available: text `$primary`, background `$primarySoft` (rgba(79,70,229,0.18)).
- Completed: text `$success`, background `$successSoft` (rgba(34,197,94,0.18)). Label = "Completed" / "مكتمل".
- Locked: text `$fg3`, background `rgba(255,255,255,0.06)` (= `$borderSubtle`). Label = "Locked" / "مقفل".

**Meta row** (3 dots-separated): minutes · questions · `+{xp} XP` — 12/500 `$fg3`. Dot = 3×3 `$fg3` disc, opacity 0.6. In AR with Eastern numerals: `٥ دقائق · ١٠ أسئلة · +٥٠ نقطة`.

**Progress bar** (8px, radius pill, bg `$bg` `#0F172A`, 1px `borderSubtle`):
- Available with progress > 0: `$gradXp` fill (90deg green→indigo). Width = `currentProgress%`.
- Completed: 100% fill `$success` flat.
- Locked: bar omitted.

**Boss chrome on LessonCard:** `BossBadge` pill positioned in the trailing-top corner of the card (logical — LTR top-right, RTL top-left). The chip overlays the content but does NOT replace it. The chip is reserved for `isBoss === true` regardless of state — yes, even Locked boss lessons show "👑 Boss".

### SkillTreeNode (4 states; Boss derived; circular disc)

Disc dims: 72×72, `borderRadius: 36` (50%). Label below — 12/700.

| State | Source | Disc background | Glyph (centered) | Border | Glow / shadow | Label color | Sub-caption |
|---|---|---|---|---|---|---|---|
| **Locked** | `state === 0` | `$cardSoft` (`#334155`) | 🔒 — `$fg4` (`#64748B`), 28px | none | `inset 0 1px 0 rgba(255,255,255,0.06)` only (no outer glow) | `$fg3` | "Locked" / "مقفل" — 10/500 `$fg3` |
| **Available** | `state === 1` (not Boss) | radial-gradient from `#A5B4FC` (30%/30%) → `$primary` (`#4F46E5`) | ✏️ — white, 30px | none | `$shadow-primary-glow` (0 0 28px rgba(99,102,241,0.6)) + `inset 0 -4px 8px rgba(0,0,0,0.2)`; pulse 1→1.04→1 2s loop | `$fg1` | "In progress" / "قيد التقدم" — 10/700 `$primaryLight` |
| **Completed** | `state === 2` (not Boss) | radial-gradient from `#86EFAC` (30%/30%) → `$success` (`#22C55E`) | ✓ — white, 30px | none | `0 8px 20px rgba(34,197,94,0.45)` + `inset 0 -4px 8px rgba(0,0,0,0.2)` | `$fg1` | 3-star row using `$xp` (#FACC15) — fill stars based on mastery ⭐⭐⭐ |
| **Boss-on-X** (derived: any lesson with `isBoss === true` references this `skillId`) | join `subjectLessons × skillTree` on FE | radial-gradient from `#FCA5A5` (30%/30%) → `$danger` (`#EF4444` 55%) → `#7C2D12` | 🔥 — white, 30px | **2px `$streak`** (`#FB923C`) | `0 0 28px rgba(239,68,68,0.55)` + `inset 0 -4px 8px rgba(0,0,0,0.25)` | `$streak` (#FB923C) | "Boss Challenge" / "تحدي البوس" — 10/700 `$danger` |

**Boss + state composition priority rule:**
- A boss skill node REPLACES the state chrome with the Boss chrome above, but the **glyph** swaps to reflect the state:
  - `Boss + Locked` → 🔒 glyph but boss disc + label `$fg3` "Locked" caption, NO pulse, NO outer glow (`shadow-streak-glow` removed; only `inset` kept).
  - `Boss + Available` → 🔥 glyph + pulse + full boss glow + caption "Boss Challenge".
  - `Boss + Completed` → ✓ glyph + boss disc (replacing the green) + 3-star row + caption "Boss Beaten!" / "هزمت البوس!".
- The boss flame chrome wins on color identity (`$danger` family); the **interaction-state** (locked/available/completed) shows through glyph + caption + glow modifier.
- Reason: the screenshot `09-skill-tree.png` shows green completed + active blue + grey locked, no boss; the preview card `components-skill-node.html` shows boss as a separate orthogonal chrome. Composition rule = "Boss chrome wins, state modulates the glyph + glow."

### SubjectRow states
- **Default:** `$card` background, 1px `$border`, radius `$card` 20, `$shadow-soft`. Hover (web): brighten 8% (bg → `$cardSoft`), scale 1.02 160ms. Press: scale 0.95 80ms.
- **Loading:** shimmer placeholder, same dims, no inner content.
- (No Locked/Completed states at the subject level in W11 — all 4 subjects are always unlocked.)

---

## 3. Net-new primitives — `@learnexia/ui` spec

### 3.1 `SubjectRow`

**Composed from:** `preview/mobile-subject-rows.html`.

**Props (TS):**
```ts
export interface SubjectRowProps {
  /** Stable id from the API (used as React key by caller; not rendered). */
  subjectId: number;
  /** Localized display name. */
  name: string;
  /** 0..100; omit to hide the bar+caption block. */
  masteryPercent?: number;
  /** Required — determines the icon tile color tint and glyph. */
  subjectKey: 'math' | 'science' | 'arabic' | 'english';
  /** Tap handler (always pressable). */
  onPress: () => void;
  /** Already-localized a11y label, e.g. "Math, 45 percent mastered". */
  accessibilityLabel: string;
  /** RTL direction from `useLocale().direction`. */
  direction?: 'ltr' | 'rtl';
  /** For the loading skeleton — render the chrome only, no content. */
  loading?: boolean;
  testID?: string;
}
```

**Dimensions:**
- Outer card: full-width, padding 16, gap 14 (row gap), radius `$card` (20), border 1px `$border`, bg `$card` (`#1E293B`), shadow `$shadow-soft`.
- Icon tile (leading): 52×52, radius `$button` (16), centered glyph 26px, color = `subjectTint[subjectKey].fg`, bg = `subjectTint[subjectKey].soft`.
- Body (flex 1):
  - Title: 16/800 Poppins/Cairo `$fg1`, `lineHeight: 1.15`.
  - Mastery caption: "{percent}% mastered" / "{percent}% إتقان" — 11/500 `$fg3`, `marginTop: 4`.
  - Mastery bar: 6px tall, radius pill, bg `$bg` (#0F172A), fill = `subjectTint[subjectKey].fg` flat. `marginTop: 6`.
- Trailing chevron: 18px, `$fg3`. Glyph `›` LTR, `‹` RTL.

**Subject tint map (token-named; FE adds to `packages/design-system/src/tokens/colors.ts` as `subjectMath` / `subjectScience` / `subjectArabic` / `subjectEnglish` exports):**

| `subjectKey` | Foreground (`fg`) | Soft fill (`soft`) | Glyph |
|---|---|---|---|
| `math` | `$primary` (`#4F46E5`) | `$primarySoft` (rgba(79,70,229,0.18)) | 🧮 |
| `science` | `$success` (`#22C55E`) | `$successSoft` (rgba(34,197,94,0.18)) | 🧪 |
| `arabic` | `$accent` (`#F59E0B`) | `$warningSoft` (rgba(245,158,11,0.18)) | 📖 |
| `english` | `$purple` (`#A855F7`) | `$purpleSoft` (rgba(168,85,247,0.18)) | 🔤 |

(Capture shows Arabic as purple and English as purple-tinted "GB" — we re-tint Arabic to warm orange `$accent` because purple is reserved for English/badges per brand grammar, and orange visually distinguishes the AR language tile from the BADGE color. Flagged deviation from `06-subject-select.png`.)

**Subject glyph for English (`🔤`)** replaces the capture's "GB" flag bubble — flag emoji is geopolitical, brand law forbids decorative emoji; `🔤` is semantic for "letters/language". Flagged deviation from capture.

**Logical layout (RTL):** `flexDirection={isRtl ? 'row-reverse' : 'row'}` for the outer row. Logical `marginStart`/`marginEnd` for inner gaps. Chevron flips glyph (`›` vs `‹`) AND is laid out logically (trailing edge auto-flips by `row-reverse`). The mastery bar stays LTR via `direction: 'ltr'` wrapper (brand law: progress reads L→R universally).

**Motion:**
- Hover (web only): `transform: scale(1.02)`, bg → `$cardSoft`, 160ms `$ease-out`.
- Press: `transform: scale(0.95)`, 80ms.
- Focus ring: `$focus-ring` (2px `$primary` + 4px `$primaryGlow` outer).

**Accessibility:** `accessibilityRole="button"`, `accessibilityLabel` required (caller provides — e.g. `"الرياضيات، ٤٥٪ إتقان"`).

---

### 3.2 `LessonCard`

**Composed from:** `preview/components-lesson-card.html` + `preview/ar-lesson-card.html`.

**Props:**
```ts
export interface LessonCardProps {
  lessonId: number;
  /** Eyebrow tag — e.g. "Math · Numbers" or "الرياضيات · الأعداد". Caller localizes. */
  tag: string;
  /** Lesson title (localized). */
  title: string;
  /** Meta strings (caller localizes; rendered with • separator). Pass [] to hide. */
  meta?: string[];
  /** 0=Locked, 1=Available, 2=Completed. From LessonInUnitDto.state. */
  state: 0 | 1 | 2;
  /** Optional progress 0..100; only rendered when state===1 OR state===2. */
  progressPercent?: number;
  /** Boss overlay. Orthogonal to state. */
  isBoss?: boolean;
  /** Stars earned (0..3) — rendered when state===2. */
  masteryStars?: number;
  /** Open the lesson player (Available/Completed). */
  onPress?: () => void;
  /** Open WhyLockedSheet (Locked). Caller passes a function that surfaces missingPrerequisites. */
  onLockTap?: () => void;
  direction?: 'ltr' | 'rtl';
  /** Already-localized a11y label, e.g. "Compare Bigger and Smaller, available, boss". */
  accessibilityLabel: string;
  testID?: string;
}
```

**Dimensions:**
- Outer card: full-width, padding 18, gap 12 (column gap), radius `$modal` (24 — matches preview, deliberately larger than `$card` 20 because lesson cards are the hero affordance on the Lessons tab), bg `$card`, border 1px `$border`, shadow `$shadow-card`. `position: relative` (for boss + lock overlays).
- Available variant: border becomes **2px `$primary`**, shadow becomes `$shadow-primary-glow` (= `0 8px 24px var(--lx-primary-glow)`).
- Locked variant: opacity 0.55 on the content stack (NOT on `BossBadge` if present). `cursor: 'not-allowed'` on web. A 🔒 glyph badge floats at trailing-top: 18px, `$fg3`, position `top: 14, end: 14` (logical).

- **Tag pill** (eyebrow): self-align start, padding `4px 10px`, radius pill, font 10/700 uppercase, letterSpacing 0.04em. Colors per state (see §2). Always rendered. AR uses Cairo 700.

- **Title:** 18/800 Poppins/Cairo, `lineHeight: 1.2`. Color per state (see §2).

- **Meta row:** 12/500 `$fg3`, 10px gap between items, 3×3 disc separator `$fg3` opacity 0.6. AR: Eastern numerals.

- **Progress bar:** 8px tall, radius pill, bg `$bg` (`#0F172A`), 1px `$borderSubtle`. Fill per §2.

- **`BossBadge` overlay** (when `isBoss === true`): positioned `top: 14, end: 14` (logical — trailing-top). Coexists with 🔒 badge by stacking the boss badge BELOW the lock badge (lock badge wins the top corner; boss sits at `top: 38, end: 14`). On Available/Completed cards, only the boss badge is at `top: 14, end: 14`.

**Motion:**
- Available hover (web): brighten 8%, scale 1.02, 160ms.
- Completed hover (web): brighten 8%, no scale.
- Press: scale 0.95, 80ms (except Locked).
- Completed pulse on first mount of "newly completed" — out of scope W11; just render the chrome.

**Accessibility:**
- `accessibilityRole="button"`.
- `accessibilityState={{ disabled: state === 0, selected: false }}`.
- `accessibilityLabel` includes state and boss flag (caller composes: `"Compare Bigger and Smaller, available, boss"`).

---

### 3.3 `SkillTreeNode`

**Composed from:** `preview/components-skill-node.html` + `preview/mobile-skill-path.html` + `screenshots/mobile/09-skill-tree.png`.

**Props:**
```ts
export interface SkillTreeNodeProps {
  skillId: number;
  /** Localized name. */
  name: string;
  /** 0=Locked, 1=Available, 2=Completed. From SkillNodeDto.state. */
  state: 0 | 1 | 2;
  /** Derived on FE by joining subjectLessons × skillTree on skillId. */
  hasBoss?: boolean;
  /** 0..3 stars; rendered as ⭐ row when state===2 (or Boss+Completed). */
  masteryStars?: number;
  /** Open the lesson player (Available/Completed/Boss-X). */
  onPress?: () => void;
  /** Open WhyLockedSheet (Locked / Boss+Locked). */
  onLockTap?: () => void;
  /** Render a connector strip BELOW this node. The last node in a Concept passes false. */
  showConnectorBelow?: boolean;
  /** Token-aware connector tint based on the NEXT node's state ('reachable' | 'locked'). */
  connectorState?: 'reachable' | 'locked';
  direction?: 'ltr' | 'rtl';
  accessibilityLabel: string;
  testID?: string;
}
```

**Dimensions:**
- Node container: `alignItems: 'center'`, gap 6.
- Disc: 72×72, `borderRadius: 36`. Centered glyph 30px (32 acceptable for the boss flame — designer call).
- Per-state visuals: §2.
- Label: 12/700, `$fg2` default (state-modulated per §2), max width 96, `textAlign: 'center'`, `numberOfLines: 2`.
- Sub-caption (state-aware): 10/500-700, color per state.
- Star row (state===2 OR Boss+Completed): row of 3 ⭐ glyphs, 10px each, color `$xp` (#FACC15) for filled stars; faded `$fg4` for unfilled. 2px gap between stars.
- Connector strip (when `showConnectorBelow`): 4px wide × 24px tall, centered horizontally, `borderRadius: 2`, bg `$borderStrong` (`reachable`) or `$border` (`locked`). 12px gap above + below.

**Motion:**
- Available state: pulse loop `transform: scale(1) → 1.04 → 1`, 2000ms `$ease-out` infinite. Implemented via Reanimated `withRepeat(withSequence(...))`. If Reanimated would balloon the bundle, fall back to CSS `@keyframes pulse` on web and a no-op on native (acceptable W11 degradation — pulse is "nice-to-have").
- Press (Available/Completed/Boss-X): scale 0.95, 80ms.
- Locked: no motion.
- On lock-tap: triggers the WhyLockedSheet open via `onLockTap`; the node itself does NOT animate.

**Accessibility:**
- `accessibilityRole="button"`, `accessibilityState={{ disabled: state === 0 }}`.
- `accessibilityLabel` includes name + state + boss + (if Locked) "double tap to see what unlocks this".
- Mark the boss decoration via the label, not via icon-only.

---

### 3.4 `BossBadge`

**Implementation:** Promote as a `Badge` variant (`variant="boss"`) — keeps the primitive count lean and reuses the Badge layout/accessibility shape. Mirrors existing pattern (CLAUDE.md rule 8: this is mirror, not a new design pattern).

**Composed from:** `preview/components-skill-node.html` boss chrome + `preview/components-badges.html` for the pill shape.

**Variant spec:**
- Render: pill, padding `4px 10px`, radius pill (9999), 1px `$streak` border.
- Background: `$streakSoft` (`rgba(251,146,60,0.13)`) → fall back to `$warningSoft` if the FE prefers (designer accepts either).
- Label: 10/800 uppercase, letterSpacing 0.04em, color `$streak` (`#FB923C`).
- Glyph + label: `👑 Boss` / `👑 بوس` — semantic emoji (brand law: crown = trophy/boss).
- Optional: replace with `🔥 Boss` if the boss is a Locked one (semantic flame = challenge). Designer's recommendation: keep `👑 Boss` everywhere for consistency; the disc itself carries 🔥 in skill-tree mode.

**Sizing tokens:** uses the `Badge` `size="sm"` token group (12px height tag).

**Accessibility:** decorative role; the parent card's `accessibilityLabel` includes "boss".

---

### 3.5 `WhyLockedSheet` (inline — `apps/student-app/app/(child)/_components/WhyLockedSheet.tsx`)

**Promotion decision:** keep INLINE in the app under `_components/`. Two consumers (Lessons + Tree) but both are in the same screen group; only promote to `@learnexia/ui` if a third consumer appears (CLAUDE.md rule 8 — mirror existing shapes, don't generalize prematurely).

**Props:**
```ts
export interface WhyLockedSheetProps {
  open: boolean;
  onClose: () => void;
  /** From LessonInUnitDto.missingPrerequisites or SkillNodeDto.missingPrerequisites (defensive: treat null as []). */
  prerequisites: MissingPrerequisiteDto[];
  /** Localized title of the lesson/skill that's locked (for context). */
  lockedItemName: string;
  direction?: 'ltr' | 'rtl';
}
```

**Layout:** §1 Surface 5.

**Motion:**
- Open: slide-up + fade in, 240ms `$dur-base` `$ease-out`. Spring overshoot NOT used (sheets are tools, not rewards).
- Close: slide-down + fade out, 200ms `$ease-out`.
- Inline strip variant (alternative for compact spaces) — NOT used in W11; the spec is bottom sheet on mobile + dialog on web.

**A11y:** `accessibilityRole="dialog"`, focus trap, autoFocus on title, `accessibilityViewIsModal={true}` (native).

---

### 3.6 `SegmentedTabs` — NEW horizontal variant of `Tabs`

**Decision:** Extend the existing `Tabs` primitive with an `orientation: 'vertical' | 'horizontal'` prop, default `'vertical'` (preserves current Settings behavior). When `'horizontal'`, the same active-pill chrome is laid out as a row inside a container pill.

**Mirror reasoning (CLAUDE.md rule 8):** the visual grammar (active-pill = `$primarySoftStrong` bg + `$primaryLight` label + `$nav` 12px radius) is identical; only the axis changes. This is NOT a design pattern — it's an orientation prop on an existing primitive. **No ask required.**

**Horizontal-mode dims:**
- Container: full-width minus 32 horizontal margins (so 326px on a 390-wide phone), height 44, padding 4, radius `$button` (16), bg `$card`, border 1px `$borderSubtle`.
- Each segment: `flex: 1`, padding `8px 14px`, radius `$nav` (12), center-aligned.
- Active segment: bg `$primarySoftStrong` (`rgba(79,70,229,0.28)`), label color `$primaryLight` (`#A5B4FC`), font 14/700.
- Inactive segment: bg transparent, label color `$fg3`, font 14/500. Hover (web): label `$fg2`, bg `rgba(255,255,255,0.04)`.
- Press: scale 0.97, 80ms.

**Items:**
- `{ value: 'lessons', label: t('child.subjects.tabs.lessons'), icon?: '📚' }`
- `{ value: 'tree', label: t('child.subjects.tabs.tree'), icon?: '🌳' }`

(Icons OPTIONAL — designer-preferred is **text-only** to keep the segmented control quiet; the Lessons | Skill-Tree split is informational, not rewarding. FE may pass `icon` undefined.)

**Implementation hint for FE:** if extending `Tabs` would balloon its TS prop surface, ship `SegmentedTabs` as a sibling export in `packages/ui/src/components/SegmentedTabs/index.tsx` that internally maps to the same row primitives. Either path is acceptable; pick the smaller diff.

---

## 4. Skill-tree edge visualization

**V1 = plain Tamagui vertical strips.** No Skia, no Reanimated, no DAG layout algorithm.

**Rules:**
- Within a Concept, render the Skills as a top-to-bottom column, centered horizontally.
- Between each consecutive pair of skills WITHIN A CONCEPT, render a single `Stack` 4×24 below the upper skill node (the `showConnectorBelow` prop on `SkillTreeNode`).
- BETWEEN concepts, render NO connector — concepts are sections, not contiguous paths in V1.
- Connector color rules:
  - If the NEXT node's `state === 0` (locked) → `$border` (`rgba(255,255,255,0.08)`).
  - Else (NEXT is Available, Completed, or Boss-X with any state) → `$borderStrong` (`rgba(255,255,255,0.16)`).
  - This reflects "is the path forward open?" — locked next = dim line.
- The capture `09-skill-tree.png` shows nodes slightly offset left/right (~30px). For V1 we render them DEAD CENTERED (no x-offset). Flag as a deliberate deviation from the capture — the L/R offsets are a polish layer that needs Skia curve drawing to look right; centered nodes ship now and the curve polish is a Wave 12+ task.

---

## 5. Tokens (full table — every reference)

### 5.1 Colors

| Use | Token | Resolved | Source |
|---|---|---|---|
| Screen canvas | `$bg` | `#0F172A` | `colors_and_type.css` |
| Cards (SubjectRow, LessonCard, sheet) | `$card` | `#1E293B` | css |
| Sheet inner rows, skill-tree locked disc | `$cardSoft` | `#334155` | css |
| Body text | `$fg2` | `#CBD5E1` | css |
| Captions, sub-text, dots | `$fg3` | `#94A3B8` | css |
| Disabled glyph, unfilled stars | `$fg4` | `#64748B` | `tokens/colors.ts` |
| Active labels, eyebrow text on tree | `$primaryLight` | `#A5B4FC` | `tokens/colors.ts` |
| Primary border + glow | `$primary` | `#4F46E5` | css |
| Available card primary-soft tint | `$primarySoft` | `rgba(79,70,229,0.18)` | css |
| Active tab pill | `$primarySoftStrong` | `rgba(79,70,229,0.28)` | `tokens/colors.ts` |
| Primary glow shadow | `$primaryGlow` | `rgba(99,102,241,0.45)` | css |
| Completed chrome | `$success` | `#22C55E` | css |
| Completed soft chip | `$successSoft` | `rgba(34,197,94,0.18)` | css |
| Boss border + label | `$streak` | `#FB923C` | css |
| Boss disc bg gradient end | `$danger` | `#EF4444` | css |
| Boss soft pill bg | `$streakSoft` | `rgba(251,146,60,0.13)` | `tokens/colors.ts` |
| Star fill | `$xp` | `#FACC15` | css |
| Subject tile bg (per subject) | `$primarySoft` / `$successSoft` / `$warningSoft` / `$purpleSoft` | various | css |
| Subject tile fg (per subject) | `$primary` / `$success` / `$accent` / `$purple` | various | css |
| Card border | `$border` | `rgba(255,255,255,0.08)` | css |
| Strong border (active card, reachable connector) | `$borderStrong` | `rgba(255,255,255,0.16)` | css |
| Subtle hairline | `$borderSubtle` | `rgba(255,255,255,0.06)` | `tokens/colors.ts` |
| Modal backdrop | `$overlay` | `rgba(15,23,42,0.72)` | css |

### 5.2 Spacing — all from `$space-*`

| Use | Token | Px |
|---|---|---|
| Bar/separator gap | `$space-1` | 4 |
| Compact gaps | `$space-2` | 8 |
| Card inner gap | `$space-3` | 12 |
| Card padding | `$space-4` | 16 |
| Section gap | `$space-5` | 20 |
| Outer screen padding, section rhythm | `$space-6` | 24 |
| Concept-to-concept rhythm | `$space-8` | 32 |

### 5.3 Radii

| Use | Token | Px |
|---|---|---|
| Tag pills, BossBadge, mastery bar, progress bar | `$radius-pill` | 9999 |
| Tab segment (inactive/active) | `$radius-nav` | 12 |
| Sheet inner rows | `$radius-card-inner` | 14 |
| Icon tile (SubjectRow), Buttons, segmented container | `$radius-button` | 16 |
| SubjectRow outer card | `$radius-card` | 20 |
| LessonCard outer, WhyLockedSheet top corners | `$radius-modal` | 24 |

### 5.4 Shadows / glows

| Use | Token | Resolved |
|---|---|---|
| Cards resting | `$shadow-soft` | `0 4px 12px rgba(0,0,0,0.15)` |
| Hover, sheet floating | `$shadow-float` | `0 8px 24px rgba(0,0,0,0.25)` |
| Sheet | `$shadow-popup` | `0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)` |
| Available LessonCard | `$shadow-primary-glow` | `0 8px 24px rgba(99,102,241,0.45)` |
| Available SkillTreeNode | `0 0 28px rgba(99,102,241,0.6)` + inset (custom — token-named here as `$shadow-primary-glow` × 1.2; designer suggests adding `$shadow-primary-glow-strong` token to `shadows.ts` to keep it 1:1 with css preview) | (raw — flag for token addition) |
| Completed SkillTreeNode | `0 8px 20px rgba(34,197,94,0.45)` + inset (designer suggests `$shadow-success-glow` token; not in CSS today — flagged) | (raw — flag) |
| Boss SkillTreeNode | `$shadow-streak-glow` (`0 0 24px rgba(251,146,60,0.45)`) — close enough to preview's `0 0 28px rgba(239,68,68,0.55)`; designer accepts the existing token even though it's orange-not-red glow, because boss chrome already carries the red disc. **Or** add `$shadow-danger-glow` (flagged below). | `0 0 24px rgba(251,146,60,0.45)` |

**Flagged token additions for the frontend to propose (NOT auto-added):**
- `shadowPrimaryGlowStrong: '0 0 28px rgba(99,102,241,0.6)'` — Available skill-tree node.
- `shadowSuccessGlow: '0 8px 20px rgba(34,197,94,0.45)'` — Completed skill-tree node.
- `shadowDangerGlow: '0 0 28px rgba(239,68,68,0.55)'` — Boss skill-tree node (preferred over reusing `$shadow-streak-glow`).
- Subject tint tokens — `subjectMath` / `subjectScience` / `subjectArabic` / `subjectEnglish` keyed maps with `fg` + `soft` pairs.

If the FE pushes back on token additions, the components ship with inline `boxShadow` strings citing this spec — acceptable for W11 but logged as design debt.

### 5.5 Typography

| Use | Family | Size | Weight | LH | Tracking |
|---|---|---|---|---|---|
| Screen H1 ("Subjects") | Poppins/Cairo | 32 | 800 | 1.15 | -0.02em |
| Subject row title | Poppins/Cairo | 16 | 800 | 1.15 | 0 |
| Lesson title | Poppins/Cairo | 18 | 800 | 1.2 | 0 |
| Concept eyebrow | Poppins/Cairo | 12 | 700 | 1.3 | 0.04em uppercase |
| Skill node label | Poppins/Cairo | 12 | 700 | 1.3 | 0 |
| Skill node sub-caption | Poppins/Tajawal | 10 | 500-700 | 1.3 | 0 |
| Tag pills | Poppins/Cairo | 10 | 700 | 1.3 | 0.04em uppercase |
| Meta row, mastery caption | Poppins/Tajawal | 11-12 | 500 | 1.3 | 0 |
| WhyLockedSheet H3 | Poppins/Cairo | 18 | 800 | 1.3 | 0 |
| WhyLockedSheet body | Poppins/Tajawal | 14 | 500 | 1.5 | 0 |
| Segmented tab label | Poppins/Cairo | 14 | 500/700 | 1.3 | 0 |

Numbers + percentages: `fontVariant: ['tabular-nums']`, weight 800 per brand law. AR percentages use Eastern numerals (`٤٥٪`).

---

## 6. Motion

| Element | Trigger | Spec |
|---|---|---|
| SubjectRow / LessonCard / SkillTreeNode | Hover (web) | brighten 8% + scale 1.02, 160ms `$ease-out`. Never darken. |
| All interactive | Press | scale 0.95, 80ms. Disabled when state = Locked. |
| SubjectRow | Tap → navigate | route fires after press-up; no extra animation on this screen (page transition is at the router level: slide+fade 250-300ms per brand law). |
| Available SkillTreeNode | Mount + idle | pulse 1 → 1.04 → 1, 2000ms `$ease-out`, infinite loop. |
| Completed SkillTreeNode | Mount (first time only — out of scope W11; do nothing) | none. (P2-05-FE will trigger a confetti pop after lesson completion.) |
| Locked SkillTreeNode / Locked LessonCard | Tap | no scale, no pulse. Open `WhyLockedSheet` (240ms slide-up). |
| WhyLockedSheet | Open | slide-up + fade, 240ms `$ease-out`. Backdrop `$overlay` fades in over same 240ms. |
| WhyLockedSheet | Close | slide-down + fade, 200ms `$ease-out`. |
| SegmentedTabs | Switch | active pill cross-fades 160ms `$ease-out`. No spring (sheets/tools = no overshoot; rewards = overshoot). |
| Loading skeleton | Idle | shimmer sweep 1200ms linear infinite. `$cardSoft → $border → $cardSoft`. |
| Connector strip | Idle | static. No motion. |
| Tree screen gradient backdrop | Idle | static. |

All durations ≤ 800ms (brand law).

---

## 7. RTL & Arabic

- **Direction:** `dir="rtl"` (set at root via `useLocale().direction`). All horizontally-oriented stacks use `flexDirection={isRtl ? 'row-reverse' : 'row'}`. No `row-reverse` hardcoded anywhere; let the direction toggle drive it.
- **Fonts:**
  - Display (titles, eyebrows, segmented control labels, BossBadge): **Cairo** in AR, **Poppins** in EN.
  - Body (captions, meta rows, sheet body): **Tajawal** in AR, **Poppins** in EN.
- **Mirroring:**
  - Chevron in SubjectRow: glyph swap `›` ↔ `‹` AND layout flip via `row-reverse`. Net effect = chevron always points toward "forward".
  - Back chevron in ScreenHeader: `←` (LTR) ↔ `→` (RTL).
  - Lock badge + BossBadge: positioned with logical `end` (the trailing edge), so they auto-flip.
  - Star row: order preserved (stars are visual, not directional); Cairo handles RTL flow correctly.
- **NOT mirrored:**
  - Skill-tree disc gradients (radial; visually unchanged).
  - Disc glyphs (✓, ✏️, 🔒, 🔥) — emoji preserve identity.
  - Progress bars + mastery bars — wrap in `direction: 'ltr'` so the fill always grows left→right (brand law).
  - Subject icon tile glyphs.
- **Numerals:**
  - All UI strings showing percentages, grade, "Unit X of Y" — use **Eastern-Arabic numerals** in AR locale (`٠١٢٣٤٥٦٧٨٩`). The existing i18n helper handles the conversion — verify it is wired into the new screens' interpolations.
  - LTR exception: lesson `lessonId` in the URL stays Latin (technical string).
- **Copy:** see Appendix §10 for verbatim EN + AR strings.

---

## 8. Accessibility / kid-UX

- **Touch targets ≥ 48×48.** SubjectRow ~84px tall, LessonCard ~120-140px tall, SkillTreeNode disc 72px + 24px chrome = 96px. All comfortably above the floor. SegmentedTabs row 44px — bump container height to 48 if tap accuracy is shaky on web; this spec accepts 44 on mobile because the segment width is large.
- **Roles + states:**
  - SubjectRow: `accessibilityRole="button"`, `accessibilityLabel="{name}, {percent} percent mastered"` ("الرياضيات، ٤٥٪ إتقان").
  - LessonCard: `accessibilityRole="button"`, `accessibilityState={{ disabled: state === 0 }}`, label includes name + state + boss ("Compare Bigger and Smaller, available, boss" / "مقارنة الأكبر والأصغر، متاح، بوس").
  - SkillTreeNode: same pattern. Locked label adds "double tap to see what unlocks this" ("اضغط مرتين لمعرفة ما يفتح هذا").
  - WhyLockedSheet: `accessibilityRole="dialog"`, focus traps inside on open, focus returns to the triggering card on close.
- **Focus visibility:** every interactive primitive renders `$focus-ring` on keyboard focus (2px `$primary` + 4px `$primaryGlow`). Disabled-locked cards still show the focus ring (so the kid can land on it and discover the WhyLockedSheet via the SR/keyboard).
- **Color contrast:**
  - Locked text `$fg3` on `$card` → 4.4:1 (AA passes for ≥14px non-bold body; the locked title is 18/800 so it passes; the meta 12 should bump to `$fg2` if AA on small text is required — designer accepts current `$fg3` because locked state is intentionally low-affordance).
  - Available + Completed states comfortably pass AA.
- **Reduced motion:** if the user has `prefers-reduced-motion`, disable the Available-skill pulse, the page slide-fade transition, and the press scale. Open/close sheet remains (it's an essential affordance — replace with fade-only).
- **Voice:** the WhyLockedSheet body uses second-person, encouraging tone — "You're at 35%, need 60%" / "أنت عند ٣٥٪، تحتاج ٦٠٪". The "Got it!" / "حسناً!" CTA is friendly (no exclamation in AR — exclamations reserved for wins per brand law; but "حسناً!" is so short and warm that the exclamation reads as enthusiastic agreement, not a fake celebration — designer accepts).

---

## 9. Implementation handoff

| Piece | Target |
|---|---|
| Subject tint color tokens | `packages/design-system/src/tokens/colors.ts` (`subjectMath`, `subjectScience`, `subjectArabic`, `subjectEnglish` — each `{ fg, soft }`) |
| Glow shadow tokens (flagged) | `packages/design-system/src/tokens/shadows.ts` — propose `shadowPrimaryGlowStrong`, `shadowSuccessGlow`, `shadowDangerGlow`. Acceptable to ship inline `boxShadow` strings in W11 if the FE prefers, with a TODO. |
| `SubjectRow` | `packages/ui/src/components/SubjectRow/index.tsx` (+ export from `packages/ui/src/index.ts`) |
| `LessonCard` | `packages/ui/src/components/LessonCard/index.tsx` (+ export) |
| `SkillTreeNode` | `packages/ui/src/components/SkillTreeNode/index.tsx` (+ export) |
| `BossBadge` | Extend existing `Badge` with `variant="boss"` (preferred) — no new file. If `Badge` can't accept the variant cleanly, ship `packages/ui/src/components/BossBadge/index.tsx` (+ export). |
| `SegmentedTabs` | Add `orientation` to existing `Tabs` OR ship `packages/ui/src/components/SegmentedTabs/index.tsx` — designer accepts either; pick smaller diff. |
| `WhyLockedSheet` | `apps/student-app/app/(child)/_components/WhyLockedSheet.tsx` (inline, NOT promoted) |
| Subjects list screen | `apps/student-app/app/(child)/index.tsx` (replace body) |
| Subject layout (segmented control) | `apps/student-app/app/(child)/subjects/[subjectId]/_layout.tsx` |
| Lessons tab | `apps/student-app/app/(child)/subjects/[subjectId]/index.tsx` |
| Skill Tree tab | `apps/student-app/app/(child)/subjects/[subjectId]/tree.tsx` |
| Lesson stub | `apps/student-app/app/(child)/lessons/[lessonId].tsx` (placeholder per plan B3-1) |
| i18n keys | `packages/shared/src/i18n/resources.ts` — AR + EN, per Appendix §10 |
| Eastern numeral helper wiring | Verify existing helper is invoked for all `{percent}`, `{grade}`, `{current}`, `{total}` interpolations under AR locale. |

---

## 10. EN + AR copy appendix

Verbatim strings the FE keys on. AR strings are pulled from the SKILL.md cheat sheet where they exist; new strings are written here to brand voice.

| i18n key | EN | AR |
|---|---|---|
| `child.subjects.title` | Subjects | المواد |
| `child.subjects.gradeLabel` | Grade {{grade}} | الصف {{grade}} |
| `child.subjects.masteryLabel` | {{percent}}% mastered | {{percent}}% إتقان |
| `child.subjects.empty` | Coming soon — no lessons yet | قريباً — لا توجد دروس بعد |
| `child.subjects.subjectNotFound` | Subject not found | المادة غير موجودة |
| `child.subjects.errorRetry` | Couldn't load. Try again | تعذّر التحميل. حاول مرة أخرى |
| `child.subjects.backToSubjects` | Back to Subjects | عودة إلى المواد |
| `child.subjects.tabs.lessons` | Lessons | الدروس |
| `child.subjects.tabs.tree` | Skill Tree | شجرة المهارات |
| `child.subjects.lessons.unitLabel` | Unit {{n}} | الوحدة {{n}} |
| `child.subjects.lessons.unitsCount` | {{units}} units · {{lessons}} lessons | {{units}} وحدات · {{lessons}} دروس |
| `child.subjects.lessons.meta.minutes` | {{n}} min | {{n}} دقائق |
| `child.subjects.lessons.meta.questions` | {{n}} questions | {{n}} أسئلة |
| `child.subjects.lessons.meta.xp` | +{{n}} XP | +{{n}} نقطة |
| `child.subjects.lessons.tagCompleted` | Completed | مكتمل |
| `child.subjects.lessons.tagLocked` | Locked | مقفل |
| `child.subjects.lessons.emptyUnit` | Coming soon | قريباً |
| `child.subjects.whyLocked.eyebrow` | Locked | مقفل |
| `child.subjects.whyLocked.title` | Why is this locked? | لماذا هذا مقفل؟ |
| `child.subjects.whyLocked.intro` | Finish these to unlock: | أكمل هذه لفتح الدرس: |
| `child.subjects.whyLocked.needLine` | You're at {{currentAccuracy}}% — need {{requiredAccuracy}}% | أنت عند {{currentAccuracy}}٪ — تحتاج {{requiredAccuracy}}٪ |
| `child.subjects.whyLocked.generic` | Finish the previous lesson first | أكمل الدرس السابق أولاً |
| `child.subjects.whyLocked.cta` | Got it! | حسناً! |
| `child.skillTree.unitOf` | Unit {{current}} of {{total}} | الوحدة {{current}} من {{total}} |
| `child.skillTree.mastery` | Mastery {{percent}}% | الإتقان {{percent}}٪ |
| `child.skillTree.conceptEyebrow` | Concept · {{name}} | المفهوم · {{name}} |
| `child.skillTree.subInProgress` | In progress | قيد التقدم |
| `child.skillTree.subCompleted` | Mastered | مُتقن |
| `child.skillTree.subLocked` | Locked | مقفل |
| `child.skillTree.subBossChallenge` | Boss Challenge | تحدي البوس |
| `child.skillTree.subBossBeaten` | Boss Beaten | تم هزم البوس |
| `child.skillTree.bossLabel` | Boss | بوس |
| `child.skillTree.a11y.lockedHint` | double tap to see what unlocks this | اضغط مرتين لمعرفة ما يفتح هذا |
| `child.lessons.stub.title` | Lesson player coming soon | مشغّل الدروس قريباً |
| `child.lessons.stub.body` | We're building this lesson. Come back soon! | نُجهّز هذا الدرس. عُد قريباً! |
| `child.lessons.stub.back` | Back | عودة |
| `child.subjects.signOut` | Sign out | تسجيل الخروج |

Subject names (canonical, FE matches API `name` field case-insensitively for the defensive 4-subject filter):

| `subjectKey` | EN | AR |
|---|---|---|
| `math` | Math | الرياضيات |
| `science` | Science | العلوم |
| `arabic` | Arabic | العربية |
| `english` | English | الإنجليزية |

---

## 11. Delta against captures (deliberate deviations the FE should know are intentional)

| Capture | Capture shows | Spec ships | Reason |
|---|---|---|---|
| `mobile/06-subject-select.png` + `mobile-ar/06-*` | 5 subjects including Social Studies | 4 subjects (Math/Science/Arabic/English) | CLAUDE.md product decision; defensive filter on FE. |
| `mobile/06-subject-select.png` | English row uses purple-tinted "GB" flag bubble | English uses `$purple` tint + `🔤` glyph | Brand law: no decorative flags; semantic emoji only. Purple is shared with Arabic/badges in the capture — we move Arabic to `$accent` (orange) so each of the 4 subjects has a unique color identity. |
| `mobile/06-subject-select.png` | Arabic row uses purple tint | Arabic uses `$accent` (orange) tint + 📖 | Disambiguates from English (also purple in capture) and from the badge palette. |
| `mobile/06-subject-select.png` | Top title "Choose a Subject" | "Subjects" / "المواد" | i18n key `child.subjects.title` per brief; tighter copy + matches the cheat sheet's "lighter wayfinding" voice. |
| `mobile/09-skill-tree.png` | Bottom tab bar (Home/Skills/…) overlapping the tree | NO bottom tab bar in W11 | P2-09 / Wave 13 territory. |
| `mobile/09-skill-tree.png` | Skill nodes offset L/R (~30px) creating a curved path | Centered column, no offset | V1 ships linear; curved offsets need Skia path drawing — Wave 12+ polish. |
| `preview/components-skill-node.html` | Disc 80px | 72px | Tighter mobile column rhythm; deliberate. |
| `preview/components-lesson-card.html` | Card radius 24 | Card radius 24 (kept) | Matches preview; deliberate use of `$modal` not `$card` — hero affordance. |
| `preview/components-skill-node.html` | Active node pulse 2s | Spec matches | Brand law: ≤800ms for celebratory motion; pulse is idle/ambient, 2s loop is acceptable. |
| `preview/components-lesson-card.html` | "Math · Numbers" tag in eyebrow | Caller passes `tag` prop; FE composes from subject name + concept name (or just subject if concept unknown for that lesson — defensive). | API does NOT carry concept name on `LessonInUnitDto`; eyebrow may end up as just "Math" / "الرياضيات" until concept enrichment lands. Acceptable. |

---

## 12. Open questions (resolved here so FE doesn't have to ask)

1. **Empty Unit (unit with `lessons: []`)?** → Render the unit header normally + a single muted "(empty) — Coming soon" tile (radius `$card`, dashed `$borderStrong`, 14/700 `$fg3`). Don't hide the unit.
2. **Subject name from API is `Mathematics` not `Math` — does the filter still match?** → Caller normalizes the API name to lowercase + matches against a `Set(['math', 'mathematics', 'science', 'arabic', 'العربية', 'english', 'الإنجليزية', ...])`. The exact normalization map lives in `apps/student-app/app/(child)/_components/subjects.ts` (FE owns it). Designer accepts either matching by `subject.name` or by a known subject-key map derived from seeder constants — pick whichever the FE finds cleaner.
3. **Tree background gradient on web — RadialGradient unavailable?** → Fall back to flat `$bg`. Pulse + node glows carry the visual weight; the radial backdrop is polish.
4. **Pulse animation — Reanimated vs CSS keyframes?** → Use CSS `@keyframes pulse` on web, Reanimated `withRepeat(withSequence)` on native. If bundle size is tight, native may degrade to no-pulse (acceptable W11; flag in HANDOFF).
5. **Boss + Locked composition — which chrome wins?** → Boss disc + state glyph (🔒 inside the boss disc); see §2. Tap routes to WhyLockedSheet, not the lesson.
6. **Sheet on web — modal dialog or bottom sheet?** → Modal dialog (centered, max-width 480, `$modal` 24 all corners). Bottom sheet on native. Single component, platform-switch internally.
7. **SegmentedTabs as Tabs.orientation or its own primitive?** → FE choice. Spec accepts both.
8. **`MasteryBar` in the sheet prereq row — green or amber?** → **Amber** (`$accent`). Green = done; amber = "almost there, keep going" — supports the encouraging voice.

---

## 13. Design gaps logged (for design-system follow-up — NOT W11 blockers)

- **`$shadow-primary-glow-strong`**, **`$shadow-success-glow`**, **`$shadow-danger-glow`** — propose adding to `colors_and_type.css` + `tokens/shadows.ts` to keep skill-tree glows token-pure. W11 ships inline strings if not added.
- **Subject tint tokens** — `subjectMath` / `subjectScience` / `subjectArabic` / `subjectEnglish` need to land in `colors.ts`. Easy add.
- **Skill-tree curved connectors** — captures show curved offset paths. V1 ships straight strips; Skia/Reanimated polish pass deferred.
- **WhyLockedSheet promotion** — if a third consumer appears (e.g. Missions in Wave 13), promote to `@learnexia/ui`.
- **`BossBadge` as `Badge` variant** — confirm with FE that `Badge` can accept `variant="boss"` cleanly; if not, ship as standalone.
- **English subject's `🔤` glyph** — if product wants a flag, we revisit with a properly-localized non-geopolitical icon set.

Design spec ready for frontend.
