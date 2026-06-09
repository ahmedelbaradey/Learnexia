# Frontend E2E Test Cases — P2-07-FE (Instant answer feedback)

> **Target agent:** `frontend-e2e-tester` → implement as `tests/e2e/specs/P2-07-FE.spec.ts`.
> **Surface:** student-app web PWA, child surface, quiz player `app/(child)/lessons/[lessonId].tsx`.
> **Run prerequisites:** backend at `http://localhost:5080` (Postgres + seed) + Expo web at `:8081`
> (see `tests/e2e/README.md`). Arabic is the **default** locale (`dir=rtl`, `lang=ar`).
> **Each case → one Playwright test, 1:1.**

---

## 0. Shared selector strategy & harness notes

**Selectors, in priority order (per `tests/e2e/README.md`):**
1. `getByTestId(...)` — **preferred**. Requested testIDs are listed under each case as *(wanted)*. They
   do **not exist yet** in `[lessonId].tsx` (see README §4 OQ1) — report missing ones back to `frontend`.
2. Fallbacks that work **today**:
   - **Feedback strip:** `page.getByRole('alert')` — `AnswerFeedbackStrip` sets `accessibilityRole="alert"`
     + `aria-live="polite"` + `aria-label`. The `aria-label` is the title alone (correct) or
     `"{title}. {reveal}"` (incorrect). Assert variant via the label text (`/Great job|أحسنت/` vs
     `/Not quite|ليست الإجابة/`) or, if needed, via computed `border-*-color` (green `#22C55E` vs red `#EF4444`).
   - **Options:** `page.getByRole('radio')` — `MCQOption`/`TrueFalseChoice` set `accessibilityRole="radio"`
     and an `aria-label` that includes localized state after submit (`correct answer` / `الإجابة الصحيحة`,
     `incorrect` / `إجابة خاطئة`, `selected` / `مختار`).
   - **Submit / Next CTA:** `page.getByRole('button', { name: /Check answer|تحقّق/ })` and
     `{ name: /Next|التالي/ }`.
   - **Reveal text:** `page.getByText(/Correct answer:|الإجابة الصحيحة:/)`.

**Helpers (reuse from `P1-09-FE.spec.ts`):** `registerParent`, `addChildViaForm`, `signInViaUI`,
`selectOption`, `uniqueEmail`. Add a new helper `reachQuizPlayer(page)` that: signs in as a child →
opens the child home → navigates to a subject → taps the first **Available** lesson → taps **Start
lesson** (`getByRole('button', { name: /Start lesson|ابدأ الدرس/ })`) so the first question is on screen.

**Determinism lever (README §4 OQ2):** for cases that need a *known* verdict, intercept the grade call:
```ts
await page.route('**/Quizzes/*/Answers', (route) =>
  route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ successed: true, errors: [],
      data: { isCorrect: true, correctAnswer: null, hintAvailable: false } }) }));
```
Flip `isCorrect`/`correctAnswer` per case. Verdict-agnostic live-BE cases do **not** mock.

**Hermetic rule:** unique email per spec; no cross-test ordering. `test.setTimeout(120_000)` (long
onboarding). Always `ctx.close()` in `finally`.

---

## Group A — Correct-answer feedback (positive, encouraging)

### FE-TC-01 — Correct answer renders the positive feedback strip (live BE, verdict-agnostic)
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child account (register parent → add child → sign in). A subject with an
  Available lesson that has ≥1 question for the child's grade.
- **Steps:**
  1. `reachQuizPlayer(page)` → first question visible.
  2. Pick the first option (`getByRole('radio').first().click()`).
  3. Tap **Check answer** (`getByRole('button', { name: /Check answer|تحقّق/ })`).
  4. Wait for a `role="alert"` node to appear.
  5. Read its `aria-label`.
- **Expected:** a feedback strip (`getByRole('alert')`) appears within the same screen. If the
  `aria-label` matches `/Great job|أحسنت/` → it is the **correct** variant with **no** reveal text and
  no "Next" button (auto-advance). The strip's leading border colour is green (`#22C55E`). (If the
  picked option was graded wrong, this case's body branches to assert the incorrect variant instead —
  the assertion is "strip variant == response verdict".)
- **Traces to:** AC1.
- *(wanted testID: `feedback-strip`, `feedback-strip-correct`)*

### FE-TC-02 — Correct answer: chosen option turns green ✓ and auto-advances after ~800ms
- **Type:** functional / state · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** same as FE-TC-01, **mock** the grade call to force
  `{ isCorrect:true, correctAnswer:null }`.
- **Steps:**
  1. `reachQuizPlayer(page)`; record the progress label text ("Question 1 of N" / "السؤال ١ من N").
  2. Pick an option; record its text.
  3. Mock `**/Quizzes/*/Answers` → `isCorrect:true`.
  4. Tap **Check answer**.
  5. Assert the green strip (`getByRole('alert')`, label `/Great job|أحسنت/`) shows and **no** "Next"
     button is present.
  6. Wait ~1200ms.
- **Expected:** the chosen option shows the **correct** chrome (its `radio` `aria-label` contains
  `correct answer` / `الإجابة الصحيحة`); **no** "Next"/"التالي" button renders during correct feedback;
  after ~800ms the screen **auto-advances** — the progress label increments to "Question 2 of N"
  (or, if it was the last question, the Summary card appears — see FE-TC-15).
- **Traces to:** AC1, "advance after feedback".
- *(wanted testID: `feedback-strip-correct`, `quiz-progress`)*

### FE-TC-09 — Correct strip is encouraging and shows NO reveal text (kid-UX voice)
- **Type:** RTL-i18n / kid-UX · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child in **Arabic** (default); mock grade `isCorrect:true`.
- **Steps:** `reachQuizPlayer` → pick → mock → Check answer → read strip `aria-label` + visible text.
- **Expected:** title is the translated `child.feedback.correct` ("أحسنت!" in ar / "Great job!" in en) —
  **not** a raw key. The correct strip contains **no** "Correct answer:" reveal line (spec §11 OQ8 — the
  green ✓ already conveys it). Single exclamation, encouraging tone.
- **Traces to:** AC1, kid-UX (NFR-6), i18n.

### FE-TC-13 — MCQ correct feedback (per-type) renders green strip + green option
- **Type:** functional (per question type) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** an **MCQ** question on screen (default first type); mock grade `isCorrect:true`.
- **Steps:** `reachQuizPlayer` (confirm 2+ radio options = MCQ) → pick → mock correct → Check answer.
- **Expected:** green `role="alert"` strip; the picked `radio` is `correct`; the other options are
  non-interactive (`aria-disabled`); no "Next" button; auto-advance follows.
- **Traces to:** AC1, per-type feedback.

---

## Group B — Wrong-answer feedback (corrective, never punishing)

### FE-TC-03 — Wrong answer renders the corrective feedback strip with the correct answer revealed
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child; **mock** grade → `{ isCorrect:false, correctAnswer:"42" }`.
- **Steps:**
  1. `reachQuizPlayer`.
  2. Pick an option.
  3. Mock `**/Quizzes/*/Answers` → `isCorrect:false, correctAnswer:"42"`.
  4. Tap **Check answer**.
  5. Read the `role="alert"` strip.
- **Expected:** red feedback strip appears: title = `child.feedback.incorrect` ("ليست الإجابة الصحيحة"
  / "Not quite"), and a reveal line `child.feedback.correctAnswer` interpolated with `42` ("الإجابة
  الصحيحة: 42" / "Correct answer: 42"). Leading border colour is red (`#EF4444`). The strip's
  `aria-label` = `"{title}. {reveal}"`.
- **Traces to:** AC2.
- *(wanted testID: `feedback-strip-incorrect`, `feedback-reveal`)*

### FE-TC-04 — Wrong answer shows an explicit "Next" CTA and does NOT auto-advance
- **Type:** functional / state · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child; mock grade `isCorrect:false, correctAnswer:"x"`.
- **Steps:**
  1. `reachQuizPlayer`; record progress label.
  2. Pick → mock wrong → Check answer.
  3. Wait ~1500ms **without** acting.
  4. Assert progress label is **unchanged** (no auto-advance).
  5. Assert a **Next** button (`getByRole('button', { name: /Next|التالي/ })`) is visible.
  6. Tap **Next**.
- **Expected:** after a wrong answer the screen **waits** (no 800ms timer); the "Next" CTA is present
  and enabled; tapping it advances to the next question (progress label increments) or completes the
  attempt on the last question.
- **Traces to:** AC2, "advance after feedback".

### FE-TC-05 — Wrong answer: chosen option turns red ✕, correct option turns green ✓, others lock
- **Type:** functional / state · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** **MCQ** on screen; mock grade `isCorrect:false` with `correctAnswer` set to
  the **text of a different option** than the one the test will pick (read the option texts first, pick
  option[0], set `correctAnswer` = option[1] text).
- **Steps:** read 2 option texts → pick option[0] → mock wrong with `correctAnswer=option[1]` → Check answer.
- **Expected:** option[0] (the pick) shows `incorrect` chrome (its `radio` `aria-label` contains
  `incorrect` / `إجابة خاطئة`); option[1] (the correct answer) shows `correct` chrome
  (`correct answer` / `الإجابة الصحيحة`); every option is now non-interactive (`aria-disabled` /
  `pointer-events:none`). Tapping any option does nothing.
- **Traces to:** AC2.

### FE-TC-10 — Wrong-answer copy is soft/supportive and translated (kid-UX voice, ar + en)
- **Type:** RTL-i18n / kid-UX · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** run twice — child in **ar** then a separate child/locale in **en**; mock wrong.
- **Steps:** force wrong → read strip title + reveal.
- **Expected:** **ar** → "ليست الإجابة الصحيحة" (no exclamation), reveal "الإجابة الصحيحة: …". **en** →
  "Not quite" (no exclamation), reveal "Correct answer: …". No raw i18n keys
  (`/child\.feedback\.(in)?correct/` must NOT appear in body text). Tone is matter-of-fact, never
  punishing (no "Wrong!", no red ✕ scolding copy).
- **Traces to:** AC2, kid-UX (NFR-6), i18n.

### FE-TC-14 — TrueFalse wrong feedback marks the picked side red and the correct side green
- **Type:** functional (per question type) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** a **TrueFalse** question on screen (exactly 2 radio sides labelled
  True/False / صحيح/خطأ); mock grade `isCorrect:false, correctAnswer:"true"`.
- **Steps:** `reachQuizPlayer` until a True/False question (or BLOCK if none seeded for the grade — see
  note) → pick "False" → mock wrong with `correctAnswer:"true"` → Check answer.
- **Expected:** the "False" side is `incorrect` (red), the "True" side is `correct` (green ✓), red strip
  "Not quite" shows, "Next" CTA present. **If no TrueFalse question is reachable in the seed, mark this
  case BLOCKED** with that reason rather than forcing it.
- **Traces to:** AC2, per-type feedback.

---

## Group C — Same-screen, no-reload guarantee

### FE-TC-06 — Submitting an answer does NOT trigger a full page reload
- **Type:** functional / state · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child in quiz player.
- **Steps:**
  1. `reachQuizPlayer`.
  2. Set a sentinel on the window: `await page.evaluate(() => (window.__noReload = true))`.
  3. Pick an option → Check answer → wait for the feedback strip.
  4. Read the sentinel: `await page.evaluate(() => window.__noReload)`.
- **Expected:** the sentinel is still `true` (the document was never reloaded — feedback rendered via
  state transition). The URL is unchanged (`/lessons/{id}`). No navigation event fired.
- **Traces to:** AC3.

### FE-TC-07 — Feedback → next question happens in-place (URL unchanged across questions)
- **Type:** functional / state · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** lesson with ≥2 questions; mock correct so it auto-advances.
- **Steps:** record URL → answer Q1 (mock correct) → wait for auto-advance → record URL + progress label.
- **Expected:** progress label goes from "1 of N" to "2 of N"; the URL path is **identical** before and
  after (same `/lessons/{id}` route — the stage machine swaps content, not the route). No spinner/reload.
- **Traces to:** AC3.

### FE-TC-08 — A submit fires exactly one POST to the grade endpoint (per answer)
- **Type:** functional / persistence-adjacent · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child in quiz player.
- **Steps:**
  1. `reachQuizPlayer`.
  2. Count requests: `page.on('request', r => { if (/\/Quizzes\/.*\/Answers/.test(r.url())) hits++; })`.
  3. Pick → Check answer → wait for the feedback strip.
- **Expected:** exactly **one** `POST` to `**/Quizzes/{attemptId}/Answers` fires (the answer is sent
  to the BE so it can be recorded — FE side of AC4). Re-tapping options or the locked area fires no
  further POSTs. (Persistence itself is verified by P2-07-BE, not here.)
- **Traces to:** AC4 (FE responsibility = sending the answer), AC3.

---

## Group D — Verdict-matches-server contract (FE never grades)

### FE-TC-11 — Forced `isCorrect:true` always renders the correct variant (regardless of pick)
- **Type:** functional / negative-isolation · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child; **mock** grade → `{ isCorrect:true, correctAnswer:null }`.
- **Steps:** pick any option → mock correct → Check answer.
- **Expected:** the **correct** (green) strip renders, "Great job!"/"أحسنت!", no "Next", auto-advance —
  even though the picked option may genuinely be wrong. Proves the FE renders the **server** verdict,
  never its own comparison.
- **Traces to:** "matches server `isCorrect`".

### FE-TC-12 — Forced `isCorrect:false` always renders the incorrect variant + reveal
- **Type:** functional / negative-isolation · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** child; **mock** grade → `{ isCorrect:false, correctAnswer:"SERVER_SAYS" }`.
- **Steps:** pick any option → mock wrong → Check answer.
- **Expected:** the **incorrect** (red) strip renders with reveal "Correct answer: SERVER_SAYS" /
  "الإجابة الصحيحة: SERVER_SAYS", and a "Next" CTA — even if the picked option was actually correct.
  Confirms the reveal text is driven verbatim by `correctAnswer` from the response.
- **Traces to:** "matches server `isCorrect`", AC2.

---

## Group E — Per-question-type, boundary & negative

### FE-TC-15 — Auto-advance on the LAST correct question completes the attempt → Summary
- **Type:** boundary / state · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** a lesson with a known small number of questions; mock **every** grade
  `isCorrect:true` so the run flies through to the last question.
- **Steps:** answer each question correct (mock) until the progress label reads "N of N" → on the last
  one, after the ~800ms timer, observe the screen.
- **Expected:** after the last correct answer, the green strip shows for ~800ms, then the attempt
  **Completes** and the **AttemptSummaryCard** mounts ("Lesson complete!" / "اكتمل الدرس!" with a
  score/accuracy/time row). No leftover feedback strip; no extra question. (Validates spec §11 OQ7.)
- **Traces to:** AC1, AC3, advance/complete boundary.

### FE-TC-16 — FillInBlank wrong feedback marks the field red and reveals the answer
- **Type:** functional (per question type) · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** a **FillInBlank** question on screen (a single text field, no radios); mock
  grade `isCorrect:false, correctAnswer:"Cairo"`.
- **Steps:** type a non-empty answer → mock wrong → Check answer.
- **Expected:** the field locks (non-editable), shows incorrect chrome (red border), the red strip
  "Not quite" shows with reveal "Correct answer: Cairo" (kept `dir=ltr` for a Latin answer per spec §6),
  and a "Next" CTA. **If no FillInBlank question is reachable in the seed, mark BLOCKED.**
- **Traces to:** AC2, per-type feedback.

### FE-TC-17 — FillInBlank correct feedback turns the field green and auto-advances
- **Type:** functional (per question type) · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** FillInBlank on screen; mock grade `isCorrect:true`.
- **Steps:** type an answer → mock correct → Check answer.
- **Expected:** field shows correct (green) chrome, the green "Great job!" strip shows (no reveal), no
  "Next", auto-advance after ~800ms. **BLOCKED if no FillInBlank reachable.**
- **Traces to:** AC1, per-type feedback.

### FE-TC-18 — Matching question (stub) submits empty + renders wrong feedback path — **BLOCKED**
- **Type:** functional (per question type) / negative · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED** — the backend seeds **zero** Matching questions (W12 spec §12; HANDOFF W12).
  The stub path (`MatchingPanel` + auto `""` payload → graded wrong → reveal) cannot be reached in E2E
  until BE seeds a Matching question. Document the blocker; do not fake.
- **Preconditions/seed (when unblocked):** a Matching question on screen.
- **Steps (when unblocked):** reach the Matching stub → tap "Next" (auto-submits `""`).
- **Expected (when unblocked):** the empty answer is graded incorrect; the red strip + reveal + "Next"
  render exactly like other wrong answers; no crash on the `""` payload.
- **Traces to:** per-type feedback (defensive).

### FE-TC-19 — Network failure during submit shows the inline error strip, NOT a feedback strip — **BLOCKED (partial)**
- **Type:** negative / state · **Priority:** P1 · **Agent:** `frontend-e2e-tester`
- **Status:** **PARTIALLY BLOCKED** — the network-error strip (`child.quiz.networkError`, rendered by
  `QuestionCard.errorMessage`) and the feedback strip are **both** `$dangerSoft` red with **no `testID`**,
  so cleanly distinguishing them needs a stable hook (README §4 OQ1). The implementable part: assert
  the answer selection is preserved, the Submit CTA returns to "Check answer", and no advance happens.
  Asserting it is the *error* strip (not the *incorrect-feedback* strip) is best-effort via the distinct
  copy `/Couldn't check your answer|تعذر التحقق/`.
- **Preconditions/seed:** child in quiz player.
- **Steps:**
  1. `reachQuizPlayer`; pick an option.
  2. `page.route('**/Quizzes/*/Answers', r => r.abort('failed'))`.
  3. Tap **Check answer**; wait.
  4. Assert the inline error copy `/Couldn't check your answer|تعذر التحقق/` is visible.
  5. Assert the picked option is **still selected** and the CTA reads **Check answer** again (re-enabled).
  6. `page.unroute(...)`, retap Check answer → a real feedback strip eventually shows.
- **Expected:** on a failed submit the screen reverts to `answering` (no feedback strip, no advance),
  shows the network-error copy, preserves the selection, and lets the user retry. The verdict feedback
  strip never appears for a failed request.
- **Traces to:** AC3 (resilience of same-screen feedback), negative path.

---

## Group F — i18n / a11y / kid-UX / deferred-feature guards

### FE-TC-20 — No raw i18n keys appear anywhere in the feedback flow (ar + en)
- **Type:** i18n · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** run on the feedback screen in both locales; force one correct + one wrong.
- **Steps:** after showing each strip, scan `document.body` text for
  `/^(child|common)\.[a-zA-Z.]+$/` and for `missingKey`.
- **Expected:** zero raw keys; the rendered copy is the translated value for `child.feedback.correct`,
  `child.feedback.incorrect`, `child.feedback.correctAnswer`, `child.quiz.next`, `child.quiz.submit`.
- **Traces to:** i18n, kid-UX.

### FE-TC-21 — Feedback strip announces to assistive tech (live region / role=alert) — **BLOCKED (partial)**
- **Type:** a11y · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **PARTIALLY BLOCKED** — Playwright cannot assert that a screen reader *spoke* the strip.
  The implementable part: assert the strip exposes `role="alert"` + `aria-live="polite"` + an
  `aria-label` carrying the title (+reveal). The actual SR announcement is manual/native QA only.
- **Steps:** force correct → `getByRole('alert')` exists with `aria-live="polite"` and an `aria-label`
  matching `/Great job|أحسنت/`. Force wrong → `aria-label` matches
  `/Not quite.*Correct answer|ليست.*الإجابة الصحيحة/`.
- **Expected:** the strip is exposed as a polite live alert with a meaningful label in both variants.
  (SR speech itself = BLOCKED, manual.)
- **Traces to:** kid-UX (NFR-6) a11y, design spec §3.5/§7.

### FE-TC-22 — Deferred-feature guard: NO confetti / NO live-XP toast on a correct answer (W12 scope)
- **Type:** regression / scope-guard · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Preconditions/seed:** force a correct answer; also force a wrong answer in the same or a sibling test.
- **Steps:** on the correct strip, scan for any "+XP" toast / confetti canvas; on the wrong strip, scan
  for any heart-decrement.
- **Expected:** during quiz feedback there is **no** confetti, **no** "+10 XP" toast, **no** Skia canvas
  overlay (those are W14/P4 — spec §10/§12). The "+10 XP (coming soon)" badge only appears on the
  **Summary** card, not on per-answer feedback. Hearts stay static at 3 on a wrong answer (no decrement
  this wave). This pins the deferral so it is traceable, not a silent gap.
- **Traces to:** AC1/AC2 deferral note (README §2).

### FE-TC-23 — Reduced-motion: correct auto-advance timer extends to ~1200ms — **BLOCKED**
- **Type:** a11y / motion · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **BLOCKED** — the reduced-motion gate (800ms→1200ms + fade-only) is **not yet wired**
  into `AnswerFeedbackStrip` / the lesson timer (HANDOFF W12 "still open"). Re-enable when
  `AccessibilityInfo.isReduceMotionEnabled()` is wired. (When unblocked: emulate
  `prefers-reduced-motion: reduce`, force correct, assert the auto-advance fires later than ~800ms and
  no translate animation occurs.)
- **Traces to:** kid-UX (NFR-6) reduced-motion, design spec §5/§7.

### FE-TC-24 — Hint affordance is visible-but-disabled during wrong feedback (W12 scope) — **BLOCKED (partial)**
- **Type:** scope-guard / a11y · **Priority:** P2 · **Agent:** `frontend-e2e-tester`
- **Status:** **PARTIALLY BLOCKED** — the AC mentions a "hint affordance" on wrong answers; W12 ships a
  **disabled** Hint button + "Hint coming in v2" helper (no endpoint). The implementable part: assert
  the Hint button is present, disabled, and labelled. The *active* hint behaviour is P3-05 and cannot
  be tested. Mark the active-hint portion BLOCKED.
- **Steps:** on a wrong-answer screen, find the Hint button
  (`getByRole('button', { name: /Hint|تلميح/ })`); assert it is disabled (`aria-disabled`) and that
  "Hint coming in v2" / "التلميح قريبًا" helper text is visible; assert tapping it does nothing
  (no network call).
- **Expected:** Hint button is reachable, disabled, honestly labelled; no hint endpoint is hit.
- **Traces to:** AC2 (hint affordance), spec §11 OQ8/OQ10.

---

## Coverage tally

| Group | Case IDs | P0 | P1 | P2 |
|---|---|---|---|---|
| A — Correct feedback | FE-TC-01, 02, 09, 13 | 2 | 2 | 0 |
| B — Wrong feedback | FE-TC-03, 04, 05, 10, 14 | 3 | 2 | 0 |
| C — No-reload / same-screen | FE-TC-06, 07, 08 | 1 | 2 | 0 |
| D — Verdict matches server | FE-TC-11, 12 | 2 | 0 | 0 |
| E — Per-type / boundary / negative | FE-TC-15, 16, 17, 18, 19 | 0 | 3 | 2 |
| F — i18n / a11y / scope-guard | FE-TC-20, 21, 22, 23, 24 | 0 | 0 | 5 |
| **Total — 24 cases** | | **8** | **9** | **7** |

**Per-priority roster (canonical):**
- **P0 (8):** FE-TC-01, 02, 03, 04, 05, 06, 11, 12.
- **P1 (9):** FE-TC-07, 08, 09, 10, 13, 14, 15, 16, 19.
- **P2 (7):** FE-TC-17, 18, 20, 21, 22, 23, 24.

**BLOCKED roster:**
- **Fully blocked:** FE-TC-18 (no Matching seed), FE-TC-23 (reduced-motion gate not wired).
- **Partially blocked (assertable part runs; noted part blocked):** FE-TC-19 (error-vs-feedback strip
  disambiguation needs a testID), FE-TC-21 (SR speech is manual), FE-TC-24 (active hint = P3-05).
- **Conditionally blocked at runtime:** FE-TC-14, FE-TC-16, FE-TC-17 — block only if the seed yields no
  TrueFalse / FillInBlank question for the test child's grade; document the seed reality when running.
