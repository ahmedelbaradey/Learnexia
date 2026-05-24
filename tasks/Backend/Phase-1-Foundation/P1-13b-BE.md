# P1-13b (backend) — Phase 1 backend hardening pass

> Story: [../../../user-stories/Phase-1-Foundation/P1-13b-backend-hardening-pass.md](../../../user-stories/Phase-1-Foundation/P1-13b-backend-hardening-pass.md)
> Phase 1 · **Hardening pass (post-leftover)** · SP: 8 · Module: **Identity + Notifications + Host** (parallel-safe) · mirror existing shapes.
> Source: the per-PR security briefs — `docs/briefs/P1-13-security-audit.md`, `P1-13-captcha-security-audit.md`, `P1-12-password-reset-security-audit.md`, `P1-12-google-signin-security-audit.md`, `P1-13a-security-audit.md`.

## Tasks
| ID | Task | Artifact | Deps | Est (h) |
|---|---|---|---|---|
| P1-13b-BE-1 | **Per-endpoint auth rate-limiting**: add tight per-IP rules (small N/min) for `Sign-In`, `Register-Parent`, `Google-SignIn`, `Forgot-Password`, `Reset-Password` via the existing `AspNetCoreRateLimit` config (keep the global rule); return **429** on exceed. Use the **Redis**-backed rate-limit store so limits hold across instances (Redis is already wired). | `appsettings` rate-limit rules + `ServiceExtensions`/Host wiring | P1-01-BE, P1-02-BE | 5 |
| P1-13b-BE-2 | **Close the forgot-password timing oracle**: dispatch the reset email **out-of-band** so the request doesn't await provider I/O (e.g. publish/handle the `PasswordResetRequestedIntegrationEvent` on a background path / fire-and-forget with isolated logging), making registered vs unknown emails latency-indistinguishable. Keep the generic 200 + anti-enumeration intact. | `ForgotPasswordCommandHandler` / event dispatch | P1-12-BE-6 | 4 |
| P1-13b-BE-3 | **Localize transactional emails**: carry the recipient **locale** on the welcome (`UserRegisteredIntegrationEvent`) + reset (`PasswordResetRequestedIntegrationEvent`) events (or resolve via `IUserLookup`); render subject/body via the localizer (ar/en) in the Notifications consumers. | `Shared.Contracts` events + Notifications handlers + resx | P1-13a, P1-12-BE-6 | 5 |
| P1-13b-BE-4 | **Transport/secret hygiene**: gate `RequireHttpsMetadata=false` to Development only; read the DB connection password from env in non-Development; document the required prod env vars in HANDOFF. *(Pre-existing platform items — confirm Phase-1 vs P6 with the lead.)* | `Identity Infrastructure` JWT bearer config + `appsettings`/env + HANDOFF | — | 3 |
| P1-13b-BE-5 | **security-auditor** pass for the rate-limit, timing-oracle, email-locale, and transport/secret changes | security-auditor | BE-1..BE-4 | — |

## Acceptance-criteria coverage
- Per-endpoint 429 on auth abuse, Redis-backed, global rule retained → **BE-1**
- Forgot-password latency indistinguishable (oracle closed), anti-enumeration intact → **BE-2**
- Welcome + reset emails localized (ar/en) → **BE-3**
- HTTPS metadata Dev-gated; DB secret from env; prod env vars documented → **BE-4**
- Security review of the above → **BE-5**

## Open decisions (for the lead)
- **BE-1:** per-endpoint thresholds (e.g. sign-in/forgot 5/min, register 3/min?) + confirm **Redis** store (vs in-memory). 
- **BE-3:** email-locale source — add a `Locale` field to the integration events (simplest) **vs** enrich via `IUserLookup`.
- **BE-4:** is the transport/DB-secret hygiene **Phase-1** scope or deferred to **P6** (stabilization)? Recommendation: BE-1/2/3 in this pass; BE-4 may defer to P6.

## Notes
- **Identity + Notifications-scoped**, parallel-safe; cross-module only via `Shared.Contracts` (BE-3 may add a `Locale` field to the existing reset/welcome events). No cross-module FK. No Unit of Work.
- **No new design pattern expected** — rate-limiting is config on the already-wired `AspNetCoreRateLimit`; email-locale is a field + the existing localizer. Ask-first only if that changes.
- Each per-PR brief's remaining Low/Mediums are non-blocking; this pass clears them as a unit before launch. Operational items (set prod env secrets, enable CAPTCHA, FE/legal Google consent) are tracked in HANDOFF, not code tasks here.
