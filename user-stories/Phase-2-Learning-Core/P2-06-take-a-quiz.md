# Take a quiz (4 question types)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Assessment Module
- **Issue type:** Story
- **Story Points:** 5 — entities + attempt flow + four distinct question-type renderers.
- **Labels:** `assessment`, `quiz`, `backend`, `frontend`
- **Requirements:** FR-QZ-1

## Description
As a student, I want to take a quiz with multiple question types, so that I can test my knowledge in varied, engaging ways.

## Acceptance Criteria
- The quiz supports MCQ, True/False, Matching, and Fill-in-the-blank questions.
- Starting a quiz creates an Attempt; the quiz screen shows progress, the question card, and answer controls per type.
- Each question type renders and accepts input correctly on mobile and desktop.
- QuizQuestion, Attempt, and StudentAnswer entities persist the session.

## Notes
- Covers B3.1 + F3.1. Instant feedback is a separate story (P2-07). Adaptive selection comes in P3 (P3-11).
