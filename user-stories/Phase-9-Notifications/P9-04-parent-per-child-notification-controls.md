# Parent controls each child's notifications (per-child, quiet hours, cap)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 5 — parent FE for the existing per-child re-engagement preference API.
- **Labels:** `notifications`, `frontend`, `parent`, `habit`, `consent`
- **Requirements:** FR-GM-8 (parent is consent authority).

## Description
As a parent, I want to control notifications **per child** — which categories are on, quiet hours, and a daily cap — so that the nudges that build my child's habit never become spam and stay within hours I approve.

> **Why this story exists:** the backend already exposes per-child re-engagement preferences (`PreferencesController` child-reengagement endpoints: per-category toggle, quiet hours, daily cap; `GetChildReengagementPreferences` / `UpdateChildReengagementPreferences`). The current FE `NotificationsPanel` only edits the **account-level** 4-category × email/push grid (P2-12a) — there is no per-child re-engagement UI. This adds it.

## Acceptance Criteria
- In parent settings (per active child via the child switcher), the parent sees every notification **category** (the catalog: streak danger, comeback, achievement, daily mission, streak milestone, league, weekly challenge, timed event, weekly recap) with an individual on/off toggle.
- The parent sets **quiet hours** (start/end, timezone-aware) and a **hard daily cap** per child; defaults are conservative/COPPA-appropriate and match the backend defaults.
- Writes are parent-only and scoped to that child; optimistic update with rollback on error (consistent with the existing panel pattern).
- Copy explains each category in parent-facing language, ar + en, full RTL; a note clarifies "many notification types, few actual sends — we pick the most important and respect your quiet hours."
- Changing prefs takes effect on the next evaluation (no client caching staleness beyond the standard query invalidation).

## Notes
- Consumes existing `PreferencesController` per-child endpoints — **no new backend** (P9-07 may add the global-cap/priority knobs; if so this screen surfaces them then).
- Extends, does not replace, the account-level `NotificationsPanel` (P2-12a).
- Child-privacy: parent is consent authority (parent-driven onboarding).
- `analyzer` + `planner` first.
