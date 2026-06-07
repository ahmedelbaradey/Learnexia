# Execution Report — P1-12-FE (Web account E2E)

> Filled by **`frontend-e2e-tester`** AFTER running the Playwright suite (`tests/e2e/specs/P1-12-FE.spec.ts`).
> The QC architect scaffolds this template only and never fills results.
> Status values: **Pass** · **Fail** · **Blocked** (env/token/native — keep the reason) · **Skipped**.

## Run context
- Date / commit:
- Branch:
- Spec file(s):
- Web app (Expo) at: http://localhost:8081  — boot OK? (Y/N)
- Backend at :5080 up? (Y/N) — seeded parent? (Y/N) — seeded child? (Y/N)
- `EXPO_PUBLIC_GOOGLE_CLIENT_ID` set in env? (Y/N)
- Reset token available (email pipeline)? (Y/N)
- Locales exercised: ar (RTL) / en (LTR)
- Avatar fixtures available (valid PNG/JPG, disallowed type, >5 MB)? (Y/N)

## Results

| ID | Title | Priority | Result | Notes / defect ref |
|----|-------|----------|--------|--------------------|
| FE-TC-01 | Profile form populates from `/Me` | P0 | | |
| FE-TC-02 | Save persists fullName/phone/country + success | P0 | | |
| FE-TC-03 | Save server error (400/422) localized banner | P1 | | |
| FE-TC-04 | Empty full name save behaviour | P2 | | |
| FE-TC-05 | Cancel resets; email immutable | P1 | | |
| FE-TC-06 | Profile loading state | P2 | | |
| FE-TC-07 | Avatar upload valid PNG/JPG happy path | P0 | | |
| FE-TC-08 | Reject disallowed type (no network call) | P0 | | |
| FE-TC-09 | Reject oversized (>5 MB) | P0 | | |
| FE-TC-10 | Upload pending overlay + buttons disabled | P1 | | |
| FE-TC-11 | Upload server error inline | P1 | | |
| FE-TC-12 | Remove hidden on initials-only avatar | P1 | | |
| FE-TC-13 | Remove happy path → initials fallback | P1 | | |
| FE-TC-14 | Google button disabled / graceful-degrades (env unset) | P0 | | |
| FE-TC-15 | Google live OAuth happy path | P1 | | BLOCKED(env) expected |
| FE-TC-16 | Google in-flight locks email + Apple + MS | P1 | | |
| FE-TC-17 | Google error generic (no enum); cancel silent | P1 | | |
| FE-TC-18 | Apple/Microsoft dimmed placeholders no-op | P2 | | |
| FE-TC-19 | Forgot link routes from Login | P1 | | |
| FE-TC-20 | Forgot email validation | P1 | | |
| FE-TC-21 | Anti-enumeration generic success (identical copy) | P0 | | |
| FE-TC-22 | Forgot server/network error (generic) | P1 | | |
| FE-TC-23 | Back to Sign in returns to Login | P2 | | |
| FE-TC-24 | Reset missing/empty token → invalid block | P0 | | |
| FE-TC-25 | Reset email prefilled read-only (LTR) | P1 | | |
| FE-TC-26 | Reset password policy + confirm match | P0 | | |
| FE-TC-27 | Reset server token-invalid/expired block | P0 | | BLOCKED(token) — partial w/ garbage token |
| FE-TC-28 | Reset happy path → success → login | P1 | | BLOCKED(token) expected |
| FE-TC-29 | Token never echoed in DOM/aria/string | P0 | | |
| FE-TC-30 | Reset other server error (generic) | P2 | | BLOCKED(token) expected |
| FE-TC-31 | Consent NOT pre-checked | P0 | | |
| FE-TC-32 | Submit with consent unchecked blocked | P0 | | |
| FE-TC-33 | Submit with country empty blocked | P1 | | |
| FE-TC-34 | Valid register posts country + acceptedTerms | P0 | | |
| FE-TC-35 | Edit affordance present on card | P1 | | |
| FE-TC-36 | Edit opens sheet pre-filled (real values) | P0 | | |
| FE-TC-37 | Slim field set; no password/learningLanguage; email RO | P0 | | |
| FE-TC-38 | Edit validation (name req, grade 1..6) | P1 | | |
| FE-TC-39 | Save success closes sheet + refetches | P0 | | |
| FE-TC-40 | Save error → banner inside sheet | P1 | | |
| FE-TC-41 | RTL ar vs LTR en + no raw i18n keys + a11y | P0 | | |

## Summary
- Total: 41 · Pass: __ · Fail: __ · Blocked: __ · Skipped: __
- P0 pass rate: __ / 16

## Defects filed (back to `frontend`)
| # | Case ID(s) | Severity | Description | Repro / artifact |
|---|-----------|----------|-------------|------------------|
| | | | | |

## Missing test hooks reported to `frontend` (README Q1)
| Surface / control | Needed `testID` |
|---|---|
| | |

## Blocked items (carry forward)
| Case ID | Blocker | Re-run condition |
|---|---|---|
| FE-TC-15 | unset `EXPO_PUBLIC_GOOGLE_CLIENT_ID` + unautomatable Google dialog | env provisioned |
| FE-TC-27 | reset token + running backend | garbage token + backend up, or real token |
| FE-TC-28 | valid reset token from email | email pipeline available |
| FE-TC-30 | valid reset submit + injected non-token error | token + error injection |

## Notes / environment caveats
-
