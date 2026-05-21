# Record granular per-question answers

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Assessment Module
- **Issue type:** Story
- **Story Points:** 3 — capture correctness, time, and hint usage per answer; data backbone for adaptivity/analytics.
- **Labels:** `assessment`, `backend`, `data`
- **Requirements:** FR-QZ-4

## Description
As a platform, I want to record per-question detail (correctness, time spent, hint used), so that adaptivity, student modeling, and analytics have accurate signals to work from.

## Acceptance Criteria
- Each StudentAnswer stores: correctness, time spent (seconds), and whether a hint was used.
- Each Attempt aggregates accuracy %, duration, hints-used count, and status.
- Records are written reliably even if the student abandons mid-quiz (partial attempt captured).
- Data is queryable per student and per skill.

## Notes
- Covers B3.2. Feeds FR-AD (adaptivity, P3) and FR-PA (analytics, P5).
