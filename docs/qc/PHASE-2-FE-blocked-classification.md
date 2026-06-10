# Phase-2 FE E2E — Blocked/Skipped Test Classification (Exit Gate)

> Every Phase-2 FE e2e case that did **not** run green is classified below by **why**, so the
> blocked long-tail is auditable (real gaps vs seed/spec/environment) rather than an opaque "skipped"
> count. Source: the per-story `docs/qc/P2-*-FE/execution-report.md` runs. None are faked passes.

## Summary by category

| # | Category | Count (approx) | Owner / action | Blocks release? |
|---|---|---|---|---|
| A | **Now-unblockable** — was DEF-P205FE-01 (correct-answer grading) | ~6 | **Fixed this PR** — re-run unlocks the live correct path | no |
| B | Unseeded question types (TrueFalse / FillInBlank / Matching) | ~12 | backend: add seed questions of those types (Matching also needs the renderer, P2-07.b) | no (MVP MCQ works) |
| C | Progressed / specific-state seed (real continue, cache-flip, populated league) | ~6 | test-harness: drive completion in-flow / a seed endpoint | no |
| D | Copy-based assertion vs Arabic-default locale | ~10 | covered structurally by `data-state`/role; optional `data-*`/testID | no |
| E | Missing testID (residual) | ~5 | frontend: small follow-up (`lesson-explanation`/`-visual`, `quiz-progress-label`, `skill-connector-*`, `quiz-hearts`) | no |
| F | Route-mock-only states (error/404/retry) | ~6 | spec: add `page.route` mocks (pattern already used elsewhere) | no |
| G | Feature not built (resume lesson, reduced-motion auto-advance) | ~3 | product backlog (P2-05 AC6 resume; P3-05 reduced-motion) | no |
| H | Spec-quality nits (`getByRole('header')`, back-nav) | ~3 | spec fix — app renders correctly | no |
| I | Multi-session seed (sign-out-others count) | ~1 | needs ≥2 real sessions for one parent | no |

## Per-category detail

**A — Now-unblockable (CorrectAnswer fix landed in this PR).** P2-06 correct-answer outcome + auto-advance, P2-07 FE-TC-02/09/13 (correct verdict — were route-mocked) can now run **live** (submit the correct option → `isCorrect:true`). Re-running P2-06/07 against the fixed backend promotes these from BLOCKED/route-mocked to live PASS.

**B — Unseeded question types.** `LearningSeeder` seeds **MCQ** (and a couple of string answers) but no reachable **TrueFalse**/**FillInBlank** lessons in the Grade-1 path, and **Matching has zero seed data** + ships as a stub renderer (P2-07.b). Blocks: P2-06 FE-TC-09..18 (per-type render/answer), P2-07 FE-TC-14/16/17/18. Action: add seed questions of each type (and finish the Matching renderer) — then these become runnable.

**C — Progressed/specific-state seed.** A fresh child has only root unlocked. Cases needing a *populated* state — P2-09 real continue (≠ fallback)/populated league (FE-TC-09/25/26), P2-03 locked→available cache-flip timing (FE-TC-09/11) — need the e2e to drive a lesson completion in-flow (the verified seed recipe: start+complete lesson 1) or a seed endpoint. Action: bake the completion-seed into those specs' setup.

**D — Copy-based assertion vs Arabic default.** Cases where the only distinguisher is Arabic copy (locked/available sub-captions, prereq skill names, eyebrow text). These are **covered structurally** (`data-state`, `role`, count) so they're marked blocked rather than asserted on brittle copy. Optional: add a `data-*`/testID to make them copy-free. Not a gap.

**E — Missing testID (residual).** `lesson-explanation`, `lesson-visual` (P2-05 FE-TC-02/03), `quiz-progress-label` (P2-06 FE-TC-06), `skill-connector-*` (P2-03 FE-TC-20), `quiz-hearts` (P2-06 FE-TC-27), and `lesson-screen` resolution on the player root. Small frontend follow-up; fallbacks (URL/role/aria-label) are in place.

**F — Route-mock-only states.** Error/500/404/retry states need `page.route` to trigger (the live backend returns 200). Several are already implemented this way; the blocked ones just need the same mock added. Spec follow-up, not an app gap.

**G — Feature not built.** P2-05 AC6 **resume-an-in-progress-lesson** is not implemented (the player always re-enters fresh) — blocked as feature-absent, not a test miss. P2-07 reduced-motion auto-advance gate (`AccessibilityInfo.isReduceMotionEnabled`) not wired — P3-05 territory.

**H — Spec-quality nits.** P2-02 used `getByRole('header')` (invalid ARIA role — should be `heading`); a back-nav assertion to refine. The app renders correctly (snapshot confirms the section/heading/rows) — these are spec edits, not app bugs.

**I — Multi-session seed.** P2-12 "sign out other sessions → count" needs ≥2 real concurrent sessions for one parent — not deterministically producible in one run.

## Exit-gate verdict
**No category blocks the Phase-2 release.** The only product-correctness defect found (DEF-P205FE-01, quiz grading) is **fixed in this PR**. The rest are seed coverage (B/C), test-harness/spec follow-ups (D/E/F/H/I), or backlog features (G) — tracked, none release-blocking.
