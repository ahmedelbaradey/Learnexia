# QC Test Plan + Coverage Report — P2-05-FE (Open & complete a lesson, web PWA)

> On-demand QC pass (Opus, design-only). **Frontend-only** — student-app web E2E. No backend HTTP catalog (this folder intentionally has no `backend-test-cases.md`).
> Surface under test: the lesson player at `apps/student-app/app/(child)/lessons/[lessonId].tsx` (3-stage view-state machine: intro → quiz → summary). Child surface, Arabic-default RTL, kid-UX.
> Scope note: this story is the **lesson CONTENT + open/complete flow**. The quiz question types are P2-06-FE and the instant answer feedback is P2-07-FE — those have (or will have) their own QC folders. Here we cover only what P2-05-FE owns: open a lesson, render content, progress through it, **complete**, and the resulting next step + states. Quiz/feedback are touched only as the "next step" boundary, not exhaustively.

---

## 1. Summary

| | |
|---|---|
| Story | `user-stories/Phase-2-Learning-Core/P2-05-open-and-complete-lesson.md` (FR-LR-4) |
| Task file | `tasks/Frontend/student-app/Phase-2-Learning-Core/P2-05-FE.md` (marked ✅ Done, audited 2026-06-07) |
| Design spec | `design-system/ui_kits/student-mobile/W12-lesson-quiz.md` (Wave 12, Surfaces 1–3) |
| Screen | `apps/student-app/app/(child)/lessons/[lessonId].tsx` (single route, view-state machine) |
| Target agent | `frontend-e2e-tester` (Playwright, web PWA at `:8081`, backend at `:5080`) |
| Total cases | **28** (all frontend) |
| By priority | P0: 9 · P1: 14 · P2: 5 |
| By status | Drivable now (no/optional testID): ~6 · **BLOCKED on missing testIDs (OQ-1): ~21** · BLOCKED on missing feature (resume): 1 (FE-TC-11) |

**Headline coverage verdict:** Every acceptance criterion is covered by at least one P0/P1 case. **However, the lesson-player screen ships with almost no screen-level `testID`s** (only the UI *primitives* accept a `testID` prop — the screen does not pass them, and the intro hero, TopBar, CTAs, and stage containers have none). Per the harness selector convention (`getByTestId` first; Arabic is the default locale so copy-based selectors are forbidden), the bulk of this catalog is **BLOCKED pending testID hooks from `frontend`** (see §4 OQ-1, the dominant open question). A handful of cases are drivable today via `accessibilityRole`/`accessibilityLabel`, URL, `html[dir]`, and a raw-key regex sweep.

---

## 2. Coverage matrix — acceptance criterion → case IDs

The story's acceptance criteria are written backend-leaning; the FE-relevant slices (plus the task-file FE breakdown and the design spec's Surface 1–3 states) are mapped here.

| # | Acceptance criterion / FE obligation (source) | Covered by | Gap? |
|---|---|---|---|
| AC1 | Lesson screen shows AI-tutor bubble (explanation), visual area, hearts display (Story AC2; task FE-1; spec Surface 1) | FE-TC-01, FE-TC-02, FE-TC-03, FE-TC-25 | covered |
| AC2 | Open a lesson → content renders (explanation/visual/title) (Story AC1; spec Surface 1) | FE-TC-01, FE-TC-02, FE-TC-03 | covered |
| AC3 | Progress through the lesson (Start → quiz stage; advance question-by-question) (task FE-2/FE-3; spec Surface 2) | FE-TC-05, FE-TC-06, FE-TC-12 | covered |
| AC4 | **Complete** the lesson → completion/summary state shown (task FE-3; spec Surface 3) | FE-TC-07, FE-TC-08 | covered |
| AC5 | Completion records progress + correct next step (Story AC3 "marks lesson progress"; screen invalidates subjectLessons + dashboard cache → node reflects Completed) | FE-TC-08, FE-TC-09, FE-TC-10 | covered (FE-observable via tree/node state on return) |
| AC6 | Resume an in-progress lesson (lead-requested; NOT in story AC, NOT implemented — see Risk R1) | FE-TC-11 (BLOCKED — feature absent) | **GAP / feature absent** |
| AC7 | Loading state while lesson GET in flight (spec Surface 1 loading) | FE-TC-13 | covered |
| AC8 | Error state — lesson GET fails → retry + back (spec Surface 1 error) | FE-TC-14, FE-TC-15 | covered |
| AC9 | 404 — lesson not found (spec Surface 1 404) | FE-TC-16 | covered |
| AC10 | Empty lesson — Start resolves with 0 questions → empty state, no quiz transition (spec Surface 1 empty) | FE-TC-17 | covered |
| AC11 | Back / exit mid-lesson → abandon attempt fired, returns to tree (screen useEffect cleanup → useAbandonAttempt; spec Surface 2 back) | FE-TC-18, FE-TC-19 | covered |
| AC12 | Network error on Start (StartAttempt fails) → intro stays, recoverable | FE-TC-20 | covered |
| AC13 | RTL (ar default) vs LTR (en) — direction, chevron glyph swap, layout (spec §6) | FE-TC-21, FE-TC-22 | covered |
| AC14 | i18n — no raw keys leak in either locale across intro/quiz/summary/states (spec §8 copy) | FE-TC-23, FE-TC-24 | covered |
| AC15 | Kid-UX — one primary action per stage; ≥48px targets; instant visual feedback (spec §7) | FE-TC-12, FE-TC-25, FE-TC-26 | covered |
| AC16 | Summary "Try again" re-creates a fresh attempt (spec Surface 3 secondary CTA) | FE-TC-27 | covered |
| AC17 | Summary "Back to lessons" navigates to subject (router.replace to subject) (spec Surface 3 primary CTA) | FE-TC-28 | covered |
| AC18 | Auth/role — signed-out cannot reach the lesson route; parent cannot reach child surface (group guard) | FE-TC-04 | covered |

**Gaps flagged:** only **AC6 (resume an in-progress lesson)** has no satisfiable case — the feature is not built (the screen always starts at `intro` and `useStartAttempt` always creates/resumes server-side, but there is no FE "resume mid-quiz" surface and the intro stage is re-entered fresh on every open). FE-TC-11 documents the case and is marked **BLOCKED — feature absent**; this is a question for the lead (is resume in P2-05 FE scope, or deferred?).

---

## 3. Risk notes (where cases were weighted)

- **R1 — Resume semantics are ambiguous/absent (highest product risk).** The lead explicitly asked to cover "resume an in-progress lesson," but the screen has no resume UI: every open shows the intro and re-fires `useStartAttempt`. `useStartAttempt` maps to `POST .../{lessonId}/Attempt` whose doc says "Creates (or resumes)" — so the *server* may resume, but the *FE* always restarts the question walk from index 0 with a fresh stage. Weighted one explicit case (FE-TC-11) + an open question; do not silently pass a "resume" assertion the UI cannot satisfy.
- **R2 — Completion → next-step correctness (core of the story).** "Complete the lesson" must (a) reach the summary and (b) cause the node to read in-progress/completed on return to the tree. The screen invalidates `subjectLessons` + `dashboard` caches on complete; the *observable* proof is the LessonCard/SkillTreeNode state when the child returns. Heavily weighted (FE-TC-07/08/09/10) — and note the asymmetry: an **abandoned** mid-lesson exit should NOT mark the node completed (FE-TC-18/19).
- **R3 — Abandon-on-exit is a cleanup side effect (easy to regress).** Back/exit mid-quiz fires `useAbandonAttempt` via the unmount `useEffect`. It is fire-and-forget and depends on the component actually unmounting. Web back-button vs the in-screen chevron vs `router.replace` take different paths in the code (the chevron in intro/quiz calls `router.back()`; summary calls `handleBack`). Weighted two cases to pin both that abandon fires and that the node is NOT completed.
- **R4 — RTL is the default, not the exception.** Arabic is the default locale; the entire flow must be validated RTL-first (chevron `›`, `flexDirection: row-reverse`, Eastern-Arabic progress numerals `٢ من ٥`). LTR/en is the secondary check. Copy-based selectors are banned.
- **R5 — testID drought is a delivery risk, not just a test risk.** The screen exposes no stable hooks for: the intro hero/title, Start CTA, the Submit/Next CTA, the progress label/dots, the summary card + its two CTAs, the loading/error/empty/404 containers, the back chevron. Without them, the majority of cases cannot be driven deterministically in the Arabic default. This is the single biggest blocker and is the top handoff item back to `frontend`.

---

## 4. Open questions / assumptions (lead must resolve before implementation)

- **OQ-1 (dominant) — missing testIDs.** The lesson player passes no `testID` to its screen-level elements. Requested hooks (names are suggestions — `frontend` to add, then `frontend-e2e-tester` uses them):
  - `lesson-intro-card`, `lesson-title`, `lesson-explanation`, `lesson-visual`, `lesson-start-cta`
  - `lesson-loading`, `lesson-error`, `lesson-error-retry`, `lesson-404`, `lesson-empty`, `lesson-back` (back chevron), `lesson-empty-back`
  - `quiz-stage`, `quiz-progress-label`, `quiz-progress-dots` (the `ProgressDots` primitive already accepts `testID` — just pass it), `quiz-submit-cta`, `quiz-next-cta`, `quiz-error`
  - `lesson-summary-card` (the `AttemptSummaryCard` primitive accepts `testID`), `summary-back-cta`, `summary-retry-cta`, `summary-score`, `summary-accuracy`, `summary-duration`
  - For MCQ/TrueFalse/FillInBlank options, pass the existing primitive `testID` props with a per-option/index suffix (e.g. `mcq-option-0`).
  Until these land, the cases in §2 mapped to those surfaces are **BLOCKED** (each case lists its needed hook).
- **OQ-2 — Deterministic seed: how to land on a *specific* lesson.** The catalog assumes the standard seed (`LearningSeeder`, 6 language-tagged roots per grade; Grade 1 child has Math/Science/Arabic/English with units + lessons). The deterministic path is: register parent → add child (Grade 1, learningLanguage ar) → sign in as child → Subjects tab → pick a subject → first **Available** lesson card → tap → lands on `/(child)/lessons/{id}?subjectId={sid}`. **Need from lead/`api-tester`:** a known-good `(lessonId, subjectId)` pair (with explanation + visual + ≥2 questions) for the default seed so the tester can also deep-link `/(child)/lessons/{id}?subjectId={sid}` directly and not depend on tree ordering. Whether a child JWT can be minted via the API for a faster precondition (vs. the full register→add-child→login chain) is also a question for `api-tester`.
- **OQ-3 — Resume scope (ties to R1/FE-TC-11).** Is "resume an in-progress lesson" in P2-05-FE scope, or deferred (the task file does not list it; the story AC does not mention it)? If in scope, what is the intended UX (resume mid-quiz at the unanswered question? a "Continue" CTA on intro?) — there is no design-spec surface for it. Blocks FE-TC-11.
- **OQ-4 — Locale switch mid-flow is unreachable.** The locale switch controls live only on the Login screen (`LocaleThemeControls`); there is no in-lesson locale toggle. So "switch locale mid-lesson" is not drivable through the UI. Assumption: RTL/LTR is validated by signing in as an Arabic child vs an English child and entering the lesson fresh in each (FE-TC-21/22), not by toggling mid-stage. Confirm acceptable.
- **OQ-5 — Hearts are static (count=3) this wave.** Per spec + screen, Hearts is hardcoded `3/3` (live value is P4-04). Cases assert presence + a11y label only, not a dynamic count. Confirm no heart-loss behavior is expected for P2-05.
- **OQ-6 — Forcing a "lesson GET fails" / "Start fails" / 404 / empty deterministically.** Error/404/network/empty cases assume the tester uses Playwright route interception (`page.route('**/api/learning/Lessons/**', ...)` / `**/Attempt**`) to fault-inject, since the seeded backend returns healthy data. If route-mocking the API is disallowed, FE-TC-02(v2)/03(v2)/13/14/15/16/17/20 become BLOCKED (no natural trigger). Confirm route-mock is permitted (it is the standard pattern used in the Phase-1 FE pass).

---

## 5. Handoff

- `frontend-test-cases.md` → **`frontend-e2e-tester`**: implement each `FE-TC-*` 1:1 as a Playwright spec `tests/e2e/specs/P2-05-FE.spec.ts`, `getByTestId` first, never copy-based (Arabic default). Cases marked BLOCKED are written but `test.skip`/`test.fixme` with the documented reason — do not fake a pass. File missing-testID requests (OQ-1 list) back to `frontend`.
- `execution-report.md` → filled by **`frontend-e2e-tester`** after the run: pass/fail per case + defects + which BLOCKED cases got unblocked. The QC author scaffolds the empty template only; never fills results.
- There is intentionally **no `backend-test-cases.md`** in this folder (frontend-only pass). The P2-05 backend HTTP surface is covered separately under the backend QC track.

**Test cases ready** — `frontend-e2e-tester` to implement `frontend-test-cases.md` and write results into `execution-report.md`. (No `api-tester` work in this frontend-only pass.)
