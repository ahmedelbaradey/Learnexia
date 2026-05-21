# Track per-skill mastery

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — AI Tutor (Week 5–6)
- **Epic:** Adaptivity & Student Modeling
- **Issue type:** Story
- **Story Points:** 5 — mastery store + rule engine + status transitions feeding tree, adaptivity, and reports.
- **Labels:** `adaptivity`, `engine`, `backend`, `data`
- **Requirements:** FR-AD-2, FR-AD-3

## Description
As a student, I want the system to track how well I've mastered each skill, so that my learning path, weak areas, and recommendations reflect what I actually know.

## Acceptance Criteria
- `StudentSkillMastery` stores mastery % and status per skill: not_started / in_progress / mastered / needs_review.
- Mastery is rule-based: ≥80% = mastered; <50% = needs remediation.
- Mastery updates after each relevant attempt and drives skill-tree unlock states.
- Mastery data is queryable per student and per skill for reports and adaptivity.

## Notes
- Covers B4.1. Feeds P2-04 (unlocks), P3-08 (difficulty), P5-02 (weak areas).
