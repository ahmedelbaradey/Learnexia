# Serve realtime gamification state from Redis

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Technical Enabler
- **Story Points:** 5 — Redis-backed read model for XP/streak/leaderboard + write-through to Postgres ledger + reconciliation.
- **Labels:** `gamification`, `backend`, `performance`, `infra`
- **Requirements:** FR-GM-1, FR-GM-7, NFR-1

## Description
As a student, I want my XP, streak, and league standing to update instantly, so that the feedback loop feels live (the dopamine loop the strategy depends on).

> **Why this story exists:** the barrier-to-entry strategy is emphatic that XP / streaks / leaderboards must be **Redis-backed realtime**. Redis is provisioned (`P1-06`) and used for token blacklist (`P1-02`), but no gamification story specifies the realtime path — as written, gamification reads/writes Postgres synchronously. Closes gap 3a-4 and the leaderboard-at-scale contradiction (3b-4).

## Acceptance Criteria
- XP totals, current streak counters, and league leaderboards are read from **Redis** in the hot path; reads meet **NFR-1 (<500ms)** under the target concurrent load.
- **Postgres remains the durable ledger** — every XP/streak mutation is written through to Postgres; Redis is a derived cache, never the source of truth.
- Leaderboards use a Redis sorted-set (or equivalent) so ranking is O(log n), not a Postgres scan, for `P4-07` leagues.
- A **reconciliation / rebuild** path can reconstruct Redis state from the Postgres ledger after cache loss, with no permanent data loss.
- Updates are driven by `P4-01` domain events; concurrent updates to the same student are correct (no lost XP) under contention.

## Notes
- Depends on: `P1-06` (Redis), `P4-01` (events), `P4-02` (XP), `P4-03` (streaks), `P4-07` (leagues). Tie performance assertions to `P6-01`.
- Refactor/enabler — extends existing gamification stories' read path rather than adding new player-facing mechanics.
- Closes gaps **3a-4** and **3b-4**.
