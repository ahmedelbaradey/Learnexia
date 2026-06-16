# Family energy wallet & per-child allocation

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 8 — family wallet + two non-convertible buckets + equal-split allocation + child-first spend algorithm + migration off the per-child `CreditAccount` model.
- **Labels:** `billing`, `credits`, `payments`, `backend`, `frontend`
- **Requirements:** FR-CREDIT-4 *(new — Phase 10)*

## Description
As a parent, I want one family energy wallet whose monthly subscription energy is split across my active-seat children (and topped up by never-expiring purchased packs), so that each child has their own allowance to spend on the AI Helper while my purchased reserve backs them all.

This story **supersedes the per-child `CreditAccount` ownership model from P10-01** — energy is now **parent/family-owned** in a `FamilyEnergyAccount`, allocated down to children. A **data migration** from the existing `CreditAccount` rows is part of this story.

## The locked family energy model (authoritative — do not reinterpret)
Energy is **parent/family-owned** via a `FamilyEnergyAccount` with **two separate, NON-CONVERTIBLE buckets**:
- **(A) Subscription / entitlement** — **temporary/monthly**. `amount = PlanEnergyPerSeat × ActivePaidSeats`. Granted into the wallet, then **allocated** to active-seat children as per-child allowances (**EQUAL split by default**; parent can customize/redistribute — see P10-16). **Resets each cycle. CANNOT convert to purchased.**
- **(B) Purchased (pack)** — **permanent, never expires**, a **SHARED family reserve** (NOT allocated per child). Fed by P10-07 pack purchases.

**Spend order (per child):** the child's **own allocated allowance row FIRST** → then the **shared family purchased row** (fallback only). Normal AI spend hits the child allocation row; the shared family row is touched **only** for the purchased fallback (keeps it cold — no contention hotspot).

## Acceptance Criteria
- **AC-1 (wallet):** Each family (parent account) has exactly one `FamilyEnergyAccount` holding two distinct, non-convertible balances — a **subscription/entitlement** balance (temporary) and a **purchased** balance (permanent, never expires). No code path moves energy from one bucket to the other.
- **AC-2 (grant computation):** The monthly subscription grant deposited into the wallet equals **`PlanEnergyPerSeat × ActivePaidSeats`**. Only **paid, active** seats generate entitlement energy; a child profile alone mints **none**. (Seat model + active-seat count is owned by P10-14; this story consumes it.)
- **AC-3 (equal-split allocation, default):** On grant, the subscription wallet balance is **allocated EQUALLY by default** across the active-seat children into per-child `ChildEnergyAllocation` rows. Any remainder from integer division is distributed deterministically (no energy lost or invented). Custom redistribution is P10-16.
- **AC-4 (spend algorithm):** A child's AI spend debits their **own `ChildEnergyAllocation` row first**; only when that allowance is insufficient does it draw the shortfall from the **shared family purchased** balance. The shared purchased row is **never** touched by a child whose own allowance still covers the cost.
- **AC-5 (per-child ledger):** Every movement — grant-allocation, spend (per source bucket), purchased fallback, redistribution in/out (P10-16), expiry, refund (P10-09/P10-17) — is an append-only entry in an **immutable per-child ledger**, with type, amount, source bucket, resulting balances, UTC timestamp, and an idempotency key. Family-level purchased movements are also ledgered.
- **AC-6 (monthly reset, no convert):** At cycle rollover the **subscription** wallet balance and all per-child subscription allocations **reset/expire** (`Expire` ledger entries); the **purchased** balance is untouched. Unspent subscription energy never converts to purchased and never carries over.
- **AC-7 (charging unchanged):** Charging stays charge-on-delivery; **cache HIT and MISS both charge**; **no charge** on failure or safety-refusal; per-intent costs **Hint = 1 / WhyWrong = 2 / Explain = 3 / SimilarExample = 5** (from GlobalSettings P10-12); the daily soft cap (P10-04) still applies. This story only changes **which rows** are debited, not when/how much.
- **AC-8 (migration):** A data migration moves the existing per-child `CreditAccount` model (P10-01) to the new wallet + allocations: each parent gets a `FamilyEnergyAccount`; existing `PurchasedBalance` rolls up into the **shared family purchased** balance; existing `GrantedBalance` becomes the child's current-cycle `ChildEnergyAllocation`. The pre-migration ledger is preserved/linked; balances reconcile against it. The seeded settings `credits.premium_monthly` (5000) / `credits.free_monthly` (100) become **`PlanEnergyPerSeat`** (per-seat, not per-child).
- **AC-9 (module ownership & isolation):** **Billing** owns the `FamilyEnergyAccount`, both buckets, `ChildEnergyAllocation`, and the per-child ledger. Child identity stays in **Identity** (loose `int` child ref, **no cross-module FK**). The AI module spends only through the existing `Shared.Contracts/Billing` spend seam — no Billing internals leak across the boundary. No free-text string literals — localized keys / enums only.

## BACKEND vs FRONTEND scope
- **BACKEND (core of this story):** `FamilyEnergyAccount` (two buckets) + `ChildEnergyAllocation` + per-child ledger; equal-split allocation on grant; the child-allocation-first → shared-purchased-fallback spend algorithm wired behind the existing `ICreditSpendService` seam; the `PlanEnergyPerSeat × ActivePaidSeats` grant computation feeding the wallet; the migration off `CreditAccount`. See `tasks/Backend/Phase-10-Payments-Billing/P10-13-BE.md`.
- **FRONTEND:** a **parent-only** "Family Energy" overview — wallet balances (subscription vs purchased), per-child allocation list, per-child spend/remaining. **Read-only** here; redistribution UI is **P10-16**. No child surface (children see only their meter, P10-10). See `tasks/Frontend/student-app/Phase-10-Payments-Billing/P10-13-FE.md`.

## Dependencies
- **Builds on P10-01** (append-only ledger + idempotency primitives) — this story **re-homes ownership** from per-child `CreditAccount` to the family wallet and migrates the data.
- **Builds on P10-02** (monthly grant job) — the grant now deposits to the **wallet** then equal-splits into allocations.
- **Builds on P10-05** (subscription) and **P10-07** (purchased packs feed the shared purchased bucket).
- **Consumes P10-14** (seats / `ActivePaidSeats` count) for the grant computation. If P10-14 is not yet landed, gate the seat-count source behind a `Shared.Contracts/Billing` seam and confirm sequencing with the lead.
- **Feeds P10-16** (parent redistribution of unspent allocations between siblings) and is reconciled by **P10-09/P10-17** (refunds — purchased only).
- Charging behavior (P10-03 / P10-04) and per-intent costs (P10-12 GlobalSettings) are unchanged.

## Notes
- **Sibling cluster (P10-13..17):** P10-13 = wallet + allocation + spend (this); **P10-14** = seats (included + extra paid) + active-seat count + seat-reserved add-child; **P10-15** = grace period & enforcement (NoSeat/Locked, keep progress); **P10-16** = parent redistribution between siblings; **P10-17** = refunds-on-purchased reconciliation extension. Keep contracts forward-compatible with these.
- **Non-convertible buckets are a hard rule** — there is intentionally **no** "top up subscription from purchased" or vice-versa. The two balances are independent columns/rows.
- **Spend hotspot avoidance is a hard rule** — the shared purchased row must stay "cold": only the purchased-fallback path may touch it, so concurrent siblings contend on their **own** allocation rows, not on one shared row.
- **Transfers (P10-16)** are **within the same family only** (sibling→sibling); **already-spent energy can never be reclaimed**. Out of scope here but the ledger/allocation shape must support it.
- **Refunds (P10-09/P10-17)** apply to **purchased only**, never subscription grants.
- **Security-sensitive** (money + child data) — the backend batch gets a `security-auditor` gate: family-scope authz on every read/spend, no cross-family energy visibility, idempotent ledgered movements, verified-webhook-only funding.
- **Migration is destructive of the old ownership model** — must run once, be idempotent, and reconcile every migrated balance against the preserved ledger before the old `CreditAccount` path is retired.
