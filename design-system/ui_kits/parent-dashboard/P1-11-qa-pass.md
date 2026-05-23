# QA Pass — P1-11-FE-13 Pixel-Perfect Audit

> Reference: `design-system/screenshots/web/` and `mobile/`. All token names map to `design-system/colors_and_type.css`. Severity key: **Blocker** = wrong layout or missing element; **Major** = wrong token; **Minor** = small spacing / typography delta.

---

## 1. Login — `web/02-login.png`

### What the capture shows
- Split-panel: left 50 % = `$primary` (#4F46E5) purple brand panel with logo + wordmark, glowing gold-star illustration, bold "Welcome back to the adventure." heading (~44px/800), body text, "🔥 240,000+ kids learning today" social proof.
- Right 50 % = `$bg` (#0F172A) form panel: "LOG IN" purple eyebrow, "Welcome back" heading (~28px/800), subtitle, a segmented pill toggle (Parent / Student), Email field, Password field + "Show" inline link, "Remember me" checkbox + "Forgot password?" link, a large `$primary` CTA button ("Log in →"), "OR CONTINUE WITH" horizontal divider, three pill social buttons (Google / Apple / Microsoft with branded SVG-style icons), "New to Learnexia? Create parent account" footer.

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| L-01 | Social button icons | Capture shows distinct colored circular SVG brand logos (Google multicolor G, Apple white apple, Microsoft colored grid) | Code renders emoji glyphs: `'G'` (letter), `'🍎'` (emoji), `'⊞'` (unicode) as `accessibilityElementsHidden` decorative text | Major | Need proper SVG icon components or at minimum monochrome SVG logos; emoji glyphs do not match the capture's monochrome-on-`$card` icons | `(auth)/_components/loginParts.tsx` `SocialButton` + `loginParts.tsx` constants |
| L-02 | Login eyebrow color — mobile | On phone (< tablet), eyebrow renders `color="$fg3"` — capture shows it in `$primary` (#4F46E5) across both breakpoints | Code has `$tablet={{ color: '$primary' }}` conditional — on phone it resolves to `$fg3` (#94A3B8) | Minor | Always use `color="$primary"` for the "LOG IN" eyebrow; remove the conditional | `apps/student-app/app/(auth)/login.tsx` line 72-78 |
| L-03 | Heading font size | Capture heading "Welcome back" is approximately `--lx-size-h1` (32px/800) | Code renders `fontSize={28}` | Minor | Change to `fontSize={32}` = `--lx-size-h1` (`$sizeH1`) | `apps/student-app/app/(auth)/login.tsx` line 83-93 |
| L-04 | Brand panel star illustration | Capture shows a circular glow `$primaryHover` circle behind a 3D gold-star emoji (~108px) | Code renders a 140×140 `$pill` circle with `backgroundColor="$primaryHover"` and `fontSize={72} ⭐` — correct approach; the circle is pill not card radius, which matches | Accepted (minor shape match acceptable) | — | — |
| L-05 | Brand panel logo mark | Capture shows a purple rounded-square icon-mark with "✦" spark and wordmark "Learnexia" at ~22px/800 | Code matches this in `LoginBrandPanel`; logo mark uses `$primaryHover` fill, width 44, `borderRadius="$button"` (16px). Capture's icon-mark appears to have `borderRadius` closer to `$card` (20px) | Minor | Change brand mark `borderRadius` from `"$button"` (16px) to `"$card"` (20px) to match capture | `apps/student-app/app/(auth)/_components/LoginBrandPanel.tsx` line 29 |
| L-06 | PersonaToggle height | Capture toggle tabs appear ~52px tall | Code renders `height={44}` per tab | Minor | Change inner tab height from 44 to 52px to match `--lx-space-12`-comparable height and match the capture's pill visual weight | `apps/student-app/app/(auth)/_components/PersonaToggle.tsx` line 68 |
| L-07 | Split panel width ratio | Capture is approximately 50/50 left/right | `SplitFormScaffold` gives both panels `flex: 1` — ratio is correct | Accepted | — | — |
| L-08 | "Forgot password?" color | Code uses `color="$primaryLight"` | Capture renders a distinctly lighter indigo, closer to `--lx-primary-hover` (#6366F1) | Minor | `$primaryLight` is the correct alias if it resolves to `$primaryHover` — verify the token alias resolves to #6366F1. If `$primaryLight` maps to something else, change to `color="$primaryHover"` | `(auth)/_components/LoginForm.tsx` line 158 |

### Accepted deferrals
- Social / Forgot password flows are UI-first stubs.
- Language/theme toggle in top corner is an added affordance (not in capture, explicit story requirement).

---

## 2. Register — `web/03-register.png`

### What the capture shows
- Split-panel, form LEFT / feature panel RIGHT (mirror of Login).
- Left: logo + wordmark, "STEP 1 OF 2" small-caps purple text, ~50 % progress bar fill (purple on `$card` track, 6px tall), "Create your parent account" heading (~32px/800), subtitle in `$fg3`, a bordered "Parent / Guardian only" info banner (purple border + `$primarySoft` bg, family emoji + "Parent / Guardian only" in purple + body text), Full name + Country two-column row, Email field, Password field + "At least 6 characters" helper, Terms checkbox (multi-line, links in purple), disabled "Continue → Add Children" button.
- Right: purple background, a game-controller emoji in a circle, "Set up once. Watch them learn forever." headline (~40px/800), four feature bullets each with a coloured icon chip.

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| R-01 | Step eyebrow color | Capture "STEP 1 OF 2" is clearly in `$primary` indigo purple | Code uses `color="$fg3"` (#94A3B8 muted) | Major | Change to `color="$primary"` (`--lx-primary`) | `apps/student-app/app/(auth)/register.tsx` line 65 |
| R-02 | Progress bar track color | Capture bar track is a very dark surface, closer to `$card` (#1E293B) | Code uses `backgroundColor="$card"` — correct | Accepted | — | — |
| R-03 | Progress bar fill color | Capture fill is `$primary` purple | Code uses `backgroundColor="$primary"` — correct | Accepted | — | — |
| R-04 | Heading font size | Capture heading "Create your parent account" is ~32px/800 | Code `fontSize={32}` — matches `--lx-size-h1` | Accepted | — | — |
| R-05 | ParentOnlyBanner border color | Capture banner border appears to be `$primary` (#4F46E5) | Code has `borderColor="$primary"` — correct | Accepted | — | — |
| R-06 | Feature panel game-controller icon circle | Capture icon circle is ~80px, sits on `$primaryHover` | Code renders 120×120 circle — oversized vs capture (capture ~80px) | Minor | Reduce from 120 to 80px (`$space-20` equivalent) for better proportional match | `apps/student-app/app/(auth)/_components/RegisterFeaturePanel.tsx` line 33 |
| R-07 | Feature panel headline font size | Capture heading is approximately 40px/800 | Code `fontSize={40}` — matches | Accepted | — | — |
| R-08 | Feature bullet icon chips | Capture chips appear to be ~40×40 rounded-square | Code is 40×40 `$button` radius — correct | Accepted | — | — |
| R-09 | Country field flag prefix | Capture shows a flag emoji before "Saudi Arabia" in the country Select | No flag rendering in the `Select` component or `RegisterForm` | Minor | This is a design-gap: the `Select` component has no flag/prefix-glyph slot. Flag prefixes require either a custom `renderOption` prop on Select or a wrapper chip. Flag per country is not yet in the COUNTRIES data structure. Flag as `design gap` (see section 7). | `packages/ui/src/components/Select/index.tsx` |
| R-10 | "Back to Sign In" link | Capture does not show a back button at top (no `ScreenHeader` visible in the capture for register) | Code includes a `ScreenHeader` with back button via `FormScaffold header` prop | Minor deviation | The capture doesn't show an explicit back button in the top-left of the form column at desktop — only the desktop register split shows the logo + form directly. The ScreenHeader may push the logo down. If the ScreenHeader is shown, it should be hidden at tablet+ or placed outside the content area | `apps/student-app/app/(auth)/register.tsx` line 37-39 and `FormScaffold.tsx` `SplitFormScaffold` — header renders above the scroll on all widths |

### Accepted deferrals
- Country is UI-only (backend TODO P1-12).
- Terms/Privacy links are UI stubs.

---

## 3. My Children — `web/04-my-children.png`

### What the capture shows
- Sidebar: 240px wide, `$bgElevated` (#111B33), logo + wordmark, active child card (orange "S" avatar, "Sami", "Grade 3 · Level 12", chevron), nav with "My Children" active (indigo pill background + start accent border), other items: Overview, Reports, Activity, Subjects, Settings. Bottom of sidebar: "THIS WEEK +340 XP Up 28% from last week" gamification card.
- Main area: "My Children" heading + "3 children linked to your account" subtitle, "This week" dropdown + "Send Report" button in the top-right. Family summary strip (purple gradient, eyebrow, "Your family is on a roll" headline, 4 KPI stats, child mascot avatars). "Pick a child to view their progress" row + "+ Add Child" button. Three child cards (Sami/orange, Layla/purple, Yusuf/blue). Dashed add-child card at bottom.

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| MC-01 | Sidebar bottom XP widget | Capture shows a small "THIS WEEK +340 XP / Up 28% from last week" gamification widget at the bottom of the sidebar in a dark card | No such widget exists in `Sidebar.tsx` — the sidebar ends after the nav list | Blocker | Add a bottom XP summary widget to `Sidebar.tsx`. Structure: `Stack` pinned `marginTop="auto"`, card-style surface (`$card` bg, `$border`), eyebrow "THIS WEEK" (`$accent` / `--lx-xp` #FACC15, uppercase 10px), value "+340 XP" (24px/800 `$fg1`), subtitle "Up 28% from last week" (12px `$fg3`). This is a Phase-5 stub (the real XP delta is server data). For now render with static stub copy behind a TODO(P5) comment. | `apps/student-app/app/(parent)/_components/Sidebar.tsx` |
| MC-02 | Sidebar width | Capture sidebar is approximately 240px at 1280px viewport | Code has `width={240}` — correct | Accepted | — | — |
| MC-03 | Family summary mascot art | Capture shows two/three overlapping child avatar "head" illustrations on the right end of the strip | Code renders `assets.mascotOwl` at 96×96 | Major | The capture shows stacked child-face avatar circles (not the owl mascot). The owl is for the splash/mobile brand. Replace with stacked `Avatar` components for each linked child (3 avatars overlapping with negative margin). Until a proper avatar-overlap primitive exists, render a horizontal stack of Avatar `size="md"` with `marginStart={-16}` per subsequent one. | `apps/student-app/app/(parent)/_components/FamilySummaryStrip.tsx` line 119-123 |
| MC-04 | KPIStatCard on FamilySummaryStrip — inline stats gap | Capture shows the 4 inline stats tightly packed in `gap="$6"` (24px) | Code uses `gap="$8"` (32px) on the stats Stack | Minor | Change to `gap="$6"` (`--lx-space-6` = 24px) | `apps/student-app/app/(parent)/_components/FamilySummaryStrip.tsx` line 83 |
| MC-05 | ChildDashboardCard — child grade info row | Capture shows grade pill ("Grade 3") directly to the left of the active status dot + text, with language flag ("GB English", "SA العربية") on a separate line | Code places grade pill + lang label in same `flexDirection={rowDir}` row, which is correct. However the flag prefix per language ("GB", "SA") is not rendered — the code renders only the translated language name string | Minor | Add flag prefix per locale: `GB` for English, `SA` for Arabic. These are static — could be inline constants. The capture uses flag emoji or country-code prefixed text | `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx` line 83-98 |
| MC-06 | ChildDashboardCard — KPIStatCard Level icon | Capture shows a brain/puzzle emoji for Level, not `'🎚'` (slider emoji) | Code uses `icon="🎚"` | Minor | Change Level stat icon from `'🎚'` to `'🧠'` or `'🎮'` to match the capture's icon appearance (capture shows a brain-like icon chip). Review capture: the level chip has a small pink/brain icon | `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx` line 105-112 |
| MC-07 | ChildDashboardCard card min-height | Capture child cards all have a consistent height (~310px) with the "View dashboard →" footer always at the bottom | Code has no explicit `minHeight` on the card — it sizes to content. If one card has less text it will be shorter, breaking the grid alignment | Minor | Add `minHeight={300}` to the `ChildDashboardCard` outer `Stack` so cards in the same row maintain equal height | `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx` line 50 |
| MC-08 | AddChildCard layout | Capture shows the dashed add-card as a horizontal row (icon left + text right) partially visible at the bottom, same height as the grid | Code renders `flexDirection={rowDir}` with icon + text — correct structure | Accepted | — | — |
| MC-09 | "This week" Select label visibility | Capture shows a "This week" dropdown with a caret, no explicit label text visible (the label is visually hidden — it acts as a native select) | Code uses `Select` with a `label` prop that likely renders a visible label above the field | Minor | The Select in the page header (period picker) should have `hideLabel` or render with `variant="inline"` style to match the capture's compact dropdown appearance without a stacked label | `apps/student-app/app/(parent)/_components/MyChildrenWeb.tsx` line 69-78 |

### Accepted deferrals
- Family stats (XP/lessons/streak/badges) are Phase-5 stubs.
- Child stats (level/XP/streak/mastery) are Phase-5 stubs.
- Subjects shown in capture (Reading/Art) are mock — built code shows the 4 correct product subjects.

---

## 4. Dashboard / Overview — `web/05-dashboard.png`

### What the capture shows
- Same sidebar as My Children (Overview nav item active).
- Main: "Sami's progress" heading + "Mon, Nov 18 → Sun, Nov 24" date range, "This week" dropdown + "Send Report". Four KPI stat cards (TIME LEARNING / XP EARNED / LESSONS DONE / DAY STREAK) each with a large number + `$success` green delta row. Daily activity card (bar chart with gradient purple highlighted bar for Sunday, "Export CSV" button). Subject mastery card (Math 72% indigo, Reading 65% purple, Science 58% green, Art 84% orange — bars with subject-colored fills). Areas to focus on card (two rows visible).

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| D-01 | KPI card background | Capture KPI cards have `$card` (#1E293B) background | Code uses `backgroundColor="$card"` — correct | Accepted | — | — |
| D-02 | KPI delta color | Capture delta text is clearly `$success` green (#22C55E) | Code uses `color="$success"` — correct | Accepted | — | — |
| D-03 | Daily activity chart | Capture shows a functional bar chart with gradient fill on the active (Sunday) bar | Code renders a muted dashed placeholder | Accepted (deferred P5-05-FE) | — | — |
| D-04 | Export CSV button style | Capture shows "Export CSV" as a small pill button with border, floating top-right in the Daily activity card | Code uses `Button variant="secondary"` which has `$cardSoft` background + `$border` — matches the bordered style | Accepted | — | — |
| D-05 | Subject mastery bar colors | Capture shows Math (indigo/purple), Reading (purple), Science (green), Art (orange) — each bar is a distinct single color per subject | Code uses `MasteryBar` which always renders the `gradXp` (green→indigo) gradient fill regardless of subject | Major | Subject mastery bars should have per-subject accent colors: Math = `$primary` (#4F46E5), Science = `$success` (#22C55E), Arabic = `$purple` (#A855F7), English = `$accent` (#F59E0B). Add an optional `fillColor` or `accent` prop to `MasteryBar`, or pass a `gradientStops` override. Since the capture uses solid single-color fills (not gradients), a solid fill via a token color is preferred | `packages/ui/src/components/MasteryBar/index.tsx` (new `accent` prop) + `apps/student-app/app/(parent)/_components/SubjectMasteryCard.tsx` (pass per-subject accent) |
| D-06 | Subject mastery label alignment | Capture shows the subject name left-aligned and percentage right-aligned in the same row above the bar | Code's `MasteryBar` does `justifyContent="space-between"` in the caption row — correct | Accepted | — | — |
| D-07 | Focus areas icon chip | Capture shows a small purple square icon chip per focus row with subject-specific icon (minus/subtraction sign for Math, letters icon for English) | Code always renders `'📘'` (book emoji) for every row | Minor | Use subject-keyed icons: Math = `'−'` or `'🔢'`, Science = `'🔬'`, Arabic = `'ع'` text, English = `'abc'` text or letter icon chip. Define a `SUBJECT_ICON` map keyed to `OverviewSubjectKey` | `apps/student-app/app/(parent)/_components/FocusAreasCard.tsx` lines 133-142 |
| D-08 | Focus areas confidence bar width | Capture shows the confidence bar at approximately 120px wide | Code uses explicit `width={120}` — correct | Accepted | — | — |
| D-09 | KPI tile inline a11y overlay | Code renders an absolutely-positioned accessible overlay on each KPI tile. This is correct per spec | Accepted | — | — |
| D-10 | Header date range text | Capture shows "Mon, Nov 18 → Sun, Nov 24" as `$fg3` muted text | Code renders a `dateRange` translation key at `color="$fg3" fontSize={14}` — correct structure | Accepted (Phase-5 data) | — | — |

### Accepted deferrals
- Bar chart deferred to P5-05-FE.
- All KPI numbers / mastery percentages are Phase-5 stubs.
- "Reading / Art" subjects in capture are mock — built correctly uses Math/Science/Arabic/English.

---

## 5. Settings — `web/07-settings.png`

### What the capture shows
- Same sidebar structure (Settings item active).
- Main: "Settings" heading + subtitle, "This week" + "Send Report" controls. A two-column layout: a ~210px left tab rail with six tabs (Profile active with indigo background + start border, Notifications, Linked children, Security, Plan & billing, Language & region), and a large content panel. Profile panel: "Profile / This is how Learnexia knows you", orange "A" avatar circle (64px), "Upload photo" (`$primary` button) + "Remove" (ghost), Full name + Email (two columns), Phone + Country (two columns), "Cancel" + "Save changes" buttons right-aligned.

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| S-01 | Settings sidebar bottom XP widget | Capture Settings sidebar also shows the "THIS WEEK +340 XP" widget at the bottom | Same as MC-01 — no widget rendered | Blocker | Same fix as MC-01: add XP summary widget to Sidebar (shared component, so fixing MC-01 fixes S-01 automatically) | `apps/student-app/app/(parent)/_components/Sidebar.tsx` |
| S-02 | Tab rail width | Capture tab rail is approximately 210px | Code `width={210}` — correct | Accepted | — | — |
| S-03 | Tabs active item background | Capture active "Profile" tab has a solid indigo background fill (appears more opaque than `$primarySoft`) | Code uses `backgroundColor="$primarySoft"` (rgba(79,70,229,0.18)) which is a very subtle tint — the capture shows a noticeably solid fill closer to `$primary` at ~30% opacity | Minor | Use `backgroundColor="$primarySoft"` with `opacity={1}` — if the visual result is too faint, override to `rgba(79,70,229,0.28)`. This is a gap between `$primarySoft` (0.18 alpha) and what the capture shows. Consider adding a `$primarySoftStrong` token at 0.28 alpha | `packages/ui/src/components/Tabs/index.tsx` line 73 |
| S-04 | Tab icon glyphs | Capture shows: Profile = a person silhouette icon, Notifications = bell, Linked children = family emoji, Security = shield, Plan & billing = diamond/gem, Language = globe | Code icons: Profile `'👤'`, Notifications `'🔔'`, LinkedChildren `'👨‍👧'`, Security `'🛡️'`, Billing `'💳'`, Language `'🌐'` | Minor | Billing icon should be `'💎'` (gem/diamond) to match the capture's diamond icon; current `'💳'` (credit card) does not match the capture's visible gem icon | `apps/student-app/app/(parent)/_components/SettingsWeb.tsx` line 52 |
| S-05 | Profile panel — avatar size | Capture shows the avatar at approximately 72–80px | Code uses `Avatar size="lg"` = 64px | Minor | Either increase Avatar `lg` size to 72px or add an `xl` size variant (80px). Update `SIZE_PX` in Avatar | `packages/ui/src/components/Avatar/index.tsx` line 49-53 |
| S-06 | "Upload photo" button state | Capture shows "Upload photo" as an active `$primary` button (not dimmed/disabled) | Code sets `disabled` on the Upload photo button | Minor (accepted as P1-12 stub) | The button correctly shows as disabled pending P1-12. Listed for completeness — do not change until P1-12 ships | Accepted |
| S-07 | "Save changes" button label | Capture shows "Save changes" | Code uses translation key `parent.settings.profile.save` — needs to resolve to "Save changes" | Accepted (i18n) | — | — |
| S-08 | Settings header "This week" + "Send Report" | Capture shows these controls — code replicates them via the same `Select` + `Button` pattern used in My Children and Overview | Correct, matches | Accepted | — | — |

### Accepted deferrals
- 4 secondary tabs are "coming soon" (Notifications/LinkedChildren/Security/Billing → P2-12).
- Profile Save/avatar upload is P1-12 stub.

---

## 6. Splash — `mobile/01-splash.png`

### What the capture shows
- Full-screen purple radial gradient (deep purple #3730A3-ish center fading to near-black edges), scattered white dot "stars", "Learnexia" wordmark centered (~40px/800 white), "AI Learning Adventure Begins" subtitle (~14px muted), three-dot pulse indicator (dots in indigo/primary), a dark rounded progress bar (~70% fill, gradient green→indigo), "Loading... ⚡" label (gray), "POWERED BY AI" small-caps eyebrow (bottom), "✦ Gamified Learning ✦" tagline (bottom).

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| SP-01 | DotPulse dot color | Capture shows dots in `$primary` indigo | Code renders `backgroundColor="$primary"` — correct | Accepted | — | — |
| SP-02 | DotPulse dot size | Capture dots appear ~10px | Code uses `width={10} height={10}` — correct | Accepted | — | — |
| SP-03 | Progress bar track color | Capture track is very dark (near `$bg` #0F172A) | Code uses `backgroundColor="$bg"` — correct | Accepted | — | — |
| SP-04 | Progress bar fill | Capture fill appears to be the `gradXp` gradient (green → indigo) | Code uses `gradientStops.gradXp` — correct | Accepted | — | — |
| SP-05 | Star dot positions | Capture shows approximately 11 stars scattered in specific positions | Code defines `STARS` array with 11 entries at percentage-based positions — matches the capture well | Accepted | — | — |
| SP-06 | Footer tagline decorators | Capture shows "✦ Gamified Learning ✦" with star glyphs either side | Code renders `t('common.splash.tagline')` as a single `Text` — the decorative "✦" flanking must be included in the translation string or added as literal flanking `Text` elements | Minor | Ensure `common.splash.tagline` resolves to "✦ Gamified Learning ✦" or split into `<Text>✦</Text><Text>{tagline}</Text><Text>✦</Text>`. Current code has no split | `apps/student-app/app/index.tsx` line 141-144 |
| SP-07 | "Loading..." with emoji | Capture shows "Loading... ⚡" with a lightning bolt after the ellipsis | Code renders `t('common.splash.loading')` — must ensure translation string includes "⚡" | Minor | Verify `common.splash.loading` translation in i18n files resolves to "Loading... ⚡" | i18n translation files (en/ar) — check `apps/student-app/src/i18n/en.json` or equivalent |

### Accepted
- Splash is LTR-always (brand chrome, not content RTL); no deviation.

---

## 7. Landing — `web/01-landing.png`

### What the capture shows
- Dark `$bg` (#0F172A) full-width layout. Fixed top nav: Learnexia SVG logo + wordmark, four nav links (How it works / Subjects / For schools / Pricing), "Log in" ghost button, "Start free" `$primary` button with glow. Hero: two-column (copy left, phone mockup right). Left: "POWERED BY AI" pill badge (indigo outline + `$primary-soft` fill), "An adventure game your kids will love — that teaches." headline (~68px/800, "adventure game" in `$accent` #F59E0B orange), paragraph, two CTA buttons ("Create parent account →" primary, "▶ Watch demo (2 min)" secondary), three trust badges (⭐ 4.9 in App Store, 🛡️ COPPA-compliant, 👨‍👩‍👧 Free for first child). Right: decorative phone mockup with purple gradient screen, "+50 XP ⭐" green chip (top-right), "🏆 New badge!" dark chip (bottom-left).

### Deviations found

| # | Element | Capture | Built code | Severity | Correct token/value | Target file |
|---|---------|---------|------------|----------|---------------------|-------------|
| LND-01 | Hero grid ratio | Capture is approximately 55/45 copy/art split | Code uses `grid-template-columns: 1.05fr 0.95fr` ≈ 52.5/47.5 — close enough | Accepted | — | — |
| LND-02 | Nav "Log in" button style | Capture shows "Log in" as a simple text/ghost link, no border | Code renders `.btnOutline` with `border: 1px solid var(--lx-border-strong)` — the capture does not show a visible border on "Log in" | Minor | Change `.btnOutline` to remove the border (use `border: none; background: transparent`) for the nav Log In link, or confirm the border is intentional at this opacity level | `apps/marketing-site/app/page.module.css` `.btnOutline` |
| LND-03 | Phone device frame background | Capture phone frame is very dark (#0a0614 near-black) | Code uses `background: #050a18` — close match | Accepted | — | — |
| LND-04 | Phone screen gradient | Capture screen shows `linear-gradient(160deg, #6d28d9, #5b21b6, #4c1d95)` | Code matches this exactly | Accepted | — | — |
| LND-05 | "+50 XP ⭐" chip position | Capture chip floats top-right, partially off the phone frame | Code uses `top: 56px; inset-inline-end: -40px` — correct relative position | Accepted | — | — |
| LND-06 | "New badge!" chip position | Capture chip is bottom-left, partially off frame | Code uses `bottom: 96px; inset-inline-start: -56px` — correct | Accepted | — | — |
| LND-07 | Headline "adventure game" accent | Capture shows "adventure game" in `$accent` (#F59E0B) orange | Code applies `color: var(--lx-accent)` via `.headlineAccent` class — correct | Accepted | — | — |
| LND-08 | Hero glow radial | Capture shows a soft purple radial glow top-left behind the copy | Code renders `.heroGlow` with `background: radial-gradient(circle, var(--lx-primary-soft) 0%, rgba(168,85,247,0.06) 45%, transparent 70%)` — correct | Accepted | — | — |
| LND-09 | Below-the-fold sections | Capture only shows the hero fold. The built below-the-fold stubs (How it works / Subjects / For schools / Pricing) are layout-faithful placeholders | Accepted | — | — |
| LND-10 | Responsive collapse (< 900px) | Code collapses to single-column + hides nav links at `max-width: 900px`, which matches the capture's ~1280px desktop-only scope | Accepted | — | — |

### Accepted deferrals
- Landing is English-only (RTL scoped out for this phase).
- Decorative phone art (not the real app).
- Below-the-fold section stubs are intentional placeholders.

---

## 8. New Components Needed (Design Gaps)

| Gap | Description | Where needed |
|-----|-------------|-------------|
| GAP-01 | **Social login SVG icons** | The `SocialButton` in `loginParts.tsx` needs proper Google/Apple/Microsoft SVG icon assets (or a wrapping `<img>` / `next/image`). Emoji glyphs are not pixel-accurate to the capture. Needs assets in `design-system/assets/icons/` and a prop to accept an SVG node rather than a glyph string. |
| GAP-02 | **`$primarySoftStrong` token** | The active Tabs/Sidebar pill background at 0.28 opacity is visually present in captures but the only token available is `$primarySoft` at 0.18 alpha. Add `--lx-primary-soft-strong: rgba(79,70,229,0.28)` to `colors_and_type.css`. |
| GAP-03 | **MasteryBar `accent` prop** | `MasteryBar` always uses the `gradXp` gradient. Dashboard subject mastery requires per-subject solid colors. Add an optional `accent` prop (token string like `'$primary'`) that overrides the gradient fill with a solid color. |
| GAP-04 | **Avatar `xl` size** | Settings profile avatar needs ~72–80px. Current `lg` = 64px. Add `xl: 72` to `SIZE_PX` and `FONT_PX` in `Avatar`. |
| GAP-05 | **Select `hideLabel` or inline variant** | The period-picker Select in page headers renders a visible stacked label above the dropdown, which does not match the capture's compact inline dropdown. A `hideLabel` boolean prop or an `inline` variant is needed on `Select`. |
| GAP-06 | **Country flag prefixes in Select** | Capture register + settings country fields show flag emoji before the country name ("SA Saudi Arabia"). The COUNTRIES data structure and Select component have no flag slot. Add a `flag` field to each country entry and a `prefixIcon` rendering path in Select options. |
| GAP-07 | **Sidebar bottom XP widget** | No component or token-backed stub exists for the sidebar's weekly-XP summary card. Needs a small `SidebarXpWidget` sub-component built with existing tokens: `$xp` color (#FACC15) for the eyebrow, `$fg1` for the value, `$fg3` for the delta, `$card` background, `$border`, `$radius-card`. |
| GAP-08 | **Avatar overlap strip for FamilySummaryStrip** | Capture shows stacked child avatar circles (not the owl mascot). An overlapping-avatar strip primitive does not exist. Either render multiple `Avatar` with `marginStart={-16}` negative margin or create a `AvatarStack` component. |

---

## 9. Severity-Sorted Gap List (all screens combined)

### Blockers (2)
1. **MC-01 / S-01** — Sidebar bottom XP widget missing on My Children and Settings. Target: `Sidebar.tsx`.

### Majors (4)
2. **L-01** — Social login button icons are emoji glyphs, not brand SVGs. Target: `loginParts.tsx` + icon assets.
3. **MC-03** — FamilySummaryStrip uses owl mascot instead of child-avatar stack. Target: `FamilySummaryStrip.tsx`.
4. **D-05** — SubjectMasteryCard bars all use `gradXp` gradient; should be per-subject solid accent colors. Target: `MasteryBar.tsx` + `SubjectMasteryCard.tsx`.
5. **R-01** — Register step eyebrow "STEP 1 OF 2" rendered in `$fg3` not `$primary`. Target: `register.tsx`.

### Minors (14)
6. **L-02** — Login eyebrow uses `$fg3` on mobile, should always be `$primary`.
7. **L-03** — Login heading is 28px, should be 32px (`--lx-size-h1`).
8. **L-05** — Login brand mark uses `$button` radius (16px), should be `$card` (20px).
9. **L-06** — PersonaToggle inner tab height 44px, should be 52px.
10. **L-08** — "Forgot password?" color — verify `$primaryLight` resolves to #6366F1.
11. **R-06** — Feature panel icon circle oversized (120px vs ~80px in capture).
12. **R-09** — Country Select missing flag prefix rendering (design gap GAP-06).
13. **R-10** — ScreenHeader back button appears at tablet+ on register — may push logo down vs capture.
14. **MC-04** — FamilySummaryStrip inline stats gap `$8` (32px) should be `$6` (24px).
15. **MC-05** — Child grade/language row missing flag prefix ("GB" / "SA").
16. **MC-06** — Level KPI icon `'🎚'` should be brain/game icon per capture.
17. **MC-07** — ChildDashboardCard missing `minHeight` for grid-row alignment.
18. **MC-09** — Period Select in page header shows visible stacked label (design gap GAP-05).
19. **D-07** — Focus areas rows all use `'📘'` icon; should be subject-keyed icons.
20. **S-03** — Tabs active background `$primarySoft` (0.18 alpha) visually lighter than capture's fill.
21. **S-04** — Billing tab icon `'💳'` should be `'💎'` to match capture's gem icon.
22. **S-05** — Profile avatar `size="lg"` = 64px; capture is ~72–80px.
23. **SP-06** — Tagline flanking ✦ glyphs need to be in translation string or literal adjacent `Text` nodes.
24. **SP-07** — "Loading... ⚡" needs ⚡ emoji in translation value.
25. **LND-02** — Nav "Log in" `.btnOutline` border may be more visible than capture's ghost link style.

---

## 10. Implementation Handoff

| Fix ID | Target file | Change |
|--------|-------------|--------|
| MC-01 / S-01 | `apps/student-app/app/(parent)/_components/Sidebar.tsx` | Add `SidebarXpWidget` stub at bottom (`marginTop="auto"`), tokens: `$card` bg, `$xp` eyebrow, `$fg1` value, `$fg3` delta |
| L-01 | `apps/student-app/app/(auth)/_components/loginParts.tsx` + `design-system/assets/icons/` | Replace glyph strings with `<img src>` or `<Image>` SVG assets for Google/Apple/Microsoft |
| MC-03 | `apps/student-app/app/(parent)/_components/FamilySummaryStrip.tsx` | Replace `mascotOwl` Image with overlapping `Avatar` components for each linked child |
| D-05 | `packages/ui/src/components/MasteryBar/index.tsx` | Add optional `accent` prop (token color string); when set, fill with solid color instead of `gradXp` gradient |
| D-05 (cont.) | `apps/student-app/app/(parent)/_components/SubjectMasteryCard.tsx` | Pass per-subject accent: Math=`'$primary'`, Science=`'$success'`, Arabic=`'$purple'`, English=`'$accent'` |
| R-01 | `apps/student-app/app/(auth)/register.tsx` | Change step eyebrow `color` from `"$fg3"` to `"$primary"` |
| L-02 | `apps/student-app/app/(auth)/login.tsx` | Remove `$tablet={{ color: '$primary' }}` conditional; always render eyebrow as `color="$primary"` |
| L-03 | `apps/student-app/app/(auth)/login.tsx` | Change heading `fontSize` from `{28}` to `{32}` |
| L-05 | `apps/student-app/app/(auth)/_components/LoginBrandPanel.tsx` | Change logo mark `borderRadius` from `"$button"` to `"$card"` |
| L-06 | `apps/student-app/app/(auth)/_components/PersonaToggle.tsx` | Change inner tab `height` from `{44}` to `{52}` |
| R-06 | `apps/student-app/app/(auth)/_components/RegisterFeaturePanel.tsx` | Reduce icon circle `width`/`height` from `{120}` to `{80}` |
| MC-04 | `apps/student-app/app/(parent)/_components/FamilySummaryStrip.tsx` | Change stats Stack `gap` from `"$8"` to `"$6"` |
| MC-06 | `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx` | Change Level KPI `icon` from `"🎚"` to `"🧠"` |
| MC-07 | `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx` | Add `minHeight={300}` to outer card Stack |
| D-07 | `apps/student-app/app/(parent)/_components/FocusAreasCard.tsx` | Replace static `'📘'` with a `TOPIC_ICON` map keyed to `OverviewSubjectKey` |
| S-03 | `packages/ui/src/components/Tabs/index.tsx` | Increase active item bg opacity: `rgba(79,70,229,0.28)` or add `--lx-primary-soft-strong` token |
| S-04 | `apps/student-app/app/(parent)/_components/SettingsWeb.tsx` | Change billing tab icon from `'💳'` to `'💎'` |
| S-05 | `packages/ui/src/components/Avatar/index.tsx` | Add `xl: 72` to `SIZE_PX` + `FONT_PX`; update `AvatarSize` type; use `size="xl"` in Settings ProfilePanel |
| SP-06/07 | i18n translation files (`en.json`/`ar.json`) | Ensure `common.splash.tagline` = "✦ Gamified Learning ✦"; `common.splash.loading` = "Loading... ⚡" |
| LND-02 | `apps/marketing-site/app/page.module.css` | Review `.btnOutline` border opacity against capture; consider `border: 1px solid var(--lx-border)` (softer) |
| GAP-02 | `design-system/colors_and_type.css` | Add `--lx-primary-soft-strong: rgba(79,70,229,0.28)` and a `$primarySoftStrong` token alias |
| GAP-05 | `packages/ui/src/components/Select/index.tsx` | Add `hideLabel?: boolean` prop to suppress the stacked label rendering for inline usage |
| GAP-06 | `packages/shared/src/constants/countries.ts` + `Select` | Add `flag` field to each country entry; render prefix in Select option |

---

## 11. Screen Proximity to Pixel-Perfect

| Screen | Status | Top remaining gap |
|--------|--------|-------------------|
| Splash | Very close | SP-06/07: translation string decorators (trivial) |
| Landing | Very close | LND-02: Log In button border subtlety |
| Login | Close | L-01 social icons (Major), L-03 heading size (Minor) |
| Register | Close | R-01 eyebrow color (Major), R-09/10 minor layout |
| Settings | Moderate | MC-01/S-01 sidebar XP widget (Blocker), S-03/S-05 tab + avatar |
| My Children | Moderate | MC-01 sidebar XP widget (Blocker), MC-03 mascot vs avatar stack (Major) |
| Dashboard | Moderate | D-05 subject mastery bar colors (Major), MC-01 sidebar |

**Summary counts:** 2 Blockers, 4 Majors, 19 Minors across 7 screens.

The Splash and Landing pages are nearest pixel-perfect. The three parent dashboard screens (My Children, Overview, Settings) share the same Blocker (missing sidebar XP widget) and have the most remaining work.
