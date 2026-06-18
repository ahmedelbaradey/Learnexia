# Parent-scoped per-child read API

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics (Week 8)
- **Epic:** Parent Dashboard
- **Issue type:** Story
- **Story Points:** 8 — cross-module read fan-out (Gamification + Learning + Billing + Ai) behind parent-owns-child authz, plus the read seams each source module must expose.
- **Labels:** `backend`, `parent`, `analytics`
- **Requirements:** FR-PA-1, FR-PA-2

## Description
As a parent, I want the app to show **my own child's** real progress, KPIs, subject mastery, focus areas, learning-time charts, helper-energy status, and recent activity — so the parent dashboard and reports reflect reality instead of placeholders.

Today the backend only exposes **self-scoped student** endpoints (a child reading their own data). The parent app's My Children / Overview / Reports / Activity screens are therefore **faked** against `parentDashboardStubs.ts`. The underlying data already exists in Gamification / Learning / Billing — this story adds **parent-scoped per-child read endpoints** that fan out to those modules behind a parent-owns-child authorization check.

## Acceptance Criteria
- A parent can read, **only for their own linked children**, each of: per-child progress snapshot (level/XP/streak/mastery%/active-today), family summary (active learners / lessons completed / total XP / best streak / badges), weekly KPIs with week-over-week deltas, per-subject mastery, focus areas (weak areas with severity), report chart series (daily XP, 20-day trend, time-of-day), helper (AI) energy status, and a recent activity feed.
- **IDOR:** the parent id is resolved from the JWT; requesting another family's child returns 403/404 and never leaks data. Verified by an explicit cross-family test.
- All responses use the `BaseResponse<T>` envelope and are localized (EN + AR).
- A child with no activity (first week) returns well-formed **empty/zero** states, never an error or garbled data.
- Cross-module access is via `Shared.Contracts` read seams only — **no cross-module FK, no module-to-module project reference**. The parent→child ownership check uses the existing `IParentChildQuery`.
- Endpoints are `[Authorize(Roles = "Parent")]`; child (Student) JWTs are rejected at the auth layer.

## Notes
- Backend brief: [../../docs/briefs/P5-parent-read-api.md](../../docs/briefs/P5-parent-read-api.md). Reference fan-out handler: `GetUserActivitySummaryQueryHandler` (Identity) already fans out to Gamification seams with per-seam graceful degradation.
- **Consumer:** the existing FE story **P5-05** (parent dashboard) + task **P5-05-FE** swap their stubs onto these endpoints 1:1 — no new FE story needed.
- The read controller + handlers live in the **Parent** module (lead-approved 2026-06-18 — no new module). Source modules expose new read seams: Gamification `IStudentXpTimeSeriesQuery`; Learning `IStudentLearningStatsQuery` + `IStudentMasterySummaryQuery`; Billing `IChildEnergyUsageQuery`; and the existing `Ai.IStudentWeakAreasQuery` is re-wired off its empty placeholder (depends on **P5-02**).
- Depends on: mastery **P3-09** (built), weak-area detection **P5-02** (built in this wave), parent linkage **P1-04** (built). The scheduled weekly **report** itself is **P5-01**.
- **KPI definitions (lead defaults, 2026-06-18):** "this week" = rolling 7 days in the child's timezone; WoW baseline = the prior rolling-7-day window; "time learning" = sum of `Attempt.DurationSeconds`; "lessons completed" = distinct completed lessons. Adjustable.
