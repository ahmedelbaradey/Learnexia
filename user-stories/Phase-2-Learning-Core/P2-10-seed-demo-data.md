# Seed demo subjects & skill trees

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Data / DB Foundation
- **Issue type:** Technical Enabler
- **Story Points:** 2 — author seed content for 4 subjects + sample trees; mostly data entry + load logic.
- **Labels:** `data`, `backend`, `seed`
- **Requirements:** SRS §6

## Description
As a developer, I want seed data for the 4 MVP subjects with sample skill trees per grade, so that the team and demos have a usable, realistic dataset to work against.

## Acceptance Criteria
- Seed logic runs at startup in Development and populates 4 subjects (Math, Science, Arabic, English).
- Each subject has at least one grade's worth of units, lessons, concepts, and skills forming a navigable skill tree.
- Seeding is idempotent (re-running doesn't duplicate data).
- Math has the deepest sample tree (deepest adaptivity target per BRD §4).

## Notes
- Covers D2.5. Blocked by P2-01. Unblocks P2-02/P2-03 demos.
- **Product decision (overrides BRD §4):** MVP is **4 subjects** — Social Studies removed.
