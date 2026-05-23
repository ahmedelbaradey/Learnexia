# P1-13a (backend) — Notifications email delivery (built first)

> Story: [../../../user-stories/Phase-1-Foundation/P1-13a-notifications-email-delivery.md](../../../user-stories/Phase-1-Foundation/P1-13a-notifications-email-delivery.md)
> Phase 1 · **Hardening enabler — build before P1-13 / P1-12d** · SP: 6 · Module: **Notifications** (parallel-safe; no cross-module FK) · mirror **Catalog** patterns.
> Source: `docs/briefs/phase-1-backend-gap-analysis.md` (Gap #4, #6). Split out of P1-13 per lead decision.

## Tasks
| ID | Task | Artifact | Deps | Est (h) |
|---|---|---|---|---|
| P1-13a-BE-1 | **Email-sender abstraction + adapter**: define `IEmailSender`, implement `SendNotificationCommandHandler` against it; one real adapter (config-driven, e.g. SMTP relay) + a **dev no-op/log sink**; secrets from env, never `appsettings`. **Pattern gate:** single `IEmailSender` + Adapter (no provider-selection Strategy until a 2nd provider is real) — **named for approval before coding** | Notifications — `IEmailSender`, adapter, `SendNotificationCommandHandler` | P1-06-BE | 6 |
| P1-13a-BE-2 | **Wire UserRegistered → welcome email**: extend `UserRegisteredIntegrationEventHandler` to send the registration/welcome email via BE-1 (best-effort, failure isolated from registration), keeping the existing notification-row write | Notifications — integration-event handler | P1-13a-BE-1 | 3 |
| P1-13a-BE-3 | **security-auditor** pass: email secrets handling, header/content injection, SSRF / open-relay, and failure-path info leakage | security-auditor | BE-1, BE-2 | — |

## Acceptance-criteria coverage
- Notifications can actually send email (config-driven; dev no-op/log; secrets from env) → **BE-1** (Gap #4) — unblocks P1-12d & P5-04
- Registration/welcome message delivered via email (best-effort) → **BE-2** (Gap #6)
- Email secrets / injection / SSRF reviewed → **BE-3**

## Contract to consumers (P1-12d password reset, P5-04 report delivery)
- `IEmailSender.SendAsync(to, subject, body/template, ...)` returning a result; handlers dispatch via the existing `SendNotificationCommand` / integration events. All envelopes `BaseResponse<T>`, `Successed`. No provider internals leak to callers.

## Notes
- **Notifications-module-scoped** → parallel-safe; cross-module only via `Shared.Contracts` (already consumes `UserRegistered`). No cross-module FK. No Unit of Work.
- **Ask before adding any design pattern** — the `IEmailSender` Adapter (+ optional Strategy/selector) must be named and approved before coding. Default: minimal single-adapter design.
- **Open before implementation:** staging/prod email-provider choice (SMTP relay vs SendGrid/SES/etc.) — lead's call.
- **Sequencing:** build **before** P1-13 BE-tasks that aren't independent and before P1-12d. P1-13 lockout/sign-in/admin-seed do **not** depend on this and may run in parallel.
