# P10-17 — Refund reconciliation (unused purchased energy) — Backend QC test cases

**Story:** `user-stories/Phase-10-Payments-Billing/P10-17-refund-reconciliation-unused-purchased-energy.md`
**Task:** `tasks/Backend/Phase-10-Payments-Billing/P10-17-BE.md`
**Surface under test:** `IRefundService.ComputeRefundableAsync` (bucket-B-only ledger reconciliation, clamp ≥ 0, FIFO per-`Purchase`-payment), `RequestPurchasedEnergyRefundCommand` (parent, initiate-only), the `refund.succeeded` webhook branch of `WebhookEventService.HandleRefundSucceededAsync` (idempotent `Refund` ledger row + shared `PurchasedBalance` decrement, never negative), the admin-side refund action (admin authz, any family, actor ledgered), and routes `GET /Refunds/Quote/{id}`, `POST /Refunds/Request`, plus admin routes.
**Existing suite:** `P10_17_Refunds_IntegrationTests` (18 tests). **Already QC'd by integration tests** — this doc traces ACs to existing tests and flags gaps. Design-only.

## Money / correctness lenses applied
- **Refundable = purchased − consumed-purchased**, reconciled from the immutable ledger (bucket B only). Subscription/entitlement (bucket A) NEVER refundable.
- **Clamp ≥ 0**; never exceeds current shared purchased balance.
- **Idempotency** — webhook idempotent on provider refund event id AND on `LedgerEntry.IdempotencyKey`; distinct event ids for the same refund → no double-decrement.
- **Never negative** — re-reconcile at settlement; balance never goes below 0 even with interleaved further spend.
- **Webhook-driven settlement** — the in-app/admin request only initiates; ledger/balance change happens on `refund.succeeded`.
- **IDOR / authz** — parent route family-scoped; admin route admin-role only (parent JWT → 403).

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to AC | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|--------------|---------------|
| QC-17-01 | Quote: bought 10000, spent 3000 purchased → refundable 7000 | functional | P0 | pack 10000 purchased, 3000 spent from purchased | `GET /Refunds/Quote/{id}` | `refundable == 7000` | Refundable = purchased − consumed | `TC_RF_01_Quote_PartialSpend_RefundableIsUnused` |
| QC-17-02 | Quote: all purchased consumed → refundable 0 | boundary | P0 | pack fully consumed | Quote | `refundable == 0` (clamp) | Clamp ≥ 0; already-consumed never refundable | `TC_RF_02_Quote_FullyConsumed_RefundableIsZero` |
| QC-17-03 | Auth: Quote/Request anon → 401 | auth | P0 | none | anon Quote + Request | 401 both | Parent-gated | `TC_RF_03_Auth_Anon_Returns401` |
| QC-17-04 | Auth: child (Student) JWT on parent Quote → 403 | auth-authz | P0 | child JWT | child Quote | 403 | Children can't request | `TC_RF_03b_Auth_ChildJwt_Returns403` |
| QC-17-05 | IDOR: Parent B quoting Parent A payment → 404 | auth-authz | P0 | A's payment, B's JWT | B quotes A's payment | 404 (ownership) | Owning-parent only | `TC_RF_04_Quote_IDOR_Returns404` |
| QC-17-06 | Request + webhook settlement happy path | functional | P0 | refundable pack | `POST /Refunds/Request` → `refund.succeeded` | provider accepted; `Refund` ledger row; `PurchasedBalance` decremented by refunded | Webhook-driven settlement | `TC_RF_05_Request_And_Webhook_Settlement_HappyPath` |
| QC-17-07 | Idempotency: same refund.succeeded replay → no double-decrement | negative | P0 | refund settled once | replay same webhook | no double-decrement | Idempotent refund | `TC_RF_06_Webhook_Idempotency_NoDoubleDecrement` |
| QC-17-08 | Bucket-A safety: subscription balance untouched | functional | P0 | wallet w/ subscription + purchased | pack refund webhook | `SubscriptionBalance` unchanged | Bucket A never refundable | `TC_RF_07_BucketA_NotTouched_By_PackRefund` |
| QC-17-09 | Multi-pack FIFO — refundable is payment-specific | functional | P1 | 2 packs, partial spend, refund pack-1 | quote/refund pack-1 | FIFO-attributable refundable per payment | Per-`Purchase` FIFO (OQ-4) | `TC_RF_08_MultiPack_FIFO_RefundableIsPaymentSpecific` |
| QC-17-10 | Admin path: quote + request any family; ledger row written | functional | P0 | any family's pack | admin Quote + Request | succeeds for any family; `Refund` ledger row with admin actor | Admin can initiate | `TC_RF_09_AdminPath_QuoteAndRequest_AnyFamily` |
| QC-17-11 | Admin IDOR: Parent JWT on admin routes → 403 | auth-authz | P0 | parent JWT | parent calls admin Quote/Request | 403 | Admin-role only | `TC_RF_10_Admin_Route_ParentJwt_Returns403` |
| QC-17-12 | Zero-refundable: Request when fully consumed → 422 | negative | P0 | fully-consumed pack | `POST /Refunds/Request` | 422 rejected (refundable ≤ 0) | Reject zero-refundable | `TC_RF_11_Request_WhenRefundableIsZero_Rejected` |
| QC-17-13 | Negative clamp: further spend after request → webhook re-reconciles | negative | P0 | request at quote, then spend more | settle webhook | re-reconciled; balance never negative | Re-reconcile at settlement, never negative | `TC_RF_12_WebhookReReconciles_ClampsToNeverNegative` |
| QC-17-14 | Envelope: Quote success has all 5 BaseResponse keys | functional | P1 | refundable pack | Quote | `statusCode/successed/message/data/(meta)` present | Envelope shape | `TC_RF_13_Envelope_Shape_QuoteSuccess` |
| QC-17-15 | Distinct event ids for same refund → no second decrement | negative | P0 | refund settled (event A) | event B distinct id, same refund | no second decrement | Idempotent on logical refund | `TC_RF_S1_DistinctEventId_DoubleRefund_NoSecondDecrement` |
| QC-17-16 | Repurchase between refund-1 and distinct refund-2 → no double-refund | negative | P0 | refund-1 settled, new pack, distinct event id | settle event-2 | no double-refund despite new balance | Idempotent + ledger source of truth | `TC_RF_S2_Repurchase_Then_DistinctEventId_NoDoubleRefund` |
| QC-17-17 | Already-refunded: quote after full refund → refundable 0 | boundary | P1 | pack fully refunded | Quote | `refundable == 0` | Ledger source of truth | `TC_RF_S3_AlreadyRefunded_Quote_Returns_Zero` |
| QC-17-18 | IDOR ownership: B's wallet resolves null for A's payment | auth-authz | P0 | A's payment, B | B quotes A's payment | explicit ownership check → cross-family rejected | Owning-parent only | `TC_RF_S4_IDOR_ExplicitOwnershipCheck_CrossFamilyQuoteRejected` |

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-17-A (refund.succeeded for a NON-purchased/seat/subscription payment):** No test asserts the webhook branch **ignores or routes correctly** a `refund.succeeded` whose original `Payment.Kind` is `Subscription` or `Seat` (must not decrement the shared purchased balance; subscription refunds stay on the P10-09 policy path). **Add a P1** to prove cross-kind isolation.
- **GAP-17-B (concurrency / negative-balance race at settlement):** Task BE-4 names the negative-balance race as the primary risk. TC-RF-12 covers the further-spend-then-settle sequential case, but not **two concurrent decrements** (refund settlement racing a purchased spend). **Add a P1** concurrency case (best-effort; mark blocked if not reproducible).
- **GAP-17-C (request validator — reason enum / bounds):** No test asserts the `RefundReason` is an enum (not free text) and an invalid/absent reason → 422. **Add a P1** validation case.
- **Better suited to unit tests:** the FIFO consumption-attribution math (`ComputeRefundableAsync`) — bought 10000/used 3000 → 7000; fully spent → 0; subscription rows excluded; multi-pack FIFO split. BE-2 explicitly calls for unit tests of this math. Keep the integration happy-paths (QC-17-01/02/09) and add focused unit tests on the reconciliation function.
