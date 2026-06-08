# Backend Test Cases — P1-13a (Notifications email delivery + preferences)

> **Target agent:** `api-tester` — implement each case 1:1 as a running-API integration test (real
> Postgres, existing `LearnexiaWebAppFactory` harness). **Design-only document** — write results into
> `execution-report.md`, never here.
>
> **Read `README.md` §3 first** — several expected results are non-obvious and load-bearing
> (422 not 400; 202 bare body not enveloped; GET returns 4 not 7 categories; the PUT validator is
> *permissive* — does not require all 4; `SendNotificationCommand` is not auto-validated).
>
> **Routes verified against code:**
> - `GET  /api/Notifications/Preferences` (`PreferencesController.GetMyPreferences`, `[Authorize]`)
> - `PUT  /api/Notifications/Preferences` (`PreferencesController.UpdateMyPreferences`, `[Authorize]`)
> - `POST /api/notifications` (`NotificationsModule` minimal API, `RequireAuthorization(AdminOnly)`)
> - `GET  /api/Notifications/Notifications/List?recipientUserId={int}` (`NotificationsController.List`, `AdminOnly`)
>
> **Category enum (`NotificationCategory`):** WeeklyReport=0, StreakAtRisk=1, ProductAnnouncement=2,
> Achievement=3, DailyMissionReminder=4, LapseWinBack=5, System=6. **User-facing set = {0,1,2,3} only.**
>
> **25 cases:** BE-TC-01..18, 19, 20, 21..24, 25.

---

## Group A — `GET /api/Notifications/Preferences` (defaults + read-no-write)

### BE-TC-01 — First-read returns defaults for the 4 user-facing categories
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** a freshly registered, authenticated user (parent or child) with **no**
  preference rows yet.
- **Steps:**
  1. `GET /api/Notifications/Preferences` with the user's JWT.
- **Expected result:** `200`. Envelope `Successed: true`. `data.preferences` is an array of **exactly 4**
  items, one per user-facing category {WeeklyReport, StreakAtRisk, ProductAnnouncement, Achievement}.
  Defaults: `WeeklyReport` → `emailEnabled:true`; the other three → `emailEnabled:false`; **all four** →
  `pushEnabled:false`. Categories 4/5/6 are **absent**.
- **Traces to:** H1 (defaults on first read).

### BE-TC-02 — Default read does NOT persist any rows (no side effects)
- **Type:** persistence (negative side-effect) · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** fresh user with no preference rows.
- **Steps:**
  1. `GET /api/Notifications/Preferences` (returns synthesised defaults).
  2. Re-`GET /api/Notifications/Preferences`.
  3. (If a DB/inspection seam exists) confirm no `NotificationPreference` rows were written for the user.
- **Expected result:** Both GETs return identical default payloads (`200`, 4 items). No rows persisted by
  the read (the second GET still shows synthesised defaults, not stored values).
- **Traces to:** H1 ("nothing persisted on read").

### BE-TC-03 — GET never returns 404 for a user with no rows
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** fresh user, no rows.
- **Steps:**
  1. `GET /api/Notifications/Preferences`.
- **Expected result:** `200` (never `404`). Envelope present with 4 default items.
- **Traces to:** H1 ("never 404").

### BE-TC-04 — Unauthenticated GET is rejected
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** none (no Authorization header).
- **Steps:**
  1. `GET /api/Notifications/Preferences` with **no** JWT.
- **Expected result:** `401 Unauthorized`. No preference data returned.
- **Traces to:** H4 (authz).

---

## Group B — `PUT /api/Notifications/Preferences` (validation + persistence + authz)

### BE-TC-05 — Happy path: upsert all 4 categories, success envelope
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** authenticated user, no rows.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with body `{ "preferences": [ {category:0, emailEnabled:false,
     pushEnabled:true}, {category:1, emailEnabled:true, pushEnabled:false}, {category:2,
     emailEnabled:true, pushEnabled:true}, {category:3, emailEnabled:false, pushEnabled:false} ] }`.
- **Expected result:** `200`. Envelope `Successed: true`, success message in `data`/`message`.
- **Traces to:** H3 (persistence — happy write).

### BE-TC-06 — Validation: empty preferences list → 422
- **Type:** validation · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** authenticated user.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with body `{ "preferences": [] }`.
- **Expected result:** `422 UnprocessableEntity`. Envelope `Successed:false`, `message:"Validation
  Failed"`, `errors[]` non-empty (NotEmpty rule on `Preferences`). **Not** 400, **not** 200.
- **Traces to:** H2 (validation).

### BE-TC-07 — Validation: undefined category value → 422
- **Type:** validation / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** authenticated user.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with body containing an out-of-range category, e.g.
     `{ "preferences": [ {category:99, emailEnabled:true, pushEnabled:false} ] }`.
- **Expected result:** `422`. `errors[]` references the invalid-category rule
  (`NotificationPreferenceInvalidCategory`). No rows persisted.
- **Traces to:** H2 (categories must be defined).

### BE-TC-08 — Validation: duplicate categories → 422
- **Type:** validation / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** authenticated user.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with body `{ "preferences": [ {category:0, emailEnabled:true,
     pushEnabled:false}, {category:0, emailEnabled:false, pushEnabled:true} ] }` (category 0 twice).
- **Expected result:** `422`. `errors[]` references the duplicate-category rule
  (`NotificationPreferenceDuplicateCategory`). No rows persisted (transaction not entered).
- **Traces to:** H2 (categories must be distinct).

### BE-TC-09 — Behaviour-of-record: a valid PARTIAL set (1 category) is accepted (NOT 422)
- **Type:** boundary / behaviour-of-record · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** authenticated user. **See README §5 Q1** — the validator does NOT require all
  4 categories; a single distinct, defined category is valid. If product intent is "all 4 required," this
  is a **defect to file**, not a green assertion.
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with body `{ "preferences": [ {category:2, emailEnabled:true,
     pushEnabled:true} ] }` (only ProductAnnouncement).
- **Expected result:** `200` `Successed:true`. (Documents the permissive contract — flag in the report if
  it diverges from product intent.)
- **Traces to:** H2 (validation scope) + README Q1.

### BE-TC-10 — Persistence: saved values round-trip on the next GET
- **Type:** persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** authenticated user, no rows.
- **Steps:**
  1. `PUT` all 4 categories with distinctive flags (e.g. WeeklyReport email:false/push:true;
     StreakAtRisk email:true/push:true; ProductAnnouncement email:false/push:false; Achievement
     email:true/push:false).
  2. `GET /api/Notifications/Preferences`.
- **Expected result:** GET returns the **exact** Email/Push flags written in step 1 for all 4 categories
  (no longer the synthesised defaults). 4 items.
- **Traces to:** H3 (persistence round-trip).

### BE-TC-11 — Upsert is update-in-place, not replace; partial PUT leaves untouched categories at their stored value
- **Type:** persistence / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** authenticated user.
- **Steps:**
  1. `PUT` all 4 categories with known flags (call this state S1).
  2. `PUT` only category 0 (WeeklyReport) with a changed flag (e.g. push toggled).
  3. `GET /api/Notifications/Preferences`.
- **Expected result:** GET shows category 0 with the **new** value from step 2; categories 1/2/3 retain
  their **S1** stored values (the partial PUT updates only the supplied row, does not reset the others to
  defaults). Confirms upsert/update-in-place semantics.
- **Traces to:** H3 (persistence) + upsert semantics.

### BE-TC-12 — Authz: prefs are self-scoped — user B never sees user A's writes (no IDOR)
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** two distinct authenticated users A and B (no shared rows).
- **Steps:**
  1. As **A**: `PUT` all 4 categories with non-default flags.
  2. As **B**: `GET /api/Notifications/Preferences`.
- **Expected result:** B's GET returns the **synthesised defaults** (4 items, unaffected by A's write) —
  identity is taken from the JWT, there is no body/route parameter to spoof another user's prefs. A's GET
  still shows A's saved values.
- **Traces to:** H4 (own prefs only, no IDOR surface).

### BE-TC-13 — Unauthenticated PUT is rejected
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** none (no token).
- **Steps:**
  1. `PUT /api/Notifications/Preferences` with a valid body but **no** JWT.
- **Expected result:** `401 Unauthorized`. No rows persisted.
- **Traces to:** H4 (authz).

---

## Group C — `POST /api/notifications` (email-delivery surface; AdminOnly)

> Default test env uses the dev **log sink** (`Email:Provider = None`) → `SendAsync` always succeeds.

### BE-TC-14 — Send with recipient email succeeds (202 Accepted)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** an authenticated **Admin/SuperAdmin** JWT.
- **Steps:**
  1. `POST /api/notifications` with body `{ recipientUserId:"<guid>", title:"Hello", body:"Body text",
     notificationTypeId:"<guid>", notificationModuleId:null, recipientEmail:"parent@example.com" }`.
- **Expected result:** `202 Accepted` with an **empty body** (`Results.Accepted()` — NOT a
  `BaseResponse<T>` envelope). The dev log sink records the (masked) send.
- **Traces to:** AC1 (config-driven send via `IEmailSender`).

### BE-TC-15 — Send with NULL recipient email is skipped but still succeeds (202)
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Admin JWT.
- **Steps:**
  1. `POST /api/notifications` with `recipientEmail` omitted / `null` (other fields valid).
- **Expected result:** `202 Accepted`, empty body. The handler logs "no recipient email … skipping" and
  does NOT call the sender; the command still succeeds.
- **Traces to:** AC1 (email step optional; in-app path unaffected).

### BE-TC-16 — Send success returns bare 202, NOT the BaseResponse envelope
- **Type:** functional / contract · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Admin JWT.
- **Steps:**
  1. `POST /api/notifications` with a valid body + recipient email.
  2. Inspect the raw response shape.
- **Expected result:** `202`, body is empty (no `successed`/`data`/`statusCode` keys). Documents the
  deliberate envelope divergence on this minimal-API endpoint so clients/tests assert the real shape.
- **Traces to:** AC1 / AC4 (contract of record).

### BE-TC-17 — Send FAILURE returns 400 with generic Error (no envelope), no provider internals
- **Type:** negative / resilience · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Admin JWT **and** a sender that returns `Result.Failure` — see README §5 Q4
  (test `IEmailSender` double, or `Email:Provider=Smtp` pointed at an unreachable host). **If the harness
  cannot force a failure, mark this case BLOCKED (test-infra) with the blocker — do NOT pass it.**
- **Steps:**
  1. Configure/inject a failing email sender.
  2. `POST /api/notifications` with a valid body + recipient email.
- **Expected result:** `400 BadRequest`. Body carries only `result.Error` (a `{code, message}` shape,
  e.g. `Email.SendFailed`) — **no** SMTP exception text, stack trace, host, or provider internals.
- **Traces to:** AC4 (no-leak failure path).

### BE-TC-18 — Failure path leaks no provider internals (assert on error shape)
- **Type:** auth-authz / security · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** as BE-TC-17 (forced failure). **Same blocker as BE-TC-17 if unforceable.**
- **Steps:**
  1. Trigger a send failure (per BE-TC-17).
  2. Inspect the response body and headers.
- **Expected result:** The returned `Error.message`/`code` is a static generic value; the response
  contains **no** SMTP host/port, credentials, exception type, or stack trace. (Server-side logging of
  detail is out of black-box scope.)
- **Traces to:** AC4 / AC5 (no info leakage).

### BE-TC-20 — Behaviour-of-record: malformed body is NOT auto-validated (no 422 on this endpoint)
- **Type:** boundary / behaviour-of-record · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** Admin JWT. **See README §5 Q2** — `SendNotificationCommand` is
  `IRequest<Result>`, not `ICommand<>`, so `ValidationBehavior` (and `SendNotificationCommandValidator`)
  does NOT fire. If "POST must 422 on a bad body" is the intended contract, this is a **defect to file**.
- **Steps:**
  1. `POST /api/notifications` with an empty `title` and `body` (and a recipient email) under the dev log
     sink.
- **Expected result:** With the dev log sink the request still succeeds → `202 Accepted` (validation does
  **not** reject it; no 422). Documents that the endpoint does not enforce body validation. Flag in the
  report against README Q2.
- **Traces to:** README Q2 (validator-not-wired finding).

### BE-TC-25 — Anonymous / non-admin send is rejected (AdminOnly)
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** (a) no JWT; (b) a non-admin (parent/child) JWT.
- **Steps:**
  1. `POST /api/notifications` with a valid body and **no** token.
  2. `POST /api/notifications` with a valid body and a **non-admin** token.
- **Expected result:** (a) `401`; (b) `403`. No send occurs in either case.
- **Traces to:** AC5 / authz (send surface is `AdminOnly`, not an unauthenticated send vector).

---

## Group D — Welcome email on `UserRegistered` (best-effort, failure-isolated)

> Observed via registration + the `AdminOnly` notifications-list read. **See README §5 Q5** — if
> `IUserLookup` is unregistered in the test host the outbound email is skipped (logged); the in-app
> welcome **row** is always written and is the observable contract for AC3 in the test env.

### BE-TC-21 — Registration writes a welcome notification row + does not fail
- **Type:** persistence / functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** an Admin JWT (to read the list); the registration endpoint available.
- **Steps:**
  1. Register a new user via the Identity registration endpoint; capture the new user's integer id.
  2. (Allow for eventual-consistency fan-out; poll if the harness supports it.)
  3. As Admin: `GET /api/Notifications/Notifications/List?recipientUserId={newUserId}`.
- **Expected result:** Registration returns success. The list returns **exactly one** welcome
  notification for the new user (`title:"Welcome to Learnexia"`, body containing the user name). The
  notification-row write is preserved regardless of email outcome.
- **Traces to:** AC3 (welcome message delivered; notification-row write kept).

### BE-TC-22 — Welcome email failure is isolated — registration + row still succeed
- **Type:** resilience / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** a failing `IEmailSender` (+ a registered `IUserLookup` returning an email)
  so the welcome-email send path is exercised and fails — see README §5 Q4/Q5. **If the harness cannot
  force the email path + a failure, mark BLOCKED (test-infra) with the blocker — do NOT pass.**
- **Steps:**
  1. Configure the failing sender + a user-lookup that resolves an email.
  2. Register a new user.
  3. As Admin: `GET /api/Notifications/Notifications/List?recipientUserId={newUserId}`.
- **Expected result:** Registration **still succeeds** (no 5xx, user is created). The welcome
  notification **row is still written** (list returns 1 item). The email failure is swallowed + logged
  server-side and never surfaces to the registration caller.
- **Traces to:** AC3 (best-effort; failure does not fail registration) / AC4 (isolation).

### BE-TC-23 — Welcome notification is idempotent — redelivery does not duplicate
- **Type:** boundary / idempotency · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Admin JWT; ability to re-publish / re-deliver the `UserRegistered` event, or
  re-trigger the consumer. **If redelivery cannot be triggered by the harness, mark BLOCKED with the
  blocker.**
- **Steps:**
  1. Register a user (welcome row created).
  2. Cause a second delivery of `UserRegisteredIntegrationEvent` for the same user id (replay).
  3. As Admin: `GET /api/Notifications/Notifications/List?recipientUserId={userId}`.
- **Expected result:** Still **exactly one** welcome notification (the `AnyAsync` idempotency guard
  prevents a duplicate). No second welcome row, no error.
- **Traces to:** AC3 (idempotent-friendly welcome write).

---

## Group E — Cross-cutting config / envelope

### BE-TC-19 — Committed config ships no secret + dev sender is the log sink
- **Type:** validation / config (behaviour-of-record) · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** access to the running host's effective email config (or assert via behaviour).
  **See README §5 Q3** — AC2 (secrets-from-env) is only weakly black-box assertable.
- **Steps:**
  1. Confirm the test/dev host resolves `LogEmailSender` (e.g. a send with a recipient succeeds as a
     no-op, BE-TC-14) — i.e. `Email:Provider` is `None`/unset in the default env.
  2. (Static) confirm committed `appsettings.json` `Email` section ships empty `Host`/`UserName`/
     `Password` placeholders and `Provider:"None"` (no secret committed).
- **Expected result:** Dev/test path uses the no-op log sink (no SMTP contacted); committed config holds
  only placeholders. Substantive secrets-from-env control is covered by the security-auditor PASS.
- **Traces to:** AC2 (secrets not committed) + README Q3.

### BE-TC-24 — Preferences endpoints return the standard BaseResponse<T> envelope with `Successed`
- **Type:** functional / contract · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** authenticated user.
- **Steps:**
  1. `GET /api/Notifications/Preferences`.
  2. `PUT /api/Notifications/Preferences` with a valid body.
- **Expected result:** Both responses are `BaseResponse<T>`-shaped (camelCase): `successed:true`,
  `statusCode`, `message`, `data`. Confirms the spelling **`successed`** (not `succeeded`/`success`) and
  envelope consistency on the Preferences surface (contrast BE-TC-16: the send endpoint is intentionally
  NOT enveloped).
- **Traces to:** API contract (`BaseResponse<T>`, `Successed` spelling).

---

## Implementation checklist for `api-tester`
- [ ] All **25** cases above implemented 1:1 (BE-TC-01..18, 19, 20, 21..24, 25).
- [ ] Expected statuses honoured: **422** (not 400) on PUT validation; **202** (not enveloped) on send
      success; **400 + Error** on send failure; **401/403** on the AdminOnly send + the no-token cases.
- [ ] BE-TC-17/18/22/23 marked **BLOCKED with blocker** if the harness cannot force a sender failure /
      welcome-email path / event redelivery — never silently passed.
- [ ] Findings against README Q1 (permissive PUT) and Q2 (unvalidated send command) reported in
      `execution-report.md` defects, not asserted as product-correct.
- [ ] Results written to `execution-report.md` only.
