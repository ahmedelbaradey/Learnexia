# Compete in weekly leagues

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 5 — league grouping + ranking + scheduled promotion/demotion job.
- **Labels:** `gamification`, `backend`, `frontend`
- **Requirements:** FR-GM-6

## Description
As a student, I want to compete in a weekly league and move up tiers, so that friendly competition motivates me to keep learning.

## Acceptance Criteria
- Students are grouped into leagues (Bronze → Silver → Gold → Diamond) and ranked by weekly XP.
- A weekly background job promotes top performers and demotes the bottom per tier rules.
- The league standings are visible with the student's current position.
- The promotion/demotion job is idempotent and runs reliably on schedule.

## Notes
- Covers B5.6. Depends on background-jobs infra (P1-07, O1.3) and XP (P4-02).
