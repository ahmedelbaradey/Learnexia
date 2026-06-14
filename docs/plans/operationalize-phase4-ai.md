# Execution Plan — Operationalize Phase-4 AI (Initiative)

> **Wave:** `feat/phase4-ai-runtime`
> **Dated:** 2026-06-15
> **Lead decisions baked in:** OQ-1 (Redis read-through, auto-approve MVP), OQ-2 (BE-12 + BE-15 in; BE-13 + BE-14 DEFERRED), OQ-3 (RAG only, default stays Empty, flip via `AiHelper:ContextProvider="Rag"`), OQ-4/7 (Hangfire re-embed + auto-approve). No re-raised OQs.

---

## Source

| Artifact | Path |
|---|---|
| Pipeline Brief | `docs/briefs/operationalize-phase4-ai.md` |
| Cost-routing strategy | `docs/briefs/ai-cost-routing.md` |
| AI Helper MVP brief | `docs/briefs/ai-helper-mvp.md` |
| P3-01 task file | `tasks/Backend/Phase-4-AI-Tutor/P3-01-BE.md` |
| P3-07 task file | `tasks/Backend/Phase-4-AI-Tutor/P3-07-BE.md` |
| Conventions | `docs/dev/CONVENTIONS.md` |
| ADR-0001 (no UoW) | `docs/dev/adr/0001-unit-of-work.md` |
| Parallelism rules | `docs/dev/PARALLELISM.md` |
| Architecture | `docs/architecture.md` |

---

## Current state (what is already built — do NOT rebuild)

All of the following exist as merged, passing code. The plan builds ONLY what is missing.

| Capability | Key files |
|---|---|
| 4 intent handlers (Explain/Hint/SimilarExample/Simplify) + DEFERRED cache seams | `Ai.Application/Features/{Explain,Hint,SimilarExample,Simplify}/Commands/*Handler.cs` |
| SafetyLayer (buffer→screen→emit, fail-closed) + arch tests | `Ai.Application/Safety/SafetyLayer.cs`, `tests/Modules.Ai.UnitTests/AiModuleArchTests.cs` |
| `IAiGateway` + `AiGateway` facade (retry, timeout, typed error, log-only usage) | `Ai.Infrastructure/Gateway/AiGateway.cs` |
| `ClaudeProvider` + `OpenAiProvider` (typed HttpClient, key from config) | `Ai.Infrastructure/Providers/ClaudeProvider.cs`, `OpenAiProvider.cs` |
| `AiRequest.CacheableSystemPrompt` field declared + `ClaudeProvider` serializes `cache_control` | `Shared.Contracts/Ai/AiRequest.cs` (line 32), `ClaudeProvider.cs` (`BuildRequestBody`, lines ~164-173) — **gateway does NOT yet populate it** |
| Curriculum module: entities + EF config + `InitialCurriculum` migration + seeder (placeholder vecs) | `Curriculum.Infrastructure/Migrations/InitialCurriculum.cs`, `Persistence/Seed/CurriculumChunkSeeder.cs` |
| `BgeM3EmbeddingProvider` (TEI typed HttpClient, fail-soft; parity guard is warn-only today) | `Curriculum.Infrastructure/Services/BgeM3EmbeddingProvider.cs` |
| `RetrieveChunksQueryHandler` (pgvector JOIN + Active-version filter + similarity floor) | `Curriculum.Infrastructure/Features/Retrieval/Queries/RetrieveChunks/RetrieveChunksQueryHandler.cs` |
| `RagContextProvider` (ILearningContextProvider impl, registered behind `AiHelper:ContextProvider="Rag"`) | `Curriculum.Infrastructure/Services/RagContextProvider.cs` |
| Redis infra (`IConnectionMultiplexer` + `IDistributedCache`) in Host, Gamification Redis/Null pattern | `Host/Program.cs` (lines 56-78), `Gamification.Infrastructure/Caching/RedisGamificationCache.cs` |
| Hangfire wiring + Gamification jobs as reference pattern | `Gamification.Infrastructure/Jobs/GamificationCacheRebuildJob.cs` |
| `AiTutorRateLimiter` (in-process `ConcurrentDictionary`, used by all 4 handlers today) | `Ai.Application/Services/AiTutorRateLimiter.cs` |
| `RetrievalController` (dev-only, already shipped) | `Curriculum.Api/Controllers/RetrievalController.cs` |

---

## Task inventory

> Legend: [B] = buildable here (no live keys/TEI needed); [E] = external dep (devops/lead).
> Story/task ID remapping: this initiative does not have its own story IDs, so tasks are labelled by cluster and work-item (WI-A*, WI-B*, WI-C*) referencing the source task IDs.

### Cluster A — RAG/BGE-M3 re-embed

| ID | Task | Stack | Summary | Artifacts | Est (h) | Depends on | Tag |
|---|---|---|---|---|---|---|---|
| WI-A1 | Harden `BgeM3EmbeddingProvider` parity guard to fail-fast | backend-feature | Change the parity mismatch from warn-only to fail-fast (return `null` + structured error log) when configured `ModelVersion` does not match the stamped row version. Add startup-log of configured `BaseUrl`/`ModelVersion` status (absent → clear warning, no crash). | `Curriculum.Infrastructure/Services/BgeM3EmbeddingProvider.cs` | 1 | — | [B] |
| WI-A2 | Re-embed Hangfire command + job (P3-07-BE-0 buildable side) | backend-feature | New Hangfire job `ReEmbedCurriculumJob` in `Curriculum.Infrastructure/Jobs/`: selects `chunk_embeddings_bge_m3` rows where `ModelVersion = DeterministicEmbedding.PlaceholderModelVersion`, calls `IEmbeddingProvider.EmbedAsync` per chunk, upserts with live `Model`/`ModelVersion`/`Provider` + `IsActive=true`. Idempotent (re-run = no-op). Parity-stamped (job reads `EmbeddingSettings.ModelVersion`). Admin-trigger endpoint `POST /api/Curriculum/ReEmbed` (`[Authorize]`, admin policy) fires the job. Mirror `GamificationCacheRebuildJob` + `IServiceScopeFactory` pattern. Register as Transient in `AddCurriculumInfrastructure`; wire `RecurringJob`/admin trigger in `CurriculumModule.InitializeAsync`. Unit-test with stub `IEmbeddingProvider` returning deterministic 1024-dim vectors: row count of placeholder rows drops to 0; re-run = idempotent. | `Curriculum.Infrastructure/Jobs/ReEmbedCurriculumJob.cs`, `Curriculum.Api/Controllers/ReEmbedController.cs` (admin), `Curriculum.Infrastructure/DependencyInjection.cs` (DI), `Curriculum.Api/CurriculumModule.cs` (InitializeAsync) | 5 | WI-A1 | [B] |
| WI-A3 | Floor re-calibration + config-binding | backend-feature | Confirm `Curriculum:Retrieval:SimilarityDistanceFloor` is config-bound (already is via `RetrievalSettings`). Document in appsettings that the current value was tuned to placeholder hash geometry and MUST be re-calibrated after real BGE-M3 vectors are live. Add appsettings comment + HANDOFF note. No code change to `RetrieveChunksQueryHandler` — the setting already reads from config. | `Curriculum.Infrastructure/Features/Retrieval/RetrievalSettings.cs` (doc only), `backend/src/Host/Learnexia.Host/appsettings.json` (add comment), `docs/dev/HANDOFF.md` | 0.5 | WI-A1 | [B] |
| WI-A0 | TEI server provisioning (P3-07-BE-0 devops side) | — | Stand up BGE-M3 TEI on Hetzner (docker-compose/deploy entry), pinned `Model`/`ModelVersion`, health endpoint, auth token in secret store, smoke test (single short-text → 1024-d vector < 4 s). Record base URL + model/version in HANDOFF + `EmbeddingSettings`. This is NOT built by the backend pipeline. | Hetzner deployment config, secret store, `docs/dev/HANDOFF.md` | — | — | [E] devops |

### Cluster B — AiResponseCache + IGlobalSettingsProvider + Redis rate limiter

| ID | Task | Stack | Summary | Artifacts | Est (h) | Depends on | Tag |
|---|---|---|---|---|---|---|---|
| WI-B1 | `IGlobalSettingsProvider` seam + bootstrap impl (P3-01-BE-15) | backend-feature | Declare `IGlobalSettingsProvider` in `Shared.Contracts/` (typed getters `GetDecimal`, `GetInt`, `GetString`, `GetBool`). Implement `BootstrapDefaultGlobalSettingsProvider` in `Shared.Infrastructure/Settings/` (new project or add to `Shared.Kernel` — see note below) returning hardcoded bootstrap defaults for every key. Register as the default `IGlobalSettingsProvider` in Host `Program.cs` (serialized shared-file). **Note on project placement:** `Shared.Infrastructure` does not currently exist as a project (glob found no files); add `BootstrapDefaultGlobalSettingsProvider` to `Shared.Kernel` or a new thin `Shared.Infrastructure` project — choose `Shared.Kernel` unless the lead wants a clean separation. Confirm with the brief's intent; if new project is needed, add to `.sln` (serialized). | `Shared.Contracts/IGlobalSettingsProvider.cs`, `Shared.Kernel/Settings/BootstrapDefaultGlobalSettingsProvider.cs`, `Host/Program.cs` (DI wire) | 2 | — | [B] |
| WI-B2 | `ai.AiResponseCache` migration (DB table) | db-migration | New EF entity `AiResponseCache` in `Ai.Domain/Entities/`; EF config `AiResponseCacheConfig` in `Ai.Infrastructure/Persistence/Configurations/`; add `DbSet<AiResponseCache>` to `AiDbContext`; generate migration `AddAiResponseCacheTable`. Table/columns per brief §4 / cost-routing §5: `Id bigserial`, `CacheKey varchar(512) UNIQUE`, `Response text`, `Type smallint`, `SkillKey varchar(256)`, `QuestionId int?`, `CurriculumVersion varchar(64)`, `PromptVersion varchar(32)`, `ModelVersion varchar(100)`, `ReviewStatus smallint` (0=PendingReview/1=Approved/2=Rejected), `Confidence decimal(5,4)?`, `CreatedAt timestamptz`, `ApprovedBy int?`, `ApprovedAt?`, `InvalidatedAt?`. Indexes: UNIQUE(`CacheKey`); `(Type, ReviewStatus)`; `(SkillKey, CurriculumVersion)`; `(QuestionId)`. Enum columns `HasConversion<int>()`. No cross-module FKs. Mirror `SafetyEventConfig`. | `Ai.Domain/Entities/AiResponseCache.cs`, `Ai.Infrastructure/Persistence/Configurations/AiResponseCacheConfig.cs`, `AiDbContext.cs` (DbSet), `Ai.Infrastructure/Migrations/*AddAiResponseCacheTable*` | 3 | — | [B] |
| WI-B3 | `IAiResponseCache` + Redis/DB read-through impl | backend-feature | Interface `IAiResponseCache` in `Shared.Contracts/Ai/`: `GetApprovedAsync(cacheKey, ct)`, `WriteAsync(entry, ct)`. Implement `AiResponseCacheRepository` in `Ai.Infrastructure/Cache/`: on GET check Redis (`IDistributedCache`) first (TTL per intent: Explain/SimilarExample 24 h; Hint 12 h; WhyWrong by compound key 6 h); on Redis miss check DB (`AiDbContext`) WHERE `ReviewStatus=Approved AND InvalidatedAt IS NULL`; on DB hit populate Redis. CacheKey = `SHA256(canonical_tuple_json)` per type (cost-routing §5). On WRITE: DB upsert first, then set Redis key. Redis key uses `IDistributedCache` (already wired in Host); **no new `IConnectionMultiplexer` usage here** — mirror Gamification pattern. Redis absent (no connection string) = null `IDistributedCache`? No — `IDistributedCache` is always registered (in-memory fallback when Redis absent, per Host Program.cs). Use it directly. `WhyWrong` LRU cap: read `whyWrongVariantCap` via `IGlobalSettingsProvider`; if at cap, evict oldest (by `CreatedAt`) before writing. Register `IAiResponseCache` in `AddAiInfrastructure`. | `Shared.Contracts/Ai/IAiResponseCache.cs`, `Ai.Infrastructure/Cache/AiResponseCacheRepository.cs`, `Ai.Infrastructure/DependencyInjection.cs` | 6 | WI-B1, WI-B2 | [B] |
| WI-B4 | Wire cache-first + cache-write into the 4 handlers (uncomment DEFERRED seams) | backend-feature | In each of the 4 handlers (`ExplainConceptCommandHandler`, `GetHintCommandHandler`, `SimilarExampleCommandHandler`, `SimplifyExplanationCommandHandler`): (1) before the safety call, compute `CacheKey` per type/intent using the resolved inputs, call `IAiResponseCache.GetApprovedAsync` — on HIT return the cached content directly (no gateway call, no safety re-run); (2) after safety passes, compute `ReviewStatus` = `Confidence >= IGlobalSettingsProvider.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m) ? Approved : PendingReview`; fire-and-forget `IAiResponseCache.WriteAsync`. The current student always receives the safety-passed response regardless of `ReviewStatus`. Update `HelpDelivered` event: `ContextSource = "Cache"` on HIT vs `"Live"` on miss. Inject `IAiResponseCache` and `IGlobalSettingsProvider` in each handler constructor. | `Ai.Application/Features/{Explain,Hint,SimilarExample,Simplify}/Commands/*Handler.cs` (4 files) | 5 | WI-B1, WI-B3 | [B] |
| WI-B5 | Redis rate limiter (replace in-process `AiTutorRateLimiter`) | backend-feature | Create `RedisAiRateLimiter` in `Ai.Infrastructure/Services/` using `IDistributedCache` for a fixed-window SETNX counter (key = `"ai:rl:{studentId}:{windowMinute}"`; TTL = window duration). Keep `TryAllow(studentId)` signature so all 4 handlers call-site is unchanged (swap out `AiTutorRateLimiter` for a new `IAiTutorRateLimiter` interface, or replace the concrete class). When `IConnectionMultiplexer` is present prefer it for atomic `INCR` + `EXPIRE` over `IDistributedCache` for correctness under concurrency; when absent (Redis not configured) fall back to in-process `ConcurrentDictionary` behavior — mirror Gamification Redis/Null selector pattern. Register in `AddAiInfrastructure` (replace singleton `AiTutorRateLimiter`). Introduce `IAiTutorRateLimiter` seam in `Ai.Application/Services/` so handlers reference the interface, not the concrete class. | `Ai.Application/Services/IAiTutorRateLimiter.cs`, `Ai.Infrastructure/Services/RedisAiRateLimiter.cs`, `Ai.Infrastructure/Services/InProcessAiRateLimiter.cs` (extracted fallback), `Ai.Infrastructure/DependencyInjection.cs`, 4 handler files (swap injection type) | 3 | WI-B1 | [B] |
| WI-B6 | Provider keys / Claude API keys | — | Inject `Ai:Providers:Claude:ApiKey` + `Ai:Providers:OpenAI:ApiKey` via secret store / env. Code already reads from `IConfiguration` — no code change. | Secret store / env | — | — | [E] devops |

### Cluster C — Grounding flip + config wiring + prompt-cache (BE-12)

| ID | Task | Stack | Summary | Artifacts | Est (h) | Depends on | Tag |
|---|---|---|---|---|---|---|---|
| WI-C1 | Prompt-cache population — gateway fills `CacheableSystemPrompt` (P3-01-BE-12) | backend-feature | In `AiGateway.CompleteAsync` (or in the prompt builder), populate `AiRequest.CacheableSystemPrompt` with the static child-safe system-prompt prefix when a `SystemPrompt` is present on the request. The `ClaudeProvider.BuildRequestBody` already serializes it to `cache_control: ephemeral`. The gateway receives the request already built by the prompt builder; the simplest place to populate is in `AiGateway` before forwarding, or in `PromptBuilder.Build` (latter is cleaner). Confirm with the existing `PromptBuilder` — add the population there. Unit-test: assert `BuildRequestBody` includes the `system` block with `cache_control` when `CacheableSystemPrompt` is non-null. Note Haiku ~4096-token cache-prefix floor; short prompts will not trigger prompt-caching — this is expected and acceptable. | `Ai.Application/PromptBuilder/PromptBuilder.cs` or `Ai.Infrastructure/Gateway/AiGateway.cs`, `ClaudeProvider.cs` (already correct), tests | 3 | — | [B] |
| WI-C2 | Config wiring + appsettings + DI audit | backend-feature | Verify/add all new config keys to `appsettings.json` with comment-only defaults (no live values committed): `AiHelper:ContextProvider` (default omitted → EmptyLearningContextProvider), `Curriculum:Embedding:BaseUrl`, `Curriculum:Embedding:AuthToken` (empty), `Curriculum:Embedding:Model`, `Curriculum:Embedding:ModelVersion`, `Curriculum:Retrieval:SimilarityDistanceFloor` (0.4 placeholder — add RECALIBRATE comment), `ConnectionStrings:Redis` (empty). Confirm no secret in any committed file. Add DI registration for `IGlobalSettingsProvider` in Program.cs (serialized). Validate DI graph resolves (build + start). | `Host/Program.cs` (serialized), `Host/appsettings.json`, `Host/appsettings.Development.json` | 2 | WI-B1 | [B] |
| WI-C3 | End-to-end config-flip documentation + HANDOFF update | backend-feature | Update `docs/dev/HANDOFF.md`: (a) re-embed runbook (run `POST /api/Curriculum/ReEmbed` after TEI is live; wait for job completion; verify placeholder row count = 0); (b) grounding flip (`AiHelper:ContextProvider = "Rag"` in appsettings after re-embed); (c) cache key/TTL matrix; (d) Redis rate-limiter config; (e) similarity floor re-calibration instruction; (f) what remains external (TEI endpoint, API keys); (g) BE-13/BE-14 deferral notice. | `docs/dev/HANDOFF.md` | 1 | all buildable tasks | [B] |

### Deferred (explicitly NOT in this initiative)

| Task | Reason | Target |
|---|---|---|
| **P3-01-BE-13** `IAiBatchGateway` / `ClaudeBatchProvider` (offline batch pre-generation) | OQ-2 decision: cold-start cache-fill is out of MVP scope; runtime write-through cache fills on misses | Phase 10 |
| **P3-01-BE-14** `IAiUsageBudget` / daily cap / `QuotaExhausted` | OQ-2 decision: deferred; the Redis rate-limiter (WI-B5) is the MVP cost-safety guard | Phase 10 |
| Human-moderation review-gate table / `PendingReview` workflow / invalidation triggers | OQ-1 decision: deferred full §4 review gate; MVP auto-approves on confidence threshold | Phase 10 (P7-09-style) |
| `AiUsageLogs` persistence | Already deferred to P7-11 | P7-11 |
| Full BL curriculum ingestion pipeline (BL-01..05) | Out of scope for this initiative | Backlog |
| P10-12 DB-backed `IGlobalSettingsProvider` upgrade | Bootstrap impl ships here; full upgrade in Phase 10 | P10-12 |

---

## Dependency order

```
(no prerequisite stories — P3-01..P3-07 already merged)

Shared-file serialization points (MUST NOT be edited concurrently):
  Program.cs  ·  Learnexia.Modular.sln  ·  appsettings.json  ·  Directory.Packages.props

Independent roots:
  WI-A1  (harden parity guard)
  WI-B1  (IGlobalSettingsProvider — Shared.Contracts + bootstrap, Program.cs write)
  WI-C1  (prompt-cache population — Ai.Application/PromptBuilder only)

  WI-A1 ──► WI-A2 (re-embed job — depends on hardened parity guard)
  WI-A2 ──► WI-A3 (floor note — depends on job being complete)

  WI-B1 ──► WI-B2 (migration — logically independent but WI-B1 must land in Program.cs first
                     to avoid a second shared-file conflict; serialize after WI-B1 Program.cs write)
  WI-B2 ──► WI-B3 (cache repo — needs entity + migration)
  WI-B1, WI-B3 ──► WI-B4 (handler wiring — needs interface + repo)
  WI-B1 ──► WI-B5 (Redis rate limiter — needs IAiTutorRateLimiter seam in Shared.Contracts or Application)

  WI-B1 ──► WI-C2 (config wiring — needs IGlobalSettingsProvider DI wire in Program.cs)
  all buildable ──► WI-C3 (HANDOFF — written last, after all code is done)
```

---

## Execution batches

### Batch 0 (pre-flight — already done)
analyzer produced the Pipeline Brief. This plan is Batch 0 output. No code written.

---

### Batch 1 — Parallel roots (3 independent backend-feature tasks)
**Mode: parallel (Mode A). No shared-file contention among themselves at start — Program.cs is touched only by WI-B1; WI-A1 and WI-C1 do not touch it.**

| Agent | Tasks | Files (primary) | Parallel? |
|---|---|---|---|
| `backend-feature` instance A | WI-A1: harden `BgeM3EmbeddingProvider` parity to fail-fast | `Curriculum.Infrastructure/Services/BgeM3EmbeddingProvider.cs` | YES, with B and C |
| `backend-feature` instance B | WI-B1: `IGlobalSettingsProvider` seam + bootstrap impl + Program.cs wire | `Shared.Contracts/IGlobalSettingsProvider.cs`, `Shared.Kernel/Settings/BootstrapDefaultGlobalSettingsProvider.cs`, `Host/Program.cs` | YES, with A and C (owns Program.cs this batch) |
| `backend-feature` instance C | WI-C1: prompt-cache `CacheableSystemPrompt` population in PromptBuilder | `Ai.Application/PromptBuilder/PromptBuilder.cs`, tests | YES, with A and B |

**Gate after Batch 1:** `reviewer` reviews all three changes. `security-auditor` is deferred until Batch 3 (the security-sensitive work arrives there). Reviewer checks: parity fail-fast behavior, no secret in logs, CONVENTIONS.md (ILoggerManager, BaseResponse, no UoW), arch test still green.

**Acceptance check (all mocked — no live keys/TEI):**
- AC-A4: parity mismatch → provider returns null + logs structured warning (no crash).
- AC-A5: absent `BaseUrl` → null vector → retrieval empty → handler redirects.
- AC-E1: `AiModuleArchTests` still green (WI-C1 touches `Ai.Application` only, not `IAiGateway`).

---

### Batch 2 — Sequential: db-migration (AiResponseCache table)
**Mode: sequential after Batch 1 (needs WI-B1 to be merged so shared files are settled). Single agent.**

| Agent | Tasks | Files (primary) |
|---|---|---|
| `db-migration` | WI-B2: `ai.AiResponseCache` entity + EF config + migration | `Ai.Domain/Entities/AiResponseCache.cs`, `Ai.Infrastructure/Persistence/Configurations/AiResponseCacheConfig.cs`, `AiDbContext.cs`, `Ai.Infrastructure/Migrations/*AddAiResponseCacheTable*` |

**Gate after Batch 2:** `reviewer` verifies: migration applies against pg17 pgvector compose image, UNIQUE on `CacheKey`, 4 indexes, no cross-module FK, enum `HasConversion<int>()`, mirrors `SafetyEventConfig` conventions.

---

### Batch 3 — Parallel feature work (cache repo + handler wiring + rate limiter + re-embed job)
**Mode: parallel where file sets are disjoint. WI-B3/WI-B4 touch `Ai.*`; WI-B5 touches `Ai.*` (rate limiter seam) but different files; WI-A2 touches `Curriculum.*`. All are independent of each other's files EXCEPT WI-B4 depends on WI-B3's interface.**

**Dispatch order within Batch 3:**
- Sub-batch 3a (parallel): WI-B3 (cache repo) + WI-B5 (Redis rate limiter) + WI-A2 (re-embed job)
- Sub-batch 3b (after 3a): WI-B4 (handler wiring — needs WI-B3 interface)

| Agent | Tasks | Files (primary) | Parallel sub-batch |
|---|---|---|---|
| `backend-feature` A | WI-B3: `IAiResponseCache` + `AiResponseCacheRepository` (Redis/DB read-through) | `Shared.Contracts/Ai/IAiResponseCache.cs`, `Ai.Infrastructure/Cache/AiResponseCacheRepository.cs`, `Ai.Infrastructure/DependencyInjection.cs` | 3a |
| `backend-feature` B | WI-B5: Redis rate limiter (introduce `IAiTutorRateLimiter`, `RedisAiRateLimiter`, in-process fallback) | `Ai.Application/Services/IAiTutorRateLimiter.cs`, `Ai.Infrastructure/Services/RedisAiRateLimiter.cs`, `Ai.Infrastructure/Services/InProcessAiRateLimiter.cs`, `Ai.Infrastructure/DependencyInjection.cs` | 3a (disjoint files from WI-B3) |
| `backend-feature` C | WI-A2: `ReEmbedCurriculumJob` + admin trigger endpoint | `Curriculum.Infrastructure/Jobs/ReEmbedCurriculumJob.cs`, `Curriculum.Api/Controllers/ReEmbedController.cs`, `Curriculum.Infrastructure/DependencyInjection.cs`, `Curriculum.Api/CurriculumModule.cs` | 3a (disjoint module) |
| `backend-feature` D | WI-B4: wire cache-first + cache-write into 4 handlers | `Ai.Application/Features/{Explain,Hint,SimilarExample,Simplify}/Commands/*Handler.cs` (4 files), `Ai.Application/DependencyInjection.cs` | 3b (after WI-B3 interface is merged) |

**Note on `Ai.Infrastructure/DependencyInjection.cs` contention:** WI-B3 and WI-B5 both register into `AddAiInfrastructure`. If dispatched truly in parallel, serialize their edits to that file by having one agent add a placeholder section and the other agent fill it in a subsequent pass — or (simpler) dispatch WI-B3 first, merge it, then dispatch WI-B5. The lead should serialize these two specifically.

**Gate after Batch 3:** `api-tester` + `security-auditor` (MANDATORY — AI prompts, child data, cache-as-safety-bypass risk, secret leakage) + `reviewer`. This is the main gate.

**Acceptance checks (mocked `IAiGateway` / embedding / TEI — no live keys):**
- AC-B1: repeat request → zero `IAiGateway` calls on HIT (mock gateway assert).
- AC-B2: miss → store (Approved if Confidence ≥ 0.85) → second request = HIT.
- AC-B3: `PendingReview` / `Invalidated` entries NOT served as cache hits.
- AC-B4: Redis rate limiter counter stored in `IDistributedCache`; Redis absent → in-process fallback (no crash).
- AC-B5: thresholds read via `IGlobalSettingsProvider`, not hardcoded.
- AC-B6: no Redis configured → AiResponseCache degrades (falls through to DB only; IDistributedCache = in-memory); no crash.
- AC-A1: re-embed job with stub `IEmbeddingProvider` → placeholder row count drops to 0; idempotent re-run.
- AC-E1: `AiModuleArchTests` still green (no `IAiGateway` reference outside `Ai.Infrastructure` + `SafetyLayer`; cache repo never calls `IAiGateway` directly).

---

### Batch 4 — Config wiring + appsettings + floor note
**Mode: sequential (after Batch 3 is reviewer-PASS; touches Program.cs — serialized shared file).**

| Agent | Tasks | Files (primary) |
|---|---|---|
| `backend-feature` | WI-C2: config wiring (DI audit, appsettings keys, confirm no secret committed) + WI-A3 (floor note in appsettings + HANDOFF note) | `Host/Program.cs` (minimal — IGlobalSettingsProvider already wired in Batch 1), `Host/appsettings.json`, `Host/appsettings.Development.json`, `Curriculum.Infrastructure/Features/Retrieval/RetrievalSettings.cs` (doc) |

**Gate after Batch 4:** `api-tester` (end-to-end config-flip path with mocked gateway + RAG + deterministic stubs) + `reviewer`.

**Acceptance checks:**
- AC-C1: `AiHelper:ContextProvider="Rag"` → resolved provider is `RagContextProvider` (not stub). Default (absent key) → `EmptyLearningContextProvider`.
- AC-C2: with RAG + mocked gateway, in-corpus Explain → `HelpDelivered` (not redirect).
- AC-C3: out-of-corpus → `HelpDeclined{NoContext}` + redirect.
- AC-D1/D2: every external dep is config-bindable; absent key/endpoint → degrade path (no crash, structured log).
- AC-D3: no secret in logs / error bodies / appsettings.

---

### Batch 5 — HANDOFF update
**Mode: sequential (last, after all code is reviewer-PASS).**

| Agent | Tasks | Files (primary) |
|---|---|---|
| `backend-feature` | WI-C3: HANDOFF.md update (re-embed runbook, grounding flip, cache key/TTL matrix, Redis limiter, floor recalibration note, what's still external, BE-13/BE-14 deferral) | `docs/dev/HANDOFF.md` |

**Gate after Batch 5:** `reviewer` (HANDOFF completeness check per CLAUDE.md protocol).

---

### Batch 6 — Committer (per story branch, wave PR)
**After reviewer PASSES all batches.**

Wave branch: `feat/phase4-ai-runtime` (cut off `main`).
Per-task story branches (cut off wave branch, `--no-ff` merged back):
- `feat/ai-rag-reembed` (WI-A1 + WI-A2 + WI-A3)
- `feat/ai-response-cache` (WI-B1 + WI-B2 + WI-B3 + WI-B4)
- `feat/ai-redis-ratelimiter` (WI-B5)
- `feat/ai-prompt-cache` (WI-C1)
- `feat/ai-config-wiring` (WI-C2)

`committer` opens one wave PR (`feat/phase4-ai-runtime → main`) for the lead to merge. Never merges the PR itself.

---

## Review gates summary

| After | Gate agents | Blocks if |
|---|---|---|
| Batch 1 | `reviewer` | CONVENTIONS violation, arch test regression, parity guard behavior wrong |
| Batch 2 | `reviewer` | Migration DDL wrong, index missing, cross-module FK introduced |
| Batch 3 | `api-tester` → `security-auditor` (MANDATORY) → `reviewer` | Any Critical/High security finding; AC-B1..B6 / AC-A1 / AC-E1 failing |
| Batch 4 | `api-tester` → `reviewer` | AC-C1..C3, AC-D1..D3 failing |
| Batch 5 | `reviewer` | HANDOFF.md missing required sections |

**Security-auditor scope (Batch 3):** cache-as-safety-bypass (R2: only `Approved + non-invalidated` entries served), secret leakage in logs/error bodies/responses (R3), AI prompt child-data exposure, embedding-text exposure in error paths, parity guard fail-fast correctness (R1), `IAiGateway` no-bypass invariant (arch test must pass). Critical/High findings block the reviewer gate.

---

## Blockers / external dependencies

The following are NOT built by this pipeline. The plan reaches "merged + green" without them — every affected code path degrades gracefully when they are absent.

| # | Blocker | Who clears | Impact when absent (handled by code) |
|---|---|---|---|
| EXT-1 | **TEI server (BGE-M3 1024-d) on Hetzner** — provisioned endpoint, pinned `Model`/`ModelVersion`, auth token, health endpoint (P3-07-BE-0 devops) | Lead / devops | `BgeM3EmbeddingProvider.EmbedAsync` returns `null`; retrieval returns empty; all 4 handlers redirect (AC-A5). App does not crash. |
| EXT-2 | **Claude API key** (`Ai:Providers:Claude:ApiKey`) | Lead / devops (secret store / env) | `ClaudeProvider` returns `AiError.Unavailable`; `SafetyLayer` fails-closed (fallback); handlers return typed error. No crash. |
| EXT-3 | **OpenAI API key** (`Ai:Providers:OpenAI:ApiKey`) | Lead / devops | Same as EXT-2 for OpenAI tier. |
| EXT-4 | **Similarity floor re-calibration** (`Curriculum:Retrieval:SimilarityDistanceFloor`) | Lead (after TEI is live and real vectors are embedded) | Current 0.4 was tuned to placeholder hash geometry; real BGE-M3 vectors will have different distance distribution. Too-tight floor → over-redirects (acceptable degradation). Too-loose floor → irrelevant chunks in context (retrieval quality issue). Must be re-calibrated by the lead against real corpus after EXT-1 + re-embed are complete. |
| EXT-5 | **Redis connection string** (`ConnectionStrings:Redis`) | Devops / compose already has `redis:6379` | Without Redis: `IDistributedCache` falls back to in-process memory (already wired in Host). Cache still works (in-memory); rate limiter falls back to in-process `InProcessAiRateLimiter`. No Redis multi-instance coordination. Acceptable for single-instance dev. |

**After EXT-1 + EXT-2 are provisioned, the lead flips the following (no code change):**
1. Set `Curriculum:Embedding:BaseUrl` + `Curriculum:Embedding:AuthToken` + `Curriculum:Embedding:ModelVersion` (from TEI provisioning docs).
2. Trigger re-embed: `POST /api/Curriculum/ReEmbed` (admin-authed).
3. Re-calibrate `Curriculum:Retrieval:SimilarityDistanceFloor` against real corpus.
4. Set `AiHelper:ContextProvider = "Rag"` to activate live grounding.
5. Verify `POST /api/Ai/Explain` end-to-end (cache miss → live generation → cache write → second call = cache HIT).

---

## Definition of done

### Per batch

| Batch | Done when |
|---|---|
| Batch 1 | `BgeM3EmbeddingProvider` parity guard returns null on mismatch (not just warns); `IGlobalSettingsProvider` resolves from DI with bootstrap defaults; `AiRequest.CacheableSystemPrompt` is populated by `PromptBuilder`; `ClaudeProvider` serializes `cache_control`; tests pass; `reviewer` PASS. |
| Batch 2 | `ai.AiResponseCache` migration applies on pg17 without errors; entity config matches §4 column spec exactly; `reviewer` PASS. |
| Batch 3 | All 4 handlers serve from cache on HIT (zero gateway calls asserted); safety-passed miss → stored as Approved/PendingReview per threshold; `RedisAiRateLimiter` stores counter in `IDistributedCache`; in-process fallback when Redis absent; re-embed job processes all placeholder rows (mock client); `api-tester` + `security-auditor` + `reviewer` all PASS; no Critical/High security findings. |
| Batch 4 | `AiHelper:ContextProvider="Rag"` resolves `RagContextProvider`; absent key resolves `EmptyLearningContextProvider`; no secret in committed appsettings; all config keys documented; `api-tester` + `reviewer` PASS. |
| Batch 5 | `HANDOFF.md` contains: re-embed runbook, grounding flip instruction, cache key/TTL matrix, Redis limiter config, floor recalibration note, external dep list, BE-13/BE-14 deferral notice; `reviewer` PASS. |
| Batch 6 | Wave PR open on `feat/phase4-ai-runtime → main`; per-story branches pushed; `committer` reports PR URL. |

### Overall (story-level acceptance)

All of the following pass with mocked `IAiGateway` / embedding / TEI (no live keys required):

- **AC-A1..A5** (re-embed + retrieval + parity + degrade)
- **AC-B1..B6** (cache HIT/miss, R5 gate, Redis limiter, thresholds via IGlobalSettingsProvider, degrade)
- **AC-C1..C3** (config-driven grounding flip, end-to-end loop with mocked gateway, out-of-corpus redirect)
- **AC-D1..D3** (config/secrets wiring, graceful-degrade matrix, no secret leakage)
- **AC-E1..E2** (arch tests still green, SafetyLayer remains sole content exit, cache never bypasses safety)

### Deferred — confirmed out of scope for this wave

- BE-13 (`IAiBatchGateway`) — Phase 10
- BE-14 (`IAiUsageBudget` / daily cap) — Phase 10
- Human-moderation `PendingReview` workflow — Phase 10 (P7-09-style)
- `AiUsageLogs` DB persistence — P7-11
- Full BL ingestion pipeline — BL-01..05 backlog

---

## New blockers (not the already-resolved OQs)

1. **`Shared.Infrastructure` project does not exist.** The brief calls for `BootstrapDefaultGlobalSettingsProvider` in `Shared.Infrastructure`. Glob found no such project. The `backend-feature` agent for WI-B1 must either (a) place the class in `Shared.Kernel/Settings/` (already a project, no `.sln` edit needed) or (b) create a new `Shared.Infrastructure` project (requires `.sln` edit — serialized shared file). **Recommendation:** use `Shared.Kernel` to avoid a new project; the bootstrap impl is trivial (no DB/Redis deps) and `Shared.Kernel` already ships with the stack. Lead confirms this placement before dispatching Batch 1.

2. **`Ai.Infrastructure/DependencyInjection.cs` shared-file contention in Batch 3.** WI-B3 and WI-B5 both register services in `AddAiInfrastructure`. **Recommendation:** serialize WI-B3 first (merge it), then dispatch WI-B5. Lead to dispatch in this order within Batch 3 rather than true-parallel for those two tasks. WI-A2 (Curriculum module) is still parallel to both.

3. **`IConnectionMultiplexer` is nullable in tests.** Host Program.cs only registers `IConnectionMultiplexer` when `ConnectionStrings:Redis` is set. The Redis rate limiter (WI-B5) must inject `IConnectionMultiplexer?` (nullable) — if it calls `.GetRequiredService<IConnectionMultiplexer>()` the integration tests will throw when Redis is absent. Agent must mirror Gamification's Redis/Null selector (conditional registration) rather than direct injection. Flag for the `reviewer` to verify.

4. **Similarity floor value is a risk, not a blocker.** The current 0.4 was calibrated to hash-vector geometry. After real BGE-M3 re-embed, the floor must be re-calibrated by the lead (EXT-4 above). No code change needed — the value is config-bound. This is documented in HANDOFF (WI-C3) and appsettings (WI-A3) so the lead cannot miss it.

---

Plan ready — dispatch Batch 1.
