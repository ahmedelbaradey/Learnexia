# Frontend E2E Test Cases — P2-03-FE · Navigate the skill tree

**Target agent:** `frontend-e2e-tester` → `tests/e2e/specs/P2-03-FE.spec.ts`
**Run:** `npx playwright test specs/P2-03-FE.spec.ts --project=chromium --reporter=line --workers=1`
**Harness pattern:** mirror `tests/e2e/specs/P1-02-FE.spec.ts` — `seedParent()` → `seedChild(grade:1)` → `signIn()` via `getByTestId('login-username'|'login-password'|'login-submit')`.
**Selector rule:** `getByTestId` first → `getByRole`/`getByLabel` fallback. **Never** copy-based (Arabic is default). When a stable hook is missing, write the case as `test.fixme(...)` and file the testID back to `frontend`.

---

## Shared seed & helper notes (for the implementer)

- **Auth/seed:** `POST /api/Users/Authentication/Register-Parent` → `POST /api/Parent/Add-Child` (`grade:1, language:'ar', learningLanguage:'ar'`) → sign in as the child. Same shape as `P1-02-FE.spec.ts` (`API_BASE = http://localhost:5080`).
- **Reach the tree tab:** child home → tap a `SubjectRow` (Math) → subject screen → tap the **Skill Tree** segment. See Q1/Q5 for missing `subject-row-*` / `segmented-tab-tree` testIDs; until added, fall back to `getByRole('button', { name: <subject aria-label> })` and the segment label — flag brittleness.
- **State source:** `SkillNodeDto.state` → `NodeState._1`=available(1), `._2`=completed(2), else locked(0). Boss is FE-derived (`isBoss` lesson joined onto `skillId`).
- **Fresh student baseline:** only the **root** skill of the first concept is `available`; all downstream are `locked`. No `completed` until a lesson is completed.
- **Progress seed (Q2):** to flip a node to completed / unlock its successor, per lesson:
  `POST /api/Learning/Quizzes/{lessonId}/Attempt` (Bearer child) → read `attemptId` → `POST /api/Learning/Quizzes/{attemptId}/Complete`. (Answers via `/Answers` if completion requires scored answers — confirm.) Because there is **no server lock-guard on start**, the root lesson can be attempted+completed directly.
- **Locale switch:** the app applies the child's `preferredLanguage`; for LTR(en) coverage, seed a second child with `language:'en', learningLanguage:'en'` and open English/Math-en, OR toggle the in-app language control if exposed. Assert `document.documentElement.dir`.

---

## Group A — Navigation into the Skill Tree tab

### FE-TC-01 — Open the Skill Tree tab from a subject (Runnable; testID fallback)
- **Type:** functional · **Priority:** P0 · **Agent:** `frontend-e2e-tester`
- **Preconditions:** fresh parent+child (grade 1, ar) seeded; signed in as child.
- **Steps:**
  1. From child home, tap the Math `SubjectRow`.
  2. On the subject screen, tap the **Skill Tree** segment in `SegmentedTabs`.
- **Expected:** URL ends with `/tree`; the tree body renders (at least one skill disc present); no crash, body not empty.
- **Notes / blocker:** `subject-row-{id}` and `segmented-tab-tree` testIDs MISSING (Q5) — use `aria-label` fallback and annotate brittleness. If neither hook lands, downgrade to `test.fixme` (testID).
- **Traces to:** AC-1 (precondition for all tree cases).

### FE-TC-02 — Segmented control switches Lessons ↔ Skill Tree without losing the subject (Runnable; fallback)
- **Type:** functional/state · **Priority:** P1
- **Steps:** open subject → confirm Lessons tab is default (URL = `/subjects/{id}`) → tap Skill Tree (URL `…/tree`) → tap Lessons again (URL back to `/subjects/{id}`).
- **Expected:** each switch updates the URL and renders the corresponding body; subjectId unchanged; back chevron remains; no double-back artifact.
- **Notes:** `segmented-tab-*` testIDs MISSING (Q5).
- **Traces to:** Design Spec Surface 2; supports AC-1.

---

## Group B — Node-state rendering (AC-1, AC-3)

### FE-TC-03 — Fresh student: root concept's first skill is UNLOCKED, downstream LOCKED (Runnable; aria fallback)
- **Type:** functional/state · **Priority:** P0
- **Preconditions:** brand-new seeded child (no progress), Math/grade-1/ar.
- **Steps:** open the Skill Tree tab; enumerate skill nodes in document order.
- **Expected:** the first skill node renders the **available** state (aria-label = skill name, NO locked-hint, NO boss word); at least one subsequent node renders **locked** (aria-label includes `child.skillTree.a11y.lockedHint` resolved text — assert via the node's `aria-label` containing the locked sub-caption, OR via `accessibilityState.disabled`/`aria-disabled="true"`). No completed node present (fresh student).
- **Notes / blocker:** preferred assertion needs `skill-node-{id}-state` `data-state` (Q1). Fallback: `getByRole('button')` within the tree + `aria-disabled` attribute (locked nodes set `accessibilityState.disabled=true`). Confirm RN-Web maps `accessibilityState.disabled`→`aria-disabled`; if not, this becomes BLOCKED (testID).
- **Traces to:** AC-1, AC-3.

### FE-TC-04 — Locked node visual state is distinct (BLOCKED — testID)
- **Type:** state · **Priority:** P1
- **Intended:** assert a locked node carries `data-state="locked"`, 🔒 glyph, "Locked"/"مقفل" sub-caption, `aria-disabled="true"`, `cursor:not-allowed`.
- **BLOCKED:** no `skill-node-{id}-state` testID; glyph/caption only addressable by copy (forbidden). Needs Q1 hook.
- **Traces to:** AC-1.

### FE-TC-05 — Unlocked (available) node visual state is distinct (BLOCKED — testID)
- **Type:** state · **Priority:** P1
- **Intended:** assert `data-state="available"`, ✏️ glyph, "In progress"/"قيد التقدم" caption, pressable (no `aria-disabled`).
- **BLOCKED:** Q1 testID. (aria-label gives name only for available nodes — not the state caption.)
- **Traces to:** AC-1.

### FE-TC-06 — Completed node visual state + stars (BLOCKED — testID + state-mix seed)
- **Type:** state · **Priority:** P1
- **Intended:** after completing the root lesson, the root node renders `data-state="completed"`, ✓ glyph, "Mastered"/"مُتقن" caption, 3-star row.
- **BLOCKED:** Q1 testID **and** Q2 progress seed (no completed node on a fresh student).
- **Traces to:** AC-1, AC-3.

### FE-TC-07 — Boss node is visually distinct (Boss+Locked) (BLOCKED — testID; seed-confirm)
- **Type:** state · **Priority:** P1
- **Intended:** a boss skill node renders boss chrome (`data-boss="true"`, 🔒 glyph in boss disc when locked, "Boss"/"بوس" reflected in the aria-label) distinct from a plain locked node.
- **BLOCKED:** Q1 testID for `data-boss`; the aria-label only includes the boss word for **non-locked** boss nodes (`tree.tsx`: locked branch wins the label and omits the boss word) — so a Boss+Locked node is indistinguishable from a plain locked node via aria-label. Also Q3: confirm a boss skill exists in the visible (root) region for a fresh student.
- **Traces to:** AC-1.

### FE-TC-08 — States reflect progress after completing a lesson (BLOCKED — state-mix seed + testID)
- **Type:** functional/state · **Priority:** P0
- **Intended:** open tree (root available) → via API, attempt+complete the root lesson → re-open tree → root now **completed**, next skill now **available** (state mix locked+available+completed visible).
- **BLOCKED:** Q2 progress seed reliability + Q1 testIDs to assert per-node state deterministically.
- **Traces to:** AC-3 (states reflect mastery/progress).

### FE-TC-09 — Completing a prerequisite unlocks the downstream node (BLOCKED — state-mix seed + testID)
- **Type:** functional/state · **Priority:** P1
- **Intended:** a previously **locked** downstream skill flips to **available** once its prerequisite skill is completed (verifies prerequisite→unlock chain end-to-end on the FE).
- **BLOCKED:** Q2 + Q1.
- **Traces to:** AC-3.

---

## Group C — Tap routing (AC-2) — the lock-gate (highest risk)

### FE-TC-10 — Tapping an UNLOCKED node navigates to its lesson (Runnable; testID/route fallback)
- **Type:** functional · **Priority:** P0
- **Preconditions:** fresh child; root skill is available and has ≥1 `lessonId`.
- **Steps:** open tree → tap the available root node.
- **Expected:** URL navigates to `/(child)/lessons/{lessonId}` (URL contains `/lessons/`); the lesson-player anchor mounts (Q4 testID e.g. `lesson-player`/`lesson-intro`).
- **Notes:** node tap needs `skill-node-{id}` (Q1) — fallback `getByRole('button', { name: <root skill name aria-label> })`. Lesson anchor testID = Q4.
- **Traces to:** AC-2a.

### FE-TC-11 — Available node with empty lessonIds is a safe no-op (BLOCKED — testID; defensive)
- **Type:** negative/boundary · **Priority:** P2
- **Intended:** an available node whose `lessonIds` is empty does NOT navigate (handler guards `length>0`) and does NOT crash.
- **BLOCKED:** Q1 testID; also requires a seeded available skill with no lessons (may not exist) — confirm or mark not-reproducible.
- **Traces to:** AC-2a (defensive).

### FE-TC-12 — Tapping a COMPLETED node opens its lesson (BLOCKED — state-mix seed + testID)
- **Type:** functional · **Priority:** P1
- **Intended:** after completing the root lesson, tapping the now-completed root node navigates to `/lessons/{lessonId}` (completed nodes are still pressable → open the lesson, per Design Spec Surface 4).
- **BLOCKED:** Q2 progress seed + Q1 testID + Q4 lesson anchor.
- **Traces to:** AC-2b.

### FE-TC-13 — Tapping a LOCKED node opens the WhyLockedSheet (Runnable; sheet testID fallback) ⭐
- **Type:** functional · **Priority:** P0
- **Preconditions:** fresh child; ≥1 locked downstream node.
- **Steps:** open tree → tap a locked node.
- **Expected:** the WhyLockedSheet appears — assert via the sheet's accessible title ("Why is this locked?"/"لماذا هذا مقفل؟") as a `role`/`aria-label` on the web modal container (`accessibilityLabel = whyLocked.title` in `WhyLockedSheet.tsx`), OR `why-locked-sheet` testID (Q1).
- **Notes:** the web sheet sets `accessibilityViewIsModal` + `accessibilityLabel` from the title key, so `getByLabel(<title>)`/`getByRole('dialog')` is a viable fallback. Locked node tap needs `skill-node-{id}` or `aria-disabled`+name fallback.
- **Traces to:** AC-2c (the FE-is-the-gate behaviour).

### FE-TC-14 — Tapping a LOCKED node does NOT navigate to a lesson (Runnable) ⭐⭐ (P0 — the gate)
- **Type:** negative · **Priority:** P0
- **Preconditions:** fresh child; ≥1 locked node.
- **Steps:** capture current URL → tap a locked node → wait for the sheet.
- **Expected:** URL does **NOT** change to `/lessons/...` (stays on `…/tree`); the lesson-player anchor (Q4) does **NOT** mount. (This is the only client-side gate — backend has no lock-guard on start.)
- **Notes:** assert `expect(page.url()).toContain('/tree')` AND `await expect(lessonPlayerAnchor).not.toBeVisible()`. Runnable today via URL + the negative lesson-anchor assertion; node tap uses `aria-disabled`+name fallback if `skill-node-{id}` absent.
- **Traces to:** AC-2c + the Phase-2 backend QC note (no server lock-guard → FE must gate).

### FE-TC-15 — WhyLockedSheet shows the missing prerequisites (BLOCKED — testID + prereq seed)
- **Type:** functional · **Priority:** P1
- **Intended:** the open sheet lists each `MissingPrerequisiteDto` as a prereq row with the skill name + "You're at {cur}% — need {req}%"/"أنت عند {cur}٪ — تحتاج {req}٪" line + a `MasteryBar`.
- **BLOCKED:** needs `why-locked-prereq-row` testID (Q1) for a copy-free row assertion, AND a seeded locked node that actually carries `missingPrerequisites` (confirm the seed populates prereqs for at least one locked skill; otherwise the generic "Finish the previous lesson first" branch shows — see FE-TC-16).
- **Traces to:** AC-2c.

### FE-TC-16 — WhyLockedSheet generic fallback when no prereqs (Runnable; aria fallback)
- **Type:** functional/boundary · **Priority:** P2
- **Intended:** a locked node whose `missingPrerequisites` is empty/null shows the generic line "Finish the previous lesson first"/"أكمل الدرس السابق أولاً" instead of a prereq list (defensive branch in `WhyLockedSheet.tsx`).
- **Notes:** sheet open via title fallback; the generic-vs-list distinction needs either the prereq-row testID (to assert ABSENT) or copy. Mark Runnable only if `why-locked-prereq-row` exists to assert count=0; else BLOCKED (testID).
- **Traces to:** AC-2c (defensive).

---

## Group D — RTL / LTR (AC-4)

### FE-TC-17 — Default locale renders the tree in RTL (Arabic) (Runnable)
- **Type:** RTL-i18n · **Priority:** P0
- **Preconditions:** child seeded `language:'ar'`; signed in; tree tab open.
- **Steps:** read `document.documentElement.dir`.
- **Expected:** `dir === 'rtl'`; the back chevron in the ScreenHeader renders `→` (RTL inward); tree body renders. No layout overflow / crash.
- **Notes:** `dir` and chevron glyph are observable without per-node testIDs. Chevron currently selectable only by text — acceptable here because it is a fixed glyph, not localized copy; prefer a `back-button` testID if added.
- **Traces to:** AC-4.

### FE-TC-18 — English child renders the tree in LTR (Runnable)
- **Type:** RTL-i18n · **Priority:** P0
- **Preconditions:** second child seeded `language:'en', learningLanguage:'en'`; signed in; open English (or Math-en) subject → tree tab.
- **Steps:** read `document.documentElement.dir`.
- **Expected:** `dir === 'ltr'`; back chevron renders `←`; tree renders. Sub-captions/eyebrows in English (no Arabic glyphs leaking).
- **Traces to:** AC-4.

### FE-TC-19 — Mastery header + concept eyebrow mirror correctly per locale (BLOCKED — testID)
- **Type:** RTL-i18n · **Priority:** P1
- **Intended:** assert the mastery header strip text uses the correct interpolation per locale (AR Eastern numerals `الإتقان ٤٥٪`, EN `Mastery 45%`) and is `writingDirection`-correct; concept eyebrow reads "Concept · {name}"/"المفهوم · {name}".
- **BLOCKED:** needs `skill-tree-mastery-header` testID (Q1) to read the strip without copy-matching; numerals/RTL otherwise only verifiable by copy.
- **Traces to:** AC-4 + Design Spec §7 numerals.

### FE-TC-20 — Connectors and mastery bars are NOT mirrored (BLOCKED — testID)
- **Type:** RTL-i18n · **Priority:** P2
- **Intended:** connector strips stay centered (vertical, no x-flip) in both dirs; the WhyLockedSheet `MasteryBar` keeps `direction:'ltr'` fill (grows L→R) even in RTL.
- **BLOCKED:** needs `skill-connector-*` testID + a sheet open with a prereq bar (Q1 + prereq seed). Layout-geometry assertion is otherwise brittle.
- **Traces to:** AC-4 + Design Spec §7 ("NOT mirrored").

---

## Group E — States, i18n, structure, a11y

### FE-TC-21 — No raw i18n keys leak on the Skill Tree tab (Runnable)
- **Type:** i18n · **Priority:** P0
- **Steps:** open the tree tab (ar), read `body.innerText`.
- **Expected:** text does NOT contain raw keys (`child.skillTree.`, `child.subjects.`, `whyLocked.`, `conceptEyebrow`, `mastery`, `unitOf`); open the WhyLockedSheet and re-assert no raw keys inside it.
- **Traces to:** i18n NFR; supports AC-1/AC-2/AC-4.

### FE-TC-22 — Loading state renders skeleton, then resolves (Runnable; testID preferred)
- **Type:** state(loading) · **Priority:** P1
- **Steps:** throttle/slow the `…/SkillTree` response (Playwright route delay) → open the tree.
- **Expected:** the skeleton (disc placeholders) shows while pending, then the real tree replaces it; no infinite spinner; no crash.
- **Notes:** prefer `skill-tree-loading` testID (Q1); without it, assert the eventual tree presence + absence of error text. Mark the skeleton-visible sub-assertion BLOCKED (testID) if no hook.
- **Traces to:** Design Spec Surface 4 loading.

### FE-TC-23 — Error state renders retry and recovers (Runnable; testID preferred)
- **Type:** state(error) · **Priority:** P1
- **Steps:** intercept `…/SkillTree` → respond 500 → open the tree → assert the error message ("Couldn't load. Try again") + a Retry button (`accessibilityLabel = errorRetry`) → remove the intercept → tap Retry.
- **Expected:** error block + retry button visible; after Retry with the intercept cleared, the tree loads. No raw key in the error text.
- **Notes:** Retry button selectable by `getByRole('button',{name:<errorRetry aria-label>})`; prefer a `skill-tree-error` testID (Q1).
- **Traces to:** Design Spec Surface 4 error.

### FE-TC-24 — Empty tree (no concepts) shows the empty state (BLOCKED — seed/testID)
- **Type:** state(empty) · **Priority:** P2
- **Intended:** a subject whose SkillTree returns `[]` shows "Coming soon — no lessons yet"/"قريباً — لا توجد دروس بعد" and no node discs.
- **BLOCKED:** every seeded grade-1 subject has a non-empty tree (no empty-tree fixture); needs either a route-intercept returning `[]` (then Runnable) or a `skill-tree-empty` testID. Implement via `route.fulfill({ json: { successed:true, data: [] } })` if the envelope shape is confirmed — promote to Runnable if so.
- **Traces to:** Design Spec Surface 4 empty.

---

## Implementation checklist for the tester
- Implement all 24 as `test(...)`; write BLOCKED ones as `test.fixme(true, '<reason + which testID/seed>')` mirroring `P1-02-FE.spec.ts`.
- File the **missing testIDs** (README §4 Q1) back to `frontend` as a single bug note — do not select by copy or reach into Tamagui-generated classes.
- For every Runnable case, assert **no raw i18n key** appears (cheap, catches regressions).
- The two ⭐ gate cases (FE-TC-13, FE-TC-14) are the must-pass core — if testIDs block them, prioritise getting the locked-node tap selectable (aria-disabled + name) so the negative no-navigation assertion runs.
- Write results into `execution-report.md`.
