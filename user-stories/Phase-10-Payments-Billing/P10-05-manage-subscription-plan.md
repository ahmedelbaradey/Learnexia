# Manage subscription plan (Free / Premium)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — plan model + change rules + benefits display.
- **Labels:** `billing`, `subscription`, `backend`, `frontend`
- **Requirements:** FR-PAY-1 *(new — Phase 10)*

## Description
As a parent, I want to choose and change my plan (Free or Premium), so that I control how much AI help my children get.

## Acceptance Criteria
- A parent can view the current plan and compare **Free vs Premium** benefits (monthly energy, daily cap).
- The benefits view shows **both pricing options** and the annual saving: **Monthly = 199 EGP** and **Annual = 1990 EGP** (≈ 10 months' price, saves ~2 months). Prices are config-driven via P10-12 (`subscription.monthlyPriceEgp` / `subscription.annualPriceEgp`), never hard-coded.
- A parent selects a **billing period {Monthly | Annual}** when subscribing or when switching cadence. Switching cadence takes effect at the next renewal (not mid-cycle).
- A parent can **upgrade** (takes effect immediately on successful payment; proration — open question) and **downgrade** (takes effect at the next cycle).
- The plan applies to **all the parent's children** (family plan) — per-child plans are out of scope (flag if wanted).
- The active plan is the **source of truth** for the monthly grant (P10-02).
- Premium activation requires a successful payment (P10-06); without it the parent stays on Free.

## Notes
- **Billing cadence is a dimension on the existing subscription** — not a new plan tier. `Free` / `Premium` remain the only plan codes; `BillingPeriod {Monthly, Annual}` is an additional field on `Subscription`.
- The **monthly credit grant (Premium 5000/mo) is unchanged regardless of billing cadence**. Annual subscribers receive the same monthly grant as monthly subscribers; cadence only affects *when payment is charged*, not credit delivery.
- **Parent-app / parent-account surface only** — never shown in the student app. Relates to the existing **P2-12** parent "plan & billing" settings tab. Blocked by **P10-01**.
