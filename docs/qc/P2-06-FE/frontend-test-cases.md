# Frontend E2E Test Cases — P2-06-FE (Take a quiz — 4 question types)

> **Target agent:** `frontend-e2e-tester` → implement as `tests/e2e/specs/P2-06-FE.spec.ts`, one Playwright test per FE-TC.
> **Surface:** student-app web PWA, **child** surface. Route: `apps/student-app/app/(child)/lessons/[lessonId].tsx`.
> **Selectors:** `getByTestId` first; until the testIDs in README §4 OQ1 are wired, use the **role/label fallback** named per case.
> Roles available now: `radio` / `radiogroup` (MCQ + TrueFalse), `text` (FillInBlank field, Matching tile), `progressbar` (ProgressDots),
> `group` (QuestionCard), `alert` (AnswerFeedbackStrip on web), `button` (Submit/Next/Start/Back), `region` (AttemptSummaryCard).
> **Seed:** API `POST /api/Users/Authentication/Register-Parent` → `POST /api/Parent/Add-Child` → login as **child** via UI (persona = student).
>   Then reach a lesson via the home ContinueCard / Subjects→Lessons tab → tap **Start lesson**. See README §4 OQ2 for the all-4-types caveat.
> **BLOCKED** cases scaffold as `test.skip` with the blocker in the title.

Legend — **Type**: functional / validation / negative / boundary / state / RTL-i18n / a11y / regression. **Priority**: P0/P1/P2.

---

## Group A — Enter the quiz + progress (AC2)

### FE-TC-01 — Start lesson creates an attempt and shows the quiz stage
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions / seed:** Seeded parent+child via API; child logged in; navigated to an Available lesson (`/(child)/lessons/{id}?subjectId={sid}`).
- **Steps:**
  1. On the Intro stage, tap **Start lesson** (`quiz-start-cta`; fallback: the only `button` with the start a11y label).
  2. Wait for `useStartAttempt` to resolve.
- **Expected:** The screen transitions from Intro to the **quiz stage** — a question card appears (`group` role / `quiz-question-card`) with at least one answer control, and the progress label "Question 1 of N" / "السؤال ١ من N" is visible.
- **Traces to:** AC2 (starting a quiz creates an Attempt; shows question card + controls).

### FE-TC-02 — Progress indicator (ProgressDots) renders with the correct count
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage (FE-TC-01).
- **Steps:**
  1. Locate the progress dots in the TopBar (`progressbar` role / `quiz-progress-dots`).
  2. Read `aria-valuenow`, `aria-valuemin`, `aria-valuemax`.
- **Expected:** `progressbar` present; `aria-valuemin=1`, `aria-valuenow=1` on the first question, `aria-valuemax` equals the total question count (matches the "of N" in the progress label).
- **Traces to:** AC2 (progress).

### FE-TC-03 — Question card shows stem + type-specific controls (kid-UX one-question focus)
- **Type:** functional / a11y · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage.
- **Steps:**
  1. Assert exactly **one** question card (`group` role) is on screen.
  2. Assert the question stem text is non-empty.
  3. Assert at least one answer control (radio / text field) is present inside it.
- **Expected:** One question card visible at a time (no multi-question scroll); non-empty stem; answer controls present. Confirms kid-UX single-question focus.
- **Traces to:** AC2, Design Spec §1 Surface 2, kid-UX.

### FE-TC-04 — No Skip affordance and no confetti (product/spec overrides)
- **Type:** negative · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage.
- **Steps:**
  1. Search the quiz stage for any "Skip" control (by role=button + EN label after switching to en, or absence).
  2. Submit a correct answer (if reachable) and confirm no confetti/RewardPopup element appears.
- **Expected:** No Skip button anywhere in the quiz; no confetti / RewardPopup. (Design Spec §10 — Skip and confetti are deliberately out of this wave.)
- **Traces to:** Design Spec §10 deltas (product overrides).

---

## Group B — MCQ (AC1, AC3)

### FE-TC-05 — MCQ renders an option list (radiogroup of options)
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Reached an **MCQ** question (`questionType=_1`). Fallback if no deterministic seed: assert conditionally when an MCQ question is the current one.
- **Steps:**
  1. Assert the MCQ renderer is present (`quiz-renderer-mcq`; fallback: a `radiogroup` containing ≥2 `radio` children, none of which is the 2-child True/False pair — disambiguation is BLOCKED on testID, see README OQ1).
  2. Count the `radio` options.
- **Expected:** ≥2 MCQ options rendered as `radio` elements within a `radiogroup`; labels non-empty.
- **Traces to:** AC1 (MCQ), AC2 (answer controls per type).

### FE-TC-06 — MCQ accepts a selection (input correctness)
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** MCQ question on screen (FE-TC-05).
- **Steps:**
  1. Tap an option (`quiz-mcq-option-0`; fallback: first `radio` in the MCQ radiogroup).
  2. Read its `aria-checked`.
- **Expected:** The tapped option becomes `aria-checked=true`; other options remain `aria-checked=false`; the Submit ("Check answer") button becomes enabled.
- **Traces to:** AC3 (renders + accepts input correctly).

### FE-TC-07 — MCQ instant visual selection state (single selection)
- **Type:** state · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** MCQ on screen.
- **Steps:**
  1. Tap option A → assert `aria-checked=true` on A, `false` on B/C/D.
  2. Tap option B → assert `aria-checked` moves to B, A returns to `false`.
- **Expected:** Selection state updates instantly on tap; exactly one option checked at a time (selected chrome is the affordance).
- **Traces to:** AC3, Design Spec §2 MCQ states, kid-UX (instant feedback on selection).

### FE-TC-08 — MCQ option a11y: radio role + ≥48px touch target + accessible label
- **Type:** a11y · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** MCQ on screen.
- **Steps:**
  1. For an option, read `role`, `aria-label`, and the rendered height.
- **Expected:** `role=radio`; `aria-label` is non-empty and not a raw i18n key (e.g. "Option A: …"); min height ≥48px (component min 56).
- **Traces to:** Design Spec §3.2 / §7 (kid-a11y), kid-UX large targets.

---

## Group C — True/False (AC1, AC3)

### FE-TC-09 — True/False renders a two-choice pair
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Reached a **True/False** question (`questionType=_2`). Conditional-assert if no deterministic seed.
- **Steps:**
  1. Assert the TrueFalse renderer (`quiz-renderer-truefalse`; fallback: a `radiogroup` with exactly **2** `radio` children).
  2. Confirm the two sides carry the True / False labels (en: "True"/"False").
- **Expected:** Exactly two `radio` choices rendered; labels present (locale-correct). Confirms the renderer is True/False, not MCQ.
- **Traces to:** AC1 (True/False), AC2.

### FE-TC-10 — True/False toggles selection
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** True/False on screen.
- **Steps:**
  1. Tap **True** (`quiz-truefalse-true`; fallback: first of the 2 radios) → assert `aria-checked=true`; False `false`.
  2. Tap **False** → assert selection moves; True returns to `false`.
  3. Confirm Submit becomes enabled after a side is chosen.
- **Expected:** Tapping toggles selection instantly, single-select; Submit enabled once a side is picked. FE converts the boolean to `"true"`/`"false"` at submit (Design Spec §11.5) — opaque to the test.
- **Traces to:** AC3.

### FE-TC-11 — True/False instant selection chrome
- **Type:** state · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** True/False on screen.
- **Steps:**
  1. Tap True, then False, asserting `aria-checked` flips correctly each time.
- **Expected:** Visual/selected state updates instantly; exactly one side selected.
- **Traces to:** Design Spec §2.2 (TrueFalse states), kid-UX.

---

## Group D — Fill-in-the-blank (AC1, AC3)

### FE-TC-12 — FillInBlank renders a text input
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Reached a **FillInBlank** question (`questionType=_4`). Conditional-assert if no deterministic seed.
- **Steps:**
  1. Assert the FillInBlank field (`quiz-fillblank-input`; fallback: a `textbox`/`text`-role input with the "Answer field" / "حقل الإجابة" a11y label and the placeholder "Type your answer" / "اكتب إجابتك").
- **Expected:** A single text input rendered with the localized placeholder + accessible "Answer field" label.
- **Traces to:** AC1 (Fill-in-the-blank), AC2.

### FE-TC-13 — FillInBlank accepts typed input
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** FillInBlank on screen.
- **Steps:**
  1. Type a value (e.g. "4") into the field.
  2. Read the input value.
  3. Assert Submit becomes enabled (gated on non-empty, non-whitespace).
- **Expected:** Field holds the typed value; Submit enabled with non-empty input.
- **Traces to:** AC3.

### FE-TC-14 — FillInBlank: whitespace-only input does not enable Submit
- **Type:** validation / boundary · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** FillInBlank on screen.
- **Steps:**
  1. Type only spaces into the field.
  2. Read the Submit button's enabled state (`aria-disabled` / disabled).
- **Expected:** Submit stays **disabled** for whitespace-only input (the screen gates on `.trim().length > 0`).
- **Traces to:** Design Spec §1 Surface 2 (Check answer disabled until an answer is picked), input correctness.

### FE-TC-15 — FillInBlank: empty input keeps Submit disabled (boundary)
- **Type:** boundary · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions:** FillInBlank on screen, field empty.
- **Steps:**
  1. Without typing, read the Submit enabled state.
- **Expected:** Submit disabled when the field is empty.
- **Traces to:** input correctness, kid-UX (no submitting an empty answer).

---

## Group E — Matching (AC1) — STUB

### FE-TC-16 — Matching renders the stub tile
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Reached a **Matching** question (`questionType=_3`). **NOTE:** BE has zero Matching questions seeded (README OQ2) — likely unreachable → BLOCKED in practice.
- **Steps:**
  1. Assert the MatchingPanel stub (`quiz-matching-panel`; fallback: a `text`-role tile whose a11y label contains the "Matching questions coming soon" + "Tap Next to skip" copy).
- **Expected:** A muted "coming soon" tile renders (🧩 glyph + title + sub). No interactive matching UI.
- **Traces to:** AC1 (Matching — as stub), Design Spec §3.6.
- **Status:** **BLOCKED** — no Matching question is seeded to reach; scaffold `test.skip` ("no seeded Matching question to reach the stub").

### FE-TC-17 — Matching stub: Submit becomes "Next" and advances with empty payload
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Matching stub on screen (FE-TC-16).
- **Steps:**
  1. Confirm the primary CTA reads "Next" / "التالي" (not "Check answer") and is enabled.
  2. Tap it → the empty-string answer is submitted → the quiz advances (or completes).
- **Expected:** CTA is "Next", enabled; tapping submits `""` and advances to the next question / Summary.
- **Traces to:** Design Spec §2.4 / §11.6 (stub submits empty payload).
- **Status:** **BLOCKED** — depends on reaching a Matching question (same blocker as FE-TC-16).

### FE-TC-18 — Real Matching interaction (drag-pair)
- **Type:** functional · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions:** A real Matching renderer (drag-pair UI).
- **Expected:** Child can pair items and the answer is submitted.
- **Traces to:** AC1 (Matching — full).
- **Status:** **BLOCKED** — real Matching renderer **does not exist this wave** (Design Spec §3.6, §12 — deferred until BE seeds Matching). Scaffold `test.skip` ("Matching renderer is a stub; real drag-pair deferred").

---

## Group F — Submit / advance flow (AC2, AC3)

### FE-TC-20 — Submitting an answer transitions to feedback and the progress reflects it
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** An answer selected on the current question (any type).
- **Steps:**
  1. Tap **Submit** (`quiz-submit`; fallback: the enabled `button` with the "Check answer" / "تحقّق" a11y label).
  2. Wait for `useSubmitAnswer` to resolve (a feedback strip — `alert` role — appears).
- **Expected:** After submit, the answer controls lock and a feedback strip (`alert`) appears (correct or incorrect variant). The Submit button either hides (correct → auto-advance) or shows "Next" (incorrect).
- **Traces to:** AC2/AC3 (answer controls + submit per type).

### FE-TC-21 — Incorrect answer shows "Next"; tapping it advances the quiz
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Submit an answer expected to be incorrect (e.g. deliberately wrong option), feedback strip = incorrect variant.
- **Steps:**
  1. After the incorrect feedback strip appears, locate the "Next" / "التالي" CTA and read the progress label.
  2. Tap Next.
  3. Read the new progress label.
- **Expected:** "Next" CTA visible + enabled on incorrect; tapping advances "Question n" → "Question n+1" (progressbar `aria-valuenow` increments). On the last question, Next completes the attempt → Summary.
- **Traces to:** AC2 (progress + advance), Design Spec §1 Surface 2.

### FE-TC-22 — Walk the full quiz to the Summary stage (flow completes → persistence proxy)
- **Type:** functional · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage at question 1.
- **Steps:**
  1. Answer + submit + advance through every question (use Next on incorrect; wait ≥800ms for auto-advance on correct).
  2. On the last question, advance / complete.
- **Expected:** After the last question, the **Summary** stage mounts (`region` role / `quiz-summary-card`) showing score / accuracy / time. Completing the flow is the FE proxy that the Attempt + StudentAnswers persisted (AC4 — full persistence asserted by api-tester).
- **Traces to:** AC2 (flow), AC4 (session persistence — FE proxy).

### FE-TC-23 — Auto-advance on correct answer (800ms) moves to the next question without a Next tap
- **Type:** state / boundary · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Submit a **correct** answer (requires knowing/forcing a correct option — may need a seeded known-answer question; otherwise mark conditional).
- **Steps:**
  1. Submit a correct answer → assert the correct feedback strip (`alert`, "Great job!" variant) appears and **no** "Next" button is shown.
  2. Wait > 800ms (use ~1500ms to be safe).
  3. Read the progress label.
- **Expected:** On correct, no Next CTA; after ~800ms the quiz auto-advances (progressbar increments) or, on the last question, the Summary mounts.
- **Traces to:** Design Spec §5 motion (800ms auto-advance), AC2.
- **Status:** **BLOCKED-soft** — needs a deterministic correct answer; if no known-answer seed, scaffold `test.skip` ("no deterministic correct answer to force auto-advance").

### FE-TC-24 — Controls lock after submit (non-interactive during feedback)
- **Type:** state / negative · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** An answer submitted, feedback strip showing.
- **Steps:**
  1. Attempt to re-tap / re-select an answer control (option / field).
  2. Read each option's `aria-disabled`.
- **Expected:** All answer controls are `aria-disabled=true` and non-interactive (`pointerEvents:none`) during feedback; re-tapping does not change selection.
- **Traces to:** Design Spec §1 Surface 2 (locked-after-submit).

---

## Group G — Responsive (AC3 mobile + desktop)

### FE-TC-25 — Quiz renders + is answerable at desktop width (≥1024)
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Desktop viewport (default harness width or set ≥1024).
- **Steps:** Enter quiz; assert the question card + controls render and an answer can be selected + submitted.
- **Expected:** Quiz fully usable at desktop width (content max-width 720, centered).
- **Traces to:** AC3 (mobile + desktop), FE-3 responsive.

### FE-TC-26 — Quiz renders + is answerable at narrow (mobile-web) width (~390)
- **Type:** functional · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Set viewport to ~390×844.
- **Steps:** Enter quiz; assert the question card + controls render without overflow and an answer can be selected.
- **Expected:** Layout adapts to narrow width; controls remain tappable (≥48px); no clipped content.
- **Traces to:** AC3 (mobile + desktop).

---

## Group H — Hearts / lives (kid-UX)

### FE-TC-27 — Hearts indicator renders (static 3 this wave)
- **Type:** functional / a11y · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage.
- **Steps:**
  1. Locate the Hearts widget in the TopBar (`quiz-hearts`; fallback: the element with the hearts a11y label "3 of 3 hearts" / "٣ قلوب").
- **Expected:** A hearts/lives indicator is present showing 3 (static this wave — decrement is Phase 3/4). No crash; a11y label present and not a raw key.
- **Traces to:** Design Spec §1 (Hearts widget, static count=3), kid-UX (lives indicator).

---

## Group I — RTL vs LTR per question type (Design Spec §6)

### FE-TC-28 — MCQ mirrors in Arabic RTL vs English LTR
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Reach an MCQ question in **Arabic** (default), then again in **English** (switch locale via the parent/settings or login locale switch before child login — note Arabic is default).
- **Steps:**
  1. In AR: assert `document.documentElement.dir === 'rtl'`; the MCQ option row reads right-to-left (radio disc on the reading-leading/right edge) — assert computed `direction: rtl` on an option's text, and the option label `aria-label` is the Arabic option text/state.
  2. In EN: assert `html[dir]==='ltr'` and the MCQ option reads left-to-right.
- **Expected:** MCQ layout mirrors with direction; labels localized; no raw keys. (Logical `marginStart`/`marginEnd` flip per Design Spec §6.)
- **Traces to:** Design Spec §6 (RTL), AC3 across locales.

### FE-TC-29 — True/False mirrors in RTL vs LTR
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** A True/False question, AR then EN.
- **Steps:**
  1. AR: assert `html[dir]==='rtl'`; the two sides use `row-reverse` (True reads from the reading-leading side); labels "صحيح"/"خطأ".
  2. EN: `ltr`; labels "True"/"False".
- **Expected:** Pair order mirrors with direction; labels localized.
- **Traces to:** Design Spec §6, §2.2.

### FE-TC-30 — FillInBlank mirrors text alignment + writing direction
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** A FillInBlank question, AR then EN.
- **Steps:**
  1. AR: assert the input's computed `text-align: right` and `direction: rtl`; placeholder is the Arabic "اكتب إجابتك".
  2. EN: `text-align: left`, `ltr`, placeholder "Type your answer".
- **Expected:** Field alignment + writing direction + placeholder follow the locale.
- **Traces to:** Design Spec §3.4 / §6.

---

## Group J — i18n, loading, error, empty

### FE-TC-31 — No raw i18n keys anywhere on the quiz stage
- **Type:** RTL-i18n · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage (any type).
- **Steps:**
  1. Read the visible text of the question card, progress label, Submit/Next button, hint helper, and (after submit) the feedback strip.
  2. Assert none match raw-key patterns (`child.`, `quiz.`, `feedback.`, `lessons.`, `summary.`).
- **Expected:** All UI copy is resolved localized text (AR by default), never a raw key like `child.quiz.submit`.
- **Traces to:** i18n correctness (no raw keys), kid-UX voice.

### FE-TC-32 — Submit network error surfaces an inline localized strip and preserves the answer
- **Type:** state / negative · **Priority:** P1 · **Target:** `frontend-e2e-tester`
- **Preconditions:** In the quiz stage with an answer selected. Intercept `POST .../answers` (or the submit endpoint) and force a 500.
- **Steps:**
  1. Select an answer.
  2. Route-intercept the submit-answer request → 500.
  3. Tap Submit.
- **Expected:** An inline error strip appears inside the question card with localized copy ("Couldn't check your answer — try again" / "تعذر التحقق — حاول مرة أخرى"), the Submit button returns to enabled "Check answer", and the **selection is preserved**.
- **Traces to:** Design Spec §1 Surface 2 (network-error state).

### FE-TC-33 — Lesson load error / 404 on the Intro stage shows a localized fallback (does not enter quiz)
- **Type:** state · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Navigate to a non-existent / failing lesson id (intercept the lesson GET → 404 or 500).
- **Steps:**
  1. Open `/(child)/lessons/{badId}?subjectId={sid}`.
  2. Observe the Intro stage.
- **Expected:** On 404 → "Lesson not found" / "الدرس غير موجود" + Back CTA; on other error → "Couldn't load lesson. Try again" + Try again + Back. The screen does **not** enter the quiz stage. Copy is localized (no raw keys).
- **Traces to:** Design Spec §1 Surface 1 (error / 404 states).

### FE-TC-34 — Empty lesson (Start resolves with 0 questions) shows the empty-state tile, not the quiz
- **Type:** state / boundary · **Priority:** P2 · **Target:** `frontend-e2e-tester`
- **Preconditions:** Intercept the Start-Attempt response to return `questions: []` (defensive path).
- **Steps:**
  1. On Intro, tap Start with the intercept forcing an empty question list.
- **Expected:** The Intro hero is replaced by the empty-state tile (📭 + "This lesson has no quiz yet" / "لا توجد أسئلة في هذا الدرس بعد" + Back CTA). The screen does **not** transition to the quiz stage.
- **Traces to:** Design Spec §1 Surface 1 (empty-lesson state), §11.12.
- **Status:** Testable via route intercept; if the intercept can't reshape the response in the harness, downgrade to `test.skip` ("cannot force empty Start-Attempt response").

---

## Summary table

| ID | Title | Type | Pri | Status |
|---|---|---|---|---|
| FE-TC-01 | Start → attempt + quiz stage | functional | P0 | Testable |
| FE-TC-02 | ProgressDots count | functional | P1 | Testable |
| FE-TC-03 | Question card stem + controls (one-focus) | functional/a11y | P0 | Testable |
| FE-TC-04 | No Skip / no confetti | negative | P2 | Testable |
| FE-TC-05 | MCQ renders option list | functional | P0 | Testable* |
| FE-TC-06 | MCQ accepts selection | functional | P0 | Testable* |
| FE-TC-07 | MCQ instant selection state | state | P0 | Testable* |
| FE-TC-08 | MCQ option a11y + target size | a11y | P1 | Testable* |
| FE-TC-09 | True/False renders pair | functional | P0 | Testable* |
| FE-TC-10 | True/False toggles | functional | P0 | Testable* |
| FE-TC-11 | True/False instant chrome | state | P1 | Testable* |
| FE-TC-12 | FillInBlank renders input | functional | P0 | Testable* |
| FE-TC-13 | FillInBlank accepts typing | functional | P0 | Testable* |
| FE-TC-14 | FillInBlank whitespace → Submit disabled | validation | P1 | Testable* |
| FE-TC-15 | FillInBlank empty → Submit disabled | boundary | P2 | Testable* |
| FE-TC-16 | Matching stub tile renders | functional | P1 | **BLOCKED** (no Matching seed) |
| FE-TC-17 | Matching stub → Next + empty payload | functional | P1 | **BLOCKED** (no Matching seed) |
| FE-TC-18 | Real Matching drag-pair | functional | P2 | **BLOCKED** (stub only) |
| FE-TC-20 | Submit → feedback + lock | functional | P0 | Testable |
| FE-TC-21 | Incorrect → Next advances | functional | P0 | Testable |
| FE-TC-22 | Full walk → Summary (persistence proxy) | functional | P0 | Testable |
| FE-TC-23 | Correct → 800ms auto-advance | state/boundary | P1 | **BLOCKED-soft** (needs known-correct answer) |
| FE-TC-24 | Controls lock after submit | state/negative | P1 | Testable |
| FE-TC-25 | Desktop responsive | functional | P1 | Testable |
| FE-TC-26 | Mobile-width responsive | functional | P1 | Testable |
| FE-TC-27 | Hearts indicator (static 3) | functional/a11y | P2 | Testable |
| FE-TC-28 | MCQ RTL vs LTR | RTL-i18n | P1 | Testable* |
| FE-TC-29 | True/False RTL vs LTR | RTL-i18n | P1 | Testable* |
| FE-TC-30 | FillInBlank RTL alignment | RTL-i18n | P1 | Testable* |
| FE-TC-31 | No raw i18n keys | RTL-i18n | P0 | Testable |
| FE-TC-32 | Submit network error strip | state/negative | P1 | Testable |
| FE-TC-33 | Lesson load error / 404 | state | P2 | Testable |
| FE-TC-34 | Empty lesson → empty tile | state/boundary | P2 | Testable (intercept) |

\* **Testable but type-conditional** — depends on reaching a question of that specific type. Without a
deterministic all-4-types seed (README OQ2), implement these to assert *if/when* the relevant type is the
current question, and `test.skip` with "type X not reachable in seed" if it never appears in a run.
