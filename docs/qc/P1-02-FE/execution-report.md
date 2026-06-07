# Execution Report — P1-02-FE (Stay signed in)

> Filled by **`frontend-e2e-tester`** after running `tests/e2e/specs/P1-02-FE.spec.ts`. QC does NOT fill results — this file ships empty as a template.
> Test design: `frontend-test-cases.md` · Plan/coverage: `README.md`.

## Run metadata
- **Date run:** _<yyyy-mm-dd>_
- **Run by (agent):** frontend-e2e-tester
- **Commit / branch under test:** _<sha / branch>_
- **Web build:** Expo web `:8081` · **Backend:** `:5080` (state: _<seeded? migrations applied?>_)
- **Playwright projects run:** _<chromium / mobile (Pixel 7)>_
- **Locale(s) exercised:** _<ar / en / both>_
- **Seed accounts used:** _<child / parent-with-children / parent-no-children — IDs or note>_

## Results summary
| Metric | Count |
|---|---|
| Total cases | 12 |
| Passed | _<n>_ |
| Failed | _<n>_ |
| Blocked / not run | _<n>_ |
| Defects filed | _<n>_ |

## Per-case results
| Case | Title | Priority | Status (PASS / FAIL / BLOCKED / SKIPPED) | Evidence (trace / screenshot / note) |
|---|---|---|---|---|
| FE-TC-01 | Boot with valid child session → child home | P0 | | |
| FE-TC-02 | Boot with valid parent (has children) → parent home | P0 | | |
| FE-TC-03 | Boot parent with no children → onboarding add-child | P1 | | |
| FE-TC-04 | Child session survives full reload | P0 | | |
| FE-TC-05 | Revoked refresh token → login + message | P1 | BLOCKED (Q1) | refresh-revoke control unavailable |
| FE-TC-06 | Corrupted stored token → clean login (no crash) | P2 | BLOCKED (Q2) | storage-tamper technique pending lead OK |
| FE-TC-07 | Invalid stored session → login + session-expired message | P1 | | |
| FE-TC-08 | Sign-out from child home → login + storage cleared | P0 | | |
| FE-TC-09 | Deep-link to protected route while signed-out → login | P0 | | |
| FE-TC-10 | Parent session survives full reload | P1 | | |
| FE-TC-11 | After sign-out, reload stays on login | P0 | | |
| FE-TC-12 | Silent refresh on expiry → stays signed in | P2 | BLOCKED (Q1) | access-token-lifetime / interception control unavailable |

## Defects found
> One row per defect. Link the failing FE-TC. File the bug back to `frontend`.

| Defect ID | FE-TC | Severity | Summary | Repro / expected vs actual | Status |
|---|---|---|---|---|---|
| | | | | | |

## Missing testIDs / selector gaps reported to `frontend`
> From README Q3 and anything discovered at runtime.

| Element | Needed hook | Where | Reported? |
|---|---|---|---|
| Login username field | `testID="login-username"` | `LoginForm.tsx` | |
| Login password field | `testID="login-password"` | `LoginForm.tsx` | |
| Login submit button | `testID="login-submit"` | `LoginForm.tsx` | |
| _<others found>_ | | | |

## Open-question outcomes (from README §5)
- **Q1 (refresh determinism):** _<resolution; did FE-TC-05/12 become runnable?>_
- **Q2 (sessionStorage tamper OK?):** _<resolution; did FE-TC-06 become runnable?>_
- **Q3 (login testIDs):** _<added? still using role/label fallback?>_
- **Q4 (seed accounts):** _<available? which?>_
- **Q5 (sessionStorage cross-tab semantics confirmed):** _<confirmed as intended?>_

## Verdict
- **Overall:** _<PASS / FAIL / PARTIAL — blocked cases noted>_
- **Notes for reviewer gate:** _<anything the reviewer must weigh>_
