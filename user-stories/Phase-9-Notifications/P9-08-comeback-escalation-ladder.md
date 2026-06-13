# Win me back with the right message at the right time (comeback escalation ladder)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 3 — escalating lapse win-back sequence extending the single P4-09 nudge.
- **Labels:** `notifications`, `backend`, `habit`, `churn-recovery`
- **Requirements:** FR-GM-8; business-gap-analysis **Gap L** (win-back has no escalation ladder).

## Description
As a lapsed student, I want the comeback messages to change as more days pass — a gentle nudge first, then a stronger pull, then a fresh-start invitation — so that the win-back fits how long I've been away instead of repeating the same line.

> **Why this story exists:** P4-09 ships a **single** `LapseWinBack` nudge ("وحشتنا! ارجع اليوم"). Mature retention loops escalate by idle-days. This extends the existing handler, it does not replace it.

## Acceptance Criteria
- The lapse win-back evaluates **idle-day tiers** and selects copy accordingly:
  - **Day ~2** — gentle: "وحشنا تقدمك يا {name}، جاهز لتحدي جديد؟"
  - **Day ~5** — stronger / streak-repair framing: "سلسلتك مستنياك — ارجع نكمّلها مع بعض 💪" *(a true streak-**repair** mechanic — restore a lost streak — depends on the reward-economy decision (business-gap Gap C) and is out of scope here; copy only until then).*
  - **Day ~14** — fresh-start: "أسبوع جديد، شجرة مهارات جديدة 🌱 يلا نبدأ من جديد"
- Each tier fires at most once (dedupe); thresholds are config-driven/tunable.
- Copy Arabic-first, child-safe, **never shaming** the absence, en fallback, personalized (name).
- Subject to P9-07 arbitration + parent toggle (LapseWinBack category); sends/opens logged per tier (P5-03 seam) so the team can see which tier actually wins kids back.

## Notes
- Extends the existing `LapseWinBackIntegrationEventHandler` + `LapseWinBack` templates. Streak-repair *action* is flagged as economy-dependent (Gap C) — this story delivers the **messaging ladder** only.
- `analyzer` + `planner` first.
