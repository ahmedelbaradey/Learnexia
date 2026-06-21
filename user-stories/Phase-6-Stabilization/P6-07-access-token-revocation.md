# Access-token revocation (per-request session validation)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 6 — Stabilization (Week 9)
- **Epic:** Stabilization & Hardening
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 5
- **Labels:** `backend`, `security`, `hardening`, `identity`, `auth`, `stabilization`
- **Requirements:** NFR-4 (security), FR-ID-1/4
- **Status:** Backlog (Phase 6). **Split out of P6-06 (audit finding G2)** — the load-bearing access-token revocation deliberately deferred from the P6-06 hardening pass so it could run its own full pipeline.

## Description
As the platform owner, I want an **already-issued access token to stop working the moment the user signs out, changes their password, or is suspended/deleted by an admin** — instead of staying valid for its full 30-minute lifetime — so that logout and account-revocation are real, not cosmetic.

> **Provenance:** Phase-1 auth audit finding **G2** (`docs/briefs/P1-12-password-reset-security-audit.md`, P6-06 story AC6). Today the JWT bearer pipeline validates a token on **signature + lifetime only** — there is **no per-request server-side check** (`JwtBearerEvents.OnTokenValidated` is not wired). Sign-out / password-change only delete the refresh-token cache entry (`userrefreshtoken-{userId}`) and bump the SecurityStamp (which nothing reads), so the **current access token keeps working until it expires** (`JwtSettings:AccessTokenExpireMinutes = 30`).

## Current state (verified 2026-06-21)
- **No `OnTokenValidated`** in `Identity.Infrastructure/DependencyInjection.cs` `AddJwtBearer` → access tokens are accepted purely on signature + `ValidateLifetime`.
- **The session store exists but is wired wrong.** `AuthenticationIdentityService.GetClaims` mints the JWT `"SessionId"` claim as a fresh `Guid.NewGuid()` **(GUID-A)**, but `SessionManagementService.CreateSessionAsync` ignores it and generates **its own** key **(GUID-B)**. So the JWT carries GUID-A while the Redis session is keyed GUID-B. Every `TerminateSessionAsync(GetClaimValue("SessionId"))` in `SignOut` / `SignOutOthers` / `ChangePassword` looks up GUID-A and **silently misses** — the keep-current-session-on-password-change / sign-out-other-devices machinery is effectively a no-op today.
- Existing seams already in place to leverage: `ISessionManagementService` (Redis-backed, `GetSessionAsync` / `TerminateSessionAsync` / `GetUserSessionsAsync`), `SignOutCommandHandler`, `SignOutOthersCommandHandler`, `SetNewPasswordCommandHandler` (change-password, keeps current session), `GetMySessionsQueryHandler`.

## Chosen design (lead, 2026-06-21): **SessionId per-request validation**
Wire `JwtBearerEvents.OnTokenValidated` in the Identity JWT bearer config to validate the token's `"SessionId"` claim against `ISessionManagementService` on every authenticated request, rejecting the request (`context.Fail()`) when the session is **absent / terminated / expired**. Reuses the entire existing session model so sign-out, sign-out-other-devices, and keep-current-session-on-password-change start working correctly for free, with per-device granularity.

**Explicitly NOT chosen:** ASP.NET security-stamp per-request validation — `UserManager.ChangePasswordAsync` bumps the stamp, which would also kill the caller's **current** device, breaking the documented P2-12 keep-current-session behavior and its tests. (Also coarser: stamp is per-user, can't power per-device sign-out.) Per-user "revoked-after timestamp" was rejected for the same all-or-nothing reason.

## Acceptance Criteria
1. **Revoked token is rejected fast.** After sign-out, an API call with the same (still-unexpired) access token returns **401**, not 200.
2. **Password change keeps the current device, kills the others.** After `ChangePassword`, the caller's **current** access token still works (P2-12 behavior preserved) while tokens for the user's **other** sessions are rejected on their next call.
3. **Password reset (anonymous) revokes everything.** A completed password **reset** terminates **all** of the user's sessions, so every previously-issued access token is rejected — there is no "current session" to preserve on the anonymous reset path.
4. **Admin suspend / delete revokes everything.** Suspending or deleting an account terminates all its sessions; its outstanding access tokens are rejected on the next call.
5. **The SessionId mismatch is fixed.** The JWT's `"SessionId"` claim equals the key of the persisted `UserSession` (so termination actually matches). A normal, un-revoked token continues to authenticate (no false rejections) and sliding-session activity is not written on the hot validation path (read-only check).
6. **Fail-soft, bounded cost.** A transient session-store/Redis outage must not lock every user out unexpectedly — the documented behavior for store errors is explicit (chosen: **fail-closed reject** vs fail-open — recorded in the brief) and the per-request lookup is a single read (no per-request write).
7. The change passes a **security-auditor** review (Critical/High block) and the existing auth integration suite stays green.

## Notes
- **Identity + Host scoped.** No cross-module references; no new module; no DB migration (sessions live in Redis via `IDistributedCache`). No Unit of Work.
- **Prereq baked into AC5:** sign-in **and** parent registration **and** Google sign-in must each persist a `UserSession` whose key is the token's `"SessionId"` claim — otherwise every token is rejected. All three issuance paths go through `AuthenticationIdentityService.GetJwtToken`; the session-creation call sites must be audited.
- **Reset path:** `ResetPasswordCommandHandler` is anonymous (no JWT / no current session) → it must terminate **all** sessions for the user (no keep-current). Confirm it has the userId in hand.
- **Cost:** one `IDistributedCache` GET per authenticated request. Acceptable; do **not** call `ValidateSessionAsync` with `updateActivity:true` on the hot path (that writes back every request) — use a read-only lookup.
- Tasks: [tasks/Backend/Phase-6-Stabilization/P6-07-BE.md](../../tasks/Backend/Phase-6-Stabilization/P6-07-BE.md) · [tasks/Frontend/Phase-6-Stabilization/P6-07-FE.md](../../tasks/Frontend/Phase-6-Stabilization/P6-07-FE.md).
