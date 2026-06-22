# P10-13 — Family energy wallet & per-child allocation — Backend QC test cases

**Story:** `user-stories/Phase-10-Payments-Billing/P10-13-family-energy-wallet-per-child-allocation.md`
**Task:** `tasks/Backend/Phase-10-Payments-Billing/P10-13-BE.md`
**Surface under test:** Billing module — `FamilyEnergyAccount` (two non-convertible buckets), `ChildEnergyAllocation`, `IFamilyEnergyAllocationService`, the re-implemented `ICreditSpendService` (allocation-first → purchased-fallback), pack-credit re-home (BE-12), the `CreditAccount → FamilyEnergyAccount` cutover, and `GET /api/Billing/FamilyEnergy/Overview`.
**Existing suite:** `P10_13_FamilyEnergyWallet_Tests` (27 tests). **This story is already QC'd by integration tests** — this doc traces each AC to the existing test and flags genuine gaps. Design-only; the `api-tester` owns implementation/execution.

> Spelling note: the success flag in the `BaseResponse<T>` envelope is **`Successed`** (do not "correct" it). Validation 422 applies to `ICommand<>` bodies only; queries are not auto-validated.

## Money / correctness lenses applied
- **Non-convertible buckets** — no path moves energy `Subscription ↔ Purchased`.
- **No energy lost or invented** — equal-split allocated sum == grant exactly (deterministic remainder).
- **Cold-row rule** — the shared `PurchasedBalance` is touched ONLY on a child-allocation shortfall.
- **Never-negative** — insufficient balance → no write, `Charged=false`.
- **Idempotency** — grant idempotent on `(parentUserId, cycleStart)`; pack webhook idempotent on `ProviderEventId`.
- **IDOR / authz** — overview is parent-JWT-only and family-scoped.

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to AC | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|--------------|---------------|
| QC-13-01 | Overview requires JWT | auth | P0 | none | `GET /api/Billing/FamilyEnergy/Overview` anon | 401 Unauthorized | AC-9 | `WAL01_Overview_Anonymous_Returns401` |
| QC-13-02 | Overview IDOR — Parent B cannot see Parent A wallet | auth-authz | P0 | Parent A wallet provisioned (grant), Parent B has none | Parent B calls Overview with own JWT | 404 (or 200 with zero balances); never Parent A's non-zero `subscriptionBalance` | AC-9 | `WAL02_IDOR_ParentBCannotSeeParentAWallet` |
| QC-13-03 | Overview envelope + data shape | functional | P1 | wallet provisioned, ≥1 child allocation | Overview with parent JWT | 200; `statusCode`/`successed=true`/`data`; data has `subscriptionBalance`,`purchasedBalance`,`children[]` (≥1) | AC-1, AC-9 | `WAL03_Overview_200_EnvelopeShape_WhenWalletExists` |
| QC-13-04 | Overview 404 when no wallet | state | P1 | parent, no children, no grant | Overview | 404 NotFound | AC-1 | `WAL04_Overview_404_WhenNoWallet` |
| QC-13-05 | Grant = PlanEnergyPerSeat × ActivePaidSeats | functional | P0 | parent + 2 children + seats granted | `AllocateSubscriptionGrantAsync(perSeat×seats)` | wallet `SubscriptionBalance == perSeat × seats` (100×2=200) | AC-2 | `GRANT01_Grant_EqualsPerSeatTimesSeats_WalletSubscriptionBalance` |
| QC-13-06 | Equal split — sum==grant, deterministic remainder | boundary | P0 | parent + 3 children (Premium seats) | grant 100 / 3 children | sum allocated == 100; split 34/33/33; exactly 1 child gets +1 | AC-3 | `GRANT02_EqualSplit_AllocatedSumEqualsGrant_DeterministicRemainder` |
| QC-13-07 | Allocation rows created; Remaining = Allocated | persistence | P1 | parent + 1 child | grant 100 | row: `AllocatedAmount=100`,`SpentAmount=0`,`Remaining=100`; cycle bounds set | AC-3, AC-5 | `GRANT03_ChildEnergyAllocationRows_Created_RemainingEqualsAllocated` |
| QC-13-08 | Grant idempotent on (parent, cycle) — no double-grant | persistence | P0 | parent + child, grant once | grant again, same key | 2nd `WasDuplicate=true`; `SubscriptionBalance` NOT doubled | AC-2, AC-5 | `GRANT04_Idempotency_SameCycle_WasDuplicate_NoDoubleGrant` |
| QC-13-09 | No active children → nothing allocated, no wallet | boundary | P1 | parent, no children | grant 100 | `ChildCount=0`,`TotalAllocated=0`; no `FamilyEnergyAccount` row created | AC-2 | `GRANT05_NoChildren_NoWalletCreated` |
| QC-13-10 | Spend debits child allocation first; PurchasedBalance untouched | functional | P0 | grant 100 to 1 child, purchased seeded | `TryDebitAsync(child,10)` | `SpentAmount=10`, `Remaining` ↓ 10; `PurchasedBalance` unchanged (cold-row) | AC-4 | `SPEND01_02_AllocationCoversFullCost_ChildRowDebited_PurchasedUntouched` |
| QC-13-11 | Shortfall drawn from PurchasedBalance | functional | P0 | grant 5, purchased seed 100, spend 8 | `TryDebitAsync(child,8)` | allocation fully spent (5), `Remaining=0`; purchased ↓ shortfall 3 (97) | AC-4 | `SPEND03_Shortfall_DrawnFromPurchasedBalance` |
| QC-13-12 | Insufficient balance → no write, never negative | negative | P0 | grant 0, purchased 0 | `TryDebitAsync(child,1)` | `Charged=false`, `Outcome=InsufficientBalance`; no new `CreditTransaction` rows | AC-4, AC-5 | `SPEND04_InsufficientBalance_Charged_False_NoWrite` |
| QC-13-13 | Daily-usage row created/incremented on spend | persistence | P2 | grant 50 | `TryDebitAsync(child,3)` | `ChildDailyUsage` row exists; `DailyUsed ≥ 3` | AC-7 | `SPEND05_ChildDailyUsage_CreatedAndIncremented` |
| QC-13-14 | Daily soft cap — flag set, not hard-blocked | boundary | P1 | grant 200, spend = cap (10) | spend cap amount; `GetBalanceAsync` | spend succeeds; `DailyCapReached=true`; `DailyUsed ≥ 10` | AC-7 | `SPEND06_DailySoftCap_DailyCapReachedFlag` |
| QC-13-15 | Pack purchase lands on shared PurchasedBalance | functional | P0 | parent + child, seeded Pack payment | `payment.succeeded` webhook | `PurchasedBalance += PackSize (1000)` on `FamilyEnergyAccount` | AC-1, AC-8 (re-home) | `PACK01_PackPurchase_CreditsSharedFamilyPurchasedBalance` |
| QC-13-16 | Pack writes bucket-B Purchase ledger row | persistence | P1 | seeded Pack payment | pack webhook | `CreditTransaction{Type=Purchase, SourceBucket=Purchased, ReasonCode=PackPurchase, Amount=1000}` linked to payment | AC-5 | `PACK02_PackPurchase_WritesBucketBLedgerRow` |
| QC-13-17 | Pack does NOT touch CreditAccount / allocation rows | functional | P1 | seeded Pack payment | pack webhook | no per-child `CreditAccount`/`ChildEnergyAllocation` mutation | AC-1, AC-9 | `PACK03_PackPurchase_NoPerChildCreditAccountTouch` |
| QC-13-18 | Duplicate pack webhook — idempotent, balance not doubled | negative | P0 | seeded Pack payment | replay same `ProviderEventId` | 2nd is no-op; `PurchasedBalance` not doubled | AC-5 | `PACK04_DuplicatePackWebhook_Idempotent_NoDuplicateCredit` |
| QC-13-19 | Cutover: granted balance → ChildEnergyAllocation | persistence | P1 | post-cutover admin grant | run grant/migrate | `PurchasedBalance`/allocation reflect granted amount | AC-8 | `CUTOVER01_Migration_GrantedBalance_ToChildEnergyAllocation` |
| QC-13-20 | Cutover: purchased balance → shared family wallet | persistence | P1 | post-cutover pack purchase | accumulate | shared `FamilyEnergyAccount.PurchasedBalance` accrues | AC-8 | `CUTOVER02_Migration_PurchasedBalance_ToSharedFamilyWallet` |
| QC-13-21 | Cutover idempotent — single wallet, no double | persistence | P1 | duplicate admin grant key | re-run | single wallet; balance not doubled; `MigrateAsync` no-op | AC-8 | `CUTOVER03_Migration_Idempotent_NoDoubleWallet` |

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-13-A (AC-6 monthly reset / no-convert):** No test exercises `ExpireSubscription` / `ChildEnergyAllocation.Expire` at cycle rollover — i.e. subscription balance + allocations reset/expire while `PurchasedBalance` is untouched and unspent subscription energy does NOT convert to purchased. **Add a P0 case** driving the rollover (or the expire service seam directly): assert `SubscriptionBalance → 0` (or new-cycle value), `Expire` ledger entries written, `PurchasedBalance` unchanged. This is the only AC with no covering test.
- **GAP-13-B (AC-1 non-convertibility, explicit):** Covered implicitly (SPEND-01/02 cold-row + WEBHOOK-SEAT-03 in P10-14), but no test asserts the absence of any `Subscription ↔ Purchased` move path. **Optional P1** negative assertion: after a spend that hits the purchased fallback, `SubscriptionBalance` is unchanged (no top-up from purchased) and vice-versa.
- **GAP-13-C (AC-8 reconcile-against-ledger):** CUTOVER tests assert post-cutover balances but not the *pre-migration* `CreditAccount → FamilyEnergyAccount` backfill + ledger reconciliation. If the destructive migration path can be seeded with legacy `CreditAccount` rows, **add a P1** asserting roll-up of `PurchasedBalance` + map of `GrantedBalance` → allocation + reconcile. If the harness cannot seed the legacy model, mark **blocked** with that reason.
- **Better suited to unit tests:** the equal-split remainder math (already pinned by QC-13-06 at integration level — keep, but a unit test on the split function would harden boundary cases like grant < childCount, grant=0, 1 child).
