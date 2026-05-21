# Observability: logging, tracing, dashboards

- **Project:** Learnexia
- **Sprint / Phase:** Phase 6 — Stabilization (Week 9)
- **Epic:** DevOps / Observability
- **Issue type:** Technical Enabler
- **Story Points:** 3 — wire NLog targets + OpenTelemetry + health checks and a KPI dashboard.
- **Labels:** `devops`, `observability`
- **Requirements:** NFR-3, FR-PA-3

## Description
As an operator, I want logging, tracing, health checks, and KPI dashboards in place, so that I can monitor uptime and learning KPIs once we launch.

## Acceptance Criteria
- `nlog.config` targets are configured and OpenTelemetry tracing is wired (currently referenced but unused).
- Health-check endpoints report the status of API, DB, Redis, and the AI Gateway.
- A KPI/analytics dashboard surfaces the events captured in P5-03 (DAU/WAU, accuracy, retention).
- Alerts or at least visible signals exist for the 99.5% uptime target.

## Notes
- Covers O2.1 + O2.2. Consumes analytics events (P5-03). Supports NFR-3 availability.
