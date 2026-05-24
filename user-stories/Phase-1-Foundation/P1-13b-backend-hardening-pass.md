# Phase 1 backend hardening pass — auth rate-limiting, timing-oracle, email localization & secrets

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation · **Hardening pass (post-leftover)**
- **Epic:** Identity & Onboarding
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 8 — bundles the non-blocking follow-ups surfaced by the P1-12/P1-13 security audits.
- **Labels:** `backend`, `security`, `hardening`, `identity`, `notifications`
- **Requirements:** NFR-4 (security), FR-ID-1/4
- **Status:** Backlog — consolidates the **non-blocking** follow-ups from the per-PR security briefs (`docs/briefs/P1-12-*-security-audit.md`, `P1-13-*-security-audit.md`). None blocked their original PR; this is the single tracked "hardening pass" to clear them before launch.

## Description
As the platform owner, I want the backend security follow-ups left as non-blocking notes across the P1-12 / P1-13 / P1-13a work closed in one pass — tighter auth rate-limiting, the forgot-password timing oracle, localized transactional emails, and remaining secrets/transport config — so the auth surface is launch-hardened.

> **Why a bundle:** each P1-12/P1-13 PR shipped with security-auditor **PASS** but recorded Low/Medium follow-ups. Rather than scatter them, this story collects them with their source briefs so they're tracked and reviewed as a unit.

## Source findings (from the security briefs)
- **Auth rate-limiting** — only a coarse global 200 req/min/IP rule exists; the anonymous auth endpoints (sign-in, register, google-sign-in, forgot/reset password) have no tighter per-endpoint limit. *(P1-13, password-reset, CAPTCHA, Google briefs.)*
- **Forgot-password timing oracle** — the reset email is sent synchronously in-request, so a real/active account incurs latency vs an unknown one (response body/status are already identical). *(Password-reset brief, Medium.)*
- **Transactional emails are English-only** — the welcome (P1-13a) and password-reset (P1-12d) emails don't honor the recipient's language (the integration events carry no locale). *(P1-13a + password-reset reviewer notes.)*
- **Transport/secret hygiene (pre-existing)** — `RequireHttpsMetadata=false` is unconditional; the DB connection password is committed as a dev default. *(P1-13 + multiple briefs, flagged out-of-scope/pre-existing.)*
- **Operational (not code)** — prod/staging must set the env secrets (`MinIOConfiguration__*`, `GoogleAuth__ClientId`, `AdminSeed__*`, `Captcha__*`, `Email__*`, `JwtSettings__Secret`) and **must enable CAPTCHA**; FE/legal must surface explicit terms/parental consent on the Google sign-in flow.

## Acceptance Criteria
- **Per-endpoint rate limiting** on the anonymous auth endpoints (sign-in, register-parent, google-sign-in, forgot-password, reset-password): a tight per-IP limit (e.g. a small N/min) returns **429** when exceeded, in addition to the existing global rule; multi-instance-safe (Redis-backed store).
- **Forgot-password timing oracle closed:** the email dispatch no longer blocks the request (out-of-band/background), so a registered vs unknown email are **latency-indistinguishable** (body/status already are).
- **Transactional emails localized:** the welcome (P1-13a) and password-reset (P1-12d) emails render in the recipient's language (ar/en) via the localizer — the recipient locale is carried on the integration event (or resolved via `IUserLookup`).
- **Transport/secret hygiene:** `RequireHttpsMetadata=false` is gated to Development only; the DB connection secret is read from env in non-Development; the required prod env vars are documented (HANDOFF).
- The changes pass a **security-auditor** review (Critical/High block).

## Notes
- **Identity + Notifications-scoped**, parallel-safe; cross-module only via `Shared.Contracts` (the email-locale may add a field to the existing reset/welcome events). No cross-module FK. No Unit of Work.
- **Ask-first** (CLAUDE.md rule #8) if any item needs a new abstraction — none expected (rate-limiting is `AspNetCoreRateLimit` config, already wired; email-locale is a field + localizer).
- Tasks: [tasks/Backend/Phase-1-Foundation/P1-13b-BE.md](../../tasks/Backend/Phase-1-Foundation/P1-13b-BE.md).
- **Open decisions for the lead** (see task file): per-endpoint rate-limit thresholds + store (Redis vs in-memory); email-locale source (event field vs `IUserLookup`); whether the transport/DB-secret item (BE-4) belongs in Phase 1 or defers to **P6** (stabilization/observability).
- Could alternatively be filed as a **P6** hardening ticket — recorded here as P1-13b because the findings originate in Phase-1 auth.
