# Family energy redistribution & intra-family transfers

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — move unspent allocated allowance between siblings + family-only transfer boundary + immutable ledger per movement.
- **Labels:** `billing`, `credits`, `family-energy`, `backend`, `frontend`
- **Requirements:** FR-PAY-7 *(new — Phase 10, family-energy-model wave)*

## Description
As a parent, I want to move **unspent** allocated energy from one of my children to another (e.g. Child A has 2000 left → send 1000 to Child B), so that I can rebalance my family's monthly entitlement toward whoever needs the AI Helper most — without buying more energy.

## Locked family-energy-model context (authoritative)
This story sits inside the **locked family-energy model** (approved 2026-06-16, family-energy-model wave P10-13..17). Energy is **parent/family-owned** via a `FamilyEnergyAccount` with two non-convertible buckets:
- **(A) Subscription/entitlement** — temporary/monthly, `PlanEnergyPerSeat × ActivePaidSeats`, **allocated** to active-seat children as per-child allowances (equal split by default; the parent can customize/redistribute). Resets each cycle.
- **(B) Purchased (pack)** — permanent, never expires, a **shared family reserve** (not allocated per child).

**This story operates on bucket (A) only — the per-child allocated allowance rows.** It does not touch the shared purchased reserve (B), does not mint or convert energy, and does not change spend order (own allocation row first → shared purchased row fallback, per the locked model). Redistribution is a **zero-sum move of unspent allocated energy between the parent's own children**.

## Acceptance Criteria
- A parent can **move unspent allocated allowance from one of their own children to another of their own children** (decrement source child's allocation row, increment destination child's allocation row by the same amount). The family total is unchanged — redistribution is **zero-sum**.
- **Only UNSPENT allocated energy is movable.** Already-spent energy can **NEVER** be reclaimed; a transfer can move at most the source child's current **remaining allocated allowance** (not its original allocation). Over-allocation is rejected.
- Transfers are **family-only**: source and destination children **must both belong to the same parent / `FamilyEnergyAccount`**. A transfer that names a child outside the family is **rejected** (anti-abuse: no cross-family transfer, no resale/credit laundering). Cross-family is impossible by construction, not just policy.
- A transfer touches **bucket (A) allocated allowance only** — it never moves, converts, or creates **purchased** (bucket B) energy, and never moves **subscription→purchased** or **purchased→subscription**. The two buckets stay non-convertible.
- **Every movement is recorded in the immutable per-child ledger** — one paired, append-only entry on each side (source debit + destination credit), each with the transfer reason/type, amount, resulting allocation balance, UTC timestamp, a shared correlation id linking the two sides, and an idempotency key. Already-spent energy is never represented as reclaimable in the ledger.
- The transfer is **atomic** — either both ledger rows + both allocation updates commit, or none do; a failure leaves balances untouched (no partial move, no double-spend).
- A parent can **view the current per-child allocation split** (remaining allowance per active-seat child) so they know how much is movable before transferring.
- **Parent-gated** — only the owning parent can redistribute; children can never move energy, and a parent can never touch another family's allocations.

## Out of scope (covered by sibling stories)
- The grant + equal-split **initial allocation** of entitlement energy each cycle — **P10-13 (family wallet & per-child allocation)**; seat math + active-seat count — **P10-14 (seats)**.
- Buying the shared purchased reserve (bucket B) — **P10-07** (rehomed onto `FamilyEnergyAccount` by the family-energy-model wave).
- Refunds of purchased energy — **P10-09 / P10-17** (refundable = purchased − consumed purchased; subscription grants are never refundable).
- Grace period / enforcement on seat reduction (NoSeat/Locked children keep progress) — **P10-15 (grace & enforcement)**.
- Spending/charging on AI help (charge-on-delivery, per-intent costs, daily soft cap) — **P10-03 / P10-04**, unchanged by this story.

## Dependencies
- **Blocked by P10-13** — `FamilyEnergyAccount` + dual-bucket model + per-child **allocation rows** (entitlement granted + equal-split per active seat) + immutable per-child ledger + the `CreditAccount → FamilyEnergyAccount` data migration (supersedes the old per-child `CreditAccount` ownership model; live `credits.premium_monthly` / `credits.free_monthly` become `PlanEnergyPerSeat`). The allocation rows must exist before there is anything to redistribute.
- **Relates to P10-14** (seats / active-seat count) — only active-seat children have an allocation row that can be a transfer source/destination.
- Builds on the **immutable ledger (P10-01 → rehomed in P10-13)** for the paired transfer entries.
- Relates to **P10-05** (subscription/seats source of truth) and **P10-09 / P10-17** (refunds — refunds touch purchased only; redistribution touches allocated only; they must not be conflated in the ledger).

## Scope split

### BACKEND (P10-16-BE)
- `TransferAllocationCommand(parentUserId, fromChildId, toChildId, amount)` in the **Billing** module: validate same-family ownership of both children, validate `amount ≤ source remaining allocated allowance`, perform the zero-sum move + paired immutable ledger entries inside one explicit transaction, idempotent.
- `GetFamilyAllocationQuery(parentUserId)` — per-child remaining allowance (movable amount) for the parent's active-seat children.
- REST endpoints under `/api/Billing/...`, parent-JWT-only with owning-parent (family-scope) authz.
- Option C service-only (Application EF-free; logic behind a service), module isolation (child profiles live in Identity; reference children by loose id, no FK), localized message keys/enums (no free-text literals).
- **Security-sensitive (money + child data)** — `security-auditor` gate.

### FRONTEND (P10-16-FE)
- A parent-facing **allocation editor / "move energy between children"** control in the parent area: shows the per-child remaining-allowance split, lets the parent pick a source child, a destination child, and an amount (capped at the source's remaining), confirm, and see updated balances.
- Parent-only surface — never rendered on any child route. EN + AR, RTL.

## Notes
- **Locked decision (2026-06-16) — do not reinterpret.** Already-spent energy is never reclaimable; transfers are intra-family only; buckets are non-convertible; all movements are ledgered. This story implements redistribution *within* those rules.
- **Module ownership (hard):** Billing owns the `FamilyEnergyAccount`, allocation rows and ledger; Identity owns child profiles. Cross-module via `Shared.Contracts` only — no cross-module FK.
- No Unit of Work — the paired debit+credit + two ledger rows are atomic via an explicit transaction (ADR 0001).
- Charging is unchanged by this story (charge-on-delivery; cache HIT and MISS both charge; no charge on failure/safety-refusal; per-intent Hint=1 / WhyWrong=2 / Explain=3 / SimilarExample=5 from GlobalSettings; daily soft cap).
