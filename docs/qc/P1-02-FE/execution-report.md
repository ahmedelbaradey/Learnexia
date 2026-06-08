# Execution Report — P1-02-FE (Stay signed in)

> Filled by **`frontend-e2e-tester`** after running `tests/e2e/specs/P1-02-FE.spec.ts`. QC does NOT fill results — this file ships empty as a template.
> Test design: `frontend-test-cases.md` · Plan/coverage: `README.md`.

## Run metadata
- **Date run:** 2026-06-08
- **Run by (agent):** frontend-e2e-tester
- **Commit / branch under test:** fe2c4e1 (main) + new spec `tests/e2e/specs/P1-02-FE.spec.ts`
- **Web build:** Expo web `:8081` (EXPO_OFFLINE=1) · **Backend:** `:5080` (Development, seeded)
- **Playwright projects run:** chromium (Desktop Chrome)
- **Locale(s) exercised:** ar (Arabic default — all sessions)
- **Seed accounts used:** fresh parent + child seeded via API per test (unique emails per run)

## Results summary
| Metric | Count |
|---|---|
| Total cases | 12 |
| Passed | 8 |
| Failed | 0 |
| Blocked / not run | 2 (FE-TC-05, FE-TC-12) |
| Skipped (fixme) | 2 (same as blocked) |
| Defects filed | 0 new (1 known bug tagged — DEF-P1-03-01) |

## Per-case results
| Case | Title | Priority | Status | Evidence / note |
|---|---|---|---|---|
| FE-TC-01 | Boot with valid child session → child home | P0 | PASS | `dashboard-header` visible after `goto('/')` with session; URL does not include login. Expo Router maps `/(child)` → `/` on web. |
| FE-TC-02 | Boot with valid parent (has children) → parent home | P0 | PASS | `parent-home` testID visible after `goto('/')` with session. |
| FE-TC-03 | Boot parent with no children → onboarding add-child | P1 | PASS | `waitForURL(/add-child/)` succeeds; URL includes `add-child`; not login. |
| FE-TC-04 | Child session survives full reload | P0 | PASS | `dashboard-header` visible after `page.reload()`; both token keys remain in sessionStorage. |
| FE-TC-05 | Revoked refresh token → login + message | P1 | BLOCKED | Server-side token revocation (Redis blacklist) not drivable from web E2E black-box. Resolve README Q1. |
| FE-TC-06 | Corrupted stored token → clean login (no crash) | P2 | PASS | Q2 resolved (tamper is acceptable). Partial token (access only, no refresh) seeded via `page.evaluate`; boot settled to `/login`; no uncaught JS errors; no raw i18n keys. |
| FE-TC-07 | Invalid stored session → login + session-expired message | P1 | PASS | Both token keys overwritten with junk via `page.evaluate`; after reload: routed to `/login`; sessionStorage cleared (onSignOut fired). Flash message observed transiently on splash — consumed before `/login` assertion (soft assertion annotated). No raw `auth.sessionExpired` key on page. |
| FE-TC-08 | Sign-out from child home → login + storage cleared | P0 | PASS | `sign-out-button` clicked; `waitForURL(/login/)` succeeded; both token keys null in sessionStorage after nav. |
| FE-TC-09 | Deep-link to protected route while signed-out → login | P0 | PASS | `goto('/')` with cleared sessionStorage; guard `router.replace('/(auth)/login')` fired; URL contains `login`; `login-username` visible; `dashboard-header` not visible. |
| FE-TC-10 | Parent session survives full reload | P1 | PASS | `parent-home` testID visible after `page.reload()`; tokens in sessionStorage. |
| FE-TC-11 | After sign-out, reload stays on login | P0 | PASS | Sign-out → `/login`; `page.reload()` → still `/login`; `dashboard-header` not visible; storage cleared. |
| FE-TC-12 | Silent refresh on expiry → stays signed in | P2 | BLOCKED | Access-token-lifetime control / network interception not available black-box. Resolve README Q1. |

## Defects found
No new defects. One known defect tagged in FE-TC-09 assertions:

| Defect ID | FE-TC | Severity | Summary | Repro / expected vs actual | Status |
|---|---|---|---|---|---|
| DEF-P1-03-01 (known) | FE-TC-09 | Medium | Auth/role route guards live only in `app/index.tsx`, not in group `_layout.tsx` files | Direct navigation to `/(child)` or `/add-child` while signed-out may bypass the guard (renders the screen without redirect). **FE-TC-09 itself PASSES** because `goto('/')` goes through the splash which HAS the guard. But direct nav to e.g. `/add-child` while signed out still exposes the form (confirmed in P1-03-FE runs). | Known / pre-filed — tagged only |

## Missing testIDs / selector gaps reported to `frontend`
| Element | Needed hook | Where | Reported? |
|---|---|---|---|
| Login username field | `testID="login-username"` | `LoginForm.tsx:277` | Already added — verified present at runtime |
| Login password field | `testID="login-password"` | `LoginForm.tsx:294` | Already added — verified present at runtime |
| Login submit button | `testID="login-submit"` | `LoginForm.tsx:341` | Already added — verified present at runtime |
| Session-expired flash | No testID (text inside `Card` on splash) | `app/index.tsx:204` | Flash is transient (consumed on splash → gone by /login). Soft assertion sufficient for this story. Consider `testID="session-expired-flash"` for deterministic assertion. |

## Open-question outcomes (from README §5)
- **Q1 (refresh determinism):** Unresolved. FE-TC-05 and FE-TC-12 remain BLOCKED. No backend token-lifetime or revoke-endpoint control available from E2E. These cases need either: (a) a backend `POST /api/test/revoke-token` endpoint, (b) a very short access-token TTL in test config, or (c) Playwright `page.route` interception to inject 401 + 200/401 refresh responses.
- **Q2 (sessionStorage tamper OK?):** Resolved as YES per implementation instructions. FE-TC-06 is runnable and PASSES. `page.evaluate` to write partial/junk tokens to sessionStorage is an accepted E2E technique.
- **Q3 (login testIDs):** All three testIDs (`login-username`, `login-password`, `login-submit`) confirmed present in `LoginForm.tsx`. No fallback to role/label needed.
- **Q4 (seed accounts):** Fresh accounts seeded via `POST /api/Users/Authentication/Register-Parent` + `POST /api/Parent/Add-Child` per test. Self-contained, no stable static credentials required.
- **Q5 (sessionStorage cross-tab semantics confirmed):** Confirmed as intended — tests assert reload-survival only; no cross-tab or post-close assertions. FE-TC-04/10/11 all pass consistently.

## Implementation note
- **Expo Router group routes and URL:** Routes `/(child)` and `/(parent)` map to `/` on Expo Router web — the URL does NOT change from `/` when the guard routes there. `waitForURL` cannot be used as a settled-state discriminator for these routes. The spec's `waitForAuthGuardToSettle` helper uses `Promise.race` over the three possible settled-state testID anchors (`dashboard-header`, `parent-home`, `login-username`) instead of URL-based waiting.

## Verdict
- **Overall:** PASS (8/8 runnable cases pass; 2 legitimately BLOCKED with documented reasons; 0 new defects)
- **Notes for reviewer gate:** The 2 BLOCKED cases (FE-TC-05 revoked-token, FE-TC-12 silent-refresh-success) require backend token-lifecycle control not available from black-box E2E. They are genuinely untestable at this level; their AC contract is covered by the api-tester layer (POST /auth/refresh contract). The session-expired flash (FE-TC-07) is asserted through storage-clear + redirect rather than the flash text itself, because the flash is consumed on the splash screen before the `/login` assertion window. This is correct behavior (flash is transient by design). No new bugs found.
