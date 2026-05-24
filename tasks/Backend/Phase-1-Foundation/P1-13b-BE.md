# P1-13b (backend) — Phase 1 backend hardening pass

> Story: [../../../user-stories/Phase-1-Foundation/P1-13b-backend-hardening-pass.md](../../../user-stories/Phase-1-Foundation/P1-13b-backend-hardening-pass.md)
> Phase 1 · **Hardening pass (post-leftover)** · Module: **Identity + Notifications + Host** · Source: the per-PR security briefs (`docs/briefs/P1-12-*-security-audit.md`, `P1-13-*-security-audit.md`).

> **Status:** **BE-1 shipped in Phase 1; the remaining items were relocated to Phase 6 — see [P6-06-BE](../Phase-6-Stabilization/P6-06-BE.md).** This file is retained for traceability of the bundle's origin.

## Tasks
| ID | Task | Status |
|---|---|---|
| P1-13b-BE-1 | **Per-endpoint auth rate-limiting** (100 req/s per IP on the 5 anonymous auth endpoints; `EnableEndpointRateLimiting`; in-memory store; 429 on exceed) | ✅ **Done — PR #50** |
| ~~P1-13b-BE-2~~ | Forgot-password timing-oracle decouple | → **moved to [P6-06-BE-1](../Phase-6-Stabilization/P6-06-BE.md)** |
| ~~P1-13b-BE-3~~ | Localize transactional emails (welcome + reset, ar/en) | → **moved to [P6-06-BE-2](../Phase-6-Stabilization/P6-06-BE.md)** |
| ~~P1-13b-BE-4~~ | Transport/secret hygiene (Dev-gate HTTPS metadata; DB password via env) | → **moved to [P6-06-BE-3](../Phase-6-Stabilization/P6-06-BE.md)** |
| ~~P1-13b-BE-5~~ | security-auditor pass | → **moved to [P6-06-BE-5](../Phase-6-Stabilization/P6-06-BE.md)** |
| — | Multi-instance (Redis-backed) rate-limit store — follow-up from BE-1 | → **[P6-06-BE-4](../Phase-6-Stabilization/P6-06-BE.md)** |

## Notes
- **Lead decision:** the non-rate-limiting hardening follow-ups land in **Phase 6 (stabilization)** rather than Phase 1 — see [P6-06](../../../user-stories/Phase-6-Stabilization/P6-06-backend-security-hardening.md) + [P6-06-BE](../Phase-6-Stabilization/P6-06-BE.md). P1-13b is effectively complete (its only Phase-1 task, BE-1, is merged).
- BE-1 detail: `AspNetCoreRateLimit` endpoint rules `Limit=100, Period="1s"` per IP; brute-force is covered separately by the P1-13 account lockout; store stays in-memory for single-instance (Redis promotion is P6-06-BE-4).
