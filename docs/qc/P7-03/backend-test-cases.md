# P7-03 — Skills & Knowledge Graph admin — Backend API test cases

> Target agent: `api-tester`. The existing `P7_03_SkillsGraph_Tests.cs` is **very strong** (~50 facts) and already
> covers the three headline graph guards (cycle, cross-language, duplicate edge), edge add/remove, skill CRUD +
> activate, GetGraph shape/scoping, full auth matrix, and validators. This catalog is **gap analysis**.
>
> Surface under test:
> - `SkillsController` — `List` (Admin), `GetById` (Admin), `Create`, `Update`, `Delete`, `{id}/Active`, `{id}/Stats` (any authed)
> - `KnowledgeGraphController` — `Prerequisites/{nodeId}` + `UnlockedBy/{nodeId}` (any authed), `Graph` (Admin),
>   `POST Edges` (Admin), `DELETE Edges/{edgeId}` (Admin)

Legend: **Covered** (file + method) / **GAP** (implement).

---

## Group A — The three graph guards (the heart of the story) — covered

| ID | Title | Type | Pri | Expected result | Covered / GAP |
|----|-------|------|-----|-----------------|---------------|
| BE-TC-01 | Cycle: B→A after A→B → rejected "would create a cycle"; A→B persists | negative | P0 | `Successed=false`; original edge intact | **Covered** — AC-EDGE-3 (2-node) |
| BE-TC-02 | 3-node cycle A→B, B→C, C→A → C→A rejected | negative | P0 | `Successed=false` | **Covered** — AC-EDGE-3 (3-node) |
| BE-TC-03 | Cross-language edge (ar node ↔ en node) → rejected "must stay within one language tree" | negative | P0 | `Successed=false` | **Covered** — AC-EDGE-2 |
| BE-TC-04 | Duplicate `(Source,Target,Type)` edge → rejected | negative | P0 | `Successed=false` | **Covered** — AC-EDGE-4 |
| BE-TC-05 | Self-loop edge (Source == Target) → rejected (degenerate cycle) | negative | P0 | `Successed=false`; no persist | **GAP** — neither the 2-node nor 3-node cycle test covers `Source==Target`; a self-edge is the simplest cycle and a classic miss. |
| BE-TC-06 | Cross-subject (same-language) edge → behavior asserted | negative/boundary | P1 | per design: rejected or scoped; NOT 500 | **GAP** — cross-*subject* but same-*language* edges (e.g. MATH/Ar skill → SCIENCE/Ar skill) are not tested; the story scopes the graph per-language but GetGraph is per-subject. Assert the documented behavior and that it is not a 500. |
| BE-TC-07 | Cycle check runs alongside cross-language check (an edge that is both a cycle AND cross-language) | negative/boundary | P2 | `Successed=false`; one clear message | **GAP** — interaction of the two guards is not tested. |

---

## Group B — Edge add/remove + validators (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-08 | AddEdge (Prerequisite / Related / explicit Strength) persists, appears in GetGraph | P0 | **Covered** — AC-EDGE-1 (3 tests) |
| BE-TC-09 | AddEdge non-existent Source/Target nodeId → `Successed=false` | P1 | **Covered** — AC-EDGE-5 (2 tests) |
| BE-TC-10 | RemoveEdge soft-deletes (gone from GetGraph; row retained); non-existent edgeId → `Successed=false` | P1 | **Covered** — AC-EDGE-7 (2 tests) |
| BE-TC-11 | Edge validators: SourceNodeId=0 / TargetNodeId=0 → 422; Strength 1.5 / -0.1 → 422 | P1 | **Covered** — AC-VAL-3, AC-EDGE-6 |
| BE-TC-12 | AddEdge with Strength boundary (0.0 and 1.0 inclusive) → accepted | boundary | P2 | **GAP** — out-of-range is tested; the inclusive bounds 0.0 / 1.0 are not. |
| BE-TC-13 | RemoveEdge with edgeId=0 → 422 (GreaterThan(0) on route-bound command) | validation | P2 | **GAP** — non-existent edgeId is tested; the 0-id validator boundary is not. |
| BE-TC-14 | AddEdge with invalid RelationshipType enum (e.g. 99) → 422 | validation | P2 | **GAP** — relationship-type enum guard not asserted. |

---

## Group C — Skill CRUD + activate (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-15 | Skill Create auto-creates exactly one KnowledgeNode visible in GetGraph | P0 | **Covered** — AC-SKILL-1 |
| BE-TC-16 | Skill soft-delete cascades — node + its edges disappear from GetGraph | P0 | **Covered** — AC-SKILL-2 |
| BE-TC-17 | Deactivate skill excludes from student SkillTree; admin List still shows it; reactivate restores | P0 | **Covered** — AC-SKILL-3 / AC-SKILL-4 / AC-SKILL-5 |
| BE-TC-18 | Skill CRUD round-trip Create→GetById→Update→Delete | P1 | **Covered** — "Skill CRUD: Create → GetById → Update → Delete round-trip" + P2-01 Extended BE-TC-04 |
| BE-TC-19 | Skill Create validators: empty Name→422; ConceptId=0→422; MasteryThreshold>100→422; threshold=-1→422 | P1 | **Covered** — AC-VAL-1, AC-VAL-2 + P2-01 Extended BE-TC-23 |
| BE-TC-20 | SetActive validator: SkillId=0 → 422 | P1 | **Covered** — AC-VAL-4 |
| BE-TC-21 | Skill Update non-existent Id → 404 not 500, no `ex.Message` leak | regression/negative | P0 | **GAP** — PR #183-style; Skill Update non-existent path untested. |
| BE-TC-22 | Skill Create under non-existent ConceptId → 404 (pre-existence check) | negative | P1 | **Covered** — P2-01 Extended BE-TC-36b |
| BE-TC-23 | Skill Delete non-existent Id → 404 not 500, no leak | regression/negative | P1 | **GAP** |
| BE-TC-24 | Skill Delete that is a prerequisite for other skills (has outgoing/incoming edges) → behavior asserted | negative/boundary | P1 | **GAP** — AC-SKILL-2 deletes a skill and expects edge cleanup, but does not assert what happens when the skill is an active *prerequisite* in a live path (cascade vs guard). Assert it is graceful (no 500) and the documented behavior. |

---

## Group D — Graph reads + auth (covered)

| ID | Title | Pri | Covered / GAP |
|----|-------|-----|---------------|
| BE-TC-25 | GetGraph returns SkillGraphDto with nodes[] + edges[]; each edge has id/source/target/type/strength | P0 | **Covered** — AC-GRAPH-1 (2 tests) |
| BE-TC-26 | GetGraph scoped by subjectId — other subjects absent; subjectId=0 → `Successed=false` | P1 | **Covered** — AC-GRAPH-3 (2 tests) |
| BE-TC-27 | Inactive skill node flagged isSkillActive=false in GetGraph | P1 | **Covered** — AC-GRAPH-2 |
| BE-TC-28 | Prerequisites/UnlockedBy reachable by any authed user (200, not 403); anonymous→401 | auth | P1 | **Covered** — AC-AUTH-3 (3 tests) |
| BE-TC-29 | Skills List/GetById anonymous→401, non-admin→403 | auth | P1 | **Covered** — AC-AUTH-2 |
| BE-TC-30 | Skills Create/Update/Delete/SetActive anonymous→401, non-admin→403 | auth | P0 | **Covered** — AC-AUTH-1 |
| BE-TC-31 | KnowledgeGraph Graph/Edges (POST+DELETE) anonymous→401, non-admin→403 | auth | P0 | **Covered** — AC-AUTH-1 |
| BE-TC-32 | AddEdge success envelope shape; GetGraph envelope + data.nodes + data.edges | functional | P1 | **Covered** — AC-ENV-1 (2 tests) |
| BE-TC-33 | Prerequisites/UnlockedBy with non-existent nodeId → graceful (200 empty or `Successed=false`, not 500) | negative | P2 | **GAP** — known-node is tested; non-existent node is not. |

---

## Group E — Per-language graph reads (P7-03-BE-7) — under-tested

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|----|-------|------|-----|-------|----------|---------------|
| BE-TC-34 | GetGraph/prereq/unlock scoped to one language renders only that language's tree | functional | P2 | build ar + en skills under same SubjectCode; GetGraph for the ar subject | only ar nodes/edges returned | **GAP** — cross-language edge rejection is covered (AC-EDGE-2), but the read-side single-language scoping is not directly asserted. |
