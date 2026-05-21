# Parse curriculum files into structured content (Multimodal Parsing)

- **Project:** Learnexia
- **Sprint / Phase:** Backlog (Phase 2+) — post-MVP
- **Epic:** Curriculum Intelligence
- **Issue type:** Story
- **Story Points:** 5 — OCR + multimodal extraction (text/images/tables/equations/layout) across formats; Arabic OCR adds risk.
- **Labels:** `curriculum`, `parsing`, `pipeline`
- **Requirements:** FR-CI-2 (parsing portion)

## Description
As the platform, I want uploaded curriculum files parsed into structured raw content — text, images, tables, equations, and layout — so that downstream ingestion has clean, machine-readable material to organize. This is the **extraction layer**: it understands the *files*, not the teaching.

## Acceptance Criteria
- Documents (PDF/DOCX/images), including scanned **Arabic** PDFs, are OCR'd via Azure Document Intelligence.
- The parser extracts and separates text, images, tables, equations, and layout structure into a normalized representation.
- Output is a structured artifact (e.g., per-document JSON) that preserves source references for traceability.
- Failures surface with diagnostics and can be re-run per document.

## Notes
- Covers **A4.1** (OCR/parsing). Tools: RAG-Anything (multimodal parsing), Azure DI (OCR), PyMuPDF/Unstructured.io.
- **Stage 1 of 3** in the curriculum pipeline — distinct from Curriculum Ingestion (BL-05) and Knowledge Graph (BL-03); see [info/Multimodal_Parsing_vs_Curriculum_Ingestion_vs_Knowledge_Graph.md](../../info/Multimodal_Parsing_vs_Curriculum_Ingestion_vs_Knowledge_Graph.md).
- Blocked by BL-01 (upload). Feeds BL-05 (ingestion).
- **Split note:** previously combined OCR + structuring + chunking in one 8-pt story; split into BL-02 (parsing) and BL-05 (ingestion) to match the pipeline's distinct layers.
