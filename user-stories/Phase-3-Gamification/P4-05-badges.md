# Earn badges

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 3 — rule-based badge engine + award persistence + collection display.
- **Labels:** `gamification`, `backend`, `frontend`
- **Requirements:** FR-GM-4

## Description
As a student, I want to earn badges for achievements, so that I feel recognized for milestones like mastering a skill or keeping a streak.

## Acceptance Criteria
- Badges are awarded by rule (e.g., skill mastered, N-day streak, quiz master) evaluated on relevant events.
- Earning a badge writes a `StudentBadge` record and triggers a reward popup.
- A badge is awarded at most once per its condition (no duplicates).
- Earned and locked badges are visible in a badge collection screen.

## Notes
- Covers B5.4. Blocked by P4-01. Collection UI is part of P4-08.
