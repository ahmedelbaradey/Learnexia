# Pipeline Brief — P3-10 `ReviewDueIntegrationEvent` (cross-module seam)

## Summary & traceability
- **Task (1 line):** Emit a `ReviewDueIntegrationEvent` from the Learning module's daily SR sweep so other modules can react to "a skill is due for review for a child" — the deferred cross-module contract that unblocks **P9-09** (spaced-repetition review reminder, Notifications); later also consumed by P4-06 missions (NOT in scope now).
- **User story:** `user-stories/Phase-4-AI-Tutor/P3-10-spaced-repetition.md` (FR-AD-4, BRD goal G1 *adaptive learning / retention*, Epic *Adaptivity & Student Modeling*, Phase 4).
- **Task file:** `tasks/Backend/Phase-4-AI-Tutor/P3-10-BE.md` — note (line 34): *"P3-10 exposes the due-list HTTP endpoint; Gamification missions (P4-06) will later consume it via an integration contract. Do NOT build a Gamification FK or cross-module call now."* **This brief builds exactly that deferred integration contract** (now authorized by the lead), as a `Shared.Contracts` integration event — NOT a cross-module call, NOT a Gamification coupling.
- **Scope guard:** Everything else in P3-10 is **already built and merged** (commit `25e9b98`, on `main`): `SpacedRepetitionEngine`, `SpacedRepetitionOptions`, `SpacedRepetitionSweepJob`, `ReviewsService` + `GET /api/Learning/Reviews/Due`, the SR columns on `StudentSkillMastery`, the `SpacedRepetition:Engine` config, and the `CompleteAttemptCommandHandler` ladder hook. **This is a surgical addition to the existing sweep job + one new contract type. Do NOT re-plan the story.**

## Business context & value
- **Who benefits:** the **student** (gets a timely "time for a quick review" nudge so weak/forgotten skills resurface at the right moment — FR-AD-4 retention), surfaced through the **parent-managed** Notifications channel (P9 arbitration + push budget).
- **Value:** the SR engine already computes *when* a skill is due, but today nothing **notifies** the learner — the due list is pull-only (`GET /Reviews/Due`). This event is the push trigger. Without it, P9-09 stays blocked (HANDOFF line 19: *"P9-09 (blocked on P3-10)"*).
- **Success measure:** P9-09 can consume a stable contract and emit copy like `🧠 وقت مراجعة سريعة لمهارة الكسور (~5 دقائق)` while respecting the P9-07 global push budget + arbitration + day-grained dedup.

## Acceptance criteria (testable)
- AC1 — The daily SR sweep publishes a `ReviewDueIntegrationEvent` for each student that has ≥1 skill due that sweep (granularity decision below: **per-student digest**, one event per student per sweep).
- AC2 — The event implements `Learnexia.Shared.Contracts.IIntegrationEvent`, lives under `Shared.Contracts/Learning/`, and carries **opaque `int` ids + curriculum display strings only — NO PII** (no names, no emails).
- AC3 — Publishing is **fail-soft and post-commit**: a publish exception is caught + logged via `ILoggerManager` and never propagates into the sweep loop or rolls back the already-committed SR column writes (mirrors `StreakSweepJob` / `WeeklyReportGeneratorService`).
- AC4 — Publishing is **non-spammy**: a student with the same unpracticed due skill across consecutive daily sweeps does not produce a runaway notification storm — bounded to **at most one event per student per sweep**, and the downstream day-grained dedup + arbitration (already in Notifications) absorbs the rest. No new column/migration is introduced (decision 3).
- AC5 — Module isolation preserved: no new project reference Learning→Gamification/Notifications; no cross-module FK; the only shared surface is the new `Shared.Contracts` record. No new design pattern introduced (rule 8).
- AC6 — Integration test proves the event fires once-per-eligible-student on a sweep with due rows, fires zero times when no rows are due, and carries the correct payload (ids + count + top-skill name + minutes).

## Affected modules & data
- **Learning module (producer)** — EDIT only:
  - `Learnexia.Modules.Learning.Infrastructure/Jobs/SpacedRepetitionSweepJob.cs` — add post-write, fail-soft publish.
- **Shared.Contracts** — NEW:
  - `Shared/Learnexia.Shared.Contracts/Learning/ReviewDueIntegrationEvent.cs` (+ a nested `DueSkillSnapshot` record).
- **No new entities, no schema change, NO migration** (see decision 3). `StudentSkillMastery` already exposes everything needed; the sweep already `Include(m => m.Skill)` so `Skill.Name` and `Skill.EstimatedTimeMinutes` are loaded.
- **Notifications module (consumer)** — **OUT OF SCOPE for this slice** (that is P9-09's own story). This brief only ships the producer + contract.

---

## Five design decisions — recommendations

### 1. Event granularity → **per-student digest (one event per student per sweep)** ✅
**Recommendation: per-student digest.** One `ReviewDueIntegrationEvent` per student per sweep, carrying `DueCount` + a small top-N (`TopSkills`, cap **3**) ordered by urgency, with the single most-urgent skill first.

Justification, grounded in the existing consumer:
- P9-09 is arbitrated and globally push-budgeted (P9-07) and day-grained-deduped per `(child, category, day)` (see `ReengagementHandlerHelper.TryAcquireDedupeAsync` + `WeeklyRecapReadyIntegrationEventHandler`). It wants to send **at most one** "time to review" nudge per child per day, with copy referencing the **top** skill (`وقت مراجعة سريعة لمهارة الكسور`). Per-skill events would force the consumer to either collapse N events back into one (wasteful fan-out: a student with 8 due skills = 8 events all but one discarded by dedup) or risk N nudges.
- A digest matches the existing precedent: `WeeklyRecapReadyIntegrationEvent` is **one event per child** carrying summary scalars, consumed into one nudge. Same shape, same consumer ergonomics.
- The top-skill name + its `EstimatedTimeMinutes` give the consumer exactly the placeholder values its template needs (`{skillName}`, `{minutes}`) with no back-reference to Learning.
- `DueCount` lets future copy say "and 4 more" without another contract change.

### 2. Payload + module isolation → exact record shape ✅
**Correction to the lead's framing:** Learning's `Skill` entity carries a **single `Name`** (string), **not** `NameEn`/`NameAr` (verified: `Skill.cs`). The `NameEn`/`NameAr` pair the lead referenced (`ActiveTimedEventDto` / `ActiveTimedEventSnapshot`) originates in **Gamification** timed events, not Learning curriculum. So the payload must carry the **single** `SkillName` that actually exists — fabricating a bilingual pair would be inventing data.

This is also **correct for the consumer**: every reengagement handler resolves the recipient's locale itself (`ReengagementHandlerHelper.GetLocaleAsync`) and renders copy from locale-aware **templates** (`ReengagementCopyTemplates.Render`) with placeholder substitution. The skill name is injected as a **placeholder value** into the consumer's own localized template — the payload does **not** need to be bilingual. (Caveat to flag: curriculum content language follows the platform's content locale; carrying the single stored `Name` is consistent with how `DueReviewDto` already surfaces `SkillName`. See OQ-1.)

Exact shape (in `Shared.Contracts/Learning/ReviewDueIntegrationEvent.cs`):

```csharp
namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Raised once per student per SR sweep when ≥1 skill is due for spaced-repetition review.
/// Consumed by P9-09 (Notifications — SpacedRepetition review reminder). Later by P4-06 missions.
/// Per-student digest: DueCount + top-N (cap 3) most-urgent due skills.
/// Opaque int ids + curriculum display strings only — NO PII.
/// </summary>
public sealed record ReviewDueIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int DueCount,
    IReadOnlyList<DueSkillSnapshot> TopSkills) : IIntegrationEvent;

/// <summary>One due skill in the digest. SkillName is Learning-owned curriculum data (allowed).</summary>
public sealed record DueSkillSnapshot(
    int SkillId,
    string SkillName,
    int EstimatedTimeMinutes);
```

Notes:
- `EstimatedTimeMinutes` comes from `Skill.EstimatedTimeMinutes` (already loaded via the existing `Include`) → enables the "~5 دقائق" copy.
- `TopSkills` is ordered most-urgent-first; `TopSkills[0]` is the headline skill for the nudge title; cap = **3** (config-free constant in the producer).
- No `Status`, no `NextReviewDueAt` in the payload — the consumer only needs "what to show", not the SR internals. Keep it lean (mirrors `WeeklyRecapReadyIntegrationEvent` minimalism).

### 3. Emission point + cadence/dedup → **emit-every-sweep, no new column, lean on consumer dedup** ✅
**Recommendation: the `SpacedRepetitionSweepJob` is the emitter; emit one digest per due student every sweep; do NOT add a "last notified" column; rely on the consumer's existing day-grained dedup + arbitration. No migration.**

Why not "emit only on newly-due transition":
- A "newly-due" check would compare *previous* `NextReviewDueAt` (future) vs *now* (elapsed). **But the sweep itself overwrites `NextReviewDueAt` every run** (verified in `SpacedRepetitionSweepJob.RunAsync`: it recomputes `nextDueAt = LastPracticedAt + interval` and writes it back for every due row, including `NeedsReview` rows getting `LastPracticedAt + 1`). So by the time we'd inspect the column it's already mutated — there is **no free, reliable newly-due signal** in the current column set.
- Detecting newly-due robustly would require a **new `LastReviewNotifiedAt` (or similar) column → a migration → db-migration becomes a first batch.** That is real added complexity for a problem the consumer already solves.

Why emit-every-sweep is safe (not spammy):
- The sweep is **daily** and emits **at most one digest per student per run** (decision 1) — bounded fan-out.
- Notifications already enforces, for every reengagement nudge: per-`(child, category, day)` **Redis SETNX dedup**, per-category daily cap, and the P9-07 **global push budget + cooldown** at `NudgeDispatcher`. A student with the same unpracticed skill day after day gets the daily nudge governed by *those* gates (P9-09's own cadence policy) — which is where review-reminder cadence **belongs** (it's a notification-policy concern, not a Learning concern). Putting cadence here would duplicate/fork that policy.
- This matches `StreakSweepJob` and `WeeklyReportGeneratorService`: both emit straight from the job and defer dedup/cadence to the consumer.

Net: **lowest-complexity, no migration, no schema change, no batch reordering.** The producer's only job is "tell the world who has reviews due today"; the consumer decides whether/how often to nudge.

> If the lead later wants Learning-side suppression (e.g. don't re-emit for the same due skill within 7 days), that is a **follow-up** requiring `LastReviewNotifiedAt` + migration — flagged as OQ-2, explicitly out of scope now.

### 4. Emission mechanics → **direct `IPublisher.Publish` from the job scope, fail-soft post-write** (mirror `WeeklyReportGeneratorService` / `StreakSweepJob`, NOT the republisher) ✅
**Recommendation: publish directly via `IPublisher` resolved from the sweep's own DI scope, after the SR column writes, each publish individually try/caught.**

Pattern selection (rule 8 — mirror existing, no new pattern):
- The **`TimedEventStartedRepublisher`** pattern (intra-module **domain event** → `INotificationHandler` republishes the integration event) is the wrong fit here: it requires a domain event raised on a change-tracked aggregate inside a MediatR request scope. The sweep is a **Hangfire job with no MediatR request scope**, and it uses `ExecuteUpdateAsync` (bulk update, **no change tracker, no aggregate, no domain event**). There is nothing to republish.
- The **right** precedent is **`WeeklyReportGeneratorService`** (and `StreakSweepJob`): a job-context producer that, *after the DB write*, resolves `IPublisher` from its own scope and publishes the `Shared.Contracts` integration event directly, with a per-publish try/catch that logs and continues. This is the established Hangfire-producer shape in this repo.

Concrete mechanics for `SpacedRepetitionSweepJob.RunAsync`:
1. Resolve `IPublisher` from the existing `scope` (`scope.ServiceProvider.GetRequiredService<IPublisher>()`) — the job already creates `_scopeFactory.CreateAsyncScope()`; the host-wide single `AddMediatR` scan (`MediatRExtensions.cs`) means `IPublisher.Publish` fans out cross-module to the Notifications handler.
2. The loop already iterates due rows and updates SR columns per row. **Group the loaded `dueRows` by `StudentId`** (an in-memory `GroupBy` over the already-fetched list — no extra query), AFTER the per-row updates complete.
3. For each student group: build the digest (`DueCount` = group size; `TopSkills` = top 3 by urgency — see ordering note below; `SkillName`/`EstimatedTimeMinutes` from the already-`Include`d `row.Skill`), then `await publisher.Publish(new ReviewDueIntegrationEvent(...), ct)` inside its own `try/catch` that logs via `_logger.LogError` and continues (does not abort the sweep).
4. UTC discipline: `OccurredOnUtc = utcNow` (the job's captured `DateTime.UtcNow`); no other timestamps in the payload.
5. Counters: extend the existing summary log line (`P3-10: SR-Sweep complete — …`) with `eventsPublished` / `eventPublishFailed`.

Ordering note for `TopSkills` (urgency): cleanest deterministic order without new data = `NeedsReview` rows first, then `Mastered` rows by oldest `NextReviewDueAt` (most overdue first). All inputs are already on the loaded rows. Keep this as a private ordering helper in the job — no engine change.

> Reviewer/security note: confirm `IPublisher` is registered in the job's scope (it is — host-wide MediatR). The publish must be **after** `UpdateSpacedRepetitionFieldsAsync` for that student's rows so the event reflects committed state (post-commit guarantee). Since each `ExecuteUpdateAsync` commits immediately (no UoW, rule 3), grouping/publishing after the per-row update loop satisfies this naturally.

### 5. Tests ✅
**Unit (Learning.UnitTests) — optional, low value here:** the engine/IsDue logic is already unit-tested (13 cases). The only *new* pure logic is the top-N ordering helper; if extracted as a private static, cover it with 2–3 cases (NeedsReview-first, most-overdue-first, cap-at-3). If left inline in the job, cover it via the integration test instead.

**Integration (Learnexia.IntegrationTests) — primary proof, api-tester:** mirror the existing `P3_10_SpacedRepetition_Tests.cs` / `StreakSweepJob` Hangfire harness (HANDOFF: jobs triggerable via `BackgroundJob.Enqueue` / direct `RunAsync`). Capture published events with a test `INotificationHandler<ReviewDueIntegrationEvent>` (or a spy `IPublisher`) registered in the test host. Cases:
- **T1 (fires once per student):** seed 2 students, student A with 2 due skills + student B with 1 due skill → run sweep → exactly **2** events; A's event has `DueCount=2` and 2 `TopSkills`; B's has `DueCount=1`.
- **T2 (no due rows → no events):** seed only not-due (`Mastered`, future `NextReviewDueAt`) rows → run sweep → **0** events.
- **T3 (payload correctness):** assert `StudentId`, `DueCount`, `TopSkills[0].SkillName` == the most-urgent skill's name, `EstimatedTimeMinutes` populated, **no PII** fields present, `OccurredOnUtc` is UTC-kind.
- **T4 (idempotent / non-spammy per sweep):** run the sweep **twice** back-to-back with the same due data → each run emits the same bounded set (one digest per student) — proves the sweep doesn't multiply events within a run and the per-run cap holds. (Cross-day suppression is the consumer's dedup, tested in P9-09 — out of scope here.)
- **T5 (fail-soft):** force the publisher to throw for one student → sweep still completes, the SR column updates for all students still committed, error logged, other students' events still published.
- **T6 (ordering):** `NeedsReview` skill ranks above an overdue `Mastered` skill in `TopSkills`; list capped at 3 when >3 due.

---

## Handoff → db-migration
**None.** No new entity, field, or schema change. Do **not** spin up a db-migration batch for this slice. (If decision 3's optional Learning-side suppression is ever approved, *that* follow-up needs `LastReviewNotifiedAt` + migration — not now.)

## Handoff → backend-feature
- **NEW:** `Shared/Learnexia.Shared.Contracts/Learning/ReviewDueIntegrationEvent.cs` — the record + nested `DueSkillSnapshot` per decision 2's exact shape. Implements `IIntegrationEvent`. XML-doc it like `WeeklyRecapReadyIntegrationEvent` (who raises, who consumes, NO-PII note).
- **EDIT:** `Modules/Learning/Learnexia.Modules.Learning.Infrastructure/Jobs/SpacedRepetitionSweepJob.cs` — per decision 4:
  - Resolve `IPublisher` from the existing job scope.
  - After the per-row SR update loop, `GroupBy(StudentId)` over the already-loaded `dueRows`; build + publish one digest per student, each in its own try/catch (fail-soft, `ILoggerManager`).
  - Top-N (=3) ordering helper (NeedsReview-first, then most-overdue `NextReviewDueAt`).
  - Extend the summary log with publish counters.
- **Constraints:** module isolation (Shared.Contracts only, no Learning→Notifications ref); rule 3 (no UoW — publishes are post-commit since `ExecuteUpdateAsync` already committed); rule 5 (`ILoggerManager`); UTC for `OccurredOnUtc`; rule 8 (no new pattern — mirror `WeeklyReportGeneratorService`).
- **No new DI registration needed** beyond what already exists (job is registered Transient; `IPublisher` is host-wide).

## Handoff → frontend
**None — backend/infra only.** No UI surface in this slice (the eventual student-facing nudge is rendered by the Notifications inbox, owned by P9-09 / the existing notifications FE).

## Open questions / assumptions / risks
- **OQ-1 (resolved with a flag):** Skill display name is a **single** `Skill.Name`, not bilingual. Payload carries the single `SkillName` (consumer localizes the *template*, injects the name as a placeholder). **Assumption:** curriculum content is single-language-per-deployment, consistent with the existing `DueReviewDto`. If product later wants bilingual skill names, that's a curriculum-model change far beyond this slice. Recommend the lead confirm this assumption but it should not block.
- **OQ-2 (out of scope, flagged):** Learning-side cross-day suppression (`LastReviewNotifiedAt`). Deliberately **deferred** — cadence is a Notifications-policy concern handled by P9-09's dedup/arbitration. Building it here would need a migration + reorder the batches. Recommend deferring unless the lead wants Learning to own review-reminder cadence (it shouldn't, by precedent).
- **OQ-3 (P9-09 contract sanity-check):** P9-09 will need a new `NotificationCategory` + copy templates + (optionally) push-enable policy — all that is **P9-09's** story, not this one. Flagging only so the planner doesn't conflate the two; this brief ships the contract, P9-09 consumes it.
- **Risk — scope creep:** the temptation is to also touch P4-06 missions or the consumer. **Do not.** This slice is the contract + producer only. Missions/Gamification coupling is explicitly excluded (task note line 34).
- **Risk — TopSkills cap as magic number:** 3 is a producer constant. Acceptable (mirrors `MaxWeakAreasSnapshot = 5` in `WeeklyReportGeneratorService`); not worth a config knob. Note in code.

## Recommended pipeline order (first cut — planner finalizes)
1. **backend-feature** (single batch): add `ReviewDueIntegrationEvent` to Shared.Contracts + wire the fail-soft publish into `SpacedRepetitionSweepJob`. (No db-migration batch — no schema change. No designer — no UI.)
2. **api-tester**: integration tests T1–T6 against the running sweep (Hangfire harness), event capture via test handler/spy publisher.
3. **security-auditor**: light pass — confirm NO-PII payload, fail-soft (no event leaks across modules with personal data), module isolation intact. (Touches cross-module event surface, so worth a quick gate.)
4. **reviewer**: gate against AC1–AC6 + CONVENTIONS (module isolation rule 1, rule 3 post-commit, rule 5 logger, rule 8 no new pattern) + api-tester + security results.
5. **committer**: branch `feat/P3-10-review-due-event`, conventional commit, push + PR. Update `docs/dev/HANDOFF.md` (mark P9-09 unblocked; record the new contract).

This is a **one-batch** addition — no parallelism needed within the story.
