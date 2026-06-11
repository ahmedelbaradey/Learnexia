# Tapping a notification takes me to the right place (deep links + foreground + web)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 5 — per-category deep-link routing + foreground presentation + web-PWA push fallback.
- **Labels:** `notifications`, `frontend`, `expo`, `habit`, `deep-link`
- **Requirements:** FR-GM-8; business-gap-analysis Gap A.

## Description
As a student, I want tapping a notification to open the exact screen it's about — today's mission, my streak, the new badge, my league — so that the nudge leads straight to the action that rebuilds the habit, not just the home screen.

## Acceptance Criteria
- Each notification carries a typed payload (the backend `NudgeMessage.DataJson` / `Code`); the app maps **category/code → destination**:
  - Streak danger / streak broken → streak screen with the "do one lesson" CTA.
  - Daily-mission reminder → today's mission.
  - Achievement / badge earned → badge gallery (deep-link to the new badge).
  - Level up → profile/level surface; League change → league screen; Timed event → the event; Weekly recap → progress/recap.
  - Comeback → today's mission (the lowest-friction return action).
- **Foreground** notifications present as an in-app toast/banner (not the OS shade) and still deep-link on tap.
- **Cold start** from a tapped notification routes to the destination after auth/guard resolution (no flash of the wrong tab; respects route guards).
- Unknown/forward-compat codes fall back to opening the in-app inbox (P9-03) rather than erroring.
- **Web PWA fallback:** where native push is unavailable, the app degrades to in-app inbox surfacing; if web-push is configured it registers a service-worker subscription, otherwise it no-ops gracefully (documented).
- Every notification **open** is reported so per-type return-rate is measurable (feeds the P5-03 analytics seam; mark-read happens via P9-03's inbox API).

## Notes
- Depends on **P9-01** (registered receiver) and the existing inbox/`MarkRead` API. Routing must match real routes (`(child)/...` mission/streak/badge/league surfaces; some gamification screens are P4-08 WIP — link to what exists, stub the rest behind a feature flag).
- Child-safe: deep links never expose another child's data; respect active-child context.
- `analyzer` + `planner` first.
