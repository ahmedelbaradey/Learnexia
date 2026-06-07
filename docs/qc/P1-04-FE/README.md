# QC Test Plan & Coverage Report — P1-04-FE (Link parent to child / child login & `/Me` routing)

> Surface: **student-app web PWA only** (Expo Router web build under `apps/student-app/app/`).
> Run type: **frontend E2E** (`frontend-e2e-tester` → Playwright `tests/e2e/specs/P1-04-FE.spec.ts`).
> Scope chosen by lead: linkage + role-driven routing. No `backend-test-cases.md` (frontend-only run).

## 1. Summary

**Story:** [user-stories/Phase-1-Foundation/P1-04-link-parent-to-child.md](../../../user-stories/Phase-1-Foundation/P1-04-link-parent-to-child.md) — FR-ID-3.
**FE tasks:** [tasks/Frontend/student-app/Phase-1-Foundation/P1-04-FE.md](../../../tasks/Frontend/student-app/Phase-1-Foundation/P1-04-FE.md) — FE-1 (My-Children list + child selector), FE-2 (link-existing-child form).

**What's in scope (this run):**
- Child sign-in → lands on the **child** home; parent sign-in → lands on the **parent** home (role-driven routing off `/Me` via `useAuthRoute`).
- Parent sees **only their own linked children** (family scope, observable in the UI — the client never sends a parent id; `useMyChildren` is JWT-scoped).
- "My Children" list reflects linked children (web grid `MyChildrenWeb`, mobile list `MyChildren`).
- Link-an-existing-child by email (`LinkChildForm`) incl. not-found / already-linked error surfacing.
- Loading / empty / error states on the My-Children surface.
- Arabic-default RTL vs English LTR across the above.
- Product overrides (no teacher persona; no student self-register; persona toggle is a UI hint only).

**Explicitly out of scope (covered by sibling stories — do not duplicate):**
- Login form mechanics, validation, session-expired flash, persona toggle visual → **P1-11-FE**.
- Add-child onboarding form + learning-language field → **P1-03-FE / P8-01-FE**.
- Child-home dashboard content (hearts/streak/XP/subjects) → **P1-09-FE / P2-09-FE**.
- Edit-child sheet, avatar, Google OAuth, password reset → **P1-12-FE**.
- Change-learning-language modal / fresh-start → **P8-04-FE**.
Here we touch those surfaces **only** as the observable endpoints of linkage + routing (e.g. "a child lands on the child home", not "the child home renders the right hearts").

**Counts:**

| Metric | Count |
|---|---|
| Total FE cases | 22 |
| Backend cases | 0 (frontend-only run) |
| P0 | 9 |
| P1 | 9 |
| P2 | 4 |
| BLOCKED (not testable yet) | 6 |

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria are largely backend/data-shape facts; this run validates the **observable FE consequences**. Routing-by-role is a story-implied FE requirement (the `/Me` consumer) and is the heart of this run.

| # | Acceptance criterion (story) | FE case(s) | Verdict |
|---|---|---|---|
| AC1 | Parent adds a child in onboarding → `ParentStudent` link created automatically | FE-TC-01, FE-TC-02 (link is **observable** as the child appearing in My-Children after onboarding) | Covered (observable) |
| AC2 | Existing parent can link an additional already-provisioned child | FE-TC-10, FE-TC-11, FE-TC-12 | Covered |
| AC3 | Parent accesses only that child's data — never other families' (FR-PA scoping) | FE-TC-06, FE-TC-07 | Covered (UI-observable scope) |
| AC4 | Parent linked to multiple children; child linked by >1 parent | FE-TC-03, FE-TC-08 | Covered |
| AC5 | Linking a non-existent child → clear error | FE-TC-13, FE-TC-14 | Covered |
| (story-implied) Role-driven routing off `/Me`: child → child home, parent → parent home | FE-TC-01, FE-TC-04, FE-TC-05, FE-TC-19 | Covered |
| (NFR) Arabic-default RTL vs English LTR | FE-TC-16, FE-TC-17, FE-TC-18 | Covered |
| (product) No teacher role / no student self-register / persona toggle is a hint only | FE-TC-20, FE-TC-21, FE-TC-22 | Covered |
| (state) loading / empty / error | FE-TC-02 (loading), FE-TC-09 (empty), FE-TC-15 (error+retry) | Covered |

**Gap flags:** No acceptance criterion is left without a case. **Caveat:** AC1/AC3/AC4 are server-enforced facts; the FE can only assert the *observable consequence*. Several cases require a seeded two-family fixture and a real child account; where the harness cannot yet seed that, the case is marked **BLOCKED** (see §3) rather than dropped.

## 3. Risk notes (where cases are weighted, and why)

1. **Role-driven routing off `/Me` (highest risk).** `useAuthRoute` branches on `me.data.roles` (student → `/(child)`), then `hasChildren` (parent → onboarding vs `/(parent)`). Failure modes weighted heavily: a child landing on the parent home (or vice-versa), a flash of the wrong surface during the `Me`-loading window (the guard is supposed to hold on the splash — FE-TC-19), and a parent with zero children being routed to onboarding rather than `/(parent)`. P0 cases FE-TC-01/04/05/19.
2. **Family scope as observable in the UI (IDOR-adjacent, P0).** The client never sends a parent id — scope is enforced server-side and surfaced via `useMyChildren`. The FE test can only assert "parent A sees A's children and not B's". Real cross-family proof needs two seeded families (FE-TC-06/07). Weighted P0 because a scope regression here is a privacy defect.
3. **Locale follows the child, not the device (P1).** On a signed-in child, `useAuthRoute` calls `setLocale(me.data.preferredLanguage)`. A child whose `preferredLanguage` differs from the parent's device locale must land in *their* language. This is a subtle linkage×i18n interaction (FE-TC-18).
4. **Link-existing error mapping (P1).** `LinkChildForm` maps 404 → not-found and 409 → already-linked, plus body-text hints. Wrong/raw-key surfacing is a UX defect (FE-TC-13/14). Note the query-key the form invalidates (`['family','my-children']`) matches `queryKeys.family.myChildren()` — list refresh after link must be verified (FE-TC-11).
5. **RTL default (P1).** Arabic is the **default** locale; copy-based selectors are unsafe. Most My-Children/link surfaces flip via logical row direction. Weighted across FE-TC-16/17.

## 4. Open questions / assumptions (lead must resolve before/at implementation)

**Selector / testID gaps (blocking clean implementation).** Per the convention (`getByTestId` first, then role/label), the following P1-04 surfaces currently expose **no `testID`** — only `accessibilityRole`/`accessibilityLabel` (i18n-keyed, Arabic-default). The tester should drive via role/aria-label where possible and **report these back to `frontend`** as needed hooks:
- `Q1` — **My-Children container + each child card** (`MyChildrenWeb` `ChildDashboardCard`, mobile `MyChildren` `ChildCard`): no `testID`. Need e.g. `testID="my-children-list"` and per-card `testID="child-card-<id>"` to assert list membership/count and family scope deterministically.
- `Q2` — **Child home vs parent home landing markers.** `(child)/index` exposes `testID="dashboard-header"`; `(parent)/index` (placeholder) and the parent dashboard (`MyChildrenWeb`) have **no** stable landing `testID`. Need a `testID` on the parent home root (e.g. `testID="parent-home"`) and confirm `dashboard-header` is the canonical child-home marker — these are the anchors for the routing assertions.
- `Q3` — **PersonaToggle tabs**: `accessibilityRole="radio"` + aria-label only (i18n). Acceptable via role, but a `testID` per tab (`persona-parent`/`persona-student`) would harden FE-TC-22.
- `Q4` — **LinkChildForm** email field / submit / success card / error banner: no `testID` (role/label only). Need `testID="link-child-email"`, `testID="link-child-submit"`, `testID="link-child-success"`.
- `Q5` — **Sidebar child-selector + nav items** (web): `accessibilityRole` button/menuitem only; a `testID="sidebar-child-selector"` would stabilize FE-TC-08.

**Fixture / harness assumptions (gating several cases).**
- `Q6` — Is there a **seed path** to create: (a) a parent with N linked children, (b) a **second** unrelated family, and (c) a real **child account** that can sign in? The E2E harness (`tests/e2e`) requires backend at `:5080`; the smoke spec only logs in an input. Without a child-account seed, **child-login routing (FE-TC-04/05/18) and cross-family scope (FE-TC-06/07) are BLOCKED**. Assumption pending lead: seed via the API (register parent → add child → obtain child credentials) per the standard "seed via the API" rule.
- `Q7` — **Can a child sign in with a username/password at all in this build?** Onboarding provisions children; confirm child credentials are returned/derivable so the persona=Student login path is exercisable. If child accounts are passcode/parent-managed only, FE-TC-04/05 convert to documented BLOCKED.
- `Q8` — **Where does the parent web dashboard actually land?** `useAuthRoute` routes a parent-with-children to `/(parent)` whose `index.tsx` is a branded **placeholder** ("coming soon" + link to My-Children), while the rich web dashboard lives at `/(parent)/children` (`MyChildrenWeb`). Confirm whether "parent home" for routing assertions = the placeholder `(parent)/index` or `(parent)/children`. FE-TC-01/05 assume `(parent)/index` is the landing; My-Children content cases (FE-TC-03/06...) navigate to `/(parent)/children`.

**Product assumption.** Persona toggle is a **UI hint only** (does not gate role) — routing is decided purely by `/Me` roles. FE-TC-22 asserts a parent selecting "Student" persona still lands on the parent home.

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| [frontend-test-cases.md](./frontend-test-cases.md) | `frontend-e2e-tester` | Implement as `tests/e2e/specs/P1-04-FE.spec.ts`. Drive via `getByTestId` → role/aria-label; **do not** use Arabic/English copy as selectors. For each missing `testID` (Q1–Q5), report the needed hook back to `frontend` rather than reaching into CSS. |
| [execution-report.md](./execution-report.md) | `frontend-e2e-tester` | Fill **after** the run — pass/fail per FE-TC, defects filed back to `frontend`, BLOCKED cases noted with reason. The QC architect created the empty template; the tester fills results (never the architect). |

**Verdict:** Every acceptance criterion has at least one covering case (AC1/AC3/AC4 covered as *observable* consequences). 6 cases are BLOCKED pending a child-account + two-family seed path (Q6/Q7) — these are listed, not dropped. Resolve Q6–Q8 and the testID gaps (Q1–Q5) before implementation for a clean, deterministic run.

Test cases ready — `frontend-e2e-tester` to implement `frontend-test-cases.md`; results written into `execution-report.md`. (No backend run — frontend-only.)
