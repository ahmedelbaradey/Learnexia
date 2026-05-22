# Retrieve curriculum context via vector search

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** RAG Retrieval
- **Issue type:** Story
- **Story Points:** 5 — embeddings + pgvector query + grade/subject/weak-area filtering; quality-sensitive.
- **Labels:** `ai`, `rag`, `data`
- **Requirements:** FR-AI-3, FR-CI-4

## Description
As the AI tutor, I want to retrieve the most relevant curriculum chunks for a student's grade, subject, and weak areas, so that explanations and questions stay grounded in real curriculum content.

## Acceptance Criteria
- Curriculum chunks are embedded (BGE-M3 / OpenAI) and stored in a pgvector column.
- A retrieval query returns top-k chunks filtered by student grade, subject, and (when present) weak area.
- Retrieval latency keeps total AI response within NFR-1 (< 4 s) for typical queries.
- Given no chunks match, then retrieval returns empty and callers handle it without fabricating content.

## Notes
- Covers A3.1 + A3.2. Depends on pgvector (P1-06) and a chunked corpus (seeded for MVP; full pipeline is Phase 2+, BL-02).
