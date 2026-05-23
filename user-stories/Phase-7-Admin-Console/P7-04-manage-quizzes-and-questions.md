# Manage quizzes & questions

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Curriculum Management
- **Issue type:** Story
- **Story Points:** 8 — CRUD over quizzes and the 4 question types with per-type validation, plus attachment to lessons/skills across module boundaries.
- **Labels:** `admin`, `curriculum`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-QZ-1, FR-QZ-3

## Description
As an admin, I want to create quizzes and author their questions across the 4 supported types, then attach a quiz to a lesson or skill, so that assessments are curated through the dashboard instead of seeded.

## Acceptance Criteria
- Given the admin dashboard, when an admin creates/edits a quiz, then it persists with title, description, and an ordered list of its questions.
- Given a quiz, when the admin adds a question, then they can choose one of the 4 types (MCQ, True/False, Matching, Fill-in-the-blank) and the editor enforces that type's shape (options, correct answer).
- Given a question with an invalid shape for its type (e.g. MCQ with no correct option), then the API rejects it with a clear per-type validation error.
- Given a quiz, when the admin attaches it to a lesson or skill, then the association is saved (via `Shared.Contracts`, no cross-module FK).
- Given a question, when the admin edits or removes it, then the quiz's question order updates accordingly.
- (Admin-only access; non-admin → 403/redirect.)

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P2-06 (`assessment` module — `QuizQuestion`, 4 question types), P2-01 (`Lesson`/`Skill` in `learning`), P1-10 (admin shell), P1-05 (Admin policy).
- Quizzes/questions live in the **`assessment`** module; the lesson/skill association crosses to `learning` and so goes through `Shared.Contracts` only — no cross-module FK. Admin-only per SRS §3.
