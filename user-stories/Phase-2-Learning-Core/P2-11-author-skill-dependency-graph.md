# Author the skill dependency graph (relational, hand-authored)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (Week 3–4)
- **Epic:** Curriculum & Learning Core
- **Issue type:** Story
- **Story Points:** 5 — relational prerequisite-edge model + authoring/seed path + validation (no cycles).
- **Labels:** `curriculum`, `backend`, `data`, `skill-graph`
- **Requirements:** FR-AD-1, FR-QZ-3 *(reuses the relational `KnowledgeNode`/`KnowledgeEdge` shape from SRS §6)*

## Description
As the platform, I need a real skill **dependency graph** (e.g. "Fractions depends on Division") at MVP, so that prerequisite-based unlocks (`P2-04`) and remediation ("review the prerequisite") rest on actual edges instead of a flat seeded list.

> **Why this story exists:** the barrier-to-entry strategy calls the skill dependency graph *"the most important asset in the whole company."* The full OCR-driven pipeline is deferred to the backlog (`BL-01..05`), and the MVP currently relies on hand-seeded demo trees with no prerequisite edges. This pulls forward a thin, **hand-authored** slice so BE5 remediation works at launch — without building the ingestion pipeline. Closes gap 3a-1 and softens the build-order inversion (3b-1).

## Acceptance Criteria
- Skills carry **prerequisite edges** (`KnowledgeEdge` / equivalent relational join) within and across lessons; the model matches the relational shape designed in SRS §6 so the future `BL-04` schema is a superset, not a rewrite.
- Edges are **hand-authored / seedable** (extends `P2-10` demo seed) for the MVP subjects & grades — no OCR, no Azure Document Intelligence dependency.
- The graph is **validated as acyclic**; cycles are rejected at author/seed time with a clear error.
- `P2-04` (unlock rules) and `P3-08`/`P3-10` (adaptivity, prerequisite review) read prerequisites from this graph, not from ad-hoc ordering.
- Graph is queryable: "prerequisites of skill X" and "skills unlocked by mastering X".

## Notes
- **Explicit strategic decision:** full Curriculum Intelligence (BE1) stays post-MVP (`BL-01..05`); this story is the launch bridge. Record the deferral in `user-stories/README.md`.
- Depends on: `P2-01` (hierarchy), `P2-10` (seed). Feeds: `P2-04`, `P3-08`, `P3-10`, and is forward-compatible with `BL-03`/`BL-04`.
- Closes gap **3a-1**; documents the conscious deferral behind **3b-1 / 3b-3**.
