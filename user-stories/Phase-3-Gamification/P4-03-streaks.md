# Maintain a daily streak

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 3 — streak engine + daily reset job; grace-period rule is an open question to confirm.
- **Labels:** `gamification`, `backend`, `frontend`
- **Requirements:** FR-GM-2

## Description
As a student, I want a streak that grows each day I learn, so that I'm motivated to come back and build a daily habit.

## Acceptance Criteria
- Completing qualifying activity on a day increments the streak; the streak state is visible (animated flame).
- Missing the required activity resets the streak per the configured rule.
- Streak count is stored on the student profile and surfaced on the dashboard.
- A daily background job evaluates streak continuity reliably across time zones.

## Notes
- Covers B5.3 (streaks). **Open question:** streak grace-period mechanics are unspecified (BRD §10) — confirm before finalizing reset rule.
