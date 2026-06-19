# Pipeline Brief — P9-06 New habit-loop notification categories

> **Cross-module wave.** Adds 3 new habit-loop nudge categories on TOP of the just-merged P9-07/05/08 arbitration subsystem (`docs/briefs/P9-nudge-arbitration.md`). Unlike that wave (Notifications-only, consumed already-published events), **this one needs NEW producers in the Gamification module** (+ possibly a recap scheduler) and NEW `Shared.Contracts.Gamification` integration events, then mirror-pattern consumers in Notifications. **Shared.Contracts is a serialized shared-file edit — see PARALLELISM.md.**
>
> **Headline producer-readiness verdict (the crux):**
> | Category | Producer exists today? | Verdict |
> |---|---|---|
> | **1. Streak milestones (3/7/14/30)** | ✅ `StreakAdvancedDomainEvent(StudentId, NewStreak, …)` already raised post-commit | **BUILDABLE NOW** — pure-add republisher, mirrors `StudentLeveledUpDomainEventRepublisher` |
> | **2. Weekly challenge lifecycle (start/halfway/ending-soon)** | ⚠️ No "weekly challenge" entity. `TimedEvent` is a **platform-wide broadcast** (no StudentId); **no halfway/ending-soon signal** at all | **DEFER** (same recipient-fanout blocker that deferred timed-event nudges in the prior wave) |
> | **3. Weekly recap (XP/skills summary)** | ✅ P5-01 `WeeklyReport` **already shipped** (Parent module) with `XpEarned`/`SkillsImproved`; `WeeklyReportJob` runs Mondays | **BUILDABLE NOW** — needs a producer + a Shared.Contracts seam/event (placement decision, OQ-R1) |

---

## Summary & traceability

| Item | Value |
|---|---|
| One-line task | Add streak-milestone, weekly-challenge-lifecycle, and weekly-recap nudge categories — new Gamification-emitted integration events consumed by the existing Notifications dispatcher/arbiter. |
| User story | `user-stories/Phase-9-Notifications/P9-06-new-habit-loop-notification-categories.md` |
| Task file | `tasks/Backend/Phase-9-Notifications/P9-06-BE.md` (BE-1..5) |
| FR-IDs | **FR-GM-8** (parent-controlled, quiet-hours, daily-cap, Arabic-first, never-shaming re-engagement notifications). |
| NFR | **NFR-1** (no producer hot-path latency degradation — republishers + scheduled jobs are async/fail-soft, off the streak-advance commit path). |
| BRD goals | **G1** (learning habit) + **G3** (engagement / retention). |
| Epic / Phase | Notifications Module · Phase 9 (post-MVP). |
| Depends on | **P4-03** (streak — ✅ built), **P4-11** (timed events — ✅ built but broadcast-only), **P4-02** (XP — ✅ built), **P5-01** (weekly report — ✅ NOW SHIPPED, story said "when shipped"), **P9-07** (arbitration — ✅ merged), **P5-03** (analytics seam — ✅ built but no notification-telemetry ingest). |
| Product decisions | Parent-driven onboarding (parent owns per-child prefs + budget; child JWT cannot write). No teacher role. None conflict. |

**Build order within the wave:** the Notifications consumers depend on the new `Shared.Contracts` event *shapes*, and the producers depend on those same shapes. So **batch 0 = define the new `Shared.Contracts.Gamification` events** (serialized, single shared-file PR), then producers (Gamification) and consumers (Notifications) can build in parallel against the agreed contract.

---

## Business context & value

The merged P9 wave already turns 11 signals into parent-gated, arbitrated nudges, but the habit loop still leans on the *daily* streak. P9-06 adds three "fresh reason to come back" categories:

- **Streak milestones** — celebration at 3/7/14/30 days ("🔥 7 أيام متواصلة! إنت بطل"). Distinct from the existing **StreakAtRisk** (loss-aversion) — this is the *reward* half. Cheapest of the three to ship (the producer event already fires; only a threshold-filtered republisher + a consumer + copy are new).
- **Weekly challenge lifecycle** — start/halfway/ending-soon countdown for time-boxed goals. The highest-intent of the three ("ending-soon = highest value" per the story) — but the underlying *per-child weekly challenge* concept does not exist (see producer inventory). Only a platform-wide `TimedEvent` exists.
- **Weekly recap** — once/week personalized summary ("إنجازك الأسبوع ده: {xp} XP و {skills} مهارات 🌟") deep-linking to progress (P9-02). The data is already computed weekly by the shipped P5-01 `WeeklyReport`.

**Success measured by:** milestone nudges fire exactly once per threshold per streak episode; recap fires once/week per active child; both flow through the existing dispatcher → arbiter (global budget + cooldown + inbox-always-written); each type is parent-toggleable and per-type send/open is logged; weekly-challenge work is honestly scoped to "deferred until a per-child challenge or recipient-fanout mechanism exists" rather than faked.

---

## "What exists today" grounding — PRODUCER inventory (the crux, with file:line)

### Category 1 — Streak milestones → **BUILDABLE NOW**

| Concern | File:line | Finding |
|---|---|---|
| Where streak length is incremented | `StudentXpProfile.AdvanceStreak` — `…Domain/Entities/StudentXpProfile.cs:163-169` | `CurrentStreak` is set, `LongestStreak` updated, then **`RaiseDomainEvent(new StreakAdvancedDomainEvent(StudentId, CurrentStreak, LongestStreak, activityDate))`** at **:168**. `ResetStreakAndStart` (:185) also raises it for the new day-1. |
| The domain event payload | `…Domain/Events/StreakAdvancedDomainEvent.cs:17-25` | `record StreakAdvancedDomainEvent(int StudentId, int NewStreak, int LongestStreak, DateOnly ActivityDate)` — **carries everything a milestone needs**. Doc-comment already lists "P4-09 re-engagement" as a forward-compat consumer. |
| Post-commit dispatch | `StreakService.AdvanceStreakAsync` — `…Infrastructure/Services/StreakService.cs:51-156`; domain events dispatched by `UnitOfWorkBehavior` strictly **after** commit | The raise point is clean and post-commit — exactly the property the prior wave's republishers rely on. Practice-mode and same-day/out-of-order transitions short-circuit *before* the raise (StreakService:87-121), so the event only fires on a genuine advance. |
| The exact republisher template to mirror | `StudentLeveledUpDomainEventRepublisher.cs` (`…Application/Features/Reengagement/Republishers/`) | `INotificationHandler<StudentLeveledUpDomainEvent>` → `_publisher.Publish(new …IntegrationEvent(…))`, fail-soft try/catch, auto-registered via the Application assembly MediatR scan. **The new `StreakMilestoneReachedRepublisher` is a near-verbatim copy.** |
| Milestone threshold config | `StreakOptions.cs` (`Gamification:Streak`) currently has only `TimeZoneId` + `DailyJobCron` | **No milestone list today.** Add `MilestoneDays` (default `3,7,14,30`) to `StreakOptions` (or read via `IGlobalSettingsProvider` for tunable-without-deploy). The republisher fires only when `NewStreak` ∈ that set. |
| Precedent: the existing freeze-on-milestone check | `StreakService.cs:124` — `if (profile.CurrentStreak % _freezeOptions.EarnEveryNStreakDays == 0)` | Confirms the module already does "fire-on-streak-N" logic; the milestone republisher is the same idea, threshold-gated, but kept in a **republisher** (not the service) to stay off the commit path and mirror the existing republisher pattern. |

**Dedupe — recommend PRODUCER-side by construction (no dedupe store needed for the happy path).** Because `StreakAdvancedDomainEvent` fires once per *day-transition* and `NewStreak` strictly increases by 1 per advance, the value 7 is crossed exactly once per streak episode. So "fire only when `NewStreak ∈ {3,7,14,30}`" is inherently one-time-per-threshold-per-episode — **the producer's job, no consumer dedupe store required**. The consumer should still keep the existing Redis dedupe call (per (child, category, day)) as a cheap idempotency guard against duplicate domain-event delivery, identical to the 11 existing handlers — that is already in the handler template.

### Category 2 — Weekly challenge lifecycle → **DEFER (degrade)**

| Concern | File:line | Finding |
|---|---|---|
| Is there a "weekly challenge" concept? | `Glob **/*Challenge*.cs` → **0 files** | **No weekly-challenge entity exists.** The story's "P4-11 weekly challenges" maps to **`TimedEvent`** (the only time-boxed gamification concept). |
| The TimedEvent entity | `…Domain/Entities/TimedEvent.cs:24-71` | A **platform-wide catalog row** (`Code`, `NameEn/Ar`, `StartUtc`, `EndUtc`, `Multiplier`, `Scope`, `IsActive`). It is an **XP-multiplier window** ("Double XP Weekend"), **not a per-child challenge with progress**. No per-student state, no completion tracking. |
| Lifecycle transitions | `TimedEventSweepJob.cs:74-144` (every 2 min) | Fires **only two** transitions: window-**enter** (`TimedEventStartedDomainEvent`) and window-**exit** (`TimedEventEndedDomainEvent`). **No "halfway" and no "ending-soon" signal exists.** |
| The recipient blocker | `TimedEventStartedRepublisher.cs:32-40`; payloads in `Shared.Contracts/Gamification/TimedEventStarted/EndedIntegrationEvent.cs` | The integration events carry `TimedEventId`, `Code`, `Multiplier`, `Scope`, `StartUtc`, `EndUtc` — **NO `StudentId`**. This is the exact broadcast blocker the prior wave documented (`TimedEventNudgeDeferralNote.cs` in Notifications) and deferred. A per-child nudge needs a recipient; there is **no active-student-id list seam** (Analytics exposes only platform aggregates; Gamification is module-isolated). |

**Verdict: defer the weekly-challenge lifecycle nudges from P9-06 v1.** To deliver them honestly you need EITHER (a) a real per-child weekly-challenge feature (new entity + per-child progress + lifecycle) — a separate, much larger story; OR (b) a recipient fan-out mechanism (active-student-id seam + budget-aware bulk dispatch) for the platform-wide `TimedEvent` — also a separate story. Neither "halfway" nor "ending-soon" signals exist to emit. Building start-only platform-broadcast nudges would require the same fan-out and still lacks halfway/ending-soon, which the story calls the highest-value. **Recommend carrying this as a follow-up (P9-06b or fold into the existing deferred timed-event-fanout backlog).** Confirm via **OQ-R2**.

### Category 3 — Weekly recap → **BUILDABLE NOW (placement decision)**

| Concern | File:line | Finding |
|---|---|---|
| Does the recap data exist? | `WeeklyReport.cs:42-48` (Parent module) — `XpEarned`, `SkillsImproved` | **YES.** The story said "P5-01 weekly-report data when shipped — degrade gracefully until then." **P5-01 is NOW SHIPPED** (migration `20260618070103_AddWeeklyReport`). `WeeklyReport` already persists per-child-per-week `XpEarned` + `SkillsImproved` (+ weak areas + recommendations). |
| Is there a weekly scheduler? | `WeeklyReportJob.cs:35-117` (Parent module) — Mondays 03:00 UTC | **YES.** It already sweeps every linked child weekly, computes the prior-week window (last-Monday math at :59-61), creates a fresh DI scope per child, and is fail-soft per child. **This is the exact template a recap producer mirrors** (and the recap could even be emitted from the same sweep so the data and the nudge stay consistent). |
| The per-student scheduled-emit template | `LapseWinBackJob.cs:59-127` (Gamification) | The closest "scan profiles → publish a per-student integration event, fail-soft per publish" pattern. A `WeeklyRecapJob` that lives in Gamification would mirror this; but the recap *data* (XP+skills aggregated for the week) is already computed in Parent's `WeeklyReport`. |
| Where XP/skills are aggregated | `WeeklyReportGeneratorService.cs:62-168` | Pulls `IStudentXpTimeSeriesQuery.GetDailySeriesAsync` (XP) + `IStudentMasterySummaryQuery` (skills) from cross-module seams, upserts the `WeeklyReport`. So the recap numbers are **already derived and persisted** — the recap nudge just needs to read them and emit a per-child event. |
| Cross-module constraint | `Shared.Contracts/Parent/IParentChildQuery.cs` exists, but **no `IWeeklyReportQuery` seam exists** | `WeeklyReport` is internal to the **Parent** module. To emit a recap from Parent's existing `WeeklyReportJob`, Parent would publish a new `Shared.Contracts.Gamification`-or-new-namespace `WeeklyRecapIntegrationEvent(StudentId, Xp, Skills, …)`. Notifications consumes it. **No cross-module FK; one-way event seam (rule 1).** |

**Verdict: BUILDABLE NOW.** The cleanest design (recommended) is to emit a `WeeklyRecapReadyIntegrationEvent(StudentId, XpEarned, SkillsImproved, WeekStartUtc, OccurredOnUtc)` **from the existing Parent `WeeklyReportJob` / `WeeklyReportGeneratorService`** right after each child's row is upserted — the data is already in hand, the scheduler already exists, no new Hangfire job, no new DB read. Notifications consumes it like any other event. The event namespace is a decision (it is Parent-emitted, not Gamification-emitted) — see **OQ-R1**. Degrade-gracefully (`Xp=0, Skills=0` no-activity weeks) is already how `WeeklyReportGeneratorService` behaves (`:28-29`).

---

## "What exists today" grounding — CONSUMER side (mostly mirrors P9-05)

| Concern | File:line | Finding |
|---|---|---|
| Handler template | `StreakFreezeConsumedIntegrationEventHandler.cs` (full file read) | The canonical P9-05 handler: resolve parent (`IParentChildQuery.FindParentForChildAsync`) → `GetOrDefaultPrefsAsync` → `CountSentTodayAsync` → `ReengagementEvaluator.Evaluate` → `TryAcquireDedupeAsync` → `GetLocaleAsync` → `BuildMessage(..placeholders..)` → `_dispatcher.DispatchAsync` → log `analytics.reengagement.sent`. Full try/catch fail-soft. **The 2 new handlers (milestone, recap) are near-verbatim copies.** |
| Arbitration is automatic | HANDOFF.md:6 + `NudgeDispatcher.cs` | The P9-07 global budget + cooldown gate lives **inside `NudgeDispatcher`** (the single choke point). New handlers get arbitration **for free** — they just dispatch; the dispatcher rations push and always writes the inbox row. No per-handler arbiter wiring. |
| Copy templates | `ReengagementCopyTemplates.cs` — `Achievement:*` block at :48-96; `Render(...)` at :177 | Pure-static `(category:code:locale) → (title, body)` dict + `Render` placeholder substitution; ar-EG + en-US. `LEVELED_UP`, `LEAGUE_TIER_CHANGED`, `STREAK_FREEZE_CONSUMED` already present. **Add `STREAK_MILESTONE` (parametrized by milestone day or 4 codes) + `WEEKLY_RECAP` entries (ar+en).** |
| Category enum already has both | `NotificationCategory.cs:7-18` | **`WeeklyReport = 0`** and **`Achievement = 3`** already exist — **no new enum members needed** for milestone (→ Achievement) or recap (→ WeeklyReport). |
| Per-child toggle set — the gotcha | `ChildReengagementPreferenceService.cs:17-22` | The service manages **only** `StreakAtRisk`, `DailyMissionReminder`, `LapseWinBack`. **Neither `Achievement` nor `WeeklyReport` is in the parent-managed set** → consistent with the merged-wave decision (HANDOFF.md:16): **Achievement-category nudges are inbox-only (no push) in v1** until the P9-04 FE per-type toggle ships. New milestone (Achievement) + recap (WeeklyReport) nudges will likewise be **inbox-only in v1**. Confirm via **OQ-R3**. |
| MediatR auto-registration | HANDOFF.md (P5-03 note) + prior brief | New handlers in `Notifications.Application` and new republishers in `Gamification.Application` auto-register via the assembly MediatR scan — **no `AddCrossModuleMediatR` edit needed** IF a new producing module (Parent for recap) is already registered. **Verify `Parent.Application` is in `AddCrossModuleMediatR`** if the recap is Parent-emitted (it must be for the republisher/job's `IPublisher.Publish` to reach Notifications). Confirm during build. |

---

## Acceptance criteria (testable — reviewer checks against these)

### Streak milestones (BE-1, BE-4, BE-5)
- [ ] A new `StreakMilestoneReachedIntegrationEvent(EventId, OccurredOnUtc, StudentId, Milestone)` exists in `Shared.Contracts/Gamification`.
- [ ] A new Gamification republisher consumes `StreakAdvancedDomainEvent` and publishes the integration event **only when `NewStreak` ∈ the configured milestone set (default 3,7,14,30)** — fires once per threshold per streak episode (producer-side dedupe by construction); fail-soft (mirrors `StudentLeveledUpDomainEventRepublisher`).
- [ ] A new Notifications handler consumes it → `NudgeDispatcher` (auto-arbitrated), category **Achievement**, code `STREAK_MILESTONE` (or per-threshold code), Redis dedupe retained.
- [ ] New `ReengagementCopyTemplates` entries (ar-EG + en-US) personalized with `{streakLength}` (and `{name}` if a name seam exists), Arabic-first, celebration > guilt.
- [ ] Inbox row always written; push gated by the existing budget/cooldown (and inbox-only in v1 per OQ-R3); send logged `analytics.reengagement.sent`.

### Weekly challenge lifecycle (BE-2)
- [ ] **Documented as DEFERRED** with the producer-readiness rationale (no per-child challenge entity; `TimedEvent` is platform-broadcast with no StudentId; no halfway/ending-soon signal). Carried as a follow-up backlog item. **No partial/faked nudge shipped.** (Subject to OQ-R2 — if the lead wants a start-only platform broadcast, that still needs the recipient-fanout seam.)

### Weekly recap (BE-3, BE-4)
- [ ] A new `WeeklyRecapReadyIntegrationEvent(EventId, OccurredOnUtc, StudentId, XpEarned, SkillsImproved, WeekStartUtc)` (namespace per OQ-R1) is emitted **once per child per week** from the existing weekly scheduler (recommend the Parent `WeeklyReportJob`/generator post-upsert; no new Hangfire job).
- [ ] A new Notifications handler consumes it → `NudgeDispatcher`, category **WeeklyReport**, code `WEEKLY_RECAP`, deep-link to progress (P9-02).
- [ ] New `ReengagementCopyTemplates` entries (ar-EG + en-US) personalized with `{xp}` + `{skills}` ("إنجازك الأسبوع ده: {xp} XP و {skills} مهارات 🌟").
- [ ] Degrades gracefully on a no-activity week (Xp=0/skills=0 — already how the generator behaves); recap still fires (or is skipped per OQ-R1b — confirm whether to suppress zero-activity recaps).

### Cross-cutting
- [ ] Each new type is subject to P9-07 arbitration (automatic via dispatcher) and the inbox is always written.
- [ ] New `Shared.Contracts` events follow the one-way module-isolation seam (Gamification/Parent publish, Notifications consumes) — no cross-module FK (rule 1).
- [ ] Per-type send/open logged (the existing `analytics.reengagement.*` `ILoggerManager` convention — first-class analytics ingest remains a separate story, per the merged wave).

---

## Affected modules & data

**Three modules touched** (cross-module wave): **Gamification** (streak-milestone republisher; +config), **Parent** (weekly-recap emit from the existing job — IF OQ-R1 = Parent-emitted), **Notifications** (2 new consumers + templates), **Shared.Contracts** (2 new events). No cross-module FK.

| Entity / artifact | New vs existing | Notes |
|---|---|---|
| `StreakMilestoneReachedIntegrationEvent` | **New** (Shared.Contracts/Gamification) | Serialized shared-file edit. |
| `WeeklyRecapReadyIntegrationEvent` | **New** (Shared.Contracts — namespace per OQ-R1) | Serialized shared-file edit. |
| `StreakMilestoneReachedRepublisher` | **New class** (Gamification.Application) | Mirrors `StudentLeveledUpDomainEventRepublisher`; threshold-gated. |
| `StreakOptions.MilestoneDays` | **Extend** (config only) | Default `3,7,14,30`. Or `IGlobalSettingsProvider` for tunable-without-deploy. **No DB.** |
| Weekly-recap emit point | **Extend** (Parent `WeeklyReportJob`/generator) **or new `WeeklyRecapJob`** (OQ-R1) | Recommend extend the existing job — no new Hangfire schedule. |
| 2 new Notifications handlers | **New classes** | Mirror `StreakFreezeConsumedIntegrationEventHandler`. |
| `ReengagementCopyTemplates` | **Extend** (data only) | +`STREAK_MILESTONE` (×2 locales, possibly ×4 thresholds) +`WEEKLY_RECAP` (×2 locales). **No schema.** |
| `NotificationCategory` | **No change** | `Achievement` + `WeeklyReport` already exist (:9, :12). |
| Per-child prefs schema | **No change** | Achievement/WeeklyReport stay inbox-only in v1 (OQ-R3). |

---

## Handoff → db-migration

**Likely NONE.** Milestone thresholds are config; the recap reads already-persisted `WeeklyReport` data; both new categories already exist in `NotificationCategory`; arbitration state stays in Redis/inbox (P9-07). **A migration is needed ONLY if** the lead decides (OQ-R3) to make `Achievement`/`WeeklyReport` parent-toggleable now — that adds them to the `ChildReengagementPreferenceService` category array (defaults synthesised on read → still likely no migration) — confirm. **Recommendation: db-migration stands by; expect no migration.**

---

## Handoff → backend-feature (split into producer + consumer batches)

**Mirror existing shapes; Option C (EF only in Infrastructure); fail-soft per ADR-0002.**

**Batch 0 — Shared.Contracts (serialized, single PR per PARALLELISM.md):** add `StreakMilestoneReachedIntegrationEvent` + `WeeklyRecapReadyIntegrationEvent`. This unblocks producers + consumers to build in parallel.

**Batch A — Gamification producer (streak milestones):**
- New `StreakMilestoneReachedRepublisher : INotificationHandler<StreakAdvancedDomainEvent>` in `…Application/Features/Reengagement/Republishers/` — verbatim copy of `StudentLeveledUpDomainEventRepublisher` shape; publish the integration event **only when `notification.NewStreak` is in the milestone set**; fail-soft try/catch; auto-registers via the Application assembly.
- Add `MilestoneDays` (default `3,7,14,30`) to `StreakOptions` (or `IGlobalSettingsProvider.GetString("Gamification:Streak:MilestoneDays", "3,7,14,30")` for tunable-without-deploy — recommend the latter to match P9-07's config style).
- **Do NOT touch `StreakService`/`AdvanceStreak`** — keep the milestone decision in the republisher (off the commit path; NFR-1).

**Batch B — Weekly-recap producer (placement per OQ-R1; recommend Parent):**
- In Parent's `WeeklyReportGeneratorService.GenerateAsync` (or the `WeeklyReportJob` loop), after the `WeeklyReport` upsert + `SaveChangesAsync`, publish `WeeklyRecapReadyIntegrationEvent(StudentId=childId, XpEarned, SkillsImproved, WeekStartUtc, OccurredOnUtc=now)` via `IPublisher` — fail-soft per child (the job is already fail-soft per child). No new scheduler, no new DB read.
- Verify `Parent.Application` is in `AddCrossModuleMediatR` so the publish reaches the Notifications handler (it should be — Parent already publishes integration work; confirm).

**Batch C — Notifications consumers (after Batch 0 contract; parallel with A/B):**
- `StreakMilestoneReachedIntegrationEventHandler` — copy `StreakFreezeConsumedIntegrationEventHandler`; category `Achievement`; code `STREAK_MILESTONE` (or `STREAK_MILESTONE_{n}`); placeholder `("streakLength", ev.Milestone.ToString())`.
- `WeeklyRecapReadyIntegrationEventHandler` — same template; category `WeeklyReport`; code `WEEKLY_RECAP`; placeholders `("xp", ev.XpEarned), ("skills", ev.SkillsImproved)`; deep-link to P9-02 progress.
- Add `ReengagementCopyTemplates` entries: `STREAK_MILESTONE` + `WEEKLY_RECAP` (ar-EG + en-US). Arabic-first, child-safe, celebration > guilt.
- **Inbox-only in v1** (Achievement/WeeklyReport not in the parent-managed push set) per OQ-R3 — the handler dispatches normally; the dispatcher's gate keeps it inbox-only. Document it.
- **Name personalization caveat** (carried from the prior wave): `{name}` needs a display-name seam — `IUserLookup` returns locale, maybe not name. If unavailable, drop `{name}` to a generic vocative. Flag during build.

---

## Handoff → frontend

**None for this wave (backend-only).** The parent toggle UX for the finer per-type categories (achievement/streak-milestone/weekly-challenge/weekly-recap) is **P9-04-FE**'s concern (`tasks/Frontend/student-app/Phase-9-Notifications/P9-04-FE.md`). When P9-04 lands per-type toggles, the v1 "inbox-only Achievement/WeeklyReport" decision (OQ-R3) flips to parent-controllable push. Note for the planner to track against P9-04; not built here.

---

## Open questions / assumptions / risks (each with a recommendation)

> **Top-1 must-resolve:** OQ-R2 (weekly-challenge scope — defer vs invest in a new feature/fanout). The rest have confident recommendations.

**OQ-R1 — Weekly-recap event namespace + emit point.** The recap data lives in the **Parent** module (`WeeklyReport`), not Gamification. Emitting from Parent's existing `WeeklyReportJob` is cleanest (data + scheduler already there). But the story/task frame all new events as "Gamification publishes." Options: (a) **Parent emits `WeeklyRecapReadyIntegrationEvent`** from the existing job — recommended (no new job, no new read, data-consistent); namespace it `Shared.Contracts/Parent` or a new `Shared.Contracts/Notifications`. (b) A new `WeeklyRecapJob` in Gamification that re-aggregates XP/skills — duplicates work the P5-01 job already does; **not recommended.** **Recommendation: (a) Parent-emitted, post-upsert.** Confirm namespace + that `Parent.Application` is MediatR-registered cross-module.
- **OQ-R1b** — suppress zero-activity recaps? **Recommend: skip the recap nudge when `XpEarned=0 AND SkillsImproved=0`** (no "you did nothing this week" message — never-shaming per FR-GM-8), even though the `WeeklyReport` row is still written. Confirm.

**OQ-R2 (decision-shaping) — weekly-challenge lifecycle: defer, or invest?** No per-child weekly-challenge exists; `TimedEvent` is platform-broadcast with no StudentId and no halfway/ending-soon signal. **Recommendation: DEFER from P9-06 v1** and carry as a follow-up (either a real per-child weekly-challenge feature, or a recipient-fanout seam for platform TimedEvents — both are separate stories). Ship streak-milestone + weekly-recap now. If the lead wants *something* for challenges in v1, the only honest option is a **start-only platform broadcast** that still needs the active-student-fanout seam (and lacks the high-value ending-soon) — confirm whether that reduced scope is worth a dedicated fanout story. **This is the one OQ that materially changes the wave's deliverable size.**

**OQ-R3 — category mapping + push vs inbox-only.** Milestone → **Achievement**, recap → **WeeklyReport** (both enum values already exist; no new members). Per the merged-wave decision (HANDOFF.md:16), **Achievement/WeeklyReport are NOT in the parent-managed push set → inbox-only in v1** until P9-04 FE toggles ship. **Recommendation: ride Achievement (milestone) + WeeklyReport (recap), inbox-only in v1, no new `NotificationCategory` values, no prefs-schema change.** If the lead wants milestone/recap to push in v1, add them to `ChildReengagementPreferenceService.ReengagementCategories` (and confirm the P9-04 FE will expose the toggles) — additive, defaults synthesised on read (likely no migration). Confirm desired granularity.

**OQ-R4 — streak-milestone dedupe: producer or consumer?** **Recommendation: PRODUCER-side by construction.** `StreakAdvancedDomainEvent` increments by 1 per day-transition, so each threshold (3/7/14/30) is crossed exactly once per episode — "fire only at threshold values" is inherently one-time. Keep the consumer's existing Redis (child, category, day) dedupe as a duplicate-delivery guard (already in the template), but **no new dedupe store/state column is needed.** A reset (`ResetStreakAndStart` → `NewStreak=1`) cleanly re-arms the next episode. Confirm. (Edge case to note: a milestone-day event re-delivered would re-fire same-day → the existing day-keyed Redis dedupe absorbs it.)

**OQ-R5 — does weekly-recap need a NEW Hangfire job?** **Recommendation: NO — extend the existing `WeeklyReportJob` (Parent, Mondays 03:00 UTC).** It already sweeps every linked child weekly with the recap data in hand; emit the event post-upsert. A standalone `WeeklyRecapJob` would duplicate the child-enumeration + aggregation. Confirm (ties to OQ-R1).

**OQ-R6 — security-auditor needed?** **Recommendation: YES (light), same scope as the merged wave.** Touches child notification data + new cross-module integration events. PII-light (int ids, XP/skill counts, milestone ints, template codes — no free text). Verify: (a) the new events carry no PII beyond ids/counts; (b) no new write endpoint / no new auth surface (recap is job-emitted, milestone is event-driven; no controller); (c) module isolation holds (no cross-module FK; one-way event seam); (d) fail-soft producers never throw into the streak commit path or the weekly job. Not expected to surface Critical/High, but child-data + new cross-module producers cross the rule-4b threshold — run it before the gate.

**Rule-8 (design pattern) check — CONFIRMED no concern.** Mirrors the existing republisher (`INotificationHandler<DomainEvent>` → publish), the existing handler/helper/dispatcher shapes, and the existing scheduled-emit job. The threshold-gated republisher is a simple `if` on config data, not a Strategy/Factory. **No new design pattern; no rule-8 approval needed.** (If milestone copy/threshold logic drifts toward a pluggable rule engine, stop and ask first.)

**Risk — Shared.Contracts serialization.** Two new events = shared-file edits; per PARALLELISM.md these must be serialized (the Batch-0 single PR). Coordinate so the producer + consumer batches don't both edit Shared.Contracts concurrently.

**Risk — recap event from Parent must reach Notifications.** If `Parent.Application` is not in `AddCrossModuleMediatR`, the recap handler silently never fires (the exact P5-03 gotcha in HANDOFF.md:42). Verify during build; add an integration test that publishes the recap event and asserts the inbox row.

**Risk — milestone double-count via `ResetStreakAndStart`.** `ResetStreakAndStart` raises `StreakAdvancedDomainEvent(NewStreak=1)` — below all thresholds, so no spurious milestone. Confirmed safe by reading the entity; note it so the reviewer doesn't worry.

---

## Recommended pipeline order (first cut — the `planner` finalizes)

Backend-only, cross-module wave. **No `designer` / `frontend` / `frontend-e2e-tester`.**

**Gate OQ-R2 (and OQ-R1/R3) with the lead first.** Then:

1. **`db-migration`** — **likely skipped.** Run only if OQ-R3 = make Achievement/WeeklyReport parent-toggleable now (and even then probably no schema change).
2. **`backend-feature` Batch 0** — Shared.Contracts: the 2 new events (serialized single PR per PARALLELISM.md). Gate before fan-out.
3. **`backend-feature` Batch A (Gamification producer)** + **Batch B (Parent recap emit)** + **Batch C (Notifications consumers + templates)** — parallel after Batch 0 (all build against the agreed contract).
4. **`api-tester`** — no new HTTP endpoints; integration tests over the event→nudge pipeline: (a) advance a streak to 7 → assert one milestone inbox row, ar+en copy, dedupe on re-delivery, no fire at non-threshold days; (b) run the weekly job for a child with XP → assert one recap inbox row with `{xp}`/`{skills}` rendered; assert zero-activity suppression (OQ-R1b); (c) assert arbitration/inbox-always-written still holds. Mirror `P4_11_StreakFreezeTimedEvents_IntegrationTests` / the merged P9 suite.
5. **`security-auditor`** — scoped (OQ-R6): new events PII-light, fail-soft producers, module isolation, no new auth surface.
6. **`reviewer`** — gate against this brief's ACs + CONVENTIONS.md (Option C, fail-soft, rule-1 isolation, rule-8 no new pattern, the documented weekly-challenge deferral), including api-tester + security-auditor results.
7. **`committer`** — per-story branch `feat/P9-06-…` (or producer/consumer sub-branches); PR with full description; update `docs/dev/HANDOFF.md` (new config `Gamification:Streak:MilestoneDays`; the recap-from-WeeklyReportJob decision; the weekly-challenge deferral + reason; the inbox-only-v1 carryover for Achievement/WeeklyReport).
