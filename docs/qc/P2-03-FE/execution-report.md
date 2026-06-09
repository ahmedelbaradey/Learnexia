# Execution Report — P2-03-FE · Navigate the skill tree

> **Template — filled by `frontend-e2e-tester` after running `tests/e2e/specs/P2-03-FE.spec.ts`.**
> qc-test-designer scaffolds this file and never fills results. Do not edit the test-case
> definitions here — they live in `frontend-test-cases.md`. Record only outcomes + defects.

- **Run date:** _<YYYY-MM-DD>_
- **Run by:** frontend-e2e-tester
- **Command:** `npx playwright test specs/P2-03-FE.spec.ts --project=chromium --reporter=line --workers=1`
- **Build / commit under test:** _<git sha>_
- **Backend up?** _<docker postgres+minio + Development dotnet run; DB seeded?>_
- **Web server (8081) up?** _<reused / Playwright-started>_

## Summary line
- **Total:** 24 · **Pass:** _ · **Fail:** _ · **Blocked/fixme:** _ · **Skipped:** _

## Per-case results

| Case | Title | Priority | Status (PASS/FAIL/BLOCKED) | Defect ID | Notes |
|---|---|---|---|---|---|
| FE-TC-01 | Open Skill Tree tab from a subject | P0 | | | |
| FE-TC-02 | Segmented control switches Lessons ↔ Tree | P1 | | | |
| FE-TC-03 | Fresh student: root unlocked, downstream locked | P0 | | | |
| FE-TC-04 | Locked node visual state distinct | P1 | | | |
| FE-TC-05 | Unlocked (available) node visual state distinct | P1 | | | |
| FE-TC-06 | Completed node visual state + stars | P1 | | | |
| FE-TC-07 | Boss node visually distinct (Boss+Locked) | P1 | | | |
| FE-TC-08 | States reflect progress after completing a lesson | P0 | | | |
| FE-TC-09 | Completing a prereq unlocks downstream node | P1 | | | |
| FE-TC-10 | Tap UNLOCKED node → navigates to lesson | P0 | | | |
| FE-TC-11 | Available node with empty lessonIds = no-op | P2 | | | |
| FE-TC-12 | Tap COMPLETED node → opens lesson | P1 | | | |
| FE-TC-13 | Tap LOCKED node → WhyLockedSheet opens ⭐ | P0 | | | |
| FE-TC-14 | Tap LOCKED node → does NOT navigate ⭐⭐ (the gate) | P0 | | | |
| FE-TC-15 | WhyLockedSheet shows missing prerequisites | P1 | | | |
| FE-TC-16 | WhyLockedSheet generic fallback (no prereqs) | P2 | | | |
| FE-TC-17 | Default locale renders tree RTL (Arabic) | P0 | | | |
| FE-TC-18 | English child renders tree LTR | P0 | | | |
| FE-TC-19 | Mastery header + concept eyebrow mirror per locale | P1 | | | |
| FE-TC-20 | Connectors + mastery bars NOT mirrored | P2 | | | |
| FE-TC-21 | No raw i18n keys leak | P0 | | | |
| FE-TC-22 | Loading state → skeleton → resolves | P1 | | | |
| FE-TC-23 | Error state → retry → recovers | P1 | | | |
| FE-TC-24 | Empty tree → empty state | P2 | | | |

## Defects found

> One row per defect. Link to the failing case. File missing-testID requests here too.

| Defect ID | Severity | Case(s) | Summary | Repro | Status |
|---|---|---|---|---|---|
| | | | | | |

## Blocked-case ledger (why each fixme stayed blocked)

| Case | Blocker (testID / seed / route) | What would unblock it |
|---|---|---|
| FE-TC-04 | missing `skill-node-{id}-state` testID | add per-node `data-state` hook |
| FE-TC-05 | missing per-node state testID | add `data-state` hook |
| FE-TC-06 | state testID + completed-node seed | add hook + progress-seed path |
| FE-TC-07 | `data-boss` testID + boss-in-root seed confirm | add hook + confirm seed |
| FE-TC-08 | state testID + progress seed | confirm attempt+complete advances state |
| FE-TC-09 | state testID + progress seed | confirm prereq-completion unlocks downstream |
| FE-TC-11 | node testID + available-no-lessons fixture | confirm fixture exists |
| FE-TC-12 | state testID + completed seed + lesson anchor | add hooks + seed |
| FE-TC-15 | `why-locked-prereq-row` testID + prereq seed | add hook + confirm seed populates prereqs |
| FE-TC-19 | `skill-tree-mastery-header` testID | add hook |
| FE-TC-20 | `skill-connector-*` testID + sheet prereq bar | add hooks + seed |
| FE-TC-24 | empty-tree fixture or route-intercept + `skill-tree-empty` | use route.fulfill `[]` or add hook |

## Recommendation to reviewer
- _<gate verdict: do the must-pass gate cases FE-TC-13 + FE-TC-14 pass? any P0 fail? net coverage after unblocking?>_
