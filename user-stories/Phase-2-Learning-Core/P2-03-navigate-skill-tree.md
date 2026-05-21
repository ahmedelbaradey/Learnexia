# Navigate the skill tree

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Learning Module
- **Issue type:** Story
- **Story Points:** 5 — node-state rendering + tree layout + visual states; central to the learning UX.
- **Labels:** `learning`, `frontend`
- **Requirements:** FR-LR-2

## Description
As a student, I want to see my subject as a skill tree with clear node states, so that I know what I've finished, what's available next, and what's still locked.

## Acceptance Criteria
- The skill tree renders nodes in one of four states: locked, unlocked, completed, boss.
- Tapping an unlocked or completed node opens its lesson; tapping a locked node shows why it's locked (prerequisite).
- Node states reflect the student's current mastery/progress data.
- The tree renders correctly in RTL (Arabic) and LTR (English).

## Notes
- Covers F2.2. Node-state logic depends on the Learning Path Engine (P2-04).
