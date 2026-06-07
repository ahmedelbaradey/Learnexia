# P1-03-FE — Frontend (web E2E) test cases

> Target agent: **`frontend-e2e-tester`** (Playwright over Expo web at `http://localhost:8081`; backend prerequisite at `:5080`).
> Story: `user-stories/Phase-1-Foundation/P1-03-complete-onboarding.md` · Task: `tasks/Frontend/student-app/Phase-1-Foundation/P1-03-FE.md`
> Scope: **student-app web only.** Surfaces: `app/(onboarding)/add-child.tsx` + `AddChildForm` + `ChildCard` list + `EditChildSheet` (in-memory mode) + `app/(onboarding)/complete.tsx` + `app/(onboarding)/_layout.tsx`; and the parent "My Children" surfaces `MyChildren` / `MyChildrenWeb` where the added child must appear.
> **Selector convention:** `getByTestId` first, then `getByRole` / `getByLabel` (RN Web maps `accessibilityLabel`→`aria-label`, `accessibilityRole`→`role`). **None of the onboarding screens carry `testID`s today** — every case below relies on `aria-label` / `role` (i18n-keyed). Arabic is the default locale, so avoid copy-as-selector except where the test deliberately asserts i18n text content. Missing `testID`s are catalogued in `README.md` Open Questions; until they land, use the documented `aria-label` hooks.

## Auth precondition note
The Add-Child screen lives in the `(onboarding)` route group, reachable only when signed in as a **parent with `hasChildren = false`** (`useAuthRoute` → `/(onboarding)/add-child`). Seed/login a fresh parent (no children) via the API before each onboarding-flow case. The post-add flow routes to `/(onboarding)/complete` → `/(parent)`; the added child then appears in `MyChildrenWeb` / `MyChildren` (re-driven by `useMyChildren`, which refetches on mount).

---

## Group A — Add-child happy path (end-to-end)

### FE-TC-01 — Parent adds one child → child appears in My Children
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Logged-in parent with **no** children; landed on `/(onboarding)/add-child` (step 1 of 2). Use a unique `loginEmail` per run (e.g. `child+<ts>@test.io`).
- **Steps:**
  1. Fill `onboarding.addChild.labelName` (name), `labelEmail` (assigned login email), `labelPassword` (a valid password).
  2. Pick Grade via the `labelGrade` picker (e.g. Grade 3).
  3. In the **Languages** group, pick **Learning language** (`aria-label` = `onboarding.addChild.labelLearningLanguage`) = English; observe **App language** auto-fills to English (see FE-TC-09).
  4. Fill `labelCountry`.
  5. Press **Add Child to List** (`aria-label` = `onboarding.addChild.addToListButton`).
  6. Assert the child now appears as a `ChildCard` in the "Children to add" list (`aria-label` = the child's full name), form is reset (fields cleared).
  7. Press **Add N Child(ren) and Continue** (`aria-label` = `onboarding.addChild.submitButton`).
  8. Assert the submit button shows its loading state, then routing lands on `/(onboarding)/complete` ("You're all set!" header, `onboarding.complete.title`).
  9. Press **Go to Dashboard** (`onboarding.complete.cta`).
  10. Assert the parent dashboard "My Children" surface lists the new child (card `aria-label` = child full name; email visible as meta).
- **Expected result:** Child is provisioned (single POST `addChild`), card status badge turns success/green before routing, complete screen shown, and the child is present in `useMyChildren` data on the dashboard.
- **Traces to:** AC "when I add a child, then I enter the child's details and set grade/language/country"; AC "Adding a child provisions a child account with a login email I assign"; FE-2, FE-5.

### FE-TC-02 — Add multiple children in one onboarding pass
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Same as FE-TC-01; two unique emails.
- **Steps:**
  1. Add child #1 (valid) → press **Add Child to List**.
  2. Without leaving the screen, fill the form again for child #2 (valid, different email) → **Add Child to List**.
  3. Assert the list now shows **two** `ChildCard`s and the list label count reads 2 (`onboarding.addChild.listLabel`).
  4. Assert the submit button label reflects count = 2 (`onboarding.addChild.submitButton` with `count: 2`).
  5. Press submit → assert both succeed and routing lands on `/(onboarding)/complete`.
  6. Go to dashboard → assert **both** children appear in My Children.
- **Expected result:** Two separate POSTs (sequential loop), both succeed, both children listed.
- **Traces to:** AC "I can add more than one child in the same onboarding flow; each child gets a separate profile and account"; FE-3.

### FE-TC-03 — Remove a draft child before submit
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Two valid drafts added to the list (as FE-TC-02 through step 3).
- **Steps:**
  1. On draft #1's `ChildCard`, press its remove affordance (editable-variant card; remove control).
  2. Assert the list now shows one card and the list count reads 1.
  3. Submit → assert only **one** child is created (one POST) and only that child appears in My Children.
- **Expected result:** Removed draft is never submitted; no orphan account created.
- **Traces to:** FE-3 (manage list: edit/remove). **Open question:** the remove control has no `testID`/`aria-label` exposed by `ChildCard` for the editable variant — see README OQ-2.

### FE-TC-04 — Edit a draft child before submit (in-memory EditChildSheet)
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** One valid draft added (idle status).
- **Steps:**
  1. Press the draft card's edit affordance → assert the `EditChildSheet` modal opens with the full `AddChildForm` pre-filled with the draft's values (name, email, grade, languages, country populated).
  2. Change the grade to a different value and press **Save changes** (`onboarding.saveChanges`).
  3. Assert the sheet closes and the card's meta reflects the new grade (meta = `Grade X · <language>`).
  4. Submit → assert the child is created with the edited grade.
- **Expected result:** Draft edited in place; submit uses the edited values.
- **Traces to:** FE-3 (edit). **Open question:** edit/close controls — close uses `aria-label` `onboarding.close`; edit trigger on the card lacks a stable hook (OQ-2).

---

## Group B — Validation & error surfacing

### FE-TC-05 — Required-field validation blocks "Add Child to List"
- **Type:** validation · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** On `/(onboarding)/add-child`, form empty.
- **Steps:**
  1. Without filling anything, press **Add Child to List**.
  2. Assert inline field errors render as **i18n text** (not raw keys) below each field:
     - name → `onboarding.addChild.errors.nameRequired`
     - learning language → `onboarding.addChild.errors.learningLanguageRequired` ("Please choose a learning language." / "يرجى اختيار لغة الدراسة.")
     - country → `onboarding.addChild.errors.countryRequired`
     - plus email/password field errors from `emailField`/`passwordField`.
  3. Assert **no** `ChildCard` was added to the list (validation fires on submit, `mode: 'onSubmit'`).
- **Expected result:** Form not accepted; localized error strings shown; list unchanged.
- **Traces to:** AC "that child entry is rejected with a specific message"; FE-4 (zod validation).

### FE-TC-06 — Learning-language required even when app-language has a default
- **Type:** validation/boundary · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** On `/(onboarding)/add-child`.
- **Steps:**
  1. Fill name, email, password, grade, country — but **do not** touch the Learning-language select (it has no default; App language defaults to `ar`).
  2. Press **Add Child to List**.
  3. Assert the learning-language field shows `learningLanguageRequired` error and the child is NOT added.
- **Expected result:** Required `learningLanguage` enforced client-side despite app-language having a value (this is the real-contract requirement — backend rejects missing `LearningLanguage`).
- **Traces to:** P8-01 learning-language requirement; backend-QC contract note.

### FE-TC-07 — Duplicate login email surfaced as specific i18n message (no account created)
- **Type:** negative · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** A child already provisioned with email `taken+<ts>@test.io` (seed via API, or add it earlier in the same flow). Logged-in parent on add-child.
- **Steps:**
  1. Add a draft using the **already-in-use** email; other fields valid → **Add Child to List**.
  2. Press submit.
  3. Assert the draft card flips to **error** status and shows `onboarding.child.errors.duplicateEmail` ("This email is already in use.") — mapped from the `BaseResponse` error by `perChildErrorKey` (matches "exists"/"duplicate" or status 409).
  4. Assert the partial-failure banner appears (`onboarding.addChild.partialFailureBanner`) and routing does **not** advance to `/complete`.
  5. Assert the failed card remains editable for retry; no second account created.
- **Expected result:** Specific localized duplicate message; flow halts on the failed card.
- **Traces to:** AC "an email already in use → that child entry is rejected with a specific message and no account is created"; FE-5.

### FE-TC-08 — Generic BaseResponse error fallback surfaces as i18n text (not raw key/JSON)
- **Type:** negative/error-state · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Force a server error on `addChild` (e.g. seed a condition that yields a 500/unmapped 422, or — if not feasible — see README OQ-4). Parent on add-child with one valid draft.
- **Steps:**
  1. Submit a draft that triggers a non-duplicate/non-grade server error.
  2. Assert the card error reads `onboarding.child.errors.generic` ("Could not add this child. Please try again.") — never a raw i18n key, never the raw `BaseResponse.Message` JSON, never a stack trace.
  3. Assert the partial-failure banner shows and flow does not advance.
- **Expected result:** Unmapped server errors degrade to the localized generic message.
- **Traces to:** error-surfacing requirement (BaseResponse → i18n).

---

## Group C — Learning language vs App language (axis A/B)

### FE-TC-09 — Selecting learning language auto-fills app language (while untouched)
- **Type:** functional/state · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** On add-child, fresh form (App language shows its `ar` default).
- **Steps:**
  1. Without touching App language, pick **Learning language** = English.
  2. Assert the **App language** select now reads English (auto-filled via the untouched-guard).
  3. Pick Learning language = Arabic.
  4. Assert App language follows to Arabic.
- **Expected result:** App language mirrors learning language while `appLanguageTouched` is false.
- **Traces to:** P8-01 auto-fill behaviour; "the child's UI language matches learning language (editable)".

### FE-TC-10 — Manually editing app language stops the auto-fill
- **Type:** functional/state · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Fresh add-child form.
- **Steps:**
  1. Pick **App language** = English directly (manual touch).
  2. Then pick **Learning language** = Arabic.
  3. Assert App language **stays** English (auto-fill no longer overwrites — `appLanguageTouched` is now true).
  4. Add to list with Learning=ar, App=en, submit → assert the POST body carries `learningLanguage: 'ar'` and `language: 'en'` independently (verify via the created child's reflected `language` in My Children, and/or network capture).
- **Expected result:** The two axes are independently editable; manual app-language edit is sticky for the rest of that draft.
- **Traces to:** P8-01 independent-editability requirement.

### FE-TC-11 — Two language fields are visibly fenced and labelled distinctly
- **Type:** functional/a11y · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Add-child form rendered.
- **Steps:**
  1. Assert the **Languages** group label is present (`onboarding.addChild.languageGroupLabel`) inside a bordered container.
  2. Assert two distinct selects exist with `aria-label`s `labelLearningLanguage` and `labelAppLanguage`, each with its helper text (`learningLanguageHelper`, `appLanguageHelper`) rendered as localized strings.
- **Expected result:** The parent can tell the two language fields apart; helpers are localized text.
- **Traces to:** P8-01 disambiguation; error-surfacing (no raw keys).

---

## Group D — RTL (Arabic default) vs LTR (English)

### FE-TC-12 — Arabic default: add-child screen renders RTL with Arabic copy
- **Type:** RTL-i18n · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Default locale (Arabic). Parent on add-child.
- **Steps:**
  1. Assert the document/root direction is RTL (`dir="rtl"` on the relevant container; `writingDirection` applied).
  2. Assert the screen title reads the Arabic `onboarding.addChild.title` and field labels/helpers render Arabic strings (e.g. learning-language helper = "اللغة التي سيدرس بها طفلك الرياضيات والعلوم.").
  3. Assert the onboarding header step label + back affordance mirror correctly (back chevron flips; step dots do not).
- **Expected result:** Full RTL layout + Arabic localized copy by default.
- **Traces to:** AC "RTL for Arabic"; project Arabic-default rule.

### FE-TC-13 — English locale: LTR layout + English copy
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Switch locale to English (via the available locale control — login carries `LocaleThemeControls`; see README OQ-3 for switching mid-onboarding).
- **Steps:**
  1. With English active, open add-child.
  2. Assert direction is LTR and labels/helpers render English (`labelLearningLanguage` = "Learning language", helper = "The language your child will study Math & Science in.").
- **Expected result:** LTR layout, English copy.
- **Traces to:** AC "language sets locale (LTR for English)".

### FE-TC-14 — Locale switch reflects on the form without losing entered draft list
- **Type:** RTL-i18n/state · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** One draft already added to the list.
- **Steps:**
  1. Switch locale (ar↔en) using the available control.
  2. Assert labels/direction flip and the **existing draft `ChildCard` is still present** (list state preserved across the locale change).
- **Expected result:** Locale flip is non-destructive to in-memory drafts.
- **Traces to:** locale-switch-mid-flow edge case. **Open question:** no locale control on the onboarding screen itself (OQ-3) — may be BLOCKED if switching mid-onboarding isn't reachable.

---

## Group E — States (loading / empty / error) on My Children

### FE-TC-15 — My Children empty state for a parent before any child exists
- **Type:** state (empty) · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Parent with zero children. Note the routing guard sends a childless parent to onboarding — to assert the empty state on the parent surface directly, see README OQ-1 (whether `(parent)` is reachable for a 0-child parent).
- **Steps:**
  1. Reach the `MyChildren` panel (mobile/native list) with zero children.
  2. Assert the empty copy `parent.myChildren.empty` ("No children linked yet.") + mascot + "Link existing child" CTA render.
- **Expected result:** Localized empty state, not a blank/spinner-forever.
- **Traces to:** empty-state coverage. **May be BLOCKED** — see OQ-1.

### FE-TC-16 — My Children loading skeletons then loaded list
- **Type:** state (loading) · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Parent with ≥1 child; throttle the `My-Children` response (Playwright route delay) to observe loading.
- **Steps:**
  1. Navigate to the dashboard "My Children" (`MyChildrenWeb`) with a slowed response.
  2. Assert skeleton cards render while `isLoading` (3 `CardSkeleton`s, `aria-label` `common.loading` on the native `MyChildren`).
  3. Assert skeletons are replaced by real child cards once loaded.
- **Expected result:** Loading → loaded transition; no error flash.
- **Traces to:** loading-state coverage.

### FE-TC-17 — My Children error state + retry
- **Type:** state (error) · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Force `My-Children` GET to fail (Playwright route → 500) for a logged-in parent.
- **Steps:**
  1. Open the dashboard My Children surface.
  2. Assert the error copy `parent.myChildren.loadError` ("Could not load your children. Tap to retry.") + a **Retry** control (`aria-label` `common.retry`).
  3. Remove the route override; press Retry.
  4. Assert the list reloads and children render.
- **Expected result:** Localized error + working retry.
- **Traces to:** error-state coverage.

### FE-TC-18 — Newly added child appears in My Children after completing onboarding
- **Type:** persistence/regression · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Continuation of FE-TC-01 (one child just added, parent routed to `/(parent)`).
- **Steps:**
  1. After "Go to Dashboard", assert the dashboard "My Children" data (`useMyChildren`) contains the just-added child (name + the assigned email).
  2. Reload the page → assert the child persists (server-backed, not just in-memory draft).
- **Expected result:** Persisted child visible across reload — confirms provisioning, not just optimistic UI. **Note:** `useAddChild` does not itself invalidate `myChildren`; the dashboard relies on `useMyChildren` refetching on fresh mount — see README OQ-5 (stale-cache risk if the parent navigates to My Children without a remount).
- **Traces to:** AC "each child gets a separate profile and account"; persistence.

---

## Group F — Product overrides (negative assertions)

### FE-TC-19 — No student self-registration / self-onboarding route
- **Type:** auth-authz/negative · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Signed out.
- **Steps:**
  1. On the registration screen (`/(auth)/register`), assert there is **no** "student"/"child" self-register option — registration is parent-only (persona is parent; no child persona that creates an account).
  2. Attempt to navigate directly to `/(onboarding)/add-child` while signed out → assert `useAuthRoute` redirects to `/(auth)/login`.
  3. Sign in as a **child/student** account and assert routing lands on `/(child)` and the onboarding/add-child group is **not** reachable (guard sends students to `(child)`).
- **Expected result:** Onboarding + child provisioning is a parent-only action; students cannot self-onboard.
- **Traces to:** AC "Onboarding completion is a parent action — a child cannot self-register or self-onboard"; product override "no student self-register".

### FE-TC-20 — Grade selector is bounded to 1–6 only
- **Type:** boundary/validation · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Add-child form, grade picker open.
- **Steps:**
  1. Open the `labelGrade` picker and assert exactly **six** options: Grade 1 … Grade 6 (`onboarding.grade.1..6`); no Grade 0, no Grade 7+, no "KG".
  2. Assert no free-text grade entry is possible.
- **Expected result:** Only grades 1–6 selectable; zod `min(1)/max(6)` is unreachable from the UI but defends against tampering (`onboarding.child.errors.invalidGrade` if a bad value is forced).
- **Traces to:** AC "set grade (1–6)" + "invalid grade (outside 1–6) → rejected"; FE-4.

### FE-TC-21 — Only the 4 supported languages/subjects context; no teacher role anywhere
- **Type:** regression/negative · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Add-child form + onboarding chrome.
- **Steps:**
  1. Assert the language selects offer exactly **Arabic** and **English** (`onboarding.language.ar/en`) — the two supported locales; no third option.
  2. Assert no "teacher"/"instructor" role, label, or persona appears anywhere in the onboarding or My-Children surfaces (no teacher in the product).
- **Expected result:** Locale options limited to ar/en; zero teacher-role surface.
- **Traces to:** product overrides "no teacher role"; "4 subjects" (subject choice is downstream, but locale set here feeds it).

---

## Coverage note
P0 cases: FE-TC-01, 02, 05, 06, 07, 09, 12, 18, 19. Every acceptance criterion is covered by at least one P0/P1 case — see `README.md` coverage matrix. Cases dependent on unreachable surfaces (OQ-1/OQ-3) are flagged inline and in the README; the tester should mark them **BLOCKED** with the stated reason rather than forcing a brittle path.
