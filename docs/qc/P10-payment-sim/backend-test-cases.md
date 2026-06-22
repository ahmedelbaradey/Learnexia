# P10 Payment-provider mock simulation — Backend QC test cases

**Story / task:** `tasks/Backend/Phase-10-Payments-Billing/P10-06-BE.md` (payment provider seam + webhook; the gated dev/staging simulate endpoint).
**Surface under test:** `POST /api/Billing/Webhooks/Simulate` (`DevWebhookController`, `[Authorize(Policy=AdminOnly)]`), `PaymentSimulationService`, and the **triple gate**: `Billing:PaymentProvider:Provider == "Fake"` + `Billing:PaymentProvider:AllowSimulation == true` (default false) + AdminOnly JWT. The simulate path reuses the **real** webhook state machine (`WebhookEventService.HandlePaymentSucceededAsync` / `HandlePaymentFailedAsync` / `HandleRefundSucceededAsync`) with a **deterministic eventId** (`sim-{paymentId}-{eventType}`).
**Existing suite:** `P10_PaymentSimulation_Tests` (14 tests). **Already QC'd by integration tests** — this doc traces the simulate contract to existing tests and flags gaps. Design-only.

## Money / correctness lenses applied
- **Triple gate** — endpoint returns **404** when `AllowSimulation` is off (existence not disclosed); off by default in prod.
- **AdminOnly** — anon → 401; non-admin (parent) JWT → 403 even when the gate is on.
- **Idempotent replay** — deterministic eventId means replaying the same `(paymentId, eventType)` dedupes via the real `WebhookEvent.ProviderEventId` guard → no double-grant.
- **Reuses the real webhook state machine** — simulate is not a parallel code path; the same activation/grant/refund logic runs.
- **Validation** — `paymentId <= 0` → 422; unknown/empty `eventType` → 422; unknown `paymentId` → 404.

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|-----------|---------------|
| QC-SIM-01 | Happy path — payment.succeeded activates subscription | functional | P0 | gate on (Fake + AllowSimulation), admin JWT, initiated subscription payment | simulate `payment.succeeded` | 200; `Payment=Succeeded`; `Subscription Active + Premium` | Reuses real webhook | `TC_SIM_01_HappyPath_PaymentSucceeded_ActivatesSubscription` |
| QC-SIM-02 | Idempotent replay — second simulate is Duplicate, no double-grant | negative | P0 | succeeded once | simulate same `(paymentId, eventType)` again | 2nd returns "Duplicate"; grant not double-applied | Deterministic eventId dedupe | `TC_SIM_02_Idempotency_ReplaySameSimulate_SecondIsDuplicate_NoDoubleGrant` |
| QC-SIM-03 | payment.failed → Payment=Failed | functional | P0 | initiated payment | simulate `payment.failed` | 200; `Payment=Failed` (no activation) | Failed branch | `TC_SIM_03_PaymentFailed_SimulatesFailedState` |
| QC-SIM-04 | refund.succeeded fires the real refund webhook path | functional | P0 | a succeeded payment | simulate `refund.succeeded` | refund fires via real webhook (refund/clawback ledger) | Refund branch reuse | `TC_SIM_04_RefundSucceeded_FiresRealWebhookPath` |
| QC-SIM-05 | Gate off (AllowSimulation false/absent) → 404 | auth-authz | P0 | default factory (gate off), admin JWT | simulate any event | 404 (existence not disclosed) | Triple gate (AllowSimulation) | `TC_SIM_05_GateOff_AllowSimulationFalse_Returns404` |
| QC-SIM-06 | Auth — anonymous → 401 (even with gate on) | auth | P0 | gate on, no JWT | simulate | 401 | AdminOnly | `TC_SIM_06a_Auth_Anonymous_Returns401` |
| QC-SIM-07 | Auth — parent (non-admin) JWT → 403 | auth-authz | P0 | gate on, parent JWT | simulate | 403 Forbidden | AdminOnly | `TC_SIM_06b_Auth_ParentJwt_Returns403` |
| QC-SIM-08 | Validation — paymentId = 0 → 422 | validation | P1 | gate on, admin JWT | simulate paymentId 0 | 422 | Command validation | `TC_SIM_07a_Validation_PaymentIdZero_Returns422` |
| QC-SIM-09 | Validation — paymentId = -1 → 422 | validation | P1 | gate on, admin JWT | simulate paymentId -1 | 422 | Command validation | `TC_SIM_07a_Validation_PaymentIdNegative_Returns422` |
| QC-SIM-10 | Validation — unknown eventType → 422 | validation | P1 | gate on, admin JWT | simulate unsupported eventType | 422 | Command validation | `TC_SIM_07b_Validation_UnknownEventType_Returns422` |
| QC-SIM-11 | Validation — empty eventType → 422 | validation | P1 | gate on, admin JWT | simulate empty eventType | 422 | Command validation | `TC_SIM_07b_Validation_EmptyEventType_Returns422` |
| QC-SIM-12 | NotFound — unknown paymentId → 404 | negative | P1 | gate on, admin JWT | simulate non-existent paymentId | 404 | Payment lookup | `TC_SIM_07c_UnknownPaymentId_Returns404` |
| QC-SIM-13 | Envelope — Simulate success has all 5 BaseResponse keys | functional | P1 | gate on, admin JWT, succeeded | simulate | `statusCode/successed/message/data/(meta)` present | Envelope shape | `TC_SIM_08_Envelope_HasAllBaseResponseKeys` |
| QC-SIM-14 | Deterministic eventId — distinct eventTypes → distinct sim-{id}-{type} ids | functional | P1 | gate on, admin JWT, a payment | simulate two distinct eventTypes | distinct deterministic `sim-{id}-{type}` ids | Deterministic eventId | `TC_SIM_09_DeterministicEventId_DistinctEventTypes_DistinctIds` |

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-SIM-A (third gate leg — Provider != Fake):** Tests cover `AllowSimulation` off (404) and AdminOnly (401/403), but there is **no test asserting the endpoint 404s when `Provider != "Fake"`** even with `AllowSimulation=true` and an admin JWT. This is the third leg of the documented triple gate and is the most important production safety check (a real provider must never be simulable). **Add a P0** with a derived factory overriding `Provider` to a non-Fake value and `AllowSimulation=true` → expect 404.
- **GAP-SIM-B (seat simulate via deterministic eventId):** Simulate is exercised for subscription succeed/fail and refund, but not for a **Seat-kind** payment (`payment.succeeded` on a Seat payment → `PurchasedExtraSeats += 1`, no energy). The seat webhook is tested directly in P10-14 with signed webhooks; whether the simulate endpoint also drives the seat branch is unverified. **Add a P2** if seat simulate is in scope; otherwise note simulate is limited to subscription/refund and document the boundary.
- **Better suited to unit tests:** the deterministic-eventId formatter (`sim-{paymentId}-{eventType}`) — pinned at integration in QC-SIM-14; a unit test would harden format stability.
