# P2-12 — Execution Report (BACKEND)

> **Template — filled by the testers, not by qc-test-designer.**
> `api-tester` runs `backend-test-cases.md` against the running API and records results here.
> Do not edit the case catalog from this file; if a case is wrong, file it under "Defects / discrepancies" and report back to the lead.

## Run metadata

| Field | Value |
|---|---|
| Date / time | _(fill)_ |
| Tester agent | api-tester |
| Build / commit under test | _(fill — git SHA)_ |
| Environment | _(fill — Development, BE base URL, DB)_ |
| Auth | _(fill — seeded Parent A / Parent B JWTs)_ |

## Results — backend (`backend-test-cases.md`)

| Case ID | Title | Priority | Result (PASS / FAIL / BLOCKED / SKIP) | Notes / evidence |
|---|---|---|---|---|
| BE-TC-01 | GET returns all 4 user-facing categories | P0 | | |
| BE-TC-02 | First GET returns defaults, not 404 | P0 | | |
| BE-TC-03 | GET is side-effect-free (no persist) | P1 | | |
| BE-TC-04 | GET anonymous → 401 | P0 | | |
| BE-TC-05 | GET never surfaces categories 4/5/6 | P1 | | |
| BE-TC-06 | PUT all 4 categories → success | P0 | | |
| BE-TC-07 | PUT then GET round-trips (persisted) | P0 | | |
| BE-TC-08 | PUT subset upserts only those | P1 | | |
| BE-TC-09 | PUT empty list → 422 | P0 | | |
| BE-TC-10 | PUT unknown category → 422 | P0 | | |
| BE-TC-11 | PUT duplicate category → 422 | P0 | | |
| BE-TC-12 | PUT category 4–6 accepted, hidden by GET | P2 | | |
| BE-TC-13 | PUT anonymous → 401 | P0 | | |
| BE-TC-14 | PUT/GET self-scoped, no cross-user (IDOR) | P0 | | |
| BE-TC-15 | Unlink co-parented child succeeds | P0 | | |
| BE-TC-16 | Last-parent guard → 400 | P0 | | |
| BE-TC-17 | Unlink not-linked child → generic 404 | P0 | | |
| BE-TC-18 | Unlink non-existent → same 404 shape | P1 | | |
| BE-TC-19 | Unlink ignores body identity; concurrent atomic | P1 | | |
| BE-TC-20 | Unlink anonymous → 401 | P0 | | |
| BE-TC-21 | Unlink ChildId <= 0 → 422 | P1 | | |
| BE-TC-22 | My-Children lists only caller's children | P0 | | |
| BE-TC-23 | My-Children empty → empty success | P2 | | |
| BE-TC-24 | Link already-linked → 409 | P1 | | |

## Summary

| Metric | Count |
|---|---|
| Total | 24 |
| PASS | |
| FAIL | |
| BLOCKED | |
| SKIP | |
| P0 failures (release-blocking) | |

## Defects / discrepancies found

> One row per defect. Reference the case ID, observed vs expected, and severity. Flag any case where the implemented behaviour diverges from the catalog's expectation (e.g. PUT full-replace vs partial-upsert per README open-Q3, or PUT accepting categories 4–6 per BE-TC-12).

| # | Case ID | Observed | Expected | Severity | Notes |
|---|---|---|---|---|---|
| | | | | | |

## Blockers encountered

> Anything that prevented a case from running (missing seed, harness gap, unmerged dependency). Name the blocker per case.
