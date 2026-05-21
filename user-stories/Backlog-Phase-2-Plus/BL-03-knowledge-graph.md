# Build & query the knowledge graph

- **Project:** Learnexia
- **Sprint / Phase:** Backlog (Phase 2+) — post-MVP
- **Epic:** Curriculum Intelligence
- **Issue type:** Story
- **Story Points:** 5 — graph builder + query API over concepts/skills with prerequisite/related edges, including prerequisite traversal.
- **Labels:** `curriculum`, `knowledge-graph`, `backend`
- **Requirements:** FR-CI-3

## Description
As the platform, I want a knowledge graph of how concepts and skills relate (dependencies, prerequisites, related concepts), so that adaptive learning, learning paths, recommendations, quiz generation, remediation, and student modeling can all reason about how knowledge connects — not just what exists.

## Acceptance Criteria
- A graph of `KnowledgeNode` (concepts/skills) and `KnowledgeEdge` is built from the **structured curriculum produced by Curriculum Ingestion** (not from raw parsed files).
- Edges capture prerequisite and related relationships, each with a strength value.
- A query API returns, for a given node: its prerequisites, its dependents, and related concepts.
- **Remediation traversal:** given a weak skill (e.g., *Fractions*), the API returns the upstream prerequisite(s) to review first (e.g., *Division*), so remediation targets the real gap.
- The graph is built with LightRAG assistance; the target store is **Neo4j (Phase 3+)**, modeled relationally in Phase 2 per the SRS §6 note.

## Notes
- Covers A4.3 + B7.3. This is the **third pipeline stage** — distinct from Multimodal Parsing (file extraction) and Curriculum Ingestion (educational structuring); see [info/Multimodal_Parsing_vs_Curriculum_Ingestion_vs_Knowledge_Graph.md](../../info/Multimodal_Parsing_vs_Curriculum_Ingestion_vs_Knowledge_Graph.md).
- **Blocked by BL-05 (Curriculum Ingestion)** — the KG is built from the structured hierarchy that ingestion produces, not from raw parsed files (BL-02).
- Consumed by: Learning Path Engine (P2-04), question generation (P3-06), adaptivity (P3-08), student modeling (P3-09), and spaced-repetition/remediation (P3-10).
