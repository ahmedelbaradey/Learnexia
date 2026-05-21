# Unlock lessons by prerequisite/mastery rules

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Learning Module
- **Issue type:** Story
- **Story Points:** 5 — deterministic engine with prerequisite graph + unlock evaluation; core differentiator.
- **Labels:** `learning`, `backend`, `engine`
- **Requirements:** FR-LR-3

## Description
As a student, I want lessons to unlock as I meet their prerequisites, so that I progress through the curriculum in a sensible order and always have a clear next step.

## Acceptance Criteria
- The Learning Path Engine determines lock/unlock state from prerequisite and mastery rules (deterministic, not AI).
- Given I complete a lesson and meet a skill's mastery threshold, then dependent lessons become unlocked.
- Given prerequisites are unmet, then the lesson stays locked and the engine can explain which prerequisite is missing.
- Unlock decisions are reproducible for the same inputs.

## Notes
- Covers B2.3. Per architecture principle: engines decide progression, AI does not (FR-AI-6).
