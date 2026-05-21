# Upload curriculum documents with metadata

- **Project:** Learnexia
- **Sprint / Phase:** Backlog (Phase 2+) — post-MVP
- **Epic:** Curriculum Intelligence
- **Issue type:** Story
- **Story Points:** 3 — upload + metadata endpoints; structuring/OCR are separate (BL-02).
- **Labels:** `curriculum`, `backend`, `admin`
- **Requirements:** FR-CI-1

## Description
As an admin, I want to upload curriculum files (PDF/DOCX/images) with metadata, so that the platform can ingest real curriculum content for grounding the AI tutor.

## Acceptance Criteria
- An admin can upload PDF/DOCX/image files and attach metadata: grade, subject, language, country.
- Uploads are validated for type/size and stored durably.
- Only admins can upload (per SRS §3); the upload appears in an ingestion queue.
- Upload status (received / processing / done / failed) is visible.

## Notes
- Covers B7.2. Phase 2+ per BRD/SRS — data model designed during MVP. Triggers BL-02 processing.
