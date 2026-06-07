# Frontend E2E Test Cases — P1-01-FE (Register screen, web PWA)

> Target agent: **`frontend-e2e-tester`** (Playwright, `tests/e2e/specs/P1-01-FE.spec.ts`).
> Surface: `apps/student-app/app/(auth)/register.tsx`, `app/(auth)/_components/RegisterForm.tsx`.
> Selector convention: `getByTestId` FIRST (none exist yet on this screen — see Open Questions in README + the **Selector reference** below), then `getByRole`/`getByLabel`. **Arabic is the default locale — never assert on copy strings.** Assert on structure, roles, aria-labels, URL, and field DOM type.

## Selector reference (how each element is reachable today, no testIDs)

| Element | Reachable via | Notes |
|---|---|---|
| Full name input | `getByRole('textbox')` (1st), or label-scoped | `accessibilityLabel`→`aria-label` = "Full name" i18n label (locale-dependent — prefer role + order) |
| Country picker | `getByRole('combobox')` | `Select` renders `accessibilityRole="combobox"`, inline options panel on press |
| Email input | `getByRole('textbox')` filtered, or `input[autocomplete="email"]` / `[inputmode/type=email]` | `autoComplete="email"`, `forceLtr` → value `writingDirection: ltr` |
| Password input | `input[type="password"]` | `secureTextEntry` → DOM `type=password`; mask check in FE-TC-12 |
| Terms checkbox | `getByRole('checkbox')` | `accessibilityRole="checkbox"`, `aria-checked` via `accessibilityState` |
| Submit button | `getByRole('button')` filtered, or by `aria-label` | `aria-label` = submit i18n key result; has `loading`/`disabled` |
| Server-error banner | `[aria-live="assertive"]` region, text = resolved message | `ServerErrorBanner` sets `aria-label`=message; rendered only when set |
| Inline field error | text node under the field, `aria-live="polite"` | resolved i18n text, NOT a raw `auth.register.errors.*` key |
| Heading | `getByRole('heading')` | `register.title` |

**Requested testIDs (report to `frontend` if cases are flaky):** `register-form`, `register-fullname`, `register-country`, `register-email`, `register-password`, `register-terms`, `register-submit`, `register-error`.

---

## Group A — Happy path & navigation

### FE-TC-01 — Register form accepts valid input and is submittable
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Fresh page at `/register`, default (Arabic) locale.
- **Steps:**
  1. Navigate to `/register`.
  2. Assert the heading (`getByRole('heading')`) is visible and the form mounted (`getByRole('checkbox')` + at least 2 textboxes + 1 combobox present).
  3. Fill full name with `Parent Tester`.
  4. Open the country `combobox`, select the first option.
  5. Fill email with a unique address `parent+<timestamp>@example.com`.
  6. Fill password with `Str0ng!Pass`.
  7. Check the Terms checkbox (`getByRole('checkbox')`).
- **Expected result:** No inline field errors are visible; the submit button is enabled (not `disabled`, not `aria-busy`). The checkbox reports `aria-checked="true"`.
- **Traces to:** AC-1.

### FE-TC-04 — Successful registration persists tokens and routes to onboarding `[BLOCKED — needs live backend at :5080]`
- **Type:** functional / persistence · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Backend + Postgres up at `:5080`; a guaranteed-unique email (timestamped). Lead-confirmed onboarding destination is reachable (see README Open Q3).
- **Steps:**
  1. Complete FE-TC-01 with a unique email.
  2. Press submit.
  3. Wait for navigation.
- **Expected result:** App navigates away from `/register` to the onboarding add-child route (URL contains `add-child` / onboarding segment); the onboarding step header (step 1 of 2) is visible. No server-error banner shown. (Token persistence is implied by reaching an authed onboarding route; deeper token assertions belong to the api-tester.)
- **Blocker:** Requires a running backend; harness does not auto-start it. If onboarding step-1 is a placeholder, assert URL change only.
- **Traces to:** AC-1.

### FE-TC-16 — "Sign in" link / back affordance returns to login
- **Type:** functional / state · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Navigate to `/register`.
  2. Activate the "Sign in" footer link (`getByRole('link')`) OR the back affordance in `ScreenHeader`.
- **Expected result:** App routes to `/login` (URL contains `login`); the login screen mounts a textbox.
- **Traces to:** AC-2 (login is the alternate auth route) / FE-task FE-4.

---

## Group B — Consent gate (`acceptedTerms`) — product override

### FE-TC-02 — Submitting without accepting Terms is blocked
- **Type:** validation / negative · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Fill full name, country, a valid email, and a valid password.
  2. Leave the Terms checkbox **unchecked** (`aria-checked="false"`).
  3. Press submit.
- **Expected result:** No navigation occurs (URL still `/register`); an inline error appears under the checkbox (the resolved `termsRequired` text — human-readable, NOT the key `auth.register.errors.termsRequired`); no server-error banner (no network call made). The checkbox remains unchecked.
- **Traces to:** AC-2 / consent-gate requirement.

### FE-TC-03 — Checking Terms toggles state and clears the consent error
- **Type:** functional / state · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Continue from FE-TC-02 (consent error showing) OR fresh form.
- **Steps:**
  1. With the form otherwise valid and the Terms error showing (after a blocked submit), check the Terms checkbox.
- **Expected result:** Checkbox reports `aria-checked="true"`; the consent inline error is no longer visible; the submit button is enabled. (Visual: the checkbox card gains the success tint — optional, do not assert exact color.)
- **Traces to:** AC-2 / consent-gate requirement.

### FE-TC-19 — Parent-only consent banner is present (parent-driven onboarding messaging)
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Navigate to `/register`.
  2. Locate the "Parent / Guardian only" info banner region.
- **Expected result:** The banner is rendered (a region containing the parent-only title + body text nodes). Assert structurally (its container/text exists) rather than on exact copy. This reinforces the parent-driven onboarding rule on-screen.
- **Traces to:** AC-2.

---

## Group C — Field validation (client zod, i18n surfacing)

### FE-TC-07 — Invalid email shows a localized inline error (not a raw key)
- **Type:** validation · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Fill the email field with `not-an-email`.
  2. Blur the field (mode is `onTouched`) or attempt submit.
- **Expected result:** An inline error appears under the email field; the visible text is human-readable and does **not** equal the i18n key `auth.register.errors.invalidEmail`. Submit does not navigate.
- **Traces to:** AC-4 (validation surfacing) / FE-task FE-2.

### FE-TC-08 — Password shorter than 6 chars is blocked client-side (specific message)
- **Type:** validation / boundary · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Fill password with `Ab1!` (4 chars — under the `min(6)` zod gate).
  2. Blur / attempt submit.
- **Expected result:** Inline error under the password field, resolved human text (not the key `auth.register.errors.weakPassword`); no navigation; no network call. The password strength meter / helper does not replace the error.
- **Traces to:** AC-4.

### FE-TC-09 — Country is required (empty selection blocked)
- **Type:** validation / negative · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Fill full name, valid email, valid password, check Terms — but leave country **unselected**.
  2. Press submit.
- **Expected result:** No navigation; an inline error appears under the country picker (resolved `countryRequired` text, not the key). The `combobox` still shows its placeholder, not a selected value.
- **Traces to:** AC-1 (form completeness) / FE-task FE-2.

### FE-TC-10 — Country picker opens and a selection sticks
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Activate the country `combobox` (`aria-expanded` flips to `true`).
  2. Choose the first option in the panel.
- **Expected result:** The panel closes; the combobox now displays a non-placeholder value (its text differs from the placeholder); `aria-expanded` returns to `false`. No country inline error.
- **Traces to:** AC-1 / FE-task FE-2.

### FE-TC-12 — Password input is masked
- **Type:** functional / a11y-hardening · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`.
- **Steps:**
  1. Type a password into the password field.
- **Expected result:** The password input renders as DOM `type="password"` (masked) by default. (The screen has a show/hide toggle button — optionally verify toggling reveals/`type=text` — secondary.)
- **Traces to:** AC-5 (UI side — password never shown in plain text on screen by default).

### FE-TC-11 — Submit shows a pending/loading state and prevents double-submit
- **Type:** state (loading) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Backend reachable OR a Playwright route stub that delays the register response. Prefer a **network stub/delay** so this is deterministic without a live backend.
- **Steps:**
  1. Fill a fully valid form (FE-TC-01).
  2. Stub `**/Register-Parent*` to respond after a delay (or 200 with a valid envelope).
  3. Press submit.
  4. While the request is in flight, inspect the submit button.
- **Expected result:** The submit button reports a busy/disabled state (`aria-busy`/`disabled`) and the fields are disabled (the form sets `disabled = register.isPending`). A second press does not fire a second request.
- **Traces to:** AC-1 (loading state, QC spec: loading/error states).

---

## Group D — Server-error surfacing (`BaseResponse.errors` → i18n banner)

### FE-TC-13 — Duplicate email shows the localized duplicate-email banner `[BLOCKED — needs seeded already-registered email]`
- **Type:** negative / functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Backend up at `:5080`; an email known to already exist (lead-provided, or register it once then re-submit). Alternatively, a Playwright route stub returning a 409 / `BaseResponse` with `errors: ["...email already exists..."]`.
- **Steps:**
  1. Fill a fully valid form using the already-registered email.
  2. Press submit.
- **Expected result:** No navigation; the server-error banner (`aria-live="assertive"` region) appears with the resolved **duplicate-email** message (human text, not the key `auth.register.errors.duplicateEmail`, not the generic server-error text). No duplicate account implied (no onboarding redirect).
- **Blocker:** Needs a seeded duplicate email + running backend (or a route stub). Confirm seed strategy with lead (README Open Q2).
- **Traces to:** AC-3.

### FE-TC-14 — Client-valid but server-weak password surfaces the weak-password banner `[BLOCKED — needs live backend at :5080]`
- **Type:** negative / boundary · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Backend up at `:5080` (enforces full `PASSWORD_REGEX`), OR a route stub returning 422 with a password/weak error.
- **Steps:**
  1. Fill a fully valid form with a unique email and password `abcdef` (6 chars, passes client `min(6)` but fails the backend's upper/digit/special rule).
  2. Check Terms, press submit.
- **Expected result:** No navigation; the server-error banner shows the resolved **weak-password** message (human text, not the key `auth.register.errors.weakPassword`). This proves the client/server password-policy split (README Risk note).
- **Blocker:** Needs running backend or a 422 route stub. Without it, simulate via Playwright route interception.
- **Traces to:** AC-4.

### FE-TC-15 — Network/transport failure surfaces a generic localized error
- **Type:** negative / state (error) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Page at `/register`; Playwright route stub that **aborts** the `**/Register-Parent*` request (simulated offline).
- **Steps:**
  1. Fill a fully valid form.
  2. Stub/abort the register request to force a transport failure.
  3. Press submit.
- **Expected result:** No navigation; the server-error banner shows the generic network-error message (`common.error.networkError` resolved text — human, not a raw key). The form is re-enabled (not stuck in pending).
- **Traces to:** AC-1 / QC spec error states.

---

## Group E — RTL / LTR (Arabic default vs English)

### FE-TC-05 — Arabic default renders the form RTL
- **Type:** RTL-i18n · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Default locale (Arabic). Do not switch locale.
- **Steps:**
  1. Navigate to `/register`.
  2. Inspect the heading and a text field label.
- **Expected result:** Direction-bearing nodes resolve to RTL — e.g. the heading / field labels render with `direction: rtl` / `writing-direction: rtl` (or right text-align). Assert via computed style / `dir` attribute on the relevant containers, NOT on Arabic copy.
- **Traces to:** FE-task FE-2 (RTL-aware) / QC spec RTL.

### FE-TC-06 — English locale renders the form LTR
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Switch the app UI language to English (via the locale control on login, or whatever toggle the harness uses) before/at `/register`.
- **Steps:**
  1. Set locale to English.
  2. Navigate to `/register`.
  3. Inspect the heading + a field label.
- **Expected result:** Direction-bearing nodes resolve to LTR (`direction: ltr` / left-aligned). The step eyebrow uses uppercase transform in LTR (per the screen's `textTransform`); assert structurally, not on copy.
- **Traces to:** FE-task FE-2 / QC spec RTL vs LTR.

### FE-TC-17 — Email value stays LTR inside the RTL form
- **Type:** RTL-i18n / boundary · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** Default (Arabic / RTL) locale, page at `/register`.
- **Steps:**
  1. Type a Latin email `user@example.com` into the email field.
- **Expected result:** The email input value renders left-to-right (`forceLtr` → `writingDirection/direction: ltr` and left text-align on the input) even though the surrounding form is RTL. (The label may still be RTL — only the value is forced LTR.)
- **Traces to:** SKILL.md technical-string rule / FE-task FE-2.

---

## Group F — Product overrides (no student self-register)

### FE-TC-18 — No student self-register route exists
- **Type:** negative / regression · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** App running.
- **Steps:**
  1. Attempt to navigate directly to plausible student self-register URLs (e.g. `/register-student`, `/student/register`, `/signup-student`).
- **Expected result:** None resolve to a working student self-registration screen (Expo Router yields a not-found / redirect, NOT a register form for a student). Only `/register` (parent) exists in the `(auth)` group.
- **Traces to:** AC-2 / FE-task FE-4 / product decision (parent-driven onboarding).

### FE-TC-20 — `(auth)` group exposes only login and (parent) register
- **Type:** regression · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions / seed:** App running.
- **Steps:**
  1. Navigate to `/register` (parent register) and confirm it mounts.
  2. Navigate to `/login` and confirm it mounts.
  3. Confirm there is no student-facing register affordance/link on the register screen (the only outbound link is "Sign in" → login).
- **Expected result:** Both intended routes mount; the register screen offers only the parent flow + a link to login. No student-register entry point is reachable from the register screen.
- **Traces to:** AC-2 / FE-task FE-4.

---

## Implementation notes for the tester

- Put all cases in `tests/e2e/specs/P1-01-FE.spec.ts`. Use unique timestamped emails for any case that reaches the backend to avoid cross-run duplicate collisions (except FE-TC-13, which intentionally reuses a known duplicate).
- For the 3 BLOCKED cases (FE-TC-04/13/14), prefer **Playwright route interception** (`page.route('**/Register-Parent*', ...)`) to simulate 200/409/422/abort deterministically when a live backend is not guaranteed — but if the backend is up, run the real path and note which mode was used in the execution report.
- Never assert on Arabic or English copy strings — assert on roles, aria-labels (already locale-bound), URL, DOM `type`/`dir`/`aria-checked`/`aria-busy`, and on the presence/absence of inline errors and the error banner.
- If a selector proves flaky for lack of a `testID`, record the exact needed hook in the execution report's defects section and flag to `frontend` (do not reach into brittle CSS).
