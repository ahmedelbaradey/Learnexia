# Security Audit — P1-13 BE-4 CAPTCHA (Cloudflare Turnstile) on register

Branch: `feat/P1-13-captcha-register` · Scope: defensive review, no code edits.

## Scope reviewed (files / endpoints)
- `TurnstileCaptchaVerifier.cs` (Identity.Infrastructure/Services) — siteverify call, fail-closed logic.
- `ICaptchaVerifier.cs` (Identity.Application/Abstractions) — seam contract.
- `CaptchaSettings.cs` (Identity.Application/Configurations) — `Enabled` + `SecretKey`.
- `DependencyInjection.cs` (Identity.Infrastructure) — config binding + typed `HttpClient` registration.
- `RegisterParentCommand.cs` / `RegisterParentCommandHandler.cs` — token plumbing + verify-before-create.
- `AuthenticationController.cs` — `POST api/Users/Authentication/Register-Parent` (`[AllowAnonymous]`).
- `appsettings.json` (`Captcha`), `Program.cs` / `ServiceExtensions.cs` (rate limiting), resource keys.

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Info | **Fail-closed verified.** Enabled + null/empty token → `false` (line 43-44). `EnsureSuccessStatusCode()` (non-2xx), timeout/`HttpRequestException`/JSON parse all throw → caught → `false` (line 63-67). `payload?.Success ?? false` defaults a null/malformed body to `false`. No fail-open path. | `TurnstileCaptchaVerifier.cs:36-69` | None — correct. |
| 2 | Info | **No-op is config-only.** Bypass (`return true`) fires solely on `!_settings.Enabled` (line 39). `Enabled` is server config (`Captcha:Enabled`), never bound from the request; `RegisterParentCommand` carries only `CaptchaToken`. An attacker cannot force the no-op via request input. | `TurnstileCaptchaVerifier.cs:39`, `CaptchaSettings.cs:15` | None — correct. |
| 3 | Info | **Server-side verification correct.** Secret + response POSTed to siteverify; the parsed `success` flag is enforced (not a client-trusted "passed" flag). The client only ever sends an opaque `CaptchaToken`. | `TurnstileCaptchaVerifier.cs:48-61` | None — correct. |
| 4 | Info | **No SSRF.** `SiteVerifyUrl` is a hardcoded `const`; no request/config value influences the outbound host or path. `remoteIp` is sent only as a form field, not used to build the URL. | `TurnstileCaptchaVerifier.cs:23,57` | None — correct. |
| 5 | Info | **Secret handling correct.** `SecretKey` bound from `Captcha` section, env override via `Captcha__SecretKey`; committed `appsettings.json` value is empty. Exception logging (`LogError(ex, "Error verifying Turnstile CAPTCHA token")`) logs a static message + exception — it does **not** log the secret, the form content, or the request body. | `DependencyInjection.cs:70`, `appsettings.json:26-29`, `TurnstileCaptchaVerifier.cs:66` | None — correct. |
| 6 | Info | **No internal leak to caller.** Failure returns generic localized `CaptchaVerificationFailed` ("CAPTCHA verification failed. Please try again."); no exception text / provider internals reach the anonymous caller. Detail is logged server-side only. | `RegisterParentCommandHandler.cs:57-58`, resx:1501 | None — correct. |
| 7 | Medium | **CAPTCHA ships disabled by default and is the primary anti-automation control on an anonymous register endpoint.** With `Enabled=false` (committed default) registration accepts any/no token. This is intentional for dev/CI but means a deploy that forgets `Captcha__Enabled=true` silently runs with bot protection off. There is no startup guard analogous to `GuardJwtSecret` to fail-fast in Production/Staging when CAPTCHA is disabled or the secret is empty. | `appsettings.json:26-29`, `CaptchaSettings.cs`, `DependencyInjection.cs:70-71` | Operational: prod/staging deploy MUST set `Captcha__Enabled=true` + `Captcha__SecretKey`. Recommend a startup guard (mirror `GuardJwtSecret`) that rejects `Enabled=false` or empty `SecretKey` in Production/Staging. Tracked as a follow-up, not a blocker. |
| 8 | Low | **Rate limiting is global, not register-specific.** `UseIpRateLimiting` applies one rule `*` = 200 req/min/IP. The register endpoint has no tighter per-endpoint limit, so up to 200 account-creation attempts/min/IP are allowed when CAPTCHA is off (or 200 token-verify round-trips when on). CAPTCHA complements but does not replace a stricter limit here. | `ServiceExtensions.cs:30`, `Program.cs:173` | Consider a tighter per-endpoint IP rule (e.g. `post:/api/Users/Authentication/Register-Parent` at a low limit/period) as defense-in-depth. Follow-up. |
| 9 | Info | **Over-posting safe.** `RegisterParentCommand` exposes no `Role`/`IsActive`/audit fields; role is server-assigned `Roles.Parent` (handler line 97), `AcceptedTermsAtUtc`/`CreatedAt` set server-side. CAPTCHA change adds only `CaptchaToken`. | `RegisterParentCommand.cs`, handler:66-97 | None — correct. |
| 10 | Info | **Dependency scan clean.** `dotnet list ... --vulnerable` reports no vulnerable packages across the solution (incl. Identity.Infrastructure HttpClient stack). | solution | None. |

## Verdict: PASS-WITH-FOLLOWUPS

No Critical/High findings — the gate is **not** blocked. The CAPTCHA implementation is correctly fail-closed when enabled, config-gated (bypass is server-controlled, not request-controlled), free of SSRF, verifies server-side, never logs/returns the secret, and returns a generic localized error. Dependency scan is clean.

Two non-blocking follow-ups:
- **#7 (Medium):** add a Production/Staging startup guard so a deploy can't silently run with CAPTCHA disabled or an empty secret (mirror the existing `GuardJwtSecret` pattern).
- **#8 (Low):** add a tighter per-endpoint rate-limit rule on `Register-Parent`.

## Notes / accepted risks
- `Captcha:Enabled=false` in committed `appsettings.json` is an **accepted** dev/CI default. **Production and Staging MUST set `Captcha__Enabled=true` and `Captcha__SecretKey` (Turnstile secret) out-of-band** — this is an operational requirement, recorded here and (per CLAUDE.md protocol) belongs in HANDOFF.md.
- `RequireHttpsMetadata=false` (DependencyInjection.cs:150) is a pre-existing JWT-bearer dev setting, out of scope for this story but flagged for prod hardening previously.

Gate verdict: PASS-WITH-FOLLOWUPS
