# Model the curriculum hierarchy

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Learning Module
- **Issue type:** Technical Enabler
- **Story Points:** 5 — new module + several related entities + migrations; foundation for all learning features.
- **Labels:** `learning`, `backend`, `data`
- **Requirements:** FR-LR-1

## Description
As a backend engineer, I want the curriculum modeled as Grade → Subject → Unit → Lesson → Skill (with Concept), so that lessons, the skill tree, and adaptivity have a consistent data structure to build on.

## Acceptance Criteria
- A new `learning` module exists with entities: Subject, Unit, Lesson, Concept, Skill, plus migrations.
- Relationships match SRS §6: Subject→Unit→Lesson, Concept→Skill, Lesson teaches Skill.
- Each Skill carries a mastery threshold and estimated time; each Lesson has difficulty, sequence order, and lock state.
- Migrations create all tables with correct FKs and indexes on PostgreSQL.

## Notes
- Covers B2.1 / D2.2. Blocked by P1-06 (PostgreSQL). Replaces the Catalog demo scaffolding (SRS §7).
