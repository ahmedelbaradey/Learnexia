# P10-14 — Child seats & seat-reserved add-child — Backend QC test cases

**Story:** `user-stories/Phase-10-Payments-Billing/P10-14-child-seats-and-seat-reserved-add-child.md`
**Task:** `tasks/Backend/Phase-10-Payments-Billing/P10-14-BE.md`
**Surface under test:** Billing seat model (`Plan.IncludedSeats`, `Subscription.PurchasedExtraSeats`, `SeatReservation`), `ISeatService`, `Shared.Contracts/Billing.ISubscriptionSeatContract` consumed by Parent `AddChildCommandHandler`, extra-seat checkout (server-side money proration), the seat webhook branch of `WebhookEventService.HandlePaymentSucceededAsync` (idempotent, ledgered, **no energy mint**), cancel-extra-seat (cycle-end), and `GET/POST /api/Billing/Seats/*`.
**Existing suite:** `P10_14_ChildSeats_IntegrationTests` (22 tests). **Already QC'd by integration tests** — this doc traces ACs to existing tests and flags gaps. Design-only.

## Money / correctness lenses applied
- **MONEY prorates, ENERGY does NOT mint mid-cycle** — seat webhook never mints/credits energy (locked 2026-06-17).
- **Server-side amount** — proration computed from cycle dates, never client-supplied.
- **Idempotency** — seat webhook idempotent on `ProviderEventId` AND on the per-payment guard (distinct event ids, same payment → increment once).
- **Atomicity / compensation** — reserve-before-create; failed inner create releases the seat; no orphan reservation, no orphan Identity row.
- **Ceiling** — total seats never exceed `seats.max=5`.
- **IDOR / authz** — seat surfaces parent-JWT-only; child never sees/buys seats.

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to AC | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|--------------|---------------|
| QC-14-01 | Free plan includes exactly 1 seat | functional | P1 | seeded Plans | inspect `Plan{Free}` | `IncludedSeats == 1` | Seat-model config | `SEAT_STATUS_01_FreePlan_HasOneIncludedSeat` |
| QC-14-02 | Premium plan includes exactly 3 seats | functional | P1 | seeded Plans | inspect `Plan{Premium}` | `IncludedSeats == 3` | Seat-model config | `SEAT_STATUS_02_PremiumPlan_HasThreeIncludedSeats` |
| QC-14-03 | Max-seats ceiling = 5 | boundary | P1 | global settings | read `seats.max` | `== 5` | Max-seats ceiling | `SEAT_STATUS_03_SeatsMax_IsHardCeiling_Five` |
| QC-14-04 | Seat status requires JWT | auth | P0 | none | `GET /Seats/Status` anon | 401 | parent-gated view | `SEAT_STATUS_04_SeatStatus_AnonymousRequest_Returns401` |
| QC-14-05 | Seat status envelope + fields | functional | P1 | parent JWT | `GET /Seats/Status` | 200; `successed`; data `includedSeats/totalSeats/availableSeats/maxSeats(=5)/children[]` | View seat status | `SEAT_STATUS_05_SeatStatus_Returns200_WithEnvelopeShape` |
| QC-14-06 | Add-child reserves a seat and succeeds within limit | functional | P0 | Free parent (1 seat) | add 1st child | 200/201; `SeatReservation` exists for child | Seat-reserved add-child | `ADD_CHILD_01_AddChild_ReservesSeat_SucceedsWithinLimit` |
| QC-14-07 | Seat becomes Active after successful add-child | persistence | P1 | Free parent | add child | `SeatReservation.Status == Active` (not Reserved) | Activate immediately | `ADD_CHILD_02_AddChild_SeatStatus_IsActive_AfterSuccess` |
| QC-14-08 | Seat-exhausted blocks add-child | negative | P0 | Free parent, 1 seat filled | add 2nd child | 409/402/400; not success | No free seat → reject | `ADD_CHILD_03_AddChild_SeatFull_IsRejected` |
| QC-14-09 | Rejection creates no child identity | negative | P0 | Free parent, full | rejected add, then sign in | sign-in fails — no Identity row | No child created on reject | `ADD_CHILD_04_AddChild_SeatFull_NoChildIdentityCreated` |
| QC-14-10 | Rejection leaves no orphan reservation | negative | P0 | Free parent, full | rejected add | active/reserved count unchanged | No orphaned reservation | `ADD_CHILD_05_AddChild_SeatFull_NoOrphanReservation` |
| QC-14-11 | Compensation — failed create releases seat | functional | P0 | Premium parent, 1 child added | re-add same email (DuplicateEmail) | add fails; occupied seat count unchanged; 0 Reserved rows | Compensation on failure | `ADD_CHILD_06_AddChild_FailedCreate_SeatReleased_NoOrphan` |
| QC-14-12 | Seat webhook increments PurchasedExtraSeats by 1 | functional | P0 | Premium parent, seeded Seat payment | `payment.succeeded` seat webhook | `PurchasedExtraSeats += 1` | Verified webhook adds seat | `WEBHOOK_SEAT_01_SeatWebhook_IncrementsPurchasedExtraSeats` |
| QC-14-13 | Seat webhook idempotent on replay (same eventId) | negative | P0 | seeded Seat payment | replay same `eventId` | no double increment; 200 no-op | Idempotent on event id | `WEBHOOK_SEAT_02_SeatWebhook_Idempotent_OnReplay` |
| QC-14-14 | Seat webhook mints NO energy | functional | P0 | Premium parent | seat webhook | `FamilyEnergyAccount.PurchasedBalance` unchanged | No energy mint mid-cycle | `WEBHOOK_SEAT_03_SeatWebhook_MintsNoEnergy` |
| QC-14-15 | Seat webhook respects max=5 ceiling | boundary | P0 | included 3 + extra 2 = 5 | one more seat webhook | `PurchasedExtraSeats` stays ≤ 2 (no over-max) | Max-seats ceiling | `WEBHOOK_SEAT_04_SeatWebhook_RespectsMaxCeiling` |
| QC-14-16 | Distinct event ids, same payment → increment once | negative | P0 | seeded Seat payment | 2 webhooks, distinct eventIds, same paymentRef | increment once (per-payment guard) | Provider-verified only, no double | `WEBHOOK_SEAT_05_DistinctEventIds_SamePayment_IncrementsOnce` |
| QC-14-17 | Mid-cycle checkout prorates money server-side | functional | P0 | Premium parent, ~20/30 days left | `POST /Seats/Checkout` qty 1 | response `amount` < full price ≈ `SeatPrice × ratio`; persisted on `Payment.Amount` | Prorated money, server-side | `SEAT_PRORATE_01_MidCycleCheckout_ProratesMoney` |
| QC-14-18 | Voluntary cancel = cycle-end marker, no grace | functional | P1 | Premium parent with extra seat | `POST /Seats/Cancel` | cycle-end removal marker set; no grace started; `PurchasedExtraSeats` not decremented mid-cycle | Cancel effective at cycle-end | `CANCEL_01_VoluntaryCancel_SchedulesCycleEndRemoval_NoGrace_NoMidCycleDecrement` |
| QC-14-19 | Cancel more than purchased → 409, no change | negative | P1 | parent with N extra seats | cancel > N | 409; no state change | Cancel validation | `CANCEL_02_CancelMoreThanPurchased_Returns409` |
| QC-14-20 | Grant job: grant = PlanEnergyPerSeat × active paid seats | functional | P0 | real `ISeatService` wired | run grant job | grant = perSeat × active paid seats | Active-seat count drives P10-13 | `GRANT_JOB_01_RealSeatService_DrivesGrant_PerActivePaidSeats` |
| QC-14-21 | ActivePaidSeats = Included + PurchasedExtra (capped) | functional | P1 | premium + extra seats | query seat count | `ActivePaidSeats = Included + PurchasedExtra` capped at max | Active seat count formula | `GRANT_JOB_02_RealSeatQuery_ActivePaidSeats_Formula` |
| QC-14-22 | ActiveChildIds only includes Active-status reservations | functional | P1 | mix of Active/other statuses | query seat info | `ActiveChildIds` contains only Active children | Active-seat children for allocation | `GRANT_JOB_03_RealSeatQuery_ActiveChildIds_OnlyActiveStatus` |

> Concurrency-on-reservation (two parallel add-child calls competing for one/more seats) is covered in the **P10-15** suite (`CONCURRENT_RESERVE_01/02`, `LIFECYCLE_IDEMPOTENCY_01`) because P10-15-BE-10 folds in the P10-14 reservation-idempotency hardening. See `docs/qc/P10-15/backend-test-cases.md` (QC-15-18..20).

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-14-A (free-plan extra-seat gating, AC):** "Buying extra seats requires a paid plan." No test asserts a **Free parent is rejected when attempting `POST /Seats/Checkout`**. **Add a P1 negative case** (Free parent → checkout rejected; localized message; no Payment created). Confirm with lead whether the gate is enabled (OQ-2 / feature flag).
- **GAP-14-B (checkout authz/IDOR):** No test asserts `POST /Seats/Checkout` and `POST /Seats/Cancel` reject **anon (401)** and **child JWT (403)**. SEAT-STATUS-04 covers only the status GET. **Add a P1** auth matrix for the two mutating routes.
- **GAP-14-C (checkout amount not client-supplied):** SEAT-PRORATE-01 proves server-side computation but does not assert that a **client-supplied amount is ignored**. **Add a P1** sending an inflated amount in the body → server uses its own computed prorated amount.
- **GAP-14-D (seat webhook on failure):** No test for `payment.failed` on a Seat payment → `Payment→Failed`, **no seat added**. **Add a P1.**
- **GAP-14-E (seat purchase ledgered):** AC says the seat purchase is ledgered. The seat-purchase ledger row is asserted in the **P10-15** suite (`LIFECYCLE_LEDGER_01` — `SeatLedgerEntry{Purchased}`, no `CreditTransaction`) because the dedicated `SeatLedgerEntry` ledger was introduced in P10-15-BE-11 and back-filled the P10-14 path. **Cross-referenced, not a gap** — note the dependency for traceability.
- **Better suited to unit tests:** the proration ratio math (`remaining-days / cycle-length`) at boundaries (0 days left, full cycle, 1 day) — keep the integration happy-path (QC-14-17) and harden boundaries with a unit test on the proration function.
