# Family energy redistribution & mid-cycle energy mechanism

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — move unspent allocated allowance between siblings (including to a just-activated mid-cycle child) + family-only transfer boundary + immutable ledger per movement.
- **Labels:** `billing`, `credits`, `family-energy`, `backend`, `frontend`
- **Requirements:** FR-PAY-7 *(new — Phase 10, family-energy-model wave)*

## Description
As a parent, I want to allocate **unspent** family-wallet energy to any of my active-seat children — whether rebalancing between existing children OR giving energy to a child just activated mid-cycle who received no automatic grant — so that I can direct the AI Helper capacity where it is most needed without buying more energy.

This story is the **only mechanism** by which a mid-cycle-added or reactivated child can receive subscription energy during the current cycle. Because adding a seat mid-cycle mints NO energy (see P10-14 / P10-15), a newly activated child starts the cycle with zero allocated allowance and the parent must explicitly allocate family-wallet credit to them using this feature.

## Locked family-energy-model context (authoritative — 2026-06-16 base; updated 2026-06-17)
This story sits inside the **locked family-energy model** (family-energy-model wave P10-13..17). Energy is **parent/family-owned** via a `FamilyEnergyAccount` with two non-convertible buckets:
- **(A) Subscription/entitlement** — temporary/monthly, `PlanEnergyPerSeat × ActivePaidSeats`, **allocated** to active-seat children as per-child allowances (equal split by default at each renewal; the parent can customize/redistribute using this story). Resets each cycle.
- **(B) Purchased (pack)** — permanent, never expires, a **shared family reserve** (not allocated per child).

**This story operates on bucket (A) only — the per-child allocated allowance rows.** It does not touch the shared purchased reserve (B), does not mint or convert energy, and does not change spend order (own allocation row first → shared purchased row fallback, per the locked model).

### Mid-cycle seat add = this story provides the child's energy (FINAL MODEL 2026-06-17)
Adding or reactivating a seat mid-cycle prorates money but **mints NO subscription energy** (P10-14 / P10-15 final model). A child activated mid-cycle starts with **zero allocated allowance**. Their energy for the rest of the current cycle must come **entirely from the parent using this feature** — the parent allocates credit from the family wallet (bucket A, the remaining unspent pool) to the newly active child.

This makes P10-16 the **MID-CYCLE ENERGY MECHANISM**: it is not only rebalancing between children who already have allowances, it is also the primary way a new mid-cycle child gets any energy at all. The destination child does NOT need to have had a prior allocation; a zero-allocation active-seat child is a valid transfer destination.

Redistribution is a **zero-sum move of unspent allocated energy between the parent's own active-seat children** — the family total (bucket A remaining) is unchanged.

## Acceptance Criteria

### Core transfer (rebalancing between existing children)
- A parent can **move unspent allocated allowance from one of their own children to another of their own children** (decrement source child's allocation row, increment destination child's allocation row by the same amount). The family total is unchanged — redistribution is **zero-sum**.
- **Only UNSPENT allocated energy is movable.** Already-spent energy can **NEVER** be reclaimed; a transfer can move at most the source child's current **remaining allocated allowance** (not its original allocation). Over-allocation is rejected.
- Transfers are **family-only**: source and destination children **must both belong to the same parent / `FamilyEnergyAccount`**. A transfer that names a child outside the family is **rejected** (anti-abuse: no cross-family transfer, no resale/credit laundering). Cross-family is impossible by construction, not just policy.
- A transfer touches **bucket (A) allocated allowance only** — it never moves, converts, or creates **purchased** (bucket B) energy, and never moves **subscription→purchased** or **purchased→subscription**. The two buckets stay non-convertible.
- **Every movement is recorded in the immutable per-child ledger** — one paired, append-only entry on each side (source debit + destination credit), each with the transfer reason/type, amount, resulting allocation balance, UTC timestamp, a shared correlation id linking the two sides, and an idempotency key. Already-spent energy is never represented as reclaimable in the ledger.
- The transfer is **atomic** — either both ledger rows + both allocation updates commit, or none do; a failure leaves balances untouched (no partial move, no double-spend).
- A parent can **view the current per-child allocation split** (remaining allowance per ALL active-seat children, including those with zero allocation) so they know how much is available in the family pool and what each child currently has.
- **Parent-gated** — only the owning parent can redistribute; children can never move energy, and a parent can never touch another family's allocations.

### Mid-cycle child: allocating to a newly activated (zero-allocation) child — FINAL MODEL 2026-06-17
- A **mid-cycle-activated child** (added or reactivated mid-cycle per P10-14/P10-15) starts the cycle with **zero allocated allowance** because no subscription energy is minted mid-cycle. This story is **the only way that child can receive subscription energy during the current cycle**.
- A parent can allocate energy **to a child with zero current allocation** — i.e. the destination child does NOT need to have had a prior allocation row for this cycle. The system must accept a destination child whose `ChildEnergyAllocation.Remaining = 0` (or where no allocation row yet exists for this cycle) and create or increment that row.
- The source of the transferred energy is the remaining unspent allocation of one or more other active-seat children (the parent picks the source child and amount). The total bucket-A balance across the family is unchanged.
- **Validation:** the destination child must hold an **active seat** (not NoSeat/Locked); the source child must have `remaining > 0`; the amount must be `> 0` and `≤ source.remaining`. A zero-allocation destination is NOT a rejection condition — it is a valid and expected use case.
- The allocation row for the destination child must be **created if it does not exist** for the current cycle (e.g. a mid-cycle-added child) or **incremented** if it exists. The same idempotency and ledger rules apply as for any transfer.
- **The parent is shown the mid-cycle context in the UI (P10-16-FE):** when a child has zero allocation, a label clarifies this is because they were activated mid-cycle and no grant was issued; the parent is directed to use this transfer tool to give them energy from the family pool.

## Out of scope (covered by sibling stories)
- The grant + equal-split **initial allocation** of entitlement energy at each cycle renewal — **P10-13 (family wallet & per-child allocation)**; seat math + active-seat count — **P10-14 (seats)**.
- **Mid-cycle seat prorated money charge** — the payment/money side of adding a seat mid-cycle is owned by **P10-14** (seat purchase webhook) and **P10-15** (reactivation checkout). This story owns what happens to the child's energy after the seat is active.
- **Mid-cycle energy minting** — there is none; this is the point. The only mechanism is P10-16 allocation transfer.
- Buying the shared purchased reserve (bucket B) — **P10-07** (rehomed onto `FamilyEnergyAccount` by the family-energy-model wave).
- Refunds of purchased energy — **P10-09 / P10-17** (refundable = purchased − consumed purchased; subscription grants are never refundable).
- Grace period / enforcement on seat reduction at renewal (NoSeat/Locked children keep progress) — **P10-15 (grace & enforcement)**. Note: there is no mid-cycle forfeit (see P10-15 final model). Voluntary seat cancel does not strip allocation mid-cycle.
- Spending/charging on AI help (charge-on-delivery, per-intent costs, daily soft cap) — **P10-03 / P10-04**, unchanged by this story.

## Dependencies
- **Blocked by P10-13** — `FamilyEnergyAccount` + dual-bucket model + per-child **allocation rows** (entitlement granted + equal-split per active seat) + immutable per-child ledger + the `CreditAccount → FamilyEnergyAccount` data migration (supersedes the old per-child `CreditAccount` ownership model; live `credits.premium_monthly` / `credits.free_monthly` become `PlanEnergyPerSeat`). The allocation rows must exist before there is anything to redistribute.
- **Relates to P10-14** (seats / active-seat count) — only active-seat children have an allocation row that can be a transfer source/destination.
- Builds on the **immutable ledger (P10-01 → rehomed in P10-13)** for the paired transfer entries.
- Relates to **P10-05** (subscription/seats source of truth) and **P10-09 / P10-17** (refunds — refunds touch purchased only; redistribution touches allocated only; they must not be conflated in the ledger).

## Scope split

### BACKEND (P10-16-BE)
- `TransferAllocationCommand(parentUserId, fromChildId, toChildId, amount)` in the **Billing** module: validate same-family ownership of both children (both must be active-seat children of this parent), validate `amount ≤ source remaining allocated allowance`, validate `amount > 0`, validate `fromChildId ≠ toChildId`; perform the zero-sum move + paired immutable ledger entries inside one explicit transaction, idempotent.
- **The destination child may have zero allocation** (mid-cycle-added child): if no `ChildEnergyAllocation` row exists for the destination child in the current cycle, the service creates one (INSERT). If a row exists, it is incremented. This is not a special-case — it is standard service behavior for the mid-cycle use case.
- `GetFamilyAllocationQuery(parentUserId)` — per-child remaining allowance (movable amount) for ALL of the parent's active-seat children, including those with zero current allocation (newly added mid-cycle children). The response must distinguish "has allocation / remaining = X" from "no allocation yet this cycle (mid-cycle seat)" so the FE can explain the zero state to the parent.
- REST endpoints under `/api/Billing/...`, parent-JWT-only with owning-parent (family-scope) authz.
- Option C service-only (Application EF-free; logic behind a service), module isolation (child profiles live in Identity; reference children by loose id, no FK), localized message keys/enums (no free-text literals).
- **Security-sensitive (money + child data)** — `security-auditor` gate.

### FRONTEND (P10-16-FE)
- A parent-facing **allocation editor / "move energy between children / give energy to a child"** control in the parent area: shows the per-child remaining-allowance split (including children with zero allocation — those children show a "no grant yet this cycle (mid-cycle activation)" label and a prompt to allocate energy to them), lets the parent pick a source child with remaining energy, a destination child (any active-seat child including a zero-allocation mid-cycle child), and an amount (capped at the source's remaining), confirm, and see updated balances.
- **Mid-cycle zero-allocation child state:** the allocation overview must clearly indicate when a child has zero current allocation because they were added mid-cycle. The "move energy" control must allow selecting such a child as a destination. An explanatory copy line should explain why the child has no energy and direct the parent to use this tool.
- Parent-only surface — never rendered on any child route. EN + AR, RTL.

## Notes
- **This story is the MID-CYCLE ENERGY MECHANISM (FINAL MODEL 2026-06-17).** Because adding or reactivating a seat mid-cycle mints NO subscription energy (P10-14 / P10-15 final model), P10-16 is the ONLY way a mid-cycle-activated child can receive subscription energy during the current cycle. Implementers must handle a zero-allocation destination child (create the row if absent; increment if present). Do not gate the transfer on the destination having a prior allocation.
- **Locked decision (2026-06-16; updated 2026-06-17).** Already-spent energy is never reclaimable; transfers are intra-family only; buckets are non-convertible; all movements are ledgered. Mid-cycle-add context adds the zero-allocation destination requirement. This story implements redistribution *within* those rules.
- **Two distinct use cases share the same command/endpoint:** (a) rebalancing between children who both have existing allocations; (b) allocating to a newly active child who has zero allocation. Both are handled by the same `TransferAllocationCommand` — the backend must not special-case them as different flows.
- **Module ownership (hard):** Billing owns the `FamilyEnergyAccount`, allocation rows and ledger; Identity owns child profiles. Cross-module via `Shared.Contracts` only — no cross-module FK. The destination child's active-seat status is validated within the Billing-owned `SeatReservation` / allocation model — not by reaching into Identity.
- **No Unit of Work** — the paired debit+credit + two ledger rows (+ potential destination allocation row creation) are atomic via an explicit transaction (ADR 0001).
- **Destination row creation:** if the destination child has no `ChildEnergyAllocation` row for the current cycle (newly added mid-cycle child), the service must INSERT a new row with the transferred amount as `Allocated = amount`, `Spent = 0`, `Remaining = amount`. This is not a separate story or schema change — it is part of `FamilyAllocationService.TransferAllocationAsync`.
- Charging is unchanged by this story (charge-on-delivery; cache HIT and MISS both charge; no charge on failure/safety-refusal; per-intent Hint=1 / WhyWrong=2 / Explain=3 / SimilarExample=5 from GlobalSettings; daily soft cap).
