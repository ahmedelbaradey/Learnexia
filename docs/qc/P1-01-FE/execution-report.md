# Execution Report — P1-01-FE (Register screen, web PWA)

> **Owner: `frontend-e2e-tester`.** Fill this in AFTER running `tests/e2e/specs/P1-01-FE.spec.ts`.
> The QC architect leaves this empty — do NOT edit the case definitions in `frontend-test-cases.md`; raise selector/testID gaps in the Defects section.
> Status legend: PASS / FAIL / BLOCKED / SKIPPED.

## Run metadata

| Field | Value |
|---|---|
| Date / time (UTC) | _TBD_ |
| Runner | frontend-e2e-tester |
| Commit / branch | _TBD_ |
| Spec file | `tests/e2e/specs/P1-01-FE.spec.ts` |
| Backend at `:5080`? | _yes / no — note if route stubs were used instead_ |
| Expo web at `:8081`? | _Playwright-owned / reused_ |
| Locale(s) exercised | _ar (default) / en_ |
| Playwright projects | _chromium / mobile (Pixel 7)_ |

## Results summary

| Metric | Count |
|---|---|
| Total cases | 20 |
| PASS | _TBD_ |
| FAIL | _TBD_ |
| BLOCKED | _TBD_ |
| SKIPPED | _TBD_ |

## Per-case results

| Case | Title | Priority | Status | Mode (real / stub) | Notes / evidence (trace, screenshot) |
|---|---|---|---|---|---|
| FE-TC-01 | Form accepts valid input + submittable | P0 | _TBD_ | | |
| FE-TC-02 | Submit blocked without Terms | P0 | _TBD_ | | |
| FE-TC-03 | Checking Terms enables submit / clears error | P1 | _TBD_ | | |
| FE-TC-04 | Success persists tokens + routes to onboarding | P0 | _TBD_ | | _BLOCKED unless backend up_ |
| FE-TC-05 | Arabic default renders RTL | P0 | _TBD_ | | |
| FE-TC-06 | English renders LTR | P1 | _TBD_ | | |
| FE-TC-07 | Invalid email → localized inline error | P0 | _TBD_ | | |
| FE-TC-08 | Password < 6 chars blocked client-side | P0 | _TBD_ | | |
| FE-TC-09 | Country required | P1 | _TBD_ | | |
| FE-TC-10 | Country picker opens + selection sticks | P1 | _TBD_ | | |
| FE-TC-11 | Submit pending/loading state + no double-submit | P1 | _TBD_ | | |
| FE-TC-12 | Password input masked | P2 | _TBD_ | | |
| FE-TC-13 | Duplicate email → localized banner | P0 | _TBD_ | | _BLOCKED — needs seeded duplicate_ |
| FE-TC-14 | Server-weak password → weak-password banner | P0 | _TBD_ | | _BLOCKED unless backend up_ |
| FE-TC-15 | Network failure → generic localized error | P1 | _TBD_ | | |
| FE-TC-16 | Sign-in link / back → login | P1 | _TBD_ | | |
| FE-TC-17 | Email value stays LTR in RTL form | P2 | _TBD_ | | |
| FE-TC-18 | No student self-register route | P0 | _TBD_ | | |
| FE-TC-19 | Parent-only consent banner present | P1 | _TBD_ | | |
| FE-TC-20 | `(auth)` exposes only login + parent register | P2 | _TBD_ | | |

## Defects found

> One row per defect. Link the failing case. For missing-selector issues, name the exact `testID` hook needed and route it back to `frontend`.

| ID | Case(s) | Severity | Type (bug / missing-testID / flake) | Description | Suggested fix / hook |
|---|---|---|---|---|---|
| _D-1_ | | | | | |

## Selector / testID gaps surfaced (route to `frontend`)

> The register screen currently ships NO `testID`s. List the hooks that would have made cases robust (see README Open Q1): `register-form`, `register-fullname`, `register-country`, `register-email`, `register-password`, `register-terms`, `register-submit`, `register-error`. Record which ones actually caused flakiness here.

| Requested testID | On element | Case(s) it would stabilize | Filed to frontend? |
|---|---|---|---|
| | | | |

## Notes for the reviewer gate

- _Coverage delta vs the planned 20 cases (any not run, and why):_ _TBD_
- _Blocked cases + the unblock condition (backend up / seed email):_ _TBD_
- _Overall verdict (ready for reviewer / needs frontend fixes first):_ _TBD_
