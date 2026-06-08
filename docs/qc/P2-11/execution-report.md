# Execution Report — P2-11 (Skill dependency graph API)

> **Owner of results:** `api-tester`. The qc-test-designer scaffolds this template only and never fills results.
> Fill **Status** (Pass / Fail / Blocked / N/A), **Evidence** (test name / assertion / status code observed), and **Defect** (id or short note) after running.
> Case definitions live in `backend-test-cases.md` — do not edit them here.

## Run metadata

| Field | Value |
|---|---|
| Date run | _TBD_ |
| Runner | _api-tester_ |
| Build / commit | _TBD_ |
| Environment | Testcontainers PostgreSQL · Testing env · `[Collection("IntegrationTests")]` |
| API base | _TBD_ |
| Seed path | `LearningSeeder.SeedAsync` called directly in `InitializeAsync` |

## Summary

| Outcome | Count |
|---|---|
| Pass | _ |
| Fail | _ |
| Blocked | _ |
| N/A | _ |
| Not Run | 22 |
| **Total** | **22** |

## §A — Testable now

| ID | Title | Priority | Status | Evidence | Defect |
|---|---|---|---|---|---|
| BE-TC-01 | Prerequisites known node → 200 + expected prereq | P0 | Not Run | | |
| BE-TC-02 | UnlockedBy known node → 200 + expected next | P0 | Not Run | | |
| BE-TC-03 | Prerequisites unknown id → 404, never 500 | P0 | Not Run | | |
| BE-TC-04 | UnlockedBy unknown id → 404, never 500 | P0 | Not Run | | |
| BE-TC-05 | Anonymous → 401 (camelCase error body) | P0 | Not Run | | |
| BE-TC-06 | Seed smoke: skill-backed nodes + prereq edges | P1 | Not Run | | |
| BE-TC-07 | Node DTO field contract + `successed` spelling | P1 | Not Run | | |
| BE-TC-08 | Cross-grade prereq edge queryable | P1 | Not Run | | |
| BE-TC-09 | Non-admin Parent reads Prerequisites → 200 (not 403) | P1 | Not Run | | |
| BE-TC-10 | Non-admin Student reads UnlockedBy → 200 (not 403) | P2 | Not Run | | |
| BE-TC-11 | Fan-in: node with >1 prereq returns full set | P2 | Not Run | | |
| BE-TC-12 | Exists, no prereqs → 200 empty (not 404) | P1 | Not Run | | |
| BE-TC-13 | Exists, unlocks nothing (leaf) → 200 empty | P2 | Not Run | | |
| BE-TC-14 | Non-integer id → framework 404 (route miss) | P2 | Not Run | | |
| BE-TC-15 | 0 / -1 id → handler 404 (not 500) | P0 | Not Run | | |

## §B — Blocked (authoring surface not built — plan Q2)

> Default status: **Blocked**. Blocker: "authoring endpoint not built (plan Q2); cycle invariant covered by `SkillGraphValidatorTests` unit tests." Do not fabricate endpoints.

| ID | Title | Priority | Status | Evidence | Defect |
|---|---|---|---|---|---|
| BE-TC-16 | Add prereq edge (happy path) persists + queryable | P0 | Blocked | authoring endpoint not built (Q2) | |
| BE-TC-17 | Direct cycle (A→B, B→A) rejected, not 500 | P0 | Blocked | unbuilt; unit: SkillGraphValidatorTests | |
| BE-TC-18 | Transitive cycle (A→B→C→A) rejected | P0 | Blocked | unbuilt; unit: SkillGraphValidatorTests | |
| BE-TC-19 | Self-loop (A→A) rejected | P0 | Blocked | unbuilt; unit: SkillGraphValidatorTests | |
| BE-TC-20 | Duplicate edge rejected / idempotent | P1 | Blocked | unbuilt (DB unique index exists) | |
| BE-TC-21 | Prereq referencing non-existent node → 404/422 | P0 | Blocked | unbuilt | |
| BE-TC-22 | Authoring authz: anon→401, non-admin→403, admin→200 | P0 | Blocked | unbuilt (P7-03) | |

## Defects log

| Defect ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| _none yet_ | | | | |

## Notes / deviations

- _api-tester records here: fixtures not found, endpoints behaving differently from the case, environment issues, or any case downgraded to N/A with the reason._
