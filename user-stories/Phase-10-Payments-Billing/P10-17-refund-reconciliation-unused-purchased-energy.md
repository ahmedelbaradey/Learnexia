# Refund reconciliation (unused purchased energy)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — two-bucket refund rules + refundable-balance computation + ledger reconciliation + parent refund-request UI.
- **Labels:** `billing`, `payments`, `refunds`, `energy`, `backend`, `frontend`
- **Requirements:** FR-PAY-5 *(extends — Phase 10)*

## Description
As a parent, I want to request a refund for energy I bought but never used, so that I only pay for the purchased energy my family actually consumed — and so subscription energy I was granted for free is never refundable.

## Locked family-energy model (authoritative — see P10-13)
This story sits inside the **LOCKED FAMILY ENERGY MODEL** (approved 2026-06-16; defined in **P10-13**). The two refund-relevant rules it depends on:
- Energy lives in a parent/family **`FamilyEnergyAccount`** with **two separate, non-convertible buckets**: **(A) Subscription/Entitlement** — temporary, monthly, allocated per active-seat child, resets each cycle; and **(B) Purchased (pack)** — permanent, never expires, a shared family reserve.
- **Refunds apply ONLY to the Purchased bucket, NEVER to subscription grants.** Refundable = `purchased − consumed-purchased` (e.g. bought 10000, used 3000 → 7000 refundable), reconciled against the **immutable per-child ledger**.

## Acceptance Criteria
- A parent can request a **refund of unused purchased energy** for a prior purchased pack (P10-07, rehomed onto the shared family purchased reserve by P10-13).
- The **refundable amount = purchased − consumed-purchased**, computed by **reconciling the immutable ledger** (sum of `Purchase` rows for that pack minus the `Spend` rows that drew from the **shared family purchased row**). Example: bought 10000, the family spent 3000 of purchased → **7000 refundable**.
- **Subscription/entitlement (bucket A) energy is NEVER refundable** — a refund request can only ever target purchased-bucket value; monthly grants and per-child allocations are excluded from every refund computation.
- **Already-consumed purchased energy can NEVER be refunded** (mirrors the "already-spent energy can never be reclaimed" rule for redistribution). The refundable amount is **clamped ≥ 0** and can never exceed the remaining purchased balance.
- A successful refund is recorded as an **idempotent `Refund` ledger entry** linked to the original `Purchase` payment, and **decrements the shared family purchased balance** by the refunded amount. The purchased balance can **never go negative**.
- Refunds are reconciled against the **immutable ledger** as the single source of truth — the refundable figure shown to the parent and the figure the provider refund settles on are both derived from the ledger, not from a mutable cached balance.
- A refund is settled **only via the verified provider webhook** (`refund.succeeded`); the in-app request initiates the provider refund but the ledger/balance change is driven by the webhook (consistent with P10-09).
- **Frontend (parent area only):** the parent can (a) see, per purchased pack, the **refundable purchased balance** in billing history (P10-08), and (b) submit a **refund request** for unused purchased energy; the resulting refund appears as a linked negative entry in billing history. **No child surface.**

## Builds on / supersedes
- **Refines** the existing `RefundService` / refund webhook branch from **P10-09** for the two-bucket model (this story **supersedes** P10-09's single per-child `PurchasedBalance` clawback semantics with the family-shared purchased-row reconciliation).
- Reads from the **immutable ledger of P10-01** (now the family/two-bucket ledger of P10-13) and the **purchased packs of P10-07** (now the shared family purchased reserve defined in P10-13).
- Subscription refunds (bucket A) remain governed by P10-05 / P10-09 policy and are **explicitly out of scope** here — this story handles purchased-bucket refunds only.

## Dependencies
- **P10-13** — `FamilyEnergyAccount` + two-bucket model + the shared family purchased reserve (the purchased rows this story refunds) + immutable family ledger (HARD dependency; defines the buckets and ledger reconciled here).
- **P10-09** — existing dunning/refund webhook + `RefundService` seam being refined.
- **P10-07 / P10-01** — original pack-purchase + ledger foundations.
- **P10-08** — billing history surface where refundable balance + refund entries render.

## Out of scope
- Subscription/entitlement refunds and mid-cycle grant clawback (P10-05 / P10-09 policy).
- Energy redistribution between siblings (P10-16) and seat/grace mechanics (P10-14 / P10-15) — refunds never create, convert, or reclaim subscription energy.
- Native IAP refund flows — **web checkout only** (same policy as P10-06 / P10-07).

## Notes
- **Money + child data → security-sensitive.** The `security-auditor` stage is mandatory for the backend batch.
- **Refund is parent-gated.** Children can never request or see refunds; the only child-visible effect is a reduced family energy reserve, surfaced naturally by the energy meter (P10-10).
- **Idempotency + no-negative-balance are the primary correctness risks** — same as P10-09; reconciliation must read the ledger and the shared purchased row inside an atomic transaction.
