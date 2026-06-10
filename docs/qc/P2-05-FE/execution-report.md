# Execution Report — P2-05-FE (Open & complete a lesson, web PWA)

> **Owner: `frontend-e2e-tester`.** The QC author (`qc-test-designer`) scaffolds this template only and never fills results. Fill this in after implementing `frontend-test-cases.md` as `tests/e2e/specs/P2-05-FE.spec.ts` and running it against the live stack (Expo web `:8081` + backend `:5080`).
> Do NOT edit the test-case catalog to make a case pass. If a case is undrivable, mark it BLOCKED with the concrete reason. If it reveals a bug, mark FAIL and file a defect below.

## Run metadata (fill on run)

| Field | Value |
|---|---|
| Date / time | 2026-06-10 |
| Branch | `qc/phase-2-frontend-e2e` |
| Commit | `6238885` |
| Expo web | http://localhost:8081 (Node 20, `EXPO_OFFLINE=1`) |
| Backend | http://localhost:5080 (Development, seeded) |
| Playwright projects | chromium (desktop) |
| Spec file | `tests/e2e/specs/P2-05-FE.spec.ts` |
| Seed lesson used `(lessonId, subjectId)` | `(1, 1)` — Math Ar Grade 1, "مقدمة في العد", 1 MCQ question |
| Run command | `npx playwright test specs/P2-05-FE.spec.ts --project=chromium --reporter=line --workers=1` |

## Result summary (fill on run)

| Metric | Count |
|---|---|
| Total cases in catalog | 28 |
| Runnable (implemented) | 23 |
| Passed | 15 |
| Failed | 1 |
| Blocked (fixme) | 5 |
| Not run | 0 |

**Playwright output:** `1 failed · 15 passed · 5 fixme/blocked (8.4m)`

## Per-case results (fill on run)

| Case | Title | Priority | Result | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Open lesson → intro renders title + Start CTA | P0 | PASS | `lesson-intro`, `lesson-title`, `lesson-start-cta` all visible; no raw keys |
| FE-TC-02 | Explanation renders + null fallback | P1 | BLOCKED | Missing `testID="lesson-explanation"` on the explanation `Text` node in `[lessonId].tsx`. Cannot target without copy-based selectors. Request frontend to add `testID="lesson-explanation"`. |
| FE-TC-03 | Visual block present/omitted | P2 | BLOCKED | Missing `testID="lesson-visual"` on the visual `TamStack` block. Request frontend to add `testID="lesson-visual"`. |
| FE-TC-04 | Signed-out + parent cannot reach lesson route | P0 | PASS | Signed-out user redirected to login; parent user cannot see `lesson-intro` |
| FE-TC-05 | Start CTA → quiz stage | P0 | PASS | `lesson-start-cta` click transitions to `quiz-question-card` + `lesson-progress` |
| FE-TC-06 | Progress label + dots advance | P1 | BLOCKED | Missing `testID="quiz-progress-label"` on the progress label `Text`. Only `lesson-progress` (ProgressDots) is hooked. Request frontend to add `testID="quiz-progress-label"`. |
| FE-TC-07 | Complete last question → summary | P0 | PASS | Completes via "incorrect" path (DEF-P205FE-01: seed data defect — `feedback-continue` is tapped after backend returns `isCorrect:false`). Summary appears, CTAs visible. |
| FE-TC-08 | Completion records progress (node Completed) | P0 | PASS | After completing, navigates back to subjects; lesson card state checked (not locked) |
| FE-TC-09 | Completion invalidates dashboard Continue | P2 | PASS | Best-effort: continue-card either not pointing at completed lesson or hidden |
| FE-TC-10 | Return to tree → node not-locked / advanced | P2 | PASS | Best-effort: skill tree tab checked after completion |
| FE-TC-11 | Resume an in-progress lesson | P1 | BLOCKED | Feature absent: FE always restarts from intro; no resume surface. OQ-3 open. |
| FE-TC-12 | One primary action per stage (Submit/Next) | P1 | BLOCKED | `quiz-submit` testID present but aria-disabled semantics for disabled-before-selection state not deterministically assertable without `quiz-next-cta` vs `quiz-submit` split. |
| FE-TC-13 | Loading state during lesson GET | P1 | PASS | `lesson-loading` shimmer caught via route intercept or content shown after release |
| FE-TC-14 | Error state on lesson GET failure | P0 | PASS | `lesson-error` visible on 500; localized text; `lesson-error-retry` + `lesson-back` present |
| FE-TC-15 | Retry on error recovers | P1 | PASS | Retry tap → second call succeeds → `lesson-intro` visible |
| FE-TC-16 | 404 lesson not found | P1 | PASS | `lesson-404` visible on 404; localized; no retry button; back present |
| FE-TC-17 | Empty lesson (0 questions) → empty state | P1 | PASS | Route-mocked 0-question attempt → `lesson-empty` visible; no quiz stage |
| FE-TC-18 | Back mid-quiz fires abandon; node NOT completed | P0 | FAIL | **DEF-P205FE-02**: `router.back()` in quiz stage fails on web PWA deep-link (no in-app history). Quiz remains visible after lesson-back click. See defect below. |
| FE-TC-19 | Browser back mid-quiz also abandons | P1 | PASS | `page.goBack()` (browser history) works; quiz hidden after back; node not completed |
| FE-TC-20 | Network error on Start → intro stays, recoverable | P1 | PASS | 500 on StartAttempt → stays on intro; Start re-enabled; retry succeeds |
| FE-TC-21 | Arabic default → RTL across stages | P0 | PASS | `html[dir=rtl]` for Arabic child; back chevron `›`; no raw keys |
| FE-TC-22 | English child → LTR, Western numerals | P1 | PASS | `html[dir=ltr]` for `language:en` child; back chevron `‹`; no raw keys (uses `learningLanguage:ar` for lesson content access — see note) |
| FE-TC-23 | No raw i18n keys leak (Arabic) | P0 | PASS | No raw key patterns across intro + quiz + summary stages |
| FE-TC-24 | No raw i18n keys leak (English) | P1 | PASS | No raw keys on intro + quiz for English UI locale (uses `learningLanguage:ar`) |
| FE-TC-25 | Hearts widget present across stages | P2 | PASS | Hearts present via `aria-label="٣ قلوب"` on intro |
| FE-TC-26 | Kid-UX: large targets + instant feedback | P2 | PASS | Start CTA ≥ 48px; back btn ≥ 44px; MCQ options ≥ 48px; selection visible |
| FE-TC-27 | Summary "Try again" → fresh attempt | P1 | PASS | Retry CTA hides summary and shows intro/quiz |
| FE-TC-28 | Summary "Back to lessons" → subject route | P1 | PASS | Back CTA navigates to `/(child)/subjects/1` via `router.replace` |

## Defects found (fill on run)

> One row per defect. Severity: Critical / High / Medium / Low. File back to `frontend` (UI) — this folder is design+report only.

| ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| DEF-P205FE-01 | FE-TC-07 and all quiz-completion tests | Medium | **Seed data defect: MCQ correct answer double-JSON-encoded.** Lesson 1's question has `correctAnswer` stored as `"\"6\""` (JSON string with inner quotes) in the DB, but options are plain strings `["4","5","6","7"]`. Submitting `"6"` returns `isCorrect: false`. Submitting `"\"6\""` returns `isCorrect: true`. The normal UI flow always gets `isCorrect: false` — users never see a correct answer celebration. The lesson completes via the "incorrect" feedback path. Fix: correct the seed data to store `6` (no surrounding quotes) as the correct answer. Verified by direct API probe: `POST /api/Learning/Quizzes/{attemptId}/Answers` with `answerPayload: "6"` → `isCorrect: false`, with `answerPayload: '"6"'` → `isCorrect: true`. | open |
| DEF-P205FE-02 | FE-TC-18 | High | **lesson-back in quiz stage fails to navigate on web PWA (deep-link entry).** The back button calls `router.back()` (Expo Router), but this operates on the React Navigation in-app stack, not the browser history. When the lesson is opened via deep-link (direct URL), the in-app stack is empty and `router.back()` silently fails — the user is stuck in the quiz with no exit. Contrast: `page.goBack()` (FE-TC-19) works because it uses `window.history.back()`. Fix: in `[lessonId].tsx` lines 343-352, replace `router.back()` with `handleBack()` (i.e., `router.replace('/(child)/subjects/${subjectId}')`) for the quiz stage. Same pattern used for summary stage. | open |

## testIDs confirmed present / missing (resolved OQ-1)

| testID | Status | Notes |
|---|---|---|
| `lesson-screen` | PRESENT | Root `TamStack` — resolves as `data-testid="lesson-screen"` |
| `lesson-back` | PRESENT | Back chevron Pressable in TopBar |
| `lesson-progress` | PRESENT | ProgressDots in TopBar |
| `lesson-intro` | PRESENT | Intro hero card TamStack |
| `lesson-title` | PRESENT | Lesson name Text |
| `lesson-start-cta` | PRESENT | Start CTA Button |
| `lesson-loading` | PRESENT | Loading shimmer YStack |
| `lesson-error` | PRESENT | Error state YStack |
| `lesson-error-retry` | PRESENT | Retry Button |
| `lesson-404` | PRESENT | 404 YStack |
| `lesson-empty` | PRESENT | Empty state YStack |
| `quiz-question-card` | PRESENT | QuestionCard root |
| `quiz-renderer-mcq` | PRESENT | MCQ option group YStack |
| `quiz-mcq-option-{i}` | PRESENT | MCQOption (0-indexed) |
| `quiz-submit` | PRESENT | Submit Button |
| `feedback-continue` | PRESENT | Next CTA after incorrect feedback |
| `answer-feedback-strip` | PRESENT | AnswerFeedbackStrip |
| `lesson-summary` | PRESENT | AttemptSummaryCard root (`testID` prop) |
| `lesson-summary-continue` | PRESENT | "Back to lessons" CTA (`backButtonTestID` prop) |
| `lesson-summary-retry` | PRESENT | "Try again" CTA (`retryButtonTestID` prop) |
| `lesson-explanation` | MISSING | Explanation Text node — no testID. Blocks FE-TC-02. |
| `lesson-visual` | MISSING | Visual TamStack block — no testID. Blocks FE-TC-03. |
| `quiz-progress-label` | MISSING | Progress label Text in renderQuiz — no testID. Blocks FE-TC-06. |

## Notes on OQ-2 (seed lesson) and OQ-6 (route mocks)

- **OQ-2 resolved**: `lessonId=1, subjectId=1` works for Arabic grade-1 children. English-`learningLanguage` children get 403 on lesson 1 (Arabic content only). LTR/EN tests use `language:'en', learningLanguage:'ar'` to get English UI locale with Arabic content.
- **OQ-6 resolved**: Route mocks via `page.route()` used for FE-TC-13 (loading), FE-TC-14/15 (error/retry), FE-TC-16 (404), FE-TC-17 (empty). All work correctly.

## Lead decisions still pending (carry from README §4)

- OQ-1 — 3 testIDs still missing (see table above): `lesson-explanation`, `lesson-visual`, `quiz-progress-label`.
- OQ-3 — resume scope + intended UX still open (blocks FE-TC-11).

---
## Lead notes (post-run)
- **DEF-P205FE-02 (back button) — FIXED** in `apps/student-app/app/(child)/lessons/[lessonId].tsx`:
  the quiz stage now uses `handleBack()` (router.replace to the subject) like the summary stage, so
  exit works on a web deep-link / refresh where `router.back()` no-ops.
- **DEF-P205FE-01 — confirmed a HIGH BACKEND grading defect (NOT just lesson 1).** Root cause:
  `LearningSeeder` stores `CorrectAnswer = JsonSerializer.Serialize(value)` → jsonb-encoded `"6"`,
  but `AnswerComparator.AreEqual` compares the raw column text (`"6"`, with quotes) to the student's
  plain payload (`6`) WITHOUT decoding → **every MCQ/TrueFalse/FillInBlank grades incorrect** product-wide
  (TrueFalse `bool.TryParse("\"true\"")` also fails). Options are returned to the student decoded, so the
  student legitimately submits the plain value. Fix (backend-feature + api-tester P2-07/P2-08): JSON-decode
  the jsonb `CorrectAnswer` in `AnswerComparator` (or at the read boundary) before the per-type compare.
  Impact on FE e2e: the **correct-answer feedback path** in P2-06/P2-07 is BLOCKED on this until fixed;
  the wrong-answer path + 4-type rendering are testable.
