# P1-13b — Execution report (filled by `api-tester` AFTER running)

> **QC scaffolds this template only — testers fill the results.** One row per BE-TC. Record observed status code + body/header shape and any defect ID. Do not edit the test-case definitions here; if a case is wrong, note it in §3.
>
> **Scope:** P1-13b BE-1 (per-endpoint auth rate-limiting). Backend-only run.

## 1. Run metadata
| Field | Value |
|---|---|
| Date run | _TBD_ |
| Run by (agent) | api-tester |
| Branch / commit | _TBD_ |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Suite file(s) | `P1_13b_BE1_AuthRateLimit_Tests.cs` (existing) + _new files TBD_ |
| Command | _TBD (e.g. `dotnet test --filter RateLimitTests`)_ |
| Overall result | _PASS / FAIL / PARTIAL_ |

## 2. Per-case results
| Case ID | Title (short) | Priority | Result (Pass/Fail/Blocked/Won't-Test) | Observed (status / body / header) | Defect ID |
|---|---|---|---|---|---|
| BE-TC-01 | Sign-In exceed → 429 | P0 | | | |
| BE-TC-02 | Register-Parent exceed → 429 | P0 | | | |
| BE-TC-03 | Forgot-Password exceed → 429 | P0 | | | |
| BE-TC-04 | Reset-Password exceed → 429 | P0 | | | |
| BE-TC-05 | Google-SignIn exceed → 429 | P1 | | | |
| BE-TC-06 | Per-IP counters independent | P1 | | | |
| BE-TC-07 | Endpoint burst doesn't throttle other endpoint | P0 | | | |
| BE-TC-08 | EnableEndpointRateLimiting honored (per-endpoint accounting) | P1 | | | |
| BE-TC-09 | 429 body non-empty (middleware active) | P1 | | | |
| BE-TC-10 | 429 Retry-After / rate-limit headers | P2 | | | |
| BE-TC-11 | /health + /health/live whitelisted (no 429) | P0 | | | |
| BE-TC-12 | Production tightened limits resolve (env-gated) | P1 | _Blocked unless Prod test host built_ | | |
| BE-TC-13 | Redis multi-instance shared counter | P2 | _Won't-Test — deferred to P6-06-BE-4_ | | |
| BE-TC-14 | Valid sign-in under limit → 200 + Successed envelope | P0 | | | |

## 3. Defects found
| Defect ID | Severity | Case(s) | Description | Status |
|---|---|---|---|---|
| | | | | |

## 4. Notes / deviations
- _Record any test-case design corrections, harness changes, or env constraints hit during execution._
- _BE-TC-12 / BE-TC-13: note final disposition (built a Prod factory? marked Won't-Test? raised to lead?)._
