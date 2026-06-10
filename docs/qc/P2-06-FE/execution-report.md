# Execution Report — P2-06-FE (Take a quiz — 4 question types)

> **Filled by `frontend-e2e-tester` AFTER implementing + running `tests/e2e/specs/P2-06-FE.spec.ts`.**
> Spec source: [`frontend-test-cases.md`](./frontend-test-cases.md).

## Run metadata

| Field | Value |
|---|---|
| Date run | 2026-06-10 |
| Spec file | `tests/e2e/specs/P2-06-FE.spec.ts` |
| Web target | `http://localhost:8081` (Expo web, EXPO_OFFLINE=1, reused server) |
| API target | `http://localhost:5080` |
| Browser / project | Chromium (Desktop Chrome, Playwright) |
| Seed method | API Register-Parent → Add-Child (grade 1, ar) → sign-in via UI → deep-link to `/(child)/lessons/1?subjectId=1` |
| testIDs wired? (README OQ1) | **Yes** — `quiz-question-card`, `quiz-renderer-mcq`, `quiz-mcq-option-{i}`, `quiz-submit`, `feedback-continue`, `answer-feedback-strip`, `lesson-summary`, `lesson-progress`, `lesson-start-cta`, `lesson-back`, `lesson-intro`, `lesson-screen`, `lesson-error`, `lesson-404`, `lesson-empty`. **UI-BUG-01** filed: testID on MCQOption is placed on inner Stack (content div) instead of outer Pressable — aria-checked / role / aria-label are on the Pressable; required `page.evaluate()` parent-walk workaround in all tests checking aria semantics on MCQ options. |
| All-4-types seed available? (README OQ2) | **No** — only lesson 1 has any questions; it contains 1 MCQ question only (questionType=1: "ما الرقم الذي يأتي بعد 5؟"). TrueFalse (type=2), FillInBlank (type=4), Matching (type=3) have zero seeded questions across all Grade-1 lessons. |

## Results summary

| Metric | Count |
|---|---|
| Total cases | 34 (+ 1 bonus security case) |
| Passed | **21** |
| Failed | **0** |
| Skipped / BLOCKED | **13** |

**Exit code: 0 (all runnable tests green). Total run time: ~7.6 minutes.**

## Per-case results

| ID | Title | Result | Notes / defect ref |
|---|---|---|---|
| FE-TC-01 | Start → attempt + quiz stage | **PASS** | quiz-question-card + quiz-renderer-mcq appeared after Start tap; lesson-intro hidden |
| FE-TC-02 | ProgressDots count | **PASS** | aria-valuenow=1, aria-valuemin=1, aria-valuemax=1 (1-question lesson) |
| FE-TC-03 | Question card stem + controls (one-focus) | **PASS** | One quiz-question-card; non-empty Arabic stem; option visible |
| FE-TC-04 | No Skip / no confetti | **PASS** | No Skip button found (count=0); no RewardPopup element |
| FE-TC-05 | MCQ renders option list | **PASS** | quiz-renderer-mcq visible; 4 radio options (["4","5","6","7"]); labels non-empty |
| FE-TC-06 | MCQ accepts selection | **PASS** | aria-checked=true on parent Pressable after tap; option 1 remains false; Submit enabled. UI-BUG-01: required parent DOM walk via evaluate() |
| FE-TC-07 | MCQ instant selection state | **PASS** | Selecting option B moves checked state from A; aria-checked flips correctly (parent walk) |
| FE-TC-08 | MCQ option a11y + target size | **PASS** | role=radio on Pressable parent; aria-label contains "خيار A: 4" (Arabic); bbox.height=56≥48px |
| FE-TC-09 | True/False renders pair | **SKIP** | BLOCKED — no seeded TrueFalse question (questionType=2) |
| FE-TC-10 | True/False toggles | **SKIP** | BLOCKED — no seeded TrueFalse question (questionType=2) |
| FE-TC-11 | True/False instant chrome | **SKIP** | BLOCKED — no seeded TrueFalse question (questionType=2) |
| FE-TC-12 | FillInBlank renders input | **SKIP** | BLOCKED — no seeded FillInBlank question (questionType=4) |
| FE-TC-13 | FillInBlank accepts typing | **SKIP** | BLOCKED — no seeded FillInBlank question (questionType=4) |
| FE-TC-14 | FillInBlank whitespace → Submit disabled | **SKIP** | BLOCKED — no seeded FillInBlank question (questionType=4) |
| FE-TC-15 | FillInBlank empty → Submit disabled | **SKIP** | BLOCKED — no seeded FillInBlank question (questionType=4) |
| FE-TC-16 | Matching stub tile renders | **SKIP** | BLOCKED — no seeded Matching question (questionType=3); BE has zero Matching questions |
| FE-TC-17 | Matching stub → Next + empty payload | **SKIP** | BLOCKED — same as FE-TC-16 |
| FE-TC-18 | Real Matching drag-pair | **SKIP** | BLOCKED — real Matching renderer does not exist this wave (stub only) |
| FE-TC-20 | Submit → feedback + lock | **PASS** | answer-feedback-strip appeared; feedback-continue (Next) visible (DEF-P205FE-01: all answers return isCorrect=false) |
| FE-TC-21 | Incorrect → Next advances | **PASS** | feedback-continue tap → lesson-summary visible (1-question lesson → completion) |
| FE-TC-22 | Full walk → Summary (persistence proxy) | **PASS** | Complete quiz walk → lesson-summary visible with localized content; lesson-summary-continue and lesson-summary-retry present |
| FE-TC-23 | Correct → 800ms auto-advance | **SKIP** | BLOCKED-soft — DEF-P205FE-01: all answers return isCorrect=false; auto-advance path unreachable |
| FE-TC-24 | Controls lock after submit | **PASS** | aria-disabled=true on MCQ option Pressable parents in feedback phase (parent DOM walk); quiz-submit gone (feedback-continue shown) |
| FE-TC-25 | Desktop responsive (1280×800) | **PASS** | quiz-question-card visible; MCQ option selectable (aria-checked=true via parent walk) at 1280px |
| FE-TC-26 | Mobile-width responsive (390×844) | **PASS** | MCQ option visible (bbox.height≥48, width>0, x≥0); selectable at 390px narrow |
| FE-TC-27 | Hearts indicator (static 3) | **PASS** | Hearts rendered with aria-label "٣ قلوب" (Arabic); localized, not a raw key |
| FE-TC-28 | MCQ RTL vs LTR | **PASS** | html[dir]=rtl; MCQ option aria-label contains "خيار" (Arabic "Option"); no raw keys |
| FE-TC-29 | True/False RTL vs LTR | **SKIP** | BLOCKED — no seeded TrueFalse question |
| FE-TC-30 | FillInBlank RTL alignment | **SKIP** | BLOCKED — no seeded FillInBlank question |
| FE-TC-31 | No raw i18n keys | **PASS** | assertNoRawKeys passed in quiz stage and feedback phase; feedback-continue text localized |
| FE-TC-32 | Submit network error strip | **PASS** | 500 intercept on answers endpoint → quiz-submit reappeared; selection preserved (aria-checked via parent walk); no raw keys |
| FE-TC-33 | Lesson load error / 404 | **PASS** | Navigate to lesson 99999 → lesson-404 or lesson-error visible; back CTA present; no quiz entry; text localized |
| FE-TC-34 | Empty lesson → empty tile | **PASS** | StartAttempt intercept returning questions:[] → lesson-empty visible; quiz-question-card absent; back CTA present; localized text |
| Bonus | correctAnswer absent from startAttempt payload | **PASS** | Questions in startAttempt response contain no `correctAnswer` field (security check) |

## Defects filed

| Defect ID | Case | Severity | Summary | Status |
|---|---|---|---|---|
| UI-BUG-01 | FE-TC-06, 07, 08, 24, 25, 26, 28, 32 | Medium | **MCQOption testID placed on inner Stack (content div), not on outer Pressable**. The Pressable carries `aria-checked`, `role="radio"`, `aria-label`, and `aria-disabled`. The inner Stack has `testID`. Callers using `getByTestId('quiz-mcq-option-X').getAttribute('aria-checked')` get `null` because the attribute is on the parent. Required `page.evaluate()` parent-walk in all 8 affected tests. Fix: move `testID` to the `Pressable` in `MCQOption/index.tsx` (and conditionally also on the locked `Stack` wrapper). | Reported — back to frontend |
| DEF-P205FE-01 | FE-TC-23 | High | **BE grades MCQ correct answer as isCorrect=false** — CorrectAnswer stored as jsonb-encoded `"\"6\""` but options comparator compares raw "6" → mismatch → always incorrect. Already known/tagged. Blocks FE-TC-23 (auto-advance on correct). | Pre-existing, not re-filed |

## Blocked-case ledger

| Case | Blocker | Resolution path |
|---|---|---|
| FE-TC-09, FE-TC-10, FE-TC-11 | No seeded TrueFalse question (questionType=2); only lesson 1 has questions and it is MCQ-only | Seed a TrueFalse question to any available lesson |
| FE-TC-12, FE-TC-13, FE-TC-14, FE-TC-15 | No seeded FillInBlank question (questionType=4); only lesson 1 has questions and it is MCQ-only | Seed a FillInBlank question to any available lesson |
| FE-TC-16, FE-TC-17 | No seeded Matching question (questionType=3); BE has zero Matching questions in all Grade-1 lessons | BE seeds a Matching question, or harness exposes a known Matching lesson ID |
| FE-TC-18 | Real Matching renderer does not exist this wave (MatchingPanel is a stub) | Build drag-pair renderer when BE seeds Matching (Design Spec §12) |
| FE-TC-23 | DEF-P205FE-01: all answers return isCorrect=false due to jsonb encoding bug; correct-answer auto-advance path unreachable | Fix BE comparator to compare unwrapped string; then verify correct submit returns isCorrect=true |
| FE-TC-29 | No seeded TrueFalse question (same as FE-TC-09) | Same as FE-TC-09–11 |
| FE-TC-30 | No seeded FillInBlank question (same as FE-TC-12) | Same as FE-TC-12–15 |

## Notes / observations

1. **Question types reachable in this run:** MCQ only (questionType=1). TrueFalse (2), FillInBlank (4), and Matching (3) are not seeded in any available Grade-1 lesson.

2. **UI-BUG-01 (MCQOption testID placement) — impact:** All 8 MCQ a11y/state tests required a `page.evaluate()` DOM-parent-walk workaround to read `aria-checked`, `role`, `aria-label`, and `aria-disabled`. This is a test-quality and semantic-HTML correctness issue. The testID should be on the `Pressable` (the accessible element), not the content `Stack`. The component declares `testID?: string` and passes it to the inner `Stack` — this should be moved to the outer interactive element. Without this fix, any consuming test harness that does `getByTestId('quiz-mcq-option-X').click()` works correctly (click propagates through DOM), but `getByTestId('quiz-mcq-option-X').getAttribute('aria-checked')` returns null.

3. **DEF-P205FE-01 impact:** All quiz submit paths in this test run returned `isCorrect=false` (incorrect feedback path). The incorrect path (`feedback-continue` → advance/complete) is thoroughly exercised and works correctly. The correct path (auto-advance after 800ms, no Next CTA) cannot be tested until DEF-P205FE-01 is fixed.

4. **RTL confirmed:** Arabic is the default locale; `document.documentElement.dir === 'rtl'` confirmed in FE-TC-28. MCQ option labels are in Arabic (`خيار A: 4`). No raw i18n keys leaked.

5. **FE-TC-33 implementation note:** Route interception of `**/Lessons/1**` was replaced by navigating to lesson ID 99999 (non-existent) to get a natural 404 from the backend. This is more reliable than intercepting a lesson that may already be cached from `seedAndSignInAsChild`.

6. **Hearts widget confirmed:** Hearts renders with `aria-label="٣ قلوب"` (Arabic, 3/3 static hearts). No `testID` prop on the Hearts component — recommend adding `quiz-hearts` testID for future tests.
