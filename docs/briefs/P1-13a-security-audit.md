# Security Audit — P1-13a Notifications email delivery (P1-13a-BE-3)

Branch: `feat/P1-13a-email-delivery` · Repo root: `e:\Wrokspace\Learnexia`
Scope: defensive review only (no code edits). Critical/High block the reviewer gate.

## Scope reviewed (files / endpoints)
- `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Infrastructure/Email/SmtpEmailSender.cs`
- `.../Infrastructure/Email/LogEmailSender.cs`
- `.../Infrastructure/Email/EmailSettings.cs`
- `.../Infrastructure/Email/EmailProvider.cs`
- `.../Infrastructure/DependencyInjection.cs`
- `.../Application/Abstractions/IEmailSender.cs`
- `.../Application/Features/SendNotification/SendNotificationCommand.cs` (+ `Handler`, `Validator`)
- `.../Application/IntegrationEventHandlers/UserRegisteredIntegrationEventHandler.cs`
- `.../Api/NotificationsModule.cs` (POST `/api/notifications` endpoint)
- `backend/src/Shared/Learnexia.Shared.Contracts/Identity/{IUserLookup,UserRegisteredIntegrationEvent}.cs`
- `backend/src/Host/Learnexia.Host/appsettings.json` (`Email` section)

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Medium | CRLF header-injection guard rejects only `\r`/`\n`, not other control chars; and the SMTP send path does not validate the recipient at the *command*-handler level. `MailMessage.Subject` will itself throw on embedded CRLF (folding), and `MailAddress` validates the address — but for defense-in-depth the guard should reject all C0 control chars (`< 0x20` except none) and NUL. Subject from `SendNotificationCommand.Title` is admin-controlled, so risk is bounded. | `SmtpEmailSender.cs:76-77` | Broaden `HasControlChars` to reject any `char.IsControl(c)` (covers `\r \n \0` and other C0). Keep the existing MailAddress validation. |
| 2 | Medium | PII in logs: success log records the full recipient email address; the log-sink (`LogEmailSender`) records `to` **and** the plaintext `subject`. For a children's platform, recipient email is parent PII and should be minimized in logs. Body is correctly logged by length only. | `SmtpEmailSender.cs:65,71`; `LogEmailSender.cs:21` | Mask the recipient (e.g. `a***@domain`) or log only a hashed/last-segment form. Do not log the subject verbatim in the log sink. Apply consistently to the warn/error paths. |
| 3 | Low | SMTP `Password` lives on a singleton `EmailSettings` held in DI for process lifetime (`AddSingleton(settings)`); acceptable, but the credential is never zeroed and is reachable to anything resolving `EmailSettings`. No log of it was found. | `DependencyInjection.cs:36-37`; `EmailSettings.cs:19` | Acceptable for now. Optionally bind credentials via `IOptions`/secret store reference and override `ToString()` on `EmailSettings` to redact secrets, guarding against accidental serialization/logging. |
| 4 | Low | `appsettings.json` `Email` section ships placeholders only (`Host`/`UserName`/`Password` empty, `Provider:"None"`) — correct, no secret committed. But there is no fail-fast: if `Provider:"Smtp"` is selected in prod with an empty `Host`, sends fail at runtime rather than at startup. | `appsettings.json:22-31`; `DependencyInjection.cs:39-48` | Add a startup guard: when `Provider == Smtp`, require non-empty `Host`/`FromAddress` (and credentials if the server needs auth), throwing at composition root. |
| 5 | Info | TLS: `UseSsl` defaults to `true` and binds to `SmtpClient.EnableSsl`. Note the BCL `SmtpClient.EnableSsl` is STARTTLS-on-587 / implicit-SSL semantics and `SmtpClient` is marked obsolete by Microsoft for new code. Credentials are only attached when `UserName` is set, and only over the SSL-enabled client. No cleartext-credential default. | `SmtpEmailSender.cs:52-61`; `EmailSettings.cs:17` | Acceptable. Document that prod must keep `UseSsl:true`. Longer term consider MailKit (explicit STARTTLS + cert validation) — already gated behind the P1-13a provider note. |
| 6 | Info | SSRF / open-relay: SMTP `Host`/`Port` come from config/env only — never request-derived. Recipient is the only user-influenced value and is constrained to a single validated `MailAddress` added to `To` (no CC/BCC from input). No relay/SSRF vector. | `SmtpEmailSender.cs:50-52` | None — confirmed safe. |
| 7 | Info | Failure-path leakage: SMTP exceptions are caught, logged server-side with full detail, and a static generic `Error` (`Email.SendFailed` / `Email.InvalidRecipient`) is returned. No stack trace or provider text reaches the caller. The HTTP endpoint returns `Results.BadRequest(result.Error)` carrying only the generic code/message. | `SmtpEmailSender.cs:68-73`; `NotificationsModule.cs:35` | None — meets the no-leak requirement. |
| 8 | Info | Child-privacy / event PII: `UserRegisteredIntegrationEvent` deliberately carries no email (Guid + UserName only, per prior P4-01 audit); the welcome-email path resolves email lazily via `IUserLookup` and skips cleanly when no implementation is registered. Welcome body embeds `UserName` (display name) — not logged (length only). No child PII broadcast across the module boundary. | `UserRegisteredIntegrationEventHandler.cs:59-104`; `UserRegisteredIntegrationEvent.cs:6-10` | None — confirmed minimized. |
| 9 | Info | DoS / unbounded send: welcome email is best-effort inside `TrySendWelcomeEmailAsync`, wrapped in try/catch that swallows+logs, so an email failure never fails registration or the notification-row write. Idempotency check prevents a second welcome per user. No retry loop, no amplification. | `UserRegisteredIntegrationEventHandler.cs:49-57,82-118` | None — failure isolation is correct. |
| 10 | Info | Authz: POST `/api/notifications` (which now accepts the optional `RecipientEmail` and triggers a send) is gated by `.RequireAuthorization(AuthorizationPolicies.AdminOnly)` — not an unauthenticated send surface. No over-posting concern: the command maps to a MediatR handler, not directly to an entity; `RecipientEmail` is a transient send target, not persisted. | `NotificationsModule.cs:32-37`; `SendNotificationCommand.cs:6-14` | None — endpoint is authorized; mass-assignment N/A. |

## Verdict: PASS-WITH-FOLLOWUPS

No Critical or High findings. The email path correctly: sources SMTP host/credentials from config/env (placeholders only in `appsettings.json`), prevents CRLF header injection plus relies on `MailMessage`/`MailAddress` validation, returns generic non-leaking errors while logging detail server-side, isolates best-effort welcome-email failures from registration, keeps PII out of the cross-module event, and gates the HTTP send behind `AdminOnly`.

Recommended follow-ups (non-blocking): broaden the control-char guard to all C0/NUL (#1), minimize recipient/subject PII in logs (#2), and add a startup fail-fast when `Provider:Smtp` is configured without a host (#4).

## Notes / accepted risks
- The pre-existing JWT `Secret` `CHANGE_ME…` default in `appsettings.json:8` is **out of scope** for this story (not in the changed surface) but remains a known prod-blocking gap tracked elsewhere — must be env-sourced before any production deploy.
- `Provider:"None"` (LogEmailSender) is the dev default and contacts no SMTP server; the audit assumes prod sets `Provider:"Smtp"` with `UseSsl:true` and env-supplied credentials.

Security: PASS — 0 blocking findings (3 medium/low follow-ups recommended).
