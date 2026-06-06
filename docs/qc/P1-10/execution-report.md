# Execution Report — P1-10 (Backend)

> **Template — to be filled by `api-tester` after implementing and running `backend-test-cases.md`.**
> The QC architect does **not** fill results. Record one row per `BE-TC-*`: PASS / FAIL / BLOCKED.
> For any FAIL, link the defect (issue/PR) and a one-line root cause. For any BLOCKED, state the blocker.

## Run metadata

| Field | Value |
|---|---|
| Tester (agent) | `api-tester` |
| Date run | _TBD_ |
| Branch / commit | _TBD_ |
| Test file(s) | _e.g. `backend/tests/Learnexia.IntegrationTests/P1_10_AdminSignIn_Tests.cs`_ |
| Host / env | `LearnexiaWebAppFactory` (Testcontainers `pgvector/pgvector:pg16`, `Testing` env) |
| Command | _e.g. `dotnet test backend/tests/Learnexia.IntegrationTests`_ |

## Results

| ID | Title | Prio | Result | Defect / note |
|---|---|---|---|---|
| BE-TC-01 | Admin valid sign-in → 200 + JWT | P0 | _TBD_ | |
| BE-TC-02 | Sign-in envelope shape; no roles in payload | P1 | _TBD_ | |
| BE-TC-03 | JWT carries Admin+SuperAdmin claims | P0 | _TBD_ | |
| BE-TC-04 | Admin token accepted by AdminOnly (round-trip) | P0 | _TBD_ | |
| BE-TC-05 | Wrong password → 400 generic | P0 | _TBD_ | |
| BE-TC-06 | Unknown user → 400 (anti-enumeration parity) | P1 | _TBD_ | |
| BE-TC-07 | Missing fields → 422 | P1 | _TBD_ | |
| BE-TC-08 | Lockout after 5 failures → 400 | P1 | _TBD_ | |
| BE-TC-09 | Deactivated account → 400 | P2 | _TBD_ | (may be BLOCKED — no HTTP deactivation path) |
| BE-TC-10 | `Me` for admin → Admin role | P0 | _TBD_ | |
| BE-TC-11 | `Me` for Parent → Parent only | P1 | _TBD_ | |
| BE-TC-12 | `Me` anonymous → 401 | P1 | _TBD_ | |
| BE-TC-13 | Anonymous AdminOnly GET → 401 | P0 | _TBD_ | |
| BE-TC-14 | Anonymous AdminOnly POST → 401 | P1 | _TBD_ | |
| BE-TC-15 | Parent AdminOnly GET → 403 | P0 | _TBD_ | |
| BE-TC-16 | Parent AdminOnly POST → 403 | P1 | _TBD_ | |
| BE-TC-17 | Basic-role AdminOnly → 403 | P0 | _TBD_ | |
| BE-TC-18 | Admin AdminOnly GET → 200 | P0 | _TBD_ | |
| BE-TC-19 | Admin AdminOnly POST → not 401/403 | P1 | _TBD_ | |
| BE-TC-20 | Tampered token → 401 (not 500) | P0 | _TBD_ | |
| BE-TC-21 | Expired token → 401 | P2 | _TBD_ | (may be BLOCKED — no token-expiry seam) |
| BE-TC-22 | `GetUserProfile` is AdminOnly (401/403/200) | P1 | _TBD_ | |
| BE-TC-23 | Register-Parent yields Parent, never Admin | P0 | _TBD_ | |
| BE-TC-24 | `AddUser` gated (anon→401, Parent→403) | P0 | _TBD_ | |
| BE-TC-25 | Admin can provision a user via gated surface | P2 | _TBD_ | |
| BE-TC-26 | Configured-admin seed no-op when unset | P2 | _TBD_ | |
| BE-TC-27 | Configured-admin seed idempotent, Admin-only | P2 | _TBD_ | (ENV-GATED — requires `AdminSeed:*` host) |
| BE-TC-28 | Admin refresh + sign-out + regression baseline | P0 | _TBD_ | |

## Summary

| Metric | Count |
|---|---|
| Total | 28 |
| PASS | _TBD_ |
| FAIL | _TBD_ |
| BLOCKED | _TBD_ |

## Defects found

_None recorded yet. For each FAIL: ID, observed vs expected, status code/body excerpt, root cause, link._

## Verdict

_PASS / FAIL / PASS-with-blocked — to be set by `api-tester`._
