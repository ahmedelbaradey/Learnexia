# Security Audit — P1-12 BE-5 Google social sign-in

Branch: `feat/P1-12-oauth-google` · Module: Identity · Date: 2026-05-24
Scope: defensive review of the Google ID-token sign-in surface. Audit + report only; no code edits.

## Scope reviewed (files / endpoints)
- `POST /api/Users/Authentication/Google-SignIn` `[AllowAnonymous]` — `AuthenticationController.cs:32-36`
- `GoogleTokenValidator.cs` (Infrastructure) — SDK token validation
- `GoogleSignInCommandHandler.cs` — find-or-create, link, JWT issuance
- `GoogleSignInCommand.cs` / `GoogleSignInValidator.cs` / `IGoogleTokenValidator.cs` / `GoogleUserInfo`
- `GoogleAuthSettings.cs` + `appsettings.json` `GoogleAuth:ClientId`
- `UserManagmentIdentityService.cs` — `CreateAsync(User)` (passwordless), `GetLoginsAsync`, `AddLoginAsync`
- `AuthenticationIdentityService.GetClaims` — role/claim sourcing
- `SignInCommandHandler.cs` — password path interaction with passwordless accounts
- `DependencyInjection.cs` — DI, Identity options, JWT guard
- `ServiceExtensions.cs` — rate limiting

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Info (confirmed-safe) | Token audience pinned to our `ClientId`; issuer/signature/expiry validated by the SDK; `InvalidJwtException` and generic exceptions both return `null` (fail-closed). Empty `ClientId` makes `Audience=[""]` so the SDK rejects every token — inert/fail-closed. | `GoogleTokenValidator.cs:34-60` | None. Behaves as designed. Ensure `GoogleAuth__ClientId` env is set in real envs (else all sign-ins fail closed, which is correct but breaks the feature). |
| 2 | Info (confirmed-safe) | `EmailVerified` is required before the email is trusted; unverified → `BadRequest`, no find/create/link. Account match + link happens only after Google attests email ownership. | `GoogleSignInCommandHandler.cs:55-57` | None. **Document the trust assumption** (see Notes). |
| 3 | Low (accepted trust assumption) | Account-linking by verified email: a Google token for an email matching an existing **password** account will sign in to / link that account. This is standard OAuth behavior and gated on Google's verified-email attestation, but it is a real trust boundary — if Google's verification is ever wrong, or a user's email was registered with a password by a different person, linking grants access. | `GoogleSignInCommandHandler.cs:60-78` | Accept as standard. Optionally, for pre-existing password accounts, require an explicit "link Google" confirmation step rather than silent auto-link (product/UX follow-up, not a code defect). |
| 4 | Info (confirmed-safe) | Passwordless account cannot be password-logged-in. `CreateAsync(User)` (no password) leaves `PasswordHash` null; `SignInCommandHandler` uses `CheckPasswordSignInAsync`, which returns failure when `PasswordHash` is null — an empty/guessed password cannot authenticate. | `UserManagmentIdentityService.cs:28`, `SignInCommandHandler.cs:69-76` | None. |
| 5 | Info (confirmed-safe) | No info leak / enumeration: invalid token → generic localized `Unauthorized`; unverified → generic `BadRequest`; deactivated → standard message; server error → localized `GoogleSignInFailed`. `ServerError<T>(string?)` takes a localized key, NOT `ex.Message` — no raw exception text reaches the client. Validation internals logged server-side only. | `GoogleSignInCommandHandler.cs:53,57,67,104-109`; `BaseResponseHandler.cs:45-58` | None. Minor: new-vs-existing user paths differ slightly in branch behavior, but all error responses are generic; not an exploitable oracle. |
| 6 | Info (confirmed-safe) | No role/claim injection from the Google payload. Role is server-assigned **Parent** only (`AddToRoleAsync(Roles.Parent)`); JWT claims (incl. roles/permissions) are built from `_userManager.GetRolesAsync(user)` server-side, never from the token. External-login key is the Google `subject` (`sub`), not the email. | `GoogleSignInCommandHandler.cs:143,150-157`; `AuthenticationIdentityService.cs:190-217` | None. |
| 7 | Info (confirmed-safe) | No secrets committed. `ClientId` is an empty placeholder in `appsettings.json`, sourced from `GoogleAuth__ClientId` env in real envs. ID-token flow needs no client secret; none present. JWT secret has a fail-closed prod/staging guard. Tokens/secrets are not logged or returned. | `GoogleAuthSettings.cs`, `appsettings.json:22-24`, `DependencyInjection.cs:186-216` | None. |
| 8 | Info | Dependency scan clean. `dotnet list ... --vulnerable` reports no vulnerable packages across the solution. `Google.Apis.Auth` is `1.69.0` (current). | `Directory.Packages.props:22` | None. |
| 9 | Low | Rate limiting is a single global IP rule (`*` = 200/min) — covers Google-SignIn but is coarse; not a per-endpoint throttle for auth abuse. Pre-existing platform config, not introduced by this story. | `ServiceExtensions.cs:28-41` | Consider a tighter dedicated limit on auth endpoints (follow-up; applies to all auth routes, not specific to this change). |
| 10 | Low (compliance follow-up) | `AcceptedTermsAtUtc` is auto-stamped at account creation with no explicit consent checkbox on the Google path. Not a code vulnerability, but a legal/compliance gap on a children's platform (parental consent must be explicit). | `GoogleSignInCommandHandler.cs:130` | Product/legal: capture explicit terms acceptance on the FE Google sign-up flow before stamping, or surface a first-login consent gate. Documented as a known follow-up. |

## Verdict: PASS-with-followups

No Critical or High findings. Token validation is correct and fail-closed; passwordless accounts cannot be password-authenticated; roles are server-assigned; no secrets, no raw-exception leakage, no enumeration oracle; dependencies clean.

## Notes / accepted risks
- **Trust assumption (finding #3, explicit):** the design trusts Google's `email_verified` attestation to bind a Google identity to an existing account by email. This is standard and acceptable for social sign-in; recorded as an accepted risk. A future hardening option is an explicit link-confirmation step when matching a pre-existing password account.
- **Consent (finding #10):** auto-stamped terms acceptance is a legal/compliance follow-up for the children's-platform context, not a code defect — route to product/legal + FE.
- **Operational:** `GoogleAuth__ClientId` must be configured in Staging/Production; with an empty value the endpoint fails closed (rejects all tokens) — correct security posture but the feature is inert until configured.

Security: PASS — 0 blocking findings (3 low / 1 compliance follow-ups noted).
