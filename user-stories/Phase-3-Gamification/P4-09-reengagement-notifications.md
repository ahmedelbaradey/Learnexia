# Bring the student back tomorrow (re-engagement notifications)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — Gamification (Week 5)
- **Epic:** Gamification Module
- **Issue type:** Story
- **Story Points:** 5 — notification service + scheduling + streak-at-risk trigger + per-child opt-in & quiet hours; child-data sensitive.
- **Labels:** `gamification`, `notifications`, `backend`, `frontend`, `habit`
- **Requirements:** FR-GM-8 (re-engagement notifications — SRS §4.6)

## Description
As a student (and the parent who controls my account), I want timely reminders to come back — a streak-at-risk nudge, a daily-mission reminder, a "we miss you" message after a lapse — so that learning becomes a daily habit.

> **Why this story exists:** the barrier-to-entry strategy names "make the child come back tomorrow" as *the single most important metric*, but no existing story sends students any re-engagement signal — notifications (`P5-04`) deliver only parent weekly reports. This closes the top gap (BE4 / gap 3a-5).

## Acceptance Criteria
- A re-engagement engine evaluates each active child daily and fires at most one nudge per category per day: **streak-at-risk** (streak active but no qualifying activity yet today), **daily-mission reminder**, **lapse win-back** (no session for N days).
- Triggers are driven by the `P4-01` domain events / streak + mission state — no polling of raw tables in the hot path.
- **Parent controls** per child: enable/disable each category, set **quiet hours**, and a hard daily cap. Defaults are conservative (opt-in friendly) and COPPA-appropriate; the child cannot change them.
- Delivery is channel-abstracted (push / web push / in-app inbox) so the same engine serves native + PWA; channel availability degrades gracefully.
- Notification copy is Arabic-first, child-safe, encouraging (never shaming a missed streak), and localizable.
- Every send/open is logged as an analytics event (`P5-03`) so re-engagement → return-rate impact is measurable.

## Notes
- **Security/privacy:** child-directed messaging — route through `security-auditor` (child-privacy, consent, no PII in payloads). Parent is the consent authority (parent-driven onboarding decision).
- Depends on: `P4-01` (events), `P4-03` (streak state), `P4-06` (missions), `P1-06` (Redis for schedule/dedupe). Feeds: `P5-03` (analytics).
- Reuses the notification delivery seam introduced for `P5-04` (extend, don't duplicate).
- Closes gap **3a-5**; complements `P4-11` (streak freeze gives the child a way to *act* on the nudge).
