# P10-Billing — Backend (API/Integration) Test Cases

**Scope:** Billing module money paths AFTER the Option-C (service-only) refactor on `refactor/optionc-billing`.
**Goal:** Pin the *observable* money behavior so `api-tester` can prove the refactored services still honor the **LOCKED energy economy** end-to-end. This is a **behavior-preserving** refactor — every case below asserts behavior the refactor must not change.
**Owner of this file:** `qc-test-designer` (design). **Implementer:** `api-tester` (writes integration tests against the running API). **Results go to:** `docs/qc/P10-Billing/execution-report.md`.

---

## Locked economy invariants (the spec these cases enforce)

| # | Invariant | Source |
|---|-----------|--------|
| INV-1 | Energy is a value meter. **Cache HIT and Cache MISS BOTH require energy AND charge energy** (per-intent cost). | `GetHintCommandHandler` DELIVERY POINT #1 (HIT) + #2 (MISS); HANDOFF "charge-on-delivery" |
| INV-2 | **Pre-authorize before the AI request; debit only AFTER successful delivery.** Delivery = cache hit OR generated answer. | `GetHintCommandHandler` pre-auth block (GetBalanceAsync → balance<cost → no debit) |
| INV-3 | **No delivery → no debit. Failures are free.** (refuse/scope-guard/no-reveal/safety-block/error/insufficient/rate-limit). | `GetHintCommandHandler` "NO debit" comments at every non-delivery branch |
| INV-4 | Per-intent costs: **Hint=1, WhyWrong=2, Explain/Simplify=3, SimilarExample(Practice)=5**, sourced from GlobalSettings `ai_cost.*`. | `CreditCostResolver`, `GlobalSettingsSeeder` |
| INV-5 | **Monthly = HARD limit (blocks).** When `TotalBalance < cost` → insufficient, no debit. | `CreditSpendService.ExecuteDebitCoreAsync` (account null/insufficient → `InsufficientBalance`, rollback) |
| INV-6 | **Daily = SOFT warning (does NOT block)** unless `Billing:HardStopEnabled=true`. | `CreditCostResolver.IsHardStopEnabled`; `EnergyBalance.DailyCapReached` |
| INV-7 | **Idempotency / no double-charge** — same `IdempotencyKey` returns prior result, no second debit (pre-check + 23505 catch). | `CreditSpendService` idempotency pre-check + `IsUniqueViolation` |
| INV-8 | **Optimistic concurrency** — concurrent debits resolve via `xmin` retry (≤3) with never-negative + no lost-update. | `CreditSpendService` `MaxRetries=3`, `DbUpdateConcurrencyException` retry |
| INV-9 | **Granted-first** — `GrantedBalance` drawn before `PurchasedBalance`; Mixed pool split recorded. | `CreditAccount.Debit` |
| INV-10 | **Webhook idempotency** — same `ProviderEventId` is an idempotent success (pre-check + 23505 dedupe). | `HandleProviderWebhookCommandHandler` STEP 3 + `WebhookEventService.IsAlreadyProcessedAsync` / `ConcurrentDuplicate` |
| INV-11 | **Webhook security** — HMAC verified FIRST before any state access; bad/missing sig → 401; forged amount ignored (server-side amount authoritative, no tier inflation). | `HandleProviderWebhookCommandHandler` STEP 1; `WebhookEventService` amount reconciliation |
| INV-12 | **Subscription activation** — `payment.succeeded` → Premium/Active + `SubscriptionActivatedIntegrationEvent` (post-commit). | `WebhookEventService.HandlePaymentSucceededAsync` |
| INV-13 | **Server-side amount** — checkout amount resolved from GlobalSettings, never client-supplied; two-save Id flush. | `SubscriptionCheckoutService.StartAsync`, `EnergyPackService.StartPackCheckoutAsync` |
| INV-14 | **Packs & refunds** — pack purchase credits `PurchasedBalance`; refund claws back (clamped, never-negative); no double-refund. | `EnergyPackService.CreditPurchasedPackAsync`, `RefundService.ProcessPackRefundAsync`, `CreditAccount.Refund` |
| INV-15 | **GlobalSettings** — AdminOnly; cross-key validation (daily ≤ monthly); type/range; non-admin rejected. | `GlobalSettingsController` `[Authorize(AdminOnly)]`, `UpdateGlobalSettingValidator` |
| INV-16 | **IDOR / authz** — a parent/child cannot view/spend/grant/refund another child's credits; deny-by-default. | IDOR scoping in `GetCreditAccountQueryHandler`, `EnergyStatusQueryHandler`, `EnergyPackService`, `BillingHistoryController` |
| INV-17 | **Envelope** — `BaseResponse<T>` with `Successed` flag (note spelling); correct status codes; 422 validation. | `BaseResponseHandler`, `NewResult` |
| INV-18 | **Dunning/grace** — `charge.failed` increments attempts; < max → PastDue + NextRetryAt; ≥ max → Dunning + GraceEndsAt. | `RefundService.ProcessChargeFailedAsync` |

---

## Real endpoints under test (enumerated from controllers)

| Verb + Route | Controller | Auth |
|---|---|---|
| `POST /api/Billing/Subscription/Checkout` | PaymentsController | `[Authorize]` (parent JWT; needs PendingPayment sub) |
| `GET /api/Billing/Subscription/Current` | SubscriptionController | `[Authorize]` |
| `GET /api/Billing/Plans/Comparison` | SubscriptionController | `[Authorize]` |
| `POST /api/Billing/Subscription/Upgrade` | SubscriptionController | `[Authorize]` (body: `{ "billingPeriod": "Monthly"\|"Annual" }`) |
| `POST /api/Billing/Subscription/Downgrade` | SubscriptionController | `[Authorize]` |
| `POST /api/Billing/Subscription/Cancel` | SubscriptionController | `[Authorize]` |
| `POST /api/Billing/Packs/Checkout` | PackController | `[Authorize]` (body: raw int `childId`) |
| `GET /api/Billing/Credits/{childId}` | CreditsController | `[Authorize]` |
| `GET /api/Billing/Credits/{childId}/Reconcile` | CreditsController | `[Authorize("Billing.View")]` |
| `POST /api/Billing/Credits/Grant` | CreditsController | `[Authorize("Billing.Create")]` (body: GrantCreditCommand) |
| `POST /api/Billing/Credits/Spend` | CreditsController | `[Authorize("Billing.Create")]` (body: SpendCreditCommand) |
| `GET /api/Billing/Energy/{childId}/Status` | EnergyController | `[Authorize]` |
| `GET /api/Billing/History` | BillingHistoryController | `[Authorize]` (query: pageNumber,pageSize) |
| `GET /api/Billing/History/Receipt/{paymentId}` | BillingHistoryController | `[Authorize]` |
| `POST /api/Admin/Billing/Refunds/{paymentId}` | AdminRefundController | `[Authorize(AdminOnly)]` (body: `{ "reason": "..." }`) |
| `GET /api/Admin/GlobalSettings` | GlobalSettingsController | `[Authorize(AdminOnly)]` |
| `PUT /api/Admin/GlobalSettings/{key}` | GlobalSettingsController | `[Authorize(AdminOnly)]` (body: `{ "newValue": "..." }`) |
| `POST /api/Billing/Webhooks/Provider` | WebhookController | **No JWT** — HMAC header `X-Hmac-Signature: sha256=<hex>` |

> **Cross-module debit seam:** `ICreditSpendService.TryDebitAsync` is **intra-process** (Ai module → Billing). It is NOT an HTTP route; the equivalent HTTP surface for direct ledger probing is `POST /api/Billing/Credits/Spend` (admin-tooling, `Billing.Create`). The locked-economy HIT/MISS/no-debit behavior is observable end-to-end via the AI Helper SSE endpoints (`POST /api/Ai/...`) combined with `GET /api/Billing/Credits/{childId}` balance assertions.

## Test environment notes (for `api-tester`)

- **Provider:** `Billing:PaymentProvider:Provider = "Fake"` (default). Set `Billing:PaymentProvider:FakeSigningSecret` to a known value in the test host so the HMAC gate is exercisable. With an empty secret, EVERY webhook fails signature verification (safe default) — set it for any webhook happy-path case.
- **Signed payloads:** use `FakePaymentProvider.BuildSignedWebhookPayload(eventId, eventType, paymentRef, amount, signingSecret)` to produce a valid `(rawBody, "sha256=<hex>")` pair. Event types: `payment.succeeded`, `payment.failed`, `charge.failed`, `refund.succeeded`.
- **Daily-cap hard-stop:** default `Billing:HardStopEnabled` is unset/false → daily cap is SOFT. For INV-6 hard-stop cases, run a variant with `Billing:HardStopEnabled=true`.
- **Seeded GlobalSettings defaults:** free_monthly=100, premium_monthly=5000, free_daily_cap=10, premium_daily_cap=250, pack_price=50.00, pack_size=1000, hint=1, explain_mistake=2, deep_explanation=3, practice_generation=5, sub_monthly=199.00, sub_annual=1990.00.
- **Seed accounts via the API:** `POST /api/Billing/Credits/Grant` (admin `Billing.Create`) is the supported seam to set up a child's `GrantedBalance` deterministically. Pack/`PurchasedBalance` is seeded by driving a signed `payment.succeeded` pack webhook against a Pack payment.
- **`Successed` is the spelling** of the success flag in `BaseResponse<T>` — assert on that key, not `Success`/`Succeeded`.

---

# Group A — Energy spend / debit-on-delivery (INV-1, INV-2, INV-3, INV-4, INV-9)

> These are validated through the AI Helper flow (cache HIT vs MISS) plus the `/Credits/Spend` ledger seam, with balance assertions via `GET /api/Billing/Credits/{childId}`.

### BE-TC-01 — Cache MISS delivery debits the per-intent cost
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 with `GrantedBalance=100`. Empty AI cache for the question. Valid attempt owned by C1. Student JWT for C1.
- **Steps:** 1) Call the Hint AI Helper endpoint (intent=Hint) for a question with NO cached answer (forces cache MISS → safety layer generates). 2) After delivery, GET `/api/Billing/Credits/{C1}`.
- **Expected:** Delivery succeeds (generated answer). Balance = 99 (debited Hint cost = 1). `GET` returns `Successed=true`, `GrantedBalance=99`. A `Spend` `CreditTransaction` exists (reason `AiHint`, amount 1).
- **Traces to:** INV-1, INV-2, INV-4

### BE-TC-02 — Cache HIT delivery ALSO debits the per-intent cost
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 with `GrantedBalance=100`. A pre-approved cache entry exists for the exact cache key (run BE-TC-01 first, or seed an Approved entry). Same question/attempt owned by C1.
- **Steps:** 1) Call the Hint endpoint so the cache HIT path is taken (log/marker "cache HIT"). 2) GET balance.
- **Expected:** Delivery succeeds from cache. Balance decremented by 1 (HIT charges identically to MISS). New `Spend` ledger row recorded.
- **Traces to:** INV-1 (the headline locked-economy rule — HIT must charge)

### BE-TC-03 — WhyWrong intent costs 2
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`. WhyWrong delivery (cache MISS or HIT).
- **Steps:** Call Helper with intent=WhyWrong; deliver; GET balance.
- **Expected:** Balance debited by 2; ledger reason `AiWhyWrong`, amount 2.
- **Traces to:** INV-4

### BE-TC-04 — Explain/Simplify intent costs 3
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`.
- **Steps:** Deliver an Explain (and separately a Simplify) response; GET balance.
- **Expected:** Each Explain and Simplify delivery debits 3; ledger reason `AiDeepExplanation`, amount 3.
- **Traces to:** INV-4

### BE-TC-05 — SimilarExample (Practice) intent costs 5
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`.
- **Steps:** Deliver a SimilarExample response; GET balance.
- **Expected:** Balance debited by 5; ledger reason `AiPracticeGeneration`, amount 5.
- **Traces to:** INV-4

### BE-TC-06 — NO delivery → NO debit: insufficient balance (pre-auth block)
- **Type:** negative / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 with `GrantedBalance=0` (or below cost: e.g. balance 2 for an Explain cost 3).
- **Steps:** 1) Call the Helper (Explain, cost 3). 2) GET balance.
- **Expected:** Request returns an "insufficient energy" typed error (no generation). Balance unchanged (still 2). NO new `Spend` ledger row. Pre-authorize blocked BEFORE the AI request.
- **Traces to:** INV-2, INV-3, INV-5

### BE-TC-07 — NO delivery → NO debit: scope-guard refuse (empty context)
- **Type:** negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`. Question with no grounding context (forces refuse-and-redirect).
- **Steps:** Call Helper; receive a Redirect (no answer delivered); GET balance.
- **Expected:** Balance UNCHANGED at 100. No `Spend` ledger row (refuse is not a delivery).
- **Traces to:** INV-3

### BE-TC-08 — NO delivery → NO debit: rate-limit rejection is free
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`. Exceed the per-student rate limit.
- **Steps:** Fire enough Helper requests to trip the limiter; on the rejected request GET balance.
- **Expected:** Rejected request returns rate-limit error; balance not debited for the rejected call.
- **Traces to:** INV-3

### BE-TC-09 — NO delivery → NO debit: generation/safety failure is free
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`. Safety layer configured to BLOCK (or generation error injected) for the test prompt.
- **Steps:** Call Helper → safety block / error; GET balance.
- **Expected:** Typed error returned, balance UNCHANGED, no ledger row. (Failures are free.)
- **Traces to:** INV-3

### BE-TC-10 — NO delivery → NO debit: Hint no-reveal violation is free
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=100`. Hint generation that would contain the correct answer (no-reveal violation path).
- **Steps:** Call Helper (Hint); receive no-reveal-violation error; GET balance.
- **Expected:** Error returned, balance UNCHANGED, no ledger row, response NOT cached.
- **Traces to:** INV-3

### BE-TC-11 — Granted-first then Purchased (Mixed pool split)
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=2`, `PurchasedBalance=10`. Deliver an Explain (cost 3).
- **Steps:** Deliver; GET balance; inspect ledger row pool split.
- **Expected:** `GrantedBalance=0`, `PurchasedBalance=9`. Ledger row pool = `Mixed`, `FromGranted=2`, `FromPurchased=1`, amount 3.
- **Traces to:** INV-9

### BE-TC-12 — Direct ledger debit via `/Credits/Spend` (admin seam) succeeds and charges
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin/ops JWT with `Billing.Create`. Child C1 `GrantedBalance=10`.
- **Steps:** `POST /api/Billing/Credits/Spend` `{ childId:C1, amount:4, reasonCode:"AiHint", idempotencyKey:"k-spend-1" }`. GET balance.
- **Expected:** `200`, `Successed=true`, `DebitResultDto.Outcome=Charged`, `FromGranted=4`. Balance = 6.
- **Traces to:** INV-1, INV-9, INV-17

### BE-TC-13 — `/Credits/Spend` over-balance returns InsufficientBalance, no debit
- **Type:** negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=3`.
- **Steps:** `POST /Credits/Spend` `{ childId:C1, amount:5, reasonCode:"AiPracticeGeneration", idempotencyKey:"k-spend-over" }`. GET balance.
- **Expected:** `Outcome=InsufficientBalance`, `Charged=false`, balance UNCHANGED at 3, no `Spend` ledger row.
- **Traces to:** INV-5

---

# Group B — Caps (monthly HARD, daily SOFT) (INV-5, INV-6)

### BE-TC-14 — Monthly HARD limit blocks at zero remaining (exactly enough then one over)
- **Type:** boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=3`. Explain cost 3.
- **Steps:** 1) Deliver one Explain → succeeds, balance 0. 2) Deliver a second Explain (cost 3 > balance 0). GET balance.
- **Expected:** First succeeds (exactly at limit). Second returns insufficient-energy, no debit, balance stays 0. (Monthly is a hard wall.)
- **Traces to:** INV-5

### BE-TC-15 — Daily SOFT cap does NOT block (HardStopEnabled=false / default)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** `Billing:HardStopEnabled` unset/false. free_daily_cap=10. Child C1 `GrantedBalance=100`. Drive `DailyUsed` to ≥10 (e.g. 10 Hint deliveries).
- **Steps:** After daily cap reached, deliver one MORE Hint; GET `/Energy/{C1}/Status` and balance.
- **Expected:** Delivery still SUCCEEDS and DEBITS (balance keeps dropping below cap). Energy status reflects a WARNING `WarningState` / `DailyCapReached=true` but the request is NOT blocked.
- **Traces to:** INV-6

### BE-TC-16 — Daily cap HARD-STOP blocks when HardStopEnabled=true
- **Type:** boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Test host with `Billing:HardStopEnabled=true`. free_daily_cap=10. Child C1 `GrantedBalance=100`, `DailyUsed=10` (cap reached).
- **Steps:** Deliver one more Helper request; GET balance.
- **Expected:** Request returns `AiDailyCapReached` typed error; balance UNCHANGED (hard-stop blocks the delivery before debit).
- **Traces to:** INV-6, INV-3

### BE-TC-17 — Daily counter lazy-reset across child-local day boundary
- **Type:** boundary / state · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Child C1 with `DailyUsed=10`, `DailyUsedDateLocal=` yesterday (child TZ Africa/Cairo), `GrantedBalance=50`.
- **Steps:** Deliver a Hint "today"; GET `/Energy/{C1}/Status`.
- **Expected:** `DailyUsed` resets to 0 then increments by the delivered cost (e.g. ends at 1), NOT 11. Status shows the post-reset value.
- **Traces to:** INV-6 (daily reset is part of soft-cap correctness)

---

# Group C — Idempotency / no double-charge (INV-7, INV-10)

### BE-TC-18 — Repeated `/Credits/Spend` with same idempotency key → single debit
- **Type:** functional / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=10`.
- **Steps:** 1) `POST /Credits/Spend` `{childId:C1, amount:3, reasonCode:"AiDeepExplanation", idempotencyKey:"idem-A"}`. 2) Repeat the IDENTICAL request. GET balance.
- **Expected:** First → `Outcome=Charged`, balance 7. Second → `Successed=true`, `Outcome=DuplicateIdempotent`, balance STILL 7 (no second debit). Exactly ONE `Spend` ledger row with key `idem-A`.
- **Traces to:** INV-7

### BE-TC-19 — Concurrent duplicate spend (same key, parallel) → single debit
- **Type:** negative / concurrency · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=10`.
- **Steps:** Fire N parallel `/Credits/Spend` requests with the SAME `idempotencyKey:"idem-par"`, amount 3.
- **Expected:** Net effect = exactly ONE debit (balance 7). At most one `Charged`; the rest `DuplicateIdempotent` (23505 dedupe). Never balance 4 or lower.
- **Traces to:** INV-7

### BE-TC-20 — Webhook re-delivery (same ProviderEventId) is idempotent success
- **Type:** negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** A `Subscription` PendingPayment + Payment row with a known `ProviderPaymentRef`. Signing secret configured.
- **Steps:** 1) POST signed `payment.succeeded` webhook (eventId E1). 2) POST the IDENTICAL signed webhook again (same eventId E1).
- **Expected:** Both return `200` `Successed=true`. First Data.Outcome = `PaymentSucceeded`; second Data.Outcome = `Duplicate` (no second activation, no duplicate `WebhookEvent` row, no duplicate integration event). Subscription Active exactly once.
- **Traces to:** INV-10

### BE-TC-21 — Concurrent duplicate webhook (same eventId, parallel) → one processed, one ConcurrentDuplicate
- **Type:** negative / concurrency · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Pack Payment + ref; signing secret set; child C1 with an account.
- **Steps:** Fire two parallel signed `payment.succeeded` pack webhooks with the SAME eventId.
- **Expected:** Both `200`. One Outcome `PackCredited`, the other `ConcurrentDuplicate` (or `Duplicate`). `PurchasedBalance` credited exactly ONCE (one pack_size, not two).
- **Traces to:** INV-10, INV-14

### BE-TC-22 — Pack credit idempotency on webhook replay (no double-credit)
- **Type:** negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Pack Payment for child C1; signing secret set; `PurchasedBalance=0`, pack_size=1000.
- **Steps:** 1) Signed `payment.succeeded` (eventId E2) → credits pack. 2) Replay identical webhook (E2). GET balance.
- **Expected:** After step 1 `PurchasedBalance=1000`. After replay STILL 1000 (credit key `pack-credit:{paymentId}:{E2}` dedupes). Single Purchase ledger row.
- **Traces to:** INV-7, INV-14

---

# Group D — Optimistic concurrency (INV-8, INV-9)

### BE-TC-23 — Concurrent distinct debits resolve without lost-update
- **Type:** concurrency · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=10`.
- **Steps:** Fire 5 parallel `/Credits/Spend` requests, each amount 2, each with a DISTINCT idempotencyKey.
- **Expected:** All 5 succeed via xmin retry. Final balance = 0 (10 − 5×2), NEVER negative, NEVER >0 due to a lost update. Exactly 5 distinct `Spend` ledger rows.
- **Traces to:** INV-8

### BE-TC-24 — Concurrent debits exceeding balance never over-draw (never-negative)
- **Type:** concurrency / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Child C1 `GrantedBalance=5`.
- **Steps:** Fire 5 parallel debits of amount 2 (total demand 10 > balance 5), distinct keys.
- **Expected:** At most 2 debits charge (4 ≤ 5; the third would need 6 > remaining). Remaining balance ≥ 0. The rest return `InsufficientBalance`. Sum of charged amounts ≤ 5. TotalBalance never goes negative.
- **Traces to:** INV-8, INV-5

### BE-TC-25 — Retries exhausted surfaces a server error (not a silent double-debit)
- **Type:** negative / concurrency · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Child C1; induce sustained concurrency conflicts beyond `MaxRetries=3` if feasible (best-effort; mark blocked if not reproducible deterministically).
- **Steps:** Drive concurrent contention; observe the call that exhausts retries.
- **Expected:** A failure surfaces (no charge applied for the failed call). Balance integrity preserved (ledger sum == stored balance).
- **Traces to:** INV-8 · **Note:** may be **blocked / non-deterministic** — flag if not reliably reproducible.

---

# Group E — Subscription lifecycle (INV-12, INV-13, INV-18)

### BE-TC-26 — Upgrade transitions subscription to PendingPayment
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 (Free subscription) JWT.
- **Steps:** `POST /api/Billing/Subscription/Upgrade` `{ "billingPeriod": "Monthly" }`. Then `GET /Subscription/Current`.
- **Expected:** `200` `Successed=true`. Subscription status `PendingPayment`, PendingBillingPeriod=Monthly.
- **Traces to:** INV-12 (precondition)

### BE-TC-27 — Checkout resolves amount server-side (monthly) with two-save Id flush
- **Type:** functional / security · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 with PendingPayment (Monthly) subscription. sub_monthly=199.00.
- **Steps:** `POST /api/Billing/Subscription/Checkout` (NO amount in body).
- **Expected:** `200`, returns `RedirectUrl` + `ProviderPaymentRef` + non-zero `PaymentId`. Payment row Amount=199.00 (server-resolved), Status `Initiated`, `ProviderPaymentRef` persisted (second save). Client supplied no amount.
- **Traces to:** INV-13

### BE-TC-28 — Checkout resolves annual amount server-side
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 PendingPayment with Annual period. sub_annual=1990.00.
- **Steps:** `POST /Subscription/Checkout`.
- **Expected:** Payment Amount=1990.00.
- **Traces to:** INV-13

### BE-TC-29 — Checkout without a PendingPayment subscription fails cleanly
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Parent P2 with NO PendingPayment subscription.
- **Steps:** `POST /Subscription/Checkout`.
- **Expected:** `Successed=false` with a "subscription not found" mapped failure (not a 500). No Payment row created.
- **Traces to:** INV-13, INV-17

### BE-TC-30 — payment.succeeded webhook activates Premium + emits SubscriptionActivated
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 PendingPayment(Monthly) + Initiated Payment with known `ProviderPaymentRef`; signing secret set.
- **Steps:** POST signed `payment.succeeded` with matching `paymentRef`, amount 199.00. Then `GET /Subscription/Current`.
- **Expected:** `200`. Subscription PlanCode=Premium, Status=Active, CurrentCycleStart set, CurrentCycleEnd = +1 month, Pending fields cleared. Payment Status=Succeeded. `SubscriptionActivatedIntegrationEvent` published (post-commit; assert via downstream consumer effect or event log if observable).
- **Traces to:** INV-12

### BE-TC-31 — payment.failed webhook marks Payment Failed + emits PaymentFailed
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Initiated subscription Payment with known ref; signing secret set.
- **Steps:** POST signed `payment.failed`.
- **Expected:** `200`. Payment Status=Failed. `PaymentFailedIntegrationEvent` fired (post-commit). Subscription NOT activated.
- **Traces to:** INV-18

### BE-TC-32 — charge.failed (1st) → PastDue + NextRetryAt (dunning retry)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Active Premium subscription with a recurring Payment ref; `dunning.max_retries=3`. FailedAttemptCount=0; signing secret set.
- **Steps:** POST signed `charge.failed`.
- **Expected:** `200`, Outcome `DunningRetryScheduled`. Subscription Status=PastDue, FailedAttemptCount=1, NextRetryAt ≈ now+24h, GraceEndsAt=null.
- **Traces to:** INV-18

### BE-TC-33 — charge.failed reaching max retries → Dunning + GraceEndsAt
- **Type:** boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Subscription with FailedAttemptCount=2, CurrentCycleEnd set; max_retries=3.
- **Steps:** POST signed `charge.failed` (distinct eventId) → 3rd failure.
- **Expected:** `200`, Outcome `DunningDowngradeScheduled`. Status=Dunning, FailedAttemptCount=3, GraceEndsAt=CurrentCycleEnd, NextRetryAt=null.
- **Traces to:** INV-18

### BE-TC-34 — charge.failed idempotent on already-Failed payment
- **Type:** negative · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Payment already Status=Failed; signing secret set.
- **Steps:** POST `charge.failed` referencing that payment (new eventId).
- **Expected:** No second FailedAttemptCount increment for that already-Failed payment (Duplicate guard). Subscription attempt count not double-incremented.
- **Traces to:** INV-18, INV-7

### BE-TC-35 — Downgrade schedules Free at cycle boundary, retains Premium until CurrentCycleEnd
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 Active Premium.
- **Steps:** `POST /Subscription/Downgrade`. `GET /Subscription/Current`.
- **Expected:** `200`. PendingPlanCode reflects downgrade scheduled; access retained until CurrentCycleEnd (Premium benefits still active now).
- **Traces to:** INV-12 (lifecycle)

### BE-TC-36 — Cancel retains Premium until CurrentCycleEnd
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 Active Premium.
- **Steps:** `POST /Subscription/Cancel`. `GET /Subscription/Current`.
- **Expected:** `200` `Successed=true`. Access continues until CurrentCycleEnd per cancel policy.
- **Traces to:** INV-12 (lifecycle)

### BE-TC-37 — Plan comparison values sourced from GlobalSettings (never hard-coded)
- **Type:** functional · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Authenticated JWT. Admin updates sub_monthly to a distinctive value first (e.g. 222.00).
- **Steps:** `GET /Billing/Plans/Comparison`.
- **Expected:** Returned pricing/benefit values reflect the GlobalSettings values (the edited 222.00), proving they are read from settings.
- **Traces to:** INV-13

---

# Group F — Webhook security (INV-11)

### BE-TC-38 — Missing signature header → 401, no state access
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Valid `payment.succeeded` body for a real Payment ref. NO `X-Hmac-Signature` header. Signing secret set.
- **Steps:** POST `/Billing/Webhooks/Provider` with body, no signature.
- **Expected:** `401` `Successed=false`, generic message (no cause leaked). NO `WebhookEvent` row, NO Payment/Subscription mutation. (Signature verified FIRST.)
- **Traces to:** INV-11

### BE-TC-39 — Wrong/forged signature → 401, no state access
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Valid body; `X-Hmac-Signature: sha256=deadbeef...` (wrong HMAC). Signing secret set.
- **Steps:** POST webhook with the bad signature.
- **Expected:** `401`, generic message. No state changed. (Constant-time compare; no timing oracle leakage in message.)
- **Traces to:** INV-11

### BE-TC-40 — Tampered body invalidates a previously-valid signature → 401
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Build a signed payload, then mutate ONE byte of the body but keep the original signature header.
- **Steps:** POST tampered body + original signature.
- **Expected:** `401`. (HMAC computed over the modified raw bytes ≠ header.) No state changed.
- **Traces to:** INV-11

### BE-TC-41 — Forged amount in payload is IGNORED (server-side amount authoritative, no tier inflation)
- **Type:** security · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Subscription Payment with server Amount=199.00 (Monthly). Build a VALID signed `payment.succeeded` whose payload `amount` is 5000.00 (forged higher).
- **Steps:** POST the validly-signed webhook with the inflated amount.
- **Expected:** `200` processed, BUT activation uses the server-side Payment.Amount/period (Monthly, +1 month) — NOT the forged amount. No upgrade to annual/higher tier from payload amount. Amount mismatch is logged, server value used.
- **Traces to:** INV-11

### BE-TC-42 — Webhook signature is verified before idempotency / parsing (ordering)
- **Type:** security · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** A malformed body (unparseable JSON) with a BAD signature.
- **Steps:** POST malformed-body + bad signature.
- **Expected:** `401` (signature failure wins) — NOT a 400 parse error. Proves signature gate runs before parsing/state.
- **Traces to:** INV-11

### BE-TC-43 — Empty signing secret rejects all webhooks (safe default)
- **Type:** security / config · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Test host variant with `Billing:PaymentProvider:FakeSigningSecret` EMPTY/unset.
- **Steps:** POST a payload with ANY signature header.
- **Expected:** `401` always (no secret configured → reject). Mark **blocked** if the test host cannot run a no-secret variant.
- **Traces to:** INV-11

---

# Group G — Packs & refunds (INV-14)

### BE-TC-44 — Pack checkout resolves price server-side + enforces parent-owns-child (IDOR)
- **Type:** functional / auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 linked to child C1. pack_price=50.00. Parent P1 JWT.
- **Steps:** `POST /api/Billing/Packs/Checkout` body = `C1` (raw int).
- **Expected:** `200`, returns PaymentId + RedirectUrl + ProviderPaymentRef. Payment Kind=Pack, Amount=50.00 (server), TargetChildId=C1, ParentUserId from JWT (not body).
- **Traces to:** INV-13, INV-14, INV-16

### BE-TC-45 — Pack checkout for a child NOT owned by the parent is rejected (anti-enumeration)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 NOT linked to child C9 (belongs to P2).
- **Steps:** `POST /Packs/Checkout` body = `C9`.
- **Expected:** Generic failure (ChildNotOwnedByParent) — NOT "child not found" (no enumeration). No Payment row created for C9.
- **Traces to:** INV-16

### BE-TC-46 — Pack purchase webhook credits PurchasedBalance by pack_size
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Pack Payment (from BE-TC-44) with ref R1; child C1 `PurchasedBalance=0`; pack_size=1000; signing secret set.
- **Steps:** POST signed `payment.succeeded` (paymentRef=R1, eventId E3). GET `/Credits/{C1}`.
- **Expected:** `PurchasedBalance=1000`, Payment Status=Succeeded, one Purchase ledger row (reason `PackPurchase`).
- **Traces to:** INV-14

### BE-TC-47 — Admin refund initiate guards: 404 unknown payment
- **Type:** negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT.
- **Steps:** `POST /api/Admin/Billing/Refunds/{999999}` body `{ "reason":"test" }`.
- **Expected:** `404` `Successed=false`, PaymentNotFound message.
- **Traces to:** INV-14, INV-17

### BE-TC-48 — Admin refund initiate guards: 422 payment not Succeeded
- **Type:** negative / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT. Payment in `Initiated` (not Succeeded).
- **Steps:** `POST /Admin/Billing/Refunds/{paymentId}` body `{ "reason":"x" }`.
- **Expected:** `422` `Successed=false`, PaymentNotRefundable message. No state change.
- **Traces to:** INV-14, INV-17

### BE-TC-49 — refund.succeeded webhook claws back unspent purchased credits (clamped)
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Pack Payment Succeeded for C1; C1 `PurchasedBalance=1000` (unspent); signing secret set.
- **Steps:** POST signed `refund.succeeded` (paymentRef=R1, eventId E4). GET balance.
- **Expected:** `200`, Outcome `PackRefunded:1000`. `PurchasedBalance=0`, Payment Status=Refunded, Refund ledger row (reason `PackRefund`).
- **Traces to:** INV-14

### BE-TC-50 — Refund clawback clamps to available balance (never negative)
- **Type:** boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Pack Payment Succeeded for C1 (pack_size=1000); but C1 already SPENT 400 → `PurchasedBalance=600`.
- **Steps:** POST signed `refund.succeeded` for that pack.
- **Expected:** Clawback clamps to 600 (not 1000). `PurchasedBalance=0`, never negative. Refund ledger amount=600.
- **Traces to:** INV-14

### BE-TC-51 — No double-refund: replayed refund.succeeded is a no-op
- **Type:** negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Pack already refunded (BE-TC-49 done); `PurchasedBalance=0`.
- **Steps:** POST another signed `refund.succeeded` (new eventId E5) for the same payment.
- **Expected:** `200`, Outcome `PackRefundDuplicate` (idempotency key `pack-refund:{paymentId}:{eventId}` OR Payment already Refunded). Balance stays 0. No second Refund ledger row that drives anything.
- **Traces to:** INV-14, INV-7

### BE-TC-52 — Subscription refund downgrades to Free, does NOT claw back granted credits
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Active Premium subscription with a Succeeded subscription Payment; child(ren) have granted monthly credits; signing secret set.
- **Steps:** POST signed `refund.succeeded` for the subscription Payment.
- **Expected:** Subscription PlanCode=Free, Status=Active, cycle/dunning fields cleared, Payment Refunded. Children's already-granted credits UNCHANGED (no clawback). Replay = `SubscriptionRefundDuplicate`.
- **Traces to:** INV-14

---

# Group H — GlobalSettings admin (INV-15, INV-17)

### BE-TC-53 — GET all settings: AdminOnly, returns managed keys
- **Type:** functional / auth-authz · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT.
- **Steps:** `GET /api/Admin/GlobalSettings`.
- **Expected:** `200` `Successed=true`, list of managed keys with value/type/audit (UpdatedBy/UpdatedAt).
- **Traces to:** INV-15

### BE-TC-54 — Update a setting (admin) takes effect immediately (cache invalidation)
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT. ai_cost.hint=1.
- **Steps:** 1) `PUT /api/Admin/GlobalSettings/ai_cost.hint` body `{ "newValue":"4" }`. 2) Deliver a Hint for a child with balance≥4. GET balance.
- **Expected:** `200`. Subsequent Hint delivery debits 4 (new value live immediately, cache invalidated). UpdatedBy derived from JWT (not body).
- **Traces to:** INV-15, INV-4

### BE-TC-55 — Cross-key: free_daily_cap > free_monthly is rejected (422)
- **Type:** validation / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT. free_monthly=100.
- **Steps:** `PUT /GlobalSettings/credits.free_daily_cap` body `{ "newValue":"500" }` (500 > 100).
- **Expected:** `422` `Successed=false`, GlobalSettingDailyCapExceedsMonthly message. Value NOT updated.
- **Traces to:** INV-15

### BE-TC-56 — Cross-key: free_daily_cap == free_monthly is allowed (boundary)
- **Type:** boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT. free_monthly=100.
- **Steps:** `PUT /GlobalSettings/credits.free_daily_cap` body `{ "newValue":"100" }`.
- **Expected:** `200` — `daily ≤ monthly` boundary passes (≤, not <).
- **Traces to:** INV-15

### BE-TC-57 — Unknown key rejected (422 allowlist)
- **Type:** validation · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT.
- **Steps:** `PUT /GlobalSettings/not.a.real.key` body `{ "newValue":"1" }`.
- **Expected:** `422`, GlobalSettingKeyNotAllowed. No row created.
- **Traces to:** INV-15

### BE-TC-58 — Type mismatch rejected (422)
- **Type:** validation · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT. ai_cost.hint is Int.
- **Steps:** `PUT /GlobalSettings/ai_cost.hint` body `{ "newValue":"abc" }`.
- **Expected:** `422`, GlobalSettingValueTypeMismatch. No change.
- **Traces to:** INV-15

### BE-TC-59 — Out-of-range rejected (422) — negative cost & confidence > 1
- **Type:** validation / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT.
- **Steps:** a) `PUT /GlobalSettings/ai_cost.hint` `{ "newValue":"-1" }`. b) `PUT /GlobalSettings/ai.cache.autoApprovalConfidence` `{ "newValue":"1.5" }`. c) `PUT /GlobalSettings/credits.pack_size` `{ "newValue":"0" }` (must be > 0).
- **Expected:** Each → `422` GlobalSettingValueOutOfRange. No changes.
- **Traces to:** INV-15

### BE-TC-60 — Empty newValue rejected (422)
- **Type:** validation · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT.
- **Steps:** `PUT /GlobalSettings/ai_cost.hint` body `{ "newValue":"" }`.
- **Expected:** `422`, GlobalSettingValueRequired.
- **Traces to:** INV-15, INV-17

### BE-TC-61 — Non-admin cannot read or update GlobalSettings (403)
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent (or student) JWT — non-admin.
- **Steps:** a) `GET /api/Admin/GlobalSettings`. b) `PUT /GlobalSettings/ai_cost.hint` `{ "newValue":"2" }`.
- **Expected:** Both `403` (AdminOnly). No setting changed.
- **Traces to:** INV-15, INV-16

### BE-TC-62 — Unauthenticated GlobalSettings access → 401
- **Type:** auth-authz · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** No JWT.
- **Steps:** `GET /api/Admin/GlobalSettings`.
- **Expected:** `401`.
- **Traces to:** INV-15, INV-16

---

# Group I — IDOR / authorization (INV-16)

### BE-TC-63 — Parent cannot view another family's child credit balance (403)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 linked to C1; child C9 belongs to P2. P1 JWT.
- **Steps:** `GET /api/Billing/Credits/{C9}`.
- **Expected:** `403` (NotAuthorizedForChild) — same generic Forbidden whether C9 exists or is unlinked (anti-IDOR). No balance leaked.
- **Traces to:** INV-16

### BE-TC-64 — Student cannot view another child's credit balance (403)
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Student JWT for C1.
- **Steps:** `GET /api/Billing/Credits/{C2}` (a different child).
- **Expected:** `403`. Owner-only for students.
- **Traces to:** INV-16

### BE-TC-65 — Child can view OWN balance; parent can view linked child's balance
- **Type:** auth-authz · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Student JWT C1; Parent JWT P1 linked to C1.
- **Steps:** a) Student C1 `GET /Credits/{C1}`. b) Parent P1 `GET /Credits/{C1}`.
- **Expected:** Both `200` with C1's balance (allowed paths).
- **Traces to:** INV-16

### BE-TC-66 — Energy status IDOR: cross-child blocked, own/linked allowed, admin any
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Student C1, Parent P1(→C1), Admin, child C9 (other family).
- **Steps:** a) C1 `GET /Energy/{C1}/Status` → 200. b) C1 `GET /Energy/{C9}/Status` → 403. c) P1 `GET /Energy/{C1}/Status` → 200. d) P1 `GET /Energy/{C9}/Status` → 403. e) Admin `GET /Energy/{C9}/Status` → 200.
- **Expected:** As annotated.
- **Traces to:** INV-16

### BE-TC-67 — Billing history is parent-scoped; receipt for another parent's payment → 404
- **Type:** auth-authz / IDOR · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent P1 + P2 each with payments. P1 JWT.
- **Steps:** a) P1 `GET /Billing/History` → only P1's items. b) P1 `GET /Billing/History/Receipt/{paymentId_of_P2}`.
- **Expected:** a) History lists ONLY P1's charges/refunds (parentId from JWT, not query). b) `404` (no distinction exists-vs-not-owned; anti-IDOR).
- **Traces to:** INV-16

### BE-TC-68 — `/Credits/Spend` and `/Grant` require Billing.Create; Reconcile requires Billing.View
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** A non-privileged JWT (parent) and a JWT lacking the policies.
- **Steps:** a) `POST /Credits/Spend` as parent. b) `POST /Credits/Grant` as parent. c) `GET /Credits/{C1}/Reconcile` as parent.
- **Expected:** Each `403` (policy not satisfied). Deny-by-default.
- **Traces to:** INV-16

### BE-TC-69 — Admin refund endpoint AdminOnly; non-admin → 403
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Parent JWT.
- **Steps:** `POST /api/Admin/Billing/Refunds/{paymentId}` body `{ "reason":"x" }`.
- **Expected:** `403`. No refund initiated.
- **Traces to:** INV-16

### BE-TC-70 — Unauthenticated money endpoints → 401
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** No JWT.
- **Steps:** `GET /Credits/{C1}`, `POST /Subscription/Checkout`, `POST /Packs/Checkout`, `GET /Billing/History`.
- **Expected:** Each `401` (all `[Authorize]`). Webhook endpoint is the documented JWT-free exception (covered in Group F).
- **Traces to:** INV-16

---

# Group J — Envelope / validation / status mapping (INV-17)

### BE-TC-71 — `/Credits/Spend` validation: 422 on non-positive amount / blank reason / blank key
- **Type:** validation · **Priority:** P0 · **Target:** api-tester
- **Preconditions/seed:** Admin/ops JWT with Billing.Create.
- **Steps:** a) `{childId:C1, amount:0, reasonCode:"AiHint", idempotencyKey:"k"}`. b) amount:-3. c) reasonCode:"". d) idempotencyKey:"". e) childId:0.
- **Expected:** Each `422` `Successed=false` with the FluentValidation message. No debit. (SpendCommand is `ICommand` → ValidationBehavior runs.)
- **Traces to:** INV-17

### BE-TC-72 — Admin refund body validation: blank/oversized reason → 422
- **Type:** validation / boundary · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Admin JWT; a Succeeded Payment.
- **Steps:** a) reason="" b) reason = 1001-char string.
- **Expected:** `422` (reason required; max 1000). No provider call.
- **Traces to:** INV-17 · **Note:** confirm validator bounds in `InitiateRefundValidator`.

### BE-TC-73 — Envelope shape on success uses `Successed` spelling + correct status code
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions/seed:** Any happy-path money read (e.g. `GET /Credits/{C1}` owner).
- **Steps:** GET; inspect the JSON envelope.
- **Expected:** Body has `Successed: true` (NOT `Success`/`Succeeded`), a `StatusCode` of 200, `Data` populated, `Message` present. Confirms `BaseResponse<T>` contract preserved by the refactor.
- **Traces to:** INV-17

### BE-TC-74 — Webhook for unknown event type returns 200 (no retry storm), records WebhookEvent
- **Type:** functional · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Signing secret set.
- **Steps:** POST a validly-signed webhook with `eventType:"subscription.paused"` (unhandled).
- **Expected:** `200` Outcome `Unhandled`. WebhookEvent row recorded; no money mutation. (Provider should not retry.)
- **Traces to:** INV-10, INV-17

### BE-TC-75 — payment.succeeded for unknown paymentRef returns 200 (recorded, no mutation)
- **Type:** negative · **Priority:** P2 · **Target:** api-tester
- **Preconditions/seed:** Signing secret set; paymentRef that matches no Payment.
- **Steps:** POST signed `payment.succeeded` with unknown ref.
- **Expected:** `200` Outcome `PaymentNotFound`. WebhookEvent recorded; no Payment/Subscription change.
- **Traces to:** INV-10, INV-11

---

## Notes for `api-tester`
- IDs are stable and zero-padded; do not reuse. One test method per case (1:1).
- Where a case asserts a ledger/DB side-effect, prefer asserting via the public read endpoints (`/Credits/{childId}`, `/Energy/{childId}/Status`, `/Billing/History`) plus, where a row's pool/split is the assertion target, a direct DB read in the integration harness.
- For webhook cases, drive the signed payload through `FakePaymentProvider.BuildSignedWebhookPayload(...)` with the SAME secret the test host is configured with.
- BE-TC-25 and BE-TC-43 may be **blocked** in the standard harness — record the blocker in `execution-report.md` rather than forcing a flaky test.
- The locked-economy HIT/MISS cases (BE-TC-01/02) are the crux of the refactor verification — give them the most care.
