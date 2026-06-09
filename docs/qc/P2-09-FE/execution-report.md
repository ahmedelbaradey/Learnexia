# Execution Report — P2-09-FE (Home Dashboard, student-app web E2E)

> **Owner of results: `frontend-e2e-tester`.** `qc-test-designer` scaffolded this template; it is intentionally empty of results. Fill it **after** running `tests/e2e/specs/P2-09-FE.spec.ts` against the live stack (Expo web `:8081` + backend `:5080`, per `tests/e2e/README.md` + the HANDOFF "Sandbox/WSL e2e run recipe"). Do not edit the case catalog — only record outcomes here.

## Run metadata

| Field | Value |
|---|---|
| Date / time | _(fill)_ |
| Runner | frontend-e2e-tester |
| Spec file | `tests/e2e/specs/P2-09-FE.spec.ts` |
| Backend commit / branch | _(fill)_ |
| Frontend commit / branch | _(fill)_ |
| Locale(s) exercised | ar (default) + en |
| Playwright projects | chromium / mobile (Pixel 7) — _(fill which ran)_ |
| testIDs available at run time | _(list — note any of the README §5 OQ testIDs that were added vs still missing)_ |

## Results summary

| | Count |
|---|---|
| Total cases | 29 |
| Passed | _(fill)_ |
| Failed | _(fill)_ |
| Blocked / Skipped | _(expected ≥ 6: FE-TC-09, 25, 26, 27, 28, 29)_ |
| Not run | _(fill)_ |

## Per-case results

| Case | Title | Priority | Result (PASS/FAIL/BLOCKED) | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Dashboard header renders on sign-in | P0 | | |
| FE-TC-02 | Hearts/Streak/XP stat strip | P0 | | |
| FE-TC-03 | Fresh child zero/empty state, no break | P0 | | |
| FE-TC-04 | Greeting uses first name from Me | P1 | | |
| FE-TC-05 | XPBar `0 / 100` Latin digits | P1 | | |
| FE-TC-06 | ContinueCard renders (fallback target) | P0 | | |
| FE-TC-07 | Tap Continue → lesson player | P0 | | |
| FE-TC-08 | Continue carries valid lessonId+subjectId (no no-op) | P0 | | |
| FE-TC-09 | Resume correct lesson for progressed child | P1 | BLOCKED | no progressed-child seed seam |
| FE-TC-10 | No MissionBanner / Daily Quests UI | P0 | | |
| FE-TC-11 | League preview hidden when null | P1 | | |
| FE-TC-12 | Full render with Phase-4 widgets degraded | P1 | | |
| FE-TC-13 | Arabic child lands RTL | P0 | | |
| FE-TC-14 | English child lands LTR | P0 | | |
| FE-TC-15 | Sign-out present + works | P1 | | |
| FE-TC-16 | No raw i18n keys (ar + en) | P1 | | |
| FE-TC-17 | Stats a11y label resolved sentence | P1 | | |
| FE-TC-18 | Loading skeleton in flight | P1 | | |
| FE-TC-19 | Scoped dashboard error strip, subjects still render | P0 | | |
| FE-TC-20 | Retry refetches and recovers | P1 | | |
| FE-TC-21 | Subjects section renders | P1 | | |
| FE-TC-22 | Exactly 4 product subjects | P0 | | |
| FE-TC-23 | No Reading/Art/Social-Studies | P0 | | |
| FE-TC-24 | Subject row navigates | P2 | | |
| FE-TC-25 | ContinueCard Boss/Completed chrome | P2 | BLOCKED | no progressed-child seed seam |
| FE-TC-26 | Widgets reflect real data after progress | P1 | BLOCKED | no progress-accrual seed seam |
| FE-TC-27 | Locale switch mid-session on dashboard | P2 | BLOCKED | no child-surface language switcher |
| FE-TC-28 | Unset GOOGLE_CLIENT_ID effect | P2 | BLOCKED | not applicable to this screen |
| FE-TC-29 | Native RTL restart boundary | P2 | BLOCKED | untestable in web E2E |

## Defects found

> One row per defect. File each back to `frontend` (or `analyzer`/lead for contract/seed issues). Include the failing FE-TC id.

| ID | Severity | FE-TC | Summary | Suggested owner |
|---|---|---|---|---|
| _(fill)_ | | | | |

## Missing-testID follow-ups (from README §5 — confirm status)

- [ ] `dashboard-error` + `dashboard-error-retry` (FE-TC-19/20)
- [ ] `league-preview` (FE-TC-11/12)
- [ ] `subject-row-{math|science|arabic|english}` (FE-TC-22/23/24)
- [ ] `dashboard-empty` (welcome empty-state tile)
- [ ] `lesson-screen` on `(child)/lessons/[lessonId].tsx` (FE-TC-07/08 landing assertion)

## Blocked-case confirmation

Confirm each BLOCKED case stayed blocked and why (or was unblocked by a new seed/testID):

- FE-TC-09 — _(fill)_
- FE-TC-25 — _(fill)_
- FE-TC-26 — _(fill)_
- FE-TC-27 — _(fill)_
- FE-TC-28 — _(fill)_
- FE-TC-29 — _(fill)_

## Notes / environment

- _(any flake, timing, locale-fix dependency (P1-09 `ar-EG` BCP-47 normalization), Docker/Metro gotchas — see HANDOFF run recipe)_
