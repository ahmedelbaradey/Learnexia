# Admin: configure plans, grants & action costs

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 3 — config CRUD + audit + validation; admin surface.
- **Labels:** `billing`, `admin`, `backend`
- **Requirements:** FR-PAY-6 *(new — Phase 9)*

## Description
As an admin, I want to configure plan prices, monthly grants, daily caps, pack size, and per-action energy costs, so that we can tune the economy without a deploy.

## Acceptance Criteria
- Admin can edit: **plan price** (199 EGP), **monthly grant** (Free 300 / Premium 3000), **daily soft cap** (20 / 150), **pack size/price** (500), and **per-action costs** (hint=1, explain-mistake=3, deep-explanation=5, practice-generation=5).
- Changes are **versioned + audited** (who/when/old→new) and take effect from the **next cycle / next request** — never retroactively re-charge a past action.
- **Validation** rejects nonsensical values (negative costs, grant < daily cap, zero pack size).
- Admin-only, behind the P7 admin console.

## Notes
- Costs are consumed by **P9-02 / P9-03 / P9-04**. Admin-only per SRS §3 (no teacher role). Blocked by **P9-01**.
