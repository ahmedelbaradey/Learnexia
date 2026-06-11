# New habit-loop notifications (streak milestones, weekly challenge, weekly recap)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 5 — new events + templates + handlers for milestone/challenge/recap nudges.
- **Labels:** `notifications`, `backend`, `gamification`, `habit`
- **Requirements:** FR-GM-8.

## Description
As a student, I want to be celebrated at milestones and reminded of time-boxed goals — "🔥 7 أيام متواصلة!", "تحدّي الأسبوع نص الطريق", "إنجازك الأسبوع ده: 320 XP و 5 مهارات" — so that there's always a fresh reason to come back, not just the daily streak.

> **Why this story exists:** these are genuinely new categories — no event/template exists today. They round out the habit loop beyond the merged set (streak-at-risk, mission, badge, level, league).

## Acceptance Criteria
- **Streak milestones** — celebrate day **3 / 7 / 14 / 30** (and configurable thresholds): "🔥 7 أيام متواصلة! إنت بطل" → new event/trigger off the streak state (distinct from `StreakAtRisk`), one-time per threshold (dedupe), Achievement category.
- **Weekly challenge lifecycle** (P4-11 weekly challenges) — *started*, *halfway*, *ending-soon (countdown)*: new events/templates; ending-soon is the highest-value of the three.
- **Weekly recap** — once per week, a short personalized summary: "إنجازك الأسبوع ده: {xp} XP و {skills} مهارات 🌟" with a deep link to progress (P9-02). Sourced from gamification totals (and the P5-01 weekly-report data when that ships — degrade gracefully until then).
- All copy Arabic-first, child-safe, encouraging, en fallback, personalized (name, streak length, XP, skill count) via `ReengagementCopyTemplates`.
- Each type is individually parent-toggleable (P9-04) and subject to P9-07 arbitration; sends/opens logged per type (P5-03 seam).
- New `Shared.Contracts` integration events follow the existing one-way module-isolation seam (Gamification publishes, Notifications consumes) — no cross-module FKs.

## Notes
- **Spaced-repetition "time for a quick review" nudge is intentionally out of this story** — it depends on P3-10 (spaced repetition, not built); see **P9-09**.
- Depends on P4-03 (streak), P4-11 (weekly challenges/timed events). Feeds P5-03.
- New events are a `Shared.Contracts` change — serialize per PARALLELISM.md.
- `analyzer` + `planner` first.
