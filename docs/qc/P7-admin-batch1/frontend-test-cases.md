# Frontend E2E Test Cases — P7 Admin Console Batch 1 (P7-06 + P7-07 + P7-08)

> Designed by `qc-test-designer` (2026-06-18). Target agent for **all** cases below: **`frontend-e2e-tester`** (Playwright vs the running admin-dashboard web app, port **3001**).
> These cases are designed against the **actually-shipped code on `main`** in `apps/admin-dashboard/app/(admin)/users/**` + `apps/admin-dashboard/components/*` + `apps/admin-dashboard/lib/strings.ts` + `packages/api-client/src/hooks/*`, not against the imagined design. Where shipped behaviour deviates from the Design Spec, the **Expected result describes the shipped behaviour** and the deviation is flagged inline as `[DEVIATION]` so the tester asserts reality and the reviewer can decide.
>
> **Read the handoff first:** see `coverage-report.md` → "Handoff: missing testIDs + harness prerequisites". The admin app currently ships **zero `data-testid` hooks** and the Playwright harness has **no admin project** (it targets student-app :8081 / marketing :3002). Both must be addressed by `frontend` before these specs can run deterministically. Until testIDs land, selectors fall back to `getByRole`/`getByLabel`/text — noted per case.

---

## Locale note (load-bearing for selectors)

The admin app is **English-first with NO runtime locale toggle** (`ADMIN_LOCALE = 'en'` is a module constant in `lib/strings.ts`; `getStrings()` is called at module load). **Arabic copy and RTL cannot be reached by a user action in the running app** — there is no UI switch and the locale is baked at build/module time. Therefore:
- The default E2E run asserts **English copy + LTR**.
- **RTL/ar coverage requires building (or running) the app with `ADMIN_LOCALE = 'ar'`** (a source edit or an injected build, NOT a click). The RTL cases below (FE-TC-40..44) are written as a **separate locale build/run**; if the tester cannot rebuild with `ar`, mark them **BLOCKED — no runtime locale toggle (Design Gap 4)** rather than dropping them. This is the single biggest i18n limitation of the shipped surface and is flagged as an open question in `coverage-report.md`.

## Selector strategy (per `tests/e2e/README.md`)
1. `getByTestId` — **not yet available** on this surface (see handoff). Use once `frontend` adds the listed testIDs.
2. `getByRole` / `getByLabel` — the primary usable strategy today: `role="dialog"`, `role="alert"`, `role="button"` (table rows + action buttons), `<th scope="col">`, native `<select>`/`<input>` via `getByLabel` (labels are wired), `aria-current="page"` on the active nav item, `aria-disabled` on gated confirm buttons.
3. Text — last resort; acceptable here because the default run is English-only, but prefer role/label so the same spec survives a future locale toggle.

## Conventions for every case
- **Seed via the API; assert via the UI.** Auth as an ADMIN (see `coverage-report.md` → Test Data / Seeding for how to obtain the admin token + seed Parent/Student/Suspended/Deleted fixtures). Use unique emails per run.
- The mutation hooks **invalidate and refetch** (never optimistic) — after a successful action, assert the new state appears **after** a refetch settles (the status badge changes because the profile query re-runs), not instantly from the response.
- Backdrop click must NOT dismiss any dialog; ESC must dismiss (cancel path only).

---

# SECTION 1 — Auth & routing (gate is shared across P7-06/07/08)

| Field | Value |
|---|---|
| **ID** | FE-TC-01 |
| **Title** | Unauthenticated visit to `/users` redirects to `/login` |
| **Type** | auth-authz |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Fresh browser context, no auth tokens in sessionStorage. |
| **Steps** | 1. `goto('/users')`. 2. Wait for navigation to settle. |
| **Expected result** | URL becomes `/login`. No user PII (no users table, no rows, no Users heading) renders at any point. `useAdminGuard` returns `redirecting` → `AdminShell` renders `null` then `router.replace('/login')`. Note the known race (Q6 / middleware pass-through): a brief shell flash is acceptable, but no PII data. |
| **Traces to** | P7-06 AC 8; P7-07 AC 6; P7-08 AC 8 |

| Field | Value |
|---|---|
| **ID** | FE-TC-02 |
| **Title** | Non-admin token (Parent/Student) is blocked from `/users` |
| **Type** | auth-authz |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Sign in (or inject sessionStorage tokens) as a **Parent** account (non-admin role claim). |
| **Steps** | 1. With a Parent access token in `authStore`/sessionStorage, `goto('/users')`. 2. Wait for settle. |
| **Expected result** | Redirected to `/login` (the guard treats "signed-in but not admin" identically to signed-out — `tokenGrantsAdmin(accessToken)` is false → `redirecting`). No users list/detail PII renders. |
| **Traces to** | P7-06 AC 8; P7-07 AC 6; P7-08 AC 8 |

| Field | Value |
|---|---|
| **ID** | FE-TC-03 |
| **Title** | Admin reaches `/users` and sees the list shell |
| **Type** | auth-authz / functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. `goto('/users')`. 2. Wait for the list to render. |
| **Expected result** | URL stays `/users`. The "User Management" heading (`usersListHeading`) renders; the filters bar (search input + Role select + Status select) renders; the table or one of the four states renders. AdminShell chrome (side nav + topbar) is present exactly once. |
| **Traces to** | P7-06 AC 8, AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-04 |
| **Title** | "Users" nav item is active (`aria-current="page"`) on `/users*` |
| **Type** | functional / a11y |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. `goto('/users')`. 2. Locate the side-nav "Users" link (`getByRole('link', { name: 'Users' })`). 3. `goto('/users/<seededId>')` and re-check. |
| **Expected result** | The Users nav `<Link href="/users">` has `aria-current="page"` on both `/users` and `/users/<id>` (active test is `pathname === '/users' || pathname.startsWith('/users/')`). The Curriculum and Content nav items are `aria-disabled` placeholders and have no `aria-current`. |
| **Traces to** | P7-06 AC 9, AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-05 |
| **Title** | AdminShell chrome renders exactly once per route (no double shell) |
| **Type** | regression / functional |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. `goto('/users')`, count nav (`role="navigation"`, `aria-label="Admin navigation"`) and topbar elements. 2. Repeat for `/users/<id>` and `/users/<childId>/edit`. |
| **Expected result** | Exactly **one** `nav[aria-label="Admin navigation"]` and **one** topbar per page. The `(admin)/users/layout.tsx` + `(admin)/users/[id]/layout.tsx` are pass-throughs; each leaf page self-wraps a single `<AdminShell>`. No nested/duplicate side nav or topbar. |
| **Traces to** | P7-06 AC 9 (shell integration); plan "double shell" mitigation |

| Field | Value |
|---|---|
| **ID** | FE-TC-06 |
| **Title** | Topbar title reflects the current page |
| **Type** | functional |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded user whose `fullName` is known. |
| **Steps** | 1. `goto('/users')` → read the topbar title. 2. `goto('/users/<id>')` for the seeded user → read title. 3. `goto('/users/<childId>/edit')` → read title. |
| **Expected result** | List title = "Users" (`pageTitleUsers`). Detail title = the user's `fullName` (falls back to "User Profile" `pageTitleUserDetail` while loading). Edit title = "Edit Student Profile" (`childEditPageTitle`). |
| **Traces to** | P7-06 AC 9 |

| Field | Value |
|---|---|
| **ID** | FE-TC-07 |
| **Title** | Direct deep-link to `/users/[id]/edit` while unauthenticated redirects to `/login` |
| **Type** | auth-authz / negative |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Fresh context, no tokens. |
| **Steps** | 1. `goto('/users/123/edit')` directly. |
| **Expected result** | Redirected to `/login`; no child PII renders. The edit page self-wraps `<AdminShell>` so the guard runs even on this deep nested route (confirms the layout-convention note in `[id]/layout.tsx`). |
| **Traces to** | P7-08 AC 8 |

---

# SECTION 2 — P7-06 Users list (`/users`)

| Field | Value |
|---|---|
| **ID** | FE-TC-08 |
| **Title** | Results state renders a semantic table with the 5 expected columns + aria-live count |
| **Type** | functional / a11y |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; ≥1 user exists. |
| **Steps** | 1. `goto('/users')`. 2. Wait for the table. 3. Inspect `<th scope="col">` cells and the result-count region. |
| **Expected result** | A `<table>` with a visually-hidden `<caption>` ("User accounts list") and 5 `<th scope="col">` headers: Name, Email, Role, Status, Created. A result-count pill "N accounts" shows in the header; an `aria-live="polite"` SR-only region also announces "N accounts". Each row is a `tr[role="button"]` with `aria-label="<fullName> — View profile"`. [DEVIATION: the design spec calls for a responsive stacked-card layout < 768px and hidden "Created" column at tablet width; the shipped table renders all 5 columns at all widths with no responsive collapse — assert the 5-column table; flag the missing responsive behaviour, do not fail on it.] |
| **Traces to** | P7-06 AC 1, AC 2, AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-09 |
| **Title** | Loading state shows a skeleton table (role="status") |
| **Type** | state (loading) |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Throttle the network or intercept `GET /api/Admin/Users` with a delay so the initial `isLoading` is observable. |
| **Steps** | 1. With the list request delayed, `goto('/users')`. 2. Inspect the results area before the response resolves. |
| **Expected result** | A `div[role="status"]` with `aria-label="Loading users…"` (`usersListLoadingLabel`) containing 6 shimmer skeleton rows plus the real `<thead>`. The filters bar is interactive during loading. |
| **Traces to** | P7-06 AC 2 |

| Field | Value |
|---|---|
| **ID** | FE-TC-10 |
| **Title** | Empty state ("No accounts found") when the search yields zero results |
| **Type** | state (empty) |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. `goto('/users')`. 2. Type a query that matches nothing (e.g. `zzz-no-such-user-<rand>`). 3. Wait for debounce (350ms) + refetch. |
| **Expected result** | Empty-state card: heading "No accounts found" (`usersNoResults`) + hint "Try adjusting the filters or search term." (`usersNoResultsHint`). Because a filter/query is active, a "Clear filters" button is shown inside the empty card. No `role="alert"` error banner (empty list is a success envelope, not an error). |
| **Traces to** | P7-06 AC 2 |

| Field | Value |
|---|---|
| **ID** | FE-TC-11 |
| **Title** | Error state with retry when the list request fails |
| **Type** | state (error) |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Intercept `GET /api/Admin/Users` and force a 500 (or network failure). |
| **Steps** | 1. With the list endpoint stubbed to fail, `goto('/users')`. 2. Inspect the results area. 3. Restore the endpoint, click "Try again". |
| **Expected result** | An `AdminErrorBanner` with `role="alert"` and message "Unable to load users. Please try again." (`usersListError`) + a "Try again" button (`usersListRetry`). Clicking it calls `refetch()`; once the endpoint succeeds, the table renders. |
| **Traces to** | P7-06 AC 2 |

| Field | Value |
|---|---|
| **ID** | FE-TC-12 |
| **Title** | Free-text search is debounced (single request after typing stops) |
| **Type** | functional / boundary |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; ≥1 matchable user. |
| **Steps** | 1. `goto('/users')`. 2. Start counting outbound `GET /api/Admin/Users` requests. 3. Type a multi-char query quickly (e.g. 6 chars within ~200ms). 4. Wait ~500ms. |
| **Expected result** | The query fires **once** ~350ms after the last keystroke (the `useDebounce(query, 350)` hook), not once per character. The request carries `Q=<typed>` (PascalCase). Verify the `Q` param value matches what was typed. |
| **Traces to** | P7-06 AC 1, AC 2 |

| Field | Value |
|---|---|
| **ID** | FE-TC-13 |
| **Title** | Role filter sends `Role=Parent` / `Role=Student` and offers only those two roles |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; ≥1 Parent and ≥1 Student. |
| **Steps** | 1. `goto('/users')`. 2. Open the Role `<select>` (label "Role" `usersListRoleFilterLabel`, sr-only). 3. Inspect options. 4. Select "Parent"; inspect the outbound request. 5. Select "Student"; inspect. |
| **Expected result** | Options are exactly: "All Roles" (value ""), "Parent", "Student" — **no Admin / SuperAdmin** option (D6). Selecting Parent fires `GET /api/Admin/Users` with `Role=Parent`; Student → `Role=Student`. Results reflect the role. |
| **Traces to** | P7-06 AC 1; product override (no teacher role; admins not support targets) |

| Field | Value |
|---|---|
| **ID** | FE-TC-14 |
| **Title** | Status filter sends `Status=0` / `Status=1` (int) and offers Active/Suspended only |
| **Type** | functional / boundary |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; ≥1 Active and ≥1 Suspended user. |
| **Steps** | 1. `goto('/users')`. 2. Open the Status `<select>`. 3. Inspect options. 4. Select "Active"; inspect request. 5. Select "Suspended"; inspect request. |
| **Expected result** | Options: "All Statuses" (""), "Active" (value "0"), "Suspended" (value "1") — **no Deleted** option in the default filter (D6). Active fires `Status=0`; Suspended fires `Status=1` (integer, from `ACCOUNT_STATUS`). The active row shows the Active badge; the suspended row the Suspended badge. |
| **Traces to** | P7-06 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-15 |
| **Title** | Changing a filter resets pagination to page 1 |
| **Type** | functional / boundary |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; enough users (>20) that `totalPages > 1`. |
| **Steps** | 1. `goto('/users')`. 2. Click "Next page" to reach page 2 (assert "Page 2 of N"). 3. Change the Role filter (or type a query). 4. Inspect the page indicator + outbound `PageNumber`. |
| **Expected result** | After the filter/query change the page resets to 1 ("Page 1 of N") and the request carries `PageNumber=1`. (The `prevFiltersRef` effect resets `page` to 1 whenever role/status/debouncedQuery changes.) |
| **Traces to** | P7-06 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-16 |
| **Title** | Server pagination — Next/Prev request the right page; controls disable at bounds |
| **Type** | functional / boundary |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; >20 users so `totalPages ≥ 2`. |
| **Steps** | 1. `goto('/users')`. 2. Assert "Prev" (`aria-label="Previous page"`) is `disabled` on page 1. 3. Click "Next" (`aria-label="Next page"`); assert `PageNumber=2` sent and "Page 2 of N". 4. Navigate to the last page; assert "Next" is `disabled`. |
| **Expected result** | Prev disabled at page 1, Next disabled at last page. Each click sends the correct `PageNumber`. The pagination control only renders when `totalPages > 1`. |
| **Traces to** | P7-06 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-17 |
| **Title** | In-flight refetch does not blank the table (placeholderData) |
| **Type** | functional / state |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; >20 users. Optionally delay the page-2 request. |
| **Steps** | 1. `goto('/users')`, wait for page-1 rows. 2. Click "Next page" (with the request delayed). 3. While the request is in flight, inspect the table body. |
| **Expected result** | Previous rows stay visible during the refetch (no skeleton, no empty state); the table container gets `opacity: 0.6` (`isFetching && !isLoading`) and returns to 1 when the new page resolves. `useSearchUsers` uses `placeholderData: keepPreviousData`. |
| **Traces to** | P7-06 AC 2 |

| Field | Value |
|---|---|
| **ID** | FE-TC-18 |
| **Title** | Clear-filters button appears only when a filter is active and resets everything |
| **Type** | functional |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. `goto('/users')`; assert no "Clear filters" button initially. 2. Type a query AND pick a Role. 3. Assert "Clear filters" appears. 4. Click it. |
| **Expected result** | "Clear filters" (`usersClearFilters`) is hidden when role/status/query are all empty; shown when any is set. Clicking resets role, status, query to empty and page to 1; the unfiltered list reloads. |
| **Traces to** | P7-06 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-19 |
| **Title** | Row click navigates to `/users/[id]`; keyboard Enter/Space also navigates |
| **Type** | functional / a11y |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a known seeded user `id`. |
| **Steps** | 1. `goto('/users')`. 2. Click the row for the seeded user. 3. Go back; focus the row (it is `tabIndex=0`, `role="button"`) and press Enter. |
| **Expected result** | Both click and Enter (and Space) navigate to `/users/<id>` (`router.push`). The row exposes `role="button"` + `aria-label="<fullName> — View profile"` + `tabIndex=0` with an `onKeyDown` handler for Enter/Space. |
| **Traces to** | P7-06 AC 1, AC 3 |

| Field | Value |
|---|---|
| **ID** | FE-TC-20 |
| **Title** | List row shows email and created date as LTR technical strings |
| **Type** | RTL-i18n / functional |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; ≥1 user. |
| **Steps** | 1. `goto('/users')`. 2. Inspect the email and created cells of a row. |
| **Expected result** | Email cell `<span dir="ltr">` (monospace); created cell `<span dir="ltr">` with `font-variant-numeric: tabular-nums`, formatted via `Intl.DateTimeFormat` (e.g. "Jun 18, 2026"). Role + status cells render `StatusBadge` chips. The list DTO carries **no** grade/language/country (lean by design) — assert those are absent from the row. |
| **Traces to** | P7-06 AC 1; privacy (lean list DTO) |

---

# SECTION 3 — P7-06 User detail (`/users/[id]`)

| Field | Value |
|---|---|
| **ID** | FE-TC-21 |
| **Title** | Detail header shows name, email, role badge + status badge |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded Active **Parent** with known name/email. |
| **Steps** | 1. `goto('/users/<parentId>')`. 2. Inspect the header card. |
| **Expected result** | Header shows the avatar initial (or `avatarUrl` image), `fullName`, `email` (`<span dir="ltr">` monospace), a role `StatusBadge` ("Parent") and a status `StatusBadge` ("Active"). |
| **Traces to** | P7-06 AC 3 |

| Field | Value |
|---|---|
| **ID** | FE-TC-22 |
| **Title** | Profile card renders read-only fields incl. "Sign-in Activity: not tracked" |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded user. |
| **Steps** | 1. `goto('/users/<id>')`. 2. Inspect the "Profile" card fields. |
| **Expected result** | Fields: Name, Email, "Member since" (formatted date), "Sign-in Activity" → renders the literal "Sign-in activity: not tracked" (`userDetailSignInNotTracked`, D5), Status (badge), "Status reason" (value or "—"), "Status changed" (date or "—"). No edit/mutation controls in the profile card itself for P7-06 scope. |
| **Traces to** | P7-06 AC 3, AC 6 (sign-in not tracked) |

| Field | Value |
|---|---|
| **ID** | FE-TC-23 |
| **Title** | Child profile shows TWO distinct language rows (preferred vs learning) — never merged |
| **Type** | functional / regression |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Student/child** with `preferredLanguage` (e.g. `ar-EG`) and `learningLanguage` (e.g. `en`), a grade, and a country. |
| **Steps** | 1. `goto('/users/<childId>')`. 2. Locate the "Student Details" sub-section. 3. Inspect both language rows. |
| **Expected result** | A "Student Details" sub-heading (only for Student role). Grade ("Grade N") + Country fields. Then **two separate bordered rows**: Row A label "Display Language (UI & Communication)" (`userDetailPreferredLanguageLabel`, indigo) with the `preferredLanguage` rendered via `Intl.DisplayNames` (e.g. "Arabic (Egypt)") + hint; Row B label "Learning Language (Math & Science)" (`userDetailLearningLanguageLabel`, purple) with `learningLanguage` mapped to "English / الإنجليزية" (`langEnglish`) + hint. The two are visually distinct boxes with different labels — assert both labels are present and the two values are in different containers (P7-06 AC 4 is the headline requirement). |
| **Traces to** | P7-06 AC 4 |

| Field | Value |
|---|---|
| **ID** | FE-TC-24 |
| **Title** | Non-child (Parent) detail hides the Student Details block + both language rows |
| **Type** | functional / negative |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Parent**. |
| **Steps** | 1. `goto('/users/<parentId>')`. 2. Search the page for "Student Details", "Learning Language", grade. |
| **Expected result** | No "Student Details" sub-section, no grade/country/language rows, no "Edit Profile" entry button (that block is gated on `roles.includes('Student')`). |
| **Traces to** | P7-06 AC 4; P7-08 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-25 |
| **Title** | Family panel for a Parent lists linked children (name + grade, NO email) with deep-links |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded Parent with **≥2 children**. |
| **Steps** | 1. `goto('/users/<parentId>')`. 2. Locate the "Linked Children" panel. 3. Inspect each member row. 4. Click a child member. |
| **Expected result** | Heading "Linked Children" (`userFamilyLinkedChildren`). Each child row shows the avatar initial, `fullName`, and secondary line "Grade N" — **no email** (child email is null by design D7; the row must not render a blank email slot). Each row is a `<Link href="/users/<childId>">` (deep-link). Clicking navigates to that child's detail. |
| **Traces to** | P7-06 AC 5; privacy D7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-26 |
| **Title** | Family panel for a child lists linked parent(s) WITH email + deep-link |
| **Type** | functional |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded child whose parent is known. |
| **Steps** | 1. `goto('/users/<childId>')`. 2. Locate the "Linked Parents" panel. 3. Inspect the parent row + click it. |
| **Expected result** | Heading "Linked Parents" (`userFamilyLinkedParents`). Parent row shows name + the parent's email (`<span dir="ltr">` monospace — parents' emails are returned). Row deep-links to `/users/<parentId>`. |
| **Traces to** | P7-06 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-27 |
| **Title** | Family panel empty state (parent with no children / child with no parents) |
| **Type** | state (empty) |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Parent with no children (or stub `GET …/family` to return empty arrays). |
| **Steps** | 1. `goto('/users/<id>')`. 2. Inspect the family panel body. |
| **Expected result** | "No linked children" (`userFamilyNoChildren`) for a parent, or "No linked parents" (`userFamilyNoParents`) for a child. No error banner. |
| **Traces to** | P7-06 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-28 |
| **Title** | Activity panel renders best-effort sections; null sections show "No data" |
| **Type** | functional / state |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a child with **partial** activity (e.g. XP present, league null) — or stub `GET …/activity` so some sections are null. |
| **Steps** | 1. `goto('/users/<childId>')`. 2. Inspect each stat card: XP/Level, Current streak, Badges, Daily missions, League. |
| **Expected result** | Each present section shows its value (numbers `dir="ltr"`, tabular-nums, Latin numerals even in AR). Each **null** section renders the "No data available." chip (`userActivityNoData`) instead — never an error. Missions render as "completed / total". League renders tier + "Rank N of M" + "Weekly XP". |
| **Traces to** | P7-06 AC 6, AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-29 |
| **Title** | Activity panel always shows "Sign-in activity: not tracked" note |
| **Type** | functional |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; any user. |
| **Steps** | 1. `goto('/users/<id>')`. 2. Inspect the top of the Activity panel. |
| **Expected result** | A fixed note "Sign-in activity: not tracked" (`userActivitySignInNote`) renders regardless of data (backend `lastSignInAtUtc` is always null, D5). |
| **Traces to** | P7-06 AC 6 |

| Field | Value |
|---|---|
| **ID** | FE-TC-30 |
| **Title** | Family / Activity panels fail independently — a failing panel must not blank the profile |
| **Type** | state (error) / regression |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Stub `GET /api/Admin/Users/{id}/activity` to 500 while `GET /api/Admin/Users/{id}` and `…/family` succeed. |
| **Steps** | 1. With activity stubbed to fail, `goto('/users/<id>')`. 2. Inspect the profile card, family panel, and activity panel. |
| **Expected result** | Profile card + family panel render normally. The activity panel shows an `AdminErrorBanner variant="warning"` (`role="alert"`, message "No data available.") + a "Try again" button — and **does not** blank the profile (they use separate queries `useAdminUserProfile`/`useUserFamily`/`useUserActivity`). Repeat with `…/family` failing → family panel shows an **error** banner + retry while profile/activity are fine. |
| **Traces to** | P7-06 AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-31 |
| **Title** | Detail loading skeleton (role="status") |
| **Type** | state (loading) |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Delay `GET /api/Admin/Users/{id}`. |
| **Steps** | 1. With the profile request delayed, `goto('/users/<id>')`. 2. Inspect before it resolves. |
| **Expected result** | A `role="status"` block (`aria-label="Loading profile…"`) with a header skeleton (avatar + name/email blocks) and a 6-field profile-card skeleton grid. |
| **Traces to** | P7-06 AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-32 |
| **Title** | Detail not-found state for a non-existent id (404) |
| **Type** | state (error) / negative |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Use an id that returns 404 (a very large/nonexistent id). |
| **Steps** | 1. `goto('/users/99999999')`. 2. Inspect the page. |
| **Expected result** | [DEVIATION — verify which renders] On a failed profile query the shipped page shows the **error branch** first: an `AdminErrorBanner variant="error"` with message "User not found." (`userDetailNotFound`) + a "Back to Users" button (because `isError` is true for a 404; the dedicated `NotFoundState` card only renders when `!isError && !profile`). Assert the user sees a friendly "User not found" message (not a crash/stack) and a route back to `/users`. Flag the error-vs-notfound branch nuance for the reviewer. |
| **Traces to** | P7-06 AC 7; Q4 (400-on-id vs 404 both treated as not-found) |

| Field | Value |
|---|---|
| **ID** | FE-TC-33 |
| **Title** | Status reason + changed-at shown in header only when present |
| **Type** | functional |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a **Suspended** user (has `lastStatusReason` + `statusChangedAtUtc`), and an Active user (both null). |
| **Steps** | 1. `goto('/users/<suspendedId>')`; inspect the header italic line. 2. `goto('/users/<activeId>')`; inspect. |
| **Expected result** | Suspended user: header shows italic "Status reason: <reason> — <date>". Active user: no italic reason line in the header (the profile-card "Status reason"/"Status changed" fields still render "—"). |
| **Traces to** | P7-06 AC 3 |

---

# SECTION 4 — P7-07 Lifecycle actions (Suspend / Reactivate / Delete)

> Actions appear in **two places** on the detail page: the header `LifecycleActionsMenu` and a secondary "Actions" card. Both open the same dialogs. Test via the header buttons; spot-check the secondary card in FE-TC-34.

| Field | Value |
|---|---|
| **ID** | FE-TC-34 |
| **Title** | Status-aware action menu — Active shows {Suspend, Delete} |
| **Type** | functional / auth-authz |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Active** user. |
| **Steps** | 1. `goto('/users/<activeId>')`. 2. Inspect the header actions area and the secondary "Actions" card. |
| **Expected result** | Both render a "Suspend" button (`lifecycleSuspendButton`) and a "Delete Account" button (`lifecycleDeleteButton`). **No** "Reactivate" button (Reactivate only renders when status === Suspended). Buttons have descriptive `aria-label`s including the user's name. |
| **Traces to** | P7-07 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-35 |
| **Title** | Status-aware action menu — Suspended shows {Reactivate, Delete} |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Suspended** user. |
| **Steps** | 1. `goto('/users/<suspendedId>')`. 2. Inspect the actions area. |
| **Expected result** | Renders "Reactivate" (`lifecycleReactivateButton`) + "Delete Account". **No** "Suspend" button. |
| **Traces to** | P7-07 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-36 |
| **Title** | Status-aware action menu — Deleted is terminal (no actions, explanatory notice) |
| **Type** | functional / negative |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Deleted** user (status 2). To reach the detail of a deleted user the id must be navigated directly (deleted users are filtered from the default list). |
| **Steps** | 1. `goto('/users/<deletedId>')`. 2. Inspect the actions area (header + secondary card). |
| **Expected result** | No Suspend/Reactivate/Delete buttons. A terminal notice "Account deleted — no further actions" (`lifecycleDeletedTerminalNotice`) shows in both the header (with a lock icon) and the secondary card. The status badge reads "Deleted". |
| **Traces to** | P7-07 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-37 |
| **Title** | Suspend dialog — required reason gates the confirm button; governance copy present |
| **Type** | validation / functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; an Active user. |
| **Steps** | 1. `goto('/users/<activeId>')`, click "Suspend". 2. Inspect the dialog before typing. 3. Type a reason; re-check the confirm button. |
| **Expected result** | A `role="dialog"` `aria-modal="true"` opens with title "Suspend Account". The governance notice (`lifecycleSuspendNotice`) explicitly distinguishes from the failed-login lockout AND does not over-promise instant logout (says sessions revoked + sign-in blocked). With an empty reason the confirm button "Suspend Account" (`lifecycleSuspendConfirm`) has `aria-disabled="true"` and `opacity 0.4`. After typing a non-empty reason, `aria-disabled` becomes false. The reason field has a `* (required)` marker and a "0 / 500" counter. |
| **Traces to** | P7-07 AC 2, AC 8 |

| Field | Value |
|---|---|
| **ID** | FE-TC-38 |
| **Title** | Suspend success → dialog closes, success banner, status badge refetches to Suspended |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a fresh **Active** user (created for this test so it can be safely suspended). |
| **Steps** | 1. `goto('/users/<activeId>')`, click "Suspend". 2. Type a reason. 3. Click "Suspend Account". 4. Wait for the mutation + invalidation refetch. |
| **Expected result** | `POST /api/Admin/Users/<id>/suspend` body `{ reason }` fires. On success the dialog closes; a success banner (warning/amber variant, `role="alert"`) shows "<name> — account has been suspended." and auto-dismisses ~5s. The status badge updates to "Suspended" **after the profile query refetches** (invalidation, not optimistic). The action menu now shows {Reactivate, Delete}. |
| **Traces to** | P7-07 AC 2, AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-39 |
| **Title** | Suspend error (already suspended, 400) surfaces inline; dialog stays open |
| **Type** | negative / state (error) |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; an **already-Suspended** user. Reach the Suspend dialog by intercepting the profile to report Active (so the Suspend button shows) OR stub `POST …/suspend` to return 400 with a message containing "suspended". |
| **Steps** | 1. Open the Suspend dialog. 2. Type a reason, click confirm. 3. Backend returns 400 ("already suspended"). |
| **Expected result** | The dialog **stays open**. An inline `AdminErrorBanner variant="error"` shows "This account is already suspended." (`lifecycleErrorAlreadySuspended` — mapped from a 400 whose message contains "suspended"). The admin can retry or cancel. (Mapping in `mapSuspendError`: 400+"suspended"→already-suspended, 400+"deleted"→already-deleted, other 400→protected, 422→validation, 404→protected, else network.) |
| **Traces to** | P7-07 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-40 |
| **Title** | Suspend validation error (422) shows on the reason field, not as a separate banner |
| **Type** | validation / negative |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; an Active user. Stub `POST …/suspend` → 422. |
| **Steps** | 1. Open Suspend, type a reason, confirm. 2. Backend returns 422. |
| **Expected result** | The reason field shows the inline `error` text "Reason is required and must be under 500 characters." (`lifecycleErrorValidation`) via the `ReasonField` `error` prop (the dialog routes the 422 message to the field, not to the separate banner — see `errorMessage === lifecycleErrorValidation` branch). Dialog stays open. |
| **Traces to** | P7-07 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-41 |
| **Title** | Reactivate dialog — optional reason; prior suspension history shown |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a **Suspended** user with a known `lastStatusReason` + `statusChangedAtUtc`. |
| **Steps** | 1. `goto('/users/<suspendedId>')`, click "Reactivate". 2. Inspect the dialog. |
| **Expected result** | Dialog title "Reactivate Account". A prior-reason block shows label "Prior suspension reason" (`lifecycleReactivatePriorLabel`), the reason text, and "Suspended on <date>". The reason field is **optional** (no `*`); the confirm button "Reactivate Account" is **enabled immediately** (reason optional — `ReactivateUserDialog` does not gate on reason). |
| **Traces to** | P7-07 AC 3, AC 8 |

| Field | Value |
|---|---|
| **ID** | FE-TC-42 |
| **Title** | Reactivate success → status refetches to Active; menu becomes {Suspend, Delete} |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a fresh user suspended in setup (so it can be reactivated). |
| **Steps** | 1. `goto('/users/<suspendedId>')`, click "Reactivate". 2. Click "Reactivate Account". 3. Wait for refetch. |
| **Expected result** | `POST /api/Admin/Users/<id>/reactivate` body `{ reason: null }` (when blank) fires. Dialog closes; success banner (success/green variant) "<name> — account has been reactivated.". Status badge refetches to "Active"; menu now shows {Suspend, Delete}. |
| **Traces to** | P7-07 AC 3, AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-43 |
| **Title** | Reactivate error (already active / deleted, 400) surfaces inline; dialog stays open |
| **Type** | negative |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Stub `POST …/reactivate` → 400 with message containing "active" (then a second run with "deleted"). |
| **Steps** | 1. Open Reactivate, click confirm. 2. Backend returns 400. |
| **Expected result** | Inline `AdminErrorBanner variant="error"`: "This account is already active." (`lifecycleErrorAlreadyActive`) when message contains "active"; "This account has already been deleted." (`lifecycleErrorAlreadyDeleted`) when it contains "deleted". Dialog stays open. |
| **Traces to** | P7-07 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-44 |
| **Title** | Delete dialog — destructive button gated on BOTH reason AND typed-email match |
| **Type** | validation / functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; an Active **non-parent** user with known email. |
| **Steps** | 1. `goto('/users/<userId>')`, click "Delete Account". 2. Inspect the gate states: (a) empty both, (b) reason only, (c) reason + wrong email, (d) reason + correct email. |
| **Expected result** | Dialog title "Delete Account". Soft-delete notice heading "Account will be permanently disabled" + body that says history retained and **does NOT claim PII is anonymized/erased** (`lifecycleDeleteNoticeBody` — D4). The destructive "Delete Account" confirm is `aria-disabled` in cases (a)(b)(c) and only becomes enabled in (d) when reason is non-empty AND the typed value matches the email **case-insensitively**. The `TypedConfirmField` shows the target email in an LTR monospace box; a "Confirmed" indicator appears only on match. [DEVIATION: on a non-matching typed value the field shows only a red border — no error text — assert the red border / `aria-invalid="true"`, not error copy.] |
| **Traces to** | P7-07 AC 4, AC 8 |

| Field | Value |
|---|---|
| **ID** | FE-TC-45 |
| **Title** | Delete typed-email match is case-insensitive |
| **Type** | boundary / functional |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a user whose email contains letters, e.g. `Jane.Doe@Example.com`. |
| **Steps** | 1. Open Delete. 2. Type a reason. 3. Type the email in a DIFFERENT case (e.g. all lowercase `jane.doe@example.com`). |
| **Expected result** | The confirm button arms (match succeeds): `typedEmail.trim().toLowerCase() === userEmail.toLowerCase()`. "Confirmed" indicator shows. |
| **Traces to** | P7-07 AC 4 |

| Field | Value |
|---|---|
| **ID** | FE-TC-46 |
| **Title** | Delete cascade checkbox appears ONLY for parents, default unchecked |
| **Type** | functional / boundary |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Parent** with children AND a non-parent (Student or childless parent) for contrast. |
| **Steps** | 1. Open Delete on the Parent → inspect for the cascade checkbox. 2. Open Delete on the non-parent → inspect. |
| **Expected result** | Parent: a checkbox "Also delete all linked children" (`lifecycleDeleteCascadeLabel`) + warning "Their accounts will also be disabled… History retained." (`lifecycleDeleteCascadeWarning`), **unchecked by default** (D10). Non-parent: **no** cascade checkbox at all. The dialog never offers a suspend-cascade (only Delete has cascade — Gap 9). |
| **Traces to** | P7-07 AC 4 |

| Field | Value |
|---|---|
| **ID** | FE-TC-47 |
| **Title** | Delete success sends `confirm:true` only at final step → status refetches to Deleted (terminal) |
| **Type** | functional / persistence / auth-authz |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a **fresh disposable Parent** with ≥1 child (created in setup so deletion is safe). |
| **Steps** | 1. Open Delete on the parent. 2. Inspect the request payload at each interaction (should be none until confirm). 3. Type reason + matching email, optionally check cascade, click "Delete Account". 4. Wait for refetch. |
| **Expected result** | `DELETE /api/Admin/Users/<id>` fires **only** on the final click, with body `{ reason, confirm: true, cascadeChildren: <checkbox> }`. `confirm:true` is never sent before the final step (no pre-fill/auto-confirm). On success the dialog closes; a red `error`-variant banner (permanent, no auto-dismiss) "<name> — account has been deleted." shows. Status badge refetches to "Deleted"; actions become the terminal notice. If cascade was checked, the child(ren) also become Deleted (verify by navigating to a child's detail). |
| **Traces to** | P7-07 AC 4, AC 7; security (confirm only at final step) |

| Field | Value |
|---|---|
| **ID** | FE-TC-48 |
| **Title** | Delete error (already deleted, 400) surfaces inline; dialog stays open |
| **Type** | negative |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Stub `DELETE …/{id}` → 400 with message containing "deleted". |
| **Steps** | 1. Open Delete, satisfy both gates, confirm. 2. Backend 400. |
| **Expected result** | Inline `AdminErrorBanner variant="error"` "This account has already been deleted." (`lifecycleErrorAlreadyDeleted`). Dialog stays open. (Other 400 → "This account cannot be modified." protected; 422 → field validation; 424 → "Please confirm by typing the email address." `lifecycleErrorConfirmMissing`; 404 → protected; else network — per `mapDeleteError`.) |
| **Traces to** | P7-07 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-49 |
| **Title** | Delete defensive 424 (confirm flag rejected) maps to the confirm-missing message |
| **Type** | negative / boundary |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Stub `DELETE …/{id}` → 424. |
| **Steps** | 1. Open Delete, satisfy both gates, confirm. 2. Backend 424. |
| **Expected result** | Inline error "Please confirm by typing the email address." (`lifecycleErrorConfirmMissing`). Dialog stays open. (This path should not occur in normal flow since the UI always sends `confirm:true`, but the mapping must degrade gracefully.) |
| **Traces to** | P7-07 AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-50 |
| **Title** | Self / SuperAdmin protection (400) maps to "cannot be modified" |
| **Type** | negative / auth-authz |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. Either navigate to the admin's own profile (if reachable) or stub a 400 whose message contains neither "suspended"/"active"/"deleted". |
| **Steps** | 1. Trigger Suspend/Delete on a protected target. 2. Backend returns a generic 400. |
| **Expected result** | Inline error "This account cannot be modified." (`lifecycleErrorProtected`). Dialog stays open. The UI does not leak that the action "would have worked" (security note). |
| **Traces to** | P7-07 AC 5; product (self/SuperAdmin protection) |

---

# SECTION 5 — P7-08 Child edit (`/users/[id]/edit`)

| Field | Value |
|---|---|
| **ID** | FE-TC-51 |
| **Title** | Child-only gate — non-student redirected/blocked from the edit page |
| **Type** | auth-authz / negative |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded **Parent** id. |
| **Steps** | 1. `goto('/users/<parentId>/edit')`. 2. Inspect the page. |
| **Expected result** | The edit form does NOT render. Instead an `AdminErrorBanner variant="warning"` "Profile editing is only available for student accounts." (`childEditNotStudent`) + a "Back to Users" button (which navigates to `/users/<id>`). [DEVIATION: the design spec says auto-redirect; the shipped page shows the warning + manual back button rather than auto-redirecting — assert the warning is shown and the form is absent.] |
| **Traces to** | P7-08 AC 1 |

| Field | Value |
|---|---|
| **ID** | FE-TC-52 |
| **Title** | Edit page renders for a Student with three distinct sections |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded Student. |
| **Steps** | 1. `goto('/users/<childId>/edit')`. 2. Inspect the form card. |
| **Expected result** | Breadcrumb (Users › <name> › Edit Profile). Section A: Country text input (`childEditCountryLabel`) + Display Language select (`childEditDisplayLanguageLabel`, options ar/en). Section B (separated): Learning Language label (`childEditLearningLanguageLabel`) + current-value purple pill + a red destructive note (`childEditLearningLanguageWarning`) + a danger-tinted "Change Learning Language" button. Section C (separated): Grade label + current-grade pill + "Override Grade" button. Footer: Cancel link + "Save Changes" button (disabled initially). |
| **Traces to** | P7-08 AC 1, AC 2, AC 3 |

| Field | Value |
|---|---|
| **ID** | FE-TC-53 |
| **Title** | Save is disabled until a harmless field changes; sends only changed fields |
| **Type** | functional / boundary |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student with known `nationality` + `preferredLanguage`. |
| **Steps** | 1. `goto('/users/<childId>/edit')`; assert "Save Changes" is `disabled`. 2. Change ONLY the country field. 3. Click "Save Changes"; inspect the PATCH body. |
| **Expected result** | "Save Changes" is disabled when nothing differs from the loaded baseline. After changing only country, it enables. The request is `PATCH /api/Admin/Users/<childId>/profile` with body containing **only** `{ country }` (not `preferredLanguage`). If only the Display Language is changed, the body contains only `{ preferredLanguage }`. The Learning Language and Grade buttons never participate in this PATCH. |
| **Traces to** | P7-08 AC 2 |

| Field | Value |
|---|---|
| **ID** | FE-TC-54 |
| **Title** | Profile PATCH success → detail refetches the change |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student. |
| **Steps** | 1. `goto('/users/<childId>/edit')`. 2. Change country to a new value. 3. Save. 4. Navigate to `/users/<childId>` and inspect the Country field. |
| **Expected result** | A success banner shows after the PATCH. [DEVIATION: the success copy reuses `gradeDialogSuccess` ("grade has been updated.") for a profile save — this is a copy bug; assert a success banner appears and flag the wrong string for the reviewer.] The detail page Country field reflects the new value (invalidation refetch; the PATCH returns only a message, so the UI must refetch — no optimistic update). |
| **Traces to** | P7-08 AC 2, AC 9 |

| Field | Value |
|---|---|
| **ID** | FE-TC-55 |
| **Title** | Profile PATCH validation error (422) surfaces inline (does not crash) |
| **Type** | negative / validation |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student. Stub `PATCH …/profile` → 422. |
| **Steps** | 1. Change a field, Save. 2. Backend 422. |
| **Expected result** | An `error`-variant banner appears above the form. [DEVIATION: `mapProfilePatchError` maps a 422 to `gradeError422` = "Grade must be between 1 and 6." which is the WRONG message for an unsupported language/country — assert an error banner shows and flag the mis-mapped copy. A correct mapping would surface an "unsupported language/country" message.] The page does not crash. |
| **Traces to** | P7-08 AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-56 |
| **Title** | Learning Language is a separate, clearly-labelled control — opens the fresh-start dialog, not an inline save |
| **Type** | functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student. |
| **Steps** | 1. `goto('/users/<childId>/edit')`. 2. Click "Change Learning Language". 3. Confirm no PATCH fires from this action. |
| **Expected result** | A `role="dialog"` (the destructive ChangeLearningLanguageDialog) opens; **no** `PATCH …/profile` request is sent by clicking it. The control is visually distinct from the Display Language select (separate section, purple pill, red warning). |
| **Traces to** | P7-08 AC 3, AC 4 |

| Field | Value |
|---|---|
| **ID** | FE-TC-57 |
| **Title** | Grade override dialog — grade 1–6, required reason (FE rule), confirm gate |
| **Type** | validation / functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student with a known current grade (e.g. 3). |
| **Steps** | 1. `goto('/users/<childId>/edit')`, click "Override Grade". 2. Inspect the grade `<select>` options. 3. Try to confirm with: (a) no selection, (b) a different grade but empty reason, (c) different grade + reason. |
| **Expected result** | Dialog "Override Grade". The select offers exactly grades 1–6 ("Grade 1".."Grade 6"). Current-grade display shows "Grade 3". A green preserve notice (`gradeDialogPreserveNotice`) appears once a different grade is chosen, stating XP/level/badges/streaks/mastery are preserved (non-destructive). The reason field is **required** (FE rule D3, `*` marker). The confirm "Override Grade" is `aria-disabled` in (a) and (b); enabled in (c) (`selectedGrade>0 && !==currentGrade && reason non-empty`). |
| **Traces to** | P7-08 AC 6, AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-58 |
| **Title** | Grade override — selecting the SAME grade shows an inline same-grade warning and keeps confirm disabled |
| **Type** | boundary / negative |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student with current grade 3. |
| **Steps** | 1. Open the grade dialog. 2. Select grade 3 (the current grade). 3. Inspect. |
| **Expected result** | An inline `AdminErrorBanner variant="warning"` "This is already the child's current grade." (`gradeError400SameGrade`) renders, and the confirm button stays `aria-disabled` (the gate requires `selectedGrade !== currentGrade`). No request is sent (the same-grade 400 is prevented client-side). |
| **Traces to** | P7-08 AC 7 (same-grade → 400 handled) |

| Field | Value |
|---|---|
| **ID** | FE-TC-59 |
| **Title** | Grade override success → detail refetches the new grade |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student with grade 3. |
| **Steps** | 1. Open the grade dialog, pick grade 5, type a reason, confirm. 2. Wait for the mutation + invalidation. 3. Check the edit page heading pill / navigate to `/users/<childId>` and check grade. |
| **Expected result** | `POST /api/Admin/Users/<childId>/grade` body `{ grade:5, reason, confirm:true }`. Dialog closes; success banner ("grade has been updated. (Grade 5)"). The detail/profile refetches to show "Grade 5" (non-optimistic). |
| **Traces to** | P7-08 AC 6, AC 9 |

| Field | Value |
|---|---|
| **ID** | FE-TC-60 |
| **Title** | Grade override range error (422) surfaces inline |
| **Type** | negative / boundary |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student. Stub `POST …/grade` → 422. (The UI select only allows 1–6, so a real 422 requires a stub.) |
| **Steps** | 1. Open grade dialog, pick a grade, type reason, confirm. 2. Backend 422. |
| **Expected result** | The reason field shows inline error "Grade must be between 1 and 6." (`gradeError422`) — routed to the field per the dialog's `error` prop branch. Dialog stays open. |
| **Traces to** | P7-08 AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-61 |
| **Title** | Change-learning-language dialog — destructive copy + typed `CONFIRM` (case-sensitive) gate |
| **Type** | validation / functional |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student with `learningLanguage = 'ar'`. |
| **Steps** | 1. `goto('/users/<childId>/edit')`, click "Change Learning Language". 2. Inspect the warning + kept-list. 3. Pick the new language. 4. Try confirm with: (a) nothing typed, (b) lowercase "confirm", (c) exact "CONFIRM". |
| **Expected result** | Dialog "Change Learning Language" (destructive variant, alert-triangle). A red loss block: title (`langDialogLossTitle`) + loss line stating **Math & Science attempts/mastery/progress are deleted and cannot be recovered** (`langDialogLossLine`) + a green kept line stating Arabic/English/XP/streak/badges are NOT affected (`langDialogKeptLine`). From→To display (current struck-through). The language `<select>` excludes the current language. The typed-confirm box shows the target token "CONFIRM" (LTR). Confirm "Reset & Change Language" is `aria-disabled` in (a) and (b) — **lowercase "confirm" does NOT satisfy** (case-sensitive `typedValue.trim() === 'CONFIRM'`); enabled in (c). |
| **Traces to** | P7-08 AC 4, AC 5 |

| Field | Value |
|---|---|
| **ID** | FE-TC-62 |
| **Title** | Learning-language change sends `confirmFreshStart:true` only at final step → success refetch |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a **fresh disposable** Student with `learningLanguage='ar'` (so its Math/Science reset is acceptable). |
| **Steps** | 1. Open the dialog, pick "English", type "CONFIRM", confirm. 2. Inspect the request. 3. Wait for invalidation; navigate to `/users/<childId>` and inspect the Learning Language row. |
| **Expected result** | `POST /api/Admin/Users/<childId>/learning-language` body `{ learningLanguage:'en', confirmFreshStart:true }` — sent **only** on the final confirm. Dialog closes; success banner "learning language has been changed. Math and Science progress has been reset. (English…)". The detail Learning Language row refetches to English (invalidation; non-optimistic, no pre-emptive cache wipe). |
| **Traces to** | P7-08 AC 4, AC 9; security (no optimistic wipe) |

| Field | Value |
|---|---|
| **ID** | FE-TC-63 |
| **Title** | Learning-language defensive 424 (unconfirmed) maps to the gate message |
| **Type** | negative / boundary |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student. Stub `POST …/learning-language` → 424. |
| **Steps** | 1. Open the dialog, pick a language, type "CONFIRM", confirm. 2. Backend 424. |
| **Expected result** | Inline `AdminErrorBanner variant="error"` "Fresh start was not confirmed. No changes were made." (`langError424`). Dialog stays open. No optimistic UI change. (This path should not occur normally since the UI always sends `confirmFreshStart:true`.) |
| **Traces to** | P7-08 AC 5, AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-64 |
| **Title** | Learning-language unsupported value (422) surfaces inline |
| **Type** | negative |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a Student. Stub `POST …/learning-language` → 422. |
| **Steps** | 1. Open the dialog, pick a language, type "CONFIRM", confirm. 2. Backend 422. |
| **Expected result** | Inline error "Language must be \"ar\" or \"en\"." (`langError422`). Dialog stays open. |
| **Traces to** | P7-08 AC 7 |

| Field | Value |
|---|---|
| **ID** | FE-TC-65 |
| **Title** | Cancel / ESC closes any dialog and clears its form; no mutation fires |
| **Type** | functional / a11y |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; relevant fixtures for each dialog. |
| **Steps** | For each of Suspend, Reactivate, Delete, Grade, ChangeLearningLanguage: 1. Open it, type into fields. 2. Press ESC. 3. Re-open the same dialog. 4. Separately, open + click "Cancel". |
| **Expected result** | ESC and Cancel both close the dialog and **never** send the mutation. On re-open the form is reset (reason cleared, typed-confirm cleared, grade/language selection cleared, cascade unchecked) — confirmed by the `handleClose` resetters in each dialog. |
| **Traces to** | P7-07 AC 8; P7-08 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-66 |
| **Title** | Backdrop click does NOT dismiss any dialog |
| **Type** | a11y / negative |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. Open the Delete dialog (most destructive). 2. Click the overlay area outside the dialog card. |
| **Expected result** | The dialog **stays open** (the overlay has no `onClick` close handler — `AdminConfirmDialog` deliberately omits backdrop-dismiss to prevent accidental destructive dismiss). Repeat for ChangeLearningLanguage. |
| **Traces to** | P7-07 AC 8; P7-08 AC 10; security (destructive-action friction) |

| Field | Value |
|---|---|
| **ID** | FE-TC-67 |
| **Title** | Dialog focus trap — Tab cycles within the dialog; focus returns to trigger on close |
| **Type** | a11y |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; an Active user. |
| **Steps** | 1. Focus the "Suspend" button, open the dialog. 2. Tab through to the last focusable element, Tab once more. 3. Shift+Tab from the first element. 4. Close with ESC. |
| **Expected result** | On open, focus moves into the dialog (first focusable element, ~50ms defer). Tab from the last element wraps to the first; Shift+Tab from the first wraps to the last (`AdminConfirmDialog` focus trap). On close, focus returns to the element that opened the dialog (`triggerRef`). |
| **Traces to** | P7-07 AC 8; P7-08 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-68 |
| **Title** | Dialog has role="dialog" + aria-modal + aria-labelledby pointing at the title |
| **Type** | a11y |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. Open any dialog. 2. Inspect the dialog card element. |
| **Expected result** | The card is `role="dialog" aria-modal="true" aria-labelledby="<id>"` and the referenced element is the title text (e.g. "Suspend Account"). The cancel button is always enabled; the gated confirm uses `aria-disabled` (not `disabled`) so it stays focusable. |
| **Traces to** | P7-07 AC 8; P7-08 AC 10 |

---

# SECTION 6 — Cross-cutting (RTL/i18n, a11y, no-PII, no-optimistic)

> **RTL cases require a separate `ADMIN_LOCALE='ar'` build/run** (no runtime toggle). If unreachable, mark BLOCKED with that reason (see locale note at top + open question Q-A in coverage-report).

| Field | Value |
|---|---|
| **ID** | FE-TC-69 |
| **Title** | English default — no raw string keys leak anywhere on the three surfaces |
| **Type** | RTL-i18n / regression |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; seeded Parent + Student. |
| **Steps** | 1. Visit `/users`, `/users/<parentId>`, `/users/<childId>`, `/users/<childId>/edit`. 2. Open each dialog. 3. Scan visible text for raw keys (e.g. `usersListHeading`, `lifecycle.*`, `childEdit*`, untranslated camelCase tokens). |
| **Expected result** | All visible copy is human English (e.g. "User Management", "Suspend Account", "Override Grade"). No raw `AdminStrings` keys, no `undefined`, no "[object Object]". |
| **Traces to** | P7-06 AC 10; P7-07 AC 8; P7-08 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-70 |
| **Title** | RTL/ar build — `dir="rtl"` + `lang="ar"`; Arabic copy renders, no raw keys |
| **Type** | RTL-i18n |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | App built/run with `ADMIN_LOCALE='ar'`. Signed in as ADMIN; seeded users. |
| **Steps** | 1. Visit `/users` and a child detail. 2. Inspect the root `dir`/`lang` and key copy. |
| **Expected result** | Side nav on the right; logical-edge layout flips (active nav indicator on the right via `borderInlineStart`). Copy is Arabic ("إدارة المستخدمين", "المستخدمون", status "نشط"/"موقوف"/"محذوف"). No raw keys, no English fallback strings on translated elements. |
| **Traces to** | P7-06 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-71 |
| **Title** | RTL — technical strings (email, dates, "CONFIRM", XP numbers) stay LTR |
| **Type** | RTL-i18n |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | `ADMIN_LOCALE='ar'` build. Signed in as ADMIN; a child with activity. |
| **Steps** | 1. In AR, inspect: a list/detail email cell, a created date, the typed-confirm target box in Delete + ChangeLanguage, XP/streak numbers in the activity panel. |
| **Expected result** | Email cells `dir="ltr"` monospace; date spans `dir="ltr"`; the typed-confirm target + input `dir="ltr"`; activity numbers in `<span dir="ltr">` with tabular-nums and **Latin** numerals (not Eastern-Arabic). Surrounding labels are Arabic/RTL. |
| **Traces to** | P7-06 AC 10; P7-08 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-72 |
| **Title** | RTL — dialogs render correctly (action row reversed, arrows mirrored) |
| **Type** | RTL-i18n / a11y |
| **Priority** | P2 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | `ADMIN_LOCALE='ar'` build. |
| **Steps** | 1. Open the Delete and ChangeLearningLanguage dialogs in AR. 2. Inspect the action row order and the from→to arrow. |
| **Expected result** | Dialog copy is Arabic; the action row uses `lx-dialog-actions` (row-reverse intended in RTL); the ChangeLanguage from→to uses the left-arrow icon in AR (`ArrowLeftIcon`); breadcrumb separator on the edit page uses the left chevron in AR. Cancel label renders "إلغاء". |
| **Traces to** | P7-07 AC 8; P7-08 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-73 |
| **Title** | a11y — table semantics, aria-live count, active nav, skeleton role |
| **Type** | a11y |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; ≥1 user. |
| **Steps** | 1. On `/users`, assert `<table>` + `<caption>` + `<th scope="col">`. 2. Assert the results area is wrapped in `aria-live="polite"` and a SR-only count region announces "N accounts". 3. Assert the active nav item has `aria-current="page"`. 4. Trigger loading and assert `role="status"`. |
| **Expected result** | All structural a11y hooks present as specified (Design Spec Part H). Run an axe/automated a11y scan on `/users` and a detail page; report any serious/critical violations as defects. |
| **Traces to** | P7-06 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-74 |
| **Title** | a11y — error/success banners use role="alert"; gated confirm uses aria-disabled |
| **Type** | a11y |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN. |
| **Steps** | 1. Trigger a list error (FE-TC-11) and a dialog error (FE-TC-39). 2. Open a gated dialog (Delete) and inspect the confirm button before the gate is met. |
| **Expected result** | All `AdminErrorBanner` instances expose `role="alert"`. The gated confirm button is `aria-disabled="true"` (focusable) rather than HTML-`disabled` until the gate is met (so SRs can read it). The `ReasonField` over-limit/error text uses `role="alert"` and the textarea gets `aria-invalid`. |
| **Traces to** | P7-06 AC 10; P7-07 AC 8; P7-08 AC 10 |

| Field | Value |
|---|---|
| **ID** | FE-TC-75 |
| **Title** | No PII in console logs, toasts, or URLs |
| **Type** | a11y / security (PII) |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; a seeded child with email/name. |
| **Steps** | 1. Attach a console listener. 2. Walk the full flow: list → child detail → edit → open + cancel each dialog → perform a grade override. 3. After each step inspect captured console output and the URL bar. |
| **Expected result** | No child/user email, full name, or token material printed to the console. The search term in the `?` query string is acceptable (it is the admin's own input), but the URL must not carry child PII beyond the `id` path param. No child PII echoed into success/error toasts beyond the name already shown in the authorized UI. |
| **Traces to** | Security/privacy (child PII); P7-08 security section |

| Field | Value |
|---|---|
| **ID** | FE-TC-76 |
| **Title** | No optimistic mutation — status/grade/language never change before the server confirms |
| **Type** | regression / state |
| **Priority** | P0 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; disposable fixtures. Delay the mutation responses. |
| **Steps** | 1. With `POST …/suspend` delayed, confirm a suspend and watch the badge during the in-flight window. 2. With `POST …/grade` delayed, confirm a grade override and watch the grade. 3. With `POST …/learning-language` delayed (or stubbed to fail), confirm and watch the learning-language row + that no cache is pre-wiped. |
| **Expected result** | The badge/grade/language do **not** change while the request is in flight; they change only **after** the success + invalidation refetch settles. On a failed mutation (stub a 500), the UI keeps the old value (no rollback needed because there was no optimistic change). |
| **Traces to** | P7-07 AC 7; P7-08 AC 9; security (no optimistic cache-wipe) |

| Field | Value |
|---|---|
| **ID** | FE-TC-77 |
| **Title** | Sign-out clears auth so the surface is no longer reachable (no PII persistence) |
| **Type** | auth-authz / security |
| **Priority** | P1 |
| **Target agent** | frontend-e2e-tester |
| **Preconditions / seed** | Signed in as ADMIN; viewed a child detail (so the TanStack cache holds child PII). |
| **Steps** | 1. View `/users/<childId>`. 2. Sign out (topbar sign-out). 3. Attempt `goto('/users/<childId>')` again. 4. Inspect sessionStorage + the page. |
| **Expected result** | After sign-out, sessionStorage tokens are cleared and `/users/<childId>` redirects to `/login` with no cached PII rendered. (TanStack cache + sessionStorage only — nothing persisted to localStorage that outlives the session.) |
| **Traces to** | Security/privacy (no persistence beyond session) |

---

## Notes for the implementer (`frontend-e2e-tester`)
- **Stubbing:** several negative cases (422/424/500, partial activity) need request interception (`page.route('**/api/Admin/Users/**', …)`). Prefer real seeded data for happy paths; reserve stubs for error/edge paths that are hard to produce server-side.
- **Disposable fixtures:** every destructive happy-path (suspend/reactivate/delete/grade/learning-language) must run against a **freshly created** user/child so the run is repeatable and does not corrupt shared fixtures.
- **Refetch timing:** assert post-mutation state with `expect(...).toHaveText/toBeVisible` and Playwright auto-wait — the change arrives after the invalidation refetch, not synchronously.
- **Selectors:** until the testIDs in `coverage-report.md` land, use `getByRole('dialog')`, `getByRole('alert')`, `getByRole('button', { name })`, `getByLabel(...)`, `getByRole('link', { name: 'Users' })`, `<th>` text. Avoid CSS-class selectors.
- Record every result (pass/fail/blocked + defect) in `execution-report.md`.
