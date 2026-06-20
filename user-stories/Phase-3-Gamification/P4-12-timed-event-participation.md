# Track my progress in a limited-time event (timed-event participation)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5) *(story IDs `P4-xx`; built post-MVP alongside Phase 9)*
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 5 — per-child participation, progress & completion layered on the P4-11 timed-event window.
- **Labels:** `gamification`, `backend`, `habit`
- **Requirements:** FR-GM-9 (timed events — SRS §4.6); **extends P4-11**. Pairs with **P9-12** (timed-event nudges).

## Description
As a student, I want my contribution to a live timed event tracked toward its goal — joining when I first take part, watching my progress climb, and getting the reward when I finish — so that a timed event is a real challenge I can **complete**, not just a passive background XP multiplier.

> **Why this story exists:** P4-11 ships timed events as a **platform-wide** XP multiplier with start/end + scope, but there's **no per-child participation, progress, or completion** — so nothing can say who's in, who's close, or who finished, and the timed-event **nudges (P9-12)** have no recipient. This adds the participation model + eligibility/participant read seams that P9-12 consumes.

## Acceptance Criteria
- A **`TimedEventParticipation`** record is created **lazily** when a child first contributes to an active timed event (first qualifying action inside the window) — **not pre-materialized** for every eligible child at event start (no upfront fan-out).
- Participation tracks **progress toward the event target** (accumulated from qualifying actions inside the window) and a **completion state** (in-progress → completed) with timestamps; the lifecycle is scoped to the window and **ends cleanly** at `EndUtc` (no progress after close).
- On completion, the reward is granted **through the existing XP/badge engine — no parallel reward path** (consistent with P4-11).
- An **eligibility query** exposes the **scope-matched cohort** of students who *could* participate (recruitment), and **participant queries** expose current participants + progress — both as cross-module **read seams** (`Shared.Contracts/Gamification`), opaque ids, no PII.
- **Per-student lifecycle integration events** are emitted post-commit, fail-soft, opaque ids only: progress-milestone (e.g. halfway), ending-soon/at-risk (close-but-incomplete near window close), and completion — for downstream consumers (P9-12).
- Mechanics dials (progress target, halfway threshold, ending-soon lead time, eligibility scope) are **config-driven**, tunable without a deploy.

## Notes
- **Recipient model (lead decision 2026-06-20): participation entity + child lifecycle + progress tracking + completion state + eligibility queries.** Explicitly **avoid** a blind active-student blast and **avoid** materializing per-child participation up front.
- Builds on **P4-11** (`TimedEvent` + scope + start/end + `IActiveTimedEventsQuery`). Reuses the cross-module read-seam pattern (`IActiveTimedEventsQuery` / `IStudentXpQuery`). Grants reward through the existing XP/badge engine (no parallel path). Pairs with **P9-12** (nudges consume the eligibility query + per-student events). **analyzer + planner first** — confirm what counts as a "qualifying action" (XP earned in-window vs mission completion) and the per-event target source.
