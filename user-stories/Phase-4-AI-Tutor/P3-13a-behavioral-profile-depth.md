# Behavioral profile depth — new derivations (grit / time-of-day) — BACKLOG

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor *(BACKLOG — deferred, not scheduled)*
- **Epic:** Adaptive Learning Profile (BE2/BE7 moat)
- **Issue type:** Story (enrichment of P3-13)
- **Story Points:** ~5 (estimate) — new profile derivations + tests + security review.
- **Labels:** `backend`, `learning`, `adaptive`, `backlog`
- **Requirements:** FR-AD-5

## Status: BACKLOG (lead decision 2026-06-18)
Recorded per rule #9 (agreed direction → story before build), but **deferred**. The current "as his level" enrichment (P5-09a + P3-14a) uses only the **5 existing** `DerivedProfile` dimensions. This story would extend P3-13 with NEW behavioral derivations the recommendation engine + Lexi could then consume. **Do not build until the lead schedules it.**

## Description
As the adaptive system, I want richer behavioral signals about each child — beyond the current 5 — so recommendations and Lexi can adapt to *how* they learn at a deeper level.

## Candidate new derivations (to refine when scheduled)
- **Persistence / grit proxy** — e.g. from hint-request rate + retry-after-wrong behaviour (does the child push through hard items or bail?).
- **Time-of-day signal** — when the child learns best / most accurately (depends on richer event capture; see **P5-03** analytics events).
- **Mastery trajectory** — rate-of-improvement per skill over time (not just current mastery %).
- **Motivation style** — engagement response to streaks/badges (needs Gamification signals via a seam).

## Acceptance Criteria (draft — to finalize at scheduling)
- New dimensions are derived in `StudentProfileEngine`, exposed on `DerivedProfile`, min-sample/confidence-guarded, cold-start safe.
- Each is deterministic + explainable; covered by `StudentProfileEngine` tests.
- Downstream consumers (P5-09a engine, P3-14a Lexi) opt in deliberately (separate follow-up edits).

## Notes
- Brief: [../../docs/briefs/recommendations-as-his-level-enrichment.md](../../docs/briefs/recommendations-as-his-level-enrichment.md) (OQ-1). Likely depends on **P5-03** (analytics event capture) for the truer time-of-day / engagement signals — the current `AttentionSpanMinutes` is a v1 proxy.
- When scheduled: run the full pipeline (analyzer → planner → backend-feature → security-auditor → reviewer).
