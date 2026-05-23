# Author skills & the skill dependency graph

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Curriculum Management
- **Issue type:** Story
- **Story Points:** 8 — CRUD over skills plus a visual editor over the relational graph with acyclic validation surfaced to the admin.
- **Labels:** `admin`, `curriculum`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-2, FR-CI-3

## Description
As an admin, I want to author skills and edit their prerequisite dependency graph through a visual UI, so that I can maintain "the most important asset in the company" without writing seed data by hand.

## Acceptance Criteria
- Given a lesson/concept, when the admin creates/edits a skill, then it persists with name, mastery threshold, estimated time, and its owning concept/lesson.
- Given the skill graph editor, when the admin adds a prerequisite edge between two skills, then a `KnowledgeEdge` (prerequisite/related + `Strength`) is created.
- Given an edit that would create a cycle, then the API rejects it with a clear "would create a cycle" error and the UI shows it without persisting.
- Given a skill, when the admin views it, then "prerequisites of X" and "skills unlocked by X" are listed (reusing the P2-11 queries).
- Given an edge, when the admin removes it, then the edge is deleted and the graph re-renders.
- (Admin-only access; non-admin → 403/redirect.)

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P2-11 (relational `KnowledgeNode`/`KnowledgeEdge` + acyclic validator + prereq/unlock queries), P2-01 (`Skill`), P1-10 (admin shell), P1-05 (Admin policy).
- This is the admin UI over the hand-authored graph from P2-11 — it replaces seed-only authoring; the OCR ingestion pipeline (BL-01..05) stays out of scope. Admin-only per SRS §3.
