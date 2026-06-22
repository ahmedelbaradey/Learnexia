# Complete notification-suppression analytics capture

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications
- **Epic:** Notifications & Re-engagement
- **Issue type:** Story (Technical Enabler)
- **Story Points:** 3
- **Labels:** `notifications`, `analytics`, `backend`, `observability`
- **Requirements:** FR-PA-3 (analytics), NFR-3 (observability)
- **Status:** Backlog. Closes the P9-11 deferred follow-up + the P6-04 triage item "notification suppression metric".

## Description
As an operator/admin, I want **every** notification that is held back to be recorded as a `NotificationSuppressed` analytics event with an accurate reason, so that the admin notification-analytics (P9-11 / P7-10) shows the true suppression rate and *why* nudges are not reaching children — not just the subset that the push arbiter denies.

> **Why this exists:** today `NudgeDispatcher.PublishSuppressedAsync` emits `NotificationSuppressedIntegrationEvent` **only** when the P9-07 push **arbiter** denies the push channel (`GlobalBudgetExhausted` / `Cooldown` / `PriorityLost`). Other suppression paths are **invisible** to analytics: (1) **per-child push pref OFF** — handlers set `ShouldPush = prefs.Push`, so when it's `false` the dispatcher's `if (message.ShouldPush)` is skipped and the arbiter is never consulted; (2) **no active device tokens** for the recipient; (3) **dedupe** — a nudge skipped by `ReengagementDedupeStore` before the dispatcher even runs. The suppression metric therefore **undercounts**, and the admin can't see pref-driven or dedupe-driven suppression.

## Acceptance Criteria
- Every distinct suppression path emits exactly one `NotificationSuppressed` analytics event with a stable, accurate `SuppressionReason`:
  - push **pref OFF** (`PushPrefOff`), **no device tokens** (`NoDeviceTokens`), **dedupe** (`Deduped`), plus the existing arbiter reasons (`GlobalBudgetExhausted`, `Cooldown`, `PriorityLost`).
- Emission is **fail-soft** — it must NEVER block dispatch or the in-app inbox write (mirror the existing `PublishSuppressedAsync`).
- **No double-counting:** a nudge that is delivered in-app but only has its *push* channel suppressed records one suppression (push), consistent with current semantics; a fully-deduped nudge records one (dedupe) and no dispatch.
- The Analytics consumer (`NotificationSuppressedEventHandler`) + the admin endpoint (`GET /api/Admin/Analytics/notifications`, P9-11) reflect the new reasons (breakdown by reason).
- Reasons are documented (a stable enum/string set) so the FE/admin can label them.

## Notes
- **Backend only.** Notifications emits; Analytics already consumes (`NotificationSuppressedEventHandler` + the `SuppressionReason` facet on `ActivityEvent` from P9-11). The admin dashboard already renders suppression counts — new reasons appear automatically (a thin FE legend update is optional, FE-lead).
- Module isolation via `Shared.Contracts.Notifications.NotificationSuppressedIntegrationEvent` (already exists). No new module, likely no migration (the `SuppressionReason` facet column already exists from P9-11).
- Decide the suppression-reason taxonomy up front (pref-off / no-device / dedupe / budget / cooldown / priority) so reasons are stable for analytics.
