# P1-03-FE — QC test plan + coverage report

Story: **Parent completes onboarding and adds children** (`user-stories/Phase-1-Foundation/P1-03-complete-onboarding.md`)
Task: `tasks/Frontend/student-app/Phase-1-Foundation/P1-03-FE.md`
Run: **frontend-only** (student-app web E2E). No `backend-test-cases.md` produced (the API surface is QC'd separately; backend-QC contract notes are honoured here).

## 1. Summary

This run designs the web-E2E test catalog for the **parent add-child / onboarding** flow and the **My Children** surfaces where added children must appear. The surfaces under test are already merged to `main` (per HANDOFF: P1-01/02/03/04 + the P8-01 learning-language field + P1-12 edit-child are all in).

Scope in:
- `app/(onboarding)/add-child.tsx` + `_components/AddChildForm.tsx` (the one-child form, incl. the P8-01 learning-language vs app-language group).
- The in-page draft list (`ChildCard`) + multi-child add/edit/remove + sequential submit loop with per-child success/failure.
- `EditChildSheet` in **in-memory (onboarding) mode** only.
- `app/(onboarding)/complete.tsx` + `_layout.tsx` (wizard chrome, 2 steps).
- `MyChildren` (native list) + `MyChildrenWeb` (dashboard grid) — where the added child must appear; loading/empty/error states.
- Routing guard `useAuthRoute` (parent-vs-child, signed-out, hasChildren).

Scope out (covered by sibling stories — not duplicated here):
- **Link existing child** flow → P1-04-FE.
- **Parent registration** form internals → P1-11-FE (only the no-student-self-register assertion is kept here).
- **Backend-wired EditChildSheet** (`childId` mode) + change-learning-language modal → P1-12-FE / P8-04-FE.
- App-side localization switch persistence → P8-99-FE.

Counts: **21 cases**, all frontend (`frontend-e2e-tester`).
- By priority: **P0 = 9** (FE-TC-01,02,05,06,07,09,12,18,19) · **P1 = 9** (03,04,08,10,13,15,16,17,20) · **P2 = 3** (11,14,21).
- By group: A add-child happy path (4) · B validation/errors (4) · C language axes (3) · D RTL/LTR (3) · E states (4) · F product overrides (3).

## 2. Coverage matrix (acceptance criterion → cases)

| Acceptance criterion (story) | Case IDs | Status |
|---|---|---|
| Add a child: enter details + set grade (1–6), language (ar/en), country | FE-TC-01, FE-TC-20, FE-TC-09/10/11 (language), FE-TC-21 (ar/en only) | Covered |
| Add more than one child in the same flow; each gets a separate profile/account | FE-TC-02 (+ FE-TC-03 remove, FE-TC-04 edit) | Covered |
| Adding a child provisions a child account with a **parent-assigned login email** | FE-TC-01 (email captured + provisioned), FE-TC-18 (persisted) | Covered |
| Onboarding completion is a **parent action**; child cannot self-register/self-onboard | FE-TC-19 | Covered |
| Invalid grade (outside 1–6) **or** email already in use → rejected with a specific message, no account created | FE-TC-20 (grade bound), FE-TC-07 (duplicate email), FE-TC-05/08 (validation + generic) | Covered |
| Each child's chosen language sets locale incl. RTL for Arabic (on child login) | FE-TC-12/13 (RTL/LTR of the onboarding surface itself), FE-TC-10 (language persisted to the child) | **Partial** — see gap G-1 |

### Gaps
- **G-1 (acceptance: "language sets the child's locale on first login"):** the *child-login* locale application is exercised by **P1-09-FE**, not by this onboarding flow. This run verifies (a) the onboarding surface honours ar/en RTL/LTR itself (FE-TC-12/13) and (b) the chosen `language` reaches the created child (FE-TC-10). The end-to-end "child logs in and sees their language" assertion is **out of scope here** and should be traced to P1-09-FE — flagged so it isn't double-counted or dropped.

All other acceptance criteria have at least one P0/P1 case. No criterion is fully uncovered.

## 3. Risk notes (where cases are weighted)

1. **Duplicate-email / error mapping (highest risk).** `perChildErrorKey` does **string matching** on the `BaseResponse` message ("exists"/"duplicate"/"grade"/"password"/"weak") plus `status === 409`. This is brittle — if the backend message wording changes, a duplicate could fall through to the generic message. Weighted P0 (FE-TC-07) + a generic-fallback case (FE-TC-08). Tester should capture the actual `BaseResponse.Message` the backend returns and confirm it still matches.
2. **Two-axis language UX (P8-01).** The learning-vs-app-language auto-fill + touched-guard is subtle and recently added; mis-wiring would silently send the wrong `language`/`learningLanguage`. Weighted P0/P1 (FE-TC-06, 09, 10).
3. **Add-child does not invalidate My-Children cache.** `useAddChild` has no `onSuccess` invalidation; the dashboard relies on `useMyChildren` refetching on a fresh mount after the `/complete → /(parent)` route replace. If a parent reaches My Children without a remount, the new child could be missing from a stale cache. Weighted FE-TC-18 + OQ-5.
4. **RTL-default correctness.** Arabic is the default locale; any LTR leakage on this form is a visible defect for the primary audience. FE-TC-12 (P0).
5. **Routing/role boundary.** A child reaching onboarding, or a signed-out user reaching add-child, would be a product-rule breach. FE-TC-19 (P0).

## 4. Open questions / assumptions (need lead/`frontend` input before implementation)

- **OQ-1 — Empty My-Children reachability:** `useAuthRoute` routes a 0-child parent to `/(onboarding)/add-child`, so the parent `(parent)` My-Children **empty** state (FE-TC-15) may be unreachable through normal navigation. Is there a supported path to `(parent)` for a childless parent, or should FE-TC-15 be marked **BLOCKED**? (Assumption: BLOCKED unless a direct route is available.)
- **OQ-2 — Missing `testID`s on the onboarding surfaces:** none of `add-child.tsx`, `AddChildForm`, `complete.tsx`, or the `(onboarding)` layout expose `testID`s, and `ChildCard`'s **edit/remove** controls (editable variant) and the per-field selects have no stable per-control hook beyond `aria-label`. Requested additions for robust selectors: `testID` on the add-to-list button, submit button, each `ChildCard`, the card edit + remove buttons, the partial-failure banner, and each language/grade select. Until added, cases use `aria-label`/`role`; FE-TC-03/04 are the most exposed.
- **OQ-3 — Locale switch mid-onboarding:** the locale control (`LocaleThemeControls`) lives on the login screen, not the onboarding chrome. Can the tester flip ar↔en while on add-child (FE-TC-13/14), or must locale be set pre-login? If unreachable mid-flow, FE-TC-14 → **BLOCKED**; FE-TC-13 should set locale before reaching onboarding.
- **OQ-4 — Forcing a generic/500 add-child error (FE-TC-08):** is there a deterministic backend condition (or is Playwright route-mocking the `addChild` POST acceptable) to exercise the non-duplicate error path? If neither, FE-TC-08 → **BLOCKED**.
- **OQ-5 — Stale My-Children cache:** confirm whether `useAddChild` should invalidate `queryKeys.family.myChildren()` (it currently does not). If the product expects the new child to appear without a full remount, this is a likely defect for `frontend` rather than a test gap.

Assumptions baked into the cases: Arabic is the default locale; the parent reaches add-child only via the auth guard (no deep-link seed bypass needed); unique emails are generated per run to avoid cross-test duplicate-email collisions.

## 5. Handoff

- **`frontend-test-cases.md` → `frontend-e2e-tester`.** Implement each FE-TC-* 1:1 as a Playwright spec under `tests/e2e/specs/P1-03-FE.spec.ts`. Honour the selector convention (`getByTestId` → `role`/`label`); where a `testID` is missing (OQ-2), use the documented `aria-label` and report the needed `testID` back to `frontend` rather than reaching into CSS. Mark OQ-blocked cases **BLOCKED** with the stated reason; do not force brittle paths.
- **No `backend-test-cases.md`** — frontend-only run by design.
- **`execution-report.md`** — the empty template is scaffolded in this folder. `frontend-e2e-tester` fills pass/fail per case + defects **after** running; QC does not fill results. Results then feed the `reviewer` gate.
