# Execution Plan — P5-05 Parent Dashboard (charts + wire real analytics)

## Source
- **Brief:** `docs/briefs/P5-05-parent-dashboard.md` (verified endpoint contracts + OQ-1..OQ-6).
- **Story:** `user-stories/Phase-5-Parent-Analytics/P5-05-parent-dashboard.md`.
- **FE task file:** `tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md` (FE-1..FE-6).
- **Surface:** `apps/student-app` (Expo universal — RN-Web + Tamagui, runtime ar/RTL), the parent area `app/(parent)/…`.
- **Repo facts verified:**
  - Stubs to retire: `apps/student-app/app/(parent)/_components/parentDashboardStubs.ts`.
  - Wiring targets: `apps/student-app/app/(parent)/_components/OverviewWeb.tsx`, `ReportsWeb.tsx`, `DailyActivityCard.tsx`.
  - api-client is a **shared package** — hooks live in `packages/api-client/src/hooks/` (exported from `hooks/index.ts`), query keys in `packages/api-client/src/query/queryKeys.ts`. Established raw-client read pattern: `useAdminGrades.ts` (`useApiClient()` → `client.get<T>()`/`client.getPaginated<T>()`, unwraps `BaseResponse`/`Successed`, keys via `queryKeys.*`). **This is the pattern the new analytics hooks mirror** (the brief's `useOverrideChildGrade` reference lives in `apps/admin-dashboard`; same shape, different app).

## Lead decisions baked in
1. **Scope this cycle = P5-05 ONLY** (charts + real-data wiring): FE-1, FE-2, FE-3, FE-4, FE-6.
2. **P5-06 grade transition — DEFERRED.** No parent-facing grade endpoint exists (only admin-only `POST /api/Admin/Users/{childId}/grade`, which 403s a parent JWT). Needs a future **P5-06-BE** story before any FE wiring. FE-portion of grade transition is OUT of this plan.
3. **P5-04 "Send Report" (FE-5) — OUT OF FE SCOPE.** Report delivery is automatic/backend (Hangfire); no on-demand FE button this cycle.
4. **Charts: hand-roll with Tamagui primitives — NO new dependency.** Do NOT add `react-native-svg`. Compose bars from Tamagui `Stack`/`View`/`GradientBox` + design tokens (resolves OQ-1 → option b).
5. **BarChart altitude: app-local in student-app** (single consumer now). Build under the parent area, NOT `packages/ui`. (Resolves OQ-2 → app-local; `packages/ui` promotion is a future option if P7/P5 charts converge — see Follow-ups.)
   - **Note:** FE hooks/queryKeys are still **shared** (`packages/api-client`) because that is where the api-client lives and is the established pattern — only the chart component is app-local.

## Task inventory
| ID | Stack | Summary | Est (h) | Depends-on | This cycle |
|---|---|---|---|---|---|
| P5-05-FE-1 | FE | `BarChart` primitive (labelled bars, gradient active bar, value labels, axis, empty/zero + loading states, RTL-aware). **App-local** (not `packages/ui`), hand-rolled (no `react-native-svg`). | 6 | Design Spec | YES |
| P5-05-FE-2 | FE | Daily-activity bar chart — replace `DailyActivityCard` placeholder with `BarChart` (XP per day, Mon–Sun, from `…/Reports.dailyXpSeries`) + working **Export CSV**. | 3 | FE-1, hooks | YES |
| P5-05-FE-3 | FE | Reports charts — 20-day XP trend (`xpTrend20Day`, exactly 20) + time-of-day breakdown (`timeOfDayBuckets`, 4) in `ReportsWeb`, replacing the two `ChartPlaceholderPanel`/`ChartPlaceholder` slots. | 5 | FE-1, hooks | YES |
| P5-05-FE-4 | FE | Wire real analytics — author the analytics api-client hooks + queryKeys + int→subject adapter; replace `parentDashboardStubs.ts` consumers 1:1 (KPIs+deltas, mastery, weak areas, progress, family summary, energy, recommendations). | 6 | hooks (this batch) | YES |
| P5-05-FE-5 | FE | "Send Report" + period select become functional. | 3 | P5-04-BE | **DEFERRED** (no BE endpoint — see Blockers) |
| P5-05-FE-6 | FE | Pixel-perfect + RTL/ar+en + dark/light QA for charted dashboard & reports. | 2 | FE-1..FE-4 | YES |
| — (P5-06 FE) | FE | Per-child grade-transition control + confirm dialog. | — | **P5-06-BE (missing)** | **DEFERRED** (see Blockers) |

**Hooks to author (FE-4 plumbing), hand-written on raw `useApiClient().get<T>()`, keyed by `childId`:**
| Hook | Route | Returns (DTO) |
|---|---|---|
| `useChildWeeklyKpis(childId)` | `GET …/Children/{id}/WeeklyKpis` | `WeeklyKpisDto` (note `xpDelta` absolute — see divergence below) |
| `useChildSubjectMastery(childId)` | `GET …/Children/{id}/SubjectMastery` | `SubjectMasteryResponseDto` (overall + `bySubject[]` 4 entries) |
| `useChildWeakAreas(childId)` | `GET …/Children/{id}/WeakAreas` | `WeakAreasResponseDto.areas[]` (severity 1..3, `suggestedNextAction` i18n key) |
| `useChildReports(childId)` | `GET …/Children/{id}/Reports` | `ChildReportsDto` (`dailyXpSeries`, `xpTrend20Day` ×20, `timeOfDayBuckets` ×4) — feeds FE-2 + FE-3 |
| `useChildRecommendations(childId)` | `GET …/Children/{id}/Recommendations` | `RecommendationsDto.items[]` (all text are i18n keys) |
| `useChildProgress(childId)` | `GET …/Children/{id}/Progress` | `ChildProgressDto` (replaces `getChildStatsStub`) |
| `useFamilySummary()` | `GET …/Family/Summary` | `FamilySummaryDto` (replaces `getFamilyTotalsStub`) |
| `useChildEnergy(childId)` | `GET …/Children/{id}/Energy` | `ChildEnergyDto` (replaces energy stubs) |
| `useChildActivity(childId)` | `GET …/Children/{id}/Activity` | `ActivityFeedDto` (**verify shape before wiring** `ActivityWeb`) |
| `useChildWeeklyReport(childId, week?)` | `GET …/Children/{id}/WeeklyReport?week=` | `WeeklyReportDto` (`reportFound` zero-state) — author if a consumer exists; otherwise defer with the panel it feeds |

Plus: add `queryKeys.parentAnalytics` namespace (keyed by `childId`), int→subject adapter (`0=Math,1=Science,2=Arabic,3=English`), and export every hook from `packages/api-client/src/hooks/index.ts`.

### Known wiring divergences to resolve in code (call out to frontend agent)
- **`xpDelta` (absolute) vs UI-expected `xpDeltaPercent`:** the `WeeklyKpisDto` returns `xpDelta` as an absolute int; the current Overview KPI copy/i18n expects a percent. **Resolution:** present the absolute delta (e.g. `+120 XP vs last week`) and change the i18n key + copy accordingly — do NOT fabricate a percent. Designer specifies the delta copy; frontend implements. Same check for the other `*Delta*` KPI fields (all absolute ints per DTO).
- **`subjectCode` int → key adapter:** reuse existing `OVERVIEW_SUBJECT` keys/accents; add the int→key mapping. Subject with no data renders **0**, not hidden (4 subjects always).
- **Keep stub file's typed enums** (`OVERVIEW_SUBJECT`, severities) as adapters; **delete the deterministic generators** once each consumer is wired.

## Dependency order
1. **Design Spec** (BarChart spec, KPI delta copy, RTL chart behavior, empty/loading states) — gates all chart/wiring batches.
2. **api-client hooks + queryKeys + adapter** (FE-4 plumbing) — gates panel wiring AND chart data (FE-2/FE-3 read `useChildReports`). *Can start in parallel with the Design Spec — no design dependency on the hooks themselves.*
3. **BarChart primitive** (FE-1) — needs Design Spec; gates FE-2/FE-3.
4. **Wire panels** (FE-4 consumers) + **daily-activity chart** (FE-2) — need hooks + BarChart.
5. **Reports charts** (FE-3) — need hooks + BarChart.
6. **RTL/dark QA** (FE-6) — after all wiring.
7. **frontend-e2e-tester** → **reviewer gate**.

Respected constraints: shared `api-client`/`queryKeys` land **before** the screens that consume them; BarChart lands before its consumers; runtime ar/RTL is a first-class requirement throughout (this app DOES render ar/RTL, unlike admin).

## Execution batches

- **Designer stage (before wiring batches; parallel-safe with Batch A):** `designer`
  → Produce `design-system/ui_kits/parent-dashboard/P5-05-*.md`: BarChart primitive spec (3 shapes — daily 7-bar, 20-day dense, time-of-day 4-bucket; decide single-component-with-props), gradient active bar via `gradientStops`/`GradientBox` tokens, value labels (LTR-safe), axis, empty/zero-state, loading skeleton, **RTL behavior** (bar order + axis mirror, value labels stay LTR), the **absolute `xpDelta` KPI copy**, dark/light. **No grade-transition dialog** (P5-06 deferred). No new design pattern without asking (rule 8).

- **Batch A (parallel with Designer):** `frontend`
  → FE-4 plumbing: author the analytics hooks (`useChildWeeklyKpis`, `useChildSubjectMastery`, `useChildWeakAreas`, `useChildReports`, `useChildRecommendations`, `useChildProgress`, `useFamilySummary`, `useChildEnergy`, `useChildActivity`; `useChildWeeklyReport` if a consumer exists) on raw `useApiClient().get<T>()` mirroring `useAdminGrades.ts`; add `queryKeys.parentAnalytics` (keyed by childId); add int→subject adapter; export from `hooks/index.ts`. Verify `ActivityFeedDto` shape before finalizing `useChildActivity`. No screen wiring yet.
  → **Review gate 1** (`reviewer`): hooks compile/typecheck, envelope unwrapped correctly, keys consistent, no NSwag regen.

- **Batch B (after Designer + Batch A):** `frontend`
  → FE-1: build app-local hand-rolled `BarChart` (Tamagui `Stack`/`View`/`GradientBox`, height-scaled bars, RTL-aware) per the Design Spec. **No `react-native-svg`.**
  → FE-4 (wiring): replace `parentDashboardStubs.ts` consumers 1:1 in `OverviewWeb.tsx` (KPIs+absolute deltas, subject mastery, weak areas, recommendations, child progress card, family summary, energy); apply the int→subject + `xpDelta` adaptations; preserve `activeChildStore` re-fetch-per-active-child; reuse `ReportsWeb` ErrorStrip/LoadingSkeleton/first-week patterns; graceful empty states; delete deterministic stub generators as each consumer is wired.
  → FE-2: daily-activity bars in `DailyActivityCard.tsx` from `useChildReports().dailyXpSeries` (Mon–Sun) + working **Export CSV** of the series.
  → **Review gate 2** (`reviewer`): Overview panels render real data, charts present, stub generators removed, empty/error/RTL states wired.

- **Batch C (after Batch B — depends on BarChart + hooks):** `frontend`
  → FE-3: in `ReportsWeb.tsx`, replace the two `ChartPlaceholder` slots with the 20-day XP trend (`xpTrend20Day`, exactly 20, zero-filled) and time-of-day breakdown (`timeOfDayBuckets`, 4) via `BarChart`.
  → FE-6: pixel-perfect + RTL/ar+en + dark/light QA across the now-charted Overview + Reports (numerals via Intl ar-EG, bar/axis mirroring, value labels LTR-safe).
  → **Review gate 3** (`reviewer`): Reports charts render real series, RTL/dark verified.

- **Batch D (after Batch C):** `frontend-e2e-tester`
  → Drive the running web PWA (Playwright) for: Overview + Reports render real charts/panels for a seeded parent+child (register parent+child+attempts via API for a parent-scoped JWT — superadmin token lacks parent scope); child switching re-fetches per child + cross-child isolation; empty/first-week child → zero-state charts, no errors; RTL/ar vs en for every panel + chart; per-panel error + retry; **403 path** (request another family's child id) shows generic error, no IDOR oracle. Export CSV produces a valid CSV. **No grade-transition / send-report flows** (deferred).
  → **Final review gate** (`reviewer`): gate the whole story against the brief's P5-05 ACs + CONVENTIONS, including e2e results.

**No `security-auditor`** this cycle — P5-05 is read-only analytics consumption; the IDOR/403 surface is already server-enforced and covered by the e2e 403 case. (A security audit becomes required only if P5-06-BE adds a parent grade-mutation endpoint later.)
**No `db-migration` / `backend-feature` / `api-tester`** — no backend change in scope (all 9 read endpoints already shipped).

## Review gates
- **Gate 1** — after Batch A (hooks + queryKeys + adapter).
- **Gate 2** — after Batch B (Overview wiring + BarChart + daily chart).
- **Gate 3** — after Batch C (Reports charts + RTL/dark QA).
- **Final gate** — after Batch D (e2e), against all P5-05 ACs.

## Blockers / prerequisites
- **P5-06 grade transition — BLOCKED, deferred out of this cycle.** No parent-facing grade endpoint exists; admin-only `POST /api/Admin/Users/{childId}/grade` 403s a parent JWT, and `PUT /api/Parent/Update-Child` is unverified for re-scope/`ChildGradeChanged` emission. **Action for lead:** spin up a dedicated **P5-06-BE** parent grade-transition story (re-scope tree + preserve history, emit the reserved `ChildGradeChanged` event) — then a follow-up P5-06-FE cycle (which WILL need `security-auditor` + `api-tester`). Do not wire FE to `Update-Child` unilaterally (rule 9).
- **P5-04 "Send Report" (FE-5) — confirmed OUT of FE scope.** Report delivery is automatic backend (Hangfire); no on-demand send/resend endpoint. Leave/remove the toast stub per the existing `ReportsWeb` header cleanup. If an on-demand send is ever wanted, it needs a separate BE command first.
- **OQ-3.5 live-data caveat — QA attribution risk.** Handlers degrade missing seams to zeros silently (per-seam try/catch). **Action:** before/early in Batch B, smoke-test each endpoint against `http://localhost:5080` with a seeded parent+child so that any all-zero panel in QA is attributed to "no data in seed" vs "endpoint/seam gap," not a chart bug. Flag any endpoint that returns only zero-state in the current seed for QA attribution.
- **`ActivityFeedDto` shape unverified** — confirm before finalizing `useChildActivity`/wiring `ActivityWeb`.
- **NSwag note:** the generated client lacks all ParentAnalytics routes — hooks are hand-written on the raw client (OQ-5 resolved → raw client, lower-risk; no NSwag regen this cycle).

## Definition of done

**Per batch:**
- **Designer:** Design Spec committed under `design-system/ui_kits/parent-dashboard/` covering BarChart (3 shapes), RTL chart behavior, empty/loading/zero states, absolute-`xpDelta` KPI copy, dark/light.
- **Batch A:** all analytics hooks typecheck and unwrap `BaseResponse`/`Successed`; `queryKeys.parentAnalytics` keyed by childId; int→subject adapter present; hooks exported from `hooks/index.ts`; reviewer PASS.
- **Batch B:** `OverviewWeb` KPIs/mastery/weak-areas/recommendations/progress/family/energy render real per-active-child data (no stub generators); `DailyActivityCard` shows real Mon–Sun bars + working Export CSV; empty/error/RTL states wired; reviewer PASS.
- **Batch C:** Reports renders 20-day trend (20 entries) + time-of-day (4 buckets) from real data; RTL/ar+en + dark/light verified; reviewer PASS.
- **Batch D:** Playwright specs pass for render, child-switch isolation, empty/first-week, RTL/en, per-panel error+retry, 403 no-oracle, CSV export; reviewer final PASS.

**Overall (tied to P5-05 acceptance criteria):**
- Overview KPIs from `WeeklyKpis` (absolute deltas), subject mastery (4+overall, zero not hidden), weak areas with severity (empty state), recommendations (i18n EN/AR, empty state) — all real, no stubs.
- Daily-activity bar chart + Export CSV; Reports 20-day trend + time-of-day charts — all real series, hand-rolled, no new dependency.
- Multi-child switch re-fetches per active child only; cross-child isolation; 403 leaks no IDOR oracle.
- Full RTL/ar (Eastern-Arabic numerals, mirrored bars/axis, LTR-safe value labels) + en + dark/light.
- Empty/first-week zero-states render without chart errors (every endpoint 200 zero-state, never 404).
- `parentDashboardStubs.ts` deterministic generators removed; typed enums retained as adapters.
- **Explicitly NOT done (deferred, documented):** P5-06 grade-transition control (needs P5-06-BE); P5-04 send-report button (out of FE scope).

## Follow-ups for the lead
- Author **P5-06-BE** (parent grade-transition) story + tasks before any P5-06 FE cycle.
- Confirm P5-04 send-report stays backend-only (recommended); remove residual toast stub if present.
- Consider promoting `BarChart` to `packages/ui` later **only if** P7/P5 chart needs converge (admin currently uses Recharts → no shared consumer today).
- Smoke-test each analytics endpoint with a seeded parent+child to pre-classify zero-state panels.

---
Plan ready — dispatch Batch 1.
