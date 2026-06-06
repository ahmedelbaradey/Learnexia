# QC Test Plan & Coverage Report — P1-10 (Backend only)

**Story:** P1-10 — Sign in to the admin dashboard (Identity & Onboarding, Phase 1)
**Scope of this run:** **Backend API surface only.** No frontend (`apps/admin-dashboard`) cases — those are out of scope for this pass.
**Run folder:** `docs/qc/P1-10/`
**Designed by:** QC test architect (design-only; no test code, no execution).
**Implements into:** `api-tester` consumes `backend-test-cases.md`; results land in `execution-report.md`.

---

## 1. Summary

P1-10 backend is **pure reuse + a tiny seed** of the existing Identity module — it adds **no new endpoint** of its own. The testable backend surface is therefore the *existing* Identity auth + admin-authz machinery that the admin dashboard depends on, verified through the lens of the P1-10 acceptance criteria:

1. **Admin sign-in issues a JWT** — `POST /api/Users/Authentication/Sign-In` → `BaseResponse<JwtAuthResponse>`.
2. **No public admin self-registration** — the only anonymous account-creation path is parent registration; there is no admin/self-register endpoint, and the admin account is provisioned by seed/config only.
3. **Admin-only routes deny non-admins (403) and anonymous (401)** — the `AdminOnly` policy (`RequireRole("Admin","SuperAdmin")`), exercised on the real admin-gated controllers.
4. **Role discovery for the dashboard gate** — `GET /api/Users/Me` returns the caller's `Roles` (PascalCase) so the FE can gate; `GET /api/Users/UserManagement/GetUserProfile` is itself admin-only.
5. **Session refresh + sign-out for admins** — reuse of the P1-02 flow with an admin token.
6. **Admin seed** — env/config-driven `SeedConfiguredAdminAsync` (no committed credential; no-op when unconfigured); legacy `superadmin` available in dev/test.

### Counts

| Metric | Count |
|---|---|
| Total cases | 28 |
| Backend (`api-tester`) | 28 |
| Frontend | 0 (out of scope — backend-only run) |
| P0 | 12 |
| P1 | 10 |
| P2 | 6 |

By type: auth-authz 12 · functional 4 · negative 4 · boundary 3 · persistence/seed 3 · validation 1 (regression folded into BE-TC-28).

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria are drawn from the user story (`user-stories/Phase-1-Foundation/P1-10-admin-sign-in.md`), the BE task file (`tasks/Backend/Phase-1-Foundation/P1-10-BE.md` BE-1..BE-4), and the canonical brief (`docs/briefs/P1-10.md` AC #2, #3, #8). FE-only criteria are listed as **N/A (frontend)** so the gap is explicit, not silent.

| # | Acceptance criterion | Source | Backend cases | Verdict |
|---|---|---|---|---|
| AC-1 | Admin signs in with valid credentials → **JWT issued** (lands on shell = FE) | Story AC#1, BE-2 | BE-TC-01, BE-TC-02 | Covered (BE half) |
| AC-2 | Admin accounts **seeded/invited only — no public admin self-registration** | Story AC#2, BE-1, Brief AC#2/#4 | BE-TC-23, BE-TC-24, BE-TC-25, BE-TC-26, BE-TC-27 | Covered |
| AC-3 | Non-admin (or anonymous) credentials → **admin routes denied (403 / 401)** | Story AC#3, BE-3, Brief AC#3/#8 | BE-TC-04, BE-TC-13, BE-TC-14, BE-TC-15, BE-TC-16, BE-TC-17, BE-TC-18, BE-TC-19, BE-TC-20, BE-TC-21, BE-TC-22 | Covered |
| AC-4 | **Session refresh + sign-out** work for admins (reuse P1-02) | Story AC#4, BE task | BE-TC-28 | Covered |
| AC-5 | Dashboard shell renders placeholder nav | Story AC#5 | — | **N/A (frontend)** — not in this run |
| AC-6 | **`Me` returns Admin role** so the dashboard can gate routes | BE-4 | BE-TC-10, BE-TC-11, BE-TC-12, BE-TC-22 | Covered |
| AC-7 | JWT carries Admin **role claims** for the seeded admin | BE-2 | BE-TC-03, BE-TC-04, BE-TC-18 | Covered |
| — | Sign-in **negative paths** (wrong password, unknown user, lockout, deactivated, validation, anti-enumeration) | Brief / SignIn handler behaviour | BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-08, BE-TC-09 | Covered |

**Verdict: every backend-relevant acceptance criterion (AC-1, AC-2, AC-3, AC-4, AC-6, AC-7) has at least one P0/P1 case.** The only uncovered story criterion is **AC-5 (dashboard shell rendering)**, which is a pure frontend surface and **out of scope for this backend-only run** — flagged here, not dropped.

---

## 3. Risk notes (where cases are weighted, and why)

1. **Authorization is the highest risk and gets the most cases (12 auth-authz cases).** P1-10's entire security value is "non-admins cannot reach admin surfaces." The `AdminOnly` policy is `RequireRole("Admin","SuperAdmin")` — a **role-name string match against the JWT role claim**. The role Name is emitted verbatim into the token and matched ordinally, so any casing drift in `RoleSeeder`/`UserSeeder` would silently make every admin 403 (or, worse, let a mis-seeded role through). We assert the full matrix on **real** admin-gated routes (`AuthorzationController`, `UserManagementController`) rather than a synthetic endpoint: anonymous→401, Parent→403, Basic→403, Admin/SuperAdmin→200, and tampered/expired token→401 (never 500, never a fake-200 envelope).

2. **The "no public admin self-registration" product rule is asserted as a negative.** The only anonymous account-minting endpoint is `Register-Parent` (role = Parent). We assert there is **no** anonymous path that yields an Admin/SuperAdmin/Student account, and that the admin-creating surface (`UserManagement/AddUser`) is itself behind `AdminOnly`. This directly defends Brief AC#2/#4 and the CLAUDE.md "no self-register" override.

3. **The role-source contract is subtle and load-bearing for the dashboard.** `JwtAuthResponse` does **not** contain roles; the dashboard reads roles from `GET /api/Users/Me` (`[Authorize]`, self-scoped, not role-gated) — **not** from `GetUserProfile` (which is admin-only and would 403 a non-admin trying to learn its own role). Mixing these up would break the FE gate. We pin both behaviours (BE-TC-02, BE-TC-10, BE-TC-22).

4. **Seed correctness has two distinct modes** (env-driven `SeedConfiguredAdminAsync` vs legacy dev `superadmin`). The integration-test host (`Testing` env) does **not** set `AdminSeed:*`, so `SeedConfiguredAdminAsync` is a **no-op** there and the admin used by tests is the legacy `superadmin` (Basic+Admin+SuperAdmin). Cases that assert the configured admin seed (BE-TC-27) are marked **environment-gated / may be BLOCKED** with the exact config required, rather than assumed.

5. **Anti-enumeration + lockout** are real behaviours in `SignInCommandHandler` (wrong-user and wrong-password return the *same* generic 400; 5 failures → lockout 400; deactivated → its own 400 message). These are easy to regress and are security-relevant, so they get explicit cases (BE-TC-05/06/08/09).

---

## 4. Open questions / assumptions (lead to resolve before implementation)

1. **Configured-admin seed test surface.** The integration host runs in the `Testing` environment with **no `AdminSeed:Email`/`AdminSeed:Password`** configured, so `SeedConfiguredAdminAsync` is a no-op and the only admin is the legacy `superadmin`. **Q:** Should `api-tester` add `AdminSeed:*` to a dedicated test host (to exercise BE-TC-27 green), or keep that case **environment-gated/BLOCKED** and validate admin behaviour via `superadmin`? **Assumption (proceed unless overridden):** validate admin authz via the seeded `superadmin`; keep BE-TC-27 (configured-admin idempotency/no-committed-credential) environment-gated with the config recipe provided. BE-TC-26 (no-op when unset) is testable on the default host.

2. **Status code for bad credentials.** `SignInCommandHandler` returns `BadRequest<>` (**HTTP 400**, `Successed=false`) for wrong user / wrong password / deactivated — **not 401 and not 422**. (422 is reserved for `ICommand` validation failures, e.g. missing `UserName`/`Password`.) **Assumption:** expected sign-in failure status = **400**; expected missing-field status = **422**. Confirm this matches the lead's contract expectation.

3. **No P1-10-specific admin endpoint exists.** P1-10 adds no new admin controller; the authz matrix is validated against existing `AdminOnly` controllers (`AuthorzationController`, `UserManagementController`). **Assumption:** these are acceptable representative surfaces for AC-3/AC-8 (they already are in `P1_05_RBAC_Tests`). A dedicated P1-10 admin "ping" endpoint would be a backend-feature change, not a QC decision.

4. **Reuse vs new test file.** Much of this overlaps `P1_05_RBAC_Tests` and `P1_02_RefreshAndSignOut_Tests`. **Recommendation:** `api-tester` implements the P1-10-specific cases (admin-token sign-in claims, `Me` role for admin, admin refresh/sign-out, no-admin-self-register negatives, configured-seed) in a new `P1_10_AdminSignIn_Tests.cs`, and treats the existing P1-05/P1-02 suites as the regression baseline (BE-TC-28 step 5 asserts they stay green) rather than duplicating them.

---

## 5. Handoff

| File | Owner | Goes to |
|---|---|---|
| `docs/qc/P1-10/README.md` | QC architect | Lead (this report) |
| `docs/qc/P1-10/backend-test-cases.md` | QC architect | **`api-tester`** — implement BE-TC-01..28 as integration tests (reuse `LearnexiaWebAppFactory`) |
| `docs/qc/P1-10/execution-report.md` | testers fill | `api-tester` records pass/fail per case + defects after running |

**How `execution-report.md` gets filled:** `api-tester` runs the implemented suite against the running API (Testcontainers Postgres host, as the existing suites do), then fills the templated table with PASS/FAIL/BLOCKED per `BE-TC-*`, links a defect for any FAIL, and writes the final verdict. The QC architect never fills results.

Run facts (for `api-tester`): tests use `LearnexiaWebAppFactory` (Testcontainers `pgvector/pgvector:pg16`), `UseEnvironment("Testing")`, and seed `superadmin`/`basicuser` via `ApplyMigrationsAndSeedAsync()`. Seeded creds: `superadmin` / `123Pa$$word!` (Basic+Admin+SuperAdmin), `basicuser` / `123Pa$$word!` (Basic). Parent tokens are minted via `POST /api/Users/Authentication/Register-Parent`.
