# Pipeline Brief — P7-10 Platform Analytics & P7-11 AI-Safety Monitoring (admin-dashboard FE)

## Summary & traceability
- **Tasks (1 line each):**
  - **P7-10** — Build the admin-dashboard **Platform Analytics & KPI dashboard** (`app/(admin)/analytics`): KPI summary cards + trend/breakdown charts with date-range, subject/grade and **language (ar/en)** slicing, all from read-only aggregate endpoints.
  - **P7-11** — Build the admin-dashboard **AI-Safety & Quality Monitoring dashboard** (`app/(admin)/ai-safety`): safety-signal aggregates, eval pass/fail + breach indicator, tutor usage/cost, and a paginated flagged-outputs drill-in.
- **Scope this brief:** **FRONTEND ONLY** — `apps/admin-dashboard` (Next.js 15) + new hooks in `packages/api-client`. The backend (P7-10-BE / P7-11-BE) is already built and shipped; this cycle wires the UI to existing endpoints. No DB, no `backend-feature` migration work.
- **User stories:** `user-stories/Phase-7-Admin-Console/P7-10-platform-analytics-dashboard.md`, `…/P7-11-ai-safety-monitoring-dashboard.md`.
- **FE task files:** `tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/P7-10-FE.md` (FE-1..6), `…/P7-11-FE.md` (FE-1..7).
- **FR-IDs:** SRS §3 (Admin role); P7-10 → FR-ADM-9, FR-PA-3; P7-11 → FR-ADM-10, FR-AI-4.
- **BRD goal:** G5 (operational excellence / oversight) + G2 (trust & child safety, P7-11). **Epic:** Admin — Analytics & AI Oversight. **Phase 7** (Admin Console, post-MVP).

> **CRITICAL — the FE task files' "Contract from Backend" sections are ASPIRATIONAL and DIVERGE from the shipped backend.** They were written before the BE landed. The verified contracts below (read from the controllers/queries/DTOs) are authoritative. Notable divergences flagged inline: there is **no `/api/Admin/Analytics/trend`** endpoint; `kpis` takes **no** subject/grade/language query params; AiSafety `signals` takes **no** subject/language params; `evals` takes **no** params.

## Business context & value
- **Who benefits:** platform admins / operators (and indirectly product + safety owners). No student/parent surface.
- **P7-10 value:** see platform health (active-student proxy, completions, engagement XP/missions, subscriptions, retention/session going-forward) without hand-querying the DB; bilingual curriculum means ar-vs-en throughput comparison is first-class.
- **P7-11 value:** confirm the AI Safety Layer is working — blocked/flagged volumes & reasons, eval pass-rate with a clear **threshold-breach** signal, and tutor usage/cost — to catch regressions before they reach children. Child-safety sensitive: PII-light by design (no raw prompt/response text, opaque content refs).
- **Success measured by:** an admin can answer "how is the platform doing?" and "is the AI safe right now?" from these two screens; all figures are aggregates; non-admins are blocked.

## Acceptance criteria (testable, traced to FE sub-tasks)

### P7-10 — Platform Analytics
- **AC1** Analytics route exists under the admin shell, in the side-nav, gated by `useAdminGuard`; non-admin → redirect; anonymous endpoint calls → 401, non-admin → 403. → **FE-1**
- **AC2** KPI **summary cards** render from `GET /Analytics/kpis`: active-students proxy (`distinctActiveStudents`) + `analyticsActiveStudents`, `lessonsCompleted`, `totalAttempts`, engagement (`missionsCompleted`, `xpEarnedInWindow`), subscriptions (`totalActiveSubscriptions` + tier), AI-safety counts, and session/retention facets. → **FE-3**
- **AC3** **Breakdown charts** render subject / grade / **language (ar/en)** slices from the embedded `bySubject` / `byGrade` / `byLanguage` arrays of the KPI DTO (NOT a separate trend call). → **FE-4**
- **AC4** A **date-range filter** (and client-side subject/grade/language slice selection) drives the query via Zustand; loading / empty / error states are handled. → **FE-5**
- **AC5** Any facet carrying an `*NaReason` string (e.g. `revenueNaReason`, `quizzesCompletedNaReason`) renders an explicit "N/A — <reason>" rather than `0`. → **FE-3** (new, surfaced from DTO)
- **AC6** Labels/dates/numbers localized; ar strings authored in `lib/strings.ts` (rendered LTR in v1 — see RTL limitation). → **FE-6**
- **AC7** Read-only; no mutations; no per-child PII on screen.

### P7-11 — AI-Safety Monitoring
- **AC1** AI-Safety route under the admin shell + nav, `useAdminGuard`-gated; non-admin → redirect / 403. → **FE-1**
- **AC2** **Safety-signal cards + breakdown** from `GET /AiSafety/signals`: total events, blocked/regenerated/fallback counts & rates, and breakdown lists by Action / ReasonCode / ModelId / TaskKind. (Subject/language breakdown is **N/A** — `SafetyEvent` has no subject/language column; do NOT promise those slices.) → **FE-3**
- **AC3** **Safety trend** chart from `GET /AiSafety/trend` (per-day buckets: total + blocked/regenerated/fallback). → **FE-3/FE-4**
- **AC4** **Eval results** panel from `GET /AiSafety/evals`: pass/fail rate, threshold, and a clear **breach indicator**; per-check / per-subject / per-language breakdown. Must handle the **bootstrap sentinel** (`runId == empty`, `totalCases == 0`, `breached == true`) as "no run yet", not as a real breach. → **FE-4**
- **AC5** **Tutor usage & cost** from `GET /AiSafety/usage`: calls, prompt/completion tokens, estimated USD cost, avg latency, cache-hit rate; by-model / by-task-kind breakdowns; per-day cost trend. → **FE-5**
- **AC6** **Flagged-outputs** paginated drill-in from `GET /AiSafety/flagged`: content ref (id), taskKind, actionTaken, reasonCodes, failedChecks, modelId, occurredAt; PII-light; page-size ≤ 100; filters (action / reasonCode / taskKind / date). → **FE-6**
- **AC7** Date-range filter (Zustand) drives all panels; loading/empty/error states; localized; read-only. → **FE-7**

## Affected modules & data (new vs existing)
- **No backend entity work.** All read-models exist: `analytics.ActivityEvents` (P5-03), `ai.SafetyEvents` (P3-02), `ai.AiUsageLogs` (P7-11/P7-11b), Learning/Gamification/Billing seams, and the embedded `safety-eval-results.json` artifact (P6-02).
- **New FE:** two routes, ~11 components, ~6 api-client hooks, FE-local DTO types (hand-written, mirroring backend DTOs — no NSwag for these), `queryKeys` entries, `strings.ts` keys, one side-nav entry per dashboard.
- **New shared primitive (decision required):** a lightweight chart/bar/sparkline primitive — **does NOT exist** in `packages/ui` today (verified: no chart/svg/sparkline source). See Open Question 1.

## VERIFIED endpoint contracts (authoritative — from source)

> Envelope: all return `BaseResponse<T>` with `successed` flag (FE reads `successed`; api-client throws `ApiEnvelopeError` on `successed === false`). Date params are UTC `DateTime?`; both default to **last 30 days** when omitted. All endpoints `[Authorize(AdminOnly)]` → anonymous 401 / non-admin 403. Queries are NOT auto-validated (range checks run in handlers). **Param casing:** these handlers bind plain query params — send them as written below; for the paged `flagged` endpoint follow the existing PascalCase pattern (`useAuditLog`).

### P7-10 — `api/Admin/Analytics` (Identity module)
Source: `backend/src/Modules/Identity/Learnexia.Modules.Identity.Api/Controllers/AdminAnalyticsController.cs`

1. **`GET /api/Admin/Analytics/kpis?from=&to=`** → `BaseResponse<PlatformKpiSummaryDto>` (200 ✅).
   - Params: `from`, `to` (UTC, optional). **No subject/grade/language params** (the query has them reserved but unused — breakdowns are embedded in the DTO; the client slices client-side).
   - DTO (`PlatformKpiSummaryDto`): `fromUtc, toUtc, lessonsCompleted, totalAttempts, distinctActiveStudents, quizzesCompletedNaReason (string), bySubject[], byGrade[], byLanguage[], missionsCompleted, xpEarnedInWindow (long), totalActiveSubscriptions, subscriptionsByTier[], revenueNaReason (string?), totalAiSafetyEvents, aiBlockedCount, aiFlaggedCount, aiRequestVolume, aiRequestVolumeNaReason (string?), analyticsActiveStudents, totalSessions, avgSessionDurationSeconds (double), avgActiveDaysPerStudent (double), returningStudentRate (double), retentionNaReason (string?, now null), sessionDurationNaReason (string?, now null)`.
   - Breakdown records: `SubjectBreakdown(subjectCode:int, language:int, lessonsCompleted, totalAttempts, distinctActiveStudents)`; `GradeBreakdown(gradeId:int, lessonsCompleted, totalAttempts, distinctActiveStudents)`; `LanguageBreakdown(language:int, lessonsCompleted, totalAttempts, distinctActiveStudents)`; `SubscriptionTierCount(planCode:string, count)`. **`language`/`subjectCode` are int enum codes — FE maps int → label.**

2. **`GET /api/Admin/Analytics/notifications?from=&to=&category=`** → `BaseResponse<NotificationAnalyticsDto>`. Exists but is **out of P7-10 scope** (it's P9-11 notification analytics). Do not wire unless the lead asks.

3. **`GET /api/Admin/Analytics/trend`** — **DOES NOT EXIST.** This is why a bare call 404'd. The KPI trend/breakdown the story asks for is served via the embedded breakdown arrays in `kpis` (subject/grade/language). There is **no time-series bucket endpoint** for platform KPIs. → **Open Question / scope note: "trend over time" charts (time-series) are not backed for P7-10; only summary + categorical breakdowns are.** FE-4 ("trend charts time series per metric") is only partially backed — flag to lead.

### P7-11 — `api/Admin/AiSafety` (Ai module)
Source: `backend/src/Modules/Ai/Learnexia.Modules.Ai.Api/Controllers/AdminAiSafetyController.cs`

1. **`GET /api/Admin/AiSafety/signals?From=&To=`** → `BaseResponse<SafetySignalSummaryDto>` (200 ✅).
   - DTO: `from, to, totalEvents, blockedCount, blockedRate (double), regeneratedCount, regeneratedRate, fallbackReturnedCount, fallbackReturnedRate, breakdownByAction[], breakdownByReasonCode[], breakdownByModelId[], breakdownByTaskKind[]` where each breakdown item = `CountBreakdownDto(label:string, count:int)`.
   - **No subject/language params or breakdowns** (SafetyEvent has no such columns).

2. **`GET /api/Admin/AiSafety/trend?From=&To=`** → `BaseResponse<IReadOnlyList<SafetyTrendBucketDto>>`. Bucket = `(bucketDate, totalCount, blockedCount, regeneratedCount, fallbackReturnedCount)`. Note: returns a **bare list** in `.data`, not a wrapped object.

3. **`GET /api/Admin/AiSafety/usage?From=&To=`** → `BaseResponse<TutorUsageDto>` (200 ✅).
   - DTO: `from, to, totalCalls, totalPromptTokens, totalCompletionTokens, totalEstimatedCostUsd (decimal), avgLatencyMs (double), cacheHitRate (double 0–1), byModel[], byTaskKind[], trend[]`.
   - `byModel` = `(modelId, calls, totalTokens, totalEstimatedCostUsd)`; `byTaskKind` = `(taskKind, calls, totalTokens, totalEstimatedCostUsd)`; `trend` = `(date, calls, totalEstimatedCostUsd)`.

4. **`GET /api/Admin/AiSafety/flagged?Action=&ReasonCode=&TaskKind=&From=&To=&PageNumber=&PageSize=`** → `BaseResponse<PaginatedResult<FlaggedOutputDto>>` (200 ✅, double-wrapped — `requestPaginated` already normalizes both flattened and `.data`-nested shapes).
   - `FlaggedOutputDto`: `contentRef (int = SafetyEvent.Id), taskKind, actionTaken, reasonCodes[], failedChecks[], modelId, studentId (int?), occurredAtUtc`. PageSize capped at 100 server-side; default 20, PageNumber default 1.

5. **`GET /api/Admin/AiSafety/evals`** → `BaseResponse<EvalResultsDto>`. **Takes NO params.** Returns **200 with a bootstrap sentinel** when no eval run artifact is present — **it does NOT 404** (verified in `AiSafetyEvalResultsQueryAdapter` + handler; sentinel = `runId == Guid.Empty`, `totalCases == 0`, `breached == true`). The lead's "404 bare" was almost certainly wrong route/casing, not a missing endpoint.
   - DTO (`EvalResultsDto`): `runId (guid), ranAt, totalCases, passedCases, failedCases, passRate (double 0–100), failRate, thresholdPercent, breached (bool), tier (string), note (string), byCheck{}, bySubject{}, byLanguage{}` where each value = `EvalCheckBreakdownDto(passed, total, passRate)`.
   - **Is the panel really backed?** The endpoint is fully implemented and read-model-backed (embedded JSON artifact). Whether **real data** exists depends on whether the `Ai.EvalTests` harness has been run to populate `…/Infrastructure/EvalResults/safety-eval-results.json`. If only the bootstrap placeholder is committed, the panel renders the **"no eval run yet"** state — that is a valid, designed state, NOT a blocker. See Open Question 2.

## What's reusable vs new
**Reuse (mirror the curriculum / moderation / audit / gamification admin surfaces already shipped):**
- Admin shell: `AdminShell`, `useAdminGuard`, `authStore`; route group `apps/admin-dashboard/app/(admin)/…`; nav config `apps/admin-dashboard/components/AdminSideNav.tsx` (add one entry each, with `label: strings.navAnalytics` / `navAiSafety` + `activePrefix`).
- Data: `packages/api-client` — `useApiClient`, `client.get` (envelope-unwrapping) for the single-object endpoints, `client.getPaginated` for `flagged` (handles double-wrap), `queryKeys` (add `adminAnalytics`, `adminAiSafety`), TanStack Query v5 with `placeholderData: keepPreviousData`. **Pattern to clone for the paginated hook: `packages/api-client/src/hooks/useAuditLog.ts`** (PascalCase params, ≤100 clamp, FE-local hand-written DTO, read-only).
- Filters: Zustand v5 store pattern as used by existing admin list pages; date-range + select inputs.
- Strings: `apps/admin-dashboard/lib/strings.ts` (bilingual `en`/`ar`, `getStrings`, `ADMIN_LOCALE`).
- States: existing loading/empty/error patterns from `audit/page.tsx`, `moderation/page.tsx`.

**New:**
- 2 routes (`analytics/page.tsx`, `ai-safety/page.tsx`) + ~11 panel components per the FE task tables.
- ~6 hooks: `usePlatformKpis`, then `useSafetySignals`, `useSafetyTrend`, `useTutorUsage`, `useFlaggedOutputs`, `useEvalResults`. (Drop the FE-task-file `useKpiTrend` — no backend; rename `usefetchPlatformKpis` → `usePlatformKpis`.)
- **Chart/bar/sparkline primitive — NEW, does not exist.** Needed for the breakdown bars (subject/grade/language, signal reasons, model/task usage), the usage cost trend sparkline, and the safety trend line. See Open Question 1 for the build-vs-library decision and altitude.

## RTL / locale limitation (load-bearing)
- The admin-dashboard pins **`ADMIN_LOCALE = 'en'`** at build time (`apps/admin-dashboard/lib/strings.ts:4144`) and renders `<html dir="ltr">` (`app/layout.tsx`). There is **no runtime locale toggle**. So: author **both ar and en** strings in `lib/strings.ts` for RTL readiness, but the dashboard ships **English, LTR** in v1. Do not build an ar/en UI-language switcher; do not assume RTL layout. (Note: the P7-10 `language` ar/en **filter** is the *curriculum/learning* language dimension of the data — unrelated to the admin UI language.)

## Handoff → db-migration
**None.** No schema or migration work — all read-models already exist. Skip this stage.

## Handoff → backend-feature
**None for the dashboards themselves.** All endpoints are shipped and verified. *If* the lead decides P7-10 needs a true time-series KPI endpoint (see OQ-4), that is a separate BE story to be authored first — out of scope for this FE cycle.

## Handoff → designer (Design Spec, runs before frontend)
- Two surfaces: `design-system/ui_kits/admin-dashboard/P7-10-analytics.md` and `…/P7-11-ai-safety.md` (or one combined spec).
- Ground in `design-system/` kit + existing shipped admin surfaces (curriculum/moderation/audit) for layout, card, table, filter-bar, empty/error conventions. Dark theme, LTR, English.
- **Must specify the chart primitive** the frontend will consume (per OQ-1 resolution): card-with-bars, horizontal bar breakdown, line/sparkline trend, and the eval pass/fail + breach badge. Keep it CSS/SVG-light unless the lead approves a library.
- Specify the **N/A / bootstrap / empty / breach** visual states explicitly (KPI `*NaReason` cells, eval "no run yet" sentinel, flagged-list empty, threshold-breach alert styling).
- Map int enum codes (subject/grade/language) → display labels.

## Handoff → frontend
- Build per FE task tables, with these corrections baked in: no `useKpiTrend`/`/Analytics/trend`; `signals` has no subject/language slices; `evals`/`signals`/`usage`/`trend` take only `From`/`To` (+ flagged paging/filters); handle the **eval bootstrap sentinel** and **`*NaReason`** facets as first-class states.
- Hooks: hand-write FE-local DTO types mirroring the verified DTOs above (no NSwag for these). Use `client.get` for object endpoints, `client.getPaginated` for `flagged`. Add `queryKeys.adminAnalytics.*` and `queryKeys.adminAiSafety.*`.
- Read-only only — no mutation hooks paired with these.
- Add nav entries + `strings.ts` keys (en + ar).

## Handoff → frontend-e2e-tester
- After frontend, drive the running admin-dashboard PWA (admin login `superadmin / 123Pa$$word!`).
- Flows: admin reaches both dashboards; non-admin → redirect/403; date-range filter refetches; KPI cards + breakdown bars render; safety signals/trend/usage render; flagged table paginates & filters; eval panel shows breach **or** "no run yet" sentinel correctly; loading/empty/error states. LTR/English (no RTL assertions — locale is pinned en).

## Open questions / assumptions / risks (RESOLVE BEFORE PLANNING)
1. **[BLOCKING — design/dependency decision] Charting approach.** No chart primitive exists in `packages/ui`. Per project rules (design-pattern/dependency = ask first) and prior no-heavy-viz-lib decisions (the skill-graph used hand-rolled SVG, no library): **hand-rolled CSS/SVG bars + sparklines, or add a charting library?** Recommend hand-rolled, and building the primitive in `packages/ui` (so both dashboards + future operator KPI views reuse it) rather than per-app. **Needs lead approval on (a) library vs hand-rolled and (b) altitude (`packages/ui` vs app-local).**
2. **[VERIFY] P7-11 `evals` data.** The endpoint is fully implemented and returns 200 (bootstrap sentinel when no run) — it is **NOT** blocked and should NOT 404. Confirm whether the committed `safety-eval-results.json` holds a real run or just the placeholder; either way the panel ships (real data or designed "no run yet" state). **No reason to drop the panel.** Lead to confirm acceptance of the sentinel state if no real run is seeded.
3. **[SCOPE] Build both this cycle, or P7-10 first?** They are independent (different modules, different routes, no shared new code except the chart primitive). Recommend: **decide the chart primitive first (OQ-1), build it once, then run P7-10 and P7-11 in parallel** (independent siblings, separate worktrees per PARALLELISM.md). If sequential is preferred, P7-10 first (simpler — one main endpoint).
4. **[SCOPE GAP] P7-10 time-series trends are NOT backed.** FE-4 asks for "trend charts (time series) per metric," but the only KPI endpoint is a point-in-time summary with categorical (subject/grade/language) breakdowns — there is **no `/Analytics/trend` time-bucket endpoint** (AiSafety has `trend`/`usage.trend`, Analytics does not). Options: (a) ship P7-10 with summary cards + categorical breakdown bars only (re-scope FE-4 to breakdown charts), or (b) author a new P7-10-BE time-series endpoint first. **Recommend (a)** for this cycle; flag (b) as a follow-up story.
5. **[CONFIRM] FE-task "Contract from Backend" is stale.** Confirm the lead accepts the verified contracts in this brief over the FE task files (param sets, missing trend, no subject/lang on signals, hook renames).
6. **[ASSUMPTION] Notifications endpoint** (`/Analytics/notifications`) is P9-11 scope, not P7-10 — excluded unless told otherwise.

## Recommended pipeline order (first cut — planner finalizes)
1. **Resolve OQ-1/2/3/4 with the lead** (chart approach + altitude; eval sentinel acceptance; both-vs-one; P7-10 trend re-scope). Nothing else starts until OQ-1 is decided.
2. **designer** — Design Spec(s) for both surfaces, including the agreed chart primitive + N/A/sentinel/breach states.
3. **(if approved) build the chart primitive** in `packages/ui` as a small shared batch (consumed by both dashboards).
4. **frontend** — P7-10 and P7-11 in parallel (independent; serialize only shared-file edits: nav config, `strings.ts`, `queryKeys`, `api-client` index). Each in its own `feat/P7-10-…` / `feat/P7-11-…` worktree.
5. **frontend-e2e-tester** per dashboard after its frontend batch.
6. **security-auditor** — P7-11 touches child-safety-sensitive data + flagged outputs; audit the flagged drill-in (PII-light) and admin gating before the gate.
7. **reviewer** gates each batch; **committer** opens a PR per story.
