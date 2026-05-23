# Phase 1 backend hardening — lockout, sign-in safety & admin seed

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation · **Hardening (post Batch-2)**
- **Epic:** Identity & Onboarding
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 8 — security/operability gaps on the sign-in path + the admin seed. *(Email delivery split to [P1-13a](P1-13a-notifications-email-delivery.md).)*
- **Labels:** `backend`, `security`, `auth`, `hardening`, `identity`
- **Requirements:** FR-ID-1, NFR-4 (child-data protection / security)
- **Status:** Backlog — sourced from `docs/briefs/phase-1-backend-gap-analysis.md`.

## Description
As the platform owner, I want the backend security and operability gaps left after Phase-1 Batch 1 and Batch 2 (P1-12) closed — account lockout actually engaged, sign-in that doesn't leak internals or enable account enumeration, and a configuration-driven admin seed with no committed credential — so that the auth path is safe to expose.

> **Why this story exists:** a code-grounded backend gap analysis of all Phase 1 stories (excluding what P1-12 owns) found six confirmed gaps. Most suspected gaps were verified as **already covered** (refresh-token rotation, sign-out revocation, RBAC `[Authorize]` + family/self-scope, JWT secret from env). The **email-delivery** gaps were split into their own story **P1-13a** (built first). What remains here are the sign-in-path and admin-seed items. See `docs/briefs/phase-1-backend-gap-analysis.md`.

## Lead decisions (recorded)
- **Email verification at registration → BYPASSED for now.** No `RequireConfirmedEmail`, no confirm flow. Deferred.
- **COPPA under-13 parental-consent record → deferred to a compliance pass.** Out of Phase 1; no entity added here. *(Distinct from P1-12f's terms-consent at registration.)*
- **Email delivery → P1-13a, built first.** The tasks in this story do not depend on it.

## Acceptance Criteria
- **Account lockout engaged:** after the configured number of consecutive failed sign-ins (`MaxFailedAccessAttempts=5` / 5-min window), the account locks; the counter resets on success; a locked account returns a clear localized (en/ar) `BaseResponse` message. *(Today `SignInCommandHandler` passes `lockoutOnFailure: false`, so lockout never engages — Gap #1.)*
- **Sign-in safety:** failed sign-in never returns raw exception text to the caller (generic localized `ServerError`, detail logged server-side), and a wrong email vs. wrong password are **indistinguishable** (single "invalid credentials" result) so attackers cannot enumerate registered emails. *(Gap #2.)*
- **Config-driven admin seed:** an idempotent Admin account is seeded from configuration/env with **no committed credential**; the legacy hardcoded `superadmin`/`basicuser` default password is removed or guarded out of non-Development. *(Gap #5 — fulfils P1-10-BE-1's intent.)*
- **Anti-automation on register (hardening):** a pluggable bot-challenge (CAPTCHA-token verification) can be required on `Register-Parent`, config-gated (no-op in dev/tests), in addition to the existing IP rate-limit. *(Gap #3 — tracked debt; may defer to a P6 hardening pass.)*
- Auth-path, secrets, and admin-seed changes pass a **security-auditor** review (Critical/High block) before the reviewer gate.

## Notes
- **Identity-module-scoped → parallel-safe** with the Phase 2 BE work and with P1-12; cross-module only via `Shared.Contracts` integration events, **no cross-module FK**. No Unit of Work — `GenericRepository` commits per call; explicit transaction only for atomic multi-write.
- **Ask before adding any design pattern** (CLAUDE.md rule #8) — e.g. the bot-challenge verifier abstraction.
- Tasks: [tasks/Backend/Phase-1-Foundation/P1-13-BE.md](../../tasks/Backend/Phase-1-Foundation/P1-13-BE.md). Excludes everything P1-12 owns and the email delivery now in **[P1-13a](P1-13a-notifications-email-delivery.md)**.
- **Sequencing:** BE-1/BE-2/BE-3 are quick and need no new abstractions; BE-4 (CAPTCHA) may defer to Phase 6.
- There is **no teacher role** (SRS §3).
