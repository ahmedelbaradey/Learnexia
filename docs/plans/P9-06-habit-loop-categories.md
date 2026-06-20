# Execution Plan — P9-06 New habit-loop notification categories

> Backend-only, **cross-module** wave (Gamification + Parent producers → Notifications consumers, on top of two new `Shared.Contracts` events). Built on the just-merged P9-07/05/08 arbitration subsystem. **No designer / frontend / frontend-e2e-tester stages.** No db-migration expected. Scope is locked to **2 of 3 categories** (streak milestones + weekly recap); weekly-challenge is **deferred** (carried as backlog).

## Source
- **Brief:** `docs/briefs/P9-06-habit-loop-categories.md`
- **Story:** `user-stories/Phase-9-Notifications/P9-06-new-habit-loop-notification-categories.md`
- **Task file:** `tasks/Backend/Phase-9-Notifications/P9-06-BE.md` (BE-1..5)
- **Prior wave (consumer/arbiter/republisher shape):** `docs/briefs/P9-nudge-arbitration.md`
- **Rules:** `CLAUDE.md` (rule-1 module isolation, rule-3 no-UoW, rule-5 ILoggerManager, rule-8 ask-first), `docs/dev/PARALLELISM.md` (Shared.Contracts is a serialized shared-file edit), `docs/dev/CONVENTIONS.md` (Option C — EF only in Infrastructure), ADR-0002 (fail-soft post-commit dispatch).
- **Locked decisions (this plan is built around them — not re-opened):** scope = streak-milestone + weekly-recap (weekly-challenge DEFERRED); producer-side dedupe by construction (no store/column); recap emitted from the existing Parent `WeeklyReportJob` post-upsert (no new Hangfire job) with zero-activity suppression; ride existing `NotificationCategory` (Achievement / WeeklyReport), inbox-only in v1; no db-migration; rule-8 no new pattern.

### Verified facts (read during planning)
- `StudentLeveledUpDomainEventRepublisher.cs` exists at `backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Application/Features/Reengagement/Republishers/` — the milestone republisher mirrors it verbatim.
- `WeeklyReportJob.cs` exists at `backend/src/Modules/Parent/Learnexia.Modules.Parent.Infrastructure/Jobs/` — the recap is emitted from this job's per-child sweep.
- `StreakFreezeConsumedIntegrationEventHandler.cs` exists at `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/IntegrationEventHandlers/Reengagement/` — the 2 new consumers mirror it.
- **`Parent.Application` IS registered in `AddCrossModuleMediatR`** (`backend/src/Host/Learnexia.Host/Extensions/MediatRExtensions.cs:37`). → The recap event published from Parent **will reach** the Notifications handler. **No `MediatRExtensions.cs` edit needed → that shared-file serialization point is NOT triggered by this wave.** (The only serialized shared file here is `Shared.Contracts`.)
- Shared.Contracts Gamification events live at `backend/src/Shared/Learnexia.Shared.Contracts/Gamification/` (e.g. `StreakFreezeConsumedIntegrationEvent.cs`). No `Shared.Contracts/Parent` event folder exists yet — see namespace note in Batch 0.

## Task inventory

| ID | Stack | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| **P9-06-BE-1** | Gamification + Notifications + Shared.Contracts | Streak milestones (day 3/7/14/30, configurable): new `StreakMilestoneReachedIntegrationEvent`, threshold-gated republisher off `StreakAdvancedDomainEvent`, consumer + templates. Dedupe **producer-side by construction**. | 5 | P4-03 (streak ✅), Batch-0 event |
| **P9-06-BE-2** | — | Weekly challenge lifecycle (started/halfway/ending-soon). | 5 | **DEFERRED** — no per-child challenge entity; `TimedEvent` is platform-broadcast with no StudentId + no halfway/ending-soon signal. **Not built. Carried as backlog (see Blockers).** |
| **P9-06-BE-3** | Parent + Notifications + Shared.Contracts | Weekly recap: new `WeeklyRecapReadyIntegrationEvent`, emitted from existing Parent `WeeklyReportJob`/generator post-upsert (no new job); zero-activity suppression; consumer + template w/ deep link. | 4 | P4-02 (XP ✅), P5-01 (WeeklyReport ✅ shipped), Batch-0 event |
| **P9-06-BE-4** | Notifications | Personalized Arabic-first `ReengagementCopyTemplates` entries (+en fallback) for `STREAK_MILESTONE` + `WEEKLY_RECAP` (placeholders: streakLength / xp / skills). | 2 | BE-1, BE-3 (folded into their consumer batch) |
| **P9-06-BE-5** | Gamification / Parent / Notifications | Category mapping (ride existing Achievement / WeeklyReport) + P9-07 arbitration enrolment (automatic via `NudgeDispatcher`) + per-type send analytics (`ILoggerManager` `analytics.reengagement.*`). Inbox-only in v1. | 2 | P9-07 ✅, BE-1, BE-3 |

**Net build (after the deferral):** 1 producer republisher (Gamification, BE-1) + 1 producer emit-point (Parent, BE-3) + 2 consumers + 2 template sets (Notifications, BE-1/BE-3/BE-4) + mapping/arbitration/analytics wiring (BE-5, mostly automatic). 2 new `Shared.Contracts` events.

## Dependency order
1. **Batch 0 — `Shared.Contracts` events** (the 2 event shapes) — both producers and both consumers compile against these. **Must land first** (serialized shared-file edit per PARALLELISM.md).
2. **Producers (Batch 1)** depend only on Batch 0:
   - **1a — Gamification** streak-milestone republisher (consumes existing `StreakAdvancedDomainEvent`; publishes new event). Different module from 1b → **logically parallel**.
   - **1b — Parent** weekly-recap emit from `WeeklyReportJob` + zero-activity suppression (publishes new event). Different module from 1a → **logically parallel**.
3. **Consumers (Batch 2)** depend on Batch 0 (event shapes) and exercise against Batch 1 at runtime — Notifications: 2 consumers + templates + mapping/analytics (BE-4/BE-5 fold in here).
   - Logically Batch 2 can build in parallel with Batch 1 (both only need the Batch-0 contract), but **the lead is serializing all dispatches on one working tree** (see note), so Batch 2 runs **after** Batch 1.

**Producer-side dedupe by construction (no ordering implication, but load-bearing for the reviewer):** `StreakAdvancedDomainEvent` increments by 1 per day-transition, so each threshold (3/7/14/30) is crossed exactly once per streak episode → "fire only when `NewStreak ∈ {3,7,14,30}`" is inherently one-time. `ResetStreakAndStart` raises `NewStreak=1` (below all thresholds) → no spurious milestone. The consumer keeps the existing Redis (child, category, day) dedupe as a duplicate-delivery guard (already in the handler template). **No new dedupe store / column.**

## Execution batches

> **Working-tree note (from the lead):** all batches run on one working tree (branch `feat/P9-06-habit-loop`), so the lead **SERIALIZES the agent dispatches** even where logically parallel. The "logically parallel" tag below records true independence (disjoint modules / files) for correctness; the dispatch order is still 0 → 1a → 1b → 2.

### Batch 0 — Shared.Contracts events (SERIALIZED, FIRST) · `backend-feature`
Single small change — the shared-contract edit both producers and consumers depend on. Per PARALLELISM.md this is a serialized shared-file edit; keep it in its own small commit.
- Add `StreakMilestoneReachedIntegrationEvent(EventId, OccurredOnUtc, StudentId, Milestone)` to `backend/src/Shared/Learnexia.Shared.Contracts/Gamification/`. (Mirror `StreakFreezeConsumedIntegrationEvent.cs` shape — `IIntegrationEvent`, `EventId`/`OccurredOnUtc`.)
- Add `WeeklyRecapReadyIntegrationEvent(EventId, OccurredOnUtc, StudentId, XpEarned, SkillsImproved, WeekStartUtc)`.
  - **Namespace decision (resolve in-batch, low-risk):** the recap is **Parent-emitted**, but no `Shared.Contracts/Parent` events folder exists today (all integration events sit under `Gamification/`). Recommend placing it under `Shared.Contracts/Gamification` (consistent home, matches the story's "Gamification publishes" framing) **or** create a new `Shared.Contracts/Parent` namespace if the team prefers producer-accurate grouping. Either is fine — pick one, note it, stay consistent. Does not affect any other module.
- **No other module touched in this batch.** No `MediatRExtensions.cs` edit (Parent already registered).
- **DoD:** both records compile in `Shared.Contracts`; solution builds; no producer/consumer references added yet.

### Batch 1 — Producers (logically PARALLEL; dispatched serially 1a then 1b) · `backend-feature`
Both depend on Batch 0 only. Disjoint modules → no shared-file contention between them.

**Batch 1a — Gamification streak-milestone republisher (BE-1 producer side):**
- New `StreakMilestoneReachedRepublisher : INotificationHandler<StreakAdvancedDomainEvent>` in `…Gamification.Application/Features/Reengagement/Republishers/` — verbatim copy of `StudentLeveledUpDomainEventRepublisher`'s shape: fail-soft try/catch, `IPublisher.Publish(new StreakMilestoneReachedIntegrationEvent(...))`, auto-registers via the Application assembly MediatR scan.
- Publish **only when `notification.NewStreak`** is in the configured milestone set. Source thresholds via `IGlobalSettingsProvider.GetString("Gamification:Streak:MilestoneDays", "3,7,14,30")` (tunable-without-deploy, matches P9-07 config style) **or** extend `StreakOptions.MilestoneDays` — pick the `IGlobalSettingsProvider` route to match the prior wave.
- **Do NOT touch `StreakService`/`AdvanceStreak`** — milestone decision stays in the republisher, off the commit path (NFR-1).
- **No shared-file edit** (new class auto-registers; Gamification.Application already scanned).
- **DoD:** advancing a streak to a threshold value publishes exactly one integration event; non-threshold advances publish nothing; `ResetStreakAndStart` (NewStreak=1) publishes nothing; republisher never throws into the commit path.

**Batch 1b — Parent weekly-recap emit (BE-3 producer side):**
- In Parent's `WeeklyReportGeneratorService.GenerateAsync` (or the `WeeklyReportJob` per-child loop) — `backend/src/Modules/Parent/Learnexia.Modules.Parent.Infrastructure/Jobs/WeeklyReportJob.cs` — **after the `WeeklyReport` upsert + `SaveChangesAsync`**, publish `WeeklyRecapReadyIntegrationEvent(StudentId=childId, XpEarned, SkillsImproved, WeekStartUtc, OccurredOnUtc=now)` via `IPublisher`, fail-soft per child (the job is already per-child fail-soft).
- **Suppress zero-activity recaps:** skip the publish when `XpEarned == 0 && SkillsImproved == 0` (never-shaming, FR-GM-8). The `WeeklyReport` row is still written; only the nudge is suppressed.
- **No new Hangfire job, no new DB read, no new scheduler.**
- `Parent.Application` is already in `AddCrossModuleMediatR` (verified) → **no shared-file edit; the publish reaches Notifications.**
- **DoD:** running the weekly sweep for a child with activity publishes exactly one recap event with correct Xp/skills/week-start; a zero-activity child publishes nothing; emit is fail-soft per child (one child's failure doesn't abort the sweep).

### Batch 2 — Notifications consumers + templates + mapping (after Batch 1) · `backend-feature`
Depends on Batch 0 (event shapes). Folds in BE-1 consumer side, BE-3 consumer side, BE-4, BE-5. Single module (Notifications).
- `StreakMilestoneReachedIntegrationEventHandler` — copy `StreakFreezeConsumedIntegrationEventHandler`; category **Achievement**; code `STREAK_MILESTONE`; placeholder `("streakLength", ev.Milestone.ToString())`; retain Redis dedupe; route through `NudgeDispatcher` (P9-07 arbitration applies automatically).
- `WeeklyRecapReadyIntegrationEventHandler` — same template; category **WeeklyReport**; code `WEEKLY_RECAP`; placeholders `("xp", ev.XpEarned), ("skills", ev.SkillsImproved)`; deep-link to P9-02 progress.
- `ReengagementCopyTemplates` entries: `STREAK_MILESTONE` + `WEEKLY_RECAP`, ar-EG primary + en-US fallback. Arabic-first, child-safe, celebration > guilt ("🔥 {streakLength} أيام متواصلة!", "إنجازك الأسبوع ده: {xp} XP و {skills} مهارات 🌟").
- **Category mapping (BE-5):** ride existing `Achievement` + `WeeklyReport` enum values — **no new `NotificationCategory` members, no prefs-schema change.** **Inbox-only in v1** (neither category is in the parent-managed push set until P9-04 FE toggles ship) — handler dispatches normally; the dispatcher's gate keeps it inbox-only. Document it.
- **Arbitration enrolment (BE-5):** automatic — `NudgeDispatcher` is the single choke point; no per-handler arbiter wiring.
- **Analytics (BE-5):** per-type send logged via `ILoggerManager` `analytics.reengagement.sent` (existing convention; first-class analytics ingest remains a separate story).
- **Name personalization caveat (carried from prior wave):** `{name}` needs a display-name seam (`IUserLookup` may return locale, not name). If unavailable, drop `{name}` to a generic vocative — flag during build, do not block.
- New handlers auto-register (Notifications.Application already scanned) — **no shared-file edit.**
- **DoD:** each event produces an inbox row with correctly-rendered ar + en copy; push gated by the existing budget/cooldown and inbox-only in v1; re-delivery is absorbed by Redis dedupe; send logged.

## Review gates
Single PR, one branch (`feat/P9-06-habit-loop`). Gates run after the implementation batches, before commit:

1. **`api-tester`** — no new HTTP endpoints; integration tests over the **event → nudge** pipeline (mirror `P4_11_StreakFreezeTimedEvents_IntegrationTests` / the merged P9 suite):
   - Advance a streak to **7** → assert exactly one milestone inbox row (Achievement, `STREAK_MILESTONE`), ar + en copy rendered; advancing to a **non-threshold** day → no milestone row; re-deliver the threshold event → Redis dedupe absorbs it (still one row). (Milestone dedup-by-construction.)
   - Run the weekly sweep for a child with XP → assert exactly one recap inbox row (WeeklyReport, `WEEKLY_RECAP`) with `{xp}`/`{skills}` rendered + deep link; **zero-activity child → no recap row** (suppression).
   - Assert inbox is **always written** and arbitration still holds (P9-07 gate); assert the recap event published from Parent **reaches** the Notifications consumer (guards the cross-module-MediatR risk).
2. **`security-auditor` (light)** — scoped per OQ-R6: child notification data + 2 new cross-module producers. Verify (a) new events carry no PII beyond ids/counts (StudentId, Milestone, Xp, SkillsImproved, WeekStartUtc — all numeric); (b) no new write endpoint / no new auth surface (event-/job-driven, no controller); (c) module isolation holds (one-way event seam, no cross-module FK — rule 1); (d) fail-soft producers never throw into the streak commit path or the weekly job. Critical/High block; not expected to surface any.
3. **`reviewer`** — gate against the brief's ACs + CONVENTIONS.md: Option C (EF only in Infrastructure), fail-soft (ADR-0002), rule-1 isolation, rule-8 no new pattern (republisher/handler/job mirrors confirmed), producer-side dedupe rationale, the documented weekly-challenge **deferral**, inbox-only-v1 carryover. Consumes api-tester + security-auditor results.
4. **`committer`** — only after reviewer PASSES. Branch `feat/P9-06-habit-loop`, conventional scoped message, push + open **one PR** with full description. Update `docs/dev/HANDOFF.md` in the same PR: new config `Gamification:Streak:MilestoneDays` (default `3,7,14,30`); the recap-from-`WeeklyReportJob` decision + zero-activity suppression; the weekly-challenge **deferral** + reason; the inbox-only-v1 carryover for Achievement/WeeklyReport; the chosen recap-event namespace.

## Blockers / prerequisites
- **No blockers to building the locked scope.** All prerequisites are merged: P4-03 streak ✅, P4-02 XP ✅, **P5-01 WeeklyReport ✅ shipped** (migration `20260618070103_AddWeeklyReport`), P9-07 arbitration ✅, `Parent.Application` cross-module MediatR registration ✅ (verified line 37).
- **db-migration: NONE.** Producer-side dedupe (no column), recap reads already-persisted `WeeklyReport` data, both categories already exist in `NotificationCategory`, arbitration state lives in Redis/inbox. **db-migration agent is not dispatched.** (Only trigger would be making Achievement/WeeklyReport parent-toggleable now — explicitly out of scope; that is P9-04 FE's concern.)
- **Shared.Contracts serialization (the one serialization point):** the 2 new events are a shared-file edit → Batch 0 lands first, in its own commit, before producers/consumers. `MediatRExtensions.cs` / `.sln` / `Program.cs` / `Claims` are **NOT** touched by this wave.
- **DEFERRED — weekly-challenge lifecycle nudges (backlog):** no per-child weekly-challenge entity exists; `TimedEvent` is a platform-wide broadcast (`Code`/`StartUtc`/`EndUtc`/`Multiplier`/`Scope`, **no StudentId**, **no halfway / ending-soon signal**). Delivering them honestly needs EITHER (a) a real per-child weekly-challenge feature (new entity + per-child progress + lifecycle events) OR (b) a recipient fan-out seam (active-student-id list + budget-aware bulk dispatch) for platform `TimedEvent`s — both are **separate, larger stories**. **Carry as a follow-up backlog item** (new story `P9-06b`, or fold into the existing deferred timed-event-fanout backlog from the prior wave). **No partial / faked challenge nudge is shipped in v1.** Recommend the lead create the backlog story (per CLAUDE.md "decisions → stories + tasks, ask first").
- **Low-risk in-batch decision:** recap-event namespace (`Shared.Contracts/Gamification` vs new `Shared.Contracts/Parent`) — resolve in Batch 0, note it; does not gate anything.
- **Carry-forward note (not a blocker):** when **P9-04 FE** ships per-type toggles, the inbox-only-v1 decision for Achievement/WeeklyReport flips to parent-controllable push. Track against `tasks/Frontend/student-app/Phase-9-Notifications/P9-04-FE.md`.

## Definition of done
**Per batch:** see the DoD line under each batch above.

**Overall (tied to story acceptance criteria):**
- **Streak milestones (BE-1/BE-4/BE-5):** `StreakMilestoneReachedIntegrationEvent` exists in `Shared.Contracts`; the Gamification republisher publishes it only at configured thresholds (default 3/7/14/30), once per threshold per episode (producer-side dedupe), fail-soft; the Notifications consumer routes it to `NudgeDispatcher` (Achievement, auto-arbitrated), Redis dedupe retained; ar-EG + en-US `STREAK_MILESTONE` copy with `{streakLength}`; inbox row always written, push inbox-only in v1, send logged.
- **Weekly recap (BE-3/BE-4):** `WeeklyRecapReadyIntegrationEvent` exists; emitted once per active child per week from the existing Parent weekly scheduler (no new Hangfire job); zero-activity weeks suppressed; the Notifications consumer routes it (WeeklyReport) with deep link to P9-02; ar-EG + en-US `WEEKLY_RECAP` copy with `{xp}`/`{skills}`.
- **Weekly challenge (BE-2):** documented as **DEFERRED** with the producer-readiness rationale; no partial/faked nudge shipped; backlog item recorded.
- **Cross-cutting:** every new type subject to P9-07 arbitration (automatic) with the inbox always written; new `Shared.Contracts` events follow the one-way module-isolation seam (no cross-module FK, rule 1); per-type send logged via the existing `analytics.reengagement.*` convention; no new design pattern (rule-8); no db-migration.
- **Gates:** api-tester (event→inbox, milestone dedup-by-construction, zero-activity recap suppression, recap reaches Notifications) green; security-auditor (light) no Critical/High; reviewer PASS; committer opens one PR on `feat/P9-06-habit-loop` and updates HANDOFF.md.

---

Plan ready — dispatch Batch 1.
