# Execution Report — P1-05 Role-based access control (BACKEND)

> **Template scaffolded by QC. To be filled by `api-tester` AFTER running the tests.**
> QC does not fill results. Record actual HTTP status / observed behavior per case, mark pass/fail,
> and log any defect with enough detail to reproduce.

## Run metadata

| Field | Value |
|---|---|
| Date run | _TBD_ |
| Agent | api-tester |
| Branch / commit | _TBD_ |
| Harness | `LearnexiaWebAppFactory` (env "Testing", Testcontainers PostgreSQL) |
| Test file(s) | `backend/tests/Learnexia.IntegrationTests/P1_05_RBAC_Tests.cs` (extend existing) |
| Overall verdict | _TBD (PASS / FAIL)_ |

## Results

| Case ID | Title | Priority | Result (PASS/FAIL/BLOCKED) | Actual status / observed | Notes / defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Role list, no token → 401 | P0 | | | |
| BE-TC-02 | Role create, no token → 401 | P0 | | | |
| BE-TC-03 | UserManagement AddUser, no token → 401 | P0 | | | |
| BE-TC-04 | Parent My-Children, no token → 401 | P0 | | | |
| BE-TC-05 | Role list, Parent token → 403 | P0 | | | |
| BE-TC-06 | Role list, Basic token → 403 | P0 | | | |
| BE-TC-07 | Role list, Admin token → 200 + envelope | P0 | | | |
| BE-TC-08 | Role create, Parent token → 403 | P1 | | | |
| BE-TC-09 | AddUser, non-admin token → 403 | P0 | | | |
| BE-TC-10 | AddUser, Admin token → not 401/403 | P1 | | | |
| BE-TC-11 | Sign-In remains anonymous → not 401 | P0 | | | |
| BE-TC-12 | Register/Validate/Refresh anonymous → not 401 | P1 | | | |
| BE-TC-13 | Health probes anonymous → 200 | P1 | | | |
| BE-TC-14 | Invalid bearer token → 401 (not 500) | P0 | | | |
| BE-TC-15 | Cross-family Link-Child deny (Parent B) | P0 | | | |
| BE-TC-16 | Parent B sees 0 of Parent A's children | P0 | | | |
| BE-TC-17 | Actor from JWT, not body (self-scope) | P1 | | | |
| BE-TC-18 | Admin allowed into ParentController | P2 | | | |
| BE-TC-19 | Parent on Student-only quiz route → 403 | P1 | | | |
| BE-TC-19b | Policies only for real modules (Learning, Parent) | P2 | | | |
| BE-TC-20 | GradesController anonymous (GAP) | P0 (finding) | | | Record actual vs desired; lead decision Q1 |
| BE-TC-21 | 401 is real HTTP, not fake 200 | P0 | | | |
| BE-TC-22 | 401 on ParentController is real HTTP | P1 | | | |
| BE-TC-23 | 403 is real HTTP, not fake 200 | P0 | | | |
| BE-TC-24 | appsettings JWT secret is placeholder | P1 | | | |

## Defects found

| # | Case ID | Severity | Summary | Repro | Status |
|---|---|---|---|---|---|
| | | | | | |

## Coverage sign-off (filled after run)

| Acceptance criterion | Covering cases | Verdict |
|---|---|---|
| Wrong role → 403 (AC-1) | BE-TC-05/06/07/08/12/23 | _TBD_ |
| Unauthenticated → 401 (AC-2) | BE-TC-01/02/03/11/21/22 | _TBD_ |
| Students/parents data isolation (AC-4) | BE-TC-15/16/17/18 | _TBD_ |
| Parent not a learner (AC-5) | BE-TC-14/19 | _TBD_ |
| Admin-only curriculum (AC-3) | BE-TC-05..10/12/13 + BE-TC-20 (gap) | _TBD_ |
| Secret out of appsettings (AC-7) | BE-TC-24 | _TBD_ |
| Claims scoped to real modules (AC-6) | BE-TC-19b | _TBD_ |
| Authn/health stay anonymous (AC-8) | BE-TC-11/12/13 | _TBD_ |

## Notes for reviewer / lead
- _TBD_ — especially the outcome of BE-TC-20 (GradesController gap) and whether it blocks P1-05.
