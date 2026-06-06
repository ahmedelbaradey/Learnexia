# P1-13 — Execution Report (filled by `api-tester` after running)

> **Owner of results:** `api-tester`. The qc-test-architect created this template and does **not** fill results.
> Record one row per `BE-TC-*` case from [`backend-test-cases.md`](./backend-test-cases.md). Status ∈ `PASS` / `FAIL` / `BLOCKED` / `SKIPPED` / `CODE-REVIEW-VERIFIED`.
> For each `FAIL`, open a defect block below and reference it in the row.

- **Run date:** _TBD_
- **Run by:** _TBD (api-tester)_
- **Harness:** xUnit + Testcontainers PostgreSQL, `WebApplicationFactory<Program>` (`Testing` env).
- **Suite files:** _e.g. `P1_13_BE1_Lockout_Tests.cs`, `P1_13_BE2_SignInSafety_Tests.cs`, `P1_13_BE3_AdminSeed_Tests.cs`, existing `P1_13_BE4_Captcha_Tests.cs`_
- **Command:** _TBD_
- **Overall result:** _TBD (X passed / Y failed / Z blocked of 34)_

---

## Results

### Area A — Lockout (BE-1)
| Case | Title | Status | Test method | Notes / defect |
|------|-------|--------|-------------|----------------|
| BE-TC-01 | Wrong password below threshold = invalid-credentials | | | |
| BE-TC-02 | Attempts 1–4 stay invalid-credentials | | | |
| BE-TC-03 | Correct password works under threshold | | | |
| BE-TC-04 | 5th failure locks (record observed boundary) | | | observed lock attempt #: ___ |
| BE-TC-05 | Locked account rejects correct password | | | |
| BE-TC-06 | Beyond-threshold attempts stay locked | | | |
| BE-TC-07 | Success resets the failed-attempt counter | | | |
| BE-TC-08 | Locked message localized (ar) | | | |
| BE-TC-09 | 5-min auto-expiry (observation) | | | |

### Area B — Sign-in safety & anti-enumeration (BE-2)
| Case | Title | Status | Test method | Notes / defect |
|------|-------|--------|-------------|----------------|
| BE-TC-10 | Non-existent user → 400 (not 404) | | | |
| BE-TC-11 | Existing user + wrong password → 400 | | | |
| BE-TC-12 | Not-found vs wrong-password indistinguishable | | | |
| BE-TC-13 | Anti-enumeration parity (en) | | | |
| BE-TC-14 | Anti-enumeration parity (ar) | | | |
| BE-TC-15 | Deactivated account → LoginAccountDeactivated | | | |
| BE-TC-16 | Timing-oracle behavioral parity | | | |
| BE-TC-17 | Success envelope well-formed | | | |
| BE-TC-18 | Missing fields → 422 (or record actual) | | | actual status: ___ |
| BE-TC-20 | Exception → generic 500, no ex.Message | | | |
| BE-TC-21 | Exception detail logged not returned | | | |
| BE-TC-22 | Locked message distinct from invalid-creds (trade-off pin) | | | |
| BE-TC-23 | Email case-insensitivity no enum signal | | | |

### Area C — Admin seed (BE-3)
| Case | Title | Status | Test method | Notes / defect |
|------|-------|--------|-------------|----------------|
| BE-TC-24 | Blank AdminSeed → no admin, app boots | | | |
| BE-TC-25 | Configured admin signs in; legacy dev-only | | | |
| BE-TC-19 | Idempotency across boots/re-seed (BLOCKED) | BLOCKED | | needs re-seed fixture (Q3) |
| BE-TC-34 | Legacy creds NOT seeded in non-Development (BLOCKED) | BLOCKED | | needs Production boot fixture (Q3) |

### Area D — CAPTCHA on register (BE-4) — verify existing suite
| Case | Title | Status | Test method (existing) | Notes / defect |
|------|-------|--------|------------------------|----------------|
| BE-TC-26 | Disabled default → register w/o token = 200 | | AC-DEF-* | |
| BE-TC-27 | Enabled + fail → 400, no account | | AC-FAIL-* | |
| BE-TC-28 | Enabled + pass → 200, account retrievable | | AC-PASS-* | |
| BE-TC-29 | Null token fail-closed → 400 | | AC-NULL-* | |
| BE-TC-30 | Failure no internal leak | | AC-FAIL-5 | |
| BE-TC-31 | Validation 422 precedes CAPTCHA | | Regression terms/email | |
| BE-TC-32 | No role injection (Parent only) | | _add if missing_ | |
| BE-TC-33 | GuardCaptcha prod fail-fast (BLOCKED) | BLOCKED | | needs Production boot fixture (Q3) |

---

## Defects found
> One block per defect. Link the failing `BE-TC-*` ID.

### DEF-01 — _title_
- **Severity:** _Critical / High / Medium / Low_
- **Case(s):** _BE-TC-___
- **Observed:** _what happened (status / body / message)_
- **Expected:** _per the case_
- **Repro:** _request + preconditions_
- **Suspected location:** _file:line_

---

## Open questions raised during execution
- _e.g. observed lockout boundary attempt number (Q4); whether the throwing-double fixture for BE-TC-20/21 was feasible (Q2); decision on the Production boot fixture for BE-TC-19/33/34 (Q3)._
