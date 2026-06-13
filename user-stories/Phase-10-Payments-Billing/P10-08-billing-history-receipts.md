# Billing history & receipts

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 3 — history view + receipt generation.
- **Labels:** `billing`, `frontend`, `backend`
- **Requirements:** FR-PAY-4 *(new — Phase 10)*

## Description
As a parent, I want to see my payment history and download receipts, so that I have a clear record of what I paid.

## Acceptance Criteria
- A parent sees a chronological list of charges (subscription renewals + energy packs) with date, amount, status, and the child (for packs).
- Each successful payment has a **downloadable receipt/invoice** (PDF or printable) carrying the legally required fields (seller, amount, tax, date, reference).
- **Refunds** (P10-09) appear as linked negative entries.
- Only the **owning parent** can view their own history (no cross-account access).

## Notes
- **Parent-app / parent-account surface only** — never shown in the student app. Blocked by **P10-06**. Receipt fields may have EGP/VAT requirements — confirm with finance.
