# Pipeline Brief — Phase 10: Payments, Billing & Credits (Energy)

> **Consolidated, build-ready brief for the whole Phase-10 cluster (12 stories, P10-01..P10-12).**
> Supersedes the per-story briefs `docs/briefs/P10-01.md … P10-12.md` and plans `docs/plans/P10-01.md … P10-12.md` (authored in PR #124) for the purpose of validation + sequencing. The per-story briefs remain useful detail, but they were written **before** the current `main` advanced — several of their "current-state findings" are now stale and are corrected here. This brief is what the `planner` should wave.
>
> **STATUS: ZERO Phase-10 code exists.** No `Billing` module, no credit/subscription/payment entity anywhere (verified). Phase 10 is genuinely greenfield except for two already-shipped seams (see Drift §0).
>
> **TWO HARD LEAD GATES block the entire cluster (do NOT let the planner wave past them):**
> 1. **Confirm-before-scaffold the new `Billing` module** (CLAUDE.md "ask before new modules").
> 2. **Payment-provider decision** (Paymob vs Fawry vs generic-abstraction-with-fake) — gates P10-06/07/09 implementation specifics.
>
> The rest of the cluster (the credit/energy ledger + grant/spend/cap + admin/settings) is **buildable and mergeable now against a fake/seam**, with the **AI tie-in** wiring into the **existing** Ai module handlers.

---

## §0 — Drift reconciliation: what changed in `main` since PR #124

The per-story briefs (#124) assumed: no `Ai` module, no `IGlobalSettingsProvider`, no `AiResponseCache`. **All three now exist.** Corrections, with evidence:

| # | Per-story brief said | Reality in current `main` | Impact on Phase-10 plan |
|---|---|---|---|
| D1 | `IGlobalSettingsProvider` does not exist; **P10-12 must create the contract + bootstrap impl** | **Already exists** — `backend/src/Shared/Learnexia.Shared.Kernel/Settings/IGlobalSettingsProvider.cs` (4 typed getters: `GetInt/GetDecimal/GetString/GetBool`) + `BootstrapDefaultGlobalSettingsProvider.cs` (reads `IConfiguration`, `appsettings.json` `AiHelper:Cache:*`). Shipped with Phase 4 exactly as the cross-phase seam predicted. | **P10-12 is now a DELTA, not a greenfield build.** It must NOT recreate the interface. The interface signature is **leaner than the P10-12 brief's proposed shape** (no named convenience getters like `FreeMonthlyCredits`, no `InvalidateCacheAsync`). Scope = (a) DB-backed `GlobalSetting` store, (b) a DB-backed impl that **replaces** the bootstrap impl at DI, (c) Redis/memory cache + invalidation, (d) admin write + audit, (e) seed managed keys. **Decide: keep the 4-getter interface as-is (callers already use string-keyed `GetX(key, default)`) — recommend yes; do NOT add named getters (would be a breaking signature change to live Ai handlers).** |
| D2 | `Ai` module / AI Gateway (P3-01) **not built**; P10-03 is "hard-blocked, cannot be implemented" | **Built.** `backend/src/Modules/Ai/...` — 4 Helper handlers (`GetHintCommandHandler` [Hint+WhyWrong], `ExplainConceptCommandHandler`, `SimilarExampleCommandHandler`, `SimplifyExplanationCommandHandler`), `ISafetyLayer`, `IAiGateway`, `IAiResponseCache` + `AiResponseCache` entity + repo, `IAiTutorRateLimiter` (in-proc + Redis). | **P10-03 is now BUILDABLE** (its dominant cross-cluster blocker is gone). The debit hook-points are concrete and visible in the handlers (cache-hit return, refuse-and-redirect, safety-block, success delivery). The cross-cluster sequencing OQ in the P10-03 brief is **resolved**. |
| D3 | P10-03 "removes the `IAiUsageBudget` daily-count guardrail" | **No `IAiUsageBudget` exists** (verified — zero matches). The runtime cost/abuse guard that actually exists is `IAiTutorRateLimiter` (per-student fixed-window, 10 req/60s, in-proc or Redis). The deferred P3-01-BE-14 (`IAiUsageBudget`/quota) was **never built**. | **P10-03 AC-7 changes meaning.** There is no daily-count budget to "tear down." The decision is now: does the energy debit **replace** `IAiTutorRateLimiter`, **coexist** with it, or does P10-03 newly **introduce** the quota/budget concept that P3-01-BE-14 deferred? See OQ-AI-1. The energy ledger is the natural home for the deferred quota — P10-03/P10-04 effectively **land** P3-01-BE-14. |
| D4 | Ai handlers don't consume a settings seam | Handlers **already** read `IGlobalSettingsProvider` (e.g. `_settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m)` in `GetHintCommandHandler`). | The pattern P10-03/04 need (resolve per-action cost from `IGlobalSettingsProvider` server-side) is **already in use** in the same handlers — mirror it. No new config-reader seam (`IBillingConfigReader`) is needed; the P10-12 brief already supersedes `BillingConfigVersion`/`IBillingConfigReader` with `IGlobalSettingsProvider` (D6). |
| D5 | Modules: Identity, Catalog, Learning, Gamification, Parent, Moderation, Notifications | Now **also** `Ai` and `Curriculum`. **Still no `Billing`.** Schemas in use: `identity`, `catalog`/`learning`, `notifications`, `gamification`, `parent`, `moderation`, `ai`, `curriculum`. **`billing` is net-new.** | Confirms the `Billing` module is still greenfield and still needs sign-off. Mirror **Ai or Curriculum** (the two most recently scaffolded modules) for the new-module shape — `Ai.Infrastructure/DependencyInjection.cs` is the cleanest recent reference. |
| D6 | P10-11 uses a bespoke `BillingConfigVersion` + `IBillingConfigReader` | The P10-12 brief already reconciled this: **P10-11 writes through `IGlobalSettingsProvider`/`UpdateGlobalSettingCommand`; `BillingConfigVersion`/`IBillingConfigReader` are dropped.** Since neither was ever built, this is just "don't build them." | **P10-11 = a thin admin write-surface over P10-12's store.** No separate config entity. P10-11 and P10-12 collapse into one config story (settings store + admin write/audit). |
| D7 | `AiResponseCache` / cache-charged-same is theoretical | `AiResponseCache` entity + `AiResponseCacheRepository` + `IAiResponseCache.GetApprovedAsync/WriteAsync` are live; cache-hit returns early (e.g. `GetHintCommandHandler` step "cache HIT"). | **P10-03 AC-3 (cache-hit charged same)** has a concrete insertion point: the cache-HIT branch must debit identically to the live branch. The branch is visible and chargeable. |

**Net effect of drift:** Phase 10 got *easier and more sequenceable*, not harder. P10-03 unblocks (Ai exists). P10-12 shrinks to a DB-backed-store delta (contract already exists). P10-11 folds into P10-12. The only genuinely external dependency remains the **payment provider** (unchanged).

---

## §1 — Scope + story map

12 stories, 4 functional groups + 1 cross-cutting. Status tagged vs current code. **Build tag:** `[BUILD]` = buildable + mergeable now on this backend; `[BUILD-FAKE]` = buildable now **behind a fake provider seam**, real provider = external; `[EXTERNAL]` = needs a live payment account / secrets / devops; `[FE]` = frontend, **out of scope for this backend lead**.

### Group A — Credit / energy ledger (the foundation + the AI tie-in)
| Story | Title | SP | Build tag | Status vs code |
|---|---|---|---|---|
| **P10-01** | Credit (energy) account & append-only ledger | 5 | **[BUILD]** | Greenfield. FOUNDATION — hard-blocks A/B/parts of C. |
| **P10-02** | Grant monthly energy per plan (Hangfire job + expiry) | 5 | **[BUILD]** | Greenfield. Hangfire pattern exists (`StreakSweepJob`). Needs plan-tier source (P10-05 contract or config default). |
| **P10-03** | Spend energy on AI help (charge-on-delivery) | 5 | **[BUILD]** | **Now buildable** — Ai module + handlers exist (D2). Wires the debit into the existing 4 handlers. **Most security-sensitive story.** |
| **P10-04** | Daily soft cap & low-energy warning | 3 | **[BUILD]** | Greenfield. Timezone-reset precedent exists (`StreakDayCalculator`). |

### Group B — Subscriptions + payments
| Story | Title | SP | Build tag | Status vs code |
|---|---|---|---|---|
| **P10-05** | Manage subscription plan (Free/Premium, Monthly/Annual) | 5 | **[BUILD]** (backend) + [FE] | Greenfield. Plan/Subscription model + change rules. Source of truth for P10-02's tier. FE (parent "Plan & billing" tab) = out of scope here. |
| **P10-06** | Pay for a subscription (payment provider) | 8 | **[BUILD-FAKE]** + [EXTERNAL] + [FE] | Greenfield. `IPaymentProvider` seam + checkout + idempotent webhook + recurring + cancel — **buildable + testable with a FAKE provider**; live Paymob/Fawry = external. |
| **P10-07** | Buy an energy pack (never expires) | 5 | **[BUILD-FAKE]** + [EXTERNAL] + [FE] | Greenfield. One-off purchase → `PurchasedBalance` (P10-01 `ApplyPurchase`). Reuses the P10-06 payment seam. |
| **P10-09** | Failed payments & refunds (dunning + clawback) | 5 | **[BUILD-FAKE]** + [EXTERNAL] | Greenfield. Dunning (Hangfire + Notifications) + idempotent refund clawback (P10-01 `Refund`). Reuses P10-06 webhook + provider seam. |

### Group C — History / receipts
| Story | Title | SP | Build tag | Status vs code |
|---|---|---|---|---|
| **P10-08** | Billing history & receipts | 3 | **[BUILD]** (backend) + [FE] | Greenfield. Parent-scoped read of payments + receipt/invoice generation (PDF/printable). Backend read + receipt fields buildable; VAT/legal fields need finance confirmation. FE = out of scope. |

### Group D — Admin + settings (collapsed — see D6)
| Story | Title | SP | Build tag | Status vs code |
|---|---|---|---|---|
| **P10-12** | Runtime-configurable economy via Global Settings | 5 | **[BUILD]** | **DELTA only** — contract + bootstrap impl already exist (D1). Build = DB-backed store + cache/invalidation + admin write + audit + seed. |
| **P10-11** | Admin: configure plans, grants & action costs | 3 | **[BUILD]** | **Folds into P10-12** (D6) — thin admin write-surface (`UpdateGlobalSettingCommand` + history) behind the P7 admin policy. No separate config entity. |

### Out of scope for THIS backend lead (FE)
| Story | Title | SP | Tag | Note |
|---|---|---|---|---|
| **P10-10** | Kid-facing energy UI (⚡ طاقة المساعد) | 5 | **[FE — EXCLUDED]** | **Student-app UI, read-only.** `designer → frontend → frontend-e2e-tester`. **EXCLUDED from this cluster's build.** The only backend obligation is that P10-03/P10-04 expose a **child-scoped energy-status read** (`EnergyStatusQuery`) the FE will consume — that read is delivered in P10-04 (see §4). Also FE-only: the parent "Plan & billing" tab (P10-05-FE), checkout/return screens (P10-06-FE), billing-history view (P10-08-FE), admin billing screen (P10-11-FE). Flag all of these to the frontend lead; they are not built here. |

---

## §2 — New `Billing` module — CONFIRM-BEFORE-SCAFFOLD

**Confirmed: no `Billing` module exists** (Grep for `Billing|CreditAccount|Subscription|IPaymentProvider` across `backend/src` = zero matches; module list = Identity, Catalog, Learning, Gamification, Parent, Moderation, Notifications, Ai, Curriculum). **Phase 10 needs it** — there is no existing module that is a sensible home for a credit ledger + subscriptions + payments (it is a distinct bounded context: "money + credits").

**🛑 GATE-1 (CLAUDE.md "ask before new modules" + MEMORY ask-before-new-modules): the lead must approve creating the `Billing` module before ANY implementer (db-migration) scaffolds it.** Do not let the planner dispatch P10-01-BE-1 until this is signed off.

**Proposed shape (mirror `Ai`/`Curriculum` — the two most recent scaffolds):**
- 4 projects: `Learnexia.Modules.Billing.Domain` / `.Application` / `.Infrastructure` / `.Api`.
- `BillingDbContext` — `public const string Schema = "billing"`; `HasDefaultSchema("billing")`; `MigrationsHistoryTable("__EFMigrationsHistory", "billing")`; `ConfigureWarnings(... Ignore(PendingModelChangesWarning))`; `UseNpgsql(GetConnectionString("Default"))`; audit-stamping `SaveChangesAsync(int userId)` override (mirror existing modules); design-time `BillingDbContextFactory`. Reference `AiDbContext` registration in `Ai.Infrastructure/DependencyInjection.cs` as the cleanest recent template.
- `AddBillingApplication` (AutoMapper profiles + FluentValidation validators; **do not** `AddMediatR` per module convention) + `AddBillingInfrastructure` (DbContext + repos + service impls).
- **Serialized shared-file edits** (PARALLELISM.md): `Learnexia.Modular.sln`, `Host/Program.cs` (`AddBillingModule` + `InitializeAsync` migrate hook), `Claims.GenerateModules()` adds `"Billing"`, possibly `Directory.Packages.props`. These must be serialized against any other in-flight work.
- **Schema-per-module `billing`.** One coherent home for ALL Phase-10 credit-economy + subscription + payment entities. **Do NOT split the ledger into the `ai` schema** — the earlier `ai-cost-routing.md §6` note that suggested `ai.AiCreditTransaction` is **superseded**; the task files already lock `billing` (P10-01-BE OQ-2 = `billing`). Confirm this once at GATE-1.
- **Where does `IGlobalSettingsProvider`'s DB-backed store live?** It is **NOT** a Billing concern — the contract + bootstrap impl already live in `Shared.Kernel`. The DB-backed store (P10-12) should also live in `Shared.Kernel` (or a small `Platform` project) using a `platform`/`settings` schema, **not** inside `Billing` (so the Ai module + future modules consume it without coupling to Billing). See OQ-CFG-1.

---

## §3 — Buildable-backend vs EXTERNAL/devops vs FRONTEND split

This is the core ask. The principle (mirroring how the **AI gateway + TEI** were done — config-driven against a provider seam, fully testable with a fake): **every payment path is built behind an `IPaymentProvider` abstraction with a FAKE implementation; nothing requires a live merchant account to build, test, or merge.**

### [BUILD] — fully buildable + mergeable NOW (no external account)
- **P10-01** credit account + append-only ledger + atomic Granted-first debit + idempotency (DB-unique) + reconciliation. Mirror `Gamification.Domain/Entities/XpAward.cs` (append-only, `FullAuditedEntity`, no mutation methods, DB-unique idempotency key).
- **P10-02** Hangfire monthly grant + expiry job. Mirror `StreakSweepJob` (`[DisableConcurrentExecution]`, fresh DI scope, `ISystemClock`, idempotent re-run, per-row fail-soft).
- **P10-03** energy debit wired into the **existing** Ai handlers (charge-on-delivery, cache-charged-same, no-charge-on-refuse/error/insufficient). No external dependency — the Ai module is live.
- **P10-04** daily soft-cap counter + low-energy thresholds + timezone reset (mirror `StreakDayCalculator`) + the shared `EnergyStatusQuery` read seam.
- **P10-05** Plan/Subscription model + upgrade/downgrade/cancel commands + plan-comparison query + the `Shared.Contracts` plan/tier seam for P10-02. (Payment *activation* is completed by P10-06; P10-05 ships `PendingPayment` transition.)
- **P10-08** parent-scoped billing-history read + receipt/invoice **generation** (the PDF/printable output is buildable; the legal/VAT field *values* need finance confirmation — see OQ-FIN-1).
- **P10-12** DB-backed `GlobalSetting` store + DB-backed `IGlobalSettingsProvider` impl (replaces bootstrap at DI) + Redis/memory cache + invalidation + seed managed keys.
- **P10-11** admin `UpdateGlobalSettingCommand` + history query + validation, behind the P7 admin policy + Moderation audit seam.
- **The `IPaymentProvider` seam itself + a `FakePaymentProvider`** (always-succeeds / configurable-decline / replayable-webhook). This is [BUILD] and is what makes P10-06/07/09 testable.

### [BUILD-FAKE] — buildable + testable NOW behind the fake, live wiring = EXTERNAL
- **P10-06** checkout-session create, idempotent webhook handler (signature-verify + dedupe on provider event id), `Payment`/`WebhookEvent` entities, subscription activation, recurring/cancel state, `SubscriptionActivated`/`PaymentFailed` events. **Built + tested against `FakePaymentProvider` + a synthetic signed webhook.** What is [EXTERNAL]: the **real adapter** (`PaymobPaymentProvider` / `FawryPaymentProvider`), real signing secret, real sandbox keys, the live webhook URL.
- **P10-07** buy-energy-pack one-off purchase → `ApplyPurchase` into `PurchasedBalance`, assigned to a specific child. Reuses the P10-06 seam; testable with the fake.
- **P10-09** dunning (Hangfire) + idempotent refund clawback (unspent-only, clamped ≥ 0) + parent notification via `Shared.Contracts` → Notifications. Testable with synthetic `charge.failed` / `refund.succeeded` events through the fake.

### [EXTERNAL] — NOT buildable here; lead/devops owns
- **Provider choice** (Paymob vs Fawry) + commercial onboarding/settlement.
- **Real provider adapter** implementing `IPaymentProvider` against the chosen provider's API/SDK.
- **Secrets provisioning**: provider API key + webhook signing secret in the secret store (never committed).
- **Live webhook endpoint** registration + reachability (devops).
- **Recurring/auto-renew capability confirmation** (not every EGP provider supports true card-on-file recurring — see OQ-PAY-2).
- **App Store / Play Store IAP-compliance review** (web-checkout-only strategy) — a **legal/commercial launch-gating** review, flagged to `security-auditor` but resolved by the lead, not code.

### [FE] — out of scope for this backend lead (flag to frontend lead)
- **P10-10** kid energy UI (the entire story). Backend obligation = the `EnergyStatusQuery` child-scoped read (delivered in P10-04).
- Parent FE: P10-05 "Plan & billing" tab, P10-06 checkout/return screens, P10-07 pack-purchase screen, P10-08 history/receipts view.
- Admin FE: P10-11 billing-config screen.

**Bottom line for the lead:** the **entire Phase-10 backend** — ledger, grant, spend, cap, subscription model, payment orchestration, packs, refunds/dunning, history, settings/admin — is **buildable and mergeable without a live payment account**, exactly as the AI gateway was built against a seam. The ONLY hard external blocker is the **provider choice + live adapter + secrets**, and that blocks only the *real adapter swap*, not the orchestration/tests/merge.

---

## §4 — The AI tie-in (P10-03 / P10-04 ↔ the existing Ai module)

This is the highest-value, highest-risk integration and the place the **deferred P3-01-BE-14 (quota/`IAiUsageBudget`) actually lands**.

### What exists (the hook-points are concrete)
The 4 Helper handlers (`GetHintCommandHandler` covers Hint+WhyWrong; `ExplainConceptCommandHandler`; `SimilarExampleCommandHandler`; `SimplifyExplanationCommandHandler`) follow this pipeline (verified in `GetHintCommandHandler`):
1. Resolve `studentId` from JWT (`ICurrentUserService.UserId`).
2. **`IAiTutorRateLimiter.TryAllow(studentId)`** — the existing cost/abuse guard (10 req/60s, in-proc or Redis).
3. Fire `HelpRequested` event.
4. Resolve grade/age/language; fetch correct answer + grounding context.
5. **Scope guard:** empty chunks → **refuse-and-redirect** (`HintResult.Redirect`) — *no charge must occur here.*
6. **Cache-first:** `IAiResponseCache.GetApprovedAsync(key)` → **cache HIT returns early** (`HintResult.Streamed`) — *this branch must charge the same as live (AC-3).*
7. Cache MISS → `ISafetyLayer.GenerateSafeAsync` → safety verdict; **safety-block → error** (*no charge*); no-reveal violation → error (*no charge*).
8. **Success delivery** → cache-write (fire-and-forget) + `HelpDelivered` event → return `HintResult.Streamed`. ***This is the debit point.***

### The cross-module seam (no cross-module FK — `Shared.Contracts`)
- P10-01 declares **`ICreditSpendService`** in `Shared.Contracts/Billing/`:
  - `Task<DebitResult> TryDebitAsync(int childId, int amount, string reasonCode, string idempotencyKey, CancellationToken ct)`
  - `Task<EnergyBalance> GetBalanceAsync(int childId, CancellationToken ct)`
  - Implemented in `Billing.Infrastructure`; the **Ai module consumes the contract only** (rule 1 — no `Billing` project reference). This mirrors how Ai already consumes `Shared.Contracts` seams (`IQuestionAnswerContract`, `ILearningContextProvider`, `IGlobalSettingsProvider`).
- **Per-action cost is resolved server-side from `IGlobalSettingsProvider`** (already injected into these handlers — D4). The handler maps the intent → a cost key → `_settings.GetInt("credits.cost.hint", 1)` etc. **The client never sends a cost** (security-critical).

### The debit placement (charge-on-delivery)
- **Pre-check (cheap, no debit):** before the cache/LLM call, `GetBalanceAsync(childId)`; if `< cost`, short-circuit to a **graceful low-energy decline** (reuse the existing refuse/redirect-style branch) + serve a cached canned response if available. **No gateway call, no debit, no error** (AC-5, never-block-learning / FR-AI-6).
- **Debit (last successful step):** at step 6 (cache HIT) **and** step 8 (live success), after safety pass, call `TryDebitAsync(childId, cost, reasonCode, idempotencyKey)` where `idempotencyKey` = the AI request/action id (one charge per delivered response). On `InsufficientBalance` at delivery (rare race) → graceful decline, no charge (OQ-AI-3).
- **No charge on refuse (step 5) / safety-block + no-reveal (step 7) / error** — the debit is unreachable on those branches because it is the final delivery step.
- **Cache HIT charged same** (step 6) — the debit runs identically in the HIT branch.

### Where P3-01-BE-14 (deferred quota/`IAiUsageBudget`) lands
- There is **no `IAiUsageBudget`** today; the only runtime guard is `IAiTutorRateLimiter` (abuse, not economy). The deferred per-child quota/budget concept is **fulfilled by the energy ledger**: monthly grant (P10-02) + daily soft cap (P10-04) + per-call debit (P10-03) **are** the quota system P3-01-BE-14 sketched.
- **Decision (OQ-AI-1):** do energy + the rate limiter **coexist** (rate limiter = pure anti-abuse spam guard; energy = economy/quota) — *recommended* — or does energy **replace** the rate limiter? The original P10-03 brief's "remove the daily-count guardrail" (AC-7) **no longer applies as written** (there is no daily-count budget); reinterpret AC-7 as "the energy debit becomes the economy gate; the per-request rate limiter stays as the abuse guard." Confirm with lead.

### Ordering inside the AI tie-in
- **P10-01 (ledger + `ICreditSpendService`) MUST land before P10-03 (spend).** Non-negotiable — P10-03 is thin wiring over the contract.
- **P10-04 (daily counter)** interlocks with P10-03: the spend path increments the daily counter inside the same atomic debit (recommended), and the low-energy threshold feeds P10-03's decline copy. P10-04 can build its `EnergyStatusQuery` + counter independently; the P10-03 increment hook lands when P10-03 does.

---

## §5 — Dependency-ordered work-item inventory + wave shape

Per-agent handoffs are detailed in the per-story briefs/task files; this is the consolidated, drift-corrected sequence. Tags: `[BUILD]` / `[BUILD-FAKE]` / `[EXTERNAL]` / `[FE]`.

### Hard dependency edges
- P10-01 → P10-02, P10-03, P10-04, P10-05, P10-07, P10-08, P10-09 (the ledger underlies everything).
- P10-12 (settings store) → consumed by P10-02 (grants), P10-03 (costs), P10-04 (caps), P10-07 (pack price). All can build against `IGlobalSettingsProvider` **today** (it exists) with config defaults, then read the DB-backed store when P10-12 lands. **So P10-12 is not a hard blocker — the seam is already live.**
- P10-05 → P10-02 (tier source; mitigated by a config-default tier seam), P10-06 (activation).
- P10-06 → P10-07 (reuses payment seam), P10-09 (reuses webhook + provider seam).
- P10-03 → P10-10 [FE] (meter reflects post-spend balance) and P10-04 (decline copy).

### Recommended wave shape (each wave a reviewer-gated batch; parallel where independent)

```
🛑 GATE-1: approve `Billing` module scaffold.
🛑 GATE-2: payment provider decision (or "generic abstraction + FakePaymentProvider for now" — recommended to unblock).
🛑 Resolve the §6 economy OQs (currency unit, per-call vs per-day, hard vs soft block) — needed by Wave 1/2.

── WAVE 1 — Ledger foundation + settings store (parallel pair) ──────────────
  W1a [BUILD]  P10-01  Billing module scaffold + CreditAccount + CreditTransaction
                       + atomic Granted-first DebitAsync + idempotency + reconciliation
                       + ICreditSpendService (Shared.Contracts/Billing).   ⟵ FOUNDATION
  W1b [BUILD]  P10-12  GlobalSetting DB store + DB-backed IGlobalSettingsProvider
                       (replaces bootstrap at DI) + Redis/memory cache + invalidation
                       + seed managed keys (the contract already exists — DELTA only).
       (W1a and W1b are independent — different schemas, no shared entity. Serialize only the
        Program.cs/.sln/Claims edits. db-migration + backend-feature per wave; security-auditor
        light on W1a ledger integrity; mandatory on neither yet.)

── WAVE 2 — Grant / spend / cap + admin write (after W1) ────────────────────
  W2a [BUILD]  P10-02  Hangfire monthly grant + expiry job (mirror StreakSweepJob),
                       config-driven allotment via IGlobalSettingsProvider, tier via
                       P10-05 seam or config default.
  W2b [BUILD]  P10-03  Energy debit wired into the existing 4 Ai handlers
                       (charge-on-delivery, cache-charged-same, no-charge-on-refuse/error/
                       insufficient). ⚠️ MANDATORY security-auditor (spend integrity).
  W2c [BUILD]  P10-04  Daily soft-cap counter + low-energy thresholds + timezone reset
                       + EnergyStatusQuery (child-scoped read; the seam P10-10 [FE] consumes).
  W2d [BUILD]  P10-11  Admin UpdateGlobalSettingCommand + history + validation
                       (behind P7 admin policy + Moderation audit). ⚠️ security-auditor (admin authz).
       (W2a/W2c independent of W2b's Ai edit; W2b depends on W1a's ICreditSpendService.
        W2d depends on W1b's store. Run W2a+W2c+W2d in parallel; W2b after W1a.)

── WAVE 3 — Subscriptions + payment seam (after W1; W3b needs GATE-2) ───────
  W3a [BUILD]      P10-05  Plan/Subscription model + upgrade/downgrade/cancel
                           + Shared.Contracts plan/tier seam (feeds P10-02's real tier).
  W3b [BUILD-FAKE] P10-06  IPaymentProvider seam + FakePaymentProvider + checkout-session
                           + idempotent signed-webhook handler + Payment/WebhookEvent
                           + activation + recurring/cancel + SubscriptionActivated/PaymentFailed.
                           ⚠️ MANDATORY security-auditor (PCI/webhook/secrets/idempotency/IAP-flag).
       (W3a before W3b. Real provider adapter = [EXTERNAL], swapped behind the seam later.)

── WAVE 4 — Packs / refunds-dunning / history (after W3) ────────────────────
  W4a [BUILD-FAKE] P10-07  Buy-energy-pack → ApplyPurchase into PurchasedBalance, child-assigned.
                           ⚠️ security-auditor (payment + child-account spend).
  W4b [BUILD-FAKE] P10-09  Dunning (Hangfire + Notifications) + idempotent refund clawback
                           (unspent-only, clamped). ⚠️ MANDATORY security-auditor (money reversal).
  W4c [BUILD]      P10-08  Parent-scoped billing-history read + receipt/invoice generation.
       (W4a/W4b reuse the W3b seam; W4c reads W3b's Payment rows.)

── (out of cluster) FE wave — flag to frontend lead ────────────────────────
  [FE] P10-10 (kid energy UI), P10-05-FE, P10-06-FE, P10-07-FE, P10-08-FE, P10-11-FE.
```

### Per-agent handoffs (cluster-level)
- **db-migration:** the `billing` schema (P10-01 entities: `CreditAccount`, `CreditTransaction` with `UX_CreditTransactions_IdempotencyKey` + `(CreditAccountId, OccurredAtUtc)` index; later `Plan`, `Subscription`, `Payment`, `WebhookEvent`); the `platform`/`settings` schema (`GlobalSettings` table, seed managed keys). One `BillingDbContext`/migration created once in W1a and extended in W3/W4 (serialize). `xmin` concurrency token on `CreditAccount`. No cross-module FK; loose `ChildId`/`ParentUserId` ints.
- **backend-feature:** mirror Ai/Gamification 4-layer shape; `BaseResponse<T>`/`Successed`; `ILoggerManager`; no UoW (explicit transaction for the debit/activation); `ValidationBehavior` on commands only; consume cross-module via `Shared.Contracts` only.
- **api-tester (HTTP endpoints):** P10-01 read seam, P10-03 spend (cost mapping, cache-charged-same, no-charge-on-refuse/error/insufficient, idempotency, cross-child guard), P10-05 plan commands, P10-06 webhook (success activates once, duplicate no-op, bad signature rejected, declined leaves plan unchanged), P10-07 pack, P10-09 clawback/dunning, P10-11 admin authz, P10-12 read/write/invalidate/audit. Use the **FakePaymentProvider** — never real card data.
- **security-auditor (MANDATORY across the cluster — payments, money, child-account spend, secrets, webhooks):** mandatory hard gate on **P10-03** (spend integrity: server-controlled cost, no-charge-without-delivery, no double-charge, cross-child guard), **P10-06** (PCI/no-card-data, webhook signature, idempotency, secrets, amount-server-side, **IAP-compliance flag**), **P10-07** (child-account purchase authz), **P10-09** (idempotent reversal, no over-clawback, refund authz). Light gate on **P10-01** (ledger tamper-resistance / append-only / DB idempotency), **P10-02** (no double-grant / no Purchased clawback / server-only trigger), **P10-04** (monthly ceiling is real), **P10-11/P10-12** (admin-only writes, key allowlist, no payment secrets in settings).
- **reviewer:** gate each wave against the per-story ACs + CONVENTIONS + the relevant security-auditor result.
- **committer:** per-story branch `feat/P10-0x-…`; PR with full description; HANDOFF.md updated in the same PR (record GATE-1/GATE-2 decisions, the `ICreditSpendService` seam, the cost-key scheme, the FakePaymentProvider, the Phase-10 FR family is new-to-SRS).
- **designer / frontend / frontend-e2e-tester:** **not dispatched by this backend lead** — P10-10 and the parent/admin FE go to the frontend lead.

---

## §6 — Open questions for the lead (resolve before/at the gates)

### 🛑 BLOCKING — must resolve before the planner waves
- **OQ-MOD-1 (GATE-1 — new `Billing` module):** approve scaffolding a new `Billing` module (4-layer, mirrors Ai/Curriculum, schema `billing`)? **Recommend yes** — distinct bounded context, no existing home. Do not scaffold until confirmed. *(CLAUDE.md ask-before-new-modules.)*
- **OQ-PAY-1 (GATE-2 — payment provider):** Paymob vs Fawry vs **"generic `IPaymentProvider` abstraction + `FakePaymentProvider` now, real adapter later"**? **Recommend the generic abstraction + fake now** (mirrors the AI gateway/TEI seam approach) so the entire payment backend builds + tests + merges without a merchant account; the provider choice then only gates the real-adapter swap. Stripe is out (does not fully serve EGP). **Do not let the planner pick the provider** — surface it.
- **OQ-AI-1 (energy vs rate-limiter / P3-01-BE-14):** does the energy debit (a) **coexist** with `IAiTutorRateLimiter` (rate limiter = anti-spam abuse guard; energy = economy/quota — *recommended*), or (b) **replace** it? The original P10-03 AC-7 ("remove the daily-count guardrail") is **stale** — there is no `IAiUsageBudget` daily-count budget in code (only the rate limiter). Reinterpret AC-7 accordingly and confirm.
- **OQ-ECON-1 (spend cadence — reconcile P10-03 vs P10-04 vs the rate limiter):** energy is spent **per-call** (P10-03: hint=1, explain-mistake=3, deep=5, practice=5) and *paced* by a **per-day soft cap** (P10-04: Free 10 / Premium 250) **bounded by the monthly pool** (P10-02: Free 100 / Premium 5000). Confirm this three-layer model is the intended economy (per-call debit + soft daily pacing + hard monthly ceiling) and that the per-call costs + intent→cost mapping are correct (Hint→1, WhyWrong→"explain-mistake"=3, Explain→"deep-explanation"=5, SimilarExample→"practice-generation"=5 — the labels don't line up 1:1 with the 4 live intents). This drives every charge.
- **OQ-BLOCK-1 (hard vs soft on zero balance — P10-04 AC-2):** on **insufficient monthly balance**, P10-03 degrades gracefully (cached canned + low-energy copy, never errors) — confirmed. On reaching the **daily soft cap**, the default is **soft** (warn + allow-continue; configurable hard-stop OFF by default). Confirm: AI generation **never HARD-blocks** on zero balance (it degrades to cache) and the daily cap is soft-by-default. *(This is the "never block learning" / FR-AI-6 invariant — confirm it holds.)*

### Important — resolve before the relevant wave
- **OQ-CFG-1 (P10-12 store placement):** the DB-backed `GlobalSetting` store + impl in **`Shared.Kernel`** (recommended — the contract already lives there; `platform`/`settings` schema) vs a new `Platform` project vs inside `Billing` (rejected — would couple Ai/other modules to Billing). Confirm.
- **OQ-CFG-2 (P10-12 interface stability):** keep the existing lean 4-getter `IGlobalSettingsProvider` (string-keyed `GetX(key, default)` — **live Ai handlers already call it this way**) and resolve named economy values via key constants + defaults — *recommended* — vs add named convenience getters (would be a breaking change to live callers). Confirm **no signature change** to the shipped interface.
- **OQ-PAY-2 (recurring/auto-renew):** does the chosen provider support true card-on-file recurring? If not, AC4 falls back to renewal-reminder + re-checkout link (affects whether a `PaymentToken` entity is needed and how P10-09 dunning is modeled). Confirm at GATE-2.
- **OQ-PAY-3 (webhook idempotency store):** dedicated `WebhookEvent` table (unique `ProviderEventId`) — *recommended* — vs idempotency key on `Payment`. Confirm.
- **OQ-PAY-4 (currency unit):** prices/costs are EGP (subscription 199/1990) but pack price is stated as **$5** and AI costs are USD while pricing is EGP. Confirm the stored unit for `credits.pack_price` (EGP vs USD-cents) and the `payment.providerFees` / `fx.usdExchangeRateBuffer` units (% vs flat) **before seeding** the managed keys (P10-12). Today's leaning: 1 credit ≈ 1¢ (hint = 1 credit) per the recalibrated economy note.
- **OQ-FIN-1 (receipts/VAT — P10-08):** the legally-required receipt fields (seller, EGP VAT, tax id, etc.) need finance confirmation before the receipt generator is final.
- **OQ-LEAD-1 (clawback policy — P10-09):** partially-spent pack refund = claw back unspent remainder clamped ≥ 0 (recommended); subscription refund = revoke access per policy, no mid-cycle granted-credit clawback. Confirm.
- **OQ-AI-2 (debit↔delivery coupling):** debit-as-final-delivery-step (idempotency key makes retries safe; a post-debit return failure is acceptable since value was produced) — recommended — vs reserve-then-confirm. Confirm.
- **OQ-AI-3 (insufficient pre-check vs debit-fail race):** if the pre-check passes but a concurrent spend drains the balance, `TryDebitAsync` returns `InsufficientBalance` at delivery — serve the graceful decline (recommended) vs serve-free. Confirm.
- **OQ-LED-1..5 (P10-01 ledger details, carried from the per-story task file):** per-child account (recommended) vs family pool; amount always-positive + Type/Pool (recommended); concurrency = `xmin` + retry (recommended); reconciliation on-demand only (recommended). These are pre-decided in `tasks/Backend/.../P10-01-BE.md` — confirm they still hold.
- **OQ-SRS-1 (traceability):** the entire `FR-CREDIT-*` / `FR-PAY-*` family is referenced by the stories but **absent from the SRS FR list**. Flag to the SRS-keeper to add them — dangling traceability, not a build blocker.

### Assumptions
- Host/module-registration plumbing works for a new `Billing` module exactly as for Ai/Curriculum/Gamification.
- The child id used as the credit-account key = the `StudentId` the rest of the system uses (Gamification/Learning/Ai reference it as a loose int; Ai handlers resolve it from `ICurrentUserService.UserId`).
- Redis (`IConnectionMultiplexer`/`IDistributedCache`) is available (used by Gamification + the Ai rate limiter) for P10-12 caching, with an in-memory fallback.
- Hangfire is live (`StreakSweepJob` etc.) for P10-02 grant + P10-09 dunning.
- `IGlobalSettingsProvider` (live) + the Moderation audit seam (P7) + the Notifications module (parent dunning) are all available to consume via the existing patterns.

### Top risks
- **R1 — building P10-01's ledger before GATE-1/economy OQs settle** risks reworking the whole phase's module + schema boundary. *Mitigation:* hard GATE-1 + OQ-ECON-1 before db-migration.
- **R2 — spend integrity** (charge on refuse/error, double-charge, client-controlled cost, cross-child spend) = economy bypass / unfair charge / abuse. *Mitigation:* server-side cost from `IGlobalSettingsProvider`, child id from session, debit-as-final-step, DB-unique idempotency, **mandatory P10-03 security-auditor gate.**
- **R3 — payment correctness** (double-activation, double-grant, non-idempotent refund clawing twice, card data on our servers, unsigned webhook trusted) = money/PCI incident. *Mitigation:* `IPaymentProvider` + `FakePaymentProvider` + `WebhookEvent` idempotency + signature verify + no-card-data; **mandatory P10-06/07/09 security-auditor gate.**
- **R4 — stale per-story briefs** (they predate the Ai module + `IGlobalSettingsProvider`) misleading implementers into recreating existing seams or "tearing down" a non-existent `IAiUsageBudget`. *Mitigation:* this brief's §0 drift table is authoritative; the planner should hand implementers §0 + this brief, not the raw #124 per-story briefs, where they conflict.

---

*Brief author: analyzer · Date: 2026-06-15 · Consolidated Phase-10 cluster brief validating PR #124 planning against current `main`.*
