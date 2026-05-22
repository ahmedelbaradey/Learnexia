# Emit learning domain events

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Technical Enabler
- **Story Points:** 5 — event/handler backbone (MediatR) that the whole gamification module fans out from.
- **Labels:** `gamification`, `backend`, `events`
- **Requirements:** FR-GM-7

## Description
As a backend engineer, I want learning actions to publish domain events that handlers subscribe to, so that XP, badges, streaks, and analytics react to the same events without tight coupling.

## Acceptance Criteria
- `LessonCompleted` and `AnswerSubmitted` domain events are published at the right points in the learning/quiz flow.
- Handlers (XP, badge, streak, analytics) subscribe via MediatR and run independently.
- A failing handler does not block other handlers or the originating request.
- Event handling is suitable for background/event-driven processing (NFR-2).

## Notes
- Covers B5.1. Blocks P4-02..P4-07. Analytics handler also feeds Phase 5 (P5-03).
