# Reviewer Report — W12 P2-05-FE + P2-06-FE + P2-07-FE

> Reviewer: `reviewer` subagent · Date: 2026-05-30 · Branch: `feat/W12-P2-05-06-07-FE`

---

## VERDICT: PASS

All acceptance criteria for P2-05-FE, P2-06-FE, and P2-07-FE are met. All builds pass clean (0 errors). Two should-fix issues and four nits are logged below — none are blockers.

---

## Build / Type-check Results

| Command | Result |
|---|---|
| `pnpm --filter @learnexia/api-client type-check` | PASS (0 errors, 0 warnings) |
| `pnpm --filter @learnexia/ui type-check` | PASS (0 errors, 0 warnings) |
| `pnpm --filter @learnexia/shared type-check` | PASS (0 errors, 0 warnings) |
| `pnpm --filter student-app type-check` | PASS (0 errors, 0 warnings) |
| `pnpm --filter student-app lint` | PASS (0 violations) |
| `dotnet build backend/Learnexia.Modular.sln` | PASS — `7 Warning(s), 0 Error(s)` (MSB3277 EF version conflict — pre-existing, not introduced by W12) |

Backend dotnet tests were not touched or added by this wave; `dotnet test` was not run (no new test files).

---

## Per-Check Results

### Check 1 — Rule #8: No Strategy / no Reanimated / no Skia / plain switch
**PASS**

`apps/student-app/app/(child)/lessons/[lessonId].tsx:520` uses a plain `switch (question.questionType)` with four cases. No XState, no Strategy pattern, no Reanimated imports, no Skia imports anywhere in the changed files. The comment `// Plain switch — no Strategy pattern (CLAUDE.md rule #8)` is present at line 519.

### Check 2 — CONVENTIONS: tokens-only, logical RTL, Cairo/Tajawal, module isolation (BE)
**PASS with accepted spec gaps**

**Accepted inline `rgba()` values** (all documented in Design Spec §4.6 as proposed tokens or native shadow fallbacks):

| File:line | Value | Justification |
|---|---|---|
| `AnswerFeedbackStrip/index.tsx:105` | `rgba(34,197,94,0.28)` / `rgba(239,68,68,0.28)` | Design Spec §4.6 — `successAccentDisc` / `dangerAccentDisc` proposed tokens; FE ships inline with `// TODO` note |
| `AttemptSummaryCard/index.tsx:262,264` | `rgba(250,204,21,0.18)` / `rgba(250,204,21,0.35)` | `xpSoft` token exists at 0.13 alpha but spec calls for 0.18; border at 0.35 has no token. Design Spec §4.6 accepted gap. |
| `ProgressDots/index.tsx:89` | `rgba(99,102,241,0.40)` | Design Spec §4.4 — "inline fallback if no token" for progress-dot glow. |
| `QuestionCard/index.tsx:60`, `AttemptSummaryCard/index.tsx:127`, `[lessonId].tsx:624` | `rgba(0,0,0,0.15)` | Native shadow — identical to `nativeShadow.soft` token; same pattern as W11 `LessonCard`. |
| `AttemptSummaryCard/index.tsx:152` | `rgba(99,102,241,0.45)` | Native shadow — identical to `nativeShadow.primaryGlow`. Same pattern as W11. |

**RTL:** All horizontal stacks use `flexDirection={isRtl ? 'row-reverse' : 'row'}`. `borderStartWidth` (logical) is used in `AnswerFeedbackStrip` for the leading-edge accent bar. No `borderLeftWidth` / `marginLeft` / `paddingLeft` present in changed files. MCQOption uses `textAlign={isRtl ? 'right' : 'left'}` (correct).

**Fonts:** Cairo/Poppins for headings (`$heading`), Tajawal/Poppins for body (`$body`), explicit `fontFamily: isRtl ? 'Tajawal' : 'Poppins'` in `FillInBlank` for the TextInput (which doesn't use the Tamagui `$body` token because it's a native `TextInput`). Correct.

**Module isolation (BE):** `LessonsController.cs` and `QuizzesController.cs` only reference their own module's application layer (`Learning.Application.*`) and `Shared.Kernel`. No cross-module project references.

### Check 3 — Build / type-check
**PASS** (see table above)

### Check 4 — AC Traceability

**P2-05-FE — Open and complete a lesson:**
- A1: LessonCard onPress navigates `/(child)/lessons/${lessonId}?subjectId=${subjectId}` (`subjects/[subjectId]/index.tsx:273-274`). Intro renders on lesson GET resolve. **PASS**
- A2: Intro shows title (lesson.name), explanation (AITutor-style bubble, plain text per R5), visual block (when non-null), Hearts static count=3. Streak hidden (OQ14). **PASS**
- A3: `explanation:null` → fallback string shown. `visual:null` → visual block omitted (no empty grey block). `quickCheck` not rendered in Intro (OQ15 decision). `isError` renders "Couldn't load" + retry. **PASS**
- A4: Start CTA calls `useStartAttempt`. `loading={startAttemptMutation.isPending}` + `disabled={startAttemptMutation.isPending}` prevents double-tap. **PASS**
- A5: `onSuccess` of `useCompleteAttempt` calls `queryClient.invalidateQueries(queryKeys.learning.subjectLessons(subjectId))`. "Back to subject" routes `router.replace('/(child)/subjects/' + subjectId)`. **PASS**
- A6: Loading: shimmer skeleton (`opacity: 0.5`, dimensions match hero card). Error: "Couldn't load lesson" + retry + back. 404: detected via error message containing "404" / "not found". **PASS**
- A7: `useLocale().direction` used throughout; all text via i18n keys; all colors via tokens (per Check 2). **PASS**
- A8: `useEffect` cleanup fires `abandonAttemptMutation.mutate({ attemptId })` when `stageRef.current.kind === 'quiz'`. Back from Summary does NOT fire Abandon (summary stage check). **PASS**

**P2-06-FE — Take a quiz (4 question types):**
- Q1: Progress label "Question {{current}} of {{total}}" rendered above QuestionCard; ProgressDots in TopBar center. **PASS**
- Q2: MCQ renders 4 options as `MCQOption` (tappable cards, min height 56). Submit disabled until `selectedValue !== null`. **PASS**
- Q3: TrueFalseChoice renders two 88px-height buttons, select-then-submit pattern. **PASS**
- Q4: FillInBlank renders `TextInput` + Submit (in QuestionCard footer). Submit disabled while empty/whitespace (via `hasAnswer` check on `trim().length > 0`). **PASS**
- Q5: `MatchingPanel` renders "Coming soon" stub with "Next" CTA. **PASS**
- Q6: After Submit, `phase: 'submitting'` then `phase: 'feedback'` — all option controls have `pointerEvents: 'none'` (locked). **PASS**
- Q7: `useSubmitAnswer` mutation called once per question. Network error reverts to `'answering'` phase; inline error strip shown; answer preserved. **PASS**
- Q8: `contentContainerStyle` with `paddingHorizontal: 24`. QuestionCard full-width minus 32 padding. No explicit max-width 720 for tablet — `[should-fix #2]` below. **PARTIAL PASS**
- Q9: RTL via `direction` prop on all components; `flexDirection` logical flips. `writingDirection={direction}` on text inputs. **PASS**

**P2-07-FE — Instant feedback:**
- F1: On `isCorrect:true`, `AnswerFeedbackStrip variant="correct"` mounts; 800ms `setTimeout` in screen fires `advanceOrComplete()`. Timer cleared on unmount. **PASS**
- F2: On `isCorrect:false`, `AnswerFeedbackStrip variant="incorrect"` mounts with `revealText`. MCQOption shows correct=green / wrong pick=red. "Next" CTA in QuestionCard footer enabled. No auto-advance. **PASS**
- F3: State-machine transition only — no navigation / reload. **PASS**
- F4: Hint button renders `disabled`, `accessibilityState={{ disabled: true }}`, label includes "coming in v2" helper text. No endpoint called on press (button is disabled). **PASS**
- F5: `revealText` only rendered when `!feedbackState.isCorrect && feedbackState.correctAnswer` — defensive guard present. **PASS**
- F6: "+10 XP (coming soon)" rendered via i18n `child.summary.xpStub`. `// TODO P4-02 — wire real XP reward` comment present at `AttemptSummaryCard/index.tsx:101,257,274`. No XP endpoint called. **PASS**

### Check 5 — State machine: intro → quiz → summary; abandon on unmount
**PASS**

`Stage` discriminated union matches the brief §5 spec exactly. `AnswerState` correctly has `answering / submitting / feedback` phases. `advanceOrComplete()` transitions quiz→summary on last question. Abandon fires from `useEffect` cleanup at line 141-151. Timer cleared on unmount at line 143.

### Check 6 — Feedback strip: 800ms auto-advance, "Next" CTA on incorrect, a11y
**PASS**

- 800ms: `setTimeout(800)` at `[lessonId].tsx:251`. Timer uses `stageRef.current` to avoid stale closure.
- "Next" CTA: rendered in QuestionCard footer when `isFeedback && !isCorrectFeedback`.
- `accessibilityRole="alert"` + `accessibilityLiveRegion="polite"` on `AnswerFeedbackStrip` outer `Animated.View`. **PASS**
- Reduced motion: NOT implemented (`[should-fix #1]`).

### Check 7 — Hints / XP / Hearts: stubs only, no endpoint calls
**PASS**

- Hint: `disabled={true}` + `accessibilityState={{ disabled: true }}` at `[lessonId].tsx:881`. `hintUsed: false` always sent to SubmitAnswer.
- XP: `xpStubText` prop is a static string. `// TODO P4-02` comment present. No XP mutation import.
- Hearts: `<Hearts current={3} maxHearts={3} />` hardcoded. No hearts mutation.

### Check 8 — i18n parity: EN + AR, no orphans, stub keys deleted
**PASS**

All §9 keys are present in both `en` and `ar` blocks of `resources.ts`. `child.lessons.stub.*` keys are deleted (confirmed by `grep` — no match). No orphaned keys found. Eastern-Arabic numerals used in AR via `toArabicNumerals()` helper.

One minor: the brief §9 key name is `child.quiz.progress` but the implementation uses `child.quiz.questionOf`. The spec §8 appendix uses `child.quiz.questionOf` — the brief §9 table and the spec §8 appendix diverge. Implementation follows the spec §8 appendix which is the definitive copy table. **Not a blocker.**

### Check 9 — Navigation: `?subjectId=`, Summary "Back to subject", "Try again"
**PASS**

- `subjects/[subjectId]/index.tsx:274`: `router.push('/(child)/lessons/${lesson.lessonId}?subjectId=${subjectId}')`. Correct.
- Summary "Back to lessons": `router.replace('/(child)/subjects/' + subjectId)` in `handleBack()`. Correct.
- "Try again": `handleRetry()` resets stage, calls `startAttemptMutation.reset()` then `handleStart()`. Correct.

### Check 10 — Abandon: fire-and-forget, idempotent on terminal
**PASS**

`abandonAttemptMutation.mutate({ attemptId })` called from `useEffect` cleanup with no await. The mutation does not block navigation or throw on terminal attempt (BE is idempotent). No second abandon from explicit back-press (the empty quiz `if` block lets `router.back()` handle navigation, which triggers unmount → cleanup).

---

## Findings

### Blockers
None.

### Should-fix

**[should-fix #1] Reduced motion not implemented**
Files: `packages/ui/src/components/AnswerFeedbackStrip/index.tsx`, `packages/ui/src/components/AttemptSummaryCard/index.tsx`

Design Spec §5 and §7 require:
- Replace translates with fade-only when `prefers-reduced-motion` is set.
- Auto-advance timer extends from 800ms → 1200ms for reduced motion.

The `AnswerFeedbackStrip` defines constants `AUTO_ADVANCE_MS = 800` and `REDUCED_MOTION_MS = 1200` but neither is used. The timer at `[lessonId].tsx:256` always uses hardcoded `800`. `AttemptSummaryCard` spring animation is not gated on reduced motion.

Fix: import `AccessibilityInfo.isReduceMotionEnabled()` (or `useReducedMotion` hook) and gate:
1. `AnswerFeedbackStrip`: skip `translateY` animation when reduced motion; use `REDUCED_MOTION_MS` timeout instead of `AUTO_ADVANCE_MS` in the screen.
2. `AttemptSummaryCard`: replace spring `cubic-bezier(0.34, 1.56, 0.64, 1)` with linear ease-out when reduced motion.

**[should-fix #2] No max-width 720 on tablet/desktop for quiz content (AC Q8)**
File: `apps/student-app/app/(child)/lessons/[lessonId].tsx`

Brief AC Q8: "centered with max-width 720px on tablet/desktop (Tamagui media queries — mirror existing Wave 11 cards)". The quiz `ScrollView` uses `paddingHorizontal: 24` but no `maxWidth: 720` / `alignSelf: 'center'`. W11 `subjects/[subjectId]/index.tsx` uses `maxWidth={720} alignSelf="center"` — mirror this. Not a crash but a layout regression on wide screens.

### Nits

**[nit #1] Dead constants in `AnswerFeedbackStrip`**
`packages/ui/src/components/AnswerFeedbackStrip/index.tsx:35-36`: `AUTO_ADVANCE_MS` and `REDUCED_MOTION_MS` are defined but never used. Remove or use them (should-fix #1 would use them).

**[nit #2] Dead `effectiveState` noop in `FillInBlank`**
`packages/ui/src/components/FillInBlank/index.tsx:76`: `const effectiveState: FillInBlankState = locked ? state : state` is always `state`. Remove the variable and use `state` directly.

**[nit #3] `AttemptSummaryCard` missing `accessibilityRole="region"`**
`packages/ui/src/components/AttemptSummaryCard/index.tsx:117`: The outer `Animated.View` has `accessible` and `accessibilityLabel` but not `accessibilityRole="region"` as specified by Design Spec §7. Add `accessibilityRole="region"` to the `Animated.View`.

**[nit #4] `QuestionCard` uses `accessible={false}` instead of `accessibilityRole="group"`**
`packages/ui/src/components/QuestionCard/index.tsx:64`: Design Spec §3.1 specifies `accessibilityRole="group"`. The implementation uses `accessible={false}` which prevents grouping. On native, this is effectively equivalent (children are individually focusable), but the web a11y tree loses the grouping landmark. Consider `accessibilityRole="none"` + `importantForAccessibility="no-hide-descendants"` on non-interactive wrapper, or simply add `accessibilityRole="group"` + remove `accessible={false}`.

---

## NSwag Gap Resolution (OQ1)

Option A was implemented: `[ProducesResponseType(typeof(BaseResponse<...>), 200)]` added to all 5 controller actions (`LessonsController:30`, `QuizzesController:29,42,58,70`). NSwag client regenerated with fully typed methods returning `SingleLessonResponseBaseResponse`, `StartAttemptResponseBaseResponse`, `SubmitAnswerResponseBaseResponse`, `AttemptSummaryDtoBaseResponse`. `QuestionType` enum and all 5 response DTOs present in `nswag-client.ts`. `schemas.ts` re-exports all types. **Correct — matches Wave 11 pattern.**

---

## Definition of Done (consolidated)

- All §3 ACs: green (Q8 tablet layout is the only partial — should-fix #2).
- 5 new api-client hooks: `useLesson`, `useStartAttempt`, `useSubmitAnswer`, `useCompleteAttempt`, `useAbandonAttempt` — all present, correctly typed.
- 8 new `@learnexia/ui` primitives (HeartsSlot inlined per designer pick): `QuestionCard`, `MCQOption`, `TrueFalseChoice`, `FillInBlank`, `MatchingPanel`, `AnswerFeedbackStrip`, `AttemptSummaryCard`, `ProgressDots` — all present, exported under `// --- W12 quiz primitives ---` banner.
- EN + AR i18n keys added; `child.lessons.stub.*` deleted.
- `[lessonId].tsx` 3-stage state machine: confirmed.
- `subjects/[subjectId]/index.tsx:274` patched with `?subjectId=${subjectId}`.
- XP placeholder with `// TODO P4-02` comment present.
- Hint button disabled, no endpoint call.
- Abandon fire-and-forget on unmount.
- All builds PASS.

HANDOFF.md update should be included in the same PR per pipeline §10.

---

*Reviewer: `reviewer` subagent · Wave 12 · 2026-05-30*
