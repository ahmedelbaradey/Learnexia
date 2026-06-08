# Frontend E2E Test Cases — P1-09-FE (Auth & Onboarding chain)

**Target agent:** `frontend-e2e-tester`
**Surface:** student-app web PWA (Playwright, `tests/e2e/`, `baseURL` http://localhost:8081)
**Implement as:** `tests/e2e/specs/P1-09.spec.ts`
**Prereq:** backend up at `:5080` (Postgres stack); seeded actor accounts (see Preconditions / §A1 in README).

## Conventions for every case below
- **Selector order:** `getByTestId` → `getByRole` / `getByLabel` → (last resort) text. **Arabic is the default locale — do NOT assert on copy for pass/fail.** When a `testID` is missing, file it back to `frontend` (see README §4) and use the documented role/label fallback noted per case.
- **Locale:** default boot is Arabic (`ar`, RTL). Cases that need English drive the `LocaleThemeControls` language radio (role `radio`, label `common.prefs.switchToEnglish`).
- **Routing groups** (`(parent)`, `(child)`, `(onboarding)`, `(auth)`) are Expo Router groups — they may not appear literally in the web URL. Anchor routing assertions on the **landing element's testID/role** first, `page.url()` second.

---

## Group A — Splash / Boot + no-content-flash (DS §3, Screen 1)

### FE-TC-01 — Splash renders on cold boot (signed-out, no session)
- **Type:** functional / state (loading)
- **Priority:** P0
- **Preconditions / seed:** No persisted token (fresh browser context / cleared storage).
- **Steps:**
  1. `page.goto('/')`.
  2. Wait for the app body to be non-empty.
  3. Assert the splash is visible: `getByTestId('splash-screen')` (fallback: brand wordmark "Learnexia" — forced Latin/LTR in both locales — and the loader label `common.splash.loading`).
- **Expected:** Splash/boot screen renders with brand wordmark + loading indicator before any redirect. No blank/null screen. Response `ok()`.
- **Traces to:** DS Screen 1, DS §3 (`unknown` → splash).

### FE-TC-02 — No content flash: splash persists during `unknown` (hydration)
- **Type:** state / regression
- **Priority:** P0
- **Preconditions / seed:** Fresh context.
- **Steps:**
  1. `page.goto('/')`.
  2. Immediately (within the first paint window) assert the splash element is present (not a blank body, not the login form, not `null`).
  3. Then allow navigation to resolve.
- **Expected:** While `authStore.status === 'unknown'`, the splash is the only thing shown — never a blank screen or a premature login flash. (DS §3 "No content flash rule".)
- **Traces to:** DS §3.

### FE-TC-03 — No content flash: signed-in user holds on splash while `Me` fetches
- **Type:** state
- **Priority:** P1
- **Preconditions / seed:** A valid persisted token for any seeded user (sign in once, reload).
- **Steps:**
  1. Sign in as a seeded user; reload the page (token now persisted → `status` becomes `signed-in` quickly, but `Me` is still in flight).
  2. Assert that during the `Me` fetch window the splash (`splash-screen` / loader) is shown — NOT the login screen, NOT a blank screen.
  3. Allow `Me` to resolve and the guard to redirect.
- **Expected:** Splash persists for the `signed-in` + `Me`-loading phase; redirect to the role target happens only after `Me` resolves. (DS §3 row "signed-in, no Me yet → splash persists".)
- **Traces to:** DS §3, `useAuthRoute` `isResolving`.

---

## Group B — Routing by `Me` (DS §3, Screens 11/12)

### FE-TC-04 — Signed-out boot redirects to Login
- **Type:** auth-routing / functional
- **Priority:** P0
- **Preconditions / seed:** No token.
- **Steps:**
  1. `page.goto('/')`.
  2. Wait for the guard to resolve.
  3. Assert the Login screen is shown: a username/email `textbox` is visible (role `textbox`, first field) and the create-account link (`auth.login.createAccount`) is present.
- **Expected:** From splash, signed-out lands on `/(auth)/login`. `page.url()` ends with `/login` (fallback anchor: the username textbox + sign-in submit `auth.login.submitButton`).
- **Traces to:** DS §3 (`signed-out → /(auth)/login`), AC1.

### FE-TC-05 — No student self-registration link on the auth chain (product override)
- **Type:** negative / product-override
- **Priority:** P1
- **Preconditions / seed:** None.
- **Steps:**
  1. Navigate to `/login`.
  2. Inspect every link/CTA on the login screen and the screen reached by the footer link.
- **Expected:** The only registration entry point is the **parent** "Create account" link (`auth.login.createAccount`), which routes to the **parent** register screen. There is NO "student sign up" / "create student account" affordance anywhere on the auth chain.
- **Traces to:** Story Notes (no student self-registration), Product decisions.

### FE-TC-06 — Parent with NO children → onboarding (add-child)
- **Type:** auth-routing / functional
- **Priority:** P0
- **Preconditions / seed:** Seeded **parent with zero linked children** (`Me.hasChildren = false`, role parent).
- **Steps:**
  1. Sign in as the no-children parent via the Login form.
  2. Let the guard resolve.
  3. Assert the **add-child / onboarding** screen is shown (onboarding wizard chrome — step label `onboarding.stepLabel`, or the add-child form heading `onboarding.addChild.*` / its first field). `page.url()` ends with `/add-child` (fallback anchor: onboarding form element).
- **Expected:** Parent with `hasChildren = false` lands on `/(onboarding)/add-child`, NOT the parent dashboard.
- **Traces to:** DS §3, AC1.

### FE-TC-07 — Child (student role) → child home, in child's language
- **Type:** auth-routing / functional
- **Priority:** P0
- **Preconditions / seed:** Seeded **child** account (role student) with a known `preferredLanguage` (e.g. `ar`) and `fullName`.
- **Steps:**
  1. Sign in with the parent-assigned child email/password.
  2. Let the guard resolve.
  3. Assert the **child home** placeholder is shown: `getByTestId('dashboard-header')` (existing) and the greeting `child.home.greeting` interpolated with the child's first name. `page.url()` resolves to the child group (fallback anchor: `dashboard-header`).
- **Expected:** Student role lands on `/(child)`, sees their own home with their name. Does NOT land on parent/onboarding surfaces.
- **Traces to:** DS §3 (`student → /(child)`), DS Screen 12, AC4.

### FE-TC-08 — Parent WITH children → parent dashboard; sign-out returns to Login
- **Type:** auth-routing / persistence (round-trip)
- **Priority:** P0
- **Preconditions / seed:** Seeded **parent with ≥1 linked child** (`Me.hasChildren = true`).
- **Steps:**
  1. Sign in as the with-children parent.
  2. Assert the **parent dashboard placeholder** is shown (heading `parent.dashboard.title`; request `testID="parent-home"`). `page.url()` resolves to the parent group.
  3. Activate sign-out (`sign-out-button` / label `auth.signOut`).
  4. Let the guard resolve.
- **Expected:** Parent with children lands on `/(parent)`. After sign-out, the guard redirects back to `/(auth)/login` (login textbox visible). Local session cleared even if the sign-out API call is slow/failing (DS Screen 11 sign-out rule).
- **Traces to:** DS §3, DS Screen 11, AC1.

---

## Group C — Locale applied from `Me.preferredLanguage` (DS §3, Screen 3 note, Screen 12)

### FE-TC-09 — Arabic child lands RTL even when login UI was English
- **Type:** RTL-i18n / state
- **Priority:** P0
- **Preconditions / seed:** Seeded child with `preferredLanguage = 'ar'`.
- **Steps:**
  1. On the Login screen, switch the language radio to **English** (`common.prefs.switchToEnglish`) so the UI is LTR before login.
  2. Sign in as the Arabic child.
  3. After landing on child home, assert `document.documentElement.dir === 'rtl'` and `lang === 'ar'`.
- **Expected:** The guard applies `Me.preferredLanguage = 'ar'` post-login → child home renders RTL/Arabic regardless of the pre-login UI locale.
- **Traces to:** `useAuthRoute` locale apply, DS Screen 3 note, DS Screen 12 Locale, AC4.

### FE-TC-10 — English child lands LTR even when login UI was Arabic (default)
- **Type:** RTL-i18n / state
- **Priority:** P1
- **Preconditions / seed:** Seeded child with `preferredLanguage = 'en'`.
- **Steps:**
  1. Boot to Login in the **default Arabic** locale (do not switch).
  2. Sign in as the English child.
  3. After landing, assert `document.documentElement.dir === 'ltr'` and `lang === 'en'`.
- **Expected:** The guard applies `Me.preferredLanguage = 'en'` → child home renders LTR/English even though the app booted Arabic.
- **Traces to:** `useAuthRoute` locale apply, AC4.

---

## Group D — Arabic-default RTL + language switch (DS §3, Screen 10)

### FE-TC-11 — Default locale is Arabic / RTL on first boot
- **Type:** RTL-i18n
- **Priority:** P0
- **Preconditions / seed:** Fresh context, no persisted locale.
- **Steps:**
  1. `page.goto('/login')`.
  2. Assert `document.documentElement.dir === 'rtl'` and `lang === 'ar'`.
  3. Assert the language radiogroup shows the Arabic radio as selected (`accessibilityState.selected` / `aria-checked` on `common.prefs.switchToArabic`).
- **Expected:** App boots Arabic-first, RTL applied on web (`applyWebDirection`), Arabic radio active.
- **Traces to:** DS §1 RTL conventions, AC5.

### FE-TC-12 — Language switch ar→en flips to LTR instantly on web
- **Type:** RTL-i18n / state
- **Priority:** P1
- **Preconditions / seed:** On Login, default Arabic.
- **Steps:**
  1. On Login, activate the English radio (`common.prefs.switchToEnglish`).
  2. Assert (no reload) `document.documentElement.dir === 'ltr'`, `lang === 'en'`, and the English radio is now selected.
  3. Assert headings/labels re-render in English copy (e.g. login title `auth.login.title`).
- **Expected:** On web the flip is **immediate** (no restart prompt) — `LocaleThemeControls` calls `setLocale` directly on web. RTL→LTR applied without reload.
- **Traces to:** DS Screen 10 (web immediate), `LocaleThemeControls.handleLocaleChange` web path, AC5.
- **Note:** This is the **web** instant-flip. The native restart-prompt path is FE-TC-22 (BLOCKED in web E2E).

### FE-TC-13 — Language switch en→ar flips back to RTL instantly on web
- **Type:** RTL-i18n / state
- **Priority:** P1
- **Preconditions / seed:** Continue from FE-TC-12 (UI now English) or switch to English first.
- **Steps:**
  1. Activate the Arabic radio (`common.prefs.switchToArabic`).
  2. Assert (no reload) `dir === 'rtl'`, `lang === 'ar'`, Arabic radio selected.
- **Expected:** Round-trips back to RTL/Arabic instantly on web. No restart prompt appears on web.
- **Traces to:** DS Screen 10, AC5.

### FE-TC-14 — Login screen renders correctly in both locales (RTL/LTR layout)
- **Type:** RTL-i18n / a11y
- **Priority:** P1
- **Preconditions / seed:** Login screen.
- **Steps:**
  1. In Arabic (default): assert the form fields, submit button (`auth.login.submitButton`), and create-account link are all visible and the language radiogroup is reachable by role.
  2. Switch to English; re-assert the same elements are visible and addressable.
- **Expected:** Both locales render the full login form without clipped/missing interactive elements; no layout breaks the form's reachability in either direction.
- **Traces to:** AC5, DS Screen 3.

---

## Group E — i18n integrity (no raw keys)

### FE-TC-15 — No raw i18n keys leak on the Arabic chain
- **Type:** RTL-i18n / regression
- **Priority:** P1
- **Preconditions / seed:** Default Arabic.
- **Steps:**
  1. Visit Login (and, where reachable without auth, the splash).
  2. Scan visible text content for raw-key patterns — no rendered string should match `^(auth|common|onboarding|parent|child)\.[a-zA-Z.]+$` (dotted namespace key) or contain `missingKey`.
- **Expected:** All copy resolves to Arabic strings; no untranslated key strings render (the failure mode when a namespace isn't registered/loaded).
- **Traces to:** DS §6 Content checklist ("All i18n keys resolved"), AC5.

### FE-TC-16 — No raw i18n keys leak on the English chain
- **Type:** RTL-i18n / regression
- **Priority:** P1
- **Preconditions / seed:** Switch to English.
- **Steps:**
  1. Switch the language radio to English on Login.
  2. Scan visible text for raw-key patterns as in FE-TC-15.
- **Expected:** All copy resolves to English strings; no raw keys, no fallback-to-key.
- **Traces to:** DS §6, AC5.

---

## Group F — Session-expired flash (DS §3 silent-refresh, Screen 1)

### FE-TC-17 — Session-expired flash shows on Login after silent-refresh failure
- **Type:** state (error) / functional
- **Priority:** P1
- **Preconditions / seed:** A signed-in session whose token can be invalidated so the api-client interceptor fires `onSignOut` (e.g. revoke/expire the refresh token, then trigger an authed request). See README §A4.
- **Steps:**
  1. Be signed in.
  2. Force a 401/refresh-failure so `onSignOut` runs → `flashMessageStore` set to `auth.sessionExpired`, `authStore.signOut()` fires.
  3. Let the guard redirect to Login.
  4. Assert the session-expired flash message (`auth.sessionExpired`) is shown on the Login screen.
- **Expected:** Login displays the session-expired flash once; user is on `/(auth)/login`.
- **Traces to:** DS §3 silent-refresh failure, DS Screen 3 flash, `_layout.tsx` onSignOut.
- **Note:** If a deterministic 401 trigger isn't available in the harness, downgrade to P2 and record the blocker.

### FE-TC-18 — Session-expired flash is one-shot (cleared after first display)
- **Type:** state / regression
- **Priority:** P2
- **Preconditions / seed:** Continue from FE-TC-17 (flash shown on Login).
- **Steps:**
  1. With the flash shown on Login, navigate away and back (or reload Login).
  2. Assert the flash is NOT shown again.
- **Expected:** Flash is consumed once (`consume()` on mount) and does not re-appear on the next visit.
- **Traces to:** DS §3 ("login reads and displays it once, then clears it").

---

## Group G — Error / loading states (DS Screens 3, 11)

### FE-TC-19 — Login error banner surfaces on invalid credentials (no field reveal)
- **Type:** validation / state (error)
- **Priority:** P1
- **Preconditions / seed:** Login screen; a known-bad credential pair.
- **Steps:**
  1. Enter a non-existent / wrong email + password and submit (`auth.login.submitButton`).
  2. Wait for the `ServerErrorBanner`.
- **Expected:** A single localized error banner appears (`auth.login.errors.invalidCredentials` or `...notFound`), NOT a raw `BaseResponse`/status code and NOT a per-field reveal of which field was wrong. The user stays on Login.
- **Traces to:** DS Screen 3 server-error mapping, AC3 (login error handling).
- **Note:** Field-level zod validation is owned by P1-11-FE — only the chain-level banner surfacing is asserted here.

### FE-TC-20 — Sign-out is resilient: returns to Login even if the API call fails
- **Type:** negative / persistence
- **Priority:** P2
- **Preconditions / seed:** Signed-in parent on the dashboard placeholder.
- **Steps:**
  1. If the harness can simulate a failing/slow `/Sign-Out` (network block on that route), do so; otherwise assert the happy round-trip.
  2. Activate sign-out (`sign-out-button` / `auth.signOut`).
- **Expected:** Local session is cleared and the guard redirects to `/(auth)/login` regardless of the sign-out API outcome (DS Screen 11: "do not block sign-out on a failed API call").
- **Traces to:** DS Screen 11 sign-out flow.

---

## Group H — Kid-UX (NFR-6)

### FE-TC-21 — Child home meets kid-UX touch-target / single-primary baseline
- **Type:** a11y / kid-UX
- **Priority:** P2
- **Preconditions / seed:** Signed in as a child, on child home.
- **Steps:**
  1. Assert the greeting (`child.home.greeting`) renders with the child's name (kid sees a warm, personal landing).
  2. Assert the sign-out control's hit box is ≥ 48×48 px (computed box) and is reachable by role/label.
  3. Assert there is no scary error/empty-state chrome on the default child landing.
- **Expected:** Child landing is warm, personal, and meets the 48px minimum touch target / one-clear-action kid-UX baseline (DS §1 kid-accessibility).
- **Traces to:** NFR-6, DS §1 kid-accessibility, DS Screen 12.

---

## Group I — Native RTL restart boundary (BLOCKED in web E2E)

### FE-TC-22 — Native LTR↔RTL switch shows restart prompt then applies RTL (BLOCKED)
- **Type:** RTL-i18n / state
- **Priority:** P1
- **Status:** **BLOCKED — not testable in web E2E.**
- **Blocker reason:** This behaviour only exists on **native** (iOS/Android): a direction-changing locale switch calls `showRestartPrompt(nextLocale)` → `applyNativeRtl()` + `react-native-restart`. On **web** the flip is instant with NO prompt (covered by FE-TC-12/13). The Playwright harness drives the **web** build only (`baseURL :8081`), so the native restart prompt and `I18nManager.forceRTL` cannot be exercised. Recorded here so the leg is not silently dropped — covered by a manual/native QA pass (P1-09-FE-5) instead.
- **Expected (for native QA reference):** Switching app language across the LTR/RTL boundary on native surfaces the restart dialog (`common.restartPrompt.*`); confirming restarts and applies `I18nManager.isRTL = true` for Arabic; "Later" defers the change.
- **Traces to:** DS Screen 10, DS §6 Native checklist, AC5 (native leg).
