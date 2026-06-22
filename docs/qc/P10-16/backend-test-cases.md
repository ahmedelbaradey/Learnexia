# P10-16 — Family energy redistribution & mid-cycle mechanism — Backend QC test cases

**Story:** `user-stories/Phase-10-Payments-Billing/P10-16-family-energy-redistribution.md`
**Task:** `tasks/Backend/Phase-10-Payments-Billing/P10-16-BE.md`
**Surface under test:** `IFamilyAllocationService` (`GetFamilyAllocationAsync`, `TransferAllocationAsync`), the `TransferAllocationCommand` validator, paired immutable ledger entries (`TransferOut`/`TransferIn`, shared `CorrelationId`, idempotency), the mid-cycle zero-allocation destination INSERT path, and routes `GET /api/Billing/FamilyEnergy/Allocation` + `POST /api/Billing/FamilyEnergy/Transfer`.
**Existing suite:** `P10_16_FamilyAllocation_IntegrationTests` (19 tests). **Already QC'd by integration tests** — this doc traces ACs to existing tests and flags gaps. Design-only.

## Money / correctness lenses applied
- **Zero-sum** — source `Remaining` ↓ amount, destination `Remaining` ↑ amount; family bucket-A total unchanged.
- **Only UNSPENT movable** — cap is source `Remaining` (allocated − spent), never original allocated; already-spent never reclaimable.
- **Buckets non-convertible** — transfer touches bucket A allocated only; `PurchasedBalance` (bucket B) never touched/converted.
- **Atomicity** — paired debit+credit + 2 ledger rows (+ optional destination INSERT) commit together or not at all.
- **Idempotency** — same idempotency key → no double-move.
- **IDOR / authz** — both children must belong to the same family; cross-family rejected by construction; parent-JWT-only.
- **Mid-cycle** — zero-allocation active-seat destination is VALID (INSERT row), not a rejection.

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to AC | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|--------------|---------------|
| QC-16-01 | GetAllocation returns 200 + per-child snapshot | functional | P1 | parent + children with allocation | `GET /FamilyEnergy/Allocation` | 200; envelope; per-child `allocated/spent/remaining` | View per-child split | `ALLOC_GET_01_GetAllocation_Returns200_WithPerChildSnapshot` |
| QC-16-02 | GetAllocation: mid-cycle child has isMidCycleNoGrant=true | functional | P0 | child added after grant (no alloc row) | GetAllocation | child present; `isMidCycleNoGrant=true`, remaining=0 | Mid-cycle child surfaced | `ALLOC_GET_02_GetAllocation_MidCycleChild_HasNoGrantFlag` |
| QC-16-03 | GetAllocation family-scoped (no IDOR) | auth-authz | P0 | Parent A + B | B reads allocation | only B's children; no A leakage | Parent-gated, family-scope | `ALLOC_GET_03_GetAllocation_FamilyScoped_NoIDOR` |
| QC-16-04 | GetAllocation authz — anon 401, child 401/403 | auth-authz | P0 | child JWT | anon + child call | anon→401; child→401/403 | Children can't view | `ALLOC_GET_04_GetAllocation_AnonymousAndChildJwt_Rejected` |
| QC-16-05 | GetAllocation: no wallet yet → 404 | state | P1 | parent, no wallet | GetAllocation | 404 | No wallet state | `ALLOC_GET_05_GetAllocation_NoWallet_Returns404` |
| QC-16-06 | Transfer happy path — zero-sum balance move | functional | P0 | 2 children with allocation | transfer amount A→B | source `remaining` ↓, dest `remaining` ↑ equal; family total unchanged | Core transfer (zero-sum) | `TRANSFER_01_TransferHappyPath_BalancesUpdatedZeroSum` |
| QC-16-07 | Transfer writes paired ledger with shared CorrelationId | persistence | P0 | 2 children | transfer | `TransferOut` (source) + `TransferIn` (dest) entries; same `CorrelationId` | Paired immutable ledger | `TRANSFER_02_TransferHappyPath_PairedLedgerWithSharedCorrelationId` |
| QC-16-08 | Transfer does NOT touch PurchasedBalance (bucket B) | functional | P0 | wallet with purchased balance | transfer A→B | `PurchasedBalance` unchanged | Bucket A only, non-convertible | `TRANSFER_03_Transfer_DoesNotTouchPurchasedBalance` |
| QC-16-09 | IDOR — toChildId foreign family → 403, no mutation | auth-authz | P0 | dest child in another family | transfer to foreign child | 403; no balance change | Cross-family rejected | `TRANSFER_04_IDOR_ToChildForeignFamily_Returns403_NoMutation` |
| QC-16-10 | IDOR — fromChildId foreign family → 403 | auth-authz | P0 | source child in another family | transfer from foreign child | 403 | Cross-family rejected | `TRANSFER_05_IDOR_FromChildForeignFamily_Returns403` |
| QC-16-11 | Amount > source.remaining → 422 | boundary | P0 | source remaining R | transfer R+1 | 422 rejected; no move | Cap at remaining | `TRANSFER_06_AmountExceedsRemaining_Rejected` |
| QC-16-12 | Amount = 0 → validator 422 | validation | P1 | valid children | transfer 0 | 422 | amount > 0 | `TRANSFER_07_AmountZero_ValidatorRejects` |
| QC-16-13 | Amount < 0 → validator 422 | validation | P1 | valid children | transfer -1 | 422 | amount > 0 | `TRANSFER_08_AmountNegative_ValidatorRejects` |
| QC-16-14 | from == to → validator 422 | validation | P1 | one child | transfer self→self | 422 | fromChildId ≠ toChildId | `TRANSFER_09_SameSourceAndDest_ValidatorRejects` |
| QC-16-15 | Cap is remaining, not allocated; spent immovable | boundary | P0 | child allocated then partially spent | transfer up to remaining only | cannot move spent energy; cap = remaining | Already-spent never reclaimable | `TRANSFER_10_CapIsRemaining_SpentEnergyImmovable` |
| QC-16-16 | Transfer to mid-cycle dest — row INSERTed | functional | P0 | dest child active-seat, no alloc row | transfer A→dest | dest row INSERTed; `remaining = amount` | Mid-cycle zero-allocation INSERT | `TRANSFER_11_Transfer_ToMidCycleDestination_InsertsRowAndSetsRemaining` |
| QC-16-17 | Idempotency — same key → no double-move | negative | P0 | 2 children | transfer twice (same key) | single move; no double-debit/credit | Idempotent transfer | `TRANSFER_12_Idempotency_SameKey_NoDoubleDMove` |
| QC-16-18 | Transfer from NoSeatLocked source → rejected | negative | P1 | source child locked | transfer from locked | rejected | Active-seat source only | `TRANSFER_13_SourceNoSeatLocked_Rejected` |
| QC-16-19 | Transfer to NoSeatLocked destination → rejected | negative | P0 | dest child locked | transfer to locked | rejected (not active-seat) | Destination must hold active seat | `TRANSFER_14_DestinationNoSeatLocked_Rejected` |

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-16-A (atomicity under failure):** AC requires "either both ledger rows + both allocation updates commit, or none." No test forces a mid-transaction failure (e.g. concurrency conflict on the destination row INSERT) to prove a clean rollback with balances untouched. **Add a P1** (hard to force deterministically — may need a fault-injection seam; mark **blocked / best-effort** if the harness cannot inject a failure).
- **GAP-16-B (idempotent row-creation case):** TRANSFER-12 proves idempotency for an existing-row transfer. The note in BE-16 stresses idempotency must also cover the **mid-cycle INSERT** case (retry must not INSERT twice when the first attempt already created the dest row). **Add a P1** combining QC-16-16 + same key replay.
- **GAP-16-C (concurrent transfers on the same source):** Two concurrent transfers draining the same source `Remaining` must not over-draw (no negative remaining). **Add a P1** concurrency case (best-effort).
- **Better suited to unit tests:** the `Remaining = Allocated − Spent` cap computation and the same-family ownership guard predicate.
