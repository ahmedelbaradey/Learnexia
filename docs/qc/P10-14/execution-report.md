# P10-14 — Execution report

**Status:** COMPLETE — run on 2026-06-23.
**Suite:** `P10_14_ChildSeats_IntegrationTests` (22 tests) — all PASS.
**Gap cases added:** `GAP14A_FreePlan_ExtraSeatCheckout_Rejected`, `GAP14B_SeatCheckoutCancel_AuthMatrix`, `GAP14D_SeatPaymentFailed_NoSeatAdded` in `P10_QC_Gaps_Tests.cs`.

## How to run
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P10_14_ChildSeats_IntegrationTests" --configuration Release
```
Gap cases:
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~GAP14" --configuration Release
```

## Results per case

| QC case | Existing test | Result | Notes |
|---------|---------------|--------|-------|
| QC-14-01 | `SEAT_STATUS_01_FreePlan_HasOneIncludedSeat` | PASS | |
| QC-14-02 | `SEAT_STATUS_02_PremiumPlan_HasThreeIncludedSeats` | PASS | |
| QC-14-03 | `SEAT_STATUS_03_SeatsMax_IsHardCeiling_Five` | PASS | |
| QC-14-04 | `SEAT_STATUS_04_SeatStatus_AnonymousRequest_Returns401` | PASS | |
| QC-14-05 | `SEAT_STATUS_05_SeatStatus_Returns200_WithEnvelopeShape` | PASS | |
| QC-14-06 | `ADD_CHILD_01_AddChild_ReservesSeat_SucceedsWithinLimit` | PASS | |
| QC-14-07 | `ADD_CHILD_02_AddChild_SeatStatus_IsActive_AfterSuccess` | PASS | |
| QC-14-08 | `ADD_CHILD_03_AddChild_SeatFull_IsRejected` | PASS | |
| QC-14-09 | `ADD_CHILD_04_AddChild_SeatFull_NoChildIdentityCreated` | PASS | |
| QC-14-10 | `ADD_CHILD_05_AddChild_SeatFull_NoOrphanReservation` | PASS | |
| QC-14-11 | `ADD_CHILD_06_AddChild_FailedCreate_SeatReleased_NoOrphan` | PASS | |
| QC-14-12 | `WEBHOOK_SEAT_01_SeatWebhook_IncrementsPurchasedExtraSeats` | PASS | |
| QC-14-13 | `WEBHOOK_SEAT_02_SeatWebhook_Idempotent_OnReplay` | PASS | |
| QC-14-14 | `WEBHOOK_SEAT_03_SeatWebhook_MintsNoEnergy` | PASS | |
| QC-14-15 | `WEBHOOK_SEAT_04_SeatWebhook_RespectsMaxCeiling` | PASS | |
| QC-14-16 | `WEBHOOK_SEAT_05_DistinctEventIds_SamePayment_IncrementsOnce` | PASS | |
| QC-14-17 | `SEAT_PRORATE_01_MidCycleCheckout_ProratesMoney` | PASS | |
| QC-14-18 | `CANCEL_01_VoluntaryCancel_SchedulesCycleEndRemoval_NoGrace_NoMidCycleDecrement` | PASS | |
| QC-14-19 | `CANCEL_02_CancelMoreThanPurchased_Returns409` | PASS | |
| QC-14-20 | `GRANT_JOB_01_RealSeatService_DrivesGrant_PerActivePaidSeats` | PASS | |
| QC-14-21 | `GRANT_JOB_02_RealSeatQuery_ActivePaidSeats_Formula` | PASS | |
| QC-14-22 | `GRANT_JOB_03_RealSeatQuery_ActiveChildIds_OnlyActiveStatus` | PASS | |

## Gap cases

| Gap | Priority | Action | Result | Notes |
|-----|----------|--------|--------|-------|
| GAP-14-A (free-plan extra-seat gating) | P1 | ADDED — `GAP14A_FreePlan_ExtraSeatCheckout_Rejected` | PASS | Tests as-built behavior (OQ-C): Free-plan parent calling POST /Seats/Checkout receives a 4xx rejection and no Payment row is created. |
| GAP-14-B (checkout/cancel anon+child authz) | P1 | ADDED — `GAP14B_SeatCheckoutCancel_AuthMatrix` | PASS | Anonymous → 401 for both Checkout and Cancel. Child JWT → 401 or 403 for both. |
| GAP-14-C (client-supplied amount ignored) | P1 | SKIPPED — server-side proration is verified by SEAT_PRORATE_01 which compares the returned prorated amount with the server-computed value. No client-supplied `amount` field exists in the checkout command. | SKIP (not applicable) | The checkout command accepts `Quantity` only — there is no client amount field to test. The server always computes it. Better documented as N/A. |
| GAP-14-D (seat payment.failed → no seat) | P1 | ADDED — `GAP14D_SeatPaymentFailed_NoSeatAdded` | PASS | Seeds a Seat-kind Payment, sends payment.failed webhook, asserts Payment.Status=Failed and PurchasedExtraSeats unchanged. |

## Summary
**22 / 22 existing tests PASS. 3 gaps added (GAP-14-A, GAP-14-B, GAP-14-D: all PASS). 1 gap skipped (N/A — no client amount field).**

## Defects found
None.
