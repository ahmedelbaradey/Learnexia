# Seat enforcement, grace period & NoSeat/Locked child lifecycle

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 8 — payment-failure grace state machine + enforcement job + child NoSeat/Locked lifecycle + parent "choose active children" flow.
- **Labels:** `billing`, `subscription`, `seats`, `child-data`, `backend`, `frontend`
- **Requirements:** FR-PAY-11 *(new — Phase 10)*

## Description
As a parent whose paid seats are reduced (downgrade, seat cancellation, or a failed payment renewal), I want a clear way to choose which children keep the remaining active seats — so that a reduction never silently deletes a child or destroys their progress, and over-limit children are only locked out of AI help, not removed.

This story is the **enforcement + lifecycle half** of the LOCKED family energy model. It builds directly on the seat model (P10-14) and the family wallet + entitlement grant + per-child allocation (P10-13), the redistribution/reset rules (P10-16), the subscription/downgrade state machine (P10-05), dunning/payment-failure (P10-09), and the immutable per-child ledger (P10-01). It does **not** define how energy is granted or split — that is P10-13/P10-16. It defines **what happens to seats and children when the number of paid active seats goes down**.

## Locked rules this story implements — FINAL MODEL (lead-approved 2026-06-17)

### One cycle, one family
All seats renew together under the parent subscription. There is one billing cycle per family; partial-cycle renewals do not exist.

### Adding or reactivating a seat mid-cycle
- **Money prorates:** the parent is charged `SeatPrice × remaining-cycle-ratio` immediately.
- The child (or reactivated child) becomes **Active immediately** — no waiting for the next cycle.
- **NO energy is minted mid-cycle** by this event. The child's mid-cycle energy comes **only** from the parent allocating credit from the family wallet (P10-16). Zero subscription energy is granted on add/reactivate; there is nothing to guard against duplicate grants.
- At the next renewal the seat is treated as a normal full-cycle seat; the renewal entitlement grant = `ActiveSeats × PlanEnergyPerSeat`.

### Voluntary seat removal or downgrade mid-cycle
- Effective at **cycle end** — the seat remains **Active** until the next renewal boundary. The child keeps their current allocation and can continue spending AI energy for the remainder of the cycle.
- **No prorated refund** is issued.
- **No energy is reclaimed, forfeited, or converted** mid-cycle. Once granted and ledgered, energy is valid for the entire billing period — "once granted + ledgered, energy stays valid for that billing period."
- At renewal the seat is removed and the entitlement grant recalculates with fewer seats. Over-seat children (more seat-holders than active paid seats) transition to **NoSeat/Locked** at that renewal boundary.
- **There is NO mid-cycle forfeit job** — no unspent allowance is stripped mid-cycle for voluntary removal.

### 7-day grace = payment-failure retry window only
- The **7-day grace window** applies **only** to the **payment-failure / dunning** path (P10-09 `PastDue`/`Dunning`). It is the retry window at the renewal boundary during which children keep full access while the parent resolves the failed charge.
- Voluntary removal and downgrade are **NOT** grace-period events. They are effective-at-cycle-end events — no grace window is started.
- During payment-failure grace, the previously-active seats stay temporarily active and children keep their allocations. On grace expiry without successful payment, limits are enforced.
- Grace is **idempotent**: repeated failed-charge webhooks do not stack, shorten, or extend a single active window.
- **OPEN NUMBER (LEAD FLAG):** the **grace-period length** is config-driven via P10-12 GlobalSettings (`seats.grace_days`). The recommended default is 7 days. Confirm with lead before implementation; do not hard-code.

### Purchased packs
Purchased (pack) energy is family-owned, never expires, is not prorated, and is not seat-linked. Seat enforcement never touches the purchased reserve.

## Acceptance Criteria

### Voluntary seat removal / downgrade — effective at cycle end
- When a parent voluntarily removes a seat or downgrades to fewer seats, the change takes effect at **cycle end** — not immediately.
- The affected child's seat remains **Active** through the rest of the current billing cycle; the child keeps their allocation and can continue spending.
- **No refund** is issued for the remainder of the cycle.
- **No energy is reclaimed, forfeited, or converted** when a seat is voluntarily removed mid-cycle. There is no mid-cycle forfeit job for voluntary removal.
- At the next renewal the seat count drops. If `children-holding-a-seat > paid-active-seats` after the renewal recalculation, over-seat children are moved to **NoSeat/Locked** (see enforcement below).
- The parent sees a **scheduled-removal notice**: "This seat will be removed at your next renewal on [date]. Your child keeps full access until then."

### Payment-failure grace (7-day retry window at renewal boundary)
- A **payment failure** at renewal starts a **7-day grace window** during which children keep full access (seats remain temporarily active; allocations stay live; AI spend continues).
- The parent is shown a **payment-failure banner**: failed charge, next retry date, grace end date, action to update payment method.
- If payment succeeds within the grace window, the cycle renews normally and no seats are lost.
- If the grace window expires without successful payment, limits are **enforced** (see below).
- Grace is **idempotent**: repeated failed-charge webhooks during an open window do not stack or shorten the deadline.

### Enforcement on renewal or grace expiry
- Enforcement runs at the **renewal boundary**: (a) after a voluntary removal/downgrade takes effect, or (b) after the payment-failure grace window expires without recovery.
- Enforcement computes `paid-active-seats` vs `children-currently-holding-a-seat`. If `children ≤ seats`, no action.
- If `children > seats`, over-limit children are moved to **NoSeat/Locked**. The parent may have pre-chosen which children keep seats (see below); otherwise a **deterministic default** is applied (earliest-reserved seats are kept; tie-break confirmed by lead).
- Enforcement **never deletes a child** and never touches Identity/Learning/Gamification records.
- Each lock transition writes an **immutable ledger entry** (P10-01) — no silent state change.
- **No mid-cycle energy forfeit on enforcement.** Energy that was granted and ledgered for the current cycle remains valid; there is no allowance-strip at enforcement time. The allocation row for a newly-locked child is removed (no future entitlement), but already-credited energy is not reclaimed.
- **Purchased (pack) energy is never touched** by enforcement — it is a permanent shared family reserve.

### Adding or reactivating a seat mid-cycle
- When a parent adds a new seat or reactivates a NoSeat/Locked child mid-cycle, the system charges the **prorated amount** (`SeatPrice × remaining-cycle-ratio`) immediately via the payment provider.
- The child (or reactivated seat) becomes **Active immediately** after the payment is confirmed via verified webhook.
- **NO subscription energy is minted** for the new/reactivated seat mid-cycle. The child's energy for the remainder of the cycle must come from the parent allocating credit from the family wallet (P10-16).
- The parent is shown a **prorated-charge preview** before confirming reactivation.
- At the next renewal the seat is a normal full-cycle seat; `renewal grant = ActiveSeats × PlanEnergyPerSeat`.

### NoSeat/Locked child state
- A NoSeat/Locked child has **no active entitlement allowance row** and **cannot spend AI energy** — the seat-based spend gate (P10-13) denies AI Helper requests with a localized, friendly message (no raw error).
- The locked child **keeps everything else**: profile, learning progress, XP, streaks, history, mastery, badges/achievements — all intact and still viewable.
- A locked child can be **reactivated** by the parent: prorated charge + immediate activation (energy via P10-16 wallet allocation).

### Parent chooses which children keep active seats
- The parent sees the list of their children with current seat state and can **choose which children keep the active seats**, constrained to the **current paid active-seat limit**.
- The choice can be made **before the renewal boundary** (pre-empts the enforcement default) or **after enforcement** (re-shuffle who is Active vs Locked).
- Submitting a choice that selects more children than the limit is **rejected** with a clear validation message; fewer is allowed (remaining seats stay unassigned).
- The action is **parent-gated and family-scoped** — a parent can only set seat assignments for their own children. Applying the choice updates seat assignments and child state and is **ledgered** (no silent state change).

## Web-checkout-first / money — COMPLIANCE FLAG
> This story does **not** itself take payment (reactivation prorated charge is via P10-14's checkout seam), but it reacts to payment-failure (P10-09) and changes paid-seat entitlement. Because it touches **money-derived entitlement and child data**, the **`security-auditor` stage is mandatory** for the backend batch (family-scope authz on seat re-assignment, no cross-family seat moves, no child deletion, idempotent enforcement).

## Backend vs Frontend scope

**Backend (P10-15-BE):**
- Voluntary-removal scheduling: record the pending removal at cycle-end on the seat model; no immediate state change; no grace window started.
- Payment-failure grace-window state on the subscription (7-day window at renewal boundary; config-driven; idempotent).
- Enforcement job (Hangfire) that runs at renewal or on payment-failure grace expiry: compute over-limit children, apply parent choice or deterministic default, move over-limit children to NoSeat/Locked, write immutable ledger entries — **no energy forfeit** — never delete a child.
- Reactivation command: validate prorated charge via payment provider seam (P10-14 checkout flow); activate immediately on confirmed webhook; **zero energy minted** (P10-16 handles wallet allocation).
- NoSeat/Locked child state in the Billing model + the **seat-based spend gate** so a locked child's AI spend is denied (graceful, localized).
- Parent commands/queries: view seat state per child, choose which children keep active seats (validated against the paid-active-seat limit, family-scoped), reactivate a child.
- `Shared.Contracts` seam so the AI/spend path and the Parent module can check seat state without crossing module boundaries.
- Option C service-only (Application EF-free); no free-text string literals (localized keys + enums).

**Frontend (P10-15-FE) — parent area only, other (frontend) lead:**
- **Payment-failure banner** on the parent plan/billing surface: failed charge, retry date, grace end date, action to update payment method.
- **Scheduled-removal notice** on affected child's card / plan surface: "seat removed at next renewal on [date]; full access until then."
- **Prorated-charge preview** before confirming mid-cycle reactivation: prorated amount, immediate activation, note that energy comes from family wallet (not auto-granted).
- **Choose-active-children** screen/modal: list children with seat state, select up to the limit, submit; validation when over the limit; confirmation copy.
- **Locked-child state** in the parent's child list / child cards: "no active seat — learning history kept, AI help paused" badge + "reactivate (prorated)" affordance.
- Never rendered on a child route; parent-gated. EN + AR, RTL.

## Dependencies
- **P10-14** (seats model + active-seat count + add-child seat reservation seam + extra-seat checkout flow used for reactivation) — **hard upstream**.
- **P10-13** (family energy account + entitlement grant + per-child allocation) — **hard upstream**; enforcement removes the allowance row for locked children.
- **P10-16** (family wallet allocation) — **hard upstream for reactivation**: energy for a reactivated child mid-cycle comes from parent wallet allocation, not from this story.
- **P10-05** (subscription / downgrade state machine) — downgrade schedules seat removal at cycle-end.
- **P10-09** (dunning / payment-failure + grace at cycle end) — payment-failure is the grace trigger; align the 7-day window.
- **P10-01** (immutable per-child ledger) — all lock/unlock movements are ledgered.
- Relates to **P10-17** (purchased-energy refunds) — keep seat-state + ledger contracts consistent.

## Notes
- **Supersedes the per-child `CreditAccount` ownership model** (LOCKED 2026-06-16) — the family-account migration is owned by P10-13 and the seat model by P10-14; this story assumes the family/seat model is already in place.
- **FINAL MODEL (2026-06-17) — three reversals from earlier drafts:**
  1. **Money prorates on ADD/REACTIVATE; energy does NOT mint mid-cycle.** Earlier drafts said no proration for MVP. The final model prorates money but mints zero energy mid-cycle.
  2. **Voluntary removal is effective at cycle end; no mid-cycle energy forfeit.** Earlier drafts (OQ-H) described a forfeit of unspent allowance back to the family wallet on enforcement. The final model removes this entirely: "once granted + ledgered, energy stays valid for that billing period." There is NO forfeit job.
  3. **7-day grace = payment-failure retry window only.** Earlier drafts applied the grace window to all reduction events (downgrade, seat-cancel, payment-failure). The final model restricts grace to payment-failure only; voluntary removal and downgrade are effective-at-cycle-end events.
- **No mid-cycle forfeit.** This is a hard product decision. Do not implement any job or service that strips unspent entitlement mid-cycle for voluntary seat removal. The OQ-H forfeit design is fully removed.
- Children are **never deleted** and **never lose progress/XP/history/achievements** — this is a hard invariant; the security-auditor must verify enforcement cannot cascade into Identity/Learning data.
- **LEAD FLAG (open number):** grace-period length — confirm `seats.grace_days` default (recommended: 7) with lead before implementation.
- **LEAD FLAG (tie-break):** deterministic default for enforcement when parent has not pre-chosen — recommend keep earliest-reserved seats; confirm before implementing the enforcement job.
