# Execution Report — P2-07-FE (Instant answer feedback)

> **Filled by:** `frontend-e2e-tester` **after** running `tests/e2e/specs/P2-07-FE.spec.ts`.
> The QC architect created this template only — **do not fill results during design.**
> Source cases: [`frontend-test-cases.md`](./frontend-test-cases.md) · Plan: [`README.md`](./README.md)

## Run metadata
- **Date / run:** _TBD_
- **Spec file:** `tests/e2e/specs/P2-07-FE.spec.ts`
- **Backend:** `http://localhost:5080` (commit/branch: _TBD_)
- **Web:** Expo web `http://localhost:8081`
- **Browser / Playwright version:** _TBD_
- **Result summary:** _N passed / N failed / N skipped(blocked) of 24_

## Per-case results

| Case ID | Title | Priority | Result (PASS / FAIL / BLOCKED / SKIP) | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Correct answer renders positive feedback strip (live, verdict-agnostic) | P0 | _TBD_ | |
| FE-TC-02 | Correct: option green ✓ + auto-advance ~800ms | P0 | _TBD_ | |
| FE-TC-03 | Wrong: corrective strip reveals correct answer | P0 | _TBD_ | |
| FE-TC-04 | Wrong: explicit "Next" CTA, no auto-advance | P0 | _TBD_ | |
| FE-TC-05 | Wrong: pick red ✕, correct green ✓, others lock | P0 | _TBD_ | |
| FE-TC-06 | Submit does not trigger a full page reload | P0 | _TBD_ | |
| FE-TC-07 | Feedback → next question in-place (URL unchanged) | P1 | _TBD_ | |
| FE-TC-08 | Exactly one POST to grade endpoint per answer | P1 | _TBD_ | |
| FE-TC-09 | Correct strip encouraging + no reveal text (kid-UX) | P1 | _TBD_ | |
| FE-TC-10 | Wrong copy soft/supportive + translated (ar+en) | P1 | _TBD_ | |
| FE-TC-11 | Forced isCorrect:true → correct variant regardless of pick | P0 | _TBD_ | |
| FE-TC-12 | Forced isCorrect:false → incorrect variant + reveal | P0 | _TBD_ | |
| FE-TC-13 | MCQ correct feedback (per-type) | P1 | _TBD_ | |
| FE-TC-14 | TrueFalse wrong feedback (per-type) | P1 | _TBD_ | block if no TF seeded |
| FE-TC-15 | Last correct question → Complete → Summary | P1 | _TBD_ | |
| FE-TC-16 | FillInBlank wrong feedback (per-type) | P1 | _TBD_ | block if no FIB seeded |
| FE-TC-17 | FillInBlank correct feedback (per-type) | P2 | _TBD_ | block if no FIB seeded |
| FE-TC-18 | Matching stub → empty submit → wrong path | P2 | **BLOCKED** | no Matching seed (W12 §12) |
| FE-TC-19 | Network failure on submit → error strip, not feedback | P1 | _TBD_ | partial — strip disambiguation needs testID |
| FE-TC-20 | No raw i18n keys in feedback flow (ar+en) | P2 | _TBD_ | |
| FE-TC-21 | Feedback strip role=alert + aria-live announce | P2 | _TBD_ | partial — SR speech is manual |
| FE-TC-22 | No confetti / no live-XP toast on correct (scope guard) | P2 | _TBD_ | |
| FE-TC-23 | Reduced-motion auto-advance ~1200ms | P2 | **BLOCKED** | reduced-motion gate not wired |
| FE-TC-24 | Hint affordance visible-but-disabled on wrong | P2 | _TBD_ | partial — active hint = P3-05 |

## Defects found
| ID | Severity | Case(s) | Summary | Status |
|---|---|---|---|---|
| _TBD_ | | | | |

## Missing test hooks reported back to `frontend` (from README §4 OQ1)
- [ ] `feedback-strip` + `feedback-strip-correct` / `feedback-strip-incorrect` on `AnswerFeedbackStrip`
- [ ] `feedback-reveal` on the reveal-text node
- [ ] `quiz-submit` / `quiz-next` on the primary CTA
- [ ] `quiz-error-strip` on the `QuestionCard` network-error strip
- [ ] `mcq-option-{index}` (+ state hook) on each `MCQOption`
- [ ] `fill-blank-input` on the FillInBlank `<input>`

## Lead decisions consumed (from README §4)
- [ ] OQ2 — determinism approach chosen: route-mock (recommended) vs seeded known-answer quiz.
- [ ] OQ4 — confirmed the seed yields ≥1 Available lesson with ≥1 question for the test child's grade.

## Notes / environment caveats
_TBD — e.g. which question types were actually reachable in the seed; any flakiness around the 800ms timer._
