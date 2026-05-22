## Wave 2 — Identity & Onboarding (auth sessions + family linkage)

Two backend stories, each through the full pipeline (analyzer → planner → backend-feature → api-tester → security-auditor → reviewer **PASS**):

- **P1-02 (BE) — Stay signed in.** Fixes the previously broken refresh/sign-out flow end-to-end: sign-in **and** registration now issue + persist a rotating refresh token in Redis (`IDistributedCache`, 7-day TTL); refresh returns a new access+refresh pair and invalidates the old one (replay → 401); sign-out revokes the refresh token; expired/invalid/missing refresh → **401** (not 500); UTC token-expiry correctness so the "token still running" guard fires properly. Commit `10cb5d2`.
- **P1-04 (BE) — Link a parent to a child.** New `ParentStudent` linkage (entity + EF config + migration in the `identity` schema, composite PK, Restrict FKs), an idempotent link service with an auto-link hook for P1-03, a fail-closed `LinkChild` command (generic errors — no email enumeration, parent resolved from JWT not the body), a Parent-gated `ParentController` (`POST Link-Child`, `GET My-Children`), and a reusable `FamilyScopeAuthorizationHandler` (+ requirement) that P1-05 will consume for family-scoped/self-scoped authorization. Commit `7796cbe`.

### Acceptance criteria
- **P1-02:** refresh issues a valid new access token; rotation invalidates the previous refresh token; sign-out revokes; bad/expired/missing refresh → 401; sign-in & registration both return a refresh token — covered by **18** integration tests.
- **P1-04:** a parent links a child they created / an unlinked child; idempotent re-link (no duplicate); cross-family IDOR blocked; non-existent/non-student/already-linked all return an identical generic failure; `My-Children` strictly caller-scoped; Parent-only gating (anon → 401, wrong role → 403) — covered by **22** integration tests.

### Tests & security
- Build clean; **full integration suite 81/81 green** (41 base + 18 P1-02 + 22 P1-04) on Testcontainers PostgreSQL.
- security-auditor: **P1-02 PASS** (0 blocking; pre-existing notes only) · **P1-04 PASS** (0 findings — IDOR / family-scope gate cleared).

### Follow-up debt (non-blocking)
- Externalize the JWT secret from `appsettings.json` + reconcile the session-key (Identity).
- Per-request JTI denylist deferred (live access tokens expire within ≤30 min after sign-out).
- Unlink/delete and under-13 (COPPA) consent deferred to dedicated stories.
- Grade-aware child projection belongs to a Curriculum-facing endpoint later (kept out to preserve module isolation).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
