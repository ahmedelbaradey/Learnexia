# Pipeline Brief — P7-09 Content Moderation Queue & Review Actions

> Story: [user-stories/Phase-7-Admin-Console/P7-09-content-moderation-queue.md](../../user-stories/Phase-7-Admin-Console/P7-09-content-moderation-queue.md)
> Tasks: [tasks/Backend/Phase-7-Admin-Console/P7-09-BE.md](../../tasks/Backend/Phase-7-Admin-Console/P7-09-BE.md)
> Branch: `feat/P7-09-moderation-queue` (off `main`)
> Scope of this brief: **Backend only** (Admin / Moderation module). FE is a separate `admin-dashboard` wave.

---

## Summary & traceability

**One-line task:** Add a human-in-the-loop moderation queue to the `Moderation` module — a `ModerationItem` store fed via Shared.Contracts events (AI-flagged outputs now; curriculum uploads when BL-01 lands), with admin-only paged/filterable list, item detail, and approve/reject/flag review actions that audit via `AdminActionPerformedEvent`.

| Trace | Value |
|---|---|
| User story | P7-09 — Content moderation queue & review actions |
| Acceptance criteria | 6 AC (see story §Acceptance Criteria) |
| BE tasks | P7-09-BE-1 … BE-8 |
| SRS / FR-IDs | SRS §3 (Admin role), **FR-ADM-8** (moderation queue), **FR-AI-4** (safety layer governance) |
| BRD goal | **G2** (child safety / trust) — human-in-the-loop on top of the automated AI Safety Layer |
| Phase / Epic | Phase 7 — Admin Console · Epic: Admin — Content Moderation & Governance · SP 8 |
| Module | **Moderation** (existing scaffold from P7-12); touches **Ai** for the publish seam (see ingest decision) |

**Product-decision overrides:** none specific to this story. General: no teacher role (admin is the only human reviewer).

---

## Business context & value

- **Who benefits:** Admins (the reviewers) directly; **children** ultimately — the queue enforces a human gate so no unsafe/low-quality AI or uploaded content slips past the automated P3-02 Safety Layer.
- **Value:** P3-02 already *blocks* unsafe AI output at generation time and writes a PII-light `SafetyEvent` row, but there is **no human review surface** on those signals today. P7-09 turns the dormant safety-signal stream into an actionable admin workflow (review → approve / reject-with-reason / flag-for-escalation), and prepares the same queue for curriculum uploads when BL-01 exists.
- **Success measured by:** all flagged AI outputs become reviewable queue items; every review action transitions status + is captured in the immutable audit log (P7-12); non-admins are fully locked out.

---

## What EXISTS today vs what P7-09 BUILDS

**Exists (verified in `main` lineage):**
- `Moderation` module fully scaffolded by P7-12 (4 projects, schema `moderation`, `ModerationDbContext`, `UnitOfWorkBehavior`, `CurrentUserService`, Host wiring in `.sln` / `Program.cs` / `AddCrossModuleMediatR` / `Claims.GenerateModules()`). Currently holds only `AuditLog`.
- The cross-module event seam is proven end-to-end: producers raise an intra-module `AdminActionPerformedDomainEvent` → a **relay handler** in `*.Application` re-publishes the Shared.Contracts `AdminActionPerformedEvent` post-commit → `Moderation`'s `AuditLogEventHandler` consumes it. P7-09 reuses this exact pattern.
- The **AI safety pipeline is merged**: `ai.SafetyEvents` table + `SafetyEvent` entity (`AddSafetyEventsTable` migration) + `ISafetyLayer`/`SafetyLayer` + `IAiSafetyEventStore`. `SafetyEvent` carries `StudentId` (plain int), `TaskKind`, `FailedChecks` (jsonb), `ReasonCodes` (jsonb), `ActionTaken` (`Blocked`/`Regenerated`/`FallbackReturned`), `ModelId`, `OccurredAtUtc`. It is **PII-light by design** (reason codes only, no prompt/response text).
- `Ai.Application` assembly is **already registered** in `AddCrossModuleMediatR`, so an Ai-side relay handler that publishes a new contract will be discovered, and a Moderation-side subscriber will receive it.
- `AuthorizationPolicies.AdminOnly` exists in `Shared.Kernel.Abstractions` (used by `AuditController`).
- `FullAuditedEntity` / `CreationAuditedEntity` / `AduitedEntity` base classes; `ModerationDbContext.SaveChangesAsync(userId)` stamps audit columns.

**P7-09 builds:**
1. `ModerationItem` entity (`FullAuditedEntity`) + EF config + migration in schema `moderation`.
2. The **ingest seam** — a new Shared.Contracts integration event for AI-flagged output + an Ai-side relay/publish + a Moderation-side subscriber that enqueues `ModerationItem` rows. (Plus a no-op-ready subscriber for the future BL-01 curriculum-upload event.)
3. Read side: `GET …/Queue` (paged, filtered) + `GET …/{id}` (detail).
4. Write side: review command (Approve / Reject-with-reason / Flag) that transitions status + records reviewer/time/reason + raises the `AdminActionPerformedDomainEvent` so the existing relay audits it.
5. Admin-only authz on the new controller; integration tests (closes the documented Moderation test gap).

> **Spec drift to note:** the story Notes + task file say "P3-02 not built yet — ship an empty-queue thin slice." That is **stale** — P3-02 IS merged. P7-09 can be fed by **live** AI safety signals now. Only the **curriculum-upload (BL-01)** source remains a future producer that degrades to an empty queue. (See OQ-1 / OQ-6 — retroactive vs forward-only.)

---

## Acceptance criteria (testable)

1. `GET /api/Moderation/Queue` returns a **paged** `BaseResponse<PagedResult<ModerationItemDto>>` of items with `source`, `submittedBy`, `contentRef`, `status`, `safetyVerdict`, `createdAt`; default order newest-first; DB-side paging (no full-table load).
2. The queue is **filterable** by `status`, `source`, `subject`, `grade`, `dateFrom`/`dateTo`, and **searchable** by `contentRef`; all filtering applied DB-side.
3. `GET /api/Moderation/{id}` returns `BaseResponse<ModerationItemDetailDto>` adding content preview + the full safety verdict (reason codes / failed checks). Preview blobs (when applicable) resolve via `Shared.Kernel.Storage.IStorageService`, not a module-local store.
4. `POST /api/Moderation/{id}/Review` with `{ decision: 'Approve'|'Reject'|'Flag', reason? }` transitions status (`Pending`→`Approved`/`Rejected`/`Flagged`), records reviewing admin id + UTC timestamp + reason; **reason is required when `decision == 'Reject'`** (FluentValidation on the `ICommand<>`); returns `BaseResponse<ModerationItemDto>`.
5. A successful review **emits** the cross-module `AdminActionPerformedEvent` (via the existing relay) so P7-12 writes an audit row (actor / action / target / before→after status in `Details`, PII-safe).
6. An AI-flagged output produced by the Safety Layer **appears as a `Pending` queue item** with `source = AiOutput` and a populated `safetyVerdict`, fed via **Shared.Contracts only — no cross-module FK, no reference to the Ai module's projects** (content reference + reason codes only).
7. **All endpoints require `AuthorizationPolicies.AdminOnly`**; non-admin → `403`. No update/delete endpoints beyond the review transition.
8. Reviewing a non-existent or already-terminal item returns a correct `BaseResponse` failure (not a 500); idempotency/double-review behavior is defined (see OQ-4).

---

## Affected modules & data

| Item | New / Existing | Notes |
|---|---|---|
| `Moderation` module scaffold | Existing | from P7-12; do not re-scaffold |
| `ModerationItem` entity + config + migration | **New** | schema `moderation`, `FullAuditedEntity`, loose ids only |
| `ModerationStatus` / `ModerationSource` enums | **New** | Domain enums; persist as string for stability (mirror `SafetyEvent.ActionTaken`) |
| Shared.Contracts AI-flag event | **New** | e.g. `AiOutputFlaggedIntegrationEvent` (the key design decision — see ingest) |
| Ai-side relay/publish of that event | **New (touches Ai module)** | `Ai.Application` only; SafetyLayer/handler raises it |
| Moderation `IntegrationEventHandlers` (enqueue) | **New** | subscribe to the AI-flag event (+ stub for future `CurriculumUploadReceivedEvent`) |
| `ModerationController` + queries + review command | **New** | mirror `AuditController` + Learning admin command shapes |
| `ai.SafetyEvents` / `SafetyEvent` | Existing — **read-only signal source** | not modified by the queue; values copied into the event/`ModerationItem` |
| P7-12 `AuditLog` + `AdminActionPerformedEvent` | Existing — reused unchanged | review actions audit through it |

### Proposed `ModerationItem` shape (db-migration finalizes)
- `Id` (PK)
- `Source` (enum→string): `AiOutput`, `CurriculumUpload` (+ room to grow)
- `Status` (enum→string): `Pending`, `Approved`, `Rejected`, `Flagged`
- `ContentRef` (string) — opaque reference into the source module (e.g. `SafetyEvent.Id` or a storage key); **no FK**
- `SubmittedBy` (int, plain) — student id for AI output / admin id for upload; nullable/0 when unknown
- `Subject` (int? or string) + `Grade` (int?) — for the filter facets (carried on the event; nullable since the current `SafetyEvent` does not expose them — see OQ-3)
- `SafetyVerdict` (string / jsonb) — failed checks + reason codes + action taken, copied from the safety signal
- `TaskKind` / `ModelId` (string?) — optional context from the AI signal
- `ReviewReason` (string?, maxlen ~2000)
- `ReviewedByAdminId` (int?), `ReviewedAtUtc` (timestamptz?)
- `SourceEventId` (Guid) — **idempotency key, unique index** (mirror `AuditLog.EventId`) so event redelivery never double-enqueues
- `OccurredAtUtc` (timestamptz) — original signal time
- Audit base columns (`FullAuditedEntity`): CreatedAt/By, UpdatedAt/By, IsDeleted/DeletedAt/By
- **Indexes:** `(Status)`, `(Source)`, `(OccurredAtUtc)`, unique `(SourceEventId)`, optional `(Subject, Grade)` for facet filters; mirror `AuditLogConfig` index style and `timestamptz` + legacy-timestamp handling.

---

## Handoff → db-migration

- Create `ModerationItem` (`FullAuditedEntity`) in `Modules/Moderation/...Domain/Entities/` + `ModerationItemConfig : IEntityTypeConfiguration<ModerationItem>` in `Infrastructure/Persistence/Configurations/` (auto-discovered by `ApplyConfigurationsFromAssembly`).
- Add `DbSet<ModerationItem>` to `ModerationDbContext`. Schema stays `moderation`.
- Persist `Status`/`Source` enums **as strings** (`HasConversion<string>()` + maxlen) for stability — mirror `SafetyEvent.ActionTaken`.
- `SafetyVerdict` as `jsonb` (mirror `SafetyEvent.FailedChecks`/`ReasonCodes`) **or** flat columns — db-migration to pick one; jsonb keeps the verdict opaque and que”-by-reason-code future-proof.
- `timestamptz` for all UTC datetimes; legacy-timestamp AppContext switch already enabled at Host.
- Unique index on `SourceEventId` (idempotency). Filter indexes per the list above.
- One migration `Migrations/*_AddModerationItem.cs` against `ModerationDbContext` (factory exists: `ModerationDbContextFactory`). **No cross-module FK.**
- If the ingest decision adds a new Shared.Contracts event but no Ai-side schema change, db-migration owns only the Moderation migration; the Ai-side change is code-only (publish), not a migration.

## Handoff → backend-feature

**Ingest (the cross-module seam — see decision below):**
- Add `Shared.Contracts/Ai/AiOutputFlaggedIntegrationEvent.cs` (record `: IIntegrationEvent`) carrying: `EventId` (Guid), `OccurredOnUtc`, `StudentId` (int), `TaskKind`, `FailedChecks`, `ReasonCodes`, `ActionTaken`, `ModelId`, and a `ContentRef` (the `SafetyEvent.Id` or storage key). **Reason codes only — no raw prompt/response (preserve P3-02 PII-light invariant).**
- In **`Ai.Application`** add a relay/publish so that when `SafetyLayer` writes a `SafetyEvent` (block/flag path), the event is published **post-persist, fail-soft** (a publish failure must never affect the student fallback). Mirror `AdminActionPerformedDomainEventRelayHandler` (domain-event raised → relay publishes Shared.Contracts event). Confirm exact placement with the lead (SafetyLayer is in Application and writes via `IAiSafetyEventStore` in Infrastructure — see OQ-2 for the post-commit-ordering nuance).
- In **`Moderation.Application/IntegrationEventHandlers`** add `AiOutputFlaggedEventHandler : INotificationHandler<AiOutputFlaggedIntegrationEvent>` → maps to a `Pending` `ModerationItem` via an Option-C writer (mirror `IAuditLogWriter`/`AuditLogWriter`: idempotent on `SourceEventId`, fail-soft). Add a sibling `CurriculumUploadReceivedEventHandler` **only if** the BL-01 contract is introduced now; otherwise leave a documented stub (degrades to empty for that source).

**Read side (mirror `GetAuditLogQuery` + `AuditController`):**
- `GetModerationQueueQuery` → `BaseResponse<PagedResult<ModerationItemDto>>`; filters status/source/subject/grade/dateFrom/dateTo + search by contentRef; DB-side paging via an Option-C query service (`IModerationQueryService`), EF out of Application.
- `GetModerationItemQuery` → `BaseResponse<ModerationItemDetailDto>`; preview blobs via `IStorageService` when a storage key is present.

**Write side (mirror Learning admin command + relay):**
- `ReviewModerationItemCommand : ICommand<…>` `{ Id, Decision, Reason? }` + `ReviewModerationItemCommandValidator` (FluentValidation: `Reason` required when `Decision == Reject`; valid enum). `ValidationBehavior` runs for `ICommand<>`.
- Handler: load item (Option-C writer/service), guard status (OQ-4), transition + set `ReviewedByAdminId`/`ReviewedAtUtc`/`ReviewReason`, persist, then **raise `AdminActionPerformedDomainEvent`** on the aggregate so the existing post-commit relay publishes `AdminActionPerformedEvent` → P7-12 audits it. `Details` must stay PII-safe (ids + before→after status only; per the P7-13 lesson, do **not** persist free-text reason into the immutable audit `Details`).
- Add a Moderation-side `AdminActionPerformedDomainEvent` + relay handler (mirror Learning/Gamification) since Moderation will now be a *producer* too.
- Return envelope spelled **`Successed`**; controllers use `NewResult(...)`.

**Controller:** new `ModerationController` (route e.g. `api/Moderation` or `api/Admin/Moderation` — confirm with lead, see OQ-5), `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` at class level, `[ProducesResponseType]` 200/403, mirror `AuditController`.

## Handoff → frontend
Out of scope for this brief (backend-only). FE contract is already specified in `P7-09-BE.md` §"Contract for Frontend" — surface in the `admin-dashboard` wave: queue table (filters + search), item detail (preview + safety verdict), review modal (Approve / Reject[reason required] / Flag). Admin-only route guarding.

---

## Per-agent pipeline & gates

| Agent | Responsibility |
|---|---|
| **db-migration** | `ModerationItem` entity + config + `DbSet` + migration (schema `moderation`, enums-as-string, jsonb verdict, unique `SourceEventId`, filter indexes). |
| **backend-feature** | Shared.Contracts `AiOutputFlaggedIntegrationEvent`; Ai-side relay/publish; Moderation subscriber + Option-C writer; queue + detail queries (Option-C query service); review command + validator + status guard; Moderation `AdminActionPerformedDomainEvent` + relay; controller + admin authz. |
| **api-tester** | Integration tests — **closes the documented Moderation test gap** (HANDOFF: "Moderation — no dedicated tests"). Cover: enqueue on AI-flag event (idempotent on redelivery), paged/filtered queue, detail, each review transition, reason-required-on-reject (422/400), audit-event emission, non-admin → 403. |
| **security-auditor** (MANDATORY) | Child-safety content + admin authz + the new cross-module event. Verify: PII-light invariant preserved across the event + `ModerationItem` (no raw prompt/response), AdminOnly enforced on every endpoint, IDOR-safe (any admin may act, but review actor is recorded from JWT not the body), no cross-module FK / project reference, fail-soft publish never affects the student safety fallback. Critical/High findings block. |
| **reviewer** | Gate against the 8 AC + CONVENTIONS (Option C, `Successed`, `BaseResponse`/`NewResult`, ILoggerManager, module isolation, no UoW), incl. api-tester + security-auditor results. |
| **committer** | After reviewer PASS — commit on `feat/P7-09-moderation-queue`, push, open PR. |

---

## Ingest seam — recommendation (the key design decision)

**Finding:** the Moderation module **cannot** learn about a flagged AI output today without a new seam. `SafetyLayer` writes a `SafetyEvent` directly via `IAiSafetyEventStore` but **publishes no integration event**, and there is **no** `AiOutputFlaggedEvent` in `Shared.Contracts`. A `Shared.Contracts` read seam (e.g. `ISafetyEventQuery`) is the alternative, but it would require Moderation to **pull**, complicating "appears as Pending on flag."

**Recommendation: event-driven push, mirroring the proven `AdminActionPerformedEvent` relay.**
1. Add `AiOutputFlaggedIntegrationEvent` to `Shared.Contracts/Ai/` (PII-light: reason codes + content ref only).
2. In `Ai.Application`, publish it **after** a `SafetyEvent` is persisted on the block/flag path, **fail-soft** (mirror the post-commit relay so a publish failure never affects the student's safe fallback).
3. `Moderation.Application` subscribes and enqueues a `Pending` `ModerationItem` via an idempotent Option-C writer.

This **requires touching the Ai module** (Application-layer publish only — no FK, no project reference; `Ai.Application` is already in `AddCrossModuleMediatR`). That is the load-bearing decision for the lead to confirm (OQ-1/OQ-2). It respects rule #1 (only `Shared.Contracts` crosses), reuses an established pattern, and degrades cleanly: the BL-01 curriculum-upload source is just a second subscriber that stays dormant until BL-01 publishes its event.

---

## Open questions / assumptions / risks (for the lead)

- **OQ-1 (ingest seam — primary):** Confirm the event-driven approach above, which **adds a publish in the Ai module's `SafetyLayer`/Application**. Acceptable, or should P7-09 stay strictly inside Moderation with a pull-based `Shared.Contracts` read seam against `SafetyEvents` (more isolation, but no real-time enqueue)?
- **OQ-2 (publish ordering / fail-soft):** `SafetyLayer` is fail-closed and its event store `AppendAsync` is fail-soft (swallows). Where exactly should the flag event publish — inside `SafetyLayer` after `PersistSafetyEventAsync`, or via a domain-event raised by the store? It must never throw into the student path. Recommend a fail-soft relay mirroring `AdminActionPerformedDomainEventRelayHandler`.
- **OQ-3 (subject/grade facets):** AC requires filter by subject/grade, but `SafetyEvent` carries **no subject/grade** today (only `StudentId`, `TaskKind`). Options: (a) enrich the new event with subject/grade from the AI request context (needs the producing handler to supply them — `SafetyLayer.PersistSafetyEventAsync` currently sets `StudentId = 0`); (b) ship the facets as nullable and leave them empty for the AI source until enriched. Which?
- **OQ-4 (review idempotency / terminal state):** Can a `Rejected`/`Approved` item be re-reviewed (e.g. Flag → Approve)? Recommend: only `Pending`/`Flagged` are actionable; re-reviewing a terminal item returns a `BaseResponse` failure. Confirm the allowed transitions.
- **OQ-5 (route prefix):** `api/Moderation/...` (per task contract) vs `api/Admin/Moderation/...` (matches `AuditController`'s `api/Admin/Audit`). Pick one for FE consistency.
- **OQ-6 (retroactive vs forward-only):** Should existing `ai.SafetyEvents` rows already in the DB be backfilled into the queue (one-time), or is the queue **forward-only** from the first flag after deploy? Recommend forward-only (simpler, avoids re-PII-handling); backfill is a follow-up if the lead wants history.
- **OQ-7 (status/source enum values):** Confirm `Status` = {Pending, Approved, Rejected, Flagged} and `Source` = {AiOutput, CurriculumUpload}. Any need for `Escalated`/`InReview` beyond `Flagged`? Persist-as-string assumed.
- **OQ-8 (retention/PII of flagged content):** P3-02 deliberately stores **no raw content**; the only "content" the queue can show is reason codes + a content ref. If admins need the actual flagged text to decide, that requires an access-controlled quarantine store (explicitly deferred from P3-02 to P7-09). Is reason-code-only review sufficient for v1, or is a quarantine store in scope? (Significant scope + PII risk — recommend reason-code-only for v1, quarantine as a separate story.)
- **OQ-9 (spec drift):** Story Notes + `P7-09-BE.md` still say "P3-02 not built — empty-queue thin slice." P3-02 IS merged. Update the story/task wording to reflect live AI ingest (only BL-01 remains future)? (Per CLAUDE.md, decisions/spec changes get written back.)
- **Assumption:** mirror **Learning** (admin command + relay) and **P7-12** (read query + Option-C writer/query-service + `AuditController`) for all structure; no new design patterns introduced.
- **Risk:** the publish added to the Ai safety path is on a child-safety-critical code path — must be provably fail-soft (security-auditor gate). A regression that lets a publish exception escape `SafetyLayer` would break the no-unscreened-content guarantee.

---

## Recommended pipeline order (first cut — planner finalizes)

1. **db-migration** — `ModerationItem` + config + migration (no upstream deps; can start immediately).
2. **backend-feature** — in two waves if parallelized:
   - 2a. Shared.Contracts event + Ai-side publish/relay (touches Ai; serialize against any other Ai work).
   - 2b. Moderation subscriber + queue/detail queries + review command + relay + controller (depends on 1 + the contract from 2a).
3. **api-tester** — after backend-feature (HTTP + event-driven enqueue).
4. **security-auditor** (mandatory) — after backend-feature, before the gate.
5. **reviewer** — gate against AC + tester/auditor results.
6. **committer** — on PASS, commit + push + PR on `feat/P7-09-moderation-queue`.

Designer stage: **skipped** (backend-only; FE design handled in the separate admin-dashboard wave).
