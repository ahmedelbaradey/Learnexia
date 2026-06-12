# Manage subscription plan (Free / Premium)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — plan model + change rules + benefits display.
- **Labels:** `billing`, `subscription`, `backend`, `frontend`
- **Requirements:** FR-PAY-1 *(new — Phase 9)*

## Description
As a parent, I want to choose and change my plan (Free or Premium at 199 EGP/month), so that I control how much AI help my children get.

## Acceptance Criteria
- A parent can view the current plan and compare **Free vs Premium** benefits (monthly energy, daily cap).
- A parent can **upgrade** (takes effect immediately; proration — open question) and **downgrade** (takes effect at the next cycle).
- The plan applies to **all the parent's children** (family plan) — per-child plans are out of scope (flag if wanted).
- The active plan is the **source of truth** for the monthly grant (P9-02).
- Premium activation requires a successful payment (P9-06); without it the parent stays on Free.

## Notes
- **Parent-app / parent-account surface only** — never shown in the student app. Relates to the existing **P2-12** parent "plan & billing" settings tab. Blocked by **P9-01**.
