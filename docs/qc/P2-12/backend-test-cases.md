# P2-12 — Backend Test Cases (for `api-tester`)

**Scope:** Notification preferences (`GET/PUT /api/Notifications/Preferences`) + linked-children settings management (`DELETE /api/Parent/Unlink-Child`, plus list/link read context). Backend HTTP only.

**Endpoints under test (confirmed from source):**

| Method | Route | Controller | Identity source |
|---|---|---|---|
| GET | `/api/Notifications/Preferences` | `PreferencesController.GetMyPreferences` (`[Authorize]`, all roles) | JWT (`ICurrentUserService`) |
| PUT | `/api/Notifications/Preferences` | `PreferencesController.UpdateMyPreferences` (`[Authorize]`) | JWT |
| DELETE | `/api/Parent/Unlink-Child` | `ParentController.UnlinkChild` (`[Authorize(Roles="Parent,Admin,SuperAdmin")]`) | JWT |
| GET | `/api/Parent/My-Children` | `ParentController.MyChildren` (cross-ref read context) | JWT |
| POST | `/api/Parent/Link-Child` | `ParentController.LinkChild` (cross-ref: already-linked 409) | JWT |

**Conventions to assert on every case:**
- Response body is the `BaseResponse<T>` envelope; success flag spelled **`Successed`** (camelCase `successed` on the wire).
- Commands (`UpdateMyNotificationPreferencesCommand`, `UnlinkChildCommand`, `LinkChildCommand`) run `ValidationBehavior` → validation failures surface as **HTTP 422** (`UnprocessableEntity`, `Message: "Validation Failed"`, `Errors[]` populated). Queries (`GET` preferences, My-Children) are **not** auto-validated.
- Status-code mapping comes from the handler's `BaseResponse.StatusCode`: `Success`→200, `BadRequest`→400, `NotFound`→404, `Conflict`→409, `Unauthorized`→401.

**Notification categories (self endpoint):** the 4 user-facing categories are `WeeklyReport(0)`, `StreakAtRisk(1)`, `ProductAnnouncement(2)`, `Achievement(3)`. Categories 4/5/6 (`DailyMissionReminder`, `LapseWinBack`, `System`) are NOT surfaced by the self GET and `Enum.IsDefined` accepts them but the GET allow-list excludes them.

**Seed entities (name these in fixtures):**
- **Parent A** (Parent role) — linked to **Child A1** (sole parent) and co-parents **Child Shared** with Parent B.
- **Parent B** (Parent role) — linked to **Child B1** (sole parent) and co-parents **Child Shared**.
- **Child Shared** — linked to BOTH Parent A and Parent B (for last-parent guard pass-through).
- **Child A1** — linked ONLY to Parent A (for last-parent block).
- **Child Orphan-Candidate** — a Student-role user not linked to anyone (for not-linked path), or reuse Child B1 from Parent A's perspective.

---

## Surface 1 — Notification preferences: `GET /api/Notifications/Preferences`

### BE-TC-01 — GET returns all 4 user-facing categories in the envelope
- **Type:** functional
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated (valid JWT). No preference rows saved yet for Parent A.
- **Steps:**
  1. `GET /api/Notifications/Preferences` with Parent A's bearer token.
- **Expected result:** HTTP 200. `successed: true`. `data.preferences` is an array of exactly **4** items, one per category `WeeklyReport(0)`, `StreakAtRisk(1)`, `ProductAnnouncement(2)`, `Achievement(3)`. Each item has `category`, `emailEnabled`, `pushEnabled` (booleans).
- **Traces to:** P2-12a — "endpoints to read notification preferences"; brief BE-1 GET.

### BE-TC-02 — First GET (no rows) returns documented defaults, not 404
- **Type:** functional / state (empty)
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** A brand-new Parent (Parent-New) with zero preference rows.
- **Steps:**
  1. `GET /api/Notifications/Preferences` as Parent-New.
- **Expected result:** HTTP 200 (NOT 404). Defaults: `WeeklyReport.emailEnabled = true`; all other `emailEnabled = false`; every `pushEnabled = false`. (See README open-Q2 — confirm exact defaults with product; structure assertion holds regardless.)
- **Traces to:** P2-12a — "first read for a user with no row returns sensible defaults (not 404)".

### BE-TC-03 — GET is side-effect-free (defaults are NOT persisted on read)
- **Type:** persistence / negative
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** Parent-New with zero preference rows.
- **Steps:**
  1. `GET /api/Notifications/Preferences` as Parent-New (returns synthesised defaults).
  2. Inspect persistence for Parent-New's rows (direct DB read of `notifications."NotificationPreferences"` for that UserId, or a follow-up state probe).
- **Expected result:** Zero rows persisted for Parent-New after the GET — defaults were synthesised in-memory only.
- **Traces to:** Handler contract "no 404, nothing persisted on read"; risk note 3.

### BE-TC-04 — GET anonymous → 401
- **Type:** auth
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** none (no Authorization header).
- **Steps:**
  1. `GET /api/Notifications/Preferences` with no bearer token.
- **Expected result:** HTTP 401 Unauthorized (framework `[Authorize]` rejection; no envelope body required).
- **Traces to:** Brief BE-1 "[Authorize] self-scope".

### BE-TC-05 — GET never surfaces re-engagement/system categories (4/5/6)
- **Type:** negative / boundary
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** A user who somehow has rows for category 4/5/6 persisted (seed directly, or a parent who used the re-engagement endpoint). If not seedable, assert from BE-TC-01 that only categories 0–3 appear.
- **Steps:**
  1. `GET /api/Notifications/Preferences` as that user.
- **Expected result:** `data.preferences` contains ONLY categories 0–3. No item with `category` 4, 5, or 6 appears (allow-list guard).
- **Traces to:** Handler P4-09 guard; README coverage "re-engagement categories not surfaced".

---

## Surface 2 — Notification preferences: `PUT /api/Notifications/Preferences`

### BE-TC-06 — PUT all 4 categories returns success envelope
- **Type:** functional
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` body: `{ "preferences": [ {category:0, emailEnabled:false, pushEnabled:true}, {category:1, emailEnabled:true, pushEnabled:true}, {category:2, emailEnabled:false, pushEnabled:false}, {category:3, emailEnabled:true, pushEnabled:false} ] }`.
- **Expected result:** HTTP 200, `successed: true`, `data` is the success message string (envelope `BaseResponse<string>`).
- **Traces to:** P2-12a — "endpoints to update notification preferences"; brief BE-1 PUT.

### BE-TC-07 — PUT then GET round-trips (persisted per user)
- **Type:** persistence
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. `PUT` the body from BE-TC-06.
  2. `GET /api/Notifications/Preferences` as Parent A.
- **Expected result:** GET returns exactly the values written in step 1 for all 4 categories (`category:0` email=false push=true, `category:1` email=true push=true, etc.). Confirms upsert persisted per user.
- **Traces to:** P2-12a — "persisted per user"; risk note 4.

### BE-TC-08 — PUT a subset upserts only those categories (does not wipe others)
- **Type:** persistence / boundary
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A has all 4 categories saved (run BE-TC-06 first).
- **Steps:**
  1. `PUT` body with ONLY `{category:0, emailEnabled:true, pushEnabled:false}`.
  2. `GET` as Parent A.
- **Expected result:** `category:0` reflects the new values; `category:1/2/3` retain their previously-saved values (not reset to default). Implements partial-upsert. (See README open-Q3 — if product requires full-replace this expectation flips and a defect is filed.)
- **Traces to:** Upsert handler semantics; risk note 4; open question 3.

### BE-TC-09 — PUT empty preferences list → 422
- **Type:** validation
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. `PUT` body: `{ "preferences": [] }`.
- **Expected result:** HTTP **422** (`ValidationBehavior` → `NotEmpty` fails). `Message: "Validation Failed"`, `errors[]` populated, `successed: false`. No rows changed.
- **Traces to:** Validator `RuleFor(x=>x.Preferences).NotEmpty()`; CONVENTIONS §4 (commands validated).

### BE-TC-10 — PUT unknown/undefined category → 422
- **Type:** validation / negative
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. `PUT` body with an item `{category: 99, emailEnabled:true, pushEnabled:true}` (not a defined `NotificationCategory`).
- **Expected result:** HTTP **422**. `errors[]` contains the "invalid category" message. No rows changed.
- **Traces to:** Validator `Enum.IsDefined` rule (`NotificationPreferenceInvalidCategory`).

### BE-TC-11 — PUT duplicate category in one request → 422
- **Type:** validation / negative
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. `PUT` body with TWO items both `category:0` (e.g. one email-on, one email-off).
- **Expected result:** HTTP **422**. `errors[]` contains the "duplicate category" message. No upsert occurs (the validator blocks the double-write of one row).
- **Traces to:** Validator distinct-count rule (`NotificationPreferenceDuplicateCategory`); risk note 4.

### BE-TC-12 — PUT a re-engagement/system category (4/5/6) is accepted by validator but not surfaced by GET
- **Type:** boundary / negative
- **Priority:** P2
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. `PUT` body with `{category:4, emailEnabled:true, pushEnabled:true}` (defined enum value → passes `Enum.IsDefined`).
  2. `GET` as Parent A.
- **Expected result:** PUT returns 200 (validator accepts defined enum value 4). GET still returns ONLY categories 0–3 — category 4 is written but the self GET allow-list excludes it. **Document as a known asymmetry** (PUT accepts 4–6, GET hides them); raise to lead if product wants PUT to reject 4–6 on the self endpoint.
- **Traces to:** Validator vs GET allow-list mismatch; README risk; potential defect note.

### BE-TC-13 — PUT anonymous → 401
- **Type:** auth
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** none.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with a valid body but no bearer token.
- **Expected result:** HTTP 401 Unauthorized. No rows changed.
- **Traces to:** `[Authorize]` on PUT.

### BE-TC-14 — PUT/GET are self-scoped — Parent B's prefs never affected by Parent A (IDOR)
- **Type:** auth-authz (IDOR)
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A and Parent B both authenticated; Parent B has a known saved preference set.
- **Steps:**
  1. As Parent A, `PUT` a distinct full set (all email-on).
  2. As Parent B, `GET /api/Notifications/Preferences`.
  3. (Body-injection probe) As Parent A, `PUT` a body that includes any extra field attempting to set another user's id (e.g. `userId`, `parentId`) — the command has no such field; assert it is ignored.
- **Expected result:** Parent B's GET in step 2 is **unchanged** by Parent A's write. In step 3, any injected identity field is ignored (UserId resolved from JWT only); only Parent A's rows change. No cross-user leakage.
- **Traces to:** Brief BE-1 "self-scope (id from JWT, never body)"; risk note 1.

---

## Surface 3 — Linked children: `DELETE /api/Parent/Unlink-Child`

### BE-TC-15 — Unlink a co-parented child succeeds (happy path)
- **Type:** functional / persistence
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A linked to **Child Shared** (which also has Parent B → unlinking A does NOT leave it parentless).
- **Steps:**
  1. As Parent A, `DELETE /api/Parent/Unlink-Child` body `{ "childId": <ChildShared.Id> }`.
  2. As Parent A, `GET /api/Parent/My-Children`.
- **Expected result:** Step 1 → HTTP 200, `successed: true`, `data: true`. Step 2 → Child Shared no longer appears in Parent A's list. The `ParentStudent` (A, ChildShared) row is removed; Parent B's link survives.
- **Traces to:** P2-12b — "unlink actions"; brief BE-2 unlink.

### BE-TC-16 — Last-parent guard: unlink the only parent → 400
- **Type:** negative / boundary (business rule)
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A is the **sole** parent of **Child A1**.
- **Steps:**
  1. As Parent A, `DELETE /api/Parent/Unlink-Child` body `{ "childId": <ChildA1.Id> }`.
- **Expected result:** HTTP **400** BadRequest, `successed: false`, `data: false`, message = "cannot unlink last parent" (`CannotUnlinkLastParent`). The link is **NOT** removed — verify Child A1 still appears in Parent A's My-Children.
- **Traces to:** Run brief "last-parent block (can't unlink the only parent) → 400"; risk note 2.

### BE-TC-17 — Unlink a child that exists but is not linked to caller → generic 404
- **Type:** auth-authz / negative (anti-enumeration)
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** **Child B1** exists and is linked only to Parent B; Parent A authenticated.
- **Steps:**
  1. As Parent A, `DELETE /api/Parent/Unlink-Child` body `{ "childId": <ChildB1.Id> }`.
- **Expected result:** HTTP **404** NotFound, `successed: false`, generic message (`CannotEditChildNotInFamily`). Parent B's link to Child B1 is untouched.
- **Traces to:** Run brief "unlink-not-linked (404)" + IDOR family scope; risk note 5.

### BE-TC-18 — Unlink a non-existent childId returns the SAME 404 shape (no enumeration leak)
- **Type:** auth-authz / negative (anti-enumeration)
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated; choose a `childId` that does not exist (e.g. 999999).
- **Steps:**
  1. As Parent A, `DELETE /api/Parent/Unlink-Child` body `{ "childId": 999999 }`.
- **Expected result:** HTTP **404** with the **identical** body/message shape as BE-TC-17 — a caller cannot distinguish "child exists but not mine" from "child doesn't exist".
- **Traces to:** Anti-enumeration contract; risk note 5; IDOR.

### BE-TC-19 — Unlink ignores any body-injected parent identity; concurrent last-parent stays atomic
- **Type:** auth-authz (IDOR) / boundary (TOCTOU)
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** Child Shared linked to Parent A and Parent B (exactly two parents).
- **Steps:**
  1. As Parent A, `DELETE /api/Parent/Unlink-Child` with a body that injects an extra `parentId` field pointing at Parent B (the command has no `ParentId` — assert it's ignored; only Parent A's own link is targeted).
  2. (Concurrency) Fire Parent A's unlink and Parent B's unlink of Child Shared **concurrently**.
- **Expected result:** Step 1 → only the (A, ChildShared) link is removed; Parent B's link untouched (acting parent from JWT, not body). Step 2 → exactly **one** unlink succeeds; the second is **blocked with 400** (last-parent guard inside the REPEATABLE READ transaction) — Child Shared always retains ≥1 parent. Never both succeed.
- **Traces to:** Brief "acting parent resolved from JWT; no ParentId in body"; security-auditor TOCTOU note; risk note 2.

### BE-TC-20 — Unlink anonymous → 401
- **Type:** auth
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** none.
- **Steps:**
  1. `DELETE /api/Parent/Unlink-Child` body `{ "childId": 1 }` with no bearer token.
- **Expected result:** HTTP 401 Unauthorized (controller `[Authorize(Roles=...)]`). No link removed.
- **Traces to:** `[Authorize]` on ParentController.

### BE-TC-21 — Unlink with ChildId <= 0 → 422
- **Type:** validation / boundary
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A authenticated.
- **Steps:**
  1. As Parent A, `DELETE /api/Parent/Unlink-Child` body `{ "childId": 0 }`.
  2. (Repeat) body `{ "childId": -5 }`.
- **Expected result:** Both → HTTP **422** (`UnlinkChildCommandValidator` `GreaterThan(0)`). `Message: "Validation Failed"`, `errors[]` populated. No DB mutation.
- **Traces to:** `UnlinkChildCommandValidator`; CONVENTIONS §4.

---

## Surface 4 — Linked children read/link context (cross-reference confirmations)

> These three confirm the settings-tab read/link behaviour P2-12 surfaces. The full link-by-email matrix is owned by **P1-04** — see README §3. Reuse P1-04 fixtures; do not re-derive the enumeration matrix.

### BE-TC-22 — My-Children lists only the caller's own linked children
- **Type:** auth-authz (family scope)
- **Priority:** P0
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A linked to {Child A1, Child Shared}; Parent B linked to {Child B1, Child Shared}.
- **Steps:**
  1. As Parent A, `GET /api/Parent/My-Children`.
  2. As Parent B, `GET /api/Parent/My-Children`.
- **Expected result:** Step 1 returns exactly {Child A1, Child Shared}; Step 2 returns exactly {Child B1, Child Shared}. No parent sees the other's exclusive child. Envelope `BaseResponse<IEnumerable<LinkedChildResponse>>`, `successed: true`.
- **Traces to:** P2-12b — "list linked children with family-scope authz"; cross-ref P1-04 `ListMyChildrenQuery`.

### BE-TC-23 — My-Children for a parent with no children → empty success (not 404)
- **Type:** state (empty)
- **Priority:** P2
- **Target agent:** api-tester
- **Preconditions / seed:** Parent-New with zero links.
- **Steps:**
  1. As Parent-New, `GET /api/Parent/My-Children`.
- **Expected result:** HTTP 200, `successed: true`, `data: []` (empty array). Not 404.
- **Traces to:** `ListMyChildrenQueryHandler` empty-list branch.

### BE-TC-24 — Link an already-linked child → 409 (recently fixed, settings re-link flow)
- **Type:** negative / regression (BUG-P104-02)
- **Priority:** P1
- **Target agent:** api-tester
- **Preconditions / seed:** Parent A already linked to Child A1; Child A1's email known.
- **Steps:**
  1. As Parent A, `POST /api/Parent/Link-Child` body `{ "childEmail": "<ChildA1.email>" }`.
- **Expected result:** HTTP **409** Conflict, `successed: false`, message = "child already linked" (`ChildAlreadyLinked`). No duplicate link row created.
- **Traces to:** Run brief "Link-Child already-linked → 409"; cross-ref P1-04 / BUG-P104-02 fix. (Full link enumeration-defence matrix remains in P1-04's suite.)
