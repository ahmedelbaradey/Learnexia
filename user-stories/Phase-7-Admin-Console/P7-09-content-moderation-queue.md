# Content moderation queue & review actions

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Content Moderation & Governance
- **Issue type:** Story
- **Story Points:** 8 — cross-cutting queue fed by multiple modules via contracts, plus stateful review actions and admin-only access.
- **Labels:** `admin`, `moderation`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-AI-4 (AI safety), FR-CI-1 (curriculum ingestion)

## Description
As an admin, I want a moderation queue where I can review, approve, reject, or flag AI-generated and uploaded content, so that no unsafe or low-quality material reaches children and the human-in-the-loop layer on top of the AI Safety Layer is enforced.

## Acceptance Criteria
- A paginated moderation queue lists items pending review with their source (AI tutor output, curriculum upload), submitter, content reference, and current status (`Pending`, `Approved`, `Rejected`, `Flagged`).
- An admin can open an item to see its content/preview plus any automated signal (e.g. the P3-02 Safety Layer verdict) and then **approve**, **reject** (with a required reason), or **flag** it for escalation.
- A review action transitions the item's status, records the reviewing admin + timestamp + reason, and is emitted as an event consumed by the audit log (P7-12).
- The queue is filterable by status, source, subject/grade, and date, and is searchable by content reference.
- Only admins can view or act on the queue; non-admin → 403/redirect.
- Items are pulled from other modules **via Shared.Contracts events/references — no cross-module FK**; the moderation record itself derives from `FullAuditedEntity`.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P3-02 (AI Safety Layer verdicts), BL-01 (curriculum uploads), P7-12 (audit log is written by review actions).
- For P7-09: moderates content from the AI tutor (P3-02 safety layer) and curriculum uploads (BL-01). The Safety Layer is automated; this story adds the human-in-the-loop review on top of it. Child data is sensitive — surface only what the admin needs to decide.
