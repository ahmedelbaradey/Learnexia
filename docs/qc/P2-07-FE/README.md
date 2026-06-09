# QC Test Plan & Coverage Report — P2-07-FE (Instant answer feedback)

> **Story:** [P2-07 — Get instant answer feedback](../../../user-stories/Phase-2-Learning-Core/P2-07-instant-answer-feedback.md)
> **Design Spec:** [W12 — Lesson Player + Quiz + Instant Feedback](../../../design-system/ui_kits/student-mobile/W12-lesson-quiz.md) (§3.5 `AnswerFeedbackStrip`, §2 state tables, §5 motion, §8 copy)
> **Task:** [P2-07-FE](../../../tasks/Frontend/student-app/Phase-2-Learning-Core/P2-07-FE.md)
> **Surface:** student-app **web PWA** (child surface), within the quiz player — `apps/student-app/app/(child)/lessons/[lessonId].tsx`
> **Scope:** frontend web E2E only. **No `backend-test-cases.md`** (no HTTP surface owned by this story — the BE submit-answer endpoint belongs to P2-07-BE).
> **Designed:** 2026-06-09 · QC architect (Opus pass)

---

## 1. Summary

P2-07 is the **instant answer feedback** slice of Wave 12. It is not a new screen — it is the
behaviour **inside** the quiz player (built in P2-06-FE) that fires the moment a student submits an
answer. The backend grades the answer server-side; the frontend renders the verdict verbatim from the
`SubmitAnswerResponse` (`isCorrect`, `correctAnswer`). This story owns:

- **Correct** answer → positive, encouraging feedback (green `AnswerFeedbackStrip` "Great job!" + the
  chosen option turns green ✓), then **800ms auto-advance** to the next question (no full reload).
- **Wrong** answer → corrective feedback (red strip "Not quite" + reveal "Correct answer: {answer}",
  the chosen option turns red ✕, the correct option turns green ✓), then an explicit **"Next"** CTA.
- Feedback is **immediate, same-screen** (state machine, no navigation/reload).
- Feedback **matches the server `isCorrect`** — the FE never grades; it renders what the BE returns.
- Kid-UX (NFR-6): encouraging never punishing; soft non-exclamatory wrong copy; live-region announce.
- Arabic-default **RTL** vs English LTR; i18n copy (no raw keys).

This QC pass is **post-implementation** (task marked Done, audited 2026-06-07). The cases below both
verify the shipped behaviour and harden the edges (per-type feedback, locale, last-question advance,
network failure during submit, no-reload guarantee, the deferred confetti/shake/XP).

### Counts

| Metric | Count |
|---|---|
| **Total cases** | **24** |
| Frontend (`frontend-e2e-tester`) | 24 |
| Backend | 0 (out of scope) |
| P0 | 8 (FE-TC-01..06, 11, 12) |
| P1 | 9 (FE-TC-07, 08, 09, 10, 13, 14, 15, 16, 19) |
| P2 | 7 (FE-TC-17, 18, 20, 21, 22, 23, 24) |
| BLOCKED (fully or partially, with reason) | 5 — FE-TC-18 (full), FE-TC-23 (full), FE-TC-19, FE-TC-21, FE-TC-24 (partial) |

---

## 2. Coverage matrix — every acceptance criterion → case IDs

| # | Acceptance criterion (story / task) | Covered by | Verdict |
|---|---|---|---|
| AC1 | Correct answer → positive confirmation; recorded correct | FE-TC-01, FE-TC-02, FE-TC-09, FE-TC-13 | ✅ covered (confetti **deferred** — see AC1 note) |
| AC2 | Wrong answer → correct/wrong screen + hint affordance (heart-loss = Phase 4) | FE-TC-03, FE-TC-04, FE-TC-05, FE-TC-10, FE-TC-14 | ✅ covered (heart-loss/hint-active **deferred** — FE-TC-22, FE-TC-24) |
| AC3 | Feedback appears in same screen without full reload | FE-TC-06, FE-TC-07, FE-TC-08, FE-TC-19 | ✅ covered |
| AC4 | Each answer result persisted for analytics | (backend — P2-07-BE) | ⛔ out of FE scope; FE-TC-08 asserts the request is sent, not persistence |
| — | Feedback matches server `isCorrect` (FE never grades) | FE-TC-11, FE-TC-12 | ✅ covered (route-mock to force verdict deterministically) |
| — | Advance to next question after feedback (correct=auto, wrong=Next) | FE-TC-02, FE-TC-04, FE-TC-07, FE-TC-15 | ✅ covered |
| — | Feedback per question type (MCQ / TrueFalse / FillInBlank / Matching) | FE-TC-13, FE-TC-14, FE-TC-16, FE-TC-17, FE-TC-18 | ✅ covered (Matching BLOCKED — no seed) |
| — | RTL ar vs LTR en | FE-TC-09, FE-TC-10 | ✅ covered |
| — | i18n — no raw keys, translated copy | FE-TC-09, FE-TC-10, FE-TC-20 | ✅ covered |
| — | Kid-UX: encouraging tone, live-region announce, no punishing chrome | FE-TC-21, FE-TC-22, FE-TC-23 | ⚠️ partial (FE-TC-21 a11y-announce partial; FE-TC-23 reduced-motion BLOCKED) |

**Gap note (AC1 confetti / AC2 hint+heart):** the Design Spec **deliberately defers** confetti (W14
polish), Reanimated soft-shake (W14), live XP, and heart-loss/active-hint to Phase 3/4 (W12 spec §10,
§12; task P2-07-FE notes). The W12 shipped surface for "positive confirmation" is the **green strip +
green option + 800ms auto-advance**, and for "wrong screen + hint affordance" is the **red strip +
reveal + locked chrome + a visible-but-disabled Hint button**. Cases assert the **shipped** behaviour
and explicitly assert the deferred pieces are absent/stubbed (FE-TC-22, FE-TC-24) so the deferral is
traceable rather than a silent gap.

**No acceptance criterion is left without a case.** AC4 is backend-owned (P2-07-BE) and flagged.

---

## 3. Risk notes (where the cases are weighted, and why)

1. **"Matches server `isCorrect`" is the load-bearing contract and the hardest to test
   deterministically.** The FE renders verbatim whatever the BE grades. There is **no documented
   seeded quiz with known correct/wrong answers** (HANDOFF lists seed existence but not answer keys).
   So the only deterministic lever in pure web E2E is **route-mocking** `**/Quizzes/*/Answers` to
   return a forced `{ isCorrect, correctAnswer }` envelope (FE-TC-11, FE-TC-12) — this isolates the
   rendering contract from the live grader. Live happy-path cases (FE-TC-01..05) use the real BE but
   must tolerate either verdict and assert the **strip variant matches whatever the response said**.
   This is the single biggest risk area; 6 cases are weighted here.

2. **The auto-advance race (correct → 800ms timer).** The timer reads `stageRef.current` at fire-time
   to avoid a stale closure. Edge risks: unmounting (back) before the timer fires (must clear the
   timer + fire Abandon, not advance), and the **last question** path where the timer fires `Complete`
   instead of advancing (FE-TC-15). Reduced-motion bumps 800ms→1200ms but **the reduced-motion gate is
   not yet wired** (HANDOFF W12 "still open"): FE-TC-23 BLOCKED.

3. **No `testID` hooks on the feedback primitives.** `[lessonId].tsx` passes **no `testID`** to
   `AnswerFeedbackStrip`, `QuestionCard`, `MCQOption`, `TrueFalseChoice`, or `FillInBlank`. E2E must
   fall back to `getByRole('alert')` (the strip's `accessibilityRole="alert"` + `aria-live="polite"`)
   and to option text / button `aria-label`s. This is fragile against Arabic-default copy and is the
   top open question (§4). It also makes the per-state colour assertions (green vs red) reliant on
   computed-style probing rather than a clean state hook.

4. **Network failure mid-submit.** On submit error the screen reverts to `answering`, preserves the
   selection, and shows the inline `child.quiz.networkError` strip — a *different* strip from the
   feedback strip, but **also `$dangerSoft` red with no testID**. A tester could conflate the two —
   FE-TC-19 isolates it but is **partially BLOCKED** pending a stable hook to distinguish them.

5. **Per-question-type feedback divergence.** MCQ/TrueFalse mark the wrong pick red + correct green;
   FillInBlank shows the field red + reveal text; Matching is a **stub** that always submits `""` and
   will be graded wrong. Each path renders the strip differently and is covered separately
   (FE-TC-13/14/16/17/18). Matching depends on the BE actually returning a question of that type,
   which it does **not** seed today (W12 spec §12) — FE-TC-18 is BLOCKED.

---

## 4. Open questions / assumptions (lead must resolve before implementation)

1. **[BLOCKER-ish] Missing `testID`s on feedback primitives.** `[lessonId].tsx` renders
   `AnswerFeedbackStrip` / `QuestionCard` / `MCQOption` / `TrueFalseChoice` / `FillInBlank` **without
   any `testID`**. Requested stable hooks (frontend to add):
   - `feedback-strip` on the `AnswerFeedbackStrip` outer node, **plus** a state hook —
     `feedback-strip-correct` / `feedback-strip-incorrect` (or a `data-` attr) so the variant is
     assertable without colour probing.
   - `feedback-reveal` on the reveal-text node (the "Correct answer: …" line).
   - `quiz-submit` / `quiz-next` on the primary CTA (label flips Check answer ↔ Next).
   - `quiz-error-strip` on the `QuestionCard` network-error strip (to distinguish it from feedback).
   - `mcq-option-{index}` (+ a state hook) on each `MCQOption`.
   - `fill-blank-input` on the FillInBlank `<input>`.
   Until these land, E2E uses `getByRole('alert')` for the strip and `aria-label`/text for options +
   buttons (documented per case). **Decision needed:** add the hooks now (preferred, mirrors P1-09's
   testID retro-fit), or accept role/label selectors for this pass?

2. **[BLOCKER] How to deterministically submit a KNOWN-correct and KNOWN-wrong answer.** The FE never
   grades; the BE does, and **no seeded quiz with a published answer key is documented**. Two options
   — lead to choose:
   - **(a) Route-mock** `POST **/Quizzes/*/Answers` to return a fixed
     `{ successed:true, data:{ isCorrect:true|false, correctAnswer:"…", hintAvailable:false } }`
     envelope. Deterministic, hermetic, isolates the *rendering* contract (which is what this story
     owns). Used by FE-TC-11/12. **Recommended.**
   - **(b) Seed a fixed quiz with known answers** (e.g. a known MCQ where option A is correct) and
     drive the real grader. More realistic but requires a BE seed fixture that does not exist yet, and
     couples the FE test to BE seed data. If chosen, file a seed request against P2-07-BE / data-seed.
   Live-BE cases (FE-TC-01..05) are written verdict-agnostic (assert the strip **matches** the
   response, whatever it is) so they pass without a known key.

3. **Assumption — `getByRole('alert')` resolves the feedback strip on RN Web.** The strip sets
   `accessibilityRole="alert"` + `aria-live="polite"` + `aria-label`. Assumed to map to an
   `role="alert"` node in the DOM. If RN Web drops the role on the `Animated.View`, fall back to
   `getByText(/Great job|أحسنت|Not quite|ليست الإجابة/)` and flag for a `testID`.

4. **Assumption — reaching the quiz player requires the full child-onboarding chain** (register parent
   → add child → sign in as child → open a subject with an **Available** lesson → Start). This is
   expensive (mirrors P1-09 helpers). Cases reuse those helpers. **Assumption:** the seeded curriculum
   has at least one Available lesson with ≥1 question for the test child's grade. If not, every case is
   blocked at setup — confirm the seed guarantees a startable lesson.

5. **Assumption — confetti / Skia / Reanimated soft-shake / live XP / heart-loss are out of scope for
   W12** (deferred per spec §10/§12). FE-TC-22 + FE-TC-24 assert their **absence/stub**; if product now
   wants them in scope, those flip from "assert absent" to "assert present" and new cases are needed.

6. **Reduced-motion timer (800ms→1200ms) is not yet wired** (HANDOFF W12 open item). FE-TC-23 is
   BLOCKED until `AccessibilityInfo.isReduceMotionEnabled()` is wired into the strip + lesson timer.

7. **Matching questions are not seeded** (W12 spec §12). FE-TC-18 is BLOCKED until the BE seeds at least
   one Matching question for a reachable lesson.

---

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| [`frontend-test-cases.md`](./frontend-test-cases.md) | **`frontend-e2e-tester`** | Implement each FE-TC-* 1:1 as a Playwright test in `tests/e2e/specs/P2-07-FE.spec.ts`. Reuse the P1-09 onboarding helpers (register → add-child → child sign-in). Prefer the requested `testID`s; if absent, use the documented role/label fallbacks and report the missing hooks back to `frontend`. |
| [`execution-report.md`](./execution-report.md) | **`frontend-e2e-tester`** (fills it) | After running, record pass/fail per case + any defects. The QC architect created the empty template only — **the tester fills results; QC never fills them**. |
| `backend-test-cases.md` | — | **Not produced** (no HTTP surface owned by P2-07-FE). |

**Coverage verdict:** every acceptance criterion has at least one P0/P1 case. AC4 (persistence) is
backend-owned and flagged out of FE scope. 5 cases are fully or partially BLOCKED with documented
reasons (no fake passes). The two top blockers needing a lead decision before implementation are
**(OQ1) missing testIDs** and **(OQ2) how to deterministically force a known-correct / known-wrong
verdict**.

Test cases ready — `frontend-e2e-tester` to implement `frontend-test-cases.md`; results go into `execution-report.md`. (No `backend-test-cases.md` — no HTTP surface in scope.)
