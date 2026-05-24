# Pixel-Alignment Pass — Dashboard / Overview (Parent Web)

**Captures:** `design-system/screenshots/web/05-dashboard.png` (EN/LTR) and `design-system/screenshots/web-ar/05-dashboard.png` (AR/RTL)
**Preview cards (canonical tokens):** `web-sidebar.html`, `web-page-header.html`, `web-kpi-row.html`, `ar-web-kpi.html`, `web-skills-mastery.html`, `web-weak-areas-list.html`, `ar-web-weak-areas.html`, `web-recommendations.html`, `ar-web-recommendations.html`, `web-activity-chart.html`
**Implementation under review:** `apps/student-app/app/(parent)/overview.tsx`, `_components/OverviewWeb.tsx`, `SubjectMasteryCard.tsx`, `FocusAreasCard.tsx`, `DailyActivityCard.tsx`, `parentDashboardStubs.ts`, `Sidebar.tsx`; `packages/ui/KPIStatCard`, `MasteryBar`, `Button`

> **Legend:** SHARED = the delta touches Sidebar.tsx or PDHeader which is also rendered by My Children / Reports / Settings pages. Any fix to those files propagates to all three pages — mark changes carefully.

---

## Delta Table

### BLOCKER — Must fix before merge

| # | Element | Current | Target (token + proving card) | Severity | Fix |
|---|---------|---------|-------------------------------|----------|-----|
| B-01 | **KPI card — value font size** | `OverviewWeb.tsx` renders value at `fontSize={32}` in a hand-rolled `Stack` (not using `KPIStatCard`) | Capture + `web-kpi-row.html` line 6: `font-size:28px; font-weight:800`. Token `--lx-size-h2:24px` is too small; the design card pins it at **28px w800 tabular-nums** = `fontSize={28}`. The current 32 is measurably taller than the capture. | Blocker | Change the value `Text` in `OverviewBody` KPI loop to `fontSize={28}`. |
| B-02 | **KPI card — label font size + tracking** | `fontSize={11}` with `letterSpacing={0.6}` | `web-kpi-row.html` line 5: `font-size:11px; letter-spacing:0.08em`. `0.08em` at 11px = 0.88px. In Tamagui letterSpacing is in pixels, not em. Current value 0.6 is 31% short. | Blocker | Change to `letterSpacing={0.88}` (or express as `letterSpacing={'0.08em'}` if Tamagui supports that). |
| B-03 | **KPI card — icon chip bg uses `$22` hex hack** | `background: \`${accent}22\`` in `PDStatCard` in `Components.jsx`. The Tamagui implementation uses no icon chip at all — the icon is rendered as a raw `Text fontSize={18}` without a tinted rounded box. | Capture clearly shows a 32×32 rounded icon box with tinted background (e.g. `rgba(79,70,229,0.13)` for ⏱️). Token: `--lx-primary-soft: rgba(79,70,229,0.18)` — use `$primarySoft` / `$successSoft` / `$warningSoft` / `$streakGlow`-based soft per accent. `web-kpi-row.html` line 5 shows `width:32px;height:32px;border-radius:10px;background:rgba(79,70,229,0.13)`. | Blocker | In the `OverviewBody` KPI tile (or refactor to use `packages/ui/KPIStatCard`): wrap the icon in a `Stack` w/h=32 `borderRadius="$sm"` (`--lx-radius-sm:8px`) with a per-accent soft background token (see accent-to-soft map below). |
| B-04 | **KPI card — is not using the `packages/ui/KPIStatCard` component** | `OverviewWeb.tsx` builds KPI tiles by hand-assembling `Stack + Text` combos (lines 207–260). `packages/ui/KPIStatCard` exists but is not used here (it is used for child mini-KPIs in `ChildCard`). | Design Spec §Implementation handoff: KPI tiles on Overview should use `<KPIStatCard>` so the single-source of truth is `packages/ui`. However the current `KPIStatCard` component is a "child tile" variant (tile vs inline) showing a small icon + value + uppercase label — not a full stat card with an icon-chip header row, big number, and delta. The `PDStatCard` in Components.jsx is the correct reference. | Blocker | Either (a) extend `KPIStatCard` to a `'dashboard'` variant matching `PDStatCard`'s layout, OR (b) inline a `PDStatCard`-equivalent layout directly in `OverviewBody` using the correct measurements from B-01/B-02/B-03. Option (b) is safer (no new design pattern per CLAUDE.md). If option (a), stop and ask the lead. |
| B-05 | **Page header — title font size wrong** | `OverviewWeb.tsx` renders `fontSize={26}` for `"{Child}'s progress"` | `web-page-header.html` line 5: `font-size:20px; font-weight:800`. `PDHeader` in `Components.jsx` also uses 22px. Capture measures approximately 20–22px (it is clearly not 26px). Token closest is `--lx-size-h3:18px` (too small) or a literal 20px. Use **22px w800** to match `PDHeader` and capture. | Blocker | Change title `fontSize` to `22`. |
| B-06 | **Page header — "Send Report" button radius** | `Button variant="primary" size="sm"` gives `borderRadius="$button"` = `--lx-radius-button:16px`. This is correct per the design system rule. However the header's period-select `<Select>` has `borderRadius="$button"` (16px) — the capture and `web-page-header.html` line 7 clearly show `border-radius:10px` for the select, which is an input/select, not a button. | `--lx-radius-sm:8px` = chips/inputs. Select = input class → should use `borderRadius="$sm"` (8px) not `$button` (16px). | Blocker | In `OverviewWeb.tsx` the `<Select>` wrapper `Stack` has no explicit radius — the `Select` component itself likely inherits `$button`. Check `packages/ui/Select` and if needed override to `borderRadius="$sm"` in the wrapping Stack or pass a `borderRadius` prop. |
| B-07 | **Subject mastery — card header font size** | `SubjectMasteryCard` sets `fontSize={18}` for the title | `web-skills-mastery.html` line 5 shows `font-weight:800;font-size:16px` and `PDPanel` in `Components.jsx` also pins panel titles at 16px. Capture title "Subject mastery" is clearly 16px-weight-800. | Blocker | Change `SubjectMasteryCard` title to `fontSize={16}`. |
| B-08 | **Subject mastery — subtitle font size** | `fontSize={13}` | `web-skills-mastery.html` line 6 and `PDPanel` use `font-size:12px`. | Blocker | Change to `fontSize={12}`. |
| B-09 | **Subject mastery — per-subject label in `MasteryBar`** | `MasteryBar` renders the label in `$fg3` (muted, `#94A3B8`) at `fontSize={11}` uppercase | `web-skills-mastery.html` line 8: subject name is `color:#F8FAFC` (fg1), `font-size:13px`, `font-weight:700` — NOT uppercase muted. Only the lesson count part is muted. The capture confirms: "Math" is white, "72%" is colored. | Blocker | In `MasteryBar`, change label color from `$fg3` to `$fg1` and size from 11 to **13**, weight stays 700. The per-subject percent should use the accent color (currently `$fg1` — same issue, fix to render in the accent color token). This is a design-system component change, affects every use of `MasteryBar`. |
| B-10 | **Subject mastery — per-subject percent color** | `MasteryBar` renders percent as `color="$fg1"` (`#F8FAFC`) | `web-skills-mastery.html` line 8: percent is colored by subject (e.g. `color:#4F46E5` for Math 72%). Capture confirms: "72%" is indigo, "65%" is purple, "58%" is green, "84%" is orange. The `MasteryBar` has an `accent` prop which drives the bar fill but does NOT drive the percent label color. | Blocker | `MasteryBar`: change the percent `Text` to `color={accent ?? '$fg1'}`. Confirm `SubjectMasteryCard` already passes per-subject accent tokens (`$primary`, `$success`, `$purple`, `$accent`) — it does (lines 34–38), so the fix is purely in `MasteryBar`. |
| B-11 | **Focus areas — card panel radius** | `FocusAreasCard` outer `Stack` uses `borderRadius="$card"` (`--lx-radius-card:20px`) | Capture and `web-weak-areas-list.html`: the outer card in the capture uses 20px. The inner row items use `border-radius:14px`. Current implementation uses `borderRadius="$sm"` (8px) for each row — too sharp. | Blocker | Change each focus-area row's `borderRadius` from `"$sm"` (8px) to `14` (literal, as no token covers 14px; or introduce `$cardInner:14` — see gap GAP-01). |
| B-12 | **Focus areas — row padding** | Row `padding="$4"` = `--lx-space-4:16px` | `web-weak-areas-list.html` line 4: `padding:12px 14px`. Token `--lx-space-3:12px` vertical, `--lx-space-3-half` does not exist. Use `paddingVertical={12}` `paddingHorizontal={14}`. | Blocker | Change row padding from `"$4"` to `paddingVertical={12} paddingHorizontal={14}`. |
| B-13 | **Focus areas — row background** | `backgroundColor="$bg"` (`#0F172A`) | `web-weak-areas-list.html` line 4: `background:#0F172A`. This is correct. However the track background inside the confidence bar is `backgroundColor="$cardSoft"` (`#334155`) which does not match — the preview card uses `background:#1E293B` (`$card`). The border is also missing: preview has `border:1px solid rgba(255,255,255,0.04)`. | Blocker | Confidence bar track: change `backgroundColor="$cardSoft"` to `"$card"`. Add `borderWidth={1} borderColor="rgba(255,255,255,0.04)"` to the bar track Stack. |
| B-14 | **Focus areas — icon chip background** | `backgroundColor="$cardSoft"` (`#334155`) | `web-weak-areas-list.html` line 5: icon chip bg is `rgba(color, 0.13)` colored by subject severity (e.g. red-tinted for danger). `$cardSoft` is flat grey with no tint. | Blocker | Icon chip background should be the severity soft color: `$dangerSoft` for `FOCUS_SEVERITY.High` (`rgba(239,68,68,0.13)`) and `$warningSoft` for `FOCUS_SEVERITY.Medium` (`rgba(245,158,11,0.13)`). Icon color should match (`$danger` / `$warning`). Current implementation uses a generic grey. See accent-to-soft map. |
| B-15 | **Focus areas — topic label font size** | `fontSize={15}` | `web-weak-areas-list.html` line 6: `font-size:13px; font-weight:700`. | Blocker | Change to `fontSize={13}`. |
| B-16 | **Focus areas — confidence bar height** | `height={8}` (correct) — but the bar `borderRadius` is `9999` on both container and fill | `web-weak-areas-list.html` line 7: `border-radius:9999px; overflow:hidden` on container. Fill has no separate radius. This is fine — but the fill `Stack` also has `borderRadius={9999}` making the fill ends rounded inside a rounded container. This creates a visual double-radius artifact when the fill is short. | Blocker | Remove `borderRadius={9999}` from the fill `Stack` (the container's `overflow:hidden` handles the clip). |
| B-17 | **Focus areas — percent label width + alignment** | `width={48}` `textAlign` toggles by RTL | `web-weak-areas-list.html` line 8: `width:44px; text-align:right`. Use `width={44}`. For RTL (`ar-web-weak-areas.html` line 8): `text-align:left` (the percent ends up on the logical START in RTL). Implementation already toggles correctly but width should be 44 not 48. | Blocker | Change `width={48}` to `width={44}`. |
| B-18 | **Daily activity — "Export CSV" button variant** | `Button variant="secondary"` which renders `backgroundColor="$cardSoft"` with a border | `web-activity-chart.html` line 7 and capture: Export CSV is a **ghost pill** — `background:transparent; border:1px solid rgba(255,255,255,0.12); border-radius:9999px; color:#A5B4FC`. That maps to a ghost pill style, not the secondary filled button. | Blocker | Change to `Button variant="ghost"` and add `borderRadius={9999}` explicitly, or wrap in a custom pill `Stack` with `onPress`. The pill style is used exclusively for card-level secondary actions (Export CSV, See all) — it is a distinct pattern from the page-level ghost button. |
| B-19 | **Page header — date range font size** | `fontSize={14}` | `web-page-header.html` line 5: `font-size:12px; color:#94A3B8`. | Blocker | Change date range `Text` to `fontSize={12}`. |

---

### MAJOR — Significant visual gap with the capture

| # | Element | Current | Target (token + proving card) | Severity | Fix |
|---|---------|---------|-------------------------------|----------|-----|
| M-01 | **Sidebar — active nav item: LEFT border accent strip** | `borderStartWidth={3}` colored indigo when active. Background `$primarySoft`. | Capture + `web-sidebar.html` line 12: the active item has `background:rgba(79,70,229,0.18)` but NO visible left border strip in the capture or the HTML. The HTML uses no border — just the tinted background. The left accent strip is a design choice not present in the DS card. It creates a layout shift (items with border are 3px wider than inactive). | Major | Remove `borderStartWidth` / `borderStartColor` from the active nav item in `Sidebar.tsx`. The active state is communicated by `$primarySoft` background alone (as in `web-sidebar.html`). SHARED change. |
| M-02 | **Sidebar — active nav item: label color when active** | `color={isActive ? '$fg1' : '$fg2'}` — active label is `$fg1` white | `web-sidebar.html` line 12: active item color is `#A5B4FC` (indigo-200 / `$primaryLight`). Inactive is `#94A3B8` (`$fg3`). Implementation uses `$fg1` (white) for active which is brighter than the capture. | Major | Change active label color to `'$primaryLight'` (token alias for `#A5B4FC`). Inactive: change `'$fg2'` to `'$fg3'`. SHARED change. |
| M-03 | **Sidebar — nav item font size** | `fontSize={15}` | `web-sidebar.html` line 12: `font-size:14px`. | Major | Change to `fontSize={14}`. SHARED change. |
| M-04 | **Sidebar — nav item padding** | `paddingHorizontal="$3"` (12px) and `minHeight={48}` | `web-sidebar.html` line 12: `padding:8px 12px` (8px vertical, 12px horizontal). `minHeight` should come from the padding+content (≈32px natural, padded to 48 for touch). Since this is web parent dashboard, 48 touch target is correct but the capture shows tighter vertical spacing. Use `paddingHorizontal={12} paddingVertical={10}` with `minHeight={40}` (the sidebar items in the capture are clearly not 48px tall). | Major | Change to `paddingHorizontal={12} paddingVertical={10}` `minHeight={40}`. SHARED change. Note: on the mobile view of the same screen the ScreenHeader handles padding, not Sidebar. |
| M-05 | **Sidebar — child-selector card border-radius** | `borderRadius="$card"` (20px) | `web-sidebar.html` line 6: `border-radius:14px`. `Components.jsx` `PDSidebar` line 6: `borderRadius:16`. Neither 20 nor 14 — the reference HTML says 14, `Components.jsx` says 16. Capture appears 14px. Use **16px** to match `Components.jsx` which is the production reference. | Major | Change `borderRadius="$card"` to `borderRadius={16}`. SHARED change. |
| M-06 | **Sidebar — child-selector card border color** | `borderColor="$borderStrong"` (`rgba(255,255,255,0.16)`) | `Components.jsx` PDSidebar line 6: no border (just background `#1E293B`). `web-sidebar.html` line 6: no border. Capture shows no distinct border on the child-selector card — it's a flat `$card` surface. | Major | Remove `borderWidth` / `borderColor` from the child-selector card, or reduce to `borderColor="$border"` (0.08 alpha). SHARED change. |
| M-07 | **Sidebar — brand logo size** | `width:32, height:32` | `Components.jsx` PDSidebar line 21: `width:36, height:36`. Capture shows approximately 36px. | Major | Change logo dimensions to `width={36} height={36}`. SHARED change. |
| M-08 | **Sidebar — brand name font size** | `fontSize={20}` | `Components.jsx` PDSidebar line 22: `font-size:18px`. `web-sidebar.html` line 5: `font-size:16px`. Use **18px** to match `Components.jsx`. | Major | Change to `fontSize={18}`. SHARED change. |
| M-09 | **KPI row — outer padding** | `OverviewWeb` wraps all content in `padding="$6"` (24px) including the KPI row | Capture and `web-page-header.html`: page content starts immediately below the header divider. `web-kpi-row.html` uses `padding:24px` for the whole preview. The 24px outer padding is correct but the `maxWidth={1200}` constrains the layout tighter than the 1280px design frame — at 1280px the content is narrower than in the capture. | Major | Change `maxWidth={1200}` to `maxWidth={1280}` in `OverviewWeb` root `Stack`, or remove the maxWidth constraint so the content fills the remaining space next to the 240px sidebar. The design frame is 1280px total; sidebar = 240px; content = 1040px. |
| M-10 | **KPI cards — gap between cards** | `gap="$4"` (16px) in the KPI `flexDirection row` stack | `web-kpi-row.html`: grid `gap:14px`. Use **14px** for the KPI row gap. | Major | Change KPI row `gap="$4"` to `gap={14}`. |
| M-11 | **KPI cards — delta text: "vs last week" suffix** | `delta` string includes " vs last week" from the i18n key (`t('parent.overview.kpi.timeDelta')` etc.). In AR the full string is rendered: "+٤٢ د عن الأسبوع الماضي". | `web-kpi-row.html` line 7: `+42m vs last week` (EN). `ar-web-kpi.html` line 7: `+٤٢ د عن الأسبوع الماضي`. Both previews confirm the full phrase. The current implementation relies on i18n keys — this is correct behavior. However, the delta string must confirm to locale: for AR the delta value itself must be Eastern-Arabic numerals. | Major | Confirm `formatDuration` and `formatNumber` use `'ar-EG'` locale (they do). Confirm i18n key shapes match. Flag that XP delta uses `%` symbol — in AR this must be `٪` (Unicode ARABIC PERCENT SIGN U+066A) or `%` with `dir="ltr"` wrap. Current `xpDeltaPercent` renders as `+28%` — in AR the `%` should appear on the right: render the full string via the i18n key and let the string read `+٢٨٪ عن الأسبوع الماضي`. |
| M-12 | **Daily activity card — header padding** | `DailyActivityCard` uses `padding="$6"` (24px) | `web-activity-chart.html` line 4 and `PDPanel`: `padding:22px`. | Major | Change to `padding={22}` or use `padding="$5"` (20px) + add `paddingTop={22}` manually. Closest clean value: `padding={22}`. |
| M-13 | **Daily activity card — placeholder area appearance** | `height={260}` `backgroundColor="$bg"` `borderStyle="dashed"` with centered text | Capture shows the chart area filled with real bars (phase-5 deferred — acknowledged). The container should still match the panel's visual identity. The `$bg` background inside a `$card` panel is correct (the chart track in the preview uses `#0F172A` for bar tracks). However the dashed border adds visual noise not present in the design system. | Major | Remove `borderStyle="dashed"` from the placeholder. Keep `backgroundColor="$bg"` and `borderWidth={1} borderColor="$border"`. The placeholder text should be `color="$fg3"` (already correct). Height 260px is reasonable since the capture chart area is approximately 200px — reduce to `height={200}` to better match. |
| M-14 | **Subject mastery — card padding** | `padding="$6"` (24px) | `web-skills-mastery.html` line 4: `padding:22px`. `PDPanel`: `padding:22`. | Major | Change to `padding={22}`. |
| M-15 | **Subject mastery — row gap** | `gap="$4"` (16px) between mastery rows | `web-skills-mastery.html` line 7: `gap:14px`. | Major | Change `gap="$4"` to `gap={14}` for the rows stack. |
| M-16 | **Subject mastery — bar track background** | `MasteryBar` track uses `backgroundColor="$bg"` (`#0F172A`) | `web-skills-mastery.html` line 8: track is `background:#0F172A`. This IS correct. No fix needed here. | — | No action. |
| M-17 | **Subject mastery — bar height** | `TRACK_HEIGHT = 8` in `MasteryBar` | `web-skills-mastery.html` line 8: `height:10px`. The capture also shows bars that are visually thicker than 8px. | Major | Change `TRACK_HEIGHT` in `MasteryBar` to **10** (matches preview card). This affects all uses of `MasteryBar`. |
| M-18 | **Focus areas — outer card padding** | `padding="$6"` (24px) | `PDPanel` / capture: 22px. Same issue as M-12/M-14. | Major | Change to `padding={22}`. |
| M-19 | **Focus areas — gap between rows** | `gap="$3"` (12px) | `web-weak-areas-list.html` outer flex container: `gap:10px`. | Major | Change rows `gap="$3"` to `gap={10}`. |
| M-20 | **Focus areas — "See all" affordance** | A plain `Stack` with no border — appears as a text link | Capture and `PDPanel` action prop: `action="See all"` renders a pill ghost button (`border:1px solid rgba(255,255,255,0.12); border-radius:9999px`). The current `FocusAreasCard` renders the "See all" as a bare `Stack + Text` with no border. | Major | Replace the bare `Stack` with the same pill ghost pattern as `PDPanel action` or `Button variant="ghost"` with `borderRadius={9999}` (pill). |
| M-21 | **RTL — progress bars in AR must stay LTR** | `MasteryBar` uses `flexDirection={rowDir}` on the bar track `Stack`, meaning in RTL the fill grows from right to left. | `SKILL.md` rule 6: "Bar charts and progress bars STAY LTR (wrap in `direction: ltr`)". `ar-web-kpi.html` mastery bars fill left-to-right even in RTL. The `ar-web-weak-areas.html` line 7 shows `direction:ltr` on the bar wrapper. | Major | In `MasteryBar`: force bar track and fill `Stack` to `flexDirection="row"` (not `rowDir`) regardless of locale. This matches `ar-web-weak-areas.html` which wraps the bar `<div style="width:120px;direction:ltr">`. Also apply same fix to the confidence bar in `FocusAreasCard`. |
| M-22 | **RTL — confidence bar in FocusAreasCard** | `flexDirection={rowDir}` on bar container | Same issue as M-21. Bar must stay LTR. | Major | Force `flexDirection="row"` on the confidence bar track `Stack`. |
| M-23 | **RTL — sidebar is LTR-only** | `Sidebar.tsx` renders the sidebar on the left always. In RTL (`index-ar.html`) the sidebar appears on the RIGHT and the content on the LEFT. | AR capture confirms: sidebar is on right, content on left. The `AppShell` in `Components.jsx` uses `display:flex` — in RTL the flex order naturally reverses. In Tamagui/RN this requires explicit direction control. | Major | In `overview.tsx` the `Stack flexDirection="row"` wrapping Sidebar + content should be `flexDirection={isRtl ? 'row-reverse' : 'row'}`. This places the sidebar on the right in RTL. SHARED change (affects My Children, Reports, Settings too). |
| M-24 | **RTL — AR subject labels in SubjectMasteryCard** | The AR capture shows: "الرياضيات", "القراءة", "العلوم", "العربية" (Math/Reading/Science/Arabic). The current 4 product subjects are Math/Science/Arabic/English (no Reading, no Art). | AR capture (`05-dashboard.png` AR) shows "القراءة" (Reading) and not English. This is stale mock data in the design-system capture. The correct subjects are Math / Science / Arabic / English per product decisions. The EN capture also shows "Reading" and "Art" which are stale. **This is an intentional deviation from the capture per product overrides.** | Major | Keep the 4 product subjects (Math/Science/Arabic/English) as implemented. The AR i18n keys need: `الرياضيات` (Math), `العلوم` (Science), `العربية` (Arabic), `الإنجليزية` (English). Confirm these are in the translation files. Flag to team that capture shows old subject set. |
| M-25 | **RTL — numbers in AR KPI tiles** | `formatNumber` uses `'ar-EG'` locale which produces Eastern-Arabic numerals (٤٨٠, ١٤, ٧) | `ar-web-kpi.html` lines 6–21: confirms Eastern-Arabic numerals for inline values. XP value is `٤٨٠`, lessons `١٤`, streak `٧`. Time learning uses Arabic format `٣ س ٢٤ د` (note: `س` = hour, `د` = minute). The current `formatDuration` produces `٣h ٢٤m` with Latin `h`/`m` in AR locale — wrong. | Major | In `formatDuration`, in AR locale use Arabic unit labels: `س` for hours and `د` for minutes. Result: `${fmt(hours)}س ${fmt(minutes)}د`. Confirm these units match the i18n key `parent.overview.kpi.timeLearning`. |
| M-26 | **RTL — percent symbol in AR** | XP delta shows `+28%` localized as `+٢٨%` — the `%` is Latin | SKILL.md: Eastern-Arabic numerals inline but `%` in AR should be `٪` (U+066A ARABIC PERCENT SIGN) or the whole percent expression should be `dir="ltr"`. `ar-web-kpi.html` line 12: `+٢٨٪ عن الأسبوع الماضي`. | Major | Use `٪` (U+066A) in the AR i18n key for XP delta: `+{value}٪ عن الأسبوع الماضي`. Do not use `dir="ltr"` wrapper for inline delta text. |
| M-27 | **Page header — "Send Report" button radius** | `Button size="sm"` has `height:40px` (from Button variant: `sm: { height: 40 }`) | `web-page-header.html` line 8: `padding:8px 16px; border-radius:10px` = 10px radius. Design system rule: buttons = 16px. The 10px in the preview card contradicts the rule. The rule wins (`--lx-radius-button:16px`). No fix needed for radius. But `padding:8px 16px` maps to the `sm` size height 40px + `paddingHorizontal="$4"` (16px). Current is correct. | — | No action on radius. Confirm current `Button size="sm"` is correct. |

---

### MINOR — Small detail or copy gap

| # | Element | Current | Target | Severity | Fix |
|---|---------|---------|--------|----------|-----|
| N-01 | **Sidebar — XP widget: `$xp` eyebrow color** | `color="$xp"` (correct token `#FACC15`) | `web-sidebar.html` line 18: `color:#FACC15` and `font-size:10px; letter-spacing:0.08em; text-transform:uppercase`. Current renders `fontSize={10}` `letterSpacing={1}` uppercase. `0.08em` at 10px = 0.8px — current 1px is close but slightly off. | Minor | Change `letterSpacing={1}` to `letterSpacing={0.8}`. SHARED change. |
| N-02 | **Sidebar — XP widget value font size** | `fontSize={24}` | `web-sidebar.html` line 18: `font-size:18px` in preview card. `Components.jsx` PDSidebar uses `font-size:20px`. Use **20px** to match Components.jsx. | Minor | Change to `fontSize={20}`. SHARED change. |
| N-03 | **Sidebar — XP widget padding** | `padding="$4"` (16px) | `Components.jsx` PDSidebar uses `padding:14`. | Minor | Change to `padding={14}`. SHARED change. |
| N-04 | **Sidebar — XP widget text suffix** | Renders as `t('parent.nav.xpWidget.delta', { percent: 28 })` → e.g. "Up 28% from last week" | `web-sidebar.html`: "Up 28% from last week" (EN). For AR: "متقدم ٢٨٪ عن الأسبوع الماضي" (or similar). Confirm AR key uses `٪` not `%`. | Minor | Update AR translation key to use `٪`. |
| N-05 | **KPI tile — hover state** | No hover style defined on the KPI tile `Stack` | Design system: `hover → brighten by ~8% (lighten surface, scale 1.02)`. Add `hoverStyle={{ backgroundColor: '$cardSoft', scale: 1.02 }}`. | Minor | Add `hoverStyle` to the KPI tile `Stack`. |
| N-06 | **KPI tile — accent-to-soft map** | B-03 requires soft backgrounds per accent. The mapping is: `⏱️/$primary` → `rgba(79,70,229,0.13)` = `$primarySoft` lighter (current token `$primarySoft` is 0.18); `⭐/$xp` → `rgba(250,204,21,0.13)` = xp-soft; `✓/$success` → `rgba(34,197,94,0.13)` = `$successSoft` lighter; `🔥/$streak` → `rgba(251,146,60,0.13)` = streak-soft. The design tokens define `--lx-primary-soft:rgba(79,70,229,0.18)` which is 0.18, not 0.13. The preview uses 0.13. | Design gap: no `0.13` alpha soft tokens. Use the closest available (`$primarySoft` at 0.18) or apply inline rgba. Since we cannot silently invent tokens, use existing `$primarySoft` / `$successSoft` / `$warningSoft` / and a new `$xpSoft` / `$streakSoft` — see GAP-02. | Minor | Use `$primarySoft` for time tile, introduce `$xpSoft` and `$streakSoft` (see design gaps). For now substitute with inline `rgba(250,204,21,0.15)` and `rgba(251,146,60,0.15)` until tokens are added. |
| N-07 | **Focus areas — icon glyph set** | `SUBJECT_ICON` uses `'🔢'` (Math), `'🔬'` (Science), `'ع'` (Arabic, text char), `'A'` (English, text char) | `web-weak-areas-list.html` uses: Math `➖` (specific to "subtraction" topic), Reading `🔤` (specific to "long vowels"). The icon is topic-specific in the preview, not subject-generic. Since current implementation uses a generic subject icon (correct for a stub), the `'ع'` and `'A'` text chars render as very small Latin/Arabic text inside a square chip — visually inconsistent with the emoji-based row icons. | Minor | Replace `'ع'` with `'📖'` (Arabic reading/language) and `'A'` with `'🔡'` (English) or use consistent icon emoji. Alternatively map to a Lucide icon SVG. The chip needs a glyphic icon, not a raw character. |
| N-08 | **AR capture — sidebar layout** | AR capture: sidebar on right, logo+wordmark on right, nav items right-aligned, "Learnexia" wordmark `dir="ltr"` | `index-ar.html` line 46: `<div class="h-display" style="font-size:20px" dir="ltr">Learnexia</div>`. SKILL.md: brand name "Learnexia" stays Latin+`dir="ltr"`. In `Sidebar.tsx` the `Text` for `t('common.appName')` has no explicit `dir` — if `appName` returns "Learnexia" in AR too (it should, as it's a brand name), the `writingDirection` must be `'ltr'` explicitly. | Minor | Add `writingDirection="ltr"` to the brand name `Text` in `Sidebar.tsx`. SHARED change. |
| N-09 | **Hover on `PDPanel`-equivalent cards** | No `hoverStyle` on any of the 3 main panel cards | Design system: "Hover: Brighten by ~8%, slight scale 1.02" (README.md, Interaction States). Cards in a parent dashboard are read-only, not interactive, so hover is not expected. No action. | — | No action (non-interactive panels). |
| N-10 | **Focus item `accessibilityLabel` uses `row.percent` raw** | `\`${topic} ${subject} ${row.percent}%\`` | In AR locale the percent should render in Eastern-Arabic numerals for the screen reader. Use `formatNumber(row.percent, locale) + '%'` in the accessibility label. | Minor | Pass `locale` into `FocusAreasCard` and format the percent in the a11y label. |
| N-11 | **"Areas to focus on" subtitle copy** | Rendered as `t('parent.overview.focusAreas.subtitle', { name: childName })` → e.g. "Topics where Sami is still building confidence" | Capture EN: "Topics where Sami is still building confidence". AR capture: "مواضيع لا يزال سامي يبني ثقته فيها". Confirm these i18n keys exist and match exactly. | Minor | Confirm i18n keys. No code change needed if correct. |
| N-12 | **KPI card gap between label row and value** | `gap="$2"` (8px) between all three rows inside the tile | `web-kpi-row.html` line 3: `gap:8px` on the column flex — correct. | — | No action. |

---

## Accent-to-Soft Token Map (for B-03 / N-06)

For the KPI icon chips, use these tinted backgrounds:

| KPI | Icon | Accent | Icon Chip Bg token | Chip Bg fallback |
|-----|------|--------|-------------------|-----------------|
| Time learning | ⏱️ | `$primary` `#4F46E5` | `$primarySoft` | `rgba(79,70,229,0.13)` |
| XP earned | ⭐ | `$xp` `#FACC15` | `$xpSoft` (new — GAP-02) | `rgba(250,204,21,0.13)` |
| Lessons done | ✅ | `$success` `#22C55E` | `$successSoft` | `rgba(34,197,94,0.13)` |
| Day streak | 🔥 | `$streak` `#FB923C` | `$streakSoft` (new — GAP-02) | `rgba(251,146,60,0.13)` |

---

## Copy Corrections — EN + AR

| Location | i18n Key | EN value | AR value | Note |
|----------|----------|----------|----------|------|
| Page title | `parent.overview.title` | "{name}'s progress" | "تقدّم {name}" | AR name stays in Arabic script; possessive constructed differently |
| Date range | `parent.overview.dateRange` | "Mon, Nov 18 → Sun, Nov 24" | "الاثنين، ١٨ نوفمبر ← الأحد، ٢٤ نوفمبر" | Arrow flips to `←` in RTL |
| Period select | `parent.overview.periodThisWeek` | "This week" | "هذا الأسبوع" | |
| Send Report btn | `parent.overview.sendReport` | "Send Report" | "إرسال التقرير" | |
| KPI: Time label | `parent.overview.kpi.timeLearning` | "TIME LEARNING" | "وقت التعلم" | AR: no uppercase, no letter-spacing |
| KPI: XP label | `parent.overview.kpi.xpEarned` | "XP EARNED" | "النقاط المكتسبة" | |
| KPI: Lessons label | `parent.overview.kpi.lessonsDone` | "LESSONS DONE" | "دروس منجزة" | |
| KPI: Streak label | `parent.overview.kpi.streak` | "DAY STREAK" | "سلسلة الأيام" | |
| KPI: Time delta | `parent.overview.kpi.timeDelta` | "+{value} vs last week" | "+{value} عن الأسبوع الماضي" | value = `٤٢ د` in AR |
| KPI: XP delta | `parent.overview.kpi.xpDelta` | "+{value}% vs last week" | "+{value}٪ عن الأسبوع الماضي" | `٪` not `%` |
| KPI: Lessons delta | `parent.overview.kpi.lessonsDelta` | "+{value} vs last week" | "+{value} عن الأسبوع الماضي" | |
| KPI: Streak delta | `parent.overview.kpi.streakDelta` | "+{value} vs last week" | "+{value} عن الأسبوع الماضي" | |
| Mastery title | `parent.overview.subjectMastery.title` | "Subject mastery" | "إتقان المواد" | |
| Mastery subtitle | `parent.overview.subjectMastery.subtitle` | "Last 7 days" | "آخر ٧ أيام" | Eastern-Arabic numeral |
| Math | `parent.overview.subjects.math` | "Math" | "الرياضيات" | |
| Science | `parent.overview.subjects.science` | "Science" | "العلوم" | |
| Arabic | `parent.overview.subjects.arabic` | "Arabic" | "العربية" | |
| English | `parent.overview.subjects.english` | "English" | "الإنجليزية" | |
| Activity title | `parent.overview.dailyActivity.title` | "Daily activity" | "النشاط اليومي" | |
| Activity subtitle | `parent.overview.dailyActivity.subtitle` | "XP earned per day" | "النقاط المكتسبة لكل يوم" | |
| Export CSV | `parent.overview.dailyActivity.exportCsv` | "Export CSV" | "تصدير CSV" | "CSV" stays Latin + `dir="ltr"` |
| Activity placeholder | `parent.overview.dailyActivity.placeholder` | "Chart coming soon" (or similar) | "الرسم البياني قريباً" | |
| Focus title | `parent.overview.focusAreas.title` | "Areas to focus on" | "مجالات للتركيز عليها" | |
| Focus subtitle | `parent.overview.focusAreas.subtitle` | "Topics where {name} is still building confidence" | "مواضيع لا يزال {name} يبني ثقته فيها" | |
| See all | `parent.overview.focusAreas.seeAll` | "See all" | "رؤية الكل" | |
| Sidebar: My Children | `parent.nav.myChildren` | "My Children" | "أطفالي" | |
| Sidebar: Overview | `parent.nav.overview` | "Overview" | "نظرة عامة" | |
| Sidebar: Reports | `parent.nav.reports` | "Reports" | "التقارير" | |
| Sidebar: Activity | `parent.nav.activity` | "Activity" | "النشاط" | |
| Sidebar: Subjects | `parent.nav.subjects` | "Subjects" | "المواد" | |
| Sidebar: Settings | `parent.nav.settings` | "Settings" | "الإعدادات" | |
| Sidebar XP eyebrow | `parent.nav.xpWidget.eyebrow` | "THIS WEEK" | "هذا الأسبوع" | AR: no uppercase/tracking |
| Sidebar XP value | `parent.nav.xpWidget.value` | "+{xp} XP" | "+{xp} نقطة" | xp = Eastern-Arabic numerals in AR |
| Sidebar XP delta | `parent.nav.xpWidget.delta` | "Up {percent}% from last week" | "متقدم {percent}٪ عن الأسبوع الماضي" | `٪` in AR |

---

## RTL Conventions Checklist (SKILL.md)

| Rule | Status | Fix needed |
|------|--------|------------|
| `dir="rtl"` on wrapper | Handled by `useLocale` / `writingDirection` prop | Confirm root `Stack` has `style={{ direction: 'rtl' }}` or equivalent in web |
| Headings: Cairo, body: Tajawal | `fontFamily="$heading"` must resolve to Cairo in AR. Confirm `theme-ar.ts` maps `$heading` → Cairo | Verify font token mapping |
| Eastern-Arabic numerals for inline text | `formatNumber` uses `'ar-EG'` — correct. `formatDuration` needs `س`/`د` units (M-25) | Fix M-25 |
| Latin + `dir="ltr"` for: XP numbers / email / brand / KPI counters | KPI values are numbers rendered via `formatNumber` in ar-EG — renders Eastern-Arabic. KPI values in the design (480 XP) should stay Eastern-Arabic (`٤٨٠`). XP label "XP" should stay Latin? `ar-web-kpi.html` shows "النقاط المكتسبة" (Arabic label) and `٤٨٠` (Eastern-Arabic numeral) — no Latin "XP" in AR. Current i18n key approach is correct. | Confirm no Latin "XP" suffix in AR |
| `%` → `٪` in AR | XP delta, sidebar delta, mastery percent | M-26, N-04 — fix i18n keys |
| Arrow `→` → `←` in RTL | Date range arrow must flip. In EN: `Mon, Nov 18 → Sun, Nov 24`. In AR: `الاثنين، ١٨ نوفمبر ← الأحد، ٢٤ نوفمبر` | Confirm AR i18n key uses `←` |
| Progress bars stay LTR | MasteryBar and confidence bar | M-21, M-22 |
| Avatars / icons not mirrored | `scaleX={isRtl ? -1 : 1}` on `›` chevron in Sidebar — correct. Do not mirror owl logo or subject icons. | Correct |
| Bar charts stay LTR | Chart container in AR: `direction:ltr`. DailyActivityCard placeholder has no chart yet; when chart is built in P5, wrap in `style={{ direction: 'ltr' }}` | Flag for P5 |
| Sidebar on RIGHT in AR | Currently `flexDirection="row"` always LTR | M-23 — fix in `overview.tsx` |
| Brand name "Learnexia" stays Latin | Sidebar brand name `Text` needs `writingDirection="ltr"` | N-08 |
| KPI numbers: Latin numerals for technical strings like email, XP raw values | In AR context: XP value is Eastern-Arabic (`٤٨٠`) per capture, not Latin. This is correct. | No action |

---

## New Components / Token Gaps

| ID | Gap | Description | Recommended Resolution |
|----|-----|-------------|----------------------|
| GAP-01 | Missing `14px` radius token | Inner row cards in Focus Areas use 14px radius — between `$sm` (8px) and `$card` (20px). No token exists. | Add `--lx-radius-inner: 14px` to `colors_and_type.css` and map to `$cardInner` in the Tamagui theme. Until added, use literal `borderRadius={14}`. |
| GAP-02 | Missing `$xpSoft` and `$streakSoft` tokens | The KPI icon chips need `rgba(250,204,21,0.13)` (XP) and `rgba(251,146,60,0.13)` (streak) as tinted backgrounds. The token system has `$xp` and `$streak` color tokens but no `-soft` variants at 0.13 alpha. | Add `--lx-xp-soft: rgba(250,204,21,0.13)` and `--lx-streak-soft: rgba(251,146,60,0.13)` to `colors_and_type.css`. Temporarily use inline `rgba(...)` values. |
| GAP-03 | `MasteryBar` accent not applied to percent label | Already documented in the `MasteryBar` source as "Design Gap GAP-03". The fix is B-10 above. | Fix in `MasteryBar` component per B-10. |
| GAP-04 | `KPIStatCard` (packages/ui) does not match Overview tile layout | `KPIStatCard` is a mini tile for child cards. The Overview KPI tile (`PDStatCard` pattern) has a richer layout: icon chip top-right, large value, delta row. These are two different components. | Either add a `'stat'` variant to `KPIStatCard` (stop + ask lead first per CLAUDE.md patterns rule) OR keep the hand-assembled tile in `OverviewWeb` and align it pixel-for-pixel using B-01 through B-05 fixes. |
| GAP-05 | No Tamagui `$cardInner` token for 14px radius | Same as GAP-01 from the Tamagui side. | Add to the Tamagui theme config in `packages/ui/src/theme.ts` once the CSS token is added. |
| GAP-06 | `Select` component radius | `packages/ui/Select` uses `borderRadius="$button"` (16px) but selects/inputs must use `$sm` (8px) per design-system rule 4. | Fix in `packages/ui/Select` component — change default `borderRadius` to `"$sm"`. This is a SHARED change that affects all `Select` usages. |

---

## Implementation Handoff Summary

### `apps/student-app/app/(parent)/overview.tsx`
- M-23: Add `isRtl` from `useLocale`; change outer `Stack flexDirection` to `isRtl ? 'row-reverse' : 'row'` so sidebar goes right in AR.

### `apps/student-app/app/(parent)/_components/OverviewWeb.tsx`
- B-05: Page title `fontSize={26}` → `22`
- B-06: Select wrapper — apply `borderRadius={8}` override after Select renders (or fix in `packages/ui/Select`)
- B-19: Date range `fontSize={14}` → `12`
- M-09: Remove `maxWidth={1200}`, let content fill available width
- M-10: KPI row `gap="$4"` → `gap={14}`
- KPI tile loop (B-01–B-03, N-05, N-06): change `fontSize={32}` → `28`, `letterSpacing={0.6}` → `0.88`, add icon chip Stack 32×32 `borderRadius={8}` with soft bg per accent map, add `hoverStyle={{ backgroundColor: '$cardSoft', scale: 1.02 }}`
- M-25: Fix `formatDuration` AR unit labels (`س`/`د`)
- M-26: Confirm AR i18n XP delta key uses `٪`

### `apps/student-app/app/(parent)/_components/SubjectMasteryCard.tsx`
- B-07: title `fontSize={18}` → `16`
- B-08: subtitle `fontSize={13}` → `12`
- M-14: `padding="$6"` → `padding={22}`
- M-15: rows gap `"$4"` → `{14}`

### `apps/student-app/app/(parent)/_components/FocusAreasCard.tsx`
- B-11: row `borderRadius="$sm"` → `{14}`
- B-12: row padding `"$4"` → `paddingVertical={12} paddingHorizontal={14}`
- B-13: bar track bg `"$cardSoft"` → `"$card"`; add `borderWidth={1} borderColor="rgba(255,255,255,0.04)"`
- B-14: icon chip bg → severity-based soft color (`$dangerSoft` / `$warningSoft`); icon color → `$danger` / `$warning`
- B-15: topic label `fontSize={15}` → `13`
- B-16: remove `borderRadius={9999}` from fill Stack
- B-17: percent `width={48}` → `{44}`
- M-18: outer `padding="$6"` → `{22}`
- M-19: rows `gap="$3"` → `{10}`
- M-20: "See all" bare Stack → pill ghost button
- M-22: confidence bar container force `flexDirection="row"` (LTR stays)
- N-10: pass locale to format percent in a11y label

### `apps/student-app/app/(parent)/_components/DailyActivityCard.tsx`
- B-18: Export CSV `variant="secondary"` → ghost pill (custom pill Stack or `Button variant="ghost"` + `borderRadius={9999}`)
- M-12: `padding="$6"` → `{22}`
- M-13: Remove `borderStyle="dashed"`; `height={260}` → `{200}`

### `apps/student-app/app/(parent)/_components/Sidebar.tsx` (SHARED)
- M-01: Remove `borderStartWidth` / `borderStartColor` from active nav item
- M-02: Active label `'$fg1'` → `'$primaryLight'`; inactive `'$fg2'` → `'$fg3'`
- M-03: Nav item `fontSize={15}` → `14`
- M-04: Nav padding `paddingHorizontal="$3"` → `{12}` `paddingVertical={10}`; `minHeight={48}` → `{40}`
- M-05: Child-selector `borderRadius="$card"` → `{16}`
- M-06: Remove `borderColor="$borderStrong"` from child-selector; use `"$border"` or none
- M-07: Logo `width={32} height={32}` → `{36}`
- M-08: Brand name `fontSize={20}` → `{18}`
- N-01: XP eyebrow `letterSpacing={1}` → `{0.8}`
- N-02: XP value `fontSize={24}` → `{20}`
- N-03: XP widget `padding="$4"` → `{14}`
- N-08: Brand name Text add `writingDirection="ltr"`

### `packages/ui/src/components/MasteryBar/index.tsx` (SHARED)
- B-09: Label `color="$fg3"` → `"$fg1"`; `fontSize={11}` → `13`
- B-10: Percent Text `color="$fg1"` → `color={accent ?? '$fg1'}`
- M-17: `TRACK_HEIGHT = 8` → `10`
- M-21: Bar track `Stack` and fill `Stack` force `flexDirection="row"` (remove rowDir dependency for the bar itself)

### `packages/ui/src/components/Select/index.tsx` (SHARED — GAP-06)
- Fix default `borderRadius` from `"$button"` (16px) to `"$sm"` (8px) — affects all Select usages.

### `design-system/colors_and_type.css` (GAP-01, GAP-02)
- Add `--lx-radius-inner: 14px`
- Add `--lx-xp-soft: rgba(250, 204, 21, 0.13)`
- Add `--lx-streak-soft: rgba(251, 146, 60, 0.13)`

### Tamagui theme (wherever `$tokens` are defined — `packages/design-system/src/tokens.ts` or equivalent)
- Add `cardInner: 14` to radii
- Add `xpSoft` and `streakSoft` color tokens

---

## Intentional Deviations from Capture (do NOT fix)

1. **Subject names:** Capture shows "Reading" and "Art" (EN) / "القراءة" and "العربية" (AR). Product uses Math / Science / Arabic / English. The implementation is correct; the capture is stale. Do not revert.
2. **Lessons done icon:** Capture shows `✓` (checkmark), implementation uses `✅` (emoji). The emoji is semantically equivalent and consistent with the design system's emoji-semantic-only rule. No change needed; either is acceptable, but `✓` (U+2713 plain) is lighter-weight.
3. **Daily activity chart:** Phase-5 placeholder acknowledged. The empty-state container is an intentional deviation from the capture — align its container per M-12/M-13 but do not build bars.
4. **"Recommendations from Lexi" section:** The capture does not show this section (it is cut off below the fold). The `OverviewWeb` implementation does not render this section either — correct. Do not add it unless a story explicitly requires it.

---

## Severity Count

| Severity | Count |
|----------|-------|
| Blocker | 19 |
| Major | 27 |
| Minor | 12 |

**Total deltas: 58**

---

## SHARED Component Changes Summary

The following files are shared with My Children, Reports, and Settings pages — changes propagate automatically:

1. **`Sidebar.tsx`** — M-01 through M-08, N-01 through N-03, N-08 (10 fixes). All sidebar pages (My Children, Overview, Reports, Settings) will pick up these fixes.
2. **`packages/ui/MasteryBar`** — B-09, B-10, M-17, M-21 (4 fixes). Used by Overview (SubjectMasteryCard), Reports (Skills mastery panel), and My Children (mastery bar in ChildCard). All will benefit.
3. **`packages/ui/Select`** — GAP-06 (1 fix). All pages using Select (Period selector in header, settings dropdowns) will become correct.
4. **`overview.tsx` outer layout (M-23 RTL sidebar direction)** — also needs to be applied to `children.tsx`, `reports.tsx`, `settings.tsx` wrappers if they share the same `Stack flexDirection="row"` pattern.
