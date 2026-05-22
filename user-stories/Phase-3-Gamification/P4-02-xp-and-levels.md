# Earn XP and level up

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 3 — XP engine + ledger + level computation reacting to events.
- **Labels:** `gamification`, `backend`, `frontend`
- **Requirements:** FR-GM-1

## Description
As a student, I want to earn XP for what I do and level up, so that I feel a steady sense of progress and reward.

## Acceptance Criteria
- XP is awarded per event (e.g., correct +10, quiz +20, lesson +50, streak bonus +30) via event handlers.
- Each award writes an `XPTransaction` ledger entry (amount, reason, timestamp).
- Level is computed from total XP and updates immediately on the dashboard/XP bar.
- XP totals are consistent (no double-award for the same event).

## Notes
- Covers B5.2. Blocked by P4-01. Surfaced in UI via P4-08 and the dashboard (P2-09).
