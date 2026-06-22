# P10-15 — Seat enforcement, grace period & NoSeat/Locked lifecycle — Backend QC test cases

**Story:** `user-stories/Phase-10-Payments-Billing/P10-15-seat-enforcement-grace-period-noseat-locked-lifecycle.md`
**Task:** `tasks/Backend/Phase-10-Payments-Billing/P10-15-BE.md`
**Surface under test:** `SeatState{Active,NoSeatLocked}`, payment-failure grace (`Subscription.SeatGrace*`, `ISeatGraceService`), voluntary-removal scheduling (`ISeatSchedulingService`), enforcement job/service (`ISeatEnforcementService`), seat-based spend gate (`ISeatStateQuery`), parent CQRS (`GetSeatStatus`, `ChooseActiveChildren`, `ReactivateChildSeat`), reactivation webhook branch, reservation idempotency/concurrency hardening (BE-10), and the dedicated `SeatLedgerEntry` audit ledger (BE-11). Routes `GET/POST /api/Billing/Seats/Status|ChooseActive|Reactivate`.
**Existing suite:** `P10_15_SeatLifecycle_IntegrationTests` (26 tests). **Already QC'd by integration tests** — this doc traces ACs to existing tests and flags gaps. Design-only.

## Money / correctness lenses applied
- **NO mid-cycle forfeit** — enforcement removes the allocation row for newly-locked children but never reclaims already-ledgered energy; purchased reserve never touched.
- **Never delete a child** — Billing-only writes; no cascade into Identity/Learning/Gamification.
- **Grace = payment-failure only** — voluntary cancel/downgrade schedule cycle-end removal, NOT a grace window; grace is idempotent (no stack/extend).
- **Enforcement idempotent** per family+cycle; reservation idempotent on key + concurrency-safe (no two children sharing a seat).
- **Reactivation = prorated money + zero energy mint**; webhook idempotent on `ProviderEventId`.
- **Seat events → `SeatLedgerEntry`** (separate from energy `CreditTransaction`).
- **IDOR / authz** — `ChooseActive` family-scoped; over-limit selection → 422.

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to AC | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|--------------|---------------|
| QC-15-01 | Seat status returns SeatState + paidActiveSeats + grace | functional | P1 | parent + children | `GET /Seats/Status` | per-child `SeatState`, `paidActiveSeats`, grace fields, `removalScheduledAt` | View seat state | `LIFECYCLE_STATUS_01_SeatStatus_ReturnsSeatStateAndGraceFields` |
| QC-15-02 | Seat status authz — anon 401, child 403 | auth-authz | P0 | child JWT available | anon + child call status | anon→401; child→403 | Parent-gated | `LIFECYCLE_STATUS_02_SeatStatus_AnonymousAndChildReturnsUnauthorized` |
| QC-15-03 | Seat status family-scoped (no IDOR) | auth-authz | P0 | Parent A + B | B reads status | only B's children; no A leakage | Family-scoped | `LIFECYCLE_STATUS_03_SeatStatus_FamilyScoped_NoIDOR` |
| QC-15-04 | ChooseActive within limit → chosen Active, rest Locked | functional | P0 | more children than seats | choose ≤ limit | chosen `Active`, others `NoSeatLocked`; allocation re-joined for chosen | Parent chooses active | `LIFECYCLE_CHOOSE_01_ChooseActive_WithinLimit_AppliesStates` |
| QC-15-05 | ChooseActive exceeds paid seats → 422 | validation | P0 | children > seats | choose > limit | 422; no state change | Selection ≤ limit | `LIFECYCLE_CHOOSE_02_ChooseActive_ExceedsLimit_Returns422` |
| QC-15-06 | ChooseActive IDOR — foreign child rejected | auth-authz | P0 | Parent A child in B's request | B chooses A's child | Forbidden; no mutation | Family-scoped authz | `LIFECYCLE_CHOOSE_03_ChooseActive_IDOR_ForeignChild_Rejected` |
| QC-15-07 | ChooseActive ledgered to SeatLedgerEntry; no CreditTransaction | persistence | P1 | choose applied | inspect ledgers | `SeatLedgerEntry{ChoiceApplied/Locked}` written; no `CreditTransaction` | Ledger every change | `LIFECYCLE_CHOOSE_04_ChooseActive_WritesLedgerEntries` |
| QC-15-08 | Enforcement locks over-limit; earliest-reserved kept | functional | P0 | over-limit, no parent choice | `EnforceAsync` | over-limit → `NoSeatLocked`; earliest-reserved kept (deterministic) | Enforcement default tie-break | `LIFECYCLE_ENFORCE_01_Enforce_LocksOverLimitChildren_EarliestReservedKept` |
| QC-15-09 | Enforcement: no energy reclaimed, child not deleted | functional | P0 | locked child had allocation | `EnforceAsync` | no energy clawback; child Identity/progress intact | No forfeit; never delete | `LIFECYCLE_ENFORCE_02_Enforce_NoEnergyReclaim_NoChildDeletion` |
| QC-15-10 | Enforcement writes SeatLedgerEntry{Locked} per child | persistence | P1 | over-limit | `EnforceAsync` | one `SeatLedgerEntry{Locked}` per locked child | Ledger lock transitions | `LIFECYCLE_ENFORCE_03_Enforce_WritesLedgerEntryPerLockedChild` |
| QC-15-11 | Enforcement idempotent — 2nd run no new locks/ledger | persistence | P0 | enforced once | `EnforceAsync` again | no new locks; no duplicate ledger | Idempotent per family+cycle | `LIFECYCLE_ENFORCE_04_Enforce_Idempotent_NoDoubleAction` |
| QC-15-12 | Enforcement idempotency key backstop | persistence | P1 | enforced once | re-run (key backstop) | no duplicate ledger via idempotency key | Idempotent enforcement | `LIFECYCLE_ENFORCE_04b_Enforce_Idempotent_KeyBackstop_NoDuplicateLedger` |
| QC-15-13 | Payment-failure grace: reason + EndsAt ≈ now+7d | functional | P0 | active subscription | start payment-failure grace | `SeatGraceReason=PaymentFailure`; `GraceEndsAt ≈ now + 7d` | 7-day grace window | `LIFECYCLE_GRACE_01_PaymentFailureGrace_SetsReasonAndEndsAt` |
| QC-15-14 | Grace idempotent within open window | negative | P0 | grace open | re-trigger grace | no stack/extend/shorten; same deadline | Grace idempotent | `LIFECYCLE_GRACE_02_PaymentFailureGrace_Idempotent_OpenWindow` |
| QC-15-15 | Voluntary cancel does NOT start grace | negative | P0 | active subscription | voluntary cancel | `GraceEndsAt` stays null | Grace = payment-failure only | `LIFECYCLE_GRACE_03_VoluntaryCancel_NoGrace` |
| QC-15-16 | NoSeatLocked child: spend denied, balance untouched | functional | P0 | locked child with allocation | `TryDebitAsync` | denied (SeatLocked); no balance/allocation/purchased touched | Spend gate on locked | `LIFECYCLE_SPEND_01_NoSeatLocked_SpendDenied_SeatLocked` |
| QC-15-17 | Active child: seat-state allows spend | functional | P1 | active child | `ISeatStateQuery`=true, spend | spend succeeds normally | Gate passes when active | `LIFECYCLE_SPEND_02_Active_Child_SpendSucceeds` |
| QC-15-18 | Reservation idempotent — same key → exactly one row | persistence | P0 | parent + key | reserve twice (same key) | exactly one `SeatReservation` | Reservation idempotent (BE-10) | `LIFECYCLE_IDEMPOTENCY_01_ReserveSeat_SameKey_IdempotentOneRow` |
| QC-15-19 | Concurrent add-child within limit → distinct rows | concurrency | P0 | 2 seats, 2 parallel adds | parallel AddChild | 2 distinct seat rows; no drift, no shared slot | Concurrency-safe (BE-10) | `CONCURRENT_RESERVE_01_TwoParallelAddChild_WithinLimit_TwoDistinctSeatRows` |
| QC-15-20 | Concurrent add-child for 1 seat → 1 active + 1 clean 409 | concurrency | P0 | 1 seat, 2 parallel adds | parallel AddChild | one succeeds, one 409; no orphan | Concurrency-safe (BE-10) | `CONCURRENT_RESERVE_02_TwoParallelAddChild_OneSeat_OneSucceeds_One409_NoOrphan` |
| QC-15-21 | Reactivate locked child → prorated checkout, no energy | functional | P0 | locked child | `POST /Seats/Reactivate` | returns redirectUrl + prorated amount; zero energy minted | Reactivation prorated | `LIFECYCLE_REACTIVATE_01_Reactivate_ReturnsCheckoutUrl_NoEnergy` |
| QC-15-22 | Reactivation webhook flips seat→Active; ledgers Reactivated | functional | P0 | locked child, seat-reactivation payment | `payment.succeeded` | seat `→ Active`; `SeatLedgerEntry{Reactivated}` with Amount | Activation on webhook | `LIFECYCLE_REACTIVATE_02_ReactivationWebhook_FlipsSeatToActive_WritesLedger` |
| QC-15-23 | Reactivation webhook idempotent on ProviderEventId | negative | P0 | reactivation payment | replay same eventId | idempotent no-op | Webhook idempotent | `LIFECYCLE_REACTIVATE_03_ReactivationWebhook_Idempotent` |
| QC-15-24 | P10-14 seat-purchase webhook writes SeatLedgerEntry{Purchased} | persistence | P1 | seat-purchase webhook | inspect ledgers | `SeatLedgerEntry{Purchased}`; no `CreditTransaction` | Seat events → SeatLedgerEntry | `LIFECYCLE_LEDGER_01_SeatPurchaseWebhook_WritesLedgerEntry_NoCreditTransaction` |
| QC-15-25 | Cycle-end cancel writes SeatLedgerEntry{CancelScheduled} | persistence | P1 | voluntary cancel | inspect ledgers | `SeatLedgerEntry{CancelScheduled}`; no `CreditTransaction` | Seat events → SeatLedgerEntry | `LIFECYCLE_LEDGER_02_CycleEndCancel_WritesLedgerEntry_NoCreditTransaction` |
| QC-15-26 | Voluntary cancel marker applied at renewal → over-limit locked | functional | P0 | cancel scheduled | run renewal enforcement | seat removed at renewal; over-limit child `NoSeatLocked` | Removal effective at cycle-end | `LIFECYCLE_RENEWAL_01_VoluntaryCancelMarker_AppliedAtRenewal_LocksOverLimitChild` |

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-15-A (grace expiry → enforcement, AC):** Enforcement is exercised directly (ENFORCE-01..04) and via the voluntary-cancel renewal path (RENEWAL-01), but there is **no test that drives the payment-failure grace path end-to-end to expiry → enforcement** (grace opens, deadline passes with no successful payment → over-limit children locked). **Add a P0** seeding `SeatGraceEndsAt` in the past and asserting enforcement fires on grace expiry. If the harness cannot advance the clock past `grace_days`, seed an already-expired window directly.
- **GAP-15-B (payment-success within grace cancels enforcement):** No test asserts that a successful renewal payment **within** the grace window clears the grace state and avoids any lock. **Add a P1.**
- **GAP-15-C (locked child keeps progress/XP/history — explicit cross-module no-touch):** ENFORCE-02 asserts the child is not deleted and energy not reclaimed; it does not explicitly assert Learning/Gamification/Identity records are untouched. This is the hard "never cascade" invariant the security-auditor must verify. **Add a P1** asserting (where seedable) XP/streak/history rows are unchanged after a lock, or mark as covered-by-security-audit if integration cannot reach those modules.
- **GAP-15-D (reactivate when no seat & no payment arrangeable):** Task BE-6 says reactivation "fails if no seats available and no payment can be arranged." No negative test. **Add a P2.**
- **Better suited to unit tests:** the deterministic tie-break selector (earliest-`ReservedAt` kept) — pinned at integration in ENFORCE-01; a unit test would harden ties (equal `ReservedAt`).
