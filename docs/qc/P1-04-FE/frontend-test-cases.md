# P1-04-FE — Frontend E2E Test Cases

> Target agent: **`frontend-e2e-tester`** → implement as `tests/e2e/specs/P1-04-FE.spec.ts`.
> Surface: student-app **web PWA** (Playwright, `baseURL` http://localhost:8081; backend prerequisite at `:5080`).
> Selector convention: **`getByTestId` first**, then `getByRole` / `getByLabel` (aria-label). **Never** select on Arabic/English copy — Arabic is the default locale. Where a `testID` is missing (README Q1–Q5), drive via role/aria-label and **report the needed hook back to `frontend`**.
> BLOCKED cases require a fixture/credential that may not exist yet (README Q6/Q7) — implement the scaffold, mark `test.fixme`/skip with the blocker reason in the spec, and record in `execution-report.md`.

Legend — **Target agent** is `frontend-e2e-tester` for every case below.

---

## Group A — Role-driven routing off `/Me` (child home vs parent home)

### FE-TC-01 — Parent with linked children lands on the parent home
- **Type:** functional / state (routing)
- **Priority:** P0
- **Preconditions / seed:** A parent account with **≥1 linked child** (seed via API: register parent → add child). `hasChildren === true` on `/Me`.
- **Steps:**
  1. Go to `/login`. Sign in with the parent credentials (persona = Parent).
  2. After submit, wait for the routing guard to resolve (splash holds while `/Me` loads).
- **Expected:** URL settles on the **parent** group (`/(parent)`), not `/(child)` and not `/(onboarding)`. The parent home marker is visible (README Q2 — parent-home root `testID`, else the parent placeholder header `parent.dashboard.title` via `accessibilityRole="header"`). The child-home marker `testID="dashboard-header"` is **absent**.
- **Traces to:** AC1, role-routing.

### FE-TC-02 — My-Children loading state renders before data resolves
- **Type:** state (loading)
- **Priority:** P1
- **Preconditions / seed:** Parent with ≥1 linked child; throttle/delay the `My-Children` response (Playwright route delay) so the loading frame is observable.
- **Steps:**
  1. Sign in as the parent; navigate to `/(parent)/children`.
  2. Observe the surface before the `useMyChildren` query settles.
- **Expected:** Skeleton cards render (web `CardSkeleton` ×3 / mobile `SkeletonRow` ×3 with `accessibilityLabel` = `common.loading`). No raw i18n keys; no child cards yet. After resolve, skeletons are replaced by real cards.
- **Traces to:** loading-state NFR; AC1 (list reflects linked children).

### FE-TC-03 — Parent linked to multiple children sees all of them in My-Children
- **Type:** functional
- **Priority:** P1
- **Preconditions / seed:** Parent with **≥2 linked children** (distinct names).
- **Steps:**
  1. Sign in as the parent; navigate to `/(parent)/children` (web) / open My-Children (mobile).
  2. Read the child-card grid/list.
- **Expected:** All N linked children appear (one card each). The header subtitle/section label reflects the count (`parent.myChildren.subtitle`/`sectionLabel` with `{count}` = N) — assert the count value, not the copy. The trailing **Add-Child** affordance is present in addition to the N cards.
- **Traces to:** AC4 (parent linked to multiple children).

### FE-TC-04 — Child sign-in lands on the child home (BLOCKED)
- **Type:** functional / state (routing)
- **Priority:** P0
- **Preconditions / seed:** A **child account** with sign-in credentials (depends README Q6/Q7). `/Me.roles` includes Student.
- **Steps:**
  1. Go to `/login`; select persona = Student; sign in with the child credentials.
  2. Wait for the routing guard.
- **Expected:** URL settles on the **child** group (`/(child)`); child-home marker `testID="dashboard-header"` is visible. The parent home / My-Children surface is **absent**.
- **BLOCKED:** requires a seeded child account that can authenticate (README Q6/Q7). Scaffold the test; `fixme` until the seed/credential path is confirmed.
- **Traces to:** role-routing (child → child home).

### FE-TC-05 — Role decides the landing, regardless of which home was requested (BLOCKED)
- **Type:** auth-authz / negative (routing)
- **Priority:** P0
- **Preconditions / seed:** Both a parent (with children) and a child account.
- **Steps:**
  1. Sign in as the **child**, then attempt to navigate directly to `/(parent)/children`.
  2. Separately: sign in as the **parent**, then attempt to navigate directly to `/(child)`.
- **Expected:** The guard redirects the **child** back to `/(child)` (never shows parent My-Children); redirects the **parent** back to their parent landing (never shows the child dashboard). No cross-role surface flashes and stays.
- **BLOCKED:** child half requires a child account (README Q6/Q7). Implement the parent half now; `fixme` the child half.
- **Traces to:** role-routing, AC3 (data isolation by role).

---

## Group B — Family scope (parent sees only their own children)

### FE-TC-06 — Parent A sees only family A's children, never family B's (BLOCKED)
- **Type:** auth-authz (scope / IDOR-observable)
- **Priority:** P0
- **Preconditions / seed:** **Two unrelated families**: parent A linked to children {A1, A2}; parent B linked to {B1}. (Client never sends a parent id — scope is JWT-derived.)
- **Steps:**
  1. Sign in as parent A; open My-Children.
  2. Enumerate the rendered child cards.
- **Expected:** Only {A1, A2} appear. {B1} is **never** rendered. Count matches family A's link count.
- **BLOCKED:** requires a two-family seed fixture (README Q6).
- **Traces to:** AC3 (parent accesses only own children).

### FE-TC-07 — Switching parent session re-scopes the list (no stale cross-family data) (BLOCKED)
- **Type:** auth-authz / persistence (cache scope)
- **Priority:** P1
- **Preconditions / seed:** Two families as in FE-TC-06.
- **Steps:**
  1. Sign in as parent A; open My-Children (cache populated with {A1, A2}).
  2. Sign out; sign in as parent B; open My-Children.
- **Expected:** Parent B sees only {B1} — no residual {A1, A2} from parent A's cached `useMyChildren` result. (Guards against a query-cache leak across sessions.)
- **BLOCKED:** two-family seed (README Q6).
- **Traces to:** AC3.

### FE-TC-08 — A child linked by more than one parent appears for each linking parent (BLOCKED)
- **Type:** functional
- **Priority:** P2
- **Preconditions / seed:** Child C linked to **both** parent A and parent B.
- **Steps:**
  1. Sign in as parent A → confirm C appears.
  2. Sign in as parent B → confirm C appears.
- **Expected:** C is listed for both A and B (linkage is many-to-many).
- **BLOCKED:** dual-link seed (README Q6).
- **Traces to:** AC4 (a child can be linked by more than one parent).

---

## Group C — My-Children states (empty / error)

### FE-TC-09 — Empty state when the parent has no linked children
- **Type:** state (empty)
- **Priority:** P1
- **Preconditions / seed:** A parent whose `/Me.hasChildren` is true but the My-Children list is empty **OR** drive the empty-list branch directly. (Note: a true zero-child parent is routed to onboarding by `useAuthRoute`; reach the empty branch via `/(parent)/children` mobile `MyChildren` or by mocking an empty list.)
- **Steps:**
  1. Reach the My-Children surface with an empty `useMyChildren` result.
- **Expected:** Empty-state renders — mascot image (hidden from a11y) + the empty message (`parent.myChildren.empty`) + a link/CTA to link a child (`parent.myChildren.linkButton`). No skeletons, no error banner, no orphan cards. Assert via role/aria-label, not copy.
- **Traces to:** empty-state NFR.

### FE-TC-15 — My-Children error state shows retry and recovers
- **Type:** state (error) / negative
- **Priority:** P1
- **Preconditions / seed:** Parent session; force the `My-Children` request to fail (Playwright route → 500/network error) on first attempt, succeed on retry.
- **Steps:**
  1. Open My-Children; the request fails.
  2. Activate the **Retry** control (`accessibilityLabel` = `common.retry`).
- **Expected:** First: localized error text (`parent.myChildren.loadError`) + a Retry affordance; no raw key, no crash, no infinite skeleton. After Retry succeeds, the child cards render and the error clears.
- **Traces to:** error-state NFR.

---

## Group D — Link an existing child (`LinkChildForm`)

### FE-TC-10 — Open the link-existing-child form
- **Type:** functional
- **Priority:** P1
- **Preconditions / seed:** Parent session.
- **Steps:**
  1. From My-Children, activate the link-child CTA (`parent.myChildren.linkButton`) → navigates to `/(parent)/link-child`.
- **Expected:** The Link-Child screen mounts with the explanation text, an email field (README Q4 — `link-child-email`, else the labelled textbox), and a submit button (`parent.linkChild.submitButton`).
- **Traces to:** AC2.

### FE-TC-11 — Linking an existing child by email succeeds and refreshes the list
- **Type:** functional / persistence
- **Priority:** P0
- **Preconditions / seed:** Parent A; a **provisioned but unlinked** child account whose email is known (seed via API).
- **Steps:**
  1. Open `/(parent)/link-child`; enter the unlinked child's email; submit.
  2. After the success card, return to My-Children.
- **Expected:** Success card renders (`ChildCard variant="linking"` + `parent.linkChild.successTitle`). The form invalidates `['family','my-children']` (= `queryKeys.family.myChildren()`), so on returning to My-Children the newly linked child now appears in the list (count +1).
- **Traces to:** AC2 (link additional existing child).

### FE-TC-12 — Link-child email field validation (client-side zod)
- **Type:** validation
- **Priority:** P1
- **Preconditions / seed:** Parent session on `/(parent)/link-child`.
- **Steps:**
  1. Submit with an empty email; then with a malformed email ("abc", "abc@").
- **Expected:** Field-level validation error from `linkChildSchema` (localized message, not a raw key); the mutation does **not** fire (no network call) until the email is valid. Submit stays usable.
- **Traces to:** AC2 (form validation).

### FE-TC-13 — Linking a non-existent child shows a clear not-found error
- **Type:** negative / error
- **Priority:** P0
- **Preconditions / seed:** Parent session; an email that maps to **no** child (force backend 404, or use a known-absent address).
- **Steps:**
  1. Enter the non-existent child's email; submit.
- **Expected:** `ServerErrorBanner` shows the localized **not-found** message (`parent.linkChild.errors.notFound`) — mapped from 404 / body hint ("not found"/"no child"/"does not exist"). No raw envelope text, no success card, list unchanged.
- **Traces to:** AC5 (non-existent child → clear error).

### FE-TC-14 — Linking an already-linked child shows the already-linked error
- **Type:** negative / error
- **Priority:** P1
- **Preconditions / seed:** Parent A already linked to child A1; attempt to link A1's email again (force 409, or use the already-linked email).
- **Steps:**
  1. Enter A1's email; submit.
- **Expected:** Banner shows the localized **already-linked** message (`parent.linkChild.errors.alreadyLinked`) — mapped from 409 / body hint ("already"/"linked"). No duplicate card appears in My-Children.
- **Traces to:** AC5 / AC4 boundary (idempotent linkage).

---

## Group E — RTL (Arabic default) vs LTR (English)

### FE-TC-16 — My-Children renders RTL in the default Arabic locale
- **Type:** RTL-i18n
- **Priority:** P1
- **Preconditions / seed:** Parent with ≥2 children; locale left at the **default (Arabic)**.
- **Steps:**
  1. Sign in (Arabic default); open My-Children.
- **Expected:** Container direction is RTL (`dir="rtl"` / logical row reversal — header row, pick-a-child row, and the web 3-col grid reverse so the Add-Child card lands at the visual left). Headings carry `writingDirection="rtl"`. No copy assertions — assert on `dir`/computed direction + structural order.
- **Traces to:** RTL NFR.

### FE-TC-17 — My-Children + Link-Child render LTR after switching to English
- **Type:** RTL-i18n
- **Priority:** P1
- **Preconditions / seed:** Parent session; switch UI language to English (via the login `LocaleThemeControls` or persisted preference).
- **Steps:**
  1. With English active, open My-Children and the Link-Child screen.
- **Expected:** Direction is LTR; rows/grid order are the English (non-reversed) arrangement; the brand wordmark "Learnexia" stays Latin/LTR in both locales. Error/empty/success text resolves to English strings (no raw keys).
- **Traces to:** LTR/i18n NFR.

### FE-TC-18 — Child lands in the child's own language, not the device locale (BLOCKED)
- **Type:** RTL-i18n / state (locale follows linkage)
- **Priority:** P1
- **Preconditions / seed:** A child account whose `/Me.preferredLanguage` differs from the device default (e.g. child = English while default = Arabic). Depends README Q6/Q7.
- **Steps:**
  1. Sign in as the child (device/default locale = Arabic).
  2. Observe the child home.
- **Expected:** `useAuthRoute` calls `setLocale(preferredLanguage)` → the child home renders in the **child's** language/direction (English/LTR here), not the Arabic default.
- **BLOCKED:** requires a child account with a set `preferredLanguage` (README Q6/Q7).
- **Traces to:** locale-follows-child rule in `useAuthRoute`.

---

## Group F — Routing edge cases + product overrides

### FE-TC-19 — No wrong-surface flash while `/Me` is loading
- **Type:** state (loading) / regression
- **Priority:** P0
- **Preconditions / seed:** Parent with children; delay the `/Me` response (Playwright route delay).
- **Steps:**
  1. Sign in; during the `/Me`-loading window, watch the rendered surface.
- **Expected:** The splash holds (no parent My-Children and no child dashboard flashes) until `/Me` resolves; only then does the guard `replace` to the correct home. Assert the child-home/parent-home markers are **absent** during the loading window.
- **Traces to:** `useAuthRoute` no-flash guarantee; role-routing.

### FE-TC-20 — No teacher persona anywhere in the login/linkage flow
- **Type:** negative / product-override
- **Priority:** P1
- **Preconditions / seed:** None (anonymous on `/login`).
- **Steps:**
  1. Inspect the persona toggle and any role affordances on login + linkage surfaces.
- **Expected:** The persona toggle exposes exactly **two** options (Parent, Student) — no Teacher. No teacher-role copy/route is reachable.
- **Traces to:** product override (no teacher role).

### FE-TC-21 — No student self-registration path
- **Type:** negative / product-override
- **Priority:** P1
- **Preconditions / seed:** None (anonymous on `/login`).
- **Steps:**
  1. Follow the "new parent" footer link from login.
- **Expected:** It routes to **parent** registration only (`/(auth)/register`). There is no student self-register entry point; the persona=Student toggle does not unlock a registration path (registration is parent-driven).
- **Traces to:** product override (parent-driven onboarding; students don't self-register).

### FE-TC-22 — Persona toggle is a UI hint only — it does not change the routed home (BLOCKED)
- **Type:** negative / auth-authz / product-override
- **Priority:** P2
- **Preconditions / seed:** A **parent** account with children.
- **Steps:**
  1. On `/login`, select persona = **Student**.
  2. Sign in with the **parent** credentials.
- **Expected:** Despite the Student persona hint, `/Me` reports the parent role and the guard routes to the **parent** home (not `/(child)`). Confirms routing is driven by `/Me` roles, not the toggle.
- **BLOCKED (partial):** the inverse (child creds with Parent persona still lands on child home) needs a child account (README Q7) — `fixme` that half. The parent-creds half is runnable now.
- **Traces to:** product override (persona toggle ≠ role); role-routing.

---

## Implementation notes for `frontend-e2e-tester`
- Group the spec by the headings above; one `test(...)` per FE-TC, ID in the title (e.g. `FE-TC-01 — parent lands on parent home`).
- Prefer Playwright **route interception** to force loading/error/404/409 branches (FE-TC-02/13/14/15/19) where a real seed is impractical — but for scope/linkage truth (FE-TC-06/07/08/11) use **real seeded data via the API**, not mocks.
- For every BLOCKED case, leave the test in place with `test.fixme(...)` and the exact blocker (e.g. "no child sign-in credential — README Q7"), so the report shows the gap rather than hiding it.
- For each missing `testID` you had to work around (Q1–Q5), list the exact hook you need in `execution-report.md` → "Selector hooks requested from `frontend`".
