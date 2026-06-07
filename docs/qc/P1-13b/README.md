# QC Test Plan & Coverage Report — P1-13b (Backend hardening pass)

> **Surface:** Backend / HTTP API only. No student-app UI surface → **no `frontend-test-cases.md`** in this run (by design).
> **Owner:** QC test architect (design only). Implementation → `api-tester`. Results → `execution-report.md`.

---

## 1. Summary

**Story:** [P1-13b — Phase 1 backend hardening pass](../../../user-stories/Phase-1-Foundation/P1-13b-backend-hardening-pass.md)
**Tasks:** [tasks/Backend/Phase-1-Foundation/P1-13b-BE.md](../../../tasks/Backend/Phase-1-Foundation/P1-13b-BE.md)
**Batch scope:** the **only Phase-1 deliverable of P1-13b: BE-1 — per-endpoint IP rate-limiting on the anonymous auth endpoints** (PR #50). All other items in the original bundle were **relocated to Phase 6 (P6-06)** by lead decision — see §4.

### What changed in P1-13b (the surface under test)
A single, tightly-scoped change in **`backend/src/Host/Learnexia.Host/Extensions/ServiceExtensions.cs` → `ConfigureRateLimitingOptions(...)`**:

- `EnableEndpointRateLimiting = true` so `"{verb}:{path}"` rules are counted **per endpoint** (not folded into the global `*` rule).
- **Per-endpoint rules** added on top of the pre-existing global `* = 200/1m`:
  - **Development / Testing** (and any non-Production/Staging env): the 5 anonymous auth endpoints get a loose DoS ceiling of `Limit=100, Period="1s"` (sign-in, register-parent, google-signin, forgot-password, reset-password); `changepassword` keeps `5/15m`.
  - **Production / Staging** (env-gated, mirrors `GuardJwtSecret` env resolution): **tightened** brute-force / enumeration / anti-spam limits — sign-in `50/5m`, register-parent `10/15m`, google-signin `50/5m`, forgot-password `5/15m`, reset-password `10/15m`, changepassword `5/15m`.
- `EndpointWhitelist = [ "get:/health", "get:/health/live" ]` — health probes never throttled.
- Store stays **in-memory** (`MemoryCacheRateLimitCounterStore`) — single-instance; Redis promotion is **P6-06-BE-4**, out of scope here.
- Middleware wired in `Program.cs` as `app.UseIpRateLimiting()` (line ~214), **before** `UseAuthentication`/`UseAuthorization` — so anonymous endpoints are throttled prior to auth.
- Exceeding a rule → **HTTP 429 Too Many Requests** with a non-empty AspNetCoreRateLimit body.

> **No new abstraction introduced** (CLAUDE.md rule #8 respected) — pure `AspNetCoreRateLimit` config. Brute-force credential attempts remain covered by the separate **P1-13 account lockout** (5/5min); rate-limiting here is a DoS/enumeration ceiling, not a replacement for lockout.

### Counts
| Metric | Count |
|---|---|
| **Total cases** | **14** |
| Backend (`api-tester`) | 14 |
| Frontend | 0 (out of scope) |
| **P0** | 6 |
| **P1** | 5 |
| **P2** | 3 |
| Blocked / not-testable-as-written | 2 (BE-TC-12, BE-TC-13 — see notes) |

> An existing integration suite already covers the core of this feature: `backend/tests/Learnexia.IntegrationTests/P1_13b_BE1_AuthRateLimit_Tests.cs` (AC-RL-1..6). The cases below **trace to and extend** that suite — `api-tester` should reconcile against it (reuse the deterministic `RateLimitWebAppFactory` PostConfigure-override pattern rather than hammering 100 req/s) and add the gaps it does not yet cover (per-endpoint isolation across all 5 routes, 429 envelope/`Retry-After` shape, Production-rule resolution).

---

## 2. Coverage matrix (acceptance criterion → case IDs)

P1-13b's acceptance criteria, scoped to the in-Phase-1 BE-1 item. Relocated criteria are listed for traceability and marked **deferred to P6-06** (not tested here).

| # | Acceptance criterion (story §AC) | In Phase-1 scope? | Case IDs | Verdict |
|---|---|---|---|---|
| AC-1 | Per-endpoint rate limit on the 5 anonymous auth endpoints; **429** on exceed, in addition to the global rule | ✅ Yes (BE-1) | BE-TC-01, 02, 03, 04, 05, 06 | **Covered** |
| AC-1a | Limit is **per-endpoint** (one endpoint's burst does not throttle another) | ✅ Yes (BE-1) | BE-TC-07, 08 | **Covered** |
| AC-1b | 429 response is well-formed (non-empty body; `Retry-After` / standard headers) | ✅ Yes (BE-1) | BE-TC-09, 10 | **Covered** |
| AC-1c | Health probes whitelisted — never throttled | ✅ Yes (BE-1) | BE-TC-11 | **Covered** |
| AC-1d | Normal under-limit traffic unaffected; valid sign-in still 200; existing suites unaffected (regression) | ✅ Yes (BE-1) | BE-TC-05, BE-TC-14 | **Covered** |
| AC-1e | Production/Staging tightened limits resolve correctly (env-gated, mirrors `GuardJwtSecret`) | ✅ Yes (BE-1, secondary) | BE-TC-12 (**blocked** — see notes) | **Design-covered; exec blocked** |
| AC-1f | "Multi-instance-safe (Redis-backed store)" | ❌ **No** — store stays in-memory; **Redis → P6-06-BE-4** | BE-TC-13 (**not-testable**, documents the blocker) | **Deferred to P6-06** |
| AC-2 | Forgot-password timing oracle closed (out-of-band email) | ❌ **No** — **→ P6-06-BE-1** | — | **Deferred to P6-06** |
| AC-3 | Transactional emails localized (ar/en) | ❌ **No** — **→ P6-06-BE-2** | — | **Deferred to P6-06** |
| AC-4 | Transport/secret hygiene (Dev-gate HTTPS metadata; DB pwd via env) | ❌ **No** — **→ P6-06-BE-3** | — | **Deferred to P6-06** |
| AC-5 | Passes security-auditor review | ✅ process gate | n/a (reviewer/security-auditor, not a test case) | n/a |

**Coverage verdict:** Every **in-Phase-1** acceptance criterion (the BE-1 rate-limiting feature) has at least one P0/P1 case. **No in-scope gap.** AC-1e is design-covered but its execution is blocked on a Production-environment test host (see open questions). AC-2/AC-3/AC-4 and the Redis store (AC-1f) are **out of P1-13b scope** — relocated to P6-06 — and are intentionally **not** tested here; they will be QC'd under a P6-06 run.

---

## 3. Risk notes (where cases are weighted and why)

1. **Highest weight → 429-on-exceed correctness across all five anonymous endpoints (BE-TC-01..06).** This is the entire security value of the story. The existing suite only exercises sign-in (and a register-parent isolation check); the other four routes (google-signin, forgot-password, reset-password) have rules but no direct "exceed → 429" assertion. Forgot/reset are the highest-value abuse targets (enumeration / reset-token spray), so they get dedicated P0 cases.
2. **Per-endpoint isolation (BE-TC-07, 08).** `EnableEndpointRateLimiting=true` is the load-bearing toggle; if it regresses to `false`, all auth traffic folds into the global `*` counter and the per-endpoint limits silently disappear. A burst on endpoint A must not throttle endpoint B.
3. **Middleware ordering / pre-auth throttling (BE-TC-03, 06).** `UseIpRateLimiting()` sits before `UseAuthentication`. Anonymous endpoints must be throttled regardless of (absent) credentials — confirmed by hitting forgot/reset with junk bodies and still tripping 429.
4. **Whitelist not over-broad / not bypassable (BE-TC-11).** Health probes must stay un-throttled (orchestrator liveness), but the whitelist must not accidentally cover auth routes.
5. **No collateral breakage (BE-TC-05, 14).** A real seeded sign-in under the limit must still return a 200 + valid `BaseResponse<JwtAuthResponse>` envelope (`Successed=true`, non-empty `accessToken`); the main integration suite (which sets `* = int.MaxValue`) must not start failing.
6. **Lower weight → Production-rule resolution (BE-TC-12).** Correct, but second-order: the tightened numbers only matter in prod/staging, and the env-gating mirrors an already-tested pattern (`GuardJwtSecret`). Marked P1 and flagged blocked because the standard `Testing` host can't easily exercise the `Production` branch without a dedicated factory.

---

## 4. Open questions / assumptions (lead must resolve before/at implementation)

1. **Is P1-13b distinct from P1-13, or overlap?** — **Distinct but narrow.** P1-13 shipped the *account lockout + CAPTCHA + reset-token* auth surface and explicitly logged the missing tighter rate-limit as an **Info-level follow-up** (P1-13 security audit, finding #4). P1-13b is the **follow-up bundle** that closes that finding (BE-1). So P1-13b **does not duplicate** P1-13 — it adds the per-endpoint rate-limit layer on top. The lockout (P1-13) and the rate limit (P1-13b) are **complementary** controls. **Net:** P1-13b's only live Phase-1 surface is the rate-limit config; the rest of its bundle moved to P6-06.
2. **Assumption — scope is BE-1 only.** Per the task file, BE-2/3/4/5 and the Redis store are relocated to P6-06; this QC run scopes cases to BE-1 only and marks the relocated criteria deferred. **Confirm** the lead does not want P6-06 items pulled forward.
3. **Production-rule exec (BE-TC-12):** the existing tests run env `Testing` (loose 100/1s) and the deterministic factory overrides the rules entirely. To assert the **tightened Production numbers** (e.g. forgot-password `5/15m`) we need a test host that resolves the `Production`/`Staging` branch of `ConfigureRateLimitingOptions`. **Decision needed:** is asserting the exact Production thresholds in scope for this QC pass, or is the env-gating logic considered covered by code review + the `GuardJwtSecret` precedent? (Cheapest option: a small dedicated `WebApplicationFactory` with `UseEnvironment("Production")` + supplied secrets, asserting one tightened rule trips at N+1.)
4. **`Retry-After` / `X-Rate-Limit-*` headers (BE-TC-10):** AspNetCoreRateLimit can emit these; the current config does not customize `QuotaExceededResponse`. **Confirm** whether a populated `Retry-After` header is a requirement (FE may need it for back-off UX) or whether a non-empty 429 body is sufficient (current behaviour). If not required, BE-TC-10 drops to P2 / informational.
5. **IP source under proxy (BE-TC-04):** the host uses `UseForwardedHeaders(ForwardedHeaders.All)`; counters key on the resolved client IP. The tests spoof `X-Real-IP`. **Confirm** the production ingress sets a trusted forwarded header so the per-IP counter keys on the real client and not the ingress IP (otherwise all users share one counter — a correctness/availability risk worth a note even if untestable in-suite).

---

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| [`backend-test-cases.md`](./backend-test-cases.md) | **`api-tester`** | Implement BE-TC-01..14 as integration tests. **Reuse** the existing deterministic pattern in `P1_13b_BE1_AuthRateLimit_Tests.cs` (`RateLimitWebAppFactory` + `PostConfigure<IpRateLimitOptions>` tiny-limit override + per-test spoofed `X-Real-IP`) — **do not** fire real 100 req/s bursts. Reconcile with existing AC-RL-1..6; add only the gaps. |
| [`execution-report.md`](./execution-report.md) | **`api-tester`** (fills after running) | Record pass/fail per BE-TC, defects, and final status. QC scaffolds the empty template only; testers fill results. |

**How `execution-report.md` gets filled:** `api-tester` runs the implemented integration tests, then edits `execution-report.md` — one row per BE-TC with Pass/Fail/Blocked, observed status code + body shape, and any defect ID. QC never fills results.

---

**Test cases ready — `api-tester` to implement `backend-test-cases.md`; results into `execution-report.md`.** (Backend-only run — no `frontend-test-cases.md`.)
