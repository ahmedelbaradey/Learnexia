# Design Spec — P7-10 Platform Analytics Dashboard

**Surface:** `apps/admin-dashboard` (Next.js 15, desktop-first, dark theme, LTR)
**Route:** `app/(admin)/analytics/page.tsx`
**Token authority:** `design-system/colors_and_type.css` + shipped `--lx-*` variable set
**Shell authority:** `P7-12-FE.md` / `P7-admin-users-wave.md` (AdminShell, same token language — all below is additive)
**Locale:** `ADMIN_LOCALE = 'en'`, `dir="ltr"`. EN ships; AR strings authored for readiness.
**Theme:** Dark default (`$bg #0F172A`). No gamification chrome, no reward gradients, no confetti.
**Read-only:** ZERO mutation affordances. No write forms, no destructive buttons.

> No new screenshot captures exist for this admin route. This spec is derived from the token set, the four-state pattern established in `audit/page.tsx` and `moderation/page.tsx`, and the verified endpoint DTOs in `docs/briefs/P7-10-P7-11-admin-dashboards.md`. Captures for the parent dashboard show the token aesthetics only; the admin surface is denser and has no gamification chrome.

---

## Admin surface design principles (enforce identically to P7-12)

1. No XP bars, reward gradients, confetti, mascot, or Level-Up fills anywhere.
2. Motion minimal: hover 120 ms `var(--lx-ease-out)`, skeleton shimmer 1400 ms linear. No spring easing.
3. No primary CTA — read-only surfaces have no "hero" button.
4. Typography: Poppins (EN body + headings), Cairo (AR display headings), Tajawal (AR body).
5. Card padding `$4` (16 px) or `$6` (24 px); table cell padding `12px 16px`.
6. All numbers rendered, never spelled out. `font-variant-numeric: tabular-nums` on all metrics.
7. KPI figures: `font-size: 28px; font-weight: 800; font-variant-numeric: tabular-nums`. Chunky and informational.

---

## PART A — Nav entry: "Analytics" in `AdminSideNav`

**File:** `apps/admin-dashboard/components/AdminSideNav.tsx` (additive — append to `NAV_ITEMS`)

### A.1 — New `RealNavItem`

```
{
  kind: 'real',
  key: 'analytics',
  label: strings.navAnalytics,     // EN: "Analytics" / AR: "التحليلات"
  icon: '📊',                      // emoji placeholder (same pattern as existing items)
  href: '/analytics',
  activePrefix: '/analytics',
}
```

Icon note: emoji placeholder per existing nav pattern. Future upgrade target: Lucide `bar-chart-2` (18 px, 2 px stroke, rounded caps).

### A.2 — Active state tokens (mirror audit/curriculum/gamification)

| State | Background | Label color | Label weight |
|---|---|---|---|
| Active (`pathname.startsWith('/analytics')`) | `$primarySoft` — `rgba(79,70,229,0.18)` | `$primary` `#4F46E5` | 600 |
| Inactive | `transparent` | `$fg2` `#CBD5E1` | 400 |
| Hover inactive | `$card` `#1E293B` | `$fg2` | 400 |

Transition: `background-color var(--lx-dur-fast) var(--lx-ease-out)` (120 ms).
Active detection: `pathname === '/analytics' || pathname.startsWith('/analytics/')`.
`aria-current="page"` on active link. `data-testid="nav-analytics"`.

### A.3 — New strings keys

```ts
// AdminStrings interface additions:
navAnalytics: string
// EN: "Analytics"    AR: "التحليلات"    i18n: admin.nav.analytics

analyticsPageTitle: string
// EN: "Analytics"    AR: "التحليلات"    i18n: admin.analytics.pageTitle

analyticsPageHeading: string
// EN: "Platform Analytics"   AR: "تحليلات المنصة"   i18n: admin.analytics.heading

analyticsPageSubtitle: string
// EN: "Aggregated platform metrics. Last 30 days by default."
// AR: "مقاييس المنصة المجمّعة. آخر ٣٠ يومًا افتراضيًا."
// i18n: admin.analytics.subtitle

analyticsLoadingLabel: string
// EN: "Loading analytics..."    AR: "جارٍ تحميل التحليلات..."

analyticsLoadError: string
// EN: "Failed to load analytics data."   AR: "فشل تحميل بيانات التحليلات."

analyticsRetry: string
// EN: "Retry"   AR: "إعادة المحاولة"

analyticsEmptyHeading: string
// EN: "No data for this range"   AR: "لا توجد بيانات لهذه الفترة"

analyticsEmptyBody: string
// EN: "Try a wider date range or check back after some activity."
// AR: "جرّب نطاقًا زمنيًا أوسع أو تحقق لاحقًا بعد حدوث نشاط."

analyticsDateFrom: string
// EN: "From"   AR: "من"

analyticsDateTo: string
// EN: "To"     AR: "إلى"

analyticsDateRangeError: string
// EN: "'To' must be after 'From'."   AR: "يجب أن يكون 'إلى' بعد 'من'."

analyticsClearFilters: string
// EN: "Clear"   AR: "مسح"

analyticsSubjectSlice: string
// EN: "Subject"   AR: "المادة"

analyticsGradeSlice: string
// EN: "Grade"   AR: "الصف"

analyticsLanguageSlice: string
// EN: "Language"   AR: "اللغة"

analyticsAllSubjects: string
// EN: "All Subjects"   AR: "جميع المواد"

analyticsAllGrades: string
// EN: "All Grades"   AR: "جميع الصفوف"

analyticsAllLanguages: string
// EN: "All Languages"   AR: "جميع اللغات"

// KPI card labels
analyticsKpiActiveStudents: string
// EN: "Active Students"   AR: "الطلاب النشطون"

analyticsKpiAnalyticsStudents: string
// EN: "Analytics Active"   AR: "النشاط التحليلي"

analyticsKpiLessonsCompleted: string
// EN: "Lessons Completed"   AR: "الدروس المكتملة"

analyticsKpiTotalAttempts: string
// EN: "Total Attempts"   AR: "إجمالي المحاولات"

analyticsKpiMissionsCompleted: string
// EN: "Missions Completed"   AR: "المهام المكتملة"

analyticsKpiXpEarned: string
// EN: "XP Earned"   AR: "النقاط المكتسبة"

analyticsKpiActiveSubscriptions: string
// EN: "Active Subscriptions"   AR: "الاشتراكات النشطة"

analyticsKpiAiSafetyEvents: string
// EN: "AI Safety Events"   AR: "أحداث سلامة الذكاء الاصطناعي"

analyticsKpiAiBlocked: string
// EN: "AI Blocked"   AR: "محجوب بالذكاء الاصطناعي"

analyticsKpiAiFlagged: string
// EN: "AI Flagged"   AR: "مُعلَّم بالذكاء الاصطناعي"

analyticsKpiAiRequests: string
// EN: "AI Request Volume"   AR: "حجم طلبات الذكاء الاصطناعي"

analyticsKpiTotalSessions: string
// EN: "Total Sessions"   AR: "إجمالي الجلسات"

analyticsKpiAvgSessionDuration: string
// EN: "Avg. Session (min)"   AR: "متوسط الجلسة (دقيقة)"

analyticsKpiAvgActiveDays: string
// EN: "Avg. Active Days / Student"   AR: "متوسط الأيام النشطة / طالب"

analyticsKpiReturningRate: string
// EN: "Returning Student Rate"   AR: "معدل عودة الطلاب"

analyticsNaValue: string
// EN: "N/A"   AR: "غ.م."   (used as prefix when NaReason is present)

// Breakdown chart section headings
analyticsBySubjectHeading: string
// EN: "By Subject"   AR: "حسب المادة"

analyticsByGradeHeading: string
// EN: "By Grade"   AR: "حسب الصف"

analyticsByLanguageHeading: string
// EN: "By Language"   AR: "حسب اللغة"

analyticsMetricLessons: string
// EN: "Lessons"   AR: "الدروس"

analyticsMetricAttempts: string
// EN: "Attempts"   AR: "المحاولات"

analyticsMetricStudents: string
// EN: "Students"   AR: "الطلاب"

analyticsSubscriptionsByTier: string
// EN: "Subscriptions by Plan"   AR: "الاشتراكات حسب الخطة"
```

---

## PART B — Page Layout (`/analytics`)

**File:** `apps/admin-dashboard/app/(admin)/analytics/page.tsx`
**Directive:** `'use client'; export const dynamic = 'force-dynamic';`

### B.1 — Outer structure

Wrapped in `<AdminShell title={strings.analyticsPageTitle}>`. The shell provides the `240 px` side nav + top bar + `<main>` with `padding: $8` (32 px) at laptop+, `$4` (16 px) at small.

```
<AdminShell title={strings.analyticsPageTitle}>
  <Stack flexDirection="column" gap="$6">          {/* 24 px between sections */}
    <PageHeader />                                  {/* heading + subtitle */}
    <AnalyticsFilters />                            {/* date-range + slice toggles */}
    <DateRangeErrorBanner />                        {/* conditional */}
    <div aria-live="polite" aria-atomic="false">
      {showSkeleton && <KpiCardsSkeleton />}
      {showError   && <ErrorState />}
      {showEmpty   && <EmptyState />}
      {showResults && <>
        <KpiCards />                               {/* summary card grid */}
        <KpiBreakdownCharts />                     {/* categorical breakdown bars */}
      </>}
    </div>
  </Stack>
</AdminShell>
```

### B.2 — Page header

```
<Stack flexDirection="row" alignItems="center" justifyContent="space-between" gap="$4">
  <Stack flexDirection="column" gap="$1">
    <Text fontFamily="$heading" fontSize={24} fontWeight="700" color="$fg1"
          data-testid="analytics-page-heading">
      {strings.analyticsPageHeading}
    </Text>
    <Text fontFamily="$body" fontSize={14} color="$fg3" style={{ marginTop: 4 }}>
      {strings.analyticsPageSubtitle}
    </Text>
  </Stack>
</Stack>
```

Typography tokens:
- Heading: `font-family: Poppins; font-size: 24px; font-weight: 700; color: var(--lx-fg1) #F1F5F9; line-height: 1.3`
- Subtitle: `font-size: 14px; font-weight: 400; color: var(--lx-fg3) #64748B; line-height: 1.5`

---

## PART C — Filter Bar (`AnalyticsFilters`)

**File:** `apps/admin-dashboard/components/analytics/AnalyticsFilters.tsx`
**State:** Zustand v5 store (mirror the Moderation/Users store pattern). Holds `{ from: string; to: string; subjectSlice: number | null; gradeSlice: number | null; languageSlice: number | null }`.

### C.1 — Filter bar layout

Horizontal flex row, `flexWrap: 'wrap'`, `gap: 12px`, `alignItems: 'flex-end'`.

Contains (left to right in LTR):
1. Date "From" input (`type="date"`, width 160 px)
2. Date "To" input (`type="date"`, width 160 px)
3. Subject slice select (width 150 px) — client-side only, drives chart highlights; does NOT re-fetch
4. Grade slice select (width 130 px) — client-side only
5. Language slice select (width 140 px) — client-side only
6. Clear button (shown when any filter is non-default)

**Important:** "From" and "To" drive `usePlatformKpis(from, to)` (re-fetches). The three slice selects are client-side-only controls that filter which bar segment is highlighted/labelled in `KpiBreakdownCharts` — they do NOT trigger a new API request.

### C.2 — Input / select styles (mirror existing pattern exactly)

```css
/* inputStyle */
height: 44px;
background-color: var(--lx-bg);
border-radius: var(--lx-radius-sm);   /* 8px */
border: 1px solid var(--lx-border);
color: var(--lx-fg1);
padding-inline: 12px;
font-size: 14px;
font-family: inherit;
outline: none;
box-sizing: border-box;

/* selectStyle */
height: 44px;
background-color: var(--lx-bg);
border-radius: var(--lx-radius-sm);   /* 8px */
border: 1px solid var(--lx-border);
color: var(--lx-fg2);
padding-inline: 12px;
font-size: 14px;
font-family: inherit;
outline: none;
cursor: pointer;
appearance: auto;
```

Focus state: `border-color: var(--lx-primary); box-shadow: var(--lx-focus-ring-admin)` (mirrors audit filter focus handlers). Blur restores border to `var(--lx-border)`.

Date error state (To < From): border-color `var(--lx-danger)` on both inputs + inline error message below bar (`role="alert"`, `color: var(--lx-danger)`, `font-size: 12px`). `data-testid="analytics-date-range-error"`.

### C.3 — Clear button

```css
height: 36px;
padding-inline: 12px;
border-radius: var(--lx-radius-button);  /* 16px */
border: 1px solid var(--lx-border);
background-color: transparent;
color: var(--lx-fg2);
font-size: 13px;
font-weight: 500;
cursor: pointer;
font-family: inherit;
```

`data-testid="analytics-clear-filters"`. Shown only when `from !== '' || to !== '' || subjectSlice !== null || gradeSlice !== null || languageSlice !== null`.

### C.4 — Label mapping for slices

Subject slice options (`subjectCode` is int in DTO; FE maps to display):
```
null  → strings.analyticsAllSubjects  ("All Subjects")
1     → "Math"         (AR: "الرياضيات")
2     → "Science"      (AR: "العلوم")
3     → "Arabic"       (AR: "اللغة العربية")
4     → "English"      (AR: "اللغة الإنجليزية")
```
i18n keys: `admin.analytics.subject.math`, `.science`, `.arabic`, `.english`

Language slice options (`language` is int in DTO):
```
null → strings.analyticsAllLanguages  ("All Languages")
0    → "Arabic"   (AR: "العربية")
1    → "English"  (AR: "الإنجليزية")
```
i18n keys: `admin.analytics.language.arabic`, `admin.analytics.language.english`

Grade slice options: `null` → "All Grades"; `1`–`12` → "Grade N" (AR: "الصف N").

### C.5 — testIDs

| Element | testID |
|---|---|
| From date | `analytics-filter-date-from` |
| To date | `analytics-filter-date-to` |
| Subject select | `analytics-filter-subject` |
| Grade select | `analytics-filter-grade` |
| Language select | `analytics-filter-language` |
| Clear button | `analytics-clear-filters` |
| Date error message | `analytics-date-range-error` |

---

## PART D — KPI Cards (`KpiCards`)

**File:** `apps/admin-dashboard/components/analytics/KpiCards.tsx`
**Data source:** `usePlatformKpis(from, to)` → `PlatformKpiSummaryDto`

### D.1 — Card grid layout

Responsive CSS grid:
```css
display: grid;
grid-template-columns: repeat(4, 1fr);   /* laptop+ */
gap: 16px;
/* Narrow: repeat(2, 1fr) below laptop */
```

Each KPI card is a `<div>` with:
```css
background-color: var(--lx-card);       /* #1E293B */
border-radius: var(--lx-radius-card);   /* 20px */
border: 1px solid var(--lx-border);
padding: 20px;
display: flex;
flex-direction: column;
gap: 8px;
```

Hover: `border-color: rgba(79,70,229,0.3)` (transition 120 ms). No scale. No glow. No card darkening.

### D.2 — KPI card anatomy

```
┌──────────────────────────────────────────┐
│ [icon circle 32px]                        │
│                                           │
│ <label>                    14px fg3 500   │
│ <value>                    28px fg1 800   │
│   OR                                      │
│ <N/A label>                12px accent    │
│ <N/A reason>               11px fg3       │
└──────────────────────────────────────────┘
```

**Label typography:**
```css
font-size: 14px;
font-weight: 500;
color: var(--lx-fg3);       /* #64748B */
font-family: Poppins, inherit;
line-height: 1.4;
```

**Value typography (normal state):**
```css
font-size: 28px;
font-weight: 800;
color: var(--lx-fg1);       /* #F1F5F9 */
font-variant-numeric: tabular-nums;
line-height: 1.2;
```

**N/A state (when `*NaReason` field is a non-empty string):**
```
<span style="font-size:13px; font-weight:700; color:var(--lx-accent); font-variant-numeric:tabular-nums;">
  N/A
</span>
<span style="font-size:11px; color:var(--lx-fg3); line-height:1.4; margin-top:2px; display:block;">
  {naReason}
</span>
```
`var(--lx-accent)` = `#F59E0B`. The N/A label is amber — distinguishable but not alarming. Do NOT show `0` when a `*NaReason` is present.

Icon circles: 32 × 32 px, `border-radius: 9999px`. Color pairs per group below.

### D.3 — KPI card inventory and N/A handling

Section 1 — Engagement (row 1, 4 cards):

| Field | Label key | Icon bg | Icon color | N/A condition |
|---|---|---|---|---|
| `distinctActiveStudents` | `analyticsKpiActiveStudents` | `rgba(79,70,229,0.15)` | `#6366F1` | never |
| `analyticsActiveStudents` | `analyticsKpiAnalyticsStudents` | `rgba(79,70,229,0.15)` | `#6366F1` | never |
| `lessonsCompleted` | `analyticsKpiLessonsCompleted` | `rgba(34,197,94,0.15)` | `#22C55E` | never |
| `totalAttempts` | `analyticsKpiTotalAttempts` | `rgba(34,197,94,0.15)` | `#22C55E` | never |

Section 2 — Gamification (row 2, 3 cards):

| Field | Label key | Icon bg | Icon color | N/A condition |
|---|---|---|---|---|
| `missionsCompleted` | `analyticsKpiMissionsCompleted` | `rgba(245,158,11,0.15)` | `#F59E0B` | never |
| `xpEarnedInWindow` | `analyticsKpiXpEarned` | `rgba(245,158,11,0.15)` | `#F59E0B` | never |
| `quizzesCompletedNaReason` — present? | `analyticsKpiQuizzesCompleted` | `rgba(34,197,94,0.15)` | `#22C55E` | when `quizzesCompletedNaReason` is non-empty string |

Note: `quizzesCompletedNaReason` is a string field on the DTO. There is no `quizzesCompleted` number field — the metric is either absent (N/A) or must be derived. Treat this card as N/A whenever the reason string is non-null/non-empty. `data-testid="kpi-quizzes-na"`.

Section 3 — Subscriptions (row 2 continued, 1 card + 1 breakdown chip):

| Field | Label key | Icon bg | Icon color | N/A condition |
|---|---|---|---|---|
| `totalActiveSubscriptions` | `analyticsKpiActiveSubscriptions` | `rgba(168,85,247,0.15)` | `#A855F7` | `revenueNaReason` non-empty (show alongside the count) |

When `revenueNaReason` is non-empty, show the subscription count normally but append a subdued N/A note beneath the value: `font-size:11px; color:var(--lx-fg3)` — "Revenue: N/A — {revenueNaReason}".

Section 4 — AI Safety (row 3, 3 cards):

| Field | Label key | Icon bg | Icon color | N/A condition |
|---|---|---|---|---|
| `totalAiSafetyEvents` | `analyticsKpiAiSafetyEvents` | `rgba(239,68,68,0.12)` | `#EF4444` | `aiRequestVolumeNaReason` non-empty (show on the volume card) |
| `aiBlockedCount` | `analyticsKpiAiBlocked` | `rgba(239,68,68,0.12)` | `#EF4444` | never |
| `aiFlaggedCount` | `analyticsKpiAiFlagged` | `rgba(239,68,68,0.12)` | `#EF4444` | never |
| `aiRequestVolume` | `analyticsKpiAiRequests` | `rgba(239,68,68,0.12)` | `#EF4444` | when `aiRequestVolumeNaReason` non-empty |

Section 5 — Session / Retention (row 4, 4 cards):

| Field | Label key | Icon bg | Icon color | N/A condition |
|---|---|---|---|---|
| `totalSessions` | `analyticsKpiTotalSessions` | `rgba(79,70,229,0.15)` | `#6366F1` | `sessionDurationNaReason` applies only to duration card |
| `avgSessionDurationSeconds` (divide by 60 for minutes display) | `analyticsKpiAvgSessionDuration` | `rgba(79,70,229,0.15)` | `#6366F1` | when `sessionDurationNaReason` non-empty |
| `avgActiveDaysPerStudent` (1 decimal) | `analyticsKpiAvgActiveDays` | `rgba(79,70,229,0.15)` | `#6366F1` | `retentionNaReason` applies to returning rate card |
| `returningStudentRate` (×100, 1 decimal, append "%") | `analyticsKpiReturningRate` | `rgba(79,70,229,0.15)` | `#6366F1` | when `retentionNaReason` non-empty |

### D.4 — Icon shapes (inline SVG, no Lucide import — mirror gamification hub pattern)

Use 16 × 16 px inline SVG shapes in the icon circles. Suggested Lucide paths (draw inline as done in `gamification/page.tsx`):
- Active students: `users` path
- Lessons: `book-open` path
- Attempts: `layers` path
- Missions: `target` path
- XP: `zap` path
- Subscriptions: `credit-card` path
- AI safety: `shield` path
- Sessions: `clock` path

### D.5 — testIDs

`data-testid="kpi-card-{fieldSlug}"` on each card wrapper. Examples:
```
kpi-card-active-students
kpi-card-analytics-students
kpi-card-lessons-completed
kpi-card-total-attempts
kpi-card-missions-completed
kpi-card-xp-earned
kpi-card-quizzes           (N/A card)
kpi-card-subscriptions
kpi-card-ai-safety-events
kpi-card-ai-blocked
kpi-card-ai-flagged
kpi-card-ai-requests
kpi-card-sessions
kpi-card-avg-session
kpi-card-avg-active-days
kpi-card-returning-rate
```
`data-testid="kpi-value-{fieldSlug}"` on the value `<span>`.

---

## PART E — KPI Breakdown Charts (`KpiBreakdownCharts`)

**File:** `apps/admin-dashboard/components/analytics/KpiBreakdownCharts.tsx`
**Data source:** `bySubject[]`, `byGrade[]`, `byLanguage[]` arrays from the same `PlatformKpiSummaryDto` (no separate API call).
**Chart library:** Recharts 2.x (`recharts@^2`). Themed via admin CSS-variable tokens. NOT Recharts defaults.
**Chart wrapper:** `apps/admin-dashboard/components/charts/BarBreakdown.tsx` — a thin wrapper around `<BarChart>` + `<Bar>` + `<XAxis>` + `<YAxis>` + `<Tooltip>` + `<Legend>`, all styled via CSS vars.

### E.1 — Section structure

Three sub-sections stacked vertically with `gap: 24px`:
1. By Subject
2. By Grade
3. By Language

Each sub-section:
```css
background-color: var(--lx-card);     /* #1E293B */
border-radius: var(--lx-radius-card); /* 20px */
border: 1px solid var(--lx-border);
padding: 20px;
display: flex;
flex-direction: column;
gap: 16px;
```

Sub-section heading:
```css
font-size: 14px;
font-weight: 600;
color: var(--lx-fg3);
text-transform: uppercase;
letter-spacing: 0.06em;
font-family: Poppins, inherit;
```

### E.2 — Metric tab strip (within each sub-section)

Three tabs to select which metric the bars show: Lessons / Attempts / Students.
Tab strip is a horizontal `flexDirection: 'row'` group of pill buttons.

Active tab style:
```css
background-color: rgba(79,70,229,0.18);   /* $primarySoft */
color: var(--lx-primary);                 /* #4F46E5 */
border: 1px solid rgba(79,70,229,0.3);
border-radius: 9999px;
height: 30px;
padding-inline: 14px;
font-size: 13px;
font-weight: 600;
```

Inactive tab style:
```css
background-color: transparent;
color: var(--lx-fg3);
border: 1px solid var(--lx-border);
border-radius: 9999px;
height: 30px;
padding-inline: 14px;
font-size: 13px;
font-weight: 400;
```

Hover (inactive): `background-color: var(--lx-card-soft)` (120 ms). No glow.

State is component-local `useState` (no Zustand — these tabs don't affect the API call, just which array column is charted).

### E.3 — BarBreakdown chart wrapper spec

```tsx
// apps/admin-dashboard/components/charts/BarBreakdown.tsx
interface BarBreakdownProps {
  data: Array<{ name: string; value: number }>;  // name = display label, value = selected metric
  height?: number;           // default 220
  barColor?: string;         // default 'var(--lx-primary)' (#4F46E5)
  emptyLabel?: string;
  testId?: string;
}
```

Recharts config (token-driven, NOT Recharts defaults):
```jsx
<ResponsiveContainer width="100%" height={height ?? 220}>
  <BarChart data={data} margin={{ top: 4, right: 8, left: 0, bottom: 4 }}>
    <CartesianGrid
      vertical={false}
      stroke="var(--lx-border)"
      strokeDasharray="4 4"
    />
    <XAxis
      dataKey="name"
      tick={{ fill: 'var(--lx-fg3)', fontSize: 12, fontFamily: 'Poppins, sans-serif' }}
      axisLine={false}
      tickLine={false}
    />
    <YAxis
      tick={{ fill: 'var(--lx-fg3)', fontSize: 12, fontFamily: 'Poppins, sans-serif' }}
      axisLine={false}
      tickLine={false}
      allowDecimals={false}
      width={50}
    />
    <Tooltip
      contentStyle={{
        backgroundColor: 'var(--lx-card)',
        border: '1px solid var(--lx-border)',
        borderRadius: 8,
        fontSize: 13,
        fontFamily: 'Poppins, sans-serif',
        color: 'var(--lx-fg1)',
      }}
      cursor={{ fill: 'rgba(79,70,229,0.08)' }}
    />
    <Bar
      dataKey="value"
      fill={barColor ?? 'var(--lx-primary)'}
      radius={[4, 4, 0, 0]}
      maxBarSize={48}
    />
  </BarChart>
</ResponsiveContainer>
```

Empty state (data.length === 0 or all values are 0):
```css
/* Centered message in the chart container */
height: 220px;
display: flex;
align-items: center;
justify-content: center;
color: var(--lx-fg3);
font-size: 13px;
```

`data-testid` on wrapper `<div>`: pass through `testId` prop.

### E.4 — Data transformation (FE-local)

The DTO carries `bySubject[]` as `{ subjectCode: int, language: int, lessonsCompleted, totalAttempts, distinctActiveStudents }`. When the "By Subject" panel is shown with "All Languages" selected, sum across language codes per subjectCode. When a language slice is active, filter to only rows matching that language before aggregating.

Subject code → display name mapping (same as filter slice mapping in Part C):
```
1 → "Math"    2 → "Science"    3 → "Arabic"    4 → "English"
```

Grade code → display name: `"Grade {gradeId}"` (no mapping needed).

Language code → display name: `0 → "Arabic"  1 → "English"`.

Bar colors per group (use consistent per-series color to match brand primaries):
- By Subject: `#4F46E5` (indigo/primary)
- By Grade: `#22C55E` (green/secondary)
- By Language: `#F59E0B` (amber/accent)

### E.5 — Subscriptions by tier (inline below KPI card row, not a chart)

The `subscriptionsByTier[]` array (`{ planCode: string, count: int }`) renders as a compact list below the subscriptions KPI card (or as a small supplement panel):

```css
/* InlineTierBreakdown — small flex-col inside the subscriptions card */
display: flex;
flex-direction: column;
gap: 4px;
margin-top: 8px;
border-top: 1px solid var(--lx-border);
padding-top: 8px;
```

Each row: `<span planCode 12px fg3>  ·  <span count 12px fg1 tabular-nums>`.

### E.6 — Accessible chart fallback

Each `BarBreakdown` chart is accompanied by a visually hidden `<table>` (`.sr-only`) with the same data for screen readers:
```
<caption class="sr-only">{sectionHeading} breakdown</caption>
<thead><tr><th>Name</th><th>Value</th></tr></thead>
<tbody>{data.map(row => <tr><td>{row.name}</td><td>{row.value}</td></tr>)}</tbody>
```

### E.7 — testIDs

| Element | testID |
|---|---|
| By Subject chart wrapper | `analytics-chart-by-subject` |
| By Grade chart wrapper | `analytics-chart-by-grade` |
| By Language chart wrapper | `analytics-chart-by-language` |
| Subscriptions tier list | `analytics-subscriptions-tier` |
| Metric tab Lessons | `analytics-tab-lessons` |
| Metric tab Attempts | `analytics-tab-attempts` |
| Metric tab Students | `analytics-tab-students` |

---

## PART F — Loading Skeleton

**Shown when:** `isLoading && !hasDateRangeError`
`data-testid="analytics-loading"`, `role="status"`, `aria-label={strings.analyticsLoadingLabel}`.

KPI skeleton: 4-column CSS grid; each cell is a shimmer `<div>` `height: 110px; border-radius: 20px` using `lx-shimmer` animation (gradient `var(--lx-card-soft) → var(--lx-card) → var(--lx-card-soft)`, `background-size: 400px 100%`, `animation: lx-shimmer 1400ms linear infinite`). Show 8 skeleton cards.

Chart skeleton: two `<div>` shimmer blocks, `height: 260px; border-radius: 20px; margin-top: 24px`.

Skeleton pattern mirrors `AdminLoadingSkeleton.tsx` and the audit page `AuditSkeletonRow` pattern exactly.

---

## PART G — Error State

```
<Stack flexDirection="column" gap="$4" data-testid="analytics-error-banner">
  <AdminErrorBanner variant="error" message={strings.analyticsLoadError} />
  <button
    type="button"
    data-testid="analytics-retry"
    onClick={() => void refetch()}
    style={{
      alignSelf: 'flex-start',
      height: 36, paddingInline: 16,
      borderRadius: 'var(--lx-radius-button)',
      border: '1px solid var(--lx-border)',
      backgroundColor: 'transparent',
      color: 'var(--lx-fg2)',
      fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
    }}
  >
    {strings.analyticsRetry}
  </button>
</Stack>
```

Uses existing `AdminErrorBanner` component unchanged (variant `"error"`).

---

## PART H — Empty State

Shown when `!isLoading && !isError && data returned but all key metrics are 0` OR the endpoint returns a `kpis` payload that is semantically empty (all breakdown arrays are length 0 and all counters are 0).

```
<Stack
  alignItems="center" justifyContent="center"
  padding={40} gap="$4"
  data-testid="analytics-empty"
  style={{
    backgroundColor: 'var(--lx-card)',
    borderRadius: 'var(--lx-radius-card)',  /* 20px */
    border: '1px solid var(--lx-border)',
  }}
>
  {/* Lucide bar-chart-2 inline SVG, 40×40, color: var(--lx-fg3) */}
  <Text fontFamily="$heading" fontSize={18} fontWeight="600" color="$fg1">
    {strings.analyticsEmptyHeading}
  </Text>
  <Text fontFamily="$body" fontSize={14} color="$fg3"
        style={{ textAlign: 'center', maxWidth: 320, lineHeight: 1.5 }}>
    {strings.analyticsEmptyBody}
  </Text>
</Stack>
```

---

## PART I — Hook and queryKey contract

**Hook file:** `packages/api-client/src/admin/analytics.ts`
**Hook name:** `usePlatformKpis(from?: string, to?: string)`
**Pattern:** clone the `useAuditLog` structure; use `client.get` (not `getPaginated`).
**queryKey:** `queryKeys.adminAnalytics.kpis(from, to)` — add to `packages/api-client/src/query/queryKeys.ts`:

```ts
adminAnalytics: {
  all: ['adminAnalytics'] as const,
  kpis: (from?: string, to?: string) =>
    [...queryKeys.adminAnalytics.all, 'kpis', { from, to }] as const,
},
```

Hook shape:
```ts
export function usePlatformKpis(
  from?: string,
  to?: string,
): UseQueryResult<PlatformKpiSummaryDto, Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminAnalytics.kpis(from, to),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.get<PlatformKpiSummaryDto>('/api/Admin/Analytics/kpis', {
        query: { from, to },
        signal,
      }),
  });
}
```

Params are sent as camelCase (`from`, `to`) matching the controller binding in the verified contract.

**FE-local DTO type** (hand-written, no NSwag):
```ts
export interface SubjectBreakdown {
  subjectCode: number;
  language: number;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
}

export interface GradeBreakdown {
  gradeId: number;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
}

export interface LanguageBreakdown {
  language: number;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
}

export interface SubscriptionTierCount {
  planCode: string;
  count: number;
}

export interface PlatformKpiSummaryDto {
  fromUtc: string;
  toUtc: string;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
  quizzesCompletedNaReason: string | null;
  bySubject: SubjectBreakdown[];
  byGrade: GradeBreakdown[];
  byLanguage: LanguageBreakdown[];
  missionsCompleted: number;
  xpEarnedInWindow: number;
  totalActiveSubscriptions: number;
  subscriptionsByTier: SubscriptionTierCount[];
  revenueNaReason: string | null;
  totalAiSafetyEvents: number;
  aiBlockedCount: number;
  aiFlaggedCount: number;
  aiRequestVolume: number;
  aiRequestVolumeNaReason: string | null;
  analyticsActiveStudents: number;
  totalSessions: number;
  avgSessionDurationSeconds: number;
  avgActiveDaysPerStudent: number;
  returningStudentRate: number;
  retentionNaReason: string | null;
  sessionDurationNaReason: string | null;
}
```

---

## PART J — Accessibility

- All inputs have `<label htmlFor="...">` with `className="sr-only"` (mirror audit page pattern).
- `<div aria-live="polite" aria-atomic="false">` wraps the entire results area so state transitions are announced.
- `<div role="status" aria-label={strings.analyticsLoadingLabel}>` on the skeleton wrapper.
- `role="alert"` on the date-range error message.
- KPI cards: each card `<div>` gets `role="region" aria-label={strings.kpiCardLabel}`.
- Chart containers each have a visually hidden `<table>` fallback (Part E.6).
- Focus ring on all interactive controls: `box-shadow: var(--lx-focus-ring-admin)`.
- Color is never the sole signal for N/A — the text "N/A" always accompanies any amber tint.

---

## PART K — Scope notes for frontend agent

1. **No `useKpiTrend` hook.** There is no `/Analytics/trend` endpoint. The "trend charts" from the FE task file are re-scoped to the categorical breakdown bars in Part E. Do not add a `useKpiTrend` hook or call any trend endpoint.
2. **Breakdown arrays are embedded in the KPI DTO.** No extra API call for breakdowns; `usePlatformKpis` fetches everything.
3. **`quizzesCompletedNaReason`** is a nullable string, not a count. There is no `quizzesCompleted` integer field — render this card as N/A-only when the reason is present.
4. **Subject/grade/language slice selects** are client-side only (they filter/highlight chart data, they do NOT re-fetch).
5. **Date-range params** (`from`/`to`) use camelCase as sent to the controller — see verified contract.
6. **Notifications endpoint** (`/Analytics/notifications`) is P9-11 scope — do not wire.
7. **Recharts must be pinned `^2`** — Recharts 3 breaks React 18.3.1.
8. **Chart wrapper stays app-local** at `apps/admin-dashboard/components/charts/` — do not promote to `packages/ui` this cycle.

---

## Implementation handoff

| Deliverable | File path | Notes |
|---|---|---|
| Nav entry | `apps/admin-dashboard/components/AdminSideNav.tsx` | Append to `NAV_ITEMS` |
| New strings keys | `apps/admin-dashboard/lib/strings.ts` | EN + AR per Part A.3 |
| queryKey namespace | `packages/api-client/src/query/queryKeys.ts` | Add `adminAnalytics` |
| API hook | `packages/api-client/src/admin/analytics.ts` | `usePlatformKpis` + FE-local DTOs |
| Page shell | `apps/admin-dashboard/app/(admin)/analytics/page.tsx` | `useAdminGuard`, 4 states |
| Filter component | `apps/admin-dashboard/components/analytics/AnalyticsFilters.tsx` | Date + slice |
| KPI cards | `apps/admin-dashboard/components/analytics/KpiCards.tsx` | N/A handling |
| Breakdown charts | `apps/admin-dashboard/components/analytics/KpiBreakdownCharts.tsx` | Recharts |
| Bar chart wrapper | `apps/admin-dashboard/components/charts/BarBreakdown.tsx` | Shared by P7-11 |
| Recharts dep | `apps/admin-dashboard/package.json` | `"recharts": "^2"` |

---

## Design gaps / open questions

1. **No `/Analytics/trend` endpoint** — the FE task file's "trend charts (time series)" are unsatisfiable this cycle. Categorical breakdown bars are the approved substitute (per Execution Plan OQ-4 resolution).
2. **`quizzesCompletedNaReason` with no count field** — the KPI card for quizzes is N/A-only if that reason is present. There is no quiz count to display.
3. **No screenshot capture for this admin route** — spec is derived from tokens + existing surfaces only. This is the same condition as all Phase-7 admin specs.
4. **Recharts `<Tooltip>` interaction is mouse-only** — keyboard/screen-reader access is covered by the `<table>` fallback (Part E.6). No additional keyboard chart interaction is required.
