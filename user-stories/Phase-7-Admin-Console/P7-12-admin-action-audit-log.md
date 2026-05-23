# Admin action audit log

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Content Moderation & Governance
- **Issue type:** Story
- **Story Points:** 5 — append-only event-fed log with searchable/exportable read side; care needed to guarantee immutability.
- **Labels:** `admin`, `moderation`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), NFR-4 (security/auditability)

## Description
As an admin, I want an immutable log of every admin action recording who did what, when, and the before/after state, so that all governance activity is traceable, accountable, and reviewable for compliance.

## Acceptance Criteria
- Every admin action (moderation decisions, curriculum uploads, role/config changes) writes an audit entry capturing actor, action, target, timestamp, and before/after snapshot.
- The audit log is **append-only and immutable** — no edit or delete path exists in the API or UI.
- A paginated log view is searchable and filterable by actor, action type, target, and date range.
- Admins can export the filtered log (CSV/JSON) for compliance.
- Only admins can read the log; non-admin → 403/redirect; the read UI is view-only.
- Entries are written by **domain/integration events** emitted when admin actions occur (fed by all P7-xx actions), not by direct caller writes; the entry derives from `FullAuditedEntity`.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy). The audit log is written by all P7-xx admin actions (notably P7-09 review actions).
- For P7-12: the log is the system of record for admin governance; immutability is the core invariant. Before/after snapshots must avoid leaking sensitive child data beyond what is needed for accountability.
