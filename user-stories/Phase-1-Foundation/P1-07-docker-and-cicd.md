# Dockerized environment & CI/CD pipeline

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** DevOps
- **Issue type:** Technical Enabler
- **Story Points:** 5 — compose for all services + build/test CI + a deploy target; several moving parts.
- **Labels:** `devops`, `infra`
- **Requirements:** NFR-3

## Description
As an engineer, I want a one-command local environment and an automated build/test/deploy pipeline, so that the team can run the full stack locally and ship changes reliably.

## Acceptance Criteria
- Given a clean checkout, when I run `docker compose up`, then API, PostgreSQL, Redis, AI Gateway, and frontend start cleanly and `GET /health` returns 200.
- The CI pipeline builds and runs tests on every push and fails the build on test failure.
- A deploy target (Azure/Railway/Render) is configured for at least a staging environment.
- Background-job infrastructure (Hangfire/Quartz) is hosted in the API for later streak/league/report jobs.

## Notes
- Aligns with existing `docker/` assets (O1.1). Background-jobs infra (O1.3) is needed by Phase 4 (B5.6) and Phase 5 (B6).
