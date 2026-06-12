# Buy an energy pack (never expires)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — one-off purchase + child assignment + never-expire pool.
- **Labels:** `billing`, `payments`, `credits`, `backend`
- **Requirements:** FR-PAY-3 *(new — Phase 9)*

## Description
As a parent, I want to buy a 1000-energy pack for a specific child, so that they can keep using the Helper after their monthly energy runs out.

## Acceptance Criteria
- A parent buys a **1000-credit pack ($5)** via the payment provider and **assigns it to a specific child**.
- Purchased credits land in `PurchasedBalance` and **never expire**.
- A successful purchase is recorded as a **`Purchase`** transaction with the payment reference.
- A **failed payment grants no credits** and is surfaced as an error (P9-09).
- Pack size and price are config-driven (P9-11).

## Notes
- Purchased-credits-persist is **lead-confirmed**. Children **cannot** purchase — purchase is parent-gated.
- Blocked by **P9-01** and **P9-06** (reuses the payment seam).
