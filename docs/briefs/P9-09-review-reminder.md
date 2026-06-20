# Pipeline Brief — P9-09 Spaced-repetition review reminder (Notifications consumer)

## Summary & traceability
- **Task (1 line):** Add a Notifications consumer for the new `ReviewDueIntegrationEvent` (from P3-10's SR sweep) that fires a "وقت مراجعة سريعة" review-reminder nudge per child per sweep — mirroring `WeeklyRecapReadyIntegrationEventHandler` field-for-field (find-parent → prefs → evaluator → dedupe → BuildMessage → NudgeDispatcher, all fail-soft).
- **User story:** `user-stories/Phase-9-Notifications/P9-09-spaced-repetition-review-reminder.md` (FR-GM-8; FR-AD-4 spaced repetition). **Now UNBLOCKED** — P3-10 shipped `ReviewDueIntegrationEvent` (PR #196, on `main`).
- **Task file:** `tasks/Backend/Phase-9-Notifications/P9-09-BE.md` (P9-09-BE-1 consumer + handler, BE-2 template, BE-3 arbitration/analytics enrolment).
- **BRD goal:** G1 (adaptive learning / retention) surfaced through the parent-managed Notifications channel.
- **Epic / phase:** Notifications Module · Phase 9 (post-MVP). SP 3.
- **Precedent wave:** P9-06 (weekly-recap) — `docs/briefs/P9-06-habit-loop-categories.md`. P9-09 is the same shape; P9-06 is the working template for both code and tests.
- **Producer contract (already merged):** `backend/src/Shared/Learnexia.Shared.Contracts/Learning/ReviewDueIntegrationEvent.cs` (record `ReviewDueIntegrationEvent(EventId, OccurredOnUtc, StudentId, DueCount, IReadOnlyList<DueSkillSnapshot> TopSkills)` + `DueSkillSnapshot(SkillId, SkillName, EstimatedTimeMinutes)`). Per-student digest, one per sweep, opaque ids + curriculum strings only, **NO PII**.

## Business context & value
- **Who benefits:** the **student** gets a timely, learning-science-driven nudge ("a quick review of {skill} — {minutes} min") so weak/forgotten skills resurface at the right moment (FR-AD-4 retention), governed by the **parent-managed** Notifications channel.
- **Value:** the SR engine already computes *what is due* (and P3-10 now *emits* it); today nothing notifies the learner. This consumer is the last mile that turns the pull-only due list (`GET /api/Learning/Reviews/Due`) into a push/inbox nudge — closing the habit-loop catalog gap (the only un-built habit category).
- **Success measure:** event in → exactly one review-reminder inbox nudge per eligible child per day, copy referencing the top due skill + its minutes, ar-first with en fallback, never spammy (dedupe + arbitration), zero PII leaked into the notification.

## Acceptance criteria (testable)
- **AC1** — Publishing a `ReviewDueIntegrationEvent` for a child with a linked parent writes exactly one `Notification` row, code `REVIEW_DUE`, in the chosen category (see Decision 1).
- **AC2** — Copy is rendered from `ReengagementCopyTemplates` (ar-EG + en-US) with `{skill}` and `{minutes}` placeholders substituted from `TopSkills[0]` (and optionally `{dueCount}`); no `{...}` placeholder remains in the persisted Title/Body; no inline copy literals in the handler.
- **AC3** — The handler is fail-soft (ADR 0002): orphan child (no parent) → log + skip, no row, no throw; any internal exception is caught + logged via `ILoggerManager` and never propagates to the publisher.
- **AC4** — Per-`(child, category, day)` Redis dedupe holds: two events for the same child on the same day → one nudge (second is `dedupe_hit`).
- **AC5** — Not-eligible suppression: when `ReengagementEvaluator.Evaluate` returns `Eligible=false` (e.g. daily cap reached) → no nudge, `not_eligible` logged.
- **AC6** — Inbox-only posture v1 (see Decision 2): the inbox row is always written; the push channel is not delivered (no parent push opt-in path exists for this category until P9-04 FE). Confirmed by the push bit not being set absent an opted-in pref.
- **AC7** — Module isolation: the handler references the `Shared.Contracts` event ONLY — no reference to `Learning.Domain`/`Learning.*`. No new design pattern (rule 8 — mirror `WeeklyRecapReadyIntegrationEventHandler`).
- **AC8** — Localization: rendering with locale `en`/`en-US` produces the English template; `ar`/`ar-EG` (and unknown locale fallback) produce the Arabic template.
- **AC9** — No PII in the nudge: only the curriculum skill name + minutes + count appear; no child name/email/DOB.

## Affected modules & data
- **Notifications module** — additive only, no schema change:
  - **NEW** `Modules/Notifications/.../Application/IntegrationEventHandlers/Reengagement/ReviewDueIntegrationEventHandler.cs` — clone of `WeeklyRecapReadyIntegrationEventHandler`. Auto-registered via the host MediatR scan (no manual DI).
  - **EDIT** `Modules/Notifications/.../Domain/Templates/ReengagementCopyTemplates.cs` — add the `REVIEW_DUE` ar-EG + en-US entries.
  - **EDIT (only if Decision 1 = new member)** `Modules/Notifications/.../Domain/Enums/NotificationCategory.cs` — add `ReviewReminder = 7`.
- **No new entity, no new column.** `NotificationCategory` is stored as a plain `int` (no DB enum type, no CHECK constraint), so adding a member needs **no migration** (see Decision 1). `Notification` rows already carry `Category`/`Code`/`Title`/`Body`.
- **Learning module:** untouched (it is the producer, already shipped). No cross-module FK, no Learning reference.

---

## Six decisions — resolved (RECOMMENDATIONS, confirm before build)

### Decision 1 — Notification category → **add a NEW member `ReviewReminder = 7`** (no migration)
**Recommendation: add `ReviewReminder = 7` to `NotificationCategory`.** It is the next free non-zero int after `System = 6`.

Why a new member rather than riding `Achievement`:
- **Semantics:** a spaced-repetition review reminder is not an achievement/celebration — it is a habit/utility prompt. The P9-04 parent catalog (`tasks/Frontend/student-app/Phase-9-Notifications/P9-04-FE.md`) is a per-type toggle list; a distinct category is what lets the parent later toggle "review reminders" independently of badges/level-ups. Folding it into `Achievement` would make it un-separable in the parent UI and pollute achievement copy/cooldown analytics.
- **Arbitration priority:** the arbiter's `Notifications:PriorityOrder` is a CSV of category names. A distinct category lets review-reminders get their own deliberate priority rank later without dragging Achievement with them. (Unlisted categories fall to lowest priority by default — fine for v1.)

**The enum-0 / `HasDefaultValueSql` trap is NOT triggered by a new non-zero member** (verified in `NotificationConfig.cs:37-41`):
- `Category` is mapped `HasConversion<int>().IsRequired().HasSentinel((NotificationCategory)(-1)).HasDefaultValueSql("6")`.
- The bug P9-06 hit was specific to **value 0** (`WeeklyReport`): EF treated the CLR-default 0 as "unset" and omitted the column, so Postgres applied the SQL default `6=System`. `HasSentinel(-1)` fixed it by declaring the only "unset" sentinel as the never-real `-1`, so EF now always sends the real value (including 0).
- A new member at **`7`** is a normal non-zero value: EF always inserts it explicitly (`7 != -1`), and `HasDefaultValueSql("6")` only affects raw/pre-P4-09 inserts that omit the column entirely — which the dispatcher never does (it always sets `Category`). **So `ReviewReminder = 7` stores correctly with no migration and no `NotificationConfig` change.** (`HasSentinel(-1)` stays correct as-is.)
- **No DB migration is needed** for the enum addition (no enum DB type, no CHECK). Therefore **no `db-migration` batch** for this story. The P9-06 sentinel migration (`20260620012826_P9_06_NotificationCategorySentinel`) is already a no-op snapshot refresh; nothing analogous is required here.

**Follow-up to note (not a blocker):** add `ReviewReminder` to the P9-04 FE per-type toggle catalog (it's currently absent) — tracked against P9-04-FE, same way Achievement/WeeklyReport are. Flag for the planner; not built here.

> Alternative if the lead prefers zero enum churn: ride `Achievement` with code `REVIEW_DUE`. Functionally works (Achievement is already inbox-only and templated by code), but loses independent parent-toggle + priority + analytics separation. **Recommend the new member.**

### Decision 2 — Push vs inbox-only v1 → **inbox-only in v1** (handler dispatches normally; posture is emergent, not special-cased)
**Recommendation: inbox-only in v1**, consistent with the OQ-R3 precedent (WeeklyReport + Achievement are inbox-only until P9-04 FE per-type toggles ship).

How "inbox-only" is actually enforced (verified — there is **no allowlist**; the posture is emergent from prefs):
- The handler builds the message via `ReengagementHandlerHelper.BuildMessage`, which sets `ShouldPush = prefs.Push` (`ReengagementHandlerHelper.cs:128`).
- `prefs` come from `ChildReengagementPreferenceService.GetOrDefaultAsync`, which returns a stored row **or** `ChildReengagementPreference.CreateDefault(...)` with `Push = false` (`ChildReengagementPreference.cs:45-46`).
- The parent can only flip `Push = true` for categories the upsert path persists, and that path iterates a hardcoded `ReengagementCategories` set = **{ StreakAtRisk, DailyMissionReminder, LapseWinBack }** (`ChildReengagementPreferenceService.cs:17-22`). `ReviewReminder` is **not** in that set → no UI/API can ever set its `Push=true` today → `GetOrDefaultAsync` always returns `Push=false` for it → `ShouldPush=false` → the dispatcher writes the inbox row and skips push. **Inbox-only falls out automatically**, exactly like WeeklyReport/Achievement.
- **The handler must NOT special-case push** — it dispatches normally (`ShouldPush = prefs.Push`); the dispatcher gate (`NudgeDispatcher.DispatchAsync` step 2 only runs when `ShouldPush`) enforces the posture. This matches rule 8 (no new branch, mirror the existing handler).

When P9-04 lands per-type toggles + adds `ReviewReminder` to `ReengagementCategories`, this same code flips to parent-controllable push with **zero handler change**. Document the v1 posture in the handler XML-doc (copy the WeeklyRecap note).

### Decision 3 — Copy → `REVIEW_DUE` templates, placeholders `{skill}` + `{minutes}` (+ optional `{dueCount}`)
**Code:** `REVIEW_DUE`. **Category:** `ReviewReminder` (Decision 1). **Placeholders from `TopSkills[0]`:** `("skill", TopSkills[0].SkillName)`, `("minutes", TopSkills[0].EstimatedTimeMinutes.ToString())`. Optionally `("dueCount", DueCount.ToString())` if copy references the count.

Add to `ReengagementCopyTemplates.Templates` (the canonical ar/en nudge-copy store — this **is** the "no inline literals" location; nudge copy is NOT in `.resx`, only API envelope messages use `SharedResourcesKey`/`IStringLocalizer<SharedResources>`):

```
[$"ReviewReminder:REVIEW_DUE:{ArEg}"] = (
    "وقت المراجعة! 🧠",
    "🧠 وقت مراجعة سريعة لمهارة {skill} ({minutes} دقائق بس) — تعالى نثبّتها!"),
[$"ReviewReminder:REVIEW_DUE:{EnUs}"] = (
    "Time to review! 🧠",
    "🧠 Quick review time for {skill} (just {minutes} min) — let's lock it in!"),
```

Notes for the implementer:
- Arabic-first, child-safe, encouraging — never shaming (BRD §8). Keep the emoji style consistent with the existing entries.
- **`{minutes}` pluralization caveat (flag, don't over-engineer):** Arabic plural agreement for "دقيقة/دقيقتين/دقائق" varies by number; the template uses a single fixed phrasing ("دقائق بس"). This is consistent with how existing templates ignore CLDR plural rules (e.g. `{streakLength} يوم`). Acceptable for v1; note it as a known simplification (same posture as the streak templates).
- **`{dueCount}` is optional for v1** — `TopSkills[0]` headline is sufficient and matches the WeeklyRecap minimalism. If the lead wants "and N more", add a count-aware second variant later; do not branch templates in v1 unless asked.
- **Deep-link target = the review surface (P9-02 routing).** Per the verified precedent, the deep link is carried implicitly by the `Code` (`BuildMessage` hardcodes `DataJson: null` for ALL reengagement handlers — `ReengagementHandlerHelper.cs:127`). P9-02 maps the notification `Code` → route; the review route resolves to the data behind `GET /api/Learning/Reviews/Due`. **Do NOT add a structured `DataJson` payload** — that would deviate from the shared helper (would require changing `BuildMessage`'s signature for every handler). Keep `REVIEW_DUE` as the routing key; if a skill-deep-link param is ever wanted, that is a separate helper change, flagged as OQ-2.

### Decision 4 — Dedupe + arbitration → **composes correctly, no per-skill fan-out, nothing new needed**
The producer already guarantees **one event per student per sweep** (the digest decision in P3-10's brief). On the Notifications side, **no per-skill fan-out** — the handler consumes the single digest and emits at most one nudge. The existing gates apply unchanged:
- **Per-`(child, category, day)` Redis SETNX dedupe** via `ReengagementHandlerHelper.TryAcquireDedupeAsync` (keyed on `category` = `ReviewReminder` + `ev.OccurredOnUtc` day) — absorbs same-day duplicate delivery.
- **P9-07 global push budget + per-type cooldown** at `NudgeDispatcher` → `NudgeArbiter` — applies only to the push channel (irrelevant in v1 inbox-only, but correct once push is enabled). Cooldown TTL falls to the `DefaultCooldownHours = 24` (≤1/day) default since `REVIEW_DUE` isn't in the special-case switch — appropriate; no config change needed (a `Notifications:Cooldown:REVIEW_DUE` key can tune it later without deploy).
- **Daily cap** via `ReengagementEvaluator.Evaluate(prefs, now, sentsToday)` using `CountSentTodayAsync(child, category, …)` — per-category daily cap holds.
- **Cadence belongs to Notifications, not Learning** — the producer fires every sweep; the consumer's dedupe/cooldown decide whether to actually nudge. This is the established division of responsibility. **No new dedupe/arbitration code; just pass `NotificationCategory.ReviewReminder` + `OccurredOnUtc` through the existing helpers.**

### Decision 5 — Cross-module MediatR registration → **already covered, no action**
The new handler is an `INotificationHandler<ReviewDueIntegrationEvent>` in the **Notifications.Application** assembly. The host's `AddCrossModuleMediatR` (`backend/src/Host/Learnexia.Host/Extensions/MediatRExtensions.cs:38`) already scans `Learnexia.Modules.Notifications.Application.AssemblyReference` — the **same** registration that picks up `WeeklyRecapReadyIntegrationEventHandler`. The Learning producer's `IPublisher.Publish(ReviewDueIntegrationEvent)` fans out cross-module via the single host-wide MediatR registration. **The new handler auto-registers; no DI/Program.cs edit, no `.sln`/`Directory.Packages.props` change.** (Good for parallelism — no serialized shared-file edits.)

### Decision 6 — Tests (api-tester integration coverage, mirror `P9_06_HabitLoop_Tests`)
Use the P9-06 harness verbatim (`backend/tests/Learnexia.IntegrationTests/P9_06_HabitLoop_Tests.cs`): `CreateParentChildPairAsync`, `PublishAsync<TEvent>` via a fresh DI scope's `IPublisher`, `GetNotificationsAsync(childId, code, category)`, `Task.Delay` for async fan-out.
- **T1 (happy path / AC1+AC2):** publish `ReviewDueIntegrationEvent(StudentId=childId, DueCount=2, TopSkills=[{1,"الكسور",5},{2,"الجمع",3}])` → exactly one row, `Code="REVIEW_DUE"`, `Category=ReviewReminder`, Title/Body non-empty, body contains "الكسور" and "5", no `{skill}`/`{minutes}` remaining.
- **T2 (dedupe / AC4):** publish twice same child same day → exactly one row.
- **T3 (not-eligible / AC5):** drive the daily cap (or a pref with `DailyCap` exhausted) → second/over-cap event produces no row; assert `not_eligible`/suppressed path (no nudge).
- **T4 (inbox-only posture / AC6):** assert the persisted row's `DeliveredChannels` has the in-app bit and **not** the push bit (push=2) for a default (no opted-in) child; register a device token to prove push is still suppressed because `prefs.Push=false`.
- **T5 (ar/en copy / AC8):** child with locale `ar` → Arabic body; child with locale `en` → English body (set via the child's `PreferredLanguage`, resolved by `GetLocaleAsync`).
- **T6 (fail-soft orphan / AC3):** publish for an orphan childId (no parent) → no throw, no row (mirror P906-TC09).
- **T7 (no PII / AC9):** assert the persisted Title/Body contain only the skill name/minutes/count — no child name/email substring.
- **Unit (optional, Notifications.UnitTests):** extend `ReengagementCopyTemplatesTests` with `REVIEW_DUE` ar+en render + placeholder-substitution cases (cheap, high signal, mirrors existing).

---

## Handoff → db-migration
**None.** No new entity, column, or schema change. `NotificationCategory` is a plain int column with `HasSentinel(-1)` + `HasDefaultValueSql("6")`; adding `ReviewReminder = 7` needs no migration and no `NotificationConfig` change (Decision 1). **Do not spin up a db-migration batch.**

## Handoff → backend-feature (single batch)
- **NEW** `ReviewDueIntegrationEventHandler.cs` — clone `WeeklyRecapReadyIntegrationEventHandler` exactly; swap: `WeeklyRecapReadyIntegrationEvent` → `ReviewDueIntegrationEvent`; `category = NotificationCategory.ReviewReminder`; `code = "REVIEW_DUE"`; placeholders `("skill", ev.TopSkills[0].SkillName)`, `("minutes", ev.TopSkills[0].EstimatedTimeMinutes.ToString())` (optionally `("dueCount", ev.DueCount.ToString())`). **Guard:** if `ev.TopSkills` is empty, log + skip (defensive — producer guarantees ≥1, but never index `[0]` unguarded). Copy the inbox-only-v1 XML-doc note. Log lines mirror the WeeklyRecap analytics tags (`analytics.reengagement.sent/dedupe_hit/not_eligible`).
- **EDIT** `ReengagementCopyTemplates.cs` — add the two `ReviewReminder:REVIEW_DUE` entries (Decision 3). No handler-side literals.
- **EDIT** `NotificationCategory.cs` — add `ReviewReminder = 7` (Decision 1).
- **Constraints:** module isolation (Shared.Contracts only — no `Learning.*` reference); Option-C (EF behind the existing services — the handler touches none directly, only the injected service interfaces); `ILoggerManager`; fail-soft top-level try/catch (ADR 0002); rule 8 (mirror, no new pattern).
- **No DI registration** beyond the host MediatR scan (Decision 5).

## Handoff → frontend
**None in this story (backend-only).** The student inbox already renders any `Notification` row (the bell + inbox list). The **parent per-type toggle** for "review reminders" (adding `ReviewReminder` to the P9-04 catalog so push becomes parent-controllable) is **P9-04-FE**'s concern — flag for the planner to track against P9-04; the v1 inbox-only posture (Decision 2) is the consequence of P9-04 not yet covering this category. P9-02 deep-link routing must map `Code="REVIEW_DUE"` → the review surface (existing FE routing concern; note for whoever owns P9-02 FE).

## Open questions / assumptions / risks
- **OQ-1 (confirm — primary):** New enum member `ReviewReminder = 7` vs ride `Achievement`. **Recommend new member** (Decision 1). Confirm before build — this is the only real decision; everything else is mechanical mirroring.
- **OQ-2 (deferred):** Structured deep-link `DataJson` (e.g. carry `skillId` so the review opens pre-filtered). Out of scope v1 — would require changing the shared `BuildMessage` signature for all handlers. Routing by `Code` is the established precedent. Defer unless the lead wants per-skill deep-linking now.
- **OQ-3 (FE follow-up, not a blocker):** `ReviewReminder` must be added to the P9-04 FE per-type toggle catalog + (for push to ever fire) to the `ReengagementCategories` set in `ChildReengagementPreferenceService`. Until then it is inbox-only by construction. Track against P9-04; do not build here.
- **Assumption:** `TopSkills[0]` is the headline (most-urgent) skill — guaranteed by P3-10 (ordered most-urgent-first). Handler guards against an empty list defensively.
- **Assumption:** single-language skill name (`SkillName`) is injected as a placeholder into the locale-aware template — consistent with P3-10's brief OQ-1 (curriculum is single-language-per-deployment). Not a P9-09 concern.
- **Risk — Arabic minutes pluralization:** fixed phrasing, no CLDR plural agreement (matches existing templates). Acceptable v1; flagged.
- **Risk — scope creep:** do not touch the Learning producer, P4-06 missions, or `BuildMessage`. This is consumer + template + enum member only.

## Recommended pipeline order (first cut — planner finalizes)
1. **backend-feature** (single batch): add `ReviewReminder=7` enum member + `REVIEW_DUE` templates + `ReviewDueIntegrationEventHandler` (mirror WeeklyRecap). No db-migration batch, no designer (no UI).
2. **api-tester**: integration tests T1–T7 against the running host (publish event via `IPublisher`, assert inbox row, dedupe, not-eligible, inbox-only channel bits, ar/en, fail-soft orphan, no PII) — mirror `P9_06_HabitLoop_Tests`. (+ optional `ReengagementCopyTemplatesTests` unit cases.)
3. **security-auditor** (light pass): confirm NO-PII in nudge copy/row, fail-soft, module isolation intact (no Learning ref), inbox-only posture (no unintended push). Touches a cross-module event surface + child-facing copy, so a quick gate is warranted.
4. **reviewer**: gate against AC1–AC9 + CONVENTIONS (rule 1 isolation, Option-C, rule 5 logger, rule 8 no new pattern, fail-soft ADR 0002).
5. **committer**: branch `feat/P9-09-review-reminder`, conventional commit, push + PR. Update `docs/dev/HANDOFF.md` (mark P9-09 done/unblocked-and-built; record the new `ReviewReminder=7` category + `REVIEW_DUE` code + inbox-only-v1 posture + the P9-04 FE catalog follow-up).

This is a **one-batch** addition — no within-story parallelism, no shared-file serialization needed (handler auto-registers via the existing host MediatR scan).
