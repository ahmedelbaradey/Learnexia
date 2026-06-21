# Design Spec — P7-11 AI-Safety & Quality Monitoring Dashboard

**Surface:** `apps/admin-dashboard` (Next.js 15, desktop-first, dark theme, LTR)
**Route:** `app/(admin)/ai-safety/page.tsx`
**Token authority:** `design-system/colors_and_type.css` + shipped `--lx-*` variable set
**Shell authority:** `P7-12-FE.md` / `P7-admin-users-wave.md` — AdminShell, same token language; all below is additive
**Locale:** `ADMIN_LOCALE = 'en'`, `dir="ltr"`. EN ships; AR strings authored for readiness.
**Theme:** Dark default (`$bg #0F172A`). No gamification chrome, no reward gradients, no confetti.
**Read-only:** ZERO mutation affordances on this surface.
**Child-safety note:** This surface shows AI safety aggregates only. No raw prompt/response text, no per-child PII. The flagged-outputs table shows only the minimum (contentRef id, taskKind, actionTaken, reasonCodes, failedChecks, modelId, occurredAtUtc). PII-light by design. The `security-auditor` agent reviews this surface before the reviewer gate.

> No new screenshot captures exist for this admin route. Spec is derived from token set + existing shipped surfaces (`audit/page.tsx`, `moderation/page.tsx`, `gamification/page.tsx`) and the verified endpoint DTOs in `docs/briefs/P7-10-P7-11-admin-dashboards.md`.

---

## Admin surface design principles (enforce identically to P7-12)

1. No XP bars, reward gradients, confetti, mascot, or Level-Up fills anywhere.
2. Motion minimal: hover 120 ms `var(--lx-ease-out)`, skeleton shimmer 1400 ms linear. No spring easing.
3. No primary CTA — read-only; no hero button.
4. Typography: Poppins (EN body + headings), Cairo (AR display), Tajawal (AR body).
5. Card padding 20 px; table cell padding `12px 16px`; section gap 24 px.
6. All numbers rendered, never spelled out. `font-variant-numeric: tabular-nums` on all metrics.
7. Threshold breach is a **critical-severity** visual signal — red/amber banner, never a silent number.

---

## PART A — Nav entry: "AI Safety" in `AdminSideNav`

**File:** `apps/admin-dashboard/components/AdminSideNav.tsx` (additive — append after `analytics` entry)

### A.1 — New `RealNavItem`

```
{
  kind: 'real',
  key: 'aiSafety',
  label: strings.navAiSafety,       // EN: "AI Safety" / AR: "سلامة الذكاء الاصطناعي"
  icon: '🛡️',                       // emoji placeholder (same pattern as existing items)
  href: '/ai-safety',
  activePrefix: '/ai-safety',
}
```

Icon note: emoji placeholder per existing nav pattern. Future upgrade target: Lucide `shield-check` (18 px, 2 px stroke, rounded caps).

### A.2 — Active state tokens (mirror audit/gamification)

| State | Background | Label color | Label weight |
|---|---|---|---|
| Active | `$primarySoft` `rgba(79,70,229,0.18)` | `$primary` `#4F46E5` | 600 |
| Inactive | `transparent` | `$fg2` `#CBD5E1` | 400 |
| Hover inactive | `$card` `#1E293B` | `$fg2` | 400 |

Transition: `background-color var(--lx-dur-fast) var(--lx-ease-out)` (120 ms).
Active detection: `pathname === '/ai-safety' || pathname.startsWith('/ai-safety/')`.
`aria-current="page"` on active link. `data-testid="nav-ai-safety"`.

### A.3 — New strings keys

```ts
// AdminStrings interface additions:
navAiSafety: string
// EN: "AI Safety"    AR: "سلامة الذكاء الاصطناعي"    i18n: admin.nav.aiSafety

aiSafetyPageTitle: string
// EN: "AI Safety"    AR: "سلامة الذكاء الاصطناعي"    i18n: admin.aiSafety.pageTitle

aiSafetyPageHeading: string
// EN: "AI Safety & Quality Monitoring"   AR: "مراقبة سلامة وجودة الذكاء الاصطناعي"

aiSafetyPageSubtitle: string
// EN: "Safety signals, eval pass/fail, tutor usage, and flagged outputs."
// AR: "إشارات السلامة ومعدلات النجاح والفشل في التقييم واستخدام المعلم والمخرجات المُعلَّمة."

aiSafetyLoadingLabel: string
// EN: "Loading AI safety data..."    AR: "جارٍ تحميل بيانات سلامة الذكاء الاصطناعي..."

aiSafetyLoadError: string
// EN: "Failed to load AI safety data."   AR: "فشل تحميل بيانات سلامة الذكاء الاصطناعي."

aiSafetyRetry: string
// EN: "Retry"   AR: "إعادة المحاولة"

aiSafetyDateFrom: string
// EN: "From"    AR: "من"

aiSafetyDateTo: string
// EN: "To"      AR: "إلى"

aiSafetyDateRangeError: string
// EN: "'To' must be after 'From'."   AR: "يجب أن يكون 'إلى' بعد 'من'."

aiSafetyClearFilters: string
// EN: "Clear"   AR: "مسح"

// ── Safety Signals section ──
aiSafetySignalsHeading: string
// EN: "Safety Signals"   AR: "إشارات السلامة"

aiSafetySignalsTotalEvents: string
// EN: "Total Events"   AR: "إجمالي الأحداث"

aiSafetySignalsBlocked: string
// EN: "Blocked"   AR: "محجوبة"

aiSafetySignalsRegenerated: string
// EN: "Regenerated"   AR: "مُعاد توليدها"

aiSafetySignalsFallback: string
// EN: "Fallback Returned"   AR: "استجابة بديلة"

aiSafetySignalsBlockedRate: string
// EN: "Block Rate"   AR: "معدل الحجب"

aiSafetySignalsRegeneratedRate: string
// EN: "Regen Rate"   AR: "معدل إعادة التوليد"

aiSafetySignalsFallbackRate: string
// EN: "Fallback Rate"   AR: "معدل الاستجابة البديلة"

aiSafetyBreakdownByAction: string
// EN: "By Action"   AR: "حسب الإجراء"

aiSafetyBreakdownByReason: string
// EN: "By Reason Code"   AR: "حسب رمز السبب"

aiSafetyBreakdownByModel: string
// EN: "By Model"   AR: "حسب النموذج"

aiSafetyBreakdownByTaskKind: string
// EN: "By Task Kind"   AR: "حسب نوع المهمة"

// ── Safety Trend section ──
aiSafetyTrendHeading: string
// EN: "Safety Event Trend"   AR: "اتجاه أحداث السلامة"

aiSafetyTrendTotal: string
// EN: "Total"   AR: "الإجمالي"

aiSafetyTrendBlocked: string
// EN: "Blocked"   AR: "محجوبة"

aiSafetyTrendRegenerated: string
// EN: "Regenerated"   AR: "مُعاد توليدها"

aiSafetyTrendFallback: string
// EN: "Fallback"   AR: "بديلة"

aiSafetyTrendEmpty: string
// EN: "No trend data for this range."   AR: "لا توجد بيانات اتجاه لهذه الفترة."

// ── Eval Results section ──
aiSafetyEvalHeading: string
// EN: "Eval Results"   AR: "نتائج التقييم"

aiSafetyEvalPassRate: string
// EN: "Pass Rate"   AR: "معدل النجاح"

aiSafetyEvalThreshold: string
// EN: "Threshold"   AR: "العتبة"

aiSafetyEvalTier: string
// EN: "Tier"   AR: "المستوى"

aiSafetyEvalNote: string
// EN: "Note"   AR: "ملاحظة"

aiSafetyEvalByCheck: string
// EN: "By Check"   AR: "حسب الفحص"

aiSafetyEvalBySubject: string
// EN: "By Subject"   AR: "حسب المادة"

aiSafetyEvalByLanguage: string
// EN: "By Language"   AR: "حسب اللغة"

aiSafetyEvalBreachBadge: string
// EN: "THRESHOLD BREACH"   AR: "خرق العتبة"

aiSafetyEvalBreachDetail: string
// EN: "Pass rate is below the required threshold."
// AR: "معدل النجاح أقل من العتبة المطلوبة."

aiSafetyEvalSentinelHeading: string
// EN: "No Eval Run Yet"   AR: "لم يُجرَ أي تقييم بعد"

aiSafetyEvalSentinelBody: string
// EN: "Run the AI eval harness to populate this panel."
// AR: "شغّل اختبارات التقييم لملء هذا القسم."

aiSafetyEvalRunId: string
// EN: "Run ID"   AR: "معرّف التشغيل"

aiSafetyEvalRanAt: string
// EN: "Ran at"   AR: "وقت التشغيل"

aiSafetyEvalTotalCases: string
// EN: "Total Cases"   AR: "إجمالي الحالات"

aiSafetyEvalPassed: string
// EN: "Passed"   AR: "ناجحة"

aiSafetyEvalFailed: string
// EN: "Failed"   AR: "فاشلة"

// ── Tutor Usage section ──
aiSafetyUsageHeading: string
// EN: "Tutor Usage & Cost"   AR: "استخدام المعلم والتكلفة"

aiSafetyUsageTotalCalls: string
// EN: "Total Calls"   AR: "إجمالي الاستدعاءات"

aiSafetyUsagePromptTokens: string
// EN: "Prompt Tokens"   AR: "رموز الإدخال"

aiSafetyUsageCompletionTokens: string
// EN: "Completion Tokens"   AR: "رموز الإتمام"

aiSafetyUsageCost: string
// EN: "Est. Cost (USD)"   AR: "التكلفة التقديرية (دولار)"

aiSafetyUsageAvgLatency: string
// EN: "Avg. Latency (ms)"   AR: "متوسط وقت الاستجابة (مللي ثانية)"

aiSafetyUsageCacheHit: string
// EN: "Cache Hit Rate"   AR: "معدل إصابة الذاكرة المؤقتة"

aiSafetyUsageByModel: string
// EN: "By Model"   AR: "حسب النموذج"

aiSafetyUsageByTaskKind: string
// EN: "By Task Kind"   AR: "حسب نوع المهمة"

aiSafetyUsageCostTrend: string
// EN: "Daily Cost Trend"   AR: "اتجاه التكلفة اليومية"

aiSafetyUsageEmpty: string
// EN: "No usage data for this range."   AR: "لا توجد بيانات استخدام لهذه الفترة."

// ── Flagged Outputs section ──
aiSafetyFlaggedHeading: string
// EN: "Flagged Outputs"   AR: "المخرجات المُعلَّمة"

aiSafetyFlaggedColContentRef: string
// EN: "Ref. ID"   AR: "المعرّف"

aiSafetyFlaggedColTaskKind: string
// EN: "Task Kind"   AR: "نوع المهمة"

aiSafetyFlaggedColAction: string
// EN: "Action"   AR: "الإجراء"

aiSafetyFlaggedColReasonCodes: string
// EN: "Reason Codes"   AR: "رموز الأسباب"

aiSafetyFlaggedColFailedChecks: string
// EN: "Failed Checks"   AR: "الفحوصات الفاشلة"

aiSafetyFlaggedColModel: string
// EN: "Model"   AR: "النموذج"

aiSafetyFlaggedColOccurredAt: string
// EN: "Occurred At"   AR: "وقت الحدوث"

aiSafetyFlaggedFilterAction: string
// EN: "Filter by action"   AR: "تصفية حسب الإجراء"

aiSafetyFlaggedFilterReason: string
// EN: "Filter by reason"   AR: "تصفية حسب السبب"

aiSafetyFlaggedFilterTaskKind: string
// EN: "Filter by task kind"   AR: "تصفية حسب النوع"

aiSafetyFlaggedAllActions: string
// EN: "All Actions"   AR: "جميع الإجراءات"

aiSafetyFlaggedAllReasons: string
// EN: "All Reasons"   AR: "جميع الأسباب"

aiSafetyFlaggedAllTaskKinds: string
// EN: "All Task Kinds"   AR: "جميع الأنواع"

aiSafetyFlaggedEmpty: string
// EN: "No flagged outputs for this range."   AR: "لا توجد مخرجات مُعلَّمة لهذه الفترة."

aiSafetyFlaggedLoadError: string
// EN: "Failed to load flagged outputs."   AR: "فشل تحميل المخرجات المُعلَّمة."

aiSafetyFlaggedTableCaption: string
// EN: "Flagged AI outputs (PII-light)"   AR: "مخرجات الذكاء الاصطناعي المُعلَّمة"

aiSafetyPrevPage: string
// EN: "Previous page"   AR: "الصفحة السابقة"

aiSafetyNextPage: string
// EN: "Next page"   AR: "الصفحة التالية"
```

---

## PART B — Page Layout (`/ai-safety`)

**File:** `apps/admin-dashboard/app/(admin)/ai-safety/page.tsx`
**Directive:** `'use client'; export const dynamic = 'force-dynamic';`

### B.1 — Outer structure

Wrapped in `<AdminShell title={strings.aiSafetyPageTitle}>`.

The page has five data-driven sections plus the global filter bar. Sections are stacked vertically with `gap: 24px`:

```
<AdminShell title={strings.aiSafetyPageTitle}>
  <Stack flexDirection="column" gap="$6">          {/* 24 px between sections */}
    <PageHeader />
    <AiSafetyFilters />                            {/* global date-range (Zustand) */}
    <DateRangeErrorBanner />                       {/* conditional */}

    {/* All panels share the date range from Zustand */}
    <SafetySignals />                              {/* signals + breakdown bars */}
    <SafetyTrend />                                {/* per-day line chart */}
    <EvalResults />                                {/* pass/fail + breach */}
    <TutorUsage />                                 {/* usage + cost */}
    <FlaggedOutputsTable />                        {/* paginated table */}
  </Stack>
</AdminShell>
```

**Each section is independently queryable** — they each call their own hook with the shared date range from the Zustand store. A failure in one section renders that section's error state without collapsing the entire page.

### B.2 — Page header

Same pattern as P7-10 Part B.2:
```
<Text fontFamily="$heading" fontSize={24} fontWeight="700" color="$fg1"
      data-testid="ai-safety-page-heading">
  {strings.aiSafetyPageHeading}
</Text>
<Text fontFamily="$body" fontSize={14} color="$fg3" style={{ marginTop: 4 }}>
  {strings.aiSafetyPageSubtitle}
</Text>
```

---

## PART C — Filter Bar (`AiSafetyFilters`)

**File:** `apps/admin-dashboard/components/ai-safety/AiSafetyFilters.tsx`
**State:** Zustand v5 store. Holds `{ from: string; to: string }`. The store is consumed by all five panel components. Flagged-outputs pagination state lives locally in `FlaggedOutputsTable`.

### C.1 — Filter bar layout

Horizontal flex row, `flexWrap: 'wrap'`, `gap: 12px`, `alignItems: 'flex-end'`.

Contains:
1. Date "From" input (`type="date"`, width 160 px)
2. Date "To" input (`type="date"`, width 160 px)
3. Clear button (shown when from or to is non-empty)

Date params for AI Safety hooks use PascalCase `From`/`To` (matches the controller binding; distinct from P7-10's camelCase `from`/`to`).

### C.2 — Input / select styles

Identical to the existing pattern from `audit/page.tsx` (Part C.2 of P7-10-analytics.md):
```css
height: 44px;
background-color: var(--lx-bg);
border-radius: var(--lx-radius-sm);   /* 8px */
border: 1px solid var(--lx-border);
color: var(--lx-fg1);
padding-inline: 12px;
font-size: 14px;
font-family: inherit;
```

Focus: `border-color: var(--lx-primary); box-shadow: var(--lx-focus-ring-admin)`.
Date error (To < From): `border-color: var(--lx-danger)` + `role="alert"` error message below bar.

### C.3 — testIDs

| Element | testID |
|---|---|
| From date | `ai-safety-filter-date-from` |
| To date | `ai-safety-filter-date-to` |
| Clear | `ai-safety-clear-filters` |
| Date error | `ai-safety-date-range-error` |

---

## PART D — Safety Signals Section (`SafetySignals`)

**File:** `apps/admin-dashboard/components/ai-safety/SafetySignals.tsx`
**Hook:** `useSafetySignals(From?, To?)` → `SafetySignalSummaryDto`
**Endpoint:** `GET /api/Admin/AiSafety/signals?From=&To=`

### D.1 — Section card

```css
background-color: var(--lx-card);     /* #1E293B */
border-radius: var(--lx-radius-card); /* 20px */
border: 1px solid var(--lx-border);
padding: 20px;
display: flex;
flex-direction: column;
gap: 20px;
```

Section heading: `font-size: 14px; font-weight: 600; color: var(--lx-fg3); text-transform: uppercase; letter-spacing: 0.06em`.

### D.2 — Signal KPI cards (inner grid, 3-column)

Sub-grid `display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px` inside the section card.

**The 6 metric cards (3 counts + 3 rates):**

| Field | Label key | Card border-left | Value color |
|---|---|---|---|
| `totalEvents` | `aiSafetySignalsTotalEvents` | none | `var(--lx-fg1)` |
| `blockedCount` | `aiSafetySignalsBlocked` | `3px solid #EF4444` | `#EF4444` |
| `regeneratedCount` | `aiSafetySignalsRegenerated` | `3px solid #F59E0B` | `#F59E0B` |
| `fallbackReturnedCount` | `aiSafetySignalsFallback` | `3px solid #A855F7` | `#A855F7` |
| `blockedRate` (×100, 1 decimal, append "%") | `aiSafetySignalsBlockedRate` | none | `#EF4444` |
| `regeneratedRate` | `aiSafetySignalsRegeneratedRate` | none | `#F59E0B` |

`fallbackReturnedRate` is rendered inline with the fallback count card as a secondary line.

Mini-card anatomy (compact, not the full KPI card from P7-10):
```css
background-color: var(--lx-card-soft);   /* #334155 (step lighter) */
border-radius: 12px;
padding: 12px 16px;
border-left: 3px solid {borderColor};    /* varies per metric, or none */
display: flex;
flex-direction: column;
gap: 4px;
```

Label:
```css
font-size: 12px; font-weight: 500; color: var(--lx-fg3); line-height: 1.4;
```

Value:
```css
font-size: 22px; font-weight: 800; font-variant-numeric: tabular-nums;
color: {valueColor};
```

`data-testid="signal-card-{slug}"` (e.g. `signal-card-total`, `signal-card-blocked`, `signal-card-regen`, `signal-card-fallback`, `signal-card-block-rate`, `signal-card-regen-rate`).

### D.3 — Breakdown bars (4 panels)

Below the signal KPI cards, four breakdown bar panels in a 2-column grid at laptop+ (`grid-template-columns: repeat(2, 1fr); gap: 16px`).

Each panel uses the shared `BarBreakdown` chart wrapper (defined in P7-10 Part E.3).

| Panel | Data source | Bar color | testID |
|---|---|---|---|
| By Action | `signals.breakdownByAction` (label, count) | `#EF4444` | `signal-breakdown-action` |
| By Reason Code | `signals.breakdownByReasonCode` | `#F59E0B` | `signal-breakdown-reason` |
| By Model ID | `signals.breakdownByModelId` | `#6366F1` | `signal-breakdown-model` |
| By Task Kind | `signals.breakdownByTaskKind` | `#22C55E` | `signal-breakdown-taskkind` |

`CountBreakdownDto` = `{ label: string, count: int }` — already in display form (no mapping needed).

Chart height: 180 px. No metric tab strip (single metric: count). Panel heading follows D.1 heading style.

**Important:** `signals` endpoint has NO subject or language breakdown — do NOT add subject/language panels here. The DTO simply does not carry those dimensions.

### D.4 — States

- Loading: shimmer skeleton — 4 mini-card placeholders + 4 chart placeholders. `data-testid="signals-loading"`, `role="status"`.
- Error: `<AdminErrorBanner variant="error" message={strings.aiSafetyLoadError} />` + retry button. `data-testid="signals-error"`.
- Empty (`totalEvents === 0` and all breakdowns are empty): centered empty message in the section card. `data-testid="signals-empty"`.
- Results: default. `data-testid="signals-results"`.

---

## PART E — Safety Trend Section (`SafetyTrend`)

**File:** `apps/admin-dashboard/components/ai-safety/SafetyTrend.tsx`
**Hook:** `useSafetyTrend(From?, To?)` → `SafetyTrendBucketDto[]` (bare list in `.data`)
**Endpoint:** `GET /api/Admin/AiSafety/trend?From=&To=`

### E.1 — Section card

Same card shell as D.1. Heading: "Safety Event Trend" (`strings.aiSafetyTrendHeading`).

### E.2 — TrendLine chart wrapper

**File:** `apps/admin-dashboard/components/charts/TrendLine.tsx`

```tsx
interface TrendLineProps {
  data: Array<{ date: string; [seriesKey: string]: number | string }>;
  series: Array<{ key: string; name: string; color: string }>;
  height?: number;             // default 260
  dateFormatter?: (d: string) => string;
  emptyLabel?: string;
  testId?: string;
}
```

Recharts config:
```jsx
<ResponsiveContainer width="100%" height={height ?? 260}>
  <LineChart data={data} margin={{ top: 8, right: 16, left: 0, bottom: 4 }}>
    <CartesianGrid
      stroke="var(--lx-border)"
      strokeDasharray="4 4"
    />
    <XAxis
      dataKey="date"
      tickFormatter={dateFormatter ?? (d => d.slice(5))}  /* "MM-DD" */
      tick={{ fill: 'var(--lx-fg3)', fontSize: 11, fontFamily: 'Poppins, sans-serif' }}
      axisLine={false}
      tickLine={false}
    />
    <YAxis
      tick={{ fill: 'var(--lx-fg3)', fontSize: 11, fontFamily: 'Poppins, sans-serif' }}
      axisLine={false}
      tickLine={false}
      allowDecimals={false}
      width={44}
    />
    <Tooltip
      contentStyle={{
        backgroundColor: 'var(--lx-card)',
        border: '1px solid var(--lx-border)',
        borderRadius: 8, fontSize: 13,
        fontFamily: 'Poppins, sans-serif',
        color: 'var(--lx-fg1)',
      }}
    />
    <Legend
      wrapperStyle={{ fontSize: 12, color: 'var(--lx-fg3)', fontFamily: 'Poppins, sans-serif' }}
    />
    {series.map(s => (
      <Line
        key={s.key}
        dataKey={s.key}
        name={s.name}
        stroke={s.color}
        strokeWidth={2}
        dot={false}
        activeDot={{ r: 4, strokeWidth: 0 }}
      />
    ))}
  </LineChart>
</ResponsiveContainer>
```

### E.3 — Series configuration for Safety Trend

```
[
  { key: 'totalCount',       name: strings.aiSafetyTrendTotal,       color: '#6366F1' },
  { key: 'blockedCount',     name: strings.aiSafetyTrendBlocked,     color: '#EF4444' },
  { key: 'regeneratedCount', name: strings.aiSafetyTrendRegenerated, color: '#F59E0B' },
  { key: 'fallbackReturnedCount', name: strings.aiSafetyTrendFallback, color: '#A855F7' },
]
```

Data transformation: map `SafetyTrendBucketDto[]` → `{ date: bucketDate, totalCount, blockedCount, regeneratedCount, fallbackReturnedCount }[]`.

`data-testid="trend-chart"`.

### E.4 — States

- Loading: shimmer placeholder `height: 280px; border-radius: 20px`. `data-testid="trend-loading"`.
- Error: `AdminErrorBanner variant="error"` + retry. `data-testid="trend-error"`.
- Empty (list length 0): centered `strings.aiSafetyTrendEmpty`. `data-testid="trend-empty"`.
- Results: chart. `data-testid="trend-results"`.

### E.5 — Accessible fallback

Visually hidden `<table>` with bucketDate + 4 count columns (same pattern as P7-10 Part E.6).

---

## PART F — Eval Results Section (`EvalResults`)

**File:** `apps/admin-dashboard/components/ai-safety/EvalResults.tsx`
**Hook:** `useEvalResults()` → `EvalResultsDto` (takes NO params — endpoint is parameterless)
**Endpoint:** `GET /api/Admin/AiSafety/evals`

### F.1 — Bootstrap sentinel detection

**Critical invariant:** when `runId === '00000000-0000-0000-0000-000000000000'` (Guid.Empty) AND `totalCases === 0` AND `breached === true`, this is the **bootstrap sentinel** — meaning no eval run artifact has been committed yet. This is NOT a real threshold breach.

Detection logic:
```ts
const isBootstrapSentinel =
  data.runId === '00000000-0000-0000-0000-000000000000' &&
  data.totalCases === 0;
```

### F.2 — Bootstrap sentinel state

`data-testid="eval-sentinel"`:

```
<Stack
  alignItems="center" justifyContent="center"
  padding={40} gap="$4"
  style={{
    backgroundColor: 'var(--lx-card)',
    borderRadius: 'var(--lx-radius-card)',
    border: '1px solid var(--lx-border)',
  }}
>
  {/* Lucide flask-conical inline SVG 40×40, color var(--lx-fg3) */}
  <Text fontFamily="$heading" fontSize={18} fontWeight="600" color="$fg1">
    {strings.aiSafetyEvalSentinelHeading}
  </Text>
  <Text fontFamily="$body" fontSize={14} color="$fg3"
        style={{ textAlign: 'center', maxWidth: 360, lineHeight: 1.5 }}>
    {strings.aiSafetyEvalSentinelBody}
  </Text>
</Stack>
```

### F.3 — Real results state: threshold-breach indicator

Three distinct states for a real eval run (non-sentinel):

**STATE 1 — Breached (`data.breached === true`, not sentinel)**

`data-testid="eval-breach-banner"`:

```css
/* Breach banner */
background: rgba(239, 68, 68, 0.12);
border: 1px solid rgba(239, 68, 68, 0.35);
border-radius: 8px;
padding: 12px 16px;
display: flex; flex-direction: row; gap: 10px; align-items: flex-start;
```

Icon: inline Lucide `alert-triangle` SVG 20 × 20, `color: #EF4444`.

Breach badge pill inside the banner:
```css
display: inline-flex; align-items: center;
padding: 2px 8px; border-radius: 9999px;
background: rgba(239, 68, 68, 0.2);
color: #EF4444; font-size: 11px; font-weight: 700;
text-transform: uppercase; letter-spacing: 0.06em;
```
Text: `strings.aiSafetyEvalBreachBadge` — "THRESHOLD BREACH"

Body text below the badge:
```
{strings.aiSafetyEvalBreachDetail}  /* "Pass rate is below the required threshold." */
```
`font-size: 13px; color: var(--lx-fg2)`.

**STATE 2 — Passing (`data.breached === false`)**

`data-testid="eval-passing-banner"`:
Render a compact green confirmation strip (same structure as the breach banner but using `AdminErrorBanner variant="success"` typography):
```css
background: rgba(34, 197, 94, 0.12);
border: 1px solid rgba(34, 197, 94, 0.3);
```
Icon: Lucide `shield-check` 20 × 20, `color: #22C55E`.
Text: `"Pass rate {passRate.toFixed(1)}% meets the {thresholdPercent.toFixed(0)}% threshold."` `data-testid="eval-pass-strip"`.

### F.4 — Eval metrics cards

Below the breach/pass strip, a 4-column grid of compact metric cards (same mini-card style as D.2):

| Field | Label key | Value format | Color |
|---|---|---|---|
| `passRate` | `aiSafetyEvalPassRate` | `{x.toFixed(1)}%` | `#22C55E` if not breached, `#EF4444` if breached |
| `thresholdPercent` | `aiSafetyEvalThreshold` | `{x.toFixed(0)}%` | `var(--lx-fg1)` |
| `totalCases` | `aiSafetyEvalTotalCases` | integer | `var(--lx-fg1)` |
| `passedCases` / `failedCases` | `aiSafetyEvalPassed` / `aiSafetyEvalFailed` | integer each | `#22C55E` / `#EF4444` |

Below the cards, a metadata row (small, `font-size: 12px; color: var(--lx-fg3)`):
- Run ID: `{data.runId}` (monospace, `dir="ltr"`)
- Ran at: formatted UTC timestamp
- Tier: `{data.tier}`
- Note: `{data.note}` (if non-empty)

### F.5 — Breakdown tables (By Check / By Subject / By Language)

`byCheck`, `bySubject`, `byLanguage` are objects where each value is `EvalCheckBreakdownDto { passed, total, passRate }`. Render as three collapsible sub-sections below the metric cards.

Each breakdown renders as a compact table:
```
<table style={{ width: '100%', borderCollapse: 'collapse' }}>
  <thead>
    <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
      <th style={thStyle}>Check / Subject / Language</th>
      <th style={thStyle}>Passed</th>
      <th style={thStyle}>Total</th>
      <th style={thStyle}>Pass Rate</th>
    </tr>
  </thead>
  <tbody>
    {Object.entries(breakdown).map(([key, val]) => (
      <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
        <td style={tdStyle}>{key}</td>
        <td style={tdStyle}>{val.passed}</td>
        <td style={tdStyle}>{val.total}</td>
        <td style={tdStyle}>
          <span style={{ color: val.passRate >= thresholdPercent ? '#22C55E' : '#EF4444',
                         fontVariantNumeric: 'tabular-nums', fontWeight: 600 }}>
            {val.passRate.toFixed(1)}%
          </span>
        </td>
      </tr>
    ))}
  </tbody>
</table>
```

`thStyle`: `padding: 10px 16px; text-align: start; font-size: 11px; font-weight: 600; color: var(--lx-fg3); text-transform: uppercase; letter-spacing: 0.06em`.
`tdStyle`: `padding: 10px 16px; font-size: 13px; color: var(--lx-fg2); vertical-align: middle`.

`data-testid="eval-breakdown-check"`, `eval-breakdown-subject"`, `"eval-breakdown-language"`.

### F.6 — States

- Loading: shimmer blocks. `data-testid="eval-loading"`.
- Error: `AdminErrorBanner variant="error"` + retry. `data-testid="eval-error"`.
- Bootstrap sentinel (isBootstrapSentinel): Part F.2 state. `data-testid="eval-sentinel"`.
- Real results: F.3 + F.4 + F.5. `data-testid="eval-results"`.

---

## PART G — Tutor Usage & Cost Section (`TutorUsage`)

**File:** `apps/admin-dashboard/components/ai-safety/TutorUsage.tsx`
**Hook:** `useTutorUsage(From?, To?)` → `TutorUsageDto`
**Endpoint:** `GET /api/Admin/AiSafety/usage?From=&To=`

### G.1 — Section card (same shell as D.1)

### G.2 — Usage KPI cards (5-column grid)

| Field | Label key | Icon bg | Icon color |
|---|---|---|---|
| `totalCalls` | `aiSafetyUsageTotalCalls` | `rgba(79,70,229,0.15)` | `#6366F1` |
| `totalPromptTokens` | `aiSafetyUsagePromptTokens` | `rgba(79,70,229,0.15)` | `#6366F1` |
| `totalCompletionTokens` | `aiSafetyUsageCompletionTokens` | `rgba(79,70,229,0.15)` | `#6366F1` |
| `totalEstimatedCostUsd` (2 decimal, prefix "$") | `aiSafetyUsageCost` | `rgba(245,158,11,0.15)` | `#F59E0B` |
| `avgLatencyMs` (0 decimal, append " ms") | `aiSafetyUsageAvgLatency` | `rgba(34,197,94,0.15)` | `#22C55E` |

Sixth metric — `cacheHitRate` (×100, 1 decimal, append "%") as a compact inline chip, not a KPI card. Rendered as: `Cache hit: {rate}%` in `font-size: 12px; color: var(--lx-fg3)` below the KPI grid.

Mini-card style: same as D.2 (compact, `var(--lx-card-soft)` background, 12 px label, 22 px value). Token colors per table above. No colored left border on usage cards.

`data-testid="usage-card-{slug}"` on each card.

### G.3 — By-Model and By-Task-Kind breakdown bars

Two breakdown `BarBreakdown` panels side-by-side (2-column grid, `gap: 16px`):

| Panel | Data source | Y metric shown | Bar color | testID |
|---|---|---|---|---|
| By Model | `byModel[]` (`modelId`, `calls`, `totalTokens`, `totalEstimatedCostUsd`) | `calls` (default) | `#6366F1` | `usage-breakdown-model` |
| By Task Kind | `byTaskKind[]` (`taskKind`, `calls`, `totalTokens`, `totalEstimatedCostUsd`) | `calls` (default) | `#22C55E` | `usage-breakdown-taskkind` |

Metric tab strip (Calls / Tokens / Cost) per breakdown panel, same tab strip style as P7-10 Part E.2. Tabs switch which field (`calls` / `totalTokens` / `totalEstimatedCostUsd`) drives the bar height.

Chart height: 200 px.

### G.4 — Daily cost trend line

Below the breakdown panels, a `TrendLine` chart showing `usage.trend[]` (`{ date, calls, totalEstimatedCostUsd }`):

Series:
```
[
  { key: 'totalEstimatedCostUsd', name: strings.aiSafetyUsageCostTrend, color: '#F59E0B' }
]
```

Height: 200 px. No secondary series on this chart (calls redundancy — keep single cost line for clarity).
`data-testid="usage-cost-trend"`.

Accessible fallback: hidden `<table>` with date + cost columns.

### G.5 — Empty state

When `totalCalls === 0` and all breakdown arrays are empty: `strings.aiSafetyUsageEmpty`. `data-testid="usage-empty"`.

### G.6 — States

- Loading: shimmer. `data-testid="usage-loading"`.
- Error: banner + retry. `data-testid="usage-error"`.
- Empty: G.5.
- Results: G.2 + G.3 + G.4.

---

## PART H — Flagged Outputs Table (`FlaggedOutputsTable`)

**File:** `apps/admin-dashboard/components/ai-safety/FlaggedOutputsTable.tsx`
**Hook:** `useFlaggedOutputs(filters)` — paginated, `client.getPaginated`, clone of `useAuditLog`
**Endpoint:** `GET /api/Admin/AiSafety/flagged?Action=&ReasonCode=&TaskKind=&From=&To=&PageNumber=&PageSize=`

**PII-LIGHT INVARIANT:** Never render `studentId` on screen. Never render raw prompt/response text. Only: `contentRef` (an opaque integer ID, not a student identifier), `taskKind`, `actionTaken`, `reasonCodes[]` (string array), `failedChecks[]` (string array), `modelId`, `occurredAtUtc`.

### H.1 — Section card

Same card shell as D.1. Heading: `strings.aiSafetyFlaggedHeading`.

### H.2 — Flagged-outputs filter bar

Within the section card, a compact filter row above the table (`flex-wrap: wrap; gap: 8px; margin-bottom: 16px`):

1. Action select (width 160 px) — `strings.aiSafetyFlaggedAllActions` as default; options populated from `CountBreakdownDto.label` values seen in signals data (or hard-coded known actions: "Blocked", "Regenerated", "FallbackReturned").
2. ReasonCode select (width 180 px) — `strings.aiSafetyFlaggedAllReasons` as default; common known values: "HarmfulContent", "OffTopicContent", "LowConfidence", "PolicyViolation". Frontend can populate with values seen in signals breakdownByReasonCode.
3. TaskKind select (width 160 px) — `strings.aiSafetyFlaggedAllTaskKinds` as default; common values: "LessonExplanation", "QuizHint", "GeneralQuery".

**Note:** These filter values are sent as raw strings to the API (`Action`, `ReasonCode`, `TaskKind`). The date range comes from the global Zustand store. Filter changes reset pagination to page 1.

Select style: identical to existing `selectStyle` in `audit/page.tsx`.

`data-testid` for selects: `flagged-filter-action`, `flagged-filter-reason`, `flagged-filter-taskkind`.

### H.3 — Table structure

Table wrapper:
```css
background-color: var(--lx-card);
border-radius: var(--lx-radius-card);  /* 20px */
overflow: hidden;
border: 1px solid var(--lx-border);
```

Caption: `<caption className="sr-only">{strings.aiSafetyFlaggedTableCaption}</caption>`.

Thead row: `background-color: var(--lx-card-soft)`.
Header cell style: `padding: 12px 16px; text-align: start; font-size: 11px; font-weight: 600; color: var(--lx-fg3); text-transform: uppercase; letter-spacing: 0.06em; white-space: nowrap`.

Columns in order:
| # | Header key | Field | Width | Notes |
|---|---|---|---|---|
| 1 | `aiSafetyFlaggedColContentRef` | `contentRef` | 80 px | Monospace, `dir="ltr"`, prefix `#` |
| 2 | `aiSafetyFlaggedColTaskKind` | `taskKind` | 140 px | Plain text, `var(--lx-fg2)` |
| 3 | `aiSafetyFlaggedColAction` | `actionTaken` | 130 px | ActionBadge chip (Part H.4) |
| 4 | `aiSafetyFlaggedColReasonCodes` | `reasonCodes[]` | — | Comma-joined string, `var(--lx-fg3)` 13 px |
| 5 | `aiSafetyFlaggedColFailedChecks` | `failedChecks[]` | — | Comma-joined string, `var(--lx-fg3)` 13 px |
| 6 | `aiSafetyFlaggedColModel` | `modelId` | 120 px | Monospace, `var(--lx-fg3)` |
| 7 | `aiSafetyFlaggedColOccurredAt` | `occurredAtUtc` | 160 px | Formatted timestamp, `dir="ltr"` |

**DO NOT render `studentId`.** It must be omitted from the table entirely.

Body row style:
```css
border-bottom: 1px solid var(--lx-border);
/* hover: background-color: var(--lx-card-soft); transition: 120ms */
cursor: default;  /* read-only — no click-through */
```

### H.4 — Action badge chip

Mini inline chip for `actionTaken` string (no int mapping — value is already a string from backend):
```
"Blocked"          → background rgba(239,68,68,0.12)  color #EF4444
"Regenerated"      → background rgba(245,158,11,0.15) color #F59E0B
"FallbackReturned" → background rgba(168,85,247,0.15) color #A855F7
(any other)        → background rgba(255,255,255,0.08) color var(--lx-fg3)
```

Chip style:
```css
display: inline-flex; align-items: center;
height: 22px; padding-inline: 8px; border-radius: 9999px;
font-size: 11px; font-weight: 600; letter-spacing: 0.04em;
text-transform: uppercase;
```

`data-testid="flagged-action-badge"` on the chip.

### H.5 — Timestamp formatter

```ts
function formatFlaggedAt(iso: string): string {
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(new Date(iso));
}
```

### H.6 — Pagination

Mirror the `AuditPagination` component from `audit/page.tsx` exactly. Page-size is 20 (default), capped at 100 server-side. Local state `[page, setPage]`.

```
data-testid="flagged-pagination-prev"
data-testid="flagged-pagination-next"
data-testid="flagged-page-indicator"
```

Page indicator: `Page {page} of {totalPages}` `dir="ltr"` `font-variant-numeric: tabular-nums`.

### H.7 — Skeleton rows

5 skeleton rows; each row has 7 shimmer cells. Shimmer style matches `AuditSkeletonRow`. `data-testid="flagged-loading"`.

### H.8 — States

| Condition | Render | testID |
|---|---|---|
| Loading | Skeleton rows | `flagged-loading` |
| Error | `AdminErrorBanner variant="error"` + retry | `flagged-error` |
| Empty (no items) | Centered empty message | `flagged-empty` |
| Results | Table + pagination | `flagged-table-wrapper` |

Table opacity during refetch (pagination change or filter change): `0.6` (same pattern as audit and moderation pages). `transition: opacity 120ms`.

`data-testid="flagged-table"` on the `<table>` element.
`data-testid="flagged-row-{contentRef}"` on each `<tr>`.

---

## PART I — Hook and queryKey contracts

### I.1 — queryKey additions

```ts
// packages/api-client/src/query/queryKeys.ts — add:
adminAiSafety: {
  all: ['adminAiSafety'] as const,
  signals: (From?: string, To?: string) =>
    [...queryKeys.adminAiSafety.all, 'signals', { From, To }] as const,
  trend: (From?: string, To?: string) =>
    [...queryKeys.adminAiSafety.all, 'trend', { From, To }] as const,
  usage: (From?: string, To?: string) =>
    [...queryKeys.adminAiSafety.all, 'usage', { From, To }] as const,
  evals: () =>
    [...queryKeys.adminAiSafety.all, 'evals'] as const,
  flagged: (filters?: object) =>
    [...queryKeys.adminAiSafety.all, 'flagged', filters ?? {}] as const,
},
```

### I.2 — Hook file

**`packages/api-client/src/admin/ai-safety.ts`** — five hooks:

```ts
// Pattern: clone useAuditLog for all; use client.get for objects, client.getPaginated for flagged.
// All params PascalCase (From, To, Action, ReasonCode, TaskKind, PageNumber, PageSize).

export function useSafetySignals(From?: string, To?: string): UseQueryResult<SafetySignalSummaryDto, Error>
export function useSafetyTrend(From?: string, To?: string): UseQueryResult<SafetyTrendBucketDto[], Error>
export function useTutorUsage(From?: string, To?: string): UseQueryResult<TutorUsageDto, Error>
export function useEvalResults(): UseQueryResult<EvalResultsDto, Error>
export function useFlaggedOutputs(filters: FlaggedOutputsFilters): UseQueryResult<PaginatedResult<FlaggedOutputDto>, Error>
```

`useFlaggedOutputs` uses `client.getPaginated` and `placeholderData: keepPreviousData`. PageSize clamped to `Math.min(filters.PageSize ?? 20, 100)`.

### I.3 — FE-local DTO types (hand-written, no NSwag)

```ts
// SafetySignalSummaryDto
export interface CountBreakdownDto { label: string; count: number; }
export interface SafetySignalSummaryDto {
  from: string; to: string;
  totalEvents: number;
  blockedCount: number; blockedRate: number;
  regeneratedCount: number; regeneratedRate: number;
  fallbackReturnedCount: number; fallbackReturnedRate: number;
  breakdownByAction: CountBreakdownDto[];
  breakdownByReasonCode: CountBreakdownDto[];
  breakdownByModelId: CountBreakdownDto[];
  breakdownByTaskKind: CountBreakdownDto[];
}

// SafetyTrendBucketDto (bare list in response)
export interface SafetyTrendBucketDto {
  bucketDate: string;
  totalCount: number;
  blockedCount: number;
  regeneratedCount: number;
  fallbackReturnedCount: number;
}

// TutorUsageDto
export interface TutorUsageByModelDto {
  modelId: string;
  calls: number;
  totalTokens: number;
  totalEstimatedCostUsd: number;
}
export interface TutorUsageByTaskKindDto {
  taskKind: string;
  calls: number;
  totalTokens: number;
  totalEstimatedCostUsd: number;
}
export interface TutorUsageTrendDto { date: string; calls: number; totalEstimatedCostUsd: number; }
export interface TutorUsageDto {
  from: string; to: string;
  totalCalls: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalEstimatedCostUsd: number;   // decimal maps to number in TS
  avgLatencyMs: number;
  cacheHitRate: number;            // 0-1 double; multiply ×100 for %
  byModel: TutorUsageByModelDto[];
  byTaskKind: TutorUsageByTaskKindDto[];
  trend: TutorUsageTrendDto[];
}

// EvalResultsDto
export interface EvalCheckBreakdownDto { passed: number; total: number; passRate: number; }
export interface EvalResultsDto {
  runId: string;
  ranAt: string;
  totalCases: number;
  passedCases: number;
  failedCases: number;
  passRate: number;        // 0-100
  failRate: number;
  thresholdPercent: number;
  breached: boolean;
  tier: string;
  note: string;
  byCheck: Record<string, EvalCheckBreakdownDto>;
  bySubject: Record<string, EvalCheckBreakdownDto>;
  byLanguage: Record<string, EvalCheckBreakdownDto>;
}

// FlaggedOutputDto
export interface FlaggedOutputDto {
  contentRef: number;      // SafetyEvent.Id — opaque, not student PII
  taskKind: string;
  actionTaken: string;
  reasonCodes: string[];
  failedChecks: string[];
  modelId: string;
  studentId: number | null;   // present in DTO but NEVER rendered
  occurredAtUtc: string;
}

// FlaggedOutputsFilters
export interface FlaggedOutputsFilters {
  Action?: string;
  ReasonCode?: string;
  TaskKind?: string;
  From?: string;
  To?: string;
  PageNumber?: number;
  PageSize?: number;
}
```

---

## PART J — Loading Skeleton (per-section)

Each section has its own inline skeleton, NOT a full-page skeleton (unlike the admin shell skeleton). Each section's skeleton is shown while that section's hook is loading.

**Skeleton card anatomy** (mirrors `AdminLoadingSkeleton` + `AuditSkeletonRow`):
```css
/* shimmerStyle (reuse identical to audit/moderation pages) */
background: linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%);
background-size: 400px 100%;
animation: lx-shimmer 1400ms linear infinite;
border-radius: 6px;
```

Per section:
- **Signals skeleton:** 3 shimmer mini-cards (height 68 px) + 2 shimmer chart blocks (height 180 px). `role="status" aria-label={strings.aiSafetyLoadingLabel}`.
- **Trend skeleton:** 1 shimmer block height 280 px.
- **Eval skeleton:** 2 shimmer blocks (heights 60 px, 160 px).
- **Usage skeleton:** 4 shimmer mini-cards + 2 chart blocks (height 200 px).
- **Flagged skeleton:** 5 skeleton table rows (7 cells each, mirroring `AuditSkeletonRow`).

---

## PART K — Accessibility

- All filter inputs have `<label htmlFor="...">` with `className="sr-only"`.
- `<div aria-live="polite" aria-atomic="false">` wraps each section's results area.
- `role="status" aria-label={...}` on each section's skeleton.
- Breach banner: `role="alert"` — screen readers announce immediately on render.
- `role="alert"` on date-range error message.
- Focus ring on all interactive controls: `box-shadow: var(--lx-focus-ring-admin)`.
- Table semantics: `<table>`, `<caption>`, `<thead>`, `<tbody>`, `<th scope="col">`.
- Chart accessible fallbacks: hidden `<table>` per TrendLine and BarBreakdown (see P7-10 Part E.6 pattern).
- Color is never the sole signal for breach: the text "THRESHOLD BREACH" always accompanies the red tint.
- `studentId` never rendered — confirmed in FlaggedOutputDto type definition (`// NEVER rendered`).

---

## PART L — Scope notes for frontend agent

1. **Signals endpoint has NO subject or language breakdown** — the `SafetyEvent` entity has no such columns. Do NOT add subject/language filter chips or breakdown panels to the Signals section. The DTO has none of these.
2. **Signals endpoint params are PascalCase `From`/`To`** — all five AI Safety hooks use `From`/`To` (capital F and T), matching the controller binding. Do not use lowercase.
3. **`evals` endpoint takes NO params** — `useEvalResults()` has no arguments. Do not pass date range.
4. **Bootstrap sentinel vs real breach** — the detection in Part F.1 is mandatory. A sentinel with `breached: true` must NOT display the red breach banner. The sentinel state (Part F.2) is the correct render.
5. **Flagged outputs are PII-light** — `studentId` is in the DTO but must never be rendered. The table spec in Part H.3 omits it from columns intentionally.
6. **TrendLine wrapper** (`components/charts/TrendLine.tsx`) is shared by the safety trend (Part E) and the cost trend in tutor usage (Part G). Build it once.
7. **BarBreakdown wrapper** (`components/charts/BarBreakdown.tsx`) is shared with P7-10. Build it once in Batch A (cross-cutting X-2).
8. **Recharts must be pinned `^2`** — Recharts 3 breaks React 18.3.1.
9. **`useSafetyTrend` returns a bare list** — the `.data` field of the envelope is `IReadOnlyList<SafetyTrendBucketDto>`, not a wrapped object. `client.get` returns the inner data, so the hook receives `SafetyTrendBucketDto[]` directly. Handle the array type in the DTO, not an object wrapper.
10. **Flagged `PageSize` clamp** — clamp to `Math.min(pageSize, 100)` client-side before calling (mirrors `useAuditLog`).

---

## Implementation handoff

| Deliverable | File path | Notes |
|---|---|---|
| Nav entry | `apps/admin-dashboard/components/AdminSideNav.tsx` | Append after analytics entry |
| New strings keys | `apps/admin-dashboard/lib/strings.ts` | EN + AR per Part A.3 |
| queryKey namespace | `packages/api-client/src/query/queryKeys.ts` | Add `adminAiSafety` (5 sub-keys) |
| API hooks | `packages/api-client/src/admin/ai-safety.ts` | 5 hooks + FE-local DTOs |
| Page shell | `apps/admin-dashboard/app/(admin)/ai-safety/page.tsx` | `useAdminGuard`, section composition |
| Filter component | `apps/admin-dashboard/components/ai-safety/AiSafetyFilters.tsx` | Zustand date-range |
| Safety signals | `apps/admin-dashboard/components/ai-safety/SafetySignals.tsx` | Cards + 4 breakdowns |
| Safety trend | `apps/admin-dashboard/components/ai-safety/SafetyTrend.tsx` | TrendLine chart |
| Eval results | `apps/admin-dashboard/components/ai-safety/EvalResults.tsx` | 3 states incl. sentinel |
| Tutor usage | `apps/admin-dashboard/components/ai-safety/TutorUsage.tsx` | Cards + bars + cost trend |
| Flagged table | `apps/admin-dashboard/components/ai-safety/FlaggedOutputsTable.tsx` | Paginated, PII-light |
| Bar chart wrapper | `apps/admin-dashboard/components/charts/BarBreakdown.tsx` | Shared with P7-10 |
| Line chart wrapper | `apps/admin-dashboard/components/charts/TrendLine.tsx` | Shared by trend + usage |
| Recharts dep | `apps/admin-dashboard/package.json` | `"recharts": "^2"` (if not already added by P7-10) |

---

## Design gaps / open questions

1. **Signals breakdown filter values are not enumerated** — `Action`, `ReasonCode`, and `TaskKind` filter options in the flagged table are populated from known values (documented in Part H.2) or bootstrapped from the `breakdownByAction`/`breakdownByReasonCode`/`breakdownByTaskKind` arrays in the signals response. If the set of valid values expands (new models, new check types), the frontend will auto-populate from those arrays. The spec provides the known values as a starting point; the actual dropdown options are driven by what the signals endpoint returns.
2. **Eval bootstrap sentinel acceptance** — the "no run yet" state (Part F.2) is designed and accepted. It will render if `safety-eval-results.json` holds only the placeholder. This is not a bug; it is a valid first-run state.
3. **No screenshot capture for this admin route** — spec derived from token set + existing surfaces only. Same condition as all prior Phase-7 admin specs.
4. **`cacheHitRate` rendering** — the DTO field is `double 0–1`. Multiply by 100 and display with 1 decimal + "%" suffix. If the value is 0 and all other usage metrics are 0, treat as the usage empty state.
5. **Security-auditor gate** — P7-11 is child-safety sensitive. The `security-auditor` agent runs after the frontend batch and before the reviewer gate. Critical/High findings block the PR. The PII-light invariant (no `studentId` on screen, no raw prompt/response) is the primary audit target.
