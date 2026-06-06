# Execution Report — P1-03 (Backend Add-Child)

> **Template scaffolded by QC (design only). The TESTERS fill this in after running.** QC never fills results.
> Implemented from: `backend-test-cases.md`. Owner of results: **`api-tester`**.

## Run metadata (tester fills)
| Field | Value |
|---|---|
| Branch / commit |  |
| Date run |  |
| Environment (API base URL, DB) |  |
| Backend build status |  |
| Tester (agent) | api-tester |

## Summary (tester fills)
| Metric | Count |
|---|---|
| Total cases | 28 |
| Passed |  |
| Failed |  |
| Blocked |  |
| Not run |  |
| Defects filed |  |

## Per-case results (tester fills `Result` / `HTTP got` / `Notes`)
Result ∈ {PASS, FAIL, BLOCKED, NOT-RUN}.

| ID | Title | Priority | Expected HTTP | HTTP got | Result | Notes / defect ref |
|---|---|---|---|---|---|---|
| BE-TC-01 | Parent adds valid child → 200 + Student account | P0 | 200 |  |  |  |
| BE-TC-02 | Login email = parent-assigned value | P0 | 200 |  |  |  |
| BE-TC-03 | Grade/language/country persisted | P0 | 200 |  |  |  |
| BE-TC-04 | Two children in one session → both listed | P0 | 200 |  |  |  |
| BE-TC-05 | Duplicate after sibling does not undo sibling | P1 | 400 (step 2) |  |  |  |
| BE-TC-06 | Grade 0 → 422 | P0 | 422 |  |  |  |
| BE-TC-07 | Grade 7 → 422 | P0 | 422 |  |  |  |
| BE-TC-08 | Grade -1 / 1000 → 422 | P1 | 422 |  |  |  |
| BE-TC-09 | Empty password → 422 | P0 | 422 |  |  |  |
| BE-TC-10 | Password fails complexity → 422 | P0 | 422 |  |  |  |
| BE-TC-11 | Minimum-valid password → 200 | P1 | 200 |  |  |  |
| BE-TC-12 | `language` not in {ar,en} → 422 | P0 | 422 |  |  |  |
| BE-TC-12b | `country` whitespace-only | P2 | 422 (expected) |  |  |  |
| BE-TC-13 | `learningLanguage` missing/invalid → 422 | P0 | 422 |  |  |  |
| BE-TC-14 | Malformed email → 422 | P0 | 422 |  |  |  |
| BE-TC-15 | No JWT → 401 | P0 | 401 |  |  |  |
| BE-TC-16 | Expired/malformed JWT → 401 | P1 | 401 |  |  |  |
| BE-TC-17 | Student-role token → 403 | P0 | 403 |  |  |  |
| BE-TC-18 | Body cannot inject role/parentId | P0 | 200 (Student only) |  |  |  |
| BE-TC-19 | Auto-link to acting parent | P0 | 200 |  |  |  |
| BE-TC-20 | Child not visible under other parent (family scope) | P0 | n/a (absent) |  |  |  |
| BE-TC-21 | Grade boundaries 1 and 6 persist | P1 | 200 |  |  |  |
| BE-TC-22 | Child signs in with assigned email+password | P0 | 200 |  |  |  |
| BE-TC-23 | Duplicate email → 400 specific message, no account | P0 | 400 |  |  |  |
| BE-TC-24 | Duplicate response same regardless of owner | P1 | 400 (×3, identical) |  |  |  |
| BE-TC-25 | Blank fullName → 422 | P1 | 422 |  |  |  |
| BE-TC-26 | Blank country → 422 | P1 | 422 |  |  |  |
| BE-TC-27 | Role-assign-fail compensating delete | P2 | 500 + no orphan |  |  |  |
| BE-TC-29 | Admin/SuperAdmin token can call (support) | P2 | 200 |  |  |  |
| BE-TC-30 | Oversized inputs → no 500 | P2 | 422/400/200 |  |  |  |

> Counts: row count above is 30 lines (28 distinct cases plus BE-TC-12b and BE-TC-30 sub-IDs); the headline catalog is **28 cases** as in README §1. Tester: reconcile if any sub-case is split out.

## Defects found (tester fills — one row per defect)
| Defect ID | Case(s) | Severity | Summary | Filed to | Status |
|---|---|---|---|---|---|
|  |  |  |  | backend-feature |  |

## Blocked cases — reason (tester fills)
| ID | Blocker |
|---|---|
| BE-TC-27 | (e.g.) no fault-injection hook to force AddToRoleAsync failure from the HTTP surface |

## Tester sign-off
- Overall verdict (PASS / FAIL):
- Unexpected 500s observed:
- Notes for reviewer:
