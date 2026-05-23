# Phase 1 backend hardening — lockout, sign-in safety, email delivery & admin seed

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation · **Hardening (post Batch-2)**
- **Epic:** Identity & Onboarding
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 13 — security/operability gaps across the sign-in path, the Notifications email infrastructure, and the admin seed.
- **Labels:** `backend`, `security`, `auth`, `hardening`, `identity`, `notifications`
- **Requirements:** FR-ID-1, NFR-4 (child-data protection / security)
- **Status:** Backlog — sourced from `docs/briefs/phase-1-backend-gap-analysis.md`.

## Description
As the platform owner, I want the backend security and operability gaps left after Phase-1 Batch 1 and Batch 2 (P1-12) closed — account lockout actually engaged, sign-in that doesn't leak internals or enable account enumeration, real email delivery in the Notifications module, and a configuration-driven admin seed with no committed credential — so that the auth path is safe to expose and dependent features (P1-12d password reset, P1-09/P1-03 registration messages) have the infrastructure they need.

> **Why this story exists:** a code-grounded backend gap analysis of all Phase 1 stories (excluding what P1-12 already owns) found six confirmed gaps. Most of the suspected gaps were verified as **already covered** (refresh-token rotation, sign-out revocation, RBAC `[Authorize]` enforcement + family/self-scope, JWT secret read from env). What remained are the items below. See `docs/briefs/phase-1-backend-gap-analysis.md`.

## Acceptance Criteria
- **Account lockout engaged:** after the configured number of consecutive failed sign-ins (`MaxFailedAccessAttempts=5` / 5-min window), the account locks; the counter resets on success; a locked account returns a clear localized (en/ar) `BaseResponse` message. *(Today `SignInCommandHandler` passes `lockoutOnFailure: false`, so lockout never engages — Gap #1.)*
- **Sign-in safety:** failed sign-in never returns raw exception text to the caller (generic localized `ServerError`, detail logged server-side), and a wrong email vs. wrong password are **indistinguishable** (single "invalid credentials" result) so attackers cannot enumerate registered emails. *(Gap #2.)*
- **Email delivery works:** the Notifications module can actually send email via a config-driven sender (secrets from env; dev = no-op/log sink), replacing the `NotImplementedException` in `SendNotificationCommandHandler`. This unblocks P1-12d (password reset). *(Gap #4 — blocker for P1-12d.)*
- **Registration message delivered:** a parent registration / welcome message is sent by email (best-effort, isolated failures), keeping the existing notification-row write. *(Gap #6.)*
- **Config-driven admin seed:** an idempotent Admin account is seeded from configuration/env with **no committed credential**; the legacy hardcoded `superadmin`/`basicuser` default password is removed or guarded out of non-Development. *(Gap #5 — fulfils P1-10-BE-1's intent.)*
- **Anti-automation on register (hardening):** a pluggable bot-challenge (CAPTCHA-token verification) can be required on `Register-Parent`, config-gated (no-op in dev/tests), in addition to the existing IP rate-limit. *(Gap #3 — tracked debt.)*
- Auth-path, secrets, and email changes pass a **security-auditor** review (Critical/High block) before the reviewer gate.

## Notes
- **Identity + Notifications-module-scoped → parallel-safe** with the Phase 2 BE work and with P1-12; cross-module only via `Shared.Contracts` integration events, **no cross-module FK**. No Unit of Work — `GenericRepository` commits per call; explicit transaction only for atomic multi-write.
- **Ask before adding any design pattern** (CLAUDE.md rule #8) — especially the email-provider abstraction (Strategy/Factory) in the email-delivery task. Name it and wait for approval.
- **Email delivery (the long pole)** is a shared enabler also consumed by P5-04 (report delivery); it may be split into its own story **P1-13a — Notifications email delivery** if sequencing favours standing it up before P1-12d starts.
- Tasks: [tasks/Backend/Phase-1-Foundation/P1-13-BE.md](../../tasks/Backend/Phase-1-Foundation/P1-13-BE.md). Excludes everything P1-12 owns (profile/avatar/OAuth/reset/edit-child/register-country+consent/`User` schema fields).
- **Open decisions for the lead** (see brief §5): email verification at registration in scope for P1 or deferred? COPPA under-13 consent record (distinct from P1-12f's terms-consent — no entity captures it today)? Is the whole story Phase-1 or a Phase-6 hardening pull (recommendation: BE-1/2/4/5 in P1 now; BE-6 CAPTCHA may defer)? Email provider choice + pattern approval.
- There is **no teacher role** (SRS §3).
