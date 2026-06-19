# Execution Plan — P5-03 Analytics event-capture backbone (+ P7-11b streaming AI-usage capture)

> Wave plan. Two **independent** backend sub-pipelines in one wave on branch `feat/P5-03-analytics-backbone` (already checked out):
> - **Sub-pipeline A — P5-03** — NEW `Analytics` module (schema `analytics`) that consumes the EXISTING `Shared.Contracts` event backbone, derives read-time sessions/retention/DAU, exposes platform read seams, and lights up the P7-10 deferred KPI facets.
> - **Sub-pipeline B — P7-11b** — close the `AiGateway.StreamAsync` capture gap so streamed (SSE) tutor calls also persist one `ai.AiUsageLogs` row via the existing `IAiUsageRecorder`.
>
> They share **only** the wave and the NFR-1 fail-soft theme. There is **no code dependency** between them → they run as two parallel tracks. P7-11b touches **none** of the shared scaffold files, so it can run alongside A safely.

---

## Source

- Brief: `docs/briefs/P5-03-analytics-backbone.md`
- Stories: `user-stories/Phase-5-Parent-Analytics/P5-03-analytics-event-capture.md`, `user-stories/Phase-7-Admin-Console/P7-11b-streaming-ai-usage-capture.md`
- Task files: `tasks/Backend/Phase-5-Parent-Analytics/P5-03-BE.md`, `tasks/Backend/Phase-7-Admin-Console/P7-11b-BE.md`
- Rules: `CLAUDE.md` (pipeline order, module-isolation #1, no-UoW #3 / ADR-0001, Option C / CONVENTIONS §7), `docs/dev/PARALLELISM.md` (shared-file serialization), `docs/dev/adr/0001-unit-of-work.md`.

> **Source-of-truth note (binding):** the brief was written before the DAU decision was locked and still describes a `UserSignedInIntegrationEvent` / Identity sign-in producer. **That is superseded.** The locked decision and the **task file** are authoritative: **DAU = distinct active STUDENTS by `ActivityEvent`** — **NO Identity sign-in producer, NO Identity change** beyond the P7-10 façade read-wiring (BE-6). This plan builds against the task file (BE-0..7) and the locked decisions. The brief's OQ-2 (sign-in identity) is therefore **RESOLVED / moot**.

---

## Task inventory

### Sub-pipeline A — P5-03 (new `Analytics` module)

| ID | Stack / agent | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P5-03-BE-0 | db-migration (scaffold) → backend-feature (DI/Host/MediatR) | Scaffold `Analytics` module (Domain/Application/Infrastructure/Api), schema `analytics`, `AnalyticsDbContext` + audit-stamping `SaveChangesAsync(int)` + `AnalyticsDbContextFactory`, `AddAnalyticsModule`, Host wiring, **cross-module MediatR registration** in `AddCrossModuleMediatR`. Mirror `Moderation`. **Holds the shared-file lock** (sln / Host Program.cs / AddCrossModuleMediatR / Directory.Packages.props). | 5 | — |
| P5-03-BE-1 | db-migration | `ActivityEvent` entity + EF config + **migration**: `StudentId` (int, no FK), `EventType` (string), `SubjectCode` (int?), `DurationSeconds` (int?), `OccurredAtUtc` (timestamptz, indexed), `SourceEventId` (uuid, unique). Indexes: `OccurredAtUtc`, `(StudentId, OccurredAtUtc)`, unique `(SourceEventId)`. Append-only, mirror `AiUsageLog`/`SafetyEvent` config. | 4 | BE-0 |
| P5-03-BE-2 | backend-feature | Consume the **existing** event backbone — identify/confirm the consumed events: `LessonCompletedIntegrationEvent`, `MissionCompletedIntegrationEvent`, `StudentLeveledUpIntegrationEvent`, AI help events (`HelpRequested`/`HelpDelivered`/`HintUsed`). **No new producer, no Identity change.** Do NOT wire the contract-only `AnswerSubmittedIntegrationEvent`. (Event-mapping plumbing for BE-3.) | 2 | BE-0 |
| P5-03-BE-3 | backend-feature | Capture consumers — idempotent, fail-soft `INotificationHandler<T>` per consumed event → map to `ActivityEvent` (`SourceEventId = notification.EventId`) → append via `IActivityEventStore` (mirror `AiUsageLogStore`; direct `SaveChangesAsync(userId:0)`, no UoW; swallow unique-violation as no-op for idempotency). | 5 | BE-1, BE-2 |
| P5-03-BE-4 | backend-feature | `IActivitySessionService` (Infrastructure) — read-time derivation over a window: distinct-active-students (DAU/WAU/MAU), session count + duration (gap-split on `SessionGapMinutes`, config default 30), retention (distinct active UTC days). `AsNoTracking`, date-windowed, sentinel-safe. | 6 | BE-1 |
| P5-03-BE-5 | backend-feature | Platform read seams in `Shared.Contracts/Analytics` — `IPlatformActivityQuery`, `IPlatformSessionStatsQuery`, `IPlatformRetentionQuery` (+ result records) implemented in `Analytics.Infrastructure`, registered Scoped, windowed `GetPlatformAsync(fromUtc, toUtc, ct)`, sentinel-safe. Mirror `IPlatformLearningStatsQuery` + adapter shape. | 5 | BE-4 |
| P5-03-BE-6 | backend-feature | Light up P7-10 façade — `GetPlatformKpisQueryHandler` (Identity.Application) consumes the three new seams in the existing `Task.WhenAll` fan-out: replace `RetentionNaReason`/`SessionDurationNaReason` with real values, upgrade `DistinctActiveStudents` to true activity-based DAU (keep proxy labelled until cutover). **No P7-10 contract shape change.** | 4 | BE-5 |
| P5-03-BE-7 | api-tester + security-auditor | Runtime tests + audit: ingest idempotency (redelivery → no double-count), fail-soft (consumer throw doesn't break producer), DAU/retention/duration aggregation correctness, window filter, empty-window 200/zeroed, AdminOnly on exposed endpoint, **child-privacy/PII-light** audit. | 6 | BE-1..6 |

### Sub-pipeline B — P7-11b (Ai module only)

| ID | Stack / agent | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| P7-11b-BE-1 | backend-feature | `AiGateway.StreamAsync` — accumulate provider end-of-stream `AiUsage`, run through existing `EnrichWithCost`, call `_usageRecorder.Record(usage, request.Task)` **after clean stream completion only** (post-loop / `finally`-guarded; no record on caller-cancel/error). Reuse the audited `IAiUsageRecorder` — no new write pattern. | 4 | — |
| P7-11b-BE-2 | backend-feature | Drop the "non-streaming only" caveat in XML docs/comments: `AiGateway`, `IPlatformAiSafetyStatsQuery.AiRequestVolume`, `PlatformKpiSummaryDto.AiRequestVolume`, P7-11 `TutorUsageDto`. **No contract shape change.** | 1 | BE-1 |
| P7-11b-BE-3 | api-tester | Streamed call → one `ai.AiUsageLogs` row (poll, fire-and-forget); caller-cancelled/errored stream behaves per BE-1 (no record); `/AiSafety/usage` + P7-10 `aiRequestVolume` include the streamed row. If fake provider doesn't stream usage → assert row-present-with-available-fields (don't fabricate). | 3 | BE-1 |

**Wave total:** P5-03 ≈ 37h + P7-11b ≈ 8h.

---

## Dependency order

**Sub-pipeline A (largely sequential):**
```
BE-0 (scaffold, shared-file lock)
  └─> BE-1 (entity + migration)
        ├─> BE-3 (consumers + store)   [also needs BE-2]
        └─> BE-4 (session/retention/DAU derivation)
              └─> BE-5 (read seams + adapters)
                    └─> BE-6 (P7-10 façade light-up)
BE-2 (confirm consumed events) ── feeds ──> BE-3   [needs only BE-0]
BE-7 (api-tester + security-auditor) ── needs ──> BE-1..6
```
- BE-2 and BE-4 can both start once their predecessors are done (BE-2 after BE-0; BE-4 after BE-1) and may overlap, but the same `backend-feature` track is otherwise mostly serial because BE-3→nothing-downstream while BE-4→BE-5→BE-6 is the long pole.
- The critical path is **BE-0 → BE-1 → BE-4 → BE-5 → BE-6 → BE-7**.

**Sub-pipeline B (independent, runs in parallel with A from t=0):**
```
BE-1 ─┬─> BE-2 (docs)
      └─> BE-3 (api-tester)
```

---

## Execution batches

> The two sub-pipelines run as parallel tracks. Within sub-pipeline A the batches are sequential (shared module/files); only the noted intra-A overlaps are safe.

### Track B — P7-11b (dispatch immediately, in parallel with Track A; Ai module only, no shared-file edits)

- **Batch B1** (backend-feature): P7-11b-BE-1 (StreamAsync capture) → then P7-11b-BE-2 (doc caveat removal). Sequential (BE-2 depends on BE-1); both are in the same agent run.
- **Gate B-a** (api-tester): P7-11b-BE-3.
- **Gate B-b** (security-auditor, **recommended/optional**): light audit of the AI gateway path — reuses the audited recorder + unchanged entity, so the reviewer may rule the prior P7-11 audit sufficient. Run it if the reviewer wants a re-audit.
- **Gate B-c** (reviewer): gate Track B against P7-11b acceptance criteria + CONVENTIONS.md.

### Track A — P5-03

- **Batch A1 — Scaffold (SEQUENTIAL; holds shared-file lock):**
  - **A1a** (db-migration): P5-03-BE-0 scaffold — create the 4 projects, `AnalyticsDbContext` (+ audit `SaveChangesAsync(int)`, `ConfigureWarnings(PendingModelChangesWarning)`, `HasDefaultSchema("analytics")`), `AnalyticsDbContextFactory`. db-migration owns the DbContext creation.
  - **A1b** (backend-feature): finish BE-0 wiring — `AddAnalyticsModule` DI, Host `Program.cs` registration, **add the Analytics assemblies to `MediatRExtensions.AddCrossModuleMediatR`** (miss this → consumers never fire), `Directory.Packages.props`, sln entries.
  - *A1a → A1b are sequential; they (and only they in this wave) edit the serialized shared files. While A1 runs, no other story may touch sln / Host Program.cs / AddCrossModuleMediatR / Directory.Packages.props. Track B does not touch these → safe to run concurrently.*

- **Batch A2 — Entity + migration (SEQUENTIAL, after A1):**
  - (db-migration): P5-03-BE-1 — `ActivityEvent` entity + EF config + migration (append-only, indexes, unique `SourceEventId`).

- **Batch A3 — Capture + derivation (after A2; PARALLEL within `backend-feature` where independent):**
  - **A3a** (backend-feature): P5-03-BE-2 (confirm consumed events) → P5-03-BE-3 (consumers + `IActivityEventStore`). Needs BE-1 + BE-2.
  - **A3b** (backend-feature): P5-03-BE-4 (`IActivitySessionService` derivation). Needs BE-1 only.
  - *A3a and A3b touch disjoint files (consumers/store vs session service) and can run in parallel; both depend only on A2.*

- **Batch A4 — Read seams (after A3b):**
  - (backend-feature): P5-03-BE-5 — seams in `Shared.Contracts/Analytics` + `Analytics.Infrastructure` adapters, Scoped registration. Depends on BE-4.

- **Batch A5 — P7-10 façade light-up (after A4; SERIALIZE the Identity.Application edit):**
  - (backend-feature): P5-03-BE-6 — wire the three seams into `GetPlatformKpisQueryHandler` + flip the N/A markers on `PlatformKpiSummaryDto`. **Serialize this Identity.Application edit** against any other story touching the façade. (Track B's BE-2 only edits an XML doc comment on `PlatformKpiSummaryDto.AiRequestVolume` — coordinate so the two don't collide; both are doc-level and small, but land them in distinct hunks.)

- **Gate A-a — security-auditor (MANDATORY, after A5):** student activity data + child-privacy + PII-light. Critical/High findings block the reviewer gate. Verify: no names/emails/prompt/response/answer text ever persisted; `StudentId` is a plain int; AdminOnly on any exposed Analytics endpoint; window queries `AsNoTracking`.

- **Gate A-b — api-tester (after A5 / alongside A-a):** P5-03-BE-7 runtime tests — publish-event-→-row (proves MediatR registration landed), idempotency (redelivery → no double-count), fail-soft (consumer throw doesn't break publisher), DAU/retention/session-duration aggregation correctness, window filter, empty-window 200/zeroed, AdminOnly.

- **Gate A-c — reviewer:** gate Track A against the brief's P5-03 acceptance criteria + CONVENTIONS.md (incl. security-auditor + api-tester results). Module-isolation #1, no-UoW #3 (append-only direct SaveChanges), Option C (EF only in Infrastructure), `BaseResponse`/`Successed`, `ILoggerManager`.

### Final

- **committer** (after BOTH reviewer gates PASS): commit on `feat/P5-03-analytics-backbone` (already checked out), open the PR(s) — see PR recommendation below.

---

## Review gates

| Gate | Agent | Scope | Blocking? |
|---|---|---|---|
| A-a | security-auditor | P5-03 — student activity data, child-privacy, PII-light | **Yes (mandatory)** — Critical/High block |
| A-b | api-tester | P5-03 — ingest idempotency, fail-soft, aggregation, window, AdminOnly | Yes |
| A-c | reviewer | P5-03 — full acceptance criteria + CONVENTIONS | Yes |
| B-a | api-tester | P7-11b — streamed row, cancel behavior, totals include stream | Yes |
| B-b | security-auditor | P7-11b — AI gateway path (recommended; reviewer may waive to prior P7-11 audit) | Optional |
| B-c | reviewer | P7-11b — acceptance criteria + CONVENTIONS | Yes |

---

## Blockers / prerequisites

- **No external blockers.** The DAU question (brief OQ-2) is **RESOLVED** = activity-based; no Identity sign-in producer, no Identity change. All consumed producer events already exist and are wired on the backbone.
- **Hard prerequisite (process):** Batch A1 (scaffold) **must complete and hold the shared-file lock alone**. No other story may edit `Learnexia.Modular.sln`, Host `Program.cs`, `MediatRExtensions.AddCrossModuleMediatR`, or `Directory.Packages.props` while A1 runs. Track B is exempt (touches none of these).
- **Highest-risk failure mode — silent no-op MediatR registration:** if the Analytics Application/Infrastructure handler assemblies are not added to `AddCrossModuleMediatR`, consumers never fire and the sink stays silently empty with no error. **Mitigation:** A1b explicitly adds the registration line; api-tester (A-b) publishes an event and asserts a row appears (this is the canary).
- **Intra-wave coordination:** BE-6 (Identity.Application façade handler) and P7-11b-BE-2 (XML doc on `PlatformKpiSummaryDto.AiRequestVolume`) both touch `Identity.Application`. Both are small/disjoint hunks — land them so they don't conflict. If running in the same checkout, sequence BE-6 and P7-11b-BE-2 rather than truly concurrent edits to that file.

### Carried open questions (minor, non-blocking — confirm during implementation, don't guess)

- **OQ-1 (EventType set):** v1 set is the existing producers only — `LessonCompleted`, `MissionCompleted`, `StudentLeveledUp`, `AiHelpRequested`/`AiHelpDelivered`/`AiHintUsed`. **No `SignIn`** (no sign-in producer). **No `AttemptCompleted`** (the contract-only `AnswerSubmittedIntegrationEvent` is not published → not wired in v1). Final string set confirmed by backend-feature in BE-2 against the actual contracts.
- **OQ-3 (subject engagement source):** keep event payloads unchanged; `SubjectCode` stays null in v1 and subject breakdown comes from the existing `IPlatformLearningStatsQuery.BySubject` seam (Learning owns subject resolution). No event-contract change.
- **OQ-4 (session duration source):** v1 session duration = read-time gap-split wall-clock; per-attempt `Attempt.DurationSeconds` is out of scope (no duration-bearing event added).
- **OQ-5 (P7-11b cancelled stream):** record **only on clean stream completion** (mirror `CompleteAsync`'s success-only rule); caller-cancel returns without recording.
- **OQ-6 (fake provider stream usage):** api-tester confirms in B-a; if no usage chunk, assert row-present-with-available-fields (don't fabricate token counts).

---

## Definition of done

### Per batch
- **A1 (scaffold):** solution builds; `Analytics` module present (4 projects, schema `analytics`), `AnalyticsDbContext` + factory mirror `Moderation`/`Ai`, `AddAnalyticsModule` wired in Host, **Analytics assemblies registered in `AddCrossModuleMediatR`**.
- **A2 (entity + migration):** `ActivityEvent` entity + config; migration generates a clean `analytics` schema table with the three indexes (incl. unique `SourceEventId`); `__EFMigrationsHistory` in `analytics`.
- **A3 (capture + derivation):** one fail-soft idempotent consumer per consumed event appending via `IActivityEventStore`; redelivery of the same `SourceEventId` is a no-op; `IActivitySessionService` returns DAU/WAU/MAU, session count/duration (gap-split, default 30), retention (distinct active days) over a window, sentinel-safe.
- **A4 (seams):** the three `Shared.Contracts/Analytics` seams implemented + Scoped-registered, windowed, sentinel-safe (empty window → zeroed, never null/throw); mirror `IPlatformLearningStatsQuery` shape.
- **A5 (façade):** P7-10 `GetPlatformKpisQuery` returns real `Retention`/`SessionDuration` (N/A markers gone) and true activity-based `DistinctActiveStudents`; no P7-10 contract shape regression.
- **B1 (StreamAsync capture):** a clean streamed call records exactly one `ai.AiUsageLogs` row via `IAiUsageRecorder` through `EnrichWithCost`; cancel/error records nothing; no SSE blocking; doc caveats removed.

### Overall (tied to story acceptance criteria)
- **P5-03:** new `Analytics` module live + MediatR-wired; append-only PII-light `ActivityEvent` sink; idempotent + fail-soft capture of the existing event backbone; activity-based DAU/WAU/MAU, read-time sessions/duration, distinct-active-days retention; three platform read seams; P7-10 deferred facets lit up with no contract change; NFR-1 (no producer-path writes, async fail-soft consumers); security-auditor PASS (child-privacy/PII-light); api-tester PASS (idempotency, fail-soft, aggregation, window, AdminOnly); reviewer PASS.
- **P7-11b:** streamed tutor calls now persist one `AiUsageLog` row via the reused recorder, fail-soft, no SSE latency; `/AiSafety/usage` + P7-10 `aiRequestVolume` include streamed calls; "non-streaming only" caveat removed in docs; `AiUsageLog` shape unchanged (PII-light); api-tester PASS; reviewer PASS.

---

## PR recommendation

**Split P7-11b into its own PR.** Rationale:
- The two stories are **independent** (no code dependency), live in **different modules** (`Analytics` vs `Ai`), and gate separately. P7-11b is small (≈8h, 3 tasks) and self-contained; it does not need to wait on the larger P5-03 module build.
- Splitting keeps each PR scoped and reviewable, lets P7-11b merge as soon as its reviewer gate passes (it has no scaffold/shared-file risk), and isolates the P5-03 shared-file changes (sln/Program.cs/MediatR/Directory.Packages.props) in one PR for a clean cross-branch merge.
- Suggested: branch `feat/P5-03-analytics-backbone` for P5-03 (already checked out) and a sibling `feat/P7-11b-streaming-ai-usage` for P7-11b (or a stacked PR off the wave branch). If the lead prefers **one PR for the wave** (both pieces are small, single-author, same cycle), that is acceptable — but the committer should still keep P7-11b changes in their own commit(s) so they read cleanly and could be reverted independently.

---

Plan ready — dispatch Batch 1 (Track A: A1a db-migration scaffold; in parallel Track B: B1 backend-feature P7-11b-BE-1).
