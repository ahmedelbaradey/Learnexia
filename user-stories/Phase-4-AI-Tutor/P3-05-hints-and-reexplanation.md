# Get progressive hints & simpler re-explanations

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** Prompt Builder & Tutor
- **Issue type:** Story
- **Story Points:** 3 — hint/simplify endpoints reusing the explain pipeline with escalation logic.
- **Labels:** `ai`, `tutor`, `backend`
- **Requirements:** FR-AI-2

## Description
As a student, I want progressive hints when I get something wrong and a simpler re-explanation if I'm still stuck, so that I can work through difficulty without just being given the answer.

## Acceptance Criteria
- Given a wrong answer, when I ask for a hint, then I get a hint that nudges without revealing the full answer.
- Requesting another hint escalates to more specific guidance.
- Requesting a simpler explanation returns a re-explanation at a lower complexity level.
- All hint/simplify output passes the Safety Layer and respects my language/grade.

## Notes
- Covers A2.2 (hint/simplify). Hint usage is recorded per answer (P2-08) and feeds adaptivity (P3-08).
