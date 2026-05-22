# Streak freeze, timed events & weekly challenges

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 5 — streak-freeze item + recurring timed events / weekly challenges layered on missions & leagues.
- **Labels:** `gamification`, `backend`, `frontend`, `habit`
- **Requirements:** FR-GM-9 (streak freeze, timed events, weekly challenges — SRS §4.6); extends FR-GM-2, FR-GM-5, FR-GM-6

## Description
As a student, I want a streak freeze to protect my hard-won streak, plus limited-time events and weekly challenges to chase, so that I have both safety nets and fresh goals that keep the daily habit alive.

> **Why this story exists:** the barrier-to-entry strategy's Daily Habit System explicitly lists **streak freeze**, **timed events**, and **weekly challenges** — none of which have stories or FRs today. It also resolves the open streak-grace dial flagged in `P4-03` / BRD §10 #4. Closes the secondary part of gap 3a-5 and gap 3a-6.

## Acceptance Criteria
- **Streak freeze:** a student can hold a limited number of freezes (earned and/or purchasable with XP/coins per the economy decision); a freeze auto-consumes to preserve the streak on a missed qualifying day. This resolves the `P4-03` open "grace-period" question — `P4-03` and this story must agree on one rule.
- **Timed events:** a scheduled, limited-window event (e.g. weekend XP boost, themed challenge) is configurable with start/end, eligibility, and rewards; it surfaces in the UI with a countdown and ends cleanly.
- **Weekly challenges:** a recurring weekly goal (distinct from daily missions `P4-06` and leagues `P4-07`) with progress tracking and a reward on completion.
- All three emit/consume `P4-01` domain events and grant rewards through the existing XP/badge engine — no parallel reward path.
- Mechanics dials (freeze count, event cadence, challenge targets) are config-driven so they can be tuned without a deploy.

## Notes
- **Resolves open question:** drive BRD §10 #4 (streak grace / hearts regen / streak-freeze) to closure as part of this story; update `P4-03` Notes once the rule is fixed.
- Depends on: `P4-01` (events), `P4-02` (XP/economy), `P4-03` (streaks), `P4-06` (missions), `P4-07` (leagues). Pairs with `P4-09` (the nudge tells the child a freeze is available).
- Closes gap **3a-5 (secondary)** and **3a-6**.
