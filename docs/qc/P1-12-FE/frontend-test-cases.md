# Frontend Web-E2E Test Cases — P1-12-FE

> Implement as Playwright specs (`tests/e2e/specs/P1-12-FE.spec.ts`) for **`frontend-e2e-tester`**.
> Surface: **student-app web PWA** (`apps/student-app`), `baseURL` http://localhost:8081. Default locale **ar (RTL)**, default theme **dark**.
>
> **Selector convention (mandatory):** `getByTestId` first; if absent (it is — see README Q1), use `getByRole` + the i18n-resolved `accessibilityLabel`/`aria-label` (RN Web maps `accessibilityLabel`→`aria-label`, `accessibilityRole`→`role`). **Never select by visible copy** — Arabic is the default locale. Resolve expected labels from `packages/shared/src/i18n/resources.ts` (`auth.*`, `parent.settings.profile.*`, `parent.myChildren.*`, `onboarding.*`). When a control has no stable hook, **report the needed `testID` back to `frontend`** — do not reach into CSS.
>
> **i18n assertion rule:** every visible string must resolve to a localized value, **never a raw key** (e.g. fail if the DOM shows `auth.forgotPassword.title`). Assert against the resolved en/ar string from the resources file for the active locale.
>
> **Auth/seed:** parent-surface cases (Profile/Avatar/Edit-child) require a signed-in parent and (for edit-child) ≥1 linked child, seeded via the API before the test (see README Q4). Auth screens (Login/Register/Forgot/Reset) are anonymous.
>
> **Status legend:** `Live` = runnable now · `BLOCKED(env)` = needs `EXPO_PUBLIC_GOOGLE_CLIENT_ID` or live backend · `BLOCKED(token)` = needs a real reset token. Blocked cases stay in the suite; record as **Blocked** (not Fail) and run every reachable leg.

---

## Surface 1 — Profile save (`SettingsWeb.tsx` → `ProfilePanel`)

Route: navigate to the parent Settings → Profile tab (default active tab). Hooks: `useMyProfile` (load), `useUpdateProfile` (save).

### FE-TC-01 — Profile form populates from enriched `/Me`
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester · **Status:** Live (needs backend + seeded parent)
- **Preconditions:** signed-in parent whose profile has known `fullName`, `phone`, `country` (seed via register/profile API).
- **Steps:**
  1. Sign in as the seeded parent; open Settings; ensure the Profile tab is active.
  2. Wait for the loading state (`parent.settings.profile.loading`) to clear.
- **Expected:** Full-name field = seeded `fullName`; phone field = seeded `phone` (rendered LTR); country Select shows the seeded country's localized label; email field shows the account email and is **disabled** (display-only).
- **Traces to:** P1-12a enriched `/Me`; FE-1.

### FE-TC-02 — Save persists fullName/phone/country + success state
- **Type:** functional / persistence · **Priority:** P0 · **Status:** Live success-leg BLOCKED(env) if backend down
- **Preconditions:** signed-in parent (FE-TC-01 state).
- **Steps:**
  1. Edit full name, phone, and select a different country.
  2. Press the **Save** button (`aria-label` = `parent.settings.profile.save`).
  3. Await the mutation.
- **Expected:** Save button shows loading then settles; a `$successSoft` panel with `parent.settings.profile.saveSuccess` appears (`aria-live=polite`). Re-loading the panel shows the new values persisted.
- **Traces to:** P1-12a update; FE-1.

### FE-TC-03 — Save server error (400/422) surfaces localized banner
- **Type:** negative / validation · **Priority:** P1 · **Status:** Live (needs a 400/422 from backend or injected)
- **Steps:**
  1. Trigger a profile-update failure (e.g. invalid value the server rejects, or route-mock a 422).
  2. Press Save.
- **Expected:** `ServerErrorBanner` shows the localized `parent.settings.profile.saveError` text (not a raw key, not the raw backend message). No success panel.
- **Traces to:** FE-1 error surfacing.

### FE-TC-04 — Empty full name save behaviour
- **Type:** boundary · **Priority:** P2 · **Status:** Live
- **Steps:** Clear the full-name field; press Save.
- **Expected:** The form submits `fullName: ''` trimmed; assert the observable result (server validation banner if the BE rejects empty, else the saved empty value). Document which the backend does — no client-side required-name guard exists on this panel, so this case proves the server is the gate.
- **Traces to:** FE-1 validation edge.

### FE-TC-05 — Cancel resets to loaded values; email immutable
- **Type:** functional · **Priority:** P1 · **Status:** Live
- **Steps:**
  1. Edit full name + phone (do not save).
  2. Press **Cancel** (`aria-label` = `parent.settings.profile.cancel`).
- **Expected:** Fields revert to the originally loaded values; any prior success/error feedback clears (`updateProfile.reset()`); the email field remains disabled and unchanged throughout.
- **Traces to:** FE-1; email display-only.

### FE-TC-06 — Profile loading state
- **Type:** state (loading) · **Priority:** P2 · **Status:** Live
- **Steps:** Open Settings → Profile and observe before `/Me` resolves (throttle network if needed).
- **Expected:** The panel shows `parent.settings.profile.loading` text; form fields are not yet rendered. After resolve, the form appears.
- **Traces to:** FE-1 state.

---

## Surface 2 — Avatar upload / remove (`SettingsWeb.tsx` `ProfilePanel`, web `<input type=file>`)

Web-only (`Platform.OS === 'web'`). Hidden `<input type=file accept="image/png,image/jpeg">`; `AVATAR_MAX_BYTES` = 5 MB; client allowlist = `image/png,image/jpeg`. Hooks: `useUploadAvatar`, `useRemoveAvatar`.

### FE-TC-07 — Upload valid PNG/JPG happy path
- **Type:** functional · **Priority:** P0 · **Status:** success-leg BLOCKED(env) if avatar backend/auth absent; pick+validation leg Live
- **Preconditions:** signed-in parent; a valid <5 MB PNG fixture.
- **Steps:**
  1. Open Settings → Profile.
  2. Activate **Upload photo** (`aria-label` = `parent.settings.profile.uploadPhoto`); set the hidden file input to the PNG fixture (`setInputFiles` on the `input[type=file]`).
  3. Await the upload mutation.
- **Expected:** During flight: pending overlay (`⏳`, `aria-busy`) over the avatar, both buttons disabled, helper → `parent.settings.profile.avatar.uploading`. On success: inline `$success` text `parent.settings.profile.avatar.uploadSuccess` (`aria-live=polite`); the avatar re-renders from the new `avatarUrl`.
- **Traces to:** P1-12b; FE-2.

### FE-TC-08 — Reject disallowed file type (no network call)
- **Type:** validation / negative · **Priority:** P0 · **Status:** Live (client-only)
- **Preconditions:** a disallowed fixture (e.g. `.gif`, `.svg`, or `.pdf`).
- **Steps:** Set the file input to the disallowed file.
- **Expected:** Inline `$danger` text `parent.settings.profile.avatar.wrongType` (`aria-live=assertive`) appears immediately; **no upload network request fires** (assert via no `/Avatar` POST). The avatar is unchanged.
- **Traces to:** P1-12b type validation; FE-2.

### FE-TC-09 — Reject oversized file (>5 MB)
- **Type:** boundary / negative · **Priority:** P0 · **Status:** Live (client-only)
- **Preconditions:** a valid-type PNG/JPG **> 5 MB**.
- **Steps:** Set the file input to the oversized image.
- **Expected:** Inline `$danger` text `parent.settings.profile.avatar.tooLarge`; **no upload request fires**; avatar unchanged.
- **Boundary note:** also exercise a file at ~5 MB (≤ cap) → passes client guard and proceeds to upload (success leg per env).
- **Traces to:** P1-12b size validation; FE-2.

### FE-TC-10 — Upload pending overlay + buttons disabled
- **Type:** state (loading) · **Priority:** P1 · **Status:** Live (mock pending) / backend
- **Steps:** Begin a valid upload with the network throttled or the upload route held pending.
- **Expected:** Avatar overlay visible with `aria-busy`; Upload button shows `loading`; both Upload and Remove (if present) are disabled (`accessibilityState.disabled`); helper text shows `…avatar.uploading`.
- **Traces to:** FE-2 pending state.

### FE-TC-11 — Upload server error surfaces inline
- **Type:** negative · **Priority:** P1 · **Status:** Live (inject 4xx/5xx) / backend
- **Steps:** Begin a valid upload that the server rejects (route-mock a 500).
- **Expected:** Inline `$danger` `parent.settings.profile.avatar.uploadError` (`aria-live=assertive`); avatar unchanged; buttons re-enabled.
- **Traces to:** FE-2 upload error.

### FE-TC-12 — Remove hidden when avatar is initials-only
- **Type:** state (empty) · **Priority:** P1 · **Status:** Live
- **Preconditions:** a parent with **no** `avatarUrl` (initials avatar).
- **Steps:** Open Settings → Profile.
- **Expected:** Only the **Upload photo** button is present; the **Remove** button (`aria-label` = `parent.settings.profile.removePhoto`) is **not** rendered.
- **Traces to:** FE-2 (Remove only when photo set).

### FE-TC-13 — Remove photo happy path → falls back to initials
- **Type:** functional · **Priority:** P1 · **Status:** success-leg BLOCKED(env) if backend/auth absent
- **Preconditions:** a parent **with** an `avatarUrl`.
- **Steps:** Press **Remove**; await the mutation. (No confirm dialog — direct remove by design.)
- **Expected:** Pending overlay during flight; on success the avatar falls back to initials, the Remove button disappears, and inline `$success` `parent.settings.profile.avatar.removeSuccess` shows. On error: `parent.settings.profile.avatar.removeError`.
- **Traces to:** FE-2 remove.

---

## Surface 3 — Google sign-in button states (`LoginForm.tsx` + `loginParts.tsx`)

Route: `/login`. Decision: **Google only** functional; Apple/Microsoft dimmed placeholders. Env: `EXPO_PUBLIC_GOOGLE_CLIENT_ID`.

### FE-TC-14 — Google button disabled / graceful-degrades when env unset
- **Type:** state / negative (graceful degradation) · **Priority:** P0 · **Status:** Live (env assumed unset)
- **Preconditions:** `EXPO_PUBLIC_GOOGLE_CLIENT_ID` **unset** in the test env (README Q2).
- **Steps:**
  1. Open `/login`.
  2. Locate the Google `SocialButton` (`role=button`, `aria-label` = `auth.login.socialGoogle`).
  3. Attempt to press it.
- **Expected:** The Google button renders but is **disabled** (`aria-disabled=true`, opacity 0.5, no press feedback); pressing it does nothing (no OAuth prompt, no navigation, no error banner). The app does **not** crash. (Dev console may warn that the client ID is unset — not asserted in prod build.)
- **Traces to:** P1-12c graceful-degrade; HANDOFF Batch-3 note.

### FE-TC-15 — Google live OAuth happy path → tokens + route to `/`
- **Type:** functional (auth) · **Priority:** P1 · **Status:** **BLOCKED(env)** — needs provisioned client ID + Google dialog (unautomatable headlessly)
- **Preconditions:** `EXPO_PUBLIC_GOOGLE_CLIENT_ID` set; backend `GoogleAuth__ClientId` matching; a Google test account.
- **Steps (when unblocked):** Press Google → complete Google's dialog → return with `id_token`.
- **Expected:** `useGoogleSignIn` persists tokens via `authStore.setTokens` (same path as email sign-in) and `router.replace('/')`.
- **Traces to:** P1-12c. **Record Blocked with reason (unset env / unautomatable dialog).**

### FE-TC-16 — Google in-flight locks email submit + Apple + Microsoft
- **Type:** state · **Priority:** P1 · **Status:** Live only if env set (else button disabled); otherwise verify via mocked in-flight or mark BLOCKED(env)
- **Preconditions:** Google configured (env set) so the button is enabled; hold the OAuth prompt/mutation pending.
- **Steps:** Trigger Google sign-in and observe while in flight.
- **Expected:** Google button shows a spinner (icon swapped, `aria-busy`, label dims to `$fg3`); the email **Sign in** submit, the username/password fields, the persona toggle, the remember-me checkbox, and the Apple + Microsoft buttons are all disabled (one-action-at-a-time). No layout shift (spinner occupies the icon slot).
- **Traces to:** P1-12c in-flight lock.

### FE-TC-17 — Google error → shared banner (no enumeration); user-cancel silent
- **Type:** negative / auth · **Priority:** P1 · **Status:** Live only if env set, else BLOCKED(env)
- **Steps:**
  1. Cancel the Google dialog (`response.type` = `cancel`/`dismiss`).
  2. Separately, force an OAuth/network error (`response.type` = `error`, or no `id_token`).
- **Expected:** On **cancel/dismiss**: no error banner, no navigation (silent). On **error**: the shared `ServerErrorBanner` shows the localized `auth.login.errors.socialFailed` — a **generic** message with no "account not found"/enumeration reveal; the Google button re-enables.
- **Traces to:** P1-12c no-enumeration.

### FE-TC-18 — Apple / Microsoft dimmed disabled placeholders (no-op)
- **Type:** state · **Priority:** P2 · **Status:** Live
- **Steps:** On `/login`, locate the Apple (`aria-label` = `auth.login.socialApple`) and Microsoft (`auth.login.socialMicrosoft`, tablet+ viewport) buttons; attempt to press.
- **Expected:** Both render at opacity 0.5, `aria-disabled=true`, `accessibilityState.disabled`; pressing does nothing (no navigation, no error). Microsoft is only present at tablet+ width (assert hidden on phone viewport).
- **Traces to:** P1-12c placeholders; product (Google-only).

---

## Surface 4 — Forgot-password (`(auth)/forgot-password.tsx`)

Anonymous. Hook: `useForgotPassword`. Schema: email required + format. Anti-enumeration: generic success on any 2xx.

### FE-TC-19 — "Forgot password?" link on Login routes to forgot-password
- **Type:** functional / routing · **Priority:** P1 · **Status:** Live
- **Steps:** On `/login`, press the forgot-password link (`role=link`, `aria-label` = `auth.login.forgotPassword`).
- **Expected:** Route changes to `/(auth)/forgot-password`; the screen header `auth.forgotPassword.title` (localized) and a single email field render.
- **Traces to:** P1-12d link wiring; FE-4.

### FE-TC-20 — Email validation (required + format)
- **Type:** validation · **Priority:** P1 · **Status:** Live
- **Steps:**
  1. On forgot-password, press Submit with the email empty → blur to trigger `onTouched`.
  2. Enter `not-an-email`; submit.
- **Expected:** Field shows the localized `auth.forgotPassword.errors.invalidEmail` (not a raw key); no network request fires for the invalid email.
- **Traces to:** FE-4 validation.

### FE-TC-21 — Anti-enumeration generic success on any 2xx (identical copy)
- **Type:** functional / security · **Priority:** P0 · **Status:** 2xx-leg BLOCKED(env) if backend down; structure Live
- **Steps:**
  1. Submit a **known** account email; await 2xx.
  2. In a fresh load, submit an **unknown** email; await 2xx.
- **Expected:** **Both** replace the form with the same `$successSoft` confirmation panel (`auth.forgotPassword.successTitle` + `auth.forgotPassword.successBody`, `aria-live=polite`); the submit field/button are gone; only "Back to Sign in" remains. The copy is **byte-identical** for known vs unknown — assert no branch reveals account existence.
- **Traces to:** P1-12d anti-enumeration; FE-4. **Highest-value privacy invariant.**

### FE-TC-22 — Server / network error (generic) keeps the field for retry
- **Type:** negative · **Priority:** P1 · **Status:** Live (inject 500 / offline)
- **Steps:** Force a 500 or offline; submit a valid email.
- **Expected:** `ServerErrorBanner` shows the localized `auth.forgotPassword.errors.generic`; the email field + submit remain (no success panel); a retry is possible.
- **Traces to:** FE-4 error.

### FE-TC-23 — "Back to Sign in" returns to Login (both views)
- **Type:** routing · **Priority:** P2 · **Status:** Live
- **Steps:** From the idle form, and again from the success panel, press "Back to Sign in" (`role=link`, `aria-label` = `auth.forgotPassword.backToSignIn`).
- **Expected:** Routes to `/(auth)/login` in both cases.
- **Traces to:** FE-4.

---

## Surface 5 — Reset-password (`(auth)/reset-password.tsx`)

Anonymous, reached via `?email=&token=`. Hook: `useResetPassword`. Token classification by **HTTP status only** (400/410/422 → token-invalid). Token never echoed.

### FE-TC-24 — Missing / empty token param → token-invalid block, no form
- **Type:** negative / boundary · **Priority:** P0 · **Status:** Live (craft URL)
- **Steps:** Navigate to `/(auth)/reset-password?email=test@x.com` (no `token`), and separately with `&token=` empty.
- **Expected:** The password form is **not** rendered; the `$warningSoft` token-error block shows (`⚠️`, localized `auth.resetPassword.errors.tokenInvalid`, `aria-live=assertive`) plus a "Request a new link" link (`aria-label` = `auth.resetPassword.requestNewLink`) that routes to `/(auth)/forgot-password`.
- **Traces to:** FE-4 absent-token handling.

### FE-TC-25 — Email param prefilled, read-only; subtitle shows it LTR
- **Type:** functional · **Priority:** P1 · **Status:** Live (craft URL with garbage token)
- **Steps:** Navigate to `/(auth)/reset-password?email=test@x.com&token=GARBAGE`.
- **Expected:** The email field shows `test@x.com`, is **disabled** and `forceLtr`; the subtitle (`auth.resetPassword.subtitle`) renders the email inline LTR. Both password fields + the strength meter helper are present.
- **Traces to:** FE-4.

### FE-TC-26 — Password policy + confirm-match client validation
- **Type:** validation · **Priority:** P0 · **Status:** Live (craft URL)
- **Preconditions:** reset URL with a non-empty `token` (garbage OK — validation is client-side, no submit needed).
- **Steps:**
  1. Type a weak password (e.g. `abc`) → blur.
  2. Type a policy-passing password; type a non-matching confirm → blur/submit.
  3. Type matching confirm.
- **Expected:** Weak → field error `auth.resetPassword.errors.weakPassword` (localized) + low strength meter; mismatch → confirm field error `auth.resetPassword.errors.mismatch`; matching + policy-passing clears errors. The strength meter appears once a value is typed; the `auth.resetPassword.passwordHelper` shows before typing.
- **Traces to:** FE-4 policy + match.

### FE-TC-27 — Server token-invalid/expired (400/410/422) → dedicated block
- **Type:** negative / auth · **Priority:** P0 · **Status:** **BLOCKED(token)** — partially runnable with a garbage token if backend is up
- **Steps:** With a non-empty but invalid `token` and valid passwords, submit; backend returns 400/410/422.
- **Expected:** The form is replaced by the token-error block (`auth.resetPassword.errors.tokenInvalid`, localized) + "Request a new link" → forgot-password. The **raw backend message is never echoed** (status-only classification). A generic 500 instead shows the generic banner, not this block (covered by FE-TC-30).
- **Traces to:** FE-4 token rejection. **If backend is up, run with a garbage token; else Blocked.**

### FE-TC-28 — Reset happy path → success panel → route to login
- **Type:** functional · **Priority:** P1 · **Status:** **BLOCKED(token)** — needs a valid token from the reset email
- **Steps (when unblocked):** Open the reset link with a valid `email`+`token`; set a policy-passing matching password; submit.
- **Expected:** `$successSoft` panel `auth.resetPassword.successBody` (`aria-live=polite`); after ~1800 ms `router.replace('/(auth)/login')`. Assert the success panel first, then the login route (do not race the redirect — README Q5).
- **Traces to:** FE-4 happy path. **Record Blocked (token).**

### FE-TC-29 — Token never echoed in DOM / aria / visible string
- **Type:** security / a11y · **Priority:** P0 · **Status:** Live (craft URL with a known sentinel token)
- **Steps:** Navigate to `/(auth)/reset-password?email=test@x.com&token=SENTINEL_TOKEN_123`; inspect the rendered DOM.
- **Expected:** The string `SENTINEL_TOKEN_123` appears **nowhere** in visible text, any `aria-label`, any element `key`/attribute, or an error message — only in the (later) mutation request body. (The URL bar itself carries it by deep-link design; assert it is not reflected into the page content.)
- **Traces to:** FE-4 token-secrecy invariant. **High-value leak guard.**

### FE-TC-30 — Other server error (non-token, e.g. 500) → generic banner
- **Type:** negative · **Priority:** P2 · **Status:** **BLOCKED(token)** — needs a valid form submit + injected non-4xx error
- **Steps (when unblocked):** Submit a valid reset that the server fails with 500 / offline.
- **Expected:** `ServerErrorBanner` shows `auth.resetPassword.errors.generic` (not the token-error block); the form stays for retry.
- **Traces to:** FE-4 generic error.

---

## Surface 6 — Register consent + country (`RegisterForm.tsx`)

Route: `/register`. Schema: `registerParentSchema`. `acceptedTerms` default **false**; `country` required. Hook: `useRegisterParent`.

### FE-TC-31 — Consent is NOT pre-checked
- **Type:** functional / security · **Priority:** P0 · **Status:** Live
- **Steps:** Open `/register`; locate the consent checkbox (`role=checkbox`, `aria-label` = `auth.register.termsA11y`).
- **Expected:** `accessibilityState.checked` is **false** on first render; the consent card uses the resting `$card`/`$border` (not the green-tint accepted state).
- **Traces to:** P1-12f consent never auto-set; legal/COPPA invariant.

### FE-TC-32 — Submit with consent unchecked is blocked
- **Type:** validation / negative · **Priority:** P0 · **Status:** Live
- **Steps:** Fill full name, country, valid email, policy-passing password; leave consent unchecked; press **Submit** (`aria-label` = `auth.register.submitButton`).
- **Expected:** A field error under the consent card shows the localized `auth.register.errors.termsRequired`; **no register network request fires**; no navigation.
- **Traces to:** P1-12f gate; FE-7.

### FE-TC-33 — Submit with country empty is blocked
- **Type:** validation / negative · **Priority:** P1 · **Status:** Live
- **Steps:** Fill the other fields and check consent; leave the country Select empty; submit.
- **Expected:** The country Select shows the localized `auth.register.errors.countryRequired`; no register request fires.
- **Traces to:** P1-12f country required; FE-7.

### FE-TC-34 — Valid register posts country + acceptedTerms → onboarding
- **Type:** functional / persistence · **Priority:** P0 · **Status:** success-leg BLOCKED(env) if backend down
- **Steps:** Fill all fields with a fresh email, select a country, check consent, submit; capture the outgoing request.
- **Expected:** The register request body includes `country` (selected code) and `acceptedTerms: true` (and `fullName`, `email`, `password`); **no `captchaToken`** (out of scope). On success, tokens persist and the app routes to `/(onboarding)/add-child`. (For a duplicate email, the banner shows `auth.register.errors.duplicateEmail`.)
- **Traces to:** P1-12f persisted country + consent; FE-7.

---

## Surface 7 — Edit child (`EditChildSheet.tsx`, `ChildDashboardCard.tsx`, `MyChildrenWeb.tsx`)

Parent My-Children web. Hook: `useUpdateChild`. Slim field set: `fullName / grade / language / country` only (email read-only display).

### FE-TC-35 — Edit affordance present on each child card
- **Type:** functional · **Priority:** P1 · **Status:** Live (needs ≥1 linked child)
- **Preconditions:** signed-in parent with ≥1 linked child.
- **Steps:** Open the My-Children web surface; locate a child card's Edit button (`role=button`, `aria-label` = `parent.myChildren.editChild` resolved with the child name).
- **Expected:** A pencil (`✎`) edit button renders in the card header inline-end; it is keyboard-focusable and ≥44×44.
- **Traces to:** P1-12e affordance; FE-5.

### FE-TC-36 — Edit opens sheet pre-filled from the child's real values
- **Type:** functional · **Priority:** P0 · **Status:** Live
- **Steps:** Press the Edit affordance on a child whose grade/language/country are known.
- **Expected:** The bottom sheet opens (title `onboarding.editChild`, localized); full-name = child name, grade picker = child's real grade, language select = child's real language, country select = child's real country (pre-filled from `LinkedChildResponse`, not placeholders).
- **Traces to:** P1-12e pre-fill (no data-loss); FE-5.

### FE-TC-37 — Slim field set only — no password / learningLanguage; email read-only
- **Type:** functional / security · **Priority:** P0 · **Status:** Live
- **Steps:** Inspect the open edit sheet's fields.
- **Expected:** Exactly four editable fields (full name, grade, language, country); **no password field**, **no learningLanguage field**. If an email is present it renders **disabled** + `forceLtr` (display-only). Submit button label = `onboarding.saveChanges`.
- **Traces to:** P1-12e four-field contract; security smell avoidance; FE-5.

### FE-TC-38 — Edit validation (name required, grade 1..6)
- **Type:** validation / boundary · **Priority:** P1 · **Status:** Live
- **Steps:** Clear the full name and submit; separately set/keep an out-of-range grade if the picker allows.
- **Expected:** Name empty → `onboarding.addChild.errors.nameRequired`; grade outside 1..6 → `onboarding.child.errors.invalidGrade` (localized). No update request fires while invalid.
- **Traces to:** FE-5 validation.

### FE-TC-39 — Save success closes sheet + refetches list
- **Type:** functional / persistence · **Priority:** P0 · **Status:** success-leg BLOCKED(env) if backend down
- **Steps:** Change the full name (and/or grade/language/country) to valid values; press Save; await the mutation.
- **Expected:** Save button shows loading; on success the sheet closes and the My-Children list refetches (`queryKeys.family.myChildren()` invalidated) — the card reflects the updated name.
- **Traces to:** P1-12e returns updated child; FE-5.

### FE-TC-40 — Save error → banner inside the sheet
- **Type:** negative · **Priority:** P1 · **Status:** Live (inject 400/422) / backend
- **Steps:** Submit an edit the server rejects (route-mock a 422, or a duplicate-name conflict).
- **Expected:** `ServerErrorBanner` appears **inside the sheet** (above the Save button) with the localized `parent.myChildren.editError`; the sheet stays open; no list refetch.
- **Traces to:** FE-5 error handling.

---

## Cross-cutting — RTL / i18n / a11y (applies across all surfaces)

### FE-TC-41 — Arabic-default RTL vs English LTR + no raw i18n keys
- **Type:** RTL-i18n / a11y · **Priority:** P0 · **Status:** Live
- **Steps:** For each surface above, run once with the app in **ar (default, RTL)** and once switched to **en (LTR)** via the LocaleThemeControls / Language panel.
- **Expected:**
  1. In `ar`, the document direction is RTL; headers, helper text, and row clusters reverse; **no English copy leaks** into ar and **no raw i18n keys** (e.g. `auth.forgotPassword.title`) appear in any locale.
  2. **Latin-technical fields stay LTR** in `ar`: email (login/register/forgot/reset/profile/edit-child), phone (profile), and the reset email subtitle are `forceLtr` — the digits/letters do not visually reorder.
  3. The **Google brand label stays Latin LTR** even in `ar` (`labelLtr`); the social row reverses but "Google" is not flipped.
  4. Interactive controls expose `role` + a resolved `aria-label`; the avatar pending overlay sets `aria-busy`; success panels are `aria-live=polite`, error/token blocks `aria-live=assertive`; headers carry `role=heading`.
- **Traces to:** FE-6; design-spec RTL/a11y clauses across all surfaces.

---

## Implementation notes for the tester
- Put all cases in `tests/e2e/specs/P1-12-FE.spec.ts`; group by surface with `test.describe`.
- Resolve every expected label from `packages/shared/src/i18n/resources.ts` for the active locale; do not hardcode English.
- For client-only validation cases (FE-TC-08/09/20/26/31/32/33/38), assert **no** network request fires using Playwright route interception / request listeners.
- For BLOCKED cases, still implement the reachable legs and `test.skip`/annotate the blocked leg with the README §5 reason; record **Blocked** in `execution-report.md`.
- Where a control lacks a stable hook, file the needed `testID` back to `frontend` (README Q1) — note it in the execution report's defects section.
