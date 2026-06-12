# Interact with the AI tutor UI

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** AI Tutor UI
- **Issue type:** Story
- **Story Points:** 5 — chat/explanation UI with streaming + hint bubbles + simplify flow.
- **Labels:** `frontend`, `ai`, `tutor`
- **Requirements:** FR-AI-1, FR-AI-2

## Description
As a student, I want a friendly tutor interface with live typing and hint bubbles, so that asking for help feels conversational and encouraging.

## Acceptance Criteria
- The Ask-AI / explanation UI streams responses with a typing animation, exposed as the **four scoped AI-Helper intents** (explain / hint / why-my-answer-is-wrong / similar-example) — **no free-text chatbot input**.
- Hint bubbles appear contextually on wrong answers, with a "simpler explanation" action.
- The interface renders correctly in Arabic (RTL) and English and uses the AI Tutor bubble component.
- Loading and error states are handled (e.g., provider error shows a gentle retry, not a crash).

## Notes
- Covers F4.1 + F4.2. Blocked by P3-04 (explain) and P3-05 (hints/simplify).
