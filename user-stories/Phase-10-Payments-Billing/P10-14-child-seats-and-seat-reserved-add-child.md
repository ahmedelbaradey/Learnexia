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

## Locked family-energy model — authoritative (approved 2026-06-16)
This story implements the **SEAT** half of the locked model; it does **not** re-decide energy buckets, allocation, redistribution, grace/enforcement, transfers, or refunds (those are P10-13/15/16/17). It must stay faithful to:
- **Seats define entitlement.** A plan grants **INCLUDED seats** and allows **EXTRA PAID seats** (monthly add-on). **Only paid ACTIVE seats** generate subscription/entitlement energy (`PlanEnergyPerSeat × ActivePaidSeats`, granted by P10-13). A child profile alone mints **none**.
- **No energy proration for seats in MVP.** Seat changes **never** create purchased energy. Canceling a seat stops **future** grants only.
- **Grace, not instant.** Seat reduction (downgrade / seat cancel / payment failure) starts a **GRACE PERIOD** — seats stay temporarily active, then on expiry limits are **enforced** (P10-15). Children are **never** deleted; over-limit children become **NoSeat/Locked** and keep all learning progress, XP, history, achievements.
- **Module ownership (hard).** **Billing** owns seats (the seat model on plan/subscription, the active-seat count, the extra-seat purchase). **Identity** owns child profiles. **Parent**'s add-child flow must **reserve a seat first** via a new `Shared.Contracts/Billing` seam, then create the child — never the reverse.

## Acceptance Criteria

### Seat model (Billing-owned)
- A **plan defines its INCLUDED seat count** (config-driven, per plan tier — open number, lead flag) and a **max-seats** ceiling (config). The subscription tracks **purchased extra seats** on top of the included count.
- **Active seat count** = `IncludedSeats + PurchasedExtraSeats` while the subscription is in a billing-active state (including grace). Only these seats are "active". → drives P10-13's `PlanEnergyPerSeat × ActivePaidSeats` grant.
- A parent can **view their seat status**: total seats, occupied seats (children currently assigned a seat), free seats, and which children hold active seats vs NoSeat/Locked.
- **Free/included-only plans** still expose a seat count (their included seats); buying extra seats requires a paid plan (lead-confirmed gating with P10-05).

### Extra-seat purchase (provider webhook)
- A parent can **buy N extra seats** as a **monthly add-on** at the config-driven **extra-seat monthly price** (open number, lead flag). Purchase goes through the **payment provider** (P10-06 seam) — **web checkout only, no native IAP**.
- A seat purchase is confirmed **only via the verified provider webhook** (signature-verified, idempotent on provider event id) — never client-trusted. On `payment.succeeded` the subscription's `PurchasedExtraSeats` increases and the change is **ledgered** (immutable per the model); on failure no seats are added (P10-09).
- Buying extra seats **never creates purchased energy** and **never grants energy directly** — it only raises the active-seat count, which the P10-13 grant job reads on the next cycle. **No proration** mid-cycle (MVP).
- A parent can **cancel an extra seat** (monthly add-on): cancellation stops **future** billing/grants for that seat and starts the **grace period** (enforcement deferred to P10-15); it never refunds subscription energy and never deletes a child.

### Seat-reserved add-child (cross-module seam)
- The Parent **AddChildCommandHandler** (`POST /api/Parent/Add-Child`) **reserves/checks a seat FIRST** via a new `Shared.Contracts/Billing` seam (e.g. `ISubscriptionSeatContract`) **before** creating the child account.
- If a **free seat is available**, the seam reserves it and the child is created and bound to that seat.
- If **no seat is available**, add-child is **rejected cleanly** with a localized "no free seat — buy an extra seat or free one up" error; **no child account is created** and no orphaned reservation remains.
- Seat reservation + child creation must not leave the system inconsistent on failure (if child creation fails after a seat reservation, the reservation is released / compensated; if seat reservation fails, no child is created). Reservation is **idempotent** under retry.
- The seam is **cross-module via `Shared.Contracts` only** — Parent never references any `Billing.*` project; no cross-module FK to the child (loose `int` id).

## Out of scope (covered by siblings — do not implement here)
- **P10-13** — Family energy account + two buckets + per-seat entitlement grant (`PlanEnergyPerSeat × ActivePaidSeats`) + per-child allowance allocation (equal split) + spend order. This story only sets the **active-seat count** P10-13 reads.
- **P10-15** — Grace-period enforcement + NoSeat/Locked transitions + parent choosing which children keep active seats.
- **P10-16** — Per-child allowance redistribution (parent moves unspent allocation) + sibling→sibling transfers.
- **P10-17** — Purchased-energy refunds (refunds apply only to purchased energy, never seats/subscription grants).
- The **CreditAccount → FamilyEnergyAccount data migration** (ownership change from per-child to parent/family) is owned by **P10-13**; this story consumes the seat model, not the energy buckets.

## Backend vs Frontend scope
- **Backend (P10-14-BE):** seat model on plan/subscription (included-seat config per plan + max-seats + `PurchasedExtraSeats` on the subscription); active-seat-count resolution; extra-seat checkout command + provider-webhook branch (idempotent, ledgered) + cancel-extra-seat command; the new `Shared.Contracts/Billing.ISubscriptionSeatContract` seam (reserve / release / check / count) consumed by Parent's add-child; seat-status query. **Security-sensitive — money + child data — `security-auditor` gate.**
- **Frontend (P10-14-FE):** parent-facing seat-management surface — view seats (total / occupied / free), see **which children are active**, **buy** extra seats (→ checkout), **cancel** an extra seat (with grace-period copy), and the **no-free-seat** error state in the add-child flow. Parent area only — never on a child route.

## Dependencies
- Blocked by **P10-05** (Plan + Subscription entities — seats hang off these) and **P10-06** (payment provider seam + webhook + `WebhookEvent` idempotency).
- Builds on **P10-01** (immutable ledger — seat purchases/cancels are ledgered) and the **P10-07** pack-checkout webhook pattern (extra-seat checkout mirrors it).
- Reads **P10-12** global settings for the config-driven numbers (included-seat counts per plan, max seats, extra-seat monthly price).
- **Feeds P10-13** (active-seat count → per-seat entitlement grant). Coordinate the seam shape with P10-13.
- Touches the **Parent** add-child flow (`AddChildCommandHandler`) — coordinate the seam consumption with the Parent module owner.

## Notes
- **Config-driven, lead to fix the numbers:** included-seat count **per plan tier**, **max seats**, and **extra-seat monthly price** are open numbers — surface as P10-12 keys, never hard-coded. A **feature flag** to enable/disable extra-seat purchase is a lead call.
- **No teacher role** (CLAUDE.md) — seats are for **children only**.
- **Provider-verified only:** seat purchases confirm via the signed provider webhook (P10-06), never on the client return URL.
- **Parent-gated:** all seat surfaces are parent-account only; children never see or buy seats.
- This story **supersedes nothing in the locked model** — it implements seats exactly as locked. Energy buckets/allocation remain P10-13.
