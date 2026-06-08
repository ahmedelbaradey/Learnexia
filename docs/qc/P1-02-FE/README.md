# QC Test Plan & Coverage Report — P1-02-FE (Stay signed in)

> Story: [user-stories/Phase-1-Foundation/P1-02-stay-signed-in.md](../../../user-stories/Phase-1-Foundation/P1-02-stay-signed-in.md) ·
> Task: [tasks/Frontend/student-app/Phase-1-Foundation/P1-02-FE.md](../../../tasks/Frontend/student-app/Phase-1-Foundation/P1-02-FE.md)
> Surface: **student-app web PWA only** (Expo Router, web build at `:8081`). Backend cases are **out of scope** for this run.
> Owner of this folder: QC test architect (design only). Execution + results: `frontend-e2e-tester`.

## 1. Summary

P1-02-FE is the in-app UX for **session persistence**: the app boots, hydrates persisted tokens, routes the user to the right authed home (or to login), keeps them signed in across a full page reload, silently recovers from an expired access token via refresh, surfaces unrecoverable refresh failures by routing back to login with a gentle message, and lets the user sign out so a reload returns to login. Most of the logic is invisible plumbing — these cases assert it only through **observable web behavior** (which screen renders, which URL the router lands on, whether the session-expired message shows).

**In scope (web E2E):**
- App-boot hydration + role routing (signed-out vs parent vs child vs onboarding).
- Session survives a full browser reload (still signed in, lands on the authed home, not login).
- Expired/invalid session → routed to login (with the session-expired flash on the splash).
- Sign-out clears the session → a reload returns to login.
- Protected routes redirect to login when unauthenticated.

**Out of scope (this run):**
- Backend `POST /auth/refresh` / `/auth/logout` contract behavior (Redis blacklist, 401 on revoked token) — that is the backend story (P1-02-BE) and would be `api-tester` cases, not produced here.
- Native (`expo-secure-store`) behavior — web uses its own `sessionStorage` strategy; see Risk R4.
- The full silent-refresh **happy path** (access expires → new token issued mid-session): see Risk R1 / BLOCKED cases — not deterministically drivable from the web UI without backend token-lifetime control or network interception, so it is marked BLOCKED with the reason rather than dropped.

**Counts:** 12 total · 12 frontend · 0 backend.
By priority: **P0 = 6**, **P1 = 4**, **P2 = 2**.
By status: **9 runnable**, **3 BLOCKED** (FE-TC-05, FE-TC-06, FE-TC-12 — see blockers).

## 2. Coverage matrix (acceptance criterion → case)

The story's written acceptance criteria are backend-phrased (refresh/logout/revoked-token HTTP behavior). The **task file** (`P1-02-FE.md`) restates them as the FE-observable obligations below; the matrix traces to those, with the parent story AC noted.

| # | FE obligation (task `P1-02-FE.md`) | Story AC traced | Case(s) | Notes |
|---|---|---|---|---|
| O1 | App boot hydrates session from token storage; routes authed vs guest (`_layout.tsx` + `useAuthRoute`) | AC1 (stay signed in across visits) | FE-TC-01, FE-TC-02, FE-TC-03, FE-TC-09 | Covered: child/parent/onboarding/signed-out routing on boot |
| O2 | Session survives a full page reload (still signed in, lands on authed home, not login) | AC1 | FE-TC-04 (child), FE-TC-10 (parent) | P0 core of this story |
| O3 | Surface silent-refresh failure → route to login with a gentle message (FE-2, api-client interceptor) | AC3 (expired/revoked refresh → re-login) | FE-TC-05 (full refresh-fail), FE-TC-06 (corrupted/invalid stored session), FE-TC-07 (session-expired flash copy) | FE-TC-05 BLOCKED (needs revoked-token control); FE-TC-06 BLOCKED on a deterministic storage-tamper hook |
| O4 | Silent refresh on expiry → new token, session continues (FE-driven by PKG-FE-3) | AC1 (auto-refresh) | FE-TC-12 | BLOCKED — not drivable without backend token-lifetime control or request interception |
| O5 | Sign-out action clears tokens + calls server Sign-Out; reload returns to login (FE-3) | AC2 (sign-out invalidates session) | FE-TC-08 (sign-out → login), FE-TC-11 (sign-out then reload stays on login) | P0; web `authStore.signOut()` clears `sessionStorage` |
| O6 | Protected routes redirect when unauthenticated (`useAuthRoute` guard) | AC3 / NFR security | FE-TC-09 | Deep-link to an authed route while signed-out → redirected to login |

**Gap check:** every FE obligation O1–O6 has at least one P0/P1 case. **No uncovered obligation.** The only AC not asserted end-to-end on web is the *successful* silent-refresh path (O4) — tracked as **FE-TC-12 (BLOCKED)** with its blocker, not dropped. The refresh-failure half of the refresh story (O3) IS covered behaviorally by FE-TC-07's flash assertion and the BLOCKED FE-TC-05/06.

## 3. Risk notes (where cases are weighted and why)

- **R1 — Refresh is invisible and timing-bound (highest risk, hardest to test).** The 401 → single-flight refresh → retry path (`apiClient.ts` `transportRequest`/`refreshTokens`) only fires when a live request gets a 401. From the web UI we cannot force the access token to expire on a deterministic schedule, nor (in pure black-box E2E) revoke the refresh token, without backend cooperation or network interception. Weighted toward the **observable outcomes** instead: the *failure* outcome (flash + redirect to login, FE-TC-07) is assertable by seeding an invalid stored session; the *success* outcome (FE-TC-12) is BLOCKED. Flagged as the top open question for the lead.
- **R2 — Web storage is `sessionStorage`, not `localStorage` (`packages/shared/src/storage/web.ts`).** This is load-bearing for "stay signed in": the session **survives a reload and same-tab navigation**, but is intentionally **cleared when the tab/window closes** and is **not shared to a new tab**. Cases assert reload-survival (FE-TC-04/10) and must NOT assert cross-tab or post-close persistence (that would be a false failure). Called out in the test cases and as an assumption.
- **R3 — No-content-flash routing.** The splash (`app/index.tsx`) must stay visible while `status === 'unknown'` (hydrating) and while a signed-in user's `Me` is still loading; only then does `useAuthRoute` `router.replace` away. A regression here would briefly flash the login screen to a signed-in user. FE-TC-01/02 should assert the user lands on the **authed home** (never bounces through login), but flash-frame timing is inherently flaky in E2E — assert the settled destination, not intermediate frames.
- **R4 — Web vs native divergence.** Token storage is platform-selected (`createPlatformTokenStorage`): `sessionStorage` on web, `expo-secure-store` on native. This run is **web only**; native persistence is a separate concern. Do not run these specs against a native target.
- **R5 — Selector fragility (Arabic default).** Arabic is the default locale, so copy-based selectors are forbidden. The authed homes expose stable testIDs (`dashboard-header` on child home); sign-out and login controls expose `accessibilityLabel` (→ `aria-label`) but the **login email/password fields have no testID** (only `accessibilityLabel`/role). Cases use `getByTestId` first, then role/label. Missing hooks are listed in §5 as open questions for `frontend`.

## 4. Selector reference (verified in source)

| Element | Stable hook | Source |
|---|---|---|
| Splash / boot screen | (no testID) — rendered while resolving; assert transient | `app/index.tsx` |
| Session-expired flash | i18n key `auth.sessionExpired` → EN "Your session expired. Please sign in again." / AR "انتهت جلستك…" | `app/index.tsx` + `resources.ts:62,742` |
| Child authed home | `getByTestId('dashboard-header')` | `app/(child)/index.tsx:282` |
| Child home secondary anchors | `getByTestId('continue-card')`, `getByTestId('subjects-list-section')` | `app/(child)/index.tsx:386,428` |
| Child sign-out button | `getByLabel(t('child.subjects.signOut'))` (`accessibilityLabel`) | `app/(child)/index.tsx:236` |
| Parent sign-out button | `getByLabel(t('auth.signOut'))` (`accessibilityLabel`) | `app/(parent)/index.tsx:40` |
| Login screen | URL `…/login`; first `getByRole('textbox')` is the username field (per smoke spec) | `app/(auth)/login.tsx`, `tests/e2e/specs/smoke.spec.ts` |
| Login username/password fields | **No testID** — only `accessibilityLabel` (`auth.login.labelUsername` / `labelPassword`) → see open question Q3 | `LoginForm.tsx:266,284` |
| Login submit | `getByLabel(t('auth.login.submitButton'))` | `LoginForm.tsx:333` |

Web token storage keys live in `sessionStorage` under `TOKEN_STORAGE_KEYS.{accessToken,refreshToken}` (`packages/shared/src/storage/tokenStorage.ts`) — usable by the tester to seed/tamper/inspect a session deterministically via `page.evaluate`.

## 5. Open questions / assumptions (lead to resolve before implementation)

1. **Q1 (blocks FE-TC-12, FE-TC-05) — How should the tester drive a deterministic refresh?** The successful silent-refresh path and the revoked-refresh-token failure are not black-box drivable from the web UI. Options: (a) backend test config with a very short access-token TTL + a revoke endpoint; (b) Playwright `page.route` interception to force a 401 + a 200/401 refresh response; (c) accept these as not-web-testable and cover them in `api-tester`. **Assumption pending an answer:** these stay BLOCKED. If (b) is approved, FE-TC-05/12 become runnable as interception specs.
2. **Q2 (affects FE-TC-06) — Is tampering with `sessionStorage` to simulate an invalid stored session an acceptable E2E technique?** It is the cleanest way to assert the "corrupted/expired stored session → login + flash" outcome without backend timing control. Assumption: yes, via `page.evaluate` writing junk tokens to the `TOKEN_STORAGE_KEYS`. Confirm.
3. **Q3 — Login email/password fields need testIDs.** They expose only `accessibilityLabel`/role today (`LoginForm.tsx`). Specs that must *log in* (to seed a real session for reload/sign-out cases) will rely on `getByRole('textbox')` ordering + the password being the `secureTextEntry` field — brittle. Recommend `frontend` add `testID="login-username"` / `testID="login-password"` / `testID="login-submit"`. Reported to `frontend`, not blocking (role/label fallback exists).
4. **Q4 — Confirm the seed account(s).** Reload/sign-out cases need a real signed-in session. They assume a seedable parent (with ≥1 child → routes to `(parent)`) and a child login (routes to `(child)`) are available per the P1-01/P1-03 seed data described in HANDOFF. If no stable seed credentials exist for web E2E, FE-TC-04/08/10/11 become blocked on test-data provisioning — flag to lead.
5. **Q5 — `sessionStorage` semantics are intended, not a bug.** Assumption: "stay signed in across visits" on web means *across reloads within a browser session*, NOT across a tab close (sessionStorage is wiped on close). Cases are written to that contract. If the product expects survival across tab-close on web, that's a `localStorage`/cookie change in `web.ts` and a new story — out of scope here.

## 6. Handoff

- `frontend-e2e-tester` implements **`frontend-test-cases.md`** as `tests/e2e/specs/P1-02-FE.spec.ts` (one `test()` per FE-TC, IDs in titles), following the harness in `tests/e2e/README.md` (backend at `:5080`, Playwright owns Expo web at `:8081`).
- BLOCKED cases (FE-TC-05, FE-TC-06, FE-TC-12) are written as `test.fixme`/`test.skip` with the blocker in the title until the lead resolves Q1/Q2.
- Any missing `testID` (Q3) is filed back to `frontend`, not worked around with CSS/copy selectors.
- Results are recorded in **`execution-report.md`** (template already scaffolded here — testers fill pass/fail per case + defects; QC does not fill results).
