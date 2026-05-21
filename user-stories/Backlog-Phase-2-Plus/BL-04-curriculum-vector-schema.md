# Curriculum, knowledge-graph & vector schema

- **Project:** Learnexia
- **Sprint / Phase:** Backlog (Phase 2+) — post-MVP (schema modeled in P3)
- **Epic:** Curriculum Intelligence
- **Issue type:** Technical Enabler
- **Story Points:** 3 — entities + migrations + pgvector column for the curriculum module.
- **Labels:** `curriculum`, `data`, `backend`
- **Requirements:** FR-CI-4

## Description
As a backend engineer, I want the curriculum module's schema — chunks, knowledge nodes/edges, and a vector column — so that ingestion, the knowledge graph, and RAG retrieval have a place to store data.

## Acceptance Criteria
- A new `curriculum` module defines `CurriculumChunk`, `KnowledgeNode`, and `KnowledgeEdge` entities with migrations.
- `CurriculumChunk` includes an `EmbeddingVectorRef` mapped to a pgvector column.
- Schema matches SRS §6 (FKs, metadata, difficulty fields).
- Migrations run cleanly on PostgreSQL.

## Notes
- Covers B7.1 + D2.4. Schema is designed during MVP (P3) even though the pipeline is Phase 2+. Underpins the full pipeline — BL-01 (upload), BL-02 (parsing), BL-05 (ingestion writes `CurriculumChunk`), BL-03 (knowledge graph reads/writes `KnowledgeNode`/`KnowledgeEdge`) — and RAG retrieval (P3-07).
