# Adjust difficulty adaptively

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — AI Tutor (Week 5–6)
- **Epic:** Adaptivity & Student Modeling
- **Issue type:** Story
- **Story Points:** 5 — deterministic engine combining several signals; core differentiator, needs tuning.
- **Labels:** `adaptivity`, `engine`, `backend`
- **Requirements:** FR-AD-1

## Description
As a student, I want the difficulty to adjust to how I'm doing, so that I'm challenged but not overwhelmed and stay in a productive learning zone.

## Acceptance Criteria
- The Adaptivity Engine sets difficulty (easy/medium/hard) from accuracy, response time, retry count, and hint usage.
- The decision is deterministic and reproducible for the same inputs (not AI-driven, per FR-AI-6).
- Sustained high performance raises difficulty; repeated struggle lowers it.
- The chosen difficulty is exposed to the quiz selection and question generation paths.

## Notes
- Covers B4.2. Consumes granular answer data (P2-08); drives adaptive quizzes (P3-11).
