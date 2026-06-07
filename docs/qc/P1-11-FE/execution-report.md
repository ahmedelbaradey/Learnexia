# P1-11-FE — E2E Execution Report

> **Filled by:** `frontend-e2e-tester` **after** running the Playwright suite. QC does NOT fill results.
> **Source cases:** `./frontend-test-cases.md` · **Spec:** `tests/e2e/specs/P1-11-FE.spec.ts`
> Record one row per `FE-TC-*`. Use status: **PASS** / **FAIL** / **BLOCKED** / **SKIP**. File any FAIL as a `frontend` defect and link it.

## Run metadata

| Field | Value |
|---|---|
| Date / time (UTC) | _TBD_ |
| Runner | `frontend-e2e-tester` |
| Harness commit | _TBD_ |
| App commit (student-app) | _TBD_ |
| Backend up at `:5080`? | _yes / no_ |
| Expo web at `:8081`? | _yes / no (Playwright-owned)_ |
| Marketing site reachable? | _yes / no — gates FE-TC-40..44_ |
| `EXPO_PUBLIC_GOOGLE_CLIENT_ID` set? | _yes / no — affects FE-TC-19_ |
| Projects run | _chromium / mobile (Pixel 7)_ |
| Locale(s) exercised | _ar (default) / en_ |

## Results

| Case ID | Title (short) | Project(s) | Status | Defect / note |
|---|---|---|---|---|
| FE-TC-01 | Signed-out boot → Login | | | |
| FE-TC-02 | Splash brand chrome (LTR wordmark) | | | |
| FE-TC-03 | Login renders all affordances | | | |
| FE-TC-04 | Invalid credentials → generic banner | | | |
| FE-TC-05 | Login language switch flips dir/fonts | | | |
| FE-TC-06 | Login theme toggle dark↔light | | | |
| FE-TC-07 | Theme persists across reload | | | |
| FE-TC-08 | Persona toggle switches selection | | | |
| FE-TC-09 | Login a11y roles/labels | | | |
| FE-TC-10 | Empty-field zod validation blocks submit | | | |
| FE-TC-11 | Register form + feature panel + step indicator | | | |
| FE-TC-12 | Password strength meter reacts | | | |
| FE-TC-13 | Terms consent gates submit (default off) | | | |
| FE-TC-14 | Register success → add-child | | | |
| FE-TC-15 | Duplicate-email / weak-password inline | | | |
| FE-TC-16 | No student self-register path | | | |
| FE-TC-17 | Parent reaches parent home (not child) | | | |
| FE-TC-18 | Wordmark/fields stay LTR in Arabic | | | |
| FE-TC-19 | Google button disables when ID unset | | | |
| FE-TC-20 | Session-expired flash surfaces | | | |
| FE-TC-21 | "Create parent account" → Register | | | |
| FE-TC-22 | Register "Sign in" → Login | | | |
| FE-TC-23 | My Children loading skeletons | | | |
| FE-TC-24 | My Children load-error + retry | | | |
| FE-TC-25 | Sidebar nav active-state per page | | | |
| FE-TC-26 | Sidebar child-selector + nav a11y | | | |
| FE-TC-27 | My Children hero + cards + Add CTA | | | |
| FE-TC-28 | Subtitle count == card count | | | |
| FE-TC-29 | My Children RTL layout | | | |
| FE-TC-30 | Add Child CTA → add-child form | | | |
| FE-TC-31 | Edit Child opens pre-filled + saves | | | |
| FE-TC-32 | Reports empty-state renders (not broken) | | | |
| FE-TC-33 | Overview header + KPIs + focus areas | | | |
| FE-TC-34 | Mastery = 4 product subjects, no mock | | | |
| FE-TC-35 | Daily-activity chart placeholder | | BLOCKED | placeholder → P5-05-FE |
| FE-TC-36 | Child-selector limitation (documented) | | | |
| FE-TC-37 | Settings Language switch app-wide + persist | | | |
| FE-TC-38 | Settings Profile load/edit/save | | | |
| FE-TC-39 | Secondary tabs "coming soon" not broken | | | |
| FE-TC-40 | Landing hero renders | | BLOCKED | harness gap (marketing server) |
| FE-TC-41 | Landing features + sections | | BLOCKED | harness gap |
| FE-TC-42 | Landing CTAs → Register/Login | | BLOCKED | harness gap |
| FE-TC-43 | Landing subjects = 4 (no Social Studies) | | BLOCKED | harness gap |
| FE-TC-44 | Landing en-LTR only | | BLOCKED | harness gap |
| FE-TC-45 | App-wide RTL/LTR via Settings | | | |
| FE-TC-46 | Sidebar collapses ≤768 | | | |
| FE-TC-47 | Auth split-panel collapses on mobile | | | |
| FE-TC-48 | Full Reports page (KPIs/charts) | | BLOCKED | placeholder (deferred) |
| FE-TC-49 | Overview empty-state (no children) | | | |
| FE-TC-50 | Avatar upload type/size guards | | | |
| FE-TC-51 | Settings six-tab bar renders | | | |
| FE-TC-52 | Settings RTL layout | | | |
| FE-TC-53 | Settings narrow layout (<768) | | | |

## Summary (fill after the run)

| Metric | Count |
|---|---|
| Total | 53 rows (48 active + 5 pre-marked BLOCKED) |
| PASS | |
| FAIL | |
| BLOCKED | |
| SKIP | |

## Defects filed (→ `frontend`)

| Defect ID / link | Case(s) | Severity | Summary |
|---|---|---|---|
| | | | |

## testID requests (→ `frontend`)

> Stable `data-testid` hooks the suite needed but the screens lack (see README open question #2).

| Surface | Element | Suggested testID |
|---|---|---|
| | | |
