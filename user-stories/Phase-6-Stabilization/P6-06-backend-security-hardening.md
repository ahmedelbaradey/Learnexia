# Backend security hardening (auth timing, email localization, secrets & rate-limit store)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 6 — Stabilization (Week 9)
- **Epic:** Stabilization & Hardening
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 8 — the launch-hardening follow-ups deferred from the Phase-1 auth work.
- **Labels:** `backend`, `security`, `hardening`, `identity`, `notifications`, `stabilization`
- **Requirements:** NFR-4 (security), NFR-1 (performance/scale), FR-ID-1/4
- **Status:** Backlog (Phase 6). **Relocated from the P1-13b hardening pass** — its rate-limiting task (BE-1) shipped in Phase 1 (PR #50); the remaining non-blocking follow-ups land here in stabilization.

## Description
As the platform owner, I want the remaining backend security follow-ups from the Phase-1 auth audits closed during stabilization — the forgot-password timing oracle, localized transactional emails, transport/secret hygiene, and a multi-instance-safe rate-limit store — so the auth surface is launch-hardened.

> **Provenance:** these are the **non-blocking** Low/Medium findings recorded across the P1-12 / P1-13 / P1-13a security-audit briefs (`docs/briefs/*-security-audit.md`). Each originating PR shipped with security-auditor **PASS**; per the lead they were bundled (P1-13b) and the non-rate-limiting items deferred to this Phase-6 stabilization story.

## Acceptance Criteria
- **Forgot-password timing oracle closed:** the reset email dispatch no longer blocks the request (out-of-band/background), so a registered vs unknown email are **latency-indistinguishable** (response body/status already are).
- **Transactional emails localized:** the welcome (P1-13a) and password-reset (P1-12d) emails render in the recipient's language (ar/en) via the localizer — recipient locale carried on the integration event (or resolved via `IUserLookup`).
- **Transport/secret hygiene:** `RequireHttpsMetadata=false` is gated to Development only; the DB connection secret is read from env in non-Development; the required prod env vars are documented (HANDOFF).
- **Rate-limit store is multi-instance-safe:** the auth rate-limiting (shipped in Phase-1 as in-memory) uses a **Redis-backed** counter/policy store so limits hold across horizontally-scaled instances.
- The changes pass a **security-auditor** review (Critical/High block).
- **Revoke live access tokens on password reset / sign-out (audit finding G2):** access tokens (JWT)
  currently have **no per-request server-side check** — sign-out and password reset only drop the
  refresh-token cache entry (`userrefreshtoken-{userId}`) and session records, so an already-issued
  access token stays valid until expiry. **Chosen design (lead, 2026-05-29): SessionId per-request
  validation** — wire `JwtBearerEvents.OnTokenValidated` in the Identity JWT bearer config to validate
  the token's `"SessionId"` claim against `ISessionManagementService` (reject when the session is
  terminated/absent). This preserves the existing P2-12 model where `ChangePassword` keeps the caller's
  current session. **Prereqs:** verify sign-in/registration persists a `UserSession` with the same
  `SessionId` that `AuthenticationIdentityService.GetClaims` mints (else every token is rejected);
  password reset (anonymous) must terminate **all** the user's sessions; do **not** use ASP.NET
  security-stamp validation (`UserManager.ChangePasswordAsync` bumps the stamp and would also kill the
  caller's current session, breaking documented P2-12 behavior + its tests); mind per-request lookup
  cost. Load-bearing auth → run the full pipeline (analyzer → planner → security-auditor → reviewer).

## Progress (2026-05-29 — branch `audit/phase-1`, build-green, NOT committed)
Two quick refinements of the Phase-1 rate-limit/CAPTCHA work were applied on this branch (full Phase-1
follow-up audit done this session):
- **Auth rate limits tightened (was the loose P1-13b 100/s):** `AddRateLimiting`
  (`Host/Extensions/ServiceExtensions.cs`) is now environment-gated — Production/Staging tightens
  register-parent 10/15m, forgot-password 5/15m, reset-password 10/15m, sign-in 50/5m; Development/Testing
  keep the prior generous limits verbatim so the integration suite (env "Testing") is unaffected.
- **CAPTCHA prod guard:** `GuardCaptcha` (Identity `DependencyInjection.cs`) now fails fast at startup in
  Production/Staging unless CAPTCHA is enabled **with** a secret; dev/Testing keep only the prior
  "enabled-without-secret is invalid" check. Mirrors `GuardJwtSecret`.

Still open in this story: forgot-password timing-oracle decouple, email localization, transport/secret
hygiene (`RequireHttpsMetadata` env-gating + DB secret from env), Redis-backed rate-limit store, and the
**G2 token-revocation** above.

## Notes
- **Identity + Notifications + Host scoped**, parallel-safe; cross-module only via `Shared.Contracts` (email-locale may add a `Locale` field to the existing welcome/reset events). No cross-module FK. No Unit of Work.
- **Open decisions** (see task file): email-locale source (event field vs `IUserLookup`); Redis store wiring (needs `AspNetCoreRateLimit.Redis` + a Redis connection — confirmed available in the target env).
- Tasks: [tasks/Backend/Phase-6-Stabilization/P6-06-BE.md](../../tasks/Backend/Phase-6-Stabilization/P6-06-BE.md).
- Done in Phase-1 (not here): **P1-13b BE-1 auth rate-limiting** (100 req/s per IP per endpoint, PR #50); account lockout, sign-in anti-enumeration, admin seed, CAPTCHA (P1-13).
- Operational follow-ups tracked in HANDOFF (not code tasks): set prod env secrets, enable CAPTCHA in prod, FE/legal terms-consent on the Google flow.
