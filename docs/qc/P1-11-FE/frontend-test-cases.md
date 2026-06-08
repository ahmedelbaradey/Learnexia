# P1-11-FE — Frontend E2E Test Cases (`frontend-e2e-tester`)

> **Target agent:** `frontend-e2e-tester` · **Harness:** `tests/e2e/` (Playwright) · **Spec file to create:** `tests/e2e/specs/P1-11-FE.spec.ts`
> **Projects:** `chromium` (desktop 1024+) and `mobile` (Pixel 7 ≈ 390w). Use viewport overrides for the 768 tablet checks.
> **Selector convention:** `getByTestId` first; where no testID exists, `getByRole(...)` + `getByLabel(...)` using the resolved i18n `accessibilityLabel`. **Never** select by visible Arabic copy (Arabic is the default locale).
> **Locale control:** the app boots Arabic-default RTL. Switch to EN via the Login/Settings language switch (`accessibilityRole="radio"`, label `common.prefs.switchToEnglish`) or by seeding the locale store; document the mechanism in the spec.
> **BLOCKED cases:** scaffold as `test.skip(...)` with the blocker reason in the title — do not assert against placeholders.

---

## A. Splash & routing guard (`app/index.tsx`)

### FE-TC-01 — Signed-out boot routes to Login
- **Type:** functional / auth-authz · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** no auth tokens in storage (fresh context).
- **Steps:**
  1. Navigate to `/`.
  2. Wait for the routing guard (`useAuthRoute`) to resolve.
- **Expected:** the splash shows briefly (brand wordmark + DotPulse), then the app `router.replace`s to the Login screen (`/(auth)/login`) — URL ends at the login route; the email field (role=textbox, label `auth.login.labelUsername`) is visible.
- **Traces to:** cross-cutting auth routing (P1-11a/c).

### FE-TC-02 — Splash renders brand chrome (LTR wordmark)
- **Type:** functional / state (loading) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** none; intercept/slow the auth-resolve so the splash is observable, or assert on first paint.
- **Steps:** load `/`; before redirect, inspect the splash.
- **Expected:** "Learnexia" wordmark renders; the DotPulse loader and the "Loading…" label (key `common.splash.loading`) are present. The wordmark `writingDirection` is `ltr` even when the app locale is Arabic.
- **Traces to:** Splash design spec (Screen 1).

### FE-TC-18 — Brand wordmark & technical fields stay LTR in Arabic
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** app in Arabic (default).
- **Steps:**
  1. Load `/` (Arabic) — inspect splash wordmark.
  2. Go to Login; inspect the email field.
- **Expected:** the "Learnexia" wordmark and the email input render LTR (`dir="ltr"` / `writingDirection=ltr`) while the surrounding page is RTL.
- **Traces to:** RTL forced-LTR rule (SKILL).

### FE-TC-20 — Session-expired flash surfaces on Splash/Login
- **Type:** state (error) · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** trigger a flash message (e.g. expired-session redirect) if reachable; otherwise **BLOCKED (no deterministic trigger from UI)**.
- **Steps:** drive the app into the session-expired path; observe the flash card.
- **Expected:** a soft card with the localized flash message (key resolved, not raw key) appears on Splash, then is consumed once on Login mount (not shown twice).
- **Traces to:** Splash flash handling.

---

## B. Login (`app/(auth)/login.tsx` + `LoginForm`)

### FE-TC-03 — Login renders all built affordances
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** at `/(auth)/login`, EN locale (switch via the language radio).
- **Steps:** render the login screen at 1024 width.
- **Expected:** present — eyebrow (`auth.login.eyebrow`), heading (role=header, `auth.login.title`), persona toggle (Parent/Student), email + password fields, Remember-me checkbox, "Forgot password?" link (role=link), submit button (label `auth.login.submitButton`), OR divider, Google/Apple/Microsoft social buttons, and the "Create parent account" footer link. At ≥768 the left brand panel (`LoginBrandPanel`) is visible; the phone-only logo mark is hidden.
- **Traces to:** P1-11c login criteria.

### FE-TC-08 — Persona toggle switches selection
- **Type:** functional / state · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen.
- **Steps:** tap the "Student" segment of the persona toggle, then "Parent".
- **Expected:** the toggle reflects the selected persona (`accessibilityState.selected`/aria-checked moves). The form fields stay the same shared form (persona is login-persona only — no separate route is shown; no self-register affordance appears).
- **Traces to:** P1-11c persona toggle; product override (login persona only).

### FE-TC-04 — Invalid credentials shows a generic localized banner
- **Type:** negative / validation · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** backend at `:5080` reachable; use a non-existent / wrong-password account.
- **Steps:**
  1. Enter a syntactically valid email + a wrong password.
  2. Submit.
- **Expected:** a single `ServerErrorBanner` shows a **generic** invalid-credentials message (key `auth.login.errors.invalidCredentials`, resolved to localized text — NOT a raw i18n key, NOT a per-field reveal of which part was wrong). HTTP 400/401 both map to the same generic copy (anti-enumeration).
- **Traces to:** P1-11c "inline errors for invalid credentials".

### FE-TC-10 — Empty-field client validation (zod) blocks submit
- **Type:** validation · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen; no backend call expected.
- **Steps:** focus then blur email + password leaving them empty; attempt submit.
- **Expected:** inline field errors render (resolved i18n text from `signInSchema` messages); no network request is fired. Banner is absent until a real server error.
- **Traces to:** form/zod surfacing.

### FE-TC-05 — Language switch on Login flips direction + fonts
- **Type:** RTL-i18n · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen, Arabic default.
- **Steps:** tap the EN radio in `LocaleThemeControls`, then the AR radio.
- **Expected:** on EN the document direction is LTR and the split panel sits brand-left/form-right; on AR it flips to RTL (brand-right/form-left) and the Arabic font stack applies. The active radio reflects `accessibilityState.selected`.
- **Traces to:** P1-11c language switch.

### FE-TC-06 — Theme toggle flips dark↔light on Login
- **Type:** functional · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen; dark is default.
- **Steps:** tap the theme button (label `common.prefs.switchToLight`), then again.
- **Expected:** the theme button label/icon toggles (☀️↔🌙) and the rendered background/token colors change between dark and light.
- **Traces to:** P1-11c dark-mode switch (default dark).

### FE-TC-07 — Theme choice persists across reload
- **Type:** persistence · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen, dark default; confirm persistence is to web storage (see open question #6).
- **Steps:** switch to light; `page.reload()`.
- **Expected:** after reload the app is still in light theme (choice persisted, not reset to dark).
- **Traces to:** P1-11a "toggle persists across reloads".

### FE-TC-09 — Login a11y roles/labels present
- **Type:** a11y · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen.
- **Steps:** query roles.
- **Expected:** heading has role=header; "Forgot password?" and "Create parent account" have role=link with aria-labels; submit has an accessible name; language switch is a radiogroup with two radios; theme toggle is a labelled button.
- **Traces to:** a11y cross-cutting.

### FE-TC-19 — Google button disables gracefully when client ID unset
- **Type:** boundary / negative · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** `EXPO_PUBLIC_GOOGLE_CLIENT_ID` **unset** in the test env (recommended).
- **Steps:** render login; inspect the Google social button.
- **Expected:** the Google button is disabled (dimmed) and pressing it is a no-op; the page does **not** crash and no uncaught error is thrown. Apple + Microsoft remain dimmed placeholders.
- **Traces to:** HANDOFF P1-12 Batch 3 (graceful degrade on unset env).

### FE-TC-21 — "Create parent account" routes to Register
- **Type:** functional / routing · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login screen.
- **Steps:** tap the footer "Create parent account" link.
- **Expected:** navigates to `/(auth)/register`; the register heading (role=header, `auth.register.title`) is visible.
- **Traces to:** P1-11c footer link.

---

## C. Register (`app/(auth)/register.tsx` + `RegisterForm`)

### FE-TC-11 — Register renders form + feature panel + step indicator
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** at `/(auth)/register`, 1024 width.
- **Steps:** render.
- **Expected:** present — "STEP 1 OF 2" eyebrow + a progressbar (role=progressbar, min1/max2/now1), heading (role=header), Parent/Guardian-only info banner, fields: Full name, Country (select), Email, Password (with strength meter / helper), Terms consent checkbox, submit button. At ≥768 the `RegisterFeaturePanel` shows on the right (brandSide="end").
- **Traces to:** P1-11d two-column form + benefits panel + consent + strength meter.

### FE-TC-12 — Password strength meter reacts to input
- **Type:** functional / boundary · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** register screen.
- **Steps:** type into password: "a" (weak), "abcdef" (≥6), "Abc123" (mixed), "Abc123!" (special).
- **Expected:** the strength meter score increases across the four states (weak→fair→good→strong); the helper "At least 6 characters" shows only when empty + no error.
- **Traces to:** P1-11d password-strength meter.

### FE-TC-13 — Terms consent gates submit; defaults unchecked
- **Type:** validation / negative · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** register screen.
- **Steps:**
  1. Fill name/country/email/valid password, leave Terms **unchecked**, submit.
  2. Then check Terms and re-submit.
- **Expected:** step 1 — submit is blocked, a consent error renders (resolved i18n), no register request fires; `acceptedTerms` is false by default (never auto-set). Step 2 — with Terms checked, the request fires.
- **Traces to:** P1-11d consent; security (never auto-set consent).

### FE-TC-14 — Successful register routes to add-child onboarding
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** backend `:5080`; a fresh unique email.
- **Steps:** fill all fields with a unique email + strong password, accept Terms, submit.
- **Expected:** on success the app persists tokens and `router.replace`s to `/(onboarding)/add-child` (the add-child form is shown). Parent is now authenticated.
- **Traces to:** P1-11d "on success routes to add-child onboarding".

### FE-TC-15 — Duplicate-email & weak-password map to localized inline copy
- **Type:** negative / validation · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** backend `:5080`; an already-registered email for duplicate; a too-short password for weak.
- **Steps:**
  1. Register with an existing email → observe.
  2. Register with a fresh email but a 3-char password → observe.
- **Expected:** duplicate → banner `auth.register.errors.duplicateEmail` (resolved); weak → `auth.register.errors.weakPassword` (resolved). Both are localized text, not raw keys; 409→duplicate, 422→weak mapping holds.
- **Traces to:** P1-11d "handles duplicate-email / weak-password inline".

### FE-TC-16 — No student self-register path exists
- **Type:** negative / auth-authz · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** register screen.
- **Steps:** inspect the register screen + the persona toggle on login.
- **Expected:** the only registration surface is the **parent** form (info banner reinforces parent/guardian-only). No "register as student" control or route is present anywhere; the login Student persona does NOT expose a self-register affordance.
- **Traces to:** product override — no student self-register.

### FE-TC-22 — Register "Sign in" link returns to Login
- **Type:** functional / routing · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** register screen.
- **Steps:** tap the "Already have an account? Sign in" link (and the ScreenHeader back affordance).
- **Expected:** both route back to `/(auth)/login`.
- **Traces to:** P1-11d navigation.

---

## D. My Children + add/edit (`children.tsx`, `MyChildrenWeb`, `MyChildren`, `EditChildSheet`)

### FE-TC-23 — My Children loading skeletons then content
- **Type:** state (loading) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; `:5080`; ≥768 width.
- **Steps:** navigate to `/(parent)/children`; observe while `useMyChildren` resolves.
- **Expected:** three card skeletons render while loading, then resolve to child cards + the trailing dashed Add card. No raw error.
- **Traces to:** loading state.

### FE-TC-24 — My Children load-error state with retry
- **Type:** state (error) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; force the children request to fail (route intercept → 500).
- **Steps:** navigate to `/(parent)/children`.
- **Expected:** an error message (`parent.myChildren.loadError`, resolved) + a Retry ghost button (label `common.retry`). Pressing Retry refetches.
- **Traces to:** error state.

### FE-TC-27 — My Children renders family hero + cards + Add CTA (≥768)
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with ≥1 child; ≥768 width.
- **Steps:** open `/(parent)/children`.
- **Expected:** sidebar (left in EN), page header (title `parent.myChildren.title` role=header + subtitle with count), `FamilySummaryStrip` hero, a "pick a child" row with the "Add Child" primary button, a 3-col grid of `ChildDashboardCard`s, and the dashed `AddChildCard`. Do **not** assert specific XP/mastery numbers (Phase-5 stubs).
- **Traces to:** P1-11e family hero + child cards + add CTA.

### FE-TC-28 — Subtitle child count matches rendered cards
- **Type:** functional · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with a known number N of children.
- **Steps:** open My Children.
- **Expected:** the subtitle ("N children linked", `parent.myChildren.subtitle`) count equals the number of `ChildDashboardCard`s (excluding the Add card).
- **Traces to:** P1-11e.

### FE-TC-30 — Add Child CTA routes to add-child form
- **Type:** functional / routing · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent.
- **Steps:** press the "Add Child" button (and separately the dashed Add card).
- **Expected:** both navigate to `/(onboarding)/add-child`; the add-child form renders (name / email / grade / language / country fields per P1-04/P8-01).
- **Traces to:** P1-11e Add child.

### FE-TC-31 — Edit Child opens the sheet pre-filled and saves
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with ≥1 child; `:5080`; ≥768.
- **Steps:**
  1. On a child card, press the Edit pencil (label `parent.myChildren.editChild` with the child name).
  2. The `EditChildSheet` opens pre-filled with the child's real grade/language/country.
  3. Change the full name, save.
- **Expected:** the sheet opens with real values (not placeholders); saving calls `useUpdateChild`, the sheet closes, and the My-Children list refreshes (invalidated query) reflecting the change.
- **Traces to:** P1-11e Edit child.

### FE-TC-29 — My Children RTL layout (Arabic)
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; Arabic locale; ≥768.
- **Steps:** open My Children in Arabic.
- **Expected:** document is RTL — the sidebar sits on the right (border on its left), the 3-col grid reverses column order (add card lands visually left), headings/labels render RTL. The mastery bar fill still grows from the visual left (forced LTR per SKILL rule 6).
- **Traces to:** P1-11e en/ar rendering.

---

## E. Dashboard / Overview (`overview.tsx`, `OverviewWeb`)

### FE-TC-33 — Overview renders header + 4 KPI cards + focus areas
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with ≥1 child; ≥768.
- **Steps:** navigate to `/(parent)/overview`.
- **Expected:** header ("<Child>'s progress" role=header + date range + period select + Send Report), four KPI tiles (Time learning / XP earned / Lessons done / Day streak — each with a green delta + an accessible composed label), the Daily-activity card, the Subject-mastery card, and the Focus-areas list. Values are Phase-5 stubs — assert presence/labels, not specific numbers.
- **Traces to:** P1-11f header + KPI cards + mastery + focus areas.

### FE-TC-34 — Subject mastery shows the 4 product subjects, no mock
- **Type:** functional / negative · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** Overview rendered with a child.
- **Steps:** inspect the Subject-mastery card labels (EN locale for stable assertion).
- **Expected:** mastery bars are keyed to Math / Science / Arabic / English. **No** "Reading", "Art", or "Social Studies" bars (mock-data subjects from the capture must NOT appear).
- **Traces to:** product override — 4 subjects; "no mock data" directive.

### FE-TC-35 — Daily-activity chart is a placeholder (BLOCKED)
- **Type:** state (placeholder) · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (placeholder → P5-05-FE).** Scaffold as `test.skip`.
- **Preconditions:** Overview rendered.
- **Steps:** inspect the Daily-activity card body.
- **Expected (documented, not asserted as a feature):** the card header + "Export CSV" render but the bar chart itself is a deferred placeholder — **do not assert a functional chart**. Optionally assert the placeholder presence only.
- **Traces to:** P1-11f chart (deferred to P5-05-FE).

### FE-TC-36 — Child selector limitation on Overview (documented)
- **Type:** functional (known limitation) · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with ≥2 children; ≥768. See open question #5.
- **Steps:** from Overview, use the sidebar child selector.
- **Expected:** the sidebar child selector currently routes to My Children rather than re-scoping the Overview to a different child (Overview reflects `children[0]`). Assert the **current** behavior; flag as a limitation only if the lead confirms it is unintended.
- **Traces to:** P1-11f "child selector switches which child".

### FE-TC-49 — Overview empty-state when no children
- **Type:** state (empty) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with **zero** children.
- **Steps:** navigate to `/(parent)/overview`.
- **Expected:** an empty-state message (`parent.overview.empty`, resolved) + a primary "Add child" button that routes to `/(onboarding)/add-child`. No KPI cards rendered.
- **Traces to:** empty state.

---

## F. Settings (`settings.tsx`, `SettingsWeb`, panels)

### FE-TC-37 — Language tab switches app language app-wide + persists
- **Type:** RTL-i18n / persistence · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; `:5080` (for `useUpdateUserLanguage`); ≥768.
- **Steps:**
  1. Open `/(parent)/settings`, select the Language & region tab.
  2. Change the language select from AR→EN.
- **Expected:** the app flips to EN/LTR immediately (web); a success confirmation (`parent.settings.language.saveSuccess`, resolved) shows; the choice is persisted (server `User.PreferredLanguage` via the mutation). Region select is UI-only (no persistence assertion).
- **Traces to:** P1-11h Language tab.

### FE-TC-38 — Profile tab loads, edits, and saves
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; `:5080` with `useMyProfile`/`useUpdateProfile`; ≥768.
- **Steps:**
  1. Open Settings → Profile (default tab).
  2. Wait for the profile to load (loading text → fields populated). Email field is display-only/disabled.
  3. Change Full name + Phone + Country, press Save changes.
  4. Press Cancel on a subsequent edit.
- **Expected:** fields seed from `useMyProfile`; Save persists via `useUpdateProfile` and shows a success card (`parent.settings.profile.saveSuccess`); a failed save shows the `ServerErrorBanner` (`parent.settings.profile.saveError`). Cancel resets fields to the loaded values. Email input is disabled.
- **Traces to:** P1-11h Profile form.

### FE-TC-50 — Avatar upload client-side guards (type/size)
- **Type:** validation / boundary · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** Settings → Profile, web (`<input type=file>` path).
- **Steps:**
  1. Press "Upload photo"; choose a non-PNG/JPG file (e.g. `.gif`).
  2. Then choose a >5 MB PNG.
- **Expected:** wrong type → inline `parent.settings.profile.avatar.wrongType` (assertive live region); oversize → `...avatar.tooLarge`. Neither fires the upload mutation. A valid small PNG/JPG triggers upload and a success message. (The avatar-upload backend may be a stub — if upload 4xx/500s, assert the error path renders rather than a crash.)
- **Traces to:** P1-12 Batch 1 avatar (web-only); robustness.

### FE-TC-39 — Secondary tabs show "coming soon", not broken
- **Type:** functional / state · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** Settings open; ≥768.
- **Steps:** select each of Notifications, Linked children, Security, Plan & billing.
- **Expected:** each renders a panel (the four are P2-12 placeholders — Notifications/Security/Billing show a "coming soon" / stub panel; Linked children shows the P8-04 learning-language panel). None throws or renders a blank/broken view; the six-tab rail stays intact and the active tab reflects selection.
- **Traces to:** P1-11h "other four tabs → coming soon, not broken".

### FE-TC-51 — Settings six-tab bar renders all tabs
- **Type:** functional · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** Settings open.
- **Steps:** inspect the tab rail.
- **Expected:** exactly six tabs render in order: Profile, Notifications, Linked children, Security, Plan & billing, Language & region (role=tab / Tabs primitive). Profile is active by default.
- **Traces to:** P1-11h six-tab bar pixel-perfect.

### FE-TC-52 — Settings RTL (Arabic) layout
- **Type:** RTL-i18n · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; Arabic; ≥768.
- **Steps:** open Settings in Arabic.
- **Expected:** document RTL — the tab rail sits on the right, panels render RTL, headings/labels are Arabic-direction. The email field stays LTR.
- **Traces to:** P1-11h en/ar rendering.

### FE-TC-53 — Settings narrow layout (<768) stacks without sidebar
- **Type:** responsive · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; Pixel-7 / 390 width.
- **Steps:** open Settings on mobile project.
- **Expected:** no `Sidebar`; a mobile `ScreenHeader` + the `SettingsWeb` content stacked; tabs still reachable.
- **Traces to:** responsive collapse.

---

## G. Landing (Next.js `apps/marketing-site`)

> **All Landing cases are BLOCKED (harness gap)** unless the lead wires the Next.js site into the Playwright config (open question #1). Scaffold as `test.skip` with the reason in the title until resolved. If a marketing webServer/baseURL is provided, un-skip.

### FE-TC-40 — Landing hero renders (BLOCKED-conditional)
- **Type:** functional · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (harness gap)** until marketing server reachable.
- **Preconditions:** marketing site served + reachable by Playwright.
- **Steps:** navigate to the landing root.
- **Expected:** top nav (logo + How it works / Subjects / For schools / Pricing + Log in + Start free), hero (pill badge, headline with the accented "adventure game" span, paragraph, two CTAs, trust row), phone mockup, and the below-the-fold sections (features / subjects / CTA banner / footer).
- **Traces to:** P1-11b matches capture.

### FE-TC-41 — Landing features + sections render
- **Type:** functional · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (harness gap).**
- **Steps:** scroll the landing.
- **Expected:** FeaturesSection, SubjectsBand, CTABanner, SiteFooter all render.
- **Traces to:** P1-11b.

### FE-TC-42 — Landing CTAs route to Register / Login (env URLs)
- **Type:** functional / routing · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (harness gap).**
- **Preconditions:** `NEXT_PUBLIC_APP_URL` set to the app base.
- **Steps:** inspect the "Start free" / hero primary CTA href and the "Log in" href.
- **Expected:** primary CTAs `href` = `${APP_URL}/register`; "Log in" = `${APP_URL}/login`. (Assert the href targets; following the link crosses into the Expo app.)
- **Traces to:** P1-11b primary CTA → Register, secondary → Login.

### FE-TC-43 — Landing subjects band = 4 product subjects only
- **Type:** negative · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (harness gap).**
- **Steps:** inspect the SubjectsBand tiles.
- **Expected:** exactly four subject tiles (Math / Science / Arabic / English). **No** "Social Studies" tile.
- **Traces to:** product override — 4 subjects.

### FE-TC-44 — Landing is English LTR only (no RTL)
- **Type:** RTL-i18n (scoped-out assertion) · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (harness gap).**
- **Steps:** load the landing.
- **Expected:** renders English / LTR; there is no language switch on the marketing site (RTL scoped out of the marketing phase). No Arabic-RTL case is authored for Landing.
- **Traces to:** P1-11b en-only note.

---

## H. Cross-cutting routing / responsive / a11y

### FE-TC-17 — Authenticated parent reaches a parent home (not a child surface)
- **Type:** auth-authz / routing · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated **parent** session (tokens seeded / via register).
- **Steps:** load `/`; let the guard resolve.
- **Expected:** the parent lands on a parent surface (onboarding add-child if no children, else a `(parent)` route) — never a `(child)` home. Role routing is correct.
- **Traces to:** auth/role routing.

### FE-TC-25 — Sidebar nav active-state per page
- **Type:** functional · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; ≥768.
- **Steps:** visit My Children, Overview, Reports, Settings; on each inspect the sidebar.
- **Expected:** the matching nav item carries the active pill (`$primarySoft` bg) and `accessibilityState.selected=true`; others are inactive. My Children/Overview/Reports/Settings route to their own screens; Activity/Subjects fall back to children (documented).
- **Traces to:** P1-11a sidebar active state.

### FE-TC-26 — Sidebar child-selector + nav a11y roles
- **Type:** a11y · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent with ≥1 child; ≥768.
- **Steps:** inspect the sidebar.
- **Expected:** the nav has role=menu with role=menuitem children; the child-selector card is a labelled button (`parent.childSelector.label`) and shows "Grade N · Level L" (stub) meta; the brand wordmark is LTR.
- **Traces to:** P1-11a sidebar composition + a11y.

### FE-TC-45 — App-wide RTL/LTR flip via Settings language
- **Type:** RTL-i18n · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; ≥768.
- **Steps:** in Settings Language tab switch AR↔EN, then navigate to My Children and Overview.
- **Expected:** the chosen direction applies app-wide across subsequently-visited parent screens (sidebar side, grid order, text direction all consistent with the selected locale).
- **Traces to:** P1-11a language switch app-wide.

### FE-TC-46 — Sidebar collapses at ≤768 (responsive)
- **Type:** responsive · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent on a parent screen.
- **Steps:** render at 1024 (sidebar present), then at 760 and at 390.
- **Expected:** at ≥768 the `Sidebar` + web main content render; at <768 the sidebar is gone and the mobile `ScreenHeader` + stacked content render. No horizontal overflow at 390.
- **Traces to:** P1-11a "collapses at ≤768"; responsive 390/768/1024.

### FE-TC-47 — Auth split-panel collapses on mobile
- **Type:** responsive · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** login + register screens.
- **Steps:** render each at 1024 (split panel) and at 390 (Pixel 7).
- **Expected:** at ≥768 the brand/feature panel shows beside the form; at 390 it collapses to a single-column form (the phone-only logo mark appears on Login). No overflow.
- **Traces to:** responsive; P1-11c/d split layout.

### FE-TC-32 — Reports renders the built empty-state (not broken)
- **Type:** state (empty) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** authenticated parent; ≥768 and <768.
- **Steps:** navigate to `/(parent)/reports`.
- **Expected:** the built surface renders — Sidebar (Reports active) + a clean body: title (role=header, `parent.reports.title`) + a muted "coming soon" line (`parent.reports.comingSoon`). It is **not** broken/blank. Do **not** assert KPIs/charts here (see FE-TC-48).
- **Traces to:** built Reports placeholder; "not a broken view".

### FE-TC-48 — Full Reports page (KPIs / 20-day / time-of-day) — BLOCKED
- **Type:** functional · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED (placeholder).** Scaffold as `test.skip` — the full Reports build is deferred (P1-11-FE-9 / P5-05-FE). Do not assert KPIs, the 20-day chart, subject mastery, or the time-of-day breakdown against the current empty-state.
- **Traces to:** P1-11g (deferred).
