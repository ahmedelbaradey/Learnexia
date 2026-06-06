# P1-09 — Execution Report (BACKEND-ONLY)

> **Owner of this file: the testers (`api-tester`).** The QC architect scaffolds this empty template;
> results are filled in **after** the integration suite runs. Do not record results before execution.
>
> Suite under test: `backend/tests/Learnexia.IntegrationTests/P1_09_Me_Tests.cs` (extended per
> `docs/qc/P1-09/backend-test-cases.md`).

## How to fill this in
1. Run the P1-09 integration suite (the `Me` tests + the new cases).
2. For each case below, set **Status** to `Pass` / `Fail` / `Blocked` / `N/A`.
3. On `Fail`/`Blocked`, add a **Defect / Notes** entry: observed vs. expected, status code, body snippet,
   and (for Blocked) the unresolved open question.
4. Fill the run metadata + summary counts at the bottom.

## Run metadata
| Field | Value |
|---|---|
| Date run | _(fill)_ |
| Run by | `api-tester` |
| Branch / commit | _(fill)_ |
| Backend env | _(fill — e.g. local PostgreSQL `Learnexia`, test factory)_ |
| Suite / filter | `P1_09_Me_Tests` |
| Overall result | _(fill — PASS / FAIL)_ |

## Backend results — `GET /api/Users/Me` + child-login locale chain

| Case | Title | Priority | Status | Defect / Notes |
|---|---|---|---|---|
| BE-TC-01 | `Me` no token → 401 | P0 | _(fill)_ | |
| BE-TC-02 | `Me` garbage token → 401 | P0 | _(fill)_ | |
| BE-TC-03 | `Me` returns caller's own id (not another user's) | P0 | _(fill)_ | |
| BE-TC-04 | `?userId=<other>` ignored — no IDOR | P0 | _(fill)_ | |
| BE-TC-05 | Fresh parent → 200 + envelope (`successed`) + roles "Parent" | P0 | _(fill)_ | |
| BE-TC-06 | Superadmin → roles "Admin" + "SuperAdmin" | P1 | _(fill)_ | |
| BE-TC-07 | Admin `Me.id` ≠ parent id | P2 | _(fill)_ | |
| BE-TC-08 | Parent `preferredLanguage` valid locale / documented fallback | P1 | _(fill)_ | |
| BE-TC-09 | Fresh parent → `isFirstLogin = true` | P0 | _(fill)_ | |
| BE-TC-10 | `hasChildren` false→true after Add-Child; B stays false | P1 | _(fill)_ | |
| BE-TC-11 | Child Sign-In → `Me.preferredLanguage` == Add-Child language | P0 | _(fill)_ | |
| BE-TC-12 | Child Sign-In → `Me.learningLanguage` == Add-Child value | P1 | _(fill)_ | |
| BE-TC-13 | Child `Me.grade` == Add-Child grade; parent grade null | P1 | _(fill)_ | |
| BE-TC-14 | Child `Me.roles` = "Student" (not "Parent"); `hasChildren=false` | P0 | _(fill)_ | |
| BE-TC-15 | No sensitive fields leaked (hash/stamps/tokens/password) | P0 | _(fill)_ | |
| BE-TC-16 | `Me.data` exposes full routing field set (tolerant of additive) | P1 | _(fill)_ | |
| BE-TC-17 | Refreshed token authorizes `Me`; post-sign-out behavior confirmed | P1 | _(fill)_ | |
| BE-TC-18 | No "Teacher" role ever returned | P2 | _(fill)_ | |

## Summary counts
| Result | Count |
|---|---|
| Pass | _(fill)_ |
| Fail | _(fill)_ |
| Blocked | _(fill)_ |
| N/A | _(fill)_ |
| **Total** | **18** |

## Defects raised
| ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| _(fill)_ | | | | |

## Blocked-case resolutions (open questions from the plan)
| Case | Open question (see README §4) | Resolution applied |
|---|---|---|
| BE-TC-08 | Expected `preferredLanguage` for a parent (ar / null / DB default)? | _(fill)_ |
| BE-TC-17 (leg 2) | `Me` status after `Sign-Out` — 401 (revoked) or 200 (until JWT expiry)? | _(fill)_ |
