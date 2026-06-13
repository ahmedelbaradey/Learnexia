# Many notification types, few sends (arbitration + global daily budget)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 5 — cross-category priority + global per-child push budget + per-type cooldowns.
- **Labels:** `notifications`, `backend`, `habit`, `anti-spam`
- **Requirements:** FR-GM-8.

## Description
As a parent (and for the child's long-term retention), I want the system to send **only the most important** notification when several could fire, so that adding many notification types never turns into spam that gets push disabled — the fastest way to lose a habit channel.

> **Why this story exists:** today `ReengagementEvaluator` enforces a **per-category** daily cap + quiet hours only. As P9-05/P9-06 multiply the catalog to ~16 types, a child could receive many pushes in a day with no cross-category arbitration. "Many TYPES, few SENDS" must be enforced centrally.

## Acceptance Criteria
- A **global per-child daily push budget** (parent-configurable, conservative default) caps total pushes/day across **all** categories — in addition to per-category caps.
- A **priority ordering** decides which nudge wins the slot when several are eligible in the same window (e.g. streak-danger > daily-mission > comeback > milestone/league/level > weekly-recap); priority is config-driven and tunable without deploy (consistent with P4-11's config approach).
- **Per-type cooldowns** prevent the same type from repeating too often (e.g. league-change at most once/day, recap once/week).
- **Quiet hours** and the existing **Redis dedupe** continue to apply; in-app inbox rows are still always written (only *push* is rationed — the inbox is unrationed).
- When a nudge is suppressed by arbitration, the reason is logged (priority-lost / budget-exhausted / cooldown) for tuning, and the suppressed item still lands in the inbox.
- Per-type send/open + suppression analytics (P5-03 seam) so the team can later **kill the types that don't drive returns**.

## Notes
- Extends the **existing** `ReengagementEvaluator` (pure, unit-testable) + dispatcher — do not fork them. Likely adds a small arbitration service over the evaluator.
- This is the counterweight story for the whole phase — P9-05/P9-06 should not ship to production push without it.
- `analyzer` + `planner` first.
