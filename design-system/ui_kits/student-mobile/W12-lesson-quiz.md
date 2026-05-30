# Design Spec — W12 Lesson Player + Quiz + Instant Feedback (student mobile)

> Wave 12 bundles **P2-05-FE** (open + complete a lesson), **P2-06-FE** (take a quiz — 4 question types), **P2-07-FE** (instant answer feedback). One route, one screen, one view-state machine. This is the design spec the `frontend` agent builds verbatim. Token-only, AR/RTL-first, dark-default. Mirrors the structure + grammar of `W11-subjects-tree.md`.

## 0. Source-of-truth pairs

| Stage / surface | LTR capture | RTL capture | Composing preview cards |
|---|---|---|---|
| Intro stage (lesson opener) | `design-system/screenshots/mobile/11-lesson.png` | `design-system/screenshots/mobile-ar/11-lesson.png` | `preview/components-tutor.html`, `preview/ar-tutor.html`, `preview/components-buttons.html`, `preview/components-hearts.html` (existing W11 primitive) |
| Quiz stage — MCQ | `design-system/screenshots/mobile/12-quiz.png` | `design-system/screenshots/mobile-ar/12-quiz.png` | `preview/components-lesson-card.html` (radius + chrome only), `preview/components-input.html` (option-style chip), `preview/components-buttons.html` |
| Quiz stage — TrueFalse, FillInBlank, Matching | (no dedicated capture — composed) | (no dedicated capture — composed) | derived from `preview/components-lesson-card.html` + `preview/components-input.html` + `preview/components-buttons.html` |
| Feedback strip (correct/incorrect) | derived (no dedicated capture in this wave; reward popup capture `mobile/13-reward.png` shown for reference only — NOT used as the strip chrome) | derived | derived — minimal token chrome; see §3.5 |
| Summary stage | derived (mirrors `preview/components-tutor.html` celebratory framing + `preview/components-buttons.html`) | derived | `preview/components-buttons.html`, `preview/components-badges.html` |

**Captures vs product overrides:**
- `12-quiz.png` shows a 4-option MCQ pattern with a progress dot row at the top and a hint/skip affordance — that is exactly the V1 we ship.
- `13-reward.png` (confetti + XP toast + mascot) is **W12 out-of-scope** — confetti deferred to W14 polish; the correct-answer feedback in this wave is the green strip + 800ms auto-advance only (Pipeline Brief §11, OQ10).
- `11-lesson.png` includes a mastery bar / streak chip / mascot in the header — we **render Hearts only** this wave (streak hidden per OQ14; mastery omitted per Pipeline Brief §11; mascot is the existing `AITutorBubble` placeholder).
- The reward popup primitive (`RewardPopup`) is NOT used this wave.

---

## 1. Surfaces & navigation flow

### Route — single screen, view-state machine

- **Route:** `apps/student-app/app/(child)/lessons/[lessonId].tsx` (replaces the 115-LOC stub).
- **Query string:** `?subjectId={id}` — passed from the Lessons tab in W11 so the Summary "Back to subject" CTA can navigate without a unit-to-subject lookup (Pipeline Brief §5, OQ6).
- **Stage type:**
  ```ts
  type Stage =
    | { kind: 'intro' }
    | { kind: 'quiz'; attemptId: number; questions: QuizQuestionDto[]; currentIndex: number; answerState: AnswerState }
    | { kind: 'summary'; summary: AttemptSummaryDto };
  ```
- **One primary action per stage:** Intro → "Start lesson". Quiz → "Check answer" (or "Next" when feedback is showing). Summary → "Back to lessons".

### Surface 1 — Intro stage

- **Layout (390 portrait):**
  - Top safe-area + 16px top padding.
  - **TopBar** (row, `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `justifyContent: 'space-between'`, `alignItems: 'center'`, 48px tall):
    - Leading: Back chevron — 44×44 hit area (≥48 with content), 24px glyph `$fg2`. LTR `‹`, RTL `›`. Tap → `router.back()`. NO Abandon fired (no attempt yet).
    - Center: empty (intro has no progress dots).
    - Trailing: `Hearts` widget (existing primitive — `count={3}`, `max={3}`, `size="sm"`). Static this wave.
  - 24px gap (`$space-6`).
  - **Hero card** (`Card variant="brandPanel"`, padding 24, gap 16, radius `$modal` 24, `$shadow-card`):
    - **Mascot slot:** 96×96 — render `AITutorBubble` in its compact "owl" placeholder layout, OR a centered hero glyph `🎯` 56px on `$primarySoft` 80×80 disc when the bubble is too verbose. **Designer pick: hero glyph 🎯** — the bubble is reserved for the explanation. Centered horizontally.
    - **Eyebrow:** "Lesson" / "درس" — 12/700 uppercase `$fg3`, tracking `0.04em`. Centered.
    - **Title:** lesson `name` — 24/800 Poppins/Cairo, `lineHeight: 1.2`, `$fg1`. Centered, `numberOfLines: 2`.
    - **AITutorBubble** (existing primitive, `variant="explain"`) — renders the `explanation` field as plain text (markdown rendering deferred per Pipeline Brief R5). When `explanation === null`: render the bubble with the fallback string `child.lessons.intro.aiBubbleFallback` ("I'll walk you through this — tap Start when you're ready." / "سأشرح لك — اضغط ابدأ عندما تكون مستعدًا.").
    - **Visual block:** when lesson `visual !== null`, render a 16:9 rounded-corner image placeholder, radius `$radius-card-inner` 14, bg `$cardSoft`, source = `visual`. When null, omit entirely (no empty grey block — saves vertical space).
  - 24px gap.
  - **Start CTA** — full-width `Button variant="primary" size="lg"` (radius `$button` 16, height 56, glow on focus). Label `child.lessons.intro.startCta` ("Start lesson" / "ابدأ الدرس"). `isLoading` while `useStartAttempt` is pending. Disabled state during pending is opacity 0.7 + spinner inside the button.
  - 24px bottom safe-area padding.

- **Empty-lesson state** (`StartAttempt` resolves with `questions.length === 0`):
  - Replace the Start CTA + visual block with a centered tile:
    - Emoji `📭` (semantic — "empty mailbox", brand law permits semantic emoji) 40px.
    - Title 18/700 `$fg2`: "This lesson has no quiz yet" / "لا توجد أسئلة في هذا الدرس بعد" (i18n `child.lessons.intro.noQuestions`).
    - Sub 14/500 `$fg3`: "Come back later — we're adding more soon." / "عُد لاحقًا — نضيف المزيد قريبًا."
    - `Button variant="ghost"` "Back to lessons" / "الرجوع إلى الدروس" (i18n `child.lessons.intro.back`).
  - The screen does NOT transition to the quiz stage when questions are empty.

- **Loading state** (Lesson GET in flight): single shimmer card placeholder, same dims as the hero card, no inner content. Mirrors W11 pattern.

- **Error state** (Lesson GET fails): centered "Couldn't load lesson" + `Button variant="primary"` "Try again" + `Button variant="ghost"` "Back to lessons". Uses the existing `Button` ghost variant.

- **404 state** (Lesson GET returns 404): centered "Lesson not found" / "الدرس غير موجود" + back CTA. Mirrors W11 Subject 404.

### Surface 2 — Quiz stage

- **Layout (390 portrait):**
  - Top safe-area + 16px top padding.
  - **TopBar** (same row as Intro):
    - Leading: Back chevron → fires `useAbandonAttempt(attemptId)` fire-and-forget on cleanup; navigates `router.back()`.
    - Center: `ProgressDots` (see §3.7) — current index filled, others outline.
    - Trailing: `Hearts` widget (`count={3}`, static).
  - 16px gap.
  - **Progress label** (above the question card): "Question {{current}} of {{total}}" / "سؤال {{current}} من {{total}}" — 12/700 uppercase `$fg3`, tracking `0.04em`, centered. AR uses Eastern-Arabic numerals (`٢ من ٥`).
  - 12px gap.
  - **QuestionCard** (`§3.1`):
    - Padding 20, gap 16, radius `$modal` 24, border 1px `$border`, bg `$card`, shadow `$shadow-card`.
    - **Question stem** (top of card): 18/700 Poppins/Cairo `$fg1`, `lineHeight: 1.4`. Renders `question.questionText`.
    - 16px gap.
    - **Type-specific renderer slot** (one of MCQ / TrueFalse / FillInBlank / Matching — see §3.2–§3.6 below).
    - 16px gap.
    - **AnswerFeedbackStrip slot** — empty during `phase: 'answering'`; populated during `phase: 'feedback'`. Inline within the card, NOT below it.
    - 16px gap.
    - **Bottom action row** (row, `justifyContent: 'space-between'`):
      - Leading: **Hint button** (disabled this wave) — `Button variant="ghost" size="sm"` label "Hint" / "تلميح" + helper text below `12/500 $fg4` "Hint coming in v2" / "التلميح قريبًا". `disabled={true}`, `accessibilityState={{ disabled: true }}`, no endpoint call. Glyph `💡` (semantic) prefix.
      - Trailing: **Submit / Next CTA** — `Button variant="primary" size="md"`. Label depends on phase:
        - `phase: 'answering'` → "Check answer" / "تحقّق", disabled until an answer is picked, scales fluidly to fit.
        - `phase: 'submitting'` → spinner inside button, label hidden, disabled.
        - `phase: 'feedback'` with `isCorrect: false` → "Next" / "التالي", enabled.
        - `phase: 'feedback'` with `isCorrect: true` → CTA is HIDDEN (auto-advance handles it).
  - 24px bottom safe-area padding.

- **Network-error state** (Submit fails): inline error strip ABOVE the action row inside the QuestionCard — 12/600 `$danger` on `$dangerSoft` (≈ `rgba(239,68,68,0.18)`) bg, 1px `$danger` border, radius `$radius-card-inner` 14, padding 10/12. Copy: "Couldn't check your answer — try again" / "تعذر التحقق — حاول مرة أخرى" (i18n `child.quiz.networkError`). The Submit CTA returns to enabled `Check answer` state; the answer selection is preserved.

- **Locked-after-submit:** during `phase: 'submitting'` and `phase: 'feedback'`, all answer controls become non-interactive — `pointerEvents: 'none'`, `opacity` unchanged on correct/incorrect chrome (state colors are the affordance), `accessibilityState={{ disabled: true }}` on each option. No press, no edit, no re-select.

### Surface 3 — Summary stage

- **Layout (390 portrait):**
  - Top safe-area + 16px top padding.
  - **TopBar** (same row):
    - Leading: Back chevron → `router.replace('/(child)/subjects/' + subjectId)`. NO Abandon fired (attempt is already Completed).
    - Center: empty (no progress).
    - Trailing: `Hearts` widget (`count={3}`).
  - 32px gap (`$space-8` — extra rhythm for celebratory framing).
  - **AttemptSummaryCard** (`§3.8`):
    - Padding 24, gap 20, radius `$modal` 24, border 1px `$borderStrong`, bg `$card`, shadow `$shadow-card`. Centered content.
    - **Mascot slot:** centered hero glyph `🏆` 56px on a 96×96 disc with `$gradXp` (90deg green→indigo) gradient bg, `$shadow-primary-glow` (per brand law: reward chrome on genuine wins). 200ms mascot rise animation on mount (see §6).
    - **Title:** "Lesson complete!" / "اكتمل الدرس!" — 24/800 Poppins/Cairo `$fg1`, centered. Exclamation permitted (genuine win).
    - **Score row** — 3-column row, `justifyContent: 'space-around'`, each column centered:
      - Score: number 28/800 tabular-nums `$success` + caption 12/500 `$fg3` "{{correct}} / {{total}}" — label below: "Correct" / "صحيحة".
      - Accuracy: number 28/800 tabular-nums `$primary` + caption "{{percent}}%" — label: "Accuracy" / "الدقة".
      - Duration: number 28/800 tabular-nums `$fg1` + caption "{{seconds}}s" — label: "Time" / "الوقت".
      - AR uses Eastern-Arabic numerals for the three numbers AND a leading `dir="ltr"` wrapper on the duration string (`٤٥ث`) to keep the unit suffix readable.
    - **XP placeholder** (Pipeline Brief §3 F6 + OQ12): a `Badge variant="xp"` (existing primitive — XP variant uses `$xp` `#FACC15` on `$xpSoft`) inline below the score row. Copy: "+10 XP (coming soon)" / "+١٠ نقطة خبرة (قريبًا)" (i18n `child.summary.xpStub`). MUST be rendered with the in-source comment `// TODO P4-02 — wire real XP reward`. NO endpoint call.
  - 24px gap.
  - **CTA stack** (column, gap 12):
    - **Primary:** `Button variant="primary" size="lg"` full-width "Back to lessons" / "الرجوع إلى الدروس" (i18n `child.summary.backToSubject`). Route: `router.replace('/(child)/subjects/' + subjectId)`.
    - **Secondary:** `Button variant="ghost" size="lg"` full-width "Try again" / "حاول مجددًا" (i18n `child.summary.tryAgain`). Resets all stage state and re-fires `useStartAttempt` (creates a fresh attempt — existing is terminal).
  - 24px bottom safe-area padding.

- **NO confetti this wave** (OQ10). NO `RewardPopup`. NO XP toast endpoint call.

---

## 2. State design — composition rules

### MCQOption (4 states)

| State | Source | Background | Border | Title color | Side glyph | Hover (web) | Press |
|---|---|---|---|---|---|---|---|
| **default** | `answerState.phase==='answering' && selectedValue !== label` | `$card` (`#1E293B`) | 1px `$border` | `$fg2` | radio outline disc 20×20, 2px `$borderStrong`, hollow | brighten 8% bg → `$cardSoft`, scale 1.02 160ms | scale 0.95 80ms |
| **selected** | `answerState.phase==='answering' && selectedValue === label` | `$primarySoft` (`rgba(79,70,229,0.18)`) | 2px `$primary` | `$fg1` | radio disc 20×20, 2px `$primary`, inner 10×10 `$primary` dot | brighten 8% | scale 0.95 80ms |
| **correct** | `phase==='feedback' && (label === correctAnswer)` (regardless of who picked) | `$successSoft` (`rgba(34,197,94,0.18)`) | 2px `$success` | `$fg1` | ✓ glyph 18px, `$success` | none (locked) | none (locked) |
| **incorrect** | `phase==='feedback' && isCorrect===false && label === selectedValue` (the user's wrong pick) | `$dangerSoft` (`rgba(239,68,68,0.18)`) | 2px `$danger` | `$fg1` | ✕ glyph 18px, `$danger` | none (locked) | none (locked) |
| **locked-default** | `phase==='feedback'` AND not correct AND not the user's pick | `$card` | 1px `$border` | `$fg3` (dimmed) | radio outline disc, `$fg4` | none (locked) | none (locked) |

**Rule:** every option becomes non-interactive on `phase==='feedback'`. The correct option ALWAYS shows green (even if the user picked something else). The user's wrong pick shows red. Untouched non-correct options dim to `locked-default`.

### TrueFalseChoice (3 states per side)

A pair of large 50/50 buttons. State map mirrors `MCQOption` but with the two sides only:

| Phase | Left ("True" / "صحيح") | Right ("False" / "خطأ") |
|---|---|---|
| `answering`, none selected | both `default` chrome | both `default` chrome |
| `answering`, value=true | `selected` chrome | `default` chrome |
| `feedback`, correctAnswer="true", user picked "true" | `correct` chrome | `locked-default` |
| `feedback`, correctAnswer="true", user picked "false" | `correct` chrome | `incorrect` chrome |
| `feedback`, correctAnswer="false", user picked "true" | `incorrect` chrome | `correct` chrome |
| `feedback`, correctAnswer="false", user picked "false" | `locked-default` | `correct` chrome |

Each side: 50% width, height 88, radius `$button` 16, padding 16, centered glyph (✓ for True, ✕ for False, 28px) above a label 16/800. 12px gap between the two cards. Same chrome tokens as MCQOption.

### FillInBlank (3 states)

A row: TextField (flex 1) + Submit button. The Submit button is the QuestionCard's primary action, NOT a separate button — but the helper text + lock chrome lives in the field.

| Phase | Field bg | Field border | Field text | Submit |
|---|---|---|---|---|
| `answering`, value empty | `$bg` (`#0F172A`) | 1px `$border` | `$fg1` typing color, `$fg4` placeholder | "Check answer" disabled |
| `answering`, value non-empty | `$bg` | 1.5px `$primary` (focused) | `$fg1` | "Check answer" enabled |
| `feedback`, isCorrect=true | `$successSoft` | 1.5px `$success` | `$fg1` (the user's text) | hidden (auto-advance) |
| `feedback`, isCorrect=false | `$dangerSoft` | 1.5px `$danger` | `$fg1` (the user's text, struck-through? **NO** — plain) + below: reveal strip with correct answer | "Next" enabled |
| `feedback` (any) | `editable: false`, `pointerEvents: 'none'` |  |  |  |

Placeholder: "Type your answer" / "اكتب إجابتك" (i18n `child.quiz.fillPlaceholder`). `autoCorrect={false}`, `autoCapitalize="none"`, `returnKeyType="done"` — pressing Return triggers Submit when enabled.

### MatchingPanel (stub — single state)

Renders a single muted tile inside the QuestionCard:
- Glyph `🧩` 32px (semantic — puzzle/matching) centered.
- Title 16/700 `$fg2` "Matching questions coming soon" / "أسئلة المطابقة قريبًا" (i18n `child.quiz.matchingStub`).
- Sub 12/500 `$fg3` "Tap Next to skip" / "اضغط التالي للتخطي".
- The QuestionCard's Submit CTA becomes "Next" (enabled), and the answer payload sent to the BE is the empty string `""`. (BE has zero matching questions seeded today — Pipeline Brief R4 — so this branch is defensive.)
- No states. No hover. No motion.

### AnswerFeedbackStrip (2 variants)

| Variant | Background | Border (leading edge) | Glyph | Title | Reveal text | Auto-dismiss | CTA |
|---|---|---|---|---|---|---|---|
| **correct** | `$successSoft` (`rgba(34,197,94,0.18)`) | 3px `$success` leading-edge bar (`borderLeftWidth` LTR / `borderRightWidth` RTL → use logical `borderStartWidth`) | ✓ 20px `$success` | "Great job!" / "أحسنت!" (i18n `child.feedback.correct`) — 16/800 `$fg1` | none | YES — 800ms after enter | none |
| **incorrect** | `$dangerSoft` (`rgba(239,68,68,0.18)`) | 3px `$danger` leading-edge bar (logical) | ✕ 20px `$danger` | "Not quite" / "ليست الإجابة الصحيحة" (i18n `child.feedback.incorrect`) — 16/800 `$fg1` | "Correct answer: {{answer}}" / "الإجابة الصحيحة: {{answer}}" (i18n `child.feedback.correctAnswer`) — 14/500 `$fg2`, `numberOfLines: 2`. | NO | "Next" — handled by the QuestionCard primary CTA, NOT inside the strip. |

Strip layout: row, padding 12/16, gap 12, radius `$radius-card-inner` 14. Glyph in a leading 32×32 disc with bg = `$success` / `$danger` at 0.18 alpha, fg = full token color. Title + reveal stack to the trailing side of the glyph (flex 1).

**RTL note:** `direction` prop drives `borderStartWidth` (not `borderLeftWidth`) so the leading-edge bar always sits on the reading-leading edge. Reveal text wraps in `dir={direction}`.

### ProgressDots (single state — current vs others)

- Row, gap 6, centered horizontally.
- Each dot:
  - **filled (current):** 8×8 disc, bg `$primary` (`#4F46E5`), no border. Subtle glow `$shadow-primary-glow` at 0.4 intensity.
  - **outline (other):** 6×6 disc, transparent bg, 1.5px `$borderStrong` border. NO motion.
- **RTL:** the row uses `flexDirection={isRtl ? 'row-reverse' : 'row'}` so the "current" dot reads from the reading-leading side. The dots themselves do not flip.
- Max total = 12. Beyond 12, the row truncates to `· · · ⬤ · · ·` style (3 dots either side of current) — defensive; BE typically returns ≤10 questions per lesson.

---

## 3. Net-new primitives — `@learnexia/ui` spec

All under `packages/ui/src/components/<Name>/index.tsx` with the standard barrel re-export under a `// --- W12 quiz primitives ---` banner in `packages/ui/src/index.ts`. Tokens only, logical RTL, kid-a11y baseline.

### 3.1 `QuestionCard`

**Composed from:** `preview/components-lesson-card.html` (card chrome + radius), `screenshots/mobile/12-quiz.png` (composition).

**Props (TS):**
```ts
export interface QuestionCardProps {
  /** The question stem text, already localized. */
  questionText: string;
  /** Slot for the type-specific renderer (MCQOption list / TrueFalseChoice / FillInBlank / MatchingPanel). */
  children: React.ReactNode;
  /** Slot for the AnswerFeedbackStrip — rendered between content and footer when present. */
  feedback?: React.ReactNode;
  /** The Submit / Next CTA (already configured by the parent screen). */
  submitButton?: React.ReactNode;
  /** Hint affordance (disabled this wave). */
  hintButton?: React.ReactNode;
  /** Inline network-error message strip — null/undefined hides it. */
  errorMessage?: string | null;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions:**
- Outer card: full-width minus 32 horizontal screen padding, padding 20, gap 16 (column), radius `$modal` 24, border 1px `$border`, bg `$card`, shadow `$shadow-card`.
- Question stem: 18/700 Poppins/Cairo `$fg1`, `lineHeight: 1.4`, `textAlign: 'start'`.
- Footer row: row, `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `justifyContent: 'space-between'`, `alignItems: 'center'`, gap 12. Hint on the leading edge, Submit on the trailing edge.
- Error message strip: above the footer row, padding 10/12, radius `$radius-card-inner` 14, 1px `$danger`, bg `$dangerSoft`, 12/600 `$danger`.

**A11y:** `accessibilityRole="group"` (web/native role for the question stem + answer affordances). Children carry their own roles.

---

### 3.2 `MCQOption`

**Composed from:** `preview/components-lesson-card.html` chrome + `preview/components-input.html` (focus/radio shape).

**Props:**
```ts
export interface MCQOptionProps {
  /** Already-localized option label. */
  label: string;
  /** Option state — drives chrome. */
  state: 'default' | 'selected' | 'correct' | 'incorrect' | 'locked-default';
  /** Press handler. Locked states are non-interactive (`pointerEvents: 'none'`). */
  onPress?: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized a11y label, e.g. "Option B, 4, selected". */
  accessibilityLabel: string;
  testID?: string;
}
```

**Dimensions:**
- Outer card: full-width, padding `14px 16px`, gap 12, radius `$button` 16 (deliberately smaller than the QuestionCard's `$modal` 24 — options nest inside the card), 1px border per state (§2), bg per state (§2).
- Row layout: `flexDirection={isRtl ? 'row-reverse' : 'row'}`, `alignItems: 'center'`, gap 12.
- Leading: 20×20 radio disc (chrome per §2).
- Body: flex 1, label 16/600 (selected/correct/incorrect: 16/700) `$fg1` per state, `textAlign: 'start'`, `numberOfLines: 2`.
- Trailing (`correct` + `incorrect` only): ✓ / ✕ glyph 18px in the state color.
- Min height: 56 (≥48 a11y floor).

**A11y:**
- `accessibilityRole="radio"`, parent list wraps in `accessibilityRole="radiogroup"`.
- `accessibilityState={{ checked: state==='selected' || state==='correct' || state==='incorrect', disabled: state==='locked-default' || state==='correct' || state==='incorrect' }}`.
- `accessibilityLabel` (caller composes — e.g. `"Option B: ٤، اختيارك، إجابة خاطئة"`). After Submit, screen-reader hears state — "Option B, incorrect" or "Option C, correct answer".

---

### 3.3 `TrueFalseChoice`

**Composed from:** mirror MCQOption visuals, applied to a 50/50 pair.

**Props:**
```ts
export interface TrueFalseChoiceProps {
  /** Selected value or null if untouched. */
  value: boolean | null;
  /** Change handler — fires on tap during `answering` phase. */
  onChange: (next: boolean) => void;
  /** Overall state of the question — drives the chrome composition (see §2). */
  phase: 'answering' | 'feedback';
  /** When phase==='feedback', the correct answer to highlight green. */
  correctAnswer?: boolean | null;
  /** When phase==='feedback', the user's actual pick (used to mark the wrong choice red). */
  selectedAnswer?: boolean | null;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized labels — default to `child.quiz.true` / `child.quiz.false`. */
  trueLabel?: string;
  falseLabel?: string;
  testID?: string;
}
```

**Dimensions:**
- Outer row: `flexDirection={isRtl ? 'row-reverse' : 'row'}`, gap 12, full-width.
- Each side: flex 1, height 88, padding 16, radius `$button` 16, alignItems centered, gap 8.
- Glyph centered above label — ✓ for True / ✕ for False, 28px in the chrome color per state.
- Label 16/800 Poppins/Cairo `$fg1`. EN: "True" / "False". AR: "صحيح" / "خطأ".
- State chrome: identical token map to MCQOption per side (§2).
- Min tap target: 88×~163 (far above the 48 floor).

**A11y:**
- Two `accessibilityRole="radio"` children inside a `radiogroup`.
- `accessibilityState={{ checked, disabled }}` driven by phase + value.
- `accessibilityLabel` per side: "True, selected" / "False, correct" / etc.

---

### 3.4 `FillInBlank`

**Composed from:** existing `TextField` primitive (re-uses chrome).

**Props:**
```ts
export interface FillInBlankProps {
  value: string;
  onChange: (next: string) => void;
  /** Returns 'true' when the field has non-whitespace content (used by the parent to gate the Submit CTA). */
  isReady?: boolean;
  /** Drives chrome — see §2 state table. */
  state: 'default' | 'focused' | 'correct' | 'incorrect';
  /** Locked once feedback shows — non-editable + non-focusable. */
  locked?: boolean;
  /** Optional placeholder override; defaults to i18n `child.quiz.fillPlaceholder`. */
  placeholder?: string;
  /** Optional submit trigger from keyboard "Done" key. */
  onSubmitEditing?: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions:**
- Full-width TextField wrapper (re-uses `TextField` primitive), no inline submit button (submit lives in QuestionCard footer).
- Min height 56 (matches MCQOption floor; TextField default is 48 — bump via `size="lg"` prop).
- States via the existing TextField `state` prop where possible; otherwise inline `borderColor` / `backgroundColor` overrides driven by `state`.
- `editable={!locked}`, `selectTextOnFocus={!locked}`.
- AR: `textAlign: 'right'`, `writingDirection: 'rtl'` (RN), Tajawal body font.

**A11y:**
- `accessibilityRole="text"` (TextField default).
- `accessibilityLabel`: "Answer field" / "حقل الإجابة".
- `accessibilityState={{ disabled: !!locked }}`.

---

### 3.5 `AnswerFeedbackStrip`

**Composed from:** derived chrome — minimal new visual language.

**Props:**
```ts
export interface AnswerFeedbackStripProps {
  variant: 'correct' | 'incorrect';
  /** Required when variant='incorrect' — already localized + interpolated. */
  revealText?: string;
  /** Optional "Next" handler — when omitted, the parent renders its own CTA (recommended pattern for W12). */
  onNext?: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions:**
- Row, padding `12px 16px`, gap 12, radius `$radius-card-inner` 14, `alignItems: 'flex-start'`.
- Background per variant (§2.5 table).
- Leading-edge bar: 3px `$success` (correct) or `$danger` (incorrect) — use logical `borderStartWidth: 3` so it auto-flips in RTL.
- Leading glyph disc: 32×32 radius pill, bg = variant-soft (already the strip bg — flatten to a darker tint `$success` / `$danger` at 0.28 alpha for contrast), centered ✓ or ✕ 18px in the full token color.
- Body column (flex 1):
  - Title 16/800 `$fg1` (correct: "Great job!" / "أحسنت!"; incorrect: "Not quite" / "ليست الإجابة الصحيحة").
  - Reveal (incorrect only): 14/500 `$fg2`, `lineHeight: 1.4`, `numberOfLines: 2`, `dir={direction}`. EN: "Correct answer: {answer}"; AR: "الإجابة الصحيحة: {answer}".
- NO close button. NO "Next" CTA inside the strip (the QuestionCard owns it). Optional `onNext` callback is a future hook.

**Motion:**
- **Enter:** slide-down + fade in, 160ms `$ease-out`, `transform: translateY(-4 → 0)`, `opacity: 0 → 1`.
- **Exit (correct variant only):** 200ms after the 800ms auto-advance timer fires, fade out + slide-up 4px. Total visible time = 800ms.
- **Exit (incorrect variant):** persists until parent transitions away (which clears the slot).
- **Reduced motion:** fade only, no translate.

**A11y:**
- `accessibilityLiveRegion="polite"` on Android, `accessibilityLabel` reads the title + reveal verbatim, `accessibilityRole="alert"` on web.
- Auto-advance respects `prefers-reduced-motion` by replacing the 800ms timer with a 1200ms timer (gives screen-reader users time to hear).

---

### 3.6 `MatchingPanel` (stub)

**Composed from:** muted-tile pattern derived from W11 empty-unit tile.

**Props:**
```ts
export interface MatchingPanelProps {
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions:**
- Tile: padding 24, gap 8, radius `$radius-card-inner` 14, 1px dashed `$borderStrong`, bg `$cardSoft`, `alignItems: 'center'`.
- Glyph 🧩 32px `$fg3`.
- Title 16/700 `$fg2` (i18n `child.quiz.matchingStub`).
- Sub 12/500 `$fg3` "Tap Next to skip" / "اضغط التالي للتخطي".
- No interaction. No state.

**A11y:** `accessibilityRole="text"`, label = title + sub.

---

### 3.7 `ProgressDots`

**Composed from:** derived. **Decision:** new primitive over reusing `ProgressSteps` — `ProgressSteps` is heavier (labels, numbered) and designed for onboarding flows (Pipeline Brief §7, OQ7).

**Props:**
```ts
export interface ProgressDotsProps {
  /** 1-based current index. */
  current: number;
  /** Total number of dots. */
  total: number;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized — "Question {{current}} of {{total}}". Used as the a11y label. */
  accessibilityLabel?: string;
  testID?: string;
}
```

**Dimensions:**
- Row, gap 6 (`$space-1.5` — if not available, use 6 raw inside a `Stack` `gap={6}` Tamagui prop), `alignItems: 'center'`, `justifyContent: 'center'`.
- Current dot: 8×8, bg `$primary`, radius pill (= 4 here, but use `$radius-pill`). Optional soft glow `0 0 6px rgba(99,102,241,0.4)` — accept inline if no token.
- Outline dot: 6×6, transparent bg, 1.5px `$borderStrong` border, radius pill.
- Row uses `flexDirection={isRtl ? 'row-reverse' : 'row'}` so the leading dot is on the reading side.

**A11y:**
- `accessibilityRole="progressbar"`, `accessibilityValue={{ now: current, min: 1, max: total }}`.
- `accessibilityLabel` = "Question {current} of {total}" / "سؤال {current} من {total}".

---

### 3.8 `AttemptSummaryCard`

**Composed from:** derived. Mirrors the Hero-card chrome from Intro (Surface 3) + reuses existing `Badge variant="xp"`.

**Props:**
```ts
export interface AttemptSummaryCardProps {
  /** Number of correct answers. */
  correct: number;
  /** Total number of questions. */
  total: number;
  /** 0..100 — already rounded by caller. */
  accuracyPercent: number;
  /** Duration in seconds. */
  durationSeconds: number;
  /** Primary CTA — "Back to lessons". */
  onBack: () => void;
  /** Secondary CTA — "Try again" (re-creates the attempt). */
  onRetry: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}
```

**Dimensions:**
- Outer card: full-width minus 32, padding 24, gap 20, radius `$modal` 24, border 1px `$borderStrong`, bg `$card`, shadow `$shadow-card`.
- Mascot/trophy slot: 96×96 disc, centered horizontally. Bg = `$gradXp` (90deg `#22C55E` → `#4F46E5`). Glow `$shadow-primary-glow`. Centered glyph `🏆` 56px.
- Title: 24/800 `$fg1`, centered, i18n `child.summary.title` ("Lesson complete!" / "اكتمل الدرس!").
- Score row: row, `justifyContent: 'space-around'`, `alignItems: 'flex-start'`, each column centered.
  - Number 28/800 tabular-nums (color: success/primary/fg1 per metric).
  - Label below: 12/500 `$fg3` uppercase, tracking `0.04em`.
  - AR uses Eastern-Arabic numerals; the duration column wraps the `{n}{unit}` string in `dir="ltr"` so the unit suffix doesn't flip.
- XP placeholder: `Badge variant="xp"` "(+10 XP coming soon)" / "(+١٠ نقطة خبرة قريبًا)" — centered, self-align center. **In-source `// TODO P4-02 — wire real XP reward` comment required.**
- CTA stack: column, gap 12, full-width.
  - Primary `Button variant="primary" size="lg"` — i18n `child.summary.backToSubject`.
  - Secondary `Button variant="ghost" size="lg"` — i18n `child.summary.tryAgain`.

**Motion (mount-only celebrate):**
- Card opacity 0 → 1, 240ms `$ease-out`.
- Trophy disc `transform: translateY(8 → 0) + scale(0.9 → 1)`, 200ms `cubic-bezier(0.34, 1.56, 0.64, 1)` (spring overshoot — genuine win moment per brand law).
- NO confetti (W14 polish).

**A11y:**
- `accessibilityRole="region"`, `accessibilityLabel` = "Lesson complete. {correct} of {total} correct. {percent}% accuracy. {duration}s."
- CTAs are standard Button a11y.

---

## 4. Tokens (full table — every reference)

### 4.1 Colors

| Use | Token | Resolved | Notes |
|---|---|---|---|
| Screen canvas | `$bg` | `#0F172A` | brand-law dark default |
| Cards (Hero, Question, Summary) | `$card` | `#1E293B` | |
| Inner sub-tiles (Matching stub, prereq rows reused W11) | `$cardSoft` | `#334155` | step lighter, never darker |
| Body text | `$fg2` | `#CBD5E1` | |
| Captions, sub-text, eyebrows, locked-default | `$fg3` | `#94A3B8` | |
| Hint disabled placeholder text | `$fg4` | `#64748B` | |
| Primary border (selected MCQ, focused TextField) | `$primary` | `#4F46E5` | |
| Selected MCQ soft fill | `$primarySoft` | `rgba(79,70,229,0.18)` | |
| Primary glow (focus ring outer, CTA glow, trophy glow) | `$primaryGlow` | `rgba(99,102,241,0.45)` | composes into `$shadow-primary-glow` |
| Correct chrome (option/feedback strip border + glyph) | `$success` | `#22C55E` | |
| Correct soft fill (option bg, strip bg) | `$successSoft` | `rgba(34,197,94,0.18)` | |
| Incorrect chrome | `$danger` | `#EF4444` | |
| Incorrect soft fill | `$dangerSoft` | `rgba(239,68,68,0.18)` | proposed token if missing — see §4.6 |
| XP placeholder Badge fg | `$xp` | `#FACC15` | |
| XP placeholder Badge soft fill | `$xpSoft` | `rgba(250,204,21,0.18)` | proposed token if missing — see §4.6 |
| Card border | `$border` | `rgba(255,255,255,0.08)` | |
| Strong border (Summary card, focused dot, dashed stub tile) | `$borderStrong` | `rgba(255,255,255,0.16)` | |
| Subtle hairline (Quiz card alt border, container chrome) | `$borderSubtle` | `rgba(255,255,255,0.06)` | |
| Summary trophy gradient | `$gradXp` | `linear-gradient(90deg, #22C55E 0%, #4F46E5 100%)` | brand-law named gradient |
| Hero glyph soft disc (Intro hero) | `$primarySoft` | (reuse) | |

### 4.2 Spacing — all from `$space-*`

| Use | Token | Px |
|---|---|---|
| Tight bar/separator | `$space-1` | 4 |
| Compact gaps (badge gap, glyph-label gap) | `$space-2` | 8 |
| Strip inner gap, card inner column | `$space-3` | 12 |
| Card padding (QuestionCard inner), MCQ option padding | `$space-4` | 16 |
| Hero card padding, section gap | `$space-5` | 20 |
| Outer screen padding, section rhythm | `$space-6` | 24 |
| Celebratory rhythm (Summary top gap, trophy → title gap) | `$space-8` | 32 |

### 4.3 Radii

| Use | Token | Px |
|---|---|---|
| Tag pills, ProgressDots, Hint helper chip | `$radius-pill` | 9999 |
| Tab container (if Tabs reused) | `$radius-nav` | 12 |
| AnswerFeedbackStrip, FillInBlank, error strip, MatchingPanel tile | `$radius-card-inner` | 14 |
| MCQOption, TrueFalseChoice side, Buttons | `$radius-button` | 16 |
| (not used this wave for outer cards — see `$modal` below) | `$radius-card` | 20 |
| QuestionCard outer, Hero card, Summary card | `$radius-modal` | 24 |

### 4.4 Shadows / glows

| Use | Token | Resolved |
|---|---|---|
| Cards resting (Question/Hero/Summary) | `$shadow-card` (= `$shadow-soft`) | `0 4px 12px rgba(0,0,0,0.15)` |
| Hover, sheet floating | `$shadow-float` | `0 8px 24px rgba(0,0,0,0.25)` |
| Summary trophy disc glow | `$shadow-primary-glow` | `0 8px 24px rgba(99,102,241,0.45)` |
| Focus ring (every interactive primitive) | `$focus-ring` | `0 0 0 2px $primary, 0 0 0 6px rgba(99,102,241,0.30)` |
| ProgressDots current-dot glow | (inline fallback if no token) | `0 0 6px rgba(99,102,241,0.40)` |

### 4.5 Typography

| Use | Family | Size | Weight | LH | Tracking |
|---|---|---|---|---|---|
| Lesson title (Intro), Summary title | Poppins/Cairo | 24 | 800 | 1.2 | -0.01em |
| Question stem | Poppins/Cairo | 18 | 700 | 1.4 | 0 |
| MCQ option label | Poppins/Cairo | 16 | 600 (selected/correct/incorrect: 700) | 1.3 | 0 |
| TrueFalse label | Poppins/Cairo | 16 | 800 | 1.2 | 0 |
| FillInBlank input | Poppins/Tajawal | 16 | 500 | 1.4 | 0 |
| Feedback strip title | Poppins/Cairo | 16 | 800 | 1.3 | 0 |
| Feedback strip reveal text | Poppins/Tajawal | 14 | 500 | 1.4 | 0 |
| Intro eyebrow ("LESSON"), progress label, Summary score labels | Poppins/Cairo | 12 | 700 | 1.3 | 0.04em uppercase |
| Hint helper "Hint coming in v2" | Poppins/Tajawal | 12 | 500 | 1.3 | 0 |
| Summary score numbers, ProgressDots a11y target | Poppins/Cairo | 28 | 800 | 1.1 | 0, `fontVariant: ['tabular-nums']` |
| Summary XP Badge | Poppins/Cairo | 12 | 800 | 1.3 | 0.04em uppercase |
| Empty-lesson title | Poppins/Cairo | 18 | 700 | 1.3 | 0 |
| Empty-lesson sub | Poppins/Tajawal | 14 | 500 | 1.5 | 0 |

All numbers (score, accuracy, duration, progress current/total when shown) use `fontVariant: ['tabular-nums']` and weight 800 per brand law. AR percentages: Eastern numerals + `٪` glyph.

### 4.6 Flagged token additions (proposals — NOT auto-added)

The frontend SHOULD propose these to `packages/design-system/src/tokens/colors.ts` + `shadows.ts`, falling back to inline strings + `// TODO` comments if rejected:

- `dangerSoft: 'rgba(239,68,68,0.18)'` — used by incorrect MCQ chrome + feedback strip bg + error strip bg.
- `xpSoft: 'rgba(250,204,21,0.18)'` — used by the Summary XP badge bg.
- `successAccentDisc: 'rgba(34,197,94,0.28)'` — slightly stronger correct-strip glyph disc bg.
- `dangerAccentDisc: 'rgba(239,68,68,0.28)'` — slightly stronger incorrect-strip glyph disc bg.

If any of these are already in `colors_and_type.css` under a different name, **the FE prefers the existing token** and notes the mapping in HANDOFF.md.

---

## 5. Motion

| Element | Trigger | Spec |
|---|---|---|
| MCQOption / TrueFalseChoice / Buttons | Hover (web) | brighten 8% bg → next-step token, scale 1.02, 160ms `$ease-out`. Never darken. |
| MCQOption / TrueFalseChoice / Buttons | Press | scale 0.95, 80ms. Locked/feedback states have NO motion. |
| MCQOption / TrueFalseChoice | `feedback` mount | NO motion on the option chrome itself (locked). The strip animates instead. |
| AnswerFeedbackStrip | Enter | slide-down `translateY(-4 → 0)` + fade `0 → 1`, **160ms** `$ease-out`. |
| AnswerFeedbackStrip (correct only) | Auto-dismiss | **800ms** after enter, fade + slide-up 4px over 200ms, then advance to next question OR Complete the attempt. |
| AnswerFeedbackStrip (incorrect) | Exit | unmount on parent transition to next question (200ms fade). |
| AttemptSummaryCard | Mount | card opacity 0 → 1 over 240ms. Trophy disc `translateY(8 → 0) + scale(0.9 → 1)` over **200ms** with spring overshoot `cubic-bezier(0.34, 1.56, 0.64, 1)` (mascot rise — genuine win moment per brand law). |
| QuestionCard | Question advance | crossfade 200ms (`opacity 1 → 0 → 1`) — no slide between questions in V1 (Reanimated layout pop is W14 polish). |
| Start CTA / Submit / Next / Back to lessons / Try again | Press | scale 0.95, 80ms. |
| Hearts widget | Idle | no animation (W12 — Phase 3 wires the live count + heart-loss shake). |
| Locked Hint button / Locked options / Locked TextField | Any | NO motion. |
| Page enter (Intro → Quiz, Quiz → Summary) | Stage change | slide+fade 250ms (Expo Router default) — leave to the router; no custom motion at the stage boundary. |
| Reduced motion (`prefers-reduced-motion`) | All animations | Replace all translates with fade-only. 800ms auto-advance becomes 1200ms (gives screen-reader users time to hear). Spring overshoot becomes linear ease-out. |

All durations ≤ 800ms (brand law).

---

## 6. RTL & Arabic

- **Direction:** `dir="rtl"` driven by `useLocale().direction`. All horizontally-oriented stacks use `flexDirection={isRtl ? 'row-reverse' : 'row'}` — no hardcoded `row-reverse`.
- **Fonts:**
  - Display (titles, eyebrows, option labels, summary numbers): **Cairo** in AR, **Poppins** in EN.
  - Body (question stem in body voice, reveal text, hint helper, FillInBlank input): **Tajawal** in AR, **Poppins** in EN.
- **Mirroring:**
  - Back chevron: glyph swap `‹` ↔ `›` AND layout via the leading edge of the TopBar (the `Hearts` widget stays on the trailing edge regardless of direction).
  - AnswerFeedbackStrip leading-edge accent bar: use `borderStartWidth` (not `borderLeftWidth`) so it auto-flips.
  - ProgressDots row uses `flexDirection={isRtl ? 'row-reverse' : 'row'}` so the leading dot reads from the reading-leading side.
  - MCQOption: leading radio disc + trailing ✓/✕ glyph both use logical positions (`marginStart` / `marginEnd`, NOT `marginLeft` / `marginRight`).
- **NOT mirrored:**
  - Trophy `$gradXp` gradient (visual identity preserved).
  - Glyphs (✓, ✕, 🔒, 🎯, 🧩, 🏆, 💡) preserve identity.
  - ProgressDots dot SHAPES (only the row order flips).
  - Summary score column ORDER (correct → accuracy → time) reads naturally in both directions; the row uses `flexDirection={isRtl ? 'row-reverse' : 'row'}`.
- **Numerals:**
  - Question progress label, score row, accuracy %, XP badge, FillInBlank user input rendering — use **Eastern-Arabic numerals** in AR (`٠١٢٣٤٥٦٧٨٩`).
  - **LTR exception:** duration string `45s` — wrap in `dir="ltr"` so `45ث` doesn't render as `ث٤٥`. Same convention as `820 / 1000 XP` in `SKILL.md` Skill 4.
  - **LTR exception:** `correctAnswer` reveal text — if the answer is a number or a Latin string (FillInBlank), keep it in `dir="ltr"` so it reads as the kid wrote it.
- **Copy:** see Appendix §8.

---

## 7. Accessibility / kid-UX

- **Touch targets ≥ 48×48.** MCQOption min height 56. TrueFalseChoice 88×~163. ProgressDots row 48 tall (dots are 6–8 but the container provides hit area). Buttons size `lg` = 56, `md` = 48. Hint button `sm` = 44 — bump to 48 for the touch container even though label is sm.
- **Roles + states:**
  - QuestionCard: `accessibilityRole="group"`.
  - MCQOption list: parent `accessibilityRole="radiogroup"`, children `accessibilityRole="radio"` + `accessibilityState={{ checked, disabled }}` + label that includes state on feedback ("Option B, incorrect", "Option C, correct answer").
  - TrueFalseChoice: identical radiogroup pattern.
  - FillInBlank: `accessibilityRole="text"` (TextField default), `accessibilityState={{ disabled: locked }}`.
  - AnswerFeedbackStrip: `accessibilityRole="alert"` (web), `accessibilityLiveRegion="polite"` (Android), so screen-readers announce "Great job!" or "Not quite. Correct answer: 4" the moment the strip mounts.
  - ProgressDots: `accessibilityRole="progressbar"`, `accessibilityValue={{ now: current, min: 1, max: total }}`, `accessibilityLabel` = "Question 3 of 5".
  - AttemptSummaryCard: `accessibilityRole="region"`, `accessibilityLabel` reads the entire score sentence.
  - Hint button: `accessibilityRole="button"`, `accessibilityState={{ disabled: true }}`, label = "Hint, coming in v2".
  - Back chevron: `accessibilityRole="button"`, `accessibilityLabel` = "Back to lessons".
- **Focus visibility:** every interactive primitive renders `$focus-ring` on keyboard focus (2px `$primary` + outer 4px `$primaryGlow`). Locked options still receive focus — screen-readers should reach them to hear "Option B, incorrect" — but the focus ring tints to `$fg4` (subdued) to signal disabled.
- **Color contrast:**
  - `$fg2` on `$card`: ≈ 9:1 (AAA body).
  - `$fg3` on `$card`: 4.4:1 (AA body for ≥14px; the hint helper at 12/500 is borderline — flagged but accepted because it's never the sole affordance).
  - Correct/Incorrect chrome (`$success` / `$danger` on `$successSoft` / `$dangerSoft`): the glyph + label both pass AA. Tested in W11 for the same token pairs.
- **Reduced motion:** disable pulse, spring overshoot, translates. Keep fades. Auto-advance timer extends from 800ms → 1200ms.
- **Voice (brand law #8):**
  - Correct: "Great job!" / "أحسنت!" — friendly older-sibling, single exclamation (genuine win).
  - Incorrect: "Not quite" / "ليست الإجابة الصحيحة" — soft, no exclamation. The reveal "Correct answer: 4" / "الإجابة الصحيحة: ٤" is matter-of-fact, supportive.
  - Summary: "Lesson complete!" / "اكتمل الدرس!" — celebratory, single exclamation, genuine win.
  - XP placeholder: "+10 XP (coming soon)" / "+١٠ نقطة خبرة (قريبًا)" — parenthetical sets honest expectation; no fake reward.
  - Hint: "Hint coming in v2" / "التلميح قريبًا" — same honest tone.
  - Empty: "This lesson has no quiz yet" / "لا توجد أسئلة في هذا الدرس بعد" — gentle, no negativity.

---

## 8. EN + AR copy appendix

Verbatim strings the FE keys on. AR strings follow `SKILL.md` cheat sheet voice (friendly older-sibling, second-person, encouraging).

| i18n key | EN | AR |
|---|---|---|
| `child.lessons.intro.title` | (uses lesson `name` from API; no static key) | (same) |
| `child.lessons.intro.eyebrow` | Lesson | درس |
| `child.lessons.intro.startCta` | Start lesson | ابدأ الدرس |
| `child.lessons.intro.aiBubbleFallback` | I'll walk you through this — tap Start when you're ready. | سأشرح لك — اضغط ابدأ عندما تكون مستعدًا. |
| `child.lessons.intro.noQuestions` | This lesson has no quiz yet — come back later | لا توجد أسئلة في هذا الدرس بعد — عُد لاحقًا |
| `child.lessons.intro.noQuestionsSub` | Come back later — we're adding more soon. | عُد لاحقًا — نضيف المزيد قريبًا. |
| `child.lessons.intro.back` | Back to lessons | الرجوع إلى الدروس |
| `child.lessons.intro.errorRetry` | Couldn't load lesson. Try again | تعذّر تحميل الدرس. حاول مرة أخرى |
| `child.lessons.intro.notFound` | Lesson not found | الدرس غير موجود |
| `child.quiz.questionOf` | Question {{current}} of {{total}} | السؤال {{current}} من {{total}} |
| `child.quiz.hint` | Hint | تلميح |
| `child.quiz.hintComingSoon` | Hint coming in v2 | التلميح قريبًا |
| `child.quiz.submit` | Check answer | تحقّق |
| `child.quiz.next` | Next | التالي |
| `child.quiz.true` | True | صحيح |
| `child.quiz.false` | False | خطأ |
| `child.quiz.fillPlaceholder` | Type your answer | اكتب إجابتك |
| `child.quiz.matchingStub` | Matching questions coming soon | أسئلة المطابقة قريبًا |
| `child.quiz.matchingSkip` | Tap Next to skip | اضغط التالي للتخطي |
| `child.quiz.networkError` | Couldn't check your answer — try again | تعذر التحقق — حاول مرة أخرى |
| `child.feedback.correct` | Great job! | أحسنت! |
| `child.feedback.incorrect` | Not quite | ليست الإجابة الصحيحة |
| `child.feedback.correctAnswer` | Correct answer: {{answer}} | الإجابة الصحيحة: {{answer}} |
| `child.summary.title` | Lesson complete! | اكتمل الدرس! |
| `child.summary.score` | {{correct}} / {{total}} | {{correct}} / {{total}} |
| `child.summary.scoreLabel` | Correct | صحيحة |
| `child.summary.accuracy` | {{percent}}% | {{percent}}٪ |
| `child.summary.accuracyLabel` | Accuracy | الدقة |
| `child.summary.duration` | {{seconds}}s | {{seconds}}ث |
| `child.summary.durationLabel` | Time | الوقت |
| `child.summary.backToSubject` | Back to lessons | الرجوع إلى الدروس |
| `child.summary.tryAgain` | Try again | حاول مجددًا |
| `child.summary.xpStub` | +10 XP (coming soon) | +١٠ نقطة خبرة (قريبًا) |
| `child.lessons.a11y.optionState.selected` | selected | مختار |
| `child.lessons.a11y.optionState.correct` | correct answer | الإجابة الصحيحة |
| `child.lessons.a11y.optionState.incorrect` | incorrect | إجابة خاطئة |
| `child.lessons.a11y.feedback.correctRegion` | Answer correct. Great job! | إجابتك صحيحة. أحسنت! |
| `child.lessons.a11y.feedback.incorrectRegion` | Answer incorrect. Correct answer is {{answer}} | إجابتك خاطئة. الإجابة الصحيحة هي {{answer}} |

**Deprecation:** delete `child.lessons.stub.title`, `child.lessons.stub.body`, `child.lessons.stub.back` from `resources.ts` (W11 stub keys — no longer used).

---

## 9. Implementation handoff

| Piece | Target |
|---|---|
| `QuestionCard` | `packages/ui/src/components/QuestionCard/index.tsx` (+ export from `packages/ui/src/index.ts` under `// --- W12 quiz primitives ---`) |
| `MCQOption` | `packages/ui/src/components/MCQOption/index.tsx` (+ export) |
| `TrueFalseChoice` | `packages/ui/src/components/TrueFalseChoice/index.tsx` (+ export) |
| `FillInBlank` | `packages/ui/src/components/FillInBlank/index.tsx` (+ export) |
| `MatchingPanel` | `packages/ui/src/components/MatchingPanel/index.tsx` (+ export) |
| `AnswerFeedbackStrip` | `packages/ui/src/components/AnswerFeedbackStrip/index.tsx` (+ export) |
| `AttemptSummaryCard` | `packages/ui/src/components/AttemptSummaryCard/index.tsx` (+ export) |
| `ProgressDots` | `packages/ui/src/components/ProgressDots/index.tsx` (+ export) |
| `HeartsSlot` wrapper | **NOT shipped.** Designer pick: inline `<Hearts current={3} max={3} size="sm" />` in the TopBar. A wrapper adds no value when there's only one consumer and the count is hard-coded. (Pipeline Brief §7 leaves this to designer call.) |
| Lesson screen (3-stage state machine) | `apps/student-app/app/(child)/lessons/[lessonId].tsx` (replace 115-LOC stub) |
| Subject Lessons tab navigation patch | `apps/student-app/app/(child)/subjects/[subjectId]/index.tsx:274` — append `?subjectId=${subjectId}` to the lesson nav URL |
| Quiz hooks | `packages/api-client/src/hooks/useLesson.ts`, `useStartAttempt.ts`, `useSubmitAnswer.ts`, `useCompleteAttempt.ts`, `useAbandonAttempt.ts` |
| Query key | `packages/api-client/src/query/queryKeys.ts` — `learning.lesson(id)` |
| Token additions (proposed) | `packages/design-system/src/tokens/colors.ts` — `dangerSoft`, `xpSoft`, optionally `successAccentDisc`, `dangerAccentDisc` |
| i18n keys | `packages/shared/src/i18n/resources.ts` — EN + AR per §8; DELETE `child.lessons.stub.*` keys |
| Eastern numeral helper wiring | Verify the W11 helper covers `{current}`, `{total}`, `{percent}`, `{seconds}`, the `correctAnswer` reveal (numbers only — leave Latin-string answers untouched). |

---

## 10. Delta against captures (deliberate deviations the FE should know are intentional)

| Capture / source | Capture shows | Spec ships | Reason |
|---|---|---|---|
| `mobile/11-lesson.png` | Streak chip in header | Streak hidden | Pipeline Brief OQ14 — no streak value to show this wave (P4-03 owns streaks); saves a header field. |
| `mobile/11-lesson.png` | Mastery bar below title | Mastery omitted | Pipeline Brief §11 — mastery in the lesson player is a Phase-3 surface. |
| `mobile/11-lesson.png` | Mascot owl as hero | Hero glyph 🎯 on `$primarySoft` disc | Mascot owl is flagged as a placeholder in `design-system/README.md`; semantic emoji is the brand-law fallback. The owl can swap in when the asset is final. |
| `mobile/12-quiz.png` | Bottom tab bar (Home/Skills/…) | NO bottom tab bar in W12 | P2-09 (Wave 13) territory — same as W11 decision. |
| `mobile/12-quiz.png` | Bottom-right "Skip" affordance | NO Skip in W12 | Story P2-06 AC does not include skip; product decision is "every answer counts" until P3-11 (adaptive). |
| `mobile/13-reward.png` | Confetti + XP toast + mascot dance on correct | NO confetti, NO XP toast | Pipeline Brief OQ10 / §11 — confetti is W14 polish; XP toast is P4-02. |
| `preview/components-lesson-card.html` | Card radius 24, soft shadow | Card radius 24 (kept) | Hero affordance; matches W11 LessonCard convention. |
| Pipeline Brief §3 F2 | "the student-selected option is highlighted red; the correct option is highlighted green" | Spec matches exactly — see §2 MCQ table | Direct map. |
| Pipeline Brief §3 F1 | "After 800ms the strip slides out and the next question loads" | Spec matches — see §5 motion table | Direct map. |
| Pipeline Brief OQ8 | "Hint button slot — disabled button visible with helper text 'Hint coming in v2'" | Spec matches — see §1 Surface 2 footer + `child.quiz.hintComingSoon` key | Direct map. |
| Pipeline Brief OQ11 | "Reanimated soft-shake on wrong — optional, designer's call" | **Skip this wave.** | The red strip + reveal copy + locked chrome covers the AC; a shake on the wrong option is a 10-line W14 polish addition. Logged below. |
| Pipeline Brief OQ13 | "Hearts header — static 3" | Spec matches — `<Hearts current={3} max={3} />` in TopBar | Direct map. |

---

## 11. Open questions (resolved here so FE doesn't have to ask)

1. **Markdown rendering of `explanation` in Intro?** → Render as **plain text** this wave (Pipeline Brief R5). Markdown rendering is a follow-up. AITutorBubble's existing `variant="explain"` accepts a string; pass `explanation` directly.
2. **Visual block — what when `visual` is a URL vs an asset key?** → Treat as a URL only this wave. When `visual` is null, omit the block entirely (no empty grey placeholder — saves vertical space). The asset-key path is a follow-up when content authoring lands.
3. **MCQ — randomize option order on each render?** → **NO.** Render in the order the BE returned them. Randomization is a Phase-3 fairness concern (P3-11 adaptive); for V1, deterministic order makes debugging + screen-reader experience easier.
4. **FillInBlank — case-sensitive comparison?** → BE owns the comparison (sends `isCorrect` per `SubmitAnswerResponse`). FE never compares answers. Render whatever string the user typed verbatim; trust the BE verdict.
5. **TrueFalseChoice — value type?** → `boolean | null`. The `answerPayload` sent to the BE is the string `"true"` / `"false"` (lowercase, per Pipeline Brief R7). FE owns the boolean → string conversion at submit time.
6. **MatchingPanel — what answer payload to send?** → `""` (empty string). BE accepts it as a non-answer; `isCorrect` will be `false` and `correctAnswer` will be revealed (or BE may special-case Matching to always skip-as-incorrect). Defensive — BE has zero matching questions seeded today.
7. **Auto-advance on the LAST question — does the 800ms timer fire before Complete?** → **YES.** On correct + last question: 800ms timer → fire `useCompleteAttempt(attemptId)` → on success, transition to Summary. The user sees the green strip for 800ms, then the Summary celebration mounts. On INcorrect + last question: tap Next → fire Complete → Summary.
8. **AnswerFeedbackStrip — correct variant also shows reveal text?** → **NO.** Correct strip shows ONLY the "Great job!" title (and the option chrome already shows green ✓ — no need to repeat). Incorrect strip shows the reveal text + Next CTA.
9. **Mascot animation on Summary mount — confetti or just trophy rise?** → **Trophy rise only.** 200ms spring overshoot on the trophy disc. Confetti is W14 polish (OQ10). The Summary celebration is intentionally restrained — the real gamification reward is P4.
10. **Hint button — keyboard focus order?** → Hint is BEFORE Submit in tab order (leading edge). Even though disabled, it should be reachable so screen-readers can confirm "Hint, disabled, coming in v2".
11. **`AttemptSummaryCard` XP badge — does it animate?** → **NO.** Static badge. The trophy rise carries the celebration; the XP badge is intentionally a placeholder and shouldn't tease an animation it can't deliver yet.
12. **Empty `StartAttempt` response — render path?** → On Intro, before Start was tapped, show empty state in §1 Surface 1. If StartAttempt resolves with `questions.length === 0` (defensive — shouldn't happen in practice for a lesson that the Lessons tab marked Available), DO NOT transition to Quiz; instead, replace the Intro hero with the empty-state tile (§1 Surface 1 empty-lesson state).
13. **Locked options — still focusable for screen-readers?** → **YES.** They receive focus (so SR can announce state) but their chrome doesn't change on focus (no scale, no glow), and `accessibilityState.disabled = true`.
14. **Try again CTA — re-creates the attempt server-side?** → **YES.** Fires `useStartAttempt` again. The previous attempt is server-side terminal (Completed). Loading state mirrors Intro's Start CTA spinner.

---

## 12. Design gaps logged (for design-system follow-up — NOT W12 blockers)

- **`dangerSoft`, `xpSoft`, `successAccentDisc`, `dangerAccentDisc` tokens** — propose adding to `colors_and_type.css` + `tokens/colors.ts`. W12 ships inline strings if rejected, with `// TODO` comments.
- **`$shadow-success-glow` / `$shadow-primary-glow-strong`** — same flagged tokens W11 raised. Trophy disc on Summary card uses `$shadow-primary-glow` (existing) — no new shadow needed for W12.
- **Reanimated soft-shake on wrong option** — W14 polish. Adds ~10 lines (`useSharedValue` + `withSequence(withTiming(-6, 60), withTiming(6, 60), withTiming(0, 60))`). The red strip + reveal copy + locked chrome already covers the AC.
- **Confetti on correct answer** — W14 polish. Skia + Reanimated. The green strip + 800ms auto-advance covers the AC; confetti is the reward layer that P4 builds on.
- **Mascot owl asset** — the design-system flags the mascot owl as a placeholder. Wherever this spec says "hero glyph 🎯" or "trophy 🏆", the asset replacement is a 1-line swap when the final mascot lands.
- **Markdown rendering for `explanation`** — currently plain text. Follow-up when content authoring needs markdown.
- **`visual` asset key vs URL** — currently URL only. Follow-up when content authoring needs asset keys.
- **`AITutorBubble` AI-tutor real text** — P3-04 wires live AI; for now the bubble shows the static `explanation` field. No change needed in W12.
- **`Hearts` decrement on wrong** — Phase 3 P4-04. W12 ships static `count=3`.
- **Streak chip in TopBar** — hidden this wave (OQ14). P4-03 wires real streak value.
- **MatchingPanel real renderer** — when BE seeds Matching questions, build the drag-pair UI. W12 stub is sufficient because BE has zero matching questions seeded.

Design spec ready for frontend.
