# Execution Plan — P9 Notifications wave (P9-07 arbitration + P9-05 light-up + P9-08 comeback ladder)

> Backend-only wave on the existing **Notifications** module. Extends the P4-09/P4-10/P4-11 re-engagement subsystem — **no new module/foundation**. All open questions are **locked by the lead** (see "Locked decisions" below); this plan builds around them and does not re-open them.

## Source
- **Brief:** `docs/briefs/P9-nudge-arbitration.md`
- **User stories:** `user-stories/Phase-9-Notifications/P9-05-light-up-existing-gamification-events.md`, `P9-07-nudge-arbitration-and-daily-budget.md`, `P9-08-comeback-escalation-ladder.md`
- **Task files:** `tasks/Backend/Phase-9-Notifications/P9-05-BE.md`, `P9-07-BE.md`, `P9-08-BE.md`
- **Rules/context:** `CLAUDE.md`, `docs/dev/PARALLELISM.md`, `docs/dev/CONVENTIONS.md` (Option C), ADR-0001 (no UoW; explicit txn for atomic multi-write)
- **Confirmed extension points (read by the implementers):**
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Services/ReengagementEvaluator.cs` (pure evaluator — extend)
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Infrastructure/Reengagement/NudgeDispatcher.cs` (always writes inbox; gates push)
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/IntegrationEventHandlers/Reengagement/*` (8 existing handlers, incl. `BadgeEarnedIntegrationEventHandler.cs`, `LapseWinBackIntegrationEventHandler.cs`, `ReengagementHandlerHelper.cs`)
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Entities/ChildReengagementPreference.cs` + `Infrastructure/Persistence/Configurations/ChildReengagementPreferenceConfig.cs`
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Infrastructure/Services/ReengagementDedupeStore.cs` (Redis SETNX, fail-open)
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Templates/ReengagementCopyTemplates.cs`
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/Abstractions/INotificationInboxService.cs` (+ impl) — add `CountPushesSentTodayAsync`
  - `backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Infrastructure/DependencyInjection.cs` (`AddNotificationsInfrastructure`) — register `INudgeArbiter`
  - **Tests:** unit `backend/tests/Modules.Notifications.UnitTests/ReengagementEvaluatorTests.cs`; integration `backend/tests/Learnexia.IntegrationTests/P4_09_Reengagement_Tests.cs` (+ pattern `P4_11_StreakFreezeTimedEvents_IntegrationTests.cs`)

## Locked decisions (the build is constrained to these — do NOT re-open)
- **Scope** = P9-07 (arbitration + global daily push budget) + P9-05 (light-up, **3 events only**) + P9-08 (comeback ladder). Additive to P4-09; no new module.
- **P9-05 ships 3 events:** `StudentLeveledUp`, `LeagueTierChanged`, `StreakFreezeConsumed` (+ ar/en templates). **`TimedEventStarted/Ended` are DEFERRED** (no `StudentId` → no recipient) — backlog follow-up needing a recipient-resolution mechanism. (Resolves OQ-1 = option (a).)
- **Arbitration:** extend the pure `ReengagementEvaluator` with new `NotEligibleReason` members (`GlobalBudgetExhausted` / `PriorityLost` / `Cooldown`) + a thin **Scoped `INudgeArbiter`** that gates **only the push decision**; in-app inbox is **always** written by `NudgeDispatcher`. (OQ-3 priority granularity = category.)
- **Persistence:** ONE additive migration — `GlobalDailyPushBudget` (int, **nullable**, config default-fallback) column on `ChildReengagementPreference` (parent-configurable). Global daily push **count** is **derived from existing inbox rows** (`Notification.DeliveredChannels & 2` push-bit + `SentAtUtc` today) via a new `CountPushesSentTodayAsync` — **NO counter table**. Cooldowns + P9-08 per-lapse-episode dedupe reuse the existing Redis SETNX `ReengagementDedupeStore`. (Resolves OQ-2/OQ-6/OQ-7.)
- **Config (tunable-without-deploy via `IGlobalSettingsProvider`):** default global budget = **4/day**; cross-category priority order **StreakAtRisk > DailyMission > LapseWinBack > Achievement > recap**; per-type cooldowns league/level/freeze **≤1/day**, recap **≤1/week**. (Resolves OQ-2/3/4.)
- **Suppress-reasons** → `ILoggerManager` structured lines (v1; extend the `analytics.reengagement.*` convention with `reason=`). First-class queryable notification analytics = **separate story** (backlog). Budget authority = the **inbox-derived DB count** (fail-open if Redis down; DB count still guards). (Resolves OQ-5.)
- **P9-08:** extend `LapseWinBackIntegrationEventHandler` — copy by idle-day tier (~2d gentle / ~5d repair / ~14d fresh-start), **per-tier (per-lapse-episode) dedupe**, config thresholds (`Notifications:LapseTiers` default `2,5,14`). **Copy-only** (streak-repair action out of scope).
- **Category granularity (OQ-8):** ride the existing per-child `Achievement` toggle for level/league/freeze in v1; finer per-type toggles are P9-04's concern (backlog).
- **Rule-8:** no new design pattern — mirrors the existing evaluator (pure static) / handler (per-event `INotificationHandler`) / static helper / Redis-store shapes. `INudgeArbiter` is a plain Scoped composing service. No rule-8 approval needed. **If implementation drifts toward a pluggable rule-chain/Strategy for priority → stop and ask the lead.**
- **No batching architecture:** handlers fire independently per event; priority is realised **emergently** (high-priority types consume the scarce budget + their own cooldowns starve lower-priority spam later in the day). The reviewer must NOT expect a scheduler/queue. Document this in code + HANDOFF.

## Task inventory

| ID | Stack | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| **P9-07-BE-1** | backend | Global per-child daily push budget (parent-configurable col + config default 4) across all categories, in addition to per-category caps; extend prefs + evaluator/arbiter | 4 | P4-09 evaluator; **migration (DB-1)** |
| **P9-07-BE-3** | backend | Per-type cooldowns (league/level/freeze ≤1/day, recap ≤1/week) via Redis SETNX, fail-open | 3 | P1-06 Redis; `ReengagementDedupeStore` |
| **P9-07-BE-2** | backend | Config-driven cross-category priority ordering (tunable w/o deploy) inside `INudgeArbiter` | 5 | P9-07-BE-1 |
| **P9-07-BE-4** | backend | In-app inbox row **always written** even when push loses arbitration/budget; only push rationed (confirm dispatcher guarantee; arbiter flips `ShouldPush` only) | 2 | P4-09 dispatcher |
| **P9-07-BE-5** | backend | Suppression-reason logging (priority-lost / budget-exhausted / cooldown) via `ILoggerManager` structured lines; per-type send logged | 3 | P9-07-BE-1..4 |
| **P9-05-BE-1** | backend | Handler `StudentLeveledUp` → `Achievement`/`LEVELED_UP` (reuse template) via arbiter+dispatcher | 2 | P4-09; **P9-07 arbiter** |
| **P9-05-BE-2** | backend | Handler `LeagueTierChanged` → `Achievement`/`LEAGUE_TIER_CHANGED` (reuse template); promotion (`Status=="Promoted"`) vs neutral-move copy split | 2 | P9-05-BE-1 |
| **P9-05-BE-3** | backend | Handler + **new template** `StreakFreezeConsumed` → "أنقذنا سلسلتك ❄️"/en; map category (Achievement per OQ-8 v1) | 2 | P4-11 freeze; P9-05-BE-1 |
| **P9-05-BE-5** | backend | Confirm each new type counts toward the P9-07 budget + per-type send logging (analytics emit = `ILoggerManager`, P5-03 sink deferred) | 1 | P9-07; P9-05-BE-1..3 |
| **P9-05-BE-4** | backend | ~~Timed-event handlers~~ — **DEFERRED** (no `StudentId`). Backlog note only; not built. | 0 | (blocked — needs recipient seam) |
| **P9-08-BE-1** | backend | Idle-day tier selection (~2/5/14, config thresholds) in lapse path; extend `LapseWinBackIntegrationEventHandler` | 3 | P4-09 lapse handler |
| **P9-08-BE-2** | backend | Three tiered templates (gentle / repair-framing / fresh-start), ar-first + en, personalized (name if seam exists, else generic vocative), never-shaming | 2 | P9-08-BE-1 |
| **P9-08-BE-3** | backend | Per-tier (per-lapse-episode) dedupe — each tier fires at most once; reuse Redis SETNX with a tier-keyed/episode key | 2 | P9-08-BE-1; dedupe store |
| **P9-08-BE-4** | backend | Arbitration enrolment (route through `INudgeArbiter`) + LapseWinBack parent toggle + per-tier send logging | 2 | **P9-07 arbiter**; P9-08-BE-1..3 |
| **DB-1** | db-migration | Additive migration: `GlobalDailyPushBudget` (int, nullable) on `ChildReengagementPreference` + entity/config update | 1 | P4-09 prefs entity |

> Total ~31h backend + ~1h migration. P9-05-BE-4 is carried as backlog (0h this wave).

## Dependency order
1. **DB-1** (additive column) — first, because **P9-07-BE-1 reads the per-child budget value** from `ChildReengagementPreference` (falls back to config default 4 when the nullable column is null).
2. **P9-07 arbiter core** — the counterweight. `INudgeArbiter` + extended `ReengagementEvaluator` + `CountPushesSentTodayAsync` + Redis cooldown + priority + suppression logging + dispatcher integration. P9-05's newly-lit types and P9-08's ladder must route through this, so the arbiter must exist **before/with** them. Internal order: BE-1 → BE-3 → BE-2 → BE-4 → BE-5.
3. **P9-05 consumers** and **P9-08 ladder** — both enrol into the arbiter; **independent of each other** → run in parallel after the arbiter lands.
4. **Tests / audit / gate / commit.**

**Cross-story rule honored:** P9-07 is the anti-spam guardrail; P9-05's pushes never reach production without it — so the arbiter is built first and the new consumers wire into it in the same wave/PR (no window where a lit event pushes un-arbitrated).

## Execution batches

> Single working tree, single wave branch **`feat/P9-nudge-arbitration`** (already checked out) — Mode A intra-story parallelism only; **no worktrees** (one branch, one PR for the wave). All three stories edit the **same Notifications module folders** → cross-story file overlap is real (shared `ReengagementCopyTemplates.cs`, shared `INudgeArbiter`), so the batches are sequenced to avoid two agents editing shared files at once.

### Batch 0 — DB (sequential, first) · agent: `db-migration`
- **DB-1:** add `GlobalDailyPushBudget int?` (nullable) to `ChildReengagementPreference` entity + `ChildReengagementPreferenceConfig`; generate an **additive** migration in the Notifications schema (mirror `20260602101101_AddChildReengagementPreferenceDailyCap`). No backfill needed (null → config default at read time). Holds the migration lock; nothing else writes schema this wave.
- **Output to Batch 1:** column name + nullable semantics (null = use `IGlobalSettingsProvider.GetInt("Notifications:GlobalDailyPushBudget", 4)`).

### Batch 1 — Arbitration core (sequential, after Batch 0) · agent: `backend-feature`
The centerpiece; build as one cohesive unit so the public seam (`INudgeArbiter`) is stable before consumers enrol.
- **P9-07-BE-1:** read per-child budget (col, fallback to config default 4); thread global-push-count into the decision.
- **P9-07-BE-4:** `CountPushesSentTodayAsync(childId, nowUtc)` on `INotificationInboxService` (push-bit `& 2` + `SentAtUtc` today, no category filter) = the **authoritative** budget count. Confirm dispatcher always writes inbox; arbiter only flips `ShouldPush`.
- **P9-07-BE-3:** per-type cooldown via Redis SETNX (`cooldown:{childId}:{typeCode}`, per-type TTL from config), **fail-open** like the dedupe store.
- **P9-07-BE-2:** config-driven priority ordering (`Notifications:PriorityOrder`, category granularity) inside the arbiter; document the emergent "no-batching" interpretation in code.
- Extend pure `ReengagementEvaluator` with `NotEligibleReason.GlobalBudgetExhausted / .PriorityLost / .Cooldown` (allocation-free, unit-testable; do not fork).
- Register `INudgeArbiter` (Scoped) in `AddNotificationsInfrastructure`.
- **P9-07-BE-5:** suppression-reason structured `ILoggerManager` lines (`analytics.reengagement.not_eligible reason=...`) extending the existing convention.
- **Shared-file note (serialize within this batch):** `IGlobalSettingsProvider` default keys (`Notifications:GlobalDailyPushBudget`, `Notifications:PriorityOrder`, `Notifications:Cooldown:*`) and DI registration are owned by **this batch only** — do not let Batch 2/3 touch them. Confirm the Notifications Application assembly is already in the cross-module MediatR registration (it is — 8 handlers fire) so **no `AddCrossModuleMediatR` shared-file edit** is needed.

### Batch 2 — Consumers + ladder (parallel, after Batch 1) · agent: `backend-feature` (two tracks)
Both depend on the arbiter from Batch 1; independent of each other. **Disjoint files except `ReengagementCopyTemplates.cs`** — see serialization note.
- **Track A — P9-05 (light-up, 3 events):**
  - **BE-1** `StudentLeveledUpIntegrationEventHandler` (copy of `BadgeEarnedIntegrationEventHandler`; `Achievement`/`LEVELED_UP`, reuse template; route through arbiter).
  - **BE-2** `LeagueTierChangedIntegrationEventHandler` (promotion vs neutral copy split on `ev.Status=="Promoted"`; reuse `LEAGUE_TIER_CHANGED`).
  - **BE-3** `StreakFreezeConsumedIntegrationEventHandler` + **new** `STREAK_FREEZE_CONSUMED` ar+en template; category per OQ-8 v1 (`Achievement`).
  - **BE-5** confirm each counts toward the budget + per-type send logging.
  - **BE-4 (timed-event)** — NOT built; backlog note.
- **Track B — P9-08 (comeback ladder):**
  - **BE-1** tier selection from `ev.DaysSinceLastActivity` vs `Notifications:LapseTiers` (`2,5,14`).
  - **BE-2** 3 tiered templates (`LAPSE_WIN_BACK_GENTLE/_REPAIR/_FRESH_START`) ar+en, never-shaming; keep merged `LAPSE_WIN_BACK` as fallback. Resolve `{name}` seam or fall back to generic vocative (flag if no name seam).
  - **BE-3** per-lapse-episode dedupe (tier-keyed; longer-TTL/episode key so a tier doesn't re-fire daily).
  - **BE-4** route through `INudgeArbiter`; respect LapseWinBack toggle; per-tier send logging.
- **Serialization within Batch 2:** both tracks add entries to `ReengagementCopyTemplates.cs`. Either (preferred) run the two tracks **sequentially** (A then B) since the wave is one branch/one PR — cheap, ~9h total — **or** if dispatched in parallel, the lead assigns template edits to disjoint regions and merges them in one pass. **Recommendation: run Track A then Track B sequentially** to avoid the shared-template edit collision (true Mode-A parallelism here saves little and risks a conflict on `ReengagementCopyTemplates.cs`).

### Batch 3 — Runtime tests (after Batch 2) · agent: `api-tester`
No new HTTP endpoints (the only DB field is exposed via the existing P9-04 prefs path, out of this wave) → run as **integration tests over the event→nudge pipeline** + **unit tests for the extended pure evaluator**. Home: integration `backend/tests/Learnexia.IntegrationTests/` (mirror `P4_09_Reengagement_Tests.cs` / `P4_11_StreakFreezeTimedEvents_IntegrationTests.cs`); unit `backend/tests/Modules.Notifications.UnitTests/ReengagementEvaluatorTests.cs`.
- Budget exhaustion **suppresses push but still writes the inbox row** (the headline AC).
- Priority: higher-priority type consumes the slot; lower loses with `reason=priority-lost`.
- Per-type cooldown blocks a repeat within TTL; fail-open when Redis down (DB count still guards).
- Each of the 3 new consumers fires → inbox row + correct template (incl. league promotion vs neutral split).
- P9-08: each idle tier selects the right copy and **fires at most once per lapse episode**.
- Global budget caps pushes **across categories** while inbox stays unrationed.
- Suppression reason is logged for each suppression path.
- **Mandatory** unit tests for the new `ReengagementEvaluator` reasons (it was prized as unit-testable).

### Batch 4 — Security audit (after Batch 2, can overlap Batch 3) · agent: `security-auditor` (LIGHT, MANDATORY)
Per brief **OQ-7** and CLAUDE.md rule-4b (child data + parent control). Scoped audit:
- (a) Child JWT cannot write the global budget / prefs (write path is the existing parent-JWT P9-04 controller — confirm unchanged).
- (b) No new PII in logs/templates (int ids + template codes only).
- (c) The new `GlobalDailyPushBudget` field is **parent-only and IDOR-safe** — a parent can only set the budget for their **own** child (mirror the existing `UpdateChildReengagementPreferences` ownership check).
- Not expected to surface Critical/High, but child-data + parent-control crosses the threshold → **runs before the reviewer gate; Critical/High block.**

### Batch 5 — Review gate · agent: `reviewer`
Gate the whole wave against the brief ACs + CONVENTIONS.md: Option C (EF only in Infrastructure), fail-soft handlers, rule-1 module isolation (no cross-module project ref; consumes `Shared.Contracts/Gamification` only), rule-2 envelope where applicable, **rule-8 no new pattern**, ADR-0001 (no UoW). Consumes api-tester + security-auditor results. Confirms the "no-batching / emergent priority" interpretation is documented and not mistaken for a missing scheduler.

### Batch 6 — Commit · agent: `committer` (only after reviewer PASSES)
- Branch **`feat/P9-nudge-arbitration`** (already checked out). **One PR for the wave** with a full description.
- Include the **HANDOFF.md** update in the same PR: new config keys (`Notifications:GlobalDailyPushBudget`=4, `Notifications:PriorityOrder`, `Notifications:Cooldown:*`, `Notifications:LapseTiers`=2,5,14), the new Redis cooldown/episode key shapes, the inbox-derived budget-count authority (fail-open Redis), the "no batching → emergent priority" interpretation, and the two backlog items below.
- Never on `main`; no amend/force-push; do not merge the PR.

## Review gates (where `reviewer` runs)
- **Primary gate:** Batch 5 — single reviewer pass over the whole wave (one branch/one PR), after `api-tester` (Batch 3) and `security-auditor` (Batch 4) both report.
- **Hard block:** any Critical/High from `security-auditor` blocks the gate.
- (Optional intermediate self-check: build must be green after Batch 1 before dispatching Batch 2, since consumers compile against `INudgeArbiter`.)

## Blockers / prerequisites
- **None blocking the build.** All OQs are locked by the lead. P4-09/P4-10/P4-11 are merged (the subsystem exists); Redis (P1-06) is present.
- **DB-1 must precede Batch 1** (the arbiter reads the new column). It is a tiny additive nullable column — low risk.
- **P9-05-BE-4 (timed-event nudges) is intentionally deferred**, not blocked work in this wave — see backlog.
- **Child-name seam for P9-08** (`{name}` personalization): confirm a display-name source exists during Batch 2 Track B; if not, fall back to a generic vocative (do not invent a new cross-module seam in this wave — flag it).

## Deferred / backlog notes (carry forward; record in HANDOFF.md)
1. **Timed-event nudges (P9-05-BE-4).** `TimedEventStarted/EndedIntegrationEvent` carry **no `StudentId`** (platform-wide broadcast) and there is no active-student source in Notifications. Deferred to a **separate follow-up story** that introduces a recipient-resolution mechanism (an "active student ids" seam) + budget-aware bulk dispatch. Not built here.
2. **First-class notification analytics sink.** v1 logs suppression + per-type send via `ILoggerManager` structured lines only. A queryable notification-analytics feed (a new `Shared.Contracts/Analytics` "notification.sent/suppressed/opened" ingest seam) is a **separate story** — the P5-03 references in the task files are aspirational; the concrete seam does not exist yet.
3. **Finer per-type parent toggles (OQ-8).** v1 rides the existing `Achievement` per-child toggle for level/league/freeze. Per-type toggle granularity is **P9-04-FE's** concern (the parent-controls story), which will also surface the new `GlobalDailyPushBudget` field in `Get/UpdateChildReengagementPreferences`.

## Definition of done

**Per batch**
- **Batch 0 (DB-1):** migration generated + applies cleanly; `ChildReengagementPreference.GlobalDailyPushBudget` (int?, nullable) present; build green.
- **Batch 1 (arbiter):** `INudgeArbiter` registered (Scoped); `ReengagementEvaluator` has the 3 new reasons (unit-coverable); `CountPushesSentTodayAsync` returns the inbox-derived push count; cooldown via Redis SETNX fail-open; priority config-driven; suppression logged; dispatcher confirmed to always write inbox & only flip push; build green.
- **Batch 2 (consumers + ladder):** 3 new P9-05 handlers fire through the arbiter (league promotion vs neutral split correct; new freeze template ar+en); P9-08 selects tier copy and dedupes per lapse episode; 3 tiered templates ar+en never-shaming; all fail-soft; build green.
- **Batch 3 (api-tester):** all pipeline integration assertions above pass + evaluator unit tests pass.
- **Batch 4 (security-auditor):** no Critical/High; child-data, parent-only budget, IDOR/ownership verified.
- **Batch 5 (reviewer):** PASS against brief ACs + CONVENTIONS + rule-8.
- **Batch 6 (committer):** wave committed on `feat/P9-nudge-arbitration`, pushed, PR opened, HANDOFF.md updated in the same PR.

**Overall (tied to story acceptance criteria)**
- **P9-05:** 3 `INotificationHandler` consumers (level/league/freeze) route through the existing dispatcher+evaluator+dedupe **and the new arbiter**; league copy distinguishes promotion from neutral; new freeze template (ar+en, child-safe, celebration>guilt); each type maps to a parent-toggleable category and counts toward the budget; sends logged per type. (Timed-event events explicitly deferred.)
- **P9-07:** global per-child daily push budget caps total pushes/day across all categories (parent-configurable col + config default 4); inbox rows not budgeted; config-driven cross-category priority; per-type cooldowns via Redis; quiet-hours + existing dedupe still apply; on lost arbitration the suppression reason is logged and the item **still lands in the inbox**; extends (does not fork) the pure evaluator + a thin arbiter.
- **P9-08:** lapse copy selected by idle-day tier (~2/5/14, config thresholds); each tier fires at most once per lapse episode; 3 tiered ar+en templates, personalized, never-shaming; subject to arbitration + LapseWinBack toggle; sends logged per tier; streak-repair **action** out of scope.

Plan ready — dispatch Batch 1.
