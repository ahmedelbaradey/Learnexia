# P1-01 — Execution Report (Backend)

> **Owner:** `api-tester` (filled AFTER running). QC scaffolds this empty template only — QC never records results here.
> **Scope:** backend cases from `backend-test-cases.md` (BE-TC-01 … BE-TC-39).
> **How to fill:** one row per case. Status = Pass / Fail / Blocked / N-A. For Fail/Blocked, link a defect and describe. Do not delete rows — mark conditional cases Blocked/N-A with the reason from the case.

## Run metadata
| Field | Value |
|---|---|
| Date run | _to fill_ |
| Run by (agent) | api-tester |
| Branch / commit | _to fill_ |
| Environment | _to fill (e.g. IntegrationTests / Testing profile, DB, Captcha:Enabled=?)_ |
| Test project | `backend/tests/Learnexia.IntegrationTests/P1_01_RegisterParent_Tests.cs` _(or as implemented)_ |
| Command | _to fill (e.g. `dotnet test … --filter P1_01`)_ |

## Result summary
| Metric | Count |
|---|---|
| Total cases | 39 |
| Pass | _to fill_ |
| Fail | _to fill_ |
| Blocked | _to fill_ |
| N-A | _to fill_ |
| P0 failures (release-blocking) | _to fill_ |

## Per-case results
| Case ID | Title (short) | Priority | Status | Evidence / actual status code | Defect ref / notes |
|---|---|---|---|---|---|
| BE-TC-01 | Valid registration → 200 + JWT | P0 | _to fill_ | | |
| BE-TC-02 | IsFirstLogin=true on register | P1 | _to fill_ | | |
| BE-TC-03 | Non-zero UserId | P1 | _to fill_ | | |
| BE-TC-04 | Round-trip register→sign-in | P0 | _to fill_ | | |
| BE-TC-05 | Only Parent role assigned | P0 | _to fill_ | | |
| BE-TC-06 | FullName omitted → local-part, no 500 | P1 | _to fill_ | | |
| BE-TC-07 | FullName provided accepted | P2 | _to fill_ | | |
| BE-TC-08 | Country accepted/persisted | P2 | _to fill_ | | |
| BE-TC-09 | Password too short → 422 | P0 | _to fill_ | | |
| BE-TC-10 | Password no digit → 422 | P0 | _to fill_ | | |
| BE-TC-11 | Password no uppercase → 422 | P0 | _to fill_ | | |
| BE-TC-12 | Password no lowercase → 422 | P0 | _to fill_ | | |
| BE-TC-13 | Password no non-alnum → 422 | P0 | _to fill_ | | |
| BE-TC-14 | Empty password → 422 | P0 | _to fill_ | | |
| BE-TC-15 | 6-char compliant password accepted | P1 | _to fill_ | | |
| BE-TC-16 | Empty email → 422 | P0 | _to fill_ | | |
| BE-TC-17 | Malformed email → 422 | P0 | _to fill_ | | |
| BE-TC-18 | Duplicate email → rejected, no dup | P0 | _to fill_ | | |
| BE-TC-19 | Duplicate → 422 Errors[] preferred | P1 | _to fill_ | | |
| BE-TC-20 | Duplicate case-insensitive | P1 | _to fill_ | | |
| BE-TC-21 | Email surrounding whitespace | P2 | _to fill_ | | |
| BE-TC-22 | AcceptedTerms=false → 422 | P0 | _to fill_ | | |
| BE-TC-23 | AcceptedTerms omitted → 422 | P1 | _to fill_ | | |
| BE-TC-24 | Country >100 chars → 422 | P2 | _to fill_ | | |
| BE-TC-25 | Response never echoes password | P0 | _to fill_ | | |
| BE-TC-26 | Password stored hashed (round-trip) | P0 | _to fill_ | | |
| BE-TC-27 | Extra `roles` field ignored | P0 | _to fill_ | | |
| BE-TC-28 | No Register-Student route (404/405) | P0 | _to fill_ | | |
| BE-TC-29 | Anonymous AddUser → 401 | P0 | _to fill_ | | |
| BE-TC-30 | Captcha disabled → no block | P1 | _to fill_ | | |
| BE-TC-31 | Captcha enabled + bad token → 400 | P2 | _to fill (Blocked if no toggle)_ | | |
| BE-TC-32 | Success envelope keys/spelling | P1 | _to fill_ | | |
| BE-TC-33 | 422 envelope keys | P1 | _to fill_ | | |
| BE-TC-34 | Aggregated validation errors | P2 | _to fill_ | | |
| BE-TC-35 | Empty body `{}` → 422 | P1 | _to fill_ | | |
| BE-TC-36 | Malformed JSON → 400 not 500 | P2 | _to fill_ | | |
| BE-TC-37 | Oversized input no 500 | P2 | _to fill_ | | |
| BE-TC-38 | GET on route → 405 | P2 | _to fill_ | | |
| BE-TC-39 | Seeded accounts still sign in | P1 | _to fill (N-A if not seeded)_ | | |

## Defects found
| # | Severity | Case(s) | Summary | Status |
|---|---|---|---|---|
| _to fill_ | | | | |

## Notes / deviations
- _Record any case run differently than specified, harness limitations, or environment caveats (e.g. captcha toggle, missing seed accounts, role-claim observability method used for BE-TC-05/27)._

## Verdict
- **Overall:** _PASS / FAIL — to fill by api-tester._
- **Release-blocking failures (P0):** _to fill._
- Hand back to `reviewer` for the P1-01 gate against AC-1…AC-6.
