# P10-16 — Execution report

**Status:** COMPLETE — run on 2026-06-23.
**Suite:** `P10_16_FamilyAllocation_IntegrationTests` (19 tests) — all PASS.
**Gap cases added:** `GAP16B_Idempotent_MidCycleInsert_Replay_NoDoubleMove` in `P10_QC_Gaps_Tests.cs`.

## How to run
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P10_16_FamilyAllocation_IntegrationTests" --configuration Release
```
Gap case:
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~GAP16B" --configuration Release
```

## Results per case

| QC case | Existing test | Result | Notes |
|---------|---------------|--------|-------|
| QC-16-01 | `ALLOC_GET_01_GetAllocation_Returns200_WithPerChildSnapshot` | PASS | |
| QC-16-02 | `ALLOC_GET_02_GetAllocation_MidCycleChild_HasNoGrantFlag` | PASS | |
| QC-16-03 | `ALLOC_GET_03_GetAllocation_FamilyScoped_NoIDOR` | PASS | |
| QC-16-04 | `ALLOC_GET_04_GetAllocation_AnonymousAndChildJwt_Rejected` | PASS | |
| QC-16-05 | `ALLOC_GET_05_GetAllocation_NoWallet_Returns404` | PASS | |
| QC-16-06 | `TRANSFER_01_TransferHappyPath_BalancesUpdatedZeroSum` | PASS | |
| QC-16-07 | `TRANSFER_02_TransferHappyPath_PairedLedgerWithSharedCorrelationId` | PASS | |
| QC-16-08 | `TRANSFER_03_Transfer_DoesNotTouchPurchasedBalance` | PASS | |
| QC-16-09 | `TRANSFER_04_IDOR_ToChildForeignFamily_Returns403_NoMutation` | PASS | |
| QC-16-10 | `TRANSFER_05_IDOR_FromChildForeignFamily_Returns403` | PASS | |
| QC-16-11 | `TRANSFER_06_AmountExceedsRemaining_Rejected` | PASS | |
| QC-16-12 | `TRANSFER_07_AmountZero_ValidatorRejects` | PASS | |
| QC-16-13 | `TRANSFER_08_AmountNegative_ValidatorRejects` | PASS | |
| QC-16-14 | `TRANSFER_09_SameSourceAndDest_ValidatorRejects` | PASS | |
| QC-16-15 | `TRANSFER_10_CapIsRemaining_SpentEnergyImmovable` | PASS | |
| QC-16-16 | `TRANSFER_11_Transfer_ToMidCycleDestination_InsertsRowAndSetsRemaining` | PASS | |
| QC-16-17 | `TRANSFER_12_Idempotency_SameKey_NoDoubleDMove` | PASS | |
| QC-16-18 | `TRANSFER_13_SourceNoSeatLocked_Rejected` | PASS | |
| QC-16-19 | `TRANSFER_14_DestinationNoSeatLocked_Rejected` | PASS | |

## Gap cases

| Gap | Priority | Action | Result | Notes |
|-----|----------|--------|--------|-------|
| GAP-16-A (atomicity rollback under failure) | P1, best-effort | SKIPPED — cannot inject a mid-transfer failure via the HTTP-level harness without intercepting EF SaveChanges. The transfer is implemented as a single EF transaction (`SaveChangesAsync` at end). A real failure would require mock injection. Deferred as a unit test on the repository/transaction boundary. | SKIP — requires fault injection | Covered architecturally: `GenericRepository`/UoW rule (ADR-0001). Unit test recommended. |
| GAP-16-B (idempotent mid-cycle INSERT replay) | P1 | ADDED — `GAP16B_Idempotent_MidCycleInsert_Replay_NoDoubleMove` | PASS | Seeds only source child's allocation (dest is mid-cycle zero). First transfer INSERTs dest row and sets Remaining=50. Replay with same idempotency key returns success (no-op). Asserts both src and dest Remaining unchanged on replay, dest allocation row count still 1. |
| GAP-16-C (concurrent transfers same source) | P1, best-effort | SKIPPED — concurrent transfer race would require two parallel HTTP clients hammering the same source child simultaneously. The DB-level check (`Remaining >= amount` in a transaction) is the production guard. No parallel-client concurrency test added. Deferred as a load/concurrency unit test on the service. | SKIP — concurrency harness limitation | Race condition prevention is DB-level (transactional read-then-write). |

## Summary
**19 / 19 existing tests PASS. 1 gap added (GAP-16-B: PASS). 2 gaps skipped (fault injection / concurrency harness limits).**

## Defects found
None.
