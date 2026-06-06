# Execution Report — P1-02 (Stay signed in: refresh & sign-out)

> **Filled by `api-tester` after running the suite.** QC (test architect) leaves this empty — it only scaffolds the template.
> **Test file:** `backend/tests/Learnexia.IntegrationTests/P1_02_RefreshAndSignOut_Tests.cs`
> **Run command:** `dotnet test backend/tests/Learnexia.IntegrationTests` (filter to `P1_02` where useful).
> Status values: PASS / FAIL / BLOCKED / SKIPPED. For FAIL or BLOCKED, fill the Defect / blocker column.

## Run metadata

| Field | Value |
|---|---|
| Date run | _(fill)_ |
| Commit / branch | _(fill — e.g. `feat/P1-02-stay-signed-in`)_ |
| `dotnet test` exit code | _(fill)_ |
| Total / Pass / Fail / Blocked | _(fill)_ |
| Redis backing in run | _(fill — in-memory `IDistributedCache` fallback expected)_ |
| Expired-JWT seam reached 200 (BE-TC-05)? | _(fill — Yes/No; if No, dependent cases are unreliable)_ |

## Results

| Case ID | Title | Priority | Mapped `[Fact]` | Status | Defect / blocker / notes |
|---|---|---|---|---|---|
| BE-TC-01 | Sign-In returns non-empty refresh token | P0 | | | |
| BE-TC-02 | Register-Parent returns non-empty refresh token | P0 | | | |
| BE-TC-03 | Refresh token persisted/retrievable (Redis round-trip) | P0 | | | |
| BE-TC-04 | Refresh token carries 7-day expireAt | P1 | | | |
| BE-TC-05 | Valid expired access + matching refresh → 200 new access (seam canary) | P0 | | | |
| BE-TC-06 | New access token is a readable valid JWT | P1 | | | |
| BE-TC-07 | Refresh preserves identity claims | P1 | | | |
| BE-TC-08 | Tampered refresh token → 401 | P0 | | | |
| BE-TC-09 | Garbage refresh token → 401 (not 500) | P0 | | | |
| BE-TC-10 | No stored token → 401 (RefreshTokenNotFound) | P1 | | | |
| BE-TC-11 | Expired stored refresh token → 401 (RefreshTokenIsExpired) | P1 | | BLOCKED — no time/TTL seam (README Open Q2) |
| BE-TC-12 | /Refresh-Token never returns 500 for auth failures | P0 | | | |
| BE-TC-13 | Sign-Out with Bearer → 200 Successed=true | P0 | | | |
| BE-TC-14 | Sign-Out persists revocation (entry removed) | P0 | | | |
| BE-TC-15 | Revoked (signed-out) refresh → 401 | P0 | | | |
| BE-TC-16 | Rotation: refreshed token differs from supplied | P0 | | | |
| BE-TC-17 | Rotation: new refresh token carries 7-day expireAt | P2 | | | |
| BE-TC-18 | TokenIsRunning: fresh access token → 400 (not 401/500) | P0 | | | |
| BE-TC-19 | Access token lifetime ≈ 30 min | P1 | | | |
| BE-TC-20 | Refresh token lifetime ≈ 7 days (sign-in) | P1 | | | |
| BE-TC-21 | Replay: original refresh after rotation → 401 | P0 | | | |
| BE-TC-22 | Missing accessToken → 422 with Errors[] | P0 | | | |
| BE-TC-23 | Missing refreshToken → 422 with Errors[] | P0 | | | |
| BE-TC-24 | Both fields missing → 422, error per field | P1 | | | |
| BE-TC-25 | Sign-Out without Bearer → 401 | P0 | | | |
| BE-TC-26 | Sign-Out with malformed Bearer → 401 | P1 | | | |
| BE-TC-27 | Sign-Out idempotent (second call no 500) | P2 | | | |
| BE-TC-28 | Regression: P1-01 sign-in still works | P1 | | | |

## Defects found

| # | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| _(fill)_ | | | | |

## Coverage verdict (filled by `api-tester` post-run)

- All AC-1..AC-8 exercised by at least one passing case? _(fill)_
- Any P0 case failing or blocked? _(fill — list)_
- Seam reliability (BE-TC-05 reached 200)? _(fill)_
- Regression suite (P1-01) green? _(fill)_

---
*`reviewer` consumes this report at the batch gate. Do not edit the case catalog (`backend-test-cases.md`) — record divergences here instead.*
