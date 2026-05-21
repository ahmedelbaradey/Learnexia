# Schedule spaced-repetition practice

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — AI Tutor (Week 5–6)
- **Epic:** Adaptivity & Student Modeling
- **Issue type:** Story
- **Story Points:** 3 — scheduler over mastery data + a background job; moderate logic.
- **Labels:** `adaptivity`, `engine`, `backend`
- **Requirements:** FR-AD-4

## Description
As a student, I want weak or forgotten skills to come back for practice at the right time, so that I retain what I've learned instead of forgetting it.

## Acceptance Criteria
- The scheduler selects skills due for review based on mastery status and time since last practice.
- Skills marked needs_review/weak are surfaced for reinforcement (e.g., in daily missions or practice).
- Scheduling runs as a background job (Hangfire/Quartz) and is idempotent.
- A reviewed skill that improves is rescheduled with a longer interval.

## Notes
- Covers B4.3. Depends on mastery data (P3-09) and jobs infra (P1-07). Surfaced via missions (P4-06).
