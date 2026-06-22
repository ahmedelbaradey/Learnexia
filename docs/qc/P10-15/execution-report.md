# P10-15 — Execution report

**Status:** COMPLETE — run on 2026-06-23.
**Suite:** `P10_15_SeatLifecycle_IntegrationTests` (26 tests) — all PASS.
**Gap cases added:** `GAP15A_GraceExpiry_Enforcement_LocksOverLimitChildren`, `GAP15B_PaymentSucceededWithinGrace_ClearsGrace` in `P10_QC_Gaps_Tests.cs`.

## How to run
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P10_15_SeatLifecycle_IntegrationTests" --configuration Release
```
Gap cases:
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~GAP15" --configuration Release
```

## Results per case

| QC case | Existing test | Result | Notes |
|---------|---------------|--------|-------|
| QC-15-01 | `LIFECYCLE_STATUS_01_SeatStatus_ReturnsSeatStateAndGraceFields` | PASS | |
| QC-15-02 | `LIFECYCLE_STATUS_02_SeatStatus_AnonymousAndChildReturnsUnauthorized` | PASS | |
| QC-15-03 | `LIFECYCLE_STATUS_03_SeatStatus_FamilyScoped_NoIDOR` | PASS | |
| QC-15-04 | `LIFECYCLE_CHOOSE_01_ChooseActive_WithinLimit_AppliesStates` | PASS | |
| QC-15-05 | `LIFECYCLE_CHOOSE_02_ChooseActive_ExceedsLimit_Returns422` | PASS | |
| QC-15-06 | `LIFECYCLE_CHOOSE_03_ChooseActive_IDOR_ForeignChild_Rejected` | PASS | |
| QC-15-07 | `LIFECYCLE_CHOOSE_04_ChooseActive_WritesLedgerEntries` | PASS | |
| QC-15-08 | `LIFECYCLE_ENFORCE_01_Enforce_LocksOverLimitChildren_EarliestReservedKept` | PASS | |
| QC-15-09 | `LIFECYCLE_ENFORCE_02_Enforce_NoEnergyReclaim_NoChildDeletion` | PASS | |
| QC-15-10 | `LIFECYCLE_ENFORCE_03_Enforce_WritesLedgerEntryPerLockedChild` | PASS | |
| QC-15-11 | `LIFECYCLE_ENFORCE_04_Enforce_Idempotent_NoDoubleAction` | PASS | |
| QC-15-12 | `LIFECYCLE_ENFORCE_04b_Enforce_Idempotent_KeyBackstop_NoDuplicateLedger` | PASS | |
| QC-15-13 | `LIFECYCLE_GRACE_01_PaymentFailureGrace_SetsReasonAndEndsAt` | PASS | |
| QC-15-14 | `LIFECYCLE_GRACE_02_PaymentFailureGrace_Idempotent_OpenWindow` | PASS | |
| QC-15-15 | `LIFECYCLE_GRACE_03_VoluntaryCancel_NoGrace` | PASS | |
| QC-15-16 | `LIFECYCLE_SPEND_01_NoSeatLocked_SpendDenied_SeatLocked` | PASS | |
| QC-15-17 | `LIFECYCLE_SPEND_02_Active_Child_SpendSucceeds` | PASS | |
| QC-15-18 | `LIFECYCLE_IDEMPOTENCY_01_ReserveSeat_SameKey_IdempotentOneRow` | PASS | |
| QC-15-19 | `CONCURRENT_RESERVE_01_TwoParallelAddChild_WithinLimit_TwoDistinctSeatRows` | PASS | |
| QC-15-20 | `CONCURRENT_RESERVE_02_TwoParallelAddChild_OneSeat_OneSucceeds_One409_NoOrphan` | PASS | |
| QC-15-21 | `LIFECYCLE_REACTIVATE_01_Reactivate_ReturnsCheckoutUrl_NoEnergy` | PASS | |
| QC-15-22 | `LIFECYCLE_REACTIVATE_02_ReactivationWebhook_FlipsSeatToActive_WritesLedger` | PASS | |
| QC-15-23 | `LIFECYCLE_REACTIVATE_03_ReactivationWebhook_Idempotent` | PASS | |
| QC-15-24 | `LIFECYCLE_LEDGER_01_SeatPurchaseWebhook_WritesLedgerEntry_NoCreditTransaction` | PASS | |
| QC-15-25 | `LIFECYCLE_LEDGER_02_CycleEndCancel_WritesLedgerEntry_NoCreditTransaction` | PASS | |
| QC-15-26 | `LIFECYCLE_RENEWAL_01_VoluntaryCancelMarker_AppliedAtRenewal_LocksOverLimitChild` | PASS | |

## Gap cases

| Gap | Priority | Action | Result | Notes |
|-----|----------|--------|--------|-------|
| GAP-15-A (grace expiry → enforcement end-to-end) | P0 | ADDED — `GAP15A_GraceExpiry_Enforcement_LocksOverLimitChildren` | PASS | Per OQ-A: seeds `GraceEndsAt = UtcNow - 1d` (expired) + `PlanCode=Free` (paidActiveSeats=1) with 3 active children. Calls `ISeatEnforcementService.EnforceAsync` directly. Asserts at least 1 child locked (NoSeatLocked) + SeatLedgerEntry{Locked} written + no energy forfeit. |
| GAP-15-B (payment.succeeded within grace clears it) | P1 | ADDED — `GAP15B_PaymentSucceededWithinGrace_ClearsGrace` | PASS (FINDING-15-B FIXED) | Test now asserts the CORRECTED behavior: after `payment.succeeded`, the subscription is Active AND `GraceEndsAt`/`SeatGraceReason`/`SeatGraceStartedAt` are cleared. FINDING-15-B fixed in `WebhookEventService.HandlePaymentSucceededAsync` (see below). |
| GAP-15-C (locked child Learning/XP explicit no-touch) | P1 | SKIPPED — LIFECYCLE_ENFORCE_02 already asserts child identity not deleted and energy not reclaimed. Cross-module (Learning/Gamification) records are outside the Billing module boundary and can only be verified by security-audit or full cross-module integration test. | SKIP — covered by security-audit boundary | Module isolation rule: Billing tests cannot reach Learning/Gamification DbContexts. ENFORCE_02 asserts the Billing-visible no-touch invariants. Full cross-module coverage = security-auditor gate. |
| GAP-15-D (reactivate with no seat / no payment) | P2 | SKIPPED — P2 priority; deferred per phase-coverage-report.md. | SKIP — P2, deferred | |

## Summary
**26 / 26 existing tests PASS. 2 gaps added (GAP-15-A: PASS; GAP-15-B: PASS with finding). 1 gap skipped (module boundary). 1 gap deferred (P2).**

## Defects / Findings found

### FINDING-15-B (Severity: Medium — Product Gap) — ✅ FIXED 2026-06-22
**Description:** When `payment.succeeded` fired for a Subscription-kind payment while a grace window was open (`GraceEndsAt != null`), the webhook handler did NOT clear `GraceEndsAt`/`SeatGraceReason`/`SeatGraceStartedAt`. The subscription went Active but the grace window stayed open (stale audit data).

**Impact:** Low practical risk (enforcement gates check subscription status, and Active = no enforcement) but it is an audit/monitoring data-integrity issue — `GraceEndsAt` showed a future date after the payment was resolved, which could mislead tooling that reads it.

**Fix (applied):** `WebhookEventService.HandlePaymentSucceededAsync` now clears `GraceEndsAt = null`, `SeatGraceReason = null`, `SeatGraceStartedAt = null` when it activates the subscription. Grace is ONLY ever payment-failure (the `SeatGraceReason` enum has a single value), so a successful payment unconditionally resolves it. `GAP15B_PaymentSucceededWithinGrace_ClearsGrace` was flipped from assert-as-built to a regression sentinel asserting grace is cleared. Verified green (`P10_QC_Gaps_Tests` + `P10_W3` + `P10_15` 58/58, no regression).

**As-built assertion:** The test verifies subscription `Status = Active` after the successful payment. The grace window state is documented but not asserted as a hard failure (the product still functions correctly because the subscription IS active — the enforcement gate uses subscription status, not just grace date). The test will need updating when the fix is applied.
