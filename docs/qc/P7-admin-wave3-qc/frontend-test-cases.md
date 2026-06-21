# P7 Admin Wave 3 — Frontend E2E Test Cases

> Surface: `apps/admin-dashboard` (Next.js 15, port **3001**). Driver: Playwright, existing `admin` project.
> Backend: `:5080` (Development, CORS allows `:3001`). Admin login: `/login`, body `{ userName, password }` = **`superadmin` / `123Pa$$word!`**.
> Stories: **P7-09** moderation queue · **P7-12** audit-log viewer · **P7-13** gamification overrides — all shipped + merged in `apps/admin-dashboard`.
>
> **For the implementer (`frontend-e2e-tester`):**
> - Reuse the auth fixture style from `tests/e2e/specs/P7-admin-batch1.spec.ts` — `loginAsAdmin(page)` (label `Username`/`Password`, button `/sign in/i`, redirect `/dashboard`) and `getAdminToken(request)` (POST `/api/Users/Authentication/Sign-In`).
> - Selector strategy: **`getByTestId` first** (all Wave-3 surfaces ship `data-testid`s — enumerated per case), then role/label, then text. Enums are **INT on the wire** (moderation status/source; tier; rarity; etc.) — the FE selects send ints; assert on rendered labels, not wire values.
> - **Locale is build-time `ADMIN_LOCALE='en'`** — there is no runtime `ar`/RTL toggle in this run. All RTL/AR cases are **BLOCKED** and verified statically (assert the bilingual string + RTL logical-prop intent exists in source), not by driving the browser. Marked `[BLOCKED-RTL]`.
> - **Backend contract-smoke** (auth on every endpoint, envelope `Successed`, enum binding, validators) is **pre-existing** — the BE shipped + tested. Do not re-test the API surface here beyond what the UI exercises. A short BE smoke list is at the end for reference only.
> - **SEED CAVEATS** (read `coverage-report.md` §Test Data before implementing): the **moderation queue may be empty** (items arrive only from AI-flagged safety events); the **audit log populates as admins act**; the **badge/mission/timed-event catalogs are seeded** (BadgeSeeder/MissionSeeder/TimedEventSeeder). Cases needing a `ModerationItem` are marked **`[SEED-DEPENDENT]`** with the obtain-or-block instruction inline.

---

## Schema per case
**ID · Title · Type · Priority · Target agent · Preconditions/seed · Steps · Expected · Traces to.**
Target agent for every case below: **`frontend-e2e-tester`**.

---

# Group A — P7-09 Content Moderation Queue

Route: `/moderation` (list), `/moderation/[id]` (detail). Enum wire ints: ModerationStatus Pending=0 Approved=1 Rejected=2 Flagged=3; ModerationSource AiOutput=0 CurriculumUpload=1.

### Queue list (page `moderation/page.tsx`)

| Field | Value |
|---|---|
| **ID** | MOD-TC-01 |
| **Title** | Queue list renders the table with all six columns, newest-first |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Logged in as admin. `[SEED-DEPENDENT]` — at least one `ModerationItem` exists (see Test Data; if none, this case asserts the empty state instead and is logged as not-fully-verifiable). |
| **Steps** | 1. Navigate `/moderation`. 2. Wait for `mod-table`. 3. Read column headers via `mod-col-source`, `mod-col-contentRef`, `mod-col-subjectGrade`, `mod-col-taskKind`, `mod-col-status`, `mod-col-detected`. 4. Read `detectedAt` of the first two rows. |
| **Expected** | `mod-table` visible; 6 `scope="col"` headers present with EN labels; first row `detectedAt` ≥ second row's (desc). Each row has `data-testid="mod-row-{id}"`, a `SourceBadge`, a moderation `StatusBadge`, content ref in a `dir="ltr"` mono span. |
| **Traces to** | P7-09 AC 1 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-02 |
| **Title** | Loading skeleton state shows before data resolves |
| **Type** | state (loading) |
| **Priority** | P1 |
| **Preconditions** | Admin. Throttle/slow the `Queue` response (route interception) or assert on first paint. |
| **Steps** | 1. Intercept `**/api/Admin/Moderation/Queue*` with a delay. 2. Navigate `/moderation`. 3. Assert `mod-loading` (role=status) visible while pending. 4. Release; assert it disappears. |
| **Expected** | `mod-loading` skeleton (6 shimmer rows) visible during fetch; replaced by `mod-table` / `mod-empty-state` on resolve. |
| **Traces to** | P7-09 AC 3 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-03 |
| **Title** | Empty state (no filters) reads as "no items", not an error |
| **Type** | state (empty) |
| **Priority** | P0 |
| **Preconditions** | Admin. Queue genuinely empty, OR intercept `Queue` to return `Successed:true` with empty `data` + `totalCount:0`. |
| **Steps** | 1. Navigate `/moderation` with an empty queue. 2. Assert `mod-empty-state` visible; `mod-error-banner` NOT present. 3. Read empty copy. |
| **Expected** | `mod-empty-state` shows `modEmptyNoFilters` heading + neutral body (not "error", not "coming soon"). No retry/error chrome. |
| **Traces to** | P7-09 AC 3 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-04 |
| **Title** | Error + retry state surfaces on a failed Queue fetch |
| **Type** | state (error) |
| **Priority** | P0 |
| **Preconditions** | Admin. Intercept `Queue` → 500 once, then allow. |
| **Steps** | 1. Force `Queue` → 500. 2. Navigate `/moderation`. 3. Assert `mod-error-banner` + `mod-retry-btn`. 4. Click `mod-retry-btn` with the route now healthy. 5. Assert results/empty replace the error. |
| **Expected** | Error banner (`modListError`) + retry shown on failure; clicking retry refetches and clears the error. |
| **Traces to** | P7-09 AC 3 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-05 |
| **Title** | Status filter sends the correct INT and narrows results |
| **Type** | functional / validation |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. Navigate `/moderation`. 2. Capture the `Queue` request when selecting `mod-status-filter` = "Pending". 3. Inspect the query string. |
| **Expected** | Request carries `Status=0` (Pending int), `PageNumber=1`. Result set respects the filter (all visible rows show Pending badge when seeded). |
| **Traces to** | P7-09 AC 2 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-06 |
| **Title** | Source filter sends correct INT; CurriculumUpload yields a legitimately-empty list |
| **Type** | functional / state (empty) |
| **Priority** | P1 |
| **Preconditions** | Admin. No live CurriculumUpload producer. |
| **Steps** | 1. Select `mod-source-filter` = "CurriculumUpload". 2. Capture request. 3. Assert state. |
| **Expected** | Request carries `Source=1`. List shows `mod-empty-state` with **filtered** copy (`modEmptyFiltered`) — neutral, not an error; clear-filters affordance present. |
| **Traces to** | P7-09 AC 2, AC 3 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-07 |
| **Title** | Subject filter offers exactly the 4 product subjects (no Social Studies) |
| **Type** | negative / product-override |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Open `mod-subject-filter`. 2. Enumerate option labels. |
| **Expected** | Options = All + Math, Science, Arabic, English only. **No "Social Studies"** option. Selecting one sends `SubjectCode=<name>` string. |
| **Traces to** | P7-09 AC 2; product override (4 subjects) |

| Field | Value |
|---|---|
| **ID** | MOD-TC-08 |
| **Title** | Grade filter spans 1–12 and sends Grade as int |
| **Type** | boundary |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. Open `mod-grade-filter`; assert 12 grade options (1..12) + All. 2. Select Grade 12; capture request. |
| **Expected** | 12 grades present; request carries `Grade=12`. |
| **Traces to** | P7-09 AC 2 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-09 |
| **Title** | Date-range filters send ISO bounds (00:00:00Z / 23:59:59Z) |
| **Type** | boundary |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. Set `mod-date-from` = a date; `mod-date-to` = a date. 2. Capture request. |
| **Expected** | `DateFrom=<d>T00:00:00Z`, `DateTo=<d>T23:59:59Z` in the query. |
| **Traces to** | P7-09 AC 2 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-10 |
| **Title** | Free-text search is debounced and sent as `Search` |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Type rapidly into `mod-search-input`. 2. Count `Queue` requests over ~500ms. 3. Inspect the final request. |
| **Expected** | Only one trailing request fires (debounce ~350ms); it carries `Search=<final value>`. Intermediate keystrokes do not each fire a request. |
| **Traces to** | P7-09 AC 2 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-11 |
| **Title** | Any filter change resets to page 1 |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. Queue large enough for >1 page, OR intercept to force `totalPages>1`. |
| **Steps** | 1. Page to 2 via `mod-pagination-next`; assert `mod-page-indicator` shows page 2. 2. Change `mod-status-filter`. 3. Capture request + read indicator. |
| **Expected** | New `Queue` request carries `PageNumber=1`; `mod-page-indicator` returns to page 1. |
| **Traces to** | P7-09 AC 1, AC 2 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-12 |
| **Title** | Pagination prev/next + keepPreviousData (rows stay during refetch) |
| **Type** | functional / state |
| **Priority** | P1 |
| **Preconditions** | Admin. `totalPages>1` (force via interception if needed). |
| **Steps** | 1. Assert `mod-pagination-prev` disabled on page 1. 2. Click `mod-pagination-next`; during the in-flight refetch assert the previous rows remain mounted (table not unmounted to skeleton) and `mod-table` opacity dims (isFetching). 3. Assert indicator increments. |
| **Expected** | Prev disabled on first page; next advances and sends `PageNumber=2`; old rows visible (dimmed) during refetch, not a full skeleton; next disabled on last page. |
| **Traces to** | P7-09 AC 1 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-13 |
| **Title** | Clear-filters appears only when a filter is active and resets all |
| **Type** | functional |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. Assert `mod-clear-filters` absent with no filters. 2. Set status + subject. 3. Assert `mod-clear-filters` present; click it. 4. Assert all selects reset and request has no filter params. |
| **Expected** | Clear button is conditional; clicking it empties every filter and returns to page 1. |
| **Traces to** | P7-09 AC 2 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-14 |
| **Title** | Row click navigates to the detail route |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. `[SEED-DEPENDENT]` — ≥1 item. |
| **Steps** | 1. Navigate `/moderation`. 2. Click `mod-row-{id}`. 3. Assert URL = `/moderation/{id}`. |
| **Expected** | Navigates to `/moderation/{id}`; detail card renders. |
| **Traces to** | P7-09 AC 1, AC 4 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-15 |
| **Title** | Row is keyboard-activable (Enter / Space) |
| **Type** | a11y |
| **Priority** | P1 |
| **Preconditions** | Admin. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Focus `mod-row-{id}` (tabIndex=0, role=button). 2. Press Enter. 3. Assert navigation. 4. Back, focus row, press Space; assert navigation (no page scroll). |
| **Expected** | Enter and Space both navigate to detail; row exposes `role="button"` + descriptive `aria-label`. |
| **Traces to** | P7-09 AC 9 |

### Detail (page `moderation/[id]/page.tsx`)

| Field | Value |
|---|---|
| **ID** | MOD-TC-16 |
| **Title** | Detail renders facets card with all item fields |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. `[SEED-DEPENDENT]` — a known item id. |
| **Steps** | 1. Navigate `/moderation/{id}`. 2. Assert `mod-detail-card`, `mod-facets-card`. 3. Read Source, Status, Content ref, Subject, Grade, Task kind, Source Event ID, Detected at, Created at. |
| **Expected** | Facets card shows every field; nullable fields render `—`; Student ID block shown only when non-null; ids/timestamps in `dir="ltr"`. |
| **Traces to** | P7-09 AC 4 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-17 |
| **Title** | SafetyVerdict shows failed checks + reason codes + action + model — never raw content |
| **Type** | functional / security |
| **Priority** | P0 |
| **Preconditions** | Admin. Item whose `safetyVerdict` has `failedChecks`/`reasonCodes` (seed via DB/API per Test Data). |
| **Steps** | 1. Open detail. 2. Assert `mod-verdict-section`. 3. Assert the privacy notice (`modVerdictPrivacyNote`) is always present. 4. Assert failed-check chips + reason-code chips render the array names. 5. Assert no field labelled prompt/response/raw content appears anywhere on the page. |
| **Expected** | Verdict section lists stable check/code names as chips, action taken, model id; privacy framing note always shown. **No raw prompt/response text** is rendered. |
| **Traces to** | P7-09 AC 4; PII-light invariant |

| Field | Value |
|---|---|
| **ID** | MOD-TC-18 |
| **Title** | SafetyVerdict degrades gracefully for empty/unparseable JSON |
| **Type** | negative |
| **Priority** | P1 |
| **Preconditions** | Admin. Item with `safetyVerdict` = `"{}"` or malformed (intercept detail response to inject). |
| **Steps** | 1. Open detail with `safetyVerdict="{}"`. 2. Assert `mod-verdict-section` present; `modVerdictUnavailable` fallback shown; no crash/blank page. 3. Repeat with malformed JSON. |
| **Expected** | Fallback "unavailable" message; privacy notice still shown; page does not error. |
| **Traces to** | P7-09 AC 4 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-19 |
| **Title** | Review history shows only for reviewed items |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. One reviewed item (`reviewedByUserId != null`) and one un-reviewed Pending item. |
| **Steps** | 1. Open the reviewed item; assert `mod-review-history` with reviewed-by id, reviewed-at, reason. 2. Open a Pending un-reviewed item; assert `mod-review-history` absent. |
| **Expected** | History card conditional on `reviewedByUserId`; shows actor id (mono ltr), timestamp, decision reason when present. |
| **Traces to** | P7-09 AC 4 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-20 |
| **Title** | 404 not-found state for a bad item id |
| **Type** | negative / state |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. Navigate `/moderation/99999999` (non-existent). 2. Assert the not-found block (`modNotFoundHeading` + body), not a generic error. |
| **Expected** | 404 → dedicated not-found state; no review panel; no crash. |
| **Traces to** | P7-09 AC 4 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-21 |
| **Title** | Detail general-error + retry on a 500 |
| **Type** | state (error) |
| **Priority** | P1 |
| **Preconditions** | Admin. Intercept detail GET → 500. |
| **Steps** | 1. Open a detail with the GET forced to 500. 2. Assert `mod-detail-error` + retry. 3. Heal route, click retry, assert content loads. |
| **Expected** | 500 (non-404) shows `modDetailError` banner + retry distinct from the 404 state. |
| **Traces to** | P7-09 AC 4 |

### Review action + gating

| Field | Value |
|---|---|
| **ID** | MOD-TC-22 |
| **Title** | Pending item shows Approve / Reject / Flag buttons |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. A **Pending** item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Open a Pending detail. 2. Assert `mod-review-actions-panel` shows `mod-review-approve-btn`, `mod-review-reject-btn`, `mod-review-flag-btn`. |
| **Expected** | All three action buttons present for Pending. |
| **Traces to** | P7-09 AC 5, AC 6 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-23 |
| **Title** | Approve with optional reason succeeds; status updates only after refetch (no optimistic) |
| **Type** | functional / no-optimistic |
| **Priority** | P0 |
| **Preconditions** | Admin. A Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Open Pending detail; click `mod-review-approve-btn`. 2. Assert `review-item-dialog` opens with `dialog-confirm-btn` enabled (reason optional). 3. Intercept the POST `Review` + the follow-up detail GET with a small delay. 4. Click confirm. 5. During the in-flight window, assert the status badge has NOT yet flipped to Approved. 6. After refetch resolves, assert status badge = Approved and `mod-review-success` banner shows; dialog closed; review buttons gone. |
| **Expected** | POST sends `{ decision:"Approved" }` (or int per wire) without admin id; status flips to Approved **only after** the invalidated refetch lands; success banner; terminal gating now applies. |
| **Traces to** | P7-09 AC 5, AC 7 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-24 |
| **Title** | Reject confirm is disarmed until a reason is entered |
| **Type** | validation |
| **Priority** | P0 |
| **Preconditions** | Admin. A Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Click `mod-review-reject-btn`. 2. Assert `dialog-confirm-btn` has `aria-disabled="true"` with empty reason; clicking does nothing (no POST). 3. Type a reason into `review-reason-field`. 4. Assert confirm becomes `aria-disabled="false"`. |
| **Expected** | Reject confirm gated on non-empty trimmed reason; no POST fires while disarmed. |
| **Traces to** | P7-09 AC 5 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-25 |
| **Title** | Reject with reason succeeds and transitions to Rejected |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Preconditions** | Admin. A Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Reject with a reason; confirm. 2. Assert POST body carries `reason`. 3. After refetch, assert status = Rejected and the reason appears in review history. |
| **Expected** | Reject persists; status Rejected after refetch; reason recorded in history. |
| **Traces to** | P7-09 AC 5, AC 7 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-26 |
| **Title** | Reason field bounded to 2000 chars (not the default 500) |
| **Type** | boundary |
| **Priority** | P1 |
| **Preconditions** | Admin. Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Open Reject dialog. 2. Inspect `review-reason-field` maxLength / counter. 3. Attempt to enter >2000 chars. |
| **Expected** | Field cap is 2000 (ReasonField passed `maxLength={2000}`); cannot exceed; confirm allowed at exactly 2000. |
| **Traces to** | P7-09 AC 5 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-27 |
| **Title** | Flag a Pending item succeeds → Flagged |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Click `mod-review-flag-btn` (optional reason). 2. Confirm. 3. After refetch assert status = Flagged. |
| **Expected** | POST `{decision:"Flagged"}`; status Flagged after refetch. |
| **Traces to** | P7-09 AC 5, AC 6 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-28 |
| **Title** | Flagged item allows Approve/Reject but hides the Flag button |
| **Type** | functional / negative |
| **Priority** | P0 |
| **Preconditions** | Admin. A **Flagged** item. `[SEED-DEPENDENT]` (flag a Pending item first, or seed). |
| **Steps** | 1. Open a Flagged detail. 2. Assert `mod-review-approve-btn` + `mod-review-reject-btn` present; `mod-review-flag-btn` **absent**; `modAlreadyFlagged` notice shown. |
| **Expected** | Re-flag is not offered (FE won't construct the 400 `AlreadyFlagged` request); Approve/Reject still available. |
| **Traces to** | P7-09 AC 6 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-29 |
| **Title** | Terminal item (Approved) shows NO review buttons + a terminal notice |
| **Type** | negative / auth-of-state |
| **Priority** | P0 |
| **Preconditions** | Admin. An **Approved** item. `[SEED-DEPENDENT]` (approve one in MOD-TC-23 or seed). |
| **Steps** | 1. Open an Approved detail. 2. Assert `mod-terminal-notice` present; `mod-review-approve-btn`/`reject`/`flag` all **absent**. 3. Repeat for a Rejected item. |
| **Expected** | Approved + Rejected are terminal: no action buttons, locked-notice shown. |
| **Traces to** | P7-09 AC 6 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-30 |
| **Title** | Review error (400 terminal/already-flagged) keeps dialog open with mapped message |
| **Type** | negative |
| **Priority** | P1 |
| **Preconditions** | Admin. Pending item. Intercept POST `Review` → 400 with message containing "flagged" / "terminal". |
| **Steps** | 1. Open Approve dialog; confirm with POST forced to 400 ("terminal"). 2. Assert dialog stays open; `AdminErrorBanner` shows `modErrAlreadyTerminal`. 3. Repeat with "flagged" message → `modErrAlreadyFlagged`. 4. Force 404 → `modErr404`; 422 → inline validation; network fail → `modErrNetwork`. |
| **Expected** | Dialog does NOT close on error; correct mapped message per status/keyword; admin can retry or cancel. |
| **Traces to** | P7-09 AC 7 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-31 |
| **Title** | Review invalidates + refetches the queue list (status reflected on return) |
| **Type** | functional / no-optimistic |
| **Priority** | P1 |
| **Preconditions** | Admin. Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Approve an item on detail. 2. Navigate back to `/moderation` (or filter by Approved). 3. Assert the item now shows Approved status in the list (came from refetch, not cache). |
| **Expected** | `adminModeration.all` invalidation makes the list reflect the new status without a hard reload. |
| **Traces to** | P7-09 AC 7 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-32 |
| **Title** | Review dialog: focus trap, ESC cancels, backdrop does NOT dismiss |
| **Type** | a11y |
| **Priority** | P1 |
| **Preconditions** | Admin. Pending item. `[SEED-DEPENDENT]`. |
| **Steps** | 1. Open `review-item-dialog` (role=dialog, aria-modal). 2. Tab through; assert focus stays within dialog. 3. Click the backdrop; assert dialog stays open. 4. Press ESC; assert dialog closes via `dialog-cancel-btn` path. |
| **Expected** | Focus trapped; ESC closes; backdrop click is inert (no accidental dismiss of a destructive action). |
| **Traces to** | P7-09 AC 9 |

### Moderation auth / nav

| Field | Value |
|---|---|
| **ID** | MOD-TC-33 |
| **Title** | Anonymous user is redirected away from `/moderation` and `/moderation/[id]` |
| **Type** | auth-authz |
| **Priority** | P0 |
| **Preconditions** | No session (fresh context, no token). |
| **Steps** | 1. Navigate `/moderation` unauthenticated. 2. Assert redirect to `/login`; no queue rows flash. 3. Repeat for `/moderation/1`. |
| **Expected** | Guard redirects to `/login`; no protected moderation content rendered pre-guard. |
| **Traces to** | P7-09 AC 8 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-34 |
| **Title** | Moderation nav item present, active-aware |
| **Type** | functional |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. On `/moderation`, assert the AdminSideNav "Moderation" item is active. 2. Navigate elsewhere; assert it deactivates. |
| **Expected** | `navModeration` item highlights for `/moderation` + `/moderation/*`. |
| **Traces to** | P7-09 AC 8 |

| Field | Value |
|---|---|
| **ID** | MOD-TC-35 |
| **Title** | `[BLOCKED-RTL]` Moderation copy is bilingual EN+AR; ltr islands for refs/dates |
| **Type** | RTL-i18n |
| **Priority** | P2 |
| **Preconditions** | `ADMIN_LOCALE='en'` build — no runtime ar. |
| **Steps** | STATIC: verify `lib/strings.ts` has AR + EN for all `mod*` keys used; verify content refs/dates/ids carry `dir="ltr"`. |
| **Expected** | Bilingual strings exist; ltr islands present in source. **BLOCKED** for live RTL drive (no ar build). |
| **Traces to** | P7-09 AC 9 |

---

# Group B — P7-12 Admin Action Audit Log

Route: `/audit`. Read-only. Hits `GET /api/Admin/Audit/Log`.

| Field | Value |
|---|---|
| **ID** | AUD-TC-01 |
| **Title** | Audit page renders the read-only table with 4 columns, newest-first |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. ≥1 audit row (auto-populates — any admin mutation, e.g. create a badge/subject, emits an entry; see Test Data). |
| **Steps** | 1. Navigate `/audit`. 2. Assert `audit-table`. 3. Read headers Admin / Action / Target / When (+ sr-only Details). 4. Compare `occurredAtUtc` order of first rows. |
| **Expected** | Table renders; columns Admin (#id chip), Action (`ActionTypeBadge`), Target (`Type #id`), When (ltr timestamp); newest-first. |
| **Traces to** | P7-12 AC 3 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-02 |
| **Title** | Loading skeleton state |
| **Type** | state (loading) |
| **Priority** | P1 |
| **Preconditions** | Admin. Delay `Log` response. |
| **Steps** | 1. Intercept `**/api/Admin/Audit/Log*` with delay. 2. Navigate `/audit`. 3. Assert `audit-loading` (role=status). |
| **Expected** | Skeleton rows shown during fetch, replaced on resolve. |
| **Traces to** | P7-12 AC 5 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-03 |
| **Title** | Empty state when no rows |
| **Type** | state (empty) |
| **Priority** | P1 |
| **Preconditions** | Admin. Intercept `Log` → success with empty `data`. |
| **Steps** | 1. Force empty `Log`. 2. Navigate `/audit`. 3. Assert `audit-empty-state`; no error chrome. |
| **Expected** | Empty heading + body; not an error; clear-filters offered only when a filter is active. |
| **Traces to** | P7-12 AC 5 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-04 |
| **Title** | Error + retry on a failed Log fetch |
| **Type** | state (error) |
| **Priority** | P0 |
| **Preconditions** | Admin. Intercept `Log` → 500. |
| **Steps** | 1. Navigate `/audit` with `Log` 500. 2. Assert `audit-error-banner` + `audit-retry-button`. 3. Heal + click retry; assert results/empty. |
| **Expected** | Error banner + retry; retry refetches. |
| **Traces to** | P7-12 AC 5 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-05 |
| **Title** | Filter by action type sends `ActionType` and resets to page 1 |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. Select `audit-filter-action-type` (e.g. `Subject.Created`). 2. Capture request; assert `ActionType=Subject.Created`, `PageNumber=1`. |
| **Expected** | Action-type string sent verbatim; page reset; rows narrowed. Options grouped by domain (`optgroup`). |
| **Traces to** | P7-12 AC 4 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-06 |
| **Title** | Action-type dropdown includes the newer admin actions (child + gamification) |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Open `audit-filter-action-type`. 2. Assert presence of `Child.LearningLanguageChanged`, `Child.GradeOverridden`, `Gamification.LeagueTierOverridden`, `Gamification.StreakFreezeGranted`, plus badge/mission/timed-event create/update/activate/deactivate actions with localized labels. |
| **Expected** | All listed newer actions are selectable with friendly labels (not raw constants only). |
| **Traces to** | P7-12 AC 7 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-07 |
| **Title** | Filter by target entity type sends `TargetEntityType` |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Select `audit-filter-target-type` (e.g. Subject/User/Child). 2. Capture request. |
| **Expected** | `TargetEntityType=<key>` sent; page reset to 1. |
| **Traces to** | P7-12 AC 4 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-08 |
| **Title** | Filter by acting admin id sends int `AdminUserId` |
| **Type** | functional / boundary |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Enter a numeric id in `audit-filter-admin-id`. 2. Capture request. 3. Enter non-numeric / empty; assert param omitted. |
| **Expected** | Valid int → `AdminUserId=<n>`; empty/NaN → param omitted (no malformed query). |
| **Traces to** | P7-12 AC 4 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-09 |
| **Title** | Date range sends ISO bounds; DateTo < DateFrom blocks the call with an inline error |
| **Type** | validation / boundary |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Set `audit-filter-date-from` later than `audit-filter-date-to`. 2. Assert a `role="alert"` `auditDateRangeError` shows and **no `Log` request** fires with that pair (or only a no-filter request). 3. Fix the order; assert `DateFrom=...T00:00:00Z` + `DateTo=...T23:59:59Z` sent. |
| **Expected** | Invalid range → inline alert, query suppressed; valid range → ISO bounds sent. |
| **Traces to** | P7-12 AC 4 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-10 |
| **Title** | Pagination prev/next + keepPreviousData |
| **Type** | functional / state |
| **Priority** | P1 |
| **Preconditions** | Admin. `totalPages>1` (force via interception if needed). |
| **Steps** | 1. Assert `audit-pagination-prev` disabled on page 1. 2. Click `audit-pagination-next`; assert `PageNumber=2`, indicator increments, prior rows stay during refetch (dimmed). |
| **Expected** | Server pagination works; previous data retained during refetch. |
| **Traces to** | P7-12 AC 5 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-11 |
| **Title** | Row inline-expand reveals the full detail panel |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. ≥1 row. |
| **Steps** | 1. Click `audit-expand-{id}` (or `audit-row-{id}`). 2. Assert `audit-detail-{id}` row appears with EventId, Admin #id, Action badge + raw action string, Target type, Target id, OccurredAt, CreatedAt. 3. Assert `aria-expanded` toggles true. 4. Click again; panel collapses. |
| **Expected** | Inline-expand shows all 8 fields; toggle button exposes `aria-expanded`/`aria-controls`; collapse works. |
| **Traces to** | P7-12 AC 6 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-12 |
| **Title** | Details rendered as escaped text / pretty JSON — never HTML |
| **Type** | security |
| **Priority** | P0 |
| **Preconditions** | Admin. A row whose `details` is JSON, and one with `details=null`. Intercept `Log` to inject a `details` string containing HTML-like text (e.g. `<img onerror=...>`). |
| **Steps** | 1. Expand a JSON-details row; assert a `<pre dir="ltr">` with pretty-printed JSON. 2. Expand the injected-HTML row; assert the markup is shown as literal text (no element created, no execution). 3. Expand the null-details row; assert `auditDetailNoDetails` (no `<pre>`). |
| **Expected** | Details always text; pretty JSON when parseable; HTML never interpreted (no `dangerouslySetInnerHTML`); null → "—"/no-details. |
| **Traces to** | P7-12 AC 6; security (XSS sink) |

| Field | Value |
|---|---|
| **ID** | AUD-TC-13 |
| **Title** | ZERO mutation affordances anywhere on the audit surface |
| **Type** | negative / security |
| **Priority** | P0 |
| **Preconditions** | Admin. ≥1 row, expanded. |
| **Steps** | 1. On `/audit` with a row expanded, scan for any edit/delete/restore/save button or form input besides filters + expand toggle + copy. 2. Assert none exist. 3. Assert the only button inside the detail panel is the copy-to-clipboard (`audit-detail-copy-{id}`), which performs no network call. |
| **Expected** | No write controls; no mutating network request is possible from this page. Copy button is client-only. |
| **Traces to** | P7-12 AC 6, AC 8 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-14 |
| **Title** | No export affordance (deferred — AC gap) |
| **Type** | negative |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. Scan `/audit` for any export/download/CSV button. 2. Assert none present. |
| **Expected** | No export control (backend has no export endpoint; documented AC gap — must NOT be faked client-side). |
| **Traces to** | P7-12 AC 12 (gap) |

| Field | Value |
|---|---|
| **ID** | AUD-TC-15 |
| **Title** | Audit references ids/enum states only — no names/emails/raw content |
| **Type** | security / PII |
| **Priority** | P1 |
| **Preconditions** | Admin. A row whose target is a User/Child. |
| **Steps** | 1. Expand the row. 2. Assert only `#adminUserId`, `TargetType #targetEntityId`, and the `details` (ids + states) appear. 3. Assert no email/full-name enrichment is shown. |
| **Expected** | UI renders only the DTO ids + enum/state `details`; no join to user PII. |
| **Traces to** | P7-12 AC 11 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-16 |
| **Title** | Anonymous user redirected from `/audit` (no data flash) |
| **Type** | auth-authz |
| **Priority** | P0 |
| **Preconditions** | No session. |
| **Steps** | 1. Navigate `/audit` unauthenticated. 2. Assert redirect to `/login`; no audit rows flash. |
| **Expected** | Guard redirects; no audit data leaked pre-guard. |
| **Traces to** | P7-12 AC 1 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-17 |
| **Title** | Audit nav item present + active-aware |
| **Type** | functional |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. On `/audit`, assert "Audit log" nav active. |
| **Expected** | `navAuditLog` item highlights for `/audit` + `/audit/*`. |
| **Traces to** | P7-12 AC 2 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-18 |
| **Title** | A11y: table caption, scope=col headers, aria-live region, keyboard-operable expand |
| **Type** | a11y |
| **Priority** | P1 |
| **Preconditions** | Admin. ≥1 row. |
| **Steps** | 1. Assert `<caption class="sr-only">` + `scope="col"` headers. 2. Assert results region is `aria-live="polite"`. 3. Focus `audit-expand-{id}`, press Enter/Space; assert toggle. |
| **Expected** | Semantic table + aria-live + keyboard-operable expand button. |
| **Traces to** | P7-12 AC 10 |

| Field | Value |
|---|---|
| **ID** | AUD-TC-19 |
| **Title** | `[BLOCKED-RTL]` Audit copy bilingual EN+AR; ids/timestamps/EventId in ltr islands |
| **Type** | RTL-i18n |
| **Priority** | P2 |
| **Preconditions** | `ADMIN_LOCALE='en'` build. |
| **Steps** | STATIC: verify `audit*` strings exist EN+AR; ids/timestamps/EventId/`<pre>` are `dir="ltr"`. |
| **Expected** | Bilingual + ltr islands present in source. **BLOCKED** for live RTL drive. |
| **Traces to** | P7-12 AC 9 |

---

# Group C — P7-13 Gamification Overrides

Routes: `/gamification` (hub), `/gamification/badges`, `/gamification/missions`, `/gamification/events`. Student overrides launched from `/users/[id]`. Enum ints: LeagueTier Bronze=1..Diamond=4; BadgeRarity 1-4; BadgeTriggerType 1-3; MissionType Daily=1/Weekly=2; MissionTargetType 1-3; TimedEventScope 1-3.

### Hub + nav + guard

| Field | Value |
|---|---|
| **ID** | GAM-TC-01 |
| **Title** | Gamification hub renders 3 catalog cards + student-overrides notice |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Navigate `/gamification`. 2. Assert `gamification-hub`, `hub-card-badges`, `hub-card-missions`, `hub-card-events`, `student-overrides-notice`. 3. Click `hub-card-badges-manage`; assert `/gamification/badges`. |
| **Expected** | Hub shows three manage cards + a notice directing student overrides to the Users surface; manage links navigate. |
| **Traces to** | P7-13 AC 1 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-02 |
| **Title** | Anonymous redirected from all `/gamification/*` routes |
| **Type** | auth-authz |
| **Priority** | P0 |
| **Preconditions** | No session. |
| **Steps** | 1. Navigate `/gamification`, `/gamification/badges`, `/gamification/missions`, `/gamification/events` unauthenticated. 2. Assert each redirects to `/login`; no content flash. |
| **Expected** | All gamification routes guarded; redirect to login, no protected data rendered. |
| **Traces to** | P7-13 AC 1 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-03 |
| **Title** | Gamification nav item present + active-aware |
| **Type** | functional |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. On `/gamification/badges`, assert nav "Gamification" active. |
| **Expected** | `navGamification` highlights for `/gamification` + children. |
| **Traces to** | P7-13 AC 1 |

### Badge catalog (`/gamification/badges`)

| Field | Value |
|---|---|
| **ID** | GAM-TC-04 |
| **Title** | Badge catalog lists seeded badges (active + inactive) |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. BadgeSeeder catalog seeded. |
| **Steps** | 1. Navigate `/gamification/badges`. 2. Assert `badge-table` + ≥1 `badge-row-{id}`. 3. Assert columns Code, Name, Rarity, Trigger, XP, Status, Actions; `ActiveBadge` present per row. |
| **Expected** | Seeded badges render with rarity/trigger labels; active + inactive both shown. |
| **Traces to** | P7-13 AC 2 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-05 |
| **Title** | Badge loading / empty / error states |
| **Type** | state |
| **Priority** | P1 |
| **Preconditions** | Admin. Intercept `Badges` for each state. |
| **Steps** | 1. Delay → assert `role=status` loader. 2. Empty `data` → assert empty state + `badge-empty-create-btn`. 3. 500 → assert error + `badge-retry-btn`; retry heals. |
| **Expected** | Three non-results states behave correctly; empty offers a create CTA. |
| **Traces to** | P7-13 AC 2 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-06 |
| **Title** | Create a badge; list refetches (no optimistic) and shows it |
| **Type** | functional / persistence / no-optimistic |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. Click `badge-create-btn`. 2. Fill `badge-form-code` (unique), `-name`, `-description`, `-icon-key`, `-rarity`, `-trigger`, `-reward-xp`, `-sort-order` (and `-threshold` if trigger needs it). 3. Click `badge-form-save`. 4. After the POST + list refetch, assert the new code appears in `badge-table`. |
| **Expected** | POST create; on success dialog closes, list invalidates+refetches, new badge appears (id from refetch, not the create response). Success banner shown. |
| **Traces to** | P7-13 AC 2, AC 10 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-07 |
| **Title** | Edit a badge; code field is immutable on edit |
| **Type** | functional / validation |
| **Priority** | P1 |
| **Preconditions** | Admin. ≥1 badge. |
| **Steps** | 1. Click `badge-edit-{id}`. 2. Assert `badge-form-code` disabled/read-only. 3. Change name; save. 4. Assert PUT body excludes `code`; list reflects the new name after refetch. |
| **Expected** | Edit opens prefilled; code locked; PUT updates non-code fields; list refetched. |
| **Traces to** | P7-13 AC 2 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-08 |
| **Title** | Badge form validation mirrors BE bounds |
| **Type** | validation / boundary |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Open create; submit empty → assert blocked with field errors. 2. Set rewardXp = 0 → assert rejected (must be >0). 3. Overlong code (>64) / name (>80) → assert capped/rejected. |
| **Expected** | Required fields enforced; rewardXp>0; length caps per BE validators; save disabled/blocked until valid. |
| **Traces to** | P7-13 AC 2 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-09 |
| **Title** | Deactivate a badge via PATCH behind a confirm; copy is "retire", not "delete" |
| **Type** | functional / no-optimistic |
| **Priority** | P0 |
| **Preconditions** | Admin. An **active** badge. |
| **Steps** | 1. Click `badge-deactivate-{id}`. 2. Assert `badge-deactivate-dialog` opens; copy references retire/hide (not delete). 3. Click `badge-deactivate-confirm-btn`. 4. Assert a **PATCH** `.../active {isActive:false}` fires; after refetch the row shows Inactive. |
| **Expected** | PATCH not DELETE; confirm required; status flips after refetch; wording is retire/hide. |
| **Traces to** | P7-13 AC 2, AC 10 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-10 |
| **Title** | Activate an inactive badge via PATCH + confirm |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. An **inactive** badge. |
| **Steps** | 1. Click `badge-activate-{id}`. 2. Confirm `badge-activate-confirm-btn`. 3. Assert PATCH `{isActive:true}`; row shows Active after refetch. |
| **Expected** | Reactivation via PATCH; status flips after refetch. |
| **Traces to** | P7-13 AC 2 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-11 |
| **Title** | No delete affordance on badges |
| **Type** | negative |
| **Priority** | P1 |
| **Preconditions** | Admin. ≥1 badge. |
| **Steps** | 1. Scan badge rows + form for any delete/remove control. 2. Assert none. |
| **Expected** | Only Edit + Activate/Deactivate; no delete (soft-retire only, preserves earned ledger). |
| **Traces to** | P7-13 AC 2 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-12 |
| **Title** | Badge set-active 404 maps to a friendly error |
| **Type** | negative |
| **Priority** | P2 |
| **Preconditions** | Admin. Intercept PATCH `.../active` → 404. |
| **Steps** | 1. Toggle active with PATCH forced to 404. 2. Assert `gamBadgeNotFoundError` banner; non-404 → `gamBadgeActionError`. |
| **Expected** | Mapped action errors; dialog closes; error banner shown. |
| **Traces to** | P7-13 AC 2 |

### Mission catalog (`/gamification/missions`)

| Field | Value |
|---|---|
| **ID** | GAM-TC-13 |
| **Title** | Mission catalog lists seeded missions with cadence/target labels |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. MissionSeeder catalog seeded. |
| **Steps** | 1. Navigate `/gamification/missions`. 2. Assert `mission-table` + rows; columns Code, Title key, Cadence, Target type+target, XP, Status, Actions. |
| **Expected** | Missions render with Daily/Weekly cadence + target-type labels. |
| **Traces to** | P7-13 AC 3 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-14 |
| **Title** | Mission cadence offers ONLY Daily + Weekly (no third option) |
| **Type** | negative / product-override |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. Open mission create; open `mission-form-cadence`. 2. Enumerate options. |
| **Expected** | Exactly Daily(1) + Weekly(2). **No "weekly-challenge"/third cadence.** |
| **Traces to** | P7-13 AC 3 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-15 |
| **Title** | Create a mission; list refetches and shows it |
| **Type** | functional / persistence / no-optimistic |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. `mission-create` → fill `mission-form-code` (unique), `-icon-key`, `-title-key`, `-cadence`, `-target-type`, `-target`, `-reward-xp`, `-sort-order`. 2. `mission-form-save`. 3. Assert new row after refetch. |
| **Expected** | POST create; invalidate+refetch; new mission visible; success banner. |
| **Traces to** | P7-13 AC 3, AC 10 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-16 |
| **Title** | Edit a mission; code immutable on edit |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. ≥1 mission. |
| **Steps** | 1. `mission-edit-{id}`; assert `mission-form-code` disabled. 2. Change target; save. 3. Assert PUT excludes code; row updates after refetch. |
| **Expected** | Edit prefilled, code locked, PUT updates, refetch reflects change. |
| **Traces to** | P7-13 AC 3 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-17 |
| **Title** | Mission activate/deactivate via PATCH + confirm |
| **Type** | functional / no-optimistic |
| **Priority** | P1 |
| **Preconditions** | Admin. ≥1 active + 1 inactive mission. |
| **Steps** | 1. Toggle active behind confirm; assert PATCH `.../active`; status flips after refetch. 2. Confirm no delete control exists. |
| **Expected** | PATCH not DELETE; confirm gated; no delete; refetch reflects state. |
| **Traces to** | P7-13 AC 3, AC 10 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-18 |
| **Title** | Mission form validation mirrors BE bounds |
| **Type** | validation |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. Submit empty → blocked. 2. target=0 / rewardXp=0 → rejected (must be >0). 3. Overlong code/iconKey/titleKey → capped. |
| **Expected** | Required fields + >0 numeric + length caps enforced before save. |
| **Traces to** | P7-13 AC 3 |

### Timed events (`/gamification/events`)

| Field | Value |
|---|---|
| **ID** | GAM-TC-19 |
| **Title** | Timed-event list renders with status derived (Scheduled/Active/Expired) |
| **Type** | functional |
| **Priority** | P0 |
| **Preconditions** | Admin. TimedEventSeeder catalog seeded (GET `api/admin/timed-events`). |
| **Steps** | 1. Navigate `/gamification/events`. 2. Assert `event-table` + rows; columns Name, Scope, Multiplier, Start, End, Status, Actions. 3. Cross-check derived status vs each row's `isActive`+window. |
| **Expected** | Status badge derived client-side: Expired if now>endUtc; Active if isActive & start≤now≤end; else Scheduled. Window times in `dir="ltr"`. |
| **Traces to** | P7-13 AC 4 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-20 |
| **Title** | Create a timed event; validation start<end + multiplier 1.0–5.0 |
| **Type** | functional / validation / boundary |
| **Priority** | P0 |
| **Preconditions** | Admin. |
| **Steps** | 1. `event-create-btn`; fill `event-form-code` (unique), `-name-en`, `-name-ar`, `-scope`, `-multiplier`, `-start`, `-end`. 2. Set start ≥ end → assert blocked. 3. Set multiplier 0.5 and 6 → assert rejected; 1.0 and 5.0 accepted. 4. Valid → `event-form-save`; assert new row after refetch. |
| **Expected** | start strictly before end enforced; multiplier clamped to [1,5]; create POSTs and list refetches (no trust in returned id). |
| **Traces to** | P7-13 AC 4, AC 10 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-21 |
| **Title** | Activate a Scheduled event via POST .../activate + confirm |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. A **Scheduled** event. |
| **Steps** | 1. On a Scheduled row click `event-activate-{id}`. 2. Confirm `event-activate-confirm-btn`. 3. Assert POST `.../activate` (no body); status transitions after refetch. |
| **Expected** | Activate action present only for Scheduled; POST activate; refetch updates status. |
| **Traces to** | P7-13 AC 4 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-22 |
| **Title** | Expire an Active event via POST .../expire + confirm |
| **Type** | functional |
| **Priority** | P1 |
| **Preconditions** | Admin. An **Active** event (create one with start in the past, end in future, then activate, or seed). |
| **Steps** | 1. On an Active row click `event-expire-{id}`. 2. Confirm `event-expire-confirm-btn`. 3. Assert POST `.../expire`; status → Expired after refetch. |
| **Expected** | Expire offered only for Active; POST expire; refetch reflects Expired. |
| **Traces to** | P7-13 AC 4 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-23 |
| **Title** | Expired event: edit disabled, no activate/expire actions |
| **Type** | negative |
| **Priority** | P1 |
| **Preconditions** | Admin. An **Expired** event. |
| **Steps** | 1. On an Expired row assert `event-edit-{id}` disabled and no activate/expire buttons. |
| **Expected** | Terminal/expired rows offer no state actions; edit disabled. |
| **Traces to** | P7-13 AC 4 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-24 |
| **Title** | Timed-event edit shows the description-gap notice |
| **Type** | functional |
| **Priority** | P2 |
| **Preconditions** | Admin. A non-expired event. |
| **Steps** | 1. `event-edit-{id}`. 2. Assert `event-form-edit-gap-notice` (descriptions not in list DTO) shown; `event-form-code` disabled. |
| **Expected** | Edit warns descriptions can't be prefilled; code locked. |
| **Traces to** | P7-13 AC 4 |

### Student overrides (from `/users/[id]`)

| Field | Value |
|---|---|
| **ID** | GAM-TC-25 |
| **Title** | Override entry points appear ONLY for Student accounts |
| **Type** | auth-of-state / negative |
| **Priority** | P0 |
| **Preconditions** | Admin. A Student user id and a Parent user id (seed a parent+child via the onboarding API). |
| **Steps** | 1. Open `/users/{studentId}`; assert `gamification-overrides-card` + `league-tier-override-btn` + `grant-streak-freeze-btn`. 2. Open `/users/{parentId}`; assert the card + buttons are **absent**. |
| **Expected** | Override affordances gated to `roles.includes('Student')`; never shown for parents. |
| **Traces to** | P7-13 AC 5, AC 6; no-teacher product rule |

| Field | Value |
|---|---|
| **ID** | GAM-TC-26 |
| **Title** | League-tier dialog gates confirm until new tier ≠ current AND reason present |
| **Type** | validation |
| **Priority** | P0 |
| **Preconditions** | Admin. A Student id. |
| **Steps** | 1. Click `league-tier-override-btn`; assert `league-tier-override-dialog`. 2. With no selection/reason assert `league-tier-confirm-btn` `aria-disabled="true"`. 3. Pick a tier in `league-tier-select` (current tier excluded from options). 4. Assert still disabled until reason typed. 5. Type reason → confirm enabled. |
| **Expected** | Confirm gated on tier+reason; current tier not selectable (no-op prevented). |
| **Traces to** | P7-13 AC 5, AC 7 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-27 |
| **Title** | League-tier override submits {newTier, reason, confirm:true} and succeeds |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Preconditions** | Admin. A Student id. |
| **Steps** | 1. Select tier (e.g. Gold=3), reason, confirm. 2. Assert POST `.../children/{id}/league-tier` body `{newTier:3, reason:"...", confirm:true}`. 3. Assert success banner; dialog closes. |
| **Expected** | Correct body incl. `confirm:true`; reason trimmed; success path; no admin id in body (actor from JWT). |
| **Traces to** | P7-13 AC 5, AC 7, AC 9 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-28 |
| **Title** | League-tier override error mapping (400/422/404/network) keeps dialog open |
| **Type** | negative |
| **Priority** | P1 |
| **Preconditions** | Admin. Intercept POST → each status. |
| **Steps** | 1. Confirm with POST forced 400 → assert `gamLeagueTierError400` inline; dialog open. 2. Repeat 422 / 404 / network → respective mapped message. |
| **Expected** | Mapped inline errors; dialog stays open for retry; envelope `Successed:false` surfaced. |
| **Traces to** | P7-13 AC 5 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-29 |
| **Title** | Streak-freeze dialog: count bounded 1–2, reason required, balance "not available" |
| **Type** | validation / boundary |
| **Priority** | P0 |
| **Preconditions** | Admin. A Student id. |
| **Steps** | 1. Click `grant-streak-freeze-btn`; assert `grant-streak-freeze-dialog`. 2. Assert `gamFreezeBalanceUnavailable` notice. 3. `freeze-count-input` min=1 max=2; set 0 and 3 → assert confirm disabled. 4. Set 2 + reason → `grant-freeze-confirm-btn` enabled. |
| **Expected** | Count clamped 1..2; reason required; balance shown as unavailable (no fake value); confirm gated. |
| **Traces to** | P7-13 AC 6, AC 7 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-30 |
| **Title** | Streak-freeze grant submits {count, reason, confirm:true} and succeeds |
| **Type** | functional / persistence |
| **Priority** | P0 |
| **Preconditions** | Admin. A Student id. |
| **Steps** | 1. count=1, reason, confirm. 2. Assert POST `.../children/{id}/streak-freeze` body `{count:1, reason, confirm:true}`. 3. Assert success banner; dialog closes. |
| **Expected** | Correct body incl. `confirm:true`; success path; no admin id in body. |
| **Traces to** | P7-13 AC 6, AC 7, AC 9 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-31 |
| **Title** | Streak-freeze error mapping keeps dialog open |
| **Type** | negative |
| **Priority** | P2 |
| **Preconditions** | Admin. Intercept POST → 400/422/404/network. |
| **Steps** | 1. Confirm with each forced status; assert respective `gamFreezeError*` inline; dialog open. |
| **Expected** | Mapped errors; dialog persists; no optimistic success. |
| **Traces to** | P7-13 AC 6 |

### Gamification cross-cutting

| Field | Value |
|---|---|
| **ID** | GAM-TC-32 |
| **Title** | Confirm dialogs trap focus, ESC cancels, backdrop inert (catalogs + overrides) |
| **Type** | a11y |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. For `badge-deactivate-dialog`, `event-activate-dialog`, `league-tier-override-dialog`: open, Tab-cycle within, click backdrop (no dismiss), press ESC (closes). |
| **Expected** | All confirm dialogs (role=dialog, aria-modal) trap focus, ESC-cancel, no backdrop-dismiss; confirm uses `aria-disabled` gating. |
| **Traces to** | P7-13 AC 8 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-33 |
| **Title** | Catalog tables semantic + aria-live / aria-busy |
| **Type** | a11y |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Assert each catalog has `caption.sr-only`, `th[scope=col]`, results region `aria-live` + `aria-busy` (true during refetch). |
| **Expected** | Semantic tables + live regions present across badge/mission/event pages. |
| **Traces to** | P7-13 AC 8 |

| Field | Value |
|---|---|
| **ID** | GAM-TC-34 |
| **Title** | `[BLOCKED-RTL]` Gamification copy + enum labels bilingual EN+AR; numbers/dates ltr |
| **Type** | RTL-i18n |
| **Priority** | P2 |
| **Preconditions** | `ADMIN_LOCALE='en'` build. |
| **Steps** | STATIC: verify tier/rarity/cadence/target/scope/status label maps + dialog/form strings have AR + EN; multipliers/dates/counts in `dir="ltr"`. |
| **Expected** | Bilingual maps + ltr numeric islands present in source. **BLOCKED** for live RTL drive. |
| **Traces to** | P7-13 AC 8 |

---

# Cross-cutting (all three surfaces)

| Field | Value |
|---|---|
| **ID** | XC-TC-01 |
| **Title** | No-PII-in-console: no studentId / verdict / email / token logged |
| **Type** | security |
| **Priority** | P1 |
| **Preconditions** | Admin. |
| **Steps** | 1. Capture `console` + network across moderation detail (with studentId), audit detail, and the two override dialogs. 2. Assert no child PII, safety-verdict raw text, email, or JWT is written to console/telemetry. |
| **Expected** | Console clean of PII/secrets; verdict raw fields never logged. |
| **Traces to** | P7-09/12/13 security handoffs |

| Field | Value |
|---|---|
| **ID** | XC-TC-02 |
| **Title** | Expired/invalid token mid-session → graceful 401 handling (no silent stale data) |
| **Type** | auth-authz / negative |
| **Priority** | P1 |
| **Preconditions** | Admin. Then clear/invalidate the session token while on a Wave-3 page. |
| **Steps** | 1. On `/moderation` (or `/audit`/`/gamification/badges`), invalidate the token, trigger a refetch (filter change / retry). 2. Assert a 401 path (redirect to login or surfaced error), not a silent render of stale admin data. |
| **Expected** | 401 handled gracefully; no leak of a prior admin's data after token loss. |
| **Traces to** | P7-13 AC 1 (guard); security |

| Field | Value |
|---|---|
| **ID** | XC-TC-03 |
| **Title** | No teacher role surfaces anywhere in Wave-3 affordances |
| **Type** | negative / product-override |
| **Priority** | P2 |
| **Preconditions** | Admin. |
| **Steps** | 1. Scan moderation subject filter, audit target/action dropdowns, and user-detail override gating for any "Teacher" option/role. |
| **Expected** | No teacher role anywhere; overrides gated to Student only. |
| **Traces to** | Product override (no teacher role) |

---

# Backend contract-smoke (PRE-EXISTING — reference only, do NOT re-implement here)

> The BE shipped + was integration-tested with `api-tester`. Listed only so the e2e tester knows the contracts the UI rides on. If a UI case fails, use these to localize FE-vs-BE.

- BE-SMOKE-01 — `GET/POST api/Admin/Moderation/*` are `[Authorize(AdminOnly)]`; 401 without JWT.
- BE-SMOKE-02 — `POST .../Review` rejects re-review of terminal items (400) and re-flag of Flagged (400); validator requires reason on Rejected.
- BE-SMOKE-03 — `GET api/Admin/Audit/Log` is read-only AdminOnly; envelope `Successed`; newest-first; pagination clamps 1–100.
- BE-SMOKE-04 — `api/Admin/Gamification/*` + `api/admin/timed-events` AdminOnly; PATCH active (not DELETE); league-tier/streak-freeze validators require `confirm:true`, reason, bounded count/tier.
