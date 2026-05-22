## Wave 3 — Identity & Onboarding (child provisioning + access control)

Two backend stories, each through the full pipeline (analyzer → planner → backend-feature → api-tester → security-auditor → reviewer **PASS**):

- **P1-03 (BE) — Parent onboarding & add children.** Adds the parent-driven `Add-Child` flow on the Parent-gated `ParentController` (`POST api/Users/Parent/Add-Child`): creates a child `User` with a server-assigned **Student** role and a parent-set password, sets per-child Grade/language/country, and **auto-links** the child to the acting parent via P1-04's `ILinkParentStudentService`. Adds `Grade`+`Age` to `User` (migration; reuses existing `PreferredLanguage`/`Nationality` — no duplicate columns). Fail-closed: if role assignment fails the orphan child is deleted. Validation (grade 1–6, email, language∈{ar,en}, country, password) → 422. Acting parent always from the JWT, never the body. Commit `4633dec`.
- **P1-05 (BE) — Role-based access control.** Turns on enforcement: gates the previously-anonymous `AuthorzationController` (role/claim CRUD), Catalog `ProductsController`/`CategoriesController` (reads = any authenticated, writes = `AdminOnly`), and the `POST /api/notifications/` command endpoint — all via the `AuthorizationPolicies.AdminOnly` constant. Permission claims scoped to existing modules (future-module claims deferred). Verified the P1-04 family-scope handler is fail-closed, the JWT secret is a placeholder behind P1-01's Production/Staging guard, and 401-vs-403 + middleware order are correct. Commit `82af813`.

### Acceptance criteria
- **P1-03:** parent adds child (Student role, assigned email, grade/lang/country); child auto-linked + visible in `My-Children`; child can sign in; multiple children supported; duplicate email + invalid grade/language/password → safe errors — **40 integration tests**.
- **P1-05:** wrong role → 403, unauthenticated → 401, admin → 200; Catalog reads require auth, writes admin-only; authn endpoints stay anonymous; family-scope cross-family denial — **39 integration tests**.

### Tests & security
- Build clean; **full integration suite 160/160 green** (81 prior + 40 P1-03 + 39 P1-05), stable with no rate-limit flakiness (test-host-only `IpRateLimitOptions` override; production limiter untouched).
- security-auditor: **P1-03 PASS** (0 Critical/High; one Low role-assignment-integrity finding fixed in-story) · **P1-05 PASS** (0 blocking; the anonymous notifications POST it found was gated in-story).

### Follow-up debt (non-blocking)
- Pre-existing hardening backlog noted in `docs/security/P1-05.md` (CORS wildcard fallback, 500-handler message, `RequireHttpsMetadata` env-gating); `RegisterParentCommandHandler` raw-error leak (separate from P1-03's hardened handler).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
