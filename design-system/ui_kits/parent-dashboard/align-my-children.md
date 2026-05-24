# Pixel-Alignment Delta Spec — My Children (web/04-my-children.png + web-ar/04-my-children.png)

> **Rule**: captures are the pixel-perfect target. Every row below cites the proving card or screenshot region. "Current" = what the implementation produces today. "Target" = what the capture shows, expressed in design-system token language. Severity: Blocker (layout breaks or element absent) / Major (visually wrong, obvious at a glance) / Minor (subtle, sub-pixel).

---

## Capture inventory

| Capture | URL bar shown |
|---|---|
| `design-system/screenshots/web/04-my-children.png` | `app.learnexia.com/children` — EN LTR |
| `design-system/screenshots/web-ar/04-my-children.png` | `app.learnexia.com/ar/children` — AR RTL |

---

## SECTION A — Sidebar (SHARED — changes here affect Overview, Reports, Settings)

> Proving cards: `web-sidebar.html`, `ar-web-sidebar.html`, `Components.jsx::PDSidebar`, AR `index-ar.html` aside.

| # | Element | Current (Sidebar.tsx) | Target (capture + card) | Token / value | Severity |
|---|---|---|---|---|---|
| S-1 | **Sidebar width** | `width={240}` — correct | 240 px — matches | `--lx-space-16 * 3.75` (hardcoded 240) | OK |
| S-2 | **Sidebar background** | `backgroundColor="$bgElevated"` (`#111B33`) | Capture: sidebar is solid `#0F172A` (same as main canvas bg), no elevation lift | `--lx-bg` `#0F172A` | **Major** — `$bgElevated` renders slightly lighter than target; must use `$bg` / `#0F172A` |
| S-3 | **Sidebar border** | `borderEndWidth={1} borderEndColor="$border"` (logical end = right in LTR) | EN: `border-right: 1px solid rgba(255,255,255,0.06)`. AR: `border-left: 1px solid rgba(255,255,255,0.06)` (logical start). Tamagui's `borderEndWidth` is already correct for LTR/RTL flip | `rgba(255,255,255,0.06)` (`$border` is `rgba(255,255,255,0.08)`) | **Minor** — token alpha is 0.08 vs capture's 0.06; use `rgba(255,255,255,0.06)` inline or add `$borderSubtle` token |
| S-4 | **Sidebar padding** | `paddingHorizontal="$4"` (16), `paddingVertical="$6"` (24) | Card: `padding: 24px 16px` = `paddingVertical 24 paddingHorizontal 16` — matches | OK | |
| S-5 | **Gap between sidebar sections** | `gap="$6"` (24) | Card: `gap: 24px` | OK | |
| S-6 | **Brand row — logo size** | `width:32 height:32` | Card (web-sidebar): `width:32 height:32`. Capture shows ~32 px glyph | OK | |
| S-7 | **Brand row — wordmark font-size** | `fontSize={20} fontWeight="800"` | Card (Components.jsx PDSidebar): `fontSize:18 fontWeight:800`. Capture reads closer to 18 px at 1280 px width | `--lx-size-h3` 18 px, weight 800 | **Minor** — current is 20 px; set to 18 px |
| S-8 | **Brand row — wordmark font-family** | `fontFamily="$heading"` (Poppins in EN) | EN: Poppins ✓. AR: must be `dir="ltr"` + `fontFamily="Cairo"` with `dir="ltr"` wrapper — "Learnexia" brand name stays Latin | `--lx-font-display` / AR: `'Cairo'` + `dir="ltr"` on the Text node | **Major** in AR — the brand name must have `dir="ltr"` attribute set on its Text to prevent RTL character reordering of the Latin string |
| S-9 | **Child-selector card — border-radius** | `borderRadius="$card"` (20 px) | Card (web-sidebar): `border-radius:14px`. Capture: ~14 px, noticeably less than 20 | 14 px (no matching token — see Design Gap DG-1) | **Major** |
| S-10 | **Child-selector card — padding** | `padding="$3"` (12) | Card: `padding:10px`. Capture: ~10 px tight fit | 10 px | **Minor** — current is 12 px, should be 10 px |
| S-11 | **Child-selector avatar — size** | `Avatar name size="sm"` (likely 32 px) | Card (PDSidebar): `width:36 height:36`. Capture: ~36 px circle | 36 px | **Minor** — ensure Avatar size="sm" resolves to 36 px, else override |
| S-12 | **Child-selector avatar — gradient** | `Avatar` uses generic Tamagui `Avatar` — gradient not guaranteed | Card: `background: linear-gradient(135deg, #FB923C, #EF4444)` on the initial circle. Capture: orange→red gradient for "S" / "س" | `linear-gradient(135deg, var(--lx-streak), var(--lx-danger))` | **Major** — the Avatar component must render a gradient background for the initial letter, not a flat color |
| S-13 | **Child-selector name — font-size / weight** | `fontSize={15} fontWeight="700"` | Card (PDSidebar): `fontSize:13 fontWeight:700`. Capture: smaller label | 13 px / 700 | **Minor** — current 15 px should be 13 px |
| S-14 | **Child-selector meta line — font-size** | `fontSize={12}` | Card: `fontSize:11`. Capture matches 11 px | 11 px | **Minor** |
| S-15 | **Child-selector meta copy (EN)** | `t('parent.childSelector.meta', {grade, level})` — key not shown | Target copy: `"Grade 3 · Level 12"`. The `·` separator must be a centered dot U+00B7 | i18n key `parent.childSelector.meta` must produce "Grade {{grade}} · Level {{level}}" | **Blocker if key missing** |
| S-16 | **Child-selector meta copy (AR)** | Same key | Target copy (AR capture): `"الصف ٣ · المستوى ١٢"` — Eastern-Arabic numerals, `·` separator | i18n key AR value: `"الصف {{grade}} · المستوى {{level}}"` with Eastern-Arabic `٣` and `١٢` via `Intl.NumberFormat('ar-EG')` | **Blocker if missing** |
| S-17 | **Child-selector chevron — direction** | `scaleX={isRtl ? -1 : 1}` applied to "›" | EN: `›` points right ✓. AR: should be `‹` (or `›` mirrored). Card uses literal `‹` in AR | Use `‹` in AR rather than mirroring `›`; or apply `transform: scaleX(-1)` only in RTL | **Minor** |
| S-18 | **Child-selector hover / press** | `pressStyle={{ scale: 0.98 }}` | Card has no explicit hover — capture implies lift (floating shadow). Per SKILL rule 9, press = scale 0.95 (not 0.98) | `pressStyle={{ scale: 0.95 }}` — 80 ms | **Minor** |
| S-19 | **Nav item — active background** | `backgroundColor={isActive ? '$primarySoft' : 'transparent'}` (`rgba(79,70,229,0.18)`) | Card: `rgba(79,70,229,0.18)` ✓ | `--lx-primary-soft` | OK |
| S-20 | **Nav item — border-radius** | `borderRadius="$button"` (16 px) | Card (PDSidebar): `borderRadius:12`. Capture matches ~12 px (rounded but not full button radius). Both preview/web-sidebar.html and Components.jsx use 10–12 px | 12 px (no token — see DG-2) | **Major** — current 16 px over-rounds; set to 12 px |
| S-21 | **Nav item — left active accent border** | `borderStartWidth={isActive ? 3 : 0} borderStartColor={isActive ? '$primary' : 'transparent'}` | Capture: no visible left accent stripe on active item — only the `rgba(79,70,229,0.18)` fill and `#A5B4FC` text. Card also omits a left border stroke | Remove `borderStartWidth` and `borderStartColor` from the active state | **Major** |
| S-22 | **Nav item — active font-weight** | `fontWeight={isActive ? '700' : '500'}` | Card: active=`fontWeight:700` inactive=`fontWeight:500/600`. Matches for active; card uses 600 for inactive | Inactive weight should be 600 per card; current 500 is one step lighter | **Minor** |
| S-23 | **Nav item — active text color** | `color={isActive ? '$fg1' : '$fg2'}` | Card: active=`#A5B4FC` (primary-light), inactive=`#94A3B8`. `$fg1` is `#F8FAFC`, not `#A5B4FC` | Active item color must be `#A5B4FC` — map to token `$primaryLight` or `--lx-primary-hover` | **Major** |
| S-24 | **Nav item — font-size** | `fontSize={15}` | Card: `fontSize:14`. Capture: ~14 px | `--lx-size-body-sm` 14 px | **Minor** |
| S-25 | **Nav item — padding** | `paddingHorizontal="$3"` (12) | Card: `padding: 10px 12px`. Current has no explicit vertical padding; minHeight=48 fills it. Card gap=2 px between items | Set `paddingVertical="$2"` (8) + `paddingHorizontal="$3"` (12) and remove `minHeight={48}` (use natural height ≥ 40 px for touch target). Keep `gap="$1"` (4) or set gap 2 | **Minor** |
| S-26 | **Nav icon — font-size** | `fontSize={16}` | Card: `fontSize:16`. Capture matches | OK | |
| S-27 | **Nav items — AR copy** | Translation keys assumed correct | Capture (AR): أطفالي / نظرة عامة / التقارير / النشاط / المواد / الإعدادات | i18n keys must resolve to exact AR strings above | **Blocker if wrong** |
| S-28 | **Nav items — icon set (EN vs AR)** | `icon: '👧'` for My Children (Sidebar.tsx line 51) | Capture EN: `👨‍👩‍👦` emoji family. Card (PDSidebar): `👨‍👩‍👦`. The AR card uses the same emoji | Change MyChildren icon from `'👧'` to `'👨‍👩‍👦'` | **Major** |
| S-29 | **Weekly XP widget — border-radius** | `borderRadius="$card"` (20 px) | Card (PDSidebar + web-sidebar.html): `borderRadius:16` | 16 px (no matching token — DG-1 area) | **Minor** |
| S-30 | **Weekly XP widget — padding** | `padding="$4"` (16) | Card: `padding:14px` | 14 px (not a token step — use `paddingVertical={14} paddingHorizontal={14}` or closest: 12/$3) | **Minor** |
| S-31 | **Weekly XP widget — XP font-size** | `fontSize={24}` | Card (PDSidebar `+340 XP`): `fontSize:20`. web-sidebar.html: `fontSize:18`. Capture: ~20 px | `--lx-size-h2` 24 px is what we have — but card shows 20 px. Use 20 px to match card | **Minor** |
| S-32 | **Weekly XP widget — eyebrow font-size** | `fontSize={10}` | Card: `fontSize:11 letterSpacing:0.08em uppercase`. Current 10 px matches closely | **Minor** — add `letterSpacing={1}` (0.08 em equivalent) | **Minor** |
| S-33 | **Weekly XP widget — delta font-size** | `fontSize={12}` | Card: `fontSize:10–11` | Set to 11 px | **Minor** |
| S-34 | **Sidebar — AR layout (SHARED RTL rule)** | `borderEndWidth` (logical) — in RTL, border moves to left side ✓ | AR capture: sidebar is on the RIGHT, border on its LEFT edge (logical start). `borderEndWidth` in RTL becomes `border-left` which is wrong — the sidebar IS the right element and the content area is to its left | In AR (`dir="rtl"`), the sidebar must be on the RIGHT. The layout parent `flexDirection="row"` in RTL will naturally place flex items right-to-left, so `Sidebar` first in DOM becomes rightmost. Verify the render order in `children.tsx` — `<Sidebar>` is first child with `flexDirection="row"`, so in LTR it's left, in RTL it becomes right. This is correct IF the document has `dir="rtl"`. The border must be `borderStartWidth` (logical start = right in RTL), not `borderEndWidth`. **Change `borderEndWidth` → `borderStartWidth` and `borderEndColor` → `borderStartColor`.** | **Blocker** — RTL sidebar border is on wrong side |
| S-35 | **Sidebar — no weekly XP widget shown in AR capture** | `SidebarXpWidget` always renders | AR screenshot does NOT show the XP widget at the bottom of the sidebar (the sidebar is shorter / content cuts it off). This may be scroll-position artifact, not an absence | Not a bug — widget is present but below fold in the AR capture. Confirm minHeight allows it to appear | Design gap note only |

---

## SECTION B — Page Header (SHARED — affects all app pages)

> Proving card: `web-page-header.html`, `Components.jsx::PDHeader`.

| # | Element | Current (MyChildrenWeb.tsx) | Target (capture + card) | Token / value | Severity |
|---|---|---|---|---|---|
| H-1 | **Header padding** | `padding="$6"` (24) applied to the whole `MyChildrenWeb` Stack, not the header row | Card `PDHeader`: `padding: 20px 32px`. The header row itself has `padding: '20px 32px'` in the reference | Header area: `paddingVertical={20} paddingHorizontal={32}` — distinct from the content padding | **Major** — current layout merges header padding with content padding; the header row needs its own 20/32 px padding and a bottom border |
| H-2 | **Header bottom border** | Not implemented in MyChildrenWeb — no border-bottom on the title row | Card: `borderBottom: 1px solid rgba(255,255,255,0.06)`. Capture shows a faint hairline below "My Children / subtitle" row | Add `borderBottomWidth={1} borderBottomColor="$border"` to the header row Stack | **Major** |
| H-3 | **Page title font-size (EN)** | `fontSize={26}` | Card (PDHeader): `fontSize:22`. Capture: ~22 px (H3-ish) | `--lx-size-h3` = 18 px (too small) — use 22 px to match card; the token doesn't cover 22 exactly | **Major** — set 22 px |
| H-4 | **Page title font-weight** | `fontWeight="800"` | Card: `fontWeight:800` ✓ | OK | |
| H-5 | **Page title copy (EN)** | `t('parent.myChildren.title')` | Capture: `"My Children"` | i18n EN key must produce `"My Children"` | OK (assumed) |
| H-6 | **Page title copy (AR)** | Same key | AR capture: `"أطفالي"` | i18n AR key: `"أطفالي"` | **Blocker if wrong** |
| H-7 | **Subtitle font-size** | `fontSize={14}` | Card: `fontSize:13`. Capture: ~13 px | 13 px | **Minor** |
| H-8 | **Subtitle copy (EN)** | `t('parent.myChildren.subtitle', { count })` | Capture: `"3 children linked to your account"` | i18n key EN: `"{{count}} children linked to your account"` | **Blocker if wrong** |
| H-9 | **Subtitle copy (AR)** | Same key | AR capture: `"٣ أطفال مربوطين بحسابك"` — Eastern-Arabic numeral, proper Arabic | i18n key AR: `"{{count}} أطفال مربوطين بحسابك"` with count via `Intl.NumberFormat('ar-EG')` | **Blocker** |
| H-10 | **Period select — border-radius** | `Select` component (unknown radius) | Card (PDHeader select): `borderRadius:10px`. Capture: ~10 px | 10 px — must override if `Select` uses a different default | **Minor** |
| H-11 | **Period select — background** | Unknown | Card: `background:#1E293B` (`$card`), `border:1px solid rgba(255,255,255,0.1)` | `$card` / `rgba(255,255,255,0.10)` | **Minor** |
| H-12 | **Send Report button — border-radius** | `Button variant="primary" size="sm"` | SKILL rule 4: buttons are 16 px radius. Card: `borderRadius:10`. Capture: ~16 px (indigo button, noticeably rounded) | Per SKILL rule 4, buttons = `--lx-radius-button` 16 px. The card has 10 px which deviates. The capture matches 16 px more closely. Use 16 px (SKILL rule wins over card). | **Major** — if `Button` component uses 16 px already, OK; if the card's 10 px bleeds through, fix to 16 px |
| H-13 | **Send Report button — padding** | `size="sm"` | Card: `padding: 9px 16px`. Capture: consistent with ~9/16 px | Ensure `size="sm"` maps to `paddingVertical={9} paddingHorizontal={16}` | **Minor** |
| H-14 | **Send Report button — copy (EN)** | `t('parent.myChildren.sendReport')` | Capture: `"Send Report"` | i18n key EN: `"Send Report"` | **Blocker if wrong** |
| H-15 | **Header row — RTL flip (AR)** | `flexDirection={rowDir}` | AR capture: `"+ أضف طفلاً"` button on LEFT, title on RIGHT. `rowDir = 'row-reverse'` in RTL achieves this — title block at end (right) and button row at start (left) | ✓ rowDir logic is correct | OK |
| H-16 | **AR header — "+ Add Child" placement** | In EN the "+ Add Child" button is in the pick-a-child row (not the header). In AR capture it appears TOP LEFT in the header area | AR capture shows `"+ أضف طفلاً"` as a primary button in the header's trailing area (logical start = left in RTL). This is the same "Add Child" button but the AR layout makes it the primary CTA at top. Implementation currently puts it in the pick-row only | The AR capture places "+ Add Child" in the page header area (right column of the header row). This is an RTL layout difference, not a translation difference. In RTL, the header actions flex-direction reverses so the "Add Child" button appears left of the period select + Send Report group. Confirm `rowDir='row-reverse'` handles this — if it does, no code change needed | Verify — if current AR render matches, OK |

---

## SECTION C — Family Summary Strip / Hero

> Proving card: `web-family-hero.html`, `ar-web-family-hero.html`, `PagesApp.jsx::MyChildrenWebPage`.

| # | Element | Current (FamilySummaryStrip.tsx) | Target (capture + card) | Token / value | Severity |
|---|---|---|---|---|---|
| F-1 | **Hero border-radius** | `borderRadius="$card"` (20 px) | Card (web-family-hero.html): `borderRadius:24`. Capture: ~24 px | `--lx-radius-modal` 24 px | **Major** — change to `borderRadius="$modal"` or 24 px |
| F-2 | **Hero gradient** | `GradientBox` stops from `gradLevelup` (`#A855F7 → #6366F1`) at 135° | Card: `linear-gradient(135deg,#A855F7 0%,#6366F1 100%)`. Capture: purple (left) → indigo (right) in EN; AR: same gradient but `GradientBox angle={225}` for RTL (flips direction) | `--lx-grad-levelup` = `linear-gradient(135deg, #A855F7, #6366F1)`. AR: 225° angle is correct per SKILL rule — do NOT mirror the gradient color itself, only the direction | OK for EN. **Minor** — confirm `GradientBox` correctly uses the 225° angle in RTL |
| F-3 | **Hero box-shadow** | Not set on the strip | Card: `box-shadow: 0 16px 36px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.18)`. Capture has visible deep purple drop-shadow under hero | Add `boxShadow` prop: `0 16px 36px rgba(99,102,241,0.4)` outer + `inset 0 1px 0 rgba(255,255,255,0.18)` inner highlight | **Major** |
| F-4 | **Hero padding** | `padding="$6"` (24) | Card: `padding:24px`. But PagesApp `padding:28`. Capture: ~28 px spacious | Use 28 px. Token: `--lx-space-7` doesn't exist; use 28 px hardcoded (between `$6`=24 and `$8`=32) | **Minor** |
| F-5 | **Hero grid layout** | `flexDirection={rowDir} alignItems="center" gap="$4" flexWrap="wrap"` — Flexbox wrapping | Card: `display:grid; grid-template-columns:1.4fr repeat(4,1fr)`. Capture: 5-column grid, label column is ~1.4x wider. Avatar cluster is a 5th column in EN (replacing one stat slot visually) | Implement as CSS Grid on web: `gridTemplateColumns: '1.4fr repeat(4, 1fr)'`. On RN/mobile fallback to flex-wrap. Target web rendering needs the grid | **Blocker** — flex-wrap does not reproduce the 5-column proportioned layout |
| F-6 | **Hero eyebrow font-size** | `fontSize={11} textTransform="uppercase" letterSpacing={1} opacity={0.85}` | Card: `fontSize:11 fontWeight:800 letterSpacing:0.12em`. Capture: all-caps tiny label | `letterSpacing: '0.12em'` (current is `{1}` px — should be `0.12em`) | **Minor** |
| F-7 | **Hero headline font-size** | `fontSize={28} fontWeight="800"` | Card: `fontSize:24 fontWeight:900`. PagesApp: `fontSize:28`. Capture: large display — matches 24–28 px range. Use 28 px to match PagesApp | 28 px / 900 weight | **Minor** — change weight to 900 |
| F-8 | **Hero headline copy (EN)** | `t('parent.familySummary.headline')` | Capture: `"Your family is on a roll"` | i18n EN: `"Your family is on a roll"` | **Blocker if wrong** |
| F-9 | **Hero headline copy (AR)** | Same key | AR capture: `"عائلتك في تقدّم رائع"` — with shadda on تقدّم | i18n AR: `"عائلتك في تقدّم رائع"` | **Blocker if wrong** |
| F-10 | **Hero eyebrow copy (EN)** | `t('parent.familySummary.eyebrow')` | Capture: `"THIS WEEK · COMBINED"` | i18n EN: `"This Week · Combined"` (CSS `text-transform:uppercase` handles caps) | **Blocker if wrong** |
| F-11 | **Hero eyebrow copy (AR)** | Same key | AR capture: `"هذا الأسبوع · الإجمالي"` | i18n AR: `"هذا الأسبوع · الإجمالي"` | **Blocker if wrong** |
| F-12 | **Hero subline copy (EN)** | `t('parent.familySummary.subline', {learners, lessons})` | Capture: `"3 active learners · 18 lessons completed"` | i18n EN: `"{{learners}} active learners · {{lessons}} lessons completed"` | **Blocker if wrong** |
| F-13 | **Hero subline copy (AR)** | Same key | AR capture: `"٣ متعلمين نشطين · ١٨ درساً"` (Eastern-Arabic, plural suffix درساً) | i18n AR: `"{{learners}} متعلمين نشطين · {{lessons}} درساً"` with `Intl.NumberFormat('ar-EG')` | **Blocker if wrong** |
| F-14 | **Hero stat icon size** | `KPIStatCard variant="inline"` — icon size unknown | Card: `fontSize:20–22` for stat emoji. PagesApp `HeroStat`: `fontSize:22`. Capture: ~22 px emoji | Ensure inline KPI icon renders at 22 px | **Minor** |
| F-15 | **Hero stat value font-size** | `KPIStatCard variant="inline"` — value size unknown | Card: `fontSize:28 fontWeight:900 fontVariantNumeric:tabular-nums`. PagesApp `HeroStat`: `fontSize:28`. Capture: chunky numbers ~28 px | 28 px / 900 / tabular-nums | **Major** — if KPIStatCard inline doesn't hit 28 px / 900 weight, the numbers look weak |
| F-16 | **Hero stat label font-size** | Unknown | Card: `fontSize:10–11 fontWeight:800 textTransform:uppercase letterSpacing:0.08em opacity:0.85`. PagesApp `HeroStat`: `fontSize:11 fontWeight:800` | 11 px / 800 / uppercase / 0.08 em | **Minor** |
| F-17 | **Hero stat label copy (EN)** | Translation keys | Capture: TOTAL XP / LESSONS / BEST STREAK / BADGES EARNED | i18n keys must produce those exact strings (uppercase via CSS transform) | **Blocker if wrong** |
| F-18 | **Hero stat label copy (AR)** | Same keys | AR capture: إجمالي النقاط / دروس / أطول سلسلة / شارات | i18n AR keys: `"إجمالي النقاط"` / `"دروس"` / `"أطول سلسلة"` / `"شارات"` | **Blocker if wrong** |
| F-19 | **Hero stat value (AR) — numeral format** | `KPIStatCard variant="inline"` value from `formatNumber(totals.totalXp, locale)` | AR capture: `٤٬٤٨٠` (Eastern-Arabic with Arabic comma separator), `١٨`, `٩ أيام` (streak value has "أيام" suffix), `٥` | `Intl.NumberFormat('ar-EG')` for XP/Lessons/Badges ✓. Streak: must append `" أيام"` suffix in AR (`"{{n}} أيام"` template in i18n). EN streak: `"9d"` (no space, Latin d). | **Blocker** — streak label must differ: EN=`"9d"` AR=`"٩ أيام"` |
| F-20 | **Hero badge emoji** | `icon="🏅"` (medal) in FamilySummaryStrip | Card (web-family-hero.html): `🏆` (trophy). Capture EN: `🏆`. AR capture: `🏆` | Change from `"🏅"` to `"🏆"` | **Major** |
| F-21 | **Hero avatar cluster — position** | `AvatarStack` appears inline at end of flex row | Card (web-family-hero.html): the family emoji `👨‍👩‍👦` is positioned `absolute; right:-20px; top:-20px; fontSize:160px; opacity:0.18` as a decorative watermark. The AR card: `left:-20px; top:-20px`. No separate avatar cluster — capture shows semi-transparent illustrated child avatars inside the hero, right side | The "avatar cluster" in the capture is the large decorative `👨‍👩‍👦` watermark emoji at 160 px / opacity 0.18, absolutely positioned. The AvatarStack component is **not** what the capture shows. Additionally, the capture shows actual illustrated child avatar bubbles (Sami / Layla / Yusuf with trophy badge) — these are product illustrations, not the design system AvatarStack | Design Gap DG-3. For MVP: replace `AvatarStack` with the 160 px decorative `👨‍👩‍👦` emoji absolutely positioned in the hero. The illustrated bubbles are a future-state illustration asset. |
| F-22 | **Hero overflow:hidden** | `overflow="hidden"` | Card: `overflow:hidden` ✓ | OK | |
| F-23 | **Hero — AR grid direction** | `flexDirection='row-reverse'` in RTL | AR card: `grid-template-columns:1.4fr repeat(4,1fr)` — same structure but the label column is at start (right in RTL). In RTL the grid auto-reverses column order with `direction:rtl`. The stat columns should appear LEFT (XP→ streak→badges) with the label block on the RIGHT | On web, set `direction:rtl` on the grid wrapper. The stats will naturally move left, label right — matching the AR capture | **Major** — if using CSS grid, must set `direction:rtl` on the grid container in AR mode |

---

## SECTION D — Pick-a-child Row

> Proving card: `PagesApp.jsx` toolbar row.

| # | Element | Current | Target | Token | Severity |
|---|---|---|---|---|---|
| P-1 | **Row heading font-size** | `fontSize={18} fontWeight="700"` | PagesApp: `fontSize:18 fontWeight:800`. Capture: ~18 px | 18 px / 800 | **Minor** — change weight to 800 |
| P-2 | **Row heading copy (EN)** | `t('parent.myChildren.pickChild')` | Capture: `"Pick a child to view their progress"` | i18n EN: `"Pick a child to view their progress"` | **Blocker if wrong** |
| P-3 | **Row heading copy (AR)** | Same key | AR capture: no pick-row heading visible (the Add Child card appears top-left; the child cards are directly after the hero). The AR layout omits the pick-row heading or it's scrolled out. Do not invent AR copy for a row that may not be shown | Confirm whether the AR layout suppresses the pick-row label; if so, conditionally hide in RTL | Design gap note |
| P-4 | **"+ Add Child" button — radius** | `Button variant="primary" size="sm"` | SKILL rule 4: 16 px. Capture: indigo rounded button, ~16 px | 16 px ✓ if Button component is correct | OK (verify) |
| P-5 | **"+ Add Child" button — copy (EN)** | `t('parent.myChildren.addChild')` | Capture: `"+ Add Child"` | i18n EN: `"+ Add Child"` | **Blocker if wrong** |
| P-6 | **"+ Add Child" button height** | `size="sm"` | Card: `height:44`. Capture: ~44 px height | `height={44}` via size="sm" mapping | **Minor** |

---

## SECTION E — Child Dashboard Card

> Proving card: `web-child-card.html`, `PagesApp.jsx::ChildWebCard`.

| # | Element | Current (ChildDashboardCard.tsx) | Target | Token | Severity |
|---|---|---|---|---|---|
| C-1 | **Card border-radius** | `borderRadius="$card"` (20 px) | Card (web-child-card.html): `borderRadius:24`. Capture: ~24 px (visibly more rounded than 20) | `--lx-radius-modal` 24 px | **Major** |
| C-2 | **Card padding** | `padding="$5"` (20 px) | Card (web-child-card.html): `padding:22`. PagesApp: `padding:24`. Capture: ~22–24 px | 22 px (use `paddingAll={22}` or `padding={22}`) | **Minor** |
| C-3 | **Card gap (internal sections)** | `gap="$4"` (16) | Card: `gap:16`. PagesApp: `gap:18`. Capture has generous spacing ~18 px | 18 px | **Minor** |
| C-4 | **Card border** | `borderWidth={1} borderColor="$border"` (`rgba(255,255,255,0.08)`) | Card: `border:1px solid rgba(255,255,255,0.06)` | `rgba(255,255,255,0.06)` | **Minor** |
| C-5 | **Card shadow** | Not set | Card: `box-shadow: 0 4px 12px rgba(0,0,0,0.15)`. Capture: visible soft shadow | Add `boxShadow="$soft"` or `0 4px 12px rgba(0,0,0,0.15)` | **Major** |
| C-6 | **Card hover state** | Not set | PagesApp: `translateY(-2px)` + floating shadow on hover. SKILL rule 9: hover = brighten ~8%, scale 1.02 | Add `hoverStyle={{ scale: 1.02, boxShadow: '0 8px 24px rgba(0,0,0,0.25)' }}` | **Minor** |
| C-7 | **Avatar size** | `Avatar name size="lg"` | Card (web-child-card.html): `width:56 height:56 fontSize:22`. PagesApp: `width:64 height:64 fontSize:26`. Capture: ~56–64 px circle | Use 64 px to match PagesApp (size="xl" or override) | **Major** |
| C-8 | **Avatar font-weight** | `Avatar` component default | Card: `fontWeight:900`. Capture: bold initial letter | Ensure avatar initial is weight 900 | **Minor** |
| C-9 | **Avatar background color** | `Avatar` generic | Card (web-child-card.html): `background:#FB923C` for Sami. PagesApp: per-child color. Each child has a deterministic color | Avatar must use a deterministic per-child accent color (not a gradient). EN Sami=orange `#FB923C`, Layla=purple `#A855F7`, Yusuf=cyan `#38BDF8`. AR: same mapping | **Major** — if Avatar always uses the same gradient, it's wrong |
| C-10 | **Avatar inner shadow** | Unknown | Card: `box-shadow: inset 0 -3px 6px rgba(0,0,0,0.2), 0 6px 16px rgba(0,0,0,0.25)` | Add inner + outer shadow to avatar circle | **Minor** |
| C-11 | **Child name font-size** | `fontSize={20} fontWeight="800"` | Card: `fontSize:20 fontWeight:900`. PagesApp: `fontSize:22`. Capture: ~20–22 px | 22 px / 900 | **Minor** |
| C-12 | **Grade pill — border-radius** | `borderRadius="$pill"` (9999 px) | Card: `border-radius:9999` ✓ | OK | |
| C-13 | **Grade pill — background + text color** | `backgroundColor="$primarySoft" color="$primaryLight"` | Card: `rgba(79,70,229,0.18)` bg / `#A5B4FC` text ✓ | OK | |
| C-14 | **Grade pill copy (EN)** | `t('onboarding.grade.N')` | Capture: `"Grade 3"` / `"Grade 1"` / `"Grade 5"` (Latin, with "Grade" prefix) | i18n key must produce `"Grade {{n}}"` | **Blocker if wrong** |
| C-15 | **Grade pill copy (AR)** | Same key | AR capture: `"الصف ٣"` / `"الصف ١"` | i18n AR: `"الصف {{n}}"` with Eastern-Arabic numeral | **Blocker if wrong** |
| C-16 | **Language label (EN)** | `t('onboarding.language.en')` / `t('onboarding.language.ar')` | Capture: `"🇬🇧 English"` or `"🇸🇦 العربية"` with flag emoji prefix | i18n keys must produce those exact strings | **Blocker if wrong** |
| C-17 | **Language label (AR capture)** | Same | AR capture: `"🇸🇦 عربي"` / `"🇬🇧 إنجليزي"` | i18n AR: `"🇸🇦 عربي"` / `"🇬🇧 إنجليزي"` | **Blocker if wrong** |
| C-18 | **Active status dot — glow** | `backgroundColor={stats.activeToday ? '$success' : '$fg4'}` | Card: `box-shadow: 0 0 6px rgba(34,197,94,0.6)` on active dot. Current has no glow | Add `boxShadow="0 0 6px rgba(34,197,94,0.6)"` conditionally when active | **Minor** |
| C-19 | **Active status copy (EN)** | `t('parent.myChildren.activeToday')` / `t('parent.myChildren.inactive')` | Capture: `"Active today"` / `"Inactive"` | i18n EN: `"Active today"` / `"Inactive"` | **Blocker if wrong** |
| C-20 | **Active status copy (AR)** | Same keys | AR capture: `"نشط اليوم"` (for active) — not visible for inactive in capture | i18n AR: `"نشط اليوم"` (active) / inactive key TBD | **Minor** |
| C-21 | **KPI tile — border-radius** | `KPIStatCard` (unknown) | Card (web-child-card.html): `borderRadius:12`. PagesApp `ChildKPI`: `borderRadius:14`. Capture: ~12–14 px | 14 px (no token — DG-2) | **Minor** |
| C-22 | **KPI tile — background** | `KPIStatCard` uses `$bg` or `$card` | Card: `background:#0F172A` (`$bg`). PagesApp `ChildKPI`: `background:#0F172A`. Capture: dark tiles distinct from card body | `$bg` / `#0F172A` | **Major** — tiles must use `$bg` not `$card` |
| C-23 | **KPI tile — border** | Unknown | Card: `border:1px solid rgba(255,255,255,0.04)` | `rgba(255,255,255,0.04)` — lighter than standard `$border` | **Minor** |
| C-24 | **KPI tile — padding** | Unknown | Card: `padding:8px 10px`. PagesApp: `padding:10px 12px` | Use `paddingVertical={10} paddingHorizontal={12}` | **Minor** |
| C-25 | **KPI tile — value font-size** | Unknown | Card: `fontSize:14 fontWeight:900 fontVariantNumeric:tabular-nums`. PagesApp `ChildKPI value`: `fontSize:16 fontWeight:900`. Capture: ~14–16 px | 16 px / 900 / tabular-nums | **Major** |
| C-26 | **KPI tile — label font-size** | Unknown | Card: `fontSize:9 fontWeight:700 uppercase letterSpacing:0.06em`. PagesApp: `fontSize:10 fontWeight:700 uppercase` | 10 px / 700 / uppercase / 0.06 em | **Minor** |
| C-27 | **KPI — Level value format (EN)** | `"${t('parent.myChildren.statLevelShort')} ${stats.level}"` | Capture: `"Lv 12"` (prefix then number, space-separated) | i18n key `statLevelShort` = `"Lv"`. Value = `"Lv 12"` | **Blocker if wrong** |
| C-28 | **KPI — Level value format (AR)** | Same approach | AR capture shows tiles with `"المستوى ١٢"` (label first, then Eastern-Arabic). Card (ar-child-card.html): `"🧠 المستوى ١٢"` inline style | In AR: value = `"المستوى {{n}}"` with Eastern-Arabic numeral; do NOT use "Lv" prefix in AR | **Major** |
| C-29 | **KPI — Streak format (EN)** | `"${formatNumber(stats.streakDays, locale)}d"` | Capture: `"7d"` (no space, Latin d). OK | OK | |
| C-30 | **KPI — Streak format (AR)** | Same function | AR capture shows `"٢"` or `"٧"` — capture does NOT show "d" suffix, it just shows the digit. Card (ar-child-card.html): `"🔥 ٧ أيام"` inline | In AR, streak should be `"{{n}} أيام"` not `"{{n}}d"`. Change `formatNumber(streakDays, locale) + (isRtl ? ' أيام' : 'd')` | **Blocker** |
| C-31 | **Mastery label font-size** | Unknown | Card (web-child-card.html): `fontSize:10 fontWeight:700 uppercase letterSpacing:0.06em`. PagesApp: `fontSize:11 fontWeight:700 uppercase letterSpacing:0.06em` | 11 px | **Minor** |
| C-32 | **Mastery bar height** | `MasteryBar` (unknown height) | Card: `height:7px`. PagesApp: `height:8px`. Capture: ~7–8 px slim bar | 8 px | **Minor** |
| C-33 | **Mastery bar gradient** | `MasteryBar` (assumed) | Card: `linear-gradient(90deg,#22C55E,#4F46E5)`. Direction always LTR per SKILL rule 6 | `--lx-grad-xp` direction must be LTR regardless of `dir="rtl"` on parent | **Major** — wrap bar in `direction:ltr` container in RTL |
| C-34 | **Mastery bar track background** | Unknown | Card: `background:#0F172A` track | `$bg` | **Minor** |
| C-35 | **Mastery percent label (EN)** | `{stats.masteryPercent}%` | Capture: `"72%"` (Latin %) | Latin numeral + Latin `%` ✓ | OK |
| C-36 | **Mastery percent label (AR)** | Same | AR capture: `"٪٧٢"` — note `٪` (Arabic percent sign, U+066A) placed BEFORE the numeral in RTL | In AR, render as `"{{n}}٪"` with Eastern-Arabic numeral + Arabic percent sign. Must be in a `dir="ltr"` span or the RTL bidi will flip the order | **Blocker** |
| C-37 | **Footer border-top** | Not explicit | Card: `border-top: 1px solid rgba(255,255,255,0.05)`. Capture: faint line above weakest/view-dashboard row | Add `borderTopWidth={1} borderTopColor="rgba(255,255,255,0.05)"` (lighter than `$border` 0.08) | **Minor** |
| C-38 | **Footer padding-top** | Not explicit | Card: `paddingTop:12`. PagesApp: `paddingTop:14` | `paddingTop={14}` | **Minor** |
| C-39 | **Weakest label copy (EN)** | `t('parent.myChildren.weakest')` + topic key | Capture: `"Weakest: Fractions"` / `"Letters"` / `"Geometry"` | i18n EN key: `"Weakest:"` prefix; topics: `"Fractions"` / `"Letters"` / `"Geometry"` | **Blocker if wrong** |
| C-40 | **Weakest label copy (AR)** | Same | AR capture: `"الأضعف: الكسور"` | i18n AR: `"الأضعف:"` prefix; topic AR: `"الكسور"` / `"الحروف"` / `"الهندسة"` | **Blocker if wrong** |
| C-41 | **"View dashboard →" font-size** | `fontSize={14} fontWeight="700"` | Card: `fontSize:12 fontWeight:800`. PagesApp: `fontSize:12 fontWeight:800 color:#A5B4FC` | 12 px / 800 | **Minor** |
| C-42 | **"View dashboard →" arrow (RTL)** | `t('parent.myChildren.viewDashboard')` — arrow in copy | AR capture: `"عرض اللوحة ←"` (arrow flips direction — points left which is "next" in RTL) | i18n AR key must include `←` in the copy, not `→`. Never mirror the arrow programmatically — put it in the string | **Blocker** |
| C-43 | **Card grid layout** | `flexDirection={rowDir} flexWrap="wrap" gap="$4"` | PagesApp: `display:grid; gridTemplateColumns:'repeat(3,1fr)'; gap:16`. Capture: 3-column equal grid. Flex-wrap with `flex:1 minWidth:300` is not a true 3-col grid — it may produce uneven widths | On web, use CSS Grid `gridTemplateColumns:'repeat(3,1fr)'` with `gap:16`. Flex fallback for narrow breakpoints | **Blocker** |
| C-44 | **Card minimum height** | `minHeight={300}` | PagesApp: no explicit minHeight — cards fill content. Capture: all 3 cards same height (grid auto-rows stretches) | Remove `minHeight` and let grid `align-items:stretch` equalize heights | **Minor** |
| C-45 | **Inactive card — "Grade N" label** | EN capture (Yusuf): `"Grade 5"` with a gray status dot and `"Inactive"` text in `#64748B` | Current `$fg4` for inactive dot — confirm `$fg4` = `#64748B` | `--lx-fg4` should be `#64748B` (if not defined, use `#64748B` inline) | **Minor** |

---

## SECTION F — Add Child Card

> Proving card: `PagesApp.jsx` dashed button.

| # | Element | Current (AddChildCard.tsx) | Target | Token | Severity |
|---|---|---|---|---|---|
| A-1 | **Dashed border** | `borderStyle='dashed' borderWidth={2} borderColor="$borderStrong"` | PagesApp: `border: '2px dashed rgba(99,102,241,0.4)'`. Capture: indigo-tinted dashed border | `rgba(99,102,241,0.4)` — not `$borderStrong`. Change `borderColor` to `rgba(99,102,241,0.4)` | **Major** |
| A-2 | **Card border-radius** | `borderRadius="$card"` (20 px) | PagesApp: `borderRadius:24`. Capture: matches 24 px | `--lx-radius-modal` 24 px | **Major** |
| A-3 | **Card minimum height** | `minHeight={120}` | PagesApp: `minHeight:260`. Capture: add card is full-height matching sibling cards | `minHeight={260}` — or better, use CSS Grid stretch to match sibling card height | **Major** |
| A-4 | **Card layout** | `flexDirection={rowDir}` — horizontal row | PagesApp: `flexDirection:'column' alignItems:'center' justifyContent:'center'`. Capture: `+` icon centered above text, vertical stacking | Change to `flexDirection="column" alignItems="center" justifyContent="center"` | **Blocker** |
| A-5 | **"+" icon container size** | `width={48} height={48}` | PagesApp: `width:64 height:64 borderRadius:20`. Capture: ~64 px rounded square | 64 px / `borderRadius={20}` | **Major** |
| A-6 | **"+" font-size** | `fontSize={26}` | PagesApp: `fontSize:32`. Capture: large `+` | 32 px | **Minor** |
| A-7 | **Card title font-size** | `fontSize={16} fontWeight="700"` | PagesApp: `fontSize:16 fontWeight:800`. | 800 weight | **Minor** |
| A-8 | **Card title copy (EN)** | `t('parent.myChildren.addCardTitle')` | Capture: `"Add a child"` | i18n EN: `"Add a child"` | **Blocker if wrong** |
| A-9 | **Card title copy (AR)** | Same key | AR capture: `"أضف طفلاً"` | i18n AR: `"أضف طفلاً"` | **Blocker if wrong** |
| A-10 | **Card subtitle copy (EN)** | `t('parent.myChildren.addCardSubtitle')` | Capture: `"Set their grade, language, and login email"` | i18n EN: `"Set their grade, language, and login email"` | **Blocker if wrong** |
| A-11 | **Card subtitle copy (AR)** | Same | AR capture: `"حدّد صفه ولغته وبريد دخوله"` | i18n AR: `"حدّد صفه ولغته وبريد دخوله"` | **Blocker if wrong** |
| A-12 | **Card subtitle font-size** | `fontSize={13}` | PagesApp: `fontSize:12 textAlign:center maxWidth:200` | 12 px / center | **Minor** |
| A-13 | **Card hover** | `hoverStyle={{ borderColor: '$primary', backgroundColor: '$primarySoft' }}` | PagesApp: background → `rgba(79,70,229,0.06)`, borderColor → `#4F46E5`. SKILL rule 9: hover = brighten | `hoverStyle={{ borderColor: '#4F46E5', backgroundColor: 'rgba(79,70,229,0.06)' }}` ✓ | OK |
| A-14 | **Card press** | `pressStyle={{ scale: 0.98 }}` | SKILL rule 9: press = scale 0.95 / 80 ms | Change to `scale: 0.95` | **Minor** |
| A-15 | **AR add-card position** | Trailing in the grid (last item) | AR capture: the add-card appears FIRST (leftmost) in the grid in RTL. This is because `dir="rtl"` reverses grid column order. The first DOM child becomes the rightmost — but the add-card is last in DOM, so it appears leftmost in RTL grid. This is **correct RTL grid behavior**, not a bug | No code change needed — grid RTL reversal naturally places add-card at left edge in AR capture. Confirm this matches | OK |

---

## SECTION G — Page layout / scroll container

| # | Element | Current | Target | Token | Severity |
|---|---|---|---|---|---|
| L-1 | **Content max-width** | `maxWidth={1200}` | PagesApp / card spec: `max-width:920px` for just the hero; overall page content uses full width within the sidebar. Capture at 1280px frame shows content area ~1040 px wide (1280 − 240 sidebar). No explicit max-width in PagesApp | Remove `maxWidth={1200}` constraint or set it generously (e.g. 1100 px) — the 1200 px limit may cause centering artifacts at narrow viewports | **Minor** |
| L-2 | **Content padding** | `padding="$6"` (24) on the container Stack | PagesApp: `padding:28`. Capture: ~28 px gutter on all sides of content area | `padding={28}` (between token steps `$6`=24 and `$8`=32) | **Minor** |
| L-3 | **Content gap (between sections)** | `gap="$6"` (24) | PagesApp: `gap:20`. Capture: ~20 px between sections | `gap={20}` | **Minor** |

---

## SECTION H — RTL layout rules (cross-cutting)

| # | Rule | Current | Target | Severity |
|---|---|---|---|---|
| R-1 | **`dir="rtl"` on root** | Controlled by i18n locale + `useLocale`. Confirm the root `<html dir>` or top-level `View` carries RTL | The document root must have `dir="rtl"` in AR. Expo web renders `<html>` — set via `i18n.language` change + `document.documentElement.setAttribute('dir','rtl')` or Tamagui's `ThemeProvider direction` | **Blocker** |
| R-2 | **Progress bars / mastery bar — always LTR** | SKILL rule 6 | Wrap `MasteryBar` in a `<View style={{ direction: 'ltr' }}>` (web) or `writingDirection="ltr"` in RTL | **Blocker** |
| R-3 | **XP counts / Latin numbers in hero** | `formatNumber` handles Eastern-Arabic in AR | EN capture: `"4,480"` Latin. AR capture: `"٤٬٤٨٠"` Eastern-Arabic. `Intl.NumberFormat('ar-EG')` for AR produces Eastern-Arabic ✓ | OK — verify |
| R-4 | **"→" / "←" arrows in copy** | i18n copy for view-dashboard | AR: `"عرض اللوحة ←"` (← = logical next in RTL). EN: `"View dashboard →"`. Must be in string, not computed | **Blocker if arrows in EN string have `→` and AR string has `→` instead of `←`** |
| R-5 | **Avatar gradient — do not mirror** | SKILL rule (AR rule 7) | Avatar gradient `linear-gradient(135deg,...)` must NOT change direction in RTL | OK — as long as gradient is CSS `background`, not flipped by `scaleX(-1)` |
| R-6 | **Flag emoji direction** | `"🇬🇧 English"` / `"🇸🇦 العربية"` | In RTL context the flag glyph may render after the text due to bidi. Wrap each flag-label pair in a `dir="ltr"` span or `writingDirection="ltr"` Text | **Minor** |

---

## Design Gaps (items the kit doesn't cover)

| ID | Gap | Impact |
|---|---|---|
| DG-1 | **No 14 px or 16 px card radius token** — spec has 8/16/20/24/pill. Sidebar selector card target is 14 px, XP widget is 16 px. The closest tokens are `$sm`=8 and `$button`=16. The design system should add `--lx-radius-inner: 14px` for nested cards within a 20 px outer card. | Use `borderRadius={14}` and `borderRadius={16}` inline until the token is added |
| DG-2 | **No 12 px nav-item radius token** — sidebar nav buttons target 12 px. Closest token is `$sm`=8. Add `--lx-radius-nav: 12px` or reuse `borderRadius={12}` inline. | Use 12 px inline |
| DG-3 | **AvatarStack vs decorative watermark** — the design spec originally placed an `AvatarStack` in the hero, but the capture shows a 160 px `👨‍👩‍👦` watermark emoji. The illustrated child-avatar bubbles visible in the capture are product illustration assets not provided in the design system. | For now: replace `AvatarStack` with the watermark emoji. File an asset request for the illustrated child-bubble SVGs. |
| DG-4 | **`$fg4` token not defined** — the inactive status dot uses `$fg4` but `colors_and_type.css` only defines `--lx-fg1` through `--lx-fg3` and `--lx-fg-inverse`. Inactive gray `#64748B` needs a token. | Add `--lx-fg4: #64748B` to `colors_and_type.css` and map to Tamagui theme |
| DG-5 | **`$borderSubtle` (0.06 alpha) not tokenized** — the standard `$border` is `rgba(255,255,255,0.08)` but several preview cards use `0.06`. | Add `--lx-border-subtle: rgba(255,255,255,0.06)` |
| DG-6 | **`$primaryLight` / `#A5B4FC` not tokenized in colors_and_type.css** — used for active nav text, grade pills, view-dashboard link. It appears as `--lx-primary-hover: #6366F1` in CSS (different shade). `#A5B4FC` is indigo-300. | Add `--lx-primary-light: #A5B4FC` token |

---

## SECTION I — Exact i18n copy cheat sheet

This section is load-bearing for the frontend. EN keys on the left, AR values on the right. All AR numerals must go through `Intl.NumberFormat('ar-EG')`.

| i18n key | EN value | AR value |
|---|---|---|
| `common.appName` | `Learnexia` | `Learnexia` (dir=ltr always) |
| `parent.myChildren.title` | `My Children` | `أطفالي` |
| `parent.myChildren.subtitle` | `{{count}} children linked to your account` | `{{count}} أطفال مربوطين بحسابك` |
| `parent.myChildren.sendReport` | `Send Report` | `إرسال التقرير` |
| `parent.myChildren.pickChild` | `Pick a child to view their progress` | (suppress or: `اختر طفلاً لعرض تقدّمه`) |
| `parent.myChildren.addChild` | `+ Add Child` | `+ أضف طفلاً` |
| `parent.myChildren.addCardTitle` | `Add a child` | `أضف طفلاً` |
| `parent.myChildren.addCardSubtitle` | `Set their grade, language, and login email` | `حدّد صفه ولغته وبريد دخوله` |
| `parent.myChildren.activeToday` | `Active today` | `نشط اليوم` |
| `parent.myChildren.inactive` | `Inactive` | `غير نشط` |
| `parent.myChildren.weakest` | `Weakest:` | `الأضعف:` |
| `parent.myChildren.viewDashboard` | `View dashboard →` | `عرض اللوحة ←` |
| `parent.myChildren.mastery` | `Mastery` | `الإتقان` |
| `parent.myChildren.statLevel` | `Level` | `المستوى` |
| `parent.myChildren.statLevelShort` | `Lv` | `المستوى` |
| `parent.myChildren.statXp` | `XP` | `النقاط` |
| `parent.myChildren.statStreak` | `Streak` | `سلسلة` |
| `parent.myChildren.statStreakValue` | `{{n}}d` | `{{n}} أيام` |
| `parent.familySummary.eyebrow` | `This Week · Combined` | `هذا الأسبوع · الإجمالي` |
| `parent.familySummary.headline` | `Your family is on a roll` | `عائلتك في تقدّم رائع` |
| `parent.familySummary.subline` | `{{learners}} active learners · {{lessons}} lessons completed` | `{{learners}} متعلمين نشطين · {{lessons}} درساً` |
| `parent.familySummary.totalXp` | `Total XP` | `إجمالي النقاط` |
| `parent.familySummary.lessons` | `Lessons` | `دروس` |
| `parent.familySummary.bestStreak` | `Best Streak` | `أطول سلسلة` |
| `parent.familySummary.badgesEarned` | `Badges Earned` | `شارات` |
| `parent.myChildren.topics.fractions` | `Fractions` | `الكسور` |
| `parent.myChildren.topics.letters` | `Letters` | `الحروف` |
| `parent.myChildren.topics.geometry` | `Geometry` | `الهندسة` |
| `parent.myChildren.topics.reading` | `Reading` | `القراءة` |
| `parent.myChildren.topics.numbers` | `Numbers` | `الأرقام` |
| `parent.childSelector.meta` | `Grade {{grade}} · Level {{level}}` | `الصف {{grade}} · المستوى {{level}}` |
| `parent.nav.myChildren` | `My Children` | `أطفالي` |
| `parent.nav.overview` | `Overview` | `نظرة عامة` |
| `parent.nav.reports` | `Reports` | `التقارير` |
| `parent.nav.activity` | `Activity` | `النشاط` |
| `parent.nav.subjects` | `Subjects` | `المواد` |
| `parent.nav.settings` | `Settings` | `الإعدادات` |
| `parent.nav.xpWidget.eyebrow` | `This week` | `هذا الأسبوع` |
| `parent.nav.xpWidget.value` | `+{{xp}} XP` | `+{{xp}} نقطة` |
| `parent.nav.xpWidget.delta` | `Up {{percent}}% from last week` | `ارتفاع {{percent}}٪ عن الأسبوع الماضي` |

---

## Ordered fix list by severity

### Blockers (must fix before any PR)

1. **S-34** — Sidebar border uses wrong logical side (`borderEndWidth` → `borderStartWidth`) for RTL
2. **R-1** — Confirm `dir="rtl"` propagates to document root in AR
3. **R-2** — MasteryBar must be wrapped in LTR container
4. **F-5** — Hero uses flex-wrap instead of CSS Grid (5-column proportioned layout broken)
5. **C-43** — Child card grid uses flex-wrap instead of CSS 3-column grid
6. **A-4** — AddChildCard is horizontal (`flexDirection="row"`) instead of vertical column
7. **S-28** — MyChildren nav icon is `'👧'` not `'👨‍👩‍👦'`
8. **S-23** — Active nav item text color is `$fg1` (`#F8FAFC`) not `$primaryLight` (`#A5B4FC`)
9. **F-19** — Streak value in AR hero must be `"{{n}} أيام"` not `"{{n}}d"`
10. **C-30** — Streak KPI tile in AR must show `"{{n}} أيام"` not `"{{n}}d"`
11. **C-36** — Mastery percent in AR must use Arabic percent sign `٪` + Eastern-Arabic numeral in LTR span
12. **C-42** — "View dashboard" copy in AR must have `←` not `→`
13. **R-4** — All "next" arrows in i18n strings must flip: EN=`→`, AR=`←`
14. **Any i18n key that produces wrong copy** — run through the copy cheat sheet above

### Major (visible at a glance)

15. **S-2** — Sidebar bg `$bgElevated` → `$bg` (`#0F172A`)
16. **S-8** — Brand wordmark in AR sidebar missing `dir="ltr"`
17. **S-9** — Child-selector card radius 20 → 14 px
18. **S-12** — Avatar background must be per-child color gradient (orange/purple/cyan)
19. **S-20** — Nav item border-radius 16 → 12 px
20. **S-21** — Remove active left-border stripe from nav items
21. **H-1** — Header row needs its own 20/32 px padding + bottom border
22. **H-2** — Add `borderBottomWidth={1}` to header row
23. **H-3** — Page title font-size 26 → 22 px
24. **F-1** — Hero border-radius 20 → 24 px
25. **F-3** — Hero missing drop-shadow + inset highlight
26. **F-20** — Hero badges emoji `🏅` → `🏆`
27. **F-21** — Replace `AvatarStack` with 160 px decorative `👨‍👩‍👦` watermark
28. **C-1** — Child card border-radius 20 → 24 px
29. **C-5** — Child card missing soft drop-shadow
30. **C-7** — Avatar size: ensure 64 px
31. **C-9** — Avatar must use per-child accent color
32. **C-22** — KPI tile background must be `$bg` (`#0F172A`), not `$card`
33. **C-25** — KPI value font-size must be 16 px / weight 900 / tabular-nums
34. **C-28** — Level KPI in AR: show `"المستوى {{n}}"` not `"Lv {{n}}"`
35. **A-1** — AddChildCard dashed border color → `rgba(99,102,241,0.4)`
36. **A-2** — AddChildCard border-radius 20 → 24 px
37. **A-3** — AddChildCard minHeight 120 → 260 px (or grid stretch)
38. **A-5** — "+" icon container 48 → 64 px / radius 20 px
39. **F-23** — Hero grid in AR: set `direction:rtl` on CSS grid container

### Minor (polish pass)

40. **S-3** — border alpha 0.08 → 0.06
41. **S-7** — Brand wordmark 20 → 18 px
42. **S-10** — Child selector padding 12 → 10 px
43. **S-13** — Selector name 15 → 13 px
44. **S-14** — Selector meta 12 → 11 px
45. **S-17** — Chevron: use `‹` in AR Text literally
46. **S-18** / **A-14** — Press scale 0.98 → 0.95
47. **S-22** — Inactive nav weight 500 → 600
48. **S-24** — Nav font-size 15 → 14 px
49. **S-25** — Nav item padding add vertical 8 px
50. **S-29** — XP widget border-radius 20 → 16 px
51. **S-31** — XP widget font-size 24 → 20 px
52. **H-7** — Subtitle font-size 14 → 13 px
53. **F-4** — Hero padding 24 → 28 px
54. **F-7** — Hero headline weight 800 → 900
55. **P-1** — Pick-row heading weight 700 → 800
56. **C-2** — Card padding 20 → 22 px
57. **C-3** — Card section gap 16 → 18 px
58. **C-4** — Card border alpha → 0.06
59. **C-6** — Add hover lift `scale:1.02` + floating shadow
60. **C-8** — Avatar initial weight 900
61. **C-10** — Avatar inner + outer shadow
62. **C-11** — Child name 20 → 22 px / weight 900
63. **C-18** — Active status dot green glow
64. **C-21** — KPI tile border-radius → 14 px
65. **C-23** — KPI tile border `rgba(255,255,255,0.04)`
66. **C-24** — KPI tile padding 10/12
67. **C-26** — KPI label 10 px / 0.06em tracking
68. **C-31** — Mastery label 11 px
69. **C-32** — Mastery bar height 8 px
70. **C-34** — Mastery track bg `$bg`
71. **C-37/38** — Footer border-top `rgba(255,255,255,0.05)` + paddingTop 14
72. **C-41** — View-dashboard text 12 px / 800 weight
73. **C-44** — Remove minHeight from child card
74. **L-2** — Content padding 24 → 28
75. **L-3** — Content gap 24 → 20
76. **R-6** — Wrap flag+label in `dir="ltr"` span
77. **A-6** — "+" font-size 26 → 32
78. **A-7** — Add card title weight 700 → 800
79. **A-12** — Add card subtitle 13 → 12 px + center align

---

## SHARED component change summary

Changes that affect the `Sidebar` component and therefore ALL dashboard pages (Overview, Reports, Settings, My Children) — must be serialized (not parallelized with other page PRs):

| Change | Ref | Impact |
|---|---|---|
| Sidebar bg `$bgElevated` → `$bg` | S-2 | ALL pages |
| Sidebar border logical side fix (`borderEndWidth` → `borderStartWidth`) | S-34 | ALL pages (RTL) |
| Brand wordmark 20 → 18 px | S-7 | ALL pages |
| Brand wordmark `dir="ltr"` in AR | S-8 | ALL pages (AR) |
| Child-selector card radius 20 → 14 px | S-9 | ALL pages |
| Child-selector padding 12 → 10 px | S-10 | ALL pages |
| Avatar gradient (per-child color) | S-12 | ALL pages |
| Selector name 15 → 13 px | S-13 | ALL pages |
| Selector meta 11 px | S-14 | ALL pages |
| Chevron direction fix | S-17 | ALL pages |
| Press scale 0.98 → 0.95 | S-18 | ALL pages |
| Nav item radius 16 → 12 px | S-20 | ALL pages |
| Remove left-border active stripe | S-21 | ALL pages |
| Active nav text `$primaryLight` (#A5B4FC) | S-23 | ALL pages |
| Nav font-size 15 → 14 px | S-24 | ALL pages |
| Nav icon: My Children `'👧'` → `'👨‍👩‍👦'` | S-28 | ALL pages |
| XP widget border-radius 20 → 16 px | S-29 | ALL pages |
| Inactive nav weight 500 → 600 | S-22 | ALL pages |
| Nav padding vertical 8 px | S-25 | ALL pages |

All of the above must ship in a single `Sidebar.tsx` PR or be the first batch in the serialized queue.

---

## Implementation handoff

| Component | File | Fix refs |
|---|---|---|
| `Sidebar` | `apps/student-app/app/(parent)/_components/Sidebar.tsx` | S-2,3,7,8,9,10,12,13,14,17,18,20,21,22,23,24,25,28,29,30,31,32,33,34 |
| `MyChildrenWeb` | `apps/student-app/app/(parent)/_components/MyChildrenWeb.tsx` | H-1,2,3,7,L-1,2,3,P-1 |
| `FamilySummaryStrip` | `apps/student-app/app/(parent)/_components/FamilySummaryStrip.tsx` | F-1,2,3,4,5,6,7,14,15,16,19,20,21,22,23 |
| `ChildDashboardCard` | `apps/student-app/app/(parent)/_components/ChildDashboardCard.tsx` | C-1 through C-45, R-2,3,6 |
| `AddChildCard` | `apps/student-app/app/(parent)/_components/AddChildCard.tsx` | A-1 through A-15 |
| `packages/ui Avatar` | `packages/ui` | C-7,8,9,10,S-11,12 |
| `packages/ui KPIStatCard` | `packages/ui` | C-21,22,23,24,25,26,F-14,15,16 |
| `packages/ui MasteryBar` | `packages/ui` | C-32,33,34,R-2 |
| `i18n EN+AR locale files` | `apps/student-app/src/i18n/` | Section I — all Blocker copy keys |
| `design-system/colors_and_type.css` | `design-system/` | DG-1,2,4,5,6 (new tokens) |
| `packages/ui` Tamagui theme | `packages/ui` | DG-4,6 (`$fg4`, `$primaryLight`) |
