# Pay for a subscription (payment provider)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 8 — payment-gateway integration, webhooks, recurring billing, PCI scope; **split candidate** (one-off checkout vs recurring renewal).
- **Labels:** `billing`, `payments`, `backend`, `security`
- **Requirements:** FR-PAY-2 *(new — Phase 9)*

## Description
As a parent, I want to pay for Premium securely, so that my subscription activates and renews each month.

## Acceptance Criteria
- A **successful payment** activates Premium and triggers the next monthly grant (P9-02).
- A **declined/failed payment** leaves the plan unchanged and shows a clear error (handed to P9-09).
- Payment runs through an **Egypt payment provider** behind an `IPaymentProvider` abstraction; **no card data touches our servers** (hosted/redirect or tokenized — PCI-safe).
- **Recurring billing:** the subscription renews monthly; the parent can cancel (cancel stops future renewals, keeps access to cycle end).
- Provider **webhooks** reconcile provider state with our subscription state **idempotently** (no double-activation).

## Notes
- ⚠️ **DECISION:** payment provider — **Paymob vs Fawry** (Stripe does not fully serve EGP). Left as a flagged decision.
- **Security-sensitive** → mandatory `security-auditor` gate (payments, webhooks, secrets). Blocked by **P9-05**.
