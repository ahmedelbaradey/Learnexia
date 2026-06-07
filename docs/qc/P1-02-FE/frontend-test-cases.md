# Frontend E2E Test Cases — P1-02-FE (Stay signed in)

> Target agent: **`frontend-e2e-tester`** · Surface: **student-app web PWA** (`:8081`, Playwright) · Backend prerequisite: `:5080`.
> Implement as `tests/e2e/specs/P1-02-FE.spec.ts` — one `test()` per FE-TC, the ID in the title.
> **Selector rule:** `getByTestId` first, then `getByRole`/`getByLabel`. Arabic is the default locale — **never** select on copy except where a case explicitly asserts the localized session-expired message (assert both EN and AR forms, or run in a known locale).
> **Storage note (web):** the session lives in `sessionStorage` under `TOKEN_STORAGE_KEYS.{accessToken,refreshToken}` (`packages/shared/src/storage/tokenStorage.ts`). It survives a reload and same-tab navigation, is wiped on tab close, and is not shared to a new tab. Seed/tamper/inspect via `page.evaluate`. Do NOT assert cross-tab or post-close persistence.
> See `README.md` §3 (risks) and §5 (open questions Q1–Q5) before implementing. BLOCKED cases are written as `test.fixme` with the blocker in the title.

Reusable helper (describe in the spec, do not hardcode credentials): **`signIn(page, role)`** — go to `/login`, fill the username field (first `getByRole('textbox')`) + password (the `secureTextEntry` field), submit (`getByLabel(t('auth.login.submitButton'))`), and wait for the authed home (`getByTestId('dashboard-header')` for child, the parent home anchor for parent). Replace with `getByTestId('login-username'/'login-password'/'login-submit')` once `frontend` adds them (open question Q3).

---

## Group A — App boot hydration + role routing

### FE-TC-01 — Boot with a valid child session lands on the child home (not login)
- **Type:** functional / state (boot)
- **Priority:** P0
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** A seeded student account that logs in as a student (routes to `(child)`). (Q4.)
- **Steps:**
  1. Sign in as the child via `signIn(page, 'child')` so a session is persisted in `sessionStorage`.
  2. Navigate the browser to the app root `/` (cold boot of the SPA with the session present).
  3. Wait for the guard to resolve (splash → home).
- **Expected result:** The app settles on the **child authed home** — `getByTestId('dashboard-header')` is visible; the URL is under `(child)`, NOT `/login`. The user never lands on the login screen.
- **Traces to:** O1, AC1 (stay signed in across visits).

### FE-TC-02 — Boot with a valid parent (has children) session lands on the parent home
- **Type:** functional / state (boot)
- **Priority:** P0
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** A seeded parent account with **≥1 linked child** (so `useAuthRoute` routes to `(parent)`, not onboarding). (Q4.)
- **Steps:**
  1. Sign in as the parent via `signIn(page, 'parent')`.
  2. Navigate to `/` (cold boot with the session present).
  3. Wait for the guard to resolve.
- **Expected result:** The app settles on the **parent home** (parent route group; the parent sign-out control `getByLabel(t('auth.signOut'))` is present). URL is NOT `/login` and NOT the onboarding add-child screen.
- **Traces to:** O1, AC1.

### FE-TC-03 — Boot with a parent that has no children routes to onboarding (add-child)
- **Type:** functional / state (boot) / negative-of-happy-routing
- **Priority:** P1
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** A seeded parent account with **zero linked children**. (Q4.)
- **Steps:**
  1. Sign in as the childless parent.
  2. Navigate to `/`.
  3. Wait for the guard to resolve.
- **Expected result:** The app settles on the **onboarding add-child** screen (`(onboarding)/add-child`), NOT the parent dashboard and NOT login — confirms `useAuthRoute`'s `hasChildren` branch.
- **Traces to:** O1.

### FE-TC-09 — Deep-link to a protected route while signed-out redirects to login
- **Type:** auth-authz / negative
- **Priority:** P0
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** No session — `sessionStorage` cleared (fresh context / `page.evaluate(() => sessionStorage.clear())`).
- **Steps:**
  1. With no session, navigate directly to an authed deep link, e.g. `/(child)` (or a child subjects/lessons route).
  2. Wait for the guard to resolve.
- **Expected result:** The router `replace`s to **`/(auth)/login`** — the login screen is shown (first `getByRole('textbox')` visible, URL `…/login`). The protected content (`dashboard-header`) is NOT rendered.
- **Traces to:** O6, AC3 / security.

---

## Group B — Session survives reload (core of the story)

### FE-TC-04 — Child session survives a full page reload (still signed in)
- **Type:** persistence / functional
- **Priority:** P0
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** Seeded child account. (Q4.)
- **Steps:**
  1. Sign in as the child; confirm the child home (`dashboard-header`) is visible.
  2. Perform a full browser reload (`page.reload()`).
  3. Wait for the guard to resolve after the reload.
- **Expected result:** After reload the user is **still on the child home** (`getByTestId('dashboard-header')` visible), NOT bounced to `/login`. The session in `sessionStorage` (access + refresh keys) is still present.
- **Traces to:** O2, AC1 (core "stay signed in").

### FE-TC-10 — Parent session survives a full page reload (still signed in)
- **Type:** persistence / functional
- **Priority:** P1
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** Seeded parent with ≥1 child. (Q4.)
- **Steps:**
  1. Sign in as the parent; confirm the parent home renders.
  2. `page.reload()`.
  3. Wait for the guard to resolve.
- **Expected result:** After reload the user is **still on the parent home** (parent sign-out control present), NOT on `/login`.
- **Traces to:** O2, AC1.

---

## Group C — Sign-out clears the session

### FE-TC-08 — Sign-out from the child home returns to login
- **Type:** functional / auth
- **Priority:** P0
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** Seeded child, signed in on the child home. (Q4.)
- **Steps:**
  1. Sign in as the child; confirm `dashboard-header`.
  2. Press the sign-out control — `getByLabel(t('child.subjects.signOut'))`.
  3. Wait for navigation.
- **Expected result:** The app `replace`s to **`/(auth)/login`** (login screen visible). The session keys in `sessionStorage` are **cleared** (assert via `page.evaluate` that both token keys are absent) — confirming `authStore.signOut()` cleared web storage. Local sign-out completes even though the server `Sign-Out` call is best-effort.
- **Traces to:** O5, AC2 (sign-out invalidates session).

### FE-TC-11 — After sign-out, a reload keeps the user on login (session not resurrected)
- **Type:** persistence / regression
- **Priority:** P0
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** Seeded child. (Q4.) Continues from / mirrors FE-TC-08.
- **Steps:**
  1. Sign in as the child, then sign out (as FE-TC-08) → on `/login`.
  2. Perform a full browser reload (`page.reload()`).
  3. Wait for the guard to resolve.
- **Expected result:** After reload the app is **still on `/login`** — boot hydration finds no tokens in `sessionStorage`, so `status` resolves to `signed-out`. The authed home is NOT reachable. (Guards against a stale in-memory session re-hydrating.)
- **Traces to:** O5, AC2.

---

## Group D — Expired / invalid session → routed to login (refresh-failure surface)

### FE-TC-07 — An invalid stored session resolves to login with the session-expired message
- **Type:** state (error) / RTL-i18n
- **Priority:** P1
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** A signed-in child session first (to land on an authed screen), then the stored/runtime session is invalidated so the next authenticated request 401s and refresh fails. Drive via the approved tamper technique (Q2): after sign-in, overwrite the `sessionStorage` token keys with junk values via `page.evaluate`, then trigger a fresh authenticated fetch (reload, which re-runs `useMe`).
- **Steps:**
  1. Sign in as the child; confirm `dashboard-header`.
  2. Overwrite both `TOKEN_STORAGE_KEYS` values in `sessionStorage` with an invalid token string (`page.evaluate`).
  3. Reload the page so the app re-hydrates the (now-invalid) tokens and `useMe` fires an authenticated request that 401s; the single-flight refresh then fails (invalid refresh token).
  4. Wait for the guard to settle.
- **Expected result:** The api-client `onSignOut` hook fires → the **session-expired flash** appears (i18n `auth.sessionExpired` — EN "Your session expired. Please sign in again." / AR "انتهت جلستك. يرجى تسجيل الدخول مجدداً.") AND the app routes to **`/(auth)/login`**. The flash text is the **localized string, not the raw key** `auth.sessionExpired`. Session keys are cleared from `sessionStorage`.
- **Traces to:** O3, AC3 (expired/revoked → re-login prompt).
- **Note:** If overwriting tokens with junk does not reliably produce a backend 401 (e.g. a malformed token is rejected before refresh), coordinate with Q1 option (b) network interception. Keep the assertion (flash + redirect) identical.

### FE-TC-05 — Revoked refresh token → silent refresh fails → login + gentle message  **[BLOCKED — see Q1]**
- **Type:** auth / state (error)
- **Priority:** P1
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** A signed-in child session whose **refresh token has been server-side revoked** (Redis blacklist via the backend logout/revoke), while the access token is expired/invalid — so the 401 → refresh path runs end-to-end against the real backend and the refresh endpoint returns 401.
- **Steps:** (1) Sign in. (2) Revoke the refresh token server-side. (3) Trigger an authenticated request that 401s. (4) Observe the refresh attempt failing.
- **Expected result:** Same observable outcome as FE-TC-07 — session-expired flash + redirect to `/login`, storage cleared — but exercised through the **real revoked-token contract** end-to-end.
- **Traces to:** O3, AC3 (revoked refresh token → 401 → re-login).
- **BLOCKER:** Requires a deterministic way to revoke the refresh token for the web E2E session (backend revoke/logout hook or a short-TTL test config) — not available black-box from the web UI. Resolve Q1; until then `test.fixme`. (This is the truest mapping of the parent story's AC3; the web-only surrogate is FE-TC-07.)

### FE-TC-06 — Corrupted/non-JSON stored token resolves cleanly to login (no crash) **[BLOCKED — pending Q2 decision]**
- **Type:** negative / boundary / resilience
- **Priority:** P2
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** `sessionStorage` seeded with a malformed token state — e.g. only the access key present and the refresh key missing, or empty-string values (`web.ts` `getTokens()` returns `null` unless BOTH keys are non-empty).
- **Steps:**
  1. With no active session, `page.evaluate` to set only one of the two `TOKEN_STORAGE_KEYS` (partial/corrupt state).
  2. Navigate to `/` (cold boot).
  3. Wait for the guard to resolve.
- **Expected result:** Hydration treats the partial/corrupt state as **no session** (`getTokens()` returns `null` → `status: signed-out`) and routes to **`/login`** without crashing or hanging on the splash. No uncaught error in the console.
- **Traces to:** O3 (resilience of boot hydration).
- **BLOCKER:** Depends on lead confirming the `sessionStorage` tamper technique is acceptable for E2E (Q2). Low-risk and self-contained; flip to runnable once Q2 is answered.

---

## Group E — Successful silent refresh (continuity)

### FE-TC-12 — Expired access token is silently refreshed mid-session; user stays signed in **[BLOCKED — see Q1]**
- **Type:** functional / persistence
- **Priority:** P2
- **Target agent:** `frontend-e2e-tester`
- **Preconditions / seed:** A signed-in session where the **access token expires while the refresh token is still valid**, so a subsequent authenticated request 401s and the single-flight refresh succeeds (new access token issued).
- **Steps:** (1) Sign in. (2) Cause the access token to expire (short-TTL test config) without revoking the refresh token. (3) Trigger an authenticated request (navigate to a screen that fetches, or reload). (4) Observe.
- **Expected result:** The user **stays signed in** — the authed screen renders with fresh data, NO session-expired flash, NO redirect to login. The `sessionStorage` access token value has been **replaced** with a new one (the refresh wrote through storage), confirming the silent refresh + one-retry path.
- **Traces to:** O4, AC1 (auto-refresh on expiry → new access token).
- **BLOCKER:** Requires backend control of access-token lifetime (or Playwright `page.route` interception to inject a 401 then a 200 refresh). Not black-box drivable from the web UI. Resolve Q1; until then `test.fixme`. This is the one acceptance behavior with **no runnable web case** — see README §2 gap note.

---

## Implementation notes for the tester
- Use a fresh browser context per test (no leaked `sessionStorage`); explicitly `sessionStorage.clear()` in setup for the signed-out cases (FE-TC-09).
- Assert the **settled** destination, not intermediate splash frames (Risk R3) — the splash is expected transiently.
- For sign-out and session-expired cases, assert BOTH the route (`/login`) AND the storage state (token keys absent) so a UI-only redirect without a real clear is caught.
- Run at least the locale-sensitive case (FE-TC-07) in a known locale, or assert the message via the resolved i18n string for the active locale — never assert the raw key.
- Tag BLOCKED cases with `test.fixme(true, 'FE-TC-0X BLOCKED: <blocker>')` and the FE-TC ID so the execution report can account for them.
