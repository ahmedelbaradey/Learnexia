# Execution Report — P1-04 (Link parent to child) — BACKEND

> **Template scaffolded by QC. Filled by `api-tester` AFTER running the tests. QC never fills results.**
> Record one row per `BE-TC-*` case. Attach defect IDs/links for any FAIL.

## Run metadata

| Field | Value |
|---|---|
| Date run | _TBD_ |
| Agent | api-tester |
| Branch / commit | _TBD_ |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Test file | `P1_04_LinkParentChild_Tests.cs` |
| Command | `dotnet test --filter "FullyQualifiedName~P1_04"` |
| Backend build status | _TBD_ |
| Overall result | _TBD (e.g. 33 passed / 0 failed / N skipped)_ |

## Results by case

| Case ID | Title (short) | Priority | Status (PASS/FAIL/BLOCKED/SKIPPED) | Notes / defect ref |
|---|---|---|---|---|
| BE-TC-01 | Auto-link on Add-Child → My-Children (delegated AC-1) | P1 | | |
| BE-TC-02 | Link existing unlinked student (happy path) | P0 | | |
| BE-TC-03 | Linked child summary fields populated | P1 | | |
| BE-TC-04 | Parent linked to two students (M:N parent side) | P0 | | |
| BE-TC-05 | Non-existent email → 400 generic, no leak | P0 | | |
| BE-TC-06 | Non-student (Admin) target → 400 generic | P0 | | |
| BE-TC-07 | Self-link → 400 generic | P0 | | |
| BE-TC-08 | Anti-enumeration: 4 rejections share status+shape | P0 | | |
| BE-TC-09 | Re-link idempotent → 200, no duplicate | P0 | | |
| BE-TC-10 | Idempotency via My-Children count == 1 | P0 | | |
| BE-TC-11 | Cross-family IDOR: B cannot claim A's child → 400 | P0 | | |
| BE-TC-12 | My-Children isolation: B empty when A has child | P0 | | |
| BE-TC-13 | My-Children empty for fresh parent → 200 [] | P1 | | |
| BE-TC-14 | Linked child retrievable via My-Children | P0 | | |
| BE-TC-15 | My-Children no cross-family leak (distinct students) | P1 | | |
| BE-TC-16 | Child linked by two parents (M:N child side) | P1 | | |
| BE-TC-17 | My-Children returns exactly caller's children | P0 | | |
| BE-TC-18 | Unauthenticated Link-Child → 401 | P0 | | |
| BE-TC-18b | Unauthenticated My-Children → 401 | P0 | | |
| BE-TC-19 | Cross-family: B's My-Children unchanged after failed claim | P0 | | |
| BE-TC-20 | Validation: empty ChildEmail → 422 errors[] | P0 | | |
| BE-TC-21 | Validation: malformed ChildEmail → 422 errors[] | P1 | | |
| BE-TC-22 | Non-parent (Basic) → 403 on Link-Child | P0 | | |
| BE-TC-22b | HasChildren flips true after link (routing signal) | P1 | | |
| BE-TC-23 | Admin permitted to call Link-Child (gate level) | P2 | | |
| BE-TC-24 | SuperAdmin My-Children → 200 empty | P2 | | |
| BE-TC-25 | Unlink child not linked to caller → 404 generic | P0 | | |
| BE-TC-25b | Unlink missing/zero ChildId → 422 (verify validator) | P2 | | |
| BE-TC-26 | Body ParentId override ignored (JWT-only) → 400 | P0 | | |
| BE-TC-27 | Unlink blocked when caller is last parent → 400 | P1 | | |
| BE-TC-28 | Concurrent unlink does not orphan child (TOCTOU) | P2 | | |
| BE-TC-29 | Success envelope keys + statusCode 200 | P1 | | |
| BE-TC-30 | 422 envelope keys + errors[] | P1 | | |
| BE-TC-31 | Link rejection is exactly 400 (status precision) | P0 | | |
| BE-TC-32 | All Link-Child failures share identical generic message | P0 | | |
| BE-TC-33 | Oversized/whitespace ChildEmail → 422, no 500 | P2 | | |
| BE-TC-34 | Case-insensitive email match (document behavior) | P2 | | |

## Defects found

| Defect ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| _none yet_ | | | | |

## Notes / deviations / blockers
- _api-tester: record any case marked BLOCKED/SKIPPED with the reason (e.g. BE-TC-16 M:N second-parent path,
  BE-TC-28 concurrency non-determinism). Note any status-code mismatch against the README mapping table as a
  potential defect, not a test bug._
