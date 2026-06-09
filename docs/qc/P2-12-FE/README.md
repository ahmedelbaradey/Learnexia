# QC Test Plan & Coverage Report — P2-12-FE (Parent Settings tabs)

> **Scope:** student-app web PWA (Expo web) E2E **only** — frontend run. **No backend test cases** (per the lead's frontend-only directive; omit `backend-test-cases.md`).
> **Surface:** the **parent** Settings page (`/settings`) — the four tabs P2-12 added into the existing P1-11 six-tab shell: **Notifications**, **Linked children**, **Security**, **Plan & billing** — plus the tab-structure that wires them.
> **Owner of this doc:** qc (test architect). **Implementer:** `frontend-e2e-tester` (Playwright, `tests/e2e/specs/P2-12-FE.spec.ts`).
> **Harness:** Playwright owns the Expo web server at `http://localhost:8081`; backend live at `http://localhost:5080` is a prerequisite for any data flow. Selector convention: `getByTestId` → `getByRole`/`getByLabel`; never visible Arabic copy (AR is the default locale).

---

## 1. Summary

P2-12-FE completes the four "coming soon" tabs of the parent Settings page inside the already-built P1-11 shell. The four panels (`NotificationsPanel`, `LinkedChildrenPanel`, `SecurityPanel`, `PlanPanel`) are live-wired to their P2-12-BE endpoints with RTL/en + dark/light (HANDOFF: status Done, audited 2026-06-07).

This pass is deliberately weighted to the **net-new** value of P2-12 and explicitly **cross-references P1-11 / P1-12** rather than re-testing them:
- **Net-new (the focus):** tab structure that surfaces the four panels (A); **notification preferences** — the 4×2 optimistic-toggle grid with full-array PUT + rollback (B); **linked-children** list/edit/unlink with family-scope (C); plan & billing read-only stub (E); the per-child learning-language row presence (P8-04 cross-ref).
- **Thin cross-ref (not re-verified here):** the Settings shell/tab-bar/Profile/Language mechanics already E2E-tested in **P1-11-FE** (FE-TC-37/38/39/51/52/53); avatar/profile/Google mechanics in **P1-12-FE**; the change-language deep flow owned by **P8-04**; theme-toggle mechanics (a documented P1-11 E2E limitation).

**Counts**
- **Total cases:** 41 — all `frontend-e2e-tester`.
- **By surface:** Tab structure 3 · Notifications 9 · Linked children 12 · Security 7 · Plan & billing 4 · Cross-cutting 3 · BLOCKED 3.
- **By priority:** **P0 = 9** · **P1 = 17** · **P2 = 15**.
- **Status flags:** **3 BLOCKED** (FE-TC-39 multi-session seed, FE-TC-40 rollback-shake motion, FE-TC-41 AR font DG-W10-01) — listed, not dropped, each with its blocker.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria from the story's child stories P2-12a/b/c/d + the epic's cross-cutting RTL/en criterion.

| Story / criterion | Case ID(s) | Notes |
|---|---|---|
| **Epic** — four remaining tabs built into the P1-11 shell, reachable, not placeholders | FE-TC-01, FE-TC-02, FE-TC-03 | structural; extends P1-11 FE-TC-51 (tab count only) |
| **P2-12a Notifications** — toggles per type × channel (email/push), 4×2 grid | FE-TC-04 | 8 switch testIDs |
| **P2-12a** — toggles reflect saved prefs | FE-TC-05 | GET payload → `aria-checked` |
| **P2-12a** — changes save with success feedback (optimistic, full-array PUT) | FE-TC-06 | full 4-item PUT asserted |
| **P2-12a** — error feedback (rollback) | FE-TC-07 | 400 → rollback + localized banner |
| **P2-12a** — in-flight safety / loading | FE-TC-08, FE-TC-09 | disabled-while-pending; loading text |
| **P2-12a** — render en (LTR) + ar (RTL) | FE-TC-10, FE-TC-11, FE-TC-12 | no raw keys; RTL flip; switch a11y |
| **P2-12b Linked children** — lists children with status | FE-TC-13, FE-TC-21, FE-TC-22 | name + email meta; loading/error; P8-04 lang row |
| **P2-12b** — empty state | FE-TC-14 | |
| **P2-12b** — link (by email) entry point | FE-TC-15 | Add-child CTA → /add-child |
| **P2-12b** — edit child (inline form → PUT) | FE-TC-16, FE-TC-17 | validation gate + persist |
| **P2-12b** — unlink with confirm-before-unlink | FE-TC-18, FE-TC-19, FE-TC-20 | confirm strip; cancel no-op; last-parent 400 |
| **P2-12b** — family-scope (parent only manages own children) | **FE-TC-24** | IDOR-ish: parent A never sees child B |
| **P2-12b** — RTL (names AR, email LTR) | FE-TC-23 | |
| **P2-12c Security** — change password (current/new/confirm + strength) | FE-TC-25, FE-TC-26, FE-TC-28, FE-TC-29 | render; client-validation; success; wrong-current |
| **P2-12c** — other sessions invalidated on change | FE-TC-28 | sessions refetch after success |
| **P2-12c** — active-sessions list | FE-TC-30, FE-TC-31 | rows + pills; empty |
| **P2-12c** — sign-out-others count | **FE-TC-39 (BLOCKED)** | needs ≥2 real sessions |
| **P2-12c** — passwords forceLtr | FE-TC-27 | |
| **P2-12d Plan & billing** — current plan + status + manage CTA (read-only) | FE-TC-32, FE-TC-33, FE-TC-34 | disabled Manage; states |
| **P2-12d** — render en/ar | FE-TC-35 | Free → مجاني; AR eyebrow |
| **Epic** — render en (LTR) + ar (RTL), no raw keys, across tabs | FE-TC-10, FE-TC-35, FE-TC-36 | extends P1-11 FE-TC-52 |
| **Cross-cutting** — auth/role routing (signed-out → login) | FE-TC-37 | /settings deep-link guard |
| **Cross-cutting** — theme carries into panels (cross-ref P1-11) | FE-TC-38 | thin; toggle mechanics not re-verified |

**Gap verdict: every acceptance criterion is covered by at least one P0/P1 case.** The only criterion **without an executable (green) case** is P2-12c's sign-out-others **count** message (FE-TC-39), which is **BLOCKED** on a deterministic multi-session seed — listed, not a silent gap. The rollback-shake motion (FE-TC-40) and AR font resolution (FE-TC-41) are non-functional/design-gap checks, also BLOCKED with reasons; the underlying functional rollback is covered by FE-TC-07.

---

## 3. Risk notes (where the cases are weighted)

1. **Notifications optimistic toggle + full-array PUT (highest weight).** The riskiest net-new logic: the FE mutates local state immediately, then PUTs the **whole 4-item array** (the BE rejects partial arrays). A regression here means a toggle that silently doesn't persist, persists the wrong category, or fails to roll back on error. P0 coverage: FE-TC-05/06/07 (saved-state, optimistic full PUT body, rollback), plus FE-TC-08 (no double-fire in flight).
2. **Family-scope / IDOR on linked children.** A parent must see and act on **only their own** children. This is the security-sensitive heart of P2-12b (parent-driven product rule). FE-TC-24 is the explicit cross-parent negative (parent A never sees child B); FE-TC-20 covers the last-parent unlink guard. Weighted P0.
3. **Selector fragility — missing testIDs on net-new rows.** The notification switches have testIDs, but the **category rows, the linked-children `ChildCard`s, the inline edit/unlink strips, session rows, and the plan badge/CTA do not**. The `ChildCard` edit/remove icons use hard-coded EN `aria-label`s ("Edit child"/"Remove child") — usable but English-only. Cases route around this with role/label selectors and the tester should file a `frontend` ticket (see open questions). Mis-selection risk is the top authoring hazard.
4. **Arabic-default RTL.** The app boots RTL; copy selectors must never be Arabic strings. EN-dependent assertions require switching locale first (the Profile/Notifications/etc. tabs have no locale switch — only the Language tab `settings-language-switch`). RTL flip + forced-LTR technical strings (email, session id, passwords) are explicitly asserted (FE-TC-11/23/27/30).
5. **Backend dependency + endpoint reality.** Every data case needs `:5080` live and seeded; the real endpoints differ from some Design-Spec assumptions (notifications = `/api/Notifications/Preferences`, unlink = `/api/Parent/Unlink-Child`, etc. — corrected inline in the cases). Route interception must use the **actual** paths or the tests will silently not intercept.
6. **Cross-ref discipline.** The shell/tab-bar/Profile/Language are already covered by P1-11; re-testing them here would be wasted run-time and double-maintenance. This pass asserts only the *new* panels + the *new* structural fact (four real panels, no ComingSoon).

---

## 4. Open questions / assumptions (lead must resolve before implementation)

1. **Missing `testID`s on net-new rows (recommend a `frontend` ticket).** Add stable testIDs to: notification category rows (e.g. `notification-row-{catKey}`), linked-children `ChildCard`s (`child-card-{id}` via the existing `testID`/`editTestID`/`removeTestID` props, which the panel currently does not pass), the inline edit-save / unlink-confirm buttons, the learning-language Change button, session rows, and the plan Manage CTA. Until then the tester uses role/EN-aria-label selectors (locale-coupled for some). Confirm whether to file this.
2. **Backend seed for multi-session (BLOCKER for FE-TC-39).** Asserting the "Signed out {count} other sessions" count needs ≥2 genuine sessions for one user. Is there (or can there be) a seed helper that creates a second session (second login/context) for the seeded parent? Until then FE-TC-39 stays `test.skip`.
3. **Notification GET default contract.** The panel renders 4 defaults if the BE returns empty. Confirm the live BE always returns 4 categories (0..3) so FE-TC-04/05 can assert exactly 8 switches against real data — otherwise the tester must intercept the GET to make state deterministic.
4. **Route interception vs live backend for mutation cases.** FE-TC-06/07/17/18/20/28/29/32/34 use `page.route` to force success/error/timing deterministically. Confirm the tester may intercept the named endpoints (recommended for the error/optimistic paths) while still hitting the live backend for the happy-path persistence checks.
5. **AR font resolution (DG-W10-01, BLOCKER for FE-TC-41).** Confirm whether locale-aware fonts (Cairo/Tajawal) actually resolve for AR yet. If not, FE-TC-41 stays a tracked design defect, not an E2E assertion.
6. **EN-locale entry for stable copy.** Profile (default) and the four P2-12 tabs have no inline locale switch. Assumption: the tester switches to EN via the Language tab (`settings-language-switch`) or seeds an EN-preferred parent before asserting EN copy. Confirm the preferred approach.

---

## 5. Handoff

- **`frontend-test-cases.md`** → `frontend-e2e-tester`: implement each `FE-TC-*` as one Playwright test (1:1) in `tests/e2e/specs/P2-12-FE.spec.ts`, reusing the `seedParentWithChild` / `loginAsParent` / `uniqueEmail` helpers from `P1-11-FE.spec.ts`. Honor the 3 BLOCKED markers as `test.skip` with the blocker in the title; do not assert against the documented design gaps.
- **No `backend-test-cases.md`** — frontend-only run by directive; the P2-12-BE endpoints are exercised indirectly through the UI.
- **`execution-report.md`** → `frontend-e2e-tester` (and `frontend` for any bug it finds): the empty templated results table is in that file. The tester fills **pass/fail per case + defects** after running; qc does **not** fill results.
- Results feed the `reviewer` gate per the CLAUDE.md pipeline (`frontend` → `frontend-e2e-tester` → `reviewer`).
