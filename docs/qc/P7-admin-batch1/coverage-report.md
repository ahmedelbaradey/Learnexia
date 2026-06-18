# Coverage Report — P7 Admin Console Batch 1 (P7-06 + P7-07 + P7-08)

> Designed by `qc-test-designer` (2026-06-18). Companion to `frontend-test-cases.md`. Maps every test case → story acceptance criterion, flags gaps, lists the exact test data / seeding the E2E run needs, the BE contract-smoke set (pre-existing coverage), and the **handoff** (missing `testID`s + harness prerequisites) that `frontend` must address before the specs run.

## 1. Summary

- **Story scope (Batch 1):** the admin-dashboard frontend wave for User & Account management — P7-06 (search + inspect users), P7-07 (suspend / reactivate / delete), P7-08 (child profile edit / grade override / learning-language change). Shipped to `main` in `apps/admin-dashboard` (Next.js 15, port 3001).
- **Focus:** **frontend E2E** of the admin screens. The backend admin endpoints (`AdminUsersController`, all `[Authorize(AdminOnly)]`) are already built, merged, and tested in earlier phases — **BE coverage is treated as pre-existing**; only a short contract-smoke set is listed (§4), and it is the responsibility of `api-tester` only if the lead wants a re-verify.
- **Counts:**
  - **Total cases: 77** — all **frontend** (`frontend-e2e-tester`). 0 net-new backend cases (BE pre-existing).
  - By section: Auth/routing 7 (FE-TC-01..07); P7-06 list 13 (08..20); P7-06 detail 13 (21..33); P7-07 lifecycle 17 (34..50); P7-08 child edit 15 (51..65) incl. shared dialog a11y (65..68); cross-cutting RTL/a11y/PII/no-optimistic 9 (69..77).
  - By priority: **P0 = 38**, **P1 = 27**, **P2 = 12**.
- **Coverage verdict:** **Every acceptance criterion across all three stories is covered by at least one P0/P1 case.** See the matrices in §2. Notable weak spots (responsive list collapse, true runtime RTL, audit visibility) are NOT failures of these specs but limitations/deviations of the shipped surface — called out in §3 + §5.

## 2. Coverage matrix (AC → case IDs)

### P7-06 — Search & inspect users (`docs/briefs/P7-06.md` AC 1–10)

| AC | Acceptance criterion (abbrev.) | Covering case IDs | Verdict |
|---|---|---|---|
| 1 | List + role/status filters + free-text + server pagination; page-1 reset on filter change | FE-TC-08, 12, 13, 14, 15, 16, 18, 19 | Covered (P0) |
| 2 | Four list states (loading / empty / error+retry / results); debounced; placeholderData | FE-TC-08, 09, 10, 11, 12, 17 | Covered (P0) |
| 3 | Read-only profile (name/email/role/status/dates; reason when present); no mutation controls in 06 scope | FE-TC-21, 22, 33 | Covered (P0) |
| 4 | Both language fields shown **distinctly** (preferred vs learning); child-only fields hidden for non-children | FE-TC-23, 24 | Covered (P0 headline) |
| 5 | Family panel (parent→children / child→parents); child email hidden; deep-links | FE-TC-25, 26, 27 | Covered (P0) |
| 6 | Activity summary best-effort; null→"no data"; sign-in "not tracked" | FE-TC-28, 29, 22 | Covered (P1) |
| 7 | Detail states (loading / not-found / error); family + activity load independently | FE-TC-30, 31, 32 | Covered (P0) |
| 8 | Admin-only gate; signed-out / non-admin → /login; no PII leak | FE-TC-01, 02, 03, 07, 77 | Covered (P0) |
| 9 | Functional "Users" nav (active state) + per-page topbar title; single shell | FE-TC-04, 05, 06 | Covered (P1) |
| 10 | i18n EN+AR + RTL-safe + a11y (table semantics, aria-current, role=alert) | FE-TC-69, 70, 71, 72, 73, 74 | Covered (P0/P1); RTL conditional — see §3 |

### P7-07 — Suspend / reactivate / delete (`docs/briefs/P7-07-FE.md` AC 1–8)

| AC | Acceptance criterion (abbrev.) | Covering case IDs | Verdict |
|---|---|---|---|
| 1 | Status-aware legal-action menu (Active→{Suspend,Delete}; Suspended→{Reactivate,Delete}; Deleted→none) | FE-TC-34, 35, 36 | Covered (P0) |
| 2 | Suspend: required reason ≤500 + confirm; governance copy (not lockout, no over-promise); → Suspended | FE-TC-37, 38, 40 | Covered (P0) |
| 3 | Reactivate: optional reason + prior history; → Active | FE-TC-41, 42, 43 | Covered (P0) |
| 4 | Delete: two-step (required reason + typed-email confirm) gate; parent-only cascade (default off); confirm:true only at final step | FE-TC-44, 45, 46, 47 | Covered (P0) |
| 5 | Envelope errors inline (already-*/protected/validation/424/network); dialog stays open | FE-TC-39, 40, 43, 48, 49, 50 | Covered (P0) |
| 6 | Admin-only gate | FE-TC-01, 02 (shared) | Covered (P0) |
| 7 | Mutation hooks invalidate profile + list → refetch new status (not optimistic) | FE-TC-38, 42, 47, 76 | Covered (P0) |
| 8 | RTL/ar + a11y: focus-trap, role=dialog/aria-modal, ESC closes, gated destructive button, role=alert | FE-TC-65, 66, 67, 68, 72, 74 | Covered (P0/P1); RTL conditional |

### P7-08 — Child profile / grade / learning-language (`docs/briefs/P7-08-FE.md` AC 1–10)

| AC | Acceptance criterion (abbrev.) | Covering case IDs | Verdict |
|---|---|---|---|
| 1 | Child-only surface (Student role gate; never parents/admins) | FE-TC-51, 52, 24 | Covered (P0) |
| 2 | Edit harmless profile (preferredLanguage + country); changed-fields-only PATCH; no progress warning | FE-TC-53, 54, 55 | Covered (P0) |
| 3 | LearningLanguage is a separate, clearly-labelled control (not in the plain PATCH) | FE-TC-52, 56 | Covered (P0) |
| 4 | LearningLanguage change confirm-gated + destructive; typed confirm; Math/Science reset copy; Arabic/English unaffected | FE-TC-56, 61, 62 | Covered (P0) |
| 5 | No confirm ⇒ nothing happens; 424 surfaced; no optimistic wipe | FE-TC-61, 63, 76 | Covered (P0) |
| 6 | Grade override 1–6 + (FE-required) reason + confirm; re-scope/preserve copy | FE-TC-57, 59 | Covered (P0) |
| 7 | Validation/envelope errors inline (422 range, 400 same-grade/confirm, 422 lang) — never raw stack | FE-TC-55, 58, 60, 63, 64 | Covered (P0/P1) |
| 8 | Admin-only gate | FE-TC-01, 02, 07 | Covered (P0) |
| 9 | Cache coherence — detail invalidated on success (+ child Math/Science on language change) | FE-TC-54, 59, 62, 76 | Covered (P0) |
| 10 | RTL/i18n + a11y (focus-trap, ESC=cancel only, destructive not color-alone) | FE-TC-65, 66, 67, 68, 70, 71, 72, 74 | Covered (P0/P1); RTL conditional |

### Cross-cutting (lead's explicit asks)

| Theme | Covering case IDs |
|---|---|
| RTL ar + en, dir flips, technical strings dir=ltr, both locales no raw keys | FE-TC-69, 70, 71, 72 |
| a11y (dialog role/aria-modal/focus-trap, semantic table, aria-live) | FE-TC-67, 68, 73, 74 |
| No PII in console/toasts/URL | FE-TC-75 |
| No optimistic mutation | FE-TC-76 |

## 3. Gaps, weak coverage, and shipped deviations the reviewer must weigh

These are **not** holes in the test design — they are limitations of the **shipped surface**. Each is encoded into a case (with `[DEVIATION]` or BLOCKED guidance) rather than dropped.

1. **No runtime RTL/locale toggle (biggest i18n gap).** `ADMIN_LOCALE='en'` is a module constant; `getStrings()` runs at module load. A user cannot reach Arabic/RTL in the running app. RTL cases (FE-TC-70/71/72) therefore require a **separate `ar` build/run**; if the tester cannot rebuild, they are **BLOCKED — Design Gap 4**. AR copy *correctness* is still partially verifiable by reading `lib/strings.ts`, but layout-direction behaviour cannot be exercised at runtime. **Open question Q-A.**
2. **List is not responsive.** The Design Spec (Part B.3) specifies a stacked-card layout < 768px and a hidden "Created" column at tablet width. The shipped `users/page.tsx` renders a fixed 5-column `<table>` at all widths. FE-TC-08 asserts the 5-column table and flags the missing responsive collapse. **Weak coverage of the responsive AC — by omission in the code, not the tests.**
3. **Profile-PATCH success copy bug.** `edit/page.tsx` `handleSave` sets the success banner message to `childEditSaveChanges + ' — ' + gradeDialogSuccess` → "Save Changes — grade has been updated." for a country/language save. FE-TC-54 asserts a banner appears and flags the wrong string.
4. **Profile-PATCH 422 mis-mapped copy.** `mapProfilePatchError` maps a 422 to `gradeError422` ("Grade must be between 1 and 6.") — wrong message for an unsupported language/country. FE-TC-55 asserts the banner and flags the mis-map.
5. **Detail not-found uses the error branch, not the dedicated NotFoundState.** A 404 sets `isError=true`, so the page shows the error banner ("User not found.") + Back button; the dedicated `NotFoundState` card only renders when `!isError && !profile` (effectively unreachable for a real 404). FE-TC-32 asserts the user-visible outcome (friendly message + route back) and flags the branch.
6. **Edit child-gate shows a warning + manual back, not an auto-redirect** (Design Spec F.1 said redirect). FE-TC-51 asserts the warning + absent form.
7. **Audit visibility (P7-12) is out of E2E scope.** Profile reads + lifecycle actions emit `AdminActionPerformedEvent` server-side; there is no FE surface for it, so no E2E case asserts the audit row. Flagged so the reviewer/security-auditor confirm the audit seam separately (not via these specs).
8. **Residual access-JWT window on suspend (G2).** Suspend revokes refresh + sessions but an already-issued access token survives until expiry. E2E asserts the **copy does not over-promise** instant logout (FE-TC-37); it does **not** attempt to verify token revocation timing (out of E2E scope — backend concern).
9. **Duplicate action surfaces.** Lifecycle actions render in BOTH the header menu and the secondary "Actions" card. Tests drive the header; FE-TC-34 spot-checks the secondary card. Not a gap, but note both open the same dialogs (a single state).

## 4. Backend contract-smoke set (PRE-EXISTING — informational; only if lead wants a re-verify)

The backend is built/merged/tested in earlier phases; **BE coverage is pre-existing**. If the lead asks `api-tester` to re-confirm the live contract the FE depends on, this minimal smoke set suffices (all against `api/Admin/Users`, all `[Authorize(AdminOnly)]`, envelope `BaseResponse<T>`/`Successed`). These are **not** new requirements.

| ID | Smoke check | Expected |
|---|---|---|
| BE-SMOKE-01 | `GET /api/Admin/Users` anonymous | 401 |
| BE-SMOKE-02 | `GET /api/Admin/Users` with a Parent (non-admin) token | 403 |
| BE-SMOKE-03 | `GET /api/Admin/Users?Role=Student&Status=0&Q=…&PageNumber=1&PageSize=20` as admin | 200; `PaginatedResult<AdminUserListItemDto>` envelope (`currentPage/totalCount/totalPages/pageSize/data[]`); list items carry no grade/language/country |
| BE-SMOKE-04 | `GET /api/Admin/Users/{id}` as admin (valid child) | 200; `AdminUserProfileDto` with **both** `preferredLanguage` + `learningLanguage`; `lastSignInAtUtc` null |
| BE-SMOKE-05 | `GET /api/Admin/Users/{nonexistent}` | 404 |
| BE-SMOKE-06 | `GET /api/Admin/Users/{parentId}/family` | 200; `children[]` populated; child members have `email: null` |
| BE-SMOKE-07 | `GET /api/Admin/Users/{id}/activity` (seam failure) | 200 (never 500); null sections allowed |
| BE-SMOKE-08 | `POST …/{id}/suspend` empty reason | 422 (validation) |
| BE-SMOKE-09 | `POST …/{id}/suspend` valid, then again | 200, then 400 (already suspended) |
| BE-SMOKE-10 | `DELETE …/{id}` with `confirm:false` | 424 (FailedDependency); no mutation |
| BE-SMOKE-11 | `POST …/{childId}/grade` grade 9 | 422; grade=current → 400; `confirm:false` → 400 |
| BE-SMOKE-12 | `POST …/{childId}/learning-language` `confirmFreshStart:false` | 424; unsupported lang → 422 |
| BE-SMOKE-13 | Any P7-08 endpoint on a non-Student id | 404 (role guard message: "not a child account") |

> **BE verdict:** pre-existing; no new BE cases authored. `backend-test-cases.md` is intentionally **omitted** from this folder (no net-new HTTP surface to design tests for).

## 5. Risk notes (where the cases are weighted, and why)

- **Destructive child actions (highest risk).** Delete (with cascade) + learning-language fresh-start hard-delete child Math/Science data. Cases are weighted P0 on: the two-gate Delete (FE-TC-44/45/46/47), `confirm`/`confirmFreshStart:true` sent only at the final step (FE-TC-47/62), the typed-`CONFIRM` case-sensitivity (FE-TC-61), no optimistic wipe (FE-TC-76), backdrop-no-dismiss (FE-TC-66), and no-PII-leak (FE-TC-75).
- **The two-language confusion (P7-06's headline AC 4).** Conflating `preferredLanguage` and `learningLanguage` would be a correctness + safety bug (an admin could think they're editing the UI language when they're wiping Math/Science). FE-TC-23 is P0 and asserts the two distinct labelled rows.
- **Auth boundary.** The FE guard is UX-only; the backend is the real gate. Cases assert anon + non-admin both redirect and never render PII (FE-TC-01/02/07), and sign-out clears the cache (FE-TC-77). The known client-side-only middleware (Q6/D11) is accepted debt — not re-raised.
- **Status-machine correctness.** Wrong legal actions per status (e.g. offering Reactivate on a Deleted account) would invite illegal-transition errors. FE-TC-34/35/36 lock the matrix.
- **Refetch-not-optimistic.** Because the mutations return only a message string, the UI must refetch. A regression to optimistic updates would show stale/wrong state on error. FE-TC-76 guards this across all three mutations.

---

## 6. HANDOFF — missing `testID`s + harness prerequisites (action for `frontend` before the E2E run)

**Two blockers must be resolved before `frontend-e2e-tester` can run these specs deterministically.**

### 6a. Playwright harness has no admin-dashboard project
- The existing harness (`tests/e2e/playwright.config.ts`) only defines **student-app (:8081)** and **marketing (:3002)** projects + webServers. There is **no project / webServer for the admin-dashboard on :3001**.
- **Needed:** add an `admin` Playwright project with `baseURL: http://localhost:3001` and a `webServer` entry that starts the admin app (e.g. `pnpm --filter @learnexia/admin-dashboard dev` on port 3001), plus a `testMatch` for the new admin spec(s) (e.g. `specs/P7-admin-batch1.spec.ts`). Mirror the marketing project pattern. Without this the specs have nowhere to run.
- The .NET backend at `:5080` is a prerequisite (same as the student-app specs) — admin auth + all data come from it.

### 6b. The admin app ships ZERO `data-testid` hooks
The admin screens are built with plain Tamagui `@tamagui/core` + raw HTML, with **no `testID`/`data-testid` anywhere**. Per `tests/e2e/README.md` the preferred selector is `getByTestId`. The specs CAN run today on `getByRole`/`getByLabel`/text (the surface is well-labelled: `role="dialog"`, `role="alert"`, `aria-current`, `<th scope>`, wired `<label>`s), **but** several elements are ambiguous without a stable hook (two action surfaces, repeated badges, repeated banners, multiple `role="button"` rows). To make the specs robust, `frontend` should add these `testID`s (RN-Web-agnostic — here just `data-testid` on the element):

**List page (`users/page.tsx`):**
- `users-search-input` (the `<input type="search">`)
- `users-role-filter`, `users-status-filter` (the two `<select>`s)
- `users-clear-filters` (clear button)
- `users-table`, `users-result-count`
- `users-row-{id}` on each `<tr>` (so a deep-link target is unambiguous)
- `users-empty-state`, `users-error-banner`, `users-loading`, `users-pagination-prev`, `users-pagination-next`, `users-page-indicator`

**Detail page (`users/[id]/page.tsx`):**
- `user-detail-header`, `user-detail-status-badge`, `user-detail-role-badge`
- `user-detail-lang-preferred`, `user-detail-lang-learning` (the two distinct language rows — directly supports the P7-06 AC 4 headline assertion)
- `user-detail-success-banner` (the post-action banner)
- Header action buttons: `lifecycle-suspend-btn`, `lifecycle-reactivate-btn`, `lifecycle-delete-btn`, `lifecycle-terminal-notice`
- `child-edit-entry-btn` (the "Edit Profile" link in the child block)
- `user-family-panel`, `user-activity-panel` (so independent-failure assertions don't depend on heading text)

**Dialogs (shared `AdminConfirmDialog` + each dialog):**
- `admin-dialog` on the dialog card (or a per-dialog id: `suspend-dialog`, `reactivate-dialog`, `delete-dialog`, `grade-dialog`, `change-language-dialog`)
- `dialog-confirm-btn`, `dialog-cancel-btn`
- `reason-field` (the `ReasonField` textarea), `reason-field-counter`, `reason-field-error`
- `typed-confirm-input`, `typed-confirm-target`, `typed-confirm-match`
- `delete-cascade-checkbox`
- `grade-select`, `lang-select`, `grade-preserve-notice`, `lang-loss-block`, `lang-kept-line`

**Edit page (`users/[id]/edit/page.tsx`):**
- `child-edit-country`, `child-edit-display-lang`
- `child-edit-change-lang-btn`, `child-edit-override-grade-btn`
- `child-edit-save-btn`, `child-edit-not-student-warning`, `child-edit-banner`

> If `frontend` cannot add these before the run, the tester proceeds with role/label/text selectors (English default) and notes the fragility in `execution-report.md`. The two **language-row** testIDs (`user-detail-lang-preferred` / `user-detail-lang-learning`) are the highest-value additions — they make the P7-06 AC 4 "never merged" assertion unambiguous.

### 6c. Backend / seed prerequisites for admin auth
- **An ADMIN account is required** — the admin app has **no self-registration** (login page only). See §7 for how to obtain one. This is a hard prerequisite for *every* case.
- **Deleted-user fixtures** can only be reached by direct id navigation (deleted users are filtered from the default list). The run must create one via the admin UI/API during setup (see §7) or have a seeded one.

---

## 7. Test Data / Seeding (what the E2E run needs)

The run is **admin-gated end to end**. Seed via the API where possible; create disposable fixtures per run with unique emails (`+e2e+<timestamp>`), mirroring the existing harness convention (`tests/e2e/specs/P1-09-FE.spec.ts`).

### 7.1 ADMIN account (mandatory — how to obtain)
The admin app login (`/login`) authenticates against the same Identity backend, but **there is no admin self-register** ("Authorised personnel only. No self-registration."). Obtain an admin credential by one of:
1. **Seeded/dev admin:** check `docs/dev/HANDOFF.md` for a seeded admin/SuperAdmin username+password in the Development DB seed. **(Preferred — confirm the exact credentials with the lead; this is open question Q-B.)**
2. **DB/role grant:** create a normal account and grant it the Admin role directly in the `Learnexia` Postgres (Identity) for the test environment, then log in via `/login`.
3. **Token injection:** if a valid admin access token can be minted/obtained, inject it into the admin app's `sessionStorage` (the app reads tokens via `createWebTokenStorage`) + `authStore` before navigating — bypassing the login UI. The guard decodes the role from the token (`tokenGrantsAdmin`), so a token with an `Admin`/`SuperAdmin` role claim suffices.

The chosen path should be wired into a Playwright setup/fixture so every spec starts authenticated as admin (or a stored `storageState`).

### 7.2 Seeded fixtures (create in setup; prefer the parent-driven onboarding API used by the student app)
| Fixture | Why / used by |
|---|---|
| **Parent P with ≥2 children** (one child `learningLanguage='ar'`, `preferredLanguage='ar-EG'`, grade 3; another any) | Family panel children list (FE-TC-25), child detail two-language rows (FE-TC-23), child edit (FE-TC-52..62), delete-cascade (FE-TC-46/47) |
| **Student/child S** (linked to P) | Child-only gate (FE-TC-51 contrast), detail (FE-TC-23/26), grade + language flows |
| **Active disposable user** (per destructive test) | Suspend happy path (FE-TC-38) without corrupting shared data |
| **Suspended user** (Active user suspended via the admin UI/API in setup, carrying a `lastStatusReason`) | Suspended menu (FE-TC-35), reactivate (FE-TC-41/42), status-reason header (FE-TC-33) |
| **Deleted user** (Active user deleted via the admin UI/API in setup) | Terminal menu (FE-TC-36) — reachable only by direct id navigation |
| **>20 users total** (bulk-seed or rely on accumulated data) | Pagination (FE-TC-15/16/17) — needs `totalPages > 1` |
| **A user/child with a mixed-case email** (e.g. `Jane.Doe@Example.com`) | Case-insensitive delete-confirm (FE-TC-45) |
| **A child with PARTIAL activity** (some gamification null) or a stubbed `…/activity` | "No data" sections (FE-TC-28) |

**Seeding mechanics:** the cleanest path is to drive the **parent-driven onboarding API** (parent register → add children) the student app already uses to create the Parent+children, then use the **admin endpoints themselves** (suspend/delete) during setup to produce the Suspended/Deleted fixtures — which doubles as live exercise of those endpoints. Keep each spec hermetic (unique emails, no cross-spec order dependence). For error-path cases (422/424/500, partial activity), use `page.route` interception rather than trying to force server errors.

---

## 8. Open questions / assumptions for the lead (resolve before the E2E run)

- **Q-A (RTL reachability):** there is no runtime locale toggle (`ADMIN_LOCALE='en'`). Is the E2E run expected to (a) build/run a second `ar` instance to exercise RTL (FE-TC-70/71/72), or (b) accept English-only runtime coverage + treat AR as design-spec/strings-file verification only? **Recommend (a)** if a quick `ADMIN_LOCALE='ar'` build is feasible; otherwise mark the RTL cases BLOCKED with this reason.
- **Q-B (admin credentials):** what is the exact admin/SuperAdmin login (or token-mint path) for the test environment? Needed for the auth fixture (every case depends on it). Not in the briefs.
- **Q-C (shipped-copy bugs — fix now or file?):** FE-TC-54 (profile-save success message reuses "grade has been updated.") and FE-TC-55 (profile-PATCH 422 mapped to "Grade must be between 1 and 6.") are copy/mapping bugs in `edit/page.tsx`. Should `frontend` fix these before the E2E run (so the specs assert correct copy), or should the specs assert current behaviour and file defects? **Recommend: fix before the run** (small, clearly wrong copy on a child-data screen).
- **Q-D (testIDs + harness):** confirm `frontend` will add the §6 testIDs and an admin Playwright project before the run. If not, the tester proceeds with role/label selectors and notes fragility.
- **Assumption:** the lead wants **no net-new backend tests** (BE is pre-existing); `backend-test-cases.md` is intentionally omitted and §4 is informational only. Confirm if a BE re-verify is actually wanted.

---

## 9. Handoff

- **`frontend-e2e-tester`** implements **`frontend-test-cases.md`** (all 77 FE-TC cases) against the running admin-dashboard (port 3001) + the .NET backend (:5080), after `frontend` adds the §6 testIDs + admin Playwright project and the lead resolves Q-A/Q-B.
- **`api-tester`** has **no `backend-test-cases.md`** to implement here — BE coverage is pre-existing; the §4 smoke set is available only if the lead requests a re-verify.
- Both write outcomes into **`execution-report.md`** in this folder (pass/fail/blocked per case + defects). `qc-test-designer` scaffolds the empty template; it never fills results.
