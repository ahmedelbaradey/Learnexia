# QC Test Plan — P2-03-FE · Navigate the skill tree (student-app web PWA)

**Surface:** student-app web E2E only (child surface). **No backend cases** — backend skill-tree/state behaviour is covered by Phase-2 backend QC; this pass treats the API as a black-box seed/oracle.

**Designed by:** qc-test-designer (Opus). **Implements into:** `tests/e2e/specs/P2-03-FE.spec.ts` by `frontend-e2e-tester`.

---

## 1. Summary

- **Story:** [user-stories/Phase-2-Learning-Core/P2-03-navigate-skill-tree.md](../../../user-stories/Phase-2-Learning-Core/P2-03-navigate-skill-tree.md)
- **Design spec:** [design-system/ui_kits/student-mobile/W11-subjects-tree.md](../../../design-system/ui_kits/student-mobile/W11-subjects-tree.md) (Surface 4 = Skill Tree tab, Surface 5 = WhyLockedSheet)
- **Task file:** [tasks/Frontend/student-app/Phase-2-Learning-Core/P2-03-FE.md](../../../tasks/Frontend/student-app/Phase-2-Learning-Core/P2-03-FE.md) (status: Done, audited 2026-06-07)
- **Screens under test:**
  - `apps/student-app/app/(child)/subjects/[subjectId]/tree.tsx` — Skill Tree tab
  - `apps/student-app/app/(child)/subjects/[subjectId]/_layout.tsx` — segmented Lessons | Skill Tree control + ScreenHeader
  - `apps/student-app/app/(child)/_components/WhyLockedSheet.tsx` — why-locked modal (web)
  - `packages/ui/src/components/SkillTreeNode/index.tsx` — disc node, 4 states + boss + connector

- **Scope in:** all 4 node states (locked / unlocked / completed / boss), prerequisite connectors, tap routing (unlocked→lesson, locked→why-locked sheet, NO navigation on locked), why-locked content, RTL(ar) vs LTR(en) mirroring, i18n (no raw keys), loading/empty/error states, the FE-as-the-gate requirement (no server lock-guard on start).
- **Scope out:** server-side lock enforcement, mastery computation correctness, native (iOS/Android) sheet behaviour, animation timing/pulse fidelity, Skia curved connectors (V1 ships straight strips), the Lessons tab itself (P2-02-FE).

### Counts
- **Total FE cases: 24** (all `frontend-e2e-tester`).
- By priority: **P0 = 11**, **P1 = 9**, **P2 = 4**.
- By status: **Runnable = 13**, **BLOCKED = 11** (blocked overwhelmingly on **missing node/sheet/state testIDs** — see §4/§5; a handful also need a state-mix seed the harness cannot yet build deterministically).

> **Coverage verdict: every acceptance criterion has at least one P0/P1 case. No criterion is uncovered.** BUT the majority of state-rendering and tap-routing cases are **BLOCKED on missing testIDs** — the screen renders `SkillTreeNode` without passing the `testID` prop it already accepts, and there are no testIDs on the disc, the sub-caption/state, the connector, the mastery header, or the WhyLockedSheet container. Until `frontend` adds those hooks, these cases cannot be implemented against stable selectors (copy-based selection is forbidden — Arabic is the default locale). This is the single biggest blocker for this story and the top action item for the lead.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

| # | Acceptance criterion (story) | Cases | Gap? |
|---|---|---|---|
| AC-1 | Tree renders nodes in one of four states: locked, unlocked, completed, boss | FE-TC-03, FE-TC-04, FE-TC-05, FE-TC-06, FE-TC-07, FE-TC-08 | No gap (all but FE-TC-03 BLOCKED on testIDs / state-mix seed) |
| AC-2a | Tapping an **unlocked** node opens its lesson | FE-TC-10, FE-TC-11 | No gap (BLOCKED on testIDs) |
| AC-2b | Tapping a **completed** node opens its lesson | FE-TC-12 | No gap (BLOCKED on testIDs + completed seed) |
| AC-2c | Tapping a **locked** node shows why it's locked (prerequisite) — does NOT navigate | FE-TC-13, FE-TC-14, FE-TC-15, FE-TC-16 | No gap (BLOCKED on testIDs) — **highest-risk behaviour (FE is the only gate)** |
| AC-3 | Node states reflect the student's current mastery/progress data | FE-TC-03 (fresh→root unlocked), FE-TC-08 (completed after progression), FE-TC-09 (downstream unlocks) | No gap (FE-TC-08/09 BLOCKED on state-mix seed) |
| AC-4 | Tree renders correctly in RTL (Arabic) and LTR (English) | FE-TC-17, FE-TC-18, FE-TC-19, FE-TC-20 | No gap (FE-TC-19/20 partly BLOCKED on testIDs) |
| — | Supporting: nav into the tab, i18n no-raw-keys, loading/empty/error, connectors, mastery header, sheet a11y | FE-TC-01, FE-TC-02, FE-TC-21, FE-TC-22, FE-TC-23, FE-TC-24 | No gap |

---

## 3. Risk notes (where the cases are weighted, and why)

1. **FE is the only lock gate (highest risk).** Phase-2 backend QC found **no server lock-guard on `Start Attempt`** — a locked lesson returns 200 server-side. So the *only* thing stopping a child from entering a locked lesson is the FE behaviour: a locked `SkillTreeNode` tap must open the WhyLockedSheet and **must not** call `router.push('/(child)/lessons/...')`. FE-TC-13–16 are P0 and assert **both** the positive (sheet opens) and the negative (URL/route did NOT change, lesson player did NOT mount). Code review of `tree.tsx` confirms the handler branches on `nodeStateValue(state)===0 → onLockTap` (no navigation) — these tests lock that behaviour against regression. A regression here would let kids bypass prerequisites entirely.
2. **State rendering correctness (AC-1/AC-3).** The disc glyph/caption/colour is the entire information scent of the screen. Mis-mapping `NodeState._1/_2` or the boss-join would silently mislead the student about what's available. Weighted heavily (FE-TC-04–08), but mostly BLOCKED on testIDs because the only stable per-node assertion today is the `accessibilityLabel`/`aria-label` — which IS composed per-state in `tree.tsx` (locked label includes the locked-hint, boss label includes the boss word). Runnable cases lean on `aria-label` as a fallback selector where a testID is absent.
3. **RTL mirroring (AC-4).** Arabic is the default locale, so RTL is the *primary* path, not the edge. `html[dir="rtl"]`, `writingDirection`, and `flexDirection: row-reverse` drive layout; the back chevron flips `←`↔`→`. Connectors and progress/mastery bars are deliberately NOT mirrored (vertical strips / `direction:'ltr'` wrappers). FE-TC-17–20 assert the document direction and the chevron glyph, which are observable without per-node testIDs.
4. **Seed determinism for state mix.** A *fresh* seeded child shows **only the root skill unlocked**; everything downstream is locked. There is no completed/boss-unlocked node until the student actually completes lessons. Getting a deterministic locked+unlocked+completed mix requires the test to **drive lesson completion via the API** (start+complete attempt) before opening the tree. This is feasible (endpoints exist) but adds setup cost and a dependency on attempt-scoring behaviour — flagged as the second-biggest blocker (Open Question Q4).

---

## 4. Open questions / assumptions (lead must resolve before implementation)

**Q1 — Missing testIDs (TOP BLOCKER).** `SkillTreeNode` declares a `testID` prop but `tree.tsx` never passes it; there are also no testIDs on:
- the per-node disc / state glyph / sub-caption (to assert state = locked/unlocked/completed/boss),
- the connector strip (to assert reachable vs locked tint / count),
- the concept eyebrow / mastery header strip,
- the `WhyLockedSheet` root container, its prereq rows, and its "Got it!" CTA.

**Requested testIDs (hand back to `frontend`):**
| testID | Element | Enables |
|---|---|---|
| `skill-node-{skillId}` | `SkillTreeNode` root (pass through existing prop) | per-node tap + presence |
| `skill-node-{skillId}-state` | element carrying `data-state="locked\|available\|completed"` (+ `data-boss="true"`) | assert the 4 states deterministically |
| `skill-connector-{skillId}` w/ `data-connector="reachable\|locked"` | connector strip | connector presence + tint |
| `skill-tree-mastery-header` | mastery header strip | AC-3 header / i18n |
| `skill-tree-empty` / `skill-tree-error` / `skill-tree-loading` | state containers | loading/empty/error |
| `why-locked-sheet` | WhyLockedSheet root | sheet open/close |
| `why-locked-prereq-row` (repeatable) | each prereq row | prereq rendering |
| `why-locked-cta` | "Got it!" button | close-via-CTA |

Cases that depend on these are marked **BLOCKED (testID)** with the specific hook named. Where an `aria-label` fallback exists (it does for the node, composed per-state in `tree.tsx`), the case is downgraded to Runnable-with-fallback and notes the brittleness.

**Q2 — How to obtain a state mix (locked + unlocked + completed [+ boss]).** A fresh child = root unlocked only. To exercise FE-TC-08/09/12 and a real RTL tree with completed nodes, the harness must progress the student. **Assumption (please confirm):** the e2e test seeds progression by calling, per lesson to complete:
`POST /api/Learning/Quizzes/{lessonId}/Attempt` → then `POST /api/Learning/Quizzes/{attemptId}/Complete` (Bearer = child token). Because there is **no lock-guard on start**, the test can complete the root lesson to flip the next skill to unlocked and the root to completed. Confirm this path scores high enough to mark the lesson complete + advance downstream state, or provide a direct progress-seed endpoint. If neither is reliable, FE-TC-08/09/12 stay BLOCKED (state-mix seed).

**Q3 — Boss node presence in the seed.** Boss is derived on FE by joining `subjectLessons.isBoss` onto the skill (`bossSkillIds` in `tree.tsx`). `LearningSeeder.MarkBossLessonsAsync` seeds boss lessons, but a boss skill is **locked** for a fresh student (it sits downstream). Confirm which seeded subject/grade exposes a boss skill and whether reaching an *available/completed* boss node is feasible within the test budget, or accept boss coverage only in the **Boss+Locked** state (FE-TC-07, reachable on a fresh student if a boss skill exists in the root concept — needs confirmation).

**Q4 — Lesson route after tapping an unlocked node.** Tap routes to `/(child)/lessons/{lessonIds[0]}`. The lesson player is a real screen (P2-05). FE-TC-10/12 assert the URL contains `/lessons/` and a lesson-player anchor mounts. Confirm the lesson-player root testID (e.g. `lesson-player` / `lesson-intro`) so the navigation assertion is not copy-based.

**Q5 — Reaching the Skill Tree tab.** Navigation path is: child home → tap a `SubjectRow` → subject screen (Lessons default) → tap the "Skill Tree" segment in `SegmentedTabs`. The `SegmentedTabs` segment + `SubjectRow` selectors: `SubjectRow` has no per-subject testID (only `accessibilityLabel = subject name`); `SegmentedTabs` segment testIDs are unknown. **Requested:** `subject-row-{subjectId}` and `segmented-tab-{value}` (`segmented-tab-tree`). Until then, the tab-navigation helper falls back to `aria-label` (brittle in AR) — flagged in FE-TC-01.

**Assumption A1 — Child grade has a seeded curriculum.** Per HANDOFF, the Development boot migrates+seeds a fresh DB; `LearningSeeder` seeds grades 1–6 with Math/Science (ar+en), Arabic(ar), English(en), each with concepts/skills/edges. The test seeds a child at **grade 1** (matching the P1 e2e helper) and opens the **Math** subject (ar tree by default for an `ar` learner). Confirm Math/grade-1/ar has ≥2 concepts and a multi-skill first concept so connectors render.

---

## 5. Handoff

- **`frontend-e2e-tester`** implements **all** of `frontend-test-cases.md` into `tests/e2e/specs/P2-03-FE.spec.ts` (mirror the `P1-02-FE.spec.ts` harness: `seedParent` → `seedChild(grade:1)` → `signIn`; reuse `getByTestId` first, `getByRole`/`getByLabel` fallback). Run: `npx playwright test specs/P2-03-FE.spec.ts --project=chromium --reporter=line --workers=1`. BLOCKED cases are written as `test.fixme(...)` with the blocker reason inline (mirror the P1-02 pattern), and the **missing testIDs** are filed back to `frontend` (do not reach into CSS / Tamagui internals).
- **No `backend-test-cases.md`** for this run (frontend-only scope). The backend is exercised only as a seed/oracle via its public API.
- **`execution-report.md`** is the empty templated results file in this folder — the tester fills pass/fail per FE-TC after running. qc-test-designer does **not** fill results.

**Test cases ready** — `frontend-e2e-tester` to implement `frontend-test-cases.md`; results written into `execution-report.md`. (No `api-tester` work this run — no backend test-case file.)
