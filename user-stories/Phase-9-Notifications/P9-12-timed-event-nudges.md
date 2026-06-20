# Pull me into the limited-time event (timed-event nudges)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 3 — timed-event lifecycle nudges over the P4-12 participation model.
- **Labels:** `notifications`, `backend`, `habit`, `gamification-driven`
- **Requirements:** FR-GM-8; FR-GM-9 (timed events — SRS §4.6). **Depends on P4-12** (timed-event participation + eligibility).

## Description
As a student, I want to hear when a limited-time event goes live, when I'm making progress, and when I'm close but the clock is running out — "🔥 تحدي نهاية الأسبوع شغّال! اكسب نقاط مضاعفة" / "باقي خطوة وتخلّص التحدي ⏳" — so that the timed-event mechanic actually brings me back **during the window** instead of passing me by.

> **Why this story exists:** timed events (P4-11) and their started/ended integration events already exist, but **no nudge fires** for them — the event carries no recipient, so the habit loop never closes. P4-12 adds the per-child participation model + eligibility queries this story consumes.

## Acceptance Criteria
- **Event-live ("join") nudge** fires when a timed event goes active — to **scope-eligible students only** (via the P4-12 eligibility query), **never a blind all-active blast** and **never a per-child fan-out materialized up front** (recruitment cohort computed on demand at send time).
- **Progress nudge** fires to a *participant* on a meaningful progress milestone (e.g. halfway).
- **Ending-soon nudge** fires to a *participant who is close but incomplete* before the window closes.
- **Completion nudge** fires when a participant completes the event.
- Uses the existing **TimedEvent** category (already in the P9-04 parent catalog); copy Arabic-first, child-safe, **never-shaming**, en fallback, personalized (name, event, countdown/remaining); deep-links to the event surface (P9-02).
- Subject to **P9-07** arbitration + parent toggle + the global push budget; sends/suppresses/opens captured by the **P9-11** analytics sink.

## Notes
- **Depends on P4-12** — `TimedEventParticipation` + lifecycle + eligibility/participant queries + the per-student progress/completion integration events. Notifications **consumes** them (module isolation rule 1, no Gamification.Domain reference).
- **Recipient model (lead decision 2026-06-20):** participation-driven — the "join" nudge targets the **scope-eligible** cohort (not all active students); progress/ending/completion nudges target **participants**. No upfront per-child fan-out.
- Kickoff reuses `TimedEventStartedIntegrationEvent`; the progress/ending/completion nudges consume the new P4-12 per-student events. **analyzer + planner first.**
