# Explain a concept on demand

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** Prompt Builder & Tutor
- **Issue type:** Story
- **Story Points:** 3 — an endpoint over gateway + prompt builder + safety; main work is wiring + streaming.
- **Labels:** `ai`, `tutor`, `backend`
- **Requirements:** FR-AI-1

## Description
As a student, I want to ask the tutor to explain a concept, so that I can understand a lesson in language and at a level that suits me.

## Acceptance Criteria
- Given a lesson/concept, when I request an explanation, then I get an age- and grade-appropriate explanation in my language.
- The explanation is grounded in **approved curriculum context** (seeded verified-skill corpus at MVP; RAG retrieval via P3-07 swaps in later behind the `ILearningContextProvider` seam) and passed through the Safety Layer.
- Responses stream to the UI and complete within the AI latency target (< 4 s, NFR-1) for typical requests.
- The AI does not decide progression/difficulty — it only generates the explanation (FR-AI-6).

## Notes
- Covers A2.2 (explain). One of the four **AI Helper** intents (see `docs/briefs/ai-helper-mvp.md`) — scoped to the student's current concept; refuses off-curriculum. Blocked by P3-01, P3-02, P3-03 (P3-07 RAG is a later upgrade, **not** an MVP blocker — MVP uses the seeded corpus). Powers the live lesson explanation (P2-05).
