# Pipeline Brief — P9-06 Weekly-challenge ending-soon nudge (deferral now obsolete)

> **Re-opens the one piece P9-06 deferred.** P9-06 shipped WEEKLY-RECAP only (PR #194); streak-milestones were dropped (redundant with the `STREAK_3/7/14/30` badge nudges) and **weekly-challenge was DEFERRED "no per-child challenge entity exists."** That deferral reason is **OUTDATED**: verified below that **weekly missions (`StudentMission` with `MissionType.Weekly`) ARE the per-child weekly challenge** — a real per-child row with `Progress`, `Target`, `Status`, `PeriodEndUtc`, `CompletedAtUtc`. So the recipient-fanout blocker that justified the defer (which was correct for the platform-wide `TimedEvent`) **does not apply to weekly missions** — they are already per-student and already scanned by an existing producer.
>
> **Backend-only, cross-module wave (Gamification emit → Notifications consume). No DB migration. No new design pattern.**

---

## Summary & traceability

| Item | Value |
|---|---|
| One-line task | Add a **weekly-challenge ending-soon / progress reminder** nudge — Gamification scans incomplete weekly missions nearing `PeriodEndUtc` and emits a new per-student `WeeklyMissionReminderIntegrationEvent`; Notifications consumes it as a new `WeeklyChallenge`-category nudge ("⏳ تحدي الأسبوع بيخلص — لسه ناقصك {remaining}! اخلصه قبل ما يفوت 💪"). |
| User story | `user-stories/Phase-9-Notifications/P9-06-new-habit-loop-notification-categories.md` (AC: "Weekly challenge lifecycle … *ending-soon (countdown)* … ending-soon is the highest-value of the three") |
| Task file | `tasks/Backend/Phase-9-Notifications/P9-06-BE.md` (BE-2 weekly-challenge lifecycle; BE-4 templates; BE-5 category mapping/arbitration) |
| FR-IDs | **FR-GM-8** (parent-controlled, quiet-hours, daily-cap, Arabic-first, never-shaming re-engagement notifications) |
| NFR | **NFR-1** (no producer hot-path latency degradation — the scan is a scheduled fail-soft job, off any commit path) |
| BRD goals | **G1** (learning habit) + **G3** (engagement / retention) |
| Epic / Phase | Notifications Module · Phase 9 (post-MVP) |
| Depends on | **P4-11** (weekly challenges via `MissionType.Weekly` — ✅ built+merged), **P4-09** (daily-mission-reminder producer/consumer to mirror — ✅ built), **P9-07** (arbitration — ✅ merged), **P9-06 recap** (✅ merged — this is the same wave's remaining piece) |
| Supersedes | The HANDOFF.md:58 "⛔ Weekly challenge (DEFERRED): no per-child challenge entity exists" note. Update it. |
| Product decisions | Parent-driven onboarding (parent owns per-child prefs + budget). No teacher role. None conflict. |

**Build order:** **Gamification (emit) first**, then **Notifications (consume)** — the consumer compiles against the new `Shared.Contracts` event shape, so the contract + producer batch lands before the consumer batch. `Shared.Contracts` is a serialized shared-file edit (PARALLELISM.md); so are `NotificationCategory` (enum) and `NudgeArbiter` (priority/cooldown). Serialize this whole wave against any other in-flight P9 work.

---

## Business context & value

The merged P9 wave turns 12+ signals into parent-gated, arbitrated nudges, and P9-06 added the weekly-recap reward. The remaining habit-loop gap is the **time-boxed weekly goal countdown** — the story explicitly calls *ending-soon* "the highest-value of the three." Today the daily mission gets a "ends soon, finish it" reminder (`DailyMissionReminder`, P4-09) but the **weekly mission gets nothing between start and completion** — a child who is 4/5 of the way through a weekly challenge with a day left receives no nudge, and the challenge silently expires.

**Who benefits:** the **student** (a fresh, well-timed reason to come back and finish a near-complete weekly goal); indirectly the **parent** (retention + the per-type toggle they will manage once P9-04 FE ships).

**Success measured by:** the reminder fires for in-window, still-incomplete weekly missions; never for completed or out-of-window ones; at most once per student per weekly period; routes through the existing dispatcher → arbiter (global budget + cooldown + inbox-always-written); inbox-only in v1; ar+en copy; no PII.

---

## Scope decision — what's genuinely new vs already-covered (the crux)

**Verified against code. Conclusion: build exactly ONE thing — the weekly ending-soon/progress reminder. Do NOT add a weekly completion nudge.**

| Lifecycle moment | Covered today? | Verdict |
|---|---|---|
| Weekly mission **started** | No dedicated nudge | **Skip.** Low value; the story ranks ending-soon highest and lists started/halfway as lesser. A "you started a challenge" nudge the same moment the child is already in-app is noise. Out of scope. |
| Weekly mission **halfway** | No dedicated nudge | **Skip for v1.** Genuinely new but lower-value and needs a separate progress-crossing signal (an event on the `Progress >= Target/2` transition, not a scan). Not worth the extra producer plumbing now; carry as backlog if the lead wants it. |
| Weekly mission **ending-soon / still-incomplete** | **NOTHING** | **BUILD — this is the genuine gap and the highest-value piece.** Mirror the daily-mission-reminder mechanic for the weekly cadence. |
| Weekly mission **completed** | **ALREADY NUDGED** by `MissionCompletedIntegrationEventHandler` → `Achievement` category, code `MISSION_COMPLETED` | **Do NOT build a separate weekly-completion nudge — REDUNDANT.** See call below. |

### Completion-nudge redundancy call (explicit, as requested)

`MissionCompletedIntegrationEvent` (`Shared.Contracts/Gamification/MissionCompletedIntegrationEvent.cs`) carries `(EventId, OccurredOnUtc, StudentId, MissionCode, RewardXp)` — **no cadence field**. It is re-published for **both** daily and weekly mission completions, and `MissionCompletedIntegrationEventHandler` already turns every one into an `Achievement` / `MISSION_COMPLETED` nudge ("أتممت مهمتك اليومية ✨ +{rewardXp} XP"). So weekly completions **are already celebrated**.

**Recommendation: leave completion to the existing generic handler. Do NOT build a weekly-specific completion nudge, and do NOT enrich `MissionCompletedIntegrationEvent` with cadence in this wave.** Rationale:
- The only *upside* of enriching with cadence would be distinct copy ("تحدي الأسبوع اكتمل!" vs the daily-flavoured "مهمتك اليومية"). That is a copy nicety, not a missing capability.
- The *cost* is real and disproportionate: `MissionCompletedIntegrationEvent` is a serialized shared-file edit on a hot, already-shipped contract with multiple consumers; the daily-flavoured copy ("مهمتك اليومية") is technically slightly wrong for weekly completions today, but it's encouraging and child-safe — not broken.
- Per the brief's own guidance: don't over-build. The single high-value gap is the **ending-soon reminder**. Ship that; if the lead later wants weekly-specific completion copy, that's a small, separate, clearly-scoped follow-up (enrich `MissionCompleted` with `MissionType` + a `MISSION_COMPLETED_WEEKLY` template).

**Net buildable scope (v1):**
- **(a)** Gamification emits a NEW `WeeklyMissionReminderIntegrationEvent` (weekly missions expiring within a config lookahead window, still incomplete, carrying Progress/Target + remaining time), mirroring `DailyMissionReminderIntegrationEvent`.
- **(b)** Notifications consumes it → a new `WeeklyChallenge`-category nudge, mirroring `DailyMissionReminderIntegrationEventHandler`.

---

## Gamification emit point + mechanics

**Verified producer to mirror:** `StreakAtRiskJob.RunAsync` (`backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Infrastructure/Jobs/StreakAtRiskJob.cs`) already runs **Pass 2: Daily-mission-reminder** — it scans `StudentMissions` where `MissionType == Daily`, `Status IN (NotStarted, InProgress)`, `PeriodEndUtc > now AND PeriodEndUtc <= now + MissionReminderLookaheadHours`, projects distinct `StudentXpProfile.StudentId`, `Take(500)` page-guard, then publishes one `DailyMissionReminderIntegrationEvent` per student via `IPublisher.Publish`, **fail-soft per publish**.

### Emit-point decision

**Add a THIRD pass to `StreakAtRiskJob` (do not create a new job, do not use `MissionRolloverJob`).** Rationale:
- `StreakAtRiskJob` is the canonical re-engagement producer; it already owns the exact "scan incomplete missions by `PeriodEndUtc` window → publish one event per student, fail-soft" pattern. Mirroring it in the same class is the smallest, most-consistent change (rule 8 — no new pattern, no new job).
- `MissionRolloverJob` is the wrong home: it's a bulk `ExecuteUpdateAsync` *expiry* sweep that bypasses the change tracker and runs at period rollover (after expiry) — by then the window has passed; nothing to remind.
- A brand-new job would duplicate scope creation, DI registration, and Hangfire wiring for no benefit.

**Caveat for the planner:** `StreakAtRiskJob` runs **daily at 18:00 UTC** (`Gamification:Reengagement:StreakAtRiskCron`). A weekly reminder driven by a daily job is correct — the lookahead window + the per-(student, period) dedupe ensure each student gets at most one weekly reminder per period even though the job runs daily (on each of the 1-2 days the mission is "in window", the dedupe guard suppresses all but the first). Document this explicitly so the reviewer understands why a daily cron drives a weekly nudge.

### Lookahead window

- Daily uses `MissionReminderLookaheadHours = 6`. Weekly should be larger — **recommend default 48 hours (2 days)** so the reminder lands 1-2 days before `PeriodEndUtc`.
- **Make it config-driven**: add `WeeklyMissionReminderLookaheadHours` (default `48`) to `ReengagementOptions` (`backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Application/Configuration/ReengagementOptions.cs`, section `Gamification:Reengagement`). Mirrors the existing `MissionReminderLookaheadHours` exactly. **Config-only, no DB.**

### Scan query (Pass 3)

Identical shape to Pass 2 but with the weekly cadence + weekly lookahead, and projecting Progress/Target. Because a student can have at most one active weekly mission per period in practice but the query must be safe for multiples, project per-student the *most-urgent* (min `PeriodEndUtc`) row and compute remaining time + Progress/Target from it:

```
WHERE m.MissionType == MissionType.Weekly
  AND (m.Status == NotStarted || m.Status == InProgress)   // excludes Completed (2) and Expired (3)
  AND m.PeriodEndUtc > nowUtc
  AND m.PeriodEndUtc <= nowUtc.AddHours(WeeklyMissionReminderLookaheadHours)
```
Group/order by `StudentXpProfile.StudentId` then earliest `PeriodEndUtc`; `Take(500)` page-guard with the same WARN log as the existing passes. `Progress`/`Target`/`PeriodEndUtc` are columns already on `StudentMission` — no extra read, no PII.

### Dedup

Mirror the existing "at most one per student per run" (the `Distinct()`/grouping already guarantees this within a run). For **across-run** dedup (daily job, weekly nudge), rely on the **consumer-side Redis dedupe** keyed by `(studentId, category, eventDay)` — but note that key is **day-scoped** (`nudge:{studentId}:{(int)category}:{yyyyMMdd}`), which would re-fire on day 2 of the window. The requirement is **at most one weekly-reminder per student per PERIOD**. Two clean options:
- **Option A (recommended — producer-side period guard):** mirror the P9-08 tier-dedupe precedent (`ReengagementDedupeStore.TryAcquireTierAsync` with no day component + a long TTL). The consumer acquires a **period-scoped** dedupe key — e.g. `nudge-weekly:{studentId}:{periodKey}` with TTL ≈ the lookahead window + a margin (e.g. 72h). This guarantees one per period regardless of how many daily runs hit the window. To do this the **event must carry the period identity** — include a stable per-period field (see event shape; recommend `MinutesRemaining` is not enough — add `PeriodKey` or `PeriodEndUtc`).
- **Option B (simpler, slightly weaker):** keep the existing day-keyed consumer dedupe and accept that a student in a 2-day window *could* get the reminder on each of 2 days. With a 48h window that's up to 2 nudges/period — arguably acceptable ("ending tomorrow" then "ending today"), and the P9-07 per-type cooldown (set it to a weekly TTL, see below) already caps push frequency. The inbox row would still be written each day, though.

**Recommendation: Option A** — it matches the story's "ending-soon (countdown)" intent of a single well-timed nudge and reuses the existing `TryAcquireTierAsync` precedent (no new pattern). Decide via OQ-1. If Option A, the consumer calls `TryAcquireTierAsync(studentId, $"WEEKLY_CHALLENGE:{periodKey}", ttl, ct)` instead of the day-keyed `TryAcquireAsync`.

### New event shape (`Shared.Contracts/Gamification/WeeklyMissionReminderIntegrationEvent.cs`)

Mirror `DailyMissionReminderIntegrationEvent` (opaque ids only, NO PII), but carry Progress/Target (for the "لسه ناقصك {remaining}" copy) and the period identity (for Option-A dedupe):

```csharp
namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Published by the Gamification StreakAtRiskJob (third pass, daily cron) when a student has an
/// active WEEKLY mission expiring within Gamification:Reengagement:WeeklyMissionReminderLookaheadHours
/// (default 48h) and still incomplete. At most one event per student per run (most-urgent mission).
/// Consumed by Notifications → WeeklyChallenge nudge. Payload carries opaque int IDs only — NO PII.
/// Mirrors DailyMissionReminderIntegrationEvent (FR-GM-8 / P9-06).
/// </summary>
public sealed record WeeklyMissionReminderIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int Progress,            // current progress toward Target
    int Target,              // weekly mission target
    int MinutesRemaining,    // (PeriodEndUtc - now), matches the daily event's unit
    string PeriodKey) : IIntegrationEvent;   // e.g. "W:2026-22" — for Option-A period dedupe
```

Notes on units (match the daily event): the daily event computes `MinutesRemaining = (int)(lookaheadEnd - nowUtc).TotalMinutes` (window-relative). For the weekly event, compute **`MinutesRemaining` from the actual mission**: `(int)(mostUrgent.PeriodEndUtc - nowUtc).TotalMinutes` — more accurate for a "بيخلص قريب" message, and "remaining" in the copy refers to **work remaining** (`Target - Progress`) which the consumer derives. Keep `PeriodKey` only if Option A is chosen; drop it (and keep `MinutesRemaining` + Progress/Target) if Option B. `PeriodKey` is a stable per-period string, not PII (`StudentMission.PeriodKey`, e.g. `"W:2026-22"`).

---

## Notifications consumer + category

**New enum member — verified value:** `NotificationCategory` currently ends at `ReviewReminder = 7` (`backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Enums/NotificationCategory.cs`). **Add `WeeklyChallenge = 8`** (next free non-zero int).

**Enum-0 trap is N/A.** Verified `NotificationConfig` (`…Infrastructure/Persistence/Configurations/NotificationConfig.cs:37-41`): `Category` is stored as `int` with `.HasSentinel((NotificationCategory)(-1))` and `.HasDefaultValueSql("6")`. The sentinel is `-1`, so EF always sends the real value for any member ≠ -1, including 8. **No migration, no config change, no DDL** — `WeeklyChallenge = 8` "just works" (this is exactly why the P9-06 recap needed the sentinel fix: `WeeklyReport = 0` collided with the CLR default; value 8 has no such collision).

**Consumer:** new `WeeklyMissionReminderIntegrationEventHandler` in `…Notifications.Application/IntegrationEventHandlers/Reengagement/`, a near-verbatim copy of `DailyMissionReminderIntegrationEventHandler`:
- `INotificationHandler<WeeklyMissionReminderIntegrationEvent>`
- resolve parent (`IParentChildQuery.FindParentForChildAsync`; skip+log if null)
- `category = NotificationCategory.WeeklyChallenge`; `const string code = "WEEKLY_CHALLENGE_REMINDER"`
- `GetOrDefaultPrefsAsync` → `CountSentTodayAsync` → `ReengagementEvaluator.Evaluate` (skip+log `not_eligible`)
- dedupe: **Option A** → `_dedupeStore.TryAcquireTierAsync(ev.StudentId, $"WEEKLY_CHALLENGE:{ev.PeriodKey}", ttl, ct)`; **Option B** → existing `TryAcquireDedupeAsync(..., ev.OccurredOnUtc, ...)`
- `GetLocaleAsync` → `BuildMessage(..., ("remaining", (ev.Target - ev.Progress).ToString()), ("progress", ev.Progress.ToString()), ("target", ev.Target.ToString()))`
- `_dispatcher.DispatchAsync` → log `analytics.reengagement.sent`
- full try/catch fail-soft (ADR-0002); `ILoggerManager` (not `ILogger<T>`)

**Auto-registration confirmed:** the consumer auto-registers via the `Notifications.Application` assembly MediatR scan — the daily handler in the same folder already fires, so `Notifications.Application` is in the host `AddCrossModuleMediatR` set. No host wiring change. Gamification's `IPublisher.Publish` from the job already reaches Notifications handlers (the daily reminder proves the path).

**New `ReengagementCopyTemplates` entries** (`…Notifications.Domain/Templates/ReengagementCopyTemplates.cs`) — Arabic-first, child-safe, encouraging, placeholders `{remaining}` (work left) and optionally `{progress}`/`{target}`:

```
[$"WeeklyChallenge:WEEKLY_CHALLENGE_REMINDER:{ArEg}"] = (
    "تحدي الأسبوع بيخلص!",
    "⏳ تحدي الأسبوع بيخلص — لسه ناقصك {remaining}! اخلصه قبل ما يفوت 💪"),
[$"WeeklyChallenge:WEEKLY_CHALLENGE_REMINDER:{EnUs}"] = (
    "Weekly challenge ending soon!",
    "⏳ Your weekly challenge ends soon — {remaining} to go! Finish it before time's up 💪"),
```

**Arbiter (P9-07) wiring** (`…Notifications.Infrastructure/Reengagement/NudgeArbiter.cs`) — mirror how `WEEKLY_RECAP` was added:
- **Priority order** (`DefaultPriorityOrder`, config `Notifications:PriorityOrder`): currently `"StreakAtRisk,DailyMission,LapseWinBack,Achievement,WeeklyReport"`. Add `WeeklyChallenge`. Recommend placing it **after `DailyMission`** (it's a time-boxed completion nudge like the daily one but lower urgency than streak loss): `"StreakAtRisk,DailyMission,WeeklyChallenge,LapseWinBack,Achievement,WeeklyReport"`. (Categories not listed default to lowest rank anyway, so omission is non-fatal — but list it explicitly for deterministic priority.) Confirm placement via OQ-2.
- **Cooldown** (`GetCooldownTtl`): add a `"WEEKLY_CHALLENGE_REMINDER" => WeeklyCooldownHours` (168h) arm in the switch, consistent with `"WEEKLY_RECAP" => WeeklyCooldownHours`. This caps push at ≤1/week per type even under Option B's day-keyed dedupe.

**Inbox-only in v1** (emergent — not in the parent-managed push set / `ReengagementCategories`), consistent with `WeeklyReport`/`ReviewReminder`/`Achievement`. The handler dispatches normally; the dispatcher's gate keeps it inbox-only because `prefs.Push` defaults false for non-managed categories. Push enables when P9-04 FE per-type toggles ship. No prefs-schema change.

---

## Cross-module + isolation

- **Spans Gamification (emit) + Notifications (consume) + Shared.Contracts (new event) + the enum + the arbiter** — all serialized shared-file edits per PARALLELISM.md. Serialize this wave; **Gamification/Shared.Contracts batch first, then Notifications.**
- **Module isolation (rule 1) holds:** Notifications consumes only the `Shared.Contracts.Gamification.WeeklyMissionReminderIntegrationEvent` record — no reference to `Gamification.Domain`, no cross-module FK. The event carries denormalized scalars (Progress/Target/MinutesRemaining/PeriodKey), so the consumer never touches a Gamification entity.
- **`AddCrossModuleMediatR`:** `Notifications.Application` is already registered (the daily handler fires) — no change. Gamification's job already publishes cross-module via `IPublisher` — no change.

---

## Affected modules & data

| Entity / artifact | New vs existing | Notes |
|---|---|---|
| `WeeklyMissionReminderIntegrationEvent` | **New** (`Shared.Contracts/Gamification`) | Serialized shared-file edit. PII-light (ids/counts/period string). |
| `StreakAtRiskJob` Pass 3 | **Extend** (Gamification.Infrastructure) | New weekly scan + publish loop, mirrors Pass 2. Fail-soft per publish. |
| `ReengagementOptions.WeeklyMissionReminderLookaheadHours` | **Extend** (config only, default 48) | Section `Gamification:Reengagement`. No DB. |
| `WeeklyMissionReminderIntegrationEventHandler` | **New class** (Notifications.Application) | Mirrors `DailyMissionReminderIntegrationEventHandler`. |
| `NotificationCategory.WeeklyChallenge = 8` | **New enum member** | Stored as int; sentinel = -1 → **NO migration**. |
| `ReengagementCopyTemplates` | **Extend** (data only) | +`WeeklyChallenge:WEEKLY_CHALLENGE_REMINDER` ar+en. No schema. |
| `NudgeArbiter` priority + cooldown | **Extend** (data/switch only) | Add to `DefaultPriorityOrder` + `GetCooldownTtl` switch (WeeklyCooldownHours). |
| Per-child prefs schema | **No change** | WeeklyChallenge inbox-only in v1 (not in `ReengagementCategories`). |

---

## Handoff → db-migration

**NONE.** Verified: `NotificationCategory` is stored as `int` with `HasSentinel((NotificationCategory)(-1))` — adding `WeeklyChallenge = 8` requires no DDL and no empty/sentinel migration (the sentinel fix already shipped with P9-06 recap; value 8 ≠ 0 ≠ -1). Progress/Target/time come from the scan into the event payload — no new column. Milestone/period dedupe lives in Redis. **db-migration: SKIP.**

---

## Handoff → backend-feature

**Mirror existing shapes; Option C (EF only in Infrastructure); fail-soft per ADR-0002; `ILoggerManager`; UTC discipline; no new design pattern (rule 8).**

**Batch 1 — Gamification + Shared.Contracts (serialized, lands first):**
1. Add `WeeklyMissionReminderIntegrationEvent` to `Shared.Contracts/Gamification` (shape above).
2. Add `WeeklyMissionReminderLookaheadHours` (default 48) to `ReengagementOptions`.
3. Add **Pass 3** to `StreakAtRiskJob.RunAsync`: scan `StudentMissions` where `MissionType == Weekly && Status IN (NotStarted, InProgress) && PeriodEndUtc > now && PeriodEndUtc <= now + WeeklyMissionReminderLookaheadHours`; per student pick the most-urgent (min `PeriodEndUtc`) row; `Take(500)` + WARN page-guard; publish one `WeeklyMissionReminderIntegrationEvent` per student via `IPublisher.Publish`, **fail-soft per publish**; log a `weeklyReminderCount/published/failed` summary line consistent with the existing two passes. UTC: use the same `nowUtc = _clock.UtcNow.ToUniversalTime()` already in the method.

**Batch 2 — Notifications (after Batch 1 contract):**
4. Add `NotificationCategory.WeeklyChallenge = 8`.
5. Add `WeeklyMissionReminderIntegrationEventHandler` (mirror the daily handler; category `WeeklyChallenge`; code `WEEKLY_CHALLENGE_REMINDER`; dedupe per OQ-1; placeholders `{remaining}`/`{progress}`/`{target}`; fail-soft).
6. Add the two `ReengagementCopyTemplates` entries (ar+en).
7. Add `WeeklyChallenge` to `NudgeArbiter` `DefaultPriorityOrder` + a `WEEKLY_CHALLENGE_REMINDER => WeeklyCooldownHours` arm in `GetCooldownTtl`.

No new HTTP endpoint, no controller, no DTO, no validator (event-driven only).

---

## Handoff → frontend

**None for this wave (backend-only).** The parent per-type toggle for the WeeklyChallenge category is **P9-04-FE**'s concern (`tasks/Frontend/student-app/Phase-9-Notifications/P9-04-FE.md` already lists "weekly challenge" as its own category in the parent catalog, per the discovery). When P9-04 lands per-type toggles, the v1 "inbox-only WeeklyChallenge" decision flips to parent-controllable push. Note for the planner to track against P9-04; not built here.

---

## Tests

**Gamification (unit, mirror existing job tests):**
- In-window, incomplete weekly mission (`Status` NotStarted/InProgress, `PeriodEndUtc` within lookahead) → **one** `WeeklyMissionReminderIntegrationEvent` with correct `Progress`/`Target`/`MinutesRemaining`/`PeriodKey`.
- Completed (`Status==Completed`) or Expired weekly mission → **no** event.
- Out-of-window (`PeriodEndUtc` beyond lookahead, or already past) → **no** event.
- Daily mission in window → **no** weekly event (cadence filter).
- Multiple weekly missions for one student → **one** event (most-urgent), not N.
- Publish failure for one student does not block the next (fail-soft).
- Page-guard WARN at 500.

**Notifications (unit + integration, mirror P9_06 recap + daily-reminder tests):**
- Event → one `WeeklyChallenge` inbox row, code `WEEKLY_CHALLENGE_REMINDER`, `Category == WeeklyChallenge` end-to-end (assert the stored int = 8, guarding the sentinel mapping like P9-06 TC-04).
- ar + en copy rendered with `{remaining}` (= Target - Progress) substituted; Arabic-first default.
- Dedupe: second delivery within the same period (Option A) / same day (Option B) → suppressed (`dedupe_hit`).
- Not-eligible (prefs off / daily cap / quiet hours) → `not_eligible`, no dispatch.
- No parent for child → skip+log, no throw.
- Inbox-only in v1 (push not sent because category not in the managed push set / `prefs.Push` default false).
- No PII in the inbox row or logs (ids/counts only).
- Arbitration still holds (routes through `NudgeDispatcher` gate; inbox always written).

`api-tester` runs the integration slice (no new HTTP endpoint — event→nudge pipeline tests, mirror `P9_06_HabitLoop_Tests` / `P9_07_NudgeArbitration_Tests`). `security-auditor` (light): new event PII-light, fail-soft producer never throws into the job, module isolation, no new auth surface.

---

## Open questions / assumptions / risks

**OQ-1 — dedupe scope: per-PERIOD (Option A) vs per-DAY (Option B)?** A daily job emitting a weekly nudge needs across-run dedupe. **Recommend Option A** (period-scoped key via `ReengagementDedupeStore.TryAcquireTierAsync`, no day component, TTL ≈ lookahead+margin; requires `PeriodKey` in the event) — one well-timed countdown per period, matching the story's "ending-soon (countdown)" intent. Option B (existing day-keyed dedupe) is simpler but allows up to ~2 inbox rows over a 48h window (push still capped by the weekly cooldown). Confirm.

**OQ-2 — arbiter priority placement.** Recommend `WeeklyChallenge` **after `DailyMission`, before `LapseWinBack`**. Confirm, or accept the default-lowest-rank behavior if unlisted.

**OQ-3 — lookahead default.** Recommend **48h**. Confirm (12-72h all reasonable; config-driven so tunable without deploy).

**OQ-4 — weekly completion copy.** Recommend **NOT** enriching `MissionCompletedIntegrationEvent` with cadence in this wave (the generic `MISSION_COMPLETED` Achievement nudge already celebrates weekly completions; daily-flavoured copy is imperfect but encouraging, not broken). Confirm you accept the slightly-daily-flavoured completion copy for weekly missions, or carry "weekly-specific completion copy" as a small separate follow-up.

**Rule-8 (design pattern) check — CONFIRMED no concern.** Mirrors the existing daily-reminder scan/publish (a third pass in the same job), the existing handler/helper/dispatcher shapes, the existing arbiter config arms. No Strategy/Factory/new abstraction. No rule-8 approval needed.

**Risk — daily cron drives weekly nudge.** Mitigated by lookahead + OQ-1 dedupe. Document so the reviewer understands the design.

**Risk — Shared.Contracts / enum / arbiter serialization.** Three shared-file edits — serialize per PARALLELISM.md (Gamification/contract batch first, then Notifications). No concurrent edits to these files from other in-flight stories.

**Risk — `MinutesRemaining` units.** The daily event computes window-relative minutes; recommend the weekly event computes **mission-relative** (`PeriodEndUtc - now`) for an accurate countdown. Note the intentional divergence so a reviewer doesn't "fix" it back to window-relative.

---

## Recommended pipeline order (first cut — the `planner` finalizes)

Backend-only, cross-module wave. **No `designer` / `frontend` / `frontend-e2e-tester`.**

**Gate OQ-1..4 with the lead first.** Then:

1. **`db-migration`** — **SKIP** (verified no DDL; sentinel handles the new non-zero enum value).
2. **`backend-feature` Batch 1** — Shared.Contracts event + `ReengagementOptions` config + `StreakAtRiskJob` Pass 3 (Gamification). Serialized shared-file edit on Shared.Contracts.
3. **`backend-feature` Batch 2** — `NotificationCategory.WeeklyChallenge = 8` + consumer + templates + arbiter priority/cooldown (Notifications). After Batch 1 compiles against the contract.
4. **`api-tester`** — event→nudge integration slice (no new endpoint); mirror `P9_06_HabitLoop_Tests`.
5. **`security-auditor`** — light scope: PII-light event, fail-soft producer, module isolation, no new auth surface.
6. **`reviewer`** — gate against this brief's ACs + CONVENTIONS.md (Option C, fail-soft, rule-1 isolation, rule-8 no new pattern, completion-redundancy decision, inbox-only-v1), including api-tester + security-auditor results.
7. **`committer`** — branch `feat/P9-06-weekly-challenge`; PR with full description; **update `docs/dev/HANDOFF.md`**: supersede the "⛔ Weekly challenge (DEFERRED): no per-child challenge entity exists" note (line 58) with "weekly missions ARE the per-child challenge → ending-soon reminder shipped"; record new config `Gamification:Reengagement:WeeklyMissionReminderLookaheadHours` (48), `NotificationCategory.WeeklyChallenge = 8`, the WeeklyChallenge priority/cooldown entries, and the completion-nudge-redundancy decision.
