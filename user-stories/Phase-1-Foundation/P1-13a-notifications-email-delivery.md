# Notifications email delivery (foundational — built first)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation · **Hardening (post Batch-2) — build before P1-13 / P1-12d**
- **Epic:** Notifications
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 6 — email-sender abstraction + provider adapter + wiring the first consumer.
- **Labels:** `backend`, `notifications`, `email`, `infrastructure`, `security`
- **Requirements:** FR-ID-1 (registration message), SRS Notifications; enabler for FR-ID password reset (P1-12d) and FR-PA-* report delivery (P5-04)
- **Status:** Backlog — **split out of P1-13 per lead decision; built first.** Source: `docs/briefs/phase-1-backend-gap-analysis.md` (Gap #4, #6).

## Description
As the platform, I need the Notifications module to actually send email through a configuration-driven sender, so that the registration/welcome message, password reset (P1-12d), and later report delivery (P5-04) have working email infrastructure instead of a handler that throws.

> **Why its own story, built first:** the backend gap analysis found `SendNotificationCommandHandler` throws `NotImplementedException` — there is **no email delivery anywhere**. This is the single shared enabler that P1-12d (password reset) and P5-04 (report delivery) both depend on, so the lead pulled it out of P1-13 to stand up first. The rest of P1-13 (lockout, sign-in safety, admin seed) does **not** depend on this and can run in parallel.

## Acceptance Criteria
- The Notifications module can **send an email** via a config-driven sender: `SendNotificationCommandHandler` is implemented against an `IEmailSender` abstraction with a real provider adapter; **dev = no-op / log sink**, staging/prod = the configured provider.
- **Secrets (SMTP creds / API key) come from env / secret storage**, never committed to `appsettings`.
- A **parent registration / welcome email** is sent on `UserRegistered` (best-effort; an email failure is isolated and does not fail registration), keeping the existing notification-row write. *(Closes Gap #6.)*
- Sending is resilient: failures are logged server-side and do not surface provider internals to callers.
- The change passes a **security-auditor** review (email secrets, header/content injection, SSRF/open-relay considerations) before the reviewer gate.

## Notes
- **Notifications-module-scoped → parallel-safe**; cross-module only via `Shared.Contracts` integration events (it already consumes `UserRegistered`). No cross-module FK. No Unit of Work.
- **Design-pattern gate (CLAUDE.md rule #8):** the `IEmailSender` + provider **Adapter** (and, if more than one provider is ever needed, a **Strategy**/selector) must be **named and approved before coding**. Default proposal: a single `IEmailSender` with one adapter (e.g. SMTP relay) + a dev no-op — minimal, no provider-selection abstraction until a second provider is real.
- **Open before implementation:** email-provider choice for staging/prod (SMTP relay vs SendGrid/SES/etc.) — lead's call.
- Tasks: [tasks/Backend/Phase-1-Foundation/P1-13a-BE.md](../../tasks/Backend/Phase-1-Foundation/P1-13a-BE.md). **Unblocks** P1-12d (password-reset email) and P5-04.
