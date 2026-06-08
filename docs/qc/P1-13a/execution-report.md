# Execution Report — P1-13a (Notifications email delivery + preferences)

> Source cases: `docs/qc/P1-13a/backend-test-cases.md` (25 cases, BE-TC-01..25).
> Status legend: **PASS** / **FAIL** / **BLOCKED** (record the blocker) / **SKIPPED** (record why).
> For any **FAIL** or **BLOCKED**, file a defect row in §3 and reference its ID.

## 1. Run metadata

| Field | Value |
|-------|-------|
| Run date / time (UTC) | 2026-06-07 |
| Tester (agent) | api-tester |
| Branch / commit | main / 8a8124c |
| Backend host + port | In-process WebApplicationFactory (no external port) |
| DB (Postgres) | Testcontainers PostgreSQL pgvector/pgvector:pg16 (throwaway container per run) |
| Email provider in test env | `None` / `LogEmailSender` (always succeeds, no SMTP contacted) |
| `IUserLookup` registered? | YES — Identity module registers `Services.UserLookup` (confirmed in DependencyInjection.cs line 90) |
| Failing-sender mechanism available? | NO — affects BE-TC-17/18/22; `LogEmailSender` always returns `Result.Success()`. No failing-sender test double or factory override exists. |
| Admin seed/token available? | YES — seeded `superadmin / 123Pa$$word!` available via `ApplyMigrationsAndSeedAsync()` |

## 2. Results per case

| ID | Title (short) | Priority | Status | Notes / observed |
|----|---------------|----------|--------|------------------|
| BE-TC-01 | GET first-read returns 4 defaults | P0 | **PASS** | 200; 4 items; WeeklyReport email=true, rest false; all push=false; categories 4/5/6 absent |
| BE-TC-02 | GET default read persists nothing | P1 | **PASS** | Two consecutive GETs return identical defaults; DB count=0 rows for user confirmed |
| BE-TC-03 | GET never 404 for no-rows user | P1 | **PASS** | 200 with 4 defaults; 404 never returned |
| BE-TC-04 | GET unauthenticated → 401 | P0 | **PASS** | 401 Unauthorized with no token |
| BE-TC-05 | PUT happy path all 4 → 200 | P0 | **PASS** | 200; Successed=true; success message in response |
| BE-TC-06 | PUT empty list → 422 | P0 | **PASS** | 422 UnprocessableEntity; errors[] non-empty (NotEmpty rule); NOT 400 |
| BE-TC-07 | PUT undefined category → 422 | P0 | **PASS** | 422; errors[] populated; category 99 rejected by Enum.IsDefined guard |
| BE-TC-08 | PUT duplicate categories → 422 | P0 | **PASS** | 422; errors[] populated; duplicate category 0 rejected by Distinct check |
| BE-TC-09 | PUT partial (1 category) → 200 (record-of-behaviour) | P1 | **PASS** | 200 Successed=true — validator is PERMISSIVE; see DEF-01 |
| BE-TC-10 | PUT values round-trip on GET | P0 | **PASS** | Written flags read back exactly as stored for all 4 categories |
| BE-TC-11 | Upsert update-in-place, partial leaves others | P1 | **PASS** | Partial PUT updates only supplied category; categories 1/2/3 retain their S1 stored values |
| BE-TC-12 | Prefs self-scoped, B never sees A | P0 | **PASS** | User B sees synthesised defaults; User A's writes not leaked; no IDOR |
| BE-TC-13 | PUT unauthenticated → 401 | P0 | **PASS** | 401 Unauthorized with no token |
| BE-TC-14 | Send with email → 202 | P0 | **PASS** | 202 Accepted; LogEmailSender records masked send |
| BE-TC-15 | Send null email → skipped, 202 | P1 | **PASS** | 202 Accepted; handler logs skip and returns Result.Success() |
| BE-TC-16 | Send success bare 202, not enveloped | P1 | **PASS** | 202; response body is empty/null — NOT a BaseResponse envelope (Results.Accepted()) |
| BE-TC-17 | Send failure → 400 generic Error | P1 | **BLOCKED** | BLOCKED (test-infra): `LogEmailSender` always returns `Result.Success()`. No failing-sender seam in `LearnexiaWebAppFactory`. Cannot force `Result.Failure` without a stub IEmailSender override. |
| BE-TC-18 | Failure leaks no provider internals | P1 | **BLOCKED** | BLOCKED (test-infra): same blocker as BE-TC-17 — cannot reach the failure path with the dev log sink. |
| BE-TC-19 | Config ships no secret + dev log sink | P2 | **PASS** | POST /api/notifications returns 202 (confirms LogEmailSender); appsettings.json Email.Provider="None", UserName="", Password="" — no secrets committed |
| BE-TC-20 | Malformed send body NOT auto-validated | P2 | **PASS** | Empty Title/Body returns 202 — `ValidationBehavior` never fires on `IRequest<Result>`. See DEF-02. |
| BE-TC-21 | Registration writes welcome row + succeeds | P0 | **PASS** | Registration 200; Admin GET List returns welcome notification with title "Welcome to Learnexia" and body containing userName |
| BE-TC-22 | Welcome email failure isolated | P0 | **BLOCKED** | BLOCKED (test-infra): `LogEmailSender` always succeeds; no failing-sender double available. Welcome-row write confirmed by BE-TC-21; failure-isolation cannot be exercised against the current test fixture. |
| BE-TC-23 | Welcome row idempotent on redelivery | P1 | **PASS** | Direct `IPublisher.Publish(UserRegisteredIntegrationEvent)` replay for same userId → still exactly 1 welcome notification (AnyAsync idempotency guard works) |
| BE-TC-24 | Prefs return BaseResponse `Successed` envelope | P2 | **PASS** | GET and PUT both return BaseResponse with `successed`, `statusCode`, `message`, `data` keys (correct spelling `successed`); POST /api/notifications confirmed bare 202 (no envelope) |
| BE-TC-25 | Anonymous / non-admin send → 401/403 | P0 | **PASS** | No token → 401; Parent JWT → 403 |

### Summary tally
| Status | Count |
|--------|-------|
| PASS | 21 |
| FAIL | 0 |
| BLOCKED | 3 |
| SKIPPED | 0 |
| **Total** | **25** |

## 3. Defects filed

| Defect ID | Case ref | Severity | Summary | Repro / expected vs actual | Owner |
|-----------|----------|----------|---------|----------------------------|-------|
| DEF-01 | BE-TC-09 | Medium | PUT preferences validator does not require all 4 categories — a single-category PUT is accepted | **Expected (if product intent is "all 4 required"):** 422 with validation error. **Actual:** 200 Successed=true. `UpdateMyNotificationPreferencesCommandValidator` enforces only NotEmpty + Enum.IsDefined + Distinct — no "exactly 4 categories" rule. Lead must confirm product intent: if "all 4 required", add a `Must(prefs => prefs.Count == 4)` rule (or similar). | backend-feature |
| DEF-02 | BE-TC-20 | Medium | `SendNotificationCommand` is `IRequest<Result>` not `ICommand<>` — validator never fires on `POST /api/notifications`; empty Title/Body reach the handler unvalidated and return 202 | **Expected (if "POST must 422 on bad body" is the contract):** 422 with validation errors from `SendNotificationCommandValidator`. **Actual:** 202 Accepted — validation pipeline (`ValidationBehavior`) only runs on `ICommand<>`, not `IRequest<>`. Fix: change `SendNotificationCommand` to `ICommand<Result>` or add explicit validation in the minimal API endpoint. Lead must confirm intended contract. | backend-feature |
| DEF-03 | BE-TC-17, BE-TC-18, BE-TC-22 | Low (test-infra) | No failing-email-sender seam in `LearnexiaWebAppFactory` — three cases blocked | The `LearnexiaWebAppFactory` does not expose a mechanism to swap `IEmailSender` for a failing stub. Cases BE-TC-17 (send failure → 400), BE-TC-18 (no provider internals on failure), and BE-TC-22 (welcome email failure isolated) cannot be exercised. **Suggested fix:** add a `FailingEmailSender` property to `LearnexiaWebAppFactory` (similar to `PushSender`) that can be optionally registered via `services.RemoveAll<IEmailSender>()` + `services.AddScoped<IEmailSender>(_ => FailingEmailSender)`. | backend-feature (test infrastructure) |

> **Pre-flagged findings (README §5):**
> - README **Q1** — PUT validator permissiveness confirmed (BE-TC-09 PASS). Filed as **DEF-01** for lead decision.
> - README **Q2** — `SendNotificationCommand` validator not wired confirmed (BE-TC-20 PASS). Filed as **DEF-02** for lead decision.
> - README **Q3** — AC2 (secrets-from-env): appsettings.json confirmed to ship Provider="None", empty credentials. Static + behavioural assertion in BE-TC-19 (PASS).
> - README **Q4** — No failing-sender seam: BE-TC-17/18/22 BLOCKED as predicted. Filed as **DEF-03** (test-infra).
> - README **Q5** — `IUserLookup` IS registered (Identity adapter in DependencyInjection.cs). Welcome-email path is attempted; LogEmailSender always succeeds so the email silently delivers to the log. Row-level assertion is primary contract (BE-TC-21 PASS).
> - README **Q6** — Admin credentials: seeded superadmin available via `GetAdminTokenAsync()`. All AdminOnly tests unblocked.

## 4. Verdict

- **Overall:** **PASS-WITH-DEFECTS** (21 PASS, 0 FAIL, 3 BLOCKED)
- **Coverage actually executed:** 22 / 25 (blocked: BE-TC-17 [no failing-sender], BE-TC-18 [same], BE-TC-22 [same])
- **Acceptance criteria status:**
  - AC1 (send email via IEmailSender): **PASS** — BE-TC-14, 15, 16, 19 green
  - AC2 (secrets not committed): **PASS** — BE-TC-19 static assertion green
  - AC3 (welcome email on registration): **PASS** — BE-TC-21 green; BE-TC-23 green; BE-TC-22 BLOCKED (failure-isolation path only)
  - AC4 (resilient, no internals surfaced): **BLOCKED** — BE-TC-17/18 blocked; no FAIL; failure path not reachable with log sink
  - AC5 (AdminOnly send, no leak): **PASS** — BE-TC-25 green; no failures to inspect for AC5 info-leakage
  - H1 (GET defaults, never 404): **PASS** — BE-TC-01/02/03 green
  - H2 (PUT validation → 422): **PASS** — BE-TC-06/07/08 green; BE-TC-09 PASS (permissive — DEF-01 filed)
  - H3 (PUT persistence): **PASS** — BE-TC-05/10/11 green
  - H4 (Authz self-scoped): **PASS** — BE-TC-04/12/13/24 green
- **Defects for backend-feature:** DEF-01 (validator permissiveness — lead decision needed), DEF-02 (validator not wired on send command — lead decision needed), DEF-03 (test-infra: no failing-sender seam).
- **Handoff:** results feed the `reviewer` gate. DEF-01 and DEF-02 require lead confirmation of product intent before backend-feature acts. DEF-03 is a test-infra improvement; blocked cases should be re-opened after the seam is added.
