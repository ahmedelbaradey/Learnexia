# Remind me when a skill is due for review (spaced repetition) — BLOCKED

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 3 — review-due reminder off the spaced-repetition scheduler. **Blocked.**
- **Labels:** `notifications`, `backend`, `habit`, `blocked`, `forward-looking`
- **Requirements:** FR-GM-8; FR-AD-4 (spaced repetition).

## Description
As a student, I want a nudge when a weak/forgotten skill is due for a quick review — "🧠 وقت مراجعة سريعة لمهارة الكسور (دقيقتين بس)" — so that learning-science timing, not just streaks, brings me back.

## Acceptance Criteria *(to be built when unblocked)*
- When the spaced-repetition scheduler marks a skill **due** for a child, a review-reminder nudge fires (new event/template), Achievement/Reminder category.
- Copy Arabic-first, child-safe, encouraging, en fallback, personalized (skill name, ~minutes), deep-links to the review (P9-02).
- Subject to P9-07 arbitration + parent toggle; sends/opens logged per type (P5-03 seam).

## Notes
- **BLOCKED by P3-10 (spaced repetition)** — not yet built; the scheduler that emits "skill due" does not exist. This story is captured now for completeness of the habit-loop catalog and **must not be scheduled as buildable** until P3-10 ships.
- When unblocked: new `Shared.Contracts` event from the AI-Tutor/Learning side; Notifications consumes (module isolation).
