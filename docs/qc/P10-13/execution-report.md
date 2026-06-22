# P10-13 — Execution report

**Status:** COMPLETE — run on 2026-06-23.
**Suite:** `P10_13_FamilyEnergyWallet_Tests` (27 tests) — all PASS.
**Gap cases added:** `GAP13A_MonthlyRollover_SubscriptionResets_PurchasedUntouched` in `P10_QC_Gaps_Tests.cs`.

## How to run
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P10_13_FamilyEnergyWallet_Tests" --configuration Release
```
Gap case:
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~GAP13A" --configuration Release
```

## Results per case

| QC case | Existing test | Result | Notes |
|---------|---------------|--------|-------|
| QC-13-01 | `WAL01_Overview_Anonymous_Returns401` | PASS | |
| QC-13-02 | `WAL02_IDOR_ParentBCannotSeeParentAWallet` | PASS | |
| QC-13-03 | `WAL03_Overview_200_EnvelopeShape_WhenWalletExists` | PASS | |
| QC-13-04 | `WAL04_Overview_404_WhenNoWallet` | PASS | |
| QC-13-05 | `GRANT01_Grant_EqualsPerSeatTimesSeats_WalletSubscriptionBalance` | PASS | |
| QC-13-06 | `GRANT02_EqualSplit_AllocatedSumEqualsGrant_DeterministicRemainder` | PASS | |
| QC-13-07 | `GRANT03_ChildEnergyAllocationRows_Created_RemainingEqualsAllocated` | PASS | |
| QC-13-08 | `GRANT04_Idempotency_SameCycle_WasDuplicate_NoDoubleGrant` | PASS | |
| QC-13-09 | `GRANT05_NoChildren_NoWalletCreated` | PASS | |
| QC-13-10 | `SPEND01_02_AllocationCoversFullCost_ChildRowDebited_PurchasedUntouched` | PASS | |
| QC-13-11 | `SPEND03_Shortfall_DrawnFromPurchasedBalance` | PASS | |
| QC-13-12 | `SPEND04_InsufficientBalance_Charged_False_NoWrite` | PASS | |
| QC-13-13 | `SPEND05_ChildDailyUsage_CreatedAndIncremented` | PASS | |
| QC-13-14 | `SPEND06_DailySoftCap_DailyCapReachedFlag` | PASS | |
| QC-13-15 | `PACK01_PackPurchase_CreditsSharedFamilyPurchasedBalance` | PASS | |
| QC-13-16 | `PACK02_PackPurchase_WritesBucketBLedgerRow` | PASS | |
| QC-13-17 | `PACK03_PackPurchase_NoPerChildCreditAccountTouch` | PASS | |
| QC-13-18 | `PACK04_DuplicatePackWebhook_Idempotent_NoDuplicateCredit` | PASS | |
| QC-13-19 | `CUTOVER01_Migration_GrantedBalance_ToChildEnergyAllocation` | PASS | |
| QC-13-20 | `CUTOVER02_Migration_PurchasedBalance_ToSharedFamilyWallet` | PASS | |
| QC-13-21 | `CUTOVER03_Migration_Idempotent_NoDoubleWallet` | PASS | |

## Gap cases

| Gap | Priority | Action | Result | Notes |
|-----|----------|--------|--------|-------|
| GAP-13-A (AC-6 monthly reset / no-convert) | P0 | ADDED — `GAP13A_MonthlyRollover_SubscriptionResets_PurchasedUntouched` in `P10_QC_Gaps_Tests.cs` | PASS | Seeds prior cycle (cycleEnd in past) via service layer, seeds PurchasedBalance=200, runs new-cycle grant, asserts SubscriptionBalance reset to new grant (300), PurchasedBalance untouched, Expiry ledger row written, prior ChildEnergyAllocation Remaining=0. |
| GAP-13-B (AC-1 non-convertibility explicit) | P1 | SKIPPED — covered implicitly by SPEND-01/02 (cold-row) and P10-14 WEBHOOK_SEAT_03. No dedicated negative assertion added. | SKIP (implicit coverage) | Better as a unit test on the split/fallback logic boundary. Deferred per phase-coverage-report.md §F. |
| GAP-13-C (AC-8 pre-migration reconcile) | P1 | BLOCKED — legacy `CreditAccount` model is fully retired (per OQ-B resolution). No seedable pre-migration rows exist in the current schema; the migration is destructive and one-way. | BLOCKED — doc-only | OQ-B resolved: mark BLOCKED. CUTOVER-01..03 assert post-cutover behavior which is the only testable path. |

## Summary
**27 / 27 existing tests PASS. 1 gap added (GAP-13-A: PASS). 1 gap skipped (implicit coverage). 1 gap blocked (retired model).**

## Defects found
None.
