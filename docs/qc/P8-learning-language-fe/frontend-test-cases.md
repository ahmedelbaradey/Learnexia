# Frontend (Web E2E) Test Cases — P8 Learning-Language FE

> **Surface owner:** `frontend-e2e-tester` (Playwright, student-app web PWA).
> **Scope:** three FE surfaces of Phase-8 localization — (1) add-child learning-language
> selection [P8-01-FE], (2) parent change-learning-language flow [P8-04-FE], (3) app-shell
> UI-language switch / RTL foundation [P8-SHELL / P8-99-FE]. Plus the **independence** of the two
> language axes (UI language vs learning language).
> **Design only** — implementer turns each case into one test 1:1.

---

## 0. Grounding facts the implementer MUST rely on (verified on disk)

These are load-bearing; they change how cases are written and asserted. Do not re-discover them.

| Fact | Evidence | Implication for tests |
|---|---|---|
| **Two distinct add-child surfaces exist** | `AddChildForm.tsx` (onboarding form, `<LanguageSelect>` for both fields) AND `AddChildModal.tsx` (dashboard modal, **grade tiles + flag tiles + a `<LanguageSelect>` for learning language**). The dashboard "Add child" CTA opens the **modal** (`add-child-modal`), per `rtl-alignment-polish` VER-C1. | Add-child cases must target the **modal** for the dashboard path (real user flow), and the **onboarding form** for the onboarding/register-wizard path. They differ structurally (modal has no `appLanguageTouched` auto-fill; it uses `app-lang-tile-{ar\|en}` + `add-child-learning-language` `<LanguageSelect>`). Cases below cover both and say which. |
| **`learningLanguage` IS on `LinkedChildResponse`** | `nswag-client.ts:7350` (`learningLanguage?: string`). The P8-04 design-spec "DATA GAP / Option B" was resolved to **Option A**. | The change-LL row CAN render the child's **current** learning language and CAN run the **client-side no-op guard**. Cases assert the current value renders and that selecting the same value disables the CTA. |
| **`learningLanguage` IS on `AddChildCommand`** | `nswag-client.ts:6782`. api-client regen landed. | Add-child submit cases can assert the field is sent on the network request. |
| **Change-LL field name is `newLearningLanguage`** (NOT `learningLanguage`) | `useChangeLearningLanguage.ts`, `ChangeLearningLanguageCommand` | Network-body assertions must check `newLearningLanguage` + `confirmFreshStart`. |
| **App does NOT set `html[dir]`** — RTL is component-level (`writingDirection`/`flexDirection`). `applyWebDirection()` sets `html[lang]` only. | All three existing specs note this explicitly. | RTL/locale assertions read `document.documentElement.lang` (`ar`/`en`) and/or element `dir` attributes on RN-Web hosts (`parent-header`), **never** `html[dir]`. |
| **UI-language switch in settings requires explicit Save** (`language-save`); persists via `useUpdateUserLanguage` → `PUT User.PreferredLanguage`. | `LanguagePanel.tsx` | Persistence cases must click Save and then verify survival across reload/relogin. |
| **Locale toggle on login screen** = `locale-switch-{ar\|en}`; **in settings** = `settings-language-switch` (combobox) → option (`role="radio"`) → `language-save`. | `LocaleThemeControls.tsx`, `LanguagePanel.tsx` | Two switch surfaces; cover both. |
| **Settings tabs**: `settings-tab-language`, `settings-tab-linkedChildren`; root `settings-root`, nav `settings-tabs-nav`. | `SettingsWeb.tsx` | Navigate to the LL row via `settings-tab-linkedChildren`. |
| **Confirm overlay danger marker uses a Unicode `⚠` glyph** (not a Lucide icon). | `ChangeLearningLanguageModal.tsx:165` | This contradicts the design spec's "use Lucide, not a Unicode glyph" (Brand law 11). **Logged as a defect candidate** (FE-defect note), not an E2E assertion — flag for reviewer. |

### testID / selector inventory (use these)

| Surface | Selector | Notes |
|---|---|---|
| Onboarding form learning-language | `add-child-learning-language` | `<LanguageSelect>` combobox; options are `role="radio"` |
| Onboarding form app-language | `add-child-app-language` | |
| Onboarding form submit | `add-child-to-list` | runs zod on press |
| Dashboard modal | `add-child-modal` | container; opened by `my-children-add-button` |
| Dashboard modal app-language tiles | `app-lang-tile-ar` / `app-lang-tile-en` | `role="radio"` tiles |
| Dashboard modal learning-language | `add-child-learning-language` | `<LanguageSelect>` (same testID as onboarding — scope by `add-child-modal`) |
| Dashboard modal grade tiles | `grade-tile-{1..6}` | |
| Dashboard modal submit | `add-child-submit` | |
| Dashboard add CTA | `my-children-add-button` | on `/children` |
| Settings language switch | `settings-language-switch` + `language-save` + `language-cancel` | |
| Login locale toggle | `locale-switch-ar` / `locale-switch-en` | login screen only |
| Settings tabs | `settings-tab-language`, `settings-tab-linkedChildren` | |
| Change-LL row → "Change" | `accessibilityLabel` = `parent.settings.linkedChildren.learningLanguage.change` (no testID) | **Gap: no testID on the LL row / Change button / picker / confirm.** Use accessible name (localized "Change"/"تغيير"). **Recommend implementer add testIDs** — see §Open Questions. |

---

## 1. P8-01-FE — Add-child learning-language selection

> Traces: P8-01 story AC ("required learning language at add-child"; "UI default-to-match, independently editable"; "no student-facing change"); P8-localization-FE brief §P8-01-FE AC1–AC5; design spec `onboarding/P8-01-FE.md`.

### P8-01-TC-01 — Learning-language field is present and starts empty (onboarding form)
- **Type:** functional · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** Seed a parent (via `Register-Parent`), log in, reach the onboarding add-child form (register wizard step 2 — see `rtl-alignment-polish` VER-R4 path: submit register → URL stays `/register` → `onboarding-add-child-tile`). If the wizard renders the **modal** instead, run P8-01-TC-12 (modal variant) for the dashboard flow.
- **Steps:**
  1. Open the add-child onboarding form.
  2. Locate the learning-language select (`add-child-learning-language`).
  3. Read its displayed value/placeholder before any interaction.
- **Expected:** The field renders, is visible, and shows the **placeholder** (`Choose learning language` / `اختر لغة الدراسة`) — **no pre-selected value**. The helper text (`learningLanguageHelper`) is visible beneath it.
- **Traces to:** P8-01-FE AC1 (required, distinct, no default).

### P8-01-TC-02 — Learning-language is required: submit blocked when empty
- **Type:** validation · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** add-child onboarding form open; all other required fields filled (name/email/password/grade) **but leave learning-language unselected**.
- **Steps:**
  1. Fill name, email, password, grade.
  2. Do NOT touch learning-language.
  3. Press `add-child-to-list`.
- **Expected:** Submit is blocked (no `onAdd`/navigation, no network add-child call). The learning-language field shows the i18n error `errors.learningLanguageRequired` (`Please choose a learning language.` / `يرجى اختيار لغة الدراسة.`) — **the resolved string, not the key `onboarding.addChild.errors.learningLanguageRequired`**.
- **Traces to:** P8-01-FE AC2 (zod blocks empty; i18n error).

### P8-01-TC-03 — Selecting "Arabic" learning language works and persists in the form
- **Type:** functional · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** add-child onboarding form open.
- **Steps:**
  1. Open `add-child-learning-language`, pick the Arabic option (`عربي / Arabic`).
- **Expected:** The select shows the Arabic option as selected; no error.
- **Traces to:** P8-01-FE AC1.

### P8-01-TC-04 — Selecting "English" learning language works
- **Type:** functional · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Steps:** Pick English (`الإنجليزية / English`) in `add-child-learning-language`.
- **Expected:** English shown as selected; no error.
- **Traces to:** P8-01-FE AC1.

### P8-01-TC-05 — App-language auto-fills to match learning language while untouched (onboarding form)
- **Type:** functional / state · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** add-child **onboarding form** (this behavior is `AddChildForm`-specific via `appLanguageTouched`; the dashboard modal does NOT auto-fill — see TC-13). App-language starts at default `ar`.
- **Steps:**
  1. Without touching app-language, pick **English** as the learning language.
  2. Read the app-language field value (`add-child-app-language`).
- **Expected:** App-language now shows **English** (auto-filled to match). No error.
- **Traces to:** P8-01-FE AC3 (UI defaults to match).

### P8-01-TC-06 — App-language stays independently editable; touching it stops auto-fill
- **Type:** state · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** onboarding form.
- **Steps:**
  1. Pick **English** learning language (app-language auto-fills to English).
  2. Manually set app-language back to **Arabic** (`add-child-app-language`).
  3. Now change learning language to **Arabic** then back to **English**.
- **Expected:** After step 2, app-language = Arabic. After step 3, app-language **stays Arabic** (auto-fill no longer overrides because the field was touched). Both fields remain enabled/editable throughout.
- **Traces to:** P8-01-FE AC3 ("independently editable; changing one does not lock the other"; the `appLanguageTouched` guard).

### P8-01-TC-07 — Learning language is sent on the add-child mutation (network assertion)
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in; a complete valid add-child form (any surface).
- **Steps:**
  1. Fill all fields; pick learning-language = **en**, app-language = **ar** (set app-language explicitly to en differs from learning so the two are distinguishable on the wire).
  2. Intercept the `POST /api/Parent/Add-Child` request (Playwright `page.waitForRequest`/route).
  3. Submit.
- **Expected:** The request body contains both `learningLanguage` (= the chosen learning value) **and** `language` (= the chosen app value) as **distinct** fields with the chosen values. Response is success; child is created.
- **Traces to:** P8-01-FE AC4 (learningLanguage sent), and axis A/B independence on the wire.

### P8-01-TC-08 — Created child's learning language round-trips (retrievable after creation)
- **Type:** persistence · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in.
- **Steps:**
  1. Add a child with learning-language = **en** (via the dashboard modal or onboarding form).
  2. Navigate to Settings → Linked children (`settings-tab-linkedChildren`).
  3. Find the new child's "Learning language" row.
- **Expected:** The row shows the **current learning language** localized as **English / الإنجليزية** (from `LinkedChildResponse.learningLanguage`). Confirms the chosen value persisted and is surfaced.
- **Traces to:** P8-01 story AC (`/Me`/child profile returns `learningLanguage`); P8-01-FE AC4; bridges into P8-04.

### P8-01-TC-09 — Add-child learning-language field renders correctly in Arabic RTL
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** locale = ar (default). add-child surface open.
- **Steps:**
  1. Confirm `html[lang] === 'ar'`.
  2. Inspect the learning-language label, placeholder, helper, and group eyebrow (onboarding form) / labels (modal).
- **Expected:** Labels/helper render in **Arabic** (`لغة الدراسة`, `اللغة التي سيدرس بها طفلك الرياضيات والعلوم.`, group eyebrow `اللغات` on the onboarding form). Text alignment is right/RTL (`writingDirection`/`textAlign`). **No raw i18n keys** (e.g. no `onboarding.addChild.labelLearningLanguage`) visible anywhere on screen.
- **Traces to:** P8-01-FE AC1; design spec §7 RTL; P8-99 key-completeness.

### P8-01-TC-10 — Add-child learning-language field renders correctly in English LTR
- **Type:** RTL-i18n · **Priority:** P2 · **Agent:** frontend-e2e-tester
- **Steps:** Switch to en (login `locale-switch-en`), open add-child, inspect.
- **Expected:** Labels render in English (`Learning language`, helper, `App language`, group `Languages`); LTR alignment; no raw keys.
- **Traces to:** P8-01-FE AC1.

### P8-01-TC-11 — Two language fields are visually/semantically distinct (disambiguation)
- **Type:** functional / a11y · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** onboarding form open.
- **Steps:**
  1. Read the two field labels.
  2. Read the two helper texts.
- **Expected:** One field is labelled **Learning language / لغة الدراسة** with helper about **Math & Science**; the other is **App language / لغة التطبيق** with helper about **buttons, menus, messages**. They are not both labelled "language". (Onboarding form also wraps them in a `Languages / اللغات` group.)
- **Traces to:** P8-01-FE AC1 (distinct from UI language); design spec §1.

### P8-01-TC-12 — Add-child via dashboard MODAL: learning-language required + sent
- **Type:** functional / validation · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in on `/children`.
- **Steps:**
  1. Click `my-children-add-button` → `add-child-modal` opens.
  2. Fill name/email/password, pick a grade tile, pick an app-language flag tile (`app-lang-tile-ar`), **leave learning-language (`add-child-learning-language`) empty**, press `add-child-submit`.
  3. Then select learning-language = en and submit, intercepting `POST /api/Parent/Add-Child`.
- **Expected:** Step 2 → modal shows the learning-language required error (`parent.addChildModal.errors.learningLanguageRequired`, resolved string) and no add-child call fires. Step 3 → request body carries `learningLanguage: "en"` distinct from `language` (the flag-tile value); success.
- **Traces to:** P8-01-FE AC2, AC4 (dashboard modal path).

### P8-01-TC-13 — Dashboard modal does NOT auto-fill app language (documented difference)
- **Type:** state / regression · **Priority:** P2 · **Agent:** frontend-e2e-tester
- **Preconditions:** dashboard modal open (app-language tiles start unselected — `null`).
- **Steps:**
  1. Pick learning-language = en (`add-child-learning-language`).
  2. Read the app-language flag tiles' selected state.
- **Expected:** No app-language tile becomes auto-selected (the modal has no auto-fill; app-language is its own required choice). This documents that the "default-to-match" affordance (AC3) lives only on the **onboarding form**, not the modal.
- **Traces to:** P8-01-FE AC3 (records the surface difference — flag if the lead expects auto-fill in the modal too; see Open Questions).

### P8-01-TC-14 — No student-facing path to set/change learning language
- **Type:** auth-authz / negative · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** Seed a parent + child; log in **as the child/student** account.
- **Steps:**
  1. Visit the student app surfaces reachable to a student (home, settings if any).
  2. Search for any control that sets/changes "learning language" / "لغة الدراسة".
- **Expected:** No student-reachable control exists to set or change learning language. The change-LL row and add-child are parent-only (`(parent)` routes / parent dashboard). If a student can reach `/settings` linked-children, that is a defect.
- **Traces to:** P8-01 story AC ("immutable by the student"); P8-01-FE AC5; product rule (no student self-service).

---

## 2. P8-04-FE — Parent changes a child's learning language (fresh-start flow)

> Traces: P8-04 story AC (parent-only; explicit confirm flag; resets Math/Science; gamification retained); P8-localization-FE brief §P8-04-FE AC1–AC5; design spec `parent-settings/P8-04-FE.md`; `LinkedChildrenPanel.tsx` + `ChangeLearningLanguageModal.tsx` + `useChangeLearningLanguage.ts`.
> **Navigation:** log in as parent → `/settings` → `settings-tab-linkedChildren`. The LL row sits below each `ChildCard`. **No testIDs on this flow today** — use accessible names (localized "Change"/"تغيير", "Change Language"/"تغيير اللغة", "Reset & Change"/"إعادة الضبط والتغيير", and the ack checkbox label).

### P8-04-TC-01 — Change-LL row shows the child's current learning language
- **Type:** functional · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** Seed parent + a child whose `learningLanguage = "ar"`; log in as parent; open Settings → Linked children.
- **Steps:**
  1. Locate the child's "Learning language" row (label `rowLabel` = `Learning language` / `لغة الدراسة`).
- **Expected:** The row renders the row label and the **current value localized** (`Arabic` / `العربية`). Confirms `LinkedChildResponse.learningLanguage` is surfaced (Option A resolved).
- **Traces to:** P8-04-FE AC1; P8-01 round-trip.

### P8-04-TC-02 — "Change" opens the picker; CTA hidden/disabled until a value is chosen
- **Type:** functional / state · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Steps:**
  1. Tap "Change" on the LL row.
  2. Observe the revealed picker (label `pickerLabel`, helper `pickerHelper`) and the "Change Language" CTA.
- **Expected:** Picker strip appears with the medium-of-instruction label + a helper clarifying it is separate from the app language. The "Change Language" CTA (`changeCta`) is **disabled** while no language is selected.
- **Traces to:** P8-04-FE AC1; design spec §3.

### P8-04-TC-03 — Same-language selection is a no-op (CTA stays disabled + hint)
- **Type:** boundary / negative · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** child current LL = ar.
- **Steps:**
  1. Open picker, select **Arabic** (== current).
- **Expected:** The "Change Language" CTA is **disabled**, and the `noChange` hint shows (`This is already the learning language.` / `هذه هي لغة الدراسة الحالية بالفعل.`). The confirm overlay does **not** open. No network call.
- **Traces to:** P8-04-FE AC4 (same language = no-op); story AC9.

### P8-04-TC-04 — Selecting a different language enables CTA; CTA opens the confirm overlay (no mutation yet)
- **Type:** functional · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** child current LL = ar.
- **Steps:**
  1. Open picker, select **English**.
  2. Note the CTA is enabled; intercept any `PUT /api/Parent/Change-Learning-Language` to assert it does NOT fire yet.
  3. Press "Change Language".
- **Expected:** Confirm overlay (`role="dialog"`) opens showing the from→to restatement, the consequence lines, the ack checkbox, and Confirm/Cancel. **No** change-LL network request has fired (the picker CTA only opens the modal).
- **Traces to:** P8-04-FE AC2/AC3 (no silent change; explicit confirm gate); design spec §4.

### P8-04-TC-05 — Confirm overlay content: from→to + reset/keep consequence copy (i18n, RTL)
- **Type:** functional / RTL-i18n · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open (ar → en change), locale = ar.
- **Steps:**
  1. Read the title, from→to line, the willReset (loss) line, the willKeep line, the rareNote.
- **Expected (ar):** Title `إعادة ضبط تقدّم الرياضيات والعلوم؟`; from→to shows `العربية ← English` (arrow per locale); loss line `سيُعاد ضبط تقدّم طفلك في الرياضيات والعلوم...` in danger styling; keep line names العربية/الإنجليزية/النقاط/السلسلة/الشارات retained; rare note present. **No raw keys** (`...confirm.title`, etc.). All RTL-aligned.
- **Traces to:** P8-04-FE AC2 ("clear loss statement; XP/streak/badges kept"); story AC (Math/Science reset, Arabic/English + gamification retained).

### P8-04-TC-06 — Confirm button gated by the acknowledgement checkbox
- **Type:** validation / negative · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open, ack unchecked.
- **Steps:**
  1. Without ticking the ack checkbox, attempt to press "Reset & Change" (Confirm).
  2. Then tick the ack checkbox (`confirm.ack`) and observe Confirm.
- **Expected:** While ack is unchecked, Confirm is **disabled** (`accessibilityState.disabled = true`, reduced opacity) and pressing it does nothing / fires no request. After ticking ack, Confirm becomes enabled.
- **Traces to:** P8-04-FE AC3 (only submitted after explicit confirm; `confirmFreshStart: true`); design spec §4.2/§8.

### P8-04-TC-07 — Confirm fires the mutation with confirmFreshStart=true and the right body
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open (child current ar → selected en), ack ticked.
- **Steps:**
  1. Intercept `PUT /api/Parent/Change-Learning-Language`.
  2. Press "Reset & Change".
- **Expected:** Exactly one request fires with body `{ childId: <child id>, newLearningLanguage: "en", confirmFreshStart: true }` (note the field name `newLearningLanguage`). Server returns success.
- **Traces to:** P8-04-FE AC3; `useChangeLearningLanguage` contract.

### P8-04-TC-08 — Success: overlay closes, success strip shows, current value refetches to new language
- **Type:** functional / state · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** valid change ar → en confirmed (TC-07 path).
- **Steps:**
  1. After confirming, observe the overlay, the in-panel success strip, and the LL row's current value.
- **Expected:** Overlay closes; a success strip appears (`learningLanguage.success` — names the child + new language; e.g. `Done. {name} now studies Math and Science in English...`), `accessibilityLiveRegion="polite"`, auto-clears (~4s). After `useMyChildren` invalidation/refetch, the LL row's **current value now reads English / الإنجليزية** (refetch, not optimistic — the row reads from query data).
- **Traces to:** P8-04-FE AC5 (success refreshes child data); hook `invalidateQueries(family.myChildren)`.

### P8-04-TC-09 — Cancel from the overlay aborts (no mutation, no change)
- **Type:** negative · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open (ar → en), ack may be ticked or not.
- **Steps:**
  1. Intercept `PUT .../Change-Learning-Language`.
  2. Tick ack, then press **Cancel** (or ✕).
- **Expected:** Overlay closes; **no** change-LL request fired; the LL row still shows the original language (ar). Re-opening the picker shows ack reset (unchecked) on next open.
- **Traces to:** P8-04-FE AC3 (no silent change); design spec §5 (cancel = no call).

### P8-04-TC-10 — Backdrop/stray tap does NOT dismiss-and-lose the warning
- **Type:** a11y / negative · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open.
- **Steps:**
  1. Tap the scrim area outside the card (if reachable on web).
- **Expected:** Overlay does **not** close on backdrop tap; only explicit Cancel / ✕ / hardware-back cancels. (The component wires `onRequestClose` only to explicit controls; the scrim Stack has no dismiss `onPress`.)
- **Traces to:** design spec §4 / §8 (no accidental confirmation); P8-04-FE AC3.

### P8-04-TC-11 — Server error (500) keeps the overlay open and surfaces a friendly message
- **Type:** state (error) / negative · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open, ack ticked.
- **Steps:**
  1. Route `PUT .../Change-Learning-Language` to return **500**.
  2. Press Confirm.
- **Expected:** Overlay stays open; a `ServerErrorBanner` shows the generic server-error message (i18n, **not** a raw key, not "No internet connection"). Cancel still works. The LL row current value unchanged.
- **Traces to:** P8-04-FE AC5 (error states surfaced); design spec §5 error matrix.

### P8-04-TC-12 — Forbidden (403) maps to the "not your child" message
- **Type:** auth-authz / negative · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open, ack ticked.
- **Steps:**
  1. Route the change-LL request to **403** (ProblemDetails / BaseResponse forbidden).
  2. Confirm.
- **Expected:** Overlay stays open; banner shows `error.forbidden` (`You can only change the learning language for your own children.` / `يمكنك تغيير لغة الدراسة لأطفالك فقط.`) via the `byStatus[403]` mapping — resolved string, not key.
- **Traces to:** P8-04-FE AC1 (parent-only/family-scoped); design spec §5; story AC1.

### P8-04-TC-13 — 424 confirm-missing maps to the confirm message (defensive path)
- **Type:** negative / boundary · **Priority:** P2 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open, ack ticked (UI normally guarantees `confirmFreshStart: true`, so 424 is unreachable in practice — assert the defensive mapping).
- **Steps:**
  1. Route the change-LL request to **424** (FailedDependency).
  2. Confirm.
- **Expected:** Banner shows `error.confirmMissing` (`Please confirm the reset before changing the language.` / `يُرجى تأكيد إعادة الضبط قبل تغيير اللغة.`) via `byStatus[424]`. Resolved string.
- **Traces to:** P8-04-FE AC3 (confirm gate); design spec §5; story AC2.

### P8-04-TC-14 — Pending state: Confirm shows loading; controls locked; overlay focus-trapped
- **Type:** state (loading) · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** overlay open, ack ticked.
- **Steps:**
  1. Route the change-LL request with an artificial delay.
  2. Press Confirm; observe during the in-flight window.
- **Expected:** Confirm label shows `common.loading`; Confirm/Cancel/✕/ack are disabled (reduced opacity, no press) while `isPending`; overlay stays open and `accessibilityViewIsModal` traps focus. After resolve → success path (TC-08).
- **Traces to:** design spec §5 (submitting), §8 (focus trap).

### P8-04-TC-15 — Re-open after success: ack resets and current value reflects the new language
- **Type:** state / regression · **Priority:** P2 · **Agent:** frontend-e2e-tester
- **Preconditions:** a successful change ar → en just completed (TC-08).
- **Steps:**
  1. Tap "Change" again; select **Arabic** (now the *different* one).
  2. Open the overlay.
- **Expected:** Picker current value reads English; selecting Arabic is now a valid (non-no-op) change; the overlay opens with ack **unchecked** (reset). The from→to reads `English ← العربية` appropriately.
- **Traces to:** P8-04-FE AC4; state reset hygiene.

### P8-04-TC-16 — Change-LL flow renders correctly in Arabic RTL (no raw keys)
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** locale = ar.
- **Steps:** Walk the full row → picker → overlay in Arabic; inspect alignment + strings.
- **Expected:** All strings localized to Arabic, RTL-aligned (rows use `row-reverse`, text `right`). The from→to arrow points to the new value in reading order. **No** raw keys (`parent.settings.linkedChildren.learningLanguage.*`) anywhere.
- **Traces to:** design spec §7; P8-99 key-completeness.

### P8-04-TC-17 — Change-LL flow renders correctly in English LTR
- **Type:** RTL-i18n · **Priority:** P2 · **Agent:** frontend-e2e-tester
- **Steps:** Same as TC-16 in English (switch via settings, save, navigate back to linked children).
- **Expected:** Strings in English, LTR-aligned, from→to uses `→`, no raw keys.
- **Traces to:** design spec §7.

### P8-04-TC-18 — Parent-only: no student route reaches the change-LL flow
- **Type:** auth-authz / negative · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** Seed parent + child; log in **as the child**.
- **Steps:**
  1. Attempt to navigate to `/settings` and the linked-children tab as the student.
- **Expected:** The student cannot reach a parent settings surface that exposes the change-LL row (redirect to a student home or no linked-children panel). No change-LL control is student-reachable.
- **Traces to:** P8-04-FE AC1; story AC1 (student cannot change); product rule.

### P8-04-TC-19 — Signed-out user cannot reach the change-LL surface
- **Type:** auth-authz / negative · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Steps:** Without logging in, navigate directly to `/settings`.
- **Expected:** Redirect to login/role-select; the change-LL row is never rendered to an unauthenticated user.
- **Traces to:** auth routing; design spec §11 (parent-only panel).

---

## 3. P8-SHELL — App-shell UI-language switch / RTL foundation (P8-99-FE)

> Traces: P8-99-FE brief AC (UI-language switch promoted to settings + persists to backend; survives sign-out/sign-in; RTL flip; no hardcoded strings). **Reconcile with existing specs** — see the "already covered" notes; new cases add what those miss (persistence across reload/relogin, raw-key sweep on the P8 surfaces, axis independence).

### P8-SHELL-TC-01 — Settings UI-language switch flips locale on Save (ar↔en)
- **Type:** functional / RTL-i18n · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in; `/settings` → language tab.
- **Steps:**
  1. Read `html[lang]`.
  2. Open `settings-language-switch`, choose the opposite locale, press `language-save`.
  3. Read `html[lang]` and a known heading's direction after save.
- **Expected:** `html[lang]` changes (ar→en or en→ar). Component direction reflects the new locale (e.g. `parent-header[dir]`). **Already partially covered** by `parent-lang-check.spec.ts` (asserts `html[lang]` flips) — this case additionally asserts the change requires Save and that the UI strings re-render in the new language. **Flag as overlap with `parent-lang-check`** — implementer should extend that spec rather than duplicate the login/nav boilerplate.
- **Traces to:** P8-99-FE AC (UI-language switch in settings, persists).

### P8-SHELL-TC-02 — UI-language choice persists to backend (Save → reload survives)
- **Type:** persistence · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in; current UI = ar.
- **Steps:**
  1. In settings, switch UI language to **en**, press `language-save`; intercept `PUT` to `User.PreferredLanguage` (the `useUpdateUserLanguage` route) and assert it carries `userPreferredLanguage: "en"` and returns success.
  2. Reload the page (`page.reload()`).
  3. Read `html[lang]` / app chrome language.
- **Expected:** Save fires the backend update with the new language; after reload the app is still in **English** (persisted, not just local). Not covered by existing specs (they switch and switch back within one session).
- **Traces to:** P8-99-FE AC ("persists to the backend ... survives sign-out/sign-in").

### P8-SHELL-TC-03 — UI-language choice survives sign-out / sign-in
- **Type:** persistence / auth · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in.
- **Steps:**
  1. Set UI language to **en** in settings and Save.
  2. Log out.
  3. Log back in as the same parent.
- **Expected:** After re-login the app loads in **English** (the persisted `PreferredLanguage` drives the locale on next session). This is the core P8-99 acceptance the existing specs do NOT cover.
- **Traces to:** P8-99-FE AC (survives sign-out/sign-in).

### P8-SHELL-TC-04 — Login-screen locale toggle flips chrome immediately (no Save)
- **Type:** functional / RTL-i18n · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** on `/login?role=parent`.
- **Steps:**
  1. Read `html[lang]`.
  2. Tap `locale-switch-en` (or `-ar`).
- **Expected:** Login chrome strings switch language immediately; `html[lang]` updates. **Already exercised incidentally** by `rtl-alignment-polish` `switchLocale()` helper — but no dedicated assertion of the login toggle. Add a focused assert; flag overlap.
- **Traces to:** P8-99-FE AC (login control still works; settings is the *additional* persistent location).

### P8-SHELL-TC-05 — Arabic default → RTL component direction is active
- **Type:** RTL-i18n · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** fresh session (default locale ar).
- **Steps:**
  1. Load login (or parent home), confirm `html[lang] === 'ar'`.
  2. Inspect a known RN-Web host (`parent-header` after login) for `dir="rtl"`.
- **Expected:** Default locale is Arabic; component direction is RTL. **Heavily covered** already (`rtl-alignment-polish` VER-L1/assertRtlActive, `rtl-reverify-fresh`). **Mark as DUPLICATE of existing coverage** — do not re-implement; cite the existing specs in the execution report.
- **Traces to:** P8-99-FE AC (RTL pass); product Arabic-first default.

### P8-SHELL-TC-06 — No raw i18n keys leak on the P8 surfaces (key-completeness sweep)
- **Type:** RTL-i18n / regression · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in; iterate ar and en.
- **Steps:**
  1. Visit: add-child modal, settings language tab, settings linked-children tab (incl. change-LL row, picker, and confirm overlay opened).
  2. Scrape visible body text.
- **Expected:** No string matching a dotted i18n-key pattern (e.g. `/onboarding\.addChild\./`, `/parent\.settings\.linkedChildren\.learningLanguage\./`, `/common\./` raw) is visible. The existing specs only spot-check a couple of keys (`auth.guardianOnly`); this is a focused sweep over the **P8 surfaces specifically**.
- **Traces to:** P8-99-FE AC (i18n key-completeness on built screens).

### P8-SHELL-TC-07 — Brand fonts load (no fallback/invisible text) on the P8 surfaces
- **Type:** functional / regression · **Priority:** P2 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent logged in.
- **Steps:**
  1. On the linked-children + add-child surfaces, check that headings/body text are visible (non-zero size, rendered) in both ar and en.
- **Expected:** Text renders (fonts resolved — Cairo/Tajawal ar, Poppins en); no invisible/zero-width text from a missing face. (Computed `font-family` includes the expected brand face where assertable, else fall back to "text is visible and laid out".) **Mark as best-effort** — exact face assertion is brittle on web; primary assertion is visibility.
- **Traces to:** P8-99-FE AC (brand fonts render on web).

---

## 4. Axis independence — UI language vs Learning language (cross-cutting)

> The single highest-value non-obvious area: the two "language" axes must be **independent**. These cases prove changing one never silently changes the other.

### P8-AXIS-TC-01 — Changing UI language does NOT change any child's learning language
- **Type:** functional / regression · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent with a child whose `learningLanguage = "en"`; parent UI = ar.
- **Steps:**
  1. Note the child's LL row value (English).
  2. Switch the parent UI language to **en** in settings, Save.
  3. Return to linked children; read the child's LL row value.
- **Expected:** The child's **learning language is still English** (unchanged). Only the app chrome language changed. **No** `Change-Learning-Language` request fired during the UI-language switch.
- **Traces to:** P8-01 story AC ("LearningLanguage separate from PreferredLanguage"); brief axis A vs axis B.

### P8-AXIS-TC-02 — Changing a child's learning language does NOT change the parent's UI language
- **Type:** functional / regression · **Priority:** P0 · **Agent:** frontend-e2e-tester
- **Preconditions:** parent UI = ar; child LL = ar.
- **Steps:**
  1. Confirm parent UI is Arabic (`html[lang]==='ar'`).
  2. Perform a full change-LL ar → en (TC-07 path) for the child.
  3. After success, read `html[lang]` and the app chrome.
- **Expected:** The parent's **UI language stays Arabic**; only the child's learning language changed. No `User.PreferredLanguage` update fired.
- **Traces to:** P8-01 story AC; brief axis independence; P8-04-FE.

### P8-AXIS-TC-03 — Add-child: learning ≠ app language values are both honored
- **Type:** functional · **Priority:** P1 · **Agent:** frontend-e2e-tester
- **Preconditions:** dashboard modal (app-language is its own choice; no auto-fill) — so the two can be set to genuinely different values.
- **Steps:**
  1. Pick app-language = **ar** (`app-lang-tile-ar`), learning-language = **en** (`add-child-learning-language`).
  2. Intercept `POST /api/Parent/Add-Child` and submit.
  3. After creation, open the child's LL row.
- **Expected:** Request body has `language: "ar"` **and** `learningLanguage: "en"` (different). The LL row shows English. (App/UI language ar is independent of learning en.)
- **Traces to:** P8-01-FE AC1/AC3/AC4; axis independence.

---

## 5. Reconciliation with existing specs (do not duplicate)

| Existing spec | What it already covers | This catalog's stance |
|---|---|---|
| `tests/e2e/specs/parent-lang-check.spec.ts` | Settings language switch flips `html[lang]` (with Save). | **P8-SHELL-TC-01** overlaps — extend that spec; do NOT add a second login/nav harness. New value is the Save-required + re-render assertion. |
| `tests/e2e/specs/rtl-alignment-polish.spec.ts` | AR RTL active (lang=ar), add-child **modal** opens (AR+EN), edit-modal pre-fill, no Country field, login/register rounded inputs, sidebar/layout RTL, language-Save no "no internet". Seeds children **with `learningLanguage`** already. | **P8-SHELL-TC-05** is a DUPLICATE (RTL-active) — cite, don't re-run. The add-child-modal open flow is reusable scaffolding for P8-01-TC-12/13. |
| `tests/e2e/specs/rtl-reverify-fresh.spec.ts` | Sidebar/overview RTL double-flip fixes; settings language Save success. | RTL-foundation overlap (P8-SHELL-TC-05). Reuse the demo-parent login helper. |

**None of the existing specs cover:** learning-language as a *distinct* required field with validation (P8-01-TC-01..13), the **change-learning-language flow** at all (P8-04-TC-01..19), UI-language **persistence across reload/relogin** (P8-SHELL-TC-02/03), the **raw-key sweep on P8 surfaces** (P8-SHELL-TC-06), or **axis independence** (P8-AXIS-TC-01..03). Those are the net-new coverage this catalog delivers.

---

## 6. Open questions / assumptions for the lead (resolve before implementation)

1. **No testIDs on the P8-04 change-LL flow.** The LL row, "Change" button, picker, "Change Language" CTA, ack checkbox, and Confirm have **no `testID`** — only localized accessible names. E2E by accessible name is locale-brittle (ar vs en strings). **Recommend the implementer/frontend add stable testIDs** (e.g. `ll-row-{childId}`, `ll-change`, `ll-picker`, `ll-change-cta`, `ll-ack`, `ll-confirm`, `ll-modal`) before/with implementation. Assumption until then: select by `role` + localized name.
2. **Dashboard modal has no app-language auto-fill** (TC-13). The "default-to-match" AC (P8-01-FE AC3) is implemented only on the **onboarding `AddChildForm`**, not the dashboard `AddChildModal` (which is the primary real-world add-child path). **Is auto-fill expected in the modal too?** If yes, TC-13 flips from "documents the difference" to a **defect**. Lead to confirm scope.
3. **Confirm overlay uses a Unicode `⚠` glyph** (`ChangeLearningLanguageModal.tsx:165`) instead of a Lucide icon — contradicts design spec §4.2 / Brand law 11. Flagged as a defect candidate, not asserted in E2E. Confirm whether to file.
4. **Math/Science reset is backend behavior** — E2E cannot directly verify attempts were deleted / gamification retained without a student-side progress surface and a seeded-progress fixture. Those are **api-tester** territory (P8-04 backend integration tests). FE cases assert the **request carried `confirmFreshStart:true`** and the **success/refresh UX**; the actual reset semantics are out of FE-E2E scope. Confirm this split.
5. **Test data seeding** — cases assume seeding via API (`Register-Parent`, `Add-Child` with `learningLanguage`) as the existing specs do (`rtl-alignment-polish` `seedParentWithChild`). Reuse those helpers. For a child whose LL must be a known value, seed `Add-Child` with the desired `learningLanguage`.
6. **Student login fixture** — TC-14 / TC-18 need a working **child/student** login. Confirm the student auth path (child credentials from `Add-Child`) and the student home route so the "no student-facing control" assertions can run; otherwise mark those cases **blocked (needs student login fixture)**.
