# Pipeline Brief — P9 Notifications wave (P9-07 arbitration + P9-05 light-up + P9-08 comeback ladder)

> **Wave brief.** Three backend-only stories that all **EXTEND** the existing P4-09/P4-10/P4-11 re-engagement subsystem in the **Notifications** module. **No new foundation** — inbox, preferences, dispatcher, evaluator, dedupe store, copy templates and the 8 existing handlers already ship. This wave adds consumers, copy, a global push budget + arbitration layer, and a comeback escalation ladder ON TOP of them.
>
> | Story | One-line | SP | FR |
> |---|---|---|---|
> | **P9-05** | Wire 5 already-published gamification integration events to nudge handlers + ar/en templates. | 3 | FR-GM-8 |
> | **P9-07** | Add a global per-child daily **push budget** + cross-category **priority arbitration** + per-type **cooldowns** + suppress-reason logging (the anti-spam counterweight). | 5 | FR-GM-8 |
> | **P9-08** | Turn the single LapseWinBack nudge into an **idle-day escalation ladder** (~2d / ~5d / ~14d), copy-only. | 3 | FR-GM-8 (Gap L) |

---

## Summary & traceability

| Item | Value |
|---|---|
| User stories | `user-stories/Phase-9-Notifications/P9-05-light-up-existing-gamification-events.md`, `P9-07-nudge-arbitration-and-daily-budget.md`, `P9-08-comeback-escalation-ladder.md` |
| Task files | `tasks/Backend/Phase-9-Notifications/P9-05-BE.md`, `P9-07-BE.md`, `P9-08-BE.md` |
| FR-IDs | **FR-GM-8** (parent-controlled, quiet-hours, daily-cap, Arabic-first, never-shaming re-engagement notifications). P9-08 also closes business-gap **Gap L** (win-back has no escalation ladder). |
| NFR | **NFR-1** (no measurable latency degradation; nudge dispatch is async, fail-soft, off the producer's hot path). |
| BRD goals | **G1** (learning outcomes / habit) + **G3** (engagement / retention). |
| Epic / Phase | Notifications Module · Phase 9 (post-MVP). |
| Product decisions | Parent-driven onboarding (parent owns the per-child prefs + budget; child JWT cannot write). No teacher role. None conflict with this wave. |

**Dependency order within the wave:** P9-07 is the **counterweight** — P9-05's newly-lit types must not ship to production push without the arbitration budget. Recommend building **P9-07 first** (or at least merging it first), then P9-05 and P9-08 (which enrol into it). P9-08 depends on P9-07's arbitration enrolment but only loosely (it can build copy + tier logic in parallel and wire the arbitration map last).

---

## Business context & value

The Notifications module already turns 8 gamification/lapse signals into parent-gated, quiet-hours-respecting, deduped nudges (P4-09). But:

- **P9-05** — five integration events the **Gamification module already publishes** (`StudentLeveledUpIntegrationEvent`, `LeagueTierChangedIntegrationEvent`, `StreakFreezeConsumedIntegrationEvent`, `TimedEventStartedIntegrationEvent`, `TimedEventEndedIntegrationEvent`) currently **fire and nothing notifies** — there is no `INotificationHandler` consumer for them. Copy for `LEVELED_UP` and `LEAGUE_TIER_CHANGED` already exists in `ReengagementCopyTemplates`; freeze + timed-event copy is new. Value: every win pulls the child back into the app. Cheapest possible retention lever (the events already exist; this is pure wiring).
- **P9-07** — today eligibility is enforced **per-category only** (`ReengagementEvaluator`: both-channels-off / per-category `DailyCap` / quiet-hours). As P9-05/P9-06 grow the catalog to ~16 types, a child could receive many pushes/day with **no cross-category arbitration** — the fastest way for a parent to disable push and lose the habit channel forever. Value: "many TYPES, few SENDS" — protects the scarce push channel; gives the team suppress-reason telemetry to later kill types that don't drive returns.
- **P9-08** — P4-09 ships **one** LapseWinBack nudge ("وحشتنا! ارجع اليوم"). Mature retention loops escalate by idle-days. Value: a gentle→repair→fresh-start ladder fits how long the child has been away instead of repeating one line; never shaming.

**Success is measured by:** the 5 events produce nudges through the existing pipeline; pushes never exceed the per-child global daily budget; suppressed nudges still land in the inbox with a logged reason; lapse copy varies by idle-day tier and each tier fires at most once. Per-type send/open analytics make each type's return-rate measurable.

---

## "What exists today" grounding (the extension points — study these)

| Concern | File (cite) | What it does today / how this wave extends it |
|---|---|---|
| **Pure eligibility logic** | `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Services/ReengagementEvaluator.cs` | Static `Evaluate(prefs, nowUtc, sentsToday)` → `EvalResult(Eligible, NotEligibleReason)`. Reasons: `DisabledByParent`, `DailyCapReached` (per-category), `QuietHours`. **P9-07 extends this** (new reasons: budget-exhausted / priority-lost / cooldown) — do **not** fork. Per-category cap stays; global budget + cooldown layer on top. |
| **Dispatcher** | `backend/src/Modules/Notifications/.../Infrastructure/Reengagement/NudgeDispatcher.cs` + `Application/Abstractions/INudgeDispatcher.cs` | Always writes the in-app `Notification` row (the durable receipt); conditionally sends push if `ShouldPush` + active tokens; fail-soft; stamps `DeliveredChannels` bitmask (InApp=4, **Push=2**) + `SentAtUtc`. **Confirms P9-07 AC**: inbox is always written; only push is rationed. Arbitration must gate only the **push** decision (set `ShouldPush=false` on suppression) — the inbox row is untouched. |
| **Handler pattern (P9-05 mirrors)** | The 8 handlers in `.../Application/IntegrationEventHandlers/Reengagement/*Handler.cs` (e.g. `BadgeEarnedIntegrationEventHandler.cs`, `LapseWinBackIntegrationEventHandler.cs`) | Each: resolve parent (`IParentChildQuery.FindParentForChildAsync`) → load prefs (`GetOrDefaultPrefsAsync`) → count sent today → `Evaluate` → dedupe (`TryAcquireDedupeAsync`) → resolve locale → `BuildMessage` → `DispatchAsync`. All wrapped in try/catch (fail-soft). **P9-05 adds 5 of these**; **P9-08 extends the lapse one.** |
| **Shared handler helper** | `.../IntegrationEventHandlers/Reengagement/ReengagementHandlerHelper.cs` | Static utility (explicitly NOT a design pattern, per rule 8) for prefs/dedupe/locale/`BuildMessage`. P9-05 handlers reuse it verbatim. |
| **Copy templates** | `.../Domain/Templates/ReengagementCopyTemplates.cs` | Pure-static `(category, code, locale) → (title, body)` with `Render(...)` placeholder substitution; ar-EG primary + en-US fallback. `LEVELED_UP` + `LEAGUE_TIER_CHANGED` already present. **P9-05 adds freeze + timed-event entries; P9-08 adds 3 tiered lapse entries.** |
| **Per-child prefs entity + service** | `.../Domain/Entities/ChildReengagementPreference.cs` (`Email/Push/InApp`, `QuietHoursStart/EndLocal`, `TimeZoneId`, `DailyCap` default 3) + `Application/Abstractions/IChildReengagementPreferenceService.cs` (impl `ChildReengagementPreferenceService.cs`) | Per (ParentId, ChildId, Category) row. **CRITICAL**: the service manages **only 3 categories** — `StreakAtRisk`, `DailyMissionReminder`, `LapseWinBack`. The new P9-05 types all map to `Achievement`, which is NOT in the per-child schedulable set (see OQ-8). `DailyCap` here is **per-category**; P9-07's **global** budget is a new, separate concept. |
| **Redis dedupe store** | `.../Infrastructure/Services/ReengagementDedupeStore.cs` (`IReengagementDedupeStore`) | Atomic Redis `SETNX` (`StringSetAsync(When.NotExists)`) key `nudge:{studentId}:{categoryInt}:{yyyyMMdd}`, TTL 36h; fail-OPEN. **This is the template for P9-07 cooldowns (SETNX per type) and the global budget counter (INCR per child/day), and for P9-08 per-tier dedupe** (a tier key). |
| **Parent resolution** | `Shared.Contracts/Parent` `IParentChildQuery.FindParentForChildAsync(childId)` | Resolves the parent that owns the child's prefs. Reused by every handler. |
| **Inbox counts** | `.../Application/Abstractions/INotificationInboxService.cs` `CountSentTodayAsync(childId, category, nowUtc)` | Per-**category** count today (the `sentsToday` input). The `Notification` row carries `DeliveredChannels` (Push=2) + `SentAtUtc`, so a **cross-category push count** for today is derivable from inbox rows (see OQ-6). |
| **Config seam (tunable without deploy)** | `Shared.Kernel/Settings/IGlobalSettingsProvider.cs` (`GetInt/GetString/GetBool/GetDecimal(key, default)`); appsettings-options precedent `Gamification.Application/Configuration/ReengagementOptions.cs` | Two config styles in the repo: (a) `IGlobalSettingsProvider` (DB-backed + Redis-cached + admin-editable after P10-12; bootstrap returns appsettings/code defaults) — the **right home for budget / priority / cooldown** ("tunable without deploy" per P4-11/P10-12 approach the tasks cite); (b) a bound `*Options` POCO (simpler, redeploy to change). Recommend `IGlobalSettingsProvider` for the runtime-tunable knobs (OQ-2/3/4). |
| **DI registration** | `.../Infrastructure/DependencyInjection.cs` (`AddNotificationsInfrastructure`) | Where the new arbitration service / budget+cooldown store get registered (Scoped, alongside `INudgeDispatcher`, `IReengagementDedupeStore`). |

**Integration-event payloads (confirmed by reading `Shared.Contracts/Gamification/*.cs`):**

| Event | Recipient field | Other payload |
|---|---|---|
| `StudentLeveledUpIntegrationEvent` | **`StudentId`** ✅ | `OldLevel`, `NewLevel` |
| `LeagueTierChangedIntegrationEvent` | **`StudentId`** ✅ | `OldTier`, `NewTier`, `Status` (`"Promoted"`/`"Stayed"`/`"Relegated"` strings) |
| `StreakFreezeConsumedIntegrationEvent` | **`StudentId`** ✅ | `CurrentStreak`, `RemainingFreezeBalance` (after consumption) |
| `TimedEventStartedIntegrationEvent` | **NO StudentId** ⚠️ | `TimedEventId`, `Code`, `Multiplier`, `Scope` (`TimedEventScopeDto`), `StartUtc`, `EndUtc` — **platform-wide broadcast** |
| `TimedEventEndedIntegrationEvent` | **NO StudentId** ⚠️ | `TimedEventId`, `Code` — **platform-wide broadcast** |

This asymmetry is the wave's headline blocker — see **OQ-1**.

---

## Acceptance criteria (testable — reviewer checks against these)

### P9-05 (light-up)
- [ ] New `INotificationHandler` consumers exist for `StudentLeveledUp`, `LeagueTierChanged`, `StreakFreezeConsumed`, and the timed-event recipient decision from OQ-1, each routing through the **existing** `NudgeDispatcher` + `ReengagementEvaluator` + dedupe (prefs, quiet hours, cap, Redis dedupe) — mirroring the 8 existing handlers.
- [ ] `LeagueTierChanged` copy distinguishes **promotion** (`Status="Promoted"` → "🏆 ارتقيت لدوري أعلى!") from a **neutral move** (else branch).
- [ ] New `ReengagementCopyTemplates` entries (ar-EG + en-US) for `STREAK_FREEZE_CONSUMED` ("أنقذنا سلسلتك! ❄️ ارجع النهاردة تكمّل") and the timed-event codes (`TIMED_EVENT_STARTED`, and a derived "last-day" reminder). All Arabic-first, child-safe, celebration > guilt, en fallback.
- [ ] `LEVELED_UP` + `LEAGUE_TIER_CHANGED` reuse existing templates (no new copy needed).
- [ ] Each new type maps to a parent-toggleable category (resolve OQ-8) and **counts toward the P9-07 arbitration budget**.
- [ ] Every send is logged per type for return-rate analytics (per-type send/open — see OQ-5).

### P9-07 (arbitration + global budget — the centerpiece)
- [ ] A **global per-child daily push budget** caps total **pushes**/day across **all** categories, in addition to the existing per-category `DailyCap`. Parent-configurable; conservative default (OQ-2). In-app inbox rows are **not** budgeted.
- [ ] A **cross-category priority ordering** decides which nudge wins the push slot when several are eligible in the same window; config-driven and tunable without deploy (OQ-3).
- [ ] **Per-type cooldowns** prevent a type repeating too often (config-driven; OQ-4) — implemented via Redis (SETNX) like the existing dedupe.
- [ ] Quiet hours + the existing Redis dedupe **continue to apply**; the in-app inbox row is **always written** (only push is rationed) — confirmed already true in `NudgeDispatcher`; arbitration gates only the push decision.
- [ ] When a nudge loses arbitration, the **suppression reason** is logged (`priority-lost` / `budget-exhausted` / `cooldown` — plus existing `quiet-hours` / `cap`) and the suppressed item **still lands in the inbox** (OQ-5 for the log sink).
- [ ] Per-type send/open + suppression analytics emitted (OQ-5).
- [ ] Extends the **existing** pure `ReengagementEvaluator` (new `NotEligibleReason` members) + a thin arbitration service that reads budget/cooldown state — does **not** fork the evaluator or dispatcher.

### P9-08 (comeback ladder)
- [ ] The lapse win-back path selects copy by **idle-day tier**: ~day 2 gentle, ~day 5 stronger / streak-repair framing, ~day 14 fresh-start. Thresholds config-driven/tunable.
- [ ] **Each tier fires at most once** (per-(child, tier) dedupe), not just per-(child, day).
- [ ] 3 new tiered templates (ar-EG + en-US), personalized (name), **never shaming** the absence, en fallback.
- [ ] Subject to P9-07 arbitration + the LapseWinBack parent toggle; sends/opens logged per tier.
- [ ] **Streak-repair *action* is OUT of scope** (economy-dependent, Gap C) — messaging ladder only; the copy frames repair without performing it.

---

## Affected modules & data

**Single module: Notifications.** No cross-module FK, no other module's projects referenced (rule 1). Consumes existing `Shared.Contracts/Gamification` events (no Gamification change). May optionally consume an Analytics seam (OQ-5).

| Entity / artifact | New vs existing | Notes |
|---|---|---|
| `ReengagementEvaluator` | **Extend** | New `NotEligibleReason` enum members; possibly a new `Evaluate` overload taking global-push-count + cooldown-active + priority-context. Keep pure/static/unit-testable. |
| Arbitration service (new) | **New class** (Application abstraction + Infrastructure impl, Option C) | Thin orchestrator: reads budget counter + cooldown state, applies priority, calls evaluator, decides `ShouldPush`. Mirrors existing service shapes — **not** a Strategy/Factory (see rule-8 note). |
| Global push budget counter | **New Redis counter** (recommended) or new column (see OQ-6) | `pushbudget:{childId}:{yyyyMMdd}` INCR + TTL, mirroring `ReengagementDedupeStore`. |
| Per-type cooldown | **New Redis key** | `cooldown:{childId}:{typeCode}` SETNX + per-type TTL, mirroring dedupe. |
| Global-budget pref field | **Possibly new column** on `ChildReengagementPreference` (OQ-2/OQ-7) | If the budget is parent-configurable, add `GlobalDailyPushBudget int` (default conservative) + migration. Else config-only default (no migration). |
| `ReengagementCopyTemplates` | **Extend** (data only) | +2 freeze/timed entries (P9-05) + 3 tiered lapse entries (P9-08), each ar+en. No schema. |
| New handlers | **New classes** | 3–5 P9-05 handlers (depending on OQ-1); P9-08 extends the existing lapse handler. |
| `NotificationCategory` enum | **Possibly extend** (OQ-8) | If new types need individual toggles beyond `Achievement`, add enum members + extend the per-child prefs service category set + migration backfill. |
| Per-tier dedupe (P9-08) | **Reuse dedupe store** with a tier-keyed call | No new persistence if the dedupe key includes the tier (see OQ-6). |

---

## Handoff → db-migration

**Likely NONE — confirm via the open questions.** The recommended design keeps budget/cooldown/tier state in **Redis** (mirroring the existing `ReengagementDedupeStore`), and the global push count is **derivable from existing inbox rows** (`Notification.DeliveredChannels & 2` + `SentAtUtc` window). So **no new table is required for the core arbitration**.

A migration is needed **only if** the lead chooses one of:
1. **OQ-2 / OQ-7** — parent-configurable global budget → add `GlobalDailyPushBudget` (int, conservative default, e.g. 4) to `ChildReengagementPreference` + an additive migration (mirror `20260602101101_AddChildReengagementPreferenceDailyCap`). If the budget is a non-parent-editable platform default, no column — use `IGlobalSettingsProvider`.
2. **OQ-8** — finer-grained per-child toggles for the new types → new `NotificationCategory` enum members are value-only (no schema), but the per-child prefs service category array changes and existing rows may need a backfill consideration (additive only; defaults synthesised on read, so likely **no** migration even here).
3. **OQ-6** — if the lead rejects Redis/inbox-derived counters and wants a durable per-child-per-day push counter table → a new `ChildDailyPushCounter` table. **Not recommended** (Redis + inbox-derive is sufficient and consistent with the module).

**Recommendation to db-migration: stand by; expect no migration. Build one additive column migration only if OQ-2/OQ-7 lands on "parent-configurable budget."**

---

## Handoff → backend-feature

This is a backend-only wave; the bulk lands here. **Mirror existing shapes; Option C (EF only in Infrastructure).**

### P9-05 — new consumers + templates
- Add `StudentLeveledUpIntegrationEventHandler`, `LeagueTierChangedIntegrationEventHandler`, `StreakFreezeConsumedIntegrationEventHandler` under `.../IntegrationEventHandlers/Reengagement/`, each a near-verbatim copy of `BadgeEarnedIntegrationEventHandler` (resolve parent → prefs → count → evaluate → dedupe → locale → `BuildMessage` → dispatch → log; full try/catch fail-soft). Category `Achievement` (or per OQ-8). Codes: `LEVELED_UP`, `LEAGUE_TIER_CHANGED`, `STREAK_FREEZE_CONSUMED`.
- `LeagueTierChanged`: branch copy on `ev.Status == "Promoted"` vs else; pass placeholders `("oldTier", ev.OldTier), ("newTier", ev.NewTier)` if templates use them.
- `StreakFreezeConsumed`: pass `("streakLength", ev.CurrentStreak.ToString())`; map to `StreakAtRisk` or `Achievement` category (confirm with OQ-8 — semantically it is a streak-save, so `StreakAtRisk` may fit better than `Achievement`).
- Timed-event handler(s): **blocked on OQ-1** (no `StudentId`). Do not build the per-child fan-out until the recipient mapping is decided.
- Add `ReengagementCopyTemplates` entries for `STREAK_FREEZE_CONSUMED` (ar+en) and the timed-event codes. Reuse existing `LEVELED_UP`/`LEAGUE_TIER_CHANGED`.
- **MediatR registration check:** verify the Notifications Application assembly is already in the cross-module MediatR registration (it is — the 8 existing handlers fire). New handlers in the same assembly auto-register; **no `AddCrossModuleMediatR` edit needed** (avoids the shared-file serialization that bit Analytics). Confirm during build.

### P9-07 — arbitration + global budget (centerpiece)
- **Extend `ReengagementEvaluator`** (pure): add `NotEligibleReason.GlobalBudgetExhausted`, `.PriorityLost`, `.Cooldown`. Either add params to `Evaluate` (global push count, per-type cooldown-active flag) or add a second pure method `ArbitratePush(...)` that the handlers call after the existing per-category `Evaluate`. Keep it allocation-free and unit-testable (the P4-09 brief prized this).
- **New arbitration service** (Application abstraction `INudgeArbiter` + Infrastructure impl, Scoped, registered in `AddNotificationsInfrastructure`): given (childId, category, typeCode, nowUtc), it (1) reads the global push count for today (Redis INCR-peek or inbox-derive — OQ-6), (2) checks per-type cooldown (Redis SETNX — OQ-4), (3) applies priority ordering (OQ-3) — and returns a push decision + suppression reason. On a granted push it consumes a budget slot (INCR) and sets the cooldown key. **The handlers set `NudgeMessage.ShouldPush = arbiterDecision` and ALWAYS keep `ShouldInApp` per prefs** so the inbox row is always written (dispatcher already guarantees the row; arbitration only flips the push bit).
- **Global budget counter:** Redis `pushbudget:{childId}:{yyyyMMdd}` with INCR + 36h TTL, mirroring `ReengagementDedupeStore`'s connect/fail-open pattern. Budget value from prefs (if OQ-2 = parent-configurable) else `IGlobalSettingsProvider.GetInt("Notifications:GlobalDailyPushBudget", 4)`.
- **Per-type cooldown:** Redis `cooldown:{childId}:{typeCode}` SETNX with per-type TTL from `IGlobalSettingsProvider` (e.g. `Notifications:Cooldown:LEAGUE_TIER_CHANGED` = 24h). Fail-open like dedupe (a Redis outage should not silently drop all pushes — confirm fail-open vs fail-closed in OQ-4).
- **Priority ordering:** a config-driven ordered list (e.g. CSV/JSON in `IGlobalSettingsProvider` `Notifications:PriorityOrder`); the arbiter resolves a type's rank. Because handlers fire **independently** per event (not batched), "win the slot when several are eligible in the same window" is realised as: higher-priority types consume the budget first, and a per-type cooldown + the global budget naturally starve lower-priority spam later in the day. Document this interpretation — there is **no batching/queue** in the current architecture (see Risk).
- **Suppression-reason logging:** emit a structured `ILoggerManager` line (the existing `analytics.reengagement.*` convention) per suppression with `reason=` (priority-lost / budget-exhausted / cooldown / quiet-hours / cap). See OQ-5 for whether to also emit to the Analytics module.

### P9-08 — comeback ladder
- **Extend `LapseWinBackIntegrationEventHandler`**: compute the idle-day tier from `ev.DaysSinceLastActivity` against config thresholds (`IGlobalSettingsProvider` `Notifications:LapseTiers` default `2,5,14`); select the tier code (`LAPSE_WIN_BACK_GENTLE` / `_REPAIR` / `_FRESH_START`).
- **Per-tier dedupe:** the existing dedupe is per (studentId, category, day). For "each tier once," include the tier in the dedupe key — either add a tier-aware overload to `IReengagementDedupeStore` (preferred; key `nudge:{studentId}:{categoryInt}:{tier}:{yyyyMMdd}` or a longer-lived key without the day so the tier truly fires once per lapse episode) or use a separate cooldown key. **Confirm the dedupe key shape in OQ-6** (per-day vs per-episode matters: a tier should fire once per lapse episode, not once per day).
- **3 templates** in `ReengagementCopyTemplates` (ar+en), personalized via the existing `("name", ...)` placeholder pattern (resolve the child's name — check how other handlers source the name; the lapse handler currently passes `daysIdle`. Name resolution may need `IUserLookup` / a profile seam — confirm it exists or flag).
- Keep the merged single `LAPSE_WIN_BACK` template as a fallback for idle days outside the tier set.

---

## Handoff → frontend

**None for this wave (backend-only).** The parent controls UI is **P9-04-FE** (separate story, not in this wave): `tasks/Frontend/student-app/Phase-9-Notifications/P9-04-FE.md` already plans the per-category toggles, quiet-hours, daily-cap, and notes "Global-cap/priority knobs (P9-07) surface here once that ships." If P9-07 lands a parent-configurable **global budget** field (OQ-2/OQ-7), the P9-04 FE/contract will need that field added to `GetChildReengagementPreferences` / `UpdateChildReengagementPreferences` — note for the planner so it's tracked against P9-04, but it is **not** built in this wave.

---

## Open questions / assumptions / risks

> **Top blocking OQ = OQ-1.** Everything else has a confident recommendation; the lead should confirm OQ-1, OQ-2, OQ-3, OQ-4, OQ-8 before/early in the build.

**OQ-1 (BLOCKING) — timed-event recipient mapping (no `StudentId`).** `TimedEventStarted/EndedIntegrationEvent` are **platform-wide broadcasts** (`TimedEventId`, `Code`, `Scope`; **no `StudentId`**), unlike the other 4. A per-child nudge needs a recipient and there is **no active-student query in the Notifications module today** (the existing handlers all receive `StudentId` on the event). Options:
  - (a) **Defer timed-event nudges from this wave** (recommended for P9-05 v1) — wire the 3 per-child events (level-up, league, freeze) now; carry timed-event-fan-out as a follow-up that needs an active-student source.
  - (b) **Fan out to all active students** — requires an active-student list. The new **Analytics module** (P5-03) owns activity data but exposes only **platform-aggregate** seams (`IPlatformActivityQuery` etc.), **not** a per-student id list — so it is not directly usable for fan-out without a new seam. Gamification owns enrolment but is module-isolated (no per-student-list contract exists). Building fan-out means a new cross-module "active student ids" seam + a budget-aware bulk dispatch — materially bigger than "light up a handler."
  - **Recommendation: (a) defer timed-event per-child nudges to a follow-up story; deliver the 3 StudentId-bearing events + freeze in P9-05 now.** Flag to the lead: if timed-event nudges are must-have, scope a separate story for the active-student seam + fan-out (and it must respect the P9-07 budget per child).

**OQ-2 — default global daily push budget value + parent-configurable?** No global budget exists today; per-category `DailyCap` defaults to 3. **Recommendation: default global budget = 4 pushes/child/day** (conservative; lower than 3×categories so it actually binds across categories), sourced from `IGlobalSettingsProvider.GetInt("Notifications:GlobalDailyPushBudget", 4)`. The story says "parent-configurable" — **recommend making it parent-editable** via a new `GlobalDailyPushBudget` column on `ChildReengagementPreference` (additive migration, default 4) surfaced through the P9-04 prefs contract. Confirm the default and whether parent-editable in v1 (if not, config-only, no migration).

**OQ-3 — default cross-category priority ordering.** **Recommendation (highest → lowest):** `StreakAtRisk` (streak-danger / hearts) > `DailyMissionReminder` > `LapseWinBack` (comeback) > `Achievement` (level/league/badge/freeze) > `WeeklyReport`/recap. Config-driven via `IGlobalSettingsProvider` `Notifications:PriorityOrder` (CSV of category or type codes), tunable without deploy. Confirm the ordering and whether priority is by **category** or finer **type** granularity.

**OQ-4 — per-type cooldown defaults + fail-open vs fail-closed.** **Recommendation:** `LEAGUE_TIER_CHANGED` ≤ 1/day (24h), `LEVELED_UP` ≤ 1/day, `STREAK_FREEZE_CONSUMED` ≤ 1/day, weekly recap ≤ 1/week (168h); achievement-class default 24h; defaults via `IGlobalSettingsProvider` `Notifications:Cooldown:{CODE}`. **Fail-open** on Redis outage (consistent with the existing dedupe store's fail-open; a Redis blip should not silently kill all pushes) — but note this means cooldowns are not enforced during an outage. Confirm fail-open is acceptable for cooldowns (it is for dedupe today).

**OQ-5 — where suppress-reasons + per-type send/open are logged.** Today the module emits **structured `ILoggerManager` lines** (`analytics.reengagement.sent`, `analytics.reengagement.not_eligible reason=...`). There is **no** write-from-Notifications path into the Analytics module — `IActivityEventStore` is internal to Analytics and consumes integration events, not arbitrary telemetry. **Recommendation for v1: emit suppression + send via `ILoggerManager` structured lines** (extend the existing `analytics.reengagement.*` convention with `reason=` for the new suppression reasons), which is the established pattern and zero new surface. If first-class queryable notification analytics are required, that is a **separate story** (a new `Shared.Contracts/Analytics` ingest seam + an `ActivityEvent`-style "notification.sent/suppressed/opened" feed) — flag, don't build it in this wave. The tasks' "P5-03 seam" references are **aspirational** — the concrete seam to write notification telemetry does not exist yet.

**OQ-6 — does arbitration need a new persisted counter, or can it reuse inbox-counts / dedupe-store?** **Recommendation: no new counter table.**
  - **Global daily push count** is derivable from existing inbox rows: `Notification` where `RecipientExternalUserId = childId`, `SentAtUtc` within today (UTC), and `DeliveredChannels & 2 != 0` (push bit). Add a `CountPushesSentTodayAsync(childId, nowUtc)` to `INotificationInboxService` (mirrors `CountSentTodayAsync`, drops the category filter, adds the push-bit filter). This is the **authoritative** count (survives Redis loss) and needs **no migration**. Optionally back it with a Redis INCR counter for hot-path speed (Redis as cache, DB as truth), mirroring P4-10's "Redis hot path + Postgres ledger."
  - **Per-type cooldown** and **P9-08 per-tier dedupe** reuse the **Redis SETNX** pattern (new keys, same store style) — no DB.
  - Confirm: is the inbox-derived push count acceptable as the budget source (recommended), or does the lead want a dedicated Redis INCR counter / a DB column? And for P9-08, is the tier dedupe **per-day** or **per-lapse-episode** (recommend per-episode: a longer-TTL key without the day component so day-5 gentle doesn't re-fire daily).

**OQ-7 — security-auditor needed?** **Recommendation: YES (light).** This touches **child notification data** and **parent controls** (a new parent-configurable budget field if OQ-2 lands; per-child push rationing). The data is PII-light (int ids, template codes — consistent with P4-09), no new endpoints unless the budget field is exposed, and no new auth surface inside this wave (the prefs write path is the existing parent-JWT P9-04 controller). So a **scoped audit** is warranted: verify (a) child JWT cannot write the global budget / prefs, (b) no new PII in logs/templates, (c) the budget field (if added) is parent-only and IDOR-safe (parent can only set budget for their own child — mirror existing `UpdateChildReengagementPreferences` ownership check). Not expected to surface Critical/High, but child-data + parent-control crosses the rule-4b threshold — include it before the gate.

**OQ-8 — category granularity for "individually toggleable" new types.** The per-child prefs service manages only `StreakAtRisk`, `DailyMissionReminder`, `LapseWinBack`. The 5 new types currently all map to `Achievement` (or `StreakAtRisk` for freeze). P9-04-FE-2 lists finer categories (achievement, league, timed event, streak milestone, weekly challenge…). **Recommendation for v1: ride the existing `Achievement` per-child toggle** for level-up/league/freeze (1 toggle covers them) to avoid an enum/migration churn, and **flag the finer-granularity catalog as P9-04's concern** (the FE story owns the toggle UX). If the lead wants per-type toggles now, add `NotificationCategory` members (value-only, no schema) + extend the prefs service category array — additive, defaults synthesised on read (likely no migration). Confirm desired granularity.

**Rule-8 (design pattern) check — CONFIRMED no concern.** This wave mirrors the existing evaluator (pure static), handler (per-event `INotificationHandler`), helper (static utility), dispatcher, and Redis-store shapes. The new arbitration service is a plain Scoped service composing existing seams — **not** a Strategy/Factory/Decorator. The config-driven priority list is data, not a pattern. **No new design pattern is introduced; no rule-8 approval needed.** (If implementation drifts toward a pluggable "rule chain" / Strategy for priority, stop and ask first.)

**Risk — no batching / fire-once-per-event architecture.** Handlers fire **independently** per integration event; there is **no queue/window** where "several eligible nudges" are compared in one pass. P9-07 priority is therefore realised emergently: higher-priority types consume the scarce budget + their own cooldowns starve lower-priority spam later in the day, rather than a literal "pick the best of N pending." This is the pragmatic, low-risk interpretation and matches the existing event-driven design — **document it explicitly** so the reviewer doesn't expect a scheduler/queue. A true windowed arbiter (collect candidates, pick top-priority) would be a larger architectural change — out of scope unless the lead requires literal slot-contention.

**Risk — fail-open budget during Redis outage.** If the global budget counter is Redis-only and fails open, an outage removes the spam guard. Mitigation: make the **inbox-derived DB count the authority** (OQ-6) so the budget still binds without Redis; Redis is only the fast path.

**Risk — child-name source for P9-08 personalization.** The gentle/repair copy uses `{name}`. The lapse handler today passes only `daysIdle`. Confirm a name seam exists (`IUserLookup` returns language but maybe not display name) — if not, either resolve via `IParentChildQuery`/a profile seam or drop `{name}` to a generic vocative. Flag during build.

---

## Recommended pipeline order (first cut — the `planner` finalizes)

Backend-only wave. **No `designer` / `frontend` / `frontend-e2e-tester` stages.**

**Gate the OQs with the lead first** (OQ-1 blocking; OQ-2/3/4/8 shape the build). Then:

1. **`db-migration`** — **conditional / likely skipped.** Run **only** if OQ-2/OQ-7 = parent-configurable budget (one additive column on `ChildReengagementPreference`) or OQ-8 = per-type toggles needing enum/prefs changes. Otherwise no DB stage. Holds the migration lock if it runs.
2. **`backend-feature` — P9-07 first** (the counterweight): extend `ReengagementEvaluator` + new arbitration service + Redis budget/cooldown + suppression logging + (optional) inbox push-count helper. This is the foundation P9-05/P9-08 enrol into.
3. **`backend-feature` — P9-05 + P9-08 in parallel** after P9-07's arbiter exists:
   - P9-05: 3 (or per OQ-1, +timed) handlers + freeze/timed templates.
   - P9-08: extend lapse handler with tier selection + per-tier dedupe + 3 templates.
   Both are independent of each other; both wire into the P9-07 arbiter.
4. **`api-tester`** — there are **no new HTTP endpoints** in this wave (unless OQ-2 exposes a budget field via the existing P9-04 prefs endpoints). So api-tester runs as **integration tests over the event→nudge pipeline**: publish each event → assert inbox row written + push gated correctly by budget/cooldown/priority/quiet-hours; assert suppression reason logged; assert each P9-08 tier fires once; assert global budget caps pushes across categories while inbox stays unrationed. (Mirror the existing P4-09/P4-11 integration tests, e.g. `P4_11_StreakFreezeTimedEvents_IntegrationTests.cs`.) Unit tests for the extended pure `ReengagementEvaluator` are mandatory (it was prized as unit-testable).
5. **`security-auditor`** — scoped audit (OQ-7): child-data, parent-control ownership/IDOR on any new budget field, no new PII in logs/templates.
6. **`reviewer`** — gate each batch against this brief's ACs + CONVENTIONS.md (Option C, fail-soft, rule-1 isolation, rule-2 envelope where applicable, rule-8 no new pattern), including api-tester + security-auditor results.
7. **`committer`** — per-story branches under `feat/P9-07-…`, `feat/P9-05-…`, `feat/P9-08-…` (or one wave branch if the lead prefers), PR with full description; update `docs/dev/HANDOFF.md` (new config keys: `Notifications:GlobalDailyPushBudget`, `Notifications:PriorityOrder`, `Notifications:Cooldown:*`, `Notifications:LapseTiers`; Redis key shapes; the "no batching" arbitration interpretation; the deferred timed-event fan-out backlog).
