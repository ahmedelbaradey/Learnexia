# Pay for a subscription (payment provider)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 8 — payment-gateway integration, webhooks, recurring billing, PCI scope; **split candidate** (one-off checkout vs recurring renewal).
- **Labels:** `billing`, `payments`, `backend`, `security`
- **Requirements:** FR-PAY-2 *(new — Phase 10)*

## Description
As a parent, I want to pay for Premium securely, so that my subscription activates and renews on the cadence I chose (monthly or annual).

## Acceptance Criteria
- A **successful payment** activates Premium and triggers the next monthly grant (P10-02).
- A **declined/failed payment** leaves the plan unchanged and shows a clear error (handed to P10-09).
- Payment runs through an **Egypt payment provider** behind an `IPaymentProvider` abstraction; **no card data touches our servers** (hosted/redirect or tokenized — PCI-safe).
- Checkout and recurring renewal support **both billing cadences**: **monthly (199 EGP)** and **annual (1990 EGP)**. The charged amount is determined server-side from the `Subscription.BillingPeriod` field and the P10-12 config keys (`subscription.monthlyPriceEgp` / `subscription.annualPriceEgp`) — never supplied by the client.
- **Recurring billing:** the subscription renews on the chosen cadence (monthly or annual); the parent can cancel (cancel stops future renewals, keeps access to cycle end).
- Provider **webhooks** reconcile provider state with our subscription state **idempotently** (no double-activation).

## Web-checkout-first — COMPLIANCE FLAG
> **The purchase/checkout path is web checkout only.** The parent pays via web (parent dashboard / hosted checkout page). **No native in-app purchase (Apple IAP / Google Play billing) is used.** The native student app does NOT sell digital goods — it links out to / defers purchase to web.
>
> **COMPLIANCE — launch-gating review item:** Apple App Store and Google Play normally **require IAP for digital goods sold inside native apps**. Routing to external web checkout is a deliberate strategy (PWA-first / "reader-app"-style exception). This **must be validated against current App Store and Google Play Store policy before native launch**. This is not a code detail — it is a legal/commercial review item that gates native distribution. Tag the `security-auditor` stage to flag this.

## Notes
- ⚠️ **DECISION:** payment provider — **Paymob vs Fawry** (Stripe does not fully serve EGP). Left as a flagged decision.
- **Security-sensitive** → mandatory `security-auditor` gate (payments, webhooks, secrets, IAP compliance flag). Blocked by **P10-05**.
