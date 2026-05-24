# Security Audit — P1-12 BE-6 Password Reset

Branch: `feat/P1-12-password-reset` (working tree, uncommitted). Defensive review only — no code edited.

## Scope reviewed (files / endpoints)
- `AuthenticationController.cs` — `[AllowAnonymous]` `POST api/Users/Authentication/Forgot-Password` + `/Reset-Password`
- `ForgotPassword/ForgotPasswordCommand(.Handler).cs`, `ResetPassword/ResetPasswordCommand(.Handler).cs`
- `Validation/ForgotPasswordCommandValidator.cs`, `ResetPasswordCommandValidator.cs`
- `UserManagmentIdentityService.cs` (`GeneratePasswordResetTokenAsync`, `ResetPasswordAsync`, `UpdateSecurityStampAsync`)
- `Shared.Contracts/Identity/PasswordResetRequestedIntegrationEvent.cs`
- `Notifications/.../PasswordResetRequestedIntegrationEventHandler.cs`
- `ErrorHandlerMiddleWare.cs`, `ValidationBehavior.cs`, `appsettings.json` (`ClientAppBaseUrl`), resx messages
- Cross-check: `SignOutCommandHandler.cs` / `AuthenticationIdentityService.cs` Redis key

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Medium | **Timing-based account-enumeration oracle.** For a known active user the handler performs `GeneratePasswordResetTokenAsync` (HMAC/data-protection) + `_publisher.Publish` (synchronous MediatR in-process → the Notifications handler calls `IEmailSender.SendAsync` SMTP **inside the request**, awaited before the 200 returns). For an unknown/inactive email it returns immediately after `FindByEmailAsync`. The body/status are identical, but the response-time delta (token mint + email send) is observable and lets an attacker enumerate accounts. The headline anti-enumeration goal is met for body/status but not for timing. | `ForgotPasswordCommandHandler.cs:60-67`, `PasswordResetRequestedIntegrationEventHandler.cs:54` | Decouple email send from the request (outbox / background queue / `Task.Run` fire-and-forget after response) so both paths return in the same time, OR add a constant-time floor. At minimum confirm the email path is async/non-blocking before the response is written. |
| 2 | Medium | **No per-endpoint rate limiting on `Forgot-Password`.** Only a global IP rule (`*` = 200 req/min) exists. An unauthenticated `Forgot-Password` endpoint that mints tokens + triggers outbound email is an abuse/spam + enumeration amplifier; 200/min/IP is far too permissive for this surface. `Reset-Password` (token brute-force) is likewise only under the global rule. | `ServiceExtensions.cs:30`, `AuthenticationController.cs:54-66` | Add a tight dedicated rate-limit rule for `*/Forgot-Password` and `*/Reset-Password` (e.g. a handful/min/IP and per-email throttle). Identity reset tokens are high-entropy so brute-force risk is low, but rate-limit anyway as defence-in-depth + anti-spam. |
| 3 | Low | **ForgotPassword malformed-email returns a distinguishable 422** (`ValidationException` → `UnprocessableEntity` with field errors) vs 200 generic success for well-formed input. This is NOT account enumeration (it depends only on string format, not account existence) and is acceptable, but it is a response-shape divergence from the "always identical" intent. | `ForgotPasswordCommandValidator.cs:14-16`, `ErrorHandlerMiddleWare.cs:55-60` | Accept as-is (format validation reveals nothing about accounts). Documented here for completeness. |
| 4 | Low | **`ClientAppBaseUrl` not set/validated for production.** Value is `http://localhost:3000` (placeholder) and falls back to a hardcoded localhost default if unset. If shipped unset/misconfigured in prod, reset links point to localhost (links break) — not an open redirect (host is config-derived, never user input), but an availability/correctness risk for the security-critical reset flow. | `ForgotPasswordCommandHandler.cs:24-25,105-107`, `appsettings.json:7` | Require `ClientAppBaseUrl` from env/secret store in prod (fail fast if missing); ensure it is `https`. No user input reaches host/scheme — open-redirect risk is NIL. |
| 5 | Info | Email body interpolates `notification.UserName` (from `User.FullName`/`UserName`) into HTML without encoding. UserName/FullName is self-set PII; a crafted name could inject markup into the email body (the link host itself is safe — config-derived + `Uri.EscapeDataString` on email & token). Low impact (own inbox), but note for the email-templating follow-up. | `PasswordResetRequestedIntegrationEventHandler.cs:46-52` | When real templating lands, HTML-encode user-supplied fields in the body. |

## Checklist verdicts (focus areas)

1. **Account enumeration — body/status:** PASS. Every ForgotPassword path returns the same `Success<string>(ForgotPasswordGenericResponse)` 200 (unknown / inactive / real / even on exception — handler:69-75 swallows and returns the same generic). ResetPassword returns one generic `BadRequest` (`ResetPasswordInvalidLink`) for unknown email, inactive, bad/expired token, AND password-policy failure — caller cannot distinguish. **Timing:** Medium gap, see Finding #1.
2. **Token handling:** PASS. Standard Identity `GeneratePasswordResetTokenAsync`/`ResetPasswordAsync` (single-use, expiry, security-stamp-bound). Token never logged anywhere (grepped handlers + consumer — only user id / EventId logged). Token transported only in the email link; **not echoed** in the API response (response payload is a localized string only).
3. **Session invalidation on reset:** PASS. On success BOTH `UpdateSecurityStampAsync(user)` (handler:77) AND `_distributedCache.RemoveAsync("userrefreshtoken-{user.Id}")` (handler:86) run. Key string matches `SignOutCommandHandler.cs:101` and the storage key in `AuthenticationIdentityService.cs:248,262` exactly — refresh token is genuinely revoked.
4. **Open redirect / link injection:** PASS. Host/scheme come from `ClientAppBaseUrl` config only (never user input); email + token `Uri.EscapeDataString`-encoded. No header injection (email is a single recipient field). See Finding #5 (body HTML) + #4 (prod config) as minor follow-ups.
5. **Password policy:** PASS. New password flows through `ResetPasswordAsync` which enforces the configured ASP.NET Identity policy; the validator deliberately only checks presence (no bypass — strength lives in one place). Policy failure surfaces as the generic failure (no oracle).
6. **Secrets / leakage:** PASS for these handlers. ForgotPassword returns generic success on exception; ResetPassword returns localized `SystemErrorSavingData` (not `ex.Message`). No raw exception text reaches the caller from this feature. NOTE: the global `ErrorHandlerMiddleWare` default branch (`ErrorHandlerMiddleWare.cs:69-70`) DOES return `ex.Message` + inner message — a pre-existing platform-wide info-disclosure issue, but these handlers catch their own exceptions so it is not reachable via this feature. Flagged for the broader backlog, not blocking here.
7. **Cross-module isolation:** PASS. Identity → Notifications only via `PasswordResetRequestedIntegrationEvent` in `Shared.Contracts`; Identity never references the Notifications module. The token rides inside `ResetUrl` on an **in-process** MediatR event (no external broker/persistence yet) — acceptable for the current in-process design; revisit if/when events become a persisted outbox or hit an external bus (token would then be at rest).

## Verdict: PASS-WITH-FOLLOWUPS
No Critical/High findings → does not block the reviewer gate. Two Medium follow-ups recommended before/with production:
- **#1** decouple email send from the request to close the timing oracle (the headline anti-enumeration requirement is only partially met until then).
- **#2** add dedicated rate limiting to `Forgot-Password`/`Reset-Password`.

## Notes / accepted risks
- In-process event delivery means the reset token is never persisted/logged (good). Re-audit if events move to a persisted outbox or external bus.
- Global `ErrorHandlerMiddleWare` `ex.Message` leak is pre-existing and not reachable through this feature; tracked separately.
