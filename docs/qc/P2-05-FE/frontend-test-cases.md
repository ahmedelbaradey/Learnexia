# Frontend Test Cases — P2-05-FE (Open & complete a lesson, web PWA)

> Target agent: **`frontend-e2e-tester`** (Playwright, Expo web at `:8081`, backend at `:5080`).
> Implement 1:1 as `tests/e2e/specs/P2-05-FE.spec.ts`. **Selector rule:** `getByTestId` first; then `getByRole`/`getByLabel`; **never** by visible Arabic/English copy (Arabic is the default locale). Where a needed `testID` is missing, the case is marked **BLOCKED — needs testID `<name>`**: write the test as `test.fixme(...)` with the reason; do not fake a pass. Report every needed hook back to `frontend`.
> Scope = lesson CONTENT + open/complete flow. Quiz question-type internals (P2-06-FE) and instant-feedback chrome (P2-07-FE) are out of scope here except as the "next step" boundary.

## Shared preconditions / helpers

- **Backend** seeded (`LearningSeeder` — Grade 1 has Math/Science/Arabic/English with units + lessons). Backend up at `:5080`, Expo web at `:8081`.
- **`seedChildAndSignIn(page, { learningLanguage })`** (compose from the existing P1-03/P1-09 helpers): register parent → `add-child` (Grade 1, `learningLanguage`, app language matching) → sign in as that child → land on child home (`dashboard-header`). Returns `{ childEmail, childPassword }`.
- **`openFirstAvailableLesson(page)`**: from child home → Subjects → open a subject → tap the first **Available** lesson card → assert URL matches `/(child)/lessons/\d+\?subjectId=\d+`. Returns `{ lessonId, subjectId }` parsed from the URL. (If `api-tester`/lead supplies a known `(lessonId, subjectId)` per OQ-2, prefer deep-linking `page.goto('/(child)/lessons/{id}?subjectId={sid}')` for determinism.)
- **Locale:** default run is **Arabic (RTL)**. The English variant signs in as a child whose `learningLanguage`/app language is `en` (no in-lesson toggle exists — OQ-4).
- **Fault injection** for error/404/network cases uses `page.route('**/api/learning/Lessons/**', ...)` / `page.route('**/Attempt**', ...)` to return 404/500/abort (OQ-6). If route-mock is disallowed, those cases become BLOCKED.

---

## Group A — Auth / reachability

### FE-TC-04 — Signed-out + parent cannot reach the lesson route
- **Type:** auth-authz · **Priority:** P0 · **Target:** `frontend-e2e-tester`
- **Preconditions:** (a) no session; (b) a parent session with children.
- **Steps:**
  1. With no session, `page.goto('/(child)/lessons/1?subjectId=1')`.
  2. Assert redirected away to Login (`login-username` visible) — not the lesson.
  3. Sign in as a **parent** (with children), then `page.goto('/(child)/lessons/1?subjectId=1')`.
  4. Assert redirected to the parent surface (not the child lesson player).
- **Expected:** group guard (`useGroupGuard` on `(child)/_layout`) blocks both; the lesson player never renders for a non-child.
- **Traces to:** AC18.
- **Status:** drivable now (URL + `login-username`/parent-surface testIDs from Phase-1 exist).

---

## Group B — Open a lesson + content renders (Intro stage, Surface 1)

### FE-TC-01 — Open a lesson → intro renders title + Start CTA
- **Type:** functional · **Priority:** P0
- **Preconditions:** `seedChildAndSignIn(ar)`; `openFirstAvailableLesson`.
- **Steps:** 1. Land on the lesson. 2. Wait for `lesson-intro-card`. 3. Assert `lesson-title` non-empty and `lesson-start-cta` visible + enabled.
- **Expected:** Intro hero card with eyebrow ("درس"), lesson name, and a full-width "ابدأ الدرس" Start CTA (one primary action).
- **Traces to:** AC1, AC2.
- **Status:** **BLOCKED — needs testIDs `lesson-intro-card`, `lesson-title`, `lesson-start-cta`** (OQ-1).

### FE-TC-02 — Explanation (AI-tutor bubble) renders, fallback when null
- **Type:** functional · **Priority:** P1
- **Preconditions:** as FE-TC-01.
- **Steps:** 1. On intro, assert `lesson-explanation` is present and shows text (seeded static explanation). 2. (Fault variant) route-mock the lesson GET to return `explanation: null`; reload; assert `lesson-explanation` shows the fallback string (i18n `child.lessons.intro.aiBubbleFallback`), not a raw key, not empty.
- **Expected:** explanation bubble renders seeded text; when null, the localized fallback shows.
- **Traces to:** AC1, AC2.
- **Status:** **BLOCKED — needs testID `lesson-explanation`** (OQ-1); variant 2 also needs route-mock (OQ-6).

### FE-TC-03 — Visual block renders when present, omitted when null
- **Type:** state · **Priority:** P2
- **Preconditions:** as FE-TC-01.
- **Steps:** 1. On a lesson whose seed `visual !== null`, assert `lesson-visual` present. 2. Route-mock the lesson GET with `visual: null`; reload; assert `lesson-visual` is absent (no empty grey block).
- **Expected:** visual placeholder shows only when `visual` is non-null.
- **Traces to:** AC1.
- **Status:** **BLOCKED — needs testID `lesson-visual`** (OQ-1) + route-mock for variant 2 (OQ-6).

### FE-TC-25 — Hearts widget present in TopBar across stages
- **Type:** functional / a11y · **Priority:** P2
- **Preconditions:** `seedChildAndSignIn(ar)`; open lesson.
- **Steps:** 1. On intro, assert a Hearts element is present (by `accessibilityLabel` `٣ قلوب` / `3 of 3 hearts`, role/aria-label — Hearts has no testID, use the a11y label). 2. Start → on the quiz stage, assert Hearts still present.
- **Expected:** static 3/3 hearts shown in the TopBar (count is hardcoded this wave — OQ-5); a11y label localized.
- **Traces to:** AC1.
- **Status:** drivable now via `getByLabel` (a11y label). (Optional: request a `lesson-hearts` testID for robustness.)

### FE-TC-13 — Loading state while lesson GET in flight
- **Type:** state (loading) · **Priority:** P1
- **Preconditions:** `seedChildAndSignIn(ar)`.
- **Steps:** 1. Route-delay the lesson GET (`page.route` with a setTimeout before fulfilling). 2. `goto` the lesson. 3. Assert `lesson-loading` shimmer visible before content. 4. Release the response; assert intro renders.
- **Expected:** single shimmer card placeholder, then the hero card.
- **Traces to:** AC7.
- **Status:** **BLOCKED — needs testID `lesson-loading`** (OQ-1) + route-delay (OQ-6).

### FE-TC-14 — Error state when lesson GET fails (non-404)
- **Type:** state (error) / negative · **Priority:** P0
- **Preconditions:** `seedChildAndSignIn(ar)`.
- **Steps:** 1. Route-mock lesson GET → 500. 2. `goto` the lesson. 3. Assert `lesson-error` text (localized "couldn't load", not a raw key). 4. Assert `lesson-error-retry` + `lesson-back` (ghost "back to lessons") present.
- **Expected:** centered error + "Try again" + "Back to lessons", localized.
- **Traces to:** AC8, AC14.
- **Status:** **BLOCKED — needs testIDs `lesson-error`, `lesson-error-retry`, `lesson-back`** (OQ-1) + route-mock (OQ-6).

### FE-TC-15 — Retry on error refetches and recovers
- **Type:** functional · **Priority:** P1
- **Preconditions:** as FE-TC-14.
- **Steps:** 1. With lesson GET mocked to 500, reach the error state. 2. Remove the route mock (or flip it to fulfill with healthy data). 3. Tap `lesson-error-retry`. 4. Assert intro renders (`lesson-intro-card`).
- **Expected:** retry triggers `lessonQuery.refetch()`; on success the intro appears.
- **Traces to:** AC8.
- **Status:** **BLOCKED — needs `lesson-error-retry`, `lesson-intro-card`** (OQ-1) + route-mock (OQ-6).

### FE-TC-16 — 404 lesson not found
- **Type:** negative · **Priority:** P1
- **Preconditions:** `seedChildAndSignIn(ar)`.
- **Steps:** 1. Route-mock lesson GET → 404 (or `goto` a non-existent lessonId if the BE returns 404). 2. Assert `lesson-404` localized "الدرس غير موجود" (no raw key). 3. Assert a back CTA present; no Retry button.
- **Expected:** "Lesson not found" + back; the screen distinguishes 404 from generic error (no retry on 404).
- **Traces to:** AC9.
- **Status:** **BLOCKED — needs testID `lesson-404`** (OQ-1); note the screen detects 404 by string-matching the error message — confirm the api-client surfaces a `statusCode`/message the screen recognizes.

---

## Group C — Progress through the lesson (Intro → Quiz, Surface 2)

### FE-TC-05 — Start CTA transitions intro → quiz stage
- **Type:** functional · **Priority:** P0
- **Preconditions:** open a lesson with ≥1 question.
- **Steps:** 1. Tap `lesson-start-cta`. 2. Assert the Start CTA shows a loading state while `useStartAttempt` is pending. 3. Assert `quiz-stage` appears with `quiz-progress-label` reading "Question 1 of N" (AR Eastern numerals `١ من ن`).
- **Expected:** intro replaced by the quiz stage; progress label + dots reflect index 1.
- **Traces to:** AC3.
- **Status:** **BLOCKED — needs testIDs `lesson-start-cta`, `quiz-stage`, `quiz-progress-label`** (OQ-1).

### FE-TC-06 — Progress label + dots advance question-by-question
- **Type:** functional · **Priority:** P1
- **Preconditions:** a lesson with ≥2 questions; in quiz stage.
- **Steps:** 1. Answer Q1 correctly (auto-advance) OR incorrectly then tap `quiz-next-cta`. 2. Assert `quiz-progress-label` advances to "2 of N" and `quiz-progress-dots` `aria-valuenow` increments.
- **Expected:** the progress indicator tracks the current question; ProgressDots `accessibilityValue.now` updates.
- **Traces to:** AC3.
- **Status:** **BLOCKED — needs `quiz-progress-label`, `quiz-next-cta`, and pass `testID` to `ProgressDots`** (OQ-1). Drivable in part via `getByRole('progressbar')` + `aria-valuenow`.

### FE-TC-12 — One primary action per stage (Submit/Next exclusivity)
- **Type:** state / kid-UX · **Priority:** P1
- **Preconditions:** in quiz stage (MCQ question).
- **Steps:** 1. Before selecting, assert `quiz-submit-cta` is disabled (`aria-disabled`). 2. Select an option; assert `quiz-submit-cta` enabled. 3. Submit an **incorrect** answer; assert the Submit CTA becomes `quiz-next-cta` (label "Next") and is enabled. 4. Submit a **correct** answer (fresh question); assert no Next CTA is shown (auto-advance handles it).
- **Expected:** exactly one primary CTA at a time; correct → auto-advance (no CTA), incorrect → explicit Next.
- **Traces to:** AC3, AC15.
- **Status:** **BLOCKED — needs `quiz-submit-cta` / `quiz-next-cta` with `aria-disabled`** (OQ-1).

---

## Group D — Complete the lesson + correct next step (Summary, Surface 3 + node state)

### FE-TC-07 — Complete the last question → summary stage appears
- **Type:** functional · **Priority:** P0
- **Preconditions:** open + start a lesson; answer all questions through to the last.
- **Steps:** 1. Walk every question to completion (answer each; the last advance triggers `useCompleteAttempt`). 2. Assert `lesson-summary-card` appears. 3. Assert `summary-score`, `summary-accuracy`, `summary-duration` render numbers (AR Eastern numerals; duration wrapped LTR `٤٥ث`). 4. Assert `summary-back-cta` + `summary-retry-cta` present (one primary "Back to lessons").
- **Expected:** completion state (trophy + score row + XP stub + CTAs); localized title "اكتمل الدرس!".
- **Traces to:** AC4, AC15.
- **Status:** **BLOCKED — needs `lesson-summary-card` (pass to `AttemptSummaryCard`), `summary-score/accuracy/duration`, `summary-back-cta`, `summary-retry-cta`** (OQ-1).

### FE-TC-08 — Completing the lesson records progress (node now Completed on return)
- **Type:** persistence · **Priority:** P0
- **Preconditions:** open + complete a lesson from a known subject; capture the lesson name/id.
- **Steps:** 1. Complete the lesson (reach summary). 2. Tap `summary-back-cta` → returns to the subject lessons tab. 3. Re-locate the same lesson card. 4. Assert its state is **Completed** (LessonCard `accessibilityLabel` includes the completed tag / `tagCompleted`).
- **Expected:** the completed lesson reflects Completed state — proves the `subjectLessons` cache invalidation + recorded progress (Story AC3).
- **Traces to:** AC4, AC5.
- **Status:** partially drivable — return-navigation + LessonCard a11y label is reachable; entering the quiz to *complete* needs the quiz testIDs (**BLOCKED on `summary-back-cta` + quiz CTAs**, OQ-1).

### FE-TC-09 — Completion invalidates the dashboard "Continue" target
- **Type:** persistence / regression · **Priority:** P2
- **Preconditions:** complete a lesson.
- **Steps:** 1. Complete a lesson. 2. Navigate to child home (`dashboard-header`). 3. Assert the `continue-card`, if present, no longer points at the just-completed lesson (or advances to the next node).
- **Expected:** dashboard cache invalidation on complete is observable (best-effort; tolerate "continue-card hidden" if the seed has no next node).
- **Traces to:** AC5.
- **Status:** drivable now for navigation/`continue-card` (testID exists) — but depends on completing the lesson first (**BLOCKED on quiz CTAs**, OQ-1).

### FE-TC-10 — Return to tree after complete shows the node not-locked / advanced
- **Type:** persistence · **Priority:** P2
- **Preconditions:** complete a lesson that unlocks a successor.
- **Steps:** 1. Complete the lesson. 2. Back to subject (lessons tab or skill tree). 3. Assert the next dependent lesson/node is no longer Locked (its prerequisite is now satisfied), where the seed defines a successor.
- **Expected:** completing a prerequisite unlocks the next node (skill-tree progression reflects the recorded completion).
- **Traces to:** AC5.
- **Status:** **BLOCKED on completing the lesson (quiz CTAs)** + depends on seed having a successor node (OQ-2).

### FE-TC-27 — Summary "Try again" re-creates a fresh attempt
- **Type:** functional · **Priority:** P1
- **Preconditions:** on the summary stage.
- **Steps:** 1. Tap `summary-retry-cta`. 2. Assert the stage resets to intro then re-enters the quiz at "Question 1 of N" (a NEW attempt — `useStartAttempt` re-fired). 3. Assert no stale feedback/answer from the prior run is shown.
- **Expected:** "Try again" resets all stage state and starts a fresh attempt.
- **Traces to:** AC16.
- **Status:** **BLOCKED — needs `summary-retry-cta`, `quiz-progress-label`** (OQ-1).

### FE-TC-28 — Summary "Back to lessons" navigates to the subject
- **Type:** functional · **Priority:** P1
- **Preconditions:** on the summary stage, reached via `?subjectId={sid}`.
- **Steps:** 1. Tap `summary-back-cta`. 2. Assert URL is `/(child)/subjects/{sid}` (router.replace). 3. Assert the back-stack does not allow returning into the completed attempt (replace, not push).
- **Expected:** primary CTA replaces to the subject lessons tab; no attempt re-entry on browser back.
- **Traces to:** AC17.
- **Status:** **BLOCKED — needs `summary-back-cta`** (OQ-1); URL assertion drivable once the CTA is tappable.

---

## Group E — Resume an in-progress lesson

### FE-TC-11 — Resume an in-progress lesson
- **Type:** functional · **Priority:** P1
- **Preconditions:** start a lesson, answer ≥1 question, exit mid-quiz (so an attempt exists in-progress server-side), then re-open the same lesson.
- **Steps:** 1. Open lesson, Start, answer Q1, exit via `lesson-back` (abandon fires). 2. Re-open the same lesson. 3. Assert the expected resume behavior.
- **Expected (DESIGN-DEPENDENT — see OQ-3):** intended behavior is undefined in the spec/task. Candidate assertions: (a) intro shows a "Continue" affordance, OR (b) opening resumes at the first unanswered question. **The current build does neither** — it always shows intro fresh and re-fires `useStartAttempt` (which creates/resumes server-side but restarts the FE walk at index 0). Plus the prior exit fired `useAbandonAttempt`, terminating that attempt.
- **Traces to:** AC6.
- **Status:** **BLOCKED — feature absent.** No FE resume surface exists and the design spec has no resume state. Requires a lead decision (OQ-3) on scope + intended UX before this case can be made assertable. Do not pass it against the current restart-from-intro behavior.

---

## Group F — Back / exit mid-lesson (abandon)

### FE-TC-18 — Back mid-quiz fires abandon; node NOT marked completed
- **Type:** functional / persistence · **Priority:** P0
- **Preconditions:** open + start a lesson (in quiz stage), capture the lesson identity.
- **Steps:** 1. In the quiz stage, tap `lesson-back` (back chevron) → leaves the screen (`router.back()`; cleanup `useEffect` fires `useAbandonAttempt`). 2. Optionally assert the abandon request fired (observe `**/Abandon**` via `page.waitForRequest`, or just assert navigation away). 3. Return to the subject lessons tab. 4. Assert the lesson is NOT Completed (still Available/in-progress, never `tagCompleted`).
- **Expected:** exiting mid-lesson abandons the attempt and does not record completion — the node must not flip to Completed.
- **Traces to:** AC11.
- **Status:** **BLOCKED — needs `lesson-back` testID** (OQ-1); the `**/Abandon**` request assertion is drivable via `waitForRequest` once you can reach the quiz stage.

### FE-TC-19 — Browser/web back mid-quiz also abandons (no orphaned completed state)
- **Type:** regression / negative · **Priority:** P1
- **Preconditions:** as FE-TC-18.
- **Steps:** 1. In the quiz stage, use `page.goBack()` (browser back) instead of the chevron. 2. Return to the lessons tab. 3. Assert the lesson is NOT Completed.
- **Expected:** the unmount-cleanup abandon path is robust to web back-button, not only the in-screen chevron.
- **Traces to:** AC11.
- **Status:** **BLOCKED — needs reaching the quiz stage (quiz testIDs)** + lesson-card a11y on return (OQ-1). Note: validates that the cleanup `useEffect` actually runs on web back (a known regression-prone path — R3).

---

## Group G — Empty / network-fault on Start

### FE-TC-17 — Empty lesson (Start resolves with 0 questions) → empty state, no quiz
- **Type:** state (empty) / boundary · **Priority:** P1
- **Preconditions:** a lesson whose StartAttempt returns `questions: []` (route-mock `**/Attempt**` to return an empty questions array, or use a seeded empty lesson if one exists).
- **Steps:** 1. Open the lesson, tap `lesson-start-cta`. 2. Assert `lesson-empty` shows the localized "no questions yet" copy (📭, `child.lessons.intro.noQuestions`), NOT a quiz stage. 3. Assert `lesson-empty-back` ghost CTA present.
- **Expected:** the screen stays on intro and shows the empty tile; it does NOT transition to the quiz stage.
- **Traces to:** AC10.
- **Status:** **BLOCKED — needs `lesson-empty`, `lesson-empty-back`** (OQ-1) + route-mock for the empty `questions` array (OQ-6).

### FE-TC-20 — Network error on Start → intro stays, recoverable
- **Type:** negative · **Priority:** P1
- **Preconditions:** open a lesson.
- **Steps:** 1. Route-mock `**/Attempt**` (StartAttempt) → 500/abort. 2. Tap `lesson-start-cta`. 3. Assert the screen stays on intro (no quiz stage), the Start CTA returns to enabled (no permanent spinner). 4. Remove the mock; tap Start again; assert the quiz stage now appears.
- **Expected:** a failed Start does not strand the user; Start is retryable.
- **Traces to:** AC12.
- **Status:** **BLOCKED — needs `lesson-start-cta`, `quiz-stage`** (OQ-1) + route-mock (OQ-6). Note: the screen has no explicit Start-error toast; assert via "still intro + CTA enabled" rather than an error message.

---

## Group H — RTL / i18n / kid-UX

### FE-TC-21 — Arabic (default) → RTL across intro → quiz → summary
- **Type:** RTL-i18n · **Priority:** P0
- **Preconditions:** `seedChildAndSignIn(ar)`; open + complete a lesson.
- **Steps:** 1. Assert `document.dir === 'rtl'` (or `html[dir=rtl]`) on the lesson route. 2. On intro, assert the back chevron glyph is `›` (RTL form). 3. On quiz, assert the progress label uses Eastern-Arabic numerals (e.g. contains `١`/`٢`/`من`). 4. On summary, assert score numbers are Eastern-Arabic and the duration is wrapped LTR (e.g. `٤٥ث` reads with the unit trailing).
- **Expected:** RTL layout + Arabic numerals throughout; chevron mirrored; duration unit stays readable.
- **Traces to:** AC13.
- **Status:** partially drivable (`html[dir]`, chevron glyph via the back element). Numeral assertions on progress/summary **BLOCKED on `quiz-progress-label`/`summary-*` testIDs** (OQ-1).

### FE-TC-22 — English child → LTR, Western numerals
- **Type:** RTL-i18n · **Priority:** P1
- **Preconditions:** `seedChildAndSignIn(en)`; open the lesson.
- **Steps:** 1. Assert `document.dir === 'ltr'`. 2. Assert back chevron glyph is `‹`. 3. On quiz, assert progress label uses Western numerals ("Question 1 of N"). 4. Assert layout is `row` (not row-reverse) — TopBar back on the leading (left) edge.
- **Expected:** LTR mirror of FE-TC-21.
- **Traces to:** AC13.
- **Status:** partially drivable (dir + chevron); label assertion **BLOCKED on `quiz-progress-label`** (OQ-1).

### FE-TC-23 — No raw i18n keys leak (Arabic) across all stages + states
- **Type:** RTL-i18n / a11y · **Priority:** P0
- **Preconditions:** `seedChildAndSignIn(ar)`.
- **Steps:** 1. Visit intro, quiz, summary (complete a lesson), plus the error/404/empty states (route-mocked). 2. On each, assert the visible text contains **no** raw key fragments (regex: no occurrence of `child.lessons.`, `child.quiz.`, `child.summary.`, `child.feedback.`, or `common.` in `page.content()` / rendered text).
- **Expected:** every string resolves to localized copy; no `child.xxx.yyy` placeholder ever renders.
- **Traces to:** AC14.
- **Status:** the regex sweep is drivable on whatever stages are reachable, but full coverage of error/empty/404 stages **depends on route-mock + the relevant testIDs** to reach them (OQ-1/OQ-6). Run it on every stage you can reach.

### FE-TC-24 — No raw i18n keys leak (English)
- **Type:** RTL-i18n · **Priority:** P1
- **Preconditions:** `seedChildAndSignIn(en)`.
- **Steps:** same key-fragment regex sweep as FE-TC-23, English session.
- **Expected:** no raw keys in English either.
- **Traces to:** AC14.
- **Status:** as FE-TC-23.

### FE-TC-26 — Kid-UX: large tap targets + instant visual feedback on select
- **Type:** a11y / state · **Priority:** P2
- **Preconditions:** in quiz stage (MCQ).
- **Steps:** 1. Assert the primary CTA and each MCQ option have a rendered height ≥ 48px (boundingBox). 2. Tap an MCQ option; assert it immediately reflects a selected state (`aria-checked=true` on the option) before submit — instant visual feedback. 3. Assert the back chevron hit area is ≥ 44×44.
- **Expected:** targets meet the ≥48 floor (44 for the chevron hit area per spec); selection gives instant `aria-checked` feedback.
- **Traces to:** AC15.
- **Status:** options expose `aria-checked` (MCQOption a11y) and boundingBox is measurable — partly drivable, but reaching the quiz stage + identifying options reliably **needs the MCQ option testIDs / quiz-stage hook** (OQ-1).

---

## Coverage tally

| Group | Cases |
|---|---|
| A — auth/reachability | FE-TC-04 |
| B — open + content + states | FE-TC-01, 02, 03, 25, 13, 14, 15, 16 |
| C — progress through | FE-TC-05, 06, 12 |
| D — complete + next step | FE-TC-07, 08, 09, 10, 27, 28 |
| E — resume | FE-TC-11 |
| F — back/exit (abandon) | FE-TC-18, 19 |
| G — empty / start fault | FE-TC-17, 20 |
| H — RTL/i18n/kid-UX | FE-TC-21, 22, 23, 24, 26 |
| **Total** | **28** |

**Priority split (authoritative — per-case `Priority:` tags):**
- **P0 (9):** FE-TC-01, 04, 05, 07, 08, 14, 18, 21, 23
- **P1 (14):** FE-TC-02, 06, 11, 12, 13, 15, 16, 17, 19, 20, 22, 24, 27, 28
- **P2 (5):** FE-TC-03, 09, 10, 25, 26

**Blocked summary:**
- BLOCKED on missing testIDs (OQ-1): the majority — every case whose Status names a missing `lesson-*`/`quiz-*`/`summary-*` hook.
- BLOCKED on feature absent (resume): FE-TC-11.
- Drivable now (no/optional testID): FE-TC-04 (auth), FE-TC-25 (a11y label), partial FE-TC-09/21/22/23/24 (URL/dir/regex portions).
