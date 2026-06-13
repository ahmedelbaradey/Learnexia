# Design Spec — C5 · Matching question pairing UI (CO-FE-5 / P2-06-FE-2)

> Carryover plan `docs/plans/p1-p2-p3-carryover.md` batch **2e**. Replaces the "coming soon" stub at
> `packages/ui/src/components/MatchingPanel/index.tsx` with a real pairing interaction inside the W12 lesson
> player (`apps/student-app/app/(child)/lessons/[lessonId].tsx`). Payload is LOCKED (plan L1) — this spec only
> designs the interaction that emits it. Heart-loss visuals arrive with B3 (batch 3a wires it; the panel must
> not block that). EN + AR/RTL, mobile + desktop. No app code here.

## 0. Source-of-truth pairs

No Matching capture exists in the kit (the mock quiz is MCQ-only — **flagged as a derived design**). Grounding:

| Piece | Grounding |
|---|---|
| Option-card chrome + the 4 answer states | `preview/components-quiz.html` (default/selected/correct/wrong literals) + `preview/ar-quiz.html` (أ ب ج د RTL twin) |
| Question frame, Submit CTA, feedback strip | existing W12 `QuestionCard` + `AnswerFeedbackStrip` (`design-system/ui_kits/student-mobile/W12-lesson-quiz.md`) — unchanged |
| Captures of the quiz frame | `screenshots/mobile/12-quiz.png` / `mobile-ar/12-quiz.png` (or `07-quiz.png`) |

## 1. Interaction model — TAP-TO-PAIR (recommended; justification)

**Tap-to-pair** over drag-and-drop, for the locked decision's "tap/drag" choice:

1. **Touch + mouse + keyboard with one code path** — drag needs a gesture handler that fights the quiz
   `ScrollView` on mobile web (the panel lives inside a vertical scroll), plus a separate keyboard fallback for
   a11y anyway. Tap IS the keyboard path.
2. **Kid motor skills (ages 6–14)** — discrete taps beat sustained drags on small screens; mis-drops frustrate.
3. **RTL is free** — no drop-target geometry to mirror.
4. Duolingo's matching exercise (the product's stated benchmark genre) is tap-to-pair.

Drag is explicitly **not** built this wave (the L1 wording "tap/drag" is satisfied by tap; revisit only if the
lead asks). Flow:

- Tap an item in the **prompt column** → it becomes *armed* (selected state). Tap an item in the **answer
  column** → the two become a *pair*; both show the pair number chip. (Order-agnostic: arming an answer first
  then tapping a prompt also pairs.)
- Tapping any *paired* item **unpairs** that pair (both return to default).
- Tapping a second item in the same column moves the *armed* state to it.
- When **every prompt item is paired**, the QuestionCard Submit CTA enables (until then: disabled, opacity 0.4,
  no layout shift — brand law 10).

## 2. Layout & anatomy

Inside the existing `QuestionCard` body (which already renders the question text per W12):

```
390 (portrait):  two columns side by side, gap 10
  [ prompt col (flex 1) ]   [ answer col (flex 1) ]
  rows stacked, gap 10; columns scroll with the page (no inner scroll)
≥768: same two columns inside the 720 container; item min-height grows to 56
```

- Column headers (only if content has >3 pairs, to anchor scanning): 11/700 uppercase tracking 0.04em `$fg3` —
  "Match these" / "صِل هذه" ↔ "…with these" / "…بهذه". Otherwise omitted (kids: minimal text).
- **Item card** (per `components-quiz.html` `.ans` literal): bg `$card`, border **2px** `rgba(255,255,255,0.08)`
  `$border`, radius **16** `$radius-button`, padding 14 16, min-height 48 (touch target), text 16/600 `$fg1`
  (`$heading` family; Cairo in AR), `justify-content: space-between`.
- **Trailing chip** (the `.key` slot in the card): default = empty placeholder dot 8×8 `$borderStrong`;
  when paired = **pair-number chip**: 20×20 `$pill`, bg `$primarySoft`, fg `$primaryLight` 11/800
  tabular-nums — same number on both members of the pair (١٢٣ Eastern-Arabic in AR). The chip, not a color-coded
  per-pair palette, is the pair identity (color-blind-safe + token-clean).
- Answer column order: render as served (C1/C3 seed pre-shuffles; FE does not reshuffle on re-render).

## 3. States (token deltas, per item card)

| State | Treatment (literals from `components-quiz.html`) |
|---|---|
| Default | as §2 |
| Armed (selected, awaiting partner) | border `$primary` `#4F46E5`, bg `rgba(79,70,229,0.15)` `$primarySoft`; transition 180ms `--lx-ease-out` |
| Paired | border `$primary`, bg transparent-back to `$card` (calmer than armed), pair chip visible on both members |
| Hover (web, unpaired) | brighten bg → `$cardSoft`, scale 1.02 — never darken |
| Press | scale 0.95, 80ms |
| Focus (keyboard) | `--lx-focus-ring` (2px `$primary` + 4px glow) |
| Locked (submitting / feedback phase) | pointer-events off, opacity unchanged (information stays readable) |
| **Correct** (after submit, all pairs) | every card: border `$secondary` `#22C55E`, bg `rgba(34,197,94,0.15)` `$successSoft`, text `$success`, "✓" appended to the chip; green pulse + 4–8 confetti via the standard P2-07 correct flow (AnswerFeedbackStrip owns the strip + auto-advance 800ms) |
| **Wrong** (after submit) | **whole panel** does the 60ms ±6px shake ×2 + cards take border `$danger`, bg `$dangerSoft`, text `$danger` (server returns only attempt-level `isCorrect` — per-pair correctness is NOT available, so do not color individual pairs differently; G-2). AnswerFeedbackStrip shows the standard "Hmm, not quite — try again" / "ليس بالضبط — حاول مرة أخرى" + correct answer per W12; **heart-loss hook**: B3 (3a) attaches the heart-break to this same wrong event — the panel emits nothing extra |
| Reduced motion | no shake/pulse; state colors only (W12 rule: auto-advance 800ms → 1200ms) |

Unpair affordance during `answering` only. No drag previews, no connector lines.

## 4. Data contract (LOCKED — plan L1 + C1 note in `Learning.Application`)

- **Question content** (parsed from the question's content/options JSON per the C1 contract note — left/right
  item lists with stable ids + localized text):
  `{ "left": [{ "id": <int>, "text": "..." }], "right": [{ "id": <int>, "text": "..." }] }`
  Parse defensively like `parseOptions()` in the lesson player — malformed/empty content renders the existing
  stub tile ("coming soon" chrome) instead of crashing, and Submit becomes "Next" with no payload change to
  other types. (Exact field names = whatever C1 lands; the FE task links to the C1 note — do not guess beyond it.)
- **Answer payload** (serialized as the `answerPayload` STRING in the existing `useSubmitAnswer` mutation —
  same call shape as MCQ/TF/FillBlank, `timeSpentSeconds`/`hintUsed` unchanged):

  ```json
  { "pairs": [ { "leftId": 3, "rightId": 7 }, ... ], "attemptOrder": 2, "timeMs": 41873 }
  ```

  - `pairs`: one entry per prompt item, in any order (comparator is order-independent pair-set equality).
  - `attemptOrder`: **1-based index of the question within the attempt** = `currentIndex + 1` from the quiz
    stage state.
  - `timeMs`: `Date.now() − questionStartRef.current` — the SAME clock the player already uses for
    `timeSpentSeconds` (which keeps being sent in seconds alongside; the JSON carries ms).
  - Byte-for-byte key names as above (Gate-2 checks the payload literally).
- Wrong answer → retry: per W12, feedback then "Next"; the attempt comparator handles incomplete/duplicate-left
  as wrong — but the UI makes that impossible (Submit gated on all-paired; one pair per prompt by construction).

## 5. RTL

- Visual mirror only: prompt column sits on the logical start (right side in AR); within each card the text and
  chip swap edges automatically via `rowDir`. `leftId`/`rightId` semantics are **logical** (left = prompt list),
  independent of visual side — no id remapping in RTL.
- AR text: Cairo 16/600; pair-chip numerals Eastern-Arabic; question frame strings already localized by W12.
- New i18n keys, namespace **`quiz.matching.*`** (handed to the Batch-2 merge owner): `promptHeader`,
  `answerHeader`, `instruction` ("Tap two cards to pair them" / "اضغط بطاقتين لتصل بينهما" — shown once, 12/500
  `$fg3` under the question text), `pairedA11y`, `unpairA11y`. The stale `child.quiz.matchingStub/matchingSkip`
  keys remain for the malformed-content fallback.

## 6. A11y / kid-UX

- Each item: `accessibilityRole="button"`; label composed — armed: "{text}, selected, choose its match" /
  paired: "{text}, paired with {partner text}, double-tap to unpair" (localized via `pairedA11y`/`unpairA11y`).
- Pair state is announced (live region polite) on pair/unpair: "Paired: {a} with {b}" / "تم الوصل: {a} مع {b}".
- Targets ≥48px; instruction line gives the one-sentence how-to; instant visual feedback on every tap
  (armed state within 1 frame); Submit disabled state is visible but never hides.

## 7. Implementation handoff

| Piece | Target |
|---|---|
| Real `MatchingPanel` (replaces stub; props: parsed content, value/onChange of pairs, phase/locked, direction, locale, localized strings, testIDs) | `packages/ui/src/components/MatchingPanel/` |
| Lesson-player wiring: `QuestionType._3` branch builds the L1 JSON string into `answerPayload`; Submit gating; `attemptOrder`/`timeMs` from existing stage state + `questionStartRef` | `apps/student-app/app/(child)/lessons/[lessonId].tsx` (the `AnswerState.selectedValue: string` shape can carry the serialized JSON — keep the existing state machine, no new state shape) |
| i18n `quiz.matching.*` | `packages/shared/src/i18n/resources.ts` via Batch-2 merge owner |
| e2e (Wave D) | happy (shuffled order ⇒ correct) / wrong / Submit-gating / RTL — needs C3 seed |

## 8. Design gaps / open questions

| # | Gap | Action |
|---|---|---|
| G-1 | No Matching capture/preview card in the kit | derived from `components-quiz.html` states; add `preview/components-matching.html` to the kit when convenient |
| G-2 | Server returns attempt-level `isCorrect` only — no per-pair grading | all-cards-red on wrong (uniform); per-pair coloring would need a BE contract change (not requested) |
| G-3 | Tap-only (no drag) this wave | justified §1 — lead confirm at Gate-1 spec review |
| G-4 | Content-shape field names depend on the C1 note (Batch-1c) | 2e starts after C1 lands per plan; if C1 names differ from §4, C1 wins |

Design spec ready for frontend.
