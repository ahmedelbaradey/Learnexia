# View the parent dashboard

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics (Week 8)
- **Epic:** Parent Dashboard
- **Issue type:** Story
- **Story Points:** 5 — dashboard with report view, weak areas, progress charts, recommendations, and a per-child grade-transition control.
- **Labels:** `frontend`, `parent`, `analytics`
- **Requirements:** FR-PA-1, FR-PA-2

## Description
As a parent, I want a simple dashboard showing each child's weekly report, weak areas, progress, and recommendations — and a control to move a child up a grade — so that I get visibility at a glance and can keep their level current.

## Acceptance Criteria
- The dashboard shows the latest weekly report, weak areas (with severity), progress charts, and recommendations.
- A parent with multiple children can switch between them; each shows only that child's data.
- Per child, the dashboard exposes a **grade-transition** control to change/advance that child's grade, with a confirmation step (calls P5-06).
- After a successful grade transition, the child's curriculum/skill tree reflects the new grade on next load; history (XP, badges, streaks) remains visible.
- The dashboard renders in Arabic (RTL) and English.
- Empty/first-week states are handled gracefully (no charts-with-no-data errors).

## Notes
- Covers F6.1. Blocked by P5-01, P5-02, and P5-06 (grade-transition backend). Parent linkage from P1-04 scopes access.
