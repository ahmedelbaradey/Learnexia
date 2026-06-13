# See my notifications in an in-app inbox

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 3 — inbox list screen + unread badge + read-state, on the existing inbox API.
- **Labels:** `notifications`, `frontend`, `habit`, `inbox`
- **Requirements:** FR-GM-8; business-gap-analysis Gap A.

## Description
As a student, I want an in-app inbox that shows my recent notifications with what's unread, so that nudges I miss (push off, quiet hours, offline) are never lost — the inbox is the always-on fallback channel.

## Acceptance Criteria
- A notifications inbox screen lists recent items via the existing `GET /api/Inbox/Me` (title, body, category icon, relative time), newest first, paginated/scrollable.
- An **unread badge count** appears on the inbox entry point (tab/bell) and updates after reads.
- Tapping an item marks it read (`MarkRead`) and deep-links to its destination (reuses P9-02 routing); a **"mark all read"** action calls `MarkAllRead`.
- Empty state is encouraging and child-friendly (never a blank screen), ar + en, full RTL.
- The inbox is the documented **degrade target**: items written when push is suppressed (quiet hours, channel off, permission denied) still appear here — because the backend `NudgeDispatcher` always writes the inbox row.
- Loading/error states use the app's standard patterns; no PII beyond the child's own items.

## Notes
- Pure consumer of existing API (`InboxController`: list / mark-read / mark-all-read) — **no new backend**. Independent of P9-01 (works even if push is denied).
- Visual style follows the design-system; category icons map to the catalog (streak/mission/badge/league/event/recap).
- `analyzer` + `planner` first.
