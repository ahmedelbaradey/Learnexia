# Generate curriculum-grounded questions (RAG)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** Prompt Builder & Tutor
- **Issue type:** Story
- **Story Points:** 5 — generation grounded in retrieval + validity checks + mapping to quiz schema.
- **Labels:** `ai`, `quiz`, `backend`
- **Requirements:** FR-AI-3

## Description
As a student, I want practice and quiz questions generated from my actual curriculum, so that what I'm tested on matches what I'm meant to be learning.

## Acceptance Criteria
- Generated questions are grounded in retrieved curriculum context for the target skill/grade (not free-form).
- Output maps to supported question types (MCQ, True/False, Fill-in-the-blank — **Matching deferred pending CO-BE-1/2**) with a valid correct answer.
- Questions pass the Safety Layer and are tagged with `GeneratedBy` for traceability.
- Given retrieval returns no relevant context, then generation declines rather than hallucinating.

## Notes
- Covers A2.3. Blocked by P3-07 (RAG). Feeds adaptive quizzes (P3-11) and quiz engine (P2-06).
