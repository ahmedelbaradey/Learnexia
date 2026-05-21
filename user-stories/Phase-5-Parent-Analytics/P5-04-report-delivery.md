# Deliver reports via notifications

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics (Week 8)
- **Epic:** Parent & Analytics
- **Issue type:** Story
- **Story Points:** 2 — reuse existing Notifications module to deliver the generated report.
- **Labels:** `parent`, `notifications`, `backend`
- **Requirements:** FR-PA-1

## Description
As a parent, I want to be notified when my child's weekly report is ready, so that I actually read it instead of having to remember to check.

## Acceptance Criteria
- When a weekly report is generated, then the parent receives a notification via the Notifications module.
- The notification links to/opens the report.
- Delivery respects the parent-child linkage and only notifies linked parents.
- Failed delivery is retried and logged.

## Notes
- Covers B6.4 — reuses the existing Notifications module (SRS §7). Blocked by P5-01.
