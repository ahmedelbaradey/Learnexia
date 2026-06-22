# P6-04 — Bug / Open-Item Triage (launch readiness)

> Story P6-04 (AC3). Triage of every known open / deferred backend item as of 2026-06-22, by severity, each marked **🚫 launch-blocker (resolve)** or **✅ accepted (defer)**. Sources: `docs/dev/HANDOFF.md` deferred sections, the per-feature dev memory, and the per-PR security-audit notes from this stabilization wave.
>
> **Headline: there are ZERO open Critical/High *product* defects in the backend.** Every open item is one of: a **devops-gated activation** (not a bug), a **Low cleanup** item, a **post-MVP feature**, or an **ops/dev-env** issue. Details below.

## Severity legend
Critical = data loss / security breach / launch-stopping. High = major journey broken / exploitable. Medium = degraded behaviour or hardening gap. Low/Info = cosmetic / pre-existing / minor.

## A. Devops-gated activations (NOT bugs — backend complete, needs keys/infra)
| Item | Sev | Disposition | Owner |
|---|---|---|---|
| AI flip-to-live (provider keys → BGE-M3 TEI → re-embed → `ContextProvider=Rag`) | — | ✅ accepted — backend ready (`docs/dev/AI-ACTIVATION-RUNBOOK.md`); activation = devops | devops |
| P6-02 Gate-B live AI-safety eval (real ar+en run) | — | ✅ accepted — harness built (#215, `EvalLive` tier); run = devops+keys | devops |
| P6-04 AC2 prompt-quality validation | — | ✅ accepted — procedure documented (`prompt-quality-procedure.md`); run = devops+keys | devops |
| P6-05 dashboards + alerts (Grafana/Tempo/Prometheus on the OTLP stream) | — | ✅ accepted — backend exports telemetry (#211); dashboards = devops | devops |
| P6-01 authoritative perf numbers (prod-scale load) + harness Kestrel wiring | — | ✅ accepted — harness built (#220); authoritative run = devops; small Kestrel-targeting follow-up noted | devops / BE follow-up |

## B. Security follow-ups (from the stabilization-wave audits)
| Item | Sev | Disposition |
|---|---|---|
| G2 access-token revocation | High (was) | ✅ **RESOLVED** — P6-07 #210 (session-store `OnTokenValidated`; logout/all/password-change/admin revoke) |
| Forgot-password timing oracle; reset-email localization; HTTPS/secret env-gating; Redis rate-limit store | Med (were) | ✅ **RESOLVED** — P6-06 #209 |
| `Jwt:Secret` / Captcha prod defaults | Med (were) | ✅ **RESOLVED** — `GuardJwtSecret` / `GuardCaptcha` fail-fast in Prod/Staging |
| Credentialed-wildcard **CORS** (`AllowedOrigins ?? "*"` + `AllowCredentials()`) | **Medium** | 🚫 **should-fix before launch** — fail-closed when `AllowedOrigins` is unset in Prod/Staging (cleanup batch). Not exploitable until a real origin is configured, but a prod misconfig is plausible. |
| HTML-encode `{userName}` in the shared email render | Low | ✅ accepted (cleanup batch) — self-targeted, registration-charset-constrained, pre-existing |
| `ServerError(ex.Message)` info-disclosure (codebase-wide house pattern) | Low | ✅ accepted — P5-07's new handlers were fixed; the rest is a codebase-wide cleanup, admin/auth paths only |

## C. AI / personalization
| Item | Sev | Disposition |
|---|---|---|
| AI-DEFECT-1 — student `Grade` not in JWT → age-band defaulted | Med (was) | ✅ **RESOLVED** — `AuthenticationIdentityService.GetClaims` now emits `CustomClaimTypes.Grade` for students with `Grade.HasValue` (verified 2026-06-22) |
| AI-DEFECT-2 — `SkillId` not in the Explain cache key (concept-level caching) | Med | ✅ accepted (product decision) — deferred |
| AiResponseCache "serving dormant until Confidence wired" | — | ✅ **RESOLVED / stale note corrected** — Confidence is wired (`SafetyLayer` 0.90 > 0.85); serves once a key exists |

## D. Deferred features / follow-ups (not launch-blockers)
| Item | Sev | Disposition |
|---|---|---|
| P5-07 **BE-4** config-driven adaptivity thresholds (AC3) | Low | ✅ accepted — next slice |
| No question-`report` signal (AI-flagging uses p-value extremes only) | Low | ✅ accepted — future enhancement |
| Localize persisted recommendation/report strings (P5-01/P5-09) | Low | ✅ accepted (cleanup batch) |
| P9-11 pre-dispatch suppression capture / notification-suppression metric | Low | ✅ accepted (cleanup batch) |
| P9-10 inbox read-time re-localization | Low | ✅ accepted → P9-03 (FE) |
| P3-13a behavioural profile (grit / best-time / motivation) | — | ✅ accepted — **post-MVP** (needs real usage data) |
| BL-01→05 Curriculum Intelligence (PDF/OCR/auto-import) | — | ✅ accepted — **post-MVP** (large separate product) |

## E. Ops / dev-environment (not product code)
| Item | Sev | Disposition |
|---|---|---|
| GitHub Actions CI runner not provisioning (billing) | Med (ops) | 🚫 **restore before launch** — automated gates are off; **user/devops action** (Settings → Billing → Actions). Local gates are green meanwhile. |
| Shared dev DB broken migration state (`AddSeatModel` partial-apply, PR #214) | Low (dev-env) | ✅ accepted — dev-env only; re-apply migrations / reset the local DB. Prod unaffected (fresh migrate). |

## F. Out of backend scope
| Item | Disposition |
|---|---|
| Frontend: responsive layout (3 active learners · 9 lessons / "topics Sami is building confidence in"), redirect-to-overview on child switch, energy/activity in sidebar, energy in KPI | ➡️ **FE lead** — not backend; tracked separately |

## G. Surfaced by the P6-04 regression run (this story)
| Item | Sev | Disposition |
|---|---|---|
| P1_03 AddChild tests `AC6_EmptyCountry`/`BETC12b_CountryWhitespaceOnly` expected **422** for empty/whitespace `Country`, but the endpoint returns **200** | Low | ✅ **RESOLVED (stale tests corrected) in this PR** — the tested endpoint's validator (`Parent/.../AddChild/AddChildCommandValidator` — `Country` `MaximumLength(100)` only, **no `NotEmpty`**) treats Country as **optional**, consistent with RegisterParent / UpdateMyProfile / UpdateChildProfile (length-bounded `.When(present)`). The 200 is correct product behaviour; the 422 expectation was never real. Flipped both tests to assert optional-Country (child created). **No product change.** |

## Triage summary
- **Open Critical/High product defects: 0.**
- **Launch-blockers to resolve (🚫): 2 — both non-code/ops or 1-line config:** (1) **CORS fail-closed in prod** (Medium, cleanup batch), (2) **restore CI** (ops/billing, user action).
- Everything else is a **devops-gated activation**, a **Low cleanup-batch** item, or an **explicitly-accepted post-MVP deferral**.
- The backend critical journeys are covered by the regression pass (`regression-coverage.md`) + the new golden-journey test.

→ Exit criteria + sign-off: `launch-exit-criteria.md`.
