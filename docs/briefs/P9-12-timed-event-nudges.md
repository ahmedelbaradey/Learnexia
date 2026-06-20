# Pipeline Brief — P9-12 Timed-event nudges

## Summary & traceability
- **Task (one line):** Light up the deferred timed-event nudges in the Notifications module by adding **four** participation-driven re-engagement consumers — an event-live "join" 1→many fan-out (consume `TimedEventStartedIntegrationEvent` → recruit the P4-12 eligibility cohort), plus three 1:1 participant consumers (progress/halfway, ending-soon, completion) of the new P4-12 per-student integration events — each gated by the existing P9-07 arbiter + parent toggle + global push budget, inbox-only in v1.
- **User story:** `user-stories/Phase-9-Notifications/P9-12-timed-event-nudges.md` (source of truth).
- **Task file:** `tasks/Backend/Phase-9-Notifications/P9-12-BE.md` (BE-1 … BE-6).
- **FR-IDs:** FR-GM-8, FR-GM-9 (timed events — SRS §4.6). **Depends on P4-12** (participation model + eligibility query + 3 per-student events — shipped, PR #201). **Pairs with P4-11** (`TimedEventStartedIntegrationEvent`).
- **BRD goal:** G3 (engagement / habit loop) — closes the timed-event habit loop so the limited-time mechanic actually brings students back **during the window** instead of being invisible background math.
- **Epic / phase / sprint:** Notifications Module · Phase 9 — Notifications (post-MVP). SP 3.
- **Product-decision overrides in play:** parent-driven onboarding (parent owns the per-child reengagement prefs the nudges consult); no teacher role. Neither conflicts.

## Business context & value
- **Who benefits:** the **student** (a timed event becomes a goal they hear about when it goes live, see climb at halfway, get a "you're almost there, clock's ticking" prompt before it closes, and a congratulation when they finish); indirectly the **parent** (these nudges are parent-toggleable via the existing TimedEvent category) and **ops** (config-tunable cadence/copy).
- **Value:** P4-11 shipped timed events as a platform-wide XP multiplier with start/end + scope but **no nudge fired** — the started event carries no recipient, so the loop never closed. P9-05 explicitly **deferred** the timed-event handlers for exactly this reason (no recipient seam — see Resolution #1). P4-12 supplied the per-child participation model + eligibility cohort + per-student lifecycle events; P9-12 consumes them to finally fire the nudges.
- **Success measured by:** join nudge reaches the scope-eligible cohort (bounded, no blind blast, no upfront fan-out); progress/ending/completion nudges reach the right participant; each phase fires at most once per (child, timed-event); all four respect the parent toggle + global push budget; inbox-only posture holds; ar/en copy renders with no PII.

## Acceptance criteria (testable)
1. **Event-live "join" nudge** — when `TimedEventStartedIntegrationEvent` is published, the handler recruits the cohort via `IEligibleStudentsForTimedEventQuery.GetEligibleAsync(TimedEventId, OccurredOnUtc)` and dispatches a `TimedEvent/TIMED_EVENT_LIVE` nudge **per eligible student** (loop), bounded by the query's `Take(500)`. **Never a blind all-active blast; never a materialized upfront fan-out** (cohort computed on demand at send time). A student with no linked parent is skipped (fail-soft), and one student throwing does **not** abort the rest of the loop.
2. **Progress (halfway) nudge** — `TimedEventParticipationProgressIntegrationEvent` → one `TimedEvent/TIMED_EVENT_PROGRESS` nudge to that participant, with `{progress}`/`{target}` substituted.
3. **Ending-soon nudge** — `TimedEventParticipationEndingSoonIntegrationEvent` → one `TimedEvent/TIMED_EVENT_ENDING` nudge to a close-but-incomplete participant, with `{remaining}` (= `Target − Progress`) and `{minutes}` (= `MinutesRemaining`) substituted.
4. **Completion nudge** — `TimedEventParticipationCompletedIntegrationEvent` → one `TimedEvent/TIMED_EVENT_COMPLETED` celebration nudge to that participant.
5. **Category + posture** — all four use `NotificationCategory.TimedEvent` (new value `9`, no migration). The category is already listed in the P9-04 parent catalog. **Inbox-only in v1**: TimedEvent is NOT in the dispatcher's parent-managed push set (`ReengagementCategories`), so push falls out by `prefs.Push` default; the handler dispatches normally and the dispatcher gate enforces the posture.
6. **Dedupe** — each phase fires **at most once per (child, timed-event, phase)** via `TryAcquireTierAsync` with a tier key combining `Code` + `TimedEventId` + a phase token, so the four phases for the same event never collide and re-delivery of any one event does not re-fire.
7. **Copy** — ar-first + en fallback templates for all four codes live in `ReengagementCopyTemplates` (NOT resx), child-safe, never-shaming, personalized with event/progress/countdown placeholders. The nudge names the event by **`Code`** (no cross-module name lookup — see Resolution #5). Deep-link by `Code` (P9-02 convention).
8. **Arbitration + analytics** — all four pass through `INudgeDispatcher` → `INudgeArbiter` (global push budget + per-type cooldown). `TimedEvent` is added to the arbiter priority order with per-phase cooldowns. Sends/suppresses/opens flow through the existing analytics log lines.

---

## Current-state investigation (verified, with citations)

### Resolution #1 — existing TimedEvent consumers (CRITICAL, avoid duplication)
**Finding: NO working TimedEvent consumer exists. The only TimedEvent file in Notifications is a deferral placeholder.**
- `Modules/Notifications/.../IntegrationEventHandlers/Reengagement/TimedEventNudgeDeferralNote.cs` is a **comment-only file** (no class) recording that **P9-05 intentionally did NOT build** the `TimedEventStartedIntegrationEvent` / `TimedEventEndedIntegrationEvent` handlers, because "both events are platform-wide broadcasts … but NO StudentId … there is no 'active student list' seam." It explicitly defers to "a follow-up story" needing a new active-student seam + budget-aware bulk dispatch.
- Grep of the whole Notifications module for `TimedEvent` returns only that note + unrelated infra (`MessageRequest`, migrations) — **no `INotificationHandler<TimedEvent…>` anywhere**.
- The 18 existing handlers in `IntegrationEventHandlers/Reengagement/` cover Badge/Mission/Streak/Hearts/League/Level/Lapse/Review/WeeklyMission/WeeklyRecap — **none for any TimedEvent**.

**→ Final phase scope: P9-12 owns ALL FOUR phases (join + progress + ending-soon + completion).** Nothing to descope. P9-05 did not ship the started nudge — it deferred it, and P9-12 is the named follow-up. **Action item:** when building, **delete `TimedEventNudgeDeferralNote.cs`** (its own comment says "do not remove until the handlers are built" — they now are).

### Resolution #2 — handler shapes confirmed (rule 8 — mirror, no new pattern)
- **1:1 consumers (progress/ending/completion)** mirror `WeeklyMissionReminderIntegrationEventHandler` field-for-field: ctor deps `IChildReengagementPreferenceService`, `INotificationInboxService`, `IReengagementDedupeStore`, `IParentChildQuery`, `INudgeDispatcher`, `ISystemClock`, `ILoggerManager`, optional `IUserLookup`. Body: `FindParentForChildAsync` → null-skip → `GetOrDefaultPrefsAsync` → `CountSentTodayAsync` → `ReengagementEvaluator.Evaluate` → not-eligible-skip → `TryAcquireTierAsync` dedupe → `GetLocaleAsync` → `BuildMessage(...)` → `DispatchAsync` → analytics log; whole body in one try/catch (fail-soft, ADR 0002).
- **1→many join consumer** is the same per-student body, wrapped in a loop over `GetEligibleAsync(...)`. Adds one dep: `IEligibleStudentsForTimedEventQuery` (Shared.Contracts.Gamification). **Each student's send is in its own try/catch** so one failure doesn't abort the cohort (fail-soft per-student, as the story requires). This loop-over-a-cohort shape already exists in the Gamification jobs (`StreakAtRiskJob` per-student publish) — not a new pattern; it is a handler that calls a read seam then loops.

### Resolution #3 — NotificationCategory value
`Domain/Enums/NotificationCategory.cs` currently ends at `WeeklyChallenge = 8` (after `ReviewReminder = 7`). **Next free value = `TimedEvent = 9`.** No migration needed — the Category column is stored as int with `HasSentinel(-1)`; value `9 != 0 != -1` persists correctly (verified by the identical reasoning the enum comment records for value 8). The P9-04 parent catalog already lists "timed event" as a category.

### Resolution #4 — dedupe-key design (per-(child, event, phase))
The store's `TryAcquireTierAsync(studentId, tierCode, ttl, ct)` is the right primitive (mirrors P9-06's period-scoped dedupe and P9-08's per-episode ladder). The tier code must encode **TimedEventId + phase** so (a) the four phases for the same event don't share a lock, and (b) two concurrent timed events don't collide. Use the event `Code` as the human-readable component and `TimedEventId` as the stable scoping component:

| Phase | Handler | Tier key (`tierCode`) | TTL | Rationale |
|---|---|---|---|---|
| Join / live | `TimedEventStartedIntegrationEventHandler` | `TIMED_EVENT_LIVE:{TimedEventId}` | event-window length, capped (e.g. `EndUtc − StartUtc`, max 72h) | One join nudge per student per event. TTL spans the window so a re-published started event doesn't re-recruit. |
| Progress | `TimedEventParticipationProgressIntegrationEventHandler` | `TIMED_EVENT_PROGRESS:{TimedEventId}` | 72h (mirror WeeklyChallenge) | Halfway is latched once by the producer, but the dedupe backstops re-delivery. |
| Ending-soon | `TimedEventParticipationEndingSoonIntegrationEventHandler` | `TIMED_EVENT_ENDING:{TimedEventId}` | 72h | Ending-soon scan (StreakAtRiskJob Pass 4) is a daily/recurring sweep — the tier key ensures at most one ending nudge per (child, event) even if the sweep runs twice in the lookahead. |
| Completion | `TimedEventParticipationCompletedIntegrationEventHandler` | `TIMED_EVENT_COMPLETED:{TimedEventId}` | 72h | Completion latches once at the producer; dedupe backstops re-delivery. |

(The store key shape is `nudge-tier:{studentId}:{tierCode}`, so `studentId` is already in the key — the `tierCode` above only needs the phase + event scoping. The dispatch is fail-open on Redis outage, consistent with existing handlers.)

### Resolution #5 — copy + how the nudge names the event
The three P4-12 events and the started event carry **`Code` (the event code string, e.g. `WEEKEND_BOOST`), not a display name.** **Recommendation: do NOT add a cross-module name lookup** (would need a Gamification name seam → module-isolation friction + extra read per nudge). Instead use **generic, event-agnostic copy** that reads naturally without naming the specific event (mirrors how `WEEKLY_CHALLENGE_REMINDER` says "تحدي الأسبوع" / "your weekly challenge" generically). The `Code` is carried in `DataJson`/deep-link for the FE to resolve the event name on the destination surface (P9-02). Draft templates (final wording is the designer/lead's call; `ReengagementCopyTemplates`, ar primary + en fallback, child-safe, never-shaming):

| Code | ar-EG (title / body) | en-US (title / body) | Placeholders |
|---|---|---|---|
| `TIMED_EVENT_LIVE` | "تحدي محدود المدة شغّال! 🔥" / "🔥 في تحدي محدود المدة شغّال دلوقتي — اكسب نقاط مضاعفة قبل ما الوقت يخلص!" | "A limited-time event is live! 🔥" / "🔥 A limited-time event is on now — earn bonus XP before the clock runs out!" | (none) |
| `TIMED_EVENT_PROGRESS` | "نص الطريق! ⚡" / "⚡ وصلت لنص التحدي — {progress} من {target}! كمّل وانت في الجو 💪" | "Halfway there! ⚡" / "⚡ You're halfway through — {progress} of {target}! Keep the momentum 💪" | `{progress}`, `{target}` |
| `TIMED_EVENT_ENDING` | "التحدي بيخلص قريب! ⏳" / "⏳ باقي خطوة وتخلّص التحدي — لسه ناقصك {remaining}! اخلصه قبل ما يفوت" | "Event ending soon! ⏳" / "⏳ Almost done — {remaining} to go! Finish before time's up" | `{remaining}` (= Target−Progress), `{minutes}` (optional) |
| `TIMED_EVENT_COMPLETED` | "خلّصت التحدي! 🎉" / "🎉 مبروك! خلّصت التحدي محدود المدة — إنجاز رائع، استمر!" | "Event complete! 🎉" / "🎉 Congrats! You finished the limited-time event — awesome work, keep it up!" | (none) |

### Resolution #6 — arbiter enrolment
`NudgeArbiter.cs` defaults `DefaultPriorityOrder = "StreakAtRisk,DailyMission,WeeklyChallenge,LapseWinBack,Achievement,WeeklyReport"` and a `GetCooldownTtl(typeCode)` switch. **Add `TimedEvent` to the priority CSV near `WeeklyChallenge`** (both are time-boxed, time-sensitive engagement prompts). Recommended order: `StreakAtRisk,DailyMission,TimedEvent,WeeklyChallenge,LapseWinBack,Achievement,WeeklyReport` (TimedEvent slightly ahead of WeeklyChallenge because its window is shorter/more perishable — confirm with lead). Add per-phase cooldown cases to the switch:
- `TIMED_EVENT_LIVE` → 24h (`DefaultCooldownHours`)
- `TIMED_EVENT_PROGRESS` → 24h
- `TIMED_EVENT_ENDING` → 24h
- `TIMED_EVENT_COMPLETED` → 24h
(Per-(child,event,phase) dedupe is the primary once-per-event guard; cooldown is the secondary push-channel rate limit, exactly as WeeklyChallenge layered them.)

### Resolution #7 — inbox-only v1
`ChildReengagementPreferenceService.ReengagementCategories` = `[StreakAtRisk, DailyMissionReminder, LapseWinBack]` only. **No change** → TimedEvent is not parent-push-managed, `prefs.Push` defaults false for it, push falls out automatically at the dispatcher. Consistent with ReviewReminder/WeeklyChallenge/WeeklyReport.

### Resolution #8 — cross-module MediatR
Confirmed: `Host/.../Extensions/MediatRExtensions.cs` `AddCrossModuleMediatR()` already registers `Learnexia.Modules.Notifications.Application.AssemblyReference`. The four new `INotificationHandler<…>` consumers auto-register via the host scan — **no host edit needed**.

---

## Affected modules & data (new vs existing)
- **Module:** Notifications only. **No new module, no new entity, no migration, no DB change.** Consumes `Shared.Contracts.Gamification` events + the `IEligibleStudentsForTimedEventQuery` seam (already in `Shared.Contracts`). **No Gamification.Domain reference** (rule 1).
- **New files (4 handlers):** `TimedEventStartedIntegrationEventHandler.cs`, `TimedEventParticipationProgressIntegrationEventHandler.cs`, `TimedEventParticipationEndingSoonIntegrationEventHandler.cs`, `TimedEventParticipationCompletedIntegrationEventHandler.cs` (all in `Application/IntegrationEventHandlers/Reengagement/`).
- **File deleted:** `TimedEventNudgeDeferralNote.cs` (handlers now built).
- **Existing files edited (SHARED — serialize, see risks):**
  - `Domain/Enums/NotificationCategory.cs` — add `TimedEvent = 9`.
  - `Domain/Templates/ReengagementCopyTemplates.cs` — add 8 entries (4 codes × ar/en).
  - `Infrastructure/Reengagement/NudgeArbiter.cs` — `DefaultPriorityOrder` + 4 cooldown switch cases.

## Handoff → db-migration
**None.** No schema change. New category value `9` is a non-zero int stored under `HasSentinel(-1)` — no migration required (same as values 7 and 8). The db-migration stage is **skipped** for this story.

## Handoff → backend-feature
- **BE-1 — Join consumer** (`TimedEventStartedIntegrationEventHandler : INotificationHandler<TimedEventStartedIntegrationEvent>`): inject the standard 8 deps + `IEligibleStudentsForTimedEventQuery`. In `Handle`: outer try/catch around `GetEligibleAsync(ev.TimedEventId, ev.OccurredOnUtc, ct)`; loop each `studentId`, **each iteration in its own try/catch** running the per-student body (`FindParentForChildAsync` → prefs → evaluator → `TryAcquireTierAsync($"TIMED_EVENT_LIVE:{ev.TimedEventId}")` → BuildMessage(`TimedEvent`,`TIMED_EVENT_LIVE`) → `DispatchAsync`). Cohort is already `Take(500)`-bounded by the seam; log a count line. Category `NotificationCategory.TimedEvent`.
- **BE-2 — Progress consumer** (`…ProgressIntegrationEvent`): mirror `WeeklyMissionReminderIntegrationEventHandler` exactly; tier key `TIMED_EVENT_PROGRESS:{ev.TimedEventId}`; placeholders `("progress", ev.Progress)`, `("target", ev.Target)`.
- **BE-3 — Ending-soon consumer** (`…EndingSoonIntegrationEvent`): same shape; tier key `TIMED_EVENT_ENDING:{ev.TimedEventId}`; placeholders `("remaining", ev.Target - ev.Progress)`, `("progress", ev.Progress)`, `("target", ev.Target)`, optional `("minutes", ev.MinutesRemaining)`.
- **BE-4 — Completion consumer** (`…CompletedIntegrationEvent`): same shape; tier key `TIMED_EVENT_COMPLETED:{ev.TimedEventId}`; no placeholders (event carries no scalars beyond Code).
- **BE-5 — Copy:** add the 8 `ReengagementCopyTemplates` entries above (ar + en for all 4 codes), child-safe/never-shaming, no PII, no raw placeholders left after Render. (NOT resx.)
- **BE-6 — Arbiter + dedupe:** edit `NudgeArbiter.DefaultPriorityOrder` (insert `TimedEvent`) + add 4 cooldown cases; the dedupe tier keys live in each handler (as constants, mirroring WeeklyChallenge's `PeriodDedupePrefix`). Sends/opens already flow through the existing dispatcher + analytics log lines (P9-11 sink) — no new emit needed.
- **DataJson / deep-link (P9-02):** existing handlers pass `DataJson: null` via the helper. If P9-02 deep-linking by `Code` is wanted on these nudges, pass a small JSON (`{"timedEventId":…, "code":"…"}`) — **confirm with lead** whether to extend `BuildMessage` for this (OQ-2) or keep null for v1 parity.
- **Rules:** module isolation (Shared.Contracts.Gamification only, no Gamification.Domain); `ILoggerManager` (not `ILogger<T>`); fail-soft whole-body try/catch + **per-student try/catch in the join loop**; rule 8 (mirror `WeeklyMissionReminderIntegrationEventHandler`, no new pattern); copy in `ReengagementCopyTemplates` ar+en.

## Handoff → frontend
**No backend-driven FE work in this pipeline.** Per the task file, there are no new endpoints — the TimedEvent category is already parent-managed (P9-04) and inbox rendering is generic. FE-side copy surfacing + deep-link routing by `Code` is a separate FE lead's surface (out of scope here).

## Open questions / assumptions / risks
- **OQ-1 (arbiter placement — LOW):** Recommend `TimedEvent` just ahead of `WeeklyChallenge` in the priority CSV (shorter, more perishable window). Confirm, or place it after WeeklyChallenge. Trivially tunable post-deploy via `Notifications:PriorityOrder`.
- **OQ-2 (deep-link DataJson — LOW):** Existing reengagement handlers pass `DataJson: null`. Confirm whether P9-12 should populate `DataJson` with `{timedEventId, code}` for P9-02 deep-linking now, or keep null for v1 parity and let FE deep-link via the inbox category. Recommend: populate `{timedEventId, code}` (cheap, no schema change, enables the deep-link the AC mentions) — but flag because no existing handler does this yet.
- **OQ-3 (join TTL — LOW):** The join dedupe TTL should span the event window so a re-published started event doesn't re-recruit. `TimedEventStartedIntegrationEvent` carries `StartUtc`/`EndUtc`, so the handler can compute `EndUtc − now` (capped, e.g. max 72h). Confirm the cap, or use a flat 72h like the other tiers. Recommend window-derived, capped.
- **OQ-4 (ending-soon "incomplete" filter — already handled upstream):** The story says ending-soon targets "close-but-incomplete." That filter lives in the **P4-12 producer** (StreakAtRiskJob Pass 4 only emits for `State == InProgress` participations) — the consumer trusts the event and does not re-check. Assumption recorded; no consumer-side state lookup (would need a Gamification read seam — avoid).
- **Assumption:** Only `AllXp`-scope timed events produce participation today (per P4-12), so the eligibility cohort and the participant events already reflect the only wired scope. `MissionXp`/`LeagueXp` are forward-compat/no-op upstream; P9-12 inherits that — no consumer-side scope logic.
- **Risk (shared-file contention — serialize):** `NotificationCategory.cs`, `ReengagementCopyTemplates.cs`, `NudgeArbiter.cs` are shared with the whole reengagement stack (P9-05/06/08/09). Run P9-12 on its own `feat/P9-12-timed-event-nudges` branch with no parallel sibling touching these three files. The four new handler files are net-new (no contention).
- **Risk (rule 8):** Nothing here needs a new design pattern — every piece mirrors `WeeklyMissionReminderIntegrationEventHandler` (1:1) or a read-seam-then-loop (the join handler, same idea as the Gamification jobs). If backend-feature finds it can't fan out cleanly without an abstraction (Strategy/Factory/etc.), **STOP and ask the lead** — do not introduce one unilaterally.

## Handoff → api-tester
Mirror `P9_06_WeeklyMissionReminder_Tests` (publish integration event via `IPublisher` → assert Notification inbox row). Per-phase coverage:
- **Join fan-out:** seed N (e.g. 3) recently-active students under parents + 1 student outside the eligibility window; publish `TimedEventStartedIntegrationEvent`; assert exactly the N eligible students each got one `TIMED_EVENT_LIVE` row (Category=9), the out-of-window student got none; one orphan (no parent) in the cohort → no throw, others still receive.
- **Progress / ending / completion 1:1:** each event → one row of the right code + Category=9; placeholders substituted (no raw `{progress}`/`{remaining}`); negative: not-eligible (daily cap reached) → no row.
- **Dedupe per phase:** same event published twice → exactly one row per phase; publishing all four phases for the same TimedEventId → four distinct rows (phases don't collide); two different TimedEventIds → independent locks.
- **Inbox-only v1:** push bit not set even with a device token registered.
- **ar/en copy:** ar-locale child → Arabic body text; en-locale child → English body; no raw placeholders.
- **No PII:** Title/Body contain only generic copy + numeric scalars; no child name/email.
- **Category sentinel guard:** persisted Category column == 9 (not 6/System default), confirming `HasSentinel(-1)` covers value 9.

## Recommended pipeline order (first cut — `planner` finalizes)
1. **(no db-migration stage — no schema change.)**
2. **backend-feature** (Batch 1): enum value `TimedEvent=9`; 4 copy code pairs in `ReengagementCopyTemplates`; arbiter priority + 4 cooldowns; 4 handlers (BE-1 join fan-out + BE-2/3/4 1:1); delete `TimedEventNudgeDeferralNote.cs`. *(Internally: enum + copy + arbiter first, then the 4 handlers.)* Gate: solution builds.
3. **api-tester** (Batch 2, after BE): new `P9_12_TimedEventNudges_Tests` per the coverage above.
4. **security-auditor** (Batch 2, parallel): child-data + no-PII on the four nudges; the join fan-out must not leak across families (each recipient resolved via its own `FindParentForChildAsync`); opaque-ids-only honored (events already enforce it). *(Lighter audit — consumer-only, no new endpoint, no new data.)*
5. **reviewer** (gate): against the 8 acceptance criteria + CONVENTIONS + api-tester + security-auditor.
6. **committer** (after PASS): `feat/P9-12-timed-event-nudges` branch, conventional commit, push + open PR. Update `docs/dev/HANDOFF.md` (TimedEvent category=9, the four codes/tier keys, that the P9-05 deferral is now resolved + the note file deleted).

**No designer stage** — backend-only (no new UI surface; FE is a separate lead's copy/routing surface).
