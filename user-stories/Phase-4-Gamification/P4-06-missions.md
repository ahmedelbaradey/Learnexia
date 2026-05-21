# Complete daily/weekly missions

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — Gamification (Week 7)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 3 — mission definitions + per-student progress tracking + reward grant.
- **Labels:** `gamification`, `backend`, `frontend`
- **Requirements:** FR-GM-5

## Description
As a student, I want daily and weekly missions with clear objectives and rewards, so that I have fresh goals that keep each session purposeful.

## Acceptance Criteria
- Missions have a type (daily/weekly), an objective, a reward (XP), and an expiry.
- `StudentMission` tracks status and progress %; progress updates from learning events.
- Completing a mission grants its reward and reflects on the dashboard.
- Expired incomplete missions close out and new ones are issued on schedule.

## Notes
- Covers B5.5. Spaced-repetition (P3-10) can target weak skills into missions. Blocked by P4-01.
