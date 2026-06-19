# P7-11 AI-Safety Dashboard — Execution Report (TEMPLATE — testers fill)

> **EXECUTION DEFERRED until PR #184 merges to `main`.** P7-11's controller/handlers are not on `main`; do not run against `main`.
> Filled by **`api-tester`** after #184 merges and after reconciling these cases against the build-wave suite (avoid duplication). The qc-test-designer scaffolds this empty.

**Run date:** _TBD (post-#184 merge)_
**Branch / commit:** _TBD_
**Harness:** `Learnexia.IntegrationTests` (WebApplicationFactory + Testcontainers PostgreSQL) · Docker required
**Filter:** `FullyQualifiedName~P7_11`

## Step 0 — reconciliation with build-wave suite

| Build-wave test exists for | Case IDs it already covers | Action |
|---|---|---|
| _list #184's existing tests here_ | | mark CONFIRM-ON-MERGE cases Covered; implement only the delta |

## Headline

| Metric | Count |
|---|---|
| Passed | _ |
| Failed | _ |
| Skipped / blocked | _ |
| Total | _ |

## Per-case results (priority adds)

| Case ID | Implemented? | Result | Notes / defect ref |
|---|---|---|---|
| BE-TC-11-11 | | | rate arithmetic / zero-total guard |
| BE-TC-11-12 | | | breakdownByReason grouping |
| BE-TC-11-13 | | | subject/language filters (AC gap if no-op) |
| BE-TC-11-17 | | | evals degrade state |
| BE-TC-11-20 | | | usage/cost degrade state |
| BE-TC-11-24 | | | flagged drill-in PII minimality (P0) |
| BE-TC-11-25 | | | flagged pageSize clamp |
| BE-TC-11-27 | | | from≥to → 400 on all routes |

## Defects found

| ID | Severity | Case(s) | Summary | Owner |
|---|---|---|---|---|

## Notes
_Confirm honest-degrade (200 + N/A, never 500) for the eval + usage facets; confirm PII minimality on the flagged drill-in._
