# Grant monthly energy per plan

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — scheduled grant + expiry + idempotency across plan tiers.
- **Labels:** `billing`, `credits`, `backend`
- **Requirements:** FR-CREDIT-2 *(new — Phase 10)*

## Description
As a parent on a plan, I want each of my children to receive their monthly energy automatically, so that they can use the AI Helper without me topping up every time.

## Acceptance Criteria
- A scheduled job grants each active child their plan's monthly allotment on the billing-cycle date — **Free = 100**, **Premium = 5000** (config-driven, see P10-11).
- Granted credits are added to `GrantedBalance` and stamped with an **expiry = end of the current cycle**.
- At cycle rollover, any unspent `GrantedBalance` **expires** (an `Expire` transaction); `PurchasedBalance` is untouched.
- Re-running the grant for the same child + cycle is **idempotent** (no double-grant).
- A plan change mid-cycle affects the **next** grant, not the current one (proration — open question).
- **The monthly credit grant (Premium 5000/mo) is unchanged regardless of billing cadence.** Annual subscribers receive the same monthly grant as monthly subscribers. `BillingPeriod` (Monthly vs Annual) only controls when payment is charged — it does not change the credit delivery schedule or amount. The grant job must not branch on `BillingPeriod`.

## Notes
- Runs on **Hangfire** (per the infra decision). Blocked by **P10-01** and **P10-05** (plan is the source of the allotment).
- Grant amounts are admin-configurable (**P10-11** / **P10-12** GlobalSettings).
