# Meet API & AI performance targets

- **Project:** Learnexia
- **Sprint / Phase:** Phase 6 — Stabilization (Week 9)
- **Epic:** QA & Stabilization
- **Issue type:** Story
- **Story Points:** 5 — load/perf testing across endpoints + AI paths, plus the fixes the results demand.
- **Labels:** `qa`, `performance`
- **Requirements:** NFR-1

## Description
As a platform owner, I want the system to meet its performance targets under load, so that students get a fast, responsive experience at launch.

## Acceptance Criteria
- Performance tests confirm core API responses < 500 ms (p95) and AI tutor responses < 4 s.
- Tests run against a realistic dataset and concurrency level.
- Identified hotspots above target are fixed or have a documented mitigation/follow-up.
- Results are recorded and repeatable in CI or a documented harness.

## Notes
- Covers Q1.4. Performance budget per NFR-1.
