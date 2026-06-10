# Phase-2 Frontend E2E — Batch 2 (final consolidated run)

> **2026-06-10**, branch `qc/phase-2-frontend-e2e`. This is the **batch-2** record of a single
> combined Playwright run over all 7 Phase-2 student-app specs. It does **not** replace the
> **batch-1** per-story results in each `docs/qc/P2-*-FE/execution-report.md` — those remain the
> **authoritative** numbers (each was run in isolation with a fresh Expo server). Keep both.

## How it was run
`npx playwright test specs/P2-09-FE … specs/P2-12-FE --project=chromium --reporter=json --workers=1`
(env per HANDOFF "Sandbox/WSL e2e": Node 20, `EXPO_OFFLINE`, userspace Chromium libs, no `CI`).

## ⚠️ Read first — combined-run contamination (NOT real regressions)
A single long combined session is **unreliable** for this stack: the **first** spec races Expo's
~50 s cold-start, and the **last** specs hit **Metro OOM** (Metro dies → `ERR_CONNECTION_RESET` →
every remaining test fails). This is why the per-story runs were done in isolation. Evidence: the
specs that "failed" en masse in batch-2 each **passed cleanly** in their isolated batch-1 run.

| Spec | Batch-1 (isolated, AUTHORITATIVE) | Batch-2 (combined) | Read |
|---|---|---|---|
| P2-09-FE dashboard | **23 pass** / 6 blocked | 0 / 23 fail | ran 1st → Expo cold-start; ignore batch-2 |
| P2-02-FE browse | **19 pass** / 6 fail / 3 blocked | 19 / 6 | ✅ consistent |
| P2-03-FE skill tree | **6 pass** / 10 blocked | 6 / 0 / 10 | ✅ consistent |
| P2-05-FE lesson player | **15 pass** / 1 fail / 5 blocked | 14 / 2 | ✅ ~consistent |
| P2-06-FE quiz | **21 pass** / 13 blocked | 1 / 20 fail | ran 5th → Metro OOM; ignore batch-2 |
| P2-07-FE feedback | **21 pass** / 5 blocked | 0 / 21 fail | ran 6th → Metro OOM; ignore batch-2 |
| P2-12-FE settings | **clean run PENDING** (spec authored, 41 cases) | 0 / 38 fail | ran 7th → Metro OOM; needs an isolated re-run |

**Authoritative total (6 stories, isolated): ~105 passing**, plus the documented blocked long-tail
(copy-based assertions vs Arabic-default locale, route-mock-only states, unseeded question types,
DEF-P205FE-01). P2-12 was authored but two isolated re-runs failed on Expo startup in the
post-batch degraded environment — **re-run `specs/P2-12-FE.spec.ts` alone on a fresh stack** to get
its real number.

## Real bugs found across the Phase-2 FE e2e (see PR #110)
- **BUG-001 (fixed):** child-home subjects resolved by name-match → all seeded (grade-suffixed)
  subjects dropped → empty section. Now keyed off the `subjectCode` enum.
- **DEF-P205FE-02 (fixed):** lesson quiz-stage back button used `router.back()` → no-op on a web
  deep-link. Now `router.replace`s to the subject.
- **DEF-P205FE-01 (HIGH, NOT fixed — backend):** seeder JSON-encodes `CorrectAnswer` (`"6"`) but
  `AnswerComparator` compares raw → **all MCQ/TrueFalse/FillInBlank grade wrong** product-wide.
  Blocks the correct-answer feedback path in P2-06/07. For backend-feature + api-tester.
- Minor: a few testID-placement nits (MCQOption testID on inner element; `quiz-hearts`,
  `lesson-explanation`/`-visual`, `skill-connector-*`).

## Reliability note for future runs
Run Phase-2 FE specs **one at a time** (fresh Expo each), not in one combined session — Metro
cannot sustain a multi-spec session at `--workers=1`. A CI job should shard one spec per job.
