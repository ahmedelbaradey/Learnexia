# Execution Plan — P7-10 Platform Analytics & P7-11 AI-Safety Monitoring (admin-dashboard FE)

## Source
- **Pipeline Brief:** `docs/briefs/P7-10-P7-11-admin-dashboards.md` (VERIFIED endpoint contracts — authoritative over the FE task files' aspirational "Contract from Backend" sections).
- **FE task files:** `tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/P7-10-FE.md` (FE-1..6), `…/P7-11-FE.md` (FE-1..7).
- **User stories:** `user-stories/Phase-7-Admin-Console/P7-10-platform-analytics-dashboard.md`, `…/P7-11-ai-safety-monitoring-dashboard.md`.
- **Reuse anchors verified in repo:**
  - App: `apps/admin-dashboard` — React `^18.3.1`, Next `^15.1.4`.
  - Shell/nav: `apps/admin-dashboard/components/AdminShell.tsx`, `…/AdminSideNav.tsx`, `useAdminGuard`, `authStore`.
  - Existing admin surfaces to mirror: `apps/admin-dashboard/app/(admin)/{audit,moderation,curriculum,gamification,users,dashboard}/`.
  - States/components to reuse: `AdminLoadingSkeleton.tsx`, `AdminErrorBanner.tsx`, `SubjectLanguageFilter.tsx`, `SubjectCodeBadge.tsx`, `LanguageBadge.tsx`.
  - Strings: `apps/admin-dashboard/lib/strings.ts` (`getStrings`, `ADMIN_LOCALE = 'en'`, pinned LTR).
  - api-client: `packages/api-client/src/query/queryKeys.ts`; multi-hook admin module pattern `packages/api-client/src/admin/gamification.ts` (mirror as `admin/analytics.ts` + `admin/ai-safety.ts`); **paginated read-only hook template `packages/api-client/src/hooks/useAuditLog.ts`** (PascalCase params, ≤100 clamp, FE-local DTO).
  - E2E harness: `tests/e2e/playwright.admin.config.ts`.

## Lead decisions baked into this plan
1. **Charting = Recharts 2.x** (React-18 compatible — NOT Recharts 3). Add to `apps/admin-dashboard/package.json`. Theme via the admin app's existing design-token CSS variables, not Recharts defaults.
2. **Both P7-10 and P7-11 this cycle**, on the single branch `feat/P7-10-P7-11-admin-dashboards`. They share `AdminSideNav`, `lib/strings.ts`, `queryKeys`, `package.json` → **NO parallel worktrees**; sequential/serialized batches on one branch.
3. **P7-11 `evals` is NOT blocked** — endpoint returns a bootstrap sentinel ("no run yet"); design for both real-data and empty states.
4. **P7-10 FE-4 re-scoped** to categorical breakdown bars (subject/grade/language) from the `kpis` payload — there is no Analytics `/trend` endpoint. Real time-series trend charts apply only to AiSafety (`/trend` + `usage.trend`). Backend time-series follow-up noted below.
5. **Chart wrapper lives in the admin app** (e.g. `apps/admin-dashboard/components/charts/`), authored generically enough to later port to P5-05 parent charts — but **not** promoted to `packages/ui` this cycle (no current second consumer justifies the altitude; ask before promoting).

## Task inventory

### P7-10 — Platform Analytics (route `app/(admin)/analytics`)
| ID | Stack | Deliverable | Est | Depends-on |
|---|---|---|---|---|
| P7-10-FE-1 | FE | `app/(admin)/analytics/page.tsx` route + `AdminSideNav` entry (`navAnalytics`, `activePrefix: /analytics`), `useAdminGuard`-gated; non-admin → redirect, 401/403 honored | 3 | Batch A nav/strings |
| P7-10-FE-2 | FE | `packages/api-client/src/admin/analytics.ts` → hook `usePlatformKpis(from,to)` (renamed from `usefetchPlatformKpis`); **`useKpiTrend` DROPPED** (no backend); FE-local `PlatformKpiSummaryDto` types; `queryKeys.adminAnalytics.*` | 3 | Batch A |
| P7-10-FE-3 | FE | `components/analytics/KpiCards.tsx` — summary cards from `kpis`: `distinctActiveStudents`/`analyticsActiveStudents`, `lessonsCompleted`, `totalAttempts`, `missionsCompleted`, `xpEarnedInWindow`, `totalActiveSubscriptions` + tier, AI-safety counts, session/retention facets. **`*NaReason` facets render "N/A — <reason>" not `0`** (AC5) | 4 | FE-2 |
| P7-10-FE-4 | FE | **RE-SCOPED** → `components/analytics/KpiBreakdownCharts.tsx` — categorical breakdown **bars** from embedded `bySubject`/`byGrade`/`byLanguage` arrays (NOT time-series). Int enum → label mapping (subject/grade/language). Recharts via chart wrapper | 5 | FE-2, Batch A chart wrapper |
| P7-10-FE-5 | FE | `components/analytics/AnalyticsFilters.tsx` — date-range filter + client-side subject/grade/language slice selection via Zustand; drives `usePlatformKpis`; loading/empty/error states | 4 | FE-3, FE-4 |
| P7-10-FE-6 | FE | Localized labels/dates/numbers; ar + en keys authored in `lib/strings.ts` (ships **en/LTR** v1) | 2 | FE-3/4/5 |

### P7-11 — AI-Safety Monitoring (route `app/(admin)/ai-safety`)
| ID | Stack | Deliverable | Est | Depends-on |
|---|---|---|---|---|
| P7-11-FE-1 | FE | `app/(admin)/ai-safety/page.tsx` route + `AdminSideNav` entry (`navAiSafety`, `activePrefix: /ai-safety`), `useAdminGuard`-gated | 3 | Batch A nav/strings |
| P7-11-FE-2 | FE | `packages/api-client/src/admin/ai-safety.ts` → `useSafetySignals`, `useSafetyTrend`, `useTutorUsage`, `useEvalResults` (object hooks, `client.get`) + `useFlaggedOutputs` (paginated, `client.getPaginated`, clone `useAuditLog`); FE-local DTOs; `queryKeys.adminAiSafety.*`. **Params: `From`/`To` only (PascalCase); flagged adds `Action`/`ReasonCode`/`TaskKind`/`PageNumber`/`PageSize`. No subject/language params.** | 3 | Batch A |
| P7-11-FE-3 | FE | `components/ai-safety/SafetySignals.tsx` — signal cards (total/blocked/regenerated/fallback counts + rates) + breakdown bars by Action/ReasonCode/ModelId/TaskKind from `signals`. **No subject/language slices** (not in data) | 4 | FE-2, chart wrapper |
| P7-11-FE-4 | FE | `components/ai-safety/SafetyTrend.tsx` — per-day line/area trend from `trend` (bare list: total + blocked/regenerated/fallback) | included in FE-3/4 | FE-2, chart wrapper |
| P7-11-FE-5 | FE | `components/ai-safety/EvalResults.tsx` — pass/fail rate, threshold, **breach indicator**; per-check/subject/language breakdown. **Handle bootstrap sentinel** (`runId==Guid.Empty`, `totalCases==0`, `breached==true`) → "no eval run yet" state, NOT a real breach (AC4) | 4 | FE-2 |
| P7-11-FE-6 | FE | `components/ai-safety/TutorUsage.tsx` — calls, prompt/completion tokens, est. USD cost, avg latency, cache-hit rate; by-model/by-task-kind bars; per-day cost trend (`usage.trend`) | 4 | FE-2, chart wrapper |
| P7-11-FE-7 | FE | `components/ai-safety/FlaggedOutputsTable.tsx` — paginated drill-in (contentRef, taskKind, actionTaken, reasonCodes, failedChecks, modelId, occurredAtUtc); PII-light; filters (action/reasonCode/taskKind/date); pageSize ≤ 100 | 4 | FE-2 |
| P7-11-FE-8 | FE | `components/ai-safety/AiSafetyFilters.tsx` — date-range (Zustand) driving all panels; loading/empty/error; localized; read-only (covers task-file FE-7) | 3 | FE-3..7 |

> Mapping note: task-file P7-11-FE numbering (FE-1..7) is preserved by deliverable; this plan splits the original FE-3 (signals+trend) and FE-7 (filters+i18n) into explicit components above for clarity. No scope added.

### Cross-cutting / new shared deliverables
| ID | Stack | Deliverable | Est | Depends-on |
|---|---|---|---|---|
| X-1 | FE | Add `recharts@^2` to `apps/admin-dashboard/package.json`; install | 1 | — |
| X-2 | FE | `apps/admin-dashboard/components/charts/` themed wrapper(s): `BarBreakdown`, `TrendLine` (and pass-through for sentinel/empty) bound to admin design-token CSS variables. Generic enough for future P5-05 reuse; stays app-local | 4 | X-1 |
| X-3 | FE | `queryKeys.adminAnalytics` + `queryKeys.adminAiSafety` namespaces in `packages/api-client/src/query/queryKeys.ts` | 1 | — |
| X-4 | FE | `strings.ts` scaffolding: `navAnalytics`, `navAiSafety`, panel/label/state keys (en + ar) | 2 | — |
| X-5 | FE | Two `AdminSideNav.tsx` entries | 1 | X-4 |

## Dependency order
1. Shared foundation (X-1..X-5 + the two api-client hook modules) must land first — every panel depends on hooks, queryKeys, the chart wrapper, nav, and strings.
2. Chart wrapper (X-2) before any breakdown/trend component (P7-10-FE-4, P7-11-FE-3/4/6).
3. `usePlatformKpis` before P7-10 panels; the five ai-safety hooks before P7-11 panels.
4. Per dashboard: cards/panels before the filter wiring (filters drive the queries the panels render).
5. Designer Design Spec(s) before the dashboard build batches (B/C).
6. E2E after both dashboards exist; security-auditor before the reviewer gate (P7-11 is child-safety sensitive).

## Execution batches (all on `feat/P7-10-P7-11-admin-dashboards`, single worktree, sequential)

### Designer stage (before Batch B) — `designer`
- Produce Design Spec(s): `design-system/ui_kits/admin-dashboard/P7-10-analytics.md` and `…/P7-11-ai-safety.md` (or one combined doc).
- Ground in `design-system/` kit + shipped curriculum/moderation/audit surfaces. Dark theme, LTR, English.
- Specify: the **Recharts-backed** chart primitives (BarBreakdown, TrendLine, eval pass/fail + breach badge); the **N/A / bootstrap-sentinel / empty / breach** visual states; int enum → label maps (subject/grade/language).

### Batch A — Shared foundation — `frontend` (sequential; gates everything)
Tasks: **X-1, X-2, X-3, X-4, X-5, P7-10-FE-2, P7-11-FE-2** (+ route shells P7-10-FE-1, P7-11-FE-1 nav wiring).
- Add Recharts 2.x; build themed chart wrapper(s).
- Add `queryKeys.adminAnalytics.*` + `queryKeys.adminAiSafety.*`.
- Author all 6 hooks across `admin/analytics.ts` (1: `usePlatformKpis`) + `admin/ai-safety.ts` (5: signals/trend/usage/evals/flagged) with FE-local DTO types matching the VERIFIED contracts.
- Scaffold `strings.ts` keys (en+ar) and both `AdminSideNav` entries + empty route shells with `useAdminGuard`.
- **Review gate G0** (reviewer) — wrapper API, hook contracts vs brief, no leaked design pattern, build green.

### Batch B — P7-10 Platform Analytics dashboard — `frontend` (after A)
Tasks: **P7-10-FE-1 (finish), P7-10-FE-3, P7-10-FE-4 (re-scoped), P7-10-FE-5, P7-10-FE-6.**
- KPI cards (incl. `*NaReason` → "N/A — reason"), categorical breakdown bars (subject/grade/language), filters (date-range + client-side slices via Zustand), localization.
- **Review gate G1** (reviewer) — AC1–AC7 of P7-10.

### Batch C — P7-11 AI-Safety dashboard — `frontend` (after B; serialized to avoid shared-file collisions)
Tasks: **P7-11-FE-1 (finish), FE-3, FE-4, FE-5, FE-6, FE-7, FE-8.**
- Signal cards + breakdown bars, safety trend line, eval panel (real + sentinel states + breach badge), tutor usage/cost, flagged paginated table with filters, date-range filter + states + localization.
- **`security-auditor`** runs here (child-safety: flagged drill-in PII-light, admin gating, no raw prompt/response text on screen).
- **Review gate G2** (reviewer) — AC1–AC7 of P7-11 + security findings (Critical/High block).

### Batch D — E2E for both dashboards — `frontend-e2e-tester` (after C)
- Drive running admin-dashboard PWA via `tests/e2e/playwright.admin.config.ts` (admin login `superadmin / 123Pa$$word!`).
- Flows: both dashboards reachable; non-admin → redirect/403; date-range refetch; KPI cards + breakdown bars; safety signals/trend/usage render; flagged table paginates & filters; eval panel shows breach **or** "no run yet" sentinel; loading/empty/error. LTR/English (no RTL assertions — locale pinned en).
- **Final review gate G3** (reviewer) — full acceptance + E2E results green → hand to `committer` (PR on `feat/P7-10-P7-11-admin-dashboards`).

## Review gates
- **G0** after Batch A (foundation: wrapper + hooks + queryKeys + nav + strings).
- **G1** after Batch B (P7-10 acceptance).
- **G2** after Batch C (P7-11 acceptance + security-auditor: Critical/High block).
- **G3** after Batch D (E2E green, overall acceptance) → committer.

## Blockers / prerequisites
- **None hard.** Backend P7-10-BE / P7-11-BE shipped; all 7 endpoints verified. DB-migration and backend-feature stages are **skipped** (no schema/feature work).
- **Watch item (not a blocker):** P7-11 `evals` may return only the bootstrap sentinel if `…/Infrastructure/EvalResults/safety-eval-results.json` holds no real run. The "no eval run yet" state is a designed, accepted state — ship it either way.
- **Recharts pin:** must be 2.x. Recharts 3 targets React 19 and would break the React-18.3.1 admin app — reviewer must confirm the pinned major.

## Follow-ups (separate stories, NOT this cycle)
- **P7-10-BE time-series KPI endpoint** — author a `/api/Admin/Analytics/trend` (or buckets) BE story to back true time-series trend charts; then a P7-10-FE follow-up to add `useKpiTrend` + a TrendLine on Platform Analytics. This cycle ships categorical breakdown bars only.
- **Promote chart wrapper to `packages/ui`** when P5-05 parent charts are built — revisit altitude then (ask the lead before promoting).
- **RTL/ar UI** — admin locale is pinned `en`/LTR (`lib/strings.ts`, `app/layout.tsx`); ar strings are authored now for readiness but no runtime toggle ships. Out of scope until an admin locale-toggle story exists.
- **`/Analytics/notifications`** endpoint is P9-11 scope — excluded.

## Definition of done

### Per batch
- **Batch A:** Recharts 2.x added; themed chart wrapper(s) render with admin tokens; all 6 hooks compile against VERIFIED DTOs; `queryKeys` namespaces added; nav entries + en/ar strings present; route shells gated by `useAdminGuard`. Build + typecheck green. G0 PASS.
- **Batch B:** P7-10 AC1–AC7 met — route gated; KPI cards incl. `*NaReason` N/A rendering; subject/grade/language breakdown bars from embedded arrays; date-range + client-side slice filters via Zustand; loading/empty/error; read-only; localized (en shipped). G1 PASS.
- **Batch C:** P7-11 AC1–AC7 met — route gated; signal cards + Action/ReasonCode/ModelId/TaskKind breakdowns; per-day safety trend; eval panel with breach indicator AND sentinel "no run yet"; tutor usage/cost + cost trend; flagged paginated table (≤100) with action/reasonCode/taskKind/date filters, PII-light; date-range drives all; states handled; read-only. security-auditor: no unresolved Critical/High. G2 PASS.
- **Batch D:** Playwright admin specs cover both dashboards' happy/empty/error + auth flows and pass against the live build. G3 PASS.

### Overall
- Both dashboards live under the admin shell + side-nav, admin-gated, read-only, no per-child PII on screen, bilingual strings authored (en shipped LTR), all panels bound to the verified endpoints with loading/empty/error handled — satisfying every acceptance criterion in `docs/briefs/P7-10-P7-11-admin-dashboards.md` for P7-10 and P7-11, on branch `feat/P7-10-P7-11-admin-dashboards`, ready for `committer` to open the PR.
