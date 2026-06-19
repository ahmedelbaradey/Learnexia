# Pipeline Brief — P5-03 Analytics event-capture backbone (+ P7-11b streaming AI-usage capture)

> Wave brief. Two independent backend stories in the same wave:
> - **P5-03** — Capture product analytics events → NEW `Analytics` module (lead-approved 2026-06-19).
> - **P7-11b** — Capture AI tutor usage for streamed (SSE) responses → existing `Ai` module.
>
> They share only the wave and the NFR-1 fail-soft theme; **no code dependency** between them. They can run as two parallel sub-pipelines (P7-11b is small and self-contained).

---

## Summary & traceability

| Item | P5-03 | P7-11b |
|---|---|---|
| One-line task | Stand up an append-only activity-event sink (new `Analytics` module) that consumes existing `Shared.Contracts` integration events, derives read-time sessions/retention/DAU, exposes platform read seams, and lights up the P7-10 deferred KPI facets. | Close the `AiGateway.StreamAsync` capture gap so streamed (SSE) tutor calls also persist one `ai.AiUsageLogs` row via the existing fire-and-forget recorder. |
| User story | `user-stories/Phase-5-Parent-Analytics/P5-03-analytics-event-capture.md` | `user-stories/Phase-7-Admin-Console/P7-11b-streaming-ai-usage-capture.md` |
| Task file | `tasks/Backend/Phase-5-Parent-Analytics/P5-03-BE.md` | `tasks/Backend/Phase-7-Admin-Console/P7-11b-BE.md` |
| FR-IDs | FR-PA-3 | FR-ADM-10, FR-AI-4 |
| BRD goals | G1 (learning outcomes), G5 (data-driven product) | — (admin observability of AI cost) |
| NFR | NFR-1 (no measurable latency degradation) | NFR-1 |
| Epic / Phase | Parent & Analytics · Phase 5 (Week 8) | Admin — Analytics & AI Oversight · Phase 7 follow-up |
| Story points | 3 | 2 |

**Product-decision context (override BRD/SRS where conflicting):** 4 subjects (Math/Science/Arabic/English), no teacher role, parent-driven onboarding. None of these conflict with this wave; they only bound the subject-code facet (4 subjects, bilingual) and the actor set (students + admin).

---

## Business context & value

**P5-03 — who benefits:** the product owner / admin (Phase 6 P6-05 and Phase 7 P7-10 KPI dashboards) and, indirectly, every student (the data feeds retention/engagement decisions). The platform currently has **no first-class activity-event store**: the P7-10 façade fakes DAU as an "activity proxy" (distinct students with a completed attempt in the window) and explicitly returns `RetentionNaReason` / `SessionDurationNaReason` = "Available after P5-03". This story makes those KPIs real:
- **DAU/WAU/MAU** — true distinct-active-students over a window, anchored on a real sign-in signal (not a completion proxy).
- **Session duration & sessions/user** — derived read-time from the event stream with an inactivity-window gap-split (default 30 min); no explicit sign-out, no Session table in v1.
- **Retention** — distinct active UTC days per student, the foundation for D1/D7/D30 cohort curves later.
- **Subject engagement** — completions/attempts faceted by subject code.

**P7-11b — who benefits:** the admin. The P7-11 tutor-cost slice and the P7-10 `aiRequestVolume` KPI today count **only** non-streaming `CompleteAsync` calls. Hint / Explain / SimilarExample run through `StreamAsync` (SSE) and are silently uncounted — a documented v1 gap. Closing it makes the AI usage/cost dashboard reflect **all** tutor calls, so cost governance and the Phase 10 energy-economy modelling are not blind to streamed traffic.

Success is measured by: the KPI facets stop returning N/A and match independently-counted ground truth; redelivery never double-counts; a consumer/recorder failure never breaks (or measurably slows) the producing path.

---

## Acceptance criteria (testable)

### P5-03
- [ ] A new `Analytics` module exists (Domain/Application/Infrastructure/Api), schema `analytics`, wired into the Host and into **cross-module MediatR registration** (consumers fire). DI entrypoint `AddAnalyticsModule`.
- [ ] An append-only `ActivityEvent` entity persists: `StudentId` (int, no FK), `EventType` (stable string), optional `SubjectCode` (int?), optional `DurationSeconds` (int?), `OccurredAtUtc` (timestamptz), and a unique `SourceEventId` (uuid) dedup key. Indexes on `OccurredAtUtc` and composite `(StudentId, OccurredAtUtc)`. **PII-light**: no names/emails/prompt/response/answer text ever.
- [ ] Capture consumers (`INotificationHandler<T>`) exist for each chosen producer event, map to an `ActivityEvent`, and append via `IActivityEventStore`. Ingest is **idempotent** (redelivery of the same `SourceEventId` → no second row) and **fail-soft** (a consumer throw is caught/logged and never propagates to the publisher / producing request).
- [ ] DAU/WAU/MAU is computed from **distinct active students** over a window (anchored on the sign-in signal — see handoff), not the attempt-completion proxy.
- [ ] Session count + session duration are derived read-time by gap-splitting one student's event stream at gaps ≥ `SessionGapMinutes` (config, default 30).
- [ ] Retention is computed as distinct active UTC days per student over the window (returning-user foundation).
- [ ] Platform read seams in `Shared.Contracts/Analytics` (`IPlatformActivityQuery`, `IPlatformSessionStatsQuery`, `IPlatformRetentionQuery`) are implemented in `Analytics.Infrastructure` (registered Scoped), `AsNoTracking`, date-windowed, sentinel-safe (empty window → zeroed, never null/throw). Mirror the `IPlatformLearningStatsQuery` + adapter shape.
- [ ] The P7-10 façade (`GetPlatformKpisQuery` in `Identity.Application`) consumes the new seams: `RetentionNaReason` / `SessionDurationNaReason` are replaced with real values, and `DistinctActiveStudents` is upgraded to true session-based DAU (keep the proxy labelled until cutover). No P7-10 API contract shape regression.
- [ ] No measurable latency degradation on any producing path (NFR-1): consumers are async, append-only, no writes on the producer's hot path.
- [ ] AdminOnly on any exposed Analytics endpoint; security-auditor passes child-privacy / PII-light.

### P7-11b
- [ ] A successful `StreamAsync` call captures exactly one `ai.AiUsageLogs` row via the existing fire-and-forget `IAiUsageRecorder`, with token counts from the provider's end-of-stream usage (when supplied) and cost enriched through the **same** `EnrichWithCost` path as `CompleteAsync`.
- [ ] Capture is fail-soft and does not block, fail, or measurably slow the SSE response (NFR-1) — same guarantees as the non-streaming path. A caller-cancelled/errored stream behaves per the BE-1 decision (no record, or record-what-is-known) and never throws to the caller.
- [ ] `GET /api/Admin/AiSafety/usage` totals and the P7-10 `aiRequestVolume` KPI now include streamed calls; the "non-streaming completions only" caveat is removed from the docs/contract (XML docs on `AiGateway`, `IPlatformAiSafetyStatsQuery.AiRequestVolume`, `PlatformKpiSummaryDto.AiRequestVolume`, P7-11 `TutorUsageDto`). No contract shape change.
- [ ] PII-light unchanged (`AiUsageLog` shape unchanged; `StudentId` stays nullable/unpopulated unless a provider-neutral path supplies it).
- [ ] If the fake/test provider does not stream usage, the test asserts "row present with available fields" rather than exact token counts (noted, not fabricated).

---

## Affected modules & data

| Module | P5-03 | P7-11b |
|---|---|---|
| **Analytics** (NEW) | Whole module: `ActivityEvent` entity, `AnalyticsDbContext` (schema `analytics`), `IActivityEventStore`, `IActivitySessionService`, capture consumers, seam adapters. | — |
| **Shared.Contracts** | NEW `Analytics/` seam interfaces (`IPlatformActivityQuery`, `IPlatformSessionStatsQuery`, `IPlatformRetentionQuery`) + their result records. **NEW `Identity/UserSignedInIntegrationEvent`** (the DAU signal — see handoff). | XML-doc edit only (`Ai/AiUsage`-area docs; `IPlatformAiSafetyStatsQuery.AiRequestVolume`). |
| **Identity** | NEW fail-soft producer for `UserSignedInIntegrationEvent` in `SignInCommandHandler` + `GoogleSignInCommandHandler`. Façade edit: `GetPlatformKpisQueryHandler` / `PlatformKpiSummaryDto` consume the new Analytics seams. | XML-doc edit on `PlatformKpiSummaryDto.AiRequestVolume`. |
| **Ai** | — | `AiGateway.StreamAsync` accumulates end-of-stream usage and calls the existing `_usageRecorder.Record(...)` post-stream. XML-doc updates. No new entity (`AiUsageLog` unchanged). |

**Shared-file serialization (PARALLELISM.md):** the P5-03 module scaffold edits `Learnexia.Modular.sln`, Host `Program.cs`, `MediatRExtensions.AddCrossModuleMediatR` (cross-module registration), and `Directory.Packages.props`. Serialize these against any other concurrently-running story. P7-11b touches none of these shared files and can run alongside.

---

## Handoff → db-migration (P5-03 only; P7-11b adds no schema)

New schema `analytics`, one table.

**`ActivityEvent`** (append-only; mirror `AiUsageLog` / `SafetyEvent` config style):
- `Id` — PK (identity).
- `StudentId` — `int`, **no FK** (cross-module isolation; plain int like every other contract id).
- `EventType` — `string` (stable, indexed-friendly). Confirmed candidate value set (see Open Questions OQ-1 for the exact final set): `SignIn`, `LessonCompleted`, `AttemptCompleted`, `MissionCompleted`, `LeveledUp`, `HelpDelivered`. Store as a string column; treat values as a stable enum-by-convention.
- `SubjectCode` — `int?` (nullable; only populated for events that carry a subject facet — see OQ-3, most current producers do NOT carry it).
- `DurationSeconds` — `int?` (nullable; populated for attempt/duration-bearing events).
- `OccurredAtUtc` — `timestamptz`, **indexed** (drives every window query).
- `SourceEventId` — `uuid`, **unique index** (idempotent-ingest dedup key; set from the integration event's `EventId`).
- Audit columns via `AnalyticsDbContext.SaveChangesAsync(int userId)` stamping (mirror `ModerationDbContext` — pass `userId: 0` for system telemetry rows, exactly as `AiUsageLogStore` does).

Indexes: `OccurredAtUtc`; composite `(StudentId, OccurredAtUtc)`; unique `(SourceEventId)`.

`AnalyticsDbContext`: `HasDefaultSchema("analytics")`, `ApplyConfigurationsFromAssembly`, `ConfigureWarnings(...PendingModelChangesWarning)`, audit-stamping `SaveChangesAsync(int)` override — copy `ModerationDbContext` verbatim. Migrations history table `__EFMigrationsHistory` in the `analytics` schema; `MigrationsAssembly` = Analytics.Infrastructure. Add a `AnalyticsDbContextFactory` for design-time (mirror `ModerationDbContextFactory`).

---

## Handoff → backend-feature

### P5-03 — producer-event inventory (the headline) — see the dedicated section below for the full table.

**Stores / services to build (Option C — EF only in Infrastructure):**
- `IActivityEventStore` (Application abstraction) + `ActivityEventStore` (Infrastructure) — append-only `AddAsync(ActivityEvent)` with try/catch fail-soft. Direct `SaveChangesAsync(userId: 0)`, no UoW (ADR-0001 append-only). **Mirror `AiUsageLogStore` exactly.**
- Capture consumers — one `INotificationHandler<T>` per consumed integration event, thin: build the `ActivityEvent` (set `SourceEventId = notification.EventId`) and delegate to the store. Idempotency is enforced by the unique `SourceEventId` index — the store must swallow the unique-violation as a no-op (mirror `ModerationQueueWriter.EnqueueIfNotExistsAsync` idempotency intent). Fail-soft is layered: the Host's `IsolatedNotificationPublisher` already isolates per-handler failures; the store's inner try/catch is the explicit guarantee. **Mirror `AiOutputFlaggedEventHandler` for handler shape.**
- `IActivitySessionService` (Infrastructure) — read-time derivation over a window: distinct-active-students (DAU/WAU/MAU), session count + session duration (gap-split on `SessionGapMinutes`, config default 30), and distinct-active-days retention. `AsNoTracking`, date-windowed. This is a query/derivation service (no write); per CONVENTIONS §7 it may orchestrate.
- Config: `SessionGapMinutes` (default 30) via options bound in `AddAnalyticsModule`.

**Read seams (new, in `Shared.Contracts/Analytics`)** — implemented in `Analytics.Infrastructure`, registered Scoped, **mirror the `IPlatformLearningStatsQuery` + `PlatformLearningStatsQueryAdapter` shape** (windowed `GetPlatformAsync(fromUtc, toUtc, ct)` returning a sentinel-safe result record):
- `IPlatformActivityQuery` → DAU/WAU/MAU over a window.
- `IPlatformSessionStatsQuery` → avg/median session duration, sessions/user.
- `IPlatformRetentionQuery` → active-days, returning-user rate.

**Façade wiring (P7-10 light-up)** — in `Identity.Application` `GetPlatformKpisQueryHandler` (the fan-out façade): inject the three new seams alongside the existing four, run them in the same parallel `Task.WhenAll` fan-out, and assemble: replace `RetentionNaReason` and `SessionDurationNaReason` with real values on `PlatformKpiSummaryDto`, and upgrade `DistinctActiveStudents` to true session DAU (keep the proxy value labelled until cutover). This is the **P7-10 façade wiring point**: `backend/src/Modules/Identity/Learnexia.Modules.Identity.Application/Features/Analytics/Queries/GetPlatformKpis/GetPlatformKpisQueryHandler.cs` (handler) + `.../Analytics/Dtos/PlatformKpiSummaryDto.cs` (DTO N/A markers, lines ~99–114). Endpoint `GET /api/Admin/Analytics/kpis` (`AdminAnalyticsController`) — no signature change.

**Sign-in producer (the DAU signal):** add a NEW `UserSignedInIntegrationEvent(EventId, OccurredOnUtc, UserId)` in `Shared.Contracts/Identity` (PII-light — UserId only, no email/name; follow the `UserRegisteredIntegrationEvent` precedent where email was deliberately stripped). Publish it **fail-soft, post-success** in both `SignInCommandHandler.Handle` (after `Success(accessToken)` is determined) and `GoogleSignInCommandHandler.Handle` (after the JWT is issued) — wrap the `_publisher.Publish(...)` in try/catch/log exactly like `PublishUserRegisteredEventAsync`. Note `SignInCommandHandler` currently has **no** `IPublisher` injected — it must be added. The Analytics `SignIn` consumer maps it to an `ActivityEvent { EventType = "SignIn" }` (no subject, no duration). See OQ-2 for whether `UserId` here is the student id or parent id.

### P7-11b
- `AiGateway.StreamAsync` (`backend/src/Modules/Ai/Learnexia.Modules.Ai.Infrastructure/Gateway/AiGateway.cs`, lines 148–173): accumulate the provider's end-of-stream `AiUsage` (token counts from the final/usage chunk where the provider supplies them), run it through the **existing** `EnrichWithCost`, then call `_usageRecorder.Record(usage, request.Task)` after the stream completes successfully. Guard partial/aborted streams (the method is an `IAsyncEnumerable` iterator — capture usage from the last chunk, record in a `finally`/post-loop block only on clean completion per BE-1 decision). **Do not add a new write pattern** — reuse the audited `IAiUsageRecorder` (Singleton, own DI scope, fail-soft).
- Confirm during implementation whether the configured fake/test provider's `StreamAsync` emits a usage chunk; if not, BE-3 asserts row-present-with-available-fields (do not fabricate token counts).
- XML-doc caveat removal on `AiGateway` (lines 25–28), `AiUsageRecorder` (lines 20–23), `IPlatformAiSafetyStatsQuery.AiRequestVolume`, `PlatformKpiSummaryDto.AiRequestVolume` (lines 88–91), P7-11 `TutorUsageDto`.

---

## Handoff → frontend

**None for this wave.** Both stories are backend-only. The KPI dashboards that consume this data are P6-05 (parent/admin) and P7-10 (admin), already built/owned separately. No new screens, no API shape change (P7-10 contract is preserved; only N/A markers flip to real values).

---

## Headline producer-event inventory (which `Shared.Contracts` integration events exist per KPI)

All existing events implement `IIntegrationEvent` (`EventId`, `OccurredOnUtc`) and are MediatR `INotification`s, fanned out cross-module via the Host's `IsolatedNotificationPublisher` (`AddCrossModuleMediatR`). The Analytics consumer assemblies must be **added to `MediatRExtensions.AddCrossModuleMediatR`** or no consumer fires (the same trap called out for Moderation in that file).

| KPI (story AC) | Producer integration event | Status | Notes for the consumer |
|---|---|---|---|
| **DAU/WAU/MAU (true)** | `Identity/UserSignedInIntegrationEvent` | **MISSING — must be added** | No sign-in event exists today. Add PII-light `(EventId, OccurredOnUtc, UserId)`; publish fail-soft post-success in `SignInCommandHandler` **and** `GoogleSignInCommandHandler`. This is the load-bearing gap-fill of the wave. |
| Active-learner proxy (interim) | `Learning/LessonCompletedIntegrationEvent` | Exists, **wired** (published by `CompleteAttemptCommandHandler`) | Carries `StudentId, LessonId, SkillId, AccuracyPercentage, CorrectAnswerCount`. Drives the current P7-10 proxy. Analytics can also consume it as `LessonCompleted`. **No subject code, no duration** on the payload. |
| Mission completion | `Gamification/MissionCompletedIntegrationEvent` | Exists, **wired** (re-published by Gamification, consumed by Notifications today) | `StudentId, MissionCode, RewardXp`. Map to `MissionCompleted`. No subject/duration. |
| Attempt / quiz accuracy & duration | `Learning/AnswerSubmittedIntegrationEvent` | Exists, **contract-only / not published** | Defined P4-01 but **not yet published** by any producer (the comment says "defined now, wired later"). Carries no accuracy and no duration. **There is no `AttemptCompletedIntegrationEvent`** — `CompleteAttempt` only publishes `LessonCompleted`. See OQ-3/OQ-4: the per-session `DurationSeconds` (server-authoritative) lives on the `Attempt` entity but is **not** broadcast on any event. |
| Level-up engagement | `Gamification/StudentLeveledUpIntegrationEvent` | Exists, **wired** | `StudentId, OldLevel, NewLevel`. Optional `LeveledUp` event. Its own doc names "analytics P5-03" as the intended consumer. |
| AI help engagement | `AiTutor/HelpDeliveredIntegrationEvent` | Exists | `StudentId, Intent, SkillId, QuestionId?, ModelUsed, ContextSource`. Doc explicitly says "Consumed by P5-03 product analytics". Optional `HelpDelivered`. Carries `SkillId`, not `SubjectCode`. |
| AI help requested | `AiTutor/HelpRequestedIntegrationEvent` | Exists | Same family; doc names P5-03. Optional. |
| Hint used | `AiTutor/HintUsedIntegrationEvent` | Exists, **wired** | Optional engagement facet. |

**Bottom line for the lead:** every KPI the story enumerates has an existing producer **except the DAU sign-in signal**, which must be added (`UserSignedInIntegrationEvent` in Identity, two publish sites). Subject engagement and true session duration are **not** carried on any current event payload — see OQ-3/OQ-4 for how to source them.

---

## Open questions / assumptions / risks

**OQ-1 (EventType set) — RECOMMEND CONFIRM.** Proposed stable `EventType` values: `SignIn`, `LessonCompleted`, `MissionCompleted`, `LeveledUp`, `HelpDelivered`. The task mentions `AttemptCompleted` but there is **no** `AttemptCompletedIntegrationEvent` (CompleteAttempt publishes `LessonCompleted`). Assumption: drop `AttemptCompleted` from v1 unless the lead wants the contract-only `AnswerSubmittedIntegrationEvent` wired now. *Flag, don't guess.*

**OQ-2 (sign-in identity) — RECOMMEND CONFIRM (load-bearing for DAU correctness).** Onboarding is parent-driven; **students do not self-register or sign in** as separate Identity users in the current model — the parent authenticates and selects a child. So `SignInCommandHandler` authenticates a **parent**, not a student. What does "DAU" count — distinct active **students** (children) or distinct active **parent accounts**? If DAU must be per-student, the sign-in event alone is insufficient (it has no child context); we'd need a "child session start"/profile-switch signal, or DAU stays anchored on per-student learning activity (the `LessonCompleted` consumer) and the sign-in event measures parent logins separately. **This is the single most important product question in the wave — recommend the lead confirm before BE-2/BE-4.**

**OQ-3 (subject engagement source) — RECOMMEND CONFIRM.** "Subject engagement" needs a `SubjectCode`, but no current event payload carries one (`LessonCompleted` carries `SkillId`/`LessonId`; `HelpDelivered` carries `SkillId`). Options: (a) leave `SubjectCode` null in v1 and surface subject engagement from the existing `IPlatformLearningStatsQuery.BySubject` (Learning already resolves subject via Lesson→Unit→Subject); (b) extend `LessonCompletedIntegrationEvent` with a `SubjectCode` (contract change + producer edit). Assumption: prefer (a) — keep the event payloads unchanged and let Learning own subject breakdown; Analytics owns session/retention/DAU. Confirm scope.

**OQ-4 (session duration source).** Read-time gap-split derives session duration from event *timestamps* (count of sessions and their wall-clock span), not from `Attempt.DurationSeconds`. `DurationSeconds` on the event is optional and only meaningful if a duration-bearing event is added. Assumption: v1 session duration = gap-split wall-clock; per-attempt `DurationSeconds` is out of scope unless an `AttemptCompleted` event is added (ties to OQ-1). Confirm.

**OQ-5 (P7-11b cancelled/aborted stream).** BE-1 leaves "no record on caller-cancel/error, or record what's known" to the analyzer/implementer. Recommendation: **record only on clean stream completion** (mirror `CompleteAsync`, which records only on `result.Successed`); a caller-cancel returns without recording. Confirm this is acceptable (it means abandoned streamed reads cost AI tokens but aren't logged — consistent with the non-streaming success-only rule).

**OQ-6 (fake provider stream usage).** Whether the configured fake/test provider emits an end-of-stream usage chunk is unverified at brief time — the api-tester confirms during BE-3; if absent, assert row-present-with-available-fields (do not fabricate). Not blocking.

**Risk — silent no-op MediatR registration.** If the Analytics Application (and any Infrastructure-resident handlers) assemblies are not added to `AddCrossModuleMediatR`, consumers never fire and the sink stays silently empty with no error — the exact failure mode documented for Moderation/Learning.Infrastructure in `MediatRExtensions.cs`. db-migration/backend-feature must verify the registration line lands. **Mitigated by an integration test that publishes an event and asserts a row appears.**

**Risk — read-time derivation cost.** Sessions/retention are derived read-time over `ActivityEvent` with no rollups. Acceptable for v1 (admin-only, low-traffic dashboards over an indexed window). Documented follow-up: materialize daily session/active-day rollups if event volume makes it slow.

**Risk — ghost-event-on-rollback.** Producers publish fire-and-forget post-commit (or in the same handler as the write, per ADR-0002, accepted outbox-deferred risk). A producing-path rollback after publish could append a ghost `ActivityEvent`. Accepted per ADR-0002; outbox is a future hardening story. PII-light + append-only contains the blast radius.

---

## Recommended pipeline order (first cut — the `planner` finalizes)

**Two parallel sub-pipelines.**

**Sub-pipeline B (P7-11b — small, run in parallel with A):**
1. `backend-feature` — BE-1 (StreamAsync capture) + BE-2 (doc caveat removal).
2. `api-tester` — BE-3 (streamed call → row; cancelled stream behavior; totals include streamed row).
3. `security-auditor` — light/optional (reviewer decides; reuses audited recorder + entity).
4. `reviewer` gate.

**Sub-pipeline A (P5-03) — gate OQ-1/OQ-2/OQ-3 with the lead first** (OQ-2 especially shapes BE-2/BE-4):
1. **Serialized scaffold first:** `db-migration` (or backend-feature) does **P5-03-BE-0** (module scaffold + sln/Program.cs/MediatR registration/Directory.Packages.props — shared-file serialization) **then** **P5-03-BE-1** (`ActivityEvent` entity + config + migration). These are sequential and hold the shared-file lock.
2. **Parallel after scaffold:**
   - `backend-feature` BE-2 (sign-in producer + `UserSignedInIntegrationEvent` — touches Identity `Shared.Contracts`; independent of the consumers).
   - `backend-feature` BE-3 (capture consumers + store) — depends on BE-1 + BE-2's event existing.
3. `backend-feature` BE-4 (session/retention/DAU derivation) — depends on BE-1.
4. `backend-feature` BE-5 (read seams in `Shared.Contracts/Analytics` + adapters) — depends on BE-4.
5. `backend-feature` BE-6 (P7-10 façade light-up) — depends on BE-5; **serialize the Identity.Application edit** if any other story touches it.
6. **`security-auditor`** — mandatory (student activity data, child-privacy, PII-light).
7. `api-tester` — BE-7 (idempotency, fail-soft, aggregation correctness, window filter, empty-window, AdminOnly, PII-light).
8. `reviewer` gate against this brief's acceptance criteria + CONVENTIONS.md.

No `designer` / `frontend` / `frontend-e2e-tester` stages — backend-only wave.
