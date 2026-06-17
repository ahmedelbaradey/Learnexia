# Handoff — Phase 1 web frontend + dev environment

> Living handoff for leads/agents picking up the web frontend + backend work. Last updated 2026-06-06 (**FE state reconciliation — see the ⭐ block directly below; Phase-1 + Phase-2 student FE confirmed merged, P8-04 FE corrected to not-started.** Earlier: **Phase 8 — Localization backend COMPLETE + merged to main (P8-01/02/03 PR #90; P8-04 PR #91) — see the Phase 8 section directly below.** Earlier status: **P4-11 BE — Streak freeze + timed events + weekly challenges + XP boost — commit/PR ready. P4-10 BE merged. P4-09 merged via PR #80. P4-08 FE WIP on `feat/P4-08-gamification-screens-motion` (Batches 2–6 still open for FE lead). Earlier P4-* per below.**).
> 2026-06-17: **BACKLOG — Parent-app parent-scoped READ API gap (Phase-5 Parent Analytics backend). DEFERRED — the Phase-10 Family-Wallet wave stays the SOLE focus first.** The parent app's analytics/gamification/energy/activity screens (My Children cards, Overview KPI/mastery/focus/recommendations/daily-activity, Reports XP/time-of-day, Helper Energy, Activity feed) are FAKED because the backend only exposes SELF-SCOPED student endpoints (`useMyBadges`/`useDashboard`/`useMyLeague`/`useStudentAttempts`…); there are NO parent-scoped "read MY child's data" endpoints. The data EXISTS (Gamification/Learning/Billing/Notifications) — fix = parent-scoped per-child READ endpoints with parent-owns-child authz via the existing `IParentChildQuery` seam + `Shared.Contracts` (no cross-module FK). **Missing endpoints (proposed) → owner:** `GET /Parent/Children/{id}/Progress` (level/xp/streak/mastery%/weakest/activeToday/energy) → Gamification+Learning+Billing; `GET /Parent/Family/Summary` (activeLearners/lessonsCompleted/totalXp/bestStreak/badges) → Gamification+Learning; `GET /Parent/Children/{id}/WeeklyKpis` (+WoW deltas) → Learning+Gamification; `GET /Parent/Children/{id}/SubjectMastery` → Learning; `GET /Parent/Children/{id}/FocusAreas` → Learning (**maps to P5-02 weak-area detection**); `GET /Parent/Children/{id}/Recommendations` (Lexi) → AI (**via the recommendations-engine feature — Learning computes / AI narrates — after P5-02**); `GET /Parent/Children/{id}/DailyActivity` + `/TimeOfDay` → Learning; `GET /Parent/Activity` (parent feed: badges/energy/alerts) → Notifications. **Energy parent read:** do NOT build `GET /Billing/CreditAccount/{childId}` — STALE: `CreditAccount` is being RETIRED by the Family-Wallet wave; the parent energy screen must consume the NEW wave endpoints (`FamilyEnergy/Overview` + per-child EnergyStatus) → folds into the wave, do not parallelize. **Parallelism:** the Gamification/Learning/Notifications reads are independent of the wave (different modules) → after the wave hits a stable checkpoint, run them as parallel `feat/<StoryID>` worktrees (serialize shared-file edits: Program.cs / Claims / Shared.Contracts). **Reconcile, don't duplicate** — Phase-5 stories already cover much of this (P5-01 weekly report, P5-02 weak areas, P5-05 parent dashboard). Per CLAUDE.md rule #9, capture as stories BEFORE building. (Source: FE-lead "Parent app — API gap report", 2026-06-17.)
> 2026-06-17: **P10-14 (Child Seats) — batch verified GREEN + KNOWN-RED-CI baseline recorded.** Re-verifying the P10-14 blocker fix surfaced 23 full-suite failures; **all attributed**. **Caused by P10-14 (all FIXED + verified green):** (1) **WEBHOOK-SEAT-04** — real code bug: the seat-payment branch in `WebhookEventService` incremented `PurchasedExtraSeats` with **no `seats.max` ceiling check** (`included 3 + purchased 3 = 6 > max 5`); `SeatService.IncrementPurchasedSeatsAsync` has the guard but the webhook can't call it (nested-tx vs the blocker-3 single-tx) → fix = **inline ceiling guard** in the webhook branch (+ injected `IGlobalSettingsProvider`, +fixed 2 Billing unit-test ctors). (2) **10 stale-seat-seed tests** (`P1_03_AddChild` AC-2×2/AC-7/BE-TC-05/24/29, `P2_12` PAR-2, `P10_13` GRANT-01/02 + CUTOVER-02) — these add 2–3 children to a **Free** parent (1 seat) and pre-date the seat gate, so the 2nd child correctly hit 409; fix = new shared **`SeatTestSupport.GrantSeatsAsync`** (seeds a Premium 3-seat subscription directly in the `billing` schema, bypassing checkout) called before the multi-child adds. The add-child 409-on-no-seat behaviour itself is **correct per P10-14 AC** ("no seat available → rejected cleanly, no child created") — NOT changed. (3) **TC-GS-04** — `GlobalSettingsSeeder` gained 4 `seats.*` keys, so the "exactly 17 rows" assertion became **21**; fix = 17→21 + assert the 4 seat values. **Verified:** blast-radius classes `P10_14`+`P10_13`+`P1_03`+`P2_12` = **168/168**, `P10_01_12_Billing` = **19/19** (after rebuild — `--no-build` masks test edits). **KNOWN RED CI — 21 PRE-EXISTING failures (exist on HEAD, NOT introduced by P10-14; lead-approved to keep P10-14 scoped + track separately):** **(A) ~19 AI-SSE tests** (`P3_04/05/06` Explain/Hint/WhyWrong/Simplify/SimilarExample `TC-*`) — `IAiProvider` has only the REAL `ClaudeProvider`/`OpenAiProvider`, no deterministic test fake, and the local LLM keys are empty (`Ai:Providers:*:ApiKey=""`), so every AI-SSE request fails to auth. **Owner: AI-infra follow-up** (add a Testing-env fake `IAiProvider`, or inject keys in CI). **(B) `BE-TC-24`** (P1_05_RBAC) — strict-parses `appsettings.json` which contains `//` comments → `JsonReaderException` line 76. **Owner: test/infra cleanup** (JSONC-tolerant parse or strip comments). **(C) `BE-TC-19b`** (P1_05_RBAC) — asserts `Claims.GenerateModules()` returns exactly 3 modules but it now returns 5 (`Curriculum`, `Billing` added in earlier phases). **Owner: test cleanup** (update expected module list). **P10-14 merge gate = "introduces no NEW failures beyond this baseline."** Do NOT fix A/B/C inside the P10-14 PR (lead decision 2026-06-17 — don't mix unrelated infra/test debt into a billing-seat PR). **Security-auditor (money + child-data) on the P10-14 tree: 1 blocking High + 2 Mediums — High & one Medium FIXED in-PR, one Medium DEFERRED to P10-15:** **(High, FIXED)** seat webhook could double-grant a seat if the provider sent TWO `payment.succeeded` events with DISTINCT event ids for the SAME payment (outer `WebhookEvent.ProviderEventId` guard only stops same-id replay; the Pack path had an inner per-payment guard, the Seat path didn't) → fix = gate the seat increment on `payment.Status == Initiated` (the flip to Succeeded is the single-shot lock), all inside the existing single tx; regression test WEBHOOK-SEAT-05 added. **(Medium, FIXED)** dead/divergent `ISeatService.IncrementPurchasedSeatsAsync` (never called; a second copy of the seats.max money-guard, with a false "appends an immutable ledger row" doc) → DELETED; the webhook's inline guard is now the single source of truth. **(Medium #3, DEFERRED → P10-15)** `SeatService.ReserveSeatAsync` ignores the passed `idempotencyKey` and uses a shared `childId=0` placeholder, so two CONCURRENT add-child calls for the SAME parent (different emails) can collide on the `(SubscriptionId, ChildId=0)` unique index → 2nd gets `AlreadyReserved`, its child is still created, and `ActivateSeatAsync` finds no free placeholder → a child created with NO Active seat row (seat undercount). **Why deferable:** single-threaded happy path is correct (19/19 green); there is NO live AI-access bypass yet because per-child seat ENFORCEMENT (gating AI on an Active seat) is P10-15 — so an unseated child has no access until enforcement exists. **P10-15 must:** (a) make the reservation idempotency-key real (persist a unique key OR use a per-attempt non-zero placeholder) to close the race, and (b) reconcile `Active`/`Reserved` reservation rows against the entitlement count during enforcement. IDOR / server-side-money / no-energy-mint / signature / single-tx invariants all verified HOLDING; no Critical/High remaining after the fix.
> 2026-06-16: **Option C sweep — Notifications module DONE (on branch `refactor/optionc-notifications`).** Third module in the persistence-refactor sweep. **Behavior-preserving refactor** — relocates all inbox/preferences/device-token/reengagement-dedupe logic from handlers into 5 service abstractions + implementations. **New services:** `INotificationInboxService` (owns mark-read, list inbox logic), `INotificationPreferenceService` (preference upserts, atomic transactions with default-synthesis), `IChildReengagementPreferenceService` (child-scoped preference mutations), `IDeviceTokenService` (device token cap/reassign/revocation), `IReengagementDedupeStore` (Redis SETNX dedupe for idempotency). All registered in `Notifications.Infrastructure/DependencyInjection.cs`. **Handlers repointed:** ~17 feature handlers (`ListQueryHandler`, `UpdateMyNotificationPreferencesCommandHandler`, `GetMyNotificationPreferencesQueryHandler`, `MarkAllNotificationsReadCommandHandler`, `MarkNotificationReadCommandHandler`, `RegisterDeviceCommandHandler`, `RevokeDeviceCommandHandler`, `UpdateChildReengagementPreferencesCommandHandler`, `GetChildReengagementPreferencesQueryHandler`, `ListMyInboxQueryHandler`) + 11 integration event handlers (`BadgeEarnedIntegrationEventHandler`, `DailyMissionReminderIntegrationEventHandler`, `HeartsDepletedIntegrationEventHandler`, `HeartsRefilledIntegrationEventHandler`, `LapseWinBackIntegrationEventHandler`, `MissionCompletedIntegrationEventHandler`, `StreakAtRiskIntegrationEventHandler`, `StreakBrokenIntegrationEventHandler`, `UserRegisteredIntegrationEventHandler`) + `ReengagementHandlerHelper` + `NudgeDispatcher` now inject services only, never `INotificationsDbContext`. **Deleted:** `INotificationsDbContext` abstraction (handlers no longer reference it). **Removed dependencies:** `Notifications.Application.csproj` dropped `Microsoft.EntityFrameworkCore` + `StackExchange.Redis`; `Notifications.Infrastructure.csproj` added `StackExchange.Redis`. **Architecture:** Service-only `Notifications.Application` + EF-free compliance (CONVENTIONS §7). **Verification:** Behavior-preserving — unit **33/33**, integration **47/48** (1 pre-existing failure `BE-TC-19` = hardcoded WSL path env test, not a regression), zero test logic changes, IDOR/auth/transactions/defaults byte-for-byte preserved. No migration. **Reviewer PASS** (pure relocating, no security gate — IDOR, auth, child-reengagement pref access controls identical). **Sweep progress:** ✅ Moderation (#152) → ✅ Gamification (#153) → ✅ Notifications → Billing (TODO) → Learning/Ai (excluded). **Non-blocking follow-up:** `BE-TC-19` hardcoded WSL2 path should be made portable in a later cleanup pass.
> Captures what's done, the decisions, the load-building config, and what's next. If you change any of these, update this file.
> 2026-06-16: **Option C sweep — Gamification DONE** — handlers→services (service-only, EF-free); unit 311 + P4 172 unchanged; behavior-preserving; no migration. **What:** 8 per-aggregate services (Xp/Streak/Heart/Badge/Mission/League/Admin/Query) own orchestration/rules/idempotency (using `IGamificationRepository` + untouched domain engines); all ~25 command/query/notification handlers repointed to inject services only — no repository, no EF, no DbContext in `Gamification.Application`; EF exceptions translated at the Infra boundary (`GamificationUniqueConstraintException` in `UnitOfWorkBehavior`). **Sweep progress:** ✅ Moderation (#152) → ✅ Gamification → ⬜ Notifications → ⬜ Billing → ⬜ Learning (Ai+Identity excluded). **Open nit:** repository INTERFACES still live in `*.Application/Abstractions` (matches Learning precedent; consumed only by Infra services, never a handler) — move to Infrastructure only if the lead wants strict "Application = service interfaces only."
> 2026-06-16: **Option C sweep — Billing module refactor COMMITTED** (`refactor/optionc-billing` → PR #155). Fourth module in the persistence-refactor sweep. **Behavior-preserving:** relocates money logic only; 5 services own all credit-ledger/webhook/subscription/checkout/global-setting persistence + workflows (transactions, xmin retry, 23505 idempotency, post-commit integration/admin events, server-side amount reconciliation moved inside services); 17 handlers thinned to validate/authorize/delegate/map; `IBillingDbContext` relocated to `Infrastructure/Persistence` (kept for pre-existing `CreditSpendService`/`RefundService`); `Billing.Application` EF-free (EF pkg removed from csproj). **Verification (money-critical, triple-gated):** build 0 errors; unit **93/93** + P10 integration **62/62**; security-auditor **0 Critical/High** (debit-on-delivery, idempotency, IDOR, webhook-HMAC-first preserved); adversarial drift hunter confirmed all 8 money invariants preserved 1:1 (`driftFound:false` high-confidence), no migration. **Sweep:** ✅ Moderation (#152) → ✅ Gamification (#153) → ✅ Notifications (#154) → ✅ Billing (#155) → ⬜ Learning (last; Ai + Identity excluded). Next: merge approval.
> 2026-06-15: **Phase 10 Wave 4 Batch A — P10-07 "Buy an Energy Pack" BUILT on `feat/phase10-wave-4` (DO NOT COMMIT — pending reviewer gate).** First story built under **Option C (new data-access rule)**: Application-layer handlers inject `IEnergyPackService` ONLY — zero `IBillingDbContext`, zero EF types in Application. All pack persistence lives in `Billing.Infrastructure/Services/EnergyPackService`. **What was built:** (1) `IEnergyPackService` (Application/Abstractions, Option C seam) + `EnergyPackService` (Infrastructure, owns EF + explicit transaction). (2) `StartPackCheckoutCommand` + handler (EF-free: resolves `parentId` from JWT, builds idempotency key, calls `IEnergyPackService.StartPackCheckoutAsync`) + `StartPackCheckoutValidator` + `PackCheckoutResultDto`. (3) `PackController.cs` — `POST /api/Billing/Packs/Checkout` (`[Authorize]`, `[FromBody] int childId`, IDOR-safe). (4) Webhook handler extended minimally — new `IEnergyPackService _energyPackService` 3rd constructor param; Pack branch inside `HandlePaymentSucceededAsync`: commits `WebhookEvent` first (outer idempotency guard), then delegates to `CreditPurchasedPackAsync` (owns its own inner transaction + inner `CreditTransaction.IdempotencyKey` guard). Subscription path UNCHANGED. (5) DI registration: `AddScoped<IEnergyPackService, EnergyPackService>()`. (6) 4 SharedResourcesKey constants + en-US + ar-EG resx entries (PackCheckoutSessionCreated, PackCheckoutChildNotOwned, PackCheckoutChildIdRequired, PackCreditedSuccessfully). **No migration** — `Payment.Kind=Pack(=1)` + `Payment.TargetChildId` already exist from W3. Pack size + price from `IGlobalSettingsProvider` keys (`credits.pack_size` / `credits.pack_price_egp`). IDOR: `IParentChildQuery.IsParentOfChildAsync` BEFORE any Payment row is created. Idempotency: two-layer — outer `WebhookEvent.ProviderEventId` unique constraint + inner `CreditTransaction.IdempotencyKey = "pack-credit:{paymentId}:{providerEventId}"` unique constraint. **Tests: 77/77 PASS** (10 new P10-07 unit tests in `EnergyPackTests.cs`). **Test-layer note:** `CreditAccount.xmin` is a Npgsql `xid`/row-version column; SQLite does not emulate it, so tests that would need to UPDATE CreditAccount via EF use domain-entity (in-memory) approach or mock `IEnergyPackService`. PACK-03/06 = domain-entity; PACK-05/07/E2E/Replay = mocked IEnergyPackService (handler routing verified without EF). The full DB path (real transaction + idempotency) is covered by integration tests against Postgres. **Build: 0 errors.** **EF-free confirmation:** grep of `StartPackCheckoutCommandHandler` returns only a comment (no real IBillingDbContext/EF reference). **W4b remaining** (after reviewer + committer gate this batch): P10-08 (payment history), P10-09 (refunds + dunning). Pack FE (checkout redirect, success screen) = FE lead scope.
> 2026-06-15: **Phase 10 Wave 3 — ALL GATES PASS, ready to commit (`feat/phase10-wave-3`, stacked on W2).** W3a (P10-05): `Plan`/`Subscription` (schema `billing`) + real `RealBillingSubscriptionContract` (replaces the W2 Free-tier stub; resolves family tier via `IParentChildQuery`; W2 `BillingGrantJob` unchanged → grants per-tier) + IDOR-scoped manage endpoints (GetCurrentPlan/Comparison/Upgrade→PendingPayment/Downgrade/Cancel) + `BillingPlansSubscriptions` migration. W3b (P10-06): `IPaymentProvider` + `FakePaymentProvider` (config-selected `Billing:PaymentProvider`, default Fake, empty secret = fail-closed) + `Payment`/`WebhookEvent` + `AddPaymentAndWebhookTables` migration + `StartSubscriptionCheckout` + **signature-gated idempotent webhook** `POST /api/Billing/Webhooks/Provider` (HMAC-SHA256 `FixedTimeEquals` verify BEFORE any mutation → 401 generic on bad sig; dedupe on unique `ProviderEventId` + 23505 race-guard → exactly-once; server-side amount/tier authoritative → forged amount can't escalate) → `SubscriptionActivatedIntegrationEvent` (post-commit) → grant tops up to Premium; `ReconcilePaymentsJob` (Hangfire). **Test-infra:** webhook/txn unit tests use SQLite `:memory:` (EF-InMemory can't do transactions); `Microsoft.EntityFrameworkCore.Sqlite` added to `Directory.Packages.props`. Reviewer PASS, **mandatory security PASS (0 blocking)**, api-tester W3 E2E **20/20**, Billing unit **67**, regressions green (W2 energy 21, AI E2E 24, W1 Billing 19). **SECURITY Low go-live follow-ups (non-blocking — before LIVE payments / real Paymob adapter):** (1) webhook amount-mismatch is only `LogInfo`'d → add a metric/alert (fraud signal); (2) provider selection **fails OPEN** (any non-"Fake" name silently falls back to FakePaymentProvider) → add a Production/Staging fail-fast so Fake can never run live; (3) webhook endpoint is `[DisableRequestSizeLimit]` with only the global rate rule → add an explicit body-size + per-endpoint rate cap. **Nits:** repeated Upgrade can create orphan `PendingPayment` rows (filtered-unique only guards Active); leftover dead `ConfigDefaultSubscriptionContract` (DI replaced) — delete in cleanup. **Flip-to-live:** set `Billing:PaymentProvider:Provider="Paymob"` + the real adapter + signing secret/API key via env (empty defaults; never commit). 
> 2026-06-15: **Phase 10 Wave 2 — ALL GATES PASS, ready to commit (`feat/phase10-wave-2`).** W2a (idempotent monthly grant job + daily-soft/monthly-hard caps + IDOR-scoped `EnergyStatusQuery` + ledger `FromGranted`/`FromPurchased` split → exact mixed-pool reconcile + `AddLedgerSplitColumns` migration) + W2b (P10-03 charge-on-delivery). Reviewer PASS, **mandatory security-auditor PASS (0 blocking)**, api-tester energy-economy E2E **21/21**, Billing unit **43**, Ai unit **287**, W1 Billing integration **19/19**. **Regression caught+fixed:** W2b charging broke the AI E2E fixture (it seeded no energy → every student hit `AiInsufficientEnergy` at pre-auth → 14/24 RED); fix = `AiRuntimeTestFactory` now migrates `BillingDbContext` + seeds a 500-credit `CreditAccount` + `GlobalSettingsSeeder` → **AI E2E back to 24/24** (TEST-FIXTURE ONLY; production handler code unchanged). **SECURITY ADVISORIES (non-blocking; carry to AI go-live, accepted-with-monitoring):** (1) **MEDIUM** — `DebitEnergyAsync` in all 4 handlers is fail-soft on billing/DB error (a billing outage → FREE AI; never over-charges a child) → before go-live add a **debit-failure-rate metric + alert** and consider a circuit policy so a sustained outage isn't an open free-tap. (2) **LOW** — cache-HIT delivery point #1 doesn't branch on a `DebitOutcome.InsufficientBalance` race (a concurrent drain can deliver ONE free HIT; revenue-leak only, never over-charge); post-safety point #2 already guards it. (3) **LOW/Info** — over-charge only on a post-debit SSE write failure (client disconnect after debit-commit, pre-emit — very narrow; content was generated+approved). 
> 2026-06-15: **Phase 10 Wave 2 Batch B — P10-03 energy-spend / AI integration BUILT on `feat/phase10-wave-2` (DO NOT COMMIT — pending reviewer gate).** Wired all 4 AI handlers with charge-on-delivery per the locked economy model. **Locked economy model:** Cache HIT charges energy. Cache MISS charges energy. Both delivery points debit. Pre-authorize before AI request; debit ONLY after successful delivery. No delivery (refuse-and-redirect, safety-block, generation error, pre-auth fail) = FREE. Monthly = HARD limit; Daily = SOFT warning (`Billing:HardStopEnabled=false` default). Per-intent costs via GlobalSettings `ai_cost.*` keys: Hint=1, WhyWrong=2, Explain/Simplify=3, SimilarExample/Practice=5. Idempotency key = per-REQUEST Guid (retry-safe). **`CreditCostResolver`** (Singleton, `Ai.Application/Services/`) reads `IGlobalSettingsProvider` ai_cost.* keys + `IConfiguration["Billing:HardStopEnabled"]` (indexer, NOT `GetValue<T>` — avoids IConfigurationSection null in unit tests). Registered as `services.AddSingleton<CreditCostResolver>()` in `Ai.Application/DependencyInjection.cs`. **`EnergyBalance` record extended** with optional `DailyUsed`/`DailyCap`/`DailyCapReached` params (defaults=0/10/false; backward-compat with all existing callers). **`CreditSpendService.ExecuteDebitCoreAsync`** atomically increments `DailyUsed` inside the same DB transaction (lazy-reset on stale date). **`CreditCostResolver.ResolveReasonCode`** returns enum name as string (e.g. `"AiHint"`, `"AiWhyWrong"`, `"AiDeepExplanation"`, `"AiPracticeGeneration"`). **`DebitEnergyAsync` helper** in each handler is fail-soft (billing outage never hard-blocks learning). **Key-name decision:** `Learnexia.Shared.Contracts.Ai.HelperIntent` namespace (NOT `AiTutor`) — `CreditCostResolver` uses `using Learnexia.Shared.Contracts.Ai`. **SharedResourcesKey:** 2 new keys added in both resx files — `AiInsufficientEnergy` (monthly hard-exhausted) and `AiDailyCapReached` (daily hard-stop). **Unit tests:** 287/287 pass. New P10-03-BE-5 no-charge tests added to all 3 existing handler test classes (ExplainConceptCommandHandlerTests: 6 new; GetHintCommandHandlerTests: 4 new + no-reveal-violation no-charge; SimilarExampleCommandHandlerTests: 4 new). `AiGradeClaimCacheDifferentiationTests` updated to supply billing mocks. Build: 0 errors. **Next:** reviewer gate → committer PR.
> 2026-06-15: **Phase 10 Wave 1 Batch A — `Billing` module + energy/credit ledger (P10-01) BUILT on `feat/phase10-wave-1`.** New module scaffold (4 projects: Domain/Application/Infrastructure/Api). Schema `billing`. Added to `Learnexia.Modular.sln` + Host (`Program.cs` + `Learnexia.Host.csproj`). `Claims.GenerateModules()` + `"Billing"` added. **Entities:** `CreditAccount` (per-child, no cross-module FK, GrantedBalance + PurchasedBalance + xmin concurrency token + P10-04 daily-cap columns folded in: DailyUsed/DailyUsedDateLocal/ChildTimeZoneId), `CreditTransaction` (append-only ledger, IdempotencyKey unique). **Debit primitive:** explicit DB transaction + idempotency pre-check + never-negative guard + Granted-first split + DbUpdateConcurrencyException bounded retry (MaxRetries=3) + unique-violation catch. **Commands:** SpendCredit (atomic debit), GrantCredit, ApplyPurchase, ExpireGrant, Refund, Adjust — all idempotent. **Queries:** GetCreditAccountQuery (read balance), ReconcileAccountQuery (drift detect, no auto-heal). **Cross-module seam:** `ICreditSpendService` declared in `Shared.Contracts/Billing/` (TryDebitAsync + GetBalanceAsync), implemented in `Billing.Infrastructure.Services.CreditSpendService` — same algorithm as command handler. Ai handlers consume it in W2/P10-03; NOT wired yet. `EnergyBalance` + `DebitResult` + `DebitOutcome` sealed records in Shared.Contracts/Billing/. **SharedResourcesKey:** 6 keys added (en-US + ar-EG): InsufficientBalance, CreditAccountNotFound, CreditDebitSucceeded, CreditGrantSucceeded, CreditReconcileSucceeded, CreditIdempotentDuplicate. **EF config:** `CreditAccountConfig` (IX_CreditAccounts_ChildId unique, xmin concurrency, timestamptz, default Africa/Cairo), `CreditTransactionConfig` (UX_CreditTransactions_IdempotencyKey unique, composite index AccountId+OccurredAtUtc, enum int conversions). **Migration:** `InitialBilling` NOT generated here — db-migration agent's responsibility (EF configs complete and ready). **Unit tests:** 11/11 pass (LA-01..LA-10 cover: grant, granted-first debit, purchased debit, mixed pool debit, never-negative throw, accounting identity, expire grant, refund clamped, adjust +/−, apply purchase). **Build:** `dotnet build Learnexia.Modular.sln` → 0 errors. **Bug fixed during build:** `ChangeTracker` in `IBillingDbContext` needed `using Microsoft.EntityFrameworkCore.ChangeTracking;`; `SaveChangesAsync(_currentUser.UserId)` → `UserId ?? 0` (ICurrentUserService.UserId is int?). **Deferred to W2:** EnergyStatusQuery (P10-04-BE-4), Ai handler wiring (P10-03), daily-cap enforcement logic. **DRIFT-1 respected:** IGlobalSettingsProvider unchanged.
> 2026-06-15: **Phase-4 AI Runtime — Cluster B security hardening: AiResponseCache key defects fixed (on wave `feat/phase4-ai-runtime`).** Three blocking/medium security-auditor findings addressed in `AiCacheKeyBuilder` + all 4 handler call-sites (no new files in production code — only `Cache/AiCacheKeyBuilder.cs` and the 4 command handlers changed, plus `tests/Modules.Ai.UnitTests/AiCacheKeyBuilderSecurityTests.cs` added). **Finding #1 (HIGH): `subjectId` absent from all 4 key builders** — a Math concept/question with the same integer id as a Science concept/question could be served from the same cache slot. Fix: `subjectId` added to `ForExplain`, `ForHint`, `ForWhyWrong`, `ForPractice`; all 4 handlers thread `learningCtx.SubjectId` (or `lesson?.SubjectId ?? 0` for Explain/Simplify). **Finding #2 (MEDIUM): Hint key had no grade/age dimension** — a hint generated for a Grade-1 student was cacheable for a Grade-9 student. Fix: `AgeBand(jwtGrade)` added to `ForHint` (consistent with how `ForWhyWrong` already carried `AgeBand`). **Finding #3 (MEDIUM): grade/age dimension sourced from spoofable lesson/context grade, not JWT** — handlers computed `gradeResolved = lesson?.GradeId > 0 ? lesson.GradeId : grade` and passed that to the key builders; a student picking a lesson tagged at a higher grade would be keyed into an older cohort's cache entries. Fix: all 4 key builders now receive `jwtGrade` (the JWT-claim grade, resolved by `TryResolveProfile` from `ICurrentUserService`, server-trusted). `gradeResolved`/lesson grade is still passed to `PromptContext` for prompt grounding (intentional: the lesson subject/topic stays in the prompt) but does NOT drive the cache key. **Prompt-grade observation (flagged, not changed):** `PromptContext.Grade` still receives `gradeResolved` (lesson grade if available) — the generated content may therefore be slightly graded toward the lesson rather than the student's JWT grade. This is an acceptable content-personalisation trade-off and does not create a safety/cohort serving hazard because the key (now keyed on JWT grade) prevents cross-cohort cache serves. The reviewer may wish to track this separately if prompt tailoring must also be strictly JWT-grade. **SimilarExample ordering change:** `SimilarExampleCommandHandler` previously did cache lookup BEFORE context fetch (to enable early HIT). This order was reversed — context is fetched first (MISS path unchanged in cost; HIT path now does one extra context fetch, which is acceptable to ensure `subjectId` is available for the key). **Key tuples after fix:** Explain = `(SubjectId, ConceptId, AgeBand(jwtGrade), Language, Difficulty, PromptVersion, CurriculumVersion)`; Hint = `(SubjectId, QuestionId, HintLevel, AgeBand(jwtGrade), Language, PromptVersion, CurriculumVersion)`; WhyWrong = `(SubjectId, QuestionId, SHA256(NormalizedWrongAnswer), Language, AgeBand(jwtGrade), PromptVersion, CurriculumVersion)`; Practice = `(SubjectId, SkillKey, VariationIndex, AgeBand(jwtGrade), Language, PromptVersion, CurriculumVersion)`. **Tests:** 18 new tests in `AiCacheKeyBuilderSecurityTests.cs` (CK-01..18) covering: different subjectId → different key (all 4 intents), different age-band → different key, same age-band (within band) → same key (cross-student reuse preserved), band boundary correctness (grade 4 and 6 = band2; grade 7 = band3), all-inputs-same → same key (determinism), cross-intent isolation. Total: 237/237 pass (was 219). Build: 0 errors.
> 2026-06-15: **Operationalize Phase-4 AI — Cluster A (RAG/BGE-M3 re-embed) DONE (on wave `feat/phase4-ai-runtime`).** Built the runtime operationalization of P3-07 RAG infrastructure: hardened the parity guard (Config-missing → RAG dormant/null, never crashes; BaseUrl set + empty ModelVersion → fail-fast, never mixes placeholder + real vectors), added the idempotent fail-soft **`ReEmbedCurriculumJob`** (Hangfire batches=50, no-progress break-guard), wired the AdminOnly **`POST /api/Admin/Curriculum/ReEmbed`** trigger endpoint, made similarity floor config-bound (`Curriculum:Retrieval:SimilarityDistanceFloor`, default 0.4 tuned to placeholder hash geometry), and documented the flip-to-live runbook. **What's ready:** (1) Parity-guard: absent `Curriculum:Embedding:BaseUrl` → `RagContextProvider.IsAvailable = null` (graceful dormancy), `BaseUrl` present + `ModelVersion = null/empty` → guard throws before any retrieval (never auto-fallback, never mix seeds + reals). (2) **Re-embed job:** Hangfire `ReEmbedCurriculumJob` runs `TriggerReEmbedCommand`, scans `chunk_embeddings_bge_m3` for `IsActive=false` rows (the seeded placeholders), calls `BgeM3EmbeddingProvider.EmbedAsync` in 50-row batches, retries TransientError, breaks on ProgressError (no infinite retry), posts to Curriculum schema. (3) **Endpoint:** `POST /api/Admin/Curriculum/ReEmbed` (`[Authorize(policy="Curriculum.ReEmbed")]`, AdminOnly) accepts optional `batchSize/skipCount`, queues `ReEmbedCurriculumJob` via Hangfire, returns job-id. (4) **Config-driven floor:** `Curriculum:Retrieval:SimilarityDistanceFloor` gates `RetrieveChunksQuery` (default 0.4). Tests: Curriculum integration 17/17 (6 new ReEmbed tests: job logic, progress guard, endpoint auth, batch pagination). **Flip-to-live runbook:** (1) Devops provisions BGE-M3 TEI on Hetzner [external], records base URL; (2) Set env `Curriculum__Embedding__BaseUrl` + `__ModelVersion` + `__AuthToken` (API key); (3) `POST /api/Admin/Curriculum/ReEmbed`, watch Hangfire dashboard until `chunk_embeddings_bge_m3.IsActive=1` rows = seed count; (4) Re-calibrate `Curriculum:Retrieval:SimilarityDistanceFloor` (0.4 = placeholder tuning; start ~0.3 for real BGE-M3, adjust per live eval); (5) Set `AiHelper:ContextProvider="Rag"` to activate. **Deferred:** TEI server provisioning (P3-07-BE-0, devops), live grounding + Claude API keys, P3-07 Part A offline-gen (BL stories). **Wave context:** part of `feat/phase4-ai-runtime` (Clusters B + C still in same wave PR). **Reviewer PASS, build clean, 17/17 integration + 6 new tests GREEN.**
> 2026-06-15: **Parent-web Arabic-RTL pixel-parity polish (merged to main via PR #137; this entry follow-up).** Visual pass against the design-system `preview/` cards (esp. `ar-child-card.html`). **LOAD-BEARING ROOT CAUSE (read before touching any RTL row):** the parent surface renders inside react-native-web Views that carry an **ambient CSS `direction: rtl`** (RNW `LocaleContext`/`writingDirection`) — so an inline `flexDirection: 'row-reverse'` **double-flips back to LTR**. The fix pattern now used across these components: set an explicit **`dir={isRtl?'rtl':'ltr'}`** on the row/container + keep **natural child order** + plain `flexDirection="row"`, letting the browser do the single flip (exactly like the `dir="rtl"` preview HTML). Tamagui forwards `dir` to the DOM (verified). Do NOT reintroduce `row-reverse` on these. **Components touched (`packages/ui`):** `ChildDashboardCard` (dir-based RTL; avatar right, KPI/mastery/footer flow; edit-button+active grouped per design = bordered 32×32 `✏️` button; avatar `card`=56px size added to `Avatar`); `Tabs` settings rail (dir-based; icon right, label hugs it); `KPIStatCard` (tile content alignment + icon/value order); `MasteryBar` (progress fill now grows **right-to-left** in AR — intentionally overrides the old SKILL.md rule-6 "bars always L→R"); `TextField` (email/phone value right-aligned in RTL while keeping LTR *writing direction* so addresses stay legible). **App (`apps/student-app`):** `SettingsWeb` PanelHeader + the four settings panels (`Notifications/Security/Plan/Language`) got explicit `textAlign` (each panel has its OWN duplicated `PanelHeader` — fix all four, not one); `FocusAreasCard` confidence bars grow RTL; `AddChildModal` RTL title/subtitle/grade/app-language labels + **password `autoComplete="new-password"`** (email stays `off`) to stop Chrome autofilling the parent's saved creds into the child form. i18n: AR `parent.myChildren.statLevelShort` → `"Lv"`. **PR #137 commits:** `287d955` (code) + `5d42349` (all pending `design-system/` source+artifacts — committed wholesale per lead: previews, ui_kits, bundle/manifest/thumbnail, screenshots, uploads; no `.gitignore` change). **NOT verified:** no automated Expo-web RTL smoke (manual browser check only; Playwright sys-libs unavailable in this env). **`feat/parent-web-rtl-polish` merged earlier** (PRs #131+#132); #137 was the stacked follow-up.
> 2026-06-14: **P7-04 BUGFIX — Admin question CorrectAnswer jsonb encode/decode (Commit pending on `fix/P7-04-question-correctanswer`).** HIGH prod bug: MCQ/FillInBlank admin question create → HTTP 500; GET→Edit round-trip → 422/double-encode. Root cause: `CorrectAnswer` is a `jsonb` column, but the write handlers persisted a bare scalar (string/array); seeder was correct (scalar→JSON). **Fix:** (a) WRITE: `AddQuestionCommandHandler` + `EditQuestionCommandHandler` now call `JsonSerializer.Serialize` on non-Matching scalar types (Matching passes through unchanged), making the write match the seeder (and all readers). (b) READ: `QuizProfile.AdminQuestionDto` decodes non-Matching `CorrectAnswer` via `DecodeJsonScalar` (mirrors `AnswerComparator.NormalizeJsonScalar`), ensuring GET→Edit is a symmetric single-encode round-trip. All writers (seeder, handlers, P7-05 RollbackToVersion which round-trips stored form) are now consistent. **No migration:** jsonb column unchanged; existing data (if any) will be double-encoded on next write. **Tests:** Authoring 21/21 + QuestionsAdmin 52/52 + Learning unit 311/311 all green. **Reviewer PASS** (no mandatory security gate for this story). **Non-blocking nit:** `DecodeJsonScalar` currently duplicates `NormalizeJsonScalar` across the Application/Domain boundary — consolidate if a shared scalar-codec seam is later introduced.
> 2026-06-14: **🔵 PHASE-4 (AI TUTOR) — WAVE 2 MERGED to main (PR #130).** All 6 Wave-2 backend stories built→gated→merged: **P3-08** (adaptivity engine + `IAdaptivityService`), **P3-10** (spaced-repetition + `SR-Sweep` Hangfire job + `/Reviews/Due`), **P3-13** (behavioral `StudentLearningProfile` + recompute job — **mandatory child-privacy gate PASS**), **P3-02** (AI Safety Layer / `ISafetyLayer` facade = the only AI-content exit + `ai.SafetyEvents` + new `AiDbContext` — **mandatory safety gate PASS**), **P3-03** (`PromptBuilder` 4-subject ar/en + anti-injection tone frame + Shared.Contracts seams — **mandatory gate PASS**), **P3-07** (RAG / new `Curriculum` module — see the detailed P3-07 entry below; ⚠️ embeddings are PLACEHOLDERS, semantic RAG DORMANT until **P3-07-BE-0** TEI endpoint + re-embed). Integrated build green; Curriculum integration 11/11 vs real pgvector. **Wave 1 (P3-01 AI Gateway + P3-09 mastery) merged earlier via PR #126.** **Phase-4 backend now 8/12 done** (P3-01/02/03/07/08/09/10/13). **NEXT = Wave 3:** P3-04 (Explain — wires `ISafetyLayer` + `IPromptBuilder` + `SeededCorpusContextProvider`/RAG) + P3-11 (Adaptive quiz — consumes `IAdaptivityService`); then Wave 4 = P3-05 (Hints) + P3-06 (Grounded questions). **Pre-P3-04 runtime deps:** real Claude/OpenAI keys (`Ai__Providers__*__ApiKey`) via secrets; the per-task <4s NFR-1 timeout is owned by P3-04 (not P3-01). **Cross-phase:** P3-01+P3-02 unblock P7-11; P3-02 unblocks the P7-09 producer (P7-10 still blocked on P5-03). **CLAUDE.md rule #3 ("no UoW") is stale for new modules** — Learning + new modules use `UnitOfWorkBehavior` per ADR-0001 (the behavior IS the explicit transaction).
> 2026-06-13: **Parent-dashboard + auth Arabic-RTL alignment / rounded-corner polish + functional fixes (branch `fix/frontend-rtl-alignment-polish`, off main).** Cross-cutting RTL + design pass from a QC findings list across Login, My-Children, Overview, Settings, layout/Sidebar, and Register. **Shared `packages/ui`:** `TextField` — direction-aware label/error `textAlign`, height 52→48, new `forceValueLtr` + `autoComplete`/`autoCorrect` props (email/phone values render LTR even in AR; auto-forced for `email-address`/`autoComplete='email'|'tel'`), reveal toggle moved into label row with localized `showLabel`/`hideLabel` (callers pass `auth.login.showPassword/hidePassword`); `Button` web primary glow + disabled `$cardSoft`; `ChildCard` RTL rows + removed active accent stripe; `CheckboxField` green check (`$secondary`/`$fgInverse`). (`Select`/`Tabs`/`MasteryBar`/radius tokens were already correct.) **CHILDREN/`AddChildModal`:** added **edit mode** (`childId`+`initialValues` → `useUpdateChild` PUT /Update-Child, re-seeds on reopen, hides email/password/photo per update contract); Country select (backend-backed via `AddChildCommand`/`UpdateChildCommand.country`); `autoComplete="off"` on email+password; the **Arabic "Add Child" button bug** was `flexWrap:"wrap"` + `row-reverse` pushing the button to an un-clickable wrapped position → fixed to `flexWrap:"nowrap"` + `flex:1` label. **OVERVIEW/SETTINGS/LAYOUT:** direction-aware `textAlign` on KPI/Recommendations/FocusAreas/header; **sidebar ChildSwitcher made functional** (was a static card that just `router.push`'d — now wired to `useActiveChildStore` like the header one) and the **duplicate top-nav ChildSwitcher removed** from the wide shell header (narrow/mobile header keeps it); **Logout added to sidebar** via `useSignOutAction` (new i18n `parent.nav.logout` en+ar). **REGISTER:** add-child is now an **inline 2-step wizard** (state-based `step` in `register.tsx`, reuses `AddChildModal`; `RegisterForm` gained `onSuccess` instead of routing to `/(onboarding)/add-child`, which is left intact for `LinkedChildrenPanel`); guardian banner border→new `$purpleBorder` token; FormScaffold scroll indicator hidden. **NEW BACKEND ENDPOINT (Settings language Save was 403):** the only language-write endpoint (`UserManagementController.UpdateUserLanguage`) is **AdminOnly** → parents got 403 mislabeled "No internet". Added self-scoped **`PUT /api/Users/Account/Language`** (`[Authorize]`, `UpdateMyPreferredLanguageCommand` — single `userPreferredLanguage`, user resolved from JWT, no IDOR/mass-assignment) in Identity `AccountController`, mirroring `UpdateMyProfile`. **api-client regenerated** by hand-editing the committed `packages/api-client/swagger.json` (new path + `UpdateMyPreferredLanguageCommand` schema) then `pnpm --filter @learnexia/api-client gen:api` (no running backend needed — `refresh:swagger` needs a live backend, `gen:api` works off the snapshot); generated method = `client.language(...)`; `useUpdateUserLanguage` rewired to it via `unwrapEnvelope` (same hook name/signature). **Gates:** reviewer **PASS** (0 blockers; 2 off-token colors + nits fixed: `$purpleBorder`/`$borderSubtle` tokens, restored `t('common.appName')`, localized password toggles at 5 call sites); **dotnet build + full workspace type-check/lint = green (18/18)**. **NOT verified live:** no Expo-web visual RTL smoke this pass (typecheck/lint/review only) — recommend a `frontend-e2e-tester` RTL run before merge. **Known minor:** `(onboarding)/_components/EditChildSheet.tsx` is now orphaned (referenced only in comments as a pattern template) — harmless dead code, left in place. **To fully ship the language Save:** when a live backend is next available, run `refresh:swagger` + `gen:api` to reconcile the hand-edited snapshot, and have `api-tester` cover the new endpoint. **PR #128** (`gh pr view 128`). **Follow-up commit `5e3a057`: removed the Country field from CHILD add/edit entirely** (product rule — children have no country): dropped from `AddChildModal`, onboarding `AddChildForm`, the shared `addChildSchema`, and the `AddChildCommand`/`UpdateChildCommand` payloads + edit pre-fill; **deleted the now-orphaned `(onboarding)/_components/EditChildSheet.tsx`**. Backend still accepts `country` as optional (unchanged) — the client just stops sending it for children; parent-registration country is untouched. (Unused `parent.addChildModal`/`onboarding.addChild` country i18n keys left in place — harmless.) type-check + lint green. **Untracked artifact:** an agent auto-generated `docs/dev/FRONTEND_DEV_MANUAL.md` (377 lines, a React-dev onboarding map for this monorepo) — left UNCOMMITTED/unvetted in the working tree; keep+vet or delete per lead.
> 2026-06-14: **P3-07 COMPLETE — `Curriculum` module + RAG retrieval system BUILT.** New `Curriculum` module (4 projects: Domain/Application/Infrastructure/Api) fully implements P3-07-BE per the locked AI architecture. **Schema:** `CurriculumVersion` (SubjectId, Language, Status={Draft/Active/Archived}, Name, audit/CreatedAt), `CurriculumChunk` (SkillKey, CurriculumVersionId, nullable ProvenanceRef, hierarchy refs), `chunk_embeddings_bge_m3` table (separate, never inline; `Provider/Model/ModelVersion/Dimension/Vector vector(1024) HNSW cosine/IsActive`). **InitialCurriculum migration** creates the pgvector extension + 3-table schema + HNSW cosine index. **Embedding provider:** `BgeM3EmbeddingProvider` (calls TEI endpoint) + `DeterministicEmbedding` (seed-only, placeholder `ModelVersion="seed-placeholder-v0"`); seeder `CurriculumChunkSeeder` plants deterministic embeddings for testing + demo (parity stamp enables future swap to real BGE-M3 without schema change). **Retrieval:** `RetrieveChunksQuery` handler (Infrastructure, needs DbContext+pgvector) joins embedding↔chunk↔version, filters CurriculumVersion.Status=Active + grade + subject + skill + similarity floor, returns empty on no hits (never hallucinate). **Context provider:** `RagContextProvider` (Infrastructure) implements BOTH `ILearningContextProvider` (runtime student-centric) AND `ICurriculumContextQuery` (the intended seam); wired via `AiHelper:ContextProvider="Rag"` config (default="Seeded"). **Cross-module MediatR:** handler lives in Curriculum.Infrastructure (isolated, has DbContext); Host scans both Curriculum.Application + Curriculum.Infrastructure AssemblyReferences for registration. **Integration tests:** 11/11 pass vs real pgvector (P3_07_RetrievalEndpoint_Tests: endpoint semantics; RagContextProviderTests: provider behavior). **CRITICAL GATE:** embeddings are PLACEHOLDERS — semantic RAG is NOT live until (1) P3-07-BE-0 (devops: provision + deploy BGE-M3 TEI endpoint on Hetzner) is done AND (2) all rows in `chunk_embeddings_bge_m3` are re-embedded with real BGE-M3 vectors (the ModelVersion parity stamp makes the swap detectable; current default `Curriculum:Retrieval:SimilarityDistanceFloor` is tuned for placeholders and must be re-tuned for real). **Activation steps:** (1) Stand up BGE-M3 TEI on Hetzner per P3-07-BE-0, record base URL. (2) Set `Curriculum:Embedding:BaseUrl` + `Model` + `ModelVersion` in config. (3) Backfill embeddings via offline job. (4) Re-tune similarity floor. **Reviewer PASS** (0 blocking, build green, 11/11 integration vs pgvector green); **Light-security PASS** (no Critical/High; API key missing by design — kept in secrets, not appsettings).
> 2026-06-13: **🔒 FINAL LOCKED AI ARCHITECTURE — Phase-4 AI Tutor + Curriculum Intelligence (docs only; nothing built yet). Canonical record: `docs/decisions-needed-ai-phase.md` → "FINAL LOCKED AI ARCHITECTURE (2026-06-13)".** Lead ratified the full AI retrieval/embedding/cache stack and aligned all the specs; this is the spec the P3-07 + BL agents implement against. **Embeddings:** self-host **BGE-M3, `vector(1024)`**, served at runtime over a **synchronous TEI (Text-Embeddings-Inference) endpoint on a Hetzner dedicated host** (64 GB RAM/NVMe, CPU-only at MVP → GPU when query latency or large ingestion batches demand), behind **`IEmbeddingProvider`** (`BgeM3EmbeddingProvider`; `IEmbeddingService` retired). **Seed-time ↔ runtime model/version PARITY is required** (stamped on `chunk_embeddings_bge_m3.Provider/Model/ModelVersion`; provider fails-fast on mismatch) — else cosine search is invalid. **Storage:** vectors live in a **separate `chunk_embeddings_bge_m3` table**, never inline on `CurriculumChunk`; future model = new per-dimension table + `IsActive` flip. **Schema ownership:** **P3-07 creates the *minimal* `CurriculumChunk` + `CurriculumVersion` + `chunk_embeddings_bge_m3` slice; BL-04 EXTENDS (ALTER/ensure-exists), BL-05 writes rows — neither re-creates** (guards added to BL-04-BE/BL-05-BE/curriculum-system-of-record §4). **Retrieval (P3-07):** JOIN embedding⋈chunk⋈version, filter `CurriculumVersion.Status = Active` + grade + subject + skill + similarity floor; empty ⇒ "no context" (never hallucinate). **Two retrieval seams kept (NOT merged):** `ILearningContextProvider`/`RagContextProvider` (runtime, student-centric) + `IChunkRetrievalContract` (P3-06 offline batch, student-less). **.NET↔Python seam:** DB-outbox `PipelineJobs` + Python poller (NOT MediatR, NOT a broker). **Cache (per `docs/briefs/ai-cost-routing.md`):** two-tier — durable reviewable **`ai.AiResponseCache`** (Postgres; col `Response`; key `SkillKey`+`CurriculumVersion`; `ReviewStatus ∈ {PendingReview,Approved,Rejected}`; auto-approve on safety-passed AND confidence ≥ 0.85) + **Redis read-through** holding Approved-only (Redis is speed, never source of truth — reload from Postgres on loss). This **unifies/supersedes** the old separate `ConceptExplanationCache`/`HintCache`. **Charging** (Redis hit / Postgres hit / fresh gen = charge; error/refusal = free) is a *rule*; the **credit ledger + charging seam are Phase 10** (P10-01 ledger, P10-03 spend-on-ai). **AI scope:** closed set of **4 intents only** (Hint, WhyWrong, Explain-concept, Generate-practice) — no open chat. **Python pipeline LOCKED same-repo at `python/curriculum_intelligence/`** (`app/ parsers/ embeddings/ kg/ workers/`); `BL-02-PY-1` is the shared foundation BL-03-PY/BL-05-PY build on (BL stories + `-PY` task tables already exist: BL-02=9, BL-03=5, BL-05=8). **Task changes made this session:** P3-07-BE.md rewritten to the locked design + **new `P3-07-BE-0` (provision/deploy the BGE-M3 TEI endpoint — Hetzner, secrets, health, compose/CI; blocks BE-6)**; BL-02-BE location locked; P3-07 plan/brief + ai-helper-mvp re-aligned. **DEVOPS TODO before P3-07 runs:** stand up the BGE-M3 TEI endpoint + record base URL + `Model`/`ModelVersion` in `EmbeddingSettings` (P3-07-BE-0). **No new user stories needed** — BL stories cover the pipeline; gaps were task-level. Earlier AI status below: Wave 1 (P3-01 gateway + P3-09 mastery) merged PR #126; P3-08/P3-10 merged locally; P3-13 in progress on this branch.
> 2026-06-13: **Onboarding add-child → MODAL (branch `feat/onboarding-add-child-modal`, off main).** The parent register/onboarding add-child step (`app/(onboarding)/add-child.tsx`) was converted from the old inline `AddChildForm` + in-memory draft-list + batch "submit all" into the **design-system Add-Child MODAL** flow: a My-Children-style screen (heading + `useMyChildren` list rendered with `@learnexia/ui` `ChildCard` + a dashed `AddChildCard` "Add a child" tile) that opens the existing `AddChildModal`; each modal confirm **creates the child immediately** (`useAddChild` → POST /api/Parent/Add-Child) and it appears in the list; a **Continue** button gated on `child count >= 1` → `/(onboarding)/complete`. **Key moves:** (1) `AddChildModal` **moved to shared `apps/student-app/src/components/AddChildModal.tsx`** (old `(parent)/_components/AddChildModal.tsx` shim deleted — zero importers); both dashboard + onboarding import the shared one. (2) `useAddChild.onSuccess` now invalidates **both** `myChildren` AND `auth.me()` (so the `hasChildren` guard isn't stale post-add). (3) Dashboard `MyChildrenWeb` "+ Add Child" button + dashed card now **open the modal** via `activeChildStore.openAddChild()` instead of `router.push('/(onboarding)/add-child')` (required — onboarding is no longer a page form). `AddChildForm.tsx`/`EditChildSheet.tsx` **kept** (dashboard's `MyChildrenWeb` still uses `EditChildSheet`); `childListTypes.ts` deleted. i18n: reused `parent.addChildModal.*`, added `onboarding.addChild.continue/listLabel/emptyHint` + reworded title/subtitle (en+ar). **Gates:** reviewer **PASS** (0 blocking); rewritten `tests/e2e/specs/P1-03-FE.spec.ts` **21/21 pass** (verified empty-state→dashed-tile→modal→list→Continue, en+ar/RTL); type-check/lint/build green (student-app/shared/api-client). **Known (backlog, not blockers):** new-child **photo is preview-only** (no avatar endpoint on `AddChildCommand` — `photoFile` held behind an eslint-disable); grade tiles + flag tiles + Select options all share `accessibilityRole="radio"` (minor a11y/selector note); `LinkedChildrenPanel` still has a `router.push('/(onboarding)/add-child')` fallback only when its `onAddChild` prop is absent.
> 2026-06-12: **Phase 1/2/3 carryover backlog started (branch `feat/p1-p2-p3-carryover`, off main; brief `docs/dev/CARRYOVER-P1-P2-P3-LEAD-BRIEF.md`).** Three gating decisions recorded (lead/user, 2026-06-12): **(1) Matching answer payload = `{ pairs: [{leftId,rightId}], attemptOrder, timeMs }`** — comparator does order-independent pair-set equality; demo seed must carry **all 4 types (MCQ/TrueFalse/FillBlank/Matching)**; FE gets a real tap/drag pairing UI. **(2) Attempt-history (P2-09 / G5) = IN SCOPE now** (not deferred to Phase 5). **(3) Marketing landing ar/RTL (CO-FE-4) = ALREADY EXISTS on main** (`apps/marketing-site/app/[locale]` /en+/ar, `middleware.ts`, `lib/copy.ts`, `LanguageSwitcher`) → **dropped from the backlog** (review-only, no build). Waves: A = Phase-1/2 FE polish, B = Phase-3 gamification FE (the bulk), C = Matching full-stack, D = E2E close-out. Brief `docs/briefs/p1-p2-p3-carryover.md`, plan `docs/plans/p1-p2-p3-carryover.md`.
> 2026-06-12: **Carryover BATCH 1 COMPLETE + gated (on `feat/p1-p2-p3-carryover`).** Shipped: **A1** admin sign-in lockout/deactivated messaging; **A2** student/parent login lockout/deactivated messaging (removed the anti-enumeration `notFound` branch); **A3** register Turnstile CAPTCHA (web-only this wave); **CO-BE-1/2/3** Matching backend — `AnswerComparator` order-independent pair-set equality, demo seeder now seeds **all 4 question types** on the two Grade-1 Math root lessons, payload `{pairs:[{leftId,rightId}],attemptOrder,timeMs}` (no schema change); **CO-BE-4** 12 Matching integration tests; **5 design specs** under `design-system/ui_kits/` (B0-nav TabBar, P4-08 gamification+motion, A4 Reports, A5 attempt-history both-surfaces, C5 tap-to-pair). Gates: reviewer **PASS** (0 blocking), security-auditor **PASS** (no Critical/High), api-tester **12/12 + P2 suite 427/0 green**. **Carry-forward (non-blocking):** (a) FE classifies auth errors on backend **localized message text** (no machine code field) — fail-safe (degrades to generic msg) but brittle; consider a stable `BaseResponse` error code later. (b) **Ops: pair `Captcha__Enabled` (backend) with `EXPO_PUBLIC_TURNSTILE_SITE_KEY` (FE) — flip together**, and do NOT enable CAPTCHA in prod until the **native** register path sends a token (web-only today). (c) admin `next.config.ts` gained a font asset-loader rule (fixes a pre-existing P8 `.ttf`-import build break). Next: Batch 2 (P4-08 motion re-cut + B0 TabBar shell + A4 Reports + A5 attempt-history + C5 MatchingPanel).
> 2026-06-13: **✅ P1/2/3 CARRYOVER COMPLETE — Wave D done, PR opened.** All four waves shipped on `feat/p1-p2-p3-carryover`: Phase-1/2 FE polish (auth messaging + CAPTCHA + Reports), full Phase-3 gamification FE (bottom TabBar + xp/streak/hearts/events/badges/missions/league screens + celebrations), Matching full-stack, attempt-history (both surfaces), parent↔child attempts authz. **Wave D:** Playwright e2e `tests/e2e/specs/carryover-d1.spec.ts` ran **39 pass / 3 intentional skips / 0 fail** (skips: admin-app A1 out-of-harness, Turnstile-needs-site-key, celebration-popup non-deterministic); stale trackers corrected (PROGRESS.md Phase-3 FE all ✅, Matching ✅, route/header de-stale); final reviewer PASS. **CARRY-FORWARD bug (predates carryover, NOT fixed here):** quiz attempt *resume* replays from `currentIndex:0` → backend HTTP 424 "already answered" → `[lessonId].tsx` `onError` silently reverts → quiz stuck on Q1 with no user feedback. E2E works around it with fresh-account-per-test. File a fix. Minor: `reports-attempts-panel` testID rendered below the test viewport — confirm it shows for a parent after their child completes a lesson. **(Historical resume notes below — Wave D is now done.)** Commits (newest→oldest): `f2eadb1` authz fix · `7e574d6` 3c+fixes · `2fde6e9` 3b · `72f7eef` 3a · `5000d73` 2d · `f160f0b` 2c/2e · `6822739` 2a/2b · `0b2c8af` Batch 1 · `a787fda` brief. **Wave D did (in order):** (1) **D1 / CO-FE-6** — implement + RUN Playwright e2e under `tests/e2e/` covering: auth locked/deactivated/invalid messaging (A1/A2), register CAPTCHA gating (A3), Matching tap-to-pair quiz happy+wrong+heart-loss (C5, demo seed = G1 Math root lesson ar tree, SequenceOrder 3), gamification TabBar nav + xp/streak/hearts/events/attempts push screens + badge/league/missions + celebration popup, attempt-history child "My activity" + parent Reports panel (authz now works → linked parent gets data), parent Reports KPIs/range/Send-Report toast — ALL ar(RTL)+en, happy+error; needs the live stack (backend :5080 + Expo web :8081 per the "Testing — E2E (Playwright)" section). (2) **Stale-tracker doc fixes**: `tasks/PROGRESS.md` flip P4-02..08/P4-11 FE + A1/A2/A3/A4/A5/CO-BE Matching to done; correct stale task-file headers (P4-06-FE "backend in progress", P1-11-FE FE-9, P4-07-FE `League/Me`→`Leagues/Me`, P4-xx-FE `(student)`→`(child)` route group). (3) **Final reviewer gate** on e2e + trackers. (4) **Update HANDOFF + open the PR** (`gh pr create --base main`; one PR for the whole carryover). **Models:** subagents use their `.claude/agents/*` default models (e2e-tester/reviewer=sonnet, committer=haiku) — NO `model` override, Fault/Fable discontinued (see memory [[agent-model-dispatch]]). **Open carry-forwards (not Wave-D blockers):** packages/ui kit debt (Hearts/XPBar/StreakFlame/Badge i18n+size+reduce-motion+onPress; Button purple variant; lift formatNumber; delete unused TabStubScreen); backend stable operationIds for ~48 P7 admin paths (NSwag); G2 JWT SecurityStamp (P6-06); honest-empty backend gaps (XP-to-next, streak history/longestStreak, heart-refill countdown, parent XP/mastery aggregates P5-05, lessonName on AttemptListItemDto, attempts endpoint unpaged).
> 2026-06-12: **Parent↔child attempts authz FIXED + committed (resolves the Batch-2 fail-closed carry-forward).** The stray worktree WIP turned out to be exactly the fix: `GetStudentAttemptsQueryHandler` now authorizes owning-student OR **linked parent** via the `IParentChildQuery.IsParentOfChildAsync` Shared.Contracts seam (impl `ParentChildQuery`, `AddScoped` in Parent.Infrastructure — module isolation intact, no Parent project ref), returns **403** (not 401) for authenticated-not-authorized, anti-enumeration (identical generic `AttemptsAccessForbidden` for unknown-id vs not-linked), no `correctAnswer` leak. P2-08 tests rewritten (BE-TC-40 + new BE-TC-40b linked-parent→200; C14 IDOR→403). **Verified: build 0 errors, P2-08 integration 50/50 pass, security-auditor PASS (0 blocking).** So the **parent Reports attempts panel is now functional** (no longer fail-closed). Remaining tie-in: the G2 JWT-SecurityStamp gap (access token ~30 min TTL) is still separate/pre-existing → P6-06.
> 2026-06-12: **Carryover BATCH 3 COMPLETE — Phase-3 gamification FE (the bulk) — on `feat/p1-p2-p3-carryover`. PAUSED here for lead review before the Wave-D e2e.** Built all 7 gamification screens off the merged backend + typed hooks: **3a** `(child)/xp.tsx` (level + XP-to-next bar, level-up RewardPopup), `streak.tsx` (animated flame + day 3/7/14/30 milestones + earned-only freeze), `hearts.tsx` (hearts + Practice Mode, soft refill); **3b** `badges.tsx` (earned/locked gallery via `useMyBadges`), `missions.tsx` (daily/weekly progress via `useMyMissions`, excludes CHALLENGE_*), `league.tsx` (full standings via `useMyLeague`, promo/demote zones, you-row autoscroll, week-end countdown), `events.tsx` (earned-only freeze + timed events + **weekly CHALLENGE_* cards from `useMyMissions`**); **3c (B-int)** registered href:null push routes (xp/streak/hearts/events; attempts already by 2d) + wired dashboard tap-throughs + `useDashboardDiff` celebration plumbing (level-up/badge/streak/mission, priority-coalesced to one popup/refresh, reduced-motion gated, cold-start safe) + TabBar clearance. Reviewer **PASS** (after a fix loop: weekly-challenge visibility + an xp.tsx stale-diff re-fire guard). type-check + lint green across student-app + shared; `expo export` web smoke passed. **Models: subagents now use their agent-definition models (analyzer/designer=opus, frontend/reviewer/etc=sonnet) — Fable override discontinued mid-Batch-3 per lead.** **Debts to clear in Wave D / follow-up:** (1) **Gate-3 frontend-e2e smoke was SKIPPED** (paused before live e2e) → D1 must cover gamification nav + each push route + celebration-on-refresh + ar/RTL. (2) **Pre-existing STRAY backend edits sit uncommitted in the worktree** (`GetStudentAttempts*`, `StudentsController.cs`, `SharedResources*.resx`, `SharedResourcesKey.cs`, P2-08 tests) — NOT part of any carryover batch; needs disposition (they look like a parent↔child attempts-authz WIP — the same capability the Batch-2 security carry-forward needs; confirm before committing/discarding). (3) `packages/ui` kit debt (Hearts/XPBar/StreakFlame/Badge: hardcoded English + size caps + no reduce-motion + no onPress → screens recomposed locally), `Button variant="purple"` inline override, local `formatNumber` dup (lift to packages/shared), unused `(child)/_components/TabStubScreen.tsx` (3b replaced all stubs → safe to delete), RewardPopup entrance spring not reduce-motion-gated internally, cross-screen celebration double-fire (xp/missions screens + Home). (4) honest-empty backend gaps logged: XP-to-next-level, per-day streak history + longestStreak, heart-refill countdown, parent XP/mastery aggregates (P5-05), `lessonName` on `AttemptListItemDto`, attempts endpoint unpaged.
> 2026-06-12: **Carryover BATCH 2 COMPLETE (on `feat/p1-p2-p3-carryover`).** 2a motion salvage + api-client regen; 2b child bottom **TabBar** shell (Home/Missions/League/Badges, href:null push screens auto-hide the bar); 2c parent **Reports** page (chart-less KPIs/mastery/range/Send-Report toast stub) + **RecentAttemptsPanel** (A5 parent) + shared `AttemptRow` + `useStudentAttempts`; 2d child **"My activity"** attempts screen (self-id via `useMe`); 2e real **tap-to-pair MatchingPanel** emitting `{pairs,attemptOrder,timeMs}`. Reviewer PASS. **⚠️ RISK-ACCEPTED security carry-forward (High, fail-closed): parent attempt-history authz.** `GetStudentAttemptsQueryHandler` authorizes ONLY the owning student (`studentId == JWT userId`); parent↔child scoping was deferred (Phase 5/7). So the **parent Reports attempts panel is non-functional/fail-closed** (renders generic empty/error) until a backend follow-up adds a proper parent↔child link check (via the `Shared.Contracts` parent-child seam, e.g. `IParentChildQuery`). **MUST NOT** be fixed with a role-only bypass (→ Critical IDOR). Recommended in that follow-up: return 403 (not 401) for authenticated-not-authorized so the FE transport doesn't attempt a token refresh; update BE-TC-40. Child "My activity" surface + Matching UI audited clean (no IDOR). Other carry-forwards: parent XP/mastery aggregates honest-empty (P5-05); `lessonName` missing from `AttemptListItemDto` ("Lesson {n}" fallback); attempts endpoint unpaged (client slices); child attempts screen auto-hides the TabBar (spec said keep visible — flip one `TAB_BAR_VISIBLE_ROUTES` entry if desired); B-int (3c) must wire the Home→"My activity" entry link (the `attempts` href:null route is already registered).
> 2026-06-07: **E2E test stage added — `frontend-e2e-tester` agent + `tests/e2e/` Playwright harness (PR #99, branch `chore/e2e-playwright-harness`). See "Testing — E2E (Playwright)" directly below.**
> 2026-06-08: **Phase-1 QC + E2E pass complete (backend PR #100 + frontend PR #105) — ~173 FE e2e pass / 0 fail, several real bugs found + fixed (route guards, locale, cache, Link-Child 409). See "QC + E2E test pass — Phase 1 (report summary)" below.**
> 2026-06-07: **QC test-design + api-tester pass over all Phase-1 backend stories (branch `qc/phase-1-backend`) — ~329 designed cases, ~520 integration tests run; surfaced one HIGH security hole + several robustness/contract defects. See "QC — Phase-1 backend test pass + defects" directly below.**
> 2026-06-09: **Phase-2 BACKEND QC pass COMPLETE + GREEN (`qc/phase-2-backend-continue`): all 11 stories implemented+run, full P2 suite 415 pass/0 fail; seeder Published-fix + 3 defects fixed (`dc30d88`). ⚠️ Full-suite run surfaced ~127 RED P7 integration tests (authored compile-only, never run) — separate next QC target. FE QC (PR #108) not implemented. See "QC — Phase-2 backend test pass (COMPLETE)" below.**
> 2026-06-08/09: **Phase 7 Admin Console — CURRICULUM (P7-01..05) + USER/ACCOUNT (P7-06..08) + AUDIT/MODERATION (P7-12) + GAMIFICATION OVERRIDES (P7-13) BACKEND COMPLETE on `feat/phase-7-backend` (one shared branch → wave PR #106). Plus a pre-req auth hotfix PR #104 (merge FIRST). 10 of 13 P7 backend stories done — ALL the buildable ones. Remaining: P7-09/10/11 (BLOCKED on unbuilt upstream phases P3-02/BL-01, P5-03, P3-01/P6-02); all P7 FE not started. See the sections directly below.**
> 2026-06-11: **Phase 9 — Notifications scoped (branch `feat/phase-9-notifications`, off main).** New user-story phase `user-stories/Phase-9-Notifications/` (P9-01..09) + FE/BE task breakdowns (`tasks/Frontend/student-app/Phase-9-Notifications/`, `tasks/Backend/Phase-9-Notifications/`) + README updates. **Scope-definition only — no implementation.** Key grounding finding: the P4-09 backend is MORE complete than the gap analysis implied — handlers + Arabic templates already exist for streak-danger (`StreakAtRisk`), comeback (`LapseWinBack`) and achievement (`Achievement:BADGE_EARNED`), plus templates for level-up/league/hearts; integration events `StudentLeveledUp`/`LeagueTierChanged`/`StreakFreezeConsumed`/`TimedEventStarted|Ended` are PUBLISHED but have **no handlers** yet. So Phase 9 = (FE) P9-01 expo push+token registration, P9-02 deep-link routing+web fallback, P9-03 in-app inbox, P9-04 parent per-child controls; (BE) P9-05 wire the already-emitted events, P9-06 new categories (streak milestones/weekly-challenge/weekly-recap), P9-07 cross-category arbitration + global daily push budget ("many types, few sends" — today only per-category caps exist in `ReengagementEvaluator`), P9-08 comeback escalation ladder, P9-09 spaced-repetition reminder (**BLOCKED on P3-10**), P9-10 notification localization (send in user's selected language — re-engagement nudges already localize via `PreferredLanguage`; welcome notification is **hardcoded English** + emails pass un-localized; coordinates with P6-06). All copy Arabic-first/child-safe/never-shaming; parent is consent authority. `analyzer`+`planner` before any implementation.
> 2026-06-11: **Business gap analysis (habit-forming launch readiness) → [docs/business-gap-analysis-by-fable.md](../business-gap-analysis-by-fable.md)** — docs-only; maps planned vs built engagement features and prioritizes launch gaps. Headline: gamification *backend* is done but the habit loop is broken at its 3 delivery surfaces (push end-to-end dead — no FE token registration/inbox; P4-08 screens unbuilt; Phase-5 parent loop stubbed), and payments/economy/parent-lifecycle-comms/placement-test/referral have **no stories at all**. Use its §4 sequencing when planning the next waves.
> 2026-06-11: **Parent-dashboard (web) UI/UX redesign (branch `feat/parent-dashboard-uiux`, off main).** Source of truth = in-repo `design-system/` (matches the latest Claude Design export; the `api.anthropic.com/v1/design` URLs are short-lived/expire — don't rely on them). **Shipped (all in `apps/student-app/app/(parent)/`):** (A) **shared parent web shell** in `_layout.tsx` (rule-8 approved) — sidebar + content scroll container + a **persistent EN/AR language + dark/light theme switcher** (reuses `localeStore`/`themeStore`; **added localStorage persistence to `themeStore`**) + an **active-child switcher** on EVERY page (was login-only); app-wide **brand scrollbar** via `useBrandScrollbar()` (`<style id="lx-brand-scrollbar">`); RTL nav order = **Overview before My Children**. (B) post-login redirect → `/(parent)/overview` (`useAuthRoute.ts`/`useGroupGuard.ts`). (C) Overview: added the missing **Recommendations-from-Lexi** panel **side-by-side** with Areas-to-focus (2-col). (D) Settings: profile-image upload affordance, **email read-only by design** (the `AccountProfileResponse` contract has NO email field + no change-email endpoint — it renders "—"; shows a locked-reason helper, NO edit flow), Language&Region **Save** button (persists language via `useUpdateUserLanguage`; region is UI-only — no endpoint). (E) **Add-Child MODAL** (`AddChildModal.tsx`, reuses the RN `<Modal transparent>`+`$overlay` convention — not a new pattern) with photo upload, grade plant-emoji **tiles**, 🇪🇬AR/🇺🇸EN **flag tiles**, + the backend-required password/learningLanguage/country fields. **NEW FE state:** `providers/activeChildStore.ts` (persisted active child + add-child-modal UI flag) — UI/selection only, child list stays in TanStack Query. **Verified in browser (all 8 items PASS)** with a fresh parent `demo.parent@learnexia.com` / `Demo!Pass1` + 3 children (capture spec `tests/e2e/specs/parent-final-capture.spec.ts`). **Known follow-ups (not blockers):** new-child **photo isn't persisted** (no child-avatar endpoint — preview only, graceful no-op); **region** has no endpoint; subject-mastery **percentages render Latin digits in AR** while KPIs use Eastern-Arabic (minor numeral inconsistency); the old demo `parent@demo.com` got Identity-**lockout** from failed logins (use the fresh account). Reviewer PASS; type-check/lint/build green across design-system/ui/shared/student-app. Brief `docs/briefs/parent-dashboard-uiux.md`, spec `design-system/ui_kits/parent-dashboard/parent-dashboard-uiux.md`.
> 2026-06-11: **Student-app Phase 1/2 UI/UX design-system audit + fixes (branch `chore/p1-p2-uiux-audit`, off main).** Reviewed all Phase-1/2 student-app screens against `design-system/SKILL.md` (10 core rules) + the reference screenshots; the app was already ~87% compliant (Tamagui theme correctly implements dark canvas, 5 primaries, 3 gradients, bucketed radii, soft shadows, fonts, press feedback). **Changes shipped:** (A) token-compliance — converted hardcoded hex/rgba/radii to design-system tokens in `SkillTreeNode`, `RegisterForm`, `RegisterFeaturePanel`, `register.tsx`, `AddChildCard`, `TextField`, `AnswerFeedbackStrip`, `ContinueCard`; added tokens `successSoftStrong`(0.30)/`dangerSoftStrong`(0.55) + shadow glows `primaryGlowStrong`/`successGlow`/`dangerGlow` to `packages/design-system` (the *Soft 0.18 tokens flattened some original alphas — the *Strong variants restore them). (B) Completed the **light-theme palette** in `packages/design-system/src/themes` (derived/contrast-safe — SKILL.md has no canonical light reference; "dark by default"). (C) **App-root native RTL** via `applyNativeRtl(I18nManager)` in `_layout.tsx` (web already handled by `applyWebDirection`). (D) **Quiz visual fidelity** per `design-system/preview/components-quiz.html` + `screenshots/mobile/12-quiz.png`: `MCQOption` now shows lettered A/B/C/D key chips (radio disc removed, `accessibilityRole="radio"` kept); the quiz progress dots → "Question X of Y" + % + gradient bar. **Decisions/notes:** progress bar is **pinned LTR in all locales** (SKILL.md rule 4.6); quiz **subject·topic eyebrow SKIPPED** — `SingleLessonResponse` DTO lacks `subjectName`/`topicName` (backend follow-up needed to expose them). The reference screenshots show Phase-3/4 features (Daily Quests/League/gems/bottom TabBar/mascot) that are **intentionally not built yet** — not regressions. Capture harness added: `tests/e2e/specs/p12-screenshots.spec.ts` + `playwright.screenshots.config.ts` (needs Node 20 + `EXPO_OFFLINE=1` + backend on :5080; see manifest). Reviewer caught+fixed token-alpha regressions before commit. type-check/lint/build green across design-system/ui/student-app.
> 2026-06-11: **Marketing site — "For Parents" value-showcase section (branch `feat/marketing-landing-reskin`, off main w/ #112+#113).** Replaced the four standalone PR-#112 bands (BenefitsPanel/ActivityChart/AITutorBubble/ChildCardPhone — now DELETED) with ONE composed `app/_components/ParentValueSection.tsx`: centered "For Parents" header + 2-col grid (large purple Benefits panel left; stacked "Your weekly report" chart +28% / Lexi tutor bubble / Sami child card right), EN+AR, between SubjectsBand and CTABanner. Source of truth = `design-system/ui_kits/parent-dashboard/PagesPublic.jsx` lines 304–426 + `index-ar.html` + screenshot `screenshots/web/01b-landing-for-parents.png`. **Note for future leads:** PR #113 ("chore/design-system-update") did NOT visually redesign the atomic `design-system/preview/*.html` files — it only added `<!-- @dsCard -->` manifest comments + a token cleanup in `colors_and_type.css`; the only real new *design* it carried was this composed For-Parents landing section in the parent-dashboard ui_kit. Approach (a) per CLAUDE.md rule 8 (inline composed component, NOT variant-props on the old components). Chart bars pinned `direction:ltr` (do not reverse in RTL) per the AR source. Reviewer PASS; type-check/lint/clean-prod-build green (/en+/ar HTTP 200, correct dir); e2e spec `marketing-components-ar.spec.ts` rewritten for `parent-value-*` testids but NOT re-run this cycle (deferred — run it before relying on the suite). Design spec: `design-system/ui_kits/marketing/for-parents-section.md`.
> 2026-06-11: **Design-system follow-up — generated bundle/manifest now tracked + add-child modal previews (PR #116, branch `chore/design-system-bundle`, off main; merged `6d0cc68`).** Lands the artifacts created after #113 merged: `design-system/_ds_bundle.js` (270K) + `_ds_manifest.json` (32K) are now **tracked in git** (lead-requested — these are the dsCard bundle + manifest; regenerate if the atomic `preview/*.html` change), plus **add-child modal/sheet previews** (`web-add-child-modal.html`, `mobile-add-child-sheet.html` + `ar-` variants), the `parent-dashboard/AddChildModal.jsx` kit component, `student-mobile/ScreensAuth.jsx`, and README/SKILL/index doc updates. 13 files, all confined to `design-system/`. (Note: #113's first commit was merged by the lead before this follow-up commit landed, hence the separate PR.)
> 2026-06-10: **Marketing site — Arabic/RTL + 4 design-system showcase components (branch `feat/marketing-components-ar`).** `apps/marketing-site` is now bilingual: App-Router `app/[locale]/` segments (`/en`, `/ar`), `middleware.ts` redirects `/`→`/en`, root `layout.tsx` reads an `x-locale` header to set `<html lang dir>`. **No i18n library** — copy is locale-keyed in `lib/copy.ts` (`COPY.en`/`COPY.ar`, `getCopy(locale)`); existing sections take a `locale` prop; Arabic copy ported from the `design-system/preview/ar-web-*` kit (Arabic-Indic numerals in `ar`). Added components (plain React + CSS Modules, `--lx-*` tokens): `BenefitsPanel`, `ActivityChart` (inert Export CSV), `AITutorBubble` (needs `public/assets/mascot-owl.svg`, copied from `design-system/assets/`), `ChildCardPhone` (sibling reusing `PhoneMockup.module.css` frame — hero untouched). Top-nav `LanguageSwitcher` uses **plain `<a>` tags (full-document nav)** — `<Link>` soft-nav left `<html dir>` stale (real bug, fixed). **Load-bearing gotchas:** (1) the `headers()`-based locale layout requires routes stay **fully dynamic** — do NOT add `generateStaticParams` to `app/[locale]/layout.tsx` (SSG + `headers()` → `DYNAMIC_SERVER_USAGE` 500). (2) A `"prebuild": "rm -rf .next"` step was added because `next start` over a stale dev `.next` crashes (`Cannot find module './503.js'`). E2E: `tests/e2e` gained `marketing`/`marketing-mobile` Playwright projects at `:3002` (student-app `:8081` untouched) — 98/98 pass. Brief/plan/spec under `docs/briefs/`, `docs/plans/`, `design-system/ui_kits/marketing/`.**
> 2026-06-12: **AI-phase + Curriculum Intelligence task breakdown — PLANNING ONLY, all tasks 🔲 (branch `docs/ai-phase-task-breakdown`, off `qc/phase-7-backend`).** Decomposed all **13 Phase-4 AI-Tutor stories (`P3-01..13`)** + the **5 Backlog Curriculum-Intelligence stories (`BL-01..05`)** into Pipeline Briefs (`docs/briefs/`), Execution Plans (`docs/plans/`), and per-stack task files (`tasks/Backend/Phase-4-AI-Tutor/`, `tasks/Backend/Backlog-Phase-2-Plus/` w/ `-PY` Python tables, `tasks/Frontend/student-app/Phase-4-AI-Tutor/P3-12-FE.md`). **No code** — these are the build plan. **Three governing cross-cutting briefs:** [`docs/briefs/ai-helper-mvp.md`](../briefs/ai-helper-mvp.md) (AI Helper not Teacher — 4 intents, refuse-and-redirect, `ILearningContextProvider` seam `Seeded`→`Rag`, ships on the **seeded corpus in parallel** with the BL pipeline, NOT gated behind it), [`docs/briefs/ai-cost-routing.md`](../briefs/ai-cost-routing.md) (offline/runtime lanes, `AiModelRouter` cheap-default+escalate — Haiku classify / **Sonnet tutoring floor** / Opus offline-only, prompt-cache + Batch API + per-plan quotas + cache-primary pre-gen), [`docs/briefs/curriculum-system-of-record.md`](../briefs/curriculum-system-of-record.md) (Curriculum = *logical* owner; `KnowledgeNode`/`Edge` stay physically in `learning` via `Shared.Contracts`; provenance layer `ContentSource`/`Chapter` ≠ pedagogical tree; immutable `CurriculumVersion` + stable `SkillKey`; separate versioned `chunk_embeddings` table [BGE-M3 `vector(1024)`]; auto-KG → `KGSuggestion` review queue). **Settled architecture:** new `Ai` + new `Curriculum` modules approved; default provider Claude; Arabic stack = Azure DI primary (diacritics) + RAG-Anything orchestration + benchmark gate. **Open lead decisions captured in [`docs/decisions-needed-ai-phase.md`](../decisions-needed-ai-phase.md)** — per-plan AI quotas, batch/cache host + schema, .NET↔Python boundary, streaming/SSE + Azure-DI provisioning + license checks. Resolve those before dispatching implementers.
> 2026-06-12: **Phase 10 — Payment, Billing & Credits task breakdown — PLANNING ONLY, all 🔲 (same branch as the AI-phase breakdown).** Authored 12 user stories (`user-stories/Phase-10-Payments-Billing/P10-01..12` — incl. P10-12 Global Settings) + briefs + plans + task files (`tasks/Backend/Phase-10-Payments-Billing/`, `tasks/Frontend/student-app/Phase-10-Payments-Billing/` parent billing + child ⚡ energy meter, `tasks/Frontend/admin-dashboard/Phase-10-Payments-Billing/P10-11-FE` config). **The AI credit economy ("⚡ طاقة المساعد") + monetization** lifted out of the AI-Helper MVP (which keeps only a minimal daily-request cap). **Parent-driven: ALL purchasing/billing/payment is parent-side; the child only spends energy + sees a read-only meter (P10-10).** New **`Billing`** module owns the credit ledger (dual pool: monthly **granted**-expire vs **purchased**-persist), subscriptions, payments, config; spend reaches the AI Gateway via `Shared.Contracts` **`ICreditSpendService`** (charge-on-delivery, cache-hits charged same, no charge on refuse/error). **Lead decisions still open before build:** new `Billing` module sign-off, **payment provider Paymob vs Fawry** (P10-06/07), refund clawback policy (P10-09), EGP/VAT receipt fields (P10-08), Hangfire for grant/dunning jobs. **P10-03 (spend) is hard-blocked on the AI Helper cluster (P3-01..06) merging first.** Per-action costs: hint=1, explain-mistake=3, deep-explanation=5, practice-generation=5 (config-driven, P10-11). The AI-phase decisions doc (`docs/decisions-needed-ai-phase.md`) is now RESOLVED.
> 2026-06-13: **AI-phase + Phase-10 planning — FINAL state, renumber + refinements.** The complete work is on **PR #124 (`docs/ai-phase-task-breakdown → main`)** — PR #122 only carried the *first* commit to `qc` (merge it via #124, not `qc`). Updates since the 06-12 notes: **(1) Renumber Payments/Billing/Credits Phase 9 → Phase 10** — `main` already owns **Phase 9 = Notifications**; ours moved to `Phase-10-Payments-Billing/` (`P10-01..12`) to avoid an ID/folder clash. **At merge to `main`, keep BOTH** the Phase-9-Notifications and Phase-10-Payments rows in the two READMEs (Sprint→Phase tables conflict after Phase 8). **(2) Unified `ai.AiResponseCache`** (Type: Explain/Hint/WhyWrong/Practice; `ReviewStatus`/`Confidence`/`PromptVersion`/`CurriculumVersion`/`QuestionId`) replaces the separate Concept/Hint caches — **WhyWrong is now cacheable** by `(QuestionId, NormalizedWrongAnswer, Language, AgeBand, …)`; **practice = rotating pool N=5** (never 1:1, no answer-key leak); **runtime review gate** (only `Approved` entries amplified; auto-approve ≥ **0.85** confidence else `PendingReview`); **subject-aware Arabic normalization** (strip tashkeel for Math/Science, preserve for the Arabic subject). WhyWrong cap = 50/question (LRU). **(3) Credit economy v2:** Free **100/mo + 10/day**, Premium **5000/mo + 250/day**, pack **1000 / $5**; charge-on-delivery, cache-hit charged same, refuse/error free. **(4) Subscriptions:** monthly **199 EGP** + **annual 1990 EGP** (`BillingPeriod` dimension); **web checkout only — NO native IAP** (App/Play Store policy review gates native launch). **(5) Global Settings (P10-12):** all economy values + cache thresholds runtime-tunable via `IGlobalSettingsProvider` (DB-backed, Redis-cached, audited; code bootstrap defaults; `BootstrapDefaultGlobalSettingsProvider` ships in Phase-4 via `P3-01-BE-15`) — 17 managed keys. **(6) Primary cost lever = app-level Redis response cache** (hit = $0 tokens); provider prompt-cache is secondary. New brief `docs/briefs/ai-eval-gate.md`. **Success metrics to track (NOT AI cost):** Free→Paid conversion, CAC, retention, avg subscription months. Still all 🔲 planning. *(Separately: PR #123 `chore/agent-model-tuning → main` — designer→sonnet, reviewer+security-auditor→opus + the carryover lead brief.)*
> 2026-06-16: **Phase-10 Family-Wallet/Seats wave scoped (intake recorded, build pending).** Stories **P10-13..17** (13 Family Energy Wallet+allocation · 14 Child Seats+seat-reserved add-child · 15 grace/enforcement/NoSeat-Locked lifecycle · 16 redistribution · 17 refund reconciliation) + BE/FE task files authored; backend brief `docs/briefs/P10-SEATS-WALLET.md` + execution plan `docs/plans/P10-SEATS-WALLET.md` (4 batches, P10-13 foundation first; security-auditor mandatory on all five; clean-cutover migration off per-child CreditAccount). This RE-HOMES energy ownership to a parent-owned FamilyEnergyAccount (two non-convertible buckets) — supersedes the per-child CreditAccount ownership model. Locked numbers: seats Free=1/Premium=3/max=5, extra-seat 169 EGP/mo, PlanEnergyPerSeat reuses credits.{free,premium}_monthly (100/5000), seat grace = 7-day window on the dunning GraceEndsAt clock (replaces P10-09 cycle-end), pre-launch clean cutover, redistribution in MVP. New CLAUDE.md rule #9: agreed decisions → stories+tasks (ask-first). FE = other lead. Backend build (Batch 1 = P10-13) follows on a stacked branch.
>

## Option C refactor — Moderation module DONE (2026-06-16)

**Behavior-preserving refactor; Application layer now EF-free.** Audit query + write logic moved into `IAuditLogQueryService` + `IAuditLogWriter` (both in Infrastructure); `IModerationDbContext` removed from Application; `Moderation.Application` no longer references `Microsoft.EntityFrameworkCore`.

**What changed:**
- **New abstractions** in `Moderation.Application/Abstractions/`: `IAuditLogQueryService` (filtered query + ProjectTo + pagination + UTC normalization), `IAuditLogWriter` (idempotent fail-soft append).
- **Service implementations** in `Moderation.Infrastructure/Service/`: `AuditLogQueryService` (owns EF queries, projections, pagination; call-site: `GetAuditLogQueryHandler`), `AuditLogWriter` (idempotent EventId + fail-soft; call-site: `AuditLogEventHandler`).
- **Handlers thinned:** `GetAuditLogQueryHandler` now just resolves the service and delegates; `AuditLogEventHandler` resolves writer and delegates.
- **Deleted:** `IModerationDbContext` (no longer needed in Application layer; `ModerationDbContext` remains in Infrastructure for full EF access).
- **DI registration:** both services registered in `Moderation.Infrastructure/DependencyInjection.cs`.

**Verification:** Build 0 errors; P7-12 audit log 22/22 unchanged (UTC normalization + EventId idempotency + fail-soft behavior preserved); no migration; reviewer PASS.

**Sweep progress:** ✅ Moderation → ⬜ Gamification → Notifications → Billing → Learning (Ai + Identity excluded per CONVENTIONS §7 scope).


## Option C refactor — Billing module DONE (2026-06-16)

**Behavior-preserving refactor; Application layer now EF-free. PR #155 expanded to include money-path QC suite + BILLING-HARDEN-01 contention hardening.**

### What's in PR #155 now

**Part 1: Option C refactor** — Five services (`ICreditLedgerService`, `IWebhookEventService`, `ISubscriptionCheckoutService`, `ISubscriptionService`, `IGlobalSettingService`) own all credit-ledger/webhook/subscription/checkout/global-setting persistence and workflows. All explicit transactions, xmin optimistic-concurrency retry, 23505 idempotency, post-commit integration/admin events, and server-side amount reconciliation moved inside the services. Seventeen handlers thinned to validate/authorize/delegate/map only. `IBillingDbContext` relocated to `Infrastructure/Persistence`. `Billing.Application` now EF-free.

**Part 2: Money-path QC suite** — qc-test-designer designed 75 cases; api-tester implemented + ran **61/61 green** (P10 **124/124** total). All 18 locked-economy invariants covered (HIT+MISS charge, debit-on-delivery, caps, idempotency/23505, xmin never-negative, HMAC-first + forged-amount-ignored, IDOR). Test file: `backend/tests/Learnexia.IntegrationTests/P10_QC_BillingMoneyPaths_Tests.cs`. Documentation: `docs/qc/P10-Billing/` (backend-test-cases, coverage-report, execution-report).

**Part 3: BILLING-HARDEN-01 (contention hardening)** — credit-debit OCC retry loop hardened: `MaxRetries` 3→6 (configurable via `Billing:Concurrency` in appsettings.json), exponential backoff + jitter applied AFTER rollback/`ChangeTracker.Clear()` (never under a held lock), shift-overflow clamp with `long` math. Resolves **D-01** (5 concurrent debits now 5/5, BE-TC-23 tightened ≥4/5→==5/5). **D-02 closed as a non-defect** (parent JWT `Id` claim mapping verified consistent; `id=0`/Free is the intended new-parent default).

**Verification:** Build 0 errors; Tests Billing unit 93/93 + P10 integration 124/124 all pass; security-auditor 0 Critical/High (money invariants, idempotency, IDOR, delay-outside-lock all preserved); no migration; reviewer PASS.

**Sweep progress:** ✅ Moderation → ✅ Billing → ⬜ Gamification → Notifications → Learning (Ai + Identity excluded per CONVENTIONS §7 scope).
## Option C refactor — Learning module DONE (2026-06-16)

**FINAL module in the persistence-refactor sweep. Behavior-preserving across 70 handlers / 18 feature areas.** Application-layer handlers now inject services only; all EF queries, `ProjectTo` projections, pagination, and write-staging moved to per-area Infrastructure services; `Microsoft.EntityFrameworkCore` removed from `Learning.Application.csproj`; `Shared.Kernel` deliberately untouched (the `IBaseService<T>` IQueryable methods are simply unused).

**What changed:**
- **New abstractions** in `Learning.Application/Abstractions/`: 10 per-area service interfaces (`IAttemptQueryService`, `IAttemptWriteService`, `IContentBlockService`, `IDashboardQueryService`, `IKnowledgeGraphService`, `ILifecycleService`, `IMasteryQueryService`, `IProgressService`, `IReviewsService`, `IStartAttemptService`).
- **Service implementations** in `Learning.Infrastructure/Service/`: 10 corresponding services own all EF queries, projections, pagination, and write-staging.
- **Handlers thinned (70 across 18 feature areas):** Attempts (6 handlers), Concepts (2), ContentBlocks (5), Dashboard (1), Grades (1), KnowledgeGraph (5), Lessons (8), Lifecycle (5), Mastery (2), Progress (1), Questions (7), Reviews (1), Skills (5), Subjects (9), Units (6). Each now resolves service and delegates.
- **Preserved:** the single `CompleteAttempt` ambient-UoW multi-write transaction (3 Domain services + 1 DB write), `FlushAsync`-for-Id hatch pattern, the 8 pure Domain engines (no EF contact), all integration-event publishes, and all IDOR ownership guards + studentId scope filters + admin authz.
- **DI registration:** all services registered in `Learning.Infrastructure/DependencyInjection.cs`.
- **Deleted:** `ILearningDbContext` removed from Application; `LearningDbContext` remains in Infrastructure.
- **Migrations:** none required.

**Verification:** Build 0 errors; **unit 311/311 pass**; security auditor **0 Critical/High** (all ownership guards + scope filters preserved); **adversarial drift verifier clean** (one log-branch distinction in `ResetMathScienceProgress` restored via tuple return on `IProgressService.ResetMathScienceProgressAsync`). Integration: all student-facing + engine suites (P2_*, P3_08/09/10/11/13, P8_04, CO_BE_4) **12/12 green**; the 113 P7-admin failures are **pre-existing test-harness defect** (grade-list page-1 scan accumulates 340+ grades in shared Testcontainers DB — same `QuestionsAdmin` handlers pass 21/21 in `P7_04_QuestionAuthoring` suite), not a regression, tracked as follow-up.

**Sweep COMPLETE:** ✅ Moderation (#152) → ✅ Gamification (#153) → ✅ Notifications (#154) → ✅ Billing (#155) → ✅ Learning. Ai + Identity excluded by CONVENTIONS §7 design.

**Known follow-up (non-blocking, pre-existing):** P7 admin integration suites' grade-resolution helper scans only page 1 of `grades/List` and fails when >200 grades accumulate in the shared Testcontainers DB. Fix by paging through, querying by unique key, or adding per-test DB reset (Respawn).
## P3-01 — AI Gateway seam — added 2026-06-13 (branch `feat/P3-01-ai-gateway`)

Built the full AI gateway infrastructure seam (BE-1 through BE-9). No HTTP endpoint ships in this story; the gateway is an in-process DI seam for P3-02/P3-03/P3-04+.

### What shipped

- **`Shared.Contracts/Ai/`** — frozen public contract: `IAiGateway`, `AiRequest`, `AiResult`, `AiError`, `AiErrorKind`, `AiUsage`, `AiChunk`, `AiTaskKind`, `AiModelTier`, `AiMessage`. P3-02/P3-03 can wire against these immediately.
- **New `Ai` module** (4 projects): `Learnexia.Modules.Ai.Domain`, `.Application`, `.Infrastructure`, `.Api`. Schema = `ai` (no DB table in P3-01 — log-only).
- **`AiGatewayOptions`** in `Ai.Application.Options`, bound from `Ai:Gateway`. Keys (RetryCount=3, TimeoutSeconds=30, RetryBackoffSeconds=1.0).
- **`AiModelRouter`** (Application layer) — pure deterministic mapping. Default routing table:
  - `CheckAnswer`, `Classify`, `ShortTask` → `Claude / claude-haiku-4-5`
  - `Explain`, `Hint`, `Simplify`, `QuestionGeneration` → `Claude / claude-sonnet-4-6`
  - `AnalyzeDiagram`, `HardReasoning`, `ContentQa` → `Claude / claude-opus-4-8`
  - All overridable from config at `Ai:Gateway:Models:TaskKind` or `Ai:Gateway:Models:TaskKind:Tier`.
- **`ClaudeProvider`** — thin typed HttpClient wrapper to Anthropic Messages API. Supports `cache_control` for system prompt caching. Named client `"claude"`.
- **`OpenAiProvider`** — thin typed HttpClient wrapper to OpenAI Chat Completions API. Named client `"openai"`. Proves abstraction is complete.
- **`AiGateway`** facade — bounded retry + exponential backoff + hard timeout CTS + typed error translation. Never throws to caller. Logs usage at Debug (no PII, no prompt/response body).
- **29 unit tests** (router: U01–U10 + arch: ARCH-01/02/03) — all GREEN.
- Build: `dotnet build Learnexia.Modular.sln` — **0 errors**.

### Secret config key paths (required before any runtime AI call)

These are env vars / secret store — NEVER committed to git:
- `Ai__Providers__Claude__ApiKey` → your Anthropic API key
- `Ai__Providers__OpenAi__ApiKey` → your OpenAI API key

### Load-bearing decisions

- **Q5: log-only usage** — no `ai.AiUsageLogs` DB table. Deferred to P7-11. No migration in P3-01.
- **Q6: streaming** — `StreamAsync` method signature frozen. Falls back to single-shot in P3-01. Real SSE streaming is P3-04's responsibility.
- **Q7: thin HttpClient wrapper** — no vendor SDK packages. `Microsoft.Extensions.Http.Resilience` is in `Directory.Packages.props`.
- `AiGatewayOptions` lives in `Application` layer (not Infrastructure) because `AiModelRouter` in Application needs it — avoids a circular dependency.
- `ILoggerManager` exposes `LogDebug`, `LogWarn`, `LogInfo`, and `LogError(Exception?, string)`. In the Ai module: usage telemetry is logged at Debug (`LogUsage`), transient retries and HTTP-status diagnostics are logged at Warn, and `LogError` is reserved for unexpected failures with the actual exception passed (never `null`).

### What P3-02 / P3-03 can do now

- P3-02 (Safety layer) wraps `IAiGateway` — register a decorator over `IAiGateway` in `Shared.Contracts` or a pass-through seam.
- P3-03 (Prompt builder) builds `AiRequest` objects — all DTOs are frozen.
- Real provider API keys are needed before any runtime call (P3-04+).

## P3-02 — AI Safety Layer — added 2026-06-13 (branch `feat/P3-02-ai-safety-layer`)

Built the mandatory AI-safety facade enforcing FR-AI-4 (child-safety critical).

### What shipped

- **ISafetyLayer contract** (new, in Shared.Contracts/Ai/) — the ONLY authorized path for AI content generation. Feature handlers inject ISafetyLayer, never IAiGateway directly.
- **SafeAiResult envelope** — carries Allowed bool, Content string, SafetyVerdict (Blocked/Fallback/Allowed), FailedChecks string[], Confidence double forwarded from gateway for cache ReviewStatus decisioning.
- **SafetyLayer facade** (Ai.Application/Safety/SafetyLayer.cs) — sole ISafetyLayer impl. 8-step: (1) optional input toxicity screen, (2) call IAiGateway.CompleteAsync, (3-4) run all checks concurrently, (5) if Block go to (7); if NeedsRegeneration bounded regen (MaxRegenerationAttempts=2), (6) on block/exhausted: write SafetyEvent, return localized fallback, (7) fail-closed on all exceptions.
- **3 composable checks, all enabled by default (FR-AI-4):**
  - **ToxicityCheck** — LLM-as-judge (Haiku), detects slurs/profanity.
  - **AgeAppropriatenessCheck** — LLM-as-judge, age-banded (grades 1-6 vs 7-12), detects sexual/violent/scary content.
  - **HallucinationCheck** — heuristic (no LLM), checks logical consistency.
  - All return CheckVerdict {Outcome, ReasonCodes[], Details}.
- **Judge prompts injection-fenced** with sentinel delimiters to prevent student content escape.
- **SafetyOptions** (Ai:Safety appsettings) — all flags default true per FR-AI-4; operator must explicitly disable. MaxRegenerationAttempts=2.
- **ai.SafetyEvents table** — append-only, PII-light: StudentId int, TaskKind varchar, FailedChecks/ReasonCodes jsonb, ActionTaken varchar, ModelId varchar, OccurredAtUtc timestamptz indexed. NO prompt/response/names/email.
- **AiDbContext + AiDbContextFactory** — mirrors Moderation. Append-only direct SaveChanges (ADR-0001).
- **Architecture test P302-ARCH-04** — enforces no-bypass: any type outside Ai.Infrastructure/SafetyLayer/Shared.Contracts referencing IAiGateway fails test.
- **Resource strings** — AiContentBlocked (ar/en), AiServiceUnavailable (gateway fallback).
- **37 unit+arch tests GREEN** (SafetyLayerTests: 24 scenarios; AiModuleArchTests: P302-ARCH-04 + 12 other rules).
- **Eval harness** (Ai.EvalTests, [Trait("Category","Eval")] tagged, CI-excluded) — runs checks against safety-eval-set.json (ar+en samples). Run with `dotnet test --filter Category=Eval` + live keys to validate Arabic moderation before P3-04 ships (Gate B, docs/briefs/ai-eval-gate.md).

### Security audit (MANDATORY GATE — PASS, 0 Critical/High)

- Fail-closed everywhere: any error → block + fallback, never unscreened.
- No bypass: P302-ARCH-04 locks IAiGateway refs. Feature handlers must use ISafetyLayer.
- PII-light SafetyEvents: reason codes only, no raw content. P7-09 reads stable codes.
- Judge prompts injection-fenced (sentinel delimiters).
- All checks default-enabled (FR-AI-4). Operator must flip config flags to disable.
- Ai:Safety config carries flags/message-keys only, no API keys (those in secret Ai:Providers:*:ApiKey).
- Eval harness must pass with live keys on ar+en before P3-04 integration. LAUNCH-GATE requirement.

### Load-bearing decisions

- **Facade (Q3):** SafetyLayer wraps IAiGateway. Architecture test locks no-bypass at type level.
- **All checks enabled (FR-AI-4):** operator must explicitly set false in config to disable.
- **MaxRegenerationAttempts=2 (bounded):** 2nd chance on marginal, prevents unbounded loops.
- **LLM-as-judge toxicity/age (Q4/Q6):** cheap Haiku models, avoids separate endpoint. Hallucination is heuristic.
- **PII-light SafetyEvents (Q5):** reason codes + check names in jsonb. Full-content quarantine deferred to P7-09.
- **Append-only SaveChanges (ADR-0001):** mirrors Moderation.AuditLog. No Unit of Work.
- **Eval-tagged CI-excluded:** live keys needed. Run `dotnet test backend/tests/Ai.EvalTests --filter Category=Eval`.
- **Arabic moderation = LAUNCH-GATE (Gate B):** must pass with real keys ar+en before P3-04 ships to prod.

### What P3-04/05/06 must do

- Inject ISafetyLayer, never IAiGateway.
- Call `await _safetyLayer.GenerateSafeAsync(request, ct)`.
- On block, return `result.Message` (localized).
- Use `Confidence + SafetyVerdict` for cache ReviewStatus decision.
- Real provider keys (Ai__Providers__Claude__ApiKey, etc.) required before runtime.

### Load-bearing config + secret paths

Secrets (env vars / secret store, NEVER git):
- Ai__Providers__Claude__ApiKey
- Ai__Providers__OpenAi__ApiKey

Config flags (appsettings.json, safe to commit):
- Ai:Safety:EnableToxicityCheck = true (default)
- Ai:Safety:EnableAgeCheck = true (default)
- Ai:Safety:EnableHallucinationCheck = true (default)
- Ai:Safety:MaxRegenerationAttempts = 2 (default)
- Ai:Safety:ModerationProvider = "Claude" (default)
- Ai:Safety:FallbackMessageKey = "AiContentBlocked" (default)
- Ai:Safety:GatewayErrorFallbackKey = "AiServiceUnavailable" (default)

## P3-03 — Prompt Builder — added 2026-06-13 (branch `feat/P3-03-prompt-builder`)

Built the full prompt-builder (P3-03 BE-1 through BE-10). Stateless deterministic prompt assembly for personalized child-safe tutor prompts.

### What shipped

- **`IPromptBuilder` contract** (new, in Ai.Application/PromptBuilder/) — single method `BuildAsync(PromptContext) → AiRequest`. Deterministic, no side effects, pure logic.
- **`PromptContext` value object** — captures all rendering variables: StudentId, ChildGrade, TutorLanguage (ar/en), HelperIntent, SubjectName, TopicName, CurrentConcept, WeakAreas[], ContextProvider (pluggable: `ILearningContextProvider`).
- **`PromptBuilder` facade** — wires 4-step pipeline: (1) TemplateSelector picks intent-specific template (`Explain`/`Hint`/`WhyWrong`/`SimilarExample`), (2) ToneFrame anti-injection + PII-minimal (grade/age only, no StudentId/name/email), (3) language variant (ar/en), (4) assemble final `AiRequest` with task-kind routing.
- **4-subject template tree** (Math, Science, Arabic, English; NO Social Studies) — each subject has 4 intent variants (Explain/Hint/WhyWrong/SimilarExample), each bilingual ar/en, code-as-config in `PromptBuilder/Templates/` (7 txt files per subject, 28 templates total). Tone frame = injected guard: student-supplied text wrapped in sentinel delimiters to block escape.
- **`TemplateSelector` static** — pure dictionary lookup: (Subject, Intent) → template string. Fast, deterministic.
- **`HelperIntent` enum** — 4 values: Explain (describe concept), Hint (guide without answer), WhyWrong (explain incorrect choice), SimilarExample (practice variant). Maps to `AiTaskKind.Explain` (Explain/SimilarExample) or `AiTaskKind.Hint` (Hint/WhyWrong) for model routing via `AiModelRouter` (Sonnet + Mid tier per brief).
- **Optional seams in `Shared.Contracts/Ai/`** (P3-03 ships stubs; implementations deferred):
  - `IStudentWeakAreasQuery` → P3-09 (mastery engine computes weak areas).
  - `IChildLearningProfileQuery` → P3-04 (profile engine).
  - `ICurriculumContextQuery` → P3-07 (curriculum module retrieves context).
  - `ILearningContextProvider` (Ai.Application/Stubs/`EmptyLearningContextProvider`) → P3-07 deferred (concrete `SeededCorpusContextProvider`/`RagContextProvider` live in P3-07; module isolation: Ai cannot reference Learning/Curriculum; config key `AiHelper:ContextProvider` introduced in P3-07).
- **`AiTutor` seam in `Shared.Contracts/AiTutor/`** — `LearningContext` DTO + `ILearningContextProvider` interface; both consumed by P3-04/05/06 through ISafetyLayer.
- **Graceful degradation** — all optional seams return safe defaults if unimplemented (empty weak areas, no profile hints, generic context). Prompts render + ship even if dependencies are stubbed.
- **4 unit test files** (203 Ai unit tests total, 166 new):
  - `PromptBuilderAssemblyTests` — verify all 28 templates exist + parse + no interpolation errors.
  - `PromptBuilderLanguageTests` — ar/en variants render + ToneFrame guards inject correctly.
  - `PromptBuilderGracefulDegradationTests` — stubs return safe defaults; full prompts assemble without crashing.
  - `TemplateSelectorTests` — dictionary lookup deterministic, all (Subject, Intent) pairs map.
- **DI wiring** in `Ai.Application/DependencyInjection.cs` — `AddScoped<IPromptBuilder, PromptBuilder>()` + optional seam registrations (defaults to stubs).
- **Security audit: MANDATORY GATE — PASS** (0 Critical/High). ToneFrame anti-injection tested; PII-minimal (grade/age only); `HelperIntent` routing prevents model-mismatch; no hardcoded secrets; graceful fallback on missing dependencies. FR-AI-6 (personalized + safe) enforced.
- Build: `dotnet build Learnexia.Modular.sln` — **0 errors**. Unit test suite 203 green (166 P3-03 new + 37 P3-02 legacy).

### Load-bearing decisions (reviewer-confirmed)

- **Pure stateless facade** — no DB reads, no side effects. PromptBuilder is a pure function PromptContext → AiRequest.
- **`HelperIntent` replaces `TutorTask`** — 4 intents (Explain/Hint/WhyWrong/SimilarExample) map 2:1 to `AiTaskKind` (Explain/SimilarExample→Explain, Hint/WhyWrong→Hint); both use Sonnet + Mid tier per the routing table.
- **4-subject invariant** (Math/Science/Arabic/English) — no Social Studies per product spec. Subject enum + all templates locked to 4 members.
- **Optional seams with safe stubs** — `IStudentWeakAreasQuery`/`IChildLearningProfileQuery`/`ICurriculumContextQuery`/`ILearningContextProvider` shipped with no-op stubs; implementations land in later stories (P3-04/07/09). Graceful degradation: if a seam is never wired, the stub returns empty/safe (weak areas=[], profile=null, context=empty).
- **Module isolation preserved** — Ai.Application can reference `Shared.Contracts` only, never Learning or Curriculum. `ILearningContextProvider` lives in `Shared.Contracts/AiTutor/`, not in Ai. The concrete provider (`SeededCorpusContextProvider`) is authored in P3-07 + wired there; `AiHelper:ContextProvider` config key introduced in P3-07 at wire-time.
- **ToneFrame anti-injection ar/en** — student text (wrong answer, question context) wrapped in sentinel delimiters to prevent escape + prompt injection. Guards tested ar+en.
- **PII-minimal prompts** — only StudentGrade + EstimatedAge passed to model. No StudentId, email, name, phone, or learning-language in the prompt body.
- **Templates = code-as-config** — .txt files in `PromptBuilder/Templates/` read at startup, parsed once, cached. No DB table, no versioning yet (P10-12 considers runtime tuning via Global Settings).
- **No migration** — P3-03 is stateless; no new tables or schema.

### What P3-04/05/06 must do

- Inject `IPromptBuilder` (now registered in DI).
- Call `await _promptBuilder.BuildAsync(context)` with filled `PromptContext`.
- Pass result `AiRequest` to `ISafetyLayer.GenerateSafeAsync(request, ct)`.
- Wire optional seams: P3-04 implements `IChildLearningProfileQuery`, P3-07 implements `ICurriculumContextQuery` + `ILearningContextProvider`, P3-09 implements `IStudentWeakAreasQuery`.

### Config + secret paths

Secrets (env vars / secret store, NEVER git):
- (None new in P3-03; inherited from P3-01/P3-02: Ai__Providers__Claude__ApiKey, Ai__Providers__OpenAi__ApiKey)

Config flags (appsettings.json, safe to commit):
- (All inherited from P3-02 SafetyOptions; P3-03 adds no new config keys. **P3-07 introduces** `AiHelper:ContextProvider` = "Seeded" or "Rag" at wire-time.)


## P3-04 — Explain a concept on demand (SSE Tutor Endpoint) — added 2026-06-14 (branch `feat/P3-04-explain`, Wave 3)

Built **P3-04** in the **`Ai` module**. Full pipeline (analyzer → planner → backend-feature → security-auditor → reviewer PASS). Mandatory security gates: error-leak audit + D-1 fixes validated.

### What shipped — CRITICAL SSE WIRE CONTRACT (for P3-12 FE consumption)

**SSE endpoint** (`ExplainController`/`POST /api/AiTutor/Explain/{skillId}`) — student-scoped (`[Authorize(Roles="Student")]`), consumes `ExplainConceptCommand` (skillId, childGrade, tutorLanguage from JWT), emits **exactly 4 event types**:

| Event Type | Data Schema | Semantics |
|---|---|---|
| `event: message` | `{"content":"<buffered text>"}` | Approved content chunk; FE appends |
| `event: redirect` | `{"type":"lesson","targetId":"<skillId>"}` | Context refused (no curriculum match); FE navigates to lesson |
| `event: error` | `{"code":"ValidationError\|UnhandledError\|<SafetyCode>","message":"<safe localized msg>"}` | Failure (never emits subsequent events); NO stack trace ever |
| `event: done` | `[DONE]` | Stream terminator; NOT emitted after error |

**Architecture:**
- **Handler** (`ExplainConceptCommandHandler`) orchestrates: (1) `ILearningContextProvider` (student skill context) → (2) `IPromptBuilder` (safe prompt) → (3) `ISafetyLayer` (buffer→filter→emit) → (4) buffered text via `RedirectResponseBuilder` (ar/en). **Never raw tokens from LLM**.
- **Refuse-and-redirect:** Empty context (no curriculum chunk found) → emit redirect event (no safety processing, fail-fast).
- **Buffering:** `ISafetyLayer.GenerateSafeAsync()` returns `SafeAiResult.Content` (post-check); handler wraps in `message` events per 100-char buffer chunks. On block: emit single `error` event.
- **Instrumentation:** `HelpRequestedIntegrationEvent` (start), `HelpDeliveredIntegrationEvent` (success), `HelpDeclinedIntegrationEvent` (refused/blocked) — logged to `ai.SafetyEvents` (PII-light).
- **Rate limiter:** `AiTutorRateLimiter` (in-process, per-student daily cap, default 20/day, config-tunable). Hard-coded to Ai module; swap to Redis before multi-instance.

### Load-bearing decisions (reviewer-confirmed, security PASS)

- **SSE (rule-8 exception):** bypassess `BaseResponse<T>` envelope (lead-approved). Stream-oriented delivery for AI.
- **Refuse-and-redirect (UX fail-safe):** empty context → immediate redirect (graceful, no hallucination attempt). Live grounding dormant until real `ILearningContextProvider` wired.
- **Buffer→safety→emit discipline:** `ISafetyLayer` is the sole content exit. Handler never calls `IAiGateway` directly.
- **ILessonContextContract seam:** `LessonContextDto` (skillId, name, description, prerequisites) + adapter in `Learning.Infrastructure/Contracts/` exposes student skill context to Ai module. Module isolation intact (Ai references only Shared.Contracts).
- **Rate limiter is in-process:** blocks per-student, fail-soft on hit (429 + `AiTutorRateLimitExceeded` message). Multi-instance deployment needs Redis.
- **Folded into Ai module:** no new module; reuses P3-01/P3-02/P3-03 seams.
- **Cache economy deferred:** `P3-01-BE-12`, `P3-01-BE-13`, `P3-01-BE-14` (P3-01 task breakdown) handle AI credit ledger + quota dispatch. MVP: free-to-student, no spend tracking yet.
- **Live grounding dormant:** `EmptyLearningContextProvider` (stub default) always redirects. Real `SeededCorpusContextProvider` (P3-07) or `RagContextProvider` (P3-07 w/ embeddings) wired at runtime via `AiHelper:ContextProvider` config.

### Test coverage + gates

- **208 unit tests** (ExplainConceptCommandHandlerTests: context/buffer/safety/redirect/rate-limit edge cases, ar/en localization). All green.
- **13 SSE integration tests** (`P3_04_ExplainSse_Tests.cs`: endpoint contract, event sequence, error handling, redirect on empty, rate-limit 429). All green.
- **Mandatory security gate PASS (0 Critical/High):** error-leak audit confirmed no stack traces in error events; D-1 feedback fixes applied (internal logic messages never exposed); `[Authorize]` enforced; rate-limiter fail-soft prevents brute-force.

### Pre-deployment checklist

- Real Claude/OpenAI keys required (`Ai__Providers__*__ApiKey` env vars).
- `AiTutor:RateLimiter:DailyCapPerStudent` configured (default 20).
- `AiHelper:ContextProvider` = "Seeded" (goes live with P3-07 curriculum).
- **CORS:** SSE requires credentialed cross-origin — ensure platform config allows (P3-04 is HTTP only; CORS + credentials cross-check is platform follow-up).
- **Not multi-instance ready:** in-process rate limiter will not coordinate across instances. Upgrade to Redis before scaling (platform backlog).

### What P3-12 (FE) consumes

- SSE endpoint path + method.
- Exact event/data schema (no breaking changes post-MVP).
- FE must handle all 4 event types + parse JSON data payloads.
- Fallback: if no `event: done` within timeout, treat as error (FE timeouts at 15s; server timeout at 25s per brief).
- Localization: `error.message` keys are from `SharedResourcesKey` (ar/en, safe text only).

### Config + secret paths

Secrets (env vars / secret store, NEVER git):
- `Ai__Providers__Claude__ApiKey` (required)
- `Ai__Providers__OpenAi__ApiKey` (optional secondary)

Config (appsettings.json, safe to commit):
- `AiTutor:RateLimiter:DailyCapPerStudent` (int, default 20)
- `AiHelper:ContextProvider` (string, "Seeded"|"Rag", default "Seeded")
- (Inherited from P3-01/P3-02/P3-03: gateway + safety + builder options)


## P3-05 — Hints + WhyWrong + Simplify (SSE Tutor Endpoints) — added 2026-06-14 (branch `feat/P3-05-hints`, Wave 4)

Built **P3-05** in the **`Ai` module**. Two SSE endpoints (GetHint + WhyWrong via shared Intent seam; Simplify reuses Explain logic). Full pipeline (analyzer → planner → backend-feature → **mandatory security gate PASS** (IDOR + no-reveal + usage-recording) → reviewer PASS).

### What shipped — SSE WIRE CONTRACTS (for P3-12 FE consumption)

**Two endpoints:**

1. **POST /api/AiTutor/Hint** — student-scoped ([Authorize(Roles="Student")]), consumes GetHintCommand (questionId, attemptedAnswers, childGrade, tutorLanguage from JWT). Emits Hint preamble frame first with hintLevel + nextHintLevel, then content chunks.

2. **POST /api/AiTutor/Simplify** — same signature, reuses Explain pipeline. Simplify does not emit Hint preamble.

**Architecture:**

- Handler (GetHintCommandHandler / SimplifyExplanationCommandHandler): (1) IDOR scope check via IQuestionAnswerContract.GetStudentAnswerAsync(studentId, questionId); (2) if IDOR fails → 403; (3) fetch/increment hint level from StudentQuestionAttempt; (4) build prompt via IPromptBuilder; (5) call ISafetyLayer.GenerateSafeAsync() with post-check (WrongAnswerNormalizer-aware text comparison); (6) buffer and emit via RedirectResponseBuilder; (7) on safety block → emit error event.
- IDOR-scoped seam: New IQuestionAnswerContract interface (Shared.Contracts/Learning/) with impl in Learning.Infrastructure/Contracts/QuestionAnswerContractAdapter. Cross-module registration: Learning.Infrastructure.AssemblyReference added to Host MediatRExtensions to discover HintUsedIntegrationEventHandler at startup.
- Usage instrumentation: HintUsedIntegrationEvent published on successful hint delivery → HintUsedIntegrationEventHandler (Learning.Infrastructure) records usage in fresh scope + direct SaveChangesAsync() per ADR-0001.

### Load-bearing decisions (mandatory security gate PASS)

- IDOR seam (critical): IQuestionAnswerContract gates hint access by JWT studentId vs question ownership. Mismatch → 403 Forbidden.
- No-reveal post-check (critical): Normalization-aware safeguard (subject-aware tashkeel stripping) compares returned hint against known wrong answer. >70% token match → blocks + logs.
- Server-derived hint level: StudentQuestionAttempt.CurrentHintLevel incremented server-side, capped at MaxHintLevels=3 config.
- Simplify reuses Explain: Routes via HelperIntent.Explain seam, no Hint preamble.
- Cache economy deferred: P3-01-BE-12/13/14 (per-hint caching, quota, batch pre-gen).
- Usage instrumentation: Fresh scope + direct SaveChanges to avoid DbContext lifetime issues during event publishing.

### Test coverage + gates

- 214 unit tests (GetHintCommandHandlerTests: IDOR, no-reveal, hint-level increment, buffer/safety edge cases, ar/en). All green.
- 21 SSE integration tests (P3_05_HintSse_Tests.cs: endpoint contract, IDOR 403, hint preamble, error handling, no-reveal block, WhyWrong intent, Simplify route). All green.
- Mandatory security gate PASS (0 Critical/High): IDOR seam validated; no-reveal post-check confirmed; usage instrumentation does not expose PII; [Authorize] enforced.

### Pre-deployment checklist

- Real Claude/OpenAI keys required (Ai__Providers__*__ApiKey env vars).
- AiTutor:RateLimiter:DailyCapPerStudent configured (shared with P3-04, default 20).
- AiHelper:ContextProvider = "Seeded" (or "Rag" when P3-07 goes live).
- StudentQuestionAttempt.CurrentHintLevel migration applied.
- Not multi-instance ready: in-process rate limiter. Upgrade to Redis before scaling.

### What P3-12 (FE) consumes

- Two endpoint paths: /api/AiTutor/Hint + /api/AiTutor/Simplify.
- Hint preamble structure: {"hintLevel": n, "nextHintLevel": n+1}.
- Content/error event schema (same as P3-04).
- Retry: if no done event within 15s FE timeout, treat as error.

### Config + secret paths

Secrets (env vars / secret store, NEVER git):
- Ai__Providers__Claude__ApiKey (required)
- Ai__Providers__OpenAi__ApiKey (optional secondary)

Config (appsettings.json, safe to commit):
- AiTutor:RateLimiter:DailyCapPerStudent (int, default 20, shared with P3-04)
- AiTutor:MaxHintLevels (int, default 3)
- AiTutor:WrongAnswerNormalizer:NoRevealThreshold (double, default 0.70)

### Carry-forward (non-blocking deferred)

- Cache economy: AiResponseCache unified table + Redis read-through (P3-01-BE-12/13/14)
- Quota dispatch: credit ledger + charge-on-delivery (P10-03)
- Batch pre-gen: seed common hints offline (P3-01-BE-12)
- PostHelpRetry / PostHelpSuccess: re-quiz components in P3-12 (FE); requires SubmitAnswerCommandHandler enhancement
- Live grounding: Real ILearningContextProvider wired at runtime (P3-07 embeddings gate).


## P3-06 Part B — SimilarExample (SSE Tutor Endpoint, Intent #4) — added 2026-06-14 (branch `feat/P3-06-grounded-questions`, Wave 4)

Built **P3-06 Part B** in the **`Ai` module**. The 4th and final intent of the AI Helper MVP. Full pipeline (analyzer → planner → backend-feature → **mandatory security gate PASS** (no Critical/High) → reviewer PASS).

### What shipped — SSE WIRE CONTRACT (for P3-12 FE consumption)

**One endpoint:**

**POST /api/AiTutor/SimilarExample** — student-scoped ([Authorize(Roles="Student")]), consumes SimilarExampleCommand (skillId, optional questionId, childGrade, tutorLanguage from JWT). **Generates curriculum-grounded practice variants without redundant preamble** (differs from P3-05 Hint which emits preamble). Emits content chunks.

**Architecture:**

- Handler (SimilarExampleCommandHandler): (1) refuse-and-redirect on empty learning context (no curriculum match) — fail-fast, no safety processing; (2) call ISafetyLayer.GenerateSafeAsync() with the grounded prompt; (3) buffer and emit via RedirectResponseBuilder; (4) on safety block → emit error event.
- **No hint-level tracking** (unlike P3-05) — similar examples are always freshly generated (not capped/leveled).
- Usage instrumentation: HelpDeliveredIntegrationEvent / HelpDeclinedIntegrationEvent / HelpRequestedIntegrationEvent — logged to ai.SafetyEvents (PII-light, reason codes only).
- Rate limiter: Shared AiTutorRateLimiter (in-process, per-student daily cap, inherited from P3-04).

### Load-bearing decisions (mandatory security gate PASS, 0 Critical/High)

- **No preamble:** Unlike P3-05 Hint (which emits {hintLevel, nextHintLevel}), P3-06 generates similar examples directly — no metadata frame, just content chunks.
- **Refuse-and-redirect (fail-safe):** Empty context → immediate redirect (same as P3-04 Explain); prevents hallucinated examples unsupported by curriculum.
- **Buffer→safety→emit discipline:** ISafetyLayer is sole content exit.
- **Shared rate limiter:** Counts against the same daily per-student cap as Explain/Hint (default 20/day, configurable AiTutor:RateLimiter:DailyCapPerStudent).
- **Folded into Ai module:** reuses P3-01/P3-02/P3-03/P3-04 seams.
- **Cache economy deferred:** P3-01-BE-12/13/14 (response cache + quota dispatch).
- **Live grounding dormant:** EmptyLearningContextProvider (stub default) always redirects. Real RagContextProvider wired at runtime via AiHelper:ContextProvider config.

### Test coverage + gates

- 219 unit tests (SimilarExampleCommandHandlerTests: context/redirect/buffer/safety edge cases, ar/en). All green.
- 13 SSE integration tests (P3_06_SimilarExampleSse_Tests.cs: endpoint contract, event sequence, refuse-and-redirect, error handling). All green.
- Mandatory security gate PASS (0 Critical/High): error-leak audit confirmed no stack traces; no IDOR issues; rate-limiter fail-soft; [Authorize] enforced.

### Pre-deployment checklist

- Real Claude/OpenAI keys required (Ai__Providers__*__ApiKey env vars).
- AiTutor:RateLimiter:DailyCapPerStudent configured (shared cap with P3-04/P3-05, default 20).
- AiHelper:ContextProvider = "Seeded" (or "Rag" when P3-07 goes live).
- Not multi-instance ready: in-process rate limiter. Upgrade to Redis before scaling.

### What P3-12 (FE) consumes

- Endpoint path: /api/AiTutor/SimilarExample.
- Content/error event schema (same as P3-04 Explain).
- Retry: if no done event within 15s FE timeout, treat as error.
- **No preamble to parse** (unlike Hint).

### Config + secret paths

Secrets (env vars / secret store, NEVER git):
- Ai__Providers__Claude__ApiKey (required)
- Ai__Providers__OpenAi__ApiKey (optional secondary)

Config (appsettings.json, safe to commit):
- AiTutor:RateLimiter:DailyCapPerStudent (int, default 20, shared cap)
- AiHelper:ContextProvider (string, "Seeded"|"Rag", default "Seeded")

### Carry-forward (non-blocking deferred)

- **Part A (offline question generation):** P3-01-BE-1 through P3-01-BE-9; generates practice-pool variants in batch at question/attempt creation time.
- **Practice-pool cache:** Seeded variant pool with dedup (P3-01-BE-13, P3-01-BE-14, plus cache parts of P3-01-BE-10).
- **Quota dispatch:** AI credit ledger + charge-on-delivery (P10-03).
- **This completes the 4-intent AI Helper MVP** (Explain, Hint, WhyWrong, SimilarExample) — all wired in P3-04/P3-05/P3-06. P3-12 FE consumes all 4 endpoints.

## Operationalize Phase-4 AI — flip-to-live runbook — 2026-06-15 (wave `feat/phase4-ai-runtime`)

This section is the canonical, end-to-end activation runbook once all Cluster A + B + C code is merged. Follow steps in order. No code change is required at any step — all flip switches are config/env.

### What the wave shipped (code summary)

- **Cluster A:** Parity-guard hardened (fail-fast on ModelVersion mismatch), `ReEmbedCurriculumJob` (Hangfire, idempotent, 50-row batches), `POST /api/Admin/Curriculum/ReEmbed` admin trigger, `Curriculum:Retrieval:SimilarityDistanceFloor` config-bound (default 0.4, placeholder-tuned).
- **Cluster B:** `AiResponseCache` DB table (`ai.AiResponseCache`, UNIQUE `CacheKey`, 4 indexes), `IAiResponseCache` Redis/DB read-through (R5 gate: only `Approved + non-invalidated` served), `IGlobalSettingsProvider` + `BootstrapDefaultGlobalSettingsProvider` (reads from `AiHelper:Cache:*` in appsettings), cache-first + cache-write wired into all 4 handlers (Explain/Hint/SimilarExample/Simplify), `RedisAiRateLimiter` (Redis fixed-window, falls back to `AiTutorRateLimiter` in-process when Redis absent).
- **Cluster C:** `PromptBuilder.Build` populates `AiRequest.CacheableSystemPrompt` with the stable tone-frame (ToneFrame.Ar / ToneFrame.En), enabling `ClaudeProvider` to emit `cache_control: ephemeral` on every request. `ILearningContextProvider` flip is config-driven via `AiHelper:ContextProvider` (already present since P3-07); `Program.cs` wires `IGlobalSettingsProvider`. All config keys documented in `appsettings.json` with safe empty defaults and inline comments.

### Flip-to-live runbook (steps 1–6, in order)

**Step 1 — Provider API keys [devops / secret store]**

Set the following env vars in the deployment secret store (NEVER commit non-empty values):

```
Ai__Providers__Claude__ApiKey=<anthropic-api-key>
Ai__Providers__OpenAi__ApiKey=<openai-api-key>   # optional secondary
```

Absent key behavior: `ClaudeProvider` / `OpenAiProvider` return `AiError.Unavailable`; `SafetyLayer` fails closed; students receive a localized error message. No startup crash.

**Step 2 — BGE-M3 TEI provisioning [devops, external]**

Stand up the BGE-M3 TEI server on Hetzner (docker-compose / deployment entry, pinned `Model` and `ModelVersion`, health endpoint, auth token). Record the base URL and model version. This is `P3-07-BE-0` (devops side) — NOT a code task.

Once the TEI endpoint is live, set the following env vars:

```
Curriculum__Embedding__BaseUrl=http://<hetzner-host>:8080
Curriculum__Embedding__ModelVersion=<version-served-by-tei>   # e.g. "1.0"
Curriculum__Embedding__AuthToken=<bearer-token>               # NEVER commit
Curriculum__Embedding__Model=bge-m3                           # safe to commit
```

Absent `BaseUrl` behavior: `BgeM3EmbeddingProvider.EmbedAsync` returns null, retrieval returns empty, all 4 handlers redirect. Startup logs a clear warning. No crash.

**Step 3 — Re-embed curriculum chunks**

Trigger the Hangfire re-embed job via the admin endpoint:

```
POST /api/Admin/Curriculum/ReEmbed
Authorization: Bearer <admin-jwt>
```

Watch the Hangfire dashboard (`/hangfire`, Development only) or query the DB until all `chunk_embeddings_bge_m3` rows have `IsActive = true` and `ModelVersion` matches `Curriculum:Embedding:ModelVersion`. Placeholder rows (`ModelVersion = 'seed-placeholder-v0'`) must drop to zero.

The job is idempotent: re-running when no placeholder rows remain is a safe no-op.

**Step 4 — Re-calibrate the similarity floor**

The current `Curriculum:Retrieval:SimilarityDistanceFloor = 0.4` was tuned for placeholder hash-vector geometry (`seed-placeholder-v0`). Real BGE-M3 embeddings have different distance distributions.

After re-embed, run representative evaluation queries (in-corpus: Grade-3 Math fractions, Science photosynthesis; out-of-corpus: geography, history) and adjust the floor:

- Typical BGE-M3 semantically-similar distances: 0.10–0.35.
- Start at `0.3` and adjust.
- Too strict (too low): over-redirects — students get "no context" for in-corpus questions.
- Too loose (too high): irrelevant chunks included — retrieval quality degrades.

Set in config (safe to commit; no live secret):

```json
"Curriculum": {
  "Retrieval": {
    "SimilarityDistanceFloor": 0.3
  }
}
```

**Step 5 — Activate live grounding**

Set `AiHelper:ContextProvider = "Rag"` in appsettings (or env `AiHelper__ContextProvider=Rag`). No code change required.

This switches `ILearningContextProvider` from `EmptyLearningContextProvider` (always-redirect) to `RagContextProvider` (live pgvector retrieval). The switch happens in `Curriculum.Infrastructure.DependencyInjection.AddCurriculumInfrastructure` which checks the key at registration time.

**Do NOT flip this before Steps 3 + 4 are complete** — activating RAG over placeholder vectors returns garbage retrieval results.

**Step 6 — Verify end-to-end**

```
POST /api/AiTutor/Explain
{ "skillId": <in-corpus-skill-id>, ... }
```

Expected sequence for a cache miss on first call:
1. `RagContextProvider` retrieves non-empty chunks (similarity floor met).
2. `PromptBuilder` assembles the prompt with `CacheableSystemPrompt` populated (Claude will prompt-cache the tone frame).
3. `ISafetyLayer` calls `ClaudeProvider` → response returned.
4. `IAiResponseCache.WriteAsync` stores the entry (`ReviewStatus = Approved` if confidence ≥ 0.85, else `PendingReview`).

Second identical call → cache HIT (zero Claude API calls; asserted by zero `IAiGateway` invocations).

### Cache TTL matrix

| Intent | TTL (Redis hot layer) | Key tuple |
|---|---|---|
| Explain | 24 h | `(SubjectId, ConceptId, AgeBand, Language, Difficulty, PromptVersion, CurriculumVersion)` |
| Hint | 12 h | `(SubjectId, QuestionId, HintLevel, AgeBand, Language, PromptVersion, CurriculumVersion)` |
| WhyWrong | 6 h | `(SubjectId, QuestionId, SHA256(NormalizedWrongAnswer), Language, AgeBand, PromptVersion, CurriculumVersion)` |
| SimilarExample (Practice) | 24 h | `(SubjectId, SkillKey, VariationIndex, AgeBand, Language, PromptVersion, CurriculumVersion)` |

`AgeBand` = `grade / 3` (floor) so Grades 1–3 share a band, Grades 4–6 share a band, etc. Prevents cross-cohort cache pollution.

`WhyWrong` variant cap: 50 per `QuestionId` (LRU by `CreatedAt`). Configurable via `AiHelper:Cache:whyWrongVariantCap`.

### Redis rate-limiter config

- Counter key: `ai:rl:{studentId}:{windowMinute}` (UTC minute epoch).
- Window: 60 seconds, max 10 requests per student per window.
- Redis absent: falls back to `AiTutorRateLimiter` (in-process `ConcurrentDictionary`); no crash; no multi-instance coordination.
- Redis present: `RedisAiRateLimiter` (atomic `INCR + EXPIRE`; shared across instances).
- Config: `ConnectionStrings:Redis` (empty = in-process fallback).

### DEFECT-3 fix — cache write scope-isolation (branch `feat/ai-runtime-activation-e2e`)

**Bug:** All 4 handlers issued the cache write as `_ = _aiCache.WriteAsync(writeEntry, CancellationToken.None)` using the request-scoped `IAiResponseCache` (which depends on the scoped `AiDbContext`). When the SSE response completed, the request scope disposed `AiDbContext` BEFORE the detached task ran → `ObjectDisposedException` swallowed by `AiResponseCacheRepository`'s fail-soft catch → cache row never persisted. Cache HITs were therefore never served in production.

**Fix (mirroring P3-05 `HintUsedIntegrationEvent` pattern):** The write is now dispatched inside `Task.Run` that creates a fresh `IServiceScopeFactory.CreateAsyncScope()`, resolves `IAiResponseCache` from the new scope, and awaits `WriteAsync`. The new scope has its own `AiDbContext` lifetime, completely independent of the request scope. The write remains non-blocking (does not delay the SSE response) and fail-soft (errors are caught + logged as Warn only).

**Files changed:**
- `ExplainConceptCommandHandler`: added `IServiceScopeFactory` injection; cache write wrapped in `Task.Run` + fresh scope.
- `SimplifyExplanationCommandHandler`: same.
- `GetHintCommandHandler`: already had `IServiceScopeFactory`; cache write (line 385 before fix) wrapped in fresh scope.
- `SimilarExampleCommandHandler`: already had `IServiceScopeFactory`; cache write (line 283 before fix) wrapped in fresh scope.
- Tests: `ExplainConceptCommandHandlerTests` — added `IServiceScopeFactory` parameter + `BuildNoOpScopeFactoryMock` + new EH-08 regression test that proves `CreateScope()` is used and the fresh-scope `IAiResponseCache.WriteAsync` is called. Hint/SimilarExample test scope mocks updated to also resolve `IAiResponseCache` (no longer log spurious warn on write).

**Activation gating untouched:** Confidence/auto-approve gate, kill-switch, cache key, SSE contract — all unchanged.

**Handoff to api-tester:** The E2E HIT/MISS tests that were previously using an in-memory cache stub should be re-pointed at the real `AiResponseCacheRepository` (in-memory EF or Postgres) to prove end-to-end DB persistence on cache MISS followed by HIT on the second call.

### AiResponseCache serving — OQ-7 RESOLVED (AI Cache Activation, branch `feat/ai-runtime-activation-e2e`)

`AiResponseCacheRepository.GetApprovedAsync` serves only entries with `ReviewStatus = Approved AND InvalidatedAt IS NULL`. **OQ-7 is now resolved:** `SafetyLayer` populates `SafeAiResult.Confidence` with `ai.cache.safetyPassConfidence` (default **0.90**) ONLY on the all-checks-pass path (null on every block/fallback/cancel/catch path). An entry is auto-approved (servable) at write time when `Allowed` + `Confidence >= ai.cache.autoApprovalConfidence` (0.85) + the kill-switch `ai.cache.autoApproveEnabled` (default **true**). So safety-passed responses are now cached AND served. **Kill-switch:** set `AiHelper:Cache:autoApproveEnabled=false` to stop NEW approvals (restores dormant write behavior). **Residual (security #6, accepted):** the kill-switch is an *approval freeze*, not a *serving freeze* — already-`Approved` rows keep serving (DB + Redis until TTL). For an incident "stop serving everything now" you'd need a GET-path short-circuit + bulk `InvalidatedAt` sweep + Redis flush (follow-up if go-live requires a panic button).

**AI-runtime KNOWN GAPS surfaced by the E2E suite (NOT fixed — distinct from the unrelated Curriculum `DEFECT-1/2` entries above):**
- **AI-DEFECT-1 (HIGH) — FIXED (branch `fix/ai-grade-jwt-claim`):** `Grade` (and `Age`) claims are now minted into the student JWT in `AuthenticationIdentityService.GetClaims`. `User.Grade` and `User.Age` are already on the Identity `User` entity (set at child creation / P7-08 override); no cross-module seam was needed. Claims are emitted only for student-role accounts (guarded by `user.Grade.HasValue`). The AI handlers' `TryResolveProfile` now resolves the real grade and the fallback of `4` only fires for legacy/non-student tokens. Grade-change caveat: a P7-08 grade override takes effect on the next token refresh — live token invalidation is not required. Constants: `CustomClaimTypes.Grade = "Grade"`, `CustomClaimTypes.Age = "Age"` added to `Identity.Domain`. Tests: `GradeClaimConstantsTests` (4 cases, Identity.UnitTests) + `AiGradeClaimCacheDifferentiationTests` (4 cases, Ai.UnitTests) covering band-isolation, within-cohort reuse, fallback, and regression guard.
- **AI-DEFECT-2 (MEDIUM, product decision):** `AiCacheKeyBuilder.ForExplain` keys on `ConceptId` but NOT `SkillId` — two skills sharing a concept share an Explain cache slot. Decide: concept-level caching (current) is acceptable, or add `SkillId` for skill-level granularity.

### External dependencies (not built by this pipeline)

| # | What | Who clears | Impact when absent |
|---|---|---|---|
| EXT-1 | BGE-M3 TEI endpoint on Hetzner | Devops | `BgeM3EmbeddingProvider` returns null; all handlers redirect. No crash. |
| EXT-2 | Claude API key (`Ai:Providers:Claude:ApiKey`) | Lead / secret store | `ClaudeProvider` returns `AiError.Unavailable`; `SafetyLayer` fails closed. No crash. |
| EXT-3 | OpenAI API key (`Ai:Providers:OpenAi:ApiKey`) | Lead / secret store | Same as EXT-2 for OpenAI tier. |
| EXT-4 | Similarity floor calibration | Lead (after EXT-1 + Step 3 complete) | 0.4 is placeholder-tuned; real vectors may over- or under-retrieve. |
| EXT-5 | Redis (`ConnectionStrings:Redis`) | Devops / compose | In-process fallback. Cache and rate-limiter work; no multi-instance coordination. |

### Deferred (NOT in this wave)

- **P3-01-BE-13 `IAiBatchGateway` / `ClaudeBatchProvider`** (offline pre-generation for cold-start cache fill) — Phase 10.
- **P3-01-BE-14 `IAiUsageBudget` / daily-cap guardrail** — Phase 10.
- **Human-moderation review-gate / `PendingReview` workflow / invalidation triggers** — Phase 10 (P7-09-style).
- **`AiUsageLogs` DB persistence** — P7-11.
- **Full BL curriculum ingestion pipeline** (BL-01..05) — backlog.
- **OQ-7 confidence signal into `SafeAiResult`** — required before cache HITs serve (see AiResponseCache serving note above).

## P3-08 — Adjust difficulty adaptively (Adaptivity Engine) — added 2026-06-13 (branch `feat/P3-08-adaptivity-engine`)

Built **P3-08** in the **`Learning` module**. Full pipeline (db-migration → backend-feature → api-tester → security-auditor → reviewer PASS).

**What shipped**

- **`AdaptivitySignals` value object** — 5 input signals (AccuracyPct 0.0–1.0, AvgTimeSeconds ≥0, HintRate 0.0–1.0, RetryCount ≥0, MasteryPct 0.0–1.0) + boolean IsDefault flag (cold-start = true). Pure data contract.
- **`AdaptivityDecision` value object** — output: TargetDifficulty enum (Easy/Medium/Hard) + IsDefault flag. Immutable result.
- **`AdaptivityOptions` config** — bound from `Learning:Adaptivity:WeightedScoreBand`: `WeightAccuracy` (0.5 default), `WeightTime` (0.2), `WeightHint` (0.2), `WeightRetry` (0.1), `HighBand` (0.8), `LowBand` (0.5), `ExpectedAttempts` (2), `FatigueSignalWeight` (0.0, optional P3-13 hook). Loaded in `Learning.Domain/Services` layer.
- **`AdaptivityEngine` domain service** — pure static method `DecideDifficulty(signals, options) → AdaptivityDecision`. Weighted-score algorithm: `score = (accuracy × WeightAccuracy) + (normalizedTime × WeightTime) + ((1 − hint) × WeightHint) + ((1 − normalizedRetry) × WeightRetry)`. Time normalization: max(AvgTimeSeconds, 0) capped at 30s baseline (degradation guard; score floor = 0 if avg ≤ 0). Retry normalization: min(RetryCount, ExpectedAttempts) / ExpectedAttempts. Score-band mapping: score ≥ HighBand → Hard, score ≥ LowBand → Medium, else Easy. Cold-start (IsDefault=true) always → Medium + IsDefault=true. Deterministic, reproducible, monotonic.
- **`SkillAdaptivityAggregate` read model** — pure logic; computes AdaptivitySignals from student attempt history per skill. Queries: cumulative accuracy, avg time, hint rate, retry count, mastery %. Seeded by the write-path (P3-08 integration handler) to avoid N+1 on dashboard read.
- **`GetAdaptivitySignalsAsync(studentId, skillId)` repo aggregate** — reads from `StudentSkillMastery` (mastery %) + joins `StudentAnswer` to compute accuracy/time/hints/retries per skill. Scoped to the learner (auth via JWT `studentId`). Returns a fresh `AdaptivitySignals` struct for the skill.
- **`IAdaptivityService` in-process seam** (Application layer) — single method `GetTargetDifficultyAsync(studentId, skillId, skillSlotId) → AdaptivityDecision`. Wires the repo + engine; caches isDefault check. **Cross-module seam for P3-11** (adaptive quiz selection consumes this). Registered in `DependencyInjection.cs`.
- **Write-path integration: `CompleteAttemptCommandHandler` P3-08 hook** — after mastery is upserted (P3-09), handler calls `PrewarmAdaptivityCacheAsync` to compute + cache the AdaptivityDecision for each skill touched by the attempt. Fail-soft (no-op on cache miss; logging at Warn).
- **Inspection endpoint** — `GET /api/Learning/Adaptivity/Decision/{skillId}` (student-scoped, returns `AdaptivityDecisionDto`). Admin-debug endpoint `GET /api/Admin/Learning/Adaptivity/Signals/{studentId}/{skillId}` (returns raw signals + computed score). No write endpoints.
- **Resource strings** — AR/EN localization for TargetDifficulty display (SharedResourcesKey.cs + .resx files). "سهل" / "Easy", "متوسط" / "Medium", "صعب" / "Hard".

**Load-bearing decisions (reviewer-confirmed)**

- **Weighted-score formula is Q1 algorithm per the brief** — all 4 signals equally critical (non-zero weights); no subject/grade/domain variants (configurable at Global Settings P10-12 later).
- **Default weights (Accuracy 0.5 / Time 0.2 / Hint 0.2 / Retry 0.1)** — tuned for K–12 learner profiles (accuracy primary, time as tiebreaker, hints/retries as secondary); configurable at runtime via `appsettings.json`.
- **Cold-start = Medium + IsDefault** — no fallback to difficulty history or random (safest for unknown students).
- **Time normalization baseline = 30s** — typical quiz-question attempt window; degradation (AvgTimeSeconds ≤ 0) zeroes the time component (never negative).
- **Retry proxy = RetryCount** — defined as `TotalAnswers − 1` (coarse, not per-skill attempt count). **Tuning follow-up** (not a blocker): P3-08 should ideally read per-skill attempt count when available.
- **No DB table for adaptivity state** — engine computes on-demand from attempt history (no migration). Reads are O(1) if mastery is cached (P3-09 UpsertMasteryForAttemptAsync pre-seeds it).
- **P3-13 fatigue signal (optional)** — `FatigueSignalWeight = 0.0` by default. If P3-13 later adds a fatigue factor, the formula can include it: `score += (fatigueLevel × FatigueSignalWeight)`. Deferred.
- **Cross-module seam:** `IAdaptivityService` is the in-process interface for P3-11 (adaptive quiz selection) + P3-13 (student profile) to query difficulty without direct Learning module calls.

**Test coverage**

- **12 unit tests** (AdaptivityEngineTests.cs: cold-start, high/medium/easy scoring, monotonicity, reproducibility, weight variation, time-normalization edge cases). All green.
- **8 integration tests** (P3_08_AdaptivityEngine_Tests.cs: repo `GetAdaptivitySignalsAsync`, seam `IAdaptivityService.GetTargetDifficultyAsync`, write-path hook in `CompleteAttemptCommandHandler`, cache warming). All green.
- **273 unit tests** + 8 integration tests combined, full Learning module suite green.

**Security audit**

- PASS, 0 blocking/high findings. Endpoint auth: student reads are JWT-gated (studentId from token); admin debug endpoint is AdminOnly; no PII in signals/decisions.

**Next story dependency**

- **P3-11** (Serve adaptive quizzes) reads `IAdaptivityService.GetTargetDifficultyAsync(…)` to select question pool difficulty (Easy/Medium/Hard) → question filter/sort.
- **P3-13** (Build student profile) reads adaptivity signals to compute proficiency bands + time-vs-accuracy trade-off insights.
## Phase 7 — Gamification admin overrides (P7-13 backend) — added 2026-06-09 (`feat/phase-7-backend`, in wave PR #106)

Built **P7-13** in the **`Gamification` module** (commit `4b31fbc`). Scope = the story's **5 admin-config areas** (lead-confirmed; NOT per-student XP/hearts/badge-grant — those aren't in the story ACs): **league-tier override** (per student), **badge catalog CRUD** + activate/deactivate, **mission catalog CRUD** + activate/deactivate, **timed-event** write/transition, **streak-freeze grant**. All AdminOnly on `AdminGamificationController` (`api/Admin/Gamification`); each override takes a required `Reason`.

- **`BadgeDefinition`/`MissionDefinition`/`TimedEvent` promoted to `AggregateRoot`** (lead-approved DDD change, no schema impact) so they raise `AdminActionPerformedDomainEvent` → relayed to the integration event **post-commit** → Moderation audit (same pattern as Learning).
- **`IsActive` added to the badge + mission catalogs** (migration `P7_13_AddBadgeMissionIsActive`, existing rows backfilled active). **Deactivation actually takes effect** — the award engine (`GetAllBadgeDefinitionsAsync`/`GetBadgeDefinitionsByTriggerAsync`) and mission lazy-instantiation (`EnsureMissionsForPeriodAsync`) now filter `IsActive` (a security-audit High — without it, deactivate was a no-op). **No clawback** of already-earned badges/in-flight missions.
- **Seeders are now seed-if-absent** (`BadgeSeeder`/`MissionSeeder` no longer drift-correct on boot) so **admin catalog edits survive a re-seed**.
- Streak-freeze grant is **clamped at `MaxFreezes` (2)**; timed-event multiplier bounded 1.0–5.0 + window `start<end`; tier override sets only `StudentXpProfile.CurrentTier` (takes effect at next rollover — the risky `FinalizePromotion` call was removed per the audit). Admin `Reason` free-text is NOT persisted into the immutable audit `Details` (ids + values only).

**Phase 7 backend is now feature-complete for everything buildable.** P7-09 (moderation queue — the `Moderation` module is scaffolded + ready for it), P7-10 (analytics), P7-11 (AI-safety) remain BLOCKED on unbuilt upstream phases. All Phase-7 FE (admin-dashboard Next.js) is not started.

## Phase 10 Wave 1 — Billing security-gate fixes — 2026-06-15 (branch `feat/phase10-wave-1`)

Security-gate (api-tester 14/19 + security-auditor FAIL: 1 Critical, 2 High) remediated. **Do NOT commit — branch carries uncommitted W1 work.**

### Fixes applied

**Finding 1 (CRITICAL IDOR) — GetCreditAccountQueryHandler**
Endpoint `GET /api/Billing/Credits/{childId}` scoped only on the route param. Fixed: handler now derives `ICurrentUserService.UserId` + `ICurrentUserService.Roles` server-side. Admin (Admin/SuperAdmin) → allowed. Student → scoped to own `UserId` only (must equal `childId`). Parent → ownership verified via `IParentChildQuery.IsParentOfChildAsync` (Shared.Contracts cross-module seam). All mismatch cases return the same generic `NotAuthorizedForChild` 403 (anti-enumeration; does not distinguish "not found" from "not yours"). `IParentChildQuery` is registered at host level via `AddParentModule` — no new Billing DI registration needed.

**Finding 2 (CRITICAL functional 500) — GetGlobalSettingsQueryHandler**
`.Where(s => GlobalSettingKeys.ManagedKeys.Contains(s.Key))` where `ManagedKeys` is `IReadOnlySet<string>` → EF Core 10 can't translate to SQL → 500 on every GET. Fixed: materialize `var managedKeys = GlobalSettingKeys.ManagedKeys.ToList()` before the query; use `managedKeys.Contains(s.Key)` in the LINQ expression.

**Finding 3 (HIGH) — spoofable UpdatedBy**
`UpdateGlobalSettingCommand.UpdatedBy` accepted from request body → admin identity spoofable. Fixed: `UpdatedBy` removed from command + request DTO + validator. Handler derives it server-side: `_currentUser.UserName ?? adminUserId.ToString()`. Controller no longer passes it; `UpdateGlobalSettingRequest` no longer exposes it. Existing integration-test bodies that send `UpdatedBy` in the JSON are silently ignored by model binding (extra properties → dropped; tests still pass).

**Finding 4 (HIGH) — unaudited settings changes + divergent IAuditLogWriter seam**
`NoOpAuditLogWriter` registered as `IAuditLogWriter` in Billing DI was a dead-end no-op diverging from the established audit relay. Fixed: `NoOpAuditLogWriter.cs` deleted, `IAuditLogWriter` removed from Billing DI. Handler now publishes `AdminActionPerformedEvent` directly via `IPublisher` after commit (best-effort try/catch, mirrors the Gamification admin handler pattern). Added `AdminActions.GlobalSettingUpdated = "GlobalSetting.Updated"` to `Shared.Contracts/Admin/AdminActions.cs`. The existing Moderation `AuditLogEventHandler` (which already consumes `AdminActionPerformedEvent`) persists the audit row — no new consumer needed.

**Finding 5 (MEDIUM) — ex.Message leak**
All 8 credit handlers (`GetCreditAccount`, `ReconcileAccount`, `SpendCredit`, `GrantCredit`, `ApplyPurchase`, `Refund`, `Adjust`, `ExpireGrant`) returned `ServerError<T>(ex.Message)` exposing internal details. Fixed: all handlers now pass a generic localized message (`SharedResourcesKey.AnErrorIsOccurredWhileSavingData` for writes, `SharedResourcesKey.SystemErrorRetrievingData` for reads) + log the exception server-side via `_logger.LogError(ex, ...)`.

**Finding 6 (MEDIUM) — missing DB CHECK constraints**
`CreditAccountConfig` docs noted non-negative guards but migration lacked the DDL. Fixed: `ToTable(t => t.HasCheckConstraint(...))` added for `GrantedBalance >= 0` and `PurchasedBalance >= 0` in `CreditAccountConfig`. `InitialBilling` migration Up() updated to include `table.CheckConstraint(...)`. Designer + ModelSnapshot updated to reflect the new check constraints.

### Rate-limit deferral (Low finding)
Rate limiting on the credit balance GET (`GET /api/Billing/Credits/{childId}`) — DEFERRED. No implementation. Revisit when a load-test pass runs against production-candidate.

### Load-bearing notes
- `IParentChildQuery` is wired at host level; Billing Application references only `Shared.Contracts`. Module isolation preserved.
- `IPublisher` (MediatR) in `UpdateGlobalSettingCommandHandler` → discovered by the host's `AddCrossModuleMediatR` scan; no new DI registration needed.
- The `InitialBilling` migration is uncommitted — it already has the CHECK constraints in the edited file. If the migration has already been applied to a dev DB without the constraints, you will need to drop and re-apply or add a new `AlterBilling` migration.

## P10-13 Family Energy Wallet foundation (BUILT) — 2026-06-16 (branch `feat/P10-13-family-wallet`, stacked on PR #157)

**✅ COMPLETE — first implementation batch of the Family-Wallet wave** (brief `docs/briefs/P10-SEATS-WALLET.md`, plan `docs/plans/P10-SEATS-WALLET.md`). All acceptance criteria met; full gating pipeline (reviewer + security-auditor) PASS.

### What shipped

**Parent-owned `FamilyEnergyAccount` replaces per-child `CreditAccount`.** Energy is now two non-convertible buckets:
- **Allocation Bucket** — monthly subscription grant divided equally per child via `ChildEnergyAllocation` (no-cost transfer; default = `(PlanEnergyPerSeat × ActivePaidSeats) ÷ ChildCount`). Allocation overrides scoped per (family, child); read-only on the API (admin override deferred to P10-14+). Per-child soft cap via `ChildDailyUsage` (advisory; no enforcement).
- **Purchased Balance** — (unchanged) pack credits bought on parent payment intent, shared across the family.

**Spend sequence:** spend handlers now attempt allocation-bucket first → if exhausted, fallback to shared purchased balance. Monthly grant = `PlanEnergyPerSeat × ActivePaidSeats` (reuses `credits.{free,premium}_monthly` settings); the `BillingGrantJob` now operates on `FamilyEnergyAccount` instead of per-child rows.

**Admin operations re-homed:** family-wallet `Grant` (add balance) and `Reconcile` (audit/fix) endpoints replace the retired child grant/purchase/refund/adjust commands. Removed: `/api/Billing/Credits/{childId}/{Adjust,ApplyPurchase,Refund,ExpireGrant}` + `GetCreditAccount` query.

**`CreditAccount` fully retired.** One-time `CreditAccountMigrationService` (registered, run pre-launch):
1. **Provision:** iterates live `CreditAccount` rows (raw SQL), provisions a `FamilyEnergyAccount` per family + `ChildEnergyAllocation` per child (preserves purchased balance on shared bucket).
2. **Cleanup:** deletes migrated `CreditAccount` rows + `CreditTransaction` ledger (post-migration audit, `CreationAuditedEntity` timestamps preserved via SQL).
3. **Orphan guard:** fails loudly if a credit-account references a deleted/missing parent or child.

The three migrations are idempotent (unique index checks; fail-soft on re-run):
- `20260616140309_AddFamilyEnergyWallet` — new tables + configs.
- `20260616141037_MigrateCreditAccountsToFamilyWallet` — raw-SQL provision + delete.
- `20260616182410_DropLegacyCreditAccounts` — DDL drop (runs post-service cleanup).

**Temp seam:** `ISeatQuery` (Billing.Infrastructure) — a stub to count `ActivePaidSeats` — will be swapped for the real multi-seat model in P10-14. Currently returns a constant (configurable; defaults 1 per family).

### Verification

- **Build:** 0 errors; baseline .NET 10 + Npgsql + EF warnings unchanged.
- **Test suite:**
  - **P10-13 unit + integration (new):** 22/22 pass (wallet allocation, spend sequence, daily-usage, grant, reconcile, migration happy-path + orphan guard).
  - **Full P10 integration suite:** 151/151 pass (all money paths end-to-end: free trial → subscription → pack purchase → spend; refund integrity; gift credit; expiration).
  - **Billing module unit:** 93/93 pass (no regression).
  - **AI module unit:** 287/287 pass (untouched; baseline confirmed).
- **Security:** 0 Critical / 0 High. **Non-blocking follow-ups documented** (see below).
- **Option C service-only:** all data access behind `IFamilyEnergyAllocationService` / `IFamilyEnergyQueryService` + migration service; no EF in Application layer.

### Non-blocking security follow-ups

1. **MEDIUM — `FamilyEnergyController` `[Authorize]` role-gate.** `GET /api/Billing/FamilyEnergy/Overview` (parent-initiated wallet view) currently uses bare `[Authorize]`; restricts to authenticated-only. Should be gated to `Parent/Admin` role; non-parent 404 (anti-enumeration, no disclosure). **Recommended:** add method-level `[Authorize(Policy = RolePolicy.ParentOrAdmin)]` + handler IDOR check. Currently safe (intended for parent use only); upgrade before releasing to production.

2. **LOW — stale enum + localization keys.** `CreditReasonCode` still carries `CreditAccountNotFound` (retired); `ReconcileAccountQueryHandler` fallback 404 uses the stale `SharedResourcesKey.CreditAccountNotFound` localization key (should rename to `FamilyWalletNotFound` for consistency). No functional impact (enum value not exposed in API); update pre-merge or in a follow-up chore.

3. **NOTE — `/api/Billing/Credits/Spend` `childId` parameter lacks ownership check.** Spend handler accepts `childId` from route without verifying parent ownership (IDOR potential). **Mitigated by current access gating:** the `Billing.Create` permission is `SuperAdmin`-only → only super-admin can call Spend. If P10-14+ widens the permission, add a parent-ownership check in the spend handler.

### Load-bearing

- `ISeatQuery.GetActivePaidSeatsForFamilyAsync(familyId)` — stub returns constant (1 per family). P10-14 replaces with real multi-seat model from Identity.
- Migration service is **best-effort, run once, pre-launch** (not idempotent across multiple runs if deletes succeed; safeguard: check row counts before & after, log discrepancies). On production deployment, run the service **before API goes live** (downtime window or read-only mode).
- `BillingDbContext.CreditAccount` + `CreditTransaction` DbSets remain in the context (columns/table deleted by migrations); the ORM doesn't reference them post-migration (queries use `FamilyEnergyAccount` only). Safe to remove the DbSet in a future chore (not done now to avoid merge conflicts on stacked branches).

### Tracked follow-ups (non-blocking, NOT done)

1. **P10-14 — `ISeatQuery` → real multi-seat allocation model.** Swap the stub with identity-backed seat-query.
2. **P10-14+ — admin allocation override.** `PatchChildAllocationCommand` to set per-child allocation ≠ default.
3. **P10-15+ — batch operations.** Family-wallet admin `BulkGrant`, `BulkReconcile`, CSV import.
4. **Cleanup chore — remove stale DbSets + enum value + localization keys** (zero functional impact; polish pre-GA).


## P7-12 FIX (DONE) — 2026-06-15 (`fix/phase7-wave-1`)

The comprehensive "verify P7-12" pass found Bucket C still open + a latent UTC-serialization bug. Both fixed with all gates (reviewer, security-auditor, completeness-critic) passing.

### Bucket C: Curriculum admin creates now produce audit rows (with real entity id)

The Learning admin create handlers (Subject/Unit/Lesson) were **not raising `AdminActionPerformedDomainEvent`** — so admin curriculum creates produced no audit row. Fix: all three handlers now raise the event, carrying the **real created entity id** (not 0) via a new `ILearningRepository.FlushAsync(adminUserId)` seam:

- **Pattern:** handler calls `AddAsync(entity)` (stages for insert), then `FlushAsync(adminUserId)` (flushes the **open UoW transaction** — DB assigns `entity.Id`), then `RaiseDomainEvent` (event captures the now-real id). The UoW's later `SaveChanges` is a no-op; post-commit domain-event dispatch still fires (zero events on rollback per ADR-0001).
- **Impl:** `ILearningRepository` gained `FlushAsync(adminUserId)` (Application seam); `LearningRepository` calls `await _dbContext.Database.CommitTransactionAsync()` on the open transaction (idempotent — noop if already committed; fails if no enclosing txn — a useful safety guard).
- **PII:** Details string is light (action type, entity id, admin name only — no curriculum names/content).

### Bucket D: GetAuditLogQueryHandler normalizes OccurredAtUtc to UTC at the read boundary

The audit log materialized rows' `OccurredAtUtc` carried the **server-local timezone offset** (not UTC) — violating the P4-11 `EnableLegacyTimestampBehavior` convention. The API emitted a timestamp with server-local-kind instead of UTC-kind, misrepresenting the event's true instant. Fix: `GetAuditLogQueryHandler` normalizes each row with `.ToUniversalTime()` on the materialized list (read boundary, one place, matches the convention used in `Lifecycle/GetVersionHistoryQueryHandler`).

### LoggerManager.LogError now actually logs the exception

The interface `ILoggerManager.LogError(Exception?, string)` was not implemented — the exception param was silently dropped. Fixed: `LoggerManager.LogError` now passes the exception to the underlying ILogger, matching the signature and the intended contract.

### Verified

- **P7-12 audit suite:** 22/22 pass (IDEM-1 made parallel-safe via .WithoutResultTracking).
- **Full P7 integration suite:** 410/414 pass. The 4 remaining failures are confirmed **pre-existing** (AC-5 Bucket-E test-data, AC-LEAK-2 + Skill-CRUD Bucket-F, P7-07 SuperAdmin parallel-load flake) — **zero regressions**.
- **Gates:** reviewer PASS (0 blockers); security-auditor PASS (0 Critical/High); completeness-critic PASS (Bucket C + D fixed; follow-ups surfaced separately).

### Follow-ups (non-blocking, NOT done — surfaced by the completeness gate)

1. **Same `TargetEntityId=0` gap in 4 OTHER Learning create handlers:**
   - `AddSkillCommandHandler` (highest value; stages Skill + KnowledgeNode, must flush after BOTH).
   - `AddQuestionCommandHandler`, `AddContentBlockCommandHandler`, `AddKnowledgeEdgeCommandHandler` — apply the same `FlushAsync` pattern.

2. **Gamification create handlers (P7-13)** read `entity.Id` (=0) before commit:
   - `CreateBadgeDefinitionCommandHandler`, `CreateMissionDefinitionCommandHandler`, `CreateTimedEventCommandHandler` — real defect; fix in P7-13.

3. **Timestamptz DTO fields with the same Kind=Local/UTC bug** lacking `.ToUniversalTime()`:
   - Notifications `ListMyInbox`/`List` `CreatedAtUtc`.
   - Identity `GetAdminUserProfile` `StatusChangedAtUtc`.
   - Several Attempt/Mastery date fields (reference correct impl: `Lifecycle/GetVersionHistoryQueryHandler`).

4. **Pre-existing double `ILoggerManager` registration** in Identity `DependencyInjection.cs` (AddScoped + AddSingleton).

5. **Guard for `FlushAsync` safety:** consider a debug-time check so `FlushAsync` fails loudly if ever called outside a UoW transaction (prevents silent bugs if a future caller misuses it).


## Phase 7 — Audit log + Moderation module (P7-12 backend) — added 2026-06-09 (`feat/phase-7-backend`, in wave PR #106)

Built **P7-12** + the lead-approved **new `Moderation` module** + fixed the wave-wide audit-event timing (commit `5446a1d`).

- **New `Moderation` module** (4 projects, schema `moderation`, full UoW scaffold — ready for the future P7-09 moderation queue). All Host wiring done: `.sln`, `Program.cs` (`AddModerationModule` + `InitializeAsync`), **`MediatRExtensions.AddCrossModuleMediatR`** (the load-bearing one — without it the audit consumer silently never runs), `Host.csproj`, `Claims.GenerateModules()`. **`Directory.Packages.props` needed no change** (CPM already covers the packages).
- **Audit log** — `AuditLog` is **append-only/immutable**: inherits `CreationAuditedEntity` (no soft-delete/update columns) and the migration `P7_12_InitialModeration` installs a **Postgres trigger blocking UPDATE/DELETE** on `moderation."AuditLogs"` (DB-enforced, not just app-layer). Idempotent (unique `EventId` index) fail-soft `INotificationHandler<AdminActionPerformedEvent>` consumer. Read-only AdminOnly API `GET /api/Admin/Audit/Log` (DB-side filter/paginate, newest-first, NO mutation endpoints). **No PII** in audit rows — only ids + enum states + the PII-safe `Details` string.
- **✅ RESOLVED — the AdminActionPerformedEvent pre-commit follow-up.** The **32 Learning curriculum admin handlers** now raise an `AdminActionPerformedDomainEvent` on the tracked aggregate, dispatched **after commit** by `UnitOfWorkBehavior` (ADR 0002) and relayed to the integration event — so a rolled-back action can't write a phantom audit row. (Identity P7-06/07/08 handlers were already post-commit via eager `UserManager` commit — left unchanged.) `AggregateRoot.RaiseDomainEvent` was widened `protected`→`public` (accepted; convention: always source `AdminUserId` from `_currentUser`, never hardcode). **The earlier "fix AdminActionPerformedEvent to post-commit before P7-12" item in the two waves below is now DONE.**

**Load-bearing:** the audit log is **best-effort** (fail-soft consumer, no outbox per ADR 0002 §5) — a persistence failure drops a row with a `LogWarn`, it is not a guaranteed-complete ledger. Migration `P7_12_*` (incl. the immutability trigger) NOT applied — run `dotnet ef database update`. The test factory (`LearnexiaWebAppFactory`) was extended to override `ModerationDbContext` + apply its migration.

**Tracked follow-up (NOT done):** the audit-log **CSV/JSON export** endpoint (story AC #8, must use `Shared.Kernel.Storage.IStorageService` + a row cap) is deferred. The Identity read-path audit events (`UserViewed`/`UserSearched`) remain best-effort fire-and-forget (now decoupled from request-cancel).

## Phase 7 — User/account wave (backend) — added 2026-06-09 (`feat/phase-7-backend`, in wave PR #106)

Built the **backend** for **P7-06/07/08** in the **`Identity` module** (commit `7105c40`); same shared branch + wave PR #106 as the curriculum wave. Full pipeline per the plan (`docs/plans/P7-user-account-wave.md`, briefs `docs/briefs/P7-0{6,7,8}.md`); **security-auditor was mandatory** (user + child data) — 2 blocking Highs fixed before merge. Integration tests authored + compile-only (no Docker locally — run in ubuntu CI); 231 Learning unit tests stay green.

**Lead decisions locked this wave:** delete = **soft-delete only** (retain row + linked children + learning history; reversible); status via a new **`AccountStatus` enum {Active=0, Suspended=1, Deleted=2}** on `User` (migration `P7_07_*`, existing rows backfilled Active); suspend/delete **blocks future sign-in only (MVP)** — drops the Redis refresh token (`userrefreshtoken-{id}`) + tracked sessions, existing short-lived access JWTs expire naturally (the G2 gap, accepted); P7-08 grade override is **non-destructive** (preserves all progress, emits `ChildGradeChanged`, NO reset); admin views carry **minimal child PII**.

**What shipped (all `api/Admin/Users`, `AdminOnly`):**
- **P7-06** — `AdminUsersController`: DB-side paginated user search (free-text + role + `AccountStatus` filters, page-size ≤100), user detail, family (via new `IParentChildQuery.GetChildIdsForParentAsync`/`GetParentIdsForChildAsync` seams), activity summary (sign-in labelled "not tracked" — no `LastSignInAt` column). Lean list DTO omits child grade/nationality/language.
- **P7-07** — Suspend/Reactivate/Delete commands: state-machine + **self-protection** + **super-admin protection**; delete is **424-confirm-gated** + **cascades to children in one explicit `IIdentityDbTransaction`** (rolls back together); sign-in now gated on **both** `IsActive` AND `AccountStatus`. New `Shared.Contracts` `AccountSuspended/Reactivated/Deleted` integration events (no PII — ids + status only) with **fail-soft no-op consumers** in Gamification/Parent/Learning.
- **P7-08** — non-destructive `OverrideChildGrade` (emits `ChildGradeChangedIntegrationEvent`; the Learning consumer is a **no-op** because curriculum reads key off the request grade, not persisted grade-scoped state); admin `UpdateChildProfile`; admin learning-language change reusing the P8-04 service + 424 fresh-start. All P7-08 commands **reject non-child targets**.

**Load-bearing:** sign-in rejects `AccountStatus ∈ {Suspended, Deleted}` (resilient to `IsActive` drift) — keep both in sync on any future user-write path. `AdminActionPerformedEvent` Details + the `Account*` integration events carry **NO names/emails/free-text** (ids + enum states only) — preserve this when P7-12 builds the durable audit store. Migration `P7_07_AddAccountStatus` NOT applied — run `dotnet ef database update`.

**P7-07 Security fixes (branch `fix/P7-07-account-delete-cascade`, uncommitted — committer picks up):**

Three independent fixes landed on this branch:

1. **Transaction-nesting fix (original HIGH #1):** `DeleteAccountCommandHandler` no longer opens an inner `BeginTransactionAsync`. The `UnitOfWorkBehavior` already opens an enclosing EF Core transaction before invoking the handler; opening a second one on the same Npgsql connection caused a nested-transaction conflict (HTTP 500 on delete). Handlers now stage mutations only (matching the deferred-commit pattern); the UoW commits atomically. Cascade failures `throw` so the UoW's enclosing transaction is rolled back.

2. **Refresh-token account-status guard (Fix #2):** `RefreshTokenCommandHandler` now checks `user.IsActive == false || user.AccountStatus != AccountStatus.Active` immediately after the user lookup, before issuing new tokens. Previously a suspended/deleted account with a valid Redis refresh token could silently obtain new access tokens. Guard returns `Unauthorized(LoginAccountDeactivated)`.

3. **Post-commit domain-events buffer (approved fix, Security High #1 + Low #4):** Before this fix, Redis session revocation + `AccountDeletedIntegrationEvent` + `AdminActionPerformedEvent` ran inside the handler body, BEFORE the UoW committed. A commit failure would have revoked a live account's sessions, fired delete consumers, and written a phantom audit record. Fix: `IIdentityDomainEventsBuffer` (Scoped) buffers an `AccountDeletedDomainEvent` in the handler; `UnitOfWorkBehavior` drains it **after** `CommitAsync` only (success path). On rollback the scoped buffer is GC'd — no side-effects fire. The `AccountDeletedDomainEventHandler` (`INotificationHandler<AccountDeletedDomainEvent>`, in Identity.Infrastructure) performs: Redis revocation for parent + all children, `AccountDeletedIntegrationEvent`, `AdminActionPerformedEvent` — all best-effort/fail-soft.

**Files changed:**
- `Identity.Application/Events/AccountDeletedDomainEvent.cs` — new; the domain event record.
- `Identity.Application/Abstractions/IIdentityDomainEventsBuffer.cs` — new; buffer interface (Add + Drain).
- `Identity.Infrastructure/Events/IdentityDomainEventsBuffer.cs` — new; scoped in-memory implementation.
- `Identity.Infrastructure/Events/AccountDeletedDomainEventHandler.cs` — new; `INotificationHandler<AccountDeletedDomainEvent>`.
- `Identity.Infrastructure/Behaviors/UnitOfWorkBehavior.cs` — adds buffer injection + post-commit drain (per-event isolated try/catch, never rethrows).
- `Identity.Application/Features/Users/Commands/DeleteAccount/DeleteAccountCommandHandler.cs` — removes inline Redis/Publish calls; adds `_eventsBuffer.Add(new AccountDeletedDomainEvent(...))`.
- `Identity.Infrastructure/DependencyInjection.cs` — registers `IIdentityDomainEventsBuffer` as Scoped.
- `Host/Extensions/MediatRExtensions.cs` — adds `Identity.Infrastructure.AssemblyReference` scan so `AccountDeletedDomainEventHandler` is discovered by MediatR (mirrors Learning.Infrastructure + Curriculum.Infrastructure pattern).
- `tests/Modules.Identity.UnitTests/DeleteAccountDomainEventBufferTests.cs` — 5 new unit tests (happy-path buffer enqueue, confirm=false guard, self-protection guard, already-deleted guard, cascade child-ids).
- `tests/Modules.Identity.UnitTests/Modules.Identity.UnitTests.csproj` — adds Shared.Contracts reference for test project.

**Build: 0 errors. Unit tests: 10/10 pass (5 new + 5 existing). No migration required.**

**Tracked follow-ups (NOT done):** residual access-token window on suspend/delete (no `OnTokenValidated` hook — the G2 work, deferred); `AdminActionPerformedEvent` pre-commit publish for P7-01..05 curriculum handlers (wave-wide, fix before P7-12 consumer — the delete handler is now correct).

## Phase 7 — Curriculum wave (backend) — added 2026-06-08 (`feat/phase-7-backend`, wave PR #106)

Built the **backend** for the Phase-7 Admin Console **curriculum wave: P7-01..P7-05**, all in the **`Learning` module** (no new module; matches the gap-analysis brief `docs/briefs/phase-7-admin-gap-analysis.md` + per-story briefs `docs/briefs/P7-0{1..5}.md` + plan `docs/plans/P7-curriculum-wave.md`). Each story ran the full pipeline (db-migration → backend-feature → api-tester → security-auditor → reviewer → committer). **Integration tests were AUTHORED + compile-verified but NOT executed locally (no Docker on this Windows checkout) — run them in ubuntu CI.** Unit suite (231 Learning) stayed green throughout.

**Merge order (IMPORTANT — bottom-up):** the wave branch is based on the **auth-hotfix tip**, so **merge PR #104 first**, then the wave PR. (Hotfix = `fix/p7-learning-crud-authorize` / PR #104.)

**Commits on `feat/phase-7-backend`:** `77ea8b5` P7-01 · `21f0fe1` P7-02 · `955d803` P7-03 · `9f8ea41` P7-04 · `cf09798` P7-05.

**Auth hotfix (PR #104, separate, off `main`):** the pre-existing **Learning curriculum CRUD writes** (`Subjects/Units/Lessons/Concepts/Skills` Create/Update/Delete) were **live-unauthenticated on `main`** — added method-level `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` (mirrors `GradesController`); reads unchanged. `P2_01_CurriculumHierarchy_Tests` updated (anonymous-write contract superseded). **Merge #104 before the wave PR.**

**What shipped per story (all admin writes `AuthorizationPolicies.AdminOnly`):**
- **P7-01 subjects & units** — `AdminActionPerformedEvent` introduced in `Shared.Contracts/Admin` (PUBLISH-ONLY this wave; PR #85/#86 were docs-only — it did NOT exist before). `Subject.SequenceOrder`/`IsActive` + `Unit.IsActive`. Reorder + activate/deactivate (scoped per `(GradeId,SubjectCode,Language)` tree). Language-coverage query. **Soft-delete only** (lead decision) — Delete sets `IsDeleted`; global `IsDeleted != true` query filters added per entity; "parent not empty" guard blocks soft-deleting a parent with non-deleted children.
- **P7-02 lessons & content** — new **`ContentBlock`** entity (typed Text/Image/Video/Callout, `jsonb` Payload, per-type `ContentBlockPayloadValidator` incl. **https-only URL guard** + size bounds), `Lesson.IsActive`/`EstimatedMinutes`. Lesson soft-delete cascades to its content blocks. New `ContentBlocksController`. `EstimatedMinutes` kept out of student DTO.
- **P7-03 skill graph** — `Skill.IsActive`; **auto-creates a `KnowledgeNode` on skill create** (server-derived Subject/Grade); node-id-keyed `AddEdge`/`RemoveEdge` over the existing P2-11 `KnowledgeNode/KnowledgeEdge` with **cross-language (fail-closed) + acyclic (reuses `SkillGraphValidator`, Prerequisite-only) + duplicate** guards; admin `GetGraph`; skill soft-delete cascades node+edges. Student `StudentKnowledgeNodeDto` omits `IsSkillActive` + hides inactive nodes.
- **P7-04 quizzes & questions** — **implicit per-lesson** (NO `Quiz` aggregate, lead decision). `QuizQuestion.IsActive`/`SequenceOrder`. New `QuestionsController` (admin) wiring the previously-orphaned `QuizQuestionTypeValidation` (MCQ/TrueFalse/Matching/FillInBlank) + jsonb size bounds. `AdminQuestionDto` exposes `CorrectAnswer` **admin-only**; student `QuizQuestionDto` excludes it; student attempt path filters `IsActive`.
- **P7-05 publish/version/preview** — **per-entity** `LifecycleState` (Draft/Published/Archived) on Subject/Unit/Lesson/QuizQuestion (lead decision). **Existing rows backfilled `Published` (DB `DEFAULT 2`); new admin-created rows default `Draft` (C# initializer).** New `ContentVersion` snapshot table (monotonic version per entity). `ContentLifecycleController` (admin): Transition (publish/archive/unpublish, legal-transition guard, snapshot on publish), Rollback, VersionHistory, **Preview** (admin preview-as-student incl. draft), PublicationCoverage. **Student reads now filter `LifecycleState == Published`** across all 6 paths: `GetSubjectsForGrade`, `GetSubjectLessons` (+`LearningRepository.GetSubjectLessonsAsync`), `GetLesson`, `GetSubjectSkillTree`, `GetDashboard`, `StartAttempt`.

**Load-bearing model (do NOT regress):** curriculum student-reads now apply THREE filters — global `IsDeleted != true`, per-query `IsActive == true`, and per-query `LifecycleState == Published`. Admin reads include inactive + all lifecycle states (Preview surfaces Draft). Any new student curriculum read MUST apply all three. New EF migrations `P7_01..05_*` are generated but **NOT applied to any DB** — run `dotnet ef database update` (or the deploy migrator) before the API serves this branch.

**Locked lead decisions this wave:** Learning module only (no `assessment`/`Moderation` module yet); single `AdminOnly` policy (granular per-area deferred); soft-delete only; quiz implicit per-lesson; per-entity lifecycle; `AdminActionPerformedEvent` introduced now (publish-only). **A Moderation module was APPROVED by the lead for the later audit wave (P7-12) but NOT built this run.**

**Tracked follow-ups (carry forward — NOT done):**
1. **`AdminActionPerformedEvent` publishes PRE-commit** (inside the handler, before `UnitOfWorkBehavior` commits) across all P7-01..05 write handlers. Harmless now (no consumer), but **fix to post-commit before P7-12 builds the audit-log consumer** (else phantom audit rows on rollback). Wave-wide.
2. **P7-03 `AddEdge` acyclic-check race** — check-then-insert is not atomic; concurrent AddEdge of complementary edges could both pass the acyclic check (DB unique index only guards duplicates, not acyclicity). **Accepted for the single-admin phase** (documented in code); add a serializable transaction if concurrent admin editing becomes real.
3. **ACCEPTED RISK (lead):** the pre-existing **P2-07 instant-feedback** flow returns `CorrectAnswer` to students on a wrong answer (`SubmitAnswerResponse`). Intended pedagogy; left unchanged. Assessment-integrity tradeoff acknowledged.
4. **P7-05 `GetPreviewQueryHandler`** still uses `JsonSerializer.Serialize(entity)` on the full EF entity (no cycle today since no `Include`s) — switch to the same whitelist-snapshot shape as `CreateSnapshotAsync` for safety. One-sided publication-coverage flag is computed FE-side (backend surfaces per-slot `IsPublished`).
## QC — Phase-2 backend test pass (COMPLETE) — updated 2026-06-09 (`qc/phase-2-backend-continue` → PR, off main)

`qc-test-designer` (Opus) designed the catalogs (backend PR #107, frontend PR #108); the **backend api-tester pass is now COMPLETE + GREEN**. All 11 API-surface stories (P2-01..09, P2-11, P2-12; P2-10 seeder has no HTTP surface) implemented + RUN against Testcontainers.

- **Full P2 integration suite: 415 pass / 0 fail / 8 skip** (the 8 skips are documented harness-seam/fixture blocks). All `docs/qc/P2-*/execution-report.md` filled.
- **Root-cause fix — `LearningSeeder` now seeds Published+active demo curriculum** (`b6a17ca`). P7-05's `LifecycleState` default made new rows `Draft` → a fresh-seeded DB showed students NO curriculum (and broke the seeded-content tests). Seeder is seed-if-absent, so it publishes only newly-created rows and never clobbers admin Draft/Archived edits on re-run. **This also fixes real fresh-deploy behavior, not just tests.**
- **Phase-7 base-test regression fixed** — `P2_06/07/08` base tests built their own lesson/question fixtures as `Draft` and broke against Phase-7's `StartAttempt` `Published` filter; their fixtures now seed Published. `P2_01` base reads gated AdminOnly now thread the token; `P2_03` boss-count invariant scoped to Published units (cross-class state leak).
- **Frontend QC catalogs — PR #108** (`qc/phase-2-frontend`): `docs/qc/P2-*-FE/` (~208 FE-TC) still **NOT implemented** by `frontend-e2e-tester`.

**⚠️ Full-suite finding (separate, larger issue): 127 P7 integration tests are RED.** The `P7_01..13_*_Tests.cs` were authored compile-only during the P7 backend waves and **never executed** (no Docker then). Running the full suite now (Docker available) → **128 failures, ~127 of them P7 tests** (P7_05 ContentLifecycle, P7_04 QuestionsAdmin, P7_13, P7_02, P7_12, …) + 1 pre-existing `P1_13a` file-read fixture. This is **pre-existing on main, independent of the P2 work**, and is what keeps CI red. **Next QC target: a P7 backend api-test validation pass** (run → triage real-defect vs stale-test/fixture → fix to green), exactly like this P2 pass.

**Defects surfaced AND FIXED in this pass (commit `dc30d88`, reviewer PASS):**
- **DEFECT-1 (High) — RESOLVED:** 2nd subject with the same `(GradeId,SubjectCode,Language)` was → **HTTP 500**; now a clean **400/422** (handler `BadRequest` + a global middleware `DbUpdateException`-unique → 422 backstop). NOTE the QC mis-diagnosed the root cause: `SubjectCode`/`Language` were **always settable** on create (inherited from `SubjectDto`, mapped) — admins can create all 6 trees; they're now **immutable on Edit** (the `(GradeId,SubjectCode,Language)` natural key). The real bug was only the 500-on-duplicate.
- **DEFECT-2 (Med) — RESOLVED:** child-create with a non-existent parent FK → was **500**; now **404** via a parent-existence pre-check in the 5 Add handlers.
- **D-P2-05-01 (Med) — RESOLVED:** malformed JSON body → was **500**; now **400** (`BadHttpRequestException`→400 in the global error middleware + `SubmitAnswer` null-guard). ⚠️ The middleware change is **Host-level (all modules)** — validated safe against P1/P2/P4 (no regression); a full CI run is the standing gate.
- **Cross-language is NOT a 403 on browse/skill-tree** (design finding, P2-02/03/04): wrong-language `subjectId` **silently redirects** to the correct-language tree (200); only single-lesson GET returns 403. Lead: intended, or 403 uniformly?
- **No start-lock-guard** (P2-04/05/06): the unlock engine is advisory read-side only — starting a **locked** lesson/quiz returns 200. So on the FE the skill-tree locked-tap gate is the only lock enforcement.
- **Convention pins:** Learning-module IDOR → **401** (not 403/404); business-state failures → **424** (not 409).
- **Descoped:** P2-11 skill-graph authoring + cycle-detection has no HTTP surface (→ P7-03); cycle invariant is unit-tested only.
- **FE testID drought** across all (child) learning screens (subjects/tree/lesson/quiz/feedback/dashboard) — the Phase-2 FE e2e will need the same testID retro-fit Phase-1 got, plus an API seed seam for a *progressed* child (mixed node states / XP>0), before most FE cases can run.

## QC — Phase-1 backend test pass + defects — added 2026-06-07 (branch `qc/phase-1-backend`)

On-demand `qc-test-designer` (Opus) designed backend test cases for the 11 Phase-1 API-surface stories (P1-01/02/03/04/05/09/10/12/13/13a/13b; infra P1-06/07 excluded — no HTTP). Catalogs live in **`docs/qc/<StoryID>/`** (`README.md` coverage report + `backend-test-cases.md` + `execution-report.md`). `api-tester` then implemented them into `backend/tests/Learnexia.IntegrationTests/` and filled each `execution-report.md`.

**Combined P1 suite: 511 passed / 6 skipped / 1 failed (518).** The one RED is the intentional bug-documenting test (P1-03 `BE-TC-30`). ~24 cases are legitimately BLOCKED on missing test-harness seams (a `Production`-env host, throwing `IEmailSender`/`SignInManager` doubles, MinIO fault-injection, cross-boot re-seed) — documented per story, none faked.

**Defects surfaced (feature code was NOT changed by api-tester — these are for `backend-feature`):**
- **🔴→✅ RESOLVED (gated) — P1-05 / `GradesController`** (`api/learning/Grades/*`): was fully anonymous (unauthenticated Create/Update/Delete of curriculum). **Lead decision (2026-06-07): gate it.** Now class-level `[Authorize]` (reads = any authenticated user) + `[Authorize(Policy = AdminOnly)]` on Create/Update/Delete. The 16 `P2_01_CurriculumHierarchy_Tests` that asserted anonymous-by-design were **rewritten to the new contract** (grade reads now need a JWT → 401 anonymous / 200 authenticated; grade writes thread a `superadmin` admin token; the 5 *other* curriculum list endpoints — Subjects/Units/Lessons/Concepts/Skills — remain anonymous, unchanged). P1-05 BE-TC-20 tests restored to assert the corrected 401/403/200. **Note for P2-01:** its story AC said "curriculum endpoints anonymous" — that's now superseded for *grades* only; update the P2-01 story doc if it's treated as canonical.
- **🟠 Missing length bounds → unhandled 500** (should be 422/400): `AddChildCommandValidator.FullName`/`Country` (DEF-P103-01, the RED test) and `RegisterParentCommandValidator.Email` (oversized → DB `varchar` overflow → 500). Add `MaximumLength`. Also register **malformed JSON body → 500** (null command dereference) should be a 400.
- **🟠 P1-09 — `preferredLanguage` round-trip**: stored/returned as BCP-47 `ar-EG`/`en-US` (via `IdentityChildAccountService.NormalizeLanguage` + hard-coded defaults) but Add-Child accepts and the FE contract expects 2-letter `ar`/`en`. **Lead decision:** normalize to 2-letter, or change the FE contract. (Deferred — not auto-fixed; wider data/contract impact.)
- **🟡 P1-13a — `Notifications` (lead-intent, deferred):** (1) `UpdateMyNotificationPreferencesCommandValidator` does NOT require all 4 categories — a single-category PUT returns 200; (2) `SendNotificationCommand` is `IRequest<Result>` not `ICommand<>`, so `ValidationBehavior` never fires → `POST /api/notifications` doesn't 422 on empty Title/Body, returns bare 202 (not a `BaseResponse` envelope). Confirm intended contract before changing.
- **Confirmations (no action unless product wants it):** post-Sign-Out the access JWT stays valid until expiry (no blocklist/security-stamp revocation); 429 responses carry no `Retry-After` header; lockout fires on attempt **5** (`MaxFailedAccessAttempts=5`, no off-by-one).

## Testing — E2E (Playwright) — added 2026-06-07 (PR #99, not yet merged)

New runtime UI-testing stage for the **student-app web PWA**, the frontend analog of `api-tester`. Lives on branch `chore/e2e-playwright-harness` (off `main`).

- **Agent `frontend-e2e-tester`** (`.claude/agents/frontend-e2e-tester.md`): drives the running Expo **web** build with Playwright (user flows, RTL ar/en, form/`BaseResponse` validation, auth/role routing, states). Tests only — files bugs back to `frontend`, results feed the `reviewer` gate. Wired into the CLAUDE.md pipeline: `frontend` → **`frontend-e2e-tester`** → `reviewer` (workflow step 4 + reviewer-gate inputs). Runs for stories with a student-app UI surface.
- **Harness `tests/e2e/`** (`@learnexia/e2e`, added to `pnpm-workspace.yaml`): `playwright.config.ts` → `baseURL` http://localhost:8081, `chromium` + `mobile` (Pixel 7) projects, trace/screenshot/video on failure, Playwright-owned Expo `webServer` (reused if already up). `specs/smoke.spec.ts` is a deliberately locale-agnostic runway (boot + login input) — story specs go in `specs/<StoryID>.spec.ts`.
- **Run recipe** (`tests/e2e/README.md`): backend at `:5080` is a prerequisite (NOT auto-started — needs the Postgres stack); `pnpm --filter @learnexia/e2e install:browser` once, then `pnpm --filter @learnexia/e2e test`. Playwright starts/owns Expo web at `:8081`.
- **Selector convention:** `getByTestId` first (RN Web maps `testID`→`data-testid`), then `getByRole`/`getByLabel` (`accessibilityRole`→`role`, `accessibilityLabel`→`aria-label`). Avoid copy-based selectors — **Arabic is the default locale**. Auth screens (login/register) currently carry `accessibilityLabel` (i18n-keyed) but **few `testID`s** — when a flow lacks a stable hook, the agent reports the needed `testID` back to `frontend` rather than reaching into CSS.
- **Status:** harness now **runs end-to-end** — smoke specs pass (2/2) against the live stack. No CI job wired yet.
- **⚠️ Sandbox/WSL e2e run recipe (load-bearing — without these the harness can't start):**
  1. **Node 20, not 24.** Repo `.nvmrc` pins 20; Expo SDK 52 / Metro hangs under Node 24. `export NVM_DIR="$HOME/.nvm"; . "$NVM_DIR/nvm.sh"; nvm install 20 && nvm use 20`.
  2. **`EXPO_OFFLINE=1`** when starting Expo. The sandbox can't reach Expo's API host, so `expo start`'s startup dependency-version fetch throws `TypeError: fetch failed` and crashes before binding `:8081`. `EXPO_OFFLINE=1` skips it. Start: `cd apps/student-app && EXPO_OFFLINE=1 CI=1 npx expo start --port 8081 --web` (first web bundle ~50s).
  3. **Playwright browser:** `cdn.playwright.dev` IS reachable → `npx playwright install chromium` works. A pre-existing `~/.cache/ms-playwright` build may be incomplete — reinstall if launch complains about a missing executable.
  4. **Chromium system libs without root:** no passwordless sudo, so `playwright install-deps` can't run. The headless shell only needs 4 libs. Fetch+extract to userspace: `apt-get download libnspr4 libnss3 libasound2t64` → `dpkg -x *.deb ~/.local/chromium-libs` → run Playwright with `export LD_LIBRARY_PATH="$HOME/.local/chromium-libs/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH"`.
  5. **Reuse the running server:** config `webServer.reuseExistingServer = !process.env.CI`. With Expo already on `:8081`, run `playwright test` **without `CI` set** so it reuses it (setting `CI=1` makes Playwright spawn its own Expo and fail offline).
  - Backend for the e2e run: `docker compose -f docker/docker-compose.yaml up -d postgres minio` then the Development `dotnet run` recipe above (migrates + seeds a fresh DB on first boot). `.env.local` already has `EXPO_PUBLIC_API_BASE_URL=http://localhost:5080` **and a real `EXPO_PUBLIC_GOOGLE_CLIENT_ID`** (so the P1-12-FE "Google disabled/unset" QC assumption is wrong locally — the button is enabled).

## QC + E2E test pass — Phase 1 (report summary, 2026-06-08)

A dedicated **`qc-test-designer`** (Opus, design-only) + **`api-tester`** (backend) + **`frontend-e2e-tester`** (Playwright web) pass over all of Phase 1. Catalogs live in `docs/qc/<StoryID>/` (backend) and `docs/qc/<StoryID>-FE/` (frontend) — each holds a README coverage report, the test-case catalog, and a filled `execution-report.md`. Two open PRs:

- **Backend — PR #100** (`qc/phase-1-backend`): QC catalogs + executed integration tests for the 11 API-surface stories (P1-01/02/03/04/05/09/10/12/13/13a/13b; infra P1-06/07 excluded). **Combined suite 511 pass / 6 skip / 1 fail (the 1 fail was a deliberate bug-doc test, since fixed).** Robustness fixes shipped: oversized/malformed inputs now 422/400 (were 500), and **`GradesController` gated** (`[Authorize]` reads + `AdminOnly` writes — lead-decided; 16 P2-01 anonymous-curriculum tests reconciled).
- **Frontend — PR #105** (`qc/phase-1-frontend`): QC catalogs + Playwright specs for the 7 student-app stories (P1-01/02/03/04/09/11/12-FE; admin P1-10-FE excluded — Next.js, not on the student-app harness). **First-ever end-to-end e2e run** (cleared the 4 sandbox blockers in the recipe above). **Net ~173 pass / 0 fail / ~19 blocked** (blocked = genuinely undrivable headless: Google OAuth dialog, real reset-token email, server token-lifetime control, native RTL restart, the Next.js Landing on its own server, placeholder Reports/charts). Student-app hardened for testability (testIDs across all screens, `aria-checked`/`aria-busy`, locale persisted to `localStorage`).

**Bugs the e2e pass found and FIXED on PR #105 (verified):**
- 🔴 **Route/role guards were missing on the group layouts** (only the splash guarded) — a signed-out user could reach `/add-child` and a signed-in **child** could reach `/children` (parent data). Fixed via a shared `useGroupGuard` on `(onboarding)/(parent)/(child)` `_layout.tsx`.
- **Child-login locale** didn't apply `Me.preferredLanguage` (compounded by the `ar-EG`/`en-US` vs 2-letter mismatch) → wrong `html[dir]`. Fixed (eager `applyWebDirection` + BCP-47 base-subtag normalization).
- **`useAddChild`** didn't invalidate the `myChildren` cache. Fixed.
- **backend `Link-Child`** returned 200 re-linking an already-linked child → now **409** (`ChildAlreadyLinked`). Verified live; xUnit test updated.

**Still-open lead decisions (NOT fixed — documented, see backend defect list):** P1-09 `preferredLanguage` is stored/returned as `ar-EG`/`en-US` at the source (FE now normalizes; a backend fix to 2-letter is the cleaner long-term call); P1-13a notification-prefs "all-4" rule + `POST /api/notifications` validation; post-sign-out JWT revocation; 429 `Retry-After`.

**⚠️ Env gotcha hit during the run:** the WSL **Docker daemon control plane went unresponsive** after hours of Metro/dotnet/Chromium/Testcontainers (running containers kept serving, but `docker ps` / Testcontainers hang). Effect: the backend P1-04 **Testcontainers** integration test for the 409 couldn't execute (fix verified via live API + FE e2e instead). **Restart Docker Desktop** to recover, then `dotnet test --filter P1_04`. CI is unaffected.

## ⭐ Frontend state reconciliation (2026-06-06) — READ FIRST; supersedes stale wave statuses below

A full audit of `main` vs. the per-wave notes below (which were written when each wave was "ready for PR") found those notes badly behind. **Ground truth as of `main` HEAD:**

- **MERGED to main (done):** monorepo foundation + `design-system`/`ui`/`api-client`/`shared` packages · **Phase-1 student FE** (P1-01/02/03/04 auth + parent onboarding + add-child + child login/`/Me` routing) · **admin sign-in shell P1-10** · **Phase-2 student FE** — P2-02/03 browse + skill-tree (PR #70), P2-05/06/07 lesson player + 4-type quiz + instant feedback (PR #71→#74), P2-09 home dashboard (PR #72→#74), P2-12 settings (PR #69). The **"Wave 11/12/13 … ready for PR / PR pending"** headers further down are STALE — all three are **merged** (#70/#71/#72/#74).
- **PARTIAL:** P1-11 parent web (screen set built; `(parent)/index` + `reports` are intentional blank placeholders) · P4-07 FE (only the dashboard `LeaguePreviewRow` flip merged; full league screen pending).
- **NOT started (carry-forwards):** **P1-12 FE** (profile save / avatar upload / Google OAuth / password-reset — all stubs) · **P8-04 FE** — ⚠️ board previously showed P8-04 FE ✅ but the `feat/P8-04` branch was **backend-only**; the parent change-learning-language UI (with the fresh-start reset warning) was **never built**. PROGRESS.md corrected to 🔲.
- **App-side localization (axis A) is PARTIALLY wired, NOT "not started"** (older notes calling it greenfield are wrong): react-i18next is initialised in `app/_layout.tsx`, ar/en resources live in `packages/shared/src/i18n/resources.ts` (~1150 lines, Arabic-first), RTL helpers + native-restart UX exist. **Real gaps:** brand fonts have no runtime loader (no `expo-font`/`useFonts` in student-app — Cairo/Tajawal may not render), the UI-language switch exists only on Login and persists only to local Zustand (not `User.PreferredLanguage`), and the learning-language (axis B) parent UX is absent (= the P8-04 FE gap).
- **Open-WIP FE branches:** `feat/P4-08-gamification-screens-motion` (motion infra + confetti + gamification hooks + a 1,140-line design spec; **resumable** — rebase onto main first) and `feat/design-system-pixel-align` (**stale, 97 behind**; carries the missing brand-font runtime loaders + RTL/`Switch` fixes — review before the localization wave, don't delete blindly). Merged/stale branches `feat/W11-…`, `feat/W12-…`, `feat/P1-11-login-code`, `fix/student-app-web-bootstrap`, `claude/phase2-backend-wave7-*`, `docs/phase-8-localization` are safe to delete (pending lead OK).
- **Recommended next FE:** Phase-3 gamification screens (start P4-02/03 XP+streak — fully unblocked, BE merged, primitives exist), and in parallel an **analyzer/planner scoping pass on app-side localization + the missing learning-language parent UX** (no FE task files exist for it yet).

## Localization FE wave (in progress, 2026-06-06)

Task files: `tasks/Frontend/student-app/Phase-8-Localization/{P8-99,P8-01,P8-04}-FE.md`; brief `docs/briefs/P8-localization-FE.md`; plan `docs/plans/P8-localization-FE.md`. Build order P8-99 → P8-01 → P8-04, strictly sequential, one PR per story (stacked).

- **P8-99-FE-0 (api-client regen) — DONE.** Re-pulled swagger from the running backend + `gen:api`. The client now exposes the P8 surface (`changeLearningLanguage`, `confirmFreshStart`, `learningLanguage` on `AddChildCommand`/`MeResponse`).
- **Durable NSwag `/Me` fix (lead-approved, BACKEND) — DONE.** The committed snapshot was P4-04-era; the fresh regen surfaced 5 `/Me` routes (Users/Badges/Leagues/Missions/Inbox) that NSwag was disambiguating **positionally** (`me`/`me2`/…), so the typed `Users/Me` binding had silently become `me5()`/void. Fix: `AddSwaggerGen` `CustomOperationIds = {ControllerName}Me` for `/Me` routes (in the **Host** — modules untouched) → stable `usersMe()`/`badgesMe()`/… ; added the missing `[ProducesResponseType]` on Badges/Me + Missions/Me. **If you regen the client, keep this** — without it the `/Me` methods regress to brittle positional names. FE drift fixes from the regen: `useMe`→`client.usersMe()`; `DailyMissionDto`→`MissionSummary` re-export (P4-06 rename). `admin-dashboard/types/assets.d.ts` added so the admin app typechecks design-system's `.ttf` font source.
- **P8-99-FE (app-shell foundation) — MERGED (PR #93).** Brand-font runtime loading (cherry-picked from `feat/design-system-pixel-align` 328ef06) + web Arabic font stacks (`Poppins, Cairo`/`Poppins, Tajawal`); RTL fixes on parent web; persisted UI-language switch (`LanguagePanel` + `useUpdateUserLanguage` → `User.PreferredLanguage`, native-restart UX); i18n key sweep.
- **P8-01-FE (add-child learning-language field) — MERGED (PR #94).** Required `learningLanguage` (ar/en) field on the add-child onboarding form (`AddChildForm`), fenced from the relabeled "App language" field (labelled group + helpers + ordering), auto-fills the child's UI language to match (guarded by `appLanguageTouched`, still editable), sent via `useAddChild` → `AddChildCommand.learningLanguage`. Design spec `design-system/ui_kits/onboarding/P8-01-FE.md`.
- **P8-04-FE (parent change-learning-language + fresh-start warning) — done, PR open.** Branch `feat/P8-04-FE` off main. Per-child "Learning language" row in `LinkedChildrenPanel` (shows current language + ghost Change); a focus-trapped `ChangeLearningLanguageModal` (mirrors `EditChildSheet`) with from→to restatement + a required `CheckboxField` acknowledgement gating an ad-hoc `$danger` Confirm that maps to `confirmFreshStart: true`; no-op guard when same language; new `useChangeLearningLanguage` hook (invalidates myChildren); 403/424 error handling; no celebratory motion (it's a reset). **Backend (lead-approved):** `learningLanguage` added to `ChildProfile`/`LinkedChildResponse` (via the `IChildAccountService` seam — module isolation preserved) so the parent UI can show/guard the current value; api-client regenerated (surgical 1-line add). Typecheck 9/9 green; **blocking security-auditor PASS — 0 blocking** (IDOR clean: parent from JWT + `IsLinkedAsync` before mutate; `confirmFreshStart` enforced server-side → 424; reset scoped to the child's MATH/SCIENCE only); reviewer PASS (minor non-blocking nits noted). Design spec `design-system/ui_kits/parent-settings/P8-04-FE.md`.
- **Wave status:** localization FE wave is **feature-complete** (P8-99 + P8-01 merged; P8-04 PR open). Known non-blocking follow-ups: brand fonts not render-verified on native; native RTL reload not verified in headless dev; a Lucide icon set isn't wired (P8-04 danger chip uses a glyph); the durable `/Me` `CustomOperationIds` must be kept across future api-client regens.

## P1-12-FE wave — web account features (in progress, 2026-06-06)

The Phase-1 account carry-forward (profile/avatar, password reset, Google OAuth, register consent, edit-child). Brief `docs/briefs/P1-12-FE.md`; plan `docs/plans/P1-12-FE.md` (**3 stacked PRs**); design spec `design-system/ui_kits/student-app/P1-12-FE.md`. Backend (P1-12 BE) is merged; the api-client already exposes every endpoint (no regen needed).

- **Batch 1 (foundation + avatar + register-consent) — MERGED (PR #96).** Transport fix: `Google-SignIn`/`Forgot-Password`/`Reset-Password` added to `ANONYMOUS_PATH_SUFFIXES` (unauthenticated — must NOT carry the bearer). 5 api-client hooks (`useUploadAvatar`/`useRemoveAvatar` consumed; `useGoogleSignIn`/`useForgotPassword`/`useResetPassword` foundation). Avatar upload/remove in settings (web-only `<input type=file>`, PNG/JPG + 5 MB cap, direct `$danger` remove; native deferred). RegisterForm posts `acceptedTerms` (default false) + `country`.
- **Batch 2 (password-reset screens + edit-child on web) — MERGED (PR #97).** `(auth)/forgot-password.tsx` (generic anti-enumeration) + `(auth)/reset-password.tsx` (`?email=&token=`, status-only token classification, token never logged). Edit-child: `ChildDashboardCard` affordance + `EditChildSheet` dual-mode slim edit → `useUpdateChild`. Backend: `LinkedChildResponse` gains `Grade`/`Language`(short ar/en)/`Country` via the `ChildProfile` seam (real-value pre-fill, no data-loss). Edit-child IDOR closed server-side.
- **Batch 3 (Google OAuth + RTL/dark QA) — done, PR open** on `feat/P1-12-FE-batch3`. Google sign-in via `expo-auth-session` (`useAuthRequest` ResponseType.IdToken + `useAutoDiscovery`); client ID from `EXPO_PUBLIC_GOOGLE_CLIENT_ID` (button gracefully disables if unset, `__DEV__`-guarded warn). On success the `id_token` → `useGoogleSignIn` → **the same `authStore.setTokens` + routing as email sign-in** (no separate token path). Apple/MS = dimmed placeholders. Added deps `expo-auth-session`/`expo-crypto`/`expo-web-browser`/`expo-application` (SDK-52-compatible). RTL/dark QA sweep across the wave's surfaces (Google label stays Latin LTR; tokens-only). Typecheck 9/9 green; security-auditor PASS (0 blocking); reviewer PASS.
  - ⚠️ **Backend coordination (REQUIRED for Google sign-in to work end-to-end):** the backend must set `GoogleAuth__ClientId` to the SAME web client ID as the FE's `EXPO_PUBLIC_GOOGLE_CLIENT_ID` — `GoogleTokenValidator` validates the idToken's **audience** against it; a mismatch rejects every Google sign-in. The actual client ID lives only in the gitignored `apps/student-app/.env.local` (it's a public web client ID, not a secret).
  - ⚠️ **Tracked pre-production debt (lead-accepted):** the OAuth flow uses Google's **implicit `id_token` grant** (token in the redirect fragment), which OAuth 2.1 deprecates. It matches the current backend (idToken-validation) contract and works today. Before production, consider migrating to `ResponseType.Code` + PKCE with a **server-side code exchange** (requires reworking the backend Google handler + the client secret). Not done now (cross-stack rearchitecture beyond this FE wave).
- **Lead-resolved decisions:** Google **only** (Apple/MS = dimmed placeholders); Google client ID via **env**; reset deep-link `?email=&token=`; avatar web-only this story; edit-child slim edit-only set; CAPTCHA out of scope (P1-11-FE-16).
- **Wave status:** P1-12-FE is **feature-complete** (Batches 1+2 merged; Batch 3 PR open). Remaining: set the backend `GoogleAuth__ClientId`; the implicit-grant migration above; native avatar picker + native OAuth client IDs (deferred); the 3 nit follow-ups (unused `tokenExpired`/`social.loading` i18n keys, glyph icons pending a Lucide wiring).

## Phase 8 — Localization (backend COMPLETE, merged to main, 2026-06-05)

Learning language (medium of instruction) vs UI language vs subject content language. **Design of record: `docs/architecture/localization-architecture.md`** (image-first SVG diagrams + Mermaid source). Stories/tasks: `user-stories/Phase-8-Localization/`, `tasks/Backend/Phase-8-Localization/`. Briefs/plans: `docs/briefs/P8-localization.md`, `docs/briefs/P8-04.md`, `docs/plans/P8-localization.md`, `docs/plans/P8-04.md`.

**Shipped + merged to main:**
- **P8-01/02/03** (docs PR #88, impl **PR #90**) — `User.LearningLanguage` (ar/en, **separate** from `PreferredLanguage`, **immutable by the student**) + `learning_language` JWT claim (emitted in `AuthenticationIdentityService.GetClaims`, re-issued on refresh) + required at add-child + returned on `/Me`. `Subject.SubjectCode` (MATH/SCIENCE/ARABIC/ENGLISH) + `Subject.Language` (ContentLanguage Ar/En) + UNIQUE index `(GradeId,SubjectCode,Language)`. `LearningSeeder` rewritten to **6 language-tagged roots per grade** (Math/Science ×2 langs, Arabic=Ar, English=En; per-language Math prereq graphs) via a **destructive re-seed migration `P8_02_AddSubjectCodeAndLanguage`** (lead-approved wipe of demo curriculum to enable the unique index). `SubjectLanguageResolver` (pure static) + `LearningLanguageClaimAccessor` (reads JWT claim, **fallback Ar + warn, never 500**) + language filter/guard across the **6 read handlers** (subjects-for-grade, skill-tree, lessons-in-unit, lesson, start-attempt, dashboard); cross-language lesson/attempt access → **403**. `StudentSubjectDto.SubjectCode` exposed. Security fixes: `[Authorize]` on `GetForGrade` + deprecated `/Lessons?id=` route; removed `ex.Message` leakage in 3 handlers.
- **P8-04** (**PR #91**) — parent-only, family-scoped, **confirm-gated** (`confirmFreshStart` false/absent → **424** BusinessValidation, enforced first in the handler) change of a child's `LearningLanguage`. Publishes `LearningLanguageChangedIntegrationEvent` (Shared.Contracts/Identity) post-commit (best-effort, mirrors `PublishUserRegisteredEventAsync`). Learning consumer (`IntegrationEventHandlers/LearningLanguageChangedIntegrationEventHandler` → internal `ResetMathScienceProgressCommand` through UoW) **hard-deletes** the child's Math/Science `Attempt` rows (StudentAnswer cascades); **Arabic/English progress + all gamification (XP/streak/badges/level) retained**; same-language request = no-op success.

**Resolution rule:** `EffectiveLanguage(SubjectCode, learnerLang)` = ARABIC→Ar, ENGLISH→En, MATH/SCIENCE→learnerLang. Arabic-medium student sees Math(Ar)/Science(Ar)/Arabic(Ar)/English(En); English-medium sees Math(En)/Science(En)/Arabic(Ar)/English(En). Both school types take **both** language subjects; Arabic/English subjects are pinned to their own language.

**Locked lead decisions:** LearningLanguage separate + immutable-by-student (parent-only change with explicit fresh-start warning, rare/start-of-year); curriculum = **parallel ar/en trees keyed on Subject** (NOT per-row translations); **hard-delete** on fresh-start (no soft-delete); reset scope = all the student's Math/Science attempts; gamification retained.

**Verification:** build green; **231 Learning + 2 Identity unit tests**; **670/670 integration tests** (real Postgres). Reviewer PASS; security PASS (P8-specific) with 2 pre-existing platform Highs carried (below).

**⚠️ Test-infra gap (next agent — being fixed):** the integration suite needs a **Postgres on `localhost:5432`** (`postgres`/`admin`/`Learnexia`) for **Hangfire** storage *in addition to* Testcontainers for EF, because `LearnexiaWebAppFactory` overrides the 5 EF DbContexts but NOT the Hangfire connection (`Program.cs:104` uses the `Default` string). Local workaround: `docker run --name lx-hangfire-pg -e POSTGRES_PASSWORD=admin -e POSTGRES_USER=postgres -e POSTGRES_DB=Learnexia -d -p 5432:5432 pgvector/pgvector:pg16`. Planned fix: `appsettings.Testing.json` or override Hangfire in the factory.

**⚠️ Stacked-PR lesson:** PR #89 (P8 impl) was stacked on PR #88 (docs base); #88 merged to main **first**, then #89 merged into the already-merged docs branch → impl **stranded off main**. Recovered via **PR #90** (`feat/P8-localization → main`). **Rule: merge stacked PRs bottom-up (code before base), or target all PRs at `main` independently.**

**Pre-existing platform Highs (NOT P8; carried/accepted, being addressed in a hardening pass):** JWT `CHANGE_ME` placeholder secret in `appsettings.json` (guarded for prod/staging by `GuardJwtSecret`); Newtonsoft.Json 11.0.1 CVE (GHSA-5crp-9r3c-p9vr) in `Gamification.Api`/`Gamification.Infrastructure`.

**Remaining localization work:** the **frontend i18n phase** (app-side react-i18next/RTL per `docs/architecture/localization-architecture.md` §1 axis A) is not started.

---

## Catalog module REMOVED (2026-06-03)

The demo **Catalog** module (Products/Categories + the `DEMO_PgvectorProof` migration) has been **deleted entirely** — all 4 src projects, the `Modules.Catalog.UnitTests` project, the `Shared.Contracts/Catalog` integration event, and its solution entries. Host wiring (`Program.cs`, `Learnexia.Host.csproj`, `MediatRExtensions`), the `"Catalog"` entry in Identity `Claims.GenerateModules()`, and all integration-test references (`LearnexiaWebAppFactory`, `P1_07`, `P1_12_BE4/BE5`, `P1_13_BE4/BE1`, `P4_01` smoke T3) were updated to drop it. `P1_05_RBAC_Tests` kept its Identity/Parent RBAC coverage and dropped the Catalog Products/Categories cases (the "real HTTP 401" envelope test was repointed to `Authorzation/RoleList`).

- **Reference module:** Catalog was the documented canonical reference. There is no named replacement — mirror an existing module (e.g. **Learning**) for new backend work.
- **pgvector:** no remaining module needs the `vector` extension (see the pgvector note below) — the image stays pinned only to match staging/prod.


## P3-09 — Track per-skill mastery / Student Modeling Engine (backend) — added 2026-06-13 (`feat/P3-09-student-mastery-engine`)

Built **P3-09** in the **`Learning` module** (commit pending); foundation of the adaptivity cluster. Full pipeline complete (db-migration → backend-feature → api-tester → security-auditor → reviewer PASS).

**What shipped:**
- **`StudentSkillMastery` entity + EF config** — per-student per-skill row (natural key `StudentId + SkillId`) with:
  - `MasteryStatus` enum (Novice / Learning / Proficient / Mastered) — read-path state machine
  - `CumulativeAccuracy` (0.0–1.0) — **same formula as P2-04** (no divergence): `CorrectCount / TotalCount`
  - `MasteryThreshold` hardcoded per-status; status Mastered = accuracy ≥ threshold; status NeedsReview = floor 50% (lead-confirmed range guard)
  - `ReviewIntervalDays` / `NextReviewDueAt` / `RepetitionNumber` (SR columns reserved for P3-10; NOT SET in P3-09, initialized null/0, no logic consumes them yet)
  - `CreatedAtUtc` / `UpdatedAtUtc` — standard audit trail
- **`MasteryEngine` domain service** — **pure static** logic to compute mastery status from cumulative accuracy:
  - `CalculateMasteryStatus(accuracy: decimal): MasteryStatus` — encodes the threshold table (Novice: <threshold, Learning: threshold–(threshold+0.3), Proficient: (threshold+0.3)–threshold+mastered_floor, Mastered: ≥mastered_floor OR ≥ custom threshold); floor of NeedsReview = 50% (lead-confirmed)
  - **10 unit tests** (thresholds, floor behavior, boundary cases)
- **`ILearningRepository` seam methods** — UpsertStudentSkillMasteryAsync (atomic insert/update, per-skill collection), GetStudentSkillMasteryAsync (by `StudentId`), GetStudentMasteryBySkillAsync (by `StudentId + SkillId`)
- **`LearningRepository` implementation** — EF Core `Upsert` on `StudentSkillMastery` table; idempotent via natural key
- **`MasteryService` + `IMasteryService` in-process seam** — wires the repo + engine for P3-08/10/11/13 to query mastery without cross-module FK; single-file service, DI injected in `DependencyInjection.cs`
- **Mastery read endpoints** — `GET /api/Learning/Mastery/Student` (all skills for the student), `GET /api/Learning/Mastery/Skill/{skillId}` (per-skill detail); no mutation endpoints in this story
- **Write-path integration: `CompleteAttemptCommandHandler` P3-09 hook** — after the attempt is marked Completed, handler calls `UpsertMasteryForAttemptAsync` to aggregate answers by `SkillId` + upsert per-skill mastery rows. Both the attempt update AND mastery upserts are **atomic within the ambient UnitOfWorkBehavior transaction** (ADR 0001 escape hatch: the behavior itself IS the explicit transaction wrapping both writes). No nested SaveChangesAsync; atomicity is guaranteed at the DB via Postgres transaction.
- **Migration `20260613125111_AddStudentSkillMasteryTable`** — creates table + unique index on `(StudentId, SkillId)` + foreign keys + a DB-level CHECK for valid MasteryStatus enum values
- **Resource strings** — AR/EN localization for MasteryStatus display + error messages (SharedResourcesKey.cs + .resx files)

**Load-bearing decisions (reviewer-confirmed):**
- **Cumulative accuracy formula:** unchanged from P2-04 (no variant rules per-subject/difficulty)
- **Per-skill `MasteryThreshold`** — hardcoded in `MasteryEngine` (config-driven deferral flagged for P5-02 / P10-12 if needed)
- **NeedsReview floor = 50%** — fixed lower bound; status transitions happen on accuracy moves
- **Write-path atomicity (transaction boundary):** `UnitOfWorkBehavior` wraps the entire `CompleteAttemptCommandHandler`, opening a single ambient transaction before the handler runs and committing after `SaveChangesAsync`. Both the attempt status update (Step 8) and the mastery upsert (Step 8b) are staged within that **same transaction** — never separate calls. This is the ADR 0001 escape hatch for multi-entity atomicity: "if you need atomic multi-writes, open an explicit transaction" — here the UoW behavior IS that explicit transaction. **Note:** CLAUDE.md rule #3 ("No Unit of Work" / "GenericRepository commits per call") is stale for new modules using `UnitOfWorkBehavior`; the behavior is the transaction seam.
- **SR columns (ReviewIntervalDays / NextReviewDueAt / RepetitionNumber)** — reserved in the schema for P3-10 spaced-repetition scheduling. **No second migration needed**; the columns are NULL/0 and unused in this story.
- **Cross-module seam:** `IMasteryService` is the in-process interface for P3-08 (adaptive difficulty), P3-10 (spaced-rep), P3-11 (adaptive quizzes), P3-13 (student profile) to read mastery without direct queries. P5-02 (parent weak-areas detection) is **deferred** to a `Shared.Contracts` seam (not a direct Learning module call) when it merges.

**Test coverage:** 246 unit tests (including 10 MasteryEngine) + 11 integration tests (P3_09_StudentMastery_Tests.cs: upsert + reads + concurrent writes). All green. Security audit: PASS, 0 blocking/high findings.

**Next story dependency:** P3-08 (Adjust difficulty adaptively) reads mastery via `IMasteryService` to select question pool; P3-10 (Schedule spaced-repetition) uses SR columns (reserved but null here) to compute review due-dates.

## P3-10 — Schedule spaced-repetition practice (backend) — added 2026-06-13 (`feat/P3-10-spaced-repetition`)

Built **P3-10** in the **`Learning` module**; expansion of P3-09 mastery foundation. Full pipeline complete (backend-feature → api-tester → security-auditor → reviewer PASS).

**What shipped:**
- **`SpacedRepetitionEngine` domain service** — pure static domain logic to compute review due-dates and interval progression:
  - `IsDue(lastPracticedAt: DateTime, nextReviewDueAt: DateTime?, repetitionNumber: int, now: DateTime): bool` — returns true if review is ready (no due date set, or due date has passed UTC now)
  - `ComputeNextReview(lastPracticedAt: DateTime, repetitionNumber: int): (nextDueAt: DateTime, newInterval: int)` — expands ladder [1,3,7,14,30] days; index 0→1 day, 1→3 days, …, 4→30 days; repetitionNumber capped at 4 to prevent overshoot
  - **13 unit tests** (ladder progression, UTC boundaries, edge cases)
- **`SpacedRepetitionOptions` config class** — `Engine.Ladder = [1,3,7,14,30]` hardcoded constant (not configurable in this cycle); seeded from `appsettings.json` `SpacedRepetition:Engine` section
- **`ILearningRepository` new seam methods:**
  - `GetDueMasteryRowsAsync(studentId: int, now: DateTime): Task<List<StudentSkillMastery>>` — finds all mastery rows where `IsDue` is true (no migration; queries existing columns)
  - `UpdateMasterySpacedRepetitionAsync(masteryId: int, nextDueAt: DateTime, newInterval: int, newRepetitionNumber: int): Task` — atomic update of the 3 SR columns; used by sweep job
- **`LearningRepository` implementation** — EF Core queries for GetDueMasteryRows (filters on `StudentId` + `(NextReviewDueAt IS NULL OR NextReviewDueAt <= @now)`); UpdateMasterySpacedRepetitionAsync via `ExecuteUpdateAsync` (no SaveChangesAsync — called from sweep job outside the pipeline)
- **`SpacedRepetitionSweepJob` Hangfire job** — scheduled at configurable cron (default `"0 0 * * *"` = daily midnight UTC). **Fixed job ID `"SR-Sweep"`** (idempotent across restarts). Two-phase:
  1. Read all due mastery rows for all students via `GetDueMasteryRowsAsync(studentId, UtcNow)`
  2. For each, call `SpacedRepetitionEngine.ComputeNextReview(...)` and update via `UpdateMasterySpacedRepetitionAsync(...)`
  - Robust to concurrent executions (idempotent ID); no domain events raised (sweep is infrastructure)
- **Write-path integration: `CompleteAttemptCommandHandler` P3-10 hook** — after mastery upsert in the P3-09 ambient transaction, handler calls `RecordMasteryCompletionForSpacedRepetitionAsync` (new method). On first attempt after P3-09's `UpsertMasteryForAttemptAsync`:
  - If `NextReviewDueAt` is null (first review), sets it to now + 1 day; sets `RepetitionNumber=0` (ladder index)
  - Runs **within the same ambient `UnitOfWorkBehavior` transaction** (P3-09 seam); no separate SaveChangesAsync
  - Subsequent reviews happen via sweep job, not the write path (write path only primes the first due-date)
- **`SpacedRepetitionService` in-process seam** — wraps engine + repo for read-path queries (e.g., P4-06 missions, future tutoring surfaces); DI injected
- **`GET /api/Learning/Reviews/Due` endpoint** — returns `DueReviewDto[]` (empty if no reviews due now). **ReviewsController** new controller. DTOs:
  - `DueReviewDto` — `{ SkillId, SkillName, LastPracticedAt, NextReviewDueAtUtc, RepetitionNumber, DaysSinceLastReview }`
  - Surfaces spaced-rep state for student-facing review UI (P4-12 deferred)
- **Resource strings** — AR/EN localization for review UI labels (SharedResourcesKey + .resx)
- **No migration required** — SR columns were reserved in P3-09; P3-10 initializes + mutates them only in memory/sweep

**Load-bearing decisions (reviewer-confirmed):**
- **Expanding ladder [1,3,7,14,30] days** — fixed, not configurable; wired from `appsettings.json` `SpacedRepetition:Engine.Ladder` for operational tuning (no restart needed if lead changes it)
- **SM-2 EaseFactor deferred** — P3-10 uses a **fixed** expanding ladder, **not** the full SM-2 algorithm with dynamic EaseFactor. SM-2 variant left for P3-11 or later if needed (marked explicit TODO in code + HANDOFF defer note)
- **UTC discipline:** all datetime comparisons use `.UtcNow` (and `.ToUniversalTime()` at Postgres mapping boundary for the Npgsql Local-kind quirk — see P4-11 note on `EnableLegacyTimestampBehavior`)
- **Hangfire sweep job fixed ID `"SR-Sweep"`** — idempotent across process restarts; Hangfire prevents concurrent execution; no domain events (sweep is pure infrastructure)
- **First review primed in write-path (CompleteAttemptCommandHandler)** — sets `NextReviewDueAt = now + 1 day` and `RepetitionNumber = 0` (ladder index 0 = first rung). Subsequent reviews are transitioned by the sweep job. This keeps the ambient transaction clean (no nested SaveChangesAsync in sweep).
- **Sweep job uses `ExecuteUpdateAsync` (not SaveChangesAsync)** — called outside the request pipeline; no domain events. Safe under concurrent Hangfire execution (DB-level consistency)
- **Cross-skill aggregation in sweep** — all students + all their due skills are processed in one job run; no per-student isolation (simple, scalable)
- **Missions-surfacing seam (P4-06 integration)** — **deferred to P4-06**. When P4-06 ships, missions may query `GetDueMasteryRowsAsync` to surface spaced-rep reviews as a challenge type. The seam is pre-built in `ISpacedRepetitionService`; P4-06 just calls it.

**Test coverage:** 286 unit tests (including 13 SpacedRepetitionEngine) + 8 integration tests (P3_10_SpacedRepetition_Tests.cs: IsDue + ComputeNextReview + sweep job simulation). All green. Security audit: PASS, 0 blocking/high findings.

**Next story dependency:** P3-11 (Serve adaptive quizzes) reads due mastery via `ISpacedRepetitionService` to filter quiz candidate pool. P4-06 missions (Wave future) may query due reviews as a challenge type.

**Stale-item fixes applied to P3-09 section (this commit):**
- Line 317: removed stale reference to `LastReviewedAtUtc` (entity uses `LastPracticedAt`)
- Line 78 (StudentSkillMastery.cs comment): corrected `RepetitionNumber` doc from "SM-2 repetition counter" to "spaced-repetition ladder index"


## P3-11 — Serve adaptive quizzes (backend) — added 2026-06-14 (`feat/P3-11-adaptive-quiz`)

Built **P3-11** in the **`Learning` module**; difficulty-tuned quiz selection engine. Full pipeline complete (backend-feature → reviewer PASS). Last story in the difficulty chain (depends on P3-08 adaptivity + P3-09 mastery + P3-10 spaced-repetition).

**What shipped:**

- **`QuizSelectionEngine` domain service** — pure static deterministic weighted-mix selection:
  - `SelectQuestions(candidates: List<Question>, targetDifficulty: int, policy: QuizSelectionOptions): (questions: List<Question>, servedMix: DifficultyMix)` — selects N questions from candidates using **70/30 weighted policy** (70% target difficulty, 30% adjacent difficulties); applies sort-by-Id for deterministic resume
  - `DifficultyMix` value object captures the policy result as jsonb-serializable record `{ Easy, Medium, Hard, Target, WasDefault }`
  - Graceful degradation: empty candidate pools at target difficulty fall back to full candidate list (never 500, never stall)
  - **9 unit tests** (selection logic, edge cases, determinism)

- **`QuizSelectionOptions` config class** — `Engine.WeightedMix` policy record (easy/medium/hard/target percentages); seeded from `appsettings.json` `QuizSelection:Engine` section (changeable per environment)

- **`Attempt` entity schema changes** (migration `20260614015416_AddAttemptServedDifficultyMix`):
  - New nullable column `ServedDifficultyMix` jsonb — persists the actual mix policy served to this attempt (set on start, never re-computed on resume for determinism); shape `{ Easy:int, Medium:int, Hard:int, Target:int, WasDefault:bool }`
  - New nullable column `TargetDifficulty` int — the student's target difficulty at attempt start, derived from `IAdaptivityService.GetTargetDifficulty(studentId, skillId)` call

- **`AttemptConfig` entity configuration** — Fluent mapping for jsonb columns (HasColumnType("jsonb"), HasDefaultValueSql)

- **`StartAttemptCommandHandler` integration** — wired after existing lifecycle/IsActive/language guards (preserves all existing guards, additive only):
  1. Call `_adaptivityService.GetTargetDifficulty(studentId, skillId, ...)`
  2. Resolve quiz candidates from lesson's questions
  3. Call `QuizSelectionEngine.SelectQuestions(candidates, target, options)`
  4. Persist `TargetDifficulty` + `ServedDifficultyMix` on new attempt
  5. Resume (existing attempt) skips re-selection: loads persisted mix from DB (deterministic across refreshes)

- **`DependencyInjection.cs`** — wired `Configure<QuizSelectionOptions>` in Learning.Infrastructure

- **`appsettings.json`** — new `QuizSelection:Engine` section with default `WeightedMix` policy (70/30 weighted)

- **Test coverage:** 311 unit tests (9 new QuizSelectionEngine) + 8 integration tests (P3_11_AdaptiveQuizSelection_Tests.cs: selection logic, resume determinism, graceful fallback). All green. Security audit: PASS, inline (no PII, no breaches).

**Load-bearing decisions (reviewer-confirmed):**

- **70/30 weighted policy:** all difficulty buckets non-empty, policy weights toward target but includes adjacent levels for reinforcement. **Fully config-bound** (swappable per environment via `QuizSelection:Engine:WeightedMix`).

- **Persisted on start only, NOT re-computed on resume:** once the student starts an attempt, the served mix is locked into `ServedDifficultyMix` (no second-guessing if mastery shifts mid-attempt). Reduces cognitive load + enables analytics (same questions re-shown on resume). Next step (Phase 5?): deep-resume could recompute target + re-select if needed (acceptable v1 deviation from persisted mix).

- **Deterministic via sort-by-Id:** when the mix policy selects from a bucket (e.g., "3 medium"), the order is stable (sorted by QuestionId ascending) so resume produces the same question sequence without round-trip state.

- **Graceful degradation (thin/empty pools):** if target-difficulty has no questions, the selection falls back to the full candidate list (no 500, no stall). Logged at Warn for ops to debug.

- **Wired into StartAttempt AFTER guards:** call to `IAdaptivityService.GetTargetDifficulty` is downstream of existing quiz-active/language checks, so the service can assume a valid attempt context.

- **No re-persistence on resume:** `CompleteAttemptCommandHandler` does NOT re-write `ServedDifficultyMix` (no churn, determinism preserved).

**Cross-story dependencies:**

- **P3-08 (adaptivity engine)** — `IAdaptivityService.GetTargetDifficulty(...)` is the only consumer of the adaptive-difficulty signal. P3-11 calls it during StartAttempt setup.
- **P3-09 (mastery)** — mastery rows inform the difficulty target (via P3-08's weighted-score algorithm). P3-11 reads that signal.
- **P3-10 (spaced-repetition)** — not directly wired into P3-11 (review due-ness is on the mastery row, not the question). Future: P3-11 could filter candidates to include "due reviews" as a reinforcement bucket (deferred to P3-12+).

**Non-blocking follow-ups:**

- **Resume recomputation:** if the student's mastery/target shifts mid-attempt (e.g., they practice outside the attempt context), a deep-resume could invalidate the cached mix. Acceptable v1 (student starts fresh quiz to re-tune).
- **P3-06 generation-side consumer:** P3-06 (offline practice-question generation) reads the same `IAdaptivityService` seam; wired at `ILearningContextProvider` seam (deferred — not in P3-11 scope).






## P3-13 — Adaptive student profile / behavioral modeling (backend) — added 2026-06-13 (`feat/P3-13-student-profile`)

Built **P3-13** in the **`Learning` module**; behavioral modeling layer for personalization feeds. Full pipeline complete (backend-feature → security-auditor [child-privacy gate] → reviewer PASS).

**What shipped:**

- **`StudentLearningProfile` domain entity** — jsonb-backed behavioral attributes table (`learning.student_learning_profiles`), keyed on `(student_id, grade_id)` with unique constraint + indices on `student_id`. Fields: `StudentId/GradeId` FKs, `QuestionTypeAffinities` (jsonb normalized 0.0–1.0 per type), `RecurringErrorClusters` (jsonb list), `AttentionSpan` (minutes, v1 proxy), `PreferredExplanationStyle` (enum PROVISIONAL), `FatigueSignal` (internal-only), `DataPointCount`, timestamps.

- **`ExplanationStyle` enum** — 4 values (Verbal, Visual, Analogical, StepByStep); PROVISIONAL pending P3-03 confirmation.

- **`StudentProfileEngine` domain service** — pure static, deterministic (16 unit tests):
  - **Derivation 1 (QuestionTypeAffinities):** normalized accuracy per question type
  - **Derivation 2 (RecurringErrorClusters):** error-pattern grouping by skill + frequency
  - **Derivation 3 (AttentionSpan):** v1 = median inter-question time within attempt (minutes)
  - **Derivation 4 (ExplanationStyle):** inferred from accuracy-improvement deltas per style; Verbal default

- **`StudentProfileService`** — wraps engine + repo. DI-injected. Methods: `ComputeProfileAsync`, `UpsertProfileAsync`.

- **`IStudentProfileService` seam** — in-process interface for P3-03/P3-08 consumption (deferred wiring; cross-module via `Shared.Contracts` if needed).

- **`StudentLearningProfileDto` DTO** — data-minimized: `QuestionTypeAffinities`, `PreferredExplanationStyle`, `DataPointCount` exposed; `FatigueSignal` internal only.

- **`StudentProfileRecomputeJob` Hangfire job** — fixed ID `"SP-Recompute"` (idempotent), cron `"0 2 * * *"` (daily 2 AM UTC). Fetches attempts, runs engine, upserts profile.

- **`CompleteAttemptCommandHandler` Step-8d hook** — calls `IStudentProfileService.UpsertProfileAsync(...)` after mastery + spaced-rep; rides P3-09/P3-10 transaction boundary.

- **`GET /api/Learning/Profile` endpoint** — returns `StudentLearningProfileDto` for authenticated student. 401 anonymous. 200 + default/empty if no history.

- **Migration `20260613180132_AddStudentLearningProfileTable`** — jsonb table + indices.

**Data minimization (mandatory security-auditor gate, PASSED):**
- DTO exposes 4 attrs + DataPointCount + style only; `FatigueSignal` stays internal
- No raw error data (pattern summary only)
- No PII (student IDs only)
- Grade-transition preserves profile (no grade filter)

**Load-bearing decisions:**
- **Behavioral separation:** `StudentLearningProfile` (learning behavior, feeds P3-03/P3-08) orthogonal to `StudentXpProfile` (achievement).
- **Pure engine, no AI:** deterministic, testable, offline-safe. AI surfaces in P3-03 (prompts) + P3-08 (difficulty).
- **Recompute cadence:** daily sweep (2 AM UTC) + eager on-attempt-complete (within transaction). Trade-off: behavior surfaces ~2s later or next sweep.
- **ExplanationStyle PROVISIONAL:** pending P3-03 confirmation (may refactor once locked).
- **AttentionSpan v1:** within-attempt proxy. P5-03 v2 upgrades to per-session/per-subject depth (upgrade path reserved; DTO unchanged).
- **IStudentProfileService seam:** in-process. P3-03/P3-08 consume synchronously.

**Test coverage:** 302 unit (16 engine + 286 inherited P3-09/P3-10) + 8 integration (P3_13_StudentProfile_Tests.cs). All green. Security audit: PASS, 0 Critical/High.

**Next dependencies:** P3-03 (tutor prompts via profile) + P3-08 (difficulty adjustment via ExplanationStyle + AttentionSpan).

**Non-blocking follow-ups:**
- ExplanationStyle taxonomy finalization (pending P3-03)
- AttentionSpan v2 (P5-03) — per-session/per-subject depth modeling
- Cross-module consumption (if P3-05/06+ need profile) — add `IStudentProfileQuery` to `Shared.Contracts`

---

## P4-11 — Streak freeze + timed events + weekly challenges (BE, commit + PR ready)

**Branch:** `feat/P4-11-streak-freeze-timed-events`. BE-only, single PR for 3 concerns.

**What shipped:**

- **Streak freeze** — earned-only, cap=2 via DB CHECK + `StudentXpProfile.MaxFreezes = 2` entity constant + handler option. Granted on every 7-day streak milestone (configurable via `FreezeOptions.EarnEveryNStreakDays`) via `AdvanceStreakCommandHandler` — raises `StreakFreezeGrantedDomainEvent` → cache invalidator. Sweep job two-pass consumes pre-break and shifts `LastActivityDateUtc`, raising `StreakFreezeConsumedDomainEvent` → republisher + invalidator.

- **Timed events** — `TimedEvent` entity + table + `TimedEventScope` enum + sweep job at `*/2 * * * *` UTC (configurable via `TimedEventOptions.SweepCron`) + `IActiveTimedEventsQuery` cross-module seam + Redis-cached decorator at 30s TTL + WELCOME_BOOST seed row 30 days in future + 2 invalidators + 2 republishers + admin read endpoint `GET /api/admin/timed-events` with `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`.

- **Weekly challenges** — 3 `CHALLENGE_*` rows in `MissionSeeder` reusing P4-06 mission engine; daily=5, weekly=6 now.

- **XP boost integration** — `IXpBoostCalculator` handler-side; wired into 5 XP-awarding handlers (lesson, answer, streak bonus, mission completion, badge). League NOT wired per lead lock since it receives boosted upstream. `max(multipliers)` capped at `MaxMultiplierCeiling=5.0`. Fail-soft with `LogWarn`.

- **Dashboard surface** — `FreezeBalance` on streak snapshot; `ActiveTimedEvents` list on `DashboardDto`.

**Locked lead decisions:**
- Single PR for all three concerns.
- Earned-only freeze (no parent/shop grants this cycle).
- Handler-side calculator (no MediatR pipeline behavior).
- `max` + ceiling 5.0 for overlapping events.

**Load-bearing config (next agent: do NOT remove):**
- `Gamification:Freeze:MaxInventory = 2` — informational; DB CHECK and `StudentXpProfile.MaxFreezes` are the hard caps.
- `Gamification:Freeze:EarnEveryNStreakDays = 7` — live-bound via `FreezeOptions`; `AdvanceStreakCommandHandler` reads this.
- `Gamification:TimedEvent:SweepCron = "*/2 * * * *"` — live-bound via `TimedEventOptions`; `GamificationModule.InitializeAsync` reads this.
- `Gamification:TimedEvent:TimeZoneId = "UTC"` — live-bound via `TimedEventOptions`; falls back to `TimeZoneInfo.Utc` on invalid string.
- `Gamification:Events:MaxMultiplierCeiling = 5.0`
- `Gamification:Cache:TimedEventsTtlSeconds = 30`

**Migration applied:** `20260602215558_P4_11_AddFreezeBalanceAndTimedEvents` — adds `FreezeBalance int NOT NULL DEFAULT 0` + CHECK constraint `>= 0 AND <= 2`; creates `TimedEvents` table with CHECK `multiplier >= 1.0 AND <= 5.0` + CHECK `start_utc < end_utc` + 3 indexes.

**Important infra fix — Npgsql legacy timestamp bug:** Under `Npgsql.EnableLegacyTimestampBehavior=true`, `timestamptz` columns are read as `DateTime` with `Kind=Local`. Comparing in-memory against `Kind=Utc` parameters gives wrong results on non-UTC hosts. Fix pattern: at the mapping boundary (Postgres impl of cross-module query seam), call `.ToUniversalTime()` on every datetime field. Future entities using `timestamptz` should follow this pattern. Concrete example: `PostgresActiveTimedEventsQuery` maps each `e.StartUtc.ToUniversalTime()`.

**Test coverage:** 16 unit tests (LeaderboardScoreEncoder-style pure logic) + 20 integration tests (Testcontainers.Redis+Postgres; 19 original + 1 T14b added in reviewer fix pass). All pass. P4-06 catalog count assertions updated from 8→11 (daily=5 unchanged, weekly=3→6).

**Security audit:** 0 Crit/High, 4 Mediums fixed in-PR (`StreakFreezeGrantedDomainEvent` + invalidator, `AdminOnly` policy on TimedEvents endpoint, `LogWarn` in `XpBoostCalculator` catch, `[DisableConcurrentExecution(120)]` on both sweep jobs). Pre-existing JWT CHANGE_ME + Newtonsoft.Json transitive scanner-noise unchanged.

**Operational notes:**
- WELCOME_BOOST is a future-dated demo row that ops must manually shift to activate in any environment. Seeder is idempotent by Code; will not duplicate or shift on second run.
- Both sweep jobs carry `[DisableConcurrentExecution(120)]` — safe under scale-out.
- Hangfire dashboard exposes `gamification:timed-event-sweep` (2 min) and `gamification:streak-sweep` (existing) for manual run.

**NOT in scope (deferred):**
- Hearts cadence boost during fast-hearts events (P4 follow-up)
- Parent-grantable freezes (would need Parent module endpoint + new domain event source field)
- XP-shop / coin economy freezes (no economy primitive exists)
- Admin write endpoints for timed events (P7 admin console)
- SignalR push to FE (server-side caching only per P4-10 decision)

---

## P4-10 — Redis realtime gamification state (BE, commit + PR ready)

**Branch:** `feat/P4-10-redis-realtime-gamification`. **No FE work.** No new endpoint, no migration, no DTO change. Pure infrastructure perf layer.

**What shipped (BE only, all 4 batches):**

Server-side Redis cache layer over the 6 dashboard read seams + a Redis sorted-set leaderboard for league cohorts + a nightly Hangfire rebuild job. Postgres remains the source of truth; Redis mirrors. "Realtime" in the story title = sub-50ms reads on the dashboard hot path, not push-to-client.

- **`Host/Program.cs`** — registers `IConnectionMultiplexer` as Singleton when `ConnectionStrings:Redis` is non-empty (same conditional gate as the existing `AddStackExchangeRedisCache`). Null otherwise → `NullGamificationCache` resolves and Postgres path is unchanged.
- **`appsettings.json` + `appsettings.Development.json`** — new `Gamification:Cache` section: `Enabled` kill-switch + 7 per-key TTLs (`XpTtlSeconds=60`, `StreakTtlSeconds=60`, `HeartsTtlSeconds=30`, `BadgesTtlSeconds=300`, `MissionsTtlSeconds=60`, `LeagueSnapshotTtlSeconds=30`, `LeaderboardSortedSetTtlSeconds=691200` = 8 days).
- **`Gamification.Application/Caching/`** — `IGamificationCache` (string + sorted-set primitives, all fail-soft), `GamificationCacheOptions`, `GamificationCacheKeys` (canonical key builders: `gamification:student:{int_id}:{seam}[:{discriminator}]` + `gamification:league:{int_id}:standings`), `LeaderboardScoreEncoder` (packed-integer `weeklyXp * 16777216 + (16777215 - joinOrder)`).
- **`Gamification.Application/Leagues/Caching/ILeagueLeaderboard.cs`** — abstraction for the sorted-set leaderboard (`UpsertMembership`, `GetRank`, `GetCohortSize`, `GetTop/BottomMembershipIds`, `Delete`).
- **`Gamification.Infrastructure/Caching/`** — `RedisGamificationCache` (fail-soft try/catch on every op, `LogWarn` static messages, no `ex.Message`/no `ex` echoed), `NullGamificationCache` (no-op fallback), `RedisLeagueLeaderboard` (ZADD CH + ZREVRANK; `int.TryParse` guard on Redis-sourced member strings), `NullLeagueLeaderboard`, `GamificationCacheRebuilder` (Scoped, uses DbContext).
- **`Gamification.Infrastructure/Queries/Cached/`** — 6 `Cached*Query` decorators (`Xp`, `Streak`, `Hearts`, `Badges`, `Missions`, `League`). Existing Postgres impls renamed to `Postgres*Query` and registered via factory DI (`AddScoped<Postgres*Query>()` + `AddScoped<I*Query>(sp => new Cached*Query(inner, cache, options))`). Decorators are `internal sealed`. Lazy-refill (Hearts) and lazy-instantiation (Missions/League) paths preserved — cache stores the post-side-effect value.
- **`Gamification.Application/Features/Cache/Invalidators/`** — 8 `INotificationHandler` cache invalidators (`XpAwarded`, `StudentLeveledUp`, `StreakAdvanced`, `StreakBroken`, `HeartsDepleted`, `HeartsRefilled`, `BadgeEarned`, `MissionCompleted`). They DEL the relevant key post-commit (ADR 0002). `XpAwardedCacheInvalidator` also DELs the league snapshot key + ZADD CH the sorted set (defence in depth alongside the in-handler write).
- **`IncrementLeagueXpCommandHandler`** — patched to call `_leaderboard.UpsertMembershipAsync` after `AddWeeklyXp(...)`. Documented as a pre-commit, best-effort leading write; phantom-score risk on Postgres rollback is corrected by the nightly rebuild job + post-commit invalidator (idempotent ZADD CH).
- **`LeagueRolloverJob`** — patched to call `_leaderboard.DeleteAsync(leagueId)` per old cohort after the rollover commit. New cohorts populate the sorted set lazily as members earn XP.
- **`Gamification.Infrastructure/Jobs/GamificationCacheRebuildJob`** — Hangfire recurring job, ID `gamification:cache-rebuild`, cron `"0 3 * * *"` UTC, mirrors `StreakSweepJob` shape (`IServiceScopeFactory + ILoggerManager`, creates async scope per run). Calls `GamificationCacheRebuilder.RebuildSortedSetsAsync` — for every active week cohort, DELs the sorted-set key then re-`ZADD CH`s every membership with freshly-encoded score. Idempotent; per-cohort fail-soft.

**Key decisions (locked by lead):**
- **Q1 — Realtime scope:** server-side caching only. NO SignalR. No FE contract change. `useDashboardDiff` continues polling.
- **Q2 — Cache scope:** all 6 dashboard read seams + sorted-set leaderboard.
- **Q5 — Score encoding:** packed integer `weeklyXp * 16777216 + (16777215 - joinOrder)`. Higher XP → higher score; equal XP → lower JoinOrder wins ties.
- **Q9 — Sorted-set update op:** `ZADD CH` with Postgres-read `WeeklyXp` after each commit (drift-free).
- **Consistency model:** write-around for per-student keys (DEL on event) + write-through-via-`ZADD CH` for the sorted set. Postgres is the durable ledger.
- **Fail-soft semantics:** every Redis op wrapped, `LogWarn` static message on `RedisException`, fall through to Postgres. Cache failure NEVER raises a 5xx.
- **Kill-switch:** `Gamification:Cache:Enabled = false` → `RedisGamificationCache` short-circuits to no-op AT TOP of every method (validated by test T08). Use this to bypass cache in ops emergencies.
- **Decorator DI:** factory delegate per seam (no Scrutor dependency).

**Load-bearing config (next agent: do NOT remove):**
- `ConnectionStrings:Redis` (already wired in P1-06) — if set, Redis path active; if empty, Null path active.
- `Gamification:Cache:Enabled` — set `false` to disable all caching without restarting Redis. Per-key TTLs are also live-tunable via appsettings reload (but `IOptions<T>` snapshots in constructor, so a host restart picks them up cleanly).
- Hangfire connection (`ConnectionStrings:Hangfire`) — must be set or the recurring job registration fails at startup. Pre-existing requirement (P1-07).

**Tests:**
- `backend/tests/Learnexia.IntegrationTests/P4_10_RedisCache_IntegrationTests.cs` — 16 cases. Uses Testcontainers.Redis (newly pinned `3.10.0` in `Directory.Packages.props`) + Testcontainers.PostgreSql. Covers: cache hit/miss, invalidator DEL on every domain event, sorted-set ZADD/ZREVRANK ordering, tiebreak, rebuild idempotency, kill-switch, Postgres fallback, no PII in keys.
- `backend/tests/Modules.Learning.UnitTests/LeaderboardScoreEncoderTests.cs` — 13 cases. Pure unit. Covers: round-trip, ordering invariants, double-precision safety, negative-input validation.
- Both filters all-pass. Full-suite shows 3 pre-existing seeder-ordering failures in `P2_09_HomeDashboard_Tests` / `P2_04_LearningPath_Tests` (unrelated to P4-10; existed on `main`).

**Security audit:** 0 Critical, 0 High, 3 Mediums all fixed in-PR (F-09: all `LogError(ex, ...)` swapped to `LogWarn(...)` static messages; `int.TryParse` guard on Redis sorted-set member strings; documented phantom-score window in `IncrementLeagueXpCommandHandler` comment). Pre-existing JWT `CHANGE_ME` + Postgres `Password=admin` defaults and the transitive `Newtonsoft.Json 11.0.1` via Hangfire.Core are NOT P4-10 introductions — tracked separately in MEMORY.md.

**Operational notes:**
- Redis sorted-set member format: `MembershipId.ToString()` (int — NOT a Guid). No PII.
- Sorted-set keys carry 8-day TTL — survives the week + rollover window + buffer.
- Per-student keys carry 30-300s TTLs — bounded staleness even if an invalidator misfires.
- Rebuild job is the drift safety net — runs nightly 03:00 UTC. Can be invoked manually via the Hangfire dashboard.

**NOT in scope (deferred / not P4-10):**
- SignalR / push-to-client realtime → would need a new story; FE has no SignalR client and no story requires it this cycle.
- Per-student rebuild API → `IGamificationCacheRebuilder` exposes only `RebuildSortedSetsAsync`. Per-student / per-league granularity can be added when ops asks for it.
- Cross-module cache layer for Learning/Notifications/etc. → `IGamificationCache` is module-private by design; other modules will own their own caches.

## P4-07 — Weekly leagues (FE Batch 5 — LeaguePreview dashboard flip, commit + PR ready)

**Branch:** `feat/P4-07-weekly-leagues`.

**What shipped (FE-only, Batch 5):**

Minimal dashboard data flip for the `LeaguePreview` section (plan task B5-1 scope: FE-2 + FE-4). No new component promoted to `@learnexia/ui`; no motion or animations (P4-08 owns those).

- **api-client snapshot verified** — `LeaguePreviewDto` with `tierName`, `rank`, `totalPlayers`, `xpThisWeek` fields was already present in `packages/api-client/swagger.json` + `nswag-client.ts` from P2-09. No regen or patch needed.
- **`apps/student-app/app/(child)/index.tsx`** — `LeaguePreviewRow` inline component added (screen-local, not promoted). Replaces the P2-09 TODO comment block. Renders `tierName` (mapped via i18n keys) + rank text when `dashboardQuery.data?.leaguePreview` is non-null. Hidden when null (brand-new student / BE not yet on P4-07).
- **`packages/shared/src/i18n/resources.ts`** — Added 7 new keys under `child.home.*` in both EN and AR locales:
  - `leagueTier.{bronze,silver,gold,diamond}` — maps BE's `LeagueTier.ToString()` strings to localized display names.
  - `leaguePreview.{rankLabel,rankUnknown,a11y}` — rank display + accessibility label.

**Key decisions:**
- **No api-client patch** — `LeaguePreviewDto` shape was already correct in the snapshot.
- **Tier name mapping** — BE sends `LeagueTier.ToString()` = "Bronze"/"Silver"/"Gold"/"Diamond" (D14 in plan). FE maps these lowercase strings to i18n keys; unknown values fall back to the raw string.
- **Null guard** — `leaguePreview` is still rendered conditionally. When BE has not yet shipped the league engine (or brand-new student), the row is hidden. No fallback "Bronze" row (per D13 in plan — sentinel is BE responsibility).
- **No new design** — FE-1 (tier badge primitives), FE-3 (full league screen), FE-5 (RTL pass) are P4-08.

**Test results:** `pnpm` not in PATH (same as all prior batches). Direct `tsc` run shows only pre-existing workspace-resolution errors (all `Cannot find module '@learnexia/*'` + `--jsx` flag issues) — same errors exist on all other unmodified files. No new type errors from the changes.

**Deferred items (P4-08):**
- Medal/tier icons, motion, league screen, promotion/demotion animations.
- RTL-specific polish pass.

---

## P4-06 — Complete daily/weekly missions (Batch 8 — commit + PR ready)


## P4-07 — Weekly leagues (Batches 0-5 — ApplyAward refactor + league engine + endpoint + FE flip, commit + PR ready)

**Branch:** `feat/P4-07-weekly-leagues`.

**What shipped — Phase-3 Gamification sixth story. The first competitive layer. Sixth event-consumer feature in Gamification module, completing the reward economy:**

### Batch 0 — ApplyAward chokepoint refactor (critical predecessor work)

- **StudentXpProfile.ApplyAward expanded to 4-arg signature** — now a single chokepoint for ALL XP additions across all 5 prior sources. Signature: `ApplyAward(int amount, int newLevel, XpReason reason, DateTime occurredAtUtc)`. Raises new `XpAwardedDomainEvent(StudentId, Amount, TotalXpAfter, Reason, OccurredAtUtc)`.
- **RecordBadgeEarned + RecordMissionCompleted now delegate to ApplyAward** — refactored to call ApplyAward instead of mutating TotalXp directly. Ensures event is raised from all XP paths.
- **Semantic change: LastAwardAtUtc uses event timestamp, not wall-clock** — critical for week-boundary correctness when events are replayed/retried.
- **85/85 P4-02..P4-06 regression PASSED post-refactor** — zero assertion updates needed.

### Schema (Batch 1)

- **AddLeagueAndLeagueMembership migration (20260601183834):**
  - Leagues table — cohort aggregator with Tier, PeriodKey, GroupIndex, unique on (Tier, PeriodKey, GroupIndex).
  - LeagueMemberships table — per-student per-week with WeeklyXp, JoinedAtUtc, TierAfter, ParticipantStatus, unique on (PeriodKey, StudentXpProfileId) and (LeagueId, StudentXpProfileId).
  - LeagueXpDeltaLogs table — idempotency ledger with unique on (LeagueMembershipId, OriginEventId).
  - StudentXpProfile.CurrentTier field (LeagueTier int, default Bronze=1).
  - MembershipStatus enum (Active=1, Promoted=2, Demoted=3, Stayed=4).

### Engine (Batches 2-4)

- **LeagueStandings pure static** — ComputeCutoffs(size) + Apply(members, tier). Handles tier extremes + small-cohort scaling (floor(size * 7/30) promote, floor(size * 5/30) demote for size >= 12; 0/0 for size < 5).
- **StudentXpProfile.UpdateTier mutation method** — encapsulates tier change during rollover.
- **LeagueOptions config** — CohortSize=30, PromoteCount=7, DemoteCount=5, PromotionJobCron="15 0 * * 1", TimeZoneId="UTC".
- **14 new IGamificationRepository methods** — GetOrCreateLeagueAsync, GetCurrentLeagueForStudentAsync, CreateLeagueMembershipAsync, IncrementLeagueMembershipXpAsync (with idempotency), GetLeagueStandingsAsync, UpdateLeaguePromotionAsync, GetStudentMembershipsForRolloverAsync, CreateLeagueMembershipsForNextWeekAsync, graph-nav attach methods.
- **LeaguePlacementService (Infrastructure)** — GetOrCreateMembershipAsync: transactional find-or-create cohort + insert membership with graph-nav pattern.
- **IncrementLeagueXpCommand + handler** — narrowed idempotency catch, period key derived from request.OccurredAtUtc (post-review fix for week-boundary correctness), no-op when no membership (lazy placement dashboard-driven).
- **XpAwardedLeagueHandler notification handler** — in own try/catch per ADR 0002 §3, consumes XpAwardedDomainEvent, fans-out to IncrementLeagueXpCommand.
- **IStudentLeagueQuery cross-module seam** with LAZY INSTANTIATION — on null membership, calls LeaguePlacementService to trigger cohort creation on first dashboard read of week.

### Cross-module + API + Dashboard (Batch 5)

- **LeagueTierDto drift enum** in Shared.Contracts — parity-tested (4/4 enum drift unit tests).
- **DashboardDto.LeaguePreview wired** — GetDashboardQueryHandler injects IStudentLeagueQuery, replaces null with real snapshot.
- **GET /api/Gamification/Leagues/Me endpoint** — JWT-only IDOR-proof. Returns MyLeagueResponse: CurrentTier, Rank, TotalPlayers, WeekStart/EndUtc, Standings(30-row cohort), PromotionCutlineRank=7, DemotionCutlineRank=26. DisplayName anonymized to "Student #N" (no PII).
- **LeagueRolloverJob Hangfire** — "15 0 * * 1" UTC Monday 00:15 (after StreakSweep 00:05 + MissionRollover 00:10). For each cohort: rank members, promote top-7, demote bottom-5, update StudentXpProfile.CurrentTier. Idempotent.
- **FE: LeaguePreviewRow component** — dashboard row using leaguePreview data + i18n tier names EN/AR.

### Post-review fixes applied

- **#2 should-fix:** IncrementLeagueXpCommandHandler period-key now from request.OccurredAtUtc (was wall-clock, broke week boundaries). 23/23 tests green.
- **#4 nits:** stale TODO P4-07 comments removed from DashboardProfile.cs, LeaguePreviewDto.cs.

### Lead-approved decisions

- **D1:** ApplyAward 4-arg chokepoint refactor.
- **D2:** Anonymization = "Student #N" (no PII).
- **D3:** Top-7/bottom-5 cutoffs (Duolingo gentler standard).
- **D4:** Endpoint + minimal FE flip bundled; full screen P4-08.
- **D5:** Reuse MissionPeriodCalculator for weekly key.

### Accepted MVP risks

- **R1:** Concurrent placement may overfill cohort by 1 (race window, bounded, unique constraint prevents double-membership).
- **D15:** XP earned before first dashboard load not credited (lazy placement trade-off).
- **JoinOrder collision:** two students could get same display name under concurrent placement (UX flaw, not data corruption).
- **XpAwardedDomainEvent ghost on retry (ADR 0002 §3):** single delivery via IsolatedNotificationPublisher, accepted.

### Test results

- **27/27 LeagueStandings unit** + **4/4 enum drift** = 31/31 unit.
- **23/23 P4-07 integration** (lazy placement, XP increment, idempotency, rankings, tier extremes, endpoint, anon, IDOR, auth).
- **85/85 P4-02..P4-06 regression** (ApplyAward refactor transparent).
- **108/108 full P4 suite ✅**

### Security: PASS (0 blocking, all Info/Low)

### Graph-nav convention (5th instance)

- AttachLeague + AttachLeagueMembership (mirrors Membership pattern).

### Deferred

- **P4-08:** Full league screen, tier badges, motion.
- **P4-09:** Promotion/demotion nudges.
- **P4-10:** Redis hot-path read model.
- **P7-03:** Admin tier override.
- **LeaguePlacementServiceTests.cs:** Service no longer pure-static (depends IGamificationRepository); behavior covered by 23/23 integration tests (T2/T3/T11/T12 lazy placement/concurrent/tier-tracking).

## P4-06 — Complete daily/weekly missions (Batch 8 — commit + PR ready)

**Branch:** `feat/P4-06-missions` (ready for committer).

**What shipped:**

**Phase-3 Gamification fifth story — second periodic-state layer on top of XP/streak/hearts/badges engines. Adds daily (5 templates) + weekly (3 templates) structured replayable goal system with progress tracking, auto-expiry, and reward chaining.**

- **Schema:** `AddMissionDefinitionStudentMissionProgressLog` migration adds three tables to `gamification` schema: `MissionDefinitions` catalog (unique on Code, FullAuditedEntity, 8 seed rows: 5 daily + 3 weekly); `StudentMissions` per-period instance (CASCADE delete from StudentXpProfile, RESTRICT from catalog, unique on (StudentXpProfileId, MissionDefinitionId, PeriodStartUtc)); `MissionProgressLogs` idempotency ledger (CASCADE from StudentMission, unique on (StudentMissionId, OriginEventId)).
- **`XpReason.MissionCompleted = 6`; `MissionTargetType` enum (CompleteLessons, CorrectAnswers, EarnXp, MaintainStreak, CompleteUnit).**
- **`MissionPeriodCalculator`** pure static — UTC-normalized + ISO 8601 week math. Daily key "D:yyyy-MM-dd", weekly key "W:ISOyyyy-WW". 10 unit tests.
- **`IncrementMissionProgressCommand` + handler** — probe → row-lock after → fetch under lock → per-mission idempotency check → ApplyProgress → inline completion (XpAward + RecordMissionCompleted + MarkCompleted) when target reached. Avoids nested-transaction issues from a separate command. Narrowed unique-constraint catches on both progress-log and mission-instance races (F2 fix applied).
- **3 notification handlers** in `Features/Missions/EventHandlers/` — `LessonCompletedMissionHandler` (+1 CompleteLessons, cross-module), `AnswerSubmittedMissionHandler` (+N CorrectAnswers when IsCorrect, cross-module), `StreakAdvancedMissionHandler` (+1 MaintainStreak, in-module). Each in own try/catch per ADR 0002 §3.
- **Cascade chain semantics** — Mission XP bonus can push student past level threshold → `StudentLeveledUpDomainEvent` → `StudentLeveledUpBadgeHandler` (P4-05) may award LEVEL_* badges. Bounded, terminates.
- **Practice Mode counts** — `LessonCompletedMissionHandler` + `AnswerSubmittedMissionHandler` fire regardless of Hearts. `StreakAdvancedDomainEvent` by-construction unreachable in PM (upstream gate), so MaintainStreak missions stay at 0 in PM.
- **`IStudentMissionsQuery` cross-module seam** with **lazy instantiation** — first dashboard read of period creates today's daily + this-week's weekly rows. Narrowed constraint-name catch (F2 fix). Sentinel zero-state for brand-new students.
- **`MissionStatusDto`/`MissionTargetTypeDto`/`MissionTypeDto`** drift-enums in `Shared.Contracts.Gamification` with parity unit test (F3 fix — no domain enum leak on API surface).
- **`DashboardDto`** — old `DailyMission` placeholder removed; `DailyMissions: IReadOnlyList<MissionSummary>?` + `WeeklyMission: MissionSummary?` appended positional, default-valued. Non-breaking.
- **`GET /api/Gamification/Missions/Me`** — JWT-only via `[Authorize]`. Returns `MyMissionsResponse { Daily, Weekly }` with full metadata using DTO enums (F3 fix).
- **`MissionRolloverJob`** Hangfire — `5 0 * * *` daily + `10 0 * * 1` Monday weekly. Bulk ExecuteUpdateAsync. Registered Transient.
- **Graph-nav convention 4th instance** — `AttachStudentMission` + `AttachMissionDefinition` repo methods (mirrors XpAward → HeartLoss → StudentBadge pattern).
- **`MissionSeeder`** idempotent atomic seed of 8 missions (5 daily + 3 weekly) at startup.

**Security follow-ups applied:**
- **F1 (Medium — comment):** Documented row-lock + missions-query scope alignment.
- **F2 (Medium):** Narrowed `EnsureMissionsForPeriodAsync` + `IncrementMissionProgressCommandHandler` catches to specific constraint names only (bare 23505 fallback removed by reviewer).
- **F3 (Medium):** `MissionStateDto` uses `Shared.Contracts` DTO enums (no domain enum leak).
- **F5 (Low):** Row-lock moved AFTER missions probe (no-op contention fix).

**Lead-approved decisions:**
- **D1:** 8-mission MVP (5 daily + 3 weekly).
- **D2:** Lazy instantiation on dashboard read; Hangfire rollover job defensive closeout only.
- **D3:** Practice Mode lesson completions count toward missions.
- **D4:** Dashboard surface = `DailyMissions` list + single `WeeklyMission`.

**Test results:**
- 10/10 MissionPeriodCalculator unit tests + 9/9 MissionEnumDrift unit tests = **19/19**
- **23/23** P4-06 integration tests (catalog seed, brand-new student, lazy instantiation, idempotency on dashboard re-read, /Missions/Me, anonymous 401, lesson→progress, 3 lessons→complete, idempotency, correct→progress, wrong→no progress, streak→MaintainStreak, Practice Mode counts, rollover expires, rollover idempotent, rollover preserves current, IDOR×2, level-up chain, weekly accumulates, enum drift)
- **62/62** P4-02/03/04/05 regression
- **11/11** P2-09 dashboard regression (verifies old `DailyMission` placeholder removal didn't break contract)
- **Full P4 suite: 85/85**

**Deferred items (next stories):**
- P4-07 (leagues), P4-08 (FE mission/badge/league screens + motion), P4-09 (notification nudges), P4-10 (Redis), P4-11 (streak freeze + weekly challenges), P7-03 (admin mission editor).

**In-cycle bug fixed:** Graph-nav 4th instance (`AttachStudentMission` + `AttachMissionDefinition`); inline completion to avoid nested-transaction issues from a separate CompleteMissionCommand.

---


## P4-05 — Earn badges (Batch 8 — commit + PR ready)

**Branch:** `feat/P4-05-earn-badges` (ready for committer).

**What shipped:**

**Phase-3 Gamification fourth story — first consumer of the domain events that P4-02 (XP) + P4-03 (streak) + P4-04 (hearts) shipped. Adds the achievements layer on top of the XP/streak/hearts engines.**

- **10-badge catalog** seeded idempotently at startup: FIRST_LESSON, STREAK_3/7/14/30, LEVEL_5/10/20, LEGENDARY_50, STREAK_100. `BadgeSeeder.SeedAsync` runs in `GamificationModule.InitializeAsync` after migrations in all environments.
- **New schema:** `AddBadgeDefinitionAndStudentBadge` migration adds `BadgeDefinitions` table (catalog; unique on Code, FullAuditedEntity) + `StudentBadges` table (append-only ledger, CreationAuditedEntity, unique constraint `(StudentXpProfileId, BadgeDefinitionId)`, CASCADE delete from profile, RESTRICT from catalog). `XpReason.BadgeEarned = 5`. `BadgeTriggerType` enum (FirstLesson, Streak, Level).
- **`BadgePredicateEvaluator`** pure static service (total function) — matches badge definitions against a trigger type + value, skipping already-earned ones. Mirrors `LazyHeartRefiller` / `StreakDayCalculator` / `LevelCurve` shape. 12 unit tests.
- **`AwardBadgeCommand` + handler** — row-lock + dual-layer idempotency (HasBadgeAsync pre-check + UX_StudentBadges_* unique constraint). Writes ledger row + rarity-scaled XP bonus via `XpAward(Reason=BadgeEarned)`. Narrowed `DbUpdateException` catch for safer error handling. `AttachBadgeDefinition` graph-navigation fix (3rd instance — now documented convention for any new entity navigating an existing untracked aggregate).
- **3 notification handlers** — `LessonCompletedBadgeHandler` (cross-module, Learning event), `StreakAdvancedBadgeHandler` (in-module domain event), `StudentLeveledUpBadgeHandler` (in-module). Each in own try/catch per ADR 0002 §3.
- **Cascade chain semantics** — A badge XP bonus can push the student past a level threshold, raising `StudentLeveledUpDomainEvent`, which awards the LEVEL_* badge in turn. Bounded by `alreadyEarned` filter — terminates in ≤ N badges.
- **Practice Mode by-construction** — STREAK_*/LEVEL_* badges cannot fire in Practice Mode (Hearts=0) because upstream `AdvanceStreakCommandHandler` + `AwardLessonCompletedXpCommandHandler` short-circuit at Hearts=0, never raising domain events. FIRST_LESSON CAN fire in PM since `LessonCompletedIntegrationEvent` fires regardless of Hearts.
- **`IStudentBadgesQuery` cross-module read seam** — sentinel `StudentBadgesSnapshot(0, [])` for brand-new students. `BadgeRarityDto` re-declared in `Shared.Contracts.Gamification` with parity enum drift unit test.
- **`DashboardDto` extended** — `BadgesCount: int` + `RecentBadges: IReadOnlyList<BadgeSummary>?` (positional appended, default-valued — non-breaking). Learning dashboard wiring updated.
- **New endpoint `GET /api/Gamification/Badges/Me`** — JWT-only via `[Authorize]`. Returns all 10 catalog definitions annotated with `IsEarned: bool` + `AwardedAtUtc: DateTime?`. IDOR-proof (no studentId param).

**Security follow-ups applied (security-auditor PASS-with-notes):**
- **F1 (Medium):** `AwardBadgeCommand.OriginEventType` field added for audit-trail forensics. All 3 notification handlers pass the actual triggering event type name.
- **F2 (Medium):** Confirmed `BadgeSeeder` already atomic (single `SaveChangesAsync` at end).
- **F4 (Low):** XML comment on `StudentBadge` corrected (RESTRICT delete, not CASCADE).
- **F5 (Low):** `AwardedAtUtc != default(DateTime)` validator rule added.

**Lead-approved decisions:**
- **D1:** All 10 badges (8 MVP + 2 Legendary stretch). Stretch ones appear locked from day 1.
- **D2:** Both count + recent 3 on `DashboardDto`.
- **D3:** XP bonus scaled by rarity: Common +20, Rare +50, Epic +100, Legendary +250 via `GamificationConstants.XpRewards.ForRarity`.
- **D4:** `GET /api/Gamification/Badges/Me` endpoint shipped (not deferred to FE story).

**Test results:**
- BadgePredicateEvaluator unit tests: 12/12
- BadgeRarityDto enum drift assertion in unit tests
- P4-05 integration tests: 17/17 (catalog seeded, FIRST_LESSON award, idempotency, streak cascade, level up, recent DESC ordering, IDOR × 2, Practice Mode by-construction, badge XP chain, 3-concurrent stress, dashboard envelope, seeder idempotency)
- P4-02/03/04 regression: 45/45 (T3/T4 + P403-T1/T13 + P404-H10 assertions updated for +20 FIRST_LESSON XP)
- Full P4 suite: 62/62
- Full integration suite: 560/568 (only 8 pre-existing failures unchanged — P2-02 TC-1, P2-04 TC-09, P2-09 C11, AC-DEF-2, AC-RL-6, AC-4 WeakPassword, AC-2c, TC-1/ForGrade)

**New conventions to carry forward:**
- **`[NotificationHandler<TDomainEvent>] → [mediator.Send(Command)] → [UoW commits]` pattern** — generalizes for ANY in-module domain event consumer. Future stories (e.g. P4-08+) can mirror this shape.
- **Graph-navigation attach pattern** — now the third instance (XpAward → HeartLoss → StudentBadge). Established as documented convention: always `_repo.AttachEntity(existing)` before `Entity.Create()` when an entity navigates an existing untracked aggregate to prevent EF duplicate-INSERT.

**Deferred items (next stories):**
- P4-06 (missions), P4-07 (leagues), P4-08 (FE badge pop-in + collection screen), P4-09 (BadgeEarned nudge consumer), P4-10 (Redis), P7-03 (admin badge catalog editor).

---


## P4-04 — Hearts + Practice Mode (Batch 3b FE — dashboard data flip)

**Branch:** `feat/P4-04-hearts-practice-mode` (current — ready for reviewer/committer).

**What changed:**

- **swagger.json** — manually updated committed snapshot: `DashboardDto` now includes `level` (int, from P4-02 BE — was missing from snapshot), `hearts` (int, P4-04), `inPracticeMode` (bool, P4-04). Regen was blocked (no pnpm/nswag runtime in CI shell); manually patched as documented fallback.
- **`nswag-client.ts`** — manually added `level?: number`, `hearts?: number`, `inPracticeMode?: boolean` to `DashboardDto` interface (only these 3 fields added; rest of file untouched).
- **`DashboardHeader`** (`packages/ui`) — 3 new optional props: `inPracticeMode`, `practiceModeLabel`, `practiceModeAccessibilityLabel`. Inline pill: `$warningSoft` bg / `$warning` text, `borderRadius={9999}`, rendered between Hearts and StreakFlame when `inPracticeMode && practiceModeLabel`. No animation.
- **`apps/student-app/app/(child)/index.tsx`** — `hearts={3}` replaced with `dashboardQuery.data?.hearts ?? 5`; `weeklyLevel={1}` replaced with `dashboardQuery.data?.level ?? 1`; `inPracticeMode`/`practiceModeLabel`/`practiceModeAccessibilityLabel` props wired. `statsA11y` hearts value also wired to real data.
- **`packages/shared/src/i18n/resources.ts`** — added `child.home.practiceMode` + `child.home.practiceModeA11y` in both EN and AR.

**Key decisions:**
- **Fallback `?? 5` for hearts** — BE contract is non-null int with default 5 (cap). The `?? 5` handles the TS optional typing from nswag `markOptionalProperties: true`.
- **Fallback `?? false` for inPracticeMode** — same reason.
- **`$warningSoft` / `$warning` tokens** — existing semantic tokens used by MissionBanner and other components. Matches the amber/yellow design intent without introducing new tokens.
- **Pill is inline in `DashboardHeader`** — not promoted to a new primitive (scope tight per task instructions).
- **No regen via nswag** — nswag runtime requires .NET 9 installed and pnpm installed; neither available in CI shell. Manual edit of swagger.json snapshot + nswag-client.ts documented and scoped to exactly the 3 new fields.

**Important for next regen:** When the backend next emits swagger (via `refresh:swagger`), the snapshot will include `level`/`hearts`/`inPracticeMode`. Running `gen:api` will regenerate the full file from scratch (overwriting the manual edits). The hand-added JSDoc comments in `nswag-client.ts` will be lost but the fields themselves will be present from the swagger.

**Not in scope (P4-08):**
- Hearts animation / shake on depletion
- Regeneration countdown timer
- "Out of hearts" bottom sheet

---

## Wave 13 — Phase 2 FE closer: student home dashboard (P2-09-FE, ready for PR)

**Branch:** `feat/W13-P2-09-FE` (based off `feat/W12-P2-05-06-07-FE`, PR pending).

**What's on the branch:**
- **BE annotations** — `DashboardController.Get` + `StudentsController.studentAttempts(studentId)` got `[ProducesResponseType(typeof(BaseResponse<TDto>), 200)]`. Behavior unchanged; NSwag now emits typed clients.
- **api-client regenerated** — `dashboard()` returns `DashboardDtoBaseResponse`; `attempts(studentId)` returns `AttemptListItemDtoListBaseResponse`. New type re-exports: `DashboardDto`, `ContinueTargetDto`, `DailyMissionDto`, `LeaguePreviewDto`.
- **1 new `@learnexia/api-client` hook**: `useDashboard()` — single endpoint, BE composes Continue/streak/XP/etc. server-side (we don't compose client-side).
- **3 new `@learnexia/ui` primitives**:
  - `DashboardHeader` — greeting + grade caption + stats strip (Hearts/StreakFlame/XPBar). `childName` is informational-only (optional); `greetingText` is the rendered string the caller composes.
  - `ContinueCard` — tap-to-resume; renders subject icon + lesson title + chevron CTA; logical `end={14}` boss badge; hidden when `continue=null`.
  - `MissionBanner` — built but **never rendered in Phase 2** (`dashboardQuery.data.dailyMission` is always `null`; Phase 4 wires it).
- **`SubjectsListSection`** extracted from W11 `(child)/index.tsx` into `apps/student-app/app/(child)/_components/SubjectsListSection.tsx` (W11 logic intact: defensive 4-subject filter, shimmer/error/empty, RTL). Helper moved to `(child)/_components/subjects.ts`.
- **`apps/student-app/app/(child)/index.tsx`** rewritten as dashboard composition: TopBar → DashboardHeader → ContinueCard (conditional) → SubjectsListSection. Hearts fixed `3` (TODO P4-04); streak/XP default `0` (TODO P4-02/03). Loading state composes `meQuery.isLoading || dashboardQuery.isLoading` for both header AND subjects section.
- **i18n** — expanded `child.home.*` namespace with 18 new EN+AR keys (greeting, gradeCaption, continueTitle, continueCta, yourSubjects, welcomeEmpty, errorRetry, statsA11y, etc.). No fork — extends existing namespace.
- **Reviewer FAIL → fixes applied** — 3 blockers cleared: dropped dead required `childName` from `DashboardHeader` (now optional), replaced ContinueCard physical `right`/`left` with logical `end={14}`, added Wave 13 section to HANDOFF (this).

**Key decisions:**
- **Single dashboard endpoint, no client-side composition.** BE resolves Continue (most-recent-attempt → engine → first Available lesson → cross-subject fallback). Avoids client-side races + duplicate heuristics.
- **All Phase-4 features stub-only** (hearts decrement / streak increment / XP / mission / league) — display surfaces, no endpoint calls, TODO comments with story IDs.
- **`MissionBanner` built but not rendered** — Phase 4 will mount it when BE returns non-null `dailyMission`.
- **`SubjectsListSection`** is a new local component (not promoted to `@learnexia/ui`) since only one consumer.

**Non-blocking follow-ups** (chore PR / next wave):
- `ContinueCard` could swap `Pressable` → Tamagui `Stack` w/ `hoverStyle`/`pressStyle` for web hover lift (mirroring `LessonCard` pattern).
- Stats strip `accessibilityRole="summary"` should be `"group"` via `Platform.OS === 'web' ?` gate (W12 carry-forward).
- Dashboard mount fade-in (240ms `opacity 0→1`, reduced-motion gated) per design spec §5.
- Append boss suffix to `continueA11y` when `continueTarget.isBoss`.
- Consolidate `SubjectKey` type (duplicated in `SubjectRow` + `ContinueCard`).
- `useDashboard` invalidation seam on `LessonCompletedIntegrationEvent` will be wired in P4-02 wave when XP/streak/hearts go live.

---

## Wave 12 — Phase 2 FE lesson + quiz + feedback (P2-05/06/07-FE, ready for PR)

**Branch:** `feat/W12-P2-05-06-07-FE` (based off `feat/W11-P2-02-P2-03-FE`, PR pending).

**What's on the branch:**
- **BE annotations** — `LessonsController` + `QuizzesController` got `[ProducesResponseType(typeof(BaseResponse<TDto>), 200)]` on the 5 student-facing endpoints (single-lesson GET, Attempt, Answers, Complete, Abandon). Behavior unchanged; NSwag now emits typed clients (was `Promise<void>`).
- **api-client regenerated** — new typed methods `lessonsGET(id)`, `attempt(lessonId)`, `answers(attemptId,body)`, `complete(attemptId)`, `abandon(attemptId)`. New types: `SingleLessonResponse`, `StartAttemptResponse`, `SubmitAnswerCommand`, `SubmitAnswerResponse`, `AttemptSummaryDto`, `QuestionType` enum.
- **5 new `@learnexia/api-client` hooks**: `useLesson(lessonId)`, `useStartAttempt()`, `useSubmitAnswer(attemptId)`, `useCompleteAttempt()`, `useAbandonAttempt()`. Extended `queryKeys.learning.lesson(id)` + `learning.dashboard()` (forward-compat for W13).
- **8 new `@learnexia/ui` primitives**: `QuestionCard`, `MCQOption`, `TrueFalseChoice`, `FillInBlank`, `MatchingPanel` (stub — BE has no Matching seed), `AnswerFeedbackStrip` (alert + live region), `AttemptSummaryCard`, `ProgressDots` (progressbar role).
- **Lesson Player** at `apps/student-app/app/(child)/lessons/[lessonId].tsx` — single route, 3-stage state machine (`intro → quiz → summary`):
  - **Intro**: `useLesson(lessonId)`, hearts widget (fixed 3 — Wave 3 wires decrement), Start CTA.
  - **Quiz**: `useStartAttempt` on Start; one question at a time via plain `switch(questionType)` (NOT Strategy); locked-after-submit; correct → 800ms auto-advance, incorrect → "Next" CTA.
  - **Summary**: `useCompleteAttempt` on last advance; `AttemptSummaryCard` with score/accuracy/duration + "+10 XP" stub (TODO P4-02 — no XP endpoint). "Back to subject" navigates to `/(child)/subjects/{?subjectId}`; "Try again" re-fires `useStartAttempt`.
  - Abandon called fire-and-forget on unmount mid-quiz (idempotent).
  - Hint button visible-disabled with "Hint coming in v2" helper (TODO P3-05 — no hint endpoint).
- **Navigation seam** — `apps/student-app/app/(child)/subjects/[subjectId]/index.tsx` now passes `?subjectId=` on lesson tap-Available so Summary can route back cleanly.
- **i18n** — added 37 EN+AR keys under `child.lessons.intro.*`, `child.quiz.*`, `child.feedback.*`, `child.summary.*`, `child.lessons.a11y.*`. Deleted obsolete `child.lessons.stub.*`.
- **Reviewer PASS** → `docs/briefs/W12-P2-05-06-07-FE-review.md`. 0 blockers. Polish applied inline: removed dead constants/variables (nit-1, nit-2), added `maxWidth=720` + centering to all 3 stages (should-fix #2). Carry-forward: reduced-motion gate (should-fix #1) and a11y region/group roles (nits 3+4 — RN's `AccessibilityRole` union doesn't include those web ARIA values; defer to a web-only polish PR).

**Key decisions:**
- **Switch-on-questionType, not Strategy** — plain JSX switch in render. Adheres to rule #8.
- **Single-route view-state machine** over multi-route (cleaner back-stack; spec recommended).
- **Hearts/XP/Hint slots are display-only** — Wave 3 (Gamification) and Wave 4 (AI Tutor) own the real wiring.
- **MatchingPanel = stub** because BE has zero Matching questions seeded (P2-08 brief).
- **Abandon = fire-and-forget mutation** (BE is idempotent on terminal).

**Non-blocking follow-ups** (chore PR):
- Wire `AccessibilityInfo.isReduceMotionEnabled()` into `AnswerFeedbackStrip` translate + lesson screen 1200ms timer (currently always 800ms).
- `AttemptSummaryCard` + `QuestionCard` web ARIA roles (`region`/`group`) need a web-only Platform.OS gate or a custom `aria-*` prop bypass since RN's TS union rejects them.
- Replace `xpStub` "+10 XP" when Wave 3 XP service lands.
- Implement real Matching renderer when BE seeds Matching questions.
- Confetti / mascot illustration on Summary (deferred to W14 polish).
- Markdown-rendering in question stem (currently plain text).

---

## Wave 11 — Phase 2 FE student-facing browse (P2-02-FE + P2-03-FE, ready for PR)

**Branch:** `feat/W11-P2-02-P2-03-FE` (off main, PR pending).

**What's on the branch:**
- **BE `MeResponse.Grade : int?`** — Identity `MeResponse` DTO + `GetMeQueryHandler` populate `Grade` from `User.Grade` (already on the entity). 2 new integration tests in `P1_09_Me_Tests.cs` (child Grade returned, parent null). All 18 P1-09 tests green.
- **BE `[ProducesResponseType]` on `SubjectsController`** — the 3 student-facing endpoints (`ForGrade`, `{id}/Lessons`, `{id}/SkillTree`) gained `[ProducesResponseType(typeof(BaseResponse<List<...Dto>>), 200)]` so NSwag emits typed clients (previously `Promise<void>`). Pattern matches Identity's `UsersController.Me`.
- **api-client regenerated** — new methods `forGrade`, `lessons`, `skillTree`; new types `StudentSubjectDto`, `UnitWithLessonsDto`, `LessonInUnitDto`, `ConceptNodeDto`, `SkillNodeDto`, `MissingPrerequisiteDto`, `NodeState` enum (int: 0=Locked, 1=Available, 2=Completed); `MeResponse.grade?: number`.
- **3 new `@learnexia/api-client` hooks**: `useSubjectsForGrade(grade)`, `useSubjectLessons(subjectId)`, `useSubjectSkillTree(subjectId)`. New `queryKeys.learning.*` namespace.
- **4 new `@learnexia/ui` primitives** + 1 Badge variant:
  - `SubjectRow` — student-facing subject card.
  - `LessonCard` — vertical card; state pill via `NodeState`; logical `end={14}` lock + Boss badges.
  - `SkillTreeNode` — 72px disc + state visuals + `hasMissingPrereqs` + `isBoss` overlay.
  - `Badge variant="boss"` — 👑 Boss pill.
  - `SegmentedTabs` — horizontal segmented control (sibling of `Tabs`).
- **New tokens** in `colors.ts`: per-subject tint + 3 glow shadow tokens.
- **i18n** — EN + AR under `child.subjects.*`, `child.skillTree.*`, `child.lessons.stub.*`.
- **Student-app screens:**
  - `(child)/index.tsx` — Subjects list (grade from `useMe`, defensive 4-subject filter, shimmer skeletons gated on `meQuery.isLoading || subjectsQuery.isLoading` so no empty-state flash).
  - `(child)/subjects/[subjectId]/_layout.tsx` + `index.tsx` (Lessons) + `tree.tsx` (Skill Tree) — `SegmentedTabs` shell + Unit-grouped lessons + concept-grouped skill nodes. In-memory boss derivation by joining lessons + tree on `skillId`.
  - `(child)/lessons/[lessonId].tsx` — STUB (Wave 12 replaces).
  - `(child)/_components/WhyLockedSheet.tsx` — inline (NOT in `@learnexia/ui`); web modal / native bottom sheet; tokens via `colors` import.
- **Reviewer PASS after fixes** → `docs/briefs/W11-P2-02-P2-03-FE-review.md`. Fixed: 3 raw-hex/physical-position blockers (`WhyLockedSheet` CTA + overlay + card bg via tokens; `LessonCard` `end={14}` logical pos), should-fix Me loading flash, should-fix RTL chevron in lesson stub.

**Key decisions:**
- `SegmentedTabs` shipped as a **sibling primitive** to `Tabs` (not a refactor) per design spec "smaller diff" guidance.
- Boss derivation is **in-memory** (not BE join) — both queries already fire on the screen.
- Lesson screen is a stub until Wave 12.
- No `api-tester`/`security-auditor` (no new BE endpoints with new risk surface; covered by existing P1-09 + P2-02 BE tests).

**Non-blocking follow-ups** (chore PR):
- `SkillTreeNode` still has 3 raw-hex disc colors + shadow strings (tokens added but not yet wired). Wire next pass.
- `WhyLockedSheet.lockedItemName` prop declared but not rendered.
- Native pulse animation on `SkillTreeNode` Available state = web-only CSS keyframe (no native pulse this wave).
- `useSubjectsForGrade` empty-state copy when BE returns zero subjects for a valid grade (currently identical to no-grade state).

---

## Wave 10 — Phase 2 FE start (P2-12-FE, merged via PR #69)

---

## Wave 10 (BE track) — Phase 3 Gamification kickoff (P4-02-BE, merged via PR #73)

### P4-02 — Earn XP and level up ✅ Merged via PR #73

**What's on main (PR #73):**

**Phase 3 Gamification kickoff — waking up the Gamification module skeleton and landing the first real business feature: XP engine + ledger + level computation.**

- **Module wake-up** ✅ Added 4 Gamification csproj (Domain/Application/Infrastructure/Api) to `Learnexia.Modular.sln` + `Modules\Gamification` solution folder. Added `using Learnexia.Modules.Gamification.Api` + `builder.Services.AddGamificationModule(builder.Configuration)` to `Program.cs`. Added Gamification's `AssemblyReference` to the cross-module MediatR scan in `AddCrossModuleMediatR()`.

- **New `gamification` schema** ✅ `StudentXpProfiles` table: `Id (int)`, `StudentId (int, unique)`, `XpTotal (int, default 0)`, `Level (int, default 1)`, `UpdatedAt (DateTime)` + FullAuditedEntity columns. `XpAwards` table (append-only ledger): `Id (int)`, `StudentId (int)`, `Amount (int)`, `Reason (XpReason enum, int)`, `OriginEventId (uuid)`, `OriginLessonId (int?, nullable)`, `OriginSkillId (int?, nullable)` + FullAuditedEntity columns. Migration `20260530042656_InitGamification`. **Idempotency at DB layer:** unique index `UX_XpAwards_OriginEventId_Reason` on `(OriginEventId, Reason)` — prevents double-award for duplicate event delivery.

- **XP rules (lead-approved SRS examples)** ✅ `GamificationConstants.XpRewards`: `CorrectAnswer = 10`, `LessonCompleted = 50`, `QuizCompleted = 20` (stub — no quiz boundary yet), `StreakBonus = 30` (stub — P4-03 owns streak engine). Stored in static class at `Domain/Constants/GamificationConstants.cs`.

- **Level curve (lead-approved table-based ramp)** ✅ `LevelCurve` pure static service at `Domain/Services/LevelCurve.cs`. Table: `[0, 100, 250, 500, 1000, 2000, 4000, 7000, 11000, 16000]` cumulative XP thresholds for L1–L10. L11+ formula: `10 + ((xp - 16000) / 5000)` (floor). 32 unit tests in `LevelCurveTests.cs`. Testable in isolation — no DB access.

- **Integration-event handlers** ✅ `LessonCompletedIntegrationEventHandler` + `AnswerSubmittedIntegrationEventHandler` at `Application/IntegrationEventHandlers/`. Both subscribe to cross-module events from Learning (P2-07 producers) via `INotificationHandler<T>`. Each handler sends an internal `ICommand` via `IMediator` (Pattern A — runs through Gamification's `UnitOfWorkBehavior` for clean commit boundary and audit stamping). Idempotency: pre-check + catch on unique-constraint violation (AC4).

- **New `GET /api/Gamification/Profile`** ✅ JWT-only endpoint (no studentId param; IDOR-proof by construction). Returns `StudentProfileDto { XpTotal: int, Level: int, XpToNextLevel: int }`. Fresh students (no `StudentXpProfile` row yet) see clean L1 + 0 XP, not 404.

- **`IStudentXpQuery` cross-module read seam** ✅ Defined in `Shared.Contracts/Gamification/IStudentXpQuery.cs` (returns `StudentXpSnapshot? { XpTotal: int, Level: int }`). Implemented in `Gamification.Infrastructure/Queries/StudentXpQuery.cs` against `GamificationDbContext`. Learning's `GetDashboardQueryHandler` now injects `IStudentXpQuery` and reads real XP + Level instead of the P2-09 zero-state placeholders `(Xp: 0, Streak: 0)`. Brand-new students still see `(0, 1)` via null mapping. **New field:** `DashboardDto.Level : int = 1` added to positional record (appended last, maintains compat).

- **Cross-module UoW assembly-filter guard (bug fix)** ✅ **Critical fix discovered during P4-02 implementation.** All 4 module `UnitOfWorkBehavior` implementations (Identity/Learning/Parent/Gamification) now early-return if the command's assembly isn't theirs. **Without this guard, nested `mediator.Send` across modules causes `BeginTransaction on already-in-transaction` failures.** This latent bug was never triggered before P4-02 because no cross-module command dispatch existed. Applied retroactively to Identity, Learning, and Parent modules in this PR.

- **Security follow-ups (per security-auditor PASS)** ✅ Applied 3 findings from this PR's security audit:
  - **F1 (Medium):** Row-lock strategy changed from `FOR UPDATE SKIP LOCKED` to `FOR UPDATE` (block-and-wait prevents lost-update race on `StudentXpProfile.XpTotal`).
  - **F2 (Medium):** Removed child accuracy% from Info logs (child-privacy minimization).
  - **F3 (Low):** Removed dead `CorrectAnswerCount` field from `AwardLessonCompletedXpCommand`.

**Test results:**
- LevelCurve unit tests: **32/32** ✅
- P4-02 integration tests: **16/16** ✅ (T1 correct-answer award, T2 wrong-answer no-award, T3 100% lesson, T4 50% lesson, T5/T6 idempotency, T7/T8 level-up, T9 zero-state, T10 real values, T11 IDOR, T11b sibling-handler isolation, T12 dashboard real XP, T13 dashboard zero-state, envelope + auth sanity)
- Full integration suite: **517/520** (only 3 pre-existing failures, same as `main`: P2-02 TC-1, P2-04 TC-09, P2-09 C11)

**Key decisions locked (all lead-approved):**
- **Q1:** NEW Gamification module — wake up the existing skeleton (approved to add to `.sln` + DI + MediatR).
- **Q2:** `IStudentXpQuery` via `Shared.Contracts/Gamification/` — mirrors `IParentChildQuery` pattern. Learning injects it; future P4-10 swaps implementation for Redis without changing dashboard handler.
- **Q3:** XP values from SRS FR-GM-1 examples: `+10/+50/+20/+30`; table-based level curve approved (L1–L10 via table, L11+ formula).
- **Q4:** Ship `GET /api/Gamification/Profile` endpoint.
- **Q5:** Pattern A — notification handler → `ICommand` → UoW (decoupled from producer's UoW).
- **Q6:** Add `Level` to `DashboardDto` positional record.
- **Q7:** `SELECT ... FOR UPDATE` row-lock on `StudentXpProfile` in command handler.
- **Q3.bis:** `LessonCompleted` XP fires unconditionally on completion (regardless of correct-answer count).

**New conventions to carry forward:**
- **UoW assembly-filter guard is now mandatory** for all modules' `UnitOfWorkBehavior`. Future modules must early-return if the command assembly doesn't match theirs — prevents cross-module transaction interference.
- **Cross-module event handler pattern:** send an `ICommand` via `IMediator` (Pattern A), not direct DbContext writes (decouples commits, enables audit stamping and domain-event dispatch).

**Not in scope (next stories):**
- Streak (P4-03), Hearts (P4-04), Badges (P4-05), Missions (P4-06), Leagues (P4-07).
- XP bar UI animations / confetti (P4-08).
- Redis hot-path read model (P4-10).
- Frontend dashboard render of `Level` field (folded into P2-09-FE or separate FE story).

**Pre-existing test failures (tracked separately, not regressions):**
- P2-02 TC-1, P2-04 TC-09, P2-09 C11 — logged; not blocking Phase 3.

### P4-03 — Maintain a daily streak ✅ Batches 1–7 complete, open as PR #75

**What's on branch `feat/P4-03-daily-streak` (ready for PR):**

- **Schema:** `AddStreakColumns` migration adds `CurrentStreak`, `LongestStreak`, `LastActivityDateUtc : DateOnly?` to `gamification.StudentXpProfiles`. Migration timestamp `20260530091454`.
- **`ISystemClock` abstraction** in `Shared.Kernel/Abstractions/` (universal date-testability primitive; UTC impl `SystemClock` in Gamification.Infrastructure).
- **`StreakDayCalculator`** pure static service with `Transition` enum (`NoOp | FirstActivity | Advance | Reset | OutOfOrder`) — total function, no exceptions. `Classify(lastActivityDate, today)` is the single source of truth for the day-boundary decision.
- **Domain mutation methods** on `StudentXpProfile`: `AdvanceStreak(today)` + `ResetStreakAndStart(today)`. Streak setters narrowed to `internal set`.
- **`AdvanceStreakCommand` + handler** — handler calls `StreakDayCalculator.Classify` and switches on `Transition`. Idempotency via `HasXpAwardAsync` pre-check + narrowed `DbUpdateException when constraintName` catch (F2 fix).
- **`LessonCompletedIntegrationEventHandler` extended** — `AwardLessonCompletedXpCommand` and `AdvanceStreakCommand` each sent in their own try/catch (failure isolation per ADR 0002 §3).
- **StreakBonus +30 XP** rides via existing `XpAward` ledger with `Reason = XpReason.StreakBonus = 4`. Same `UX_XpAwards_OriginEventId_Reason` unique index covers idempotency.
- **`StreakSweepJob`** Hangfire recurring at `5 0 * * *` UTC (00:05 daily UTC). Bulk `ExecuteUpdateAsync` resets `CurrentStreak=0` for `LastActivityDateUtc < today - 1 day`. Registered Transient, uses `IServiceScopeFactory.CreateAsyncScope` for fresh DbContext per run. **Does NOT raise `StreakBrokenDomainEvent`** — bypass of EF change tracker is intentional, deferred to P4-09.
- **`IStudentStreakQuery` cross-module seam** in `Shared.Contracts/Gamification/` (mirrors P4-02's `IStudentXpQuery`). Returns `StudentStreakSnapshot(CurrentStreak, LongestStreak, LastActivityDateUtc)` — no StudentId field (F8 cleanup applied from start).
- **Learning dashboard wiring**: `GetDashboardQueryHandler` injects `IStudentStreakQuery`, dashboard `Streak` field now real. Brand-new students still see `Streak=0` via null mapping.
- **`StreakOptions` config** (`Gamification:Streak` in appsettings) with `TimeZoneId="UTC"` + `DailyJobCron="5 0 * * *"`. TZ-aware calculator means future per-user TZ is a config swap.

**Test results:**
- `StreakDayCalculatorTests`: 13/13 unit tests
- `P4_03_DailyStreak_Tests`: 15/15 integration tests (advance / reset / same-day no-op / idempotency / sweep job / dashboard wiring / cross-student isolation / AnswerSubmitted no-advance)
- `P4_02_EarnXpAndLevelUp_Tests` regression: 16/16 (T3/T4 updated to include +30 StreakBonus in expected totals — correct behavioral change)
- Full integration suite: **532/535** (3 pre-existing failures unchanged)

**Lead-approved decisions:**
- **D1:** Day-boundary = **UTC** (Identity has no TimeZoneId yet; defer per-user TZ).
- **D2:** Activity trigger = **lesson completion only** (`AnswerSubmittedIntegrationEvent` is XP-only, doesn't touch streak).
- **D3:** StreakBonus +30 XP fires **every day the streak advances** (including day-1 brand-new and post-reset day-1).
- **D4:** Sweep job **ships in P4-03** — handler is source of truth (lazy advance/reset on next activity); Hangfire is defensive observability.

**Security follow-ups applied in this PR:**
- **F1 (Medium):** `AdvanceStreakCommandHandler` now calls `StreakDayCalculator.Classify` and switches on `Transition` (was inline if/else duplicating the calculator's logic). Calculator is now total via new `OutOfOrder` transition.
- **F2 (Medium):** `catch (DbUpdateException)` narrowed via `when` clause checking constraint name — unrelated DB errors no longer silently swallowed.
- **F3 (Low):** `StreakSweepJob` registration changed Scoped → Transient.

**Not in scope (future stories):**
- Streak freeze / weekly challenges → P4-11
- `StreakBrokenDomainEvent` consumer + sweep-time domain dispatch → P4-09
- Redis hot-path read model → P4-10
- Per-user TZ (requires Identity schema change) → no story yet
- Hearts (P4-04), Badges (P4-05), Missions (P4-06), Leagues (P4-07)
- Gamification UI motion (P4-08), Re-engagement notifications (P4-09)

---

## Wave 10 (FE track) — Phase 2 FE start (P2-12-FE, merged via PR #69)

### P2-12-FE — Parent Settings tabs (Notifications / Linked children / Security / Plan)

**Branch:** `feat/W10-P2-12-FE-settings-tabs` — merged to main.

**What's on the branch:**
- **`Switch` primitive** added to `@learnexia/ui` — 44×24 track + 20px thumb, on=`$primary` w/ `$primaryGlow`, off=`$cardSoft`, thumb=`$fg1`, 160ms `cubic-bezier(0.16,1,0.3,1)`, logical-RTL thumb via `insetInlineStart`, `accessibilityRole="switch"` + `accessibilityState={checked,disabled}`, 44px min touch target, focus outline 2px `$primary`. Mirrors `CheckboxField` prop shape.
- **8 new `@learnexia/api-client` hooks** + new `queryKeys`: `useNotificationPreferences`, `useUpdateNotificationPreferences` (optimistic w/ rollback), `useUpdateChild`, `useUnlinkChild`, `useChangePassword` (targets `/api/Users/Account/ChangePassword` — NOT the stale admin `changePasswordForUser`), `useMySessions`, `useSignOutOtherSessions` (invalidates sessions), `useMyPlan`.
- **api-client regenerated** against running BE — `myChildren` route moved to `/api/Parent/My-Children` (the legacy `/api/Users/Parent/*` shape is gone). All P2-12 endpoints present.
- **4 Settings panels** under `apps/student-app/app/(parent)/_components/settings/`:
  - `NotificationsPanel.tsx` — 4-row × 2-toggle (Email/Push) grid for the 4 BE categories (WeeklyReport / StreakAtRisk / ProductAnnouncement / Achievement). Optimistic toggle with rollback. Full-array PUT body (BE validator requires all 4 categories distinct).
  - `LinkedChildrenPanel.tsx` — `ChildCard` per child + inline Edit form (fullName/grade/language/country) + **inline Unlink confirm strip** (NOT a Dialog, per rule #8). Add Child CTA → `/(onboarding)/add-child`. Empty state when no children.
  - `SecurityPanel.tsx` — Change-password form (current/new/confirm + `PasswordStrengthMeter`, `forceLtr`, correct `autoComplete` attrs) + Sessions list (truncated 8-char id in `dir="ltr"`, locale-formatted `expiresAt`, Active/Expired pill) + Sign-out-others CTA (success strip counts other sessions captured pre-mutation).
  - `PlanPanel.tsx` — read-only plan name + status badge; "Manage subscription" disabled with `TODO(P2-12-PAYMENTS)` until a payments BE lands.
- **i18n** — every new copy slot keyed in EN + AR under `parent.settings.{notifications,linkedChildren,security,billing}.*`.
- **`SettingsWeb.tsx`** — `renderActivePanel()` switch replaces the 4 `ComingSoonPanel` stubs; Profile + Language untouched.
- **Security audit** ✅ PASS-WITH-FOLLOWUPS — `docs/briefs/W10-P2-12-FE-security-audit.md`. 0 Critical/High. Fixed inline: F-01 (i18n key for "No active sessions"), F-02 (`refetch()` → `invalidateQueries`), F-04 (stale `sessions.length - 1` count captured pre-mutation). Carry-forward: F-03 (missing `Stack.Screen name="settings"` in `(parent)/_layout.tsx` — pre-existing gap), F-04 (toolchain `tar` advisory — not bundled to runtime).
- **Reviewer** ✅ PASS conditional — `docs/briefs/W10-P2-12-FE-review.md`. All blockers (i18n, security gate, HANDOFF) cleared. Build/type-check/lint clean across `@learnexia/{api-client,ui,shared}` + `student-app`.

**Key decisions:**
- **No Dialog primitive** — Unlink uses inline confirm strip inside `ChildCard` per rule #8 (no design-pattern unilateral additions).
- **No `Badge` variant extension** — plan/session status pills are inline `Stack`+`Text` w/ same tokens (`$successSoft`/`$success`, `$dangerSoft`/`$danger`, `$cardSoft`/`$fg3`) since `Badge` only ships achievement-disc variants today.
- **No payments integration** — Plan tab is read-only; Manage CTA disabled.
- **Edit-child form opens with empty grade/language/country** because `LinkedChildResponse` only exposes `{id, fullName, email}` — the BE seam doesn't return grade/language/country on parent's My-Children list (carry-forward to BE if product wants pre-fill).
- **Sessions list shows truncated id only** — BE `SessionInfo` has no device/IP/UA metadata. Carry-forward if richer audit UI needed (P6-06).
- **Brand new Switch primitive added directly on this branch** (rather than cherry-picking from the un-merged `feat/design-system-pixel-align`).

**Non-blocking follow-ups** (recorded above; route to a chore PR):
- F-03: declare `<Stack.Screen name="settings" />` in `(parent)/_layout.tsx` (pre-existing gap, not introduced by W10).
- F-04: track `tar` upgrade via `expo` release cadence (toolchain only, not bundled).
- Extract panel `PanelSurface`/`PanelHeader` to `settings/shared.tsx` when convenient (currently duplicated across 4 panels + `SettingsWeb`).
- `Switch.hideLabel` uses `opacity: 0` (keeps label in layout flow); design spec suggested `clip` — fine for now since Notifications never passes `hideLabel`.

---

## Wave 9 — Phase 2 backend (in progress)

### P2-03 — Navigate the skill tree (boss flag) ✅ Batches 1–3 complete, PR pending

**What's on branch `feat/P2-03-navigate-skill-tree` (ready for PR):**
- **Schema** ✅ `Lesson.IsBoss : bool` non-nullable with `defaultValue: false`. Migration `20260529231653_AddLessonIsBoss` in `learning` schema (single `AddColumn` op).
- **`LearningSeeder.MarkBossLessonsAsync`** ✅ called from `SeedAsync` after `SeedDemoLessonContentAsync`. Marks the highest-`SequenceOrder` lesson in each Unit as boss (one per Unit). Idempotent + drift-prevention (also resets `IsBoss = false` if the wrong lesson got marked). 66 boss rows / 162 total / 66 units (one per unit, confirmed in tests).
- **3 DTOs extended** ✅ `LessonInUnitDto.IsBoss` (`{ get; init; }`), `SingleLessonResponse.IsBoss` (`{ get; set; }` matching parent `LessonDto` style), `ContinueTargetDto.IsBoss` (positional record member, appended last).
- **2 handlers populate `IsBoss`** ✅ `GetSubjectLessonsQueryHandler` in 3 construction sites (authenticated happy path, authenticated defensive fallback, anonymous fallback); `GetDashboardQueryHandler` in `TryResolveContinueForSubjectAsync`.
- **AutoMapper profiles** — verified: `Lesson → SingleLessonResponse` flows `IsBoss` by-name (no `ForMember` needed); `LessonInUnitDto` and `ContinueTargetDto` are hand-projected.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_03_SkillTreeBoss_Tests.cs` — 5 cases: seeder boss-count == unit-count, Math G1 Lessons endpoint per-unit boss invariant, boss-lesson GET returns `isBoss=true`, non-boss GET returns `false`, seeder idempotency. **One-line edit** to `P2_09_HomeDashboard_Tests.cs` C03 — asserts `continue.isBoss == false` for the fresh-student case (root lesson is `SequenceOrder=1`, not a boss). **Full Wave-7+8+9 regression: 87/87 PASS** (~3m, Testcontainers Postgres pg16).

**Key decisions:**
- **Q1 → `Lesson.IsBoss` (NOT `Skill.IsBoss`)** — story says "end-of-unit challenge"; units own lessons.
- **Q4 — `NodeState` enum unchanged** at Locked/Available/Completed. Boss is orthogonal (a boss lesson can be in any of the 3 states).
- **Q3 — Seeder rule:** highest-`SequenceOrder` lesson per Unit.
- **Q8 — Skip `HasBoss` rollup** on `SkillNodeDto`/`ConceptNodeDto` — FE renders boss on lesson cards only.
- **Q11 — No admin endpoint** to toggle `IsBoss` — deferred to P7-03.

**Status check — BE-1 and BE-2 were ALREADY DONE via P2-04:**
- **BE-1 (per-node state):** `LearningPathEngine` + `GetSubjectSkillTreeQueryHandler` already compute `Locked/Available/Completed` for skills + concepts + lessons. 95% shipped via PR #63.
- **BE-2 (why-locked):** `SkillNodeDto.MissingPrerequisites` and `LessonInUnitDto.MissingPrerequisites` already populated. 100% shipped.
- **BE-3 (boss flag):** the only real new work in P2-03. Done.

**Non-blocking follow-ups** (carry forward):
- P7-03 admin curriculum console: provide UI to toggle `IsBoss` per lesson.

### P2-09 — Home dashboard ✅ Merged via PR #67

Wave-9 story 2, now on main. `GET /api/Learning/Dashboard` returns XP/Streak (= 0 in Phase 2; TODOs for P4-02/P4-03), Mission/League (= null; TODOs for P4-06/P4-07), and `Continue` (most-recent-Attempt subject → engine → first Available lesson; cross-subject fallback Math/Science/Arabic/English; default Grade-1 Math when no attempts). New repo method `GetMostRecentActivitySubjectIdAsync`. 11 integration tests including cross-student IDOR isolation. See `docs/briefs/P2-09.md` + `docs/plans/P2-09.md`.

### P2-09 — Home dashboard ✅ Batches 1–2 complete, PR pending

**What's on branch `feat/P2-09-home-dashboard` (ready for PR):**
- **`DashboardController`** ✅ new `GET /api/Learning/Dashboard` `[Authorize]` (any role; per-student via `_currentUser.UserId` — no studentId param, IDOR-proof by construction).
- **`GetDashboardQuery` + Handler** ✅ parameterless query → `DashboardDto { Xp:int=0, Streak:int=0, DailyMission:DailyMissionDto?=null, LeaguePreview:LeaguePreviewDto?=null, Continue:ContinueTargetDto? }`. Continue resolution: most-recent-Attempt subject → engine → first Available lesson (SequenceOrder ASC then Id ASC); if no Available, cross-subject fallback Math/Science/Arabic/English; falls back to Grade 1 Math when student has no attempts. Returns `Continue=null` if nothing Available anywhere.
- **`DTOs`** ✅ at `Application/Features/Dashboard/Dtos/` — `DashboardDto`, `ContinueTargetDto (SubjectId, SubjectName, LessonId, LessonName, UnitName, SkillId?, SkillName?, NodeState)`, `DailyMissionDto (Type, Target?, Progress?)`, `LeaguePreviewDto (TierName?, Rank?, TotalPlayers?, XpThisWeek?)` — Mission + League are nullable wrappers; Phase-4 owners (P4-06/P4-07) will populate.
- **`ILearningRepository`** ✅ extended with `GetMostRecentActivitySubjectIdAsync(int studentId, CT) → Task<int?>` (AsNoTracking; correlated subquery `Attempts → Lessons → Unit.SubjectId`). Reuses the 5 P2-04 repo methods for the engine inputs.
- **No new migration.** Read-only aggregation over existing P2-01/P2-08/P2-10/P2-11 schema.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_09_HomeDashboard_Tests.cs` — 11 cases: anonymous 401, fresh-student happy path, continue shape, XP/Streak/Mission/League null-state, most-recent-attempt drives Continue (Math + Science), cross-student IDOR isolation, idempotency, seeder smoke, envelope `"successed":` camelCase. All 11 PASS. **Full Wave-7+8+9 regression (excl. P2-05): 71/71 PASS** (~1m44s, Testcontainers Postgres pg16).

**Key decisions:**
- **Q3 → Option A (most-recent activity)** — query `Attempts` for student, order by `StartedAt DESC`, take first, join `Lesson → Unit → SubjectId`. Fallback Grade 1 Math when no attempts.
- **Q5 — XP/Streak = 0** with `TODO P4-02 / P4-03` comments. Phase-2 zero-state by design.
- **Q6 — Mission/League = null** (typed nullable wrappers, NOT "ComingSoon" shells). FE renders "Coming soon" conditionally.
- **Q9 — No caching.** ~5 DB queries per request worst case. Flagged for P6-06 perf pass (Redis with short TTL keyed on `(studentId, subjectId)`).
- **Q11 — Added one repo method** (`GetMostRecentActivitySubjectIdAsync`) for clean separation; alternative was inline LINQ in handler.

**Non-blocking follow-ups** (carry forward):
- Phase-2 zero-state for XP/Streak/Mission/League will become live in P4-02/P4-03/P4-06/P4-07.
- Dashboard performance — Redis cache per `(studentId, subjectId)` in P6-06.
- File overlap with P2-05 (PR #66): both add methods to `ILearningRepository.cs`. Additive merge — git auto-handles when both PRs land.

### P2-05 — Open and complete a lesson ✅ Merged via PR #66

Wave-9 story 1, now on main. Added `Lesson.Explanation` + `Lesson.Visual` columns (migration `AddLessonContent`), `GET /api/Learning/Lessons/{id}` `[Authorize]` route with `QuickCheck` field, `LearningSeeder.SeedDemoLessonContentAsync` for 4 Grade-1 root lessons (Math/Science/Arabic/English) with hand-authored content + 1 MCQ each, full e2e completion-flow integration test, `ex.Message` leak fix in `GetLessonQueryHandler` (Q12). See `docs/briefs/P2-05.md` + `docs/plans/P2-05.md` for the full record.

**P2-05 carry-forwards still open** (filed on main but not fixed in #66):
- Remove the old `GET /api/Learning/Lessons?id={id}` back-compat action in a future hardening wave.
- Fix `ex.Message` leak in `GetSubjectLessonsQueryHandler` (sibling to the one fixed) → P6-06.
- `QuizQuestion` has no `Order` column — "first by `Id ASC`" is the quick-check selection rule. Fragile when P3-05 generates multiple questions per lesson.
- `StartAttempt` lock-enforcement gap (R3) — `StartAttempt` does NOT currently enforce `LearningPathEngine`-derived `Locked` state → hardening wave.
- `LessonsController` does NOT have a `[Route(...)]` attribute today — current convention works; verify if routing convention changes.

### P2-03 — Navigate the skill tree ⏸️ Pending start

Wave-9 story 3. BE-1 + BE-2 may already be substantially done by P2-04 (engine surfaces `MissingPrerequisites`); BE-3 (boss-node flag) needs a `Lesson` schema change. P2-05's migration is now on main, so the schema base is clear — P2-03 can start whenever the lead is ready.

## Wave 8 — Phase 2 backend ✅ Fully merged

All Wave-8 work is merged to main (P2-04 via PR #63, P2-07 via PR #64). Original Wave-8 briefs preserved below for historical reference.

### P2-07 — Instant answer feedback ✅ Batches 1–5 complete, PR pending

**What's on branch `feat/P2-07-instant-answer-feedback` (ready for PR):**
- **`AnswerComparator`** ✅ pure static at `Learning.Domain/Services/AnswerComparator.cs` — plain `switch` on `QuestionType` (no design pattern). MCQ: `OrdinalIgnoreCase` (preserves P2-08 behavior); TrueFalse: `bool.TryParse` both sides + equality; FillInBlank: trim + `OrdinalIgnoreCase`; Matching: string-compare fallthrough with `TODO P2-07.b` (no matching questions seeded today). Null/whitespace inputs return `false` (no throw). 12 unit tests in `AnswerComparatorTests.cs`.
- **`SubmitAnswerCommandHandler`** ✅ uses `AnswerComparator.AreEqual(...)` for correctness; injects `IPublisher`; publishes `AnswerSubmittedIntegrationEvent` after `AddAsync` and before return (direct publish per ADR 0002 Option B, NOT outbox). Guarded on `question.SkillId.HasValue` — null skips with `_logger.LogWarn` + `TODO P3-09`. Try/catch around `Publish` is fail-soft (publisher exception is logged via `_logger.LogError(ex, msg)`; user request still succeeds).
- **`CompleteAttemptCommandHandler`** ✅ same pattern. Loads `Lesson.SkillId` via the new `GetLessonSkillIdAsync` repo method; publishes `LessonCompletedIntegrationEvent` (7 fields: `EventId, OccurredOnUtc, StudentId, LessonId, SkillId, AccuracyPercentage:int (rounded from double), CorrectAnswerCount`). Same null-skip + fail-soft pattern. `AbandonAttemptCommandHandler` is **NOT** touched — abandonment is not a completion event.
- **`ILearningRepository` extended** ✅ `GetLessonSkillIdAsync(int lessonId, CT) → Task<int?>` (AsNoTracking, single projection).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_07_InstantAnswerFeedback_Tests.cs` — 13 cases via in-test `INotificationHandler<T>` capture (factory layered with `WithWebHostBuilder` — `LearnexiaWebAppFactory` not modified). Covers MCQ/TrueFalse/FillInBlank correctness, event-captured-on-success-with-SkillId, NO event on null-SkillId, NO event on rejection paths (duplicate/IDOR/state guards), `LessonCompletedIntegrationEvent` happy + null-SkillId, idempotent Complete doesn't re-fire, handler isolation (throwing subscriber doesn't fail the API), envelope still `"successed":` camelCase, Abandon doesn't publish. Full Wave-7+Wave-8 regression suite: 60/60 PASS.
- **Security audit** ✅ `docs/briefs/P2-07-security-audit.md` — PASS, 0 Critical/High. Event payloads carry IDs only (no `CorrectAnswer`/`AnswerPayload`/PII). `ex.Message` not leaked. Log lines contain IDs only. Ghost-event-on-rollback documented as accepted Phase-2 trade-off per ADR 0002.

**Key decisions:** Per-type correctness via plain `switch` (no Strategy). Direct `IPublisher.Publish` inside the UoW transaction (Option B), matching the Identity precedent. Skip event when `SkillId IS NULL` (don't extend the cross-module event contract with a sentinel). Fail-soft try/catch around publish (publisher failure must NOT fail the user request). `CorrectAnswerCount` on `LessonCompletedIntegrationEvent` is the 7th field — initially missed by Batch 3 spec, corrected in implementation. Adjusted FillInBlank integration test to use JSON-encoded strings (`CorrectAnswer` is `jsonb`; bare words are invalid JSON) — whitespace-trim still covered by unit tests.

**Non-blocking follow-ups** (carry forward): switch the 4 new log lines to structured-logging placeholder syntax (`"... {AttemptId}"` instead of `$"...AttemptId={attempt.Id}"`) for observability — security-audit F-01 Low. P2-08 inherited: still no `MaximumLength` validator on `AnswerPayload` (recommended for Phase 3 scale-up).

### P2-04 — Unlock rules / Learning Path Engine ✅ Merged via PR #63

Wave-8 story 1 — `LearningPathEngine` (pure static memoized DFS) + 5 AsNoTracking repo methods + JWT-aware wiring into P2-02 handlers + `[Authorize]` tightening on `Subjects/{id}/{Lessons,SkillTree}`. See git log + `docs/briefs/P2-04.md` + `docs/plans/P2-04.md` for full details. **Breaking change**: those two endpoints now return 401 to unauthenticated callers.

### P2-04 — Unlock rules / Learning Path Engine ✅ Batches 1–4 complete, PR pending

**What's on branch `feat/P2-04-unlock-rules-learning-path-engine` (ready for PR):**
- **Engine** ✅ `Learning.Domain/Services/LearningPathEngine.cs` — pure static, three-color memoized DFS over Prerequisite edges. Caller pre-fetches inputs (no DI, no DB). Inputs: `IReadOnlyList<Lesson>`, `IReadOnlyList<KnowledgeNode>`, `IReadOnlyList<KnowledgeEdge>`, `IReadOnlyDictionary<int, SkillMastery> mastery`, `IReadOnlySet<int> completedLessonIds`, `IReadOnlyDictionary<int, Skill> skillsById` (separate from `SkillMastery` so the 3-param mastery record stays tiny). Returns `IReadOnlyDictionary<int, LessonUnlockStateDto>` keyed by `Lesson.Id`. 12 unit tests cover acyclic / cycle / self-loop / null-SkillId / no-prereqs / partial-mastery / exact-threshold / cross-grade / completed-lesson.
- **DTOs at `Domain/Services/`** (next to engine — not under `Application/Features/.../Dtos/`): `SkillMastery (SkillId, AccuracyPercentage:double, TotalAnswers)`, `LessonUnlockStateDto (LessonId, NodeState, IReadOnlyList<MissingPrerequisiteDto>)`, `MissingPrerequisiteDto (PrereqSkillId, PrereqSkillName, PrereqNodeId, RequiredAccuracy:int, CurrentAccuracy:decimal)`.
- **Repository extension** ✅ `ILearningRepository` + `LearningRepository` got 5 new AsNoTracking methods: `GetSubjectKnowledgeNodesAsync`, `GetSubjectKnowledgeEdgesAsync` (returns edges whose both endpoints are in the subject), `GetSkillMasteryForStudentInSubjectAsync` (returns mastery rows for EVERY skill in the subject — zero-row skills get `TotalAnswers=0` so the engine has the threshold), `GetCompletedLessonIdsForStudentInSubjectAsync`, `GetSubjectLessonsAsync`.
- **Wired into 2 existing P2-02 handlers** ✅ `GetSubjectSkillTreeQueryHandler` + `GetSubjectLessonsQueryHandler` now branch on `_currentUser.UserId.HasValue`: authenticated → run engine + project real `NodeState` + `MissingPrerequisites`; anonymous → fall back to existing placeholder (now never reached after Batch 4). Skill-level `NodeState` aggregated from its lessons (Completed > Available > Locked); Concept-level aggregated from its skills.
- **DTOs extended** ✅ `LessonInUnitDto` got `State : NodeState` (new) + `MissingPrerequisites : IReadOnlyList<MissingPrerequisiteDto>` (defaults to empty). `IsLocked` kept for back-compat, marked `[Obsolete("Replaced by LearningPathEngine in P2-04. Will be removed in P2-09 or P6-06.")]`. `SkillNodeDto.MissingPrerequisites` added as nullable (null when anonymous).
- **Auth tightening** ✅ `[Authorize]` added to `GET /api/learning/Subjects/{id}/SkillTree` AND `GET /api/learning/Subjects/{id}/Lessons`. `GET /api/learning/Subjects/ForGrade` stays anonymous. **BREAKING CHANGE:** any client currently calling the two gated endpoints without a JWT will start getting 401. FE wiring already uses auth.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_04_LearningPath_Tests.cs` — 12 cases (anonymous 401 gate × 2; fresh-student root-Available/downstream-Locked × 2; root-mastery unlocks next-skill; `MissingPrerequisites` shape; completed-lesson state; cross-student isolation; anonymous ForGrade still 200; unknown-subject 404; null-SkillId lesson Available; envelope camelCase). P2-02 tests updated to pass Student JWT on the 7 now-gated cases. All 24 green (~66s, Testcontainers Postgres).
- **2 new localized message keys** in `SharedResources*.resx` + `SharedResourcesKey.cs`: `LearningPathSubjectNotFound`, `LearningPathUnauthorized`.

**Key decisions:** Mastery = `AccuracyPercentage >= MasteryThreshold` (int 0..100) AND `TotalAnswers >= 1`. Completion = ≥1 `Attempt.Status=Completed` for that `(student, lesson)`. Lessons with `SkillId IS NULL` → `Available`. Skills with no prereq edges → `Available` (root nodes). `MissingPrerequisites` = immediate prereqs only (no transitive closure). `Strength` ignored in v1 (kept on schema). Edge of next concern: `Lesson.IsLocked` boolean is deprecated but still in the DB and DTO — removal scheduled for P2-09 or P6-06. P2-07 (sibling Wave-8 story) also touches `ILearningRepository.cs` — ship P2-04 first, rebase P2-07 on top.

## Wave 7 — Phase 2 backend ✅ Fully merged

All 3 stories merged to main (P2-11 via PR #60, P2-08 via PR #61, P2-02 via PR #62). See git log for full details. Original Wave 7 brief and decisions preserved below for historical reference.

### P2-11 — Skill dependency graph ✅ Batches 1–4 complete, PR pending

### P2-11 — Skill dependency graph ✅ Batches 1–4 complete, PR pending

**What's on main (PR #56):**
- `KnowledgeNode` entity — wraps `Skill` via nullable `SkillId?` FK (filtered unique index `UX_KnowledgeNodes_SkillId WHERE SkillId IS NOT NULL`). Fields: Name, NodeType (Skill/Concept/Review enum), SubjectId FK, GradeId FK, Difficulty (int 1–5).
- `KnowledgeEdge` entity — self-referential directed edge. Fields: SourceNodeId, TargetNodeId, RelationshipType (Prerequisite/Related enum), Strength (decimal 0–1, default 1.0). Both FKs `DeleteBehavior.Restrict`; SkillId FK `SetNull`.
- Migration `AddSkillGraphTables` (learning schema).

**What's on branch `feat/P2-11-skill-dependency-graph` (ready for PR):**
- **BE-3** ✅ `SkillGraphValidator.AssertAcyclic` (static, three-color DFS over Prerequisite edges only) at `Learning.Domain/Services/SkillGraphValidator.cs` + 6 unit tests (acyclic / cycle / self-loop / related-excluded / empty / mixed) — all green.
- **BE-5** ✅ `GetPrerequisitesQuery` + `GetUnlockedByQuery` CQRS handlers under `Learning.Application/Features/KnowledgeGraph/` + `KnowledgeNodeDto` + `KnowledgeGraphProfile` (placed in `Application/Mapping/` to match the existing convention, not under `Features/`); `KnowledgeGraphController` exposing `GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId}` + `/UnlockedBy/{nodeId}` (both `[Authorize]`). Repository extended on `ILearningRepository` with `GetPrerequisiteNodesAsync`, `GetUnlockedByNodeAsync`, `KnowledgeNodeExistsAsync`. Localized `KnowledgeNodeNotFound` key added in en-US + ar-EG resources.
- **BE-4** ✅ `LearningSeeder.SeedSkillGraphAsync` — maps every seeded `Skill` → `KnowledgeNode` (idempotent on `SkillId`, Difficulty=3 default); authors 7 Prerequisite edges across Math G1→G6 (skipped chains where a P2-10 skill name doesn't exist, e.g. "Place Value", "Division" — documented inline). Calls `SkillGraphValidator.AssertAcyclic(existing.Concat(@new))` before save; on cycle detection logs error + skips save (does NOT crash startup). Uses `GetService<ILoggerManager>()` (null-tolerant) so existing seeder unit tests keep working with a minimal service provider.
- **BE-6 DESCOPED** — no wiring to P2-04/P3-08/P3-10; the query API IS the integration seam; P2-04 consumes it when built (Wave 8).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_11_KnowledgeGraph_Tests.cs` — 6 tests (Prerequisites happy path, UnlockedBy happy path, unknown nodeId ≠ 500, unauthenticated → 401, seed smoke check, `"successed":` envelope literal) all green against Testcontainers PostgreSQL.

**Key decisions:** KnowledgeNode wraps (not replaces) Skill; within-subject edges only in demo seed; BE-6 seam only. **Skill Name strings must not be renamed** (P2-10 seeder + P2-11 use them as lookup keys). Math prereq chain skips Division (no Division skill seeded in P2-10) — jumps G3 Multiplication → G5 Fractions; revisit when P2-10 fills out Division skills. BL-01..05 deferral now recorded in `user-stories/README.md` (AC-7).

### P2-08 — Record granular answers ✅ Batches 1–4 complete, security PASS, PR pending

**What's on main (PR #58):**
- Migration `AddAttemptQueryIndexes` — composite `(StudentId, Status)` on `learning.Attempts`; `(AttemptId, QuestionId)` on `learning.StudentAnswers`. Schema from P2-06 already had all needed columns (zero gaps).
- `AttemptStatus` has `Abandoned=3`.

**What's on branch `feat/P2-08-record-granular-answers` (ready for PR):**
- **BE-1** ✅ `SubmitAnswerCommand` → `POST /api/Learning/Quizzes/{attemptId}/Answers` `[Authorize(Roles="Student")]`. Cross-lesson injection guard (`question.LessonId == attempt.LessonId`), re-answer guard (duplicate `(AttemptId, QuestionId)` → 424), case-insensitive correctness check, returns `{isCorrect, correctAnswer:null-when-correct, hintAvailable:false}`. TODO comment for P2-07 `AnswerSubmittedIntegrationEvent`.
- **BE-2/3** ✅ `CompleteAttemptCommand` + `AbandonAttemptCommand` → `POST …/Complete` and `POST …/Abandon` `[Authorize(Roles="Student")]`. Both idempotent on terminal state (re-call returns current snapshot); cross-terminal rejected (Complete on Abandoned → 424 and vice versa). `RecomputeAggregates` private helper duplicated in both handlers (plan-authorized; not a shared service). Returns `AttemptSummaryDto`. TODO comment for P2-07 `LessonCompletedIntegrationEvent`.
- **BE-4** ✅ `GetStudentAttemptsQuery` → `GET /api/Learning/Students/{studentId}/Attempts` `[Authorize]` (new `StudentsController`) + `GetSkillStatsQuery` → `GET /api/Learning/Skills/{skillId}/Stats?studentId=` `[Authorize]` (appended to existing `SkillsController`). Both enforce per-student IDOR guard (`studentId == _currentUser.UserId`). `AttemptListItemDto` and `SkillStatsDto` both omit `CorrectAnswer` entirely. Skill-stats zero-data case returns zeroed DTO (not 404/500); questions with null `SkillId` silently excluded (correct behavior).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_08_RecordGranularAnswers_Tests.cs` — 17 test cases (all 6 SubmitAnswer + 3 Complete + 3 Abandon + 2 GetStudentAttempts + 3 GetSkillStats per plan Batch 5) — all green (~30s, Testcontainers Postgres + Student-role JWT via parent→child onboarding flow).
- **Security audit** ✅ `docs/briefs/P2-08-security-audit.md` — 0 Critical/High; all 7 focus areas PASS (JWT-derived StudentId, ownership, IDOR, no `CorrectAnswer` leak, no `ex.Message` leak, `TimeSpentSeconds ≤ 3600`, cross-lesson guard). 2 Low + 4 Info findings documented, none blocking.
- **Bug fix surfaced + applied:** `RecomputeAggregates` was computing negative `DurationSeconds` because Npgsql returns `timestamp with time zone` columns with `Kind == Local`. Fixed by normalizing `attempt.StartedAt.ToUniversalTime()` before subtracting `DateTime.UtcNow` (+ `Math.Max(0, …)` belt-and-suspenders). Comment in both handlers explains the Kind=Local rationale.

**Key decisions:** P2-08 owns `SubmitAnswerCommand`; P2-07 (Wave 8) extends it with feedback. DurationSeconds = server-side `UtcNow - StartedAt.ToUniversalTime()`; per-answer TimeSpentSeconds advisory (validated ≥0, ≤3600). Reject duplicate QuestionId in same attempt. Validators: Submit/Complete/Abandon all enforce `AttemptId > 0`; SubmitAnswer also enforces `AnswerPayload` not-empty + `TimeSpentSeconds` 0..3600 range. 14 new localized message keys (en-US + ar-EG).

### P2-02 — Browse subjects & lessons ✅ Batch 1 merged (PR #57), api-tester PR pending

**What's on main (PR #57):**
- `NodeState` enum at `Domain/Enums/NodeState.cs` — `Locked=0`, `Available=1`, `Completed=2` (placeholder from `Lesson.IsLocked`; P2-03/P2-04 replace the logic)
- `GET /api/learning/Subjects/ForGrade?grade={1-6}` → `GetSubjectsForGradeQuery`
- `GET /api/learning/Subjects/{id}/Lessons` → `GetSubjectLessonsQuery` (nested Units→Lessons, SequenceOrder)
- `GET /api/learning/Subjects/{id}/SkillTree` → `GetSubjectSkillTreeQuery` (Concepts+Skills with placeholder NodeState)
- No migration — P2-01 schema + P2-10 seed already in place

**What's on branch `feat/P2-02-browse-subjects-lessons` (ready for PR):**
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_02_BrowseSubjectsAndLessons_Tests.cs` — 12 cases: ForGrade happy paths (G1 + G6) returning 4 subjects each, out-of-range grade=99 → 400 (handler guards 1..6), missing param → 400, item shape (id/name/gradeNumber); Lessons happy path (5 units × 3 lessons for Math G1), order-by-SequenceOrder, unknown subject → 404; SkillTree happy path (5 concepts × 3 skills for Math G1), `state` field present + value ∈ {0,1,2}, unknown subject → 404; envelope `"successed":` camelCase check. All green (~55s, Testcontainers Postgres).

**Confirmed contract:** `grade` query param validated 1..6 in handler (out-of-range → 400, not empty list). `NodeState` serializes as int (no `JsonStringEnumConverter` registered). `SkillNodeDto.State` JSON key is `"state":` (not `"nodeState":`). Endpoints are anonymous-callable today — no `[Authorize]` yet.

**Deferred follow-ups:** Grade JWT claim seam (P6-06); `Concept/Skill.SequenceOrder` columns (P2-11 follow-up; currently ordered by Id); `[Authorize]` on new actions (hardening wave).

### Cloud-env worktree note
Worktrees at `/home/user/Learnexia.worktrees/{P2-11,P2-08,P2-02}` (branches off `claude/phase2-backend-wave7-U48WT`). **Direct `git commit` from the main session's Bash tool fails inside worktrees** (signing server 400 "missing source"). Workaround: dispatch a background `committer` subagent — background agents sign successfully. Main checkout commits without issue.

## TL;DR
- The repo now runs natively in **WSL2** (`~/projects/learnexia`). Clean install + `dotnet build` + Expo web/native bundling are validated.
- The Expo **student-app web** now boots, translates (ar/en), and talks to the backend end-to-end (register/login → 200 + JWT).
- **P1-11** (parent web pages, pixel-perfect from `design-system/screenshots/`) is planned + two screens built: **Login** and **Register**.
- All **new backend** the design implies is deferred to **P1-12 "Batch 2"** (Identity-scoped, parallel-safe with the Phase 2 BE lead) — see "For the backend lead".


## P2-06 — Take a quiz (folded into Learning module)
> Committed on `feat/P2-06-assessment-quiz`; pending Wave-6 PR. Build green, integration + unit tests pass, reviewer PASS.

**Lead decision:** quiz/assessment functionality lives in the **Learning** module (schema `learning`), NOT a separate Assessment module. A separate Assessment module was scaffolded then deleted per lead instruction. **Ask before creating new modules** — all quiz work goes in Learning going forward.

**New domain entities (Learning.Domain):**
- `QuizQuestion` — polymorphic question record with `QuestionType` (MCQ/TrueFalse/Matching/FillInBlank), `Content` (JSON blob), `CorrectAnswer`, `Order`, and `GeneratedBy` (Human/AI). Linked to a `Lesson`.
- `Attempt` — student quiz attempt record; status `AttemptStatus` (NotStarted/InProgress/Completed/Abandoned); links to a `Lesson` and `StudentId`.
- `StudentAnswer` — per-question answer record inside an attempt.

**Migration:** `AddQuizTables` (learning schema) — creates `quiz_questions`, `attempts`, `student_answers` tables in the `learning` schema.

**New endpoint:**
- `POST /api/Learning/Quizzes/{lessonId}/Attempt` — `[Authorize(Roles="Student")]` — creates a new `InProgress` attempt (or resumes an existing one) and returns the lesson's questions **without** the `CorrectAnswer` field. Enforces: lesson-existence check (404), Student-role-only (403), no-answer-leak.

**4 question types modeled** (MCQ / TrueFalse / Matching / FillInBlank) with a per-type content validator (`QuizQuestionContentValidator` helper) and unit tests in `Modules.Learning.UnitTests/QuizQuestionTypeValidationTests.cs`.

**`AttemptService.StartNewAsync` explicit SaveChangesAsync:** calls `LearningDbContext.SaveChangesAsync` directly (not waiting for UoW) to obtain the DB-generated `AttemptId` before returning questions — mirrors the `LinkParentStudentService` precedent. UoW's later save is a no-op.

**Secret hygiene (no new secrets introduced):**
- Remote dev DB connection string lives ONLY in gitignored `appsettings.Development.local.json`.
- `Program.cs` now loads optional `appsettings.{Environment}.local.json` at startup (before other config, optional:true so the app runs without it).
- Tracked `appsettings.Development.json` keeps the localhost default only. **Never commit the .local.json file.**
- Remote DB (75.119.158.102:5346/learnexia): all 5 module schemas migrated; NOT seeded yet. To seed, run `dotnet run --project backend/src/Host/Learnexia.Host -- --environment Development --MinIOConfiguration:Enabled false` (or add a `Bash(dotnet run:*)` allow-rule for the seeding agent).

**P6-06 pre-existing deferrals (NOT introduced by P2-06):**
- F2: JWT `CHANGE_ME` secret in `appsettings.json` should be env-driven + startup-guarded.
- F6: `RequireHttpsMetadata=false` should be Development-only.
- F9: `DbContext` audit stamp uses `DateTime.Now` (should be `UtcNow`).
- F11: MinIO default credentials should be env-driven.
- MSB3277: EF 10.0.0/10.0.8 version conflict to resolve in `Directory.Packages.props`.

## P2-10 — Seed demo subjects & skill trees
> Committed on `feat/P2-10-seed-demo-data`; pending Wave-6 PR. Dev-only idempotent seeder; unit tests green.

- **Seeder location:** `backend/src/Modules/Learning/Learnexia.Modules.Learning.Infrastructure/Persistence/Seed/LearningSeeder.cs`
- **Activation:** runs at startup ONLY in Development, via `IHostEnvironment.IsDevelopment()` inside `LearningModule.InitializeAsync`. The environment check lives in `LearningModule` (not in the seeder) so the seeder is environment-neutral and unit tests can call it directly.
- **Coverage:** all **6 grades × 4 subjects** (Math, Science, Arabic, English; **NO Social Studies**). Math is the deepest tree: 5 units / 15 lessons / 5 concepts / 15 skills per grade; the other three subjects use 2 units / 4 lessons / 2 concepts / 4 skills per grade.
- **Idempotent:** natural-key checks on Subject.Name + Grade; re-running the seeder in an already-seeded DB adds zero rows.
- **`SystemUserId = 0`** convention for all seed-authored rows (matches the broader platform convention for system-generated data).
- **P2-11 extension seam:** Skill `Name` strings are stable lookup keys — P2-11 (skill dependency graph) will use them to attach prerequisite edges. **Do NOT rename skill name strings** after the seeder ships.
- **Demo-ready:** P2-02 (browse subjects/lessons) and P2-03 (navigate skill tree) can now be demoed against a populated DB. Run the backend in `Development` mode to auto-seed.

## P2-12 — Account settings (3-module refactor)
> Committed on `feat/P2-12-account-settings-apis`; pending Wave-6 PR. Build green, 39/39 integration tests pass, security-auditor 2 High findings remediated.

**Architecture:** the original Identity-only plan was restructured (lead decision) into **3 modules + a Shared.Contracts seam**:

- **NEW `Parent` module** (schema `parent`) — owns ALL parent↔child family code: `AddChild`, `LinkChild`, `UpdateChild`, `ListMyChildren`, plus new `UnlinkChild`. Identity's `Family/` handlers, `FamilyScope` authz handler, `ParentController`, and `ParentStudents` entity are **fully removed** from Identity. Route base changed from `/api/Users/Parent/*` to **`/api/Parent/*`**.
- **`Shared.Contracts` seams** — `IChildAccountService` (implemented in `Identity.Infrastructure`) is the ONLY cross-module bridge for child-account create/read/update (mirrors `IUserLookup`). `IParentChildQuery` (implemented in `Parent`) is the reverse seam so Identity `GetMe` can still return `HasChildren`.
- **`Notifications` module** — gained `NotificationPreference` entity (schema `notifications`) + `GET /api/Notifications/Preferences` and `PUT /api/Notifications/Preferences`. Categories: `WeeklyReport`, `StreakAtRisk`, `ProductAnnouncement`, `Achievement` x `Email`/`Push`. First `GET` returns defaults (not persisted until first `PUT`).
- **`Identity` module** — kept account-security endpoints: `POST /api/Users/Account/ChangePassword` (now invalidates OTHER sessions + revokes refresh token; rate-limited 5/15m), `GET /api/Users/Account/Sessions`, `POST /api/Users/Account/Sessions/SignOutOthers`, `GET /api/Users/Account/Plan` (STUB returning `{planName:"Free",status:"Active"}` — replace when payments module lands, **TODO P2-12-PAYMENTS**).

**Migrations applied locally (3 total):**
- `InitialParent` — creates `parent` schema + `ParentStudent` table in the Parent module.
- `AddNotificationPreferences` — creates `notifications.NotificationPreferences` table.
- `DropParentStudent` — drops `identity.ParentStudents` table from Identity.

**Production follow-up:** `identity."ParentStudents"` rows are **NOT** copied to `parent."ParentStudent"` (dev rows are disposable; lead-accepted). A data-copy migration **must** be written before applying `DropParentStudent` to any environment with real link data.

**Known gaps (non-blocking):**
- `Notifications.Application` does not register `ValidationBehavior` per-module (masked by global registration — functionally OK).
- MSB3277 EF version-conflict warning on `Parent.Api` / `Learning.Api` (track in `Directory.Packages.props` alignment).
- `RequireHttpsMetadata` + MinIO default creds deferred to **P6-06**.


## ⚠️ Load-bearing config — do NOT "clean up"
These exist because the WSL clean install drifts dependencies past the Expo SDK 52 pins. Removing them reintroduces a hard crash.
- **`.npmrc` → `auto-install-peers=false`** — stops `*` / `^18||^19` peers grabbing **react-dom 19 / expo 56**, which breaks React 18 ("Should have a queue" hook crash). Requires `@babel/preset-env` to be an explicit dep of student-app (it is).
- **root `package.json` → `pnpm.overrides`**: `inline-style-prefixer ^6.0.4` (keeps web SSR resolving past rnw 0.21's v7), `react`/`react-dom` `18.3.1`.
- **i18n is initialized at module load** in `apps/student-app/app/_layout.tsx` (NOT in a useEffect) — react-i18next changes its hook count unready→ready, so initializing mid-mount crashes. Keep `initI18n()` at module scope.
- **i18n resources are one flat namespace** (`packages/shared/src/i18n/config.ts`) — components use dotted keys like `t('auth.login.title')`. `i18next ^24` / `react-i18next ^15.4` aligned across student-app + `@learnexia/shared` (a major mismatch caused a duplicate react-i18next instance).
- **Backend error envelopes are camelCase** — `ErrorHandlerMiddleWare` serializes with `JsonNamingPolicy.CamelCase` so error responses match the `BaseResponse` success shape (the typed client parses them).
- **Postgres image is pinned to pgvector** (`pgvector/pgvector:pg15` in `docker/docker-compose.yaml`, pinned to pg15 to match staging/prod). NOTE: the hard `CREATE EXTENSION vector` startup requirement came from the **Catalog** module's `DEMO_PgvectorProof` migration, which has been **removed along with the Catalog module** — no remaining module needs the `vector` extension, so a plain `postgres` image would no longer fail at startup. The image stays pinned to pgvector to match staging/prod (and to keep the door open for future pgvector use); keep using it unless you deliberately decide otherwise.
- **Remote shared DB:** `learnexia` @ `75.119.158.102:5344` runs `pgvector/pgvector:pg15`; fully migrated + seeded (24 subjects / 162 lessons / 162 skills / 13 roles). Its connection string lives ONLY in gitignored `appsettings.Development.local.json` (loaded via the optional `appsettings.{Environment}.local.json` line in `Program.cs`) — never commit it.
- **Regenerating `@learnexia/api-client` needs the .NET 9 runtime** — `nswag` 14.x ships a **Net90** binary and self-checks the runtime, so it won't run on net10 alone. Install side-by-side: `dotnet-install.sh --runtime dotnet --channel 9.0` **and** `--runtime aspnetcore --channel 9.0`. Then: start the backend, `SWAGGER_URL=http://localhost:5080/swagger/v2/swagger.json pnpm --filter @learnexia/api-client refresh:swagger` → `pnpm --filter @learnexia/api-client gen:api` (the default SWAGGER_URL is https://localhost:7080; override to the HTTP :5080 dev URL).

## How to run the stack (dev)
1. **Postgres (pgvector)** — `docker compose -f docker/docker-compose.yaml up -d postgres` (or an existing pgvector container on `localhost:5432`, DB `Learnexia`, `postgres/admin`). Redis is **not** required for dev (connection string empty).
2. **Backend** — from `backend/src/Host/Learnexia.Host`:
   `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 AllowedOrigins=http://localhost:8081,http://127.0.0.1:8081 dotnet run --no-launch-profile`
   (HTTP avoids the untrusted dev cert in WSL; `AllowedOrigins` must list the web origin because CORS uses `AllowCredentials`.)
3. **Frontend** — from `apps/student-app`: `npx expo start --port 8081`. The API base URL is set via `apps/student-app/.env.local` (`EXPO_PUBLIC_API_BASE_URL=http://localhost:5080`, gitignored). Web at http://localhost:8081; LAN/device via `exp://<lan-ip>:8081`.
4. Default locale is **Arabic** (product is Arabic-first). Default theme is **dark**.

## What's built / merged to main
- Dev-env + bootstrap fixes (deps, i18n, auth error handling) — earlier PRs.
- **P1-11 planning docs** (story, tasks, pixel audit, designer pixel-perfect rule) + **P2-12** (settings tabs) + **P1-12** (Batch-2 BE) + the **gap analysis**.
- **Login** screen pixel-perfect (split layout, persona toggle, social buttons UI-only, theme/lang switches) + shared `SplitFormScaffold`.
- **Register** screen pixel-perfect + `packages/ui` `CheckboxField` (merged).
- **My Children** screen pixel-perfect (parent `Sidebar` + child-selector, family-summary strip, child cards, dashed add-card) + new `packages/ui` primitives **`Avatar`, `KPIStatCard`, `MasteryBar`, `GradientBox`** (PR #29, merged). Per-child + family stats are **Phase-5 stubs** (`parentDashboardStubs.ts`, TODO(P5)) since `LinkedChildResponse` only exposes id/fullName/email.
- **Splash** screen pixel-perfect (`app/index.tsx`): removed the mascot; purple gradient bg + star field, wordmark + subtitle, `DotPulse`, decorative progress bar, "Loading… ⚡", "POWERED BY AI / Gamified Learning" footer. Boot logic (i18n init + `useAuthRoute` guard, hook order) preserved (PR #31). Added `splashBg` gradient tokens.
- **Dashboard / Overview** screen pixel-perfect **minus the chart** (`(parent)/overview.tsx` + cards): header, 4 KPI tiles w/ deltas, subject-mastery (4 product subjects), "Areas to focus on"; the **daily-activity chart is a placeholder** (pending merge). Stats are Phase-5 stubs. **Charts were carved out to Phase 5 → [P5-05-FE](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md)** (BarChart primitive + daily/20-day/time-of-day + wire real analytics). NB: KPI tiles built inline (not `KPIStatCard` — it lacks a delta slot) to stay pixel-perfect.
- **Settings** screen pixel-perfect (`(parent)/settings.tsx`): six-tab rail via new `packages/ui` **`Tabs`** primitive; **Profile** + **Language & region** functional; the other four tabs (Notifications/Linked/Security/Plan) are "coming soon" → **P2-12**. **Profile is now wired to the real backend** (P1-12-FE-1, pending merge): `useMyProfile`/`useUpdateProfile` hooks load + **save** fullName/phone/country via `GET`/`PUT /api/Users/Account/Profile` (api-client regenerated from #40), success/error states, avatar shows `avatarUrl`. **Avatar upload/remove stays a stub** until BE-4; email is display-only (not in the profile command).
- **Reports** = **blank placeholder** only (`(parent)/reports.tsx`) wired to the sidebar — full Reports + charts deferred (`P1-11-FE-9` / `P5-05-FE`) per product call (pending merge).
- **Landing** scaffolded **`apps/marketing-site`** as a Next.js 15 app (mirrors `admin-dashboard`) + the Landing page pixel-perfect to `01-landing.png` (nav, hero headline/CTAs/trust row, phone mockup). CTAs link to the student app via `NEXT_PUBLIC_APP_URL` (default `http://localhost:8081` → `/register`, `/login`). English-only (RTL scoped out for marketing); design-system tokens/fonts wired via `app/globals.css`. build/type-check/lint pass (pending merge). **This completes the P1-11 screen set.**
- **P1-11 pixel-perfect QA pass** ([P1-11-qa-pass.md](../../design-system/ui_kits/parent-dashboard/P1-11-qa-pass.md)) + fixes (pending merge): closed the Blocker (shared sidebar **"THIS WEEK +XP"** widget) + 4 Majors (Login brand **social SVG icons**, `FamilySummaryStrip` **AvatarStack** of children vs mascot, **per-subject mastery colors**, Register eyebrow `$primary`) + most Minors. New: `AvatarStack`, `SocialIcons`, `primarySoftStrong` token, `MasteryBar.accent`, `Avatar xl`, `Select.hideLabel`. Deferred minors: country **flag prefixes** (GAP-06 — no `flag` in COUNTRIES), a couple of `ScreenHeader` tablet deltas. Social icons are token-styled marks (no SVG transformer wired yet — swap for licensed vectors later).
- **Design system — Arabic/RTL + atomic-component preview pass** (`design-system/`): added an **Arabic (RTL) capture set** (`screenshots/mobile-ar/` 24, `screenshots/web-ar/` 7 — same screens as English) and **`index-ar.html`** RTL versions of both UI kits (`ui_kits/parent-dashboard`, `ui_kits/student-mobile`). New **`design-system/preview/`** with ~81 **atomic component cards** (per-component HTML, both stacks): 29 `mobile-*`, 25 `web-*` (English) + 27 `ar-*` (Arabic RTL) on a shared `_base-ar.css`. Updated the kit JSX (`Components/PagesApp/PagesPublic/Screens/ScreensAuth/ScreensExtra/index.html`) + `screenshots/README.md` (now documents EN+AR captures + the preview cards). **For `frontend`/`designer` agents:** these are the per-component RTL/Arabic source of truth alongside the screen captures — cite the matching `preview/*.html` / `screenshots/*-ar/*` when building RTL or component-level work.
- **P1-11 pixel-alignment v2 — full preview-card + EN/AR pass** (branch `feat/design-system-pixel-align`, pending PR): re-aligned all 7 built surfaces (Login, Register, My Children, Overview, Settings, Splash, Landing) to the new `design-system/preview/*.html` atomic cards + `screenshots/{web,web-ar,mobile,mobile-ar}/`, in **both EN (LTR) and Arabic (RTL)**. Per-screen delta specs live in `design-system/ui_kits/parent-dashboard/align-*.md` + `student-mobile/align-splash.md`. **Updated `.claude/agents/designer.md`** to make the preview cards co-canonical with screenshots and fold in the `README.md`/`SKILL.md` brand law (10 rules, voice/tone, emoji semantics, Eastern-Arabic-numeral RTL conventions + Latin exceptions, copy cheat sheet, UI-kit click-through refs, motion specs, fraction-detail extraction checklist). **New tokens** (mirrored in `colors_and_type.css` + `packages/design-system/src/tokens/*`): `primaryLight`, `fg4`, `purpleLight`, `fg2Alpha`, `xpSoft`, `streakSoft`, `borderInput`, `borderSubtle`, `radius.nav`(12), `radius.cardInner`(14), `fontSize.wordmark`(36), `gradBrandPanel`, `splashProgress`, and a **corrected warm `splashBg`** (was cold blue-indigo). **Shared primitives** updated (MasteryBar accent/LTR/height, Tabs active-pill + no border-stripe + radius 12, Select radius 8 + `size`, Button radius 16 + press 0.95 + primary glow, TextField height 48 + `forceLtr`) + **new `PasswordStrengthMeter`** (the P1-11-FE-14 primitive). Shared `Sidebar` re-styled. Reviewers PASS; typecheck + lint + marketing build green. **Deferred follow-ups:** Login "Show/Hide" password as TEXT in label row (needs shared `TextField` change — still emoji reveal); Settings email needs BE `email` on `AccountProfileResponse`; DG-01 AR Settings sidebar parent-context prop; `parent.linkChild.explanation` AR still transliterates "Learnexia"; KPIStatCard value weight 800 vs spec 900; Landing AR/RTL appendix (marketing EN-only); splash 🌟 = placeholder mascot. **Process note:** an implementer subagent ran `git stash` in the shared worktree mid-parallel-batch and reverted everyone's uncommitted work into a stash; recovered by restoring `Sidebar.tsx` + `resources.ts`. **Never let implementer/reviewer agents run `git stash`/`reset`/`checkout` — shared worktree.**
- **Phase 7 — Admin Console backlog** (PR #21, merged): 12 admin stories `P7-01..P7-12` (curriculum mgmt, user/account mgmt, content moderation, analytics/AI-safety oversight) — the feature set behind the P1-10 shell — each with BE + admin-dashboard (Next.js) task files in `…/Phase-7-Admin-Console/`. Added a real **`FR-ADM-1..12`** group to [SRS §4.9](../SRS.md) (note: `FR-ADM`, not `FR-AD` = Adaptivity) and expanded §3 + the goal matrix; all P7 stories trace to it. **Backlog/spec only — nothing implemented (all P7 rows in PROGRESS.md are 🔲).** Handoff/decisions for whoever builds it: [docs/briefs/P7-admin-console.md](../briefs/P7-admin-console.md) (PR #24).

### P7 — gap analysis + modular spec refit (2026-06-03 — PRs #85 + #86 open)
> A second, **code-grounded** Phase-7 pass: compared the 12 P7 stories/tasks against actual `main`. Status still **0/12 built**, but several conflicts + a live security finding surfaced. Brief: [docs/briefs/phase-7-admin-gap-analysis.md](../briefs/phase-7-admin-gap-analysis.md) (PR #85). Spec refit across 16 P7 files: PR #86.
- **🚨 Live auth hole on `main` (predates P7 — surfaced by the analysis):** the existing **Learning controllers** (`Subjects` / `Units` / `Lessons` / `Concepts` / `Skills` / `Grades`) expose `Create` / `Update` / `Delete` **anonymously**. Hotfix = method-level **`[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`** on the writes; reads stay at `[Authorize]` (P2-02/04/05) — **not class-level AdminOnly** (would lock students out of curriculum reads). Recommended **before** any P7-01..P7-05 work.
- **Modular boundaries clarified — there is no "Admin" backend module.** Admin Console = the Next.js `apps/admin-dashboard` FE + role-gated admin endpoints owned by each module. Distribution: **P7-01..P7-05 curriculum mgmt → `Learning`** (existing controllers, harden+extend); **P7-06..P7-08 user/account → `Identity`** (extend existing `UserManagementController`); **P7-09 moderation + P7-12 audit → NEW `Moderation` module** (scaffolded by **new task `P7-12-BE-0`** in PR #86; serialization point per PARALLELISM.md); **P7-10/P7-11 analytics → read-models over `Shared.Contracts` events** (no cross-module schema joins).
- **`Shared.Contracts` seams added by PR #86:** **`AdminActionPerformedEvent`** (every admin handler in any module publishes; `Moderation` audit log consumes); **`ChildGradeChanged`** (P5-06 not yet built — P7-08 introduces the contract via `P7-08-BE-2` so downstream can subscribe when P5-06 lands).
- **Spec fixes in PR #86:** `mirror Catalog` → `mirror Learning` (Catalog deleted 2026-06-03); five made-up policy names (`Learning.ManageCurriculum`, `Assessment.ManageQuizzes`, `Moderation.Review`, `Audit.Read`, "Analytics admin policy") collapsed to **`AuthorizationPolicies.AdminOnly`** (granular `{Module}.{Action}` via `Claims.GenerateModules()` is a follow-up); **P7-04/P7-05 re-pointed from a non-existent `assessment` module → `Learning`** (P2-06 `QuizQuestion` lives in Learning); file uploads now cite **`Shared.Kernel.Storage.IStorageService`** (the shared seam from P1-12 BE-4), not module-local MinIO.
- **`Modules/Learning/Application/Features/Questions/`** is the empty shell that P7-04 fills: contains only an **orphaned** static helper `QuizQuestionTypeValidation.Validate(...)` (per-type shape: MCQ / TrueFalse / Matching / FillInBlank) with **zero source callers** — scaffolded ahead of `AddQuestionCommandValidator` / `EditQuestionCommandValidator` (which P7-04 will add here). The `QuizQuestion` entity itself exists in `Learning.Domain` (P2-06); the student-side `Attempts` feature reads it; the **admin authoring path is the P7-04 gap**.
- **Recommended order (in the brief):** **0.** spec-fixes + auth-hole hotfix → **1.** curriculum wave P7-01→02→03 (graph-editor gated) →04 (in `learning`)→05 → **2.** P7-12 audit log + `Moderation` scaffold first so P7-06..09 emit `AdminActionPerformedEvent` → **3.** user/account P7-06/07/08 (security-auditor gated) → **4.** deferred until upstream lands: P7-09 (P3-02 + BL-01), P7-10 (P5-03), P7-11 (P3-01/P3-02/P6-02).

## Key decisions (so you don't relitigate them)
- **Pixel-perfect to `design-system/screenshots/`** is the bar. The `designer` agent has a rule: when a capture exists it's the highest-priority target (cite it, match it, express in `--lx-*` tokens). See `.claude/agents/designer.md`.
- **Subjects = Math / Science / Arabic / English** everywhere (the dashboard/reports captures show "Reading"/"Art" — that's mock data; use the 4 product subjects).
- **Scope trims:** Child Home → **P2-09** (not P1-11); secondary Settings tabs (Notifications/Linked/Security/Plan) → **P2-12** (back + front).
- **All new backend → P1-12 "Batch 2" + P1-13 hardening: ✅ BUILT & MERGED** (profile/`Me`, avatar upload [MinIO], Google OAuth, password reset, update-child, register country+consent; lockout, sign-in anti-enumeration, admin seed). See the "Backend — … DONE" section below. FE can now light up the UI-first surfaces (regenerate the api-client).
- Per CLAUDE.md: **ask before adding any design pattern**; mirror existing shapes (existing modules backend, existing component/hook shapes frontend).

## For the backend lead (P1-12, Batch 2) — ✅ DONE (retained for traceability)
> All items below are **built & merged** — see the "Backend — … DONE" section for PRs/details. Kept here as the original gap list.
All Identity-module-scoped, parallel-safe with your Phase 2 BE work. Stories + tasks:
- `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md` + `tasks/Backend/Phase-1-Foundation/P1-12-BE.md`.
- Gaps found while building the UI: **profile read/update + enriched `/Me`** (no `Phone` column today), **avatar upload** (no storage/`AvatarUrl`), **OAuth** (Google/Apple/Microsoft), **password reset**, **update-child** (no UpdateChild command exists), **register country + terms-consent** (`RegisterParentCommand` takes only `{email,password,fullName}`).
- Source analysis: `docs/briefs/phase-1-design-gap-analysis.md`.

## What's next (web FE)
- **P1-11 screen set is complete**: Login, Register, My Children, Splash, Dashboard (chart-less), Settings, Landing all built; Reports is a deliberate blank placeholder. Remaining P1-11 follow-ups are the **UI-first wiring once P1-12 BE lands** (profile save, avatar, social/forgot, edit-child) and the **CAPTCHA/lockout FE** (`P1-11-FE-15/16`, after P1-13 BE).
- **Charts moved to Phase 5** ([P5-05-FE](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md)); `P1-11-FE-2` retired into it. **Full Reports** (KPIs/mastery/charts) = `P1-11-FE-9` + P5-05-FE when picked up.
- Remaining shared primitives (`P1-11-FE-14`): **Switch**, **PasswordStrengthMeter** (Avatar, KPIStatCard, Sidebar, MasteryBar, GradientBox, CheckboxField, **Tabs** now built).
- Per-child/family analytics stats are stubbed (`(parent)/_components/parentDashboardStubs.ts`) until **Phase 5** (P5-01/P5-05) lands real data.

## Backend — P1-12 Batch 2 + P1-13 hardening: ✅ DONE (merged to main)
> The Phase-1 backend leftover is complete and on `main` (all Identity-module-scoped, parallel-safe). Every story ran **security-auditor + api-tester + reviewer**; the integration suite is green (**334 tests**, incl. real PostgreSQL + MinIO containers). Source: [phase-1-design-gap-analysis.md](../briefs/phase-1-design-gap-analysis.md) + [phase-1-backend-gap-analysis.md](../briefs/phase-1-backend-gap-analysis.md).
- **P1-13a** (PR #33) — Notifications email delivery: `IEmailSender` + SMTP adapter + dev log-sink; `UserRegistered` → best-effort welcome email.
- **IUserLookup** (PR #35) — Identity seam in `Shared.Contracts` so Notifications can resolve a recipient email.
- **P1-13** (PR #39) — hardening: account **lockout** engaged; sign-in **anti-enumeration** + no `ex.Message` leak (⚠️ sign-in errors are now **uniform** — FE must NOT branch on not-found vs wrong-password); config/env-driven **Admin seed** (legacy `superadmin`/`basicuser` dev-only). **BE-4 CAPTCHA NOT built** — see "Still open".
- **P1-12** (PRs #40, #43, #44, #45, #46): BE-3 migration (reused `PhoneNumber`/`Nationality`, added `AvatarUrl` + `AcceptedTermsAtUtc`); BE-1/2 profile read/update + enriched `/Me`; BE-9 register `country`+terms-consent; BE-8 edit-child (family-scope, 403 on non-own); BE-4 **avatar via self-hosted MinIO** (`HttpClient` + hand-rolled **AWS SigV4**, **NO MinIO SDK** — "AWS SigV4" is just the S3 signing algo, no AWS dependency; storage lives in **`Shared.Kernel`** as `IStorageService`, stream-based, registered at the Host → reuse it for ANY future upload e.g. BL-01); BE-5 **Google** social sign-in (`Google.Apis.Auth`, ID-token flow); BE-6 password reset (anti-enumeration + session invalidation, email via the `Shared.Contracts` event seam).

### ⚠️ Load-bearing backend config — set via ENV in staging/prod (do NOT commit real values)
- **MinIO:** `MinIOConfiguration__AccessKey` / `__SecretKey` (self-hosted `minio` container in `docker/docker-compose.yaml`; dev defaults `minioadmin`; private `avatars` bucket; presigned URLs).
- **Google:** `GoogleAuth__ClientId` (sign-in audience; inert/fail-closed if unset).
- **Admin seed:** `AdminSeed__Email` / `__Password` (no-op if unset; no committed credential).
- **Password reset:** `ClientAppBaseUrl` (reset-link origin; dev default `http://localhost:3000`).
- **Email:** `Email__Provider=Smtp` + `Email__Host/__UserName/__Password` for real delivery (dev = `None`/log sink).

### Still open (backend)
- **P1-13 BE-4 — CAPTCHA on register**: ✅ BUILT (Cloudflare Turnstile `TurnstileCaptchaVerifier` + `ICaptchaVerifier`; config-gated, fail-closed). Ships `Captcha:Enabled=false` by default; **PR #65 now fail-fasts in Production/Staging** unless enabled + secret set. FE consumer `P1-11-FE-16`.
- **Hardening follow-ups** (non-blocking; in the per-PR security briefs): **per-IP throttle on auth endpoints ✅ tightened in PR #65** (env-gated; prod/staging: sign-in 50/5m, register 10/15m, forgot 5/15m, reset 10/15m); forgot-password **timing-oracle** decouple (email send still synchronous in-request) — ⏳ P6-06 AC-5; **localize** reset + welcome emails (English-only) — ⏳ P6-06; MinIO presign TTL ✅ already 60m.

### Phase-1 security follow-up audit (2026-05-29) — branch `audit/phase-1` → PR #65
Verified every Phase-1 security-audit follow-up against `main` (all original audits were PASS / PASS-WITH-FOLLOWUPS — **zero Critical/High**). ~10 of ~18 follow-ups already applied (timing-oracle dummy-hash, CRLF guard, email PII masking, SMTP fail-fast, MinIO no-`ex.Message`/TTL→60m/detected-Content-Type, no raw-Identity-error concat on register, per-endpoint rate limits, GuardJwtSecret).
- **Fixed in PR #65:** **B1** CAPTCHA prod-guard (`GuardCaptcha` in Identity `DependencyInjection.cs`); **G1/B2** env-gated auth rate limits (`Host/Extensions/ServiceExtensions.cs` `ConfigureRateLimitingOptions(IConfiguration)`; Dev/Testing keep the prior 100/s rules verbatim so the integration suite is unaffected). Build green; Testcontainers suite NOT run this session (no Docker) — reviewer/api-tester to run before merge.
- **Routed to P6-06** (`user-stories/Phase-6-Stabilization/P6-06-...md`, new AC-7): **G2** — JWT bearer does NOT validate any per-request server state, so an already-issued access token survives sign-out/password-reset until expiry (only the refresh-token cache + sessions are dropped). Chosen design: **SessionId per-request validation** via `JwtBearerEvents.OnTokenValidated` against `ISessionManagementService` (preserves P2-12 "ChangePassword keeps current session"); explicitly NOT security-stamp validation. Load-bearing auth → full pipeline.
- **Still outstanding (Low/Info, mostly P6-06):** `RequireHttpsMetadata=false` not env-gated; DB password default in `appsettings.json` no fail-fast; no `[RequestSizeLimit]` on avatar upload; child `Email` echoed in Added/Updated/LinkedChildResponse DTOs; Google auto-link w/o confirmation + auto-stamped consent; CORS `?? "*"` + `AllowCredentials()` fallback unguarded.

### FE now unblocked (regenerate the `api-client`)
Profile save (`/Account/Profile`), avatar upload/remove (`/Account/Avatar`), Google button (`/Authentication/Google-SignIn`), forgot/reset (`/Authentication/Forgot-Password` + `Reset-Password`), edit-child (`/Parent/Update-Child`), register `country`+`acceptedTerms`. Sign-in errors are uniform now (`P1-11-FE-15` / `P1-10-FE-6`).

### Backend → Frontend coverage gap analysis (new, 2026-05-24)
> The reverse of the FE-design gap analysis: starting from every Phase-1 **backend capability**, does a FE story/task consume it? Brief: [docs/briefs/phase-1-frontend-coverage-gap-analysis.md](../briefs/phase-1-frontend-coverage-gap-analysis.md) (grounded in the real Identity/Notifications controllers).
- **Headline:** most backend is already FE-covered — the earlier design gap analysis routed every design-implied backend gap into **P1-12 (Batch 2)**, and **P1-12-FE already plans that wiring** (FE-1..5). Those are deferred, not gaps.
- **Real FE gaps found → tasks added (no new story needed):**
  - **F2 (sign-in contract change, highest value):** P1-13-BE-1/2 change Sign-In (locked-account message + uniform "invalid credentials" anti-enumeration) but no FE consumed it → added **P1-11-FE-15** (student login) + **P1-10-FE-6** (admin login). **Both must land after P1-13-BE-1/2 merge.**
  - **F1 (register country+consent wiring):** P1-12-BE-9 persists `country`+terms-consent but no FE task wired the collected fields → added **P1-12-FE-7** (Batch 2, after BE-9 + api-client regen).
- **CAPTCHA on register (P1-13-BE-4) — confirmed in P1 scope (2026-05-24):** added **P1-11-FE-16** — Register integrates the bot-challenge and sends the token when the server advertises the requirement; **lands after P1-13-BE-4 merges**. (P1-13-BE-4 stays in P1, no longer deferred to P6.)
- **Resolved non-gaps:** student-app sign-out is already covered by **P1-02-FE-3** (`useSignOut`); email-verification UX is N/A (BYPASSED by lead decision); the AdminOnly UserManagement/Authorzation surface is correctly deferred to the Phase 7 Admin Console.

## Workflow notes
- Branch per change; **PRs to main**, the user merges. **Don't stack PRs on an unmerged base and then merge the base first** — the stacked changes get stranded (this happened to Register; it was re-PR'd straight to main). Now that Login is in main, branch new screens **off main**.
- Git identity isn't set in this WSL checkout — commits use a per-invocation `-c user.name/email` override (`Ahmed Elbaradey <elbaradeyahmed1985@gmail.com>`); set it permanently if you prefer.
- Pixel-perfect verification needs a browser; headless Chromium wouldn't download in this env, so screenshot review has been done by the human. The error overlay's **Log 1 of N** is the root error (later logs cascade).
- **Activate the auto-load hook on first pull:** a committed `SessionStart` hook (`.claude/settings.json`) auto-loads this file into context — but if your session was already open when you pulled it, run **`/hooks`** once (or restart Claude Code / start a new session) to load it. New sessions after that pick it up automatically.
