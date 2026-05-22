# Serve adaptive quizzes

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** Assessment Module
- **Issue type:** Story
- **Story Points:** 3 — selection hook joining quiz engine to adaptivity; moderate integration.
- **Labels:** `assessment`, `adaptivity`, `backend`
- **Requirements:** FR-QZ-3

## Description
As a student, I want each quiz tuned to my current level, so that questions feel appropriately challenging rather than too easy or too hard.

## Acceptance Criteria
- When a quiz is generated, then question difficulty is selected using the student's adaptivity model (P3-08).
- Given a student modeled as struggling on a skill, then easier/reinforcement questions are favored; a strong student gets harder ones.
- Selection falls back to a sensible default when no model exists yet (new student).
- The chosen difficulty mix is recorded with the attempt.

## Notes
- Covers B3.3. Blocked by P3-08 (adaptivity) and P3-06 (question generation).
