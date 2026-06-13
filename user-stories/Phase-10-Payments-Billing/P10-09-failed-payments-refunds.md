# Failed payments & refunds

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — dunning + refund + clawback policy.
- **Labels:** `billing`, `payments`, `backend`
- **Requirements:** FR-PAY-5 *(new — Phase 10)*

## Description
As the platform, I want to handle failed payments and refunds correctly, so that access and credits stay consistent with what was actually paid.

## Acceptance Criteria
- A **failed recurring charge** triggers a **dunning** flow (retry schedule + parent notification); after N failures the plan downgrades to Free at cycle end (configurable grace period).
- A **refund** reverses the entitlement: a **pack refund** claws back **unspent** purchased credits; a **subscription refund** follows the stated policy (no mid-cycle granted-credit clawback beyond policy).
- All refunds are **idempotent** ledger entries linked to the original payment.
- Provider webhook events (charge.failed, refund.succeeded) are processed idempotently and reconciled with our state.

## Notes
- **Clawback policy** (subscription refund, partially-spent pack) needs lead confirmation. Blocked by **P10-06**.
