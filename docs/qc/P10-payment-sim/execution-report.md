# P10 Payment-sim — Execution report

**Status:** COMPLETE — run on 2026-06-23.
**Suite:** `P10_PaymentSimulation_Tests` (14 tests) — all PASS.
**Gap cases added:** `GAP_SIM_A_ProviderNotFake_Returns404_EvenWithAllowSimulationAndAdminJwt` in `P10_QC_Gaps_Tests.cs`.

## How to run
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P10_PaymentSimulation_Tests" --configuration Release
```
(Enabled-gate tests use a derived factory with `Billing:PaymentProvider:AllowSimulation=true` + `Provider=Fake`; the gate-off test uses the default factory.)

Gap case:
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~GAP_SIM_A" --configuration Release
```

## Results per case

| QC case | Existing test | Result | Notes |
|---------|---------------|--------|-------|
| QC-SIM-01 | `TC_SIM_01_HappyPath_PaymentSucceeded_ActivatesSubscription` | PASS | |
| QC-SIM-02 | `TC_SIM_02_Idempotency_ReplaySameSimulate_SecondIsDuplicate_NoDoubleGrant` | PASS | |
| QC-SIM-03 | `TC_SIM_03_PaymentFailed_SimulatesFailedState` | PASS | |
| QC-SIM-04 | `TC_SIM_04_RefundSucceeded_FiresRealWebhookPath` | PASS | |
| QC-SIM-05 | `TC_SIM_05_GateOff_AllowSimulationFalse_Returns404` | PASS | |
| QC-SIM-06 | `TC_SIM_06a_Auth_Anonymous_Returns401` | PASS | |
| QC-SIM-07 | `TC_SIM_06b_Auth_ParentJwt_Returns403` | PASS | |
| QC-SIM-08 | `TC_SIM_07a_Validation_PaymentIdZero_Returns422` | PASS | |
| QC-SIM-09 | `TC_SIM_07a_Validation_PaymentIdNegative_Returns422` | PASS | |
| QC-SIM-10 | `TC_SIM_07b_Validation_UnknownEventType_Returns422` | PASS | |
| QC-SIM-11 | `TC_SIM_07b_Validation_EmptyEventType_Returns422` | PASS | |
| QC-SIM-12 | `TC_SIM_07c_UnknownPaymentId_Returns404` | PASS | |
| QC-SIM-13 | `TC_SIM_08_Envelope_HasAllBaseResponseKeys` | PASS | |
| QC-SIM-14 | `TC_SIM_09_DeterministicEventId_DistinctEventTypes_DistinctIds` | PASS | |

## Gap cases

| Gap | Priority | Action | Result | Notes |
|-----|----------|--------|--------|-------|
| GAP-SIM-A (third gate leg: Provider != Fake → 404) | P0 | ADDED — `GAP_SIM_A_ProviderNotFake_Returns404_EvenWithAllowSimulationAndAdminJwt` | PASS | Creates a derived factory with `Provider=Paymob` + `AllowSimulation=true`. Calls POST /Simulate with admin JWT. Asserts 404 (existence not disclosed). Production safety: a real payment provider can never be simulated. |
| GAP-SIM-B (seat-kind simulate) | P2 | DOCUMENTED BOUNDARY — per OQ-D resolution: the simulate endpoint is designed for subscription succeed/fail + refund only. Seat-kind payments are webhook-tested directly in P10-14 with signed webhooks (`payment.succeeded`/`payment.failed` on `PaymentKind.Seat`). The simulate endpoint is not intended to cover the seat branch. | SKIP — documented sim limitation, not a gap to close | |

## Summary
**14 / 14 existing tests PASS. 1 gap added (GAP-SIM-A: PASS). 1 gap documented as sim boundary (OQ-D).**

## Defects found
None.
