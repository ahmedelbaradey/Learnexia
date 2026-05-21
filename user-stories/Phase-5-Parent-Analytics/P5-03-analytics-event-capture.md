# Capture product analytics events

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics (Week 8)
- **Epic:** Parent & Analytics
- **Issue type:** Story
- **Story Points:** 3 — event capture pipeline for the core KPIs, reusing the event backbone.
- **Labels:** `analytics`, `backend`, `data`
- **Requirements:** FR-PA-3

## Description
As a product owner, I want core engagement and learning KPIs captured, so that I can measure retention, habit formation, and learning outcomes against our goals.

## Acceptance Criteria
- The system captures: DAU/WAU, session duration, mission completion, quiz accuracy, retention, and subject engagement.
- Events are recorded reliably via the gamification/analytics event handlers (P4-01).
- Captured data is queryable for KPI dashboards (built in Phase 6, P6-05).
- Event capture does not measurably degrade request latency (NFR-1).

## Notes
- Covers B6.3. Maps to BRD goals G1/G5. Dashboards/visualization land in P6-05.
