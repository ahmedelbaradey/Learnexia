# Execution Report — P2-03 Navigate the skill tree (BACKEND)

> **Template scaffolded by `qc-test-designer`. To be filled by `api-tester` AFTER running the tests.**
> Do not fill results during design. One row per `BE-TC-*` from `backend-test-cases.md`.
> Status legend: **PASS** / **FAIL** / **BLOCKED** / **NOT-RUN** / **NOT-TESTABLE-WITHOUT-FIXTURE**.

## Run metadata (api-tester to complete)

| Field | Value |
|---|---|
| Run by | _(agent / person)_ |
| Date | _(yyyy-mm-dd)_ |
| Branch / commit | _(e.g. qc/phase-... @ sha)_ |
| Test file(s) | `backend/tests/Learnexia.IntegrationTests/P2_03_SkillTree_Tests.cs` _(+ any edits)_ |
| Build status | _(green/red)_ |
| Full Phase-2 suite regression | _(green/red — note any pre-existing failures)_ |
| Open Question #1 resolution | _(cross-language E1: silent-redirect-200 confirmed correct, OR reclassified as defect)_ |

## Results

| Case | Title (short) | Priority | Status | Evidence (status code / assertion / body snippet) | Defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | SkillTree happy-path shape | P0 | | | |
| BE-TC-02 | Envelope + `Successed` spelling | P0 | | | |
| BE-TC-03 | Anonymous SkillTree → 401 | P0 | | | |
| BE-TC-04 | Fresh student: root Available, prereq-gated Locked | P0 | | | |
| BE-TC-05 | Fresh student: no Completed skill | P1 | | | |
| BE-TC-06 | Locked skill carries missingPrerequisites | P0 | | | |
| BE-TC-07 | Available/Completed skill: empty/absent missingPrerequisites | P1 | | | |
| BE-TC-08 | Prereq-edge name matches seeded graph | P1 | | | |
| BE-TC-09 | Cross-language SkillTree → silent redirect 200 (NOT 403) | P0 | | | |
| BE-TC-10 | En-medium student gets En tree for their grade | P1 | | | |
| BE-TC-11 | Ar-medium student gets Ar tree | P1 | | | |
| BE-TC-12 | Cross-grade subject still served; status is this student's | P1 | | | |
| BE-TC-13 | 4 subjects, no Social Studies | P1 | | | |
| BE-TC-14 | Empty/concept-less subject → 200 + empty collection | P2 | | | |
| BE-TC-15 | Non-existent subject → 404 | P1 | | | |
| BE-TC-16 | Lessons endpoint happy-path shape + envelope | P0 | | | |
| BE-TC-17 | Exactly one boss per unit = highest sequenceOrder | P0 | | | |
| BE-TC-18 | Boss flag across Science subject | P1 | | | |
| BE-TC-19 | Boss can be Locked (orthogonal to state) | P1 | | | |
| BE-TC-20 | Locked lesson carries missingPrerequisites | P1 | | | |
| BE-TC-21 | Completed attempt → state Completed + downstream unlock | P0 | | | |
| BE-TC-22 | Boss tally one-per-unit, stable/idempotent | P2 | | | |
| BE-TC-23 | Cross-language single-lesson → 403 | P0 | | | |
| BE-TC-24 | Anonymous lessons-list + single-lesson → 401 | P1 | | | |

## Summary (api-tester to complete)

| Metric | Count |
|---|---|
| Total | 24 |
| PASS | |
| FAIL | |
| BLOCKED / NOT-TESTABLE | |
| NOT-RUN | |
| P0 pass rate | |

## Defects found

_(api-tester: list each FAIL with repro, expected vs actual, and a proposed severity. If BE-TC-09 fails
because the code returns 403 instead of redirecting — or vice versa — record it here and link the
Open Question #1 resolution.)_

| # | Case | Severity | Summary | Expected | Actual |
|---|---|---|---|---|---|
| | | | | | |

## Notes / deviations

_(Record any case implemented differently from the spec, any fixture synthesised, and the
Open Question #1 decision actually applied.)_
