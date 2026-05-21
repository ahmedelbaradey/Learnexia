# Browse subjects and lessons

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Learning Module
- **Issue type:** Story
- **Story Points:** 3 — CRUD/query endpoints + subject-selection UI over the curriculum model.
- **Labels:** `learning`, `backend`, `frontend`
- **Requirements:** FR-LR-1, FR-LR-2

## Description
As a student, I want to browse the available subjects and see the lessons within them, so that I can choose what to learn.

## Acceptance Criteria
- Given I'm signed in, when I open Subject Selection, then I see the 4 MVP subjects available for my grade (Math, Science, Arabic, English).
- Selecting a subject shows its units and lessons in sequence order.
- Query endpoints return subjects/lessons/skill-tree filtered by the student's grade.
- Given a subject with no lessons for my grade, then I see an appropriate empty state.

## Notes
- Covers B2.2 + F2.2 (subject selection). Blocked by P2-01 and P2-10 (seed data).
- **Product decision (overrides BRD §4):** MVP is **4 subjects** — Social Studies removed.
