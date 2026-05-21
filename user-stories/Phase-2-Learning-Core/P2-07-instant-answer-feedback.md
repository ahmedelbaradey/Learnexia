# Get instant answer feedback

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Assessment Module
- **Issue type:** Story
- **Story Points:** 3 — validation + correct/wrong screens; ties into XP/hearts which arrive in P4.
- **Labels:** `assessment`, `quiz`, `frontend`, `backend`
- **Requirements:** FR-QZ-2

## Description
As a student, I want immediate feedback when I answer a question, so that I learn from mistakes right away and feel rewarded for correct answers.

## Acceptance Criteria
- Given a correct answer, then I see a positive confirmation (confetti) and the answer is recorded as correct.
- Given a wrong answer, then I see the correct/wrong screen with a hint affordance and (once gamification lands) a heart loss.
- Feedback appears within the same screen without a full reload.
- The result of each answer is persisted for later analytics.

## Notes
- Covers B3.2 + F3.2. XP/streak/heart effects are realized when Phase 4 gamification fires on `AnswerSubmitted`.
