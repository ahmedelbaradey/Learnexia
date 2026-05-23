# Security Audit — P1-13 Backend Hardening (BE-1/BE-2/BE-3)

Branch: `feat/P1-13-backend-hardening` · Auditor: security-auditor · Date: 2026-05-24
Scope: Identity sign-in hardening, lockout, anti-enumeration, env-driven admin seed.

## Scope reviewed (files / endpoints)
- `…Identity.Application/Features/Authentications/Commands/SignIn/SignInCommandHandler.cs` — auth path, anti-enumeration, lockout, exception handling.
- `…Identity.Infrastructure/Persistence/Seed/UserSeeder.cs` + `IdentitySeeder.cs` — config/env-driven admin seed; legacy dev-only accounts.
- `…Shared.Resources/SharedResourcesKey.cs` + `SharedResources.en-US.resx` + `.ar-EG.resx` — new message keys.
- `Host/Learnexia.Host/appsettings.json` — `AdminSeed` section.
- Cross-referenced (not changed in this PR but load-bearing): `…Identity.Infrastructure/DependencyInjection.cs` (lockout 5/5min, `GuardJwtSecret`, JWT bearer), `Host/Extensions/ServiceExtensions.cs` (IP rate limit), `BaseResponseHandler.cs` (status mapping).

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Low | **Timing oracle on user-existence.** When `user == null`, the handler returns before `CheckPasswordSignInAsync`, so a non-existent username skips password-hash work while a real one incurs it. The measurable latency delta lets an attacker enumerate registered emails despite identical messages/status. | `SignInCommandHandler.cs:49-50` | Normalize timing: on `user == null` perform a dummy password verification against a throwaway hash (or call `_signInManager.CheckPasswordSignInAsync` against a sentinel user / `PasswordHasher.VerifyHashedPassword` on a constant hash) before returning the generic result. Track as a follow-up; not blocking. |
| 2 | Low | **Locked-out message reveals account existence.** `LoginTooManyFailedAttempts` is only reachable for an existing, lockable account; an attacker who triggers it confirms the email exists. Accepted trade-off (UX clarity vs. enumeration), but it partially undercuts finding-#1's anti-enumeration goal. | `SignInCommandHandler.cs:61-62` | Acceptable as a documented risk. If strict non-enumeration is required later, return the generic invalid-credentials message on lockout too (worse UX). Note in HANDOFF. |
| 3 | Low | **Lockout-based DoS.** With `lockoutOnFailure: true` and `MaxFailedAccessAttempts=5`, an attacker who knows a victim's email can lock them out for 5 min by submitting 5 bad passwords. Standard Identity trade-off; auto-expires in 5 min. | `DependencyInjection.cs:96-98` (config) + `SignInCommandHandler.cs:58` | Accept as a deliberate trade-off (preferred over no brute-force protection). Optionally pair with per-IP throttling on the auth route so an attacker can't cheaply lock many accounts. Not blocking. |
| 4 | Info | **Auth endpoint relies on the global IP rate limit** (`*` = 200 req/min, in-memory store). No auth-specific (tighter) limit; in-memory counter is per-instance and resets on restart / won't share across replicas. | `Host/Extensions/ServiceExtensions.cs:30` | Combined with account lockout this is adequate for now. For production multi-instance, back the counter with the Redis store and consider a tighter limit on `/auth/*`. Follow-up. |
| 5 | Info | **`RequireHttpsMetadata = false`** is unconditional (not gated to Development). Pre-existing, not introduced by this PR. | `DependencyInjection.cs:132` | Gate to `IsDevelopment()` for prod hardening. Out of scope for P1-13; log as a separate item. |
| 6 | Info | **Connection string with password committed** in `appsettings.json` (`Password=admin`, localhost). Pre-existing dev default, not part of this PR. | `appsettings.json:3` | Source DB credentials from env/secret store in non-Development (same pattern as `GuardJwtSecret` / `AdminSeed`). Separate hardening item. |

## Verified correct (no finding)
- **Account enumeration (primary goal): FIXED.** Not-found and wrong-password both return `BadRequest<>` (HTTP 400) with the same `LoginInvalidCredentials` message. Previously not-found returned `NotFound<>` (404) — a clear oracle, now removed. Status + body are identical. (`SignInCommandHandler.cs:50,65`; `BaseResponseHandler.cs:23-35`.)
- **Info leakage: FIXED.** Catch block no longer returns `ex.Message`; it logs server-side via `ILoggerManager` and returns the generic `LoginSystemError`. (`SignInCommandHandler.cs:90-96`.)
- **Credential exposure: SAFE.** `appsettings.json` `AdminSeed:Password` is empty; `SeedConfiguredAdminAsync` no-ops when email/password absent (no committed fallback) and reads password from `AdminSeed:Password` (env `AdminSeed__Password`). Password is passed only to `userManager.CreateAsync` — never logged. (`UserSeeder.cs:33-64`.)
- **Legacy hardcoded creds (`123Pa$$word!`): correctly gated** to `environment.IsDevelopment()` in `IdentitySeeder.SeedAsync`; non-Development seeds only role claims. (`IdentitySeeder.cs:31-41`.)
- **JWT default secret: guarded.** `GuardJwtSecret` throws in Production/Staging when secret is empty or equals the `CHANGE_ME…` default; defaults to Production (fail-closed) when env unresolved. (`DependencyInjection.cs:176-205`.)
- **Lockout engaged** with `MaxFailedAccessAttempts=5` / 5-min window; success resets counter. (`DependencyInjection.cs:96-98`, `SignInCommandHandler.cs:58`.)
- **Dependency scan:** `dotnet list … --vulnerable` → no vulnerable packages across all projects.

## Verdict: PASS-WITH-FOLLOWUPS
No Critical/High findings. The PR's stated security goals (anti-enumeration on message/status, no raw exception leakage, no committed admin credential, lockout) are correctly implemented. Remaining items are Low/Info trade-offs and pre-existing issues outside this PR's scope.

### Top follow-ups (non-blocking)
1. (#1) Add dummy-hash on the user-not-found path to close the timing oracle.
2. (#3/#4) Pair lockout with per-IP throttling on `/auth/*` (Redis-backed) to limit lockout-DoS and credential stuffing.
3. (#5/#6) Gate `RequireHttpsMetadata=false` to Development and move the DB connection-string password to env/secret store.

**Security: PASS** (PASS-WITH-FOLLOWUPS — 0 blocking findings)
