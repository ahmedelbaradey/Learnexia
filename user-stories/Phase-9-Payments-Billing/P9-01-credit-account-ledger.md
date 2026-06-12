# Credit (energy) account & ledger

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Technical Enabler
- **Story Points:** 5 — append-only ledger + dual-pool balance + idempotency; foundational for the whole phase.
- **Labels:** `billing`, `credits`, `backend`, `enabler`
- **Requirements:** FR-CREDIT-1 *(new — Phase 9)*

## Description
As the platform, I want a per-child credit ("energy") account backed by an append-only transaction ledger, so that every grant, spend, purchase, expiry, and refund is auditable and the balance is always reconstructable.

## Acceptance Criteria
- Each child has a `CreditAccount` with **two balances**: `GrantedBalance` (expires monthly) and `PurchasedBalance` (never expires).
- Every change is an append-only `CreditTransaction` — `Type` (Grant / Spend / Purchase / Expire / Refund / Adjustment), amount, reason, resulting balance, UTC timestamp, and an **idempotency key**.
- Account balance equals the sum of its ledger; a reconciliation check can detect drift.
- Spends draw from **`GrantedBalance` first, then `PurchasedBalance`** (perishable credits used first).
- Lives in a new **`Billing`** module (schema `billing`); the credit tables may sit in `ai`/`billing` per the cache-schema decision — confirm. Child is a **loose reference by id** (no cross-module FK to Identity).

## Notes
- **Foundation for all P9 stories.** Parent-driven: the account belongs to the **child**, funded by the parent's plan.
- **Open decision:** per-child accounts (modeled) vs a shared family pool — confirm with lead.
- No Unit of Work — a spend (debit + ledger row) is atomic via an explicit transaction (ADR 0001).
