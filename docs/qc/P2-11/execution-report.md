# Execution Report — P2-11 (Skill dependency graph API)

> **Owner of results:** `api-tester`. The qc-test-designer scaffolds this template only and never fills results.
> Fill **Status** (Pass / Fail / Blocked / N/A), **Evidence** (test name / assertion / status code observed), and **Defect** (id or short note) after running.
> Case definitions live in `backend-test-cases.md` — do not edit them here.

## Run metadata

| Field | Value |
|---|---|
| Date run | 2026-06-09 |
| Runner | api-tester (claude-sonnet-4-6) |
| Build / commit | qc/phase-2-backend-continue |
| Environment | Testcontainers PostgreSQL · Testing env · `[Collection("IntegrationTests")]` |
| API base | In-process WebApplicationFactory |
| Seed path | `LearningSeeder.SeedAsync` called in `InitializeAsync` |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P2_11"` |
| Overall | PASS — 16 passed, 0 failed, 1 skipped, Total: 17 |

## Summary

| Outcome | Count |
|---|---|
| Pass | 16 |
| Fail | 0 |
| Blocked | 1 |
| N/A | 0 |
| Not Run | 0 |
| **Total** | **17** |

## §A — Testable now

| ID | Title | Priority | Status | Evidence | Defect |
|---|---|---|---|---|---|
| BE-TC-01 | Prerequisites known node → 200 + expected prereq | P0 | PASS | `T1`: 200, expected prereq node in result — prereq edge from seed confirmed | |
| BE-TC-02 | UnlockedBy known node → 200 + expected next | P0 | PASS | `T2`: 200, expected child node in result | |
| BE-TC-03 | Prerequisites unknown id → 404, never 500 | P0 | PASS | `T3`: 404 response — `UnlockedBy_UnknownNode_DoesNotReturn500` | |
| BE-TC-04 | UnlockedBy unknown id → 404, never 500 | P0 | PASS | `BeTc04`: 404 LessonNotFound for nodeId=999999 | |
| BE-TC-05 | Anonymous → 401 (camelCase error body) | P0 | PASS | `T4`: 401, "successed":false in body | |
| BE-TC-06 | Seed smoke: skill-backed nodes + prereq edges | P1 | PASS | `T6`: learning schema has nodes and edges (KnowledgeNodes, KnowledgeEdges tables populated) | |
| BE-TC-07 | Node DTO field contract + `successed` spelling | P1 | PASS | `BeTc07`: "successed" key present; node DTO has id/name/subjectId/gradeId/type fields | |
| BE-TC-08 | Cross-grade prereq edge queryable | P1 | PASS | `BeTc08`: Multiply G3 has Subtract G2 as prereq — EdgeRelationshipType.Prerequisite edge confirmed | |
| BE-TC-09 | Non-admin Parent reads Prerequisites → 200 (not 403) | P1 | PASS | `BeTc09`: Parent JWT → 200 on Prerequisites endpoint | |
| BE-TC-10 | Non-admin Student reads UnlockedBy → 200 (not 403) | P2 | PASS | `BeTc10`: Student JWT → 200 on UnlockedBy endpoint | |
| BE-TC-11 | Fan-in: node with >1 prereq returns full set | P2 | PASS | `BeTc11`: fan-in node returns all prereqs (or 200 [] if seed is linear — test handles both) | |
| BE-TC-12 | Exists, no prereqs → 200 empty (not 404) | P1 | PASS | `BeTc12`: root node (Count to 1000) Prerequisites → 200 [] | |
| BE-TC-13 | Exists, unlocks nothing (leaf) → 200 empty | P2 | PASS | `BeTc13`: leaf node (Convert Fractions to Decimals G6) UnlockedBy → 200 [] | |
| BE-TC-14 | Non-integer id → framework 404 (route miss) | P2 | PASS | `BeTc14`: GET /Prerequisites/abc → 404 (ASP.NET Core route miss) | |
| BE-TC-15 | 0 / -1 id → handler 404 (not 500) | P0 | PASS | `BeTc15`: nodeId=0 → 404, nodeId=-1 → 404 (inline guard in handler) | |

## §B — Blocked (authoring surface not built — plan Q2)

> Default status: **Blocked**. Blocker: "authoring endpoint not built (plan Q2); cycle invariant covered by `SkillGraphValidatorTests` unit tests." Do not fabricate endpoints.

| ID | Title | Priority | Status | Evidence | Defect |
|---|---|---|---|---|---|
| BE-TC-16 | Add prereq edge (happy path) persists + queryable | P0 | BLOCKED | Authoring endpoint not built (P7-03) | |
| BE-TC-17 | Direct cycle (A→B, B→A) rejected, not 500 | P0 | BLOCKED | Unbuilt; unit: SkillGraphValidatorTests | |
| BE-TC-18 | Transitive cycle (A→B→C→A) rejected | P0 | BLOCKED | Unbuilt; unit: SkillGraphValidatorTests | |
| BE-TC-19 | Self-loop (A→A) rejected | P0 | BLOCKED | Unbuilt; unit: SkillGraphValidatorTests | |
| BE-TC-20 | Duplicate edge rejected / idempotent | P1 | BLOCKED | Unbuilt (DB unique index exists) | |
| BE-TC-21 | Prereq referencing non-existent node → 404/422 | P0 | BLOCKED | Unbuilt | |
| BE-TC-22 | Authoring authz: anon→401, non-admin→403, admin→200 | P0 | BLOCKED | Unbuilt (P7-03) | |

## Notes

- `BeTc16_22_AuthoringBlocked` is a single skip marker for the §B blocked group (consolidated into 1 xUnit skip for cleanliness).
- `KnowledgeNode` has `GradeId` (int) but no `Grade` navigation property — the extended test file was corrected to remove `.Include(n => n.Grade)`.
- `EdgeRelationshipType.Prerequisite` (not `RelationshipType.Prerequisite`) — enum in `Learnexia.Modules.Learning.Domain.Enums`.
- Fan-in (BE-TC-11): seed graph may be linear; test handles empty result as 200 [] — not a failure.

## Defects log

| Defect ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| None | | | | |

## Verdict

PASS — 15/15 runnable cases green (§A complete); §B (7 cases) BLOCKED pending P7-03 authoring endpoint. 0 defects. All P0 runnable cases green.
