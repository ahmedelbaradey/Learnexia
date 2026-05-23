# Design Spec — P1-11 Pixel-Perfect Audit
## Student-App Screens vs. Design-System Captures

**Audit date:** 2026-05-23  
**Auditor:** designer agent  
**Source captures:** `design-system/screenshots/`  
**Token source of truth:** `design-system/colors_and_type.css`, `packages/design-system/src/tokens/`

---

## 1. Splash Screen

**Code:** `apps/student-app/app/index.tsx` + `src/components/DotPulse.tsx`  
**Capture:** `design-system/screenshots/mobile/01-splash.png`

### What the capture shows
- Full-bleed background `#0F172A` (`$bg`) with a radial purple glow centered behind the logo text area.
- The **wordmark "Learnexia"** in Poppins ExtraBold (display-size ~36–40px), white, centered at vertical midpoint.
- Subtitle copy: **"AI Learning Adventure Begins"** in Poppins Regular ~14–15px, `$fg2` (`#CBD5E1`), centered, two lines.
- **Three dot loading indicator** (staggered pulse), leftmost and rightmost in `$primary` (`#4F46E5`), center in a brighter/saturated indigo — all three are `#4F46E5` colored.
- A horizontal **progress bar** below the dots, dark track, partially filled with a linear-gradient (indigo/purple).
- **"Loading... ⚡"** text label below the progress bar in small caps or body-sm.
- Footer strip: **"POWERED BY AI"** in caps tracking-wide `$fg3`, and below: **"✦ Gamified Learning ✦"** with decorative diamonds.
- No mascot owl visible in the capture.
- Background has scattered small dots (particle pattern) on the purple-glow layer.

### Implementation vs. capture

| Element | Capture | Implementation | Status | Severity |
|---|---|---|---|---|
| Background color | `#0F172A` (`$bg`) | `backgroundColor="$bg"` | Match | — |
| Logo asset | Wordmark "Learnexia" text rendered large (~36-40px) | `assets.logo` image 160×80 (contains the wordmark) | Match | — |
| Mascot owl | Not visible in capture | Rendered 120×120 unless `hasFlash` | **DEVIATION** | Major |
| Subtitle copy | "AI Learning Adventure Begins" centered | Not rendered in code — no subtitle Text node | **MISSING** | Blocker |
| Loading dots | Three dots, all `$primary` (#4F46E5) | `DotPulse` renders 3 dots in `$primary` | Match | — |
| Progress bar | Horizontal bar with gradient fill below the dots | Not implemented — only DotPulse, no bar | **MISSING** | Major |
| "Loading... ⚡" label | Present below bar | Not implemented | **MISSING** | Major |
| "POWERED BY AI" footer | Present, caps, `$fg3`, with diamond-glyph "Gamified Learning" line | Not implemented | **MISSING** | Major |
| Particle/star scatter background | Visible in capture (decorative dots on BG) | Not implemented | Minor |
| Radial purple glow on BG | Visible behind logo area | Not implemented | Minor |
| Font size of wordmark | Display ~36-40px | Logo is an image — depends on asset; acceptable | Acceptable | — |

### RTL note
Splash is brand animation; RTL layout is not required here (LTR-always by spec). No issue.

---

## 2. Login Screen

**Code:** `apps/student-app/app/(auth)/login.tsx` + `(auth)/_components/LoginForm.tsx`  
**Captures:** `design-system/screenshots/mobile/02-login.png` + `web/02-login.png`

### What the mobile capture shows
- App-icon logo mark (rounded-square purple icon with orange star) — **NOT the wordmark**.
- Heading: **"Welcome back"** Poppins ExtraBold ~28px `$fg1`.
- Subtitle: **"Log in to keep your streak alive 🔥"** Poppins Regular 14px `$fg3`.
- **Parent / Student toggle**: segmented pill control, "👨‍👩‍👧 Parent" tab is active (`$primary` fill, white text), "🎓 Student" tab is inactive (ghost/`$card` bg). Width full.
- **Email** label + input, **Password** label + input with "Show" right-aligned link-text.
- **Forgot password?** right-aligned link in `$primaryLight`.
- **"Log In →"** CTA button full-width, currently disabled state (muted bg ~`$card`, label `$fg4`).
- **"OR"** divider with horizontal rules.
- **Google** and **Apple** social buttons (side-by-side, ~`$cardSoft` bg, white text + brand icons), no Microsoft on mobile.
- **"New here? Create parent account"** footer link.

### What the web capture shows (split-panel)
- Left brand panel: `$primary` (#4F46E5) full-height, stars particle bg, logo wordmark + mark top-left, large star illustration center, tagline "Welcome back to the adventure." + description, "240,000+ kids learning today" social proof with flame icon.
- Right form panel: `$bg` (#0F172A), "LOG IN" label uppercase `$fg3`, same "Welcome back" heading H1, Parent/Student toggle, same form fields + "Remember me" checkbox + "Forgot password?", Log In button, "OR CONTINUE WITH" divider, **Google + Apple + Microsoft** social buttons (3 equal-width), "New to Learnexia? Create parent account" footer.

### Implementation vs. capture

| Element | Capture | Implementation | Status | Severity |
|---|---|---|---|---|
| Logo in header | Logo-mark icon (rounded square, app-icon shape) | `assets.mascotOwl` (owl image 80×80) + `assets.logo` (wordmark 140×64) | **DEVIATION** — capture shows app-icon mark, not mascot + wordmark stacked | Major |
| Parent/Student persona toggle | Full-width segmented pill (active=`$primary`, inactive=`$card`) | **Not implemented** — LoginForm has no toggle | **MISSING** | Blocker |
| "Show" password reveal label | Inline right-aligned text "Show" next to Password label | TextField has 👁 icon button (eye emoji) — not the text "Show" | Minor |
| "Forgot password?" link | Right-aligned below password field | **Not implemented** | Major |
| "OR" divider | Horizontal rule + "OR" or "OR CONTINUE WITH" text | **Not implemented** | Major |
| Google social button | Present (mobile: half-width; web: 1/3-width) | **Not implemented** | Major |
| Apple social button | Present (mobile: half-width; web: 1/3-width) | **Not implemented** | Major |
| Microsoft social button | Present on web only (1/3-width) | **Not implemented** | Major |
| "Remember me" checkbox | Present on web only | **Not implemented** (web path) | Major |
| "240,000+ kids learning today" social proof | Left panel, web only | **Not implemented** (web left panel entirely absent) | Blocker |
| Web split-panel layout | Left brand panel + right form panel at ≥768px | FormScaffold centers a card at tablet+ — no brand panel | **MISSING** (Blocker for web) | Blocker |
| "LOG IN" eyebrow label | Present on web (above heading, uppercase `$fg3`) | Not implemented | Minor |
| Heading size | ~28px Poppins ExtraBold | `fontSize={24}` fontWeight="700" | Minor (should be 28px / fontWeight 800) |
| Subtitle text | "Log in to keep your streak alive 🔥" | Reads from i18n key `auth.login.subtitle` — content match depends on translation file | Acceptable (audit content value at runtime) |
| Footer link text | "New here? Create parent account" | `auth.login.newParent` + `auth.login.createAccount` i18n | Match (structure correct) |
| Background | `$bg` dark | `$bg` | Match |
| Input height | 48px per preview spec | TextField renders 52px (local `height={52}`) | Minor — input spec is 48px (`height:48px` in components-input.html), impl uses 52px |
| Input border-radius | 14px (per `inputRadius` local const) | 14px | Match |

### RTL note
`writingDirection` and `direction` props are threaded through correctly. The login screen row for footer uses logical `flexDirection` based on locale. No issue found in RTL handling of what exists.

---

## 3. Register Screen

**Code:** `apps/student-app/app/(auth)/register.tsx` + `(auth)/_components/RegisterForm.tsx`  
**Captures:** `design-system/screenshots/web/03-register.png` + `mobile/03-register.png`

### What the captures show

**Mobile:**
- "STEP 1 OF 2" in `$primary` uppercase small-caps / tracking-wide.
- Heading: "Create parent account" ExtraBold ~26px.
- Subtitle: "You'll add your children's accounts in the next step."
- **Info banner**: purple-soft bg (`$primarySoft`), "👨‍👩‍👧 Parent / Guardian" label in `$primary` bold, body text about kids not being able to self-register.
- Fields in order: Full name (with person icon in label), Email (with envelope icon in label), Password (with lock icon in label), Country (with globe icon in label, dropdown-style).
- **Terms checkbox**: "I'm a parent or legal guardian and I agree to the Terms and Privacy Policy, including consent to create accounts for my children."
- CTA: "Continue → Add Children" (not visible fully due to scroll).
- No "Confirm password" field visible in mobile capture — only 4 fields: name, email, password, country.

**Web:**
- Left panel: `$bg` dark, "STEP 1 OF 2" eyebrow + step progress bar (filled portion = `$primary`, unfilled = `$border`), Heading "Create your parent account" H1, subtitle, info banner, two-column row for Full name + Country, then Email, Password with hint "At least 6 characters", Terms checkbox, CTA.
- Right panel: `$primary`-to-purple gradient, illustration (game controller), "Set up once. Watch them learn forever." + 4 feature bullets (AI-powered, weekly reports, daily missions, COPPA-compliant).
- **No Confirm Password field** visible on either capture.

### Implementation vs. capture

| Element | Capture | Implementation | Status | Severity |
|---|---|---|---|---|
| "STEP 1 OF 2" eyebrow | Present, `$primary` uppercase text | **Not implemented** | Blocker |
| Step progress bar | Thin bar, filled portion = `$primary` | **Not implemented** | Major |
| Info banner (parent-only) | `$primarySoft` bg card with icon + copy | **Not implemented** — no such banner in RegisterForm | Blocker |
| Field order | Name, Email, Password, Country (mobile: 4 fields; web: Name+Country same row) | Name, Email, Password, **ConfirmPassword** (4 fields, no Country, Country is in AddChildForm instead) | **DEVIATION** — ConfirmPassword present but capture shows Country, not ConfirmPassword | Major |
| Country field | Present (globe icon, dropdown) in capture | **Missing** from RegisterForm — it's in AddChildForm | Major |
| Confirm Password field | Not visible in captures | Present in implementation (`confirmPassword` field) | Extra field | Minor |
| Terms checkbox | Present: "I'm a parent or legal guardian..." with linked Terms + Privacy Policy | **Not implemented** | Blocker |
| Label icons | Person icon, envelope icon, lock icon, globe icon as prefix in labels | Not implemented — labels are plain text uppercase | Minor |
| "STEP 1 OF 2" + step counter | Present | Not implemented | Blocker |
| Web split-panel (right feature panel) | Present: gradient panel with illustration + 4 bullets | Not implemented — FormScaffold shows centered card, no split | Blocker (web) |
| Password hint text | "At least 6 characters" below field | Not implemented — only validation error shown | Minor |
| Heading size | ~28px ExtraBold | `fontSize={28}` fontWeight="800" | Match |
| Logo mark at top | Small logo-mark (app-icon) top-left on mobile | `assets.logoMark` 48×48 | Match |
| CTA label | "Continue → Add Children" | `auth.register.submitButton` i18n — must map to this exact copy | Minor (verify i18n value) |
| ScreenHeader back button | Present (← back to login) | `ScreenHeader` with `onBack` | Match |
| Background | `$bg` | `$bg` | Match |

---

## 4. My Children Screen

**Code:** `apps/student-app/app/(parent)/children.tsx` + `(parent)/_components/MyChildren.tsx` + `packages/ui/src/components/ChildCard/`  
**Captures:** `design-system/screenshots/web/04-my-children.png` + `mobile/07-my-children.png`

### What the captures show

**Mobile:**
- Eyebrow: "👨‍👩‍👧 PARENT · AHMED" in `$primary` uppercase small text.
- Heading: "My Children" ~26px ExtraBold.
- Subtitle: "2 children linked to your account" `$fg3`.
- **FAB/Add button** (top-right, circular `$primary` bg, `+` glyph, ~52×52px pill).
- **This Week summary strip**: `$primary`/purple gradient card — "THIS WEEK - ALL CHILDREN" label + 3 stats: Total XP (star icon), Lessons (book icon), Active (flame icon), each with large number + label below.
- **Child cards** (one per child): avatar circle (letter initial, colored), Name + Grade badge, email below, stat row (Lv + XP + Streak + Active status dot), Language info + "View progress →" link. Separator between cards via card borders.
- **"+ Add a child"** dashed-border action card at bottom: "Set their grade, language, and login email".
- Bottom safety strip: lock icon + "You can see only your own children's progress."

**Web:**
- Left sidebar: nav (My Children active, Overview, Reports, Activity, Subjects, Settings), child switcher at top (Sami · Grade 3 · Level 12 with orange avatar).
- Main area: "My Children" heading + "3 children linked" subtitle + "This week" dropdown + "Send Report" button.
- Full-width summary strip (purple gradient): "Your family is on a roll", 4 stats: Total XP, Lessons, Best Streak, Badges Earned, + avatar cluster.
- Grid of 3 ChildCards (one per column) with detailed stats: avatar, name, grade, active status, XP, streak, level, mastery bar (gradient), weakest topic, "View dashboard →" link.
- "+ Add Child" dashed-border card below grid.

### Implementation vs. capture

| Element | Capture | Implementation | Status | Severity |
|---|---|---|---|---|
| "PARENT · AHMED" eyebrow | Present, `$primary`, uppercase, above heading | **Not implemented** | Major |
| Heading "My Children" | `$fg1` ExtraBold ~26px | `ScreenHeader` title at 18px fontWeight 700 | **Deviation** — ScreenHeader is too small (18px/700 vs 26px/800), and ScreenHeader puts title in a back-button row, not standalone below eyebrow | Major |
| Subtitle "N children linked" | Below heading, `$fg3` 14px | **Not implemented** in `children.tsx` or `MyChildren` loaded state | Major |
| FAB "+ Add" button (mobile) | Circular `$primary` pill, top-right, ~52px | **Not implemented** — current impl has a ghost button at bottom of list | Major |
| Weekly summary strip | Purple gradient card with combined family stats | **Not implemented** | Blocker |
| Child card avatar style | Colored circle (orange for Sami, purple for Layla) specific to child | ChildCard uses gradient `#A78BFA` → `#334155` for all avatars | Minor (single gradient for all vs. per-child color) |
| Child card stat row | Level badge + XP + Streak + Active status dot in card body | ChildCard `selectable` variant shows only: avatar + fullName + meta (grade·language) + chevron | **DEVIATION** — missing XP, Streak, Level, Active status in card | Major |
| "View progress →" link | Present inside each child card | **Not implemented** inside ChildCard | Major |
| Language info row | "Language: GB English" | Rendered as `meta` prop in format "Grade 1 · en" via `MyChildren.tsx` passing `child.email` as meta | **Deviation** — meta is email, not grade+language | Major |
| "+ Add a child" action card | Dashed-border card, icon + title + subtitle | Bottom of list has a ghost `Button` — no dashed-border card style | Major |
| Safety strip ("only your own children") | Visible at bottom, lock icon + muted text | **Not implemented** | Minor |
| Web left sidebar nav | Full sidebar with nav items | **Not implemented** — parent web view is a pushed screen with ScreenHeader, not sidebar layout | Blocker (web) |
| Web "Send Report" button | Top-right header action | **Not implemented** | Minor |
| Web "This week" period selector | Dropdown top-right | **Not implemented** | Minor |
| Web summary strip (purple gradient, badges stat) | 4-stat strip (XP, Lessons, Streak, Badges) | **Not implemented** | Blocker (web) |
| Web 3-column card grid | Cards displayed in a responsive 3-col grid | Rendered as a vertical list | Major (web) |
| Web mastery bar in card | `lx-grad-xp` gradient bar + % label | **Not implemented** | Major |
| Web "Weakest:" topic row | Below mastery bar | **Not implemented** | Minor |
| Web "View dashboard →" link | Per-card | **Not implemented** | Major |

---

## 5. Child Home Screen

**Code:** `apps/student-app/app/(child)/index.tsx`  
**Capture:** `design-system/screenshots/mobile/08-home.png`

### What the capture shows
- **HUD bar** at the top (inside safe area): Streak pill (🔥 7, `$streakSoft`/`$streak`), Hearts pill (❤️ 4, `$heartSoft`/`$heart`), XP pill (⭐ 1,240, `$xpSoft`/`$xp`), Gem pill (💎 42, `$gemSoft`/`$gem`). All pills are pill-shaped with icon + value. Pill bg matches the preview from `components-hud.html`.
- **Child avatar + greeting**: circular avatar (purple bg with owl mascot, ~48px), text "Welcome back, **Sami!**" (name bold, ~22px ExtraBold).
- **"Continue Learning 📚 See all"** section header: "Continue Learning" bold ~18px `$fg1`, book icon, "See all" text link right-aligned `$primary`.
- **Subject cards** (2-column grid): Math card (abacus icon, subject name ExtraBold 18px, "Fractions · 60%" subtitle, green progress bar) + Science card (flask icon, "Plants · 35%", green-to-indigo progress bar). Cards have `$card` bg, `$card` border, 20px radius (`$card` token).
- **"Daily Quests"** section header: "Daily Quests" bold + "See all →" link.
- **Mission rows**: styled per `components-missions.html` — icon (48×48 rounded-14 bg), title, sub-progress, XP reward pill. Row bg `$card`, radius 20px. Two visible rows.
- **Bottom nav bar**: Home (active, filled house icon), Skills (bag/backpack icon), Quests (target icon), League (trophy icon), Me (person icon). Nav bg `$card`/dark, 5 items.

### Implementation vs. capture

| Element | Capture | Implementation | Status | Severity |
|---|---|---|---|---|
| HUD bar (streak/hearts/XP/gem pills) | Present, pill-shaped, 4 gamification stats, full-width row | **Not implemented** — no HUD, no StreakFlame, no Hearts, no XPBar rendered | Blocker |
| Child avatar circle with mascot | Purple circle ~48px with owl inside | Simple `assets.mascotOwl` image 140×140 floating center | **Deviation** — capture has small avatar in greeting row; impl shows large centered mascot as hero | Blocker |
| Greeting text | "Welcome back, **Sami!**" inline (avatar + text row) | `t('child.home.greeting', { childName })` text 26px centered — no inline avatar | Major |
| "Continue Learning" section | Section with subject cards | **Not implemented** | Blocker |
| Subject cards (2-column grid) | Math + Science (4 subjects, 2 shown) | **Not implemented** | Blocker |
| "Daily Quests" section | Mission rows | **Not implemented** | Blocker |
| Bottom navigation bar | 5-tab nav: Home/Skills/Quests/League/Me | **Not implemented** — no tab bar rendered | Blocker |
| Sign-out button | Not in capture — is a small "Sign out" text in header | Present in code (top-right text button) | Extra element — acceptable for MVP placeholder |
| Logo mark in header | Not clearly visible in capture header | Logo mark 32×32 top-left | Minor |
| Screen layout | Scrollable content with sticky HUD + bottom nav | Single centered column with mascot + greeting + AITutorBubble | **Complete structural deviation** | Blocker |
| AITutorBubble | Not present on home screen in capture | Present (mascot message bubble rendered prominently) | Extra element | Minor |
| Background | `$bg` | `$bg` | Match |
| Child name resolution | First-name only "Sami" | Uses `fullName.split(' ')[0]` | Match |

---

## 6. Supplementary Screens (no direct capture — token/style consistency audit)

### 6a. `(onboarding)/add-child.tsx` + `AddChildForm`

**Deviations found:**
- No "STEP 2 OF 2" eyebrow + step progress bar (matches capture pattern from register step 1/2).
- The form card uses `$cardSoft` bg with padding — acceptable, but no explicit border visible in spec.
- `AddChildForm` uses `variant="secondary"` for the "Add to List" button. The secondary variant is `$cardSoft` bg — visually acceptable but the capture (if it were visible) would likely show a `$primary` ghost-style for this action. **Flag as Minor.**
- Country is a free-text `TextField`, not a country-flag dropdown like the register capture suggests. **Minor** inconsistency.
- ProgressSteps component (`packages/ui/src/components/ProgressSteps/index.tsx`) exists but is not used. **Major gap** — the "STEP 1 OF 2" + bar pattern needs `ProgressSteps` on both register and add-child screens.

### 6b. `(onboarding)/complete.tsx`

**Deviations found:**
- Check icon is a plain text character `'✓'` inside a `$successSoft` circle. Spec / preview would expect this to be a more polished icon (SVG from `design-system/assets/icons/`). Flag as Minor.
- No confetti or celebration animation (the design system spec mentions `correct=confetti+pulse` and `level-up burst` for reward/success screens). A spring-animated scale pop is implemented (Moti), but no confetti. **Minor** for this placeholder screen.
- `$success` token for the text — correct.
- The `maxWidth={320}` constraint on the CTA button is correct behavior.

### 6c. `(parent)/link-child.tsx` + `LinkChildForm`

**Deviations found:**
- No eyebrow or contextual header copy explaining the link-by-email concept before the form — only `ScreenHeader` title and then the explanation text. Consistent with spec intent.
- The success state renders a `ChildCard variant="linking"` — the `StatusBadge` success shows `✓` in a 24px circle. Acceptable.
- `$success` color for the success title text — correct.
- No animation on success state. Minor.

### 6d. `EditChildSheet`

**Deviations found:**
- Modal drag handle (40×4 pill, `$cardSoft`) — matches the modal spec pattern (radius 24px modal, drag handle).
- `borderTopStartRadius="$modal"` / `borderTopEndRadius="$modal"` — maps to 24px. Correct.
- `$overlay` bg on the backdrop — correct.
- Close button uses `✕` glyph text — acceptable.
- No `ScrollView` inside the sheet — if the form is taller than the sheet `maxHeight="85%"`, content will be clipped. **Flag as Major** — needs a ScrollView inside the sheet content area for smaller devices.
- `visible && initialValues` guard prevents rendering the form before it has data — correct.

### 6e. `FormScaffold`

**Deviations found:**
- Phone: full-width column on `$bg`, `$6` (24px) horizontal padding. Correct.
- Tablet+ (`$tablet`): centered card, max-width 480, `$card` bg, `$card` radius, `$8` (32px) padding, `$border` border. Token usage correct.
- **Missing** the web split-panel layout required by login (web/02) and register (web/03) — the split brand + form panel. FormScaffold cannot produce this without a branch on route or a `SplitFormScaffold` variant. **Blocker** — the web login and register captures require a fundamentally different two-column layout that FormScaffold does not support.

### 6f. `ScreenHeader`

- Height 56px, `$6` (24px) horizontal padding, RTL-flipped chevron. Token usage correct.
- Title font 18px / weight 700. The My-Children capture heading is visually ~26px ExtraBold — the ScreenHeader title size is undersized for that context. See gap in Screen 4 above.
- The back-chevron uses the `‹` Unicode character at 22px. Spec does not mandate a specific icon, but the `design-system/assets/icons/` SVG icons should be used for pixel-perfect rendering. Flag as **Minor**.

### 6g. `ServerErrorBanner`

- `$dangerSoft` bg, `$danger` text, `$sm` radius (8px), `$3` padding (12px). Correct per spec.
- `accessibilityLiveRegion="assertive"` — correct.
- Text alignment is `textAlign="left"` regardless of direction; for RTL should be `"right"`. **Minor** RTL bug.

### 6h. `DotPulse`

- Three dots in `$primary`, staggered Moti pulse. Correct. Size 10×10 is slightly smaller than the dots visible in the capture (appear ~10–12px), acceptable.

---

## Severity-Sorted Gap List

### BLOCKER (wrong layout / missing element that breaks the screen)

| ID | Screen | Gap | Correct Token / Approach |
|---|---|---|---|
| B-01 | Login (web) | Split brand panel entirely absent — FormScaffold has no two-column layout at `$tablet`+ | Create `SplitFormScaffold` variant with left `$primary` brand panel + right `$bg` form panel at `$tablet` breakpoint |
| B-02 | Login (mobile + web) | Parent/Student persona toggle missing | New segmented pill component: active tab `$primary` bg, inactive `$card`, pill-radius `$pill`, full-width, Poppins 700 15px |
| B-03 | Register (web) | Right feature panel (gradient + bullets) absent | `SplitFormScaffold` right panel: `lx-grad-levelup` (`#A855F7`→`#6366F1`) bg with illustration + bullet list |
| B-04 | Register (mobile + web) | "STEP 1 OF 2" eyebrow + step progress bar missing | Use `ProgressSteps` component from `packages/ui/src/components/ProgressSteps/`; or new `StepIndicator` with `$primary` fill |
| B-05 | Register (mobile + web) | Info banner ("Parent / Guardian only") missing | `Card` variant with `$primarySoft` bg, `$primary` label text, `$body` copy — matches `design-system/preview/components-hud.html` pill style |
| B-06 | Register | Terms + Privacy Policy checkbox entirely absent | New `CheckboxField` component: 24×24 checkbox, `$primary` checkmark when checked, linked text in `$primaryLight` |
| B-07 | My Children (web) | Left sidebar nav entirely absent | Requires sidebar layout for web; `$card` bg sidebar, nav items with `$primary` active state |
| B-08 | My Children (mobile + web) | Weekly family summary strip (purple gradient card) absent | `Stack` with `lx-grad-levelup` bg, border-radius `$card` (20px), padding `$6`, 3–4 stat items |
| B-09 | Child Home | Entire content structure is wrong — HUD + sections + bottom nav all absent; only mascot + greeting + AITutorBubble rendered | Full re-implementation required (see handoff below) |
| B-10 | Child Home | Bottom tab navigation bar absent | 5-tab bar: Home/Skills/Quests/League/Me; bg `$card`, active icon `$primary`, inactive `$fg3` |

### MAJOR (wrong token / important missing element)

| ID | Screen | Gap | Correct Token / Approach |
|---|---|---|---|
| M-01 | Splash | Mascot owl rendered by default; capture shows no mascot | Remove unconditional mascot; it should be hidden or replaced by the subtitle copy |
| M-02 | Splash | Subtitle "AI Learning Adventure Begins" missing | `Text` color `$fg2`, fontSize 14, fontFamily `$body`, textAlign center |
| M-03 | Splash | Progress bar below dots missing | Horizontal `Stack` h:6, `$bg` track, gradient fill `lx-grad-xp` (#22C55E→#4F46E5), radius `$pill` |
| M-04 | Splash | "Loading... ⚡" label missing | `Text` color `$fg3`, fontSize 14, `$body` |
| M-05 | Splash | "POWERED BY AI" + "✦ Gamified Learning ✦" footer missing | `Text` color `$fg3`, fontSize 12, `$small` class (uppercase, tracking-wide) |
| M-06 | Login | Logo area: capture shows app-icon logo-mark (purple rounded-square icon), not mascot+wordmark | Replace with `assets.logoMark` in rounded-square container (purple gradient bg, 72×72, radius 20) |
| M-07 | Login | "Forgot password?" link absent | `Text` color `$primaryLight`, fontSize 14, fontWeight 600, aligned to end (RTL-aware), `onPress` → forgot-password route |
| M-08 | Login | "OR" / "OR CONTINUE WITH" divider absent | Two `Stack h:1 bg:$border` + centered `Text` color `$fg3`, fontSize 12, tracking-wide |
| M-09 | Login | Google OAuth button absent | `Button` variant secondary, iconBefore Google SVG, label "Google" |
| M-10 | Login | Apple OAuth button absent | `Button` variant secondary, iconBefore Apple SVG (from `design-system/assets/icons/`), label "Apple" |
| M-11 | Login | Microsoft OAuth button (web only) absent | `Button` variant secondary, iconBefore Microsoft SVG, label "Microsoft" |
| M-12 | Login | Heading size: 24px/700 vs. capture ~28px/800 | `fontSize={28}` `fontWeight="800"` |
| M-13 | Register | Country field missing from RegisterForm (placed in AddChildForm instead) | Add `country` field to `RegisterParentFormValues`/`registerParentSchema` + RegisterForm |
| M-14 | Register | Confirm Password field present but not in captures | Remove `confirmPassword` from visible fields (or make it server-validated only); capture omits it |
| M-15 | Register | Field label icons (person, envelope, lock, globe) missing | Prefix icon before each label; use `design-system/assets/icons/` SVGs |
| M-16 | My Children | Eyebrow "PARENT · {name}" missing | `Text` color `$primary`, fontSize 12, fontWeight 600, textTransform uppercase |
| M-17 | My Children | Heading is in ScreenHeader (18px/700); should be standalone ~26px/800 below eyebrow | Move heading out of ScreenHeader into page body as `Text` fontSize 26 fontWeight 800 |
| M-18 | My Children | Subtitle "N children linked" missing | `Text` color `$fg3`, fontSize 14, `$body` |
| M-19 | My Children | FAB "+" button (mobile) absent | `Stack` w:52 h:52 radius `$pill` bg `$primary`, `+` icon, `position="absolute"` top-right |
| M-20 | My Children | Child card stat row (Level + XP + Streak + Active) absent from ChildCard selectable variant | Extend `ChildCard` with optional `stats` prop: Level, XP, streak value + active indicator dot |
| M-21 | My Children | Meta shows `child.email`; should show grade + language | Change `meta` prop passed in `MyChildren.tsx` to `${grade} · ${language}` localized string |
| M-22 | My Children | "View progress →" link inside child card absent | Add `onViewProgress` action to ChildCard selectable variant |
| M-23 | My Children | "+ Add a child" dashed-border card absent | New `AddChildCTA` card: `$card` bg, `$border` dashed border (2px dashed), icon circle, title + subtitle |
| M-24 | Child Home | HUD bar (streak/hearts/XP/gem pills) absent | Use HUD from `components-hud.html`: `StreakFlame`, `Hearts`, `XPBar` area, gem pill arranged in a horizontal pill container; bg `$card` with `$border`, radius `$pill` |
| M-25 | Child Home | "Continue Learning" section with 2-col subject card grid absent | Section header (`Text` 18px/700) + `See all` link + `FlatList` 2-col grid of subject `LessonCard` components |
| M-26 | Child Home | "Daily Quests" section with mission rows absent | Section header + mission rows per `components-missions.html` (bg `$card`, radius 20, icon 48×48 rounded-14, progress bar) |
| M-27 | EditChildSheet | No ScrollView inside sheet content; form can overflow on small devices | Wrap form content in `ScrollView` inside `Stack maxHeight="85%"` |

### MINOR (small spacing / token / icon mismatch)

| ID | Screen | Gap | Correct Token / Approach |
|---|---|---|---|
| m-01 | Splash | Particle/star scatter background absent | Decorative only — scatter 6–8 `Stack` dots (2×2, `$primary` 0.15 opacity) via `position="absolute"` |
| m-02 | Splash | Radial purple glow behind logo absent | `Stack` `position="absolute"` with radial gradient bg or a semi-transparent `$primarySoft` circle ~200px diameter |
| m-03 | Login | Password field reveal shows 👁 emoji; capture shows text "Show" | Change to `Text` "Show" / "Hide" with color `$primaryLight`, fontSize 14, fontWeight 600 |
| m-04 | Login | "LOG IN" eyebrow label (web) absent | `Text` color `$fg3`, fontSize 12, fontWeight 600, textTransform uppercase, letterSpacing wide |
| m-05 | Login | "Remember me" checkbox (web) absent | Same `CheckboxField` component as B-06 |
| m-06 | Login | Input height 52px vs. preview spec 48px | Change TextField wrapper `height` to 48px (`$12` maps to 48 in size scale) |
| m-07 | Register | Password hint "At least 6 characters" absent below field | `Text` color `$fg3`, fontSize 12, `$body`, rendered below password `TextField` unconditionally |
| m-08 | My Children | Avatar gradient is single fixed `#A78BFA`→`#334155` for all; capture shows distinct per-child colors | Pass `avatarColor` prop (deterministic from name hash) to ChildCard; map to palette |
| m-09 | My Children | "You can see only your own children's progress" safety strip absent | `Stack` flexRow, lock icon + `Text` color `$fg3`, fontSize 12, marginTop `$4` |
| m-10 | My Children | ChildCard `selectable` meta prop receives email, should receive grade+language | Caller fix: `MyChildren.tsx` line 94 `meta: child.email` → `meta: childMeta(child)` |
| m-11 | onboarding/complete | Check icon is text `'✓'`; should use SVG icon | Use `design-system/assets/icons/` check SVG or a proper icon component |
| m-12 | onboarding/complete | No confetti animation on success | Moti: import `MotiView` animate sequence; or a lightweight confetti lib |
| m-13 | ServerErrorBanner | `textAlign="left"` ignores RTL (should be `"right"` for `dir=rtl`) | `textAlign={direction === 'rtl' ? 'right' : 'left'}` |
| m-14 | ScreenHeader | Back chevron `‹` is Unicode character, not design-system SVG icon | Use `design-system/assets/icons/chevron-left.svg` (if available) |
| m-15 | AddChildForm | "Add to List" button uses `variant="secondary"`; likely should be primary-style action | Change to `variant="primary"` or outline-primary style |

---

## Implementation Handoff

### Tokens — no new tokens needed; all existing

| Gap ID | Fix | Target token |
|---|---|---|
| B-01, B-03 | Brand panel bg | `lx-grad-levelup` (`#A855F7`→`#6366F1`) from `packages/design-system/src/tokens/gradients.ts` |
| B-05, B-08 | Summary strip | `$primarySoft` bg / `lx-grad-levelup` for the gradient card |
| M-24 | HUD pills | `$streak` / `$streakGlow` / `$heart` / `$heartGlow` / `$xp` / `$xpGlow` / `$gem` from `packages/design-system/src/tokens/colors.ts` |
| m-01, m-02 | Splash particles/glow | `$primarySoft` = `rgba(79,70,229,0.18)` |

### New / extended UI components needed

| Component | Location | Basis |
|---|---|---|
| `SplitFormScaffold` | `apps/student-app/src/components/SplitFormScaffold.tsx` | Left: `$primary` brand panel; right: `$bg` form panel; splits at `$tablet` (768px); replaces `FormScaffold` on login + register web paths |
| `PersonaToggle` | `packages/ui/src/components/PersonaToggle/index.tsx` | Segmented pill: parent/student tabs; active = `$primary` fill white text; inactive = `$card` bg `$fg3` text; radius `$pill`; 52px height; Poppins 700 15px |
| `StepIndicator` | `packages/ui/src/components/StepIndicator/index.tsx` OR reuse existing `ProgressSteps` | "STEP N OF M" text `$primary` + progress bar: filled `$primary`, unfilled `$border`, height 4px, radius `$pill` |
| `ParentInfoBanner` | `apps/student-app/app/(auth)/_components/ParentInfoBanner.tsx` | `Card` with `$primarySoft` bg, family emoji icon, "Parent / Guardian" label in `$primary` bold, body text `$fg2` |
| `CheckboxField` | `packages/ui/src/components/CheckboxField/index.tsx` | 24×24 checkbox (`$card` bg, `$primary` checkmark when checked), label with inline links in `$primaryLight`; 48px min touch target |
| `FamilySummaryStrip` | `apps/student-app/app/(parent)/_components/FamilySummaryStrip.tsx` | `lx-grad-levelup` gradient card, 3–4 stat columns (icon + number + label), radius `$card` (20px) |
| `HUDBar` | `apps/student-app/app/(child)/_components/HUDBar.tsx` | Horizontal row of 4 pills; wraps `StreakFlame`, `Hearts`, `XPBar` (compact), gem pill; pill bg = color-soft, text = color-accent, radius `$pill`; ref `components-hud.html` |
| `SubjectCard` | `apps/student-app/app/(child)/_components/SubjectCard.tsx` OR extend `LessonCard` | 2-col grid card: subject icon, name 18px/800, topic+% subtitle `$fg3`, progress bar gradient `lx-grad-xp`; bg `$card`, radius `$card` (20px) |
| `MissionRow` | Use existing or extend from `packages/ui` | Ref `components-missions.html`: bg `$card`, radius 20, icon 48×48 rounded-14, title/sub, XP reward pill `$xpSoft`/`$xp` |
| `ChildHomeTabBar` | `apps/student-app/app/(child)/_layout.tsx` | 5-tab Expo Router tab bar; bg `$card`; active tab icon `$primary`, label `$primary`; inactive `$fg3`; height 60px + safe-area-bottom |

### Route / file targets

| Gap | File to edit / create |
|---|---|
| B-01, B-03 web split-panel | Create `apps/student-app/src/components/SplitFormScaffold.tsx`; update `(auth)/login.tsx` and `(auth)/register.tsx` to use it on web |
| B-02 persona toggle | Create `packages/ui/src/components/PersonaToggle/`; add to `(auth)/login.tsx` above email field |
| B-04, B-05, B-06 register form | Edit `(auth)/register.tsx` + `RegisterForm.tsx`; add `StepIndicator`, `ParentInfoBanner`, `CheckboxField`; add `country` field; remove or relocate `confirmPassword` |
| B-07 web sidebar | Create `apps/student-app/app/(parent)/_layout.tsx` with web-aware sidebar nav |
| B-08 family summary | Create `FamilySummaryStrip.tsx`; add to `(parent)/children.tsx` between ScreenHeader and list |
| B-09, B-10 child home | Rewrite `(child)/index.tsx` to full spec layout with HUDBar, greeting row, Continue Learning section, Daily Quests section; add `(child)/_layout.tsx` with tab bar |
| M-01–M-05 splash | Edit `app/index.tsx`: remove unconditional mascot, add subtitle Text, add progress bar Stack, add "Loading..." label, add footer labels |
| M-06 login logo | Edit `(auth)/login.tsx`: replace mascot+wordmark with logo-mark in purple gradient rounded-square container (72×72, radius 20) |
| M-07–M-11 login social | Edit `LoginForm.tsx`: add Forgot password link, OR divider, social buttons (Google/Apple on both, Microsoft on web via `$tablet` condition) |
| M-16–M-23 My Children header | Edit `(parent)/children.tsx` to add eyebrow + hero heading + subtitle + FAB; edit `MyChildren.tsx` to fix meta prop and add AddChildCTA |
| m-13 RTL banner | Edit `src/components/ServerErrorBanner.tsx`: `textAlign={direction === 'rtl' ? 'right' : 'left'}` |
| m-10 meta fix | Edit `(parent)/_components/MyChildren.tsx` line 94: `meta: childMeta(child)` where `childMeta` formats grade + language |
| m-27 EditChildSheet scroll | Edit `(onboarding)/_components/EditChildSheet.tsx`: wrap form content in `ScrollView` |

---

## Design Gaps / Open Questions

1. **Social auth (Google/Apple/Microsoft)** — the captures show social login buttons but the backend has no OAuth endpoints defined in scope. The frontend should render the buttons in disabled/coming-soon state until the OAuth story is in plan, or the implementer must confirm they are wired.

2. **`PersonaToggle` behavior** — the "Student" tab on login presumably changes the form behavior (e.g. different API endpoint, different redirect). The design shows a shared login screen; the implementer must clarify what happens when "I'm a Student" is selected (separate email flow vs. parent-linked QR code).

3. **`country` field on Register vs. AddChild** — the capture clearly shows `country` on the register form, but the current `RegisterParentFormValues` schema (backend) may not accept `country` at parent registration. Must verify the `POST /api/Auth/register` contract before adding the field.

4. **`confirmPassword` removal** — the captures do not show a Confirm Password field. If removed from the form the backend must validate password complexity server-side. Confirm with planner whether the field is truly removed or only hidden.

5. **Child Home is a full build** — the current `(child)/index.tsx` is explicitly a placeholder. The full build (HUD, subject grid, mission rows, tab bar) is a separate story scope (likely P1-08 or subsequent). This audit flags it as Blocker so the frontend agent knows the placeholder is not pixel-perfect.

6. **`design-system/assets/icons/`** — the audit assumes SVG icons exist for social brands (Google, Apple, Microsoft), chevron, lock, person, envelope, globe. If they do not exist, the implementer must source or create them.
