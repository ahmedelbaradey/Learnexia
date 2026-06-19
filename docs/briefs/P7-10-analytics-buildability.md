# Buildability Assessment — P7-10 Platform analytics & KPI dashboard

## Summary & traceability
- **Task (1 line):** Decide whether the platform-wide KPI dashboard (P7-10) can be built **now** over data the platform already has, or whether it is genuinely blocked on P5-03 (analytics event capture, not built).
- **User story:** [user-stories/Phase-7-Admin-Console/P7-10-platform-analytics-dashboard.md](../../user-stories/Phase-7-Admin-Console/P7-10-platform-analytics-dashboard.md).
- **Task file:** [tasks/Backend/Phase-7-Admin-Console/P7-10-BE.md](../../tasks/Backend/Phase-7-Admin-Console/P7-10-BE.md).
- **FR-IDs:** FR-ADM-9, FR-PA-3. **SRS §3** (Admin role). **NFR-1**. BRD goal G5 (operator insight / data-driven product). Epic: *Admin — Analytics & AI Oversight*.
- **Stated dependency:** P5-03 (analytics events backbone) — **not built**.

## The core question
Can the platform KPIs be aggregated from existing module data **without** P5-03's event stream? Answer below, KPI by KPI.

## What exists today (the grounding constraint)
The repo has a rich set of **cross-module read seams** in `Shared.Contracts`, **but every one of them is per-student / per-child** (it takes a `studentId`/`childId`). Examples:
- `IStudentLearningStatsQuery.GetStatsAsync(studentId, fromUtc, toUtc)` → `LearningStats(LessonsCompleted, TotalAttempts, TimeLearningSeconds, AvgAccuracy)` — per student, windowed. (`Shared.Contracts/Learning/IStudentLearningStatsQuery.cs`)
- `IStudentXpQuery.GetByStudentIdAsync(studentId)` → `StudentXpSnapshot(StudentId, TotalXp, CurrentLevel)` — per student. `IStudentXpTimeSeriesQuery` — per student.
- `IStudentMasterySummaryQuery`, `IStudentAllSubjectsWeakAreasQuery`, `IChildEnergyUsageQuery` — all per-child.
- `IBillingSubscriptionContract.GetActiveChildrenWithTierAsync()` → all active children with tier (**this one is platform-wide** — no studentId).
- `ai.SafetyEvents` table — directly aggregatable (the P7-11 slice already does this), platform-wide by time range.
- `IUserLookup.FindByIdAsync(userId)` — per user; **no platform user-count / DAU seam exists.**

**There is no platform-wide aggregate seam** (no "count active students in window", no "sum XP across all students", no "count lessons completed platform-wide"). Building P7-10 over existing data therefore requires **adding new `Shared.Contracts` aggregate read seams** in each producing module (Learning, Gamification, Identity, Billing), each backed by an `AsNoTracking` group-by query over that module's own tables. That is real work, but it is mechanical and mirrors the existing per-student seam pattern.

**The genuine P5-03 gap is the *event/session* dimension.** KPIs that depend on a **session or activity-event stream** (DAU/WAU/MAU as "users who had a session", session duration, retention cohorts, engagement events) cannot be derived from the current row-state tables — there is no session/event log. The closest proxy is "students with a completed `Attempt` in the window" (an *activity* proxy, not a true *session* metric).

## KPI-by-KPI feasibility

| KPI (story AC) | Derivable NOW? | Source module / table | Seam needed |
|---|---|---|---|
| **Lessons completed** | ✅ Yes | Learning `Attempt` (Completed, distinct lessons) | NEW platform-aggregate seam `ICompletionStatsQuery.GetPlatformAsync(from,to)` over Learning |
| **Attempts** | ✅ Yes | Learning `Attempt` (count) | same seam |
| **Quizzes completed** | ✅ Likely | Learning quiz attempts | same seam (confirm quiz attempts are distinguishable from lesson attempts) |
| **XP earned (window)** | ✅ Yes | Gamification XP ledger / profile | NEW `IPlatformXpQuery.GetXpEarnedAsync(from,to)` — needs a per-event XP ledger; if only running totals are stored, *earned-in-window* may not be derivable (confirm Gamification persists XP transactions, not just totals) |
| **Active subscriptions** | ✅ Yes | Billing subscription/plan | `IBillingSubscriptionContract.GetActiveChildrenWithTierAsync()` exists (tier counts); a count/group-by aggregate seam is a thin add |
| **Revenue** | ⚠️ Partial | Billing payments/transactions | Billing has payment records (P10), but revenue aggregation seam not built; **and the live payment provider is Fake/Paymob-later**, so revenue is mostly synthetic today |
| **AI requests / blocks** | ✅ Yes | `ai.SafetyEvents` (blocks/flags) + (requests only if P7-11 slice (a) builds `AiUsageLogs`) | aggregate over SafetyEvents now; **request volume** needs the `AiUsageLogs` table from P7-11 |
| **Active users (DAU/WAU/MAU)** | ⚠️ Proxy only | Learning `Attempt.OccurredAt` distinct students = *active-today* proxy | needs NEW `IActiveStudentsQuery.CountDistinctActiveAsync(from,to)`; this is an **activity** proxy, **not** a true session-based DAU |
| **Retention (cohort)** | ❌ No | — | genuinely needs P5-03 session/event capture + cohort logic |
| **Session duration** | ❌ No | `Attempt.DurationSeconds` is per-attempt, not per-session | needs P5-03 session boundary events |
| **Engagement (missions completed)** | ✅ Yes | Gamification missions | NEW platform-aggregate seam over Gamification |
| **Breakdown by subject** | ✅ Yes | Learning/Curriculum carry subject | add subject group-by to the new seams |
| **Breakdown by grade** | ✅ Likely | Identity child grade / Learning | add grade dimension (may need a join the seam resolves internally) |
| **Breakdown by language (ar/en)** | ⚠️ Depends | content `ContentLanguage` on Learning/Curriculum | derivable if the completion/attempt rows carry/resolve `ContentLanguage`; confirm |

**Tally under "build now over existing data":** of the ~13 KPI facets the story wants, roughly **8 would carry real data** (lessons/attempts/quizzes completed, XP earned*, active subscriptions, AI blocks, missions completed, subject/grade/language breakdowns), **2–3 would be proxy-only or partial** (DAU/WAU/MAU as activity-proxy, revenue partly synthetic, AI request-volume only if `AiUsageLogs` exists), and **2 are genuinely blocked on P5-03** (true retention cohorts, session duration). *XP-earned-in-window is conditional on Gamification persisting an XP ledger.

## Recommendation: **(a) build P7-10 over existing data now — as a "real where we can, N/A where we can't" v1**, with two caveats
A meaningful majority of the dashboard (~8 of ~13 facets) would show **real** data immediately, which delivers G5 operator value well before P5-03. The honest framing for the FE: cards backed by real seams render real numbers; retention + session-duration render an explicit "available after analytics events (P5-03)" state; DAU/WAU/MAU is labelled an **activity** metric (distinct active learners), not a session metric, until P5-03.

**Why not (b) build minimal P5-03 first:** P5-03 is a cross-cutting event/session backbone (producers in every module + a sink). That is a larger, separate piece of work and would block P7-10 unnecessarily when ~60% of the dashboard is derivable today. Sequence P5-03 as its own story; it then *upgrades* P7-10's DAU/retention/session facets behind the same endpoints.

**Why not (c) defer entirely:** would leave the admin console without any platform view despite most of the data being readily aggregatable.

### Effort under option (a)
- **New `Shared.Contracts` aggregate seams** (the bulk of the work) — one platform-aggregate query interface per producing module, each implemented in that module's Infrastructure with an `AsNoTracking` group-by, mirroring the existing per-student seam + adapter pattern (`IStudentLearningStatsQuery` is the template):
  - Learning: `IPlatformLearningStatsQuery` (lessons/quizzes/attempts completed, distinct active students, by subject/grade/language).
  - Gamification: `IPlatformEngagementQuery` (XP earned, missions completed) — **conditional on an XP/mission ledger existing.**
  - Billing: `IPlatformSubscriptionStatsQuery` (active subs by tier, revenue) — extend the existing contract.
  - Ai: reuse the P7-11 safety aggregate (blocks/flags); request volume via `AiUsageLogs` if built.
- **Aggregation host:** mirror the task file's option (b) (**per-module admin summary endpoints aggregated via a thin façade**) OR a small read-only façade query handler that fans out to the seams. **Recommendation: a thin façade (a single `GetPlatformKpisQuery` handler that injects the new seams)** rather than standing up a whole new `Analytics` module — less ceremony, no new schema, preserves isolation. A dedicated `Analytics` module only earns its keep once P5-03's event read-model exists.
- **Controller:** `AdminAnalyticsController`, `api/Admin/Analytics`, `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`, `BaseResponse<T>`, Redis-cached aggregates (NFR-1), in-handler validation of range/breakdown inputs.
- **Effort estimate:** moderate-to-large — dominated by the N new aggregate seams (each is small but spans 4 modules, and shared-file edits to each module's DI registration must be serialized per PARALLELISM.md). Materially larger than P7-11's safety slice because P7-11 reads one table the Ai module already owns, whereas P7-10 must add platform-aggregate seams across 4 modules.

## Decision inputs / open questions for the lead
- **OQ-1:** Confirm Gamification persists an **XP transaction ledger** (so "XP earned in window" is derivable) vs running totals only. If totals-only, XP-earned becomes blocked-on-ledger.
- **OQ-2:** Confirm Learning distinguishes **quiz** attempts from **lesson** attempts for the "quizzes completed" KPI.
- **OQ-3:** DAU/WAU/MAU semantics — is the lead OK shipping an **activity-based** proxy (distinct students with a completed attempt in window) labelled as such until P5-03 delivers true sessions? If a *true* session metric is required, that facet is blocked on P5-03.
- **OQ-4:** Revenue — given payments are Fake-provider until Paymob, is revenue worth surfacing now or deferring to when real payments flow?
- **OQ-5:** AI **request volume** depends on the P7-11 `AiUsageLogs` decision — sequence P7-11 slice (a) first if request-volume is wanted on the analytics dashboard.
- **OQ-6 (cross-cutting):** the 4 new aggregate seams touch each module's DI registration (shared files) — serialize per [docs/dev/PARALLELISM.md](../../docs/dev/PARALLELISM.md); don't parallelize these edits with other in-flight stories on the same files.

## Bottom line for the lead
**Recommend option (a): build P7-10 now** as an honest v1 — ~8 of ~13 KPI facets render real data via new per-module platform-aggregate `Shared.Contracts` seams (mirroring `IStudentLearningStatsQuery`), aggregated by a thin façade `GetPlatformKpisQuery` handler (no new module, no new schema), AdminOnly + Redis-cached. DAU/WAU/MAU ships as a labelled **activity** proxy; **retention and session-duration are the only genuinely P5-03-blocked facets** and render an explicit "available after P5-03" state. Sequence P5-03 separately to later upgrade those two facets behind the same endpoints. This is **not** truly blocked on P5-03 — that earlier "blocked" call was over-broad.
