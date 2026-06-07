# QC Test Plan + Coverage Report — P1-13a (Notifications email delivery)

> **Run scope:** BACKEND-ONLY. Story `P1-13a` (Notifications email delivery) plus the
> Notifications-module preferences surface that now ships alongside it (per HANDOFF — `GET/PUT
> /api/Notifications/Preferences`). No frontend surface in scope → **no `frontend-test-cases.md`**.
> **Design-only artifact** — the testers implement + run; this folder never carries results until
> `execution-report.md` is filled by `api-tester`.

## 1. Summary

- **Story:** `user-stories/Phase-1-Foundation/P1-13a-notifications-email-delivery.md` — stand up real,
  config-driven email delivery in the Notifications module (`IEmailSender` abstraction + SMTP adapter +
  dev log/no-op sink), wire the welcome email on `UserRegistered`, keep failures isolated and
  non-leaking. Security-audit brief: `docs/briefs/P1-13a-security-audit.md` (PASS-with-followups).
- **What's in scope (HTTP surface under test):**
  - `GET  /api/Notifications/Preferences` — self-scoped read; synthesises defaults for the 4 user-facing
    categories; never 404, never persists on read.
  - `PUT  /api/Notifications/Preferences` — self-scoped upsert (`ICommand<>`, validated → 422 on
    failure); atomic multi-row write in an explicit transaction.
  - `POST /api/notifications` — `AdminOnly` minimal-API send endpoint; maps to `SendNotificationCommand`
    → `IEmailSender`; returns **202 Accepted** on success / **400 BadRequest** on handler failure.
  - **Email-delivery behaviour as exposed via API** — observed through `POST /api/notifications`
    (success/skip/failure) and through the welcome-email path triggered by registration, observed via
    `GET /api/Notifications/Notifications/List` (`AdminOnly`, the in-app notification row that the
    welcome path always writes).
- **Out of scope (other Notifications endpoints not part of this story):** parent per-child
  re-engagement preferences (`…/Preferences/Children/{childId}/Reengagement`), Devices, Inbox. Listed in
  §4 risks only; no cases written.
- **Counts:** **25 cases total — all backend (`api-tester`).** By priority: **P0 = 13**, **P1 = 9**,
  **P2 = 3**. By surface: Preferences GET = 4 (BE-TC-01..04), Preferences PUT = 9 (BE-TC-05..13),
  Send/email-delivery `POST /api/notifications` = 7 (BE-TC-14..18, 20, 25), welcome-email-on-registration
  = 3 (BE-TC-21..23), cross-cutting config/envelope = 2 (BE-TC-19, 24).

## 2. Coverage matrix (acceptance criterion → case IDs)

| # | Acceptance criterion (story / HANDOFF) | Case IDs | Covered? |
|---|----------------------------------------|----------|----------|
| AC1 | Notifications can **send email** via a config-driven sender (`SendNotificationCommandHandler` → `IEmailSender`; dev = no-op/log sink) | BE-TC-14, BE-TC-15, BE-TC-16, BE-TC-19 | ✅ |
| AC2 | Secrets come from env/secret storage, never `appsettings` | BE-TC-19 (config-shape assertion — *partial*, see §4/§5) | ⚠️ partial |
| AC3 | A **welcome email** is sent on `UserRegistered`, best-effort; an email failure does **not** fail registration; the notification-row write is kept | BE-TC-21, BE-TC-22, BE-TC-23 | ✅ |
| AC4 | Sending is resilient: failures logged server-side, **no provider internals** surfaced to callers | BE-TC-17, BE-TC-18 | ✅ |
| AC5 | Passes security-auditor (injection / SSRF / leakage) | covered by `security-auditor` (brief already PASS); BE-TC-18 (no-leak on failure), BE-TC-25 (AdminOnly send surface) | ✅ (defensive) |
| H1 (HANDOFF) | `GET /Preferences` returns **defaults** for all user-facing categories on first read; never 404; nothing persisted on read | BE-TC-01, BE-TC-02, BE-TC-03 | ✅ |
| H2 (HANDOFF) | `PUT /Preferences` validation — categories must be **defined** and **distinct**, else **422** | BE-TC-06, BE-TC-07, BE-TC-08, BE-TC-09 | ✅ |
| H3 (HANDOFF) | `PUT /Preferences` **persistence** — saved values round-trip on the next GET (Email/Push per category) | BE-TC-05, BE-TC-10, BE-TC-11 | ✅ |
| H4 (HANDOFF) | **Authz** — own prefs only; UserId resolved from JWT, no IDOR surface; 401 unauthenticated | BE-TC-04, BE-TC-12, BE-TC-13, BE-TC-24 | ✅ |

**Coverage verdict:** every acceptance criterion has at least one P0/P1 case. **One partial: AC2**
(secrets-from-env) is only weakly assertable from a black-box API test — see §5 open question Q3; the
substantive control is config/code, already covered by the security-auditor pass. No criterion is left
with **zero** coverage.

## 3. Test-design notes (verified against code, load-bearing)

These were verified by reading the module — implementers must hold them exactly, they change expected
results:

1. **`PUT /Preferences` validator does NOT require all 4 categories.** It enforces only:
   `Preferences` non-empty, every `Category` is a **defined** `NotificationCategory`, and categories are
   **distinct**. A PUT with a single category is **valid** (200), not 422. *(The prompt's "PUT validator
   requires all 4 categories" is inaccurate against `UpdateMyNotificationPreferencesCommandValidator.cs`
   — see §5 Q1.)* BE-TC-09 asserts the actual permissive behaviour; BE-TC-06/07/08 assert the rules that
   ARE enforced.
2. **Validation maps to 422, not 400.** `UpdateMyNotificationPreferencesCommand` is an `ICommand<>`, so
   `ValidationBehavior` runs; a failure throws `ValidationException`, mapped by `ErrorHandlerMiddleWare`
   to **422 UnprocessableEntity** with `Successed=false`, `message="Validation Failed"`, and an `errors[]`
   array of `{propertyName, errorMessage}`. (The controller's `[ProducesResponseType(400)]` attribute is
   misleading — the live status is 422.)
3. **`GET /Preferences` returns exactly 4 items** even though the enum has 7 values: `WeeklyReport`,
   `StreakAtRisk`, `ProductAnnouncement`, `Achievement`. Categories 4/5/6 (`DailyMissionReminder`,
   `LapseWinBack`, `System`) are deliberately excluded. **Defaults:** `WeeklyReport` → `emailEnabled:true`;
   all other three → `emailEnabled:false`; **all four** → `pushEnabled:false`.
4. **Both Preferences endpoints are JWT-self-scoped** (`[Authorize]`, `UserId` from
   `ICurrentUserService`, never from body/route). There is **no path parameter** carrying a user id → no
   direct IDOR vector; the test asserts isolation by writing as user A and reading the unchanged defaults
   as user B.
5. **`POST /api/notifications` is the email-send surface and is `AdminOnly`.** It maps the JSON body to
   `SendNotificationCommand` → `IEmailSender`. On success it returns **`202 Accepted` with an empty body**
   (NOT a `BaseResponse<T>` envelope — it's `Results.Accepted()`); on handler failure it returns
   **`400 BadRequest` carrying `result.Error`** (a `{code, message}` shape, NOT the full envelope).
   Anonymous/non-admin → **401/403**.
6. **`SendNotificationCommand` is `IRequest<Result>`, NOT `ICommand<>`** → its
   `SendNotificationCommandValidator` does **NOT** auto-fire through the pipeline. So a POST with an empty
   `Title`/`Body` or malformed `RecipientEmail` is **not** rejected with 422 by validation — it reaches
   the handler. With the dev **log sink** the malformed input still succeeds (202). BE-TC-20 documents
   this as a behaviour-of-record + flags it (see §5 Q2).
7. **Dev/test environment uses `LogEmailSender`** (`Email:Provider = None`) — no SMTP server contacted,
   `SendAsync` always returns `Result.Success()`. So in the default test env, a send with a recipient
   email returns **202**; a send with `RecipientEmail = null` is **skipped** but **still 202**. A genuine
   **400/failure** path can only be observed by forcing a failing sender (test double / a `Smtp` provider
   pointed at an unreachable host) — see Q4.
8. **Welcome email is best-effort and resolved via `IUserLookup`**, which may be unregistered in the test
   host → the email is **skipped (logged)**, but the in-app welcome `Notification` row is **always**
   written, and registration always succeeds. The observable assertion for AC3 is therefore: registration
   succeeds + the welcome row is retrievable via `GET /…/Notifications/List?recipientUserId={id}`
   (`AdminOnly`), regardless of whether the email actually went out.

## 4. Risk notes (where cases are weighted, and why)

- **Highest weight → `PUT /Preferences` validation + persistence (9 cases).** This is the only
  request-body, validated, multi-row, transactional write in scope. The distinct-category rule, the
  upsert-not-replace semantics, and the round-trip persistence are the likeliest places for a regression.
  The validator's *permissiveness* (single-category PUT is valid) is itself a risk surface — a frontend
  could send a partial set and silently leave other categories at their last-saved value, which BE-TC-09
  + BE-TC-11 pin down.
- **Authz / self-scoping (4 cases).** Both Preferences endpoints derive identity from the JWT only;
  the test must confirm user A's writes never leak into user B's reads, and that 401 is returned with no
  token. Low IDOR surface by design, but it is the security-critical invariant.
- **Email-delivery failure isolation (AC3/AC4).** The single most important *resilience* property is
  that an email failure never breaks registration or the notification-row write. Because the default test
  sender always succeeds, the *failure* path (BE-TC-22) needs a forced-failure sender or is otherwise
  marked **blocked** with the blocker noted — see Q4. Do not silently drop it.
- **Envelope inconsistency on the send endpoint.** `POST /api/notifications` returns bare
  `202 Accepted` / `400 + Error`, NOT the `BaseResponse<T>` envelope the rest of the API uses. Clients and
  tests must assert the *actual* shape (BE-TC-16/17), not the standard envelope.
- **Out-of-scope Notifications endpoints** (parent re-engagement prefs, devices, inbox) ship in the same
  controller/module but belong to P4-09 / P2-12 reengagement stories. They are **not** tested here to keep
  this run scoped to P1-13a + the self-preferences surface HANDOFF named. Flagged so the lead can spin a
  separate QC run if desired.

## 5. Open questions / assumptions (lead to resolve before implementation)

- **Q1 — "All 4 categories required" mismatch.** The prompt says the PUT validator "requires all 4
  categories distinct." The code requires **distinct + defined** but does **NOT** require all 4 to be
  present (a 1-category PUT is accepted). **Assumption taken:** test the *actual* code behaviour
  (BE-TC-09 asserts 200 for a valid partial set). If product intent is "all 4 required," that's a
  **backend defect** to file, not a test to assert green — confirm with lead.
- **Q2 — `SendNotificationCommand` validator is effectively dead.** It implements `IRequest<Result>`,
  not `ICommand<>`, so `ValidationBehavior` never invokes it; a POST with empty `Title`/`Body` or a
  malformed email reaches the handler unvalidated. BE-TC-20 documents this. Is the intended contract that
  `POST /api/notifications` **422s** on a bad body? If so, this is a backend gap (the command should be
  `ICommand<>` or the endpoint should validate). Lead call: assert-as-is, or file as a defect.
- **Q3 — AC2 (secrets-from-env) is not black-box assertable.** A running-API test cannot prove SMTP
  credentials come from env vs. `appsettings`. BE-TC-19 only asserts the committed `appsettings.json`
  ships placeholders / `Provider:"None"` (no secret committed). The substantive control is the
  security-auditor pass (already PASS). Acceptable? Or does the lead want a startup/integration assertion
  added elsewhere?
- **Q4 — Forcing the email-FAILURE path in tests.** The default test sender (`LogEmailSender`) always
  succeeds, so the 400/failure path (BE-TC-17/18) and the welcome-email-failure isolation (BE-TC-22) need
  either (a) a test `IEmailSender` double that returns `Result.Failure`, or (b) configuring
  `Email:Provider = Smtp` against an unreachable host in a dedicated test fixture. **Which does the
  harness support?** If neither is available yet, BE-TC-17/18/22 are marked **blocked (test-infra)** with
  the blocker recorded — not dropped.
- **Q5 — Is `IUserLookup` registered in the integration-test host?** If not, the welcome **email** is
  always skipped (logged), so BE-TC-21/22 can only assert the welcome **row** + registration success, not
  an actual outbound email. Assumption: row-level assertion is the contract for AC3 in the test env;
  confirm whether the harness wires an `IUserLookup` + a capturing email sender.
- **Q6 — Admin credentials / seed for `AdminOnly` endpoints.** `POST /api/notifications` and `GET
  /…/Notifications/List` require an Admin/SuperAdmin JWT. The cases assume the harness can mint/seed an
  admin token (as prior `P1_13_BE*` integration tests did). Confirm the seed helper is available.

## 6. Handoff

- **`backend-test-cases.md` → `api-tester`.** Implement each `BE-TC-*` 1:1 as an integration test against
  the running API (real Postgres per the existing harness). Seed via the API where possible (register
  users, mint admin token). Honour the §3 design notes — several expected results (422 not 400, 202 not
  enveloped, 4-not-7 categories, permissive PUT) are non-obvious and load-bearing.
- **`execution-report.md`** — empty template in this folder. `api-tester` fills pass/fail per case +
  defects **after** running. The QC architect never fills results.
- **`frontend-test-cases.md`** — intentionally **absent** (backend-only run).
- Cases marked **blocked** (per Q4/Q5 if the harness can't force a failure or capture email) must be
  recorded as blocked **with the blocker**, not silently passed or removed.
