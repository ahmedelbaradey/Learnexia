# Pipeline Brief — P6-06 Backend security hardening (stabilization)

## Summary & traceability
- **Task (1 line):** Close the four non-blocking Phase-1 auth-audit follow-ups during stabilization — forgot-password timing oracle, reset-email localization, transport/secret hygiene, and a multi-instance-safe (Redis) rate-limit store — then a mandatory security-auditor pass.
- **User story (source of truth):** [user-stories/Phase-6-Stabilization/P6-06-backend-security-hardening.md](../../user-stories/Phase-6-Stabilization/P6-06-backend-security-hardening.md)
- **Task file:** [tasks/Backend/Phase-6-Stabilization/P6-06-BE.md](../../tasks/Backend/Phase-6-Stabilization/P6-06-BE.md) — BE-1..BE-5.
- **Source per-PR briefs:** [docs/briefs/P1-12-password-reset-security-audit.md](P1-12-password-reset-security-audit.md) (Finding #1 = the timing oracle), [docs/briefs/P1-13a-security-audit.md](P1-13a-security-audit.md) (email-delivery path).
- **Requirements:** NFR-4 (security), NFR-1 (performance/scale), FR-ID-1/4. **BRD goal:** G2 (trust/safety of the auth surface ahead of launch).
- **Phase/Epic:** Phase 6 — Stabilization (Week 9) · Epic: Stabilization & Hardening · SP 8.
- **Modules:** Identity + Notifications + Host. Cross-module only via `Shared.Contracts` (BE-2 adds a `Locale` field to the reset event). No cross-module FK. No Unit of Work. No migration.

> **SCOPE FLAG (must confirm with lead):** the story `.md` carries a **sixth** acceptance criterion — **G2 access-token revocation on reset/sign-out** (SessionId per-request `JwtBearerEvents.OnTokenValidated` validation). The **task file P6-06-BE.md only lists BE-1..BE-5** (timing oracle, email locale, transport/secret, Redis store, auditor) and the assignment for this brief is those five. **This brief covers BE-1..BE-5 only.** The G2 token-revocation work is materially larger (load-bearing auth, per-request session lookup, all-sessions termination on anonymous reset) and is NOT decomposed into a task row. Recommend the lead either (a) split G2 into its own story/tasks, or (b) explicitly fold it in and re-scope this story's SP. Do not start G2 until that is resolved.

## Business context & value
- **Who benefits:** parents (account holders) + admins (platform owner). Students don't self-register, so the auth surface is parent-facing.
- **Value:** the auth surface ships launch-hardened. (1) An attacker can no longer enumerate which emails are registered by timing the forgot-password response. (2) Arabic-first parents receive the reset email in their own language (the platform's Arabic-first promise, BRD §8 — already delivered for the welcome email in P9-10). (3) A misconfigured prod deploy can't silently accept tokens over plain HTTP or ship a committed DB password. (4) Rate limits actually hold once the app is horizontally scaled (today the in-memory counters are per-instance, so N instances = N× the intended ceiling).
- **Success measure:** registered-vs-unknown forgot-password latency is statistically indistinguishable; reset email renders ar/en; `RequireHttpsMetadata` true in prod + DB secret env-sourced; rate-limit counters shared across instances via Redis; security-auditor returns no Critical/High.

## Acceptance criteria (testable)
- **BE-1 — timing oracle closed:** forgot-password no longer awaits token-mint + SMTP send inside the request; a registered email and an unknown email return in indistinguishable time. Anti-enumeration body/status (the same `Success<string>(ForgotPasswordGenericResponse)` 200 on every path) stays intact. A reset-email failure must NOT change the response (it's out-of-band) and must NOT 500 the request.
- **BE-2 — reset email localized:** the password-reset email subject + body render in the recipient's `PreferredLanguage` (ar-EG / en-US), mirroring the P9-10 welcome path. (Welcome email is **already localized** — see resolution C — so BE-2 is **reset-only**.)
- **BE-3 — transport/secret hygiene:** `RequireHttpsMetadata=false` is gated to Development/Testing only (true in Production/Staging); the DB connection password is env-sourced in non-Development; the required prod env vars are documented in HANDOFF.
- **BE-4 — multi-instance rate-limit store:** the auth rate-limiting uses a Redis-backed `IRateLimitCounterStore`/`IIpPolicyStore` when a Redis connection is configured, with the existing in-memory store as the fallback when Redis is absent (dev/tests). Existing rules + env-gated limits unchanged.
- **BE-5 — security-auditor PASS:** Critical/High block the gate.
- **Regression guard:** the existing forgot/reset flow, token generation, session/refresh-token revocation on reset, and the integration suite (env `Testing`) all still pass unchanged.

## Affected modules & data
- **No new entities, no schema change, no migration.** This is config/wiring + one additive `Shared.Contracts` event field + two copy-template strings.
- **Changed surface:**
  - **Shared.Contracts:** add `string? Locale` to `PasswordResetRequestedIntegrationEvent` (additive, nullable, backward-safe).
  - **Identity:** `ForgotPasswordCommandHandler` (BE-1 out-of-band dispatch + BE-2 resolve `Locale` at emit time); `Identity.Infrastructure/DependencyInjection.cs` (BE-3 gate `RequireHttpsMetadata`).
  - **Notifications:** `PasswordResetRequestedIntegrationEventHandler` (BE-2 localized render); `ReengagementCopyTemplates` (BE-2 add `System:PASSWORD_RESET` ar/en entries).
  - **Host:** `Extensions/ServiceExtensions.cs` (BE-4 Redis store wiring); `appsettings.json` / env (BE-3 DB secret); `Directory.Packages.props` (BE-4 new package).

---

## BE-1 — Forgot-password timing oracle (grounded in code)

**Confirmed mechanism (the oracle is real):**
- `ForgotPasswordCommandHandler.Handle` (`ForgotPasswordCommandHandler.cs:49-76`) returns the SAME `Success<string>(_localizer[ForgotPasswordGenericResponse])` on every path (unknown/inactive → line 63; real user → line 67; exception → line 74). **Body/status anti-enumeration is intact.**
- BUT for a real, active user it calls `await PublishPasswordResetRequestedEventAsync` (line 66), which does `await GeneratePasswordResetTokenAsync` (HMAC/data-protection, `ForgotPasswordCommandHandler.cs:82`) **and** `await _publisher.Publish(integrationEvent)` (`:92`).
- `_publisher` is the unified cross-module MediatR `IPublisher` (`MediatRExtensions.cs:22-67`) using `IsolatedNotificationPublisher` (`IsolatedNotificationPublisher.cs:26-45`). That publisher **`await`s each handler's `HandlerCallback` inline** — it isolates exceptions but is fully synchronous w.r.t. the calling request.
- So `PasswordResetRequestedIntegrationEventHandler.Handle` (`PasswordResetRequestedIntegrationEventHandler.cs:40-68`) → `await _emailSender.SendAsync(...)` (`:56`, SMTP I/O) **runs inside the forgot-password request** before the 200 is written. Result: real-user response time = token-mint + SMTP latency; unknown-email response time ≈ a single `FindByEmailAsync`. That delta is the enumeration oracle (audit Finding #1).
- **No constant-time floor / dummy-hash exists on this path.** (The HANDOFF "timing-oracle dummy-hash" note refers to the **sign-in** path, not forgot-password — verified: no `Stopwatch`/`Task.Delay`/dummy-hash in `ForgotPasswordCommandHandler`.)

**Welcome-email dispatch (the shape to mirror):** registration (`RegisterParentCommandHandler.PublishUserRegisteredEventAsync`, `:133-151`) also uses inline `await _publisher.Publish(UserRegisteredIntegrationEvent)`. Welcome being inline is acceptable there (registration is not an anonymous-enumeration surface and already does heavy DB writes); forgot-password being inline is the problem.

**Fix (out-of-band dispatch) — established pattern, NOT a new one (rule 8):** the codebase already has a precedented "fire-and-forget background write with a fresh DI scope" pattern: the AI-cache write uses `Task.Run` + `IServiceScopeFactory.CreateAsyncScope()` (HANDOFF "AI cache write ObjectDisposedException" fix; P7-11 lead-approved "fire-and-forget background write"). Apply the same to the reset-email dispatch:
- In `PublishPasswordResetRequestedEventAsync`, mint the token + build the URL + build the event inline (cheap; token-mint is in-process HMAC, NOT the SMTP I/O — keeping it inline is fine and keeps the token off any queue/log), then dispatch the **publish** out-of-band: `Task.Run` that opens a fresh `IServiceScopeFactory.CreateAsyncScope()`, resolves `IPublisher` from that scope, and `await`s `Publish`. The handler returns the generic 200 immediately without awaiting the SMTP send.
- Wrap the background body in try/catch with `ILoggerManager` (isolated logging) — never log the token/URL (mirrors the existing `:94-100` rule).
- Inject `IServiceScopeFactory` into `ForgotPasswordCommandHandler` (mirrors `GetHintCommandHandler` / `ExplainConceptCommandHandler`).

**Decision point for the lead (B in resolutions):** out-of-band via `Task.Run`+fresh-scope is the lowest-risk, already-precedented option. The audit also offered a "constant-time floor" alternative — NOT recommended (it adds latency to every request and is brittle). A full outbox/`IHostedService` queue is over-engineering for one email and would be a new pattern (rule 8 → would need explicit sign-off). **Recommend the `Task.Run`+fresh-scope decouple.**

**Risk (the single biggest one):** the decouple must not break token generation / the existing reset flow. Because the token is minted **before** the background dispatch and embedded in the URL inside the event, this is preserved. The fresh-scope `IPublisher` must be resolved from the new scope (not captured from the request scope) or it will dispose mid-send — exactly the bug the AI-cache fix documents. The handler's own try/catch (`:69-75`) already returns the generic success on any synchronous failure, so even a `Task.Run` scheduling failure can't leak an oracle.

## BE-2 — Reset email localization (grounded in code)

**Confirmed contract gap:** `PasswordResetRequestedIntegrationEvent` (`PasswordResetRequestedIntegrationEvent.cs:10-15`) carries `(EventId, OccurredOnUtc, Email, ResetUrl, UserName?)` — **NO `UserId`, NO `Locale`.** So the consumer cannot use `IUserLookup.FindByIdAsync` (by-id only, `IUserLookup.cs:5`) to resolve a locale the way the welcome handler does. (HANDOFF line 137 records this exact deferral to P6-06.)

**Confirmed welcome render mechanism (the shape to mirror):** `UserRegisteredIntegrationEventHandler` (`:53-65`) resolves locale via `ReengagementHandlerHelper.GetLocaleAsync(userLookup, UserId, ct)` (reads `UserSummary.PreferredLanguage`, falls back `ar-EG`; `ReengagementHandlerHelper.cs:80-97`) then renders from `ReengagementCopyTemplates.Render(NotificationCategory.System, "WELCOME", locale, ("userName", ...))` (`ReengagementCopyTemplates.cs:140-145`) and uses the rendered `(title, body)` as the email subject + body. The reset email currently uses a hardcoded English `EmailSubject` + interpolated English body (`PasswordResetRequestedIntegrationEventHandler.cs:27, 48-54`).

**Resolved approach (locale source = add `Locale` to the event):**
1. **Shared.Contracts:** add `string? Locale` to `PasswordResetRequestedIntegrationEvent` (additive nullable record param). Identity resolves it at **emit time** — it already has the full `User` in `PublishPasswordResetRequestedEventAsync` (`ForgotPasswordCommandHandler.cs:78-101`), so set `Locale: user.PreferredLanguage` (no extra lookup; cleaner than the welcome path which has only a UserId). Consumer falls back to `ar-EG` when null.
2. **Notifications copy template (mirror welcome):** add two entries to `ReengagementCopyTemplates.Templates` — `System:PASSWORD_RESET:ar-EG` and `System:PASSWORD_RESET:en-US` — with placeholders `{userName}` and `{resetUrl}`. The body keeps the existing HTML `<a href="{resetUrl}">...</a>` shape. `Render` does plain `string.Replace`, so the URL placeholder substitutes correctly. Child-safe/encouraging copy not required here (transactional), but keep it Arabic-first.
3. **Consumer:** `PasswordResetRequestedIntegrationEventHandler.Handle` resolves `locale = notification.Locale ?? "ar-EG"` (or via `ReengagementCopyTemplates`'s own `ar`/`en` resolution), renders `(subject, body) = ReengagementCopyTemplates.Render(NotificationCategory.System, "PASSWORD_RESET", locale, ("userName", greetingName), ("resetUrl", notification.ResetUrl))`, then `await _emailSender.SendAsync(notification.Email, subject, body, ct)`. Keep the existing best-effort try/catch (`:46-67`) and the "never log the URL/token" rule (`:42-44`).

**Localization mechanism note:** the welcome path uses the `ReengagementCopyTemplates` static dictionary (in `Notifications.Domain`), **NOT** resx/`IStringLocalizer`. Mirror that exactly for the reset email — do NOT introduce `IStringLocalizer` into this email path (it would diverge from P9-10 and the rule-8 "mirror existing shapes" requirement). The story AC text mentions "the localizer" generically; the **established localizer for Notifications copy is `ReengagementCopyTemplates`**. (The resx `SharedResourcesKey` reset keys at `SharedResourcesKey.cs:213-215, 731-732` are API **response** strings, not email copy — leave them alone.)

**Welcome is already done (resolution C):** `UserRegisteredIntegrationEventHandler` (`:53-82`) already localizes the welcome inbox **and** email via `GetLocaleAsync` + `System:WELCOME` template (P9-10). The reset handler's own comment (`PasswordResetRequestedIntegrationEventHandler.cs:25-26`) records the deferral. **BE-2 is reset-only — do not re-touch the welcome path.**

## BE-3 — Transport / secret hygiene (grounded in code)

- **`RequireHttpsMetadata`:** set unconditionally to `false` at `Identity.Infrastructure/DependencyInjection.cs:173` inside the `AddJwtBearer(x => { ... })` lambda. The `AddIdentityService` method already has `IConfiguration configuration` in scope and a reusable helper `IsProtectedEnvironment(IConfiguration)` (`DependencyInjection.cs:297-307`, defaults to Production fail-closed). **Fix:** `x.RequireHttpsMetadata = IsProtectedEnvironment(configuration);` → true in Production/Staging, false in Development/Testing (so dev + the integration suite over HTTP are unaffected). Mirror `GuardJwtSecret`/`GuardCaptcha` env resolution exactly.
- **DB connection secret:** `appsettings.json:3` ships `ConnectionStrings:Default = Host=localhost;...;Password=admin` (committed). `AddDbContext` reads `configuration.GetConnectionString("default")` (`DependencyInjection.cs:115`). ASP.NET config already supports env override of the whole connection string via `ConnectionStrings__Default`. **Recommended approach (lowest-risk, no code change to the read path):** document that prod supplies `ConnectionStrings__Default` (or the password component) via env/secret store; keep the committed value as the dev placeholder. If the lead wants an active guard, add a `GuardDbSecret` startup check (mirror `GuardJwtSecret`) that throws in Production/Staging when the connection string still contains the committed `Password=admin` or is unset — **flag as an optional sub-task**, not assumed. The remote shared DB already keeps its real connection string only in gitignored `appsettings.Development.local.json` (HANDOFF), so the override mechanism is proven.
- **Prod env vars to document in HANDOFF (resolution D):**
  | Env var | Purpose |
  |---|---|
  | `ConnectionStrings__Default` | Postgres connection string incl. real password (replaces the committed `Password=admin`). |
  | `ConnectionStrings__Redis` | Redis endpoint (e.g. `redis:6379`) — also activates the BE-4 Redis rate-limit store + distributed cache + `IConnectionMultiplexer`. |
  | `JwtSettings__Secret` | Strong signing key (`GuardJwtSecret` throws in Prod/Staging if unset/placeholder). |
  | `ClientAppBaseUrl` | HTTPS origin for reset links (audit P1-12 Finding #4 — must be https + set in prod). |
  | `Captcha__Enabled` + `Captcha__SecretKey` | `GuardCaptcha` throws in Prod/Staging unless both set. |
  | `Email__Provider` (=`Smtp`) + `Email__Host` / `Email__UserName` / `Email__Password` / `Email__FromAddress` | SMTP for the (now localized) reset + welcome emails. |
  | `Ai__Providers__Claude__ApiKey` / `Ai__Providers__OpenAi__ApiKey`, `Curriculum__Embedding__AuthToken`, `MinIOConfiguration__*` | Pre-existing prod secrets (already in HANDOFF; list for completeness). |
  | `ASPNETCORE_ENVIRONMENT=Production` (or `Staging`) | Drives every env-gate above incl. the new `RequireHttpsMetadata`. |

## BE-4 — Multi-instance (Redis) rate-limit store (grounded in code)

**Confirmed current setup:** `ConfigureRateLimitingOptions` (`ServiceExtensions.cs:29-80`) builds env-gated `IpRateLimitOptions` and registers the **in-memory** stores:
```
services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();  // :76
services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();                  // :77
services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();           // :78
services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();       // :79
```
Middleware: `app.UseIpRateLimiting()` (`Program.cs:236`). Package: `AspNetCoreRateLimit` **5.0.0** (`Directory.Packages.props:84`, central versioning confirmed via `ManagePackageVersionsCentrally=true`, `:3`).

**Confirmed Redis wiring to reuse:** Program.cs (`:63-77`) already registers `IConnectionMultiplexer` as a **singleton, gated on `ConnectionStrings:Redis` being non-empty**, alongside `AddStackExchangeRedisCache`; otherwise it falls back to `AddDistributedMemoryCache`. `StackExchange.Redis` **2.7.27** is centrally pinned (`Directory.Packages.props:60`). This is the exact "Redis when present, in-memory fallback when absent" pattern BE-4 must mirror.

**Resolved approach (resolution B):**
1. **New package (BE-4 only):** add `AspNetCoreRateLimit.Redis` to `Directory.Packages.props` (centrally versioned). Pick the version compatible with `AspNetCoreRateLimit 5.0.0` (the 2.x line of `AspNetCoreRateLimit.Redis` targets the 5.x core — the planner/implementer should pin the matching version and verify restore). Reference it from `Host.csproj`.
2. **Gate the store choice on the Redis connection string** (same gate as Program.cs `:63-64`). Because `ConfigureRateLimitingOptions` runs at `Program.cs:46` — **before** the `IConnectionMultiplexer` registration at `:63-77` — the cleanest options are: (a) move/keep the Redis-store registration so it can resolve `IConnectionMultiplexer` lazily (the `AspNetCoreRateLimit.Redis` stores take `IConnectionMultiplexer` via DI, resolved at request time, so registration order of the singleton vs the store doesn't matter as long as both are registered before the app builds); or (b) pass the Redis connection string into `ConfigureRateLimitingOptions` and branch there. **Recommend:** branch inside `ConfigureRateLimitingOptions` on `configuration.GetConnectionString("Redis")` — when non-empty register `RedisRateLimitCounterStore` + `RedisProcessingStrategy` (and the Redis IP-policy store) which depend on the DI `IConnectionMultiplexer`; when empty keep the existing `MemoryCache*` registrations verbatim. This keeps dev/tests (no Redis) on the in-memory store unchanged, so the integration suite (`RateLimitWebAppFactory`, env `Testing`, no Redis) is unaffected.
3. **Do not change the rules or env-gated limits** (`:44-67`) — only the backing store.

**Open confirmation (resolution B caveat):** the task's own open decision asks to confirm a Redis connection is provisioned in staging/prod. HANDOFF EXT-5 lists `ConnectionStrings:Redis` as a devops item with an in-process fallback. The fallback design means BE-4 is safe to ship before Redis is provisioned (dev/staging without Redis simply stays in-memory). Document in HANDOFF that **multi-instance correctness only holds once `ConnectionStrings__Redis` is set in prod.**

---

## Handoff → db-migration
**None.** No new entities, no schema change, no new columns. BE-2's `Locale` is an in-memory `Shared.Contracts` event field (transient, never persisted). Do **not** dispatch db-migration for this story.

## Handoff → backend-feature
- **Shared.Contracts:** `PasswordResetRequestedIntegrationEvent` — add `string? Locale` (additive nullable record param; default null; update the XML doc to note it's the recipient `PreferredLanguage` resolved at emit time).
- **Identity (`ForgotPasswordCommandHandler.cs`):**
  - Inject `IServiceScopeFactory`.
  - BE-1: dispatch `_publisher.Publish` out-of-band via `Task.Run` + fresh `CreateAsyncScope()` (resolve `IPublisher` from the new scope), isolated try/catch + `ILoggerManager`, never log token/URL. Mint token + build URL + build event BEFORE the background dispatch.
  - BE-2: set `Locale: user.PreferredLanguage` on the event.
  - Keep the generic-success-on-every-path contract (`:52, 63, 67, 74`) intact.
- **Identity (`Infrastructure/DependencyInjection.cs:173`):** `x.RequireHttpsMetadata = IsProtectedEnvironment(configuration);`.
- **Notifications (`PasswordResetRequestedIntegrationEventHandler.cs`):** resolve `locale = notification.Locale ?? "ar-EG"`, render subject+body from `ReengagementCopyTemplates.Render(NotificationCategory.System, "PASSWORD_RESET", locale, ("userName", greetingName), ("resetUrl", notification.ResetUrl))`, send via `IEmailSender.SendAsync`; keep best-effort try/catch + no-log-URL.
- **Notifications (`ReengagementCopyTemplates.cs`):** add `System:PASSWORD_RESET:ar-EG` + `:en-US` entries (placeholders `{userName}`, `{resetUrl}`; keep the `<a href="{resetUrl}">` shape; Arabic-first).
- **Host (`ServiceExtensions.cs` `ConfigureRateLimitingOptions`):** branch on `ConnectionStrings:Redis` — Redis stores when present, existing `MemoryCache*` when absent.
- **Host (`Directory.Packages.props` + `Host.csproj`):** add `AspNetCoreRateLimit.Redis` (centrally versioned, version-matched to core 5.0.0).
- **DB secret (BE-3):** env-source `ConnectionStrings__Default` in non-Development; optional `GuardDbSecret` startup guard (flag to lead before adding).
- **Validation:** no new commands/queries → no new validators. `ForgotPasswordCommand` is unchanged.
- **No new endpoints, no DTO changes.** (The forgot-password endpoint response payload is unchanged — same localized generic string.)

## Handoff → frontend
**None.** Backend-only story (Identity + Notifications + Host config/wiring). No FE task file exists for P6-06; do not dispatch designer/frontend.

## Handoff → api-tester (BE-5 prep)
- HTTP surface touched = the existing `POST /api/users/authentication/forgot-password` (behavior preserved; timing decoupled). Add/extend integration tests: (1) forgot-password still returns the generic 200 for known + unknown emails (env `Testing`); (2) a reset email is dispatched out-of-band for a real active user (assert via the test `IEmailSender`/log sink, not by timing); (3) reset email renders ar vs en per the user's `PreferredLanguage`; (4) rate-limit store still enforces (existing `RateLimitWebAppFactory` path, in-memory in `Testing` — Redis path covered by a `Testcontainers.Redis`-backed test if feasible). Reuse the existing `P1_13b_*`, `P1_12_*`, and `FrontendRTL_UpdateLanguage_Tests` shapes.

---

## Open questions / assumptions / risks
1. **G2 token revocation (BIGGEST SCOPE QUESTION):** the story `.md` lists a 6th AC (SessionId per-request validation on reset/sign-out) that is **not** in the BE-1..BE-5 task rows. Confirm with the lead whether G2 is in-scope for this build or split to its own story. This brief excludes it.
2. **BE-3 DB-secret enforcement (assumption):** assumed = "env-source + document", NOT an active startup guard, unless the lead wants `GuardDbSecret`. Confirm.
3. **BE-4 package version:** `AspNetCoreRateLimit.Redis` version must be matched to `AspNetCoreRateLimit 5.0.0`; the implementer must verify restore + that the Redis stores resolve the existing DI `IConnectionMultiplexer` (not a second connection). Risk: a version mismatch or a second multiplexer. Mitigation: reuse the Program.cs `IConnectionMultiplexer` singleton.
4. **BE-1 biggest risk (call out):** the out-of-band decouple must NOT break token generation or the existing reset flow / refresh-token revocation. The `IPublisher` MUST be resolved from the **fresh** `CreateAsyncScope()` (not captured from the request scope) — otherwise the scope disposes mid-send (the documented AI-cache `ObjectDisposedException` trap). Token is minted inline before the background dispatch so it's never lost; the handler's existing catch-all keeps the generic 200 on any failure, so the anti-enumeration guarantee can't regress.
5. **Email copy review:** the new `PASSWORD_RESET` ar/en template strings are user-facing transactional copy — flag for the lead/copy review (Arabic phrasing).
6. **Risk — leaving welcome inline (accepted):** registration's welcome email stays inline `await Publish`; that's intentional (not an anonymous-enumeration surface). If a future audit wants registration latency-flat too, decouple it the same way — out of scope here.

## Recommended pipeline order (first cut — planner finalizes)
- **No migration, no UI** → skip db-migration, designer, frontend, frontend-e2e-tester.
- **Batch 1 (parallel-safe within the story):** `backend-feature` does BE-1..BE-4. They touch mostly disjoint files; **serialize the shared-file edits**: `Directory.Packages.props` + `Host.csproj` (BE-4) and `Host/Extensions/ServiceExtensions.cs` (BE-4) are Host-shared — keep BE-4's Host edits in one focused change; BE-1/BE-2 (Identity + Notifications + Shared.Contracts) and BE-3 (Identity DI) can land together. BE-1 and BE-2 both edit `ForgotPasswordCommandHandler.cs` → do them in the same change, not in parallel.
- **Batch 2 (gate):** `api-tester` validates the running API (forgot-password timing-decoupled behavior, ar/en reset email, rate-limit enforcement).
- **Batch 3 (MANDATORY, security-sensitive — auth + secrets):** `security-auditor` (BE-5) — auth/secret/rate-limit changes; Critical/High block.
- **Batch 4 (gate):** `reviewer` against this brief's ACs + CONVENTIONS, including api-tester + security-auditor results.
- **HANDOFF update** (prod env-var table from BE-3, Redis-store activation note, reset-localization done) must land in the same PR.
