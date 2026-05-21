# Ingest parsed content into the curriculum hierarchy (Curriculum Ingestion)

- **Project:** Learnexia
- **Sprint / Phase:** Backlog (Phase 2+) — post-MVP
- **Epic:** Curriculum Intelligence
- **Issue type:** Story
- **Story Points:** 5 — AI-driven structuring into the educational hierarchy + semantic chunking + embedding prep; quality-sensitive.
- **Labels:** `curriculum`, `ai`, `pipeline`
- **Requirements:** FR-CI-2 (structuring portion)

## Description
As the platform, I want parsed content organized into the educational hierarchy and prepared for retrieval, so that raw files become a structured learning system the tutor can ground on. This is the **educational intelligence layer**: it turns content into Grade → Subject → Unit → Lesson → Concept → Skill.

## Acceptance Criteria
- Parsed output (from BL-02) is structured into Grade → Subject → Unit → Lesson → Concept → Skill via the ingestion pipeline.
- Lessons, concepts, and skills are extracted with metadata (grade/subject/language/difficulty) using AI (LLM extraction + semantic classification).
- Content is semantically chunked into `CurriculumChunk` records and prepared for embedding (feeds RAG, P3-07).
- Given low-confidence extraction, the item is flagged for review rather than silently mis-classified.
- Re-running ingestion for a document is idempotent and updates the structured output.

## Notes
- Covers **A4.2** (structuring/chunking). Tools: RAGFlow (ingestion), DeepTutor concepts (educational reasoning), custom AI logic (curriculum mapping).
- **Stage 2 of 3** in the curriculum pipeline — between Multimodal Parsing (BL-02) and Knowledge Graph (BL-03); see [info/Multimodal_Parsing_vs_Curriculum_Ingestion_vs_Knowledge_Graph.md](../../info/Multimodal_Parsing_vs_Curriculum_Ingestion_vs_Knowledge_Graph.md).
- Blocked by BL-02 (parsing). Feeds BL-03 (knowledge graph) and RAG retrieval (P3-07). Writes to the schema in BL-04.
