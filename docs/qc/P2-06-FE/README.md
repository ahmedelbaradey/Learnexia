# QC Test Plan + Coverage Report — P2-06-FE (Take a quiz — 4 question types)

> **Surface:** student-app web PWA, **child** surface. Frontend-only QC pass.
> **Scope:** the W12 Lesson Player **quiz stage** + the 4 question-type renderers.
> **Story:** [`user-stories/Phase-2-Learning-Core/P2-06-take-a-quiz.md`](../../../user-stories/Phase-2-Learning-Core/P2-06-take-a-quiz.md)
> **Design Spec:** [`design-system/ui_kits/student-mobile/W12-lesson-quiz.md`](../../../design-system/ui_kits/student-mobile/W12-lesson-quiz.md)
> **Task file:** [`tasks/Frontend/student-app/Phase-2-Learning-Core/P2-06-FE.md`](../../../tasks/Frontend/student-app/Phase-2-Learning-Core/P2-06-FE.md)
> **Designed:** 2026-06-09 · **Designer:** qc-test-designer (Opus)

This pass is **design-only**. The cases below are implemented by `frontend-e2e-tester` from
[`frontend-test-cases.md`](./frontend-test-cases.md); results land in [`execution-report.md`](./execution-report.md).
No `backend-test-cases.md` — this is a frontend-only run (the BE attempt/answer/complete contract is
exercised live by the UI flow but is owned by P2-06-BE / `api-tester`, not this story's QC pass).

---

## 1. Summary

The quiz lives inside a single route — `apps/student-app/app/(child)/lessons/[lessonId].tsx` — as the
middle stage of a 3-stage state machine (`intro → quiz → summary`). A child reaches it from the home
**ContinueCard** or the **Subjects → Lessons** tab, taps **Start lesson**, then walks one question at a
time. Each question renders one of four type-specific renderers via a plain `switch(question.questionType)`:

| `QuestionType` enum | Type | Renderer component | Status |
|---|---|---|---|
| `_1` | MCQ | `MCQOption` list (radiogroup) | ✅ live |
| `_2` | True/False | `TrueFalseChoice` (50/50 pair) | ✅ live |
| `_4` | Fill-in-the-blank | `FillInBlank` (TextInput) | ✅ live |
| `_3` | Matching | `MatchingPanel` | 🟡 **STUB** — renders a "coming soon" tile, submits `""` |

Answering gives instant token-driven selection chrome; **Submit** ("Check answer") sends the answer;
on **correct** the feedback strip shows for 800ms then auto-advances; on **incorrect** a "Next" CTA
appears (correct-answer reveal is the P2-07 feedback strip, also bundled in this same wave). The last
question completes the attempt → Summary.

### Case counts

| | Count |
|---|---|
| **Total FE cases** | **34** |
| Backend cases | 0 (frontend-only run) |
| **By priority** | P0: 14 · P1: 13 · P2: 7 |
| **By status** | Testable: 24 · **BLOCKED: 10** |

The high blocked count is driven by **two structural gaps** (see §3): (a) **no testIDs are wired on the
quiz surface** — the screen never passes `testID` to any question-type component, option, button, or
container; and (b) **no deterministic seed exists to reach a quiz containing all 4 question types** (and
Matching has zero seeded data anywhere — it is a pure stub). Cases that depend on either are scaffolded
as `test.skip` with the blocker named, not dropped.

---

## 2. Coverage matrix — acceptance criteria → cases

Acceptance criteria from the story (lines 15–18). Only the **frontend-observable** portion of each AC is
in scope; persistence (AC4) is a BE concern, covered here only as "the UI flow that drives persistence completes".

| # | Acceptance criterion (frontend-observable portion) | Case IDs | Verdict |
|---|---|---|---|
| AC1 | Quiz supports **MCQ** | FE-TC-05, FE-TC-06, FE-TC-07 | ✅ covered |
| AC1 | Quiz supports **True/False** | FE-TC-09, FE-TC-10 | ✅ covered |
| AC1 | Quiz supports **Fill-in-the-blank** | FE-TC-12, FE-TC-13, FE-TC-14 | ✅ covered |
| AC1 | Quiz supports **Matching** | FE-TC-16, FE-TC-17 | ⚠️ covered as **STUB only** (BLOCKED for real renderer — none exists this wave) |
| AC2 | Starting a quiz creates an Attempt; screen shows **progress** | FE-TC-01, FE-TC-02, FE-TC-20 | ✅ covered |
| AC2 | Screen shows the **question card** | FE-TC-03 | ✅ covered |
| AC2 | Screen shows **answer controls per type** | FE-TC-05, FE-TC-09, FE-TC-12, FE-TC-16 | ✅ covered |
| AC3 | Each type **renders + accepts input correctly** (desktop web) | FE-TC-06, FE-TC-10, FE-TC-13, (FE-TC-17 stub) | ✅ covered (Matching = stub) |
| AC3 | Works on **mobile + desktop** (responsive) | FE-TC-25, FE-TC-26 | ✅ covered (web viewport proxy) |
| AC4 | QuizQuestion/Attempt/StudentAnswer **persist the session** | FE-TC-22 (flow completes → Summary persists), FE-TC-23 | ⚠️ FE proxy only (persistence asserted by api-tester, not here) |

### Additional coverage (not 1:1 to an AC line but required by the Design Spec / kid-UX / product rules)

| Theme | Case IDs |
|---|---|
| Submit / advance flow (Check answer → feedback → Next/auto-advance) | FE-TC-20, FE-TC-21, FE-TC-22 |
| Instant visual selection state | FE-TC-07, FE-TC-08, FE-TC-11 |
| Locked-after-submit (controls non-interactive) | FE-TC-24 |
| Hearts / lives indicator (static 3 this wave) | FE-TC-27 |
| RTL (ar) vs LTR (en) per type | FE-TC-28, FE-TC-29, FE-TC-30 |
| i18n — no raw keys | FE-TC-31 |
| Loading / error states | FE-TC-32, FE-TC-33 |
| Empty-lesson (0 questions) defensive path | FE-TC-34 |
| Kid-UX — touch targets, one-question focus, a11y roles | FE-TC-03, FE-TC-08, FE-TC-27 |
| Negative — no Skip affordance, no confetti (product/spec overrides) | FE-TC-04 |

**Coverage verdict:** every acceptance criterion has **at least one** P0/P1 case. The only criterion not
fully satisfiable is **AC1 Matching → real renderer**: it ships as a deliberate stub this wave (Design Spec
§3.6, §11 item 6), so the Matching cases assert the **stub contract** (tile renders, Submit becomes "Next",
empty payload submits) and the real-renderer cases are **BLOCKED**. This is a known, intentional gap — not a
QC miss. AC4 persistence is a BE assertion; the FE pass only proves the UI flow that triggers it completes.

---

## 3. Risk notes (where the cases are weighted, and why)

1. **No testIDs on the quiz surface — highest risk to testability.** `apps/student-app/app/(child)/lessons/[lessonId].tsx`
   renders `MCQOption`, `TrueFalseChoice`, `FillInBlank`, `MatchingPanel`, `QuestionCard`, `ProgressDots`,
   `AnswerFeedbackStrip`, `AttemptSummaryCard`, the Submit/Next `Button`, the Start CTA, and the hero card
   **without passing `testID`** — even though every one of those components accepts a `testID` prop. The
   testers must fall back to `getByRole` (`radio` / `radiogroup` / `progressbar` / `group` / `alert` /
   `button` / `text`) and `getByLabel` (the composed a11y labels), which are present and usable but coarser:
   e.g. there is no per-question-type hook to assert "the MCQ renderer is the one on screen" vs TrueFalse —
   both expose a `radiogroup`. Cases that need to deterministically target a **specific** question type, a
   **specific** option, or the Submit button are weighted as P0/P1 but flagged BLOCKED-on-testID where the
   role/label fallback cannot disambiguate. **Resolving this is the single highest-leverage fix** — see §4 OQ1.

2. **Seeding a quiz with all 4 types deterministically.** There is no documented seed path (HANDOFF, README)
   that produces a lesson whose questions cover MCQ + TrueFalse + FillInBlank + Matching in a known order. The
   e2e harness seeds parent+child via API, then drives the UI; but which lesson the child lands on, and which
   question types it contains, is **content-dependent and non-deterministic** from the tester's view. Worse,
   per HANDOFF + Design Spec the **BE has zero Matching questions seeded** — so Matching can likely never be
   reached organically at all. Per-type cases are therefore weighted P0/P1 but split: "if a question of type X
   appears, assert its renderer" (robust, type-conditional) vs "navigate to a known type-X question" (BLOCKED
   on a deterministic seed). See §4 OQ2.

3. **Matching is a stub.** Real Matching interaction does not exist (Design Spec §3.6 — muted tile, no states).
   Any case asserting drag-pair matching is BLOCKED with "stub only, real renderer deferred until BE seeds
   Matching questions". The stub-contract cases (tile + Submit-becomes-Next + empty payload) are testable
   **only if** a Matching question can be reached (which currently it cannot — double-blocked).

4. **Correct-answer reveal is P2-07, not P2-06.** The feedback strip + auto-advance live in the same screen
   (same wave) but belong to story P2-07. This pass asserts the **P2-06 portion** — that an answer is submittable
   and the quiz advances — and treats the strip's *content* (reveal text, "Great job!" copy) as adjacent
   surface it can sanity-check (FE-TC-21) without owning the full P2-07 coverage.

5. **Auto-advance timing (800ms) is timing-sensitive.** Correct-answer auto-advance fires on a `setTimeout(800)`.
   E2E assertions on "advanced to next question" must wait past 800ms (or 1200ms under reduced-motion). Flaky
   if asserted too eagerly — cases note the wait explicitly.

6. **RTL is the default.** Arabic is the default locale; the child surface may also derive direction from
   `Me.preferredLanguage` (and there is a **known pre-existing bug** — HANDOFF / P1-09 FE-TC-09 — that child
   login may not apply `preferredLanguage` over the persisted UI locale). RTL cases assert `html[dir]` and
   logical-position mirroring; the locale-from-Me quirk is noted so a wrong `dir` on first child landing is
   attributed correctly and not mis-filed as a P2-06 defect.

---

## 4. Open questions / assumptions (lead must resolve before/at implementation)

**OQ1 — Missing testIDs on the quiz surface (BLOCKER for clean targeting).** The screen passes no `testID`
to any quiz primitive. Requested additions (the components already accept the prop — this is a one-line wiring
change per slot in `[lessonId].tsx`):

| Needed testID | On | Why the tester needs it |
|---|---|---|
| `quiz-question-card` | `QuestionCard` | Anchor the current question; assert one-question-at-a-time. |
| `quiz-renderer-mcq` / `quiz-renderer-truefalse` / `quiz-renderer-fillblank` / `quiz-renderer-matching` | the renderer container per branch | **Disambiguate which question type is on screen** (roles alone can't distinguish MCQ vs TrueFalse — both are `radiogroup`). |
| `quiz-mcq-option-{index}` | each `MCQOption` | Select a specific MCQ option deterministically. |
| `quiz-truefalse-true` / `quiz-truefalse-false` | each `TrueFalseChoice` side | Toggle a specific side. |
| `quiz-fillblank-input` | `FillInBlank` TextInput | Type into the field. |
| `quiz-matching-panel` | `MatchingPanel` | Assert the stub tile. |
| `quiz-submit` | the Submit / Next `Button` | The single most-used control — currently only reachable by its (locale-dependent) a11y label. |
| `quiz-progress-dots` | `ProgressDots` | Already exposes `progressbar` role + `accessibilityValue` — testID is a convenience. |
| `quiz-feedback-strip` | `AnswerFeedbackStrip` | Exposes `alert` role; testID lets us assert variant + reveal text. |
| `quiz-hearts` | `Hearts` in the TopBar | Assert the lives indicator. |
| `quiz-start-cta` | Start `Button` on Intro | Enter the quiz. |
| `quiz-summary-card` | `AttemptSummaryCard` | Assert the quiz completed. |

→ **Decision needed:** add these testIDs to `[lessonId].tsx` before the tester runs? Without them, ~7–8 cases
fall back to role/label selectors (workable but coarser) and the per-type **disambiguation** cases stay BLOCKED.

**OQ2 — Deterministic seed for a quiz with all 4 question types.** How does the tester reach a lesson whose
questions cover MCQ + TrueFalse + FillInBlank (+ Matching) in a known order?
  - Is there a seed lesson ID (e.g. a fixed Grade-1 Math lesson) guaranteed to contain MCQ + TrueFalse + FillInBlank?
  - Can the BE/test harness expose a seeded "QC quiz" lesson, or a seed script, the tester can navigate to directly
    via `/(child)/lessons/{knownId}?subjectId={knownId}`?
  - **Matching has zero seeded data** (HANDOFF, Design Spec §11.6) — confirm Matching can be reached at all. If
    not, all Matching cases stay BLOCKED and we assert only the stub via a unit-level/mocked path if the lead approves.
  - Assumed fallback (used in the cases): seed parent+child via API, log in as child, navigate to the first
    Available lesson, **assert types conditionally** ("if an MCQ question is present, …"). This is robust but
    cannot *guarantee* every type appears in one run.

**OQ3 — Is asserting the P2-07 feedback strip content in-scope for this P2-06 pass?** The strip + auto-advance
share the screen. Assumption: this pass sanity-checks that the strip appears and the quiz advances (FE-TC-21),
but full reveal-text / "Great job!" coverage is P2-07's QC pass. Confirm the boundary.

**OQ4 — Reduced-motion timing.** Auto-advance is 800ms (1200ms under `prefers-reduced-motion`). Should the
tester run with reduced-motion forced (stable 1200ms) or default? Assumption: default (800ms), with generous waits.

**OQ5 — Child-login seed.** Cases assume the child logs in via the login form with the **persona toggle set to
student** (no student self-register). Confirm the child-login UI path is the intended seed for the child surface
(vs a token-injection shortcut).

---

## 5. Handoff

| File | Owner | Consumed by |
|---|---|---|
| [`frontend-test-cases.md`](./frontend-test-cases.md) | qc-test-designer (this pass) | **`frontend-e2e-tester`** — implements each FE-TC as one Playwright test in `tests/e2e/specs/P2-06-FE.spec.ts`. |
| [`execution-report.md`](./execution-report.md) | scaffolded empty by this pass | **`frontend-e2e-tester`** — fills pass/fail per case + defects **after** running. The designer never fills results. |
| `backend-test-cases.md` | — | **Not produced** (frontend-only run). |

**Implementation notes for the tester:**
- Selector order per the harness README: `getByTestId` → `getByRole` / `getByLabel`. Never copy-based (Arabic default).
- Until OQ1 is resolved, use the role/label fallbacks listed inline per case.
- Seed via API (`Register-Parent` → `Add-Child`), assert via UI; unique emails per run; hermetic specs.
- BLOCKED cases scaffold as `test.skip(...)` with the blocker reason in the title, mirroring the P1 specs.
- Report any *new* needed testID back to `frontend` (don't reach into CSS classes).

---

**Test plan ready.** See [`frontend-test-cases.md`](./frontend-test-cases.md) for the 34 FE-TC cases.
