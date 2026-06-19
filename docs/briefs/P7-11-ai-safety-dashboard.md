# Pipeline Brief — P7-11 AI-safety & quality monitoring dashboard (buildable slice)

## Summary & traceability
- **Task (1 line):** Expose admin-only, aggregate read endpoints over the AI Safety Layer's signals so an admin can confirm the safety layer is working and drill into recent blocked/flagged outputs — building the slice that is buildable **now** over the existing `ai.SafetyEvents` table, and deciding the tutor usage/cost slice.
- **User story:** [user-stories/Phase-7-Admin-Console/P7-11-ai-safety-monitoring-dashboard.md](../../user-stories/Phase-7-Admin-Console/P7-11-ai-safety-monitoring-dashboard.md) (source of truth).
- **Task file:** [tasks/Backend/Phase-7-Admin-Console/P7-11-BE.md](../../tasks/Backend/Phase-7-Admin-Console/P7-11-BE.md).
- **FR-IDs:** FR-ADM-10 (admin AI-safety oversight), FR-AI-4. **SRS §3** (Admin role). **NFR-1** (aggregates must not degrade live request latency).
- **BRD goal:** G2 (trust & safety — child-safe AI). Epic: *Admin — Analytics & AI Oversight*. Phase 7 — Admin Console.
- **Scope decision:** Backend-only (admin-dashboard FE is the other lead's). This brief grounds the **buildable slice**, not the full story.

## Business context & value
- **Who benefits:** the **admin/operator**. Value: confirm the P3-02 Safety Layer is actually catching unsafe AI output, spot regressions (a spike in blocks/flags by reason or model) before children are exposed, and have an auditable drill-in of recent flagged outputs. This is the oversight half of G2 (the enforcement half — blocking — already ships in P3-02).
- **Success measure:** an admin can answer "is the safety layer working, and what's it catching?" without a DB query: total tutor outputs evaluated, blocked/flagged counts and rates, breakdown by reason/action/model, over a chosen time range, plus a paged drill-in.

## What exists today (grounding)
- **`ai.SafetyEvents` table is built and merged** (migration `20260613185735_AddSafetyEventsTable`). Entity: `SafetyEvent : CreationAuditedEntity` in `Learnexia.Modules.Ai.Domain.Entities`. It is append-only, PII-light by design (P3-02 Q5/Q6 — no prompt/response text, no name/email).
  - File: `backend/src/Modules/Ai/Learnexia.Modules.Ai.Domain/Entities/SafetyEvent.cs`
  - Config: `backend/src/Modules/Ai/Learnexia.Modules.Ai.Infrastructure/Persistence/Configurations/SafetyEventConfig.cs`
- **Available fields** (all queryable today):
  - `Id` (int PK), `StudentId` (int, no FK), `TaskKind` (string — "Explain"/"Hint"/"WhyWrong"/"Practice", maxlen 100, **not** indexed), `FailedChecks` (jsonb array of check names), `ReasonCodes` (jsonb array of stable reason codes), `ActionTaken` (string — "Blocked"/"Regenerated"/"FallbackReturned", maxlen 50, **indexed** `IX_SafetyEvents_ActionTaken`), `ModelId` (string, maxlen 200, **not** indexed), `OccurredAtUtc` (timestamptz, **indexed** `IX_SafetyEvents_OccurredAtUtc`), plus the `CreationAuditedEntity` audit columns.
  - Secondary index `IX_SafetyEvents_StudentId`.
- **Stable reason codes:** `backend/src/Modules/Ai/Learnexia.Modules.Ai.Domain/Safety/ReasonCodes.cs` (categorize by these — they are deliberately stable for P7-09/P7-11).
- **Cross-module event already exists:** `AiOutputFlaggedIntegrationEvent` (`Shared.Contracts/Ai`) — published by the Ai module after persisting a non-Allowed `SafetyEvent`; already consumed by the **Moderation** module (P7-09) which has an `api/Admin/Moderation` read API. The dashboard is **aggregate**, distinct from Moderation's per-item review queue — do not duplicate the queue.
- **Tutor usage/cost is NOT persisted.** The AI Gateway logs usage at Debug only — `AiGateway.LogUsage(...)` explicitly states "Log-only (Q5 — no DB write)". The `ai.AiUsageLogs` table the story needs does **not** exist. `AiUsage` (`Shared.Contracts/Ai/AiUsage.cs`) is the per-call shape (Provider, ModelId, PromptTokens, CompletionTokens, LatencyMs, EstimatedCostUsd, WasCacheHit) but it is never written to a table.
  - File: `backend/src/Modules/Ai/Learnexia.Modules.Ai.Infrastructure/Gateway/AiGateway.cs` (see `LogUsage`, `EnrichWithCost`).

## Buildable slice (ship NOW)
1. **Safety-signal summary** over a date range: total safety events, count + rate of `Blocked` vs `Regenerated` vs `FallbackReturned`, broken down by **reason code**, **action**, **model**, and **taskKind**. (Story also wants subject/language breakdown — see Open Questions: `SafetyEvent` has no subject/language column, so those facets are **N/A in this slice**.)
2. **Flagged-outputs drill-in**: paged, newest-first list of recent events (Id as content reference, FailedChecks, ReasonCodes, ActionTaken, ModelId, TaskKind, StudentId optional, OccurredAtUtc). Minimal data — no extra PII (the table is already PII-light).
3. **Time-series**: events bucketed by day over the range (for a trend chart), optionally split by action.

## Deferred (blocked / out of slice)
- **Eval pass/fail results** (AC2): **BLOCKED on P6-02** — there is no eval harness, no eval-run table, no producer. Confirmed: no eval entity/table exists in the repo. The eval endpoint (`P7-11-BE-3`) must be **deferred** and the brief notes it. The dashboard can ship without it; FE shows an empty/"not available yet" state.
- **Tutor usage/cost** (AC3): needs `ai.AiUsageLogs` which does not exist — see the recommendation below.

## Tutor usage/cost — recommendation: (a) build `AiUsageLogs` as part of P7-11
The story explicitly lists "AI tutor usage & cost" as a first-class AC, and the data is cheap to capture (the gateway already computes `AiUsage` per call, it just throws it away after a Debug log). Two options:
- **(a) Create `ai.AiUsageLogs` + wire the gateway to persist usage, then query it.** This makes usage/cost a real deliverable of P7-11. Effort is modest because `EnrichWithCost`/`LogUsage` already produce the exact shape. **Recommended.**
- **(b) Defer cost, ship only the safety dashboard now.** Lower risk, faster, but leaves a named AC (AC3) unmet and the gateway change still has to happen eventually.

**Recommendation: (a), but as a clearly separated sub-batch** so the safety slice (which needs no new table) can land/ship independently if the lead wants to descope. Design notes for (a):
- New append-only entity `AiUsageLog` in `Ai.Domain.Entities` (mirror `SafetyEvent`): `Id`, `Provider`, `ModelId`, `TaskKind`, `PromptTokens`, `CompletionTokens`, `EstimatedCostUsd` (decimal), `LatencyMs`, `WasCacheHit` (bool), `StudentId` (int, no FK), `OccurredAtUtc` (timestamptz, indexed). Subject/grade are **not** on `AiUsage` today (Open Question OQ-3).
- **Write path:** persist in/around the gateway. **Caution (CLAUDE.md rule #3 — no Unit of Work; GenericRepository commits per call):** do **not** add a synchronous DB write on the hot child-facing AI path that could add latency or fail the request. Prefer **fire-and-forget / fail-soft** persistence (the gateway already wraps providers in try/catch and never throws to caller) **or** capture via an integration event the Ai module persists out-of-band. **This is a design choice that needs the lead's sign-off** (it touches the hot path and may imply a background-write pattern — see rule #8 "design patterns ask first"). Flag in Open Questions.
- **Read path:** `GetTutorUsageQuery` aggregates `AiUsageLogs` (volume, token sum, cost sum, avg latency, cache-hit rate) by day/model/taskKind over the range.

## Cross-module rule — recommendation: Ai-module admin endpoints (no Shared.Contracts seam needed)
The safety data is **owned by the Ai module**. Unlike P5-08 (Parent reaches *into other modules*), here the read-model and the data live in the **same module**, so there is **no cross-module boundary to cross** for the safety + usage slices. Recommendation:
- **Build the read endpoints as an Ai-module admin controller** (`AdminAiSafetyController` under the Ai.Api project), `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`, route `api/Admin/AiSafety/...`, returning `BaseResponse<T>` via `NewResult(...)`. Mirror the shape of `ModerationController` (`api/Admin/Moderation`, AdminOnly, paged) and `AdminUsersController`.
- This keeps module isolation intact **without** inventing a `Shared.Contracts` seam — the brief recommends **option (b) from the task file (per-module admin endpoint)** over a new Analytics module, because the data is single-module and there is no aggregation across schemas. A `Shared.Contracts` seam would only be needed if a *separate* Analytics module had to read Ai data — not the case here.
- If/when P7-10 (or a future Analytics module) needs the same safety aggregates, expose them then via a `Shared.Contracts` `IAiSafetyReadModel` query interface implemented in `Ai.Infrastructure` (mirror `IStudentLearningStatsQuery` + its adapter). Not needed for this slice.

## Acceptance criteria (this slice)
- `GET /api/Admin/AiSafety/signals?from=&to=` → `BaseResponse<SafetySignalSummaryDto>`: total events, blocked/flagged/regenerated/fallback counts + rates, breakdownByReason[], breakdownByAction[], breakdownByModel[], breakdownByTaskKind[], over the range.
- `GET /api/Admin/AiSafety/flagged?from=&to=&page=&pageSize=` → `BaseResponse<PagedResult<FlaggedOutputDto>>`: newest-first, minimal fields (contentRef = SafetyEvent.Id, failedChecks, reasonCodes, actionTaken, modelId, taskKind, occurredAt). Page size capped in-handler (mirror AdminUsers cap of 100).
- `GET /api/Admin/AiSafety/trend?from=&to=` → `BaseResponse<...>`: per-day buckets, optionally by action.
- (If slice (a) chosen) `GET /api/Admin/AiSafety/usage?from=&to=` → `BaseResponse<TutorUsageDto>`: volume, token/cost trend, avg latency, cache-hit rate; by model/taskKind.
- All endpoints AdminOnly (anonymous → 401, non-admin → 403). Aggregates **cached in Redis (`IDistributedCache`, short TTL)** so the live AI path is untouched (NFR-1).
- Range/paging inputs **validated in-handler** (queries are not auto-validated — `ValidationBehavior` is `ICommand<>` only).
- Empty range → 200 with zeroed/empty payload, never 404 (mirror Parent/Moderation graceful-empty behavior).
- Eval endpoint returns a graceful "not available" shape OR is omitted until P6-02 (deferred — noted).

## Affected modules & data
- **Module:** Ai (read endpoints; queries/handlers/DTOs/controller). No other module touched for the safety slice.
- **Existing entity (read-only):** `SafetyEvent` / `ai.SafetyEvents` — no schema change.
- **New entity (only if slice (a)):** `AiUsageLog` / `ai.AiUsageLogs` (append-only) + EF config + migration + gateway write wiring.
- **No new write entities** otherwise (optional cached aggregate lives in Redis, not a table).

## Handoff → db-migration
- **Only if usage slice (a) is approved:** add `AiUsageLog` entity + `AiUsageLogConfig` (mirror `SafetyEventConfig`: jsonb not needed here; `OccurredAtUtc` timestamptz + index; `EstimatedCostUsd` as `decimal`/numeric; index `ModelId`/`TaskKind` if the breakdown queries need it) + EF migration in `Ai.Infrastructure/Migrations`, schema `ai`. Register `DbSet` on `AiDbContext`. No FK to other modules (StudentId plain int).
- **If usage deferred:** no migration — the safety + flagged + trend endpoints read the existing table.

## Handoff → backend-feature
- Queries/handlers (mirror existing Ai feature folder shape `Features/<Area>/Queries/...`):
  - `GetSafetySignalSummaryQuery` → `SafetySignalSummaryDto` (aggregate over `SafetyEvents`; group-by in SQL via `AsNoTracking`; parse jsonb arrays for reason/check breakdown).
  - `GetFlaggedOutputsQuery` → `PagedResult<FlaggedOutputDto>` (paged, ordered by `OccurredAtUtc desc`).
  - `GetSafetySignalTrendQuery` → per-day buckets.
  - (slice a) `GetTutorUsageQuery` → `TutorUsageDto`.
- Controller `AdminAiSafetyController` in `Ai.Api/Controllers`, `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`, `api/Admin/AiSafety`, `NewResult(...)`, `BaseResponse<T>`, `Successed` envelope flag.
- Cache aggregate responses in Redis (`IDistributedCache`, short TTL); validate `from < to`, range cap, page/pageSize in-handler.
- **ReasonCodes/FailedChecks are jsonb arrays** — aggregating "by reason" requires unnesting the JSON arrays. Confirm the Npgsql/EF approach (jsonb operators or in-memory expansion of a date-windowed slice). Note in plan.

## Handoff → frontend
- Out of scope for this lead (admin-dashboard FE owned by the other lead). The endpoint contracts above are the FE contract.

## Open questions / assumptions / risks
- **OQ-1 (subject/language breakdown):** Story AC1 wants blocked/flagged broken down by **subject** and **language**; `SafetyEvent` has **no** subject or language column. `AiOutputFlaggedIntegrationEvent` carries nullable `SubjectCode`/`Grade`, but those aren't persisted on the table. → **Assumption:** ship subject/language as **N/A in this slice**; flag whether to add `SubjectCode`/`Language` columns to `SafetyEvent` (schema change + producer change in P3-02) as a follow-up. **Lead decision needed.**
- **OQ-2 (usage slice a vs b):** confirm whether P7-11 owns creating `AiUsageLogs` + the gateway write. Recommended (a) but it touches the hot AI path.
- **OQ-3 (usage write pattern):** if (a), the gateway persist-usage approach (fire-and-forget vs integration event vs background queue) is a **design-pattern decision — ask the lead first** (CLAUDE.md rule #8). Must be fail-soft and must not add latency to the child-facing path (NFR-1, rule #3 no-UoW).
- **OQ-4 (usage subject/grade breakdown):** Story AC3 wants usage by subject/grade; `AiUsage` carries neither today. Same N/A treatment as OQ-1 unless the gateway is enriched.
- **OQ-5 (eval results):** confirmed BLOCKED on P6-02 — defer. Lead to confirm the FE gets an "unavailable" placeholder vs the endpoint being omitted.
- **Risk — security-sensitive:** this is **child-safety AI data** → `security-auditor` is **mandatory** before the review gate (AdminOnly enforcement, no PII leakage in drill-in, no IDOR, no full-table loads — paged only).
- **Risk — performance:** jsonb-array unnest aggregation over a large `SafetyEvents` table; mitigate with date-windowed queries + Redis cache + the existing `OccurredAtUtc` index. `ModelId`/`TaskKind` are unindexed — add indexes if breakdown queries are slow.

## Recommended pipeline order (first cut — planner finalizes)
1. **db-migration** — *only if* usage slice (a) approved (`AiUsageLog` + config + migration + gateway write). Skippable for the safety-only slice.
2. **backend-feature** — safety summary + flagged drill-in + trend queries/handlers/controller (independent of #1); usage query/handler (depends on #1 if (a)).
3. **api-tester** — integration tests over the running `api/Admin/AiSafety/*` endpoints (AdminOnly 401/403, empty-range graceful, paging cap, aggregate correctness).
4. **security-auditor** — *mandatory* (AI/child-safety data). Critical/High block.
5. **reviewer** — gate against this brief's acceptance criteria + CONVENTIONS.md, including api-tester + security-auditor results.
6. **committer** — after PASS, on `feat/P7-11-…`, open PR.
- Parallelism: the safety-slice backend-feature can run while db-migration prepares `AiUsageLogs`; merge before the usage handler.
