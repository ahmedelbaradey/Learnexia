# Buy an energy pack (never expires)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — one-off purchase + child assignment + never-expire pool.
- **Labels:** `billing`, `payments`, `credits`, `backend`
- **Requirements:** FR-PAY-3 *(new — Phase 10)*

## Description
As a parent, I want to buy a 1000-energy pack for a specific child, so that they can keep using the Helper after their monthly energy runs out.

## Acceptance Criteria
- A parent buys a **1000-credit pack ($5)** via the payment provider and **assigns it to a specific child**.
- Purchased credits land in `PurchasedBalance` and **never expire**.
- A successful purchase is recorded as a **`Purchase`** transaction with the payment reference.
- A **failed payment grants no credits** and is surfaced as an error (P10-09).
- Pack size and price are config-driven (P10-11).

## Web-checkout-first — COMPLIANCE FLAG
> **Same policy as P10-06:** the pack purchase path is **web checkout only**. No native IAP (Apple/Google). **Validate against current App Store / Play Store policy before native launch.** The security-auditor stage must flag this.

## Notes
- Purchased-credits-persist is **lead-confirmed**. Children **cannot** purchase — purchase is parent-gated.
- Blocked by **P10-01** and **P10-06** (reuses the payment seam).
