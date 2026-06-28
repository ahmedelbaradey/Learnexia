# P6-04 — MVP Launch Exit Criteria + Sign-off (backend)

> Story P6-04 (AC4): "Exit criteria for launch are documented and signed off." This is the backend launch-readiness checklist + current assessment as of 2026-06-22. Scope: backend only (FE is a separate lead). Companion docs: `bug-triage.md`, `regression-coverage.md`, `prompt-quality-procedure.md`.

## Exit criteria (must all be ✅ to launch)

| # | Criterion | Status | Evidence / owner |
|---|---|---|---|
| EC-1 | **Zero open Critical/High product defects** | ✅ MET | `bug-triage.md` — 0 open Critical/High; all open items are devops-gated activations, Low cleanup, or post-MVP deferrals |
| EC-2 | **Security audits PASS** on all sensitive surfaces (auth, child data, AI, payments) | ✅ MET | Per-PR security-auditor PASS across the stabilization wave (G2 #210, P6-06 #209, P5-07 #216, P6-02 #215, P6-05 #211) — no Critical/High |
| EC-3 | **Access-token revocation works** (logout / logout-all / password-change / admin-revoke) | ✅ MET | P6-07 #210 — session-store `OnTokenValidated`; 13/13 + regression green |
| EC-4 | **Critical-journey regression green** (register → learn → quiz → progress → parent report) | ✅ MET | New `P6_04_CriticalJourneys_Tests` PASS + representative slice **257✓/0✗/4 skip**; 2 stale P1_03 tests corrected. Full ~95-file suite green = CI/devops once restored. `regression-coverage.md` |
| EC-5 | **Build clean (0 errors)** on `main` | ✅ MET | Verified each merge this wave |
| EC-6 | **Observability in place** (health checks + telemetry export) | ✅ backend MET · ⚠️ dashboards = devops | P6-05 #211 — `/health` (DB/Redis/AI-gateway/MinIO) + OTel OTLP; Grafana dashboards/alerts = devops |
| EC-7 | **AI safety validated** | ⚠️ offline MET · live = devops | P6-02 offline eval (62 cases) PASS; **Gate-B live ar+en run = devops+keys** (harness #215, `prompt-quality-procedure.md`) |
| EC-8 | **Performance within NFR-1** (API p95<500ms, AI<4s) | ⚠️ deferred to devops | P6-01 harness #220; authoritative numbers = devops live-Kestrel run (local = env-floor) |
| EC-9 | **Prod secrets/config gated** (JWT, Captcha, HTTPS, DB pwd, keys from env) | ✅ MET | `GuardJwtSecret`/`GuardCaptcha`/`IsProtectedEnvironment`; prod env-var table in HANDOFF |
| EC-10 | **CORS fail-closed in prod** | ✅ **MET (2026-06-23)** | `Host/Extensions/ServiceExtensions.cs ConfigureCors` ("Audit H2 fix"): Prod/Staging THROWS if `AllowedOrigins` unset/`*`; dev = `AllowAnyOrigin` WITHOUT credentials; prod = `WithOrigins`+`AllowCredentials`. (Doc previously marked OPEN — corrected.) |
| EC-11 | **Automated CI gates running** | 🚫 **OPEN (ops)** | GitHub Actions not provisioning (billing) — **user/devops action**; local gates green meanwhile |

## Launch-blockers remaining (must close before go-live)
1. **EC-11 — restore CI** (ops/billing — user/devops action). *(This is now the ONLY remaining launch-blocker — EC-10 CORS is resolved.)*

## Devops-gated activations (own alongside go-live, not code work)
- AI flip-to-live (keys → BGE-M3 TEI → re-embed → `ContextProvider=Rag`) — `AI-ACTIVATION-RUNBOOK.md`.
- Gate-B live AI-safety eval + prompt-quality validation (real keys) — `prompt-quality-procedure.md`.
- Authoritative perf run (prod-scale, live-Kestrel) — `docs/perf/P6-01-baseline.md`.
- Grafana dashboards + alerts on the OTLP stream.

## Sign-off
- **Backend launch-readiness: CONDITIONAL-GO** — all product/security exit criteria met; **1 launch-blocker remaining (CI restore — ops/billing)** + the devops-gated activations + the live Paymob/Fawry adapter (business decision + sandbox keys; stub wired #242) must be closed by the lead/devops. **EC-10 CORS resolved.** No open Critical/High product defects.
- Frontend readiness: separate (FE lead).
- _Sign-off owner / date: ______ (lead)._
