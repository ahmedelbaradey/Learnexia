# Child seats & seat-reserved add-child

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 8 — seat model on plan/subscription + extra-seat purchase flow + cross-module seat reservation on add-child.
- **Labels:** `billing`, `subscription`, `seats`, `payments`, `backend`, `frontend`
- **Requirements:** FR-PAY-7 *(new — Phase 10)*

## Description
As a parent, I want each of my children to occupy a paid **seat** on my plan — included seats plus extra seats I can buy monthly — so that only the children I'm paying for can use the platform, and so that adding a child reserves a seat before the child is created. Only **paid, active seats** generate the monthly entitlement energy that funds my children (per the locked family-energy model); a child profile on its own mints no energy.

## Locked family-energy model — authoritative (final mid-cycle seat model, lead-approved 2026-06-17)
This story implements the **SEAT** half of the locked model; it does **not** re-decide energy buckets, allocation, redistribution, grace/enforcement, transfers, or refunds (those are P10-13/15/16/17). It must stay faithful to:

- **One family = one billing cycle. All seats renew together under the parent subscription.** There is no per-seat billing cycle.
- **Seats define entitlement.** A plan grants **INCLUDED seats** and allows **EXTRA PAID seats** (monthly add-on). **Only paid ACTIVE seats** generate subscription/entitlement energy (`PlanEnergyPerSeat × ActivePaidSeats`, granted by P10-13). A child profile alone mints **none**.
- **ADD/REACTIVATE a seat mid-cycle — PRORATE MONEY, DO NOT MINT ENERGY.** When a new or reactivated seat is added mid-cycle:
  - The parent is charged a **prorated amount** = `SeatPrice × remaining-cycle-ratio`. The child is **activated immediately**.
  - **NO subscription energy is minted mid-cycle** for that seat. The child's energy during the current cycle comes exclusively from the parent **allocating family-wallet credit via P10-16**.
  - At the next renewal the seat becomes a normal full-cycle seat; the renewal grant recalculates as `ActiveSeats × PlanEnergyPerSeat`.
  - **No duplicate-grant guard is needed** (no mid-cycle grant is issued).
  - **This REVERSES BOTH the earlier "no proration for MVP" AND the earlier "prorate energy" wording. MONEY prorates, ENERGY does NOT mint mid-cycle.**
- **REMOVE/CANCEL a seat mid-cycle (voluntary) — effective at CYCLE END.** When a parent cancels a seat:
  - The seat stays **Active until the next renewal**; the child keeps the remaining energy allowance.
  - **No prorated refund. No energy reclaim, forfeit, or conversion** mid-cycle — "once granted and ledgered, energy stays valid for that billing period."
  - At renewal the seat is removed and the grant recalculates with fewer active seats; over-seat children become **NoSeat/Locked** (never deleted; keep all history/XP/progress/achievements).
  - **This OVERRIDES any earlier "OQ-H forfeit unspent allowance on enforcement" — there is NO mid-cycle forfeit.**
- **7-day grace = payment-failure retry window at the renewal boundary only.** Grace is NOT a mid-cycle energy strip and does NOT reclaim or prorate energy mid-cycle. Seats stay active during the grace window; enforcement (NoSeat/Locked) triggers only on grace expiry with no successful renewal payment. All "grace" wording must be reconciled to this definition — voluntary seat cancels do NOT trigger a grace period.
- **Purchased packs unaffected.** Pack energy is family-owned, never expires, never prorated, and not seat-linked.
- **Module ownership (hard).** **Billing** owns seats (the seat model on plan/subscription, the active-seat count, the extra-seat purchase). **Identity** owns child profiles. **Parent**'s add-child flow must **reserve a seat first** via a new `Shared.Contracts/Billing` seam, then create the child — never the reverse.

## Acceptance Criteria

### Seat model (Billing-owned)
- A **plan defines its INCLUDED seat count** (config-driven, per plan tier — open number, lead flag) and a **max-seats** ceiling (config). The subscription tracks **purchased extra seats** on top of the included count.
- **Active seat count** = `IncludedSeats + PurchasedExtraSeats` while the subscription is in a billing-active state (including grace). Only these seats are "active". → drives P10-13's `PlanEnergyPerSeat × ActivePaidSeats` grant.
- A parent can **view their seat status**: total seats, occupied seats (children currently assigned a seat), free seats, and which children hold active seats vs NoSeat/Locked.
- **Free/included-only plans** still expose a seat count (their included seats); buying extra seats requires a paid plan (lead-confirmed gating with P10-05).

### Extra-seat purchase — mid-cycle money proration (provider webhook)
- A parent can **buy N extra seats** as a **monthly add-on** at the config-driven **extra-seat monthly price** (open number, lead flag). Purchase goes through the **payment provider** (P10-06 seam) — **web checkout only, no native IAP**.
- When a seat is added **mid-cycle**, the charge is **`SeatPrice × remaining-cycle-ratio`** (prorated money), computed server-side from the billing cycle dates — never client-supplied. The child is **activated immediately** upon successful payment.
- A seat purchase is confirmed **only via the verified provider webhook** (signature-verified, idempotent on provider event id) — never client-trusted. On `payment.succeeded` the subscription's `PurchasedExtraSeats` increases and the change is **ledgered** (immutable per the model); on failure no seats are added (P10-09).
- **Mid-cycle seat add never mints subscription energy.** Buying extra seats never creates purchased energy and never grants energy directly. The newly-activated seat's child receives **no mid-cycle energy grant** — the parent must allocate family-wallet credit to that child via P10-16. At the next renewal the seat is included in the normal `ActiveSeats × PlanEnergyPerSeat` grant. **No duplicate-grant guard is needed** (no grant is issued at seat-add time).
- A parent can **cancel an extra seat** (monthly add-on): cancellation is **effective at CYCLE END** — the seat stays Active until the next renewal; the child keeps the remaining allocation and can continue spending for the rest of the cycle. **No prorated refund. No energy reclaim, forfeit, or conversion mid-cycle.** At renewal the seat is removed, the grant recalculates with fewer seats, and over-seat children move to NoSeat/Locked (enforcement deferred to P10-15). **Voluntary seat cancels do NOT trigger the P10-15 grace period — grace applies to payment failures only.**

### Seat reactivation mid-cycle
- Reactivating a previously-cancelled (but still-Active-until-cycle-end) seat, or re-assigning a NoSeat/Locked child to a newly-purchased seat mid-cycle, follows the same rule: **prorate MONEY, no energy mint**. Mid-cycle energy for that child comes from P10-16 wallet allocation only.

### Seat-reserved add-child (cross-module seam)
- The Parent **AddChildCommandHandler** (`POST /api/Parent/Add-Child`) **reserves/checks a seat FIRST** via a new `Shared.Contracts/Billing` seam (e.g. `ISubscriptionSeatContract`) **before** creating the child account.
- If a **free seat is available**, the seam reserves it and the child is created, bound to that seat, and **activated immediately**.
- If **no seat is available**, add-child is **rejected cleanly** with a localized "no free seat — buy an extra seat or free one up" error; **no child account is created** and no orphaned reservation remains.
- Seat reservation + child creation must not leave the system inconsistent on failure (if child creation fails after a seat reservation, the reservation is released / compensated; if seat reservation fails, no child is created). Reservation is **idempotent** under retry.
- The seam is **cross-module via `Shared.Contracts` only** — Parent never references any `Billing.*` project; no cross-module FK to the child (loose `int` id).

## Out of scope (covered by siblings — do not implement here)
- **P10-13** — Family energy account + two buckets + per-seat entitlement grant (`PlanEnergyPerSeat × ActivePaidSeats`) + per-child allowance allocation (equal split) + spend order. This story only sets the **active-seat count** P10-13 reads.
- **P10-15** — Grace-period enforcement + NoSeat/Locked transitions + parent choosing which children keep active seats.
- **P10-16** — Per-child allowance redistribution (parent moves unspent family-wallet allocation, including to a newly-seated child mid-cycle) + sibling→sibling transfers. P10-16 is the **only** source of mid-cycle energy for a newly-added or reactivated seat — this story does not mint energy.
- **P10-17** — Purchased-energy refunds (refunds apply only to purchased energy, never seats/subscription grants).
- The **CreditAccount → FamilyEnergyAccount data migration** (ownership change from per-child to parent/family) is owned by **P10-13**; this story consumes the seat model, not the energy buckets.

## Backend vs Frontend scope
- **Backend (P10-14-BE):** seat model on plan/subscription (included-seat config per plan + max-seats + `PurchasedExtraSeats` on the subscription); active-seat-count resolution; extra-seat checkout command with **server-side money proration** (`SeatPrice × remaining-cycle-ratio`) + provider-webhook branch (idempotent, ledgered, **no energy mint**, immediate activation) + cancel-extra-seat command (cycle-end effective, no energy reclaim); the new `Shared.Contracts/Billing.ISubscriptionSeatContract` seam (reserve / release / check / count) consumed by Parent's add-child; seat-status query. **Security-sensitive — money + child data — `security-auditor` gate.**
- **Frontend (P10-14-FE):** parent-facing seat-management surface — view seats (total / occupied / free), see **which children are active**, **buy** extra seats (→ checkout showing the prorated charge for mid-cycle adds), **cancel** an extra seat (with **cycle-end** copy — not instant-forfeit or grace-period copy), and the **no-free-seat** error state in the add-child flow. Parent area only — never on a child route.

## Dependencies
- Blocked by **P10-05** (Plan + Subscription entities — seats hang off these) and **P10-06** (payment provider seam + webhook + `WebhookEvent` idempotency).
- Builds on **P10-01** (immutable ledger — seat purchases/cancels are ledgered) and the **P10-07** pack-checkout webhook pattern (extra-seat checkout mirrors it).
- Reads **P10-12** global settings for the config-driven numbers (included-seat counts per plan, max seats, extra-seat monthly price).
- **Feeds P10-13** (active-seat count → per-seat entitlement grant). Coordinate the seam shape with P10-13.
- **Requires P10-16** for mid-cycle energy provision to newly-seated children. This story mints no energy — P10-16 wallet allocation is the only mid-cycle energy path.
- Touches the **Parent** add-child flow (`AddChildCommandHandler`) — coordinate the seam consumption with the Parent module owner.

## Notes
- **Config-driven, lead to fix the numbers:** included-seat count **per plan tier**, **max seats**, and **extra-seat monthly price** are open numbers — surface as P10-12 keys, never hard-coded. A **feature flag** to enable/disable extra-seat purchase is a lead call.
- **No teacher role** (CLAUDE.md) — seats are for **children only**.
- **Provider-verified only:** seat purchases confirm via the signed provider webhook (P10-06), never on the client return URL.
- **Parent-gated:** all seat surfaces are parent-account only; children never see or buy seats.
- **Mid-cycle add = MONEY prorates, ENERGY does NOT mint (lead-approved 2026-06-17).** Webhook branch for seat purchase must compute and charge `SeatPrice × remaining-cycle-ratio` and activate the seat immediately; it must NOT mint or deposit subscription energy. A newly-seated child needs the parent to allocate family-wallet credit via P10-16.
- **No duplicate-grant guard needed.** Because no grant is issued at seat-add time, there is no risk of double-granting; remove any earlier "duplicate grant guard" language from implementation plans.
- **Mid-cycle cancel = effective at CYCLE END only (lead-approved 2026-06-17).** Voluntary cancels do not trigger a P10-15 grace period mid-cycle; they simply mark the seat for removal at the next renewal with no energy reclaim.
- **7-day grace = payment-failure at renewal boundary only.** Do not conflate grace with voluntary mid-cycle seat cancels. Reconcile any "cancel starts grace" or "grace strips energy mid-cycle" wording in P10-15 and elsewhere to this definition.
- **Config-driven numbers (included-seat count per plan tier, max seats, extra-seat monthly price)** are open numbers — surface as P10-12 keys; never hard-code. Placeholder: Free=1 included seat, Premium=3 included seats, max=5, extra-seat price=169 EGP/mo — all subject to lead confirmation.
- This story implements seats exactly as locked (2026-06-17 final). Energy buckets/allocation/redistribution remain P10-13/16.
