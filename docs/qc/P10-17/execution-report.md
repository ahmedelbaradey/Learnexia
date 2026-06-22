# P10-17 — Execution report

**Status:** COMPLETE — run on 2026-06-23.
**Suite:** `P10_17_Refunds_IntegrationTests` (18 tests) — all PASS.
**Gap cases added:** `GAP17A_RefundSucceeded_SubscriptionKind_PurchasedBalanceUntouched`, `GAP17C_RefundRequest_InvalidRefundReason_Returns422` in `P10_QC_Gaps_Tests.cs`.

## How to run
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P10_17_Refunds_IntegrationTests" --configuration Release
```
Gap cases:
```
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~GAP17" --configuration Release
```

## Results per case

| QC case | Existing test | Result | Notes |
|---------|---------------|--------|-------|
| QC-17-01 | `TC_RF_01_Quote_PartialSpend_RefundableIsUnused` | PASS | |
| QC-17-02 | `TC_RF_02_Quote_FullyConsumed_RefundableIsZero` | PASS | |
| QC-17-03 | `TC_RF_03_Auth_Anon_Returns401` | PASS | |
| QC-17-04 | `TC_RF_03b_Auth_ChildJwt_Returns403` | PASS | |
| QC-17-05 | `TC_RF_04_Quote_IDOR_Returns404` | PASS | |
| QC-17-06 | `TC_RF_05_Request_And_Webhook_Settlement_HappyPath` | PASS | |
| QC-17-07 | `TC_RF_06_Webhook_Idempotency_NoDoubleDecrement` | PASS | |
| QC-17-08 | `TC_RF_07_BucketA_NotTouched_By_PackRefund` | PASS | |
| QC-17-09 | `TC_RF_08_MultiPack_FIFO_RefundableIsPaymentSpecific` | PASS | |
| QC-17-10 | `TC_RF_09_AdminPath_QuoteAndRequest_AnyFamily` | PASS | |
| QC-17-11 | `TC_RF_10_Admin_Route_ParentJwt_Returns403` | PASS | |
| QC-17-12 | `TC_RF_11_Request_WhenRefundableIsZero_Rejected` | PASS | |
| QC-17-13 | `TC_RF_12_WebhookReReconciles_ClampsToNeverNegative` | PASS | |
| QC-17-14 | `TC_RF_13_Envelope_Shape_QuoteSuccess` | PASS | |
| QC-17-15 | `TC_RF_S1_DistinctEventId_DoubleRefund_NoSecondDecrement` | PASS | |
| QC-17-16 | `TC_RF_S2_Repurchase_Then_DistinctEventId_NoDoubleRefund` | PASS | |
| QC-17-17 | `TC_RF_S3_AlreadyRefunded_Quote_Returns_Zero` | PASS | |
| QC-17-18 | `TC_RF_S4_IDOR_ExplicitOwnershipCheck_CrossFamilyQuoteRejected` | PASS | |

## Gap cases

| Gap | Priority | Action | Result | Notes |
|-----|----------|--------|--------|-------|
| GAP-17-A (refund.succeeded on Subscription/Seat kind — cross-kind isolation) | P1 | ADDED — `GAP17A_RefundSucceeded_SubscriptionKind_PurchasedBalanceUntouched` | PASS | Seeds PurchasedBalance=500, sends refund.succeeded for the Subscription payment, asserts PurchasedBalance remains 500 (subscription refund must NOT touch the purchased bucket). |
| GAP-17-B (concurrent negative-balance race) | P1, best-effort | SKIPPED — concurrent refund requests targeting the same payment simultaneously cannot be reliably triggered at the HTTP integration level. The DB-level guard (`clamp >= 0`) is tested by TC_RF_12. Deferred as a unit test on `ComputeRefundableAsync` / the decrement path. | SKIP — concurrency harness limitation | |
| GAP-17-C (RefundReason enum / invalid reason → 422) | P1 | ADDED — `GAP17C_RefundRequest_InvalidRefundReason_Returns422` | PASS | Posts `RefundReason=99` (out-of-range enum) to `/Refunds/Request`; expects 422 or 400 from FluentValidation. |

## Summary
**18 / 18 existing tests PASS. 2 gaps added (GAP-17-A, GAP-17-C: both PASS). 1 gap skipped (concurrency harness limit).**

## Defects found
None.
