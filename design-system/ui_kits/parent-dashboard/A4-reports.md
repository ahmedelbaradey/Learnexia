# Design Spec — A4 · Parent Reports page (CO-FE-1 / P1-11-FE-9, chart-less this wave)

> Carryover plan `docs/plans/p1-p2-p3-carryover.md` batch **2c**. Replaces the "comingSoon" stub at
> `apps/student-app/app/(parent)/reports.tsx`, building on the **already-merged redesigned parent shell**
> (plan L8: `(parent)/_layout.tsx` shell header + ChildSwitcher + Sidebar + single scroll region —
> see `design-system/ui_kits/parent-dashboard/parent-dashboard-uiux.md` §A; this page renders BODY ONLY on wide).
> Charts (20-day XP, time-of-day) are **deferred to P5-05** — layout reserves their slots. "Send Report" =
> **toast stub** (plan L6). EN/LTR + AR/RTL, dark + light(-tolerant). No app code here.

## 0. Source-of-truth pairs

| Piece | LTR capture | RTL capture | Preview card(s) |
|---|---|---|---|
| Page composition | `design-system/screenshots/web/06-reports.png` | `design-system/screenshots/web-ar/06-reports.png` | — (composition) |
| Page header (title + range + Send Report) | same | same | `preview/web-page-header.html` |
| KPI row | same | same | `preview/web-kpi-row.html`, `preview/ar-web-kpi.html` |
| Subject mastery panel | same | same | `preview/web-skills-mastery.html` |
| Chart placeholders (P5-05) | same (slots only) | same | `preview/web-activity-chart.html`, `preview/web-time-of-day.html` (future targets) |

**Capture overrides:** `06-reports.png` shows subjects **Reading/Art** and 20 days of mock data — product is
**Math / Science / Arabic / English only** (4 rows). Sidebar items "Activity/Subjects" in the capture are NOT in
the shipped Sidebar — no nav change in this batch (plan §3: nobody edits `(parent)/_layout.tsx`).

## 1. Layout

Wide (≥768, shell-owned scroll): page body max content width follows the existing Overview page (fills the
content column; inner padding `$6` 24). Vertical composition:

```
PAGE HEADER (title block ↔ [range select][Send Report])            ← §2.1
  ↓ 24 ($space-6)
KPI ROW — 4-up grid (2×2 at <1024, 1-col <560)                      ← §2.2
  ↓ 24
ATTEMPT/CHART SLOT — "Last 20 days · XP earned" placeholder panel   ← §2.4 (P5-05)
  ↓ 24
2-COL ROW (align-items:start; stacks <1024):
  [ Skills mastery panel §2.3 ]  [ Time-of-day placeholder §2.4 ]
  ↓ 24
RECENT ATTEMPTS PANEL (A5-parent — see A5 spec §3; rendered by 2d)  ← shared page
```

Narrow (<768): mobile `ScreenHeader` (existing pattern in `reports.tsx`) + the same stack 1-col; range select +
Send Report wrap under the title. The **active child** comes from the shell `ChildSwitcher`
(`activeChildStore.activeChildId`) — the page itself has no child picker.

One primary action per screen = **Send Report**.

## 2. Components & tokens (fraction detail)

### 2.1 Page header — `preview/web-page-header.html` literal
- Row, `justify-content: space-between`, padding 20 28 (use `$5`/`$7`≈28 → `paddingVertical={20} paddingHorizontal={24}` to match the page gutter), bottom hairline 1px `$borderSubtle`.
- Title 20/800 `$fg1` (Poppins; Cairo in AR): **"{Child}'s reports"** / **"تقارير {اسم الطفل}"**
  (capture: "Sami's reports"). Sub 12 `$fg3` margin-top 2: **"Detailed breakdown · Switch child in header"** /
  **"تفاصيل كاملة · بدّل الطفل من الأعلى"** (capture says "Switch child in sidebar" — shell moved the switcher
  to the header; intended copy deviation, flagged).
- **Range select** (trailing group, gap 10): the card's `<select>` chrome → use the existing `packages/ui`
  `Select`: bg `$card`, border 1px `$borderInput`, padding 7 12, radius 10 (card literal; nearest token `$sm` 8 —
  keep 10 per pixel rule), 13/600 `$fg1`. Options (i18n `parent.reports.range.*`):
  **This week / This month / All time** → **هذا الأسبوع / هذا الشهر / كل الوقت**. Default: This week.
  Changing range refetches/refilters §2.2–§2.3 data (client-side filter is acceptable this wave).
- **Send Report button**: bg `$primary`, white label 13/700, padding 8 16, radius 10 (card literal), shadow
  `0 4px 12px rgba(99,102,241,0.4)` (`$primaryGlow` family). Label **"Send Report"** / **"إرسال التقرير"**.
  Hover: brighten (`$primaryHover`) + scale 1.02; press 0.95/80ms; focus `--lx-focus-ring`.
  **Action (L6 stub):** fires a toast — reuse the app's existing toast/inline-notice pattern (Settings save
  notice): success-styled strip "Report sent to your email — coming soon!" /
  "سيصلك التقرير على بريدك — قريبًا!" + the button enters a 2s disabled (opacity 0.4) cooldown. No network call.

### 2.2 KPI row — `preview/web-kpi-row.html` literal (cells already exist as `packages/ui` `KPIStatCard` chrome on Overview; reuse the Overview tile implementation)
Per tile: bg `$card`, border 1px `$borderSubtle`, radius `$card` 20, padding 18, column gap 8;
label 11/700 uppercase tracking 0.08em `$fg3`; icon chip 32×32 radius 10, soft tint bg + accent fg;
value 28/800 `$fg1` tabular-nums line-height 1; delta line 12/700 `$success` (or `$fg3` when no delta).

| Tile | Icon (chip tint / fg) | Label EN / AR | Value source |
|---|---|---|---|
| Time learning | ⏱️ `$primarySoft` / `$primary` | TIME LEARNING / وقت التعلم | Σ `durationSeconds` of attempts in range → "3h 24m" / "٣ س ٢٤ د" |
| XP earned | ⭐ `$xpSoft` / `$xp` | XP EARNED / النقاط المكتسبة | **gap** — no per-child XP read for parents; show "—" with sub "Coming with full reports" until P5-05 (G-2) or derive nothing. Do NOT fake. |
| Lessons done | ✓ `$successSoft` / `$success` | LESSONS DONE / الدروس المكتملة | count of attempts `status === Completed` in range |
| Avg. accuracy | 🎯 `$streakSoft` / `$streak` | AVG. ACCURACY / متوسط الدقة | mean `accuracyPercentage` of completed attempts → "84%" / "٨٤٪" |

Deltas ("+38% vs last week" / "+٣٨٪ عن الأسبوع الماضي"): computed from the previous equal-length window of the
same attempts data; omit the line when the previous window is empty (first week).
Numbers: weight 800 tabular-nums; AR uses Eastern-Arabic numerals for prose values (time, %), Latin for any
`XP` figure (technical-string rule).

### 2.3 Skills mastery panel — `preview/web-skills-mastery.html` literal
- Panel: bg `$card`, radius `$modal` 24 (card literal — note this panel uses 24, not 20), padding 22, border 1px
  `$borderSubtle`, shadow `$shadowSoft`.
- Title 16/800 `$fg1` **"Skills mastery"** / **"إتقان المهارات"**; sub 12 `$fg3` margin 6→16
  **"Mastery levels across subjects"** / **"مستويات الإتقان حسب المادة"**.
- 4 rows, gap 14 — **Math / Science / Arabic / English** (product override). Each row =
  `packages/ui` `MasteryBar` with per-subject `accent` (Design Gap GAP-03 precedent):
  Math `$subjectMathFg` · Science `$subjectScienceFg` · Arabic `$subjectArabicFg` · English `$subjectEnglishFg`.
  Header line: subject 13/700 `$fg1` ↔ trailing 13 `$fg3` "14 lessons · **72%**" (percent 800, colored by the
  subject accent) / "١٤ درسًا · **٧٢٪**". Bar: height 10, track `$bg`, radius `$pill`, solid accent fill.
  **Bars stay LTR in AR** (`direction:ltr` wrapper).
- Data: **gap** — no parent per-subject mastery endpoint is typed today. Layout-first per the task file:
  derive per-subject lesson counts from attempts where possible, otherwise render the panel in its **empty
  state** (G-1, §5): bars at 0 with sub "Mastery appears after the first lessons" / "يظهر الإتقان بعد أولى الدروس".

### 2.4 Chart placeholder panels (P5-05 slots — reserve space, build nothing)
- Two panels with the real panels' chrome (bg `$card`, radius 24/20 per their preview cards, border
  `$borderSubtle`, padding 22) and real titles:
  "Last 20 days · XP earned" / "آخر ٢٠ يومًا · النقاط" (full-width, min-height 200) and
  "Time of day" / "أوقات اليوم" (half-width, min-height 200).
- Body: centered `$fg4` 13/600 "Charts coming soon" / "الرسوم البيانية قريبًا" + 📊-free (no decorative emoji) —
  a simple `$cardSoft` skeleton band 8px tall ×3 as a visual hint. `TODO(P5-05)` in code.

### 2.5 Empty / first-week page state
When the active child has **zero attempts** in range: KPI tiles show "—" values with sub
"No activity yet" / "لا يوجد نشاط بعد"; mastery panel empty state §2.3; a friendly inline band above the KPI row:
`$primarySoft` card, radius 20, padding 16: 13/700 `$fg1`
**"{Child} hasn't started yet — their first lesson will light this page up!"** /
**"لم يبدأ {الاسم} بعد — أول درس سيضيء هذه الصفحة!"** with no CTA (the parent can't start lessons).
Loading: skeleton tiles (4 KPI + 2 panel blocks, `$cardSoft`, opacity 0.5–0.7). Error: the W13-style error strip
(`$dangerSoft` + ⚠️ disc + Retry ghost) replacing the KPI row only; header stays.
No children at all: the page defers to the shell (ChildSwitcher add-child path) — show the same band with
"Add a child to see reports" / "أضف طفلًا لعرض التقارير" and a ghost button calling `openAddChild()`.

## 3. Data (api-client — no API calls in components)

| Need | Source |
|---|---|
| Active child | `useActiveChildStore().activeChildId` + `useMyChildren()` (shell pattern) |
| Attempts in range (KPIs, deltas, lessons-done, accuracy, time) | `useStudentAttempts(childId)` — the A5 hook over `GET /api/Learning/Students/{studentId}/Attempts` (`AttemptListItemDto[]`); range filter client-side on `startedAt` |
| Per-subject mastery % | **none typed today** — empty state until P5-05 aggregate endpoint (G-1) |
| Per-child XP | **none for parents** — "—" placeholder (G-2) |
| Send Report | no endpoint — toast stub (L6) |

2c and 2d share `useStudentAttempts` — 2d owns the hook (plan); 2c consumes it. If dispatch collapses 2c+2d into
one agent (plan §3 note), nothing changes.

## 4. RTL / i18n / a11y

- AR: Cairo headings, Tajawal body; Eastern-Arabic numerals in prose (lessons counts, %, time), Latin+LTR for
  XP figures and emails; bars LTR; the 2-col row flips via document `dir` (no row-reverse — shell rule).
- i18n namespace **`parent.reports.*`** (2c is the Batch-2 resources.ts merge owner per plan §3). Keys:
  `title`, `subtitle`, `range.{week,month,all}`, `send`, `sendToast`, `kpi.{time,xp,lessons,accuracy}`,
  `kpi.noActivity`, `delta.vsLastWeek`, `mastery.{title,sub,empty}`, `charts.{xpTitle,todTitle,comingSoon}`,
  `empty.{firstWeek,addChild}`. Replace the now-dead `parent.reports.comingSoon`.
- A11y: header `accessibilityRole="header"`; KPI tiles use `KPIStatCard.accessibilityLabel` composed
  ("Time learning, 3 hours 24 minutes, up 38% vs last week"); Select labeled "Report range" / "نطاق التقرير";
  toast `accessibilityLiveRegion="polite"`. Focus ring on Select + button. Touch targets ≥48 on narrow.

## 5. Design gaps / open questions

| # | Gap | Action |
|---|---|---|
| G-1 | No typed parent endpoint for per-subject mastery | empty-state panel; backend aggregate lands with P5-05 — record in HANDOFF |
| G-2 | No parent-readable child XP figure | KPI shows "—"; confirm with lead whether to drop the tile instead (capture keeps 4 tiles — recommend keep with "—") |
| G-3 | Header sub copy changed ("sidebar" → "header") | intended deviation, e2e copy check updates |
| G-4 | Capture subjects Reading/Art | product override applied (4 subjects) |
| G-5 | Light theme remains partial (SKILL caveat #4) | page must not hard-code dark hexes outside tokens; acceptable known caveat |

## 6. Implementation handoff

| Piece | Target |
|---|---|
| Page body (header, KPI row, mastery, placeholders, states) | `apps/student-app/app/(parent)/reports.tsx` + `(parent)/_components/Reports*.tsx` (batch 2c owns) |
| `MasteryBar`, `Select`, KPI tile chrome | existing `packages/ui` — reuse, no new components |
| Toast/notice | reuse the existing Settings notice pattern (no new pattern) |
| i18n `parent.reports.*` | `packages/shared/src/i18n/resources.ts` (2c = merge owner) |
| Recent-attempts panel on this page | A5 spec §3 (batch 2d) |

Design spec ready for frontend.
