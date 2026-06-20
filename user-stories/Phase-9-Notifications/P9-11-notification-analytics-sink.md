# See which nudges actually land (notification analytics sink)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 3 — first-class send/suppress/open capture feeding the P5-03 analytics backbone.
- **Labels:** `notifications`, `analytics`, `backend`, `observability`
- **Requirements:** FR-GM-8; builds on **P5-03** (analytics backbone / `ActivityEvent` stream); feeds **P7-10** / **P7-11** dashboards.

## Description
As the product/admin team, I want every notification's lifecycle — **dispatched**, **suppressed** (and why), **opened** — captured in the analytics pipeline, so that we can see which nudges actually reach kids and which get throttled, and tune the habit-loop catalog with data instead of guesswork.

> **Why this story exists:** Phase-9 notifications (P9-05..08) ship a rich catalog gated by P9-07 arbitration + a global push budget, but **effectiveness is unmeasured** — v1 only writes logs. The team can't see send/suppress/open rates per category, so it can't tell a winning nudge from a muted one. This closes the "first-class notification-analytics sink" gap recorded in the P9-07 backlog.

## Acceptance Criteria
- Each notification lifecycle transition produces a **fail-soft** analytics signal, with no PII (opaque ids + `code` / `category` / `reason` scalars only):
  - **Dispatched** — in-app row written and the per-channel delivery result (which of email/push/in-app actually went out).
  - **Suppressed** — arbitration/global-budget/quiet-hours/cooldown/dedupe blocked the send, **carrying the reason** (the P9-07 suppression reasons).
  - **Opened** — the recipient engaged: inbox read (existing `InboxController` MarkRead) now; push-tap when the P9-02 FE reports it.
- The **Analytics module consumes** these into its existing append-only `ActivityEvent` stream (the P5-03 backbone) — Notifications **emits** `Shared.Contracts` integration events; Analytics is the single sink (module isolation rule 1, no cross-module FK).
- An **admin-readable aggregate** exists: send / suppress / open counts (and open-rate) **by code and category over a date range** — surfaced through the existing analytics read path so P7-10 / P7-11 can chart it.
- Capture is **off the hot path** and **never** blocks or fails a dispatch (fire-and-forget / fail-soft, consistent with the existing `NudgeDispatcher`).

## Notes
- **Sink home = the Analytics module** (lead decision 2026-06-20) — one analytics home, reuse the `ActivityEvent` stream rather than a second metrics table in the `notifications` schema.
- **Opened** is partial in v1: the **read** path (inbox MarkRead) is wired now; **push-tap** open reporting depends on the **P9-02** FE deep-link handler and lands when that ships.
- Supersedes the "v1 logs only / could feed the analytics-module `ActivityEvent` stream" note from **P9-07**. Reuses the fire-and-forget recorder shape proven by `AiUsageRecorder` (P7-11b). **analyzer + planner first.**
