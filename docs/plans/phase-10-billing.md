# Execution Plan — Phase 10: Payments, Billing & Credits (Energy)

> **Wave-structured, dependency-ordered execution plan for the whole Phase-10 cluster (12 stories, P10-01..P10-12).**
> Derived from the consolidated brief `docs/briefs/phase-10-billing.md` (which supersedes the per-story #124 briefs/plans where they conflict), the per-story task files `tasks/Backend/Phase-10-Payments-Billing/P10-0x-BE.md`, and verification of the live `Ai` module / `IGlobalSettingsProvider` / `AiResponseCache` code on current `main`.
>
> **ALL lead gates + open questions are RESOLVED** (locked decisions baked in below). This plan does NOT re-raise GATE-1 (Billing module — APPROVED), GATE-2 (payment provider — generic `IPaymentProvider` + `FakePaymentProvider` now, real Paymob later), or the economy OQs. It encodes them.
>
> **Build model:** four reviewer-gated waves, [[phased-build-cadence]] — one wave branch per W (`feat/phase10-wave-N`), per-story branches off it (`feat/P10-0x-…`), `--no-ff` merged back into the wave branch, one wave PR per wave for the lead to merge. Independent stories within a wave run in parallel (Mode A); shared-file edits are serialized (PARALLELISM.md).

---

## Source

| Input | Path |
|---|---|
| Consolidated cluster brief (authoritative) | `docs/briefs/phase-10-billing.md` |
| Per-story briefs (detail; some stale — see brief §0) | `docs/briefs/P10-01.md … P10-12.md` |
| Per-story plans (#124 — superseded for sequencing) | `docs/plans/P10-01.md … P10-12.md` |
| Per-stack task files (work-item source of truth) | `tasks/Backend/Phase-10-Payments-Billing/P10-01-BE.md … P10-12-BE.md` |
| Live settings seam (P10-12 builds on this) | `backend/src/Shared/Learnexia.Shared.Kernel/Settings/IGlobalSettingsProvider.cs`, `BootstrapDefaultGlobalSettingsProvider.cs` |
| Live AI handlers (P10-03 wires the debit into these 4) | `backend/src/Modules/Ai/Learnexia.Modules.Ai.Application/Features/{Hint,Explain,SimilarExample,Simplify}/Commands/*CommandHandler.cs` |
| Live cache + rate limiter | `backend/src/Shared/Learnexia.Shared.Contracts/Ai/IAiResponseCache.cs`; `backend/src/Modules/Ai/.../Services/IAiTutorRateLimiter.cs` |
| Reference scaffold (Billing mirrors this) | `backend/src/Modules/Ai/Learnexia.Modules.Ai.Infrastructure/{DependencyInjection.cs,Persistence/AiDbContext.cs}` |
| Hangfire job precedent (P10-02 grant, P10-06/09 jobs mirror) | `backend/src/Modules/Gamification/.../Jobs/StreakSweepJob.cs` |
| Append-only ledger precedent (`CreditTransaction` mirrors `XpAward`) | `backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Domain/Entities/XpAward.cs` |

### Verified facts that shape this plan (from live code)
- `IGlobalSettingsProvider` is **already live** in `Shared.Kernel.Settings` with a **lean 4-getter signature** — `GetDecimal/GetInt/GetString/GetBool(key, default)` — registered as a Singleton in `Host/Program.cs` **and** in `Ai.Infrastructure/DependencyInjection.cs`. **The 4 Ai handlers already inject and call it** (e.g. `_settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m)`). **P10-12 must NOT change this signature** (it would break live callers).
- `IAiResponseCache` lives in `Shared.Contracts/Ai/` with `Task<string?> GetApprovedAsync(string cacheKey, ct)` (HIT returns text, MISS returns null) + `Task WriteAsync(AiCacheWriteEntry, ct)`.
- `IAiTutorRateLimiter.TryAllow(int studentId)` is a Singleton (Redis or in-proc), fail-soft. **The energy debit COEXISTS with it** — both run.
- All 4 handlers share one pipeline shape: resolve studentId → `TryAllow` rate-limit → fire `HelpRequested` → context + **scope-guard (empty chunks → refuse-and-redirect)** → **cache-first `GetApprovedAsync` (HIT early-return)** → `SafetyLayer.GenerateSafeAsync` → **safety-block → error** → cache-write (fire-and-forget) → fire `HelpDelivered` → return. **Two delivery points: the cache-HIT early-return and the post-safety success.** Both must debit. Refuse/safety-block/error must not.
- `Shared.Contracts` namespace pattern is `Learnexia.Shared.Contracts.<Domain>`; create `Billing/` and (per brief) `Platform/` subfolders. Existing examples to mirror: `IQuestionAnswerContract` (Learning), `ILearningContextProvider` (AiTutor).

---

## Task inventory

Build tags: **[BUILD]** = buildable + mergeable now on this backend; **[BUILD-FAKE]** = buildable + testable now behind `FakePaymentProvider`, live adapter = external; **[EXTERNAL]** = needs live Paymob account/secrets/devops; **[FE]** = frontend, excluded from this backend lead.

### P10-01 — Credit account & append-only ledger — **FOUNDATION** [BUILD]
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-01-BE-1 | [BUILD] | Scaffold `Billing` module (4 projects), `.sln`, `BillingDbContext` (`HasDefaultSchema("billing")`, audit `SaveChangesAsync(int userId)`, ignore `PendingModelChangesWarning`), `BillingDbContextFactory`, `BillingSchema`. Serialized edits: `.sln`, `Program.cs` (`AddBillingModule`/`InitializeAsync`), `Claims.GenerateModules()`. | 5 | — (GATE-1 ✅) |
| P10-01-BE-2 | [BUILD] | `CreditAccount` aggregate: `ChildId`, `GrantedBalance`, `PurchasedBalance`, `GrantExpiresAtUtc`, `xmin` `Version`; mutators `ApplyGrant/Debit/ApplyPurchase/Expire/Refund/Adjust` each returning the matching `CreditTransaction`. `TotalBalance` derived. | 4 | BE-1 |
| P10-01-BE-3 | [BUILD] | `CreditTransaction` append-only entity (mirror `XpAward`): `CreditAccountId`, `Type`, `Pool`, `Amount` (positive), `ReasonCode`+`Reason`, resulting balances, `OccurredAtUtc`, `IdempotencyKey` (unique), `RelatedActionId`, `RelatedPaymentId`. No mutators. | 3 | BE-2 |
| P10-01-BE-4 | [BUILD] | EF configs + migration `InitialBilling`: `ChildId` unique, `xmin` rowversion, enum `HasConversion<int>()`, **`UX_CreditTransactions_IdempotencyKey`**, composite `(CreditAccountId, OccurredAtUtc)` index, `Type` index, `timestamptz`. | 4 | BE-3 |
| P10-01-BE-5 | [BUILD] | `DebitAsync` atomic primitive (`SpendCreditCommand`): explicit txn → load w/ concurrency token → idempotency pre-check → `TotalBalance < amount` → typed `InsufficientBalance` → Granted-first split → `account.Debit(...)` → insert txn → commit; retry on `DbUpdateConcurrencyException`; unique-violation → idempotent success. Returns `BaseResponse<DebitResult>`. | 5 | BE-4 |
| P10-01-BE-6 | [BUILD] | Remaining ledger commands (`GrantCredit/ApplyPurchase/ExpireGrant/Refund/Adjust`) — same txn + idempotency pattern, differ by pool + Type. | 5 | BE-5 |
| P10-01-BE-7 | [BUILD] | `GetCreditAccountQuery` (read seam) + `ReconcileAccountQuery` (admin drift recompute, no auto-heal). Queries — no `ValidationBehavior`. | 3 | BE-4 |
| P10-01-BE-8 | [BUILD] | **`ICreditSpendService` in `Shared.Contracts/Billing/`** — `Task<DebitResult> TryDebitAsync(int childId, int amount, string reasonCode, string idempotencyKey, ct)` + `Task<EnergyBalance> GetBalanceAsync(int childId, ct)`; impl `CreditSpendService` in `Billing.Infrastructure`. **The W2 AI seam.** | 3 | BE-6 |
| P10-01-BE-9 | [BUILD] | DI (`AddBillingApplication` + `AddBillingInfrastructure` incl. `ICreditSpendService` impl); optional dev `CreditAccountSeeder` (no seeded balances). | 2 | BE-8 |

### P10-12 — Global Settings DB store (DELTA on the live seam) [BUILD]
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-12-BE-1 | [BUILD] | `GlobalSetting` entity + EF config + migration `AddGlobalSettings` (schema **`platform`**, table `GlobalSettings`): `Key` PK, `Value`, `Type` enum, `UpdatedBy`, `UpdatedAt`; `Update(...)` mutator. **Seed the 17 managed keys** with bootstrap defaults. | 4 | P10-01-BE-4 (shared infra/schema patterns exist) |
| P10-12-BE-2 | [BUILD] | `GlobalSettingKeys` constants + `ManagedKeys` allowlist set (string-keyed; same keys the live Ai handlers already use). | 1 | BE-1 |
| P10-12-BE-3 | [BUILD] ⚠️**DELTA-CORRECTION** | DB-backed `GlobalSettingsProvider` (Redis `IDistributedCache` primary + `IMemoryCache` fallback + startup warm-up `IHostedService` + explicit invalidation). **Implements the EXISTING lean 4-getter `IGlobalSettingsProvider` in `Shared.Kernel.Settings` UNCHANGED.** Registered to **replace** `BootstrapDefaultGlobalSettingsProvider` at DI (both registration sites). See "Drift correction" note below. | 5 | BE-2 |
| P10-12-BE-4 | [BUILD] | `IAuditLogWriter` Moderation seam consume (declare in `Shared.Contracts/Moderation/` if absent) + cache invalidation on write (folded into BE-5's command). | (in BE-5) | BE-3 |
| P10-12-BE-5 | [BUILD] | `UpdateGlobalSettingCommand` (admin write): allowlist check → type-parse → per-key range → load row → capture old → `Update` → save → **audit via `IAuditLogWriter`** → **invalidate cache**. Explicit txn (update+audit atomic). `[Authorize(AdminPolicy)]`. | 4 | BE-3 |
| P10-12-BE-6 | [BUILD] | `UpdateGlobalSettingValidator` (FluentValidation, `ValidationBehavior`): allowlist, type-parse, per-key ranges, cross-key `free_daily_cap ≤ free_monthly` / `premium_daily_cap ≤ premium_monthly` (reads the related key direct-from-DB, not cache), confidence ∈ [0,1]. | 3 | BE-5 |
| P10-12-BE-7 | [BUILD] | `GetGlobalSettingsQuery` (admin read) — all 17 keys w/ value/type/UpdatedBy/UpdatedAt. `[Authorize(AdminPolicy)]`. | 2 | BE-1 |
| P10-12-BE-8 | [BUILD] | `GlobalSettingsController` (`GET /api/Admin/GlobalSettings`, `PUT /api/Admin/GlobalSettings/{key}`) `[Authorize(AdminPolicy)]` + DI of the DB-backed impl + `IAuditLogWriter` + warm-up hosted service. **Placement: `Shared.Kernel` + controller hosted in `Billing.Api` (or a thin Platform Api).** | 2 | BE-5, BE-7 |
| P10-12-BE-9 | [BUILD] | P10-11 reconciliation cleanup — **no-op in practice** (the superseded `BillingConfigVersion`/`IBillingConfigReader` are never built). Verify P10-02/03/04/07 reference `IGlobalSettingsProvider`; no drop-migration needed (BE-1..BE-5 of P10-11 never dispatched). | 1 | BE-8 |

### P10-02 — Monthly energy grant (Hangfire + expiry) [BUILD]
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-02-BE-1 | [BUILD] | Inject `IGlobalSettingsProvider` into `BillingGrantJob`; resolve amounts via **string keys** `credits.free_monthly`/`credits.premium_monthly` (NOT named getters — see drift note); keep only cron+timezone in appsettings. **No `BillingGrantOptions`.** | 1 | P10-01-BE-9, P10-12-BE-3 |
| P10-02-BE-2 | [BUILD] | `IBillingSubscriptionContract.GetActiveChildrenWithTierAsync()` in `Shared.Contracts/Billing/` → `IReadOnlyList<ActiveChildPlanDto>`; stub `ConfigDefaultSubscriptionContract` (all children Free) until P10-05 lands. | 2 | P10-01-BE-9 |
| P10-02-BE-3 | [BUILD] | `BillingGrantJob` (mirror `StreakSweepJob`): `[DisableConcurrentExecution]`, fresh scope, `ISystemClock`, `cycleId=yyyyMM`, paged 500; per child `Expire` prior (`expire:{childId}:{priorCycle}`) then `ApplyGrant` (`grant:{childId}:{cycle}`) in one txn; unique-violation → skip; per-child fail-soft. | 5 | BE-1, BE-2, P10-01-BE-6, P10-12-BE-3 |
| P10-02-BE-4 | [BUILD] | Register `BillingGrantJob` Transient + wire recurring cron (`"0 1 1 * *"`) in `BillingModule.InitializeAsync`. | 2 | BE-3 |

### P10-03 — Spend energy on AI help (charge-on-delivery) [BUILD] ⚠️ MANDATORY security-auditor
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-03-BE-1 | [BUILD] | `CreditCostResolver` (pure, server-side) mapping intent → cost via `IGlobalSettingsProvider` **string keys**: `ai_cost.hint`=1, `ai_cost.explain_mistake`=2, `ai_cost.deep_explanation`=3, `ai_cost.practice_generation`=5. **Client never supplies cost.** Lives in `Ai.Application` (no `Billing.*` reference). | 2 | P10-01-BE-8/9, P10-12-BE-3 |
| P10-03-BE-2 | [BUILD] | **Pre-authorize branch** at handler entry (after rate-limit): `ICreditSpendService.GetBalanceAsync(childId)`; if `Total < cost` → graceful low-energy decline (reuse refuse/redirect shape + cached canned if available) — **no gateway call, no debit, no error.** Wired into all 4 handlers. | 3 | P10-01-BE-8, (4 Ai handlers live) |
| P10-03-BE-3 | [BUILD] | **Debit-on-delivery** at BOTH delivery points — the cache-HIT early-return AND the post-safety success — `idempotencyKey` = server-generated per-request action id; `TryDebitAsync(childId, cost, reasonCode, key)`; `InsufficientBalance` race → graceful decline, no debit. Debit is the final step of a delivered response. | 4 | BE-2, P10-01-BE-5 |
| P10-03-BE-4 | [BUILD] | **AC-7 reinterpreted (drift D3):** there is **no `IAiUsageBudget`** to remove. Document that the energy debit is the new economy gate while `IAiTutorRateLimiter` stays as the abuse guard (they coexist). No teardown code. | 1 | BE-3 |
| P10-03-BE-5 | [BUILD] | No-charge-on-no-delivery tests: refuse-and-redirect, provider/processing error, safety-block + no-reveal → assert **zero** `TryDebitAsync` calls / no `Spend` rows. Also cache-HIT-charged-same + cross-child guard. | 3 | BE-3 |

### P10-04 — Daily soft cap & low-energy warning [BUILD]
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-04-BE-1 | [BUILD] | Migration `AddDailyCapTracking`: `DailyUsed` (int) + `DailyUsedDateLocal` (date?) on `billing.CreditAccounts`. **Fold into `InitialBilling`** if dispatched in W1 with P10-01; else additive. | 2 | P10-01-BE-4 |
| P10-04-BE-2 | [BUILD] | `DailyCapHelper` (pure, mirror `StreakDayCalculator`): child local "today" from `childTimeZoneId` + `ISystemClock`. Unit-tested. | 2 | BE-1 |
| P10-04-BE-3 | [BUILD] | Lazy daily reset on spend/read (stale `DailyUsedDateLocal` → zero `DailyUsed`), inside the atomic debit txn and the read query. Monthly pool untouched. | 2 | BE-2 |
| P10-04-BE-4 | [BUILD] | **`EnergyStatusQuery`** (child-scoped read seam — the only backend obligation to FE P10-10): `{MonthlyBalance, DailyUsed, DailyCap, DailyCapReached, LowEnergy, WarningState, GrantExpiresAtUtc}`; caps/thresholds from `IGlobalSettingsProvider` string keys. `[Authorize]` child-from-JWT. | 3 | BE-3, P10-12-BE-3 |
| P10-04-BE-5 | [BUILD] | `IncrementDailyUsage(childId, amount)` seam called inside P10-03's debit txn; `HardStopEnabled` feature flag in appsettings (default OFF) — soft by default. `IsHardStopBlocking(...)` utility. | 2 | BE-4 |

### P10-05 — Manage subscription plan (Free/Premium, Monthly/Annual) [BUILD] + [FE excluded]
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-05-BE-1 | [BUILD] | (Module already scaffolded in W1 P10-01-BE-1.) Verify Billing module + `BillingDbContext` present; no re-scaffold. | (verify) | P10-01-BE-1 |
| P10-05-BE-2 | [BUILD] | `Plan` + `Subscription` entities (`BillingPeriod {Monthly,Annual}`, `PendingPlanCode`, `PendingBillingPeriod`, status enum, cycle dates). Prices NOT on entity — resolved from P10-12 keys. | 3 | BE-1 |
| P10-05-BE-3 | [BUILD] | EF configs + migration `BillingPlansSubscriptions`; filtered unique index `ParentUserId WHERE Status=Active`; idempotent seed of Free/Premium plans. | 4 | BE-2 |
| P10-05-BE-4 | [BUILD] | Queries `GetCurrentPlanQuery`, `GetPlanComparisonQuery` (reads `subscription.monthly_price_egp`=199 / `annual_price_egp`=1990). | 4 | BE-3 |
| P10-05-BE-5 | [BUILD] | Commands `RequestUpgrade(parent, billingPeriod)` → `PendingPayment`; `RequestDowngrade` → `PendingPlanCode=Free`; `CancelSubscription`. Owning-parent authz; explicit txn; FluentValidation. | 5 | BE-4 |
| P10-05-BE-6 | [BUILD] | `SubscriptionController` (`GET Current`, `GET Plans/Comparison`, `POST Upgrade/Downgrade/Cancel`) `[Authorize]` parent-scoped. | 2 | BE-5 |
| P10-05-BE-7 | [BUILD] | **Replace** the P10-02 stub: real `IBillingSubscriptionContract`/`IPlanQuery` impl in `Billing.Infrastructure` resolving active children + tier (the seam P10-02 grant consumes). | 2 | BE-5 |

### P10-06 — Pay for a subscription (payment provider) [BUILD-FAKE] + [EXTERNAL] + [FE excluded] ⚠️ MANDATORY security-auditor
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-06-BE-1 | [BUILD-FAKE] | `Payment` + `WebhookEvent` entities (`Payment`: SubscriptionId, ParentUserId, ProviderPaymentRef, Amount, `Currency=EGP`, Status, `Kind {Subscription,Pack}`, `TargetChildId?`, IdempotencyKey; `WebhookEvent`: `ProviderEventId` unique, EventType, Payload, ProcessedAt, Status). **No card columns.** | 3 | P10-05-BE-1 |
| P10-06-BE-2 | [BUILD-FAKE] | EF configs + migration `AddPaymentAndWebhookTables` (serialized after `BillingPlansSubscriptions`); unique `WebhookEvent.ProviderEventId`, unique `Payment.IdempotencyKey`, indexes. | 4 | BE-1, P10-05-BE-3 |
| P10-06-BE-3 | [BUILD-FAKE] / impl [EXTERNAL] | **`IPaymentProvider` seam** (`CreateCheckoutSession/VerifyWebhookSignature/ParseWebhookEvent/CancelRecurring`) + **`FakePaymentProvider`** (always-succeed / configurable-decline / replayable synthetic-signed webhook). **One interface, one impl now** — no Strategy/Factory (rule 8). Real `PaymobPaymentProvider` = [EXTERNAL], swapped behind the seam later. Secrets from secret store only. | 6 | GATE-2 ✅, BE-1 |
| P10-06-BE-4 | [BUILD-FAKE] | `StartSubscriptionCheckoutCommand` — owning-parent check; **amount resolved SERVER-SIDE** from `BillingPeriod` + P10-12 keys; create `Payment{Kind=Subscription,Status=Initiated}`; `CreateCheckoutSession`; return redirect URL. **Web hosted checkout — NO native IAP.** | 3 | BE-3, P10-05-BE-5 |
| P10-06-BE-5 | [BUILD-FAKE] | `HandleProviderWebhookCommand` — (a) HMAC signature verify; (b) dedupe on `ProviderEventId`; (c) `payment.succeeded` → atomic txn: `Payment→Succeeded`, `Subscription→Active` w/ cycle dates per `BillingPeriod`, emit `SubscriptionActivatedIntegrationEvent` (→ P10-02 grant); (d) `payment.failed` → `Payment→Failed`, emit `PaymentFailedIntegrationEvent` (→ P10-09). Idempotency is the core invariant. | 6 | BE-3, BE-2 |
| P10-06-BE-6 | [BUILD-FAKE] | `CancelSubscriptionCommand` — stop renewal, access to cycle end; provider cancel-recurring if supported; owning-parent authz. (Coordinate w/ P10-05-BE-5 shell.) | 2 | BE-3, P10-05-BE-5 |
| P10-06-BE-7 | [BUILD-FAKE] | Webhook endpoint `POST /api/Billing/Webhooks/Provider` — **JWT-free, signature-authenticated**; reads raw body before middleware transforms. | 2 | BE-5 |
| P10-06-BE-8 | [BUILD-FAKE] / live [EXTERNAL] | Hangfire `ReconcilePaymentsJob` — re-queries provider for non-terminal `Payment` rows; idempotent. (Against the fake now; real status-API call when live.) | 4 | BE-3, BE-2 |

### P10-07 — Buy an energy pack (never expires) [BUILD-FAKE] + [EXTERNAL] + [FE excluded] ⚠️ MANDATORY security-auditor
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-07-BE-1 | [BUILD-FAKE] | Verify `Payment.Kind=Pack` + `Payment.TargetChildId` exist from P10-06-BE-1/2; add migration `AddPaymentPackFields` only if missing. | 1 | P10-06-BE-2 |
| P10-07-BE-2 | [BUILD-FAKE] | `StartPackCheckoutCommand(parent, childId)` — **parent-owns-child family-scope check**; pack size/price from P10-12 keys `credits.pack_size`/`credits.pack_price_egp`; `Payment{Kind=Pack,TargetChildId}`; checkout; parent-JWT only (child JWT rejected). | 3 | BE-1, P10-06-BE-3, P10-12-BE-3 |
| P10-07-BE-3 | [BUILD-FAKE] | Extend `HandleProviderWebhookCommand` with `Kind=Pack` branch: on success, explicit txn append `Purchase` `CreditTransaction` → `PurchasedBalance += packSize`; idempotent on `ProviderEventId` AND `CreditTransaction.IdempotencyKey`. | 4 | BE-1, P10-06-BE-5, P10-01-BE-6 |
| P10-07-BE-4 | [BUILD-FAKE] | `POST /api/Billing/Packs/Checkout` `[Authorize]` parent-JWT. | 1 | BE-2 |

### P10-09 — Failed payments & refunds (dunning + clawback) [BUILD-FAKE] + [EXTERNAL] ⚠️ MANDATORY security-auditor
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-09-BE-1 | [BUILD-FAKE] | Extend `Subscription` (FailedAttemptCount, NextRetryAt, GraceEndsAt, status `PastDue`/`Dunning`) + `Payment` refund linkage (`RefundedPaymentId?`/`Status=Refunded`); migration `AddDunningAndRefundFields`. | 4 | P10-06-BE-2 |
| P10-09-BE-2 | [BUILD-FAKE] | Webhook `charge.failed` branch: increment failed count, `Payment→Failed`, schedule retry, emit `DunningNotificationEvent` → Notifications via `Shared.Contracts`; after N failures → `Downgrading` + `GraceEndsAt=CycleEnd`. Idempotent per (subscription, providerEventId). | 5 | BE-1, P10-06-BE-5 |
| P10-09-BE-3 | [BUILD-FAKE] | Webhook `refund.succeeded` branch: **Pack** → claw back **unspent only**, clamped ≥ 0, append `Refund` `CreditTransaction` in explicit txn; **Subscription** → revoke access per policy, no granted-credit clawback. Idempotent (event id + idempotency key). Concurrency guard against negative-balance race. | 6 | BE-1, P10-06-BE-5, P10-07-BE-3 |
| P10-09-BE-4 | [BUILD-FAKE] / live [EXTERNAL] | Hangfire `DunningRetryJob` — process `PastDue`/`Dunning` whose `NextRetryAt` passed; re-charge via `IPaymentProvider` (or re-checkout link if no card-on-file); idempotent per subscription+cycle. | 3 | BE-2, P10-06-BE-3 |
| P10-09-BE-5 | [BUILD-FAKE] (conditional) | `InitiateRefundCommand` admin-only + audited (`[Authorize(Policy="Billing.Admin")]`); calls `IPaymentProvider.Refund`; state change driven by the subsequent webhook. **Locked: provider-dashboard + admin action both supported → BUILD it.** | 3 | BE-3, P10-06-BE-3 |

### P10-08 — Billing history & receipts [BUILD] + [FE excluded]
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-08-BE-1 | [BUILD] (conditional) | Capture `NetAmount`/`VatAmount`/`GrossAmount` at charge time (prefer in P10-06). Sequential `InvoiceNumber`/`Invoice` table only if finance requires — **flag OQ-FIN-1, no-op if VAT fields already on `Payment`.** | 3 | P10-06-BE-2 |
| P10-08-BE-2 | [BUILD] | `GetBillingHistoryQuery(parent, paging)` — chronological `Payment` + refund entries, strictly `parentUserId`-scoped, paginated. | 3 | P10-06-BE-2, P10-07-BE-1, P10-09 (refund rows) |
| P10-08-BE-3 | [BUILD] | `GetReceiptQuery(parent, paymentId)` — receipt DTO with legal fields (seller from config, VAT breakdown). **Re-check ownership in handler (anti-IDOR).** **v1: printable HTML** (no PDF lib — rule 8). | 3 | BE-1, BE-2 |
| P10-08-BE-4 | [BUILD] | `BillingHistoryController` (`GET /api/Billing/History`, `GET /api/Billing/History/Receipt/{paymentId}`) `[Authorize]` parent-scoped. | 1 | BE-3 |

### P10-11 — Admin: configure plans/grants/costs [BUILD] (folds into P10-12) ⚠️ security-auditor (admin authz)
| ID | Build tag | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P10-11-BE-1..5 | — | **SUPERSEDED by P10-12 — DO NOT DISPATCH.** No `BillingConfigVersion`/`IBillingConfigReader`/`UpdateBillingConfigCommand`/`GetBillingConfigQuery`. | 0 | — |
| P10-11-BE-6 | [BUILD] | **RETAINED** — thin admin billing-config controller delegating to `GetGlobalSettingsQuery` (filtered to economy keys) + `UpdateGlobalSettingCommand`. `[Authorize(AdminPolicy)]`. **Or** point admin UI directly at `GlobalSettingsController` (P10-12-BE-8) — confirm whether the wrapper is wanted; default to reusing P10-12's controller (skip BE-6) unless the lead wants a billing-scoped route. | 2 | P10-12-BE-8 |

### Out of scope — frontend lead [FE — EXCLUDED]
| Story | Note |
|---|---|
| **P10-10** kid energy UI (entire story) | Backend obligation = `EnergyStatusQuery` (delivered in P10-04-BE-4). |
| P10-05-FE / P10-06-FE / P10-07-FE / P10-08-FE / P10-11-FE | Parent "Plan & billing" tab, checkout/return, pack-purchase, history/receipts, admin billing screen — all FE. Flag to frontend lead. |

---

## Dependency order

### Hard edges (the spine)
```
P10-01 (ledger + ICreditSpendService)  ──►  P10-02, P10-03, P10-04, P10-05, P10-07, P10-08, P10-09  (everything)
P10-12 (settings DB store)             ──►  P10-02 cost/grant, P10-03 cost, P10-04 caps, P10-07 price, P10-11 admin write
        (NOTE: the IGlobalSettingsProvider seam is ALREADY LIVE — bootstrap defaults cover the gap,
         so consumers can compile/build before P10-12-BE-3 lands; the DB-backed store just replaces the impl.)
P10-05 (plan/tier)  ──►  P10-02 (real tier, replacing the stub),  P10-06 (activation target)
P10-06 (IPaymentProvider + webhook)  ──►  P10-07 (reuse seam),  P10-09 (reuse webhook),  P10-08 (reads Payment rows)
P10-03  ──►  P10-04 decline copy / increment hook ;  P10-03 + P10-04  ──►  P10-10 [FE]
```

### Module-isolation seams (rule 1 — no cross-module project refs)
- **Ai ↔ Billing:** `Shared.Contracts/Billing/ICreditSpendService` (declared P10-01-BE-8, implemented in `Billing.Infrastructure`, consumed by the 4 Ai handlers). Ai also reads cost via the already-injected `IGlobalSettingsProvider` (in `Shared.Kernel`).
- **Billing ↔ Identity/Subscription tier:** `Shared.Contracts/Billing/IBillingSubscriptionContract` (stub in P10-02, real impl in P10-05).
- **Billing → Notifications:** `Shared.Contracts/Billing/DunningNotificationEvent` (P10-09).
- **Billing → Moderation:** `Shared.Contracts/Moderation/IAuditLogWriter` (P10-12 admin audit).
- **Billing payment events:** `Shared.Contracts/Billing/{SubscriptionActivated,PaymentFailed,PackPurchased}IntegrationEvent`.

### Shared-file serialization points (PARALLELISM.md — serialize across all in-flight work)
- `Learnexia.Modular.sln`, `Host/Program.cs` (`AddBillingModule` + `InitializeAsync` migrate hook), `Claims.GenerateModules()` — **all touched once in W1 P10-01-BE-1; freeze other edits while it's in flight.**
- `Directory.Packages.props` — only if a new package is needed (e.g. payment SDK in the real adapter — not in [BUILD-FAKE]).
- **`BillingDbContext` + the `billing` migration chain** — serialize across stories that add tables: `InitialBilling` (W1) → `BillingPlansSubscriptions` (W3 P10-05) → `AddPaymentAndWebhookTables` (W3 P10-06) → `AddDunningAndRefundFields` (W4 P10-09) → conditional `AddPaymentPackFields`/`AddBillingReceiptFields` (W4). Never run two `BillingDbContext` migrations concurrently.
- **The 4 Ai handlers** (`{Hint,Explain,SimilarExample,Simplify}CommandHandler.cs`) — all four edited in W2 P10-03; serialize against any concurrent Ai work.
- **`Shared.Contracts` seam files** — declared early (P10-01-BE-8 in W1; the subscription/notification/audit/event seams as their stories land); serialize concurrent edits to the same file.

---

## Execution batches (waves)

> Each wave = a reviewer-gated batch on a wave branch `feat/phase10-wave-N`; per-story branches off it; `--no-ff` back; one wave PR per wave for the lead to merge. Within a wave, run the parallel set concurrently (Mode A); honor serialization points above.

### 🟢 WAVE 1 — Ledger foundation + settings store (parallel pair)
Branch: `feat/phase10-wave-1`. The two stories are independent (`billing` schema vs `platform` schema, no shared entity) — **serialize only the `Program.cs`/`.sln`/`Claims` edits** (do P10-01-BE-1's shared edits first, then P10-12 wiring).

- **Batch W1-A (sequential within story):** `db-migration` → P10-01-BE-1 (scaffold + `BillingDbContext`), P10-01-BE-4 (`InitialBilling` migration, **fold in P10-04-BE-1 daily-cap columns** so W2 needs no extra migration). Then `backend-feature` → P10-01-BE-2/3/5/6/7/8/9 (entities, `DebitAsync`, ledger commands, read+reconcile queries, `ICreditSpendService` seam, DI).
- **Batch W1-B (parallel with W1-A):** `db-migration` → P10-12-BE-1 (`platform.GlobalSettings` + `AddGlobalSettings` + seed 17 keys). Then `backend-feature` → P10-12-BE-2/3/4/5/6/7/8 (constants, DB-backed provider replacing bootstrap, audit-seam, update command+validator, read query, controller). P10-12-BE-9 = verify-only no-op.
- **api-tester (after both):** P10-01 read seam (`GetCreditAccountQuery`, reconcile), P10-12 `GET/PUT /api/Admin/GlobalSettings` (admin authz 403, allowlist reject, type-parse, cross-key validation, invalidate-then-read consistency).
- **security-auditor (light gate):** P10-01 ledger tamper-resistance / append-only / DB idempotency; P10-12 admin-only writes + key allowlist + **no payment secrets in settings keys**.
- **reviewer gate W1** → **committer** (per-story branches `feat/P10-01-…`, `feat/P10-12-…` → `--no-ff` into wave-1 → wave-1 PR).

### 🟡 WAVE 2 — Grant / spend / cap + admin write (after W1 merged)
Branch: `feat/phase10-wave-2`. P10-02, P10-04, P10-11 are independent of P10-03's Ai edit; P10-03 depends on W1's `ICreditSpendService`. **P10-04-BE-1 migration already folded into W1** → P10-04 here is code-only.

- **Batch W2-A (parallel):** `backend-feature` → **P10-02** (BE-1 inject settings, BE-2 subscription-tier stub, BE-3 `BillingGrantJob` mirror `StreakSweepJob`, BE-4 register cron).
- **Batch W2-B (parallel):** `backend-feature` → **P10-04** (BE-2 `DailyCapHelper`, BE-3 lazy reset, BE-4 `EnergyStatusQuery`, BE-5 `IncrementDailyUsage` + hard-stop flag).
- **Batch W2-C (parallel):** `backend-feature` → **P10-11-BE-6** (thin admin controller delegating to P10-12 — or skip if reusing `GlobalSettingsController`; confirm).
- **Batch W2-D (after W1, serialize the 4 Ai handler edits):** `backend-feature` → **P10-03** (BE-1 `CreditCostResolver`, BE-2 pre-authorize branch, BE-3 debit-on-delivery at BOTH delivery points, BE-4 AC-7 doc-only, BE-5 no-charge tests). **Coordinate P10-04-BE-5 `IncrementDailyUsage`** to run inside P10-03-BE-3's debit txn.
- **api-tester (after):** P10-03 spend (cost mapping server-side, cache-charged-same, no-charge-on-refuse/error/insufficient, idempotency, cross-child guard); P10-04 `EnergyStatusQuery` (caps, warning states, timezone reset); P10-02 grant idempotency (re-run no-op); P10-11 admin authz.
- **security-auditor:** ⚠️ **MANDATORY on P10-03** (server-controlled cost, no-charge-without-delivery, no double-charge, child-id-from-session, cross-child guard). ⚠️ **on P10-11** (admin authz). Light on P10-02 (no double-grant / no Purchased clawback / server-only trigger), P10-04 (monthly ceiling real, hard-stop honored).
- **reviewer gate W2** (consumes security-auditor result; Critical/High on P10-03 block) → **committer** → wave-2 PR.

### 🟠 WAVE 3 — Subscriptions + payment seam (after W1; GATE-2 ✅ resolved)
Branch: `feat/phase10-wave-3`. P10-05 before P10-06 (P10-06 activates a `PendingPayment` subscription). **Serialize the `BillingDbContext` migrations:** `BillingPlansSubscriptions` (P10-05) before `AddPaymentAndWebhookTables` (P10-06).

- **Batch W3-A (sequential):** `db-migration` → P10-05-BE-3 migration; `backend-feature` → P10-05-BE-2/4/5/6 (entities, queries, commands, controller) + **P10-05-BE-7 (real `IBillingSubscriptionContract` impl → replaces P10-02's stub).**
- **Batch W3-B (after W3-A):** `db-migration` → P10-06-BE-2 migration (after `BillingPlansSubscriptions`); `backend-feature` → P10-06-BE-1/3/4/5/6/7/8 — **`IPaymentProvider` seam + `FakePaymentProvider`**, checkout, idempotent signed webhook handler, activation, cancel, JWT-free webhook endpoint, reconcile job. Real adapter NOT built (external).
- **api-tester (after):** P10-05 plan commands (upgrade→PendingPayment, downgrade, cancel, owning-parent authz); P10-06 webhook via **FakePaymentProvider + synthetic signed webhook** (success activates once, duplicate event no-op, bad signature rejected, declined leaves plan unchanged). **Never real card data.**
- **security-auditor:** ⚠️ **MANDATORY on P10-06** (no card data on our servers, webhook signature verify, idempotency, secrets in secret store, amount server-side, **IAP-compliance flag** for native launch). Light on P10-05 (owning-parent authz, no privilege escalation).
- **reviewer gate W3** (Critical/High on P10-06 block) → **committer** → wave-3 PR.

### 🔵 WAVE 4 — Packs / refunds-dunning / history (after W3)
Branch: `feat/phase10-wave-4`. All reuse the W3 payment seam. **Serialize `AddDunningAndRefundFields` (P10-09) and any conditional pack/receipt migration** on `BillingDbContext`.

- **Batch W4-A (parallel):** `backend-feature` → **P10-07** (BE-1 verify Pack fields, BE-2 `StartPackCheckoutCommand` family-scope, BE-3 webhook Pack branch → `PurchasedBalance`, BE-4 endpoint).
- **Batch W4-B (parallel; migration first):** `db-migration` → P10-09-BE-1 (`AddDunningAndRefundFields`); `backend-feature` → **P10-09** (BE-2 dunning + Notifications event, BE-3 idempotent refund clawback unspent-only, BE-4 `DunningRetryJob`, BE-5 admin `InitiateRefundCommand`).
- **Batch W4-C (after W4-A/B Payment+refund rows exist):** `db-migration` (conditional P10-08-BE-1 VAT/invoice fields, prefer captured in P10-06); `backend-feature` → **P10-08** (BE-2 history query parent-scoped, BE-3 receipt query anti-IDOR + printable HTML, BE-4 controller).
- **api-tester (after):** P10-07 pack (parent-only, family-scope, credit lands once, idempotent); P10-09 clawback (unspent-only, clamped ≥ 0, idempotent refund, dunning state transitions) + admin refund authz; P10-08 history (parent-scoped, no cross-account IDOR, receipt fields).
- **security-auditor:** ⚠️ **MANDATORY on P10-07** (child-account purchase authz — parent owns child, child JWT rejected) and ⚠️ **on P10-09** (idempotent money reversal, no over-clawback, refund authz, no negative balance). Light on P10-08 (IDOR on receipts).
- **reviewer gate W4** (Critical/High on P10-07/P10-09 block) → **committer** → wave-4 PR.

### (out of cluster) FE wave — flag to frontend lead
`[FE]` P10-10 (kid energy UI), P10-05-FE, P10-06-FE, P10-07-FE, P10-08-FE, P10-11-FE. The only backend obligation (`EnergyStatusQuery`) ships in W2 (P10-04-BE-4). **Not dispatched by this backend lead.**

---

## The energy-spend integration design (Wave 2)

This is the highest-value, highest-risk integration and where the deferred **P3-01-BE-14 (`IAiUsageBudget`)** lands — fulfilled by the energy ledger (monthly grant P10-02 + daily soft cap P10-04 + per-call debit P10-03), NOT by a new budget class.

### The `Shared.Contracts` seam (Ai consumes; Billing implements — no cross-module FK)
Declared in P10-01-BE-8, namespace `Learnexia.Shared.Contracts.Billing`:
```csharp
public interface ICreditSpendService
{
    Task<DebitResult>   TryDebitAsync(int childId, int amount, string reasonCode, string idempotencyKey, CancellationToken ct);
    Task<EnergyBalance> GetBalanceAsync(int childId, CancellationToken ct);
}
// DebitResult { bool Charged; int FromGranted; int FromPurchased; int ResultingTotal; DebitOutcome Outcome /* Charged | InsufficientBalance | DuplicateIdempotent */ }
// EnergyBalance { int GrantedBalance; int PurchasedBalance; int TotalBalance; DateTime? GrantExpiresAtUtc }
```
The 4 Ai handlers inject `ICreditSpendService` (alongside their existing `IAiTutorRateLimiter` + `IGlobalSettingsProvider`). **No `Ai.*` project references any `Billing.*` project** (rule 1) — mirrors how Ai already consumes `IQuestionAnswerContract`/`ILearningContextProvider`/`IGlobalSettingsProvider`.

### Cost resolution (server-side, client-blind)
`CreditCostResolver` (P10-03-BE-1) in `Ai.Application` maps the intent → cost via the already-injected `IGlobalSettingsProvider` using **string keys** (the live handlers already call it `_settings.GetX(key, default)`):
| Intent (handler) | Key | Default |
|---|---|---|
| Hint (`GetHintCommandHandler`, Hint mode) | `ai_cost.hint` | 1 |
| WhyWrong (`GetHintCommandHandler`, WhyWrong mode) | `ai_cost.explain_mistake` | 2 |
| Explain (`ExplainConceptCommandHandler` / `SimplifyExplanationCommandHandler`) | `ai_cost.deep_explanation` | 3 |
| Practice (`SimilarExampleCommandHandler`) | `ai_cost.practice_generation` | 5 |
**The client never sends a cost or an intent kind** (security-critical). `childId` always comes from `ICurrentUserService.UserId` (JWT), never the request body.

### Debit placement in the live handler pipeline (charge-on-delivery)
Mapped to the verified pipeline (identical across all 4 handlers):
1. studentId from JWT → `IAiTutorRateLimiter.TryAllow(studentId)` (**unchanged — coexists**).
2. **Pre-authorize (P10-03-BE-2):** `GetBalanceAsync(childId)`; if `Total < cost` (monthly-hard-exhausted) → **graceful low-energy decline** (reuse refuse/redirect shape + cached canned if available). **No gateway call, no debit, no error** (never-block-learning / FR-AI-6). If `HardStopEnabled` (default OFF) AND daily-cap reached → also decline here.
3. fire `HelpRequested`; resolve context.
4. **Scope guard:** empty chunks → **refuse-and-redirect** → return. **No debit reached.**
5. **Cache-first `GetApprovedAsync`:** **HIT → DEBIT then early-return** (this is delivery point #1 — **cache-hit charges same**, brief LOCKED #3).
6. Cache MISS → `SafetyLayer.GenerateSafeAsync`; **safety-block / no-reveal violation → error → return. No debit reached.**
7. **Success → DEBIT then return** (delivery point #2). Cache-write is fire-and-forget after.

The debit at points #5 and #7: build `idempotencyKey` = server-generated per-request action id (stored in request context so an HTTP retry reuses the same key), call `TryDebitAsync(childId, cost, reasonCode, key)`. **`InsufficientBalance` at delivery (rare race where pre-check passed but a concurrent spend drained it)** → graceful decline, **no debit written** (OQ-AI-3 resolved). The debit is the **final** step of a delivered response, so refuse (step 4), safety-block (step 6), and provider error are structurally unreachable for the debit (P10-03-BE-5 asserts this with tests).

### Daily soft-cap surfacing (P10-04)
- The daily counter (`DailyUsed`/`DailyUsedDateLocal` columns on `CreditAccount`) is **incremented inside the same atomic debit transaction** (P10-04-BE-5 `IncrementDailyUsage`, called from P10-03-BE-3) — no double-count, no separate balance.
- **How the soft warning is surfaced to the kid app:** via the dedicated **`EnergyStatusQuery`** read seam (P10-04-BE-4), returning a typed `WarningState {None, DailyCapReached, LowEnergy}` + balances. **The FE polls/reads this query** (it is the only backend obligation to P10-10). The AI Helper response itself stays a clean delivery; the warning is a separate read-model the meter renders. **Daily cap is SOFT by default** (warn + allow-continue; `HardStopEnabled` global flag default OFF). **Monthly grant is the HARD limit** (exhausted → step 2 decline). *(Recommendation if the lead later wants the warning piggy-backed on the AI stream: add a `warningState` field to the Helper SSE/result envelope — but the read-query approach is the clean module-isolated default and is what P10-04-BE-4 delivers.)*

---

## Atomicity / correctness notes

- **No double-debit:** `CreditTransaction` carries a DB-unique `IdempotencyKey` (`UX_CreditTransactions_IdempotencyKey`). The key = the per-request action id (one charge per delivered response). A retry of the same HTTP call reuses the key → the second `TryDebitAsync` is an idempotent no-op (`DebitOutcome.DuplicateIdempotent`), not a second charge.
- **No debit-without-delivery:** the debit is the final step of a successful delivery (cache-HIT or post-safety success). Refuse-and-redirect, safety-block, no-reveal violation, and provider/processing errors all `return` before the debit. P10-03-BE-5 unit/integration tests assert zero `TryDebitAsync` calls + zero `Spend` rows on each negative path.
- **Pre-auth vs debit reconciliation:** this design uses **debit-as-final-step** (OQ-AI-2 resolved), not reserve-then-confirm — the cheap `GetBalanceAsync` pre-check is advisory (blocks the obviously-exhausted case), and the authoritative atomic check is inside `DebitAsync` (loads with the `xmin` concurrency token, re-checks `TotalBalance < amount`, retries on `DbUpdateConcurrencyException`). No reservation row to leak or reconcile.
- **Granted-before-Purchased, atomic:** `DebitAsync` opens an explicit transaction (no UoW per rule 3 / ADR-0001), computes the Granted-first split, mutates the account, inserts the ledger row, commits — all in one transaction. The daily-counter increment (P10-04) joins the same transaction.
- **Concurrency:** `CreditAccount.Version` is the `xmin` concurrency token; the debit retries a bounded number of times on conflict. The refund clawback (P10-09-BE-3) uses the same explicit-transaction + clamp-≥-0 pattern to avoid a negative-balance race.
- **Payment activation atomicity:** the webhook handler (P10-06-BE-5) flips `Payment→Succeeded` + `Subscription→Active` + emits the integration event inside one explicit transaction; the grant fires from the post-commit `SubscriptionActivated` event (ADR-0002 post-commit events — the grant runs only after the activation commit, never inside it).
- **Webhook idempotency:** the dedicated `WebhookEvent` table with unique `ProviderEventId` (OQ-PAY-3 resolved) makes every webhook branch (activate / fail / pack-credit / refund-clawback) a no-op on replay. Refund clawback is additionally idempotent on the `CreditTransaction.IdempotencyKey`.
- **Never-retroactive config:** `GlobalSetting` rows are mutable (immediate-on-write + cache invalidation), but `CreditTransaction.Amount` ledger rows are immutable — a cost change affects future debits only. The `Moderation.AuditLog` old→new record is the traceability.

---

## Testability WITHOUT external accounts

**Every wave reaches "merged + green" with NO live payment account.** Confirmed per wave:
- **W1:** pure ledger + settings — no external dependency. `api-tester` (WebApplicationFactory + Testcontainers PostgreSQL) covers the read/admin endpoints.
- **W2 (energy debit):** verified with the **existing AI E2E harness** (fake Claude / fake BGE) **extended** to assert: balance **decremented by cost on cache-hit delivery and on generated delivery**; balance **unchanged** on refuse-and-redirect, safety-block, and provider error; **idempotent** on retry; **cross-child** debit rejected; daily counter increments + resets at local midnight.
- **W3 (payments):** `IPaymentProvider` + **`FakePaymentProvider`** (always-succeed / configurable-decline / replayable webhook) + **synthetic HMAC-signed webhooks**. `api-tester` drives `POST /api/Billing/Webhooks/Provider` with a synthetic signed body: success activates once, duplicate `ProviderEventId` is a no-op, bad signature is rejected, declined leaves the plan unchanged. **No real card data ever.**
- **W4 (packs/refunds/dunning):** synthetic `payment.succeeded` (pack → `PurchasedBalance += packSize`, idempotent), `charge.failed` (dunning state machine), `refund.succeeded` (clawback unspent-only, clamped ≥ 0, idempotent) — all through the fake.

**Flip-to-live steps are isolated to the real adapter** — see External/devops below.

---

## Review gates

| After | Gate |
|---|---|
| Each wave's implementer batches | `reviewer` against per-story ACs + CONVENTIONS + the relevant `security-auditor` result. Critical/High findings on a mandatory-audit story **block** the gate. |
| W1 batches | `api-tester` (P10-01 read seam, P10-12 admin endpoints) + `security-auditor` **light** (P10-01 ledger integrity, P10-12 admin-only + allowlist + no-secrets) → `reviewer` W1. |
| W2 batches | `api-tester` (P10-03 spend, P10-04 status, P10-02 grant idempotency, P10-11 authz) + `security-auditor` **MANDATORY on P10-03**, on P10-11; **light** on P10-02/P10-04 → `reviewer` W2. |
| W3 batches | `api-tester` (P10-05 commands, P10-06 webhook via fake) + `security-auditor` **MANDATORY on P10-06** (incl. IAP-compliance flag); light on P10-05 → `reviewer` W3. |
| W4 batches | `api-tester` (P10-07 pack, P10-09 clawback/dunning, P10-08 history) + `security-auditor` **MANDATORY on P10-07 & P10-09**; light on P10-08 → `reviewer` W4. |
| Each wave (final) | `committer` — per-story branches `feat/P10-0x-…` off the wave branch, `--no-ff` back into `feat/phase10-wave-N`, one wave PR per wave; **HANDOFF.md updated in the wave PR** (record GATE-1/GATE-2 decisions, the `ICreditSpendService` seam, the cost-key scheme, the `FakePaymentProvider`, and that the `FR-CREDIT-*`/`FR-PAY-*` family is new-to-SRS). Lead merges each wave PR. |

---

## Blockers / prerequisites

### Resolved (baked in — do NOT re-raise)
- ✅ **GATE-1** — `Billing` module APPROVED (4-layer, schema `billing`, mirror `Ai`/`Curriculum`).
- ✅ **GATE-2** — generic `IPaymentProvider` + `FakePaymentProvider` now; real Paymob adapter later (external).
- ✅ Energy economy model (value-meter; cache-hit charges; charge-on-delivery; monthly-hard / daily-soft; coexists with rate limiter; costs 1/2/3/5).
- ✅ P10-12 = DELTA on the existing `IGlobalSettingsProvider` (keep lean signature); P10-11 folds in.
- ✅ P10-10 + all FE = out of scope; backend obligation = `EnergyStatusQuery` (P10-04-BE-4).

### ⚠️ NEW blocker found (task-file drift the implementers must NOT follow)
- **DRIFT-1 — `IGlobalSettingsProvider` signature.** The **task file `P10-12-BE-3` proposes named convenience properties** (`FreeMonthlyCredits`, `PremiumMonthlyCredits`, etc.) **and an `InvalidateCacheAsync` method on the public interface**, and several task files (P10-02-BE-1/3, P10-04-BE-4, P10-07-BE-2) reference `provider.FreeMonthlyCredits` style accessors. **This contradicts the LOCKED decision #4 and brief §0/D1:** the live interface (`backend/src/Shared/Learnexia.Shared.Kernel/Settings/IGlobalSettingsProvider.cs`) is the **lean 4-getter shape** and is **already called by the 4 live Ai handlers via string keys** — adding named getters / changing the signature is a **breaking change to shipped callers**. **Resolution baked into this plan:** P10-12-BE-3 implements the EXISTING lean interface **unchanged**; all consumers resolve values via **`GlobalSettingKeys` string constants + defaults** (`_settings.GetInt("credits.free_monthly", 100)`), NOT named properties. Cache invalidation is an **internal concern of the DB-backed impl / the update command** (not a public interface method). **Hand implementers brief §0/D1 + this note, not the raw P10-12-BE-3 wording.** This is a planning correction, not a lead decision needed.

### External / devops — separated (the flip-to-live steps; none block merge)
- **[EXTERNAL] Real provider adapter** — `PaymobPaymentProvider` implementing `IPaymentProvider` against Paymob's API/SDK. Swapped behind the seam; does not block W3/W4 merge (FakePaymentProvider carries the tests).
- **[EXTERNAL] Secrets** — Paymob API key + webhook signing secret in the secret store (never committed).
- **[EXTERNAL] Live webhook URL** — register + reachability (devops).
- **[EXTERNAL] Recurring/auto-renew capability** — confirm Paymob supports true card-on-file recurring; if not, P10-06 AC4 / P10-09 dunning fall back to renewal-reminder + re-checkout link (the seam + `FakePaymentProvider` model both; the live behavior is the external concern).
- **[EXTERNAL] App/Play Store IAP-compliance** — web-checkout-only strategy is a legal/commercial **native-launch-gating** review (flagged by `security-auditor` on P10-06, resolved by the lead — not code; does not block backend merge).

### Non-blocking flags to surface
- **OQ-FIN-1 (P10-08 receipts/VAT):** legally-required EGP/VAT receipt fields + sequential invoice numbering need finance confirmation before the receipt generator is final. Backend builds the read + printable HTML now; the field *values* are conditional (P10-08-BE-1).
- **OQ-PAY-4 (currency unit):** confirm the stored unit for `credits.pack_price_egp`, `payment.provider_fees` (% vs flat), `fx.usd_exchange_rate_buffer` **before seeding** the 17 keys in P10-12-BE-1.
- **OQ-SRS-1 (traceability):** the `FR-CREDIT-*` / `FR-PAY-*` family is referenced by the stories but absent from the SRS FR list — flag to the SRS-keeper (dangling traceability, not a build blocker).

---

## Definition of done

### Per wave
- **W1:** `Billing` module scaffolded (4 layers, schema `billing`, in `.sln`/`Program.cs`/`Claims`); `CreditAccount` + append-only `CreditTransaction` with DB-unique idempotency; `DebitAsync` draws Granted-before-Purchased atomically with no over-draw under concurrency; `GetCreditAccountQuery` + `ReconcileAccountQuery`; **`ICreditSpendService` seam published**. `platform.GlobalSettings` store seeded with 17 keys; DB-backed `GlobalSettingsProvider` replaces bootstrap behind the **unchanged** lean interface; admin `GET/PUT /api/Admin/GlobalSettings` admin-only + allowlist + cache-invalidate-on-write + audit. api-tester green; security-auditor light gate clear; reviewer PASS; wave-1 PR opened.
- **W2:** Monthly grant job (mirror `StreakSweepJob`, idempotent per child+cycle, expire-then-grant); energy debit wired into all 4 Ai handlers — **charge-on-delivery, cache-hit charged same, no-charge on refuse/safety-block/error/insufficient, idempotent, child-from-JWT**; daily soft-cap counter + lazy timezone reset + `EnergyStatusQuery` (the FE seam); admin write-surface live. **security-auditor MANDATORY on P10-03 PASS** (no Critical/High). api-tester green; reviewer PASS; wave-2 PR.
- **W3:** Plan/Subscription model + upgrade(→PendingPayment)/downgrade/cancel + plan-comparison; real `IBillingSubscriptionContract` replaces the P10-02 stub; **`IPaymentProvider` + `FakePaymentProvider`**, server-side amount, idempotent signed-webhook activation, `SubscriptionActivated`/`PaymentFailed` events, JWT-free signature-authenticated webhook endpoint, reconcile job. **security-auditor MANDATORY on P10-06 PASS** (no card data, signature verify, idempotency, secrets out of code, IAP flag recorded). api-tester green (via fake); reviewer PASS; wave-3 PR.
- **W4:** Energy pack purchase → `PurchasedBalance` (never expires), parent-only + family-scoped; dunning state machine + Notifications event + idempotent refund clawback (unspent-only, clamped ≥ 0) + dunning retry job + admin refund; parent-scoped billing history + anti-IDOR printable-HTML receipts. **security-auditor MANDATORY on P10-07 & P10-09 PASS.** api-tester green; reviewer PASS; wave-4 PR.

### Overall (tied to story acceptance criteria)
- The **entire Phase-10 backend** — ledger, grant, spend, cap, subscription model, payment orchestration, packs, refunds/dunning, history, settings/admin — is **built, tested, and merged WITHOUT a live payment account**, every payment path verifiable via `FakePaymentProvider` + synthetic signed webhooks and every energy debit verifiable via the extended AI E2E harness.
- Module isolation holds: no `Ai.*`→`Billing.*` reference; all cross-module interaction via `Shared.Contracts` seams; no cross-module FK.
- Energy invariants hold: cache-hit charges same; charge-on-delivery only; no-delivery → no-charge (free); monthly grant is the hard limit; daily cap is soft by default; energy coexists with the rate limiter; costs 1/2/3/5 server-resolved from `IGlobalSettingsProvider`.
- The deferred **P3-01-BE-14 (quota/`IAiUsageBudget`)** is landed by the energy ledger (grant + daily cap + per-call debit) — no separate budget class introduced; AC-7 reinterpreted (drift D3) and documented.
- HANDOFF.md records the GATE-1/GATE-2 decisions, the `ICreditSpendService` seam + cost-key scheme, the `FakePaymentProvider`, the lean-interface drift correction, and the External/devops flip-to-live steps.

---

*Plan author: planner · Date: 2026-06-15 · Source: `docs/briefs/phase-10-billing.md` (consolidated cluster brief) + `tasks/Backend/Phase-10-Payments-Billing/*` + live-code verification of `Ai`/`IGlobalSettingsProvider`/`AiResponseCache` on `main`.*
