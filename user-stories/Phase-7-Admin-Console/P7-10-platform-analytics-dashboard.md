# Platform analytics & KPI dashboard

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Analytics & AI Oversight
- **Issue type:** Story
- **Story Points:** 5 — read-only aggregate read-model over the P5-03 analytics events plus a charted KPI dashboard; admin-only.
- **Labels:** `admin`, `analytics`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-9, FR-PA-3

## Description
As an admin, I want a platform-wide KPI dashboard, so that I can see how the whole platform is performing — active users, retention, learning throughput, and engagement — and make product decisions without querying the database by hand.

## Acceptance Criteria
- Given the admin dashboard, when I open Analytics, then I see platform-wide KPI cards: **active users (DAU/WAU/MAU), retention, lessons & quizzes completed, and engagement** (e.g. session duration, missions completed), each over a selectable time range.
- KPIs are rendered as **charts/trends** (time series + summary cards) with a date-range filter and, where relevant, breakdown by subject (Math, Science, Arabic, English) and grade.
- All figures are **aggregates** sourced from the P5-03 analytics events via a reporting read-model — no individual child PII is shown on this dashboard.
- The dashboard reads from a summary/aggregate query that responds quickly and does not degrade live request latency (NFR-1); aggregates may be cached.
- Only an admin can reach these views and endpoints; non-admin → 403/redirect.
- Cross-module figures are aggregated via integration contracts / a reporting read-model, **not** direct cross-module FK joins.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P5-03 (analytics event capture).
- P7-10 reads analytics events from P5-03 and exposes them as admin aggregates; this is the admin-facing complement to the operator KPI view in P6-05. Read/aggregate-only — no new write entities beyond optional cached aggregates. Charts are admin-facing (web). RTL/Arabic + English.
