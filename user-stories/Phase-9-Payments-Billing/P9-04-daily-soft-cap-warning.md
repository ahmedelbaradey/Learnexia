# Daily soft cap & low-energy warning

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 3 — daily counter + thresholds + reset; soft-cap semantics.
- **Labels:** `billing`, `credits`, `backend`
- **Requirements:** FR-CREDIT-4 *(new — Phase 9)*

## Description
As a child, I want a friendly warning as I approach my daily energy limit, so that I learn to pace my use without being abruptly cut off.

## Acceptance Criteria
- A **daily soft cap** is tracked per child per day — **Free = 10, Premium = 250** (config-driven) — drawn from the monthly pool.
- **"Soft"** = on reaching the cap the child sees a warning explaining it's part of the **monthly** limit, and may continue while monthly balance remains (a configurable hard-stop is **off** by default).
- A **low-energy warning** fires at a threshold (e.g. ≤ 10% of monthly remaining, or near the daily cap).
- The daily counter resets at the child's **local midnight** (timezone-aware).

## Notes
- Soft cap is **bounded by the monthly pool** (lead-confirmed). Blocked by **P9-01**.
- Warning copy is surfaced by the kid energy UI (**P9-10**).
