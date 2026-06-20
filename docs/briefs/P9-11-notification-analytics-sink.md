# Pipeline Brief — P9-11 Notification analytics sink

## Summary & traceability
- **Task (one line):** Capture every notification lifecycle transition — **Dispatched** (with per-channel result), **Suppressed** (with the P9-07 reason), **Opened** (inbox read) — into the Analytics module's append-only `ActivityEvent` stream, and expose an admin-readable send/suppress/open aggregate by code & category over a date range.
- **User story:** `user-stories/Phase-9-Notifications/P9-11-notification-analytics-sink.md` (SP 3).
- **Tasks:** `tasks/Backend/Phase-9-Notifications/P9-11-BE.md` (BE-1…BE-6). No FE in this build except the documented admin-dashboard contract (`P9-11-FE`).
- **FR-IDs / spec:** FR-GM-8; builds on **P5-03** (Analytics backbone / `ActivityEvent`); supersedes the "v1 logs only" gap recorded in **P9-07**; feeds **P7-10 / P7-11** admin dashboards.
- **BRD goal:** G3 (retention / habit-loop) — measure which nudges land vs. get throttled so the catalog can be tuned with data.
- **Epic / phase:** Notifications Module · Phase 9 (post-MVP).
- **Lead decision (2026-06-20):** Sink home = **Analytics module**. Notifications emits `Shared.Contracts` lifecycle events → Analytics consumes into `ActivityEvent`. One analytics home; no second metrics table in the `notifications` schema.

## Business context & value
- **Who benefits:** product/admin team (analytics consumers); indirectly students (a tuned, less-spammy nudge catalog).
- **Value:** P9-05..08 shipped a rich nudge catalog gated by P9-07 arbitration + a global push budget, but effectiveness is **unmeasured** — only logs exist. This makes send/suppress/open rates per code & category first-class data so the team can distinguish a winning nudge from a muted one.
- **Success measure:** admin endpoint returns non-zero, correctly-bucketed dispatched/suppressed/opened counts + open-rate per code & category over a window; suppression-reason buckets reflect the real P9-07 reasons. Capture never breaks a dispatch.

## Acceptance criteria (testable)
1. **Dispatched signal** — after a successful `NudgeDispatcher.DispatchAsync`, exactly one `NotificationDispatchedIntegrationEvent` is published carrying `StudentId`, `Code`, `Category`, and the **`DeliveredChannels` bitmask** (which of inApp=4 / push=2 actually went out). No PII.
2. **Suppressed signal** — when the P9-07 arbiter denies the push (`result.ShouldPush == false`), exactly one `NotificationSuppressedIntegrationEvent` is published carrying `StudentId`, `Code`, `Category`, and the **reason string** (one of: `DisabledByParent`, `DailyCapReached`, `QuietHours`, `GlobalBudgetExhausted`, `PriorityLost`, `Cooldown`). No PII.
3. **Opened signal** — when `InboxController` MarkRead succeeds for the owning recipient, exactly one `NotificationOpenedIntegrationEvent` is published carrying `StudentId`, `Code`, `Category`. No PII. (**Push-tap Opened is DEFERRED to P9-02 FE** — only the inbox-read path is wired now; see Open questions.)
4. **Fail-soft / off hot path** — emission failure (no handler, MediatR throw, serialization) NEVER blocks or aborts a dispatch or a mark-read; the inbox row, push send, and HTTP response are unaffected.
5. **Analytics is the single sink** — the Analytics module consumes all 3 events into the existing append-only `ActivityEvent` stream via `Shared.Contracts` only. No cross-module FK; Notifications never references Analytics.Domain and vice-versa.
6. **Idempotent ingest** — redelivery of the same lifecycle event produces exactly one `ActivityEvent` row (existing `SourceEventId` unique-index guard).
7. **Admin aggregate** — `GET /api/Admin/Analytics/notifications?from=&to=&category=` returns `BaseResponse<NotificationAnalyticsDto>` with dispatched/suppressed/opened counts + open-rate **by code and by category** over `[from,to)`, plus suppression-reason buckets. AdminOnly: anonymous → 401, non-admin → 403. `Successed` flag + localized message (EN+AR).
8. **No new pattern** — consumers mirror the existing 6 Analytics `INotificationHandler<T>` shape; the admin read mirrors `GetPlatformKpisQuery` + `IPlatformAnalyticsQuery`; emit mirrors the existing `_publisher.Publish` fail-soft republisher shape.

## Affected modules & data
- **Notifications module (emit side)** — `NudgeDispatcher` (Infrastructure) gains a Dispatched + Suppressed emit; `MarkNotificationReadCommandHandler` (Application) gains an Opened emit. No new entities/columns in the `notifications` schema.
- **Analytics module (consume + read side)** — 3 new `INotificationHandler<T>` consumers (Application) + an extended read seam. **`ActivityEvent` entity gains facet columns** (see Handoff → db-migration). Schema `analytics`.
- **Identity module (admin façade)** — new admin query + endpoint, mirroring `AdminAnalyticsController` / `GetPlatformKpisQueryHandler`. The admin analytics dashboard lives in Identity (it is the cross-cutting admin read façade), not Analytics.
- **Shared.Contracts** — new `Notifications/` folder with 3 lifecycle integration events; an extension to the `Analytics/IPlatformAnalyticsQuery` seam (new method + DTO).
- **Shared.Resources** — 1 admin success message key (EN + AR), mirroring `PlatformKpisRetrievedSuccessfully`.

### New vs existing entities (cited)
- **Existing, reused:** `Analytics.Domain.Entities.ActivityEvent` (`backend/src/Modules/Analytics/.../Entities/ActivityEvent.cs`), `IActivityEventStore` + `ActivityEventStore` (append-only, `SaveChangesAsync(userId:0)`, fail-soft, `SourceEventId` idempotency), `IPlatformAnalyticsQuery` (`backend/src/Shared/Learnexia.Shared.Contracts/Analytics/IPlatformAnalyticsQuery.cs`), `NudgeDispatcher`, `MarkNotificationReadCommandHandler`, `Notification.DeliveredChannels`/`MarkRead`, `ReengagementEvaluator.NotEligibleReason`.
- **New:** 3 Shared.Contracts records; 3 Analytics consumers; facet columns on `ActivityEvent` (+ migration); 1 read-seam method + DTO + adapter impl; 1 Identity admin query/handler/endpoint + DTO; 1 resx key.

---

## Handoff → db-migration (REQUIRED — runs first)

**Decision (resolves point E): a migration IS needed.** `ActivityEvent` today has **no jsonb/metadata column** and only two unused nullable int facets (`SubjectCode`, `DurationSeconds`) — neither is queried anywhere (verified: `ActivitySessionService` selects only `StudentId` + `OccurredAtUtc`). The admin aggregate must group **by code (string)** and **by category**, and bucket **suppression reasons (string)**, which cannot ride the existing columns. Add nullable facet columns to `ActivityEvent` — this matches the entity's own documented "reserved for future per-event tagging without a schema change is NOT possible for new dimensions" reality and keeps one sink.

Add to `ActivityEvent` (all **nullable** so all existing producers keep writing `null`; mirror `AiUsageLog.TaskKind` string-facet style):
| Column | Type | Purpose |
|---|---|---|
| `NotificationCode` | `string?` (maxlen 64) | template code, e.g. `BADGE_EARNED`, `STREAK_BROKEN`. Group-by + filter. |
| `NotificationCategory` | `int?` | the `NotificationCategory` enum value (0..9). Group-by + filter. Stored as the int value, interpreted by the admin layer — Analytics must NOT reference Notifications.Domain. |
| `NotificationChannels` | `int?` | the `DeliveredChannels` bitmask (inApp=4, push=2) for `NotificationDispatched`; null for suppressed/opened. |
| `SuppressionReason` | `string?` (maxlen 32) | the P9-07 reason string for `NotificationSuppressed`; null otherwise. |

Indexes (so the append-only table answers the range/group-by efficiently — flagged per point E):
- Composite **`(EventType, OccurredAtUtc)`** — the admin query filters on the 3 notification `EventType` discriminators over a date range. (`EventType` already has a standalone index; add the composite for the date-range scan.)
- Optionally **`(EventType, NotificationCategory)`** if category filtering proves hot — start without it; add only if the api-tester / reviewer flags slow group-by. Document the decision.
- No FK, append-only, no UoW (ADR-0001) — unchanged.

Update `ActivityEventConfig` + `AnalyticsDbContextModelSnapshot` via `dotnet ef migrations add` against `AnalyticsDbContext`. Migration name suggestion: `P9_11_AddNotificationFacetsToActivityEvent`.

> **Alternative considered + rejected:** encoding facets into the existing `SubjectCode`/`DurationSeconds` ints or stuffing code+reason into the 64-char `EventType` string. Rejected — it would make the admin group-by brittle (string parsing) and overload columns with unrelated semantics. Nullable typed columns are the honest, query-efficient choice and the reviewer can verify them directly.

---

## Handoff → backend-feature

### A. The 3 lifecycle integration events (`Shared.Contracts/Notifications/` — NEW folder) — RESOLVED
All three are `sealed record`, implement `IIntegrationEvent` (so `Guid EventId` + `DateTime OccurredOnUtc` lead the param list), carry **opaque ids + scalars only, NO PII**. **Category and Reason travel as the precedent-set primitive shapes** so neither module references the other's Domain — exactly as `BadgeEarnedIntegrationEvent.Rarity` carries a domain enum as a plain string for module-isolation stability.

- **`Category` → `int`** (the `NotificationCategory` enum's int value). Notifications casts `(int)message.Category`; Analytics stores the int as-is; the admin layer maps int→label. Rationale: the enum already has stable explicit int values and `ActivityEvent.NotificationCategory` is `int?`. (A shared enum in Shared.Contracts is the alternative; int avoids adding a contract type and matches the entity column.)
- **`Reason` → `string`** (the `ReengagementEvaluator.NotEligibleReason` name, e.g. `"GlobalBudgetExhausted"`). Notifications maps `result.SuppressReason.ToString()`; Analytics stores the string; admin buckets by string. Rationale: the reason enum is a Notifications.Domain type and must not cross the boundary; string is the isolation-safe carrier (same call already logged today: `reason={result.SuppressReason}`).
- **`Code` → `string`**, **`StudentId` → `int`**, **`DeliveredChannels` → `int`** (bitmask).

```
// Shared.Contracts/Notifications/NotificationDispatchedIntegrationEvent.cs
public sealed record NotificationDispatchedIntegrationEvent(
    Guid EventId, DateTime OccurredOnUtc,
    int StudentId, string Code, int Category, int DeliveredChannels) : IIntegrationEvent;

// NotificationSuppressedIntegrationEvent.cs
public sealed record NotificationSuppressedIntegrationEvent(
    Guid EventId, DateTime OccurredOnUtc,
    int StudentId, string Code, int Category, string Reason) : IIntegrationEvent;

// NotificationOpenedIntegrationEvent.cs
public sealed record NotificationOpenedIntegrationEvent(
    Guid EventId, DateTime OccurredOnUtc,
    int StudentId, string Code, int Category) : IIntegrationEvent;
```

### B. Emit mechanics — RESOLVED: inline `IPublisher.Publish` in fail-soft try/catch (NOT the `AiUsageRecorder` Task.Run recorder)
Grounded in the actual `NudgeDispatcher` code (`backend/src/Modules/Notifications/.../Reengagement/NudgeDispatcher.cs`):
- The dispatcher **is already off the request-critical path** — it runs inside post-commit `INotificationHandler<T>` consumers (the integration-event backbone), wrapped in its own top-level `try/catch` that swallows everything (lines 124-130). It is not in an HTTP request's hot path.
- The whole body already does `_db.SaveChangesAsync(ct)` and awaits the arbiter + push sender — an extra `await _publisher.Publish(...)` (an in-process MediatR fan-out) is cheap relative to those and runs on the same already-async path.
- **MediatR is registered once at the Host** with `IsolatedNotificationPublisher` (`MediatRExtensions.cs`) — a failing Analytics consumer cannot abort siblings, and the dispatcher's own try/catch is the second guarantee.
- The `Task.Run` + `IServiceScopeFactory` recorder pattern (`AiUsageRecorder`) exists specifically because the AI gateway emits from a **request-scoped** path where the scoped `AiDbContext` may be disposed before the background task runs. **That risk does not apply here** — the dispatcher does not hand a scoped entity to the publish, and the publish completes within the consumer's scope. Adding Task.Run here would (a) be a heavier pattern than the situation needs and (b) re-trip the "ask before new pattern" rule for no benefit.

**Resolution:** inject `IPublisher` into `NudgeDispatcher`; after the successful `SaveChangesAsync`, emit the Dispatched event (and, inside `TryArbiterGrantAsync`'s `if (!result.ShouldPush)` branch, emit the Suppressed event) via a small private fail-soft helper that mirrors `TimedEventStartedRepublisher` (`await _publisher.Publish(new ...IntegrationEvent(Guid.NewGuid(), nowUtc, ...))` in its own try/catch that logs + swallows). This matches an existing approved shape (rule 8) and keeps the single choke point.

Exact emit points in `NudgeDispatcher.DispatchAsync`:
- **Dispatched:** after `await _db.SaveChangesAsync(ct)` (line 117), using the final `deliveredChannels` value, `message.Code`, `(int)message.Category`, `message.RecipientChildUserId`, `nowUtc`.
- **Suppressed:** inside `TryArbiterGrantAsync`, in the existing `if (!result.ShouldPush)` block (lines 176-182), using `result.SuppressReason.ToString()`. (One Suppressed event per push denial; the inbox row is still written — Suppressed is specifically the *push-suppressed* signal, consistent with P9-07 semantics. Note this in the DTO docs so admins read "suppressed" as "push suppressed.")

> Edge case to spec: a single nudge can produce BOTH a Suppressed (push denied) AND a Dispatched (in-app delivered) signal — that is correct and intended (the inbox receipt landed; the push did not). The admin aggregate counts them in separate buckets; open-rate is computed against Dispatched (delivered), not Suppressed.

### C. Analytics consumer(s) — RESOLVED: 3 separate `INotificationHandler<T>` (mirror the existing 6)
New files in `Analytics.Application/IntegrationEventHandlers/` (mirror `LessonCompletedEventHandler` exactly — inject `IActivityEventStore` + `ILoggerManager`, map → `ActivityEvent`, `await _store.AddAsync`). The store already gives fail-soft + `SourceEventId` idempotency.

| Consumer | `EventType` value | Facets written |
|---|---|---|
| `NotificationDispatchedEventHandler` | `"NotificationDispatched"` | `NotificationCode`=Code, `NotificationCategory`=Category, `NotificationChannels`=DeliveredChannels, `SuppressionReason`=null |
| `NotificationSuppressedEventHandler` | `"NotificationSuppressed"` | `NotificationCode`=Code, `NotificationCategory`=Category, `NotificationChannels`=null, `SuppressionReason`=Reason |
| `NotificationOpenedEventHandler` | `"NotificationOpened"` | `NotificationCode`=Code, `NotificationCategory`=Category, others null |

All three set `StudentId`=StudentId, `OccurredAtUtc`=OccurredOnUtc, `SourceEventId`=EventId, `SubjectCode`/`DurationSeconds`=null. Keep the existing fail-soft/idempotent contract (the store handles both).

**DI / cross-module relay — RESOLVED (BE-6):** **no DI change needed.** Both the **Analytics Application assembly** (line 62) and the **Notifications Application assembly** (line 38) are already in the host `AddCrossModuleMediatR` scan (`backend/src/Host/Learnexia.Host/Extensions/MediatRExtensions.cs`). New Analytics `INotificationHandler<NotificationDispatchedIntegrationEvent>` etc. are auto-discovered; `_publisher.Publish` from the Notifications dispatcher fans out to them. The api-tester should still assert the relay end-to-end (publish → row appended) since that is the load-bearing wiring.

### D. Admin aggregate — RESOLVED
Extend the read seam, NOT a parallel one. Add a method to `IPlatformAnalyticsQuery` (it is already the Analytics cross-module read seam consumed by the Identity façade):

```
// Shared.Contracts/Analytics/IPlatformAnalyticsQuery.cs  (new method on existing interface)
Task<NotificationAnalyticsStats> GetNotificationsAsync(
    DateTime fromUtc, DateTime toUtc, int? category, CancellationToken ct = default);

// new records in the same file (mirror PlatformAnalyticsStats sentinel-safe style)
public record NotificationAnalyticsStats(
    int TotalDispatched, int TotalSuppressed, int TotalOpened, double OpenRate,
    IReadOnlyList<NotificationCodeStat> ByCode,
    IReadOnlyList<NotificationCategoryStat> ByCategory,
    IReadOnlyList<NotificationReasonStat> SuppressionReasons);
public record NotificationCodeStat(string Code, int Dispatched, int Suppressed, int Opened, double OpenRate);
public record NotificationCategoryStat(int Category, int Dispatched, int Suppressed, int Opened, double OpenRate);
public record NotificationReasonStat(string Reason, int Count);
```

- **Impl:** extend `PlatformAnalyticsQueryAdapter` to delegate to a new EF-bound service method (mirror `IActivitySessionService` — keep EF only in Infrastructure, Option C). The query filters `ActivityEvents` where `EventType IN ('NotificationDispatched','NotificationSuppressed','NotificationOpened')` AND `OccurredAtUtc >= from AND < to` (AND `NotificationCategory == category` when supplied), then group-by `NotificationCode` / `NotificationCategory` / `SuppressionReason` in SQL. **Open-rate = Opened / Dispatched** per bucket (guard divide-by-zero → 0.0, sentinel-safe). Compute the group-bys server-side (these are admin-only, low-traffic).
- **Admin endpoint:** add `GET /api/Admin/Analytics/notifications?from=&to=&category=` to `AdminAnalyticsController` (Identity.Api) + a new `GetNotificationAnalyticsQuery` handler in `Identity.Application/Features/Analytics/` mirroring `GetPlatformKpisQueryHandler` (inject `IPlatformAnalyticsQuery`, default `from`=30d ago / `to`=now, max-window 365d guard, `BadRequest` on `from >= to`, `BaseResponse<NotificationAnalyticsDto>`, `Successed`, localized success message). Gate with the existing `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` on the controller. Map the seam record → an Identity-side `NotificationAnalyticsDto`.
- **resx:** add `NotificationAnalyticsRetrievedSuccessfully` (and reuse existing `PlatformKpisFromMustBeBeforeTo` / `PlatformKpisWindowTooLarge` for validation) to `SharedResourcesKey.cs` + `SharedResources.en-US.resx` + `SharedResources.ar-EG.resx`.

### E. Migration? — RESOLVED: YES, one (see Handoff → db-migration). It rides `ActivityEvent` but needs the 4 nullable facet columns + the `(EventType, OccurredAtUtc)` composite index. db-migration batch runs first.

---

## Handoff → frontend
No student-app UI in this story. The only FE surface is the **admin dashboard** (separate `P9-11-FE`), which consumes the contract already documented in the task file:
`GET /api/Admin/Analytics/notifications?from=&to=&category=` → `BaseResponse<NotificationAnalyticsDto>` (per-code/category dispatched/suppressed/opened + open-rate + suppression-reason buckets). Admin-gated; read `Successed`. No `designer` / `frontend` / `frontend-e2e-tester` stages in this pipeline.

## Open questions / assumptions / risks
1. **Push-tap Opened DEFERRED (flagged, not dropped):** Only the inbox-read path emits `NotificationOpened` now. Push-tap open reporting depends on the **P9-02 FE deep-link handler**. The admin "open-rate" therefore undercounts opens until P9-02 ships — the DTO/endpoint docs MUST state this so admins don't misread it. Recommend a one-line note in the endpoint XML doc + HANDOFF.md. **No action needed from the lead unless you want a different v1 framing.**
2. **"Suppressed" = push-suppressed, not nudge-dropped.** P9-07 suppression only rations the *push* channel; the in-app inbox row is always written. So a Suppressed signal usually co-occurs with a Dispatched (in-app) signal for the same nudge. Assumption: this is the intended semantic (matches AC + P9-07). Open-rate is computed against Dispatched. Confirm this framing is acceptable for the admin chart, or whether you want push-specific vs inbox-specific dispatched counts split further (would need a channel facet on the Dispatched bucket — already available via `NotificationChannels`, can be a follow-up).
3. **Other suppression points beyond the arbiter:** `DisabledByParent` / `DailyCapReached` / `QuietHours` are produced by `ReengagementEvaluator.Evaluate` **inside each handler** (e.g. `BadgeEarnedIntegrationEventHandler` logs `not_eligible reason=...` and returns *before* calling the dispatcher), and dedupe drops are logged as `dedupe_hit` in `ReengagementHandlerHelper`. The dispatcher only ever sees the **push-arbitration** reasons (`GlobalBudgetExhausted`, `PriorityLost`, `Cooldown`). **Decision needed / assumption:** v1 captures Suppressed only at the dispatcher choke point (the P9-07 push reasons), per the story's "single choke point" wording and the task's "Suppressed (with P9-07 reason)". Capturing the *pre-dispatcher* eligibility/dedupe suppressions would require emitting from ~11 handlers (or the shared helper) — broader surface, not the single-choke-point design. **Recommend: dispatcher-only in v1; note the pre-dispatch suppressions as a follow-up.** Flag for lead confirmation — this changes how complete the "suppressed" bucket is.
4. **`category` filter type on the endpoint:** the query param is the raw `NotificationCategory` int (0..9). Admin FE maps int→label. Acceptable given the enum has stable int values; no shared label contract added.
5. **Index scope:** start with the `(EventType, OccurredAtUtc)` composite only. If the by-category group-by is slow at volume, add `(EventType, NotificationCategory)` later. v1 is admin-only low-traffic; matches the existing "materialise rollups later" note in `ActivitySessionService`.
6. **Security (for security-auditor):** events carry opaque `StudentId` + code/category/reason scalars only — no name/email/body/title. The admin endpoint must be `AdminOnly` (verify policy enforced, not just declared — rule 6 notes policies exist but aren't always enforced). No IDOR surface (no per-user param; aggregate only).

## Recommended pipeline order (first cut — `planner` finalizes)
Cross-module story: shared-file edits (`Shared.Contracts`, `IPlatformAnalyticsQuery`, `AdminAnalyticsController`, `SharedResourcesKey.cs` + resx, `Directory.Packages.props` n/a) must be serialized.

1. **Batch 0 — db-migration (FIRST, blocking):** add the 4 nullable facet columns + `(EventType, OccurredAtUtc)` index to `ActivityEvent`; generate `P9_11_AddNotificationFacetsToActivityEvent` migration. Blocks the Analytics consumer + admin query.
2. **Batch 1 — Shared.Contracts (serialized, blocking):** add the 3 `Notifications/Notification*IntegrationEvent.cs` records + the `GetNotificationsAsync` method & DTO records on `IPlatformAnalyticsQuery`. Blocks both emit and consume sides.
3. **Batch 2 — parallel backend-feature** (independent once Batches 0+1 land):
   - **2a (Notifications emit):** inject `IPublisher` into `NudgeDispatcher`, emit Dispatched + Suppressed (fail-soft); emit Opened from `MarkNotificationReadCommandHandler`.
   - **2b (Analytics consume + read):** 3 `INotificationHandler` consumers; extend `PlatformAnalyticsQueryAdapter` + the EF service method for the aggregate.
   - **2c (Identity admin):** `GetNotificationAnalyticsQuery` + handler + DTO + `AdminAnalyticsController` endpoint + resx keys. (Depends on Batch 1's seam method; can start against the interface as soon as it lands.)
4. **Gate — api-tester:** integration test the relay end-to-end (publish each event → assert `ActivityEvent` row + EventType + facets; idempotent redelivery → one row), the dispatcher emits (dispatched/suppressed on a real arbiter denial), the MarkRead Opened emit, and the admin endpoint (auth 401/403, by-code/category buckets, open-rate math, window validation). Mirror `P9_12_TimedEventNudges_Tests.cs` setup.
5. **Gate — security-auditor:** analytics PII (events carry no PII) + admin authz (AdminOnly actually enforced). Security-sensitive (admin + child-linked ids) → Critical/High block.
6. **Gate — reviewer:** against the AC above + CONVENTIONS (append-only/no-UoW, `ILoggerManager`, `Successed`, module isolation, localized resx, fail-soft, rule-8 no-new-pattern). Include api-tester + security-auditor results.
7. **committer:** per-story branch `feat/P9-11-notification-analytics-sink`, conventional message, push + open PR. Update `docs/dev/HANDOFF.md` (mark the "first-class notification analytics sink" backlog item done; note the deferred push-tap Opened + dispatcher-only suppression scope).
