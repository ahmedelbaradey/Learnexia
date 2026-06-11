# Light up the gamification events we already emit (level up, league, freeze, timed events)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 3 — wire already-published integration events to nudge handlers + templates.
- **Labels:** `notifications`, `backend`, `gamification`, `habit`
- **Requirements:** FR-GM-8.

## Description
As a student, I want to be notified the moment something good happens — I level up, I move up a league, my streak freeze saves me, a timed event starts — so that every win pulls me back into the app.

> **Why this story exists:** the Gamification module **already publishes** `StudentLeveledUpIntegrationEvent`, `LeagueTierChangedIntegrationEvent`, `StreakFreezeConsumedIntegrationEvent`, `TimedEventStartedIntegrationEvent`, `TimedEventEndedIntegrationEvent`, and copy templates already exist for `LEVELED_UP` and `LEAGUE_TIER_CHANGED`. But there are **no integration-event handlers** wiring them into the `NudgeDispatcher` — so these events fire and nothing notifies. This story closes that wiring gap on the existing pattern (`Reengagement/*IntegrationEventHandler`).

## Acceptance Criteria
- New re-engagement handlers route through the **existing** `NudgeDispatcher` + `ReengagementEvaluator` (prefs, quiet hours, cap, Redis dedupe) for:
  - **Level up** (`StudentLeveledUp`) → Achievement / `LEVELED_UP` (template exists).
  - **League tier changed** (`LeagueTierChanged`) → Achievement / `LEAGUE_TIER_CHANGED` (template exists); copy distinguishes promotion ("🏆 ارتقيت لدوري أعلى!") from a neutral move.
  - **Streak freeze used** (`StreakFreezeConsumed`) → new template: "أنقذنا سلسلتك! ❄️ ارجع النهاردة تكمّل" / en fallback.
  - **Timed event started / last-day** (`TimedEventStarted` / `TimedEventEnded` → derive a "last-day" reminder) → new templates.
- All new copy is Arabic-first, child-safe, encouraging (celebration > guilt), with en fallback, via `ReengagementCopyTemplates`.
- Each new notification type is individually parent-toggleable (category mapping consistent with P9-04) and counts toward the arbitration budget defined in P9-07.
- Every send/open is logged per type (P5-03 seam) so each type's return-rate is measurable.

## Notes
- **Builds on P4-09 / P4-10 / P4-11.** Pure additive handlers + templates; no new events needed (they already exist in `Shared.Contracts`).
- Pairs with P9-07 (so newly-lit types don't blow the daily budget).
- `analyzer` + `planner` first.
