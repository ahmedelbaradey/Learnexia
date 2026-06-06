# Backend Test Cases — P1-04 (Link parent to child)

> Target agent: **`api-tester`**. Implement/reconcile against the running API
> (`WebApplicationFactory` + seed). Mirror `backend/tests/Learnexia.IntegrationTests/P1_04_LinkParentChild_Tests.cs`
> and `P1_01_RegisterParent_Tests.cs` (dual camelCase/PascalCase JSON path assertions via a `TryProp` helper).
> Update the existing `P1_04_LinkParentChild_Tests.cs` **in place** (do not fork a parallel file) — see README Q2.

## Surface under test (actual shipped reality)

- Controller: `Learnexia.Modules.Parent.Api.Controllers.ParentController`, `[Route("api/Parent")]`,
  class-level `[Authorize(Roles = "Parent,Admin,SuperAdmin")]`.
- `POST api/Parent/Link-Child` — body `{ "ChildEmail": "<email>" }`. No `ParentId` (JWT-only).
- `GET  api/Parent/My-Children` — no params; keyed on the JWT caller's id.
- `DELETE api/Parent/Unlink-Child` — body `{ "ChildId": <int> }`. No `ParentId` (JWT-only).
- Envelope: `BaseResponse<T>` → `{ statusCode, succeeded?/successed, message, errors[], data }`. Success flag is
  spelled **`Successed`** (assert case-insensitively to tolerate camelCase serialization).

## Actual status-code mapping (verified against handlers — assert exactly)

| Outcome | Handler call | HTTP | `Successed` |
|---|---|---|---|
| Link success / idempotent re-link | `Success(...)` | **200** | true |
| Link: non-existent email / non-student / self-link / cross-family claim | `BadRequest(...)` (generic msg) | **400** | false |
| Link/Unlink/MyChildren: no `UserId` in JWT | `Unauthorized(...)` | 401 envelope — but framework `[Authorize]` returns real **401** first for missing token | false |
| Validation failure (empty/malformed email) | `ValidationBehavior` → middleware | **422** + `errors[]` | false |
| MyChildren success (incl. empty) | `Success(...)` | **200** | true |
| Unlink success | `Success(true)` | **200** | true |
| Unlink: caller not linked to child | `NotFound(...)` (generic msg) | **404** | false |
| Unlink: would orphan child (last parent) | `BadRequest(...)` | **400** | false |
| Non-parent role token (Basic/Student) | framework role gate | **403** | n/a |
| Missing token | framework | **401** | n/a |

## Standard seed helpers (reuse from existing file)
- Register parent → JWT: `POST api/Users/Authentication/Register-Parent { Email, Password, AcceptedTerms=true }`.
- Create student (admin-gated): `POST api/Users/UserManagement/AddUser { Email, UserName, FullName, Roles:["Student"] }` with SuperAdmin token (seeded `superadmin` / `123Pa$$word!`).
- Seeded non-parent: `basicuser` / `123Pa$$word!` (Basic role).
- Unique emails per test to guarantee isolation.

---

## Group A — Link-Child happy path & many-to-many (AC-2, AC-4)

### BE-TC-02 — Link an existing unlinked student (happy path) `(existing)`
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P (JWT). Student S created via admin (no parent yet).
- **Steps:** 1) `POST api/Parent/Link-Child` as P, body `{ ChildEmail: S.email }`.
- **Expected:** **200**; `Successed=true`; `data.id > 0`; `data.fullName` non-blank; `data.email == S.email`.
- **Traces to:** AC-2.

### BE-TC-03 — Linked child summary fields are populated
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** As BE-TC-02.
- **Steps:** 1) Link S to P. 2) Inspect `data`.
- **Expected:** **200**; `data` contains `id`, `fullName`, `email`, `learningLanguage`, `grade` (may be null), `language` ∈ {`ar`,`en`}, `country`. `language` is the short code, never `en-US`/`ar-EG`.
- **Traces to:** AC-2 (contract for FE).

### BE-TC-04 — Parent linked to two students (M:N, parent side) `(existing)`
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P; students S1, S2 (admin-created, unlinked).
- **Steps:** 1) Link S1 to P → 200. 2) Link S2 to P → 200. 3) `GET api/Parent/My-Children` as P.
- **Expected:** Both links 200; `My-Children` `data` length **== 2**; both emails present.
- **Traces to:** AC-4.

### BE-TC-16 — Child linked by two parents (M:N, child side)
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** Parents A and B; one student S created **by parent A** (so A is `CreatedByParentId`). For B to also link, S must be in a state B is allowed to claim — but the cross-family guard blocks B once S has any parent. **This case validates the DATA MODEL supports M:N, not the link endpoint's policy.** Drive it where the product allows two parents (e.g. both parents created via the same family flow), OR assert at the DB level that the composite PK `(ParentId, StudentId)` permits two rows with the same `StudentId`.
- **Steps:** 1) Establish two `(ParentId, StudentId)` rows for the same `StudentId` via a supported path (e.g. add-child by A, then a sanctioned second-parent link if one exists; otherwise direct seed). 2) Each parent calls `My-Children`.
- **Expected:** Each parent sees S in their own list; the schema holds 2 rows for the same child.
- **Notes:** If no product path exists for a second parent to self-claim (cross-family guard blocks it), mark this **partially blocked** — assert the *schema* supports M:N (composite PK on `(ParentId, StudentId)`, index on `StudentId`) and note the only sanctioned multi-parent path is add-child, deferred to a future story. Trace to AC-4 + open question.
- **Traces to:** AC-4.

### BE-TC-17 — My-Children returns exactly the caller's linked children, not more
- **Type:** functional/persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P links S1, S2. A different parent Q links S3.
- **Steps:** 1) `GET api/Parent/My-Children` as P.
- **Expected:** **200**; exactly {S1, S2}; S3 **absent**.
- **Traces to:** AC-3, AC-4.

---

## Group B — Link-Child negative / fail-closed / anti-enumeration (AC-5, AC-7)

> All four rejections below MUST return the **same 400 + same generic message + same shape**.

### BE-TC-05 — Link to a non-existent email → 400 generic, no leak `(existing)`
- **Type:** negative/auth · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P. A guaranteed-nonexistent email.
- **Steps:** 1) `POST Link-Child` as P with the ghost email.
- **Expected:** **400**; `Successed=false`; body does **not** contain `"not found"` / `"does not exist"` / the email's existence status.
- **Traces to:** AC-5.

### BE-TC-06 — Link to a non-student user (Admin email) → 400 generic `(existing)`
- **Type:** negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P. Target = seeded admin email (`superadmin@gmail.com`).
- **Steps:** 1) `POST Link-Child` as P with the admin email.
- **Expected:** **400**; `Successed=false`. Same message/shape as BE-TC-05.
- **Traces to:** AC-5.

### BE-TC-07 — Self-link (parent links own email) → 400 generic `(existing)`
- **Type:** negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P.
- **Steps:** 1) `POST Link-Child` as P with P's own email.
- **Expected:** **400**; `Successed=false`. Same generic message/shape.
- **Traces to:** AC-5, AC-7.

### BE-TC-08 — Anti-enumeration: all four rejections share status + shape `(existing, extend)`
- **Type:** auth/negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P; an admin email; a ghost email; a student already linked to another parent (set up via BE-TC-11).
- **Steps:** Call Link-Child for: ghost email, admin email, self email, cross-family student email.
- **Expected:** **All four return the same HTTP status (400)** and the **same `message`** (`CannotLinkChild`); none reveals which class of failure occurred (no "not found", "already linked", "not a student", ownership info).
- **Traces to:** AC-5, AC-7 (enumeration resistance).

### BE-TC-11 — Cross-family IDOR: parent B cannot claim parent A's child → 400 `(existing)`
- **Type:** auth-authz/negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent A; student S (unlinked). Parent B.
- **Steps:** 1) A links S → 200. 2) B `POST Link-Child` with S.email.
- **Expected:** B gets **400**; `Successed=false`; body does **not** contain `"parent A"`, `"already linked"`, or any ownership disclosure.
- **Traces to:** AC-7.

### BE-TC-19 — Cross-family: B's My-Children unchanged after a failed claim
- **Type:** persistence/authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** As BE-TC-11.
- **Steps:** 1) A links S. 2) B attempts to claim S (fails 400). 3) `GET My-Children` as B.
- **Expected:** **200**; B's `data` length **== 0** (the failed claim created no row).
- **Traces to:** AC-7, AC-3.

---

## Group C — Idempotency (AC-6)

### BE-TC-09 — Re-link same child is idempotent → 200, no duplicate `(existing)`
- **Type:** functional/boundary · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P; student S (unlinked).
- **Steps:** 1) Link S to P → 200. 2) Link S to P **again**.
- **Expected:** Second call **200**; `Successed=true`; child summary returned (no error).
- **Traces to:** AC-6.

### BE-TC-10 — Idempotency confirmed via My-Children count `(existing)`
- **Type:** persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** As BE-TC-09.
- **Steps:** 1) Link S twice. 2) `GET My-Children`.
- **Expected:** **200**; `data` length **== 1** (composite PK `(ParentId, StudentId)` prevents the duplicate).
- **Traces to:** AC-6, AC-4 (PK).

---

## Group D — Family-scoped read isolation (AC-3)

### BE-TC-12 — My-Children isolation: parent B sees empty when A has a child `(existing)`
- **Type:** auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent A links student S; Parent B has none.
- **Steps:** 1) `GET My-Children` as B.
- **Expected:** **200**; `Successed=true`; `data` length **== 0**.
- **Traces to:** AC-3.

### BE-TC-13 — My-Children empty for a fresh parent → 200 empty array `(existing)`
- **Type:** state(empty) · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** Fresh parent, no links.
- **Steps:** 1) `GET My-Children`.
- **Expected:** **200**; `Successed=true`; `data` is `[]`.
- **Traces to:** AC-3.

### BE-TC-14 — Persistence: linked child is retrievable via My-Children `(existing)`
- **Type:** persistence · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P; student S.
- **Steps:** 1) Link S → 200. 2) `GET My-Children`.
- **Expected:** **200**; `data` length 1; `data[0].email == S.email`.
- **Traces to:** AC-2, AC-3.

### BE-TC-15 — My-Children does not leak other-family children even with shared student emails
- **Type:** auth-authz/boundary · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** Parent A links S_A; Parent B links S_B (distinct students). 
- **Steps:** 1) `GET My-Children` as A. 2) `GET My-Children` as B.
- **Expected:** A sees only S_A; B sees only S_B; neither list contains the other's student.
- **Traces to:** AC-3.

---

## Group E — Auth / authz gate matrix (AC-2/AC-7 + product overrides)

### BE-TC-18 — Unauthenticated Link-Child → 401 `(existing)`
- **Type:** auth · **Priority:** P0 · **Agent:** api-tester
- **Steps:** `POST Link-Child` with **no** bearer token.
- **Expected:** **401**.
- **Traces to:** AC-2, AC-7.

### BE-TC-18b — Unauthenticated My-Children → 401 `(existing)`
- **Type:** auth · **Priority:** P0 · **Agent:** api-tester
- **Steps:** `GET My-Children` with no token.
- **Expected:** **401**.
- **Traces to:** AC-3.

### BE-TC-20 — Validation: empty/null ChildEmail → 422 with errors[] `(existing)`
- **Type:** validation · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent P (JWT).
- **Steps:** `POST Link-Child` body `{ ChildEmail: "" }`.
- **Expected:** **422**; `Successed=false`; `errors[]` length > 0. (Validation runs because `LinkChildCommand` is `ICommand<>`.)
- **Traces to:** AC-5 (input guard), envelope.

### BE-TC-21 — Validation: malformed ChildEmail → 422 with errors[] `(existing)`
- **Type:** validation · **Priority:** P1 · **Agent:** api-tester
- **Steps:** `POST Link-Child` body `{ ChildEmail: "not-an-email" }`.
- **Expected:** **422**; `errors[]` non-empty.
- **Traces to:** AC-5, envelope.

### BE-TC-22 — Non-parent role (Basic) → 403 on Link-Child `(existing)`
- **Type:** auth-authz / product-override · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** `basicuser` token (Basic role — not Parent/Admin/SuperAdmin).
- **Steps:** `POST Link-Child` with the Basic token.
- **Expected:** **403** (gate blocks). Asserts only Parent/Admin/SuperAdmin may link — and that there is **no teacher role** path.
- **Traces to:** AC-7, product override (no teacher role).

### BE-TC-22b — HasChildren routing signal flips true after linking
- **Type:** functional/state · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** Parent P; student S.
- **Steps:** 1) `GET api/Users/Authentication/Me` as P → assert `data.hasChildren == false`. 2) Link S to P. 3) `GET Me` again.
- **Expected:** After link, `data.hasChildren == true`. (This is the only login-routing input P1-04 produces — see README Q6; full role routing is P1-09.)
- **Traces to:** AC-1/AC-2 (downstream routing signal).

### BE-TC-23 — Admin role permitted to call Link-Child (gate level) `(new)`
- **Type:** auth-authz · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** SuperAdmin token; a student S.
- **Steps:** `POST Link-Child` as SuperAdmin with S.email.
- **Expected:** **Not 403** (gate permits Admin/SuperAdmin). Status is 200 or 400 depending on guard, but never 403/401. Note: semantic correctness of admin-self-linking is a product question (README Q4) — assert the **gate**, not the semantics.
- **Traces to:** controller gate; README Q4.

### BE-TC-24 — SuperAdmin My-Children → 200 empty (gate permits, no data) `(existing)`
- **Type:** auth-authz/state · **Priority:** P2 · **Agent:** api-tester
- **Steps:** `GET My-Children` as SuperAdmin.
- **Expected:** **200**; `Successed=true`; empty `data` (admin has no `ParentStudent` rows).
- **Traces to:** AC-3, README Q5.

### BE-TC-26 — Body ParentId override is ignored; acting parent is JWT-only `(existing)`
- **Type:** auth-authz/negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent A links S. Parent B.
- **Steps:** 1) As B, `POST Link-Child` body `{ ChildEmail: S.email, ParentId: 999999 }`.
- **Expected:** **400** (B cannot claim A's child); the extra `ParentId` is silently ignored by model binding. 2) `GET My-Children` as B → 200, empty (no row created for B).
- **Traces to:** AC-7 (IDOR — no body-driven identity).

---

## Group F — Unlink family-scope / IDOR / last-parent (P2-12; family-scope regression) `[Unlink/P2-12]`

> Tagged for easy removal if the lead scopes Unlink to a P2-12 QC folder (README Q3).

### BE-TC-25 — Unlink a child NOT linked to caller → 404 generic `[Unlink/P2-12]`
- **Type:** auth-authz/negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** Parent A links student S. Parent B (not linked to S).
- **Steps:** As B, `DELETE api/Parent/Unlink-Child` body `{ ChildId: S.id }`.
- **Expected:** **404**; `Successed=false`; generic message (`CannotEditChildNotInFamily`); body does not disclose S exists or belongs to A. A's link to S is **unaffected** (verify A still sees S in My-Children).
- **Traces to:** AC-3/AC-7 (family scope on the mutating path).

### BE-TC-27 — Unlink blocked when caller is the last parent → 400 `[Unlink/P2-12]`
- **Type:** boundary/negative · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** Parent A is the ONLY parent linked to student S.
- **Steps:** As A, `DELETE Unlink-Child` body `{ ChildId: S.id }`.
- **Expected:** **400**; `Successed=false`; message indicates last-parent block (`CannotUnlinkLastParent`); the link is **still present** (A still sees S in My-Children). Asserts a minor is never orphaned.
- **Traces to:** last-parent invariant (P2-12).

### BE-TC-28 — Concurrent unlink does not orphan the child (TOCTOU) `[Unlink/P2-12]`
- **Type:** concurrency/boundary · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** Student S linked to exactly two parents A and B.
- **Steps:** Fire `DELETE Unlink-Child` for S concurrently from A and B (two near-simultaneous requests).
- **Expected:** **At most one** unlink succeeds (200); the other is blocked **400** (last-parent). S always retains ≥1 parent. (Driven by `pg_advisory_xact_lock`.) **Note:** hard to make deterministic over HTTP; if not reliably reproducible, downgrade to a documented manual/known-limitation note rather than a flaky assertion.
- **Traces to:** last-parent invariant under concurrency.

### BE-TC-25b — Unlink validation: missing/zero ChildId → 422 `[Unlink/P2-12]`
- **Type:** validation · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** Parent token.
- **Steps:** `DELETE Unlink-Child` body `{ }` or `{ ChildId: 0 }`.
- **Expected:** **422** (if `UnlinkChildCommandValidator` requires `ChildId > 0`) with `errors[]`; otherwise the handler returns 404. Verify against `UnlinkChildCommandValidator` and assert the actual contract; do not assume.
- **Traces to:** input guard.

---

## Group G — Envelope shape & status-code precision

### BE-TC-29 — Success envelope has all BaseResponse keys + statusCode 200 `(existing)`
- **Type:** functional/contract · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Successful Link-Child; inspect root.
- **Expected:** root has `statusCode` (==200), `successed` (==true), `message`, `data` (not null). Success flag spelled `Successed`.
- **Traces to:** envelope convention.

### BE-TC-30 — 422 envelope has statusCode/successed/message/errors `(existing)`
- **Type:** contract · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Trigger a 422 (empty email); inspect root.
- **Expected:** root has `statusCode`, `successed` (==false), `message`, `errors` (array, non-empty).
- **Traces to:** envelope convention.

### BE-TC-31 — Link rejection is exactly 400 (not 404, not 422) `(new — status precision)`
- **Type:** contract/negative · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Link a ghost email (passes validation, fails the guard).
- **Expected:** HTTP **400** precisely; `statusCode` in envelope **== 400**. Distinguishes guard rejection (400) from validation (422) and from unlink-not-linked (404).
- **Traces to:** README status mapping; AC-5.

### BE-TC-32 — All Link-Child failures carry an identical generic message `(new — anti-enumeration)`
- **Type:** security/contract · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Capture `message` for ghost / admin / self / cross-family rejections.
- **Expected:** all four `message` values are **identical** (the localized `CannotLinkChild`); none contains a discriminating phrase.
- **Traces to:** AC-5, AC-7.

---

## Group H — AC-1 delegation + input boundary

### BE-TC-01 — Auto-link on Add-Child surfaces in My-Children (delegated AC-1)
- **Type:** functional/regression · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** Parent P (JWT).
- **Steps:** 1) `POST api/Parent/Add-Child` as P (valid child payload per P1-03 contract). 2) `GET api/Parent/My-Children` as P.
- **Expected:** Add-Child **200/201**; the new child appears in `My-Children` (auto-link created). **Note:** AC-1's primary coverage is `P1_03_AddChild_Tests`; this case is a thin confirming assertion — see README Q1. If P1-03 already asserts this, mark as covered-elsewhere rather than duplicating.
- **Traces to:** AC-1.

### BE-TC-33 — Oversized / boundary ChildEmail input → 422 (no unhandled 500) `(new — boundary)`
- **Type:** boundary/validation · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** Parent token.
- **Steps:** `POST Link-Child` with a very long (e.g. 5000-char) string in `ChildEmail`, and separately a string with leading/trailing whitespace around a valid email.
- **Expected:** Oversized/malformed → **422** (FluentValidation `EmailAddress`), never an unhandled **500**. For the whitespace-wrapped valid email, document the actual behavior (trim vs reject) — assert it does not 500.
- **Traces to:** input robustness; AC-5.

### BE-TC-34 — Case-insensitive email match (link resolves regardless of case) `(new — gap)`
- **Type:** functional/boundary · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** Parent P; student S with email `Child@Test.Local`.
- **Steps:** `POST Link-Child` with `child@test.local` (different case).
- **Expected:** Document the actual `FindLinkableChildByEmailAsync` behavior. If Identity normalizes email (ASP.NET Identity NormalizedEmail), expect **200** link success; if case-sensitive, expect **400** generic. Assert whichever the implementation does and flag if the result is surprising (case-sensitive email lookup is a UX trap for parents typing a child's email).
- **Traces to:** AC-2/AC-5 edge; flag to lead if mismatched.

---

## Implementation notes for `api-tester`
- Reconcile, don't duplicate: ~20 cases above already exist in `P1_04_LinkParentChild_Tests.cs`. Update that
  file in place; tighten status assertions to the exact **400/404/422** values in the mapping table; add the
  `(new)` cases.
- For every Link-Child rejection, prefer asserting the **exact** `statusCode` (400) over `NotBeInRange(200,299)`.
- Keep per-test unique emails; never share seed state across cases.
- Tag `[Unlink/P2-12]` cases so they can be lifted to a P2-12 folder if the lead decides (README Q3).
