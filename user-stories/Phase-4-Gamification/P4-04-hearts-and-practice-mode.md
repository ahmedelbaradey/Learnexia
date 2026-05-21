# Lose hearts and enter Practice Mode

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — Gamification (Week 7)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 3 — hearts state + depletion → Practice Mode transition; regeneration rule is open.
- **Labels:** `gamification`, `backend`, `frontend`
- **Requirements:** FR-GM-3

## Description
As a student, I want a limited number of hearts that I lose on wrong answers, so that answering carefully matters and I get a gentle nudge to practice when I run out.

## Acceptance Criteria
- A student starts a session with a set number of hearts; a wrong answer deducts one.
- When hearts reach zero, the student enters Practice Mode rather than being hard-blocked.
- Hearts-remaining is stored on the profile and shown in the lesson/quiz UI.
- Heart loss is triggered by the `AnswerSubmitted` event (P4-01), consistent with instant feedback (P2-07).

## Notes
- Covers B5.3 (hearts). **Open question:** hearts-regeneration mechanics unspecified (BRD §10) — confirm.
