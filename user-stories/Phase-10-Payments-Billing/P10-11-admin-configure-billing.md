# Admin: configure plans, grants & action costs

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 3 — config CRUD + audit + validation; admin surface.
- **Labels:** `billing`, `admin`, `backend`
- **Requirements:** FR-PAY-6 *(new — Phase 10)*

## Description
As an admin, I want to configure plan prices, monthly grants, daily caps, pack size, and per-action energy costs, so that we can tune the economy without a deploy.

## Acceptance Criteria
- Admin can edit: **plan price** (199 EGP), **monthly grant** (Free 100 / Premium 5000), **daily soft cap** (10 / 250), **pack size/price** (1000 credits / $5), and **per-action costs** (hint=1, explain-mistake=3, deep-explanation=5, practice-generation=5).
- **Note:** Grant sizes were recalibrated to the revised credit value (1 credit ≈ 1¢; hint = 1 credit) — they reflect the new economy, not the old.
- These are the **default** config values; all are admin-tunable without a deploy.
- Changes are **versioned + audited** (who/when/old→new) and take effect from the **next cycle / next request** — never retroactively re-charge a past action.
- **Validation** rejects nonsensical values (negative costs, grant < daily cap, zero pack size).
- Admin-only, behind the P7 admin console.

## Notes
- Costs are consumed by **P10-02 / P10-03 / P10-04**. Admin-only per SRS §3 (no teacher role). Blocked by **P10-01**. Grant/cap defaults reflect the revised credit economy (1 credit ≈ 1¢).
