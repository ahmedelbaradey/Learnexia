# Transition a child to a new grade

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics (Week 8)
- **Epic:** Parent & Analytics
- **Issue type:** Story
- **Story Points:** 5 — re-scope curriculum/skill tree to the new grade while preserving history; touches learning, mastery, and gamification reads.
- **Labels:** `parent`, `learning`, `backend`
- **Requirements:** FR-ID-2 (extends), FR-LR-1

## Description
As a parent, I want to move my child to a different grade, so that their learning content stays matched to their actual level (e.g., at the start of a new school year).

## Acceptance Criteria
- Given a linked child, when I confirm a grade transition, then the child's grade is updated and their curriculum/skill tree **re-scopes to the new grade**.
- **History is preserved:** XP, level, badges, and streaks carry over; past mastery records are retained (not deleted).
- Prior-grade weak areas remain available for review/spaced-repetition (P3-10) rather than being discarded.
- Only a parent linked to that child can perform the transition (authorization scoped per P1-04); the action is recorded with timestamp and actor.
- Given an invalid target grade (outside 1–6), then the transition is rejected with a clear message.
- The child sees the new grade's skill tree on their next session; the home dashboard reflects the change.

## Notes
- **Product decision:** chosen behavior is **re-scope to new grade, keep history** (confirmed during grooming). Alternatives considered: fresh start, or carry-forward of overlapping mastered skills — not selected.
- Backs the dashboard control in P5-05. Depends on the learning model (P2-01) and student modeling (P3-09).
- Open follow-up: whether overlapping mastered skills should auto-complete in the new grade is deferred (current rule: new grade unlocks fresh, history retained).
