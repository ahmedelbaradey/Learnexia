# P3-13a (backend) — Behavioral profile depth (new derivations) — BACKLOG

> Story: [../../../user-stories/Phase-4-AI-Tutor/P3-13a-behavioral-profile-depth.md](../../../user-stories/Phase-4-AI-Tutor/P3-13a-behavioral-profile-depth.md)
> Brief: [../../../docs/briefs/recommendations-as-his-level-enrichment.md](../../../docs/briefs/recommendations-as-his-level-enrichment.md) (OQ-1)
> Phase 4 · Module: **Learning** (`StudentProfileEngine` / `DerivedProfile`).

## Status: BACKLOG — deferred (lead decision 2026-06-18). DO NOT build until scheduled.
Recorded per rule #9. The current enrichment (P5-09a + P3-14a) uses only the 5 existing `DerivedProfile` dimensions; this extends P3-13 with NEW behavioral derivations. **Decompose into full tasks when the lead schedules it** (run analyzer → planner first).

## Candidate scope (to refine at scheduling)
- Persistence/grit proxy (hint-rate + retry-after-wrong); time-of-day signal (likely needs **P5-03** analytics events — current `AttentionSpanMinutes` is a v1 proxy); mastery trajectory (rate-of-improvement); motivation style (Gamification engagement, via a seam).
- Each: derived in `StudentProfileEngine`, exposed on `DerivedProfile`, min-sample/confidence-guarded, cold-start safe, deterministic + tested.
- Downstream opt-in (P5-09a engine, P3-14a Lexi) = separate follow-up edits, not part of this story.

## Notes
- Likely depends on **P5-03** (analytics event capture) for the truer time-of-day / engagement signals.
- Mandatory security-auditor when built (child behavioral data).
