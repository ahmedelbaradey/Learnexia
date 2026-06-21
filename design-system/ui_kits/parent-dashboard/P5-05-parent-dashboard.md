# Design Spec — P5-05 Parent Dashboard Charts + Real Analytics Wiring

> Scope per plan: Overview KPI wiring + daily-activity BarChart (P5-05-FE-1/2/4) · Reports 20-day trend BarChart + time-of-day BarChart (P5-05-FE-3/4) · All states per panel. P5-05-FE-5 (Send Report / period select) and P5-06 (grade-transition) are DEFERRED — do not design those here.

---

## 1. Screens in scope

| Screen (route) | LTR capture | RTL capture | Composing preview cards |
|---|---|---|---|
| Overview `(parent)/overview` | `screenshots/web/05-dashboard.png` | `screenshots/web-ar/05-dashboard.png` | `web-kpi-row.html`, `ar-web-kpi.html`, `web-activity-chart.html`, `web-skills-mastery.html`, `ar-web-weak-areas.html` |
| Reports `(parent)/reports` | `screenshots/web/06-reports.png` | `screenshots/web-ar/06-reports.png` | `web-kpi-row.html`, `ar-web-kpi.html`, `web-activity-chart.html` (20-day variant), `web-time-of-day.html` |

**Capture caveat:** both captures show "Reading / Art" in mastery rows and a "Lessons mastered / Avg. Accuracy" KPI set in Reports. Apply product overrides:
- Subjects = Math / Science / Arabic / English only. No Reading, no Art, no Social Studies.
- Overview KPI 2 = XP Earned (absolute delta, not %). Reports KPI 2 = XP Earned (honest "—" placeholder per G-2 gap; this card does NOT gain real data in P5-05 — see section 3.1).
- Reports KPI 3 = Lessons Done. Reports KPI 4 = Avg. Accuracy. (These two do get real data in P5-05.)
- The capture's "Today highlighted in indigo" subtitle on Reports 20-day chart: keep that label exactly.
- AR capture shows day-labels in Latin (`Mon`, `Tue` etc.) on the daily-activity chart — this is intentional per Brand Law #RTL rule 6: bar charts stay LTR, day abbreviations remain Latin.

---

## 2. BarChart Primitive — `packages/ui/src/components/BarChart/index.tsx`

This is a **new component** with no existing file; the frontend must create it. It is app-local to `packages/ui` — not a charting library, not SVG. Built entirely from Tamagui `Stack` and `Text` primitives. Three shape variants cover all usages in P5-05.

### 2.1 Component API

```
interface BarChartDataPoint {
  /** Localized label shown below the bar (already formatted for the locale). */
  label: string;
  /** Raw numeric value for proportional height calculation. */
  value: number;
  /** When true, renders with the "active/today" gradient + indigo glow. */
  isActive?: boolean;
}

interface BarChartProps {
  data: BarChartDataPoint[];
  /**
   * Maximum value for proportional scaling.
   * When omitted, max = Math.max(...data.map(d => d.value)).
   */
  maxValue?: number;
  /**
   * Pixel height of the tallest bar (the chart "well" height).
   * Shape A: 180. Shape B: 200. Shape C: 160.
   */
  chartHeight?: number;
  /**
   * Show a numeric value label above each bar.
   * Default true.
   */
  showValueLabels?: boolean;
  /**
   * "ltr" | "rtl". Controls label text direction and value-label alignment.
   * The bar layout itself is always LTR (Brand Law RTL rule 6).
   */
  direction?: 'ltr' | 'rtl';
  /**
   * Base (inactive) bar fill. One of the three shapes:
   * - "muted"  — `#334155` → `#1E293B` top-to-bottom gradient (default)
   * - "purple" — solid `$purple` (#A855F7) for secondary-active bars
   * Token: "$cardSoft" → "$card" for muted; "$purple" for purple variant.
   */
  barVariant?: 'muted' | 'purple';
  /**
   * Locale string passed to Intl.NumberFormat for value label formatting.
   * "ar" → ar-EG Eastern-Arabic numerals. "en" → en-US.
   * The value LABEL uses the locale; the day/time AXIS label is pre-formatted
   * by the caller and passed verbatim in BarChartDataPoint.label.
   */
  locale?: string;
  /** testID applied to the outermost Stack for E2E queries. */
  testID?: string;
  /**
   * Accessible summary of the chart read by screen readers instead of
   * iterating individual bars. Required — callers must provide it.
   * Example (EN): "Daily activity chart. Sunday: 110 XP, Saturday: 70 XP, ..."
   * Example (AR): "مخطط النشاط اليومي. الأحد: ١١٠ نقطة، السبت: ٧٠ نقطة، ..."
   */
  accessibilityLabel: string;
}
```

### 2.2 Rendering rules (all shapes)

The outermost wrapper is `accessible accessibilityRole="image"` with the caller-provided `accessibilityLabel`. All inner `Stack` and `Text` nodes carry `accessibilityElementsHidden`. This collapses the chart into one a11y node — parents read a summary, not 7 individual bar heights.

Bar proportion:

```
barHeightPx = Math.max(4, Math.round((value / resolvedMax) * chartHeight))
```

A zero value renders as a 4px stub bar (visible but minimal) — never 0px height (would make it disappear).

Bar gap is always `12px` between columns. Each column is `flex: 1` (equal widths, fills available space). The container is `flexDirection: "row"` with `alignItems: "flex-end"` and `height: chartHeight + 28` (28px = value-label row above bars).

The bar layout container always carries an explicit `direction: ltr` style on web — this overrides any inherited RTL so bars always grow left-to-right per Brand Law RTL rule 6.

Value labels (`showValueLabels=true`) are rendered absolutely above each bar:
- Position: `position: absolute; bottom: 100%; left: 50%; transform: translateX(-50%); marginBottom: 4`
- Typography: `fontSize: 11, fontWeight: 800, fontFamily: $heading, fontVariantNumeric: tabular-nums`
- Color: inactive bars → `$fg3` (`#64748B`); active bar → `$primaryLight` (`#A5B4FC`)
- Locale-formatted via `Intl.NumberFormat`

Axis labels (below each bar):
- Typography: `fontSize: 11, fontWeight: 700, fontFamily: $heading, textTransform: uppercase, letterSpacing: 0.06em` (= `0.06 * 11 = 0.66px`)
- Color: inactive → `$fg3` (`#94A3B8`); active → `$fg1` (`#F8FAFC`)

### 2.3 Shape A — Daily Activity (Overview, Mon–Sun)

Source: `preview/web-activity-chart.html` (literal values used below).

**Data source:** `GET /api/v1/analytics/children/{childId}/weekly-activity` (P5-03-BE). Response: `{ days: [{ date, xpEarned }] }` — 7 entries Mon→Sun. The active day is determined by comparing each date to today's date client-side.

Resolved parameters:
- `chartHeight`: 180
- `barVariant`: "muted" for inactive bars
- `showValueLabels`: true
- Active bar: `background: linear-gradient(180deg, #A855F7, #4F46E5)` (Level-Up gradient, `--lx-grad-levelup`), `box-shadow: 0 6px 18px rgba(99,102,241,0.4)` (indigo glow, `--lx-primary-glow` base)
- Inactive bar: `background: linear-gradient(180deg, #334155, #1E293B)` (top-to-bottom `$cardSoft` → `$card`)
- Bar border-radius: `10px 10px 4px 4px` (rounded top, near-flat bottom)
- Gap between bars: `12px`

**Axis labels (day abbreviations):** always Latin (`Mon Tue Wed Thu Fri Sat Sun`), always in `direction: ltr`, regardless of locale. Per Brand Law RTL rule 6 and confirmed by the AR capture which shows Latin day labels.

**Value labels:** locale-formatted integers (AR → Eastern-Arabic numerals; EN → Latin). Shown above each bar.

**Card wrapper** (the `DailyActivityCard` shell already built — the chart replaces the placeholder `Stack`):
- The existing `CHART_AREA_HEIGHT = 200` constant becomes the chart container's outer height (`chartHeight: 180` + label clearance = ~200px total). No change to the outer card dimensions.
- Card: `borderRadius: 20` (`--lx-radius-card`), `backgroundColor: $card` (`#1E293B`), `borderWidth: 1`, `borderColor: $borderSubtle` (= `rgba(255,255,255,0.06)`), `padding: 22`, `gap: $5` (20px)

**Export CSV button** (already built — no change): ghost pill, `borderRadius: 9999`, `borderColor: $borderStrong` (`rgba(255,255,255,0.12)`), `color: $primaryLight` (`#A5B4FC`), `fontSize: 12`, `fontWeight: 700`, `padding: 6px 12px`.

### 2.4 Shape B — 20-Day XP Trend (Reports)

Source: `screenshots/web/06-reports.png` + `screenshots/web-ar/06-reports.png`. No dedicated preview card exists — capture is the authority.

**Data source:** `GET /api/v1/analytics/children/{childId}/twenty-day-activity` (P5-02-BE or P5-03-BE). Response: `{ days: [{ date, xpEarned }] }` — 20 entries. The active day = today (most recent entry if fewer than 20 days of history).

Resolved parameters:
- `chartHeight`: 200
- `barVariant`: "muted" for inactive bars (same `#334155 → #1E293B` gradient as Shape A)
- `showValueLabels`: false (the 20-day chart shows NO value labels above bars — too dense; confirmed by both captures)
- Active bar: Level-Up gradient `#A855F7 → #4F46E5` (180deg, same as Shape A) with indigo glow `0 6px 18px rgba(99,102,241,0.4)`
- Inactive bar: `background: linear-gradient(180deg, #334155, #1E293B)`
- Bar border-radius: `10px 10px 4px 4px`
- Gap between bars: `6px` (narrower gap, 20 bars in the same horizontal space)

**Axis labels (day numbers):** 1 through 20 (Latin digits, `dir: ltr`). Locale-exception: these are technical counters (day-of-month indices), not in-sentence text — they stay Latin even in AR (same rule as XP counters). `fontSize: 11, fontWeight: 700, color: $fg3, textTransform: none`.

**Panel wrapper** (replaces the `ChartPlaceholderPanel` with `testID="reports-chart-slot-xp"`):
- Title: `"Last 20 days · XP earned"` (EN) / `"آخر ٢٠ يوماً · النقاط"` (AR) — i18n key `parent.reports.charts.xpTitle`
- Subtitle: `"Today highlighted in indigo"` (EN) / `"اليوم بالنيلي"` (AR) — i18n key `parent.reports.charts.xpSubtitle` (NEW key — the existing placeholder used `parent.reports.charts.comingSoon`; the frontend must add this key)
- Panel: `borderRadius: 24` (`$modal`, `--lx-radius-modal`), `backgroundColor: $card`, `borderWidth: 1`, `borderColor: $borderSubtle`, `padding: 22`, `minHeight: 200`, `width: "100%"`
- Header row (title + Export CSV): same pattern as DailyActivityCard — Export CSV ghost pill, top-right

**Export CSV** on the 20-day chart: same ghost pill as Shape A. The capture shows "Export CSV" top-right of the 20-day chart panel; wire it to the same TODO stub (P5-05-FE-2 covers the actual export implementation).

### 2.5 Shape C — Time of Day (Reports)

Source: `preview/web-time-of-day.html` (literal values below).

**Data source:** `GET /api/v1/analytics/children/{childId}/time-of-day` (P5-02-BE). Response: `{ buckets: [{ hour: number, minutes: number }] }` — 8 buckets (6a, 8a, 10a, 12p, 2p, 4p, 6p, 8p) or similar. Peak bucket = bucket with highest `minutes`.

Resolved parameters:
- `chartHeight`: 160 (bars rendered bottom-up within a `height: 160px` well + 18px bottom padding for axis labels in the card)
- `barVariant`: "muted" for default bars; peak bar = Reward gradient `#FB923C → #EF4444` (top-to-bottom, 180deg); secondary-high bars = solid `$purple` (`#A855F7`)
- `showValueLabels`: true (minute-count labels above each bar; e.g. "95m", "60m")
- Bar border-radius: `6px 6px 3px 3px` (slightly tighter than Shape A — per card source literal)
- Gap between bars: `6px`

Value label format for Shape C: `{n}m` (EN) / `{n}د` (AR, Eastern-Arabic numeral + Arabic minute abbreviation). Locale-formatted via `Intl.NumberFormat` for the digit; suffix is a constant per locale.

**Bar fill rules (Shape C, per `web-time-of-day.html`):**

| Bar state | Fill |
|---|---|
| Default/inactive | `#334155` (flat, no gradient) |
| Secondary elevated | `#A855F7` (solid purple) |
| Peak (highest value) | `linear-gradient(180deg, #FB923C, #EF4444)` · `box-shadow: 0 4px 14px rgba(251,146,60,0.35)` |

Peak bar label color: `$fg1` (`#F8FAFC`). Non-peak bar label color: `$fg3` (`#94A3B8`).

**Axis labels (hour strings):** `6a`, `8a`, `10a`, `12p`, `2p`, `4p`, `6p`, `8p` — always Latin, `dir: ltr`. The backend may return 24-hour integers; the chart component converts them: `h < 12 ? ${h === 0 ? 12 : h}a : h === 12 ? '12p' : ${h - 12}p`. This is a display formatter, not a locale translation (hour labels are technical strings, keep Latin per Brand Law exception).

**"Peak focus" insight tip** (below the chart, within the panel):
- Only rendered when at least one bar has data.
- Container: `backgroundColor: rgba(251,146,60,0.08)`, `borderWidth: 1`, `borderColor: rgba(251,146,60,0.2)`, `borderRadius: 12`, `padding: 10px 12px`, `marginTop: 8`, `flexDirection: rowDir`
- Content: `💡` + text
  - EN: `"Peak focus is {peakStart}–{peakEnd} — great time for new material"` — i18n key `parent.reports.charts.peakInsight` (NEW i18n key)
  - AR: `"أفضل تركيز في {peakStart}–{peakEnd} — وقت مثالي للمادة الجديدة"` (NEW i18n key)
- Typography: `fontSize: 13, fontWeight: 700, color: #FB923C ($streak)`, `fontFamily: $heading`
- The `{peakStart}` / `{peakEnd}` strings are Latin technical strings (e.g. "4p", "5p") even in AR.

**Panel wrapper** (replaces `ChartPlaceholderPanel` with `testID="reports-chart-slot-tod"`):
- Title: `"Time of day"` (EN) / `"وقت اليوم"` (AR) — i18n key `parent.reports.charts.todTitle`
- Subtitle: `"When {name} learns best"` (EN) / `"متى يتعلم {name} بأفضل ما يمكن"` (AR) — i18n key `parent.reports.charts.todSubtitle` (NEW)
- Panel: `borderRadius: 20` (`$card`), `backgroundColor: $card`, `borderWidth: 1`, `borderColor: $borderSubtle`, `padding: 20`, `width: "100%"`

---

## 3. Panel-by-Panel Spec

### 3.1 Overview KPI Row (`testID="overview-kpi-region"`)

**Capture:** `web/05-dashboard.png` · `web-ar/05-dashboard.png`
**Preview cards:** `web-kpi-row.html`, `ar-web-kpi.html`

**Data source (P5-05-FE-4):** Replace `getOverviewKpiStub` with a real query hook. The endpoint is `GET /api/v1/analytics/children/{childId}/weekly-kpis` (P5-01-BE). Response shape per Brief:

```
WeeklyKpisDto {
  timeLearningMinutes: number       // total this week
  timeLearningDeltaMinutes: number  // absolute difference vs last week
  xpEarned: number                  // ABSOLUTE total this week
  xpDelta: number                   // ABSOLUTE difference vs last week (NOT a percent)
  lessonsDone: number
  lessonsDelta: number
  streakDays: number
  streakDelta: number
}
```

**CRITICAL delta display rule:** The current stub code uses `xpDeltaPercent` and renders e.g. "+28% vs last week". This must change. `WeeklyKpisDto.xpDelta` is an ABSOLUTE number of XP points, not a percentage. The copy must read "+120 XP this week" not "+28%". See copy table below.

**KPI tile specs (literal from `web-kpi-row.html`):**

| Tile | Icon | chipBg | Value format | Delta copy (EN) | Delta copy (AR) | i18n key |
|---|---|---|---|---|---|---|
| Time Learning | ⏱️ | `rgba(79,70,229,0.13)` | `{h}h {m}m` / `{h}س {m}د` | `+{n}m vs last week` | `+{n}د عن الأسبوع الماضي` | `parent.overview.kpi.timeDelta` |
| XP Earned | ⭐ | `rgba(250,204,21,0.13)` | locale-number | `+{n} XP this week` | `+{n} نقطة هذا الأسبوع` | `parent.overview.kpi.xpDelta` |
| Lessons Done | ✅ | `rgba(34,197,94,0.13)` | locale-number | `+{n} vs last week` | `+{n} عن الأسبوع الماضي` | `parent.overview.kpi.lessonsDelta` |
| Day Streak | 🔥 | `rgba(251,146,60,0.13)` | locale-number | `+{n} vs last week` | `+{n} عن الأسبوع الماضي` | `parent.overview.kpi.streakDelta` |

The EN copy for XP must say "XP" (Latin technical string, never localized). The Arabic copy uses Eastern-Arabic numerals for the delta value and "نقطة" (points), never the Latin "XP". AR locale uses `Intl.NumberFormat("ar-EG")`.

**Token-exact tile anatomy (from `web-kpi-row.html`):**

```
Card shell:
  background:    #1E293B  ($card, --lx-card)
  border:        1px solid rgba(255,255,255,0.06)  ($borderSubtle)
  border-radius: 20px  ($card, --lx-radius-card)
  padding:       18px  (= $5 = 20px approx; card source uses 18px literally)
  gap:           8px   ($2)

Header row:
  flexDirection: rowDir (row in LTR, row-reverse in RTL)
  alignItems:    center
  justifyContent: space-between

Label:
  font-size:       11px
  font-weight:     700
  color:           #94A3B8  ($fg3)
  text-transform:  uppercase
  letter-spacing:  0.08em  (= 0.88px at 11px)
  font-family:     $heading (Poppins / Cairo)

Icon chip:
  width: 32px, height: 32px
  border-radius: 10px
  background: (per tile above)
  display: flex, align-items/justify-content: center
  font-size: 15px emoji

Value:
  font-size:   28px
  font-weight: 800
  color:       #F8FAFC  ($fg1)
  font-variant-numeric: tabular-nums
  line-height: 1  (=28px)
  font-family: $heading

Delta line:
  font-size:   12px
  font-weight: 700
  color:       #22C55E  ($success) for positive delta
              $danger   for negative delta
              $fg3      when no previous week data
  font-family: $heading
```

**Arabic KPI differences (from `ar-web-kpi.html`):**
- `dir="rtl"` on each card
- Label font: Cairo (`font-family: 'Cairo'`), same 11px/700 but NO `text-transform: uppercase` (uppercase is a Latin convention; Cairo labels render normally)
- Value font: Cairo, 24px/800 for time format (`٣ س ٢٤ د`), 28px/800 for numeric values
- Delta: Eastern-Arabic digits, Arabic suffix phrases (table above)

**Grid:** 4-column `display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px` on desktop (≥1025). 2-column on tablet+mobile: `repeat(2, 1fr)`. `direction: rtl` on the grid itself in AR locale (so columns order right-to-left). This is already correctly handled by the existing `kpiGridStyle` in `OverviewWeb`/`ReportsWeb` — the frontend only needs to fix the delta copy, not the grid layout.

**States for this panel:**

| State | Render |
|---|---|
| Loading | 4 skeleton cards: `height: 110, borderRadius: $card, backgroundColor: $cardSoft, opacity: 0.6` |
| Error | ErrorStrip (existing component in ReportsWeb) + Retry |
| Empty (no children) | Handled at OverviewWeb root — shows "Add child" CTA, this panel never renders |
| First week (no prior data) | Show tiles with current values; suppress delta line (omit the `subText` when `xpDelta === 0 && lessonsDelta === 0 && previous window is empty`). Show empty value `—` for XP tile only if endpoint returns null (G-2 gap is resolved in P5-05 for Overview) |
| Populated | Normal render per anatomy above |

**Child-switch behavior:** When `activeChildId` changes in `useActiveChildStore`, the query key must include `childId`. All 4 KPI tiles re-fetch and re-render for the new child. Show loading skeletons during the fetch transition.

### 3.2 Daily Activity Chart Panel (`testID="daily-activity-card"`)

**Capture:** `web/05-dashboard.png` (lower left) · `web-ar/05-dashboard.png`
**Preview card:** `web-activity-chart.html`
**File to modify:** `apps/student-app/app/(parent)/_components/DailyActivityCard.tsx`

**Data source (P5-05-FE-2):** `GET /api/v1/analytics/children/{childId}/weekly-activity`. Returns 7 `{ date: string (ISO), xpEarned: number }` items. The "active" bar = the item whose date matches today's calendar date (client local time). If today is not in the returned range, no bar is active.

**Replace the placeholder `Stack` (lines 100–116 of `DailyActivityCard.tsx`)** with:

```
<BarChart
  data={weeklyData.map(d => ({
    label: formatDayLabel(d.date, locale),   // "Mon", "Tue" etc. always Latin
    value: d.xpEarned,
    isActive: isSameCalendarDay(d.date, today),
  }))}
  chartHeight={180}
  showValueLabels={true}
  direction={direction}
  locale={locale}
  testID="daily-activity-chart"
  accessibilityLabel={buildDailyChartA11yLabel(weeklyData, locale, t)}
/>
```

`formatDayLabel(date, locale)`: returns the 3-letter English abbreviation (`Mon`, `Tue`, etc.) regardless of locale — always Latin per Brand Law. Use `new Intl.DateTimeFormat('en-US', { weekday: 'short' }).format(new Date(date))`.

`buildDailyChartA11yLabel`: builds an ordered summary string for screen readers:
- EN: `"Daily activity chart. {day}: {xp} XP, ..."` (sort by date, highest-XP day first in the narrative, or chronological — designer preference: chronological is fine)
- AR: `"مخطط النشاط اليومي. {day}: {xp} نقطة، ..."` (Eastern-Arabic numerals for XP values; Latin day abbreviations since bar context)

**States for this panel:**

| State | Render |
|---|---|
| Loading | Single skeleton rect: `height: 200, borderRadius: $sm, backgroundColor: $bg, opacity: 0.5` inside the card |
| Error | Inline error text (`$fg3`) + Retry button (ghost pill); no toast, no full-page error |
| Empty / first week (all xpEarned === 0) | Render bars at their minimum stub height (4px); show a muted caption below: `"No activity yet this week"` (EN) / `"لا نشاط هذا الأسبوع"` (AR) — i18n key `parent.overview.dailyActivity.empty` (NEW) |
| Populated | BarChart Shape A per §2.3 |

**Export CSV (P5-05-FE-2):** On press, serialize the 7 `{ date, xpEarned }` rows to a CSV string and trigger a browser download via a temporary `<a href="data:text/csv...">` element. Column headers: `Date,XP` (EN) or `التاريخ,النقاط` (AR). Dates stay ISO-8601 (technical strings). The button is already wired to `onPress` — just fill the stub.

### 3.3 Subject Mastery Card (`testID="overview-mastery-region"`)

**Capture:** `web/05-dashboard.png` (lower right) · `web-ar/05-dashboard.png`
**Preview card:** `web-skills-mastery.html`
**File:** `SubjectMasteryCard.tsx` (already built with stubs)

**Data source (P5-05-FE-4):** Replace `getSubjectMasteryStub` with a real query: `GET /api/v1/analytics/children/{childId}/subject-mastery`. Response: `{ subjects: [{ subjectId, masteryPercent, lessonsCount }] }`. The 4 product subjects may be returned in any order — render in fixed display order: Math → Science → Arabic → English.

The capture's mastery panel (web-skills-mastery.html) shows "14 lessons · 72%" on the right side of the label row. The current `MasteryBar` component does not render a lesson count — that is a new element to add to the `SubjectMasteryCard` (not to `MasteryBar` itself, which is a shared primitive). Update `SubjectMasteryCard` to show `"{n} lessons"` (EN) / `"دروس" (AR)` on the right of the label row, using `lessonsCount` from the response.

**Updated mastery row anatomy (from `web-skills-mastery.html`):**

```
Row (flexDirection: rowDir, justifyContent: space-between, fontSize: 13, fontWeight: 700):
  Left:  subject name ($fg1)
  Right: "{lessonsCount} lessons · {pct}%"
         - lesson count color: $fg3
         - pct color: per-subject accent (Math=$primary, Science=$success, Arabic=$purple, English=$accent)
         - pct font-weight: 800

Bar:
  height: 10px
  background: $bg (#0F172A)
  border-radius: 9999px (pill)
  overflow: hidden
  Fill: solid color (per SUBJECT_ACCENT above), no gradient (design spec for mastery uses solid accent color)
  Fill width: {masteryPercent}%

Gap between row header and bar: 6px
Gap between subject rows: 14px
```

NOTE: The `web-skills-mastery.html` uses `--lx-font-display` throughout. The AR mastery panel (seen in `ar-web-kpi.html` context) uses Cairo for labels. Apply the same font-family switch as the KPI tiles.

**States:**

| State | Render |
|---|---|
| Loading | 4 skeleton bars: `height: 10, borderRadius: 9999, backgroundColor: $cardSoft, opacity: 0.5`, stacked with 14px gap |
| Error | Same inline error + retry pattern as DailyActivityCard |
| Empty (no data) | Bars at 0% with `"—"` lesson count; caption below: `"Complete lessons to see mastery"` (EN) / `"أكمل الدروس لعرض الإتقان"` (AR) — i18n key `parent.overview.subjectMastery.empty` (NEW) |
| Populated | Per anatomy above |

### 3.4 Reports KPI Row (`testID="reports-kpi-region"`)

**Capture:** `web/06-reports.png` · `web-ar/06-reports.png`
**Preview cards:** `web-kpi-row.html`, `ar-web-kpi.html`

**Data source (P5-05-FE-4):** Same `GET /api/v1/analytics/children/{childId}/weekly-kpis` endpoint as Overview. The current `ReportsWeb` derives KPIs from `useStudentAttempts` client-side. Replace that derivation for the following tiles. Note G-2 gap: XP tile remains honest placeholder ("—") because the XP endpoint does not expose raw XP to the parent per the brief's confirmed gap.

**Tile map for Reports (applying product overrides to the capture):**

| Tile | i18n key (label) | Value source | Delta |
|---|---|---|---|
| Time Learning | `parent.reports.kpi.time` | `timeLearningMinutes` from endpoint | `timeLearningDeltaMinutes` (show sign: "+42m" or "–15m") |
| XP Earned | `parent.reports.kpi.xp` | `"—"` (G-2 gap, honest) | sub-copy: `parent.reports.kpi.xpComingSoon` |
| Lessons Done | `parent.reports.kpi.lessons` | `lessonsDone` from endpoint | `lessonsDelta` |
| Avg. Accuracy | `parent.reports.kpi.accuracy` | `avgAccuracyPercent` from endpoint | `accuracyDelta` (pct-pt diff) |

The Reports page's existing `splitByRange` + `meanAccuracy` client-side derivation is superseded for time/lessons/accuracy by the endpoint values. Keep the `avgAccuracy` calculation only as a fallback if the endpoint is unavailable.

**Delta sign display:** Positive delta → `$success` color (#22C55E) + "+" prefix. Negative delta → `$danger` color (#EF4444) + "−" prefix. Zero or null → `$fg3` color, no prefix. The arabic "−" prefix is a minus sign (U+2212), not a hyphen; the value reads RTL-correct because it is inside a `dir=ltr` span for the technical number.

**States:** Same as Overview KPI Row (§3.1). The Reports page shows a `LoadingSkeleton` covering all panels during initial load (already implemented — retain).

### 3.5 Reports 20-Day Trend Chart (`testID="reports-chart-slot-xp"` → rename to `"reports-chart-20day"`)

**Capture:** `web/06-reports.png` (center panel) · `web-ar/06-reports.png`

**Data source:** `GET /api/v1/analytics/children/{childId}/twenty-day-activity`. Returns `{ days: [{ date, xpEarned }] }` 20 entries. Active day = today.

Render with BarChart Shape B (§2.4). The `ChartPlaceholderPanel` with `testID="reports-chart-slot-xp"` is replaced entirely. The new component is a card with:

```
Title row:
  Left: "Last 20 days · XP earned" / "آخر ٢٠ يوماً · النقاط"  [i18n: parent.reports.charts.xpTitle]
  Sub:  "Today highlighted in indigo" / "اليوم بالنيلي"         [i18n: parent.reports.charts.xpSubtitle — NEW]
  Right: "Export CSV" ghost pill (same as DailyActivityCard)

Chart:
  BarChart shape B (showValueLabels=false, chartHeight=200, gap=6px)

testID: "reports-chart-20day"
```

The Export CSV for the 20-day chart: serializes `{ date, xpEarned }` x20 to CSV; same browser-download mechanic.

**States:**

| State | Render |
|---|---|
| Loading | Skeleton rect: `height: 200, borderRadius: $modal, backgroundColor: $cardSoft, opacity: 0.5` |
| Error | ErrorStrip + retry |
| Empty / first week | Show 20 bars at 4px minimum; caption: `"No data yet — check back soon"` (EN) / `"لا بيانات بعد — تحقق قريباً"` (AR) [i18n key `parent.reports.charts.noData` — NEW] |
| Populated | Shape B per §2.4 |

### 3.6 Reports Time-of-Day Chart (`testID="reports-chart-slot-tod"` → rename to `"reports-chart-tod"`)

**Capture:** `web/06-reports.png` (lower right) · `web-ar/06-reports.png`
**Preview card:** `web-time-of-day.html`

**Data source:** `GET /api/v1/analytics/children/{childId}/time-of-day`. Returns `{ buckets: [{ hour: 6|8|10|12|14|16|18|20, minutes: number }] }`. The peak bucket = max `minutes`.

Render with BarChart Shape C (§2.5). The `ChartPlaceholderPanel` with `testID="reports-chart-slot-tod"` is replaced.

**Peak bucket threshold for purple vs muted:** Any bucket with `minutes > 0` and `minutes >= peakMinutes * 0.6` renders as "purple" variant (`$purple` solid fill). The single highest bucket renders as the Reward gradient. Buckets below 60% of peak render as muted. This approximates the visual distribution in `web-time-of-day.html` where 2p, 4p, 6p are elevated and 4p is the peak.

**States:**

| State | Render |
|---|---|
| Loading | Skeleton rect: `height: 160, borderRadius: $card, backgroundColor: $cardSoft, opacity: 0.5` |
| Error | ErrorStrip + retry |
| Empty (all minutes === 0) | 8 bars at 4px minimum; no peak insight tip; caption: `"No session data yet"` (EN) / `"لا بيانات جلسات بعد"` (AR) [i18n key `parent.reports.charts.todEmpty` — NEW] |
| Populated | Shape C per §2.5 |

### 3.7 Reports Skills Mastery Panel (`testID="reports-mastery-panel"`)

**Data source:** Same subject-mastery endpoint as Overview (§3.3). The `SkillsMasteryPanel` in `ReportsWeb` currently renders bars at 0% (G-1 gap). P5-05 resolves this. Replace the `value={0}` with real `masteryPercent` from the endpoint.

Add the lesson count to the right of the label row (same as §3.3). The Reports mastery panel uses `borderRadius: $modal` (24px) while Overview mastery uses `borderRadius: $card` (20px) — keep these distinct as they already exist.

**States:** Same as §3.3.

---

## 4. RTL / Arabic Behavior

### 4.1 Universal RTL rules

- All chart container `Stack` elements carry `style={{ direction: 'ltr' }}` on web. This is a hard override — bar charts always grow left-to-right. Wrap with `<View style={{ direction: 'ltr' }}>` if needed.
- Value labels and axis labels inside the chart are Latin-locale only, never Eastern-Arabic (they are positional / technical).
- The KPI tile VALUES use Eastern-Arabic numerals when `locale === "ar"` (via `Intl.NumberFormat("ar-EG")`).
- XP delta copy in AR: `"+١٢٠ نقطة هذا الأسبوع"` — Eastern-Arabic digit + `نقطة` (points).
- Time format in AR: `"٣ س ٢٤ د"` — Eastern-Arabic digits + Arabic hour/minute symbols.
- Insight tip text direction: `writingDirection={direction}` (RTL text reads from right).

### 4.2 Per-component RTL checklist

| Component | RTL behavior |
|---|---|
| KPI tile header row | `flexDirection="row-reverse"` (label on right, chip on left in RTL) |
| KPI value + delta | `textAlign: "right"`, `writingDirection: "rtl"` |
| BarChart bars | always LTR column order (Mon=leftmost, Sun=rightmost) even in AR |
| BarChart day labels | Latin (`Mon`, `Tue`) — same in EN and AR |
| DailyActivityCard header | `flexDirection="row-reverse"` → title on right, Export CSV on left |
| 20-day chart header | same reversal |
| Time-of-day chart header | title on right (AR) |
| Insight tip | `flexDirection="row-reverse"` so 💡 is on the left |
| Mastery label row | `flexDirection="row-reverse"` → subject name on right, lesson count+pct on left |
| Mastery bar fill | Stays LTR (confirmed by `ar-web-weak-areas.html` + Brand Law) |
| Export CSV button | Label flips to `تصدير CSV` in AR |

### 4.3 Copy strings (EN / AR)

| i18n key | EN | AR | Status |
|---|---|---|---|
| `parent.overview.kpi.timeLearning` | "TIME LEARNING" | "وقت التعلم" | exists |
| `parent.overview.kpi.xpEarned` | "XP EARNED" | "النقاط المكتسبة" | exists |
| `parent.overview.kpi.lessonsDone` | "LESSONS DONE" | "دروس منجزة" | exists |
| `parent.overview.kpi.streak` | "DAY STREAK" | "سلسلة الأيام" | exists |
| `parent.overview.kpi.timeDelta` | "+{value} vs last week" | "+{value} عن الأسبوع الماضي" | UPDATE — remove % |
| `parent.overview.kpi.xpDelta` | "+{value} XP this week" | "+{value} نقطة هذا الأسبوع" | UPDATE — was xpDeltaPercent |
| `parent.overview.kpi.lessonsDelta` | "+{value} vs last week" | "+{value} عن الأسبوع الماضي" | exists (check sign) |
| `parent.overview.kpi.streakDelta` | "+{value} vs last week" | "+{value} عن الأسبوع الماضي" | exists |
| `parent.overview.dailyActivity.empty` | "No activity yet this week" | "لا نشاط هذا الأسبوع" | NEW |
| `parent.overview.subjectMastery.empty` | "Complete lessons to see mastery" | "أكمل الدروس لعرض الإتقان" | NEW |
| `parent.reports.charts.xpTitle` | "Last 20 days · XP earned" | "آخر ٢٠ يوماً · النقاط" | exists |
| `parent.reports.charts.xpSubtitle` | "Today highlighted in indigo" | "اليوم بالنيلي" | NEW |
| `parent.reports.charts.todTitle` | "Time of day" | "وقت اليوم" | exists |
| `parent.reports.charts.todSubtitle` | "When {name} learns best" | "متى يتعلم {name} بأفضل ما يمكن" | NEW |
| `parent.reports.charts.peakInsight` | "Peak focus is {start}–{end} — great time for new material" | "أفضل تركيز في {start}–{end} — وقت مثالي للمادة الجديدة" | NEW |
| `parent.reports.charts.noData` | "No data yet — check back soon" | "لا بيانات بعد — تحقق قريباً" | NEW |
| `parent.reports.charts.todEmpty` | "No session data yet" | "لا بيانات جلسات بعد" | NEW |
| `parent.overview.dailyActivity.exportCsv` | "Export CSV" | "تصدير CSV" | exists — keep "CSV" Latin |

**Note on "CSV":** "CSV" stays Latin (technical string) even in the Arabic label, per Brand Law exception rule. Arabic label reads: `"تصدير CSV"` — Arabic verb + Latin acronym. Correct per RTL conventions.

---

## 5. Token Ledger (exact values, no approximation)

| Token | CSS var | Value |
|---|---|---|
| `$bg` | `--lx-bg` | `#0F172A` |
| `$card` | `--lx-card` | `#1E293B` |
| `$cardSoft` | `--lx-card-soft` | `#334155` |
| `$fg1` | `--lx-fg1` | `#F8FAFC` |
| `$fg3` | `--lx-fg3` | `#94A3B8` |
| `$fg4` | (no token) | `#64748B` — DESIGN GAP (see §7) |
| `$borderSubtle` | `--lx-border` | `rgba(255,255,255,0.06)` (preview cards) / `rgba(255,255,255,0.08)` (css vars). Use the preview card value `rgba(255,255,255,0.06)` per actual card renders. |
| `$borderStrong` | `--lx-border-strong` | `rgba(255,255,255,0.12)` |
| `$primary` | `--lx-primary` | `#4F46E5` |
| `$primaryLight` | (no CSS var — Tamagui token only) | `#A5B4FC` |
| `$primarySoft` | `--lx-primary-soft` | `rgba(79,70,229,0.18)` |
| `$success` | `--lx-secondary` | `#22C55E` |
| `$successSoft` | `--lx-success-soft` | `rgba(34,197,94,0.18)` |
| `$purple` | `--lx-purple` | `#A855F7` |
| `$accent` | `--lx-accent` | `#F59E0B` |
| `$streak` | `--lx-streak` | `#FB923C` |
| `$streakSoft` | `--lx-streak-glow` n/a, uses custom `rgba(251,146,60,0.13)` | chip bg only |
| `$xp` | `--lx-xp` | `#FACC15` |
| `$xpSoft` | (custom) | `rgba(250,204,21,0.13)` |
| `$danger` | `--lx-danger` | `#EF4444` |
| `--lx-grad-levelup` | Level-Up gradient | `linear-gradient(135deg, #A855F7, #6366F1)` — active bar: use `180deg` (vertical top-to-bottom for bars) |
| `--lx-grad-reward` | Reward gradient | `linear-gradient(90deg, #F59E0B, #EF4444)` — TOD peak bar: use `180deg` vertical → `linear-gradient(180deg, #FB923C, #EF4444)` (card uses #FB923C not #F59E0B at top — streak orange) |
| `--lx-shadow-soft` | resting card shadow | `0 4px 12px rgba(0,0,0,0.15)` |
| `--lx-radius-card` | `$card` | `20px` |
| `--lx-radius-modal` | `$modal` | `24px` |
| `--lx-radius-sm` | `$sm` | `8px` |
| `--lx-radius-pill` | `$pill` | `9999px` |
| `--lx-space-5` | `$5` | `20px` |
| `--lx-space-6` | `$6` | `24px` |
| KPI padding | literal | `18px` (not `$5=20`, matches `web-kpi-row.html` literal) |
| Panel padding | literal | `22px` (DailyActivityCard, SubjectMastery, 20-day chart, TOD chart) |

---

## 6. Motion Spec

All chart components use the existing brand motion rules — no new patterns introduced.

| Interaction | Motion |
|---|---|
| Child switch (new data loads) | KPI tiles fade out (opacity 0→0 skeleton) then bars animate in; chart bars grow from 0 height to target over 600ms ease-out (`--lx-ease-out`) |
| Bar mount (initial render) | Each bar animates `height: 0 → target` staggered by 40ms per bar. Maximum total duration = 40ms * 7 bars = 280ms (within the ≤800ms snappy rule). Shape B (20 bars): stagger 20ms per bar = 400ms total. Shape C (8 bars): 50ms per bar = 400ms. |
| Active bar glow | Static (no pulse animation). The indigo `box-shadow` is always-on for the active bar — no looping glow. |
| Peak bar (Shape C) | Static Reward gradient fill + static orange `box-shadow`. No pulse. |
| KPI tile hover (web) | `scale: 1.02`, `backgroundColor: $cardSoft`, `transition: all 120ms`; reverts on mouse-out. Already in `OverviewWeb`. |
| KPI tile press | `scale: 0.95`, `duration: 80ms` (brand standard). |
| Export CSV press | `scale: 0.95`, `80ms`. |
| Loading → populated | Skeleton fades out (opacity 0.6→0, 120ms), chart fades in (0→1, 240ms). |

**Bar grow animation implementation:** Use React Native's `Animated.timing` or Reanimated `withTiming` if available in the Expo setup. If neither is available without a new dependency, use CSS `transition: height 600ms cubic-bezier(0.16,1,0.3,1)` on web via `style` prop. Do NOT add a new animation library. Ask before adding Reanimated if not already in the dependency tree.

---

## 7. Design Gaps and Open Questions

### GAP-1: `$fg4` token does not exist in `colors_and_type.css`
The existing code uses `color="$fg4"` in `ReportsWeb.tsx` (ChartPlaceholderPanel). There is no `--lx-fg4` in the design token CSS. The closest match is `#64748B` (Slate 500, between `$fg3` `#94A3B8` and `$bg`). The frontend should use `$fg3` or add `--lx-fg4: #64748B` as a new token and define it in `packages/design-system/src/tokens`. Flag for token system maintainer.

### GAP-2: `$borderSubtle` resolves differently in CSS vs Tamagui
`colors_and_type.css` defines `--lx-border: rgba(255,255,255,0.08)` but the preview cards use `rgba(255,255,255,0.06)`. The Tamagui token `$borderSubtle` presumably mirrors one of these. The frontend should verify the Tamagui token value and use whichever matches the preview cards (`0.06`). Do not use `0.08` unless the Tamagui token maps to that.

### GAP-3: No dedicated AR preview card for the daily-activity chart
`web-activity-chart.html` has no AR twin (`ar-web-activity-chart.html`). The AR behavior is inferred from `web-ar/05-dashboard.png` and Brand Law RTL rules. The spec above fully covers the AR behavior. No design action needed — frontend implements from this spec.

### GAP-4: No dedicated preview card for the 20-day trend chart
`web-activity-chart.html` shows a 7-bar weekly chart only. The 20-day variant is inferred from `web/06-reports.png`. The Shape B spec in §2.4 is derived from the capture. No preview card exists; the frontend should not wait for one.

### GAP-5: `lessonsCount` on mastery bars requires mastery endpoint update
The current mastery response shape is unknown (the endpoint is P5-01-BE/P5-02-BE). If the endpoint does not return `lessonsCount`, the frontend should omit the lesson count from the label row (not show "— lessons") and log a design gap in the PR. The lesson count is a "nice to have" that matches the capture, not a blocking requirement for P5-05 AC.

### GAP-6: Animation library availability
Staggered bar-grow animation requires either Reanimated or CSS transitions. The spec calls for CSS transitions on web (the primary surface). If Reanimated is in the monorepo, use it. If not, use CSS transitions on web and skip animation on native (static render). Do NOT add Reanimated unilaterally — ask the lead.

### GAP-7: `peakStart`/`peakEnd` format for insight tip
The time-of-day endpoint returns `hour` as an integer. The insight tip says "4–5pm". The "end" is `peakHour + 2` (2-hour bucket). Confirm bucket size with the backend team. The spec assumes 2-hour buckets (per `web-time-of-day.html` which shows 6a, 8a, 10a... in 2-hour steps).

### GAP-8: XP delta sign in Overview is currently misnamed `xpDeltaPercent`
The `OverviewKpiStub` interface has `xpDeltaPercent: number` and the current `buildKpis()` function formats it as a percentage (`+28% vs last week`). This is wrong per the Brief: the real endpoint returns `xpDelta` (absolute). The frontend must:
1. Rename the field in `OverviewKpiStub` to `xpDelta` (or remove the stub once real data lands).
2. Update the `buildKpis` function to format as "+120 XP this week" not "+28%".
3. Update the i18n key `parent.overview.kpi.xpDelta` to use absolute-value copy.

---

## 8. Accessibility Spec

| Rule | Implementation |
|---|---|
| Chart is one a11y node | `accessible` + `accessibilityRole="image"` on the outermost BarChart wrapper; all inner nodes `accessibilityElementsHidden` |
| Chart summary | `accessibilityLabel` prop required on every BarChart usage; callers build the summary string (§2.2) |
| KPI tile summary | Already implemented via absolute-positioned overlay `accessibilityLabel={label + value + delta}` (existing OverviewWeb pattern — retain) |
| Focus ring | Tamagui `focusStyle` with `--lx-focus-ring` (`0 0 0 2px #4F46E5, 0 0 0 6px rgba(99,102,241,0.45)`) on all interactive elements (Export CSV, Retry button) |
| Export CSV min target | `minHeight: 36` already in DailyActivityCard; ensure `minWidth: 44` too for touch targets ≥44px per brand spec |
| Color-only information | Bar color alone does not communicate meaning (the "active" bar also has a higher value label in `$primaryLight`). Insight tip uses text. No color-only encoding. |
| Progressbar ARIA | MasteryBar already implements `accessibilityRole="progressbar"` with `accessibilityValue`. No change needed. |

---

## 9. testID Conventions for `frontend-e2e-tester`

| Element | testID |
|---|---|
| Overview KPI region | `"overview-kpi-region"` (existing) |
| Overview daily activity card | `"daily-activity-card"` (existing outer card) |
| Daily activity BarChart | `"daily-activity-chart"` (new — on BarChart component) |
| Overview mastery region | `"overview-mastery-region"` (existing) |
| Reports root | `"reports-root"` (existing) |
| Reports KPI region | `"reports-kpi-region"` (existing) |
| Reports 20-day trend chart | `"reports-chart-20day"` (rename from `"reports-chart-slot-xp"`) |
| Reports time-of-day chart | `"reports-chart-tod"` (rename from `"reports-chart-slot-tod"`) |
| Reports mastery panel | `"reports-mastery-panel"` (existing) |
| Reports loading skeleton | `"reports-loading"` (existing) |
| Reports error strip | `"reports-error-strip"` (existing) |
| Reports first-week band | `"reports-first-week-band"` (existing) |
| BarChart active bar | `"bar-chart-bar-active"` (new — on the active bar Stack) |
| BarChart bar (indexed) | `"bar-chart-bar-{index}"` (new — on each bar Stack, zero-indexed) |
| Peak insight tip | `"tod-peak-insight"` (new) |

---

## 10. Implementation Handoff

| Piece | Target | Notes |
|---|---|---|
| `BarChart` primitive | `packages/ui/src/components/BarChart/index.tsx` | New file. Export from `packages/ui/src/index.ts`. |
| `BarChart` types | `packages/ui/src/components/BarChart/types.ts` | `BarChartDataPoint`, `BarChartProps` |
| Replace DailyActivityCard placeholder | `apps/student-app/app/(parent)/_components/DailyActivityCard.tsx` lines 100–116 | Import `BarChart` from `@learnexia/ui` |
| Wire Overview KPIs | `apps/student-app/app/(parent)/_components/OverviewWeb.tsx` | Replace `getOverviewKpiStub` with real query hook. Fix `xpDeltaPercent` → `xpDelta` absolute. |
| Wire Subject Mastery | `apps/student-app/app/(parent)/_components/SubjectMasteryCard.tsx` | Replace `getSubjectMasteryStub`, add lesson count to label row |
| Replace 20-day chart placeholder | `apps/student-app/app/(parent)/reports.tsx` (`ChartPlaceholderPanel` for `xpTitle`) | New inline component or extracted `TwentyDayChartPanel.tsx` |
| Replace TOD chart placeholder | `apps/student-app/app/(parent)/reports.tsx` (`ChartPlaceholderPanel` for `todTitle`) | New inline component or extracted `TimeOfDayChartPanel.tsx` |
| Wire Reports KPIs | `apps/student-app/app/(parent)/reports.tsx` `KpiRow` component | Replace client-side attempt derivation with endpoint values |
| Wire Reports Mastery | `apps/student-app/app/(parent)/reports.tsx` `SkillsMasteryPanel` | Replace `value={0}` with endpoint values |
| Remove stubs | `apps/student-app/app/(parent)/_components/parentDashboardStubs.ts` | Remove `getOverviewKpiStub`, `getSubjectMasteryStub` once real endpoints are wired. Retain `getChildStatsStub`, energy stubs, and activity stubs (not in P5-05 scope). |
| i18n keys (NEW) | `apps/student-app/src/i18n/en.json` + `ar.json` | See §4.3 — 7 new keys needed |
| Design token `$fg4` | `packages/design-system/src/tokens/colors.ts` | Add `fg4: '#64748B'` (GAP-1) |

---

## 11. Delta Table (alignment pass on existing screens)

| Element | Current value | Target value (token + card) | Severity | Fix |
|---|---|---|---|---|
| Overview XP delta copy | `"+28% vs last week"` (percent) | `"+120 XP this week"` (absolute) | Blocker | Change `xpDeltaPercent` field to `xpDelta`, update copy template — `web-kpi-row.html` shows `+28% vs last week` but Brief confirms xpDelta is absolute |
| Daily activity chart area | Empty grey placeholder rect, 200px | BarChart Shape A (7 bars, 180px well, value labels, active bar Level-Up gradient) | Blocker | Replace with BarChart component |
| Reports 20-day chart slot | `ChartPlaceholderPanel` with skeleton stubs and "coming soon" copy | BarChart Shape B (20 bars, 200px well, no value labels, indigo active) | Blocker | Replace ChartPlaceholderPanel |
| Reports TOD chart slot | `ChartPlaceholderPanel` with skeleton stubs | BarChart Shape C (8 bars, 160px well, value labels in minutes, peak insight) | Blocker | Replace ChartPlaceholderPanel |
| Overview KPI data | Deterministic stubs (`getOverviewKpiStub`) | Real endpoint `weekly-kpis` per child | Blocker | Replace stub with real query hook, pass `childId` |
| Subject mastery data | Deterministic stubs, no lesson count | Real endpoint `subject-mastery`, add lesson count to label row | Major | Replace stub, update mastery row anatomy |
| Reports mastery bars | All at `value={0}` (G-1 gap) | Real endpoint `subject-mastery` values | Major | Replace `value={0}` with endpoint value |
| Reports KPI XP tile | Honest `"—"` placeholder (correct) | No change — G-2 gap persists in P5-05 | — | No action |
| testID `reports-chart-slot-xp` | `"reports-chart-slot-xp"` | `"reports-chart-20day"` | Minor | Rename testID |
| testID `reports-chart-slot-tod` | `"reports-chart-slot-tod"` | `"reports-chart-tod"` | Minor | Rename testID |
| Reports mastery panel `borderRadius` | `"$modal"` (24px) | Keep `$modal` (24px) — confirmed per capture | — | No change |
| Overview mastery card `borderColor` | `"$border"` | `"$borderSubtle"` (to match `rgba(0.06)` from card previews) | Minor | Change token reference |
| KPI tile padding | `padding={18}` | Correct — matches `web-kpi-row.html` `padding:18px` | — | No change |
| Missing i18n keys | 7 keys absent | See §4.3 NEW keys | Major | Add to en.json + ar.json |

---

## Summary for Frontend Agent

**New component to build:** `packages/ui/src/components/BarChart` — a pure Tamagui `Stack`/`Text` bar chart primitive with three shapes:
- Shape A: 7-bar weekly chart, value labels above, Level-Up gradient active bar, 180px well
- Shape B: 20-bar trend chart, no value labels, Level-Up gradient active bar, 200px well, 6px gap
- Shape C: 8-bar time-of-day chart, minute-value labels above, peak = Reward gradient, secondary-high = purple solid, 160px well

**Props summary:** `data: BarChartDataPoint[]`, `maxValue?`, `chartHeight?`, `showValueLabels?`, `direction?`, `barVariant?`, `locale?`, `testID?`, `accessibilityLabel` (required). Bar layout always `direction: ltr` on web. Active bar determined by `isActive` flag in data, not by index.

**Key data wiring changes:** Overview KPIs + subject mastery get real endpoint data (replace stubs). Reports charts replace two `ChartPlaceholderPanel` slots. Reports mastery gets real values. XP delta copy changes from percent to absolute. Seven new i18n keys must be added.

**Deferred (NOT in this spec):** Send Report button functionality, period-select dropdown, grade-transition control (P5-06).

Design spec ready for frontend.
