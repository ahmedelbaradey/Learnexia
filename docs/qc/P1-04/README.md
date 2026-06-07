# QC Test Plan & Coverage Report — P1-04 (Link parent to child) — BACKEND ONLY

> Run type: **Backend-only** QC test-case design pass.
> Story: `user-stories/Phase-1-Foundation/P1-04-link-parent-to-child.md` (FR-ID-3)
> Brief: `docs/briefs/P1-04.md` · Plan: `docs/plans/P1-04.md` · Tasks: `tasks/Backend/Phase-1-Foundation/P1-04-BE.md`
> Author: QC test architect (design only — no test code, no execution).

---

## 1. Summary

P1-04 establishes the **family graph** (`ParentStudent` linkage) and the authorization boundary that
prevents one family from reading another family's child data — the single most sensitive data class
(minors). This pass designs the backend API/HTTP test catalog for `api-tester` to implement.

**IMPORTANT — implementation drifted from the brief/plan.** The shipped code is more advanced and lives
in a **different module than the brief assumed**. The test cases below target the *actual* shipped
surface, not the brief's assumptions:

| Brief/plan assumption | Actual shipped reality (what to test against) |
|---|---|
| Linkage lives in **Identity** module | Linkage moved into a dedicated **Parent** module, schema `parent` (relocated in P2-12). |
| Route `api/Users/Parent/Link-Child` / `My-Children` | Routes are **`api/Parent/Link-Child`**, **`api/Parent/My-Children`** (controller `ParentController`, `[Route("api/Parent")]`). |
| `FamilyScopeAuthorizationHandler` (resource-based authz) is the scoping primitive | **Not the path used by these endpoints.** Family scope here is enforced **inside each handler** via the `ILinkParentStudentService` linkage checks + `ICurrentUserService.UserId`. The `My-Children` list is keyed on the JWT caller's id. (A reusable `FamilyScopeAuthorizationHandler` is a P1-05 concern; out of scope for this backend run.) |
| Non-existent / non-student child → 404 or 422 (open Q3) | Lead decision implemented as **fail-closed 400 `BadRequest`** with one generic message (anti-enumeration). |
| Unlink out of scope | **Unlink shipped** (`DELETE api/Parent/Unlink-Child`, P2-12) with a last-parent guard + TOCTOU advisory-lock. Included here as in-scope-adjacent regression coverage because it is a family-scope/IDOR surface on the same data. |
| Child-by-email is the link key | Confirmed: `LinkChildCommand { string ChildEmail }`; **no `ParentId` in the body** (acting parent is JWT-only). |

**Endpoints in scope (all gated `[Authorize(Roles = "Parent,Admin,SuperAdmin")]`):**

| Method | Route | Feature | Maps to |
|---|---|---|---|
| POST | `api/Parent/Link-Child` | Link an existing student to the calling parent | AC-2, AC-4, AC-5, AC-6, AC-7 |
| GET  | `api/Parent/My-Children` | List the calling parent's linked children (family-scoped) | AC-3, AC-4 |
| DELETE | `api/Parent/Unlink-Child` | Remove a (parent, child) link (P2-12; family-scope regression) | AC-3/AC-7 adjacent |
| (auto) | — | Auto-link on Add-Child (AC-1) | AC-1 — covered by P1-03 tests, **not** re-implemented here (see §4) |

**Counts:** 33 backend cases total · 33 backend / 0 frontend · by priority **P0: 18 · P1: 11 · P2: 4**.

A substantial integration test already exists: `backend/tests/Learnexia.IntegrationTests/P1_04_LinkParentChild_Tests.cs`.
Many cases below **already have an implementing test** — those are marked `(existing)` in the catalog so
`api-tester` reconciles rather than duplicates. The new value of this pass is the **status-code precision**
(400 vs 404 vs 422), the **Admin/SuperAdmin permitted-caller** cases, the **Unlink family-scope/IDOR + last-parent**
cases, and a couple of gaps (e.g. case-insensitive email match, oversized input).

---

## 2. Coverage matrix (every acceptance criterion → case IDs)

| AC (from brief) | Covered by | Verdict |
|---|---|---|
| **AC-1** — Auto-link on Add-Child | `BE-TC-01` (cross-references P1-03 `P1_03_AddChild_Tests`) | Covered (delegated — see §4 note). Not a P1-04 endpoint. |
| **AC-2** — Link an existing child → 200 + child summary | `BE-TC-02`, `BE-TC-03`, `BE-TC-31` | Covered (P0) |
| **AC-3** — Family-scoped read (parent sees only own children) | `BE-TC-12`, `BE-TC-13`, `BE-TC-14`, `BE-TC-15`, `BE-TC-27`, `BE-TC-28` | Covered (P0) |
| **AC-4** — Many-to-many (parent↔multiple children; child↔multiple parents) | `BE-TC-04`, `BE-TC-16`, `BE-TC-17` | Covered (P0) |
| **AC-5** — Non-existent / non-student child → clear non-leaking error | `BE-TC-05`, `BE-TC-06`, `BE-TC-07`, `BE-TC-08` | Covered (P0) |
| **AC-6** — Idempotency / no double-link | `BE-TC-09`, `BE-TC-10` | Covered (P0) |
| **AC-7** — Guard against arbitrary / cross-family linking (IDOR) | `BE-TC-11`, `BE-TC-18`, `BE-TC-19`, `BE-TC-25` | Covered (P0) |

**No acceptance criterion is uncovered.** AC-1 is delegated (see §4); every other AC has ≥1 P0 case.

Cross-cutting coverage not tied to a single AC: auth/authz gate matrix (`BE-TC-18`..`BE-TC-24`),
`BaseResponse` envelope + status mapping (`BE-TC-29`..`BE-TC-32`), validation→422 (`BE-TC-20`, `BE-TC-21`),
Unlink family-scope/IDOR + last-parent guard (`BE-TC-25`..`BE-TC-28`), product-override negatives (`BE-TC-22`),
input boundary (`BE-TC-33`).

---

## 3. Risk notes (where cases are weighted, and why)

1. **IDOR / cross-family access on minors' data (highest risk).** The whole story is an access-control
   primitive. Weighted heavily: `BE-TC-11` (parent B cannot claim parent A's child), `BE-TC-18`/`BE-TC-19`
   (non-parent & unauth blocked), `BE-TC-12`..`BE-TC-15` (My-Children isolation), `BE-TC-25`..`BE-TC-28`
   (Unlink only acts on the caller's own links). A single false `Succeed` here leaks a child to a stranger.
2. **Email enumeration via the link error surface.** The fail-closed contract requires **identical
   status + shape** for non-existent vs existing-but-ineligible vs cross-family. `BE-TC-05`..`BE-TC-08`
   and `BE-TC-32` assert non-existent, non-student (Admin), self-link, and cross-family all return the
   **same 400 + same generic message** with no `"not found"` / `"already linked"` / ownership leakage.
3. **Parent identity must come from the JWT, never the body.** `LinkChildCommand`/`UnlinkChildCommand`
   have no `ParentId`. `BE-TC-26` asserts a body `ParentId` override is ignored.
4. **Last-parent invariant + concurrency (Unlink).** A minor must never be orphaned. `BE-TC-27` asserts
   the last parent cannot unlink (400); `BE-TC-28` is a best-effort concurrent-unlink case (TOCTOU /
   `pg_advisory_xact_lock`) — marked P2 + may be flaky/hard to drive deterministically over HTTP.
5. **Status-code precision.** Implementation maps to **400** (link rejections, last-parent) and **404**
   (unlink of a non-linked child) and **422** (FluentValidation). These are easy to get wrong; explicit
   assertions in `BE-TC-29`..`BE-TC-32`.

---

## 4. Open questions / assumptions (lead must resolve before/while implementing)

- **Q1 — AC-1 ownership.** AC-1 (auto-link on Add-Child) is exercised by the **P1-03** add-child flow
  (`P1_03_AddChild_Tests.cs`), not by a P1-04 endpoint. **Assumption:** P1-04 backend tests *reference* but
  do not re-implement AC-1; `BE-TC-01` is a thin assertion (after Add-Child the child appears in
  `My-Children`). Confirm this delegation is acceptable, or promote `BE-TC-01` to a full add-child→link flow.
- **Q2 — Reconcile with the existing test file.** `P1_04_LinkParentChild_Tests.cs` already implements
  ~20 of these. **Assumption:** `api-tester` updates the existing file in place (do not create a parallel
  file), adds the missing cases (Admin-as-caller link, case-insensitive email, oversized email, Unlink
  family-scope + last-parent, explicit 400 status assertions), and reconciles status-code expectations
  with the **actual** 400/404/422 mapping documented above. Confirm.
- **Q3 — Unlink scope in *this* run.** Unlink shipped in P2-12, not P1-04. **Assumption:** include Unlink
  family-scope/IDOR + last-parent cases here because they are the same minors'-data boundary and are not
  covered by a dedicated P1-04 file. If the lead prefers Unlink stay under a P2-12 QC folder, drop
  `BE-TC-25`..`BE-TC-28` from this run (they are tagged `[Unlink/P2-12]` for easy removal).
- **Q4 — Admin/SuperAdmin as caller of Link-Child.** The controller permits `Admin,SuperAdmin` to call
  Link-Child/Unlink-Child (support flows). The handler resolves the acting "parent" from the JWT, so an
  admin would link a child *to the admin's own id*. `BE-TC-23` asserts the gate **permits** (non-403) an
  admin caller; the *semantic* correctness of an admin self-linking a child is **not specified** by the
  story — flagged as a product question, asserted only at the auth-gate level.
- **Q5 — `My-Children` for an Admin/SuperAdmin caller.** Returns 200 + empty (admin has no `ParentStudent`
  rows). `BE-TC-24` asserts 200 + empty for a SuperAdmin token (gate permits; data is correctly empty).
- **Q6 — Child login routing.** The dispatch mentions "child login routing." There is **no child-login
  routing endpoint in the P1-04 backend surface** — login/role/`HasChildren` routing is served by
  `GET api/Users/Authentication/Me` (`GetMeQueryHandler`) and Sign-In, owned by P1-01/P1-02/P1-09. The only
  P1-04-relevant signal is `MeResponse.HasChildren`, which reads the `ParentStudent` link via the
  `IParentChildQuery` seam. `BE-TC-22b` asserts `HasChildren` flips true after a link (the only routing-input
  this story produces). Full role-based routing tests belong to P1-09, not this run — confirm.

---

## 5. Handoff

- **`backend-test-cases.md`** → **`api-tester`**: implement/reconcile `BE-TC-01`..`BE-TC-33` against the
  running API (`WebApplicationFactory` + seed), mirroring `P1_04_LinkParentChild_Tests.cs` and
  `P1_01_RegisterParent_Tests.cs` (dual camelCase/PascalCase JSON path assertions). Update the existing
  `P1_04_LinkParentChild_Tests.cs` in place per Q2; do not fork a parallel file.
- **No `frontend-test-cases.md`** — backend-only run by design.
- **`execution-report.md`** — empty template scaffolded by QC. `api-tester` fills pass/fail per case + defects
  **after** running. QC never fills results.

---

## Coverage verdict

**PASS — every acceptance criterion (AC-1..AC-7) has at least one P0/P1 case; AC-1 is delegated to P1-03 with
a confirming assertion (Q1). No criterion is left uncovered.** The riskiest surface (cross-family IDOR on
minors' data + email enumeration) carries the most P0 cases. Resolve Q1–Q6 before/while implementing.
