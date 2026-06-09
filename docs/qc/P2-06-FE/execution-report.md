# Execution Report — P2-06-FE (Take a quiz — 4 question types)

> **Filled by `frontend-e2e-tester` AFTER implementing + running `tests/e2e/specs/P2-06-FE.spec.ts`.**
> The qc-test-designer scaffolds this template only and never fills results.
> Spec source: [`frontend-test-cases.md`](./frontend-test-cases.md).

## Run metadata (tester fills)

| Field | Value |
|---|---|
| Date run | _TBD_ |
| Spec file | `tests/e2e/specs/P2-06-FE.spec.ts` |
| Web target | `http://localhost:8081` (Expo web) |
| API target | `http://localhost:5080` |
| Browser / project | _TBD_ |
| Seed method | _TBD (API Register-Parent → Add-Child → child login via UI)_ |
| testIDs wired? (README OQ1) | _Yes / No — note which_ |
| All-4-types seed available? (README OQ2) | _Yes / No — describe_ |

## Results summary (tester fills)

| Metric | Count |
|---|---|
| Total cases | 34 |
| Passed | _TBD_ |
| Failed | _TBD_ |
| Skipped / BLOCKED | _TBD_ |

## Per-case results (tester fills)

| ID | Title | Result (PASS / FAIL / SKIP) | Notes / defect ref |
|---|---|---|---|
| FE-TC-01 | Start → attempt + quiz stage | | |
| FE-TC-02 | ProgressDots count | | |
| FE-TC-03 | Question card stem + controls (one-focus) | | |
| FE-TC-04 | No Skip / no confetti | | |
| FE-TC-05 | MCQ renders option list | | |
| FE-TC-06 | MCQ accepts selection | | |
| FE-TC-07 | MCQ instant selection state | | |
| FE-TC-08 | MCQ option a11y + target size | | |
| FE-TC-09 | True/False renders pair | | |
| FE-TC-10 | True/False toggles | | |
| FE-TC-11 | True/False instant chrome | | |
| FE-TC-12 | FillInBlank renders input | | |
| FE-TC-13 | FillInBlank accepts typing | | |
| FE-TC-14 | FillInBlank whitespace → Submit disabled | | |
| FE-TC-15 | FillInBlank empty → Submit disabled | | |
| FE-TC-16 | Matching stub tile renders | | BLOCKED — no Matching seed |
| FE-TC-17 | Matching stub → Next + empty payload | | BLOCKED — no Matching seed |
| FE-TC-18 | Real Matching drag-pair | | BLOCKED — stub only |
| FE-TC-20 | Submit → feedback + lock | | |
| FE-TC-21 | Incorrect → Next advances | | |
| FE-TC-22 | Full walk → Summary (persistence proxy) | | |
| FE-TC-23 | Correct → 800ms auto-advance | | BLOCKED-soft — needs known-correct answer |
| FE-TC-24 | Controls lock after submit | | |
| FE-TC-25 | Desktop responsive | | |
| FE-TC-26 | Mobile-width responsive | | |
| FE-TC-27 | Hearts indicator (static 3) | | |
| FE-TC-28 | MCQ RTL vs LTR | | |
| FE-TC-29 | True/False RTL vs LTR | | |
| FE-TC-30 | FillInBlank RTL alignment | | |
| FE-TC-31 | No raw i18n keys | | |
| FE-TC-32 | Submit network error strip | | |
| FE-TC-33 | Lesson load error / 404 | | |
| FE-TC-34 | Empty lesson → empty tile | | |

## Defects filed (tester fills)

| Defect ID | Case | Severity | Summary | Status |
|---|---|---|---|---|
| _DEFECT-…_ | | | | |

## Blocked-case ledger (tester confirms / updates)

| Case | Blocker | Resolution path |
|---|---|---|
| FE-TC-16, FE-TC-17 | No seeded Matching question to reach the stub | BE seeds a Matching question, or harness exposes a Matching-bearing lesson |
| FE-TC-18 | Real Matching renderer does not exist (stub only) | Build drag-pair renderer when BE seeds Matching (Design Spec §12) |
| FE-TC-23 | No deterministic correct answer to force auto-advance | Seed a known-answer question or expose the correct answer to the tester |
| FE-TC-05–15, 28–30 (`*`) | Type-conditional — depends on reaching that question type | Deterministic all-4-types seed lesson (README OQ2) |

## Notes / observations (tester fills)
_TBD_
