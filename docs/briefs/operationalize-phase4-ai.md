# Pipeline Brief — Operationalize Phase-4 AI

> **Status: DRAFT for lead review.** This is an *initiative-level* brief (it spans several stories/tasks: P3-07-BE-0, the deferred P3-01-BE-12/13/14/15, the AiResponseCache from `ai-cost-routing.md`, the grounding-provider flip, and the Redis rate limiter). It does **not** replace the per-story briefs/plans for P3-01 and P3-07 — it sequences the *runtime wiring + completion* that turns the already-merged AI Tutor from inert to live.
>
> **Material ambiguity exists** (see Open Questions). Several items (OQ-1 cache TTL/key authority, OQ-2 BE-12/13/14 scope, OQ-3 default live provider, OQ-7 re-embed host) should be resolved by the lead **before** the planner cuts the executable plan. Recommend the lead reads §"Open questions" first.

---

## Summary & traceability

**One-liner:** Wire and complete the runtime around the already-built 4-intent AI Helper — activate semantic RAG (real BGE-M3 embeddings + re-embed), add the application-level `AiResponseCache` + move the rate limiter to Redis, flip live grounding on via config, and make every provider key / TEI endpoint config-driven and absence-tolerant — so the AI Tutor actually runs end-to-end **without** scoping payments/credits/analytics.

| Dimension | Value |
|---|---|
| User stories / tasks | **P3-07** (RAG retrieval — `P3-07-BE-0` real BGE-M3 TEI + re-embed), **P3-01** (deferred `BE-12` prompt-cache completion, `BE-13` Batch gateway, `BE-14` daily-cap budget, `BE-15` `IGlobalSettingsProvider`), **`AiResponseCache`** (from `docs/briefs/ai-cost-routing.md §4/§5`), **grounding-provider flip** (`ai-helper-mvp.md §3.1`), **Redis rate limiter** (replaces `AiTutorRateLimiter`) |
| FR-IDs | FR-AI-3 (RAG retrieval), FR-AI-4 (safety — invariant preserved, not changed), FR-AI-6 (gateway returns content only), FR-CI-4 (curriculum context) |
| NFR-IDs | **NFR-1** (< 4 s/AI task — embedding + retrieval + generation budget), NFR-2/NFR-4/NFR-8/NFR-9 (gateway resilience/secrets), cost-economy NFR (per-student AI COGS under the 199 EGP plan — `ai-cost-routing.md §1`) |
| BRD goal | **G1** (personalized adaptive learning — AI Helper is the differentiating feature), **G4** (unit economics / business viability — the cache is the primary cost lever) |
| Phase / epic | Phase 4 — AI Tutor. Epics: AI Gateway (P3-01), RAG Retrieval (P3-07). Cross-cutting: AI Cost & Model Routing strategy. |
| Product overrides honored | 4 subjects (Math/Science/Arabic/English — seeded corpus has no Social Studies); no teacher role; parent-driven onboarding (student profile via JWT claims) |

**Scope boundary (lead-approved):** make AI value *real* first. **Explicitly DEFERRED and NOT in this initiative:** payments / credit ledger / subscriptions (Phase 10, `P10-*`), analytics dashboards (Phase 5 / P7-10). The minimal **daily request cap** (BE-14) is a cost-safety guardrail only — it counts requests, not money, and carries no per-action pricing.

---

## Business context & value

The AI Helper (Explain / Hint / WhyWrong / SimilarExample) is the product's most differentiating capability (BRD G1) and is **fully built and merged but inert**:

- It always **refuses-and-redirects** because the default `ILearningContextProvider` is `EmptyLearningContextProvider` (returns empty chunks → every intent redirects). The HANDOFF calls this "live grounding dormant."
- RAG (P3-07) is mechanically complete but the seeded corpus carries **placeholder embeddings** (`DeterministicEmbedding`, stamped `ModelVersion = "seed-placeholder-v0"`) — a hash-seeded vector that is *not semantically meaningful*. Cosine search over it returns near-distance only for *identical text*, so it can't ground a real student question.
- There is **no application-level response cache**, so once AI goes live every request is a cache miss → unbounded LLM spend. `ai-cost-routing.md §1` is blunt: at 199 EGP/month, defaulting all calls to live LLM with no cache makes the unit economics non-viable (BRD G4). The Redis/`AiResponseCache` read-through is the single most powerful cost lever (a HIT = $0 AI tokens).
- The rate limiter (`AiTutorRateLimiter`) is **in-process only** (a `ConcurrentDictionary`), so it does not coordinate across instances — a known multi-instance gap flagged in HANDOFF.

**Who benefits:** the **student** (gets real grounded help instead of a redirect — closing the "wrong answer → AI help → retry → success" loop from `ai-helper-mvp.md §5`); the **business/parent** (AI COGS stays under the plan price; cached pedagogy is reviewed pedagogy per the R5 review gate). **Success measured by:** (a) grounded RAG returns real chunks for in-corpus skills and an empty signal for out-of-corpus (testable without live keys via the deterministic stub); (b) repeat AI requests served from cache without an `IAiGateway` call; (c) rate-limit state shared across instances via Redis; (d) the system **degrades, not crashes**, when keys/endpoints are absent.

---

## The invariants that MUST NOT break (load-bearing)

These are non-negotiable; any work item that touches the request path must preserve them, and the reviewer/security-auditor gate on them:

1. **Buffer → Safety → Emit / no-bypass.** Feature handlers call `ISafetyLayer.GenerateSafeAsync` — **NEVER `IAiGateway` directly**. `SafetyLayer` is the *only* class that calls `IAiGateway.CompleteAsync`. Enforced by the architecture test **`AiModuleArchTests`** (`P302-ARCH-04`: no type outside `Ai.Infrastructure` + `SafetyLayer` references `IAiGateway`). The full response is buffered and screened before any token is emitted. *Any cache-first lookup that returns a stored answer must still only serve content that previously passed safety (the R5 review gate) — the cache must not become a bypass of the safety layer.*
2. **Refuse-and-redirect = no grounding, no answer.** Empty `LearningContext.Chunks` → handler returns a templated redirect (EN+AR, with skill name), emits `HelpDeclined{Reason=NoContext}`, and does **not** call the LLM (`ai-helper-mvp.md §4`). Activating live grounding must not weaken this: out-of-corpus / below-floor retrieval must still produce the empty signal.
3. **Fail-closed / fail-soft.** `SafetyLayer` fails *closed* (any exception → blocked fallback, never unscreened content). The embedding provider and RAG handler fail *soft* (null/empty → empty result → redirect). Absent keys/endpoints must keep these contracts: degrade to redirect, never throw to the student.
4. **Seed ↔ runtime model parity (REQUIRED).** The `Model`/`ModelVersion` stamped on `chunk_embeddings_bge_m3` rows MUST equal the live TEI endpoint's served model/version, or cosine search is invalid (different vector spaces = garbage retrieval). The re-embed work item exists precisely to retire the `seed-placeholder-v0` rows and restore parity.
5. **Module isolation.** `Ai` references `Shared.Contracts` only (never Learning/Curriculum). The grounding seam (`ILearningContextProvider`) lives in `Shared.Contracts/AiTutor/`; `RagContextProvider` lives in `Curriculum.Infrastructure` and overrides the stub via DI registration order behind `AiHelper:ContextProvider`. No cross-module FKs (`CurriculumChunk` references the Learning hierarchy by plain int).
6. **No provider SDK leak + no secret in logs.** Provider SDK namespaces isolated to `Ai.Infrastructure` (arch test `P301-ARCH-01`). API keys sourced from `IConfiguration`/env only (`Ai:Providers:Claude:ApiKey`, `Curriculum:Embedding:AuthToken`), never committed, never logged.

---

## Current state of record (what is already built — do NOT rebuild)

| Capability | Status | Evidence |
|---|---|---|
| 4 intents (Explain/Hint/WhyWrong/Simplify-Simplify + SimilarExample) + handlers | **BUILT + merged** | `Modules/Ai/.../Features/{Explain,Hint,SimilarExample,Simplify}/...Handler.cs` |
| Safety layer (buffer→screen→emit, fail-closed) + arch tests | **BUILT** | `Ai.Application/Safety/SafetyLayer.cs`; `Modules.Ai.UnitTests/AiModuleArchTests.cs` |
| `IAiGateway` + `AiGateway` facade (router, retry, hard timeout, typed `AiError`, usage log-only) | **BUILT** | `Ai.Infrastructure/Gateway/AiGateway.cs` |
| `ClaudeProvider` + `OpenAiProvider` (thin typed HttpClient, key from config) | **BUILT** | `Ai.Infrastructure/Providers/*.cs` |
| **Prompt-caching `cache_control`** on the system-prompt prefix | **PARTIALLY BUILT** — `AiRequest.CacheableSystemPrompt` exists and `ClaudeProvider` serializes it as `cache_control: ephemeral`. **Gateway does not yet populate it** from the prompt builder. | `Shared.Contracts/Ai/AiRequest.cs` (field); `ClaudeProvider.cs` lines ~164-173 |
| Curriculum module + `CurriculumChunk` / `CurriculumVersion` / `chunk_embeddings_bge_m3` schema + HNSW + migration | **BUILT** | `Curriculum.Infrastructure/Migrations/InitialCurriculum.cs`; `Persistence/Configurations/*` |
| `IEmbeddingProvider` + `BgeM3EmbeddingProvider` (TEI typed HttpClient, fail-soft, parity-guard warning) | **BUILT** | `Curriculum.Infrastructure/Services/BgeM3EmbeddingProvider.cs` |
| `RetrieveChunksQuery` handler (server-side pgvector `<=>` JOIN, Active-version filter, similarity floor) | **BUILT** | `.../Features/Retrieval/.../RetrieveChunksQueryHandler.cs` |
| `RagContextProvider` (`ILearningContextProvider` + `ICurriculumContextQuery`) behind `AiHelper:ContextProvider="Rag"` | **BUILT** | `Curriculum.Infrastructure/Services/RagContextProvider.cs` |
| Seeded corpus (4 subjects × Grade-3, AR+EN) with **placeholder** embeddings | **BUILT (placeholder)** | `Curriculum.Infrastructure/Persistence/Seed/CurriculumChunkSeeder.cs` + `DeterministicEmbedding.cs` |
| Redis infra (`IDistributedCache` + `IConnectionMultiplexer`, conditional on `ConnectionStrings:Redis`), Gamification Redis/Null fail-soft pattern, docker-compose `redis` service | **BUILT (reusable)** | `Host/Program.cs` lines 56-78; `Gamification.Infrastructure/Caching/RedisGamificationCache.cs` + DI Redis/Null selector; `docker/docker-compose.yaml` (`redis:6379`) |

**NOT built (this initiative builds these):**

- **Real BGE-M3 embeddings live path** — the TEI endpoint is config-driven but **unprovisioned**; the seeded rows are placeholder; there is **no re-embed mechanism** to replace `seed-placeholder-v0` rows.
- **`SeededCorpusContextProvider`** — referenced in HANDOFF/comments but **does NOT exist as a file**. Only `EmptyLearningContextProvider` (stub) and `RagContextProvider` exist. So the only path to live grounding today is RAG. (See OQ-3.)
- **`AiResponseCache`** (application-level table + Redis read-through) — **not built**. The handlers carry only *commented* "DEFERRED P3-04-BE-9" cache-first / cache-write seams; no `IAiResponseCache` interface, repository, table, or migration exists.
- **`IAiBatchGateway` / `ClaudeBatchProvider`** (P3-01-BE-13) — **not built** (no file). Needed for offline pre-generation that *fills* the cache.
- **`IAiUsageBudget` / daily-cap guardrail + `QuotaExhausted` error kind** (P3-01-BE-14) — **not built**.
- **`IGlobalSettingsProvider` + bootstrap impl** (P3-01-BE-15) — **not built**; the cache thresholds (`ai.cache.autoApprovalConfidence`=0.85, `whyWrongVariantCap`=50, `practicePoolSize`=5) have no read seam yet.
- **Redis-backed rate limiter** — `AiTutorRateLimiter` is in-process only.

---

## Acceptance criteria (testable WITHOUT live keys — mock `IAiGateway` / embedding / TEI / Batch clients)

### A. RAG activation + real BGE-M3 embeddings (P3-07-BE-0 + re-embed)
- **AC-A1 (re-embed, mocked client):** with a mocked/stub embedding client that returns deterministic real-shaped 1024-dim vectors, the re-embed mechanism replaces every row with `ModelVersion='seed-placeholder-v0'` with rows stamped to the configured live `Model`/`ModelVersion`, and the row count of placeholder rows drops to 0. Idempotent: re-running embeds nothing new.
- **AC-A2 (grounded retrieval):** after re-embed (with the same model/version used for the query at runtime), an in-corpus query (e.g. a Grade-3 Math fractions question) returns non-empty, correctly-filtered chunks (Math/Grade-3/Active-version), ordered by cosine distance, all clearing the similarity floor.
- **AC-A3 (empty signal preserved):** an out-of-corpus / unrelated query returns an empty `Chunks` list with `Successed=true` (not an error, not a fabricated chunk) — the refuse-and-redirect path still fires.
- **AC-A4 (parity guard):** when the configured `Curriculum:Embedding:ModelVersion` does not match the value stamped on served embedding rows, the provider logs the parity-guard warning (and, per BE-6 intent, fails-fast/returns null rather than searching a mismatched space). No silent garbage retrieval.
- **AC-A5 (degrade, no crash):** with `Curriculum:Embedding:BaseUrl` empty (no TEI), `EmbedAsync` returns null → retrieval returns empty → handler redirects. Startup logs a clear warning; the host does not crash.

### B. AiResponseCache + Redis (cost economy)
- **AC-B1 (cache HIT, no gateway call):** a repeat request whose canonical cache key already has an `Approved`, non-invalidated `AiResponseCache` entry returns the stored `SafeAiResult`/content **without invoking `IAiGateway`** (assert the mocked gateway received zero calls on the second request).
- **AC-B2 (cache MISS → store):** on a miss, after `ISafetyLayer` passes, the approved response is written to the cache (`ReviewStatus = Approved` when `Confidence ≥ ai.cache.autoApprovalConfidence`, else `PendingReview`); a subsequent identical request is a HIT (AC-B1). `WhyWrong` keys by normalized wrong-answer; `Explain`/`Hint` per their canonical tuples (`ai-cost-routing.md §5`).
- **AC-B3 (R5 gate not bypassed):** `PendingReview` / `Invalidated` / `Rejected` entries are **never** served as cache hits to other students (the current student still gets their safety-passed response). The cache is not a safety bypass.
- **AC-B4 (Redis rate limiter survives instances):** the per-student rate-limit counter is stored in Redis; simulating two app instances sharing one Redis, a student's combined request count is enforced across both. With Redis absent, it falls back to the in-process/Null behavior (degrade, no crash) — mirroring the Gamification Redis/Null pattern.
- **AC-B5 (thresholds via `IGlobalSettingsProvider`):** the cache reads `ai.cache.autoApprovalConfidence` (0.85), `whyWrongVariantCap` (50), `practicePoolSize` (5) through `IGlobalSettingsProvider` (bootstrap defaults), never from a hardcoded constant or `AiGatewayOptions`.
- **AC-B6 (degrade, no crash):** with no Redis configured, AiResponseCache + rate limiter operate in a degraded (Null/in-memory) mode without throwing.

### C. Live grounding wiring (provider flip + config)
- **AC-C1 (config-driven flip):** with `AiHelper:ContextProvider = "Rag"`, the resolved `ILearningContextProvider` is `RagContextProvider`; with the key absent/other, the resolved provider is the stub (`EmptyLearningContextProvider`) → always redirect. No code change needed to flip.
- **AC-C2 (end-to-end help loop, mocked gateway):** with RAG active over real-shaped embeddings and a mocked `IAiGateway`, an in-corpus Explain/Hint request flows context → prompt → safety → emit and returns delivered content (not a redirect), and emits `HelpDelivered`.
- **AC-C3 (out-of-scope still redirects):** an out-of-corpus request with RAG active emits `HelpDeclined{NoContext}` and redirects.

### D. Config / secrets wiring (absence-tolerant)
- **AC-D1:** every external dependency is bindable via config/env with no committed secret: `Ai:Providers:Claude:ApiKey` (+ OpenAI), `Curriculum:Embedding:{BaseUrl,AuthToken,Model,ModelVersion}`, `Curriculum:Retrieval:SimilarityDistanceFloor`, `AiHelper:ContextProvider`, `ConnectionStrings:Redis`, and the new cache/budget keys.
- **AC-D2 (graceful degrade matrix):** absent Claude key → gateway returns typed `AiError` → SafetyLayer fail-closed fallback (no crash). Absent TEI → empty retrieval → redirect. Absent Redis → Null cache + in-process limiter. Each case logs a clear, secret-free startup/runtime warning.
- **AC-D3 (no secret leakage):** security-auditor confirms no key/token in logs, error bodies, `AiUsage` records, or committed appsettings.

### E. Invariants regression (must still pass)
- **AC-E1:** `AiModuleArchTests` (`P301-ARCH-01`, `P302-ARCH-04/05`) still pass — no `IAiGateway` bypass introduced by the cache; no provider SDK leak.
- **AC-E2:** SafetyLayer remains the sole content exit; cache-served content is content that previously passed safety.

---

## Affected modules & data (new vs existing)

| Module | Change | New vs existing |
|---|---|---|
| `Curriculum` (Infrastructure) | Re-embed mechanism over `chunk_embeddings_bge_m3`; live TEI wiring (config already present) | New code; **existing** tables/entities |
| `Ai` (Application/Infrastructure) | `AiResponseCache` cache-first/cache-write in handlers (uncomment + implement the deferred seams); Redis rate limiter replacing/augmenting `AiTutorRateLimiter`; populate `AiRequest.CacheableSystemPrompt` in the gateway; (optional) `IAiBatchGateway`, `IAiUsageBudget` | New code + new entity/table (see below) |
| `Shared.Contracts` (`/Ai`) | New seams: `IAiResponseCache` (or repository), `IGlobalSettingsProvider`, optionally `IAiBatchGateway` + result DTOs, `IAiUsageBudget` + `BudgetCheckResult`, `AiErrorKind.QuotaExhausted` | New contracts |
| `Shared.Infrastructure` | `BootstrapDefaultGlobalSettingsProvider` | New |
| `Host` | Wire new DI (cache, budget, batch, global-settings, Redis rate limiter); appsettings keys | Edit (serialized: `Program.cs`) |

### New entity — `ai.AiResponseCache` (per `ai-cost-routing.md §4`, lives in the **`ai` schema**)
Columns (authoritative table in the cost-routing brief): `Id bigserial`, `CacheKey varchar(512) UNIQUE` (= `SHA256(canonical_key_tuple_json)`), `Response text`, `Type smallint` (`Explain=1/Hint=2/WhyWrong=3/Practice=4`), `SkillKey varchar(256)`, `QuestionId int? ` (per-question types only), `CurriculumVersion varchar(64)`, `PromptVersion varchar(32)`, `ModelVersion varchar(100)`, `ReviewStatus smallint` (`PendingReview=0/Approved=1/Rejected=2`), `Confidence decimal(5,4)?`, `CreatedAt`, `ApprovedBy int?`, `ApprovedAt?`, `InvalidatedAt?`. Indexes: UNIQUE(`CacheKey`); `(Type, ReviewStatus)`; `(SkillKey, CurriculumVersion)`; `(QuestionId)`. No cross-module FK (`QuestionId`/`ApprovedBy` are plain ints). Served-row predicate: `WHERE InvalidatedAt IS NULL AND ReviewStatus = Approved`.

**Decision needed (OQ-1):** whether MVP needs the *full* table (review gate, invalidation, all key types) or a *minimal* read-through (Redis-only, content-keyed, TTL'd) with the DB table deferred. The cost-routing brief specifies the full table; this initiative's "make AI value real" framing may justify a thinner first cut.

---

## Handoff → db-migration

- **Schema owner:** `ai` module (`AiDbContext`, schema `ai`, mirrors the existing `SafetyEvents` migration shape). The Curriculum tables already exist — **no curriculum schema change**; the re-embed mechanism only writes rows (UPDATE/INSERT into `chunk_embeddings_bge_m3`), no DDL.
- **New migration (if OQ-1 = full table):** `ai.AiResponseCache` per the §4 column list above. Enum columns `HasConversion<int>()`; UNIQUE on `CacheKey`; the four indexes; no cross-module FK. Mirror `SafetyEventConfig` / `AiDbContext` conventions. `SaveChangesAsync(userId)` override already exists on `AiDbContext`.
- **Budget store (BE-14, if in scope — OQ-2/OQ-D):** the daily request counter is preferentially a **Redis counter** (ephemeral, fast) rather than a DB column. If a durable counter is required, confirm placement (plan/subscription column owned by another module → must go through a `Shared.Contracts` seam, not a cross-module FK). **Flag, don't guess.**
- **No vector-column DDL changes** — `chunk_embeddings_bge_m3` already fixed at `vector(1024)` with HNSW `vector_cosine_ops`.

## Handoff → backend-feature

- **Re-embed mechanism (P3-07-BE-0 buildable side):** a command/job that selects `chunk_embeddings_bge_m3` rows where `ModelVersion='seed-placeholder-v0'` (or `Provider='deterministic-placeholder'`), calls `IEmbeddingProvider.EmbedAsync` per chunk content, and upserts the row with the live `Model`/`ModelVersion`/`Provider` + `IsActive=true`. Idempotent. **Mockable** via `IEmbeddingProvider` (inject the deterministic stub in tests). Decide host shape — see OQ-7 (Hangfire job vs admin command vs `IHostedService`). Hangfire **is** in the stack (used by Gamification jobs) — reuse it.
- **AiResponseCache read-through (uncomment the deferred seams):** in each of the 4 handlers, the commented "DEFERRED P3-04-BE-9" blocks mark exactly where cache-first lookup (before the safety call) and cache-write (after safety passes) slot in. Introduce `IAiResponseCache` in `Shared.Contracts/Ai/` + a `Redis + DB` implementation in `Ai.Infrastructure`. **Cache-first must not bypass safety** — only `Approved` entries (which already passed safety on the producing call) are served. Compute `CacheKey = SHA256(canonical_tuple)` per `Type` (cost-routing §5 key table). `WhyWrong` is always runtime per-student but cacheable by compound key with the `whyWrongVariantCap` LRU.
- **`IGlobalSettingsProvider` (BE-15):** typed getters (`GetDecimal/GetInt/GetString/GetBool(key, fallback)`) in `Shared.Contracts`; `BootstrapDefaultGlobalSettingsProvider` in `Shared.Infrastructure` returning code/appsettings fallback. Register as default. Cache thresholds read through this. (P10-12 later swaps the impl with no caller change.)
- **Redis rate limiter:** replace/augment `AiTutorRateLimiter` (in-process `ConcurrentDictionary`) with a Redis fixed-window using `IConnectionMultiplexer` (already registered when `ConnectionStrings:Redis` is set), mirroring `RedisGamificationCache`'s fail-soft Redis/Null pattern. When Redis is absent, fall back to the existing in-process behavior. Keep the `TryAllow(studentId)` call shape so handlers don't change.
- **Prompt-cache completion (BE-12):** populate `AiRequest.CacheableSystemPrompt` in the gateway/prompt path (the `ClaudeProvider` already serializes it). Note Haiku's ~4096-token cacheable-prefix floor (cost-routing §2 caveat) — caching may not fire on short prompts; that is expected.
- **`IAiBatchGateway` / `ClaudeBatchProvider` (BE-13)** — only needed to *fill* the cache via offline pre-generation. Scope per OQ-2: if cold-start cache-fill is out of MVP scope, this can be deferred (the runtime cache still works as a write-through on misses). **Recommend confirming with lead.**
- **`IAiUsageBudget` / daily cap (BE-14)** — minimal request-count guardrail returning `QuotaExhausted` (degrade-to-cache, never a hard error). Scope per OQ-2/OQ-E (exact cap numbers are a product decision).
- **Conventions:** `ILoggerManager` (not `ILogger<T>`); `BaseResponse<T>`/`Successed` for any new MediatR handlers; no UoW (commit-per-call; explicit transaction only if atomic multi-write); design-patterns-ask-first (the cache read-through "decorator" shape mirrors Gamification's `Cached*Query` — confirm it's an approved mirror, not a new pattern).

## Handoff → frontend

**None.** This is backend/infra/devops only. The student-app UI (P3-12) consumes the already-shipped SSE contract; no API shape changes are introduced by this initiative (cache hits/misses and grounding are transparent to the client). No designer or frontend-e2e stage.

---

## Buildable-here vs external-dep split (the critical separation)

The lead owns BACKEND. Real provider keys and TEI server provisioning are devops/secrets the lead supplies — the backend must be **config-driven against a configurable endpoint/key** and **testable WITHOUT live keys** (mock `IAiGateway` / embedding / TEI / Batch clients).

| Work item | Buildable here | External dep (lead/devops) |
|---|---|---|
| **W1** Re-embed mechanism (select placeholder rows → `IEmbeddingProvider.EmbedAsync` → upsert with live model/version, idempotent) | ✅ **[buildable here]** — fully testable with the deterministic stub injected as `IEmbeddingProvider` | — |
| **W2** TEI server (BGE-M3, 1024-d) provisioning on Hetzner + pinned model/version + auth token + health endpoint | — | 🔧 **[external dep]** — `P3-07-BE-0` infra portion (docker-compose/deploy entry, secret store, smoke test). Not built by this pipeline. |
| **W3** `BgeM3EmbeddingProvider` live wiring + parity guard hardening (fail-fast on mismatch) | ✅ **[buildable here]** — provider exists; harden parity to fail-fast; config-bind URL/token/model/version | Needs W2 endpoint values to run *live* (mock for tests) |
| **W4** `AiResponseCache` table + migration (`ai` schema) | ✅ **[buildable here]** (pending OQ-1 full vs minimal) | — |
| **W5** `IAiResponseCache` + Redis/DB read-through; cache-first + cache-write in the 4 handlers (R5 gate) | ✅ **[buildable here]** — mock gateway asserts zero calls on HIT | — |
| **W6** Redis rate limiter (replace in-process) | ✅ **[buildable here]** — Redis infra already in repo; mock/Null fallback testable | Live multi-instance proof needs a real Redis (compose provides it) |
| **W7** `IGlobalSettingsProvider` + bootstrap impl (BE-15) | ✅ **[buildable here]** | — |
| **W8** Prompt-cache completion — populate `CacheableSystemPrompt` (BE-12) | ✅ **[buildable here]** | — |
| **W9** `IAiBatchGateway` + `ClaudeBatchProvider` (BE-13) | ✅ **[buildable here]** (scope per OQ-2) | Live batch runs need real Claude key |
| **W10** `IAiUsageBudget` daily-cap guardrail + `QuotaExhausted` (BE-14) | ✅ **[buildable here]** (scope per OQ-2; cap numbers per OQ-E) | Cap *values* = product decision |
| **W11** Claude/OpenAI provider API keys | — | 🔧 **[external dep]** — secret store / env (`Ai__Providers__*__ApiKey`). Code already config-driven. |
| **W12** `appsettings`/env binding + graceful-degrade behavior + DI wiring | ✅ **[buildable here]** | Real values injected by devops |

---

## Open questions / assumptions / risks

### Open questions for the lead (recommend resolving BEFORE the planner finalizes the plan)
- **OQ-1 — AiResponseCache scope (full table vs minimal):** Build the *full* `ai.AiResponseCache` (review gate, all 4 key types, invalidation, indexes per cost-routing §4) now, or a *minimal* Redis-only content-keyed TTL read-through with the DB table + R5 review gate deferred? Also: **cache TTL + key strategy** — confirm the canonical key tuples per Type and a default TTL (none specified in the brief; Redis layer needs one).
- **OQ-2 — BE-12/13/14 subset:** Which of the gateway-economy tasks are in scope *for the cache to function* vs deferrable? Assessment: **BE-12 (prompt-cache)** is cheap and mostly done — finish it. **BE-15 (`IGlobalSettingsProvider`)** is a prerequisite for reading cache thresholds — build it. **BE-13 (Batch gateway)** only matters for *cold-start cache-fill*; the runtime write-through cache works without it — recommend **defer unless cold-start fill is required**. **BE-14 (daily cap)** is independent of the cache and is a cost guardrail — recommend **build a minimal version** (or defer to Phase 10 if the lead prefers no spend controls pre-monetization). Confirm.
- **OQ-3 — default live provider: SeededCorpus vs RAG.** The `ai-helper-mvp.md` plan says ship `SeededCorpusContextProvider` first, then swap to RAG. **But `SeededCorpusContextProvider` was never built** — only `RagContextProvider` exists. So "activate live grounding" today means setting `AiHelper:ContextProvider="Rag"` + real embeddings. **Confirm:** is RAG (post-re-embed) the intended default live provider, or should a `SeededCorpusContextProvider` be built as a simpler interim? (Recommendation: skip the seeded provider; RAG over the seeded corpus with real embeddings is the same data with better retrieval.)
- **OQ-4 — re-embed host:** Hangfire job (Hangfire is in the stack), an admin/CLI command, or an `IHostedService` one-shot? Recommendation: a Hangfire job + an admin trigger, mirroring the Gamification cache-rebuild job pattern. Confirm.
- **OQ-5 — Redis connection expectations:** Reuse the existing single `ConnectionStrings:Redis` + `IConnectionMultiplexer` (registered only when present) and the Redis/Null fail-soft pattern? (Recommended — do not introduce a second Redis registration; CLAUDE.md rule against duplicate registrations.) Confirm whether the AiResponseCache uses `IDistributedCache` (already Redis-backed via `AddStackExchangeRedisCache`) or `IConnectionMultiplexer` directly.
- **OQ-6 — similarity floor for real embeddings:** the seeder used a placeholder floor of 0.4 tuned for the hash-vector geometry. Real BGE-M3 vectors have different distance distributions — `Curriculum:Retrieval:SimilarityDistanceFloor` must be **re-calibrated** against the real corpus. Who calibrates, and against what eval slice? (Risk: a wrong floor either over-redirects or grounds on irrelevant chunks.)
- **OQ-7 — auto-approval + confidence source:** the R5 gate needs a `Confidence` value, but `SafetyLayer` currently forwards `Confidence = null` (the gateway doesn't yet extract a structured confidence). Confirm: for MVP, do safety-passed responses auto-approve (treat null confidence as "pass" → `Approved`), or all go to `PendingReview` until a confidence signal exists? This directly affects whether the cache ever serves hits.

### Assumptions (stated; correct me if wrong)
- The seeded corpus (Grade-3, 4 subjects, AR+EN) is the MVP grounding scope; broader grades/subjects come with the BL ingestion pipeline (out of scope here).
- "Operational" = the seeded corpus is queryable with real semantics and the help loop runs end-to-end behind config; it does **not** require the full BL-01..05 ingestion pipeline (explicitly backlog).
- Prompt-cache (`CacheableSystemPrompt`) is treated as the *secondary* lever; the *primary* lever is the application `AiResponseCache`.

### Risks
- **R1 — Parity drift (HIGH):** if re-embed stamps a model/version that the live TEI doesn't actually serve, retrieval silently returns garbage. Mitigation: parity guard must **fail-fast** (not just warn) on mismatch, and re-embed must stamp from the *same* config the runtime provider reads. Security-auditor + reviewer gate.
- **R2 — Cache as safety bypass (HIGH, security):** a naive cache-first that serves any stored content (incl. `PendingReview`) would bypass the safety/review gate and amplify a bad answer to thousands of children (cost-routing §5 R5). Mitigation: serve only `Approved` + non-invalidated; security-auditor mandatory.
- **R3 — Secret leakage (HIGH, security):** keys/tokens for Claude/OpenAI/TEI must never hit logs/appsettings/usage records. Mitigation: security-auditor mandatory; existing patterns (header-only, never-logged) must be preserved in new code.
- **R4 — Floor mis-calibration (MEDIUM):** see OQ-6.
- **R5 — Cost blow-up before cache warms (MEDIUM):** cold-start = all misses (cost-routing §1). Without BE-13 batch pre-gen, the cache fills only via runtime write-through; the daily cap (BE-14) is the backstop. Accept for MVP or build BE-13 — see OQ-2.
- **R6 — Serialized shared-file contention:** `Program.cs`, `Directory.Packages.props`, `.sln`, `appsettings.json`, Claims edits must be serialized per PARALLELISM.md if any sibling story runs concurrently.

---

## Recommended pipeline order (first cut — the `planner` finalizes)

**Pre-flight (lead, before planning):** resolve OQ-1, OQ-2, OQ-3, OQ-7 (these gate schema + scope). OQ-4/5/6 can be resolved in-batch.

Three largely-independent clusters can run in parallel; the security-auditor + reviewer are mandatory gates at the end.

- **Batch 0 — `analyzer` (this brief) → `planner`.** No designer stage (no UI).

- **Cluster A (RAG/BGE-M3) — can run ∥ B ∥ C:**
  - `backend-feature`: W3 (harden `BgeM3EmbeddingProvider` parity fail-fast + config-bind) → W1 (re-embed mechanism, host per OQ-4) → re-calibrate floor (OQ-6).
  - `api-tester`: grounded retrieval (AC-A2), empty signal (AC-A3), parity guard (AC-A4), degrade (AC-A5) — against running Curriculum module with the deterministic stub standing in for TEI.
  - **External (lead/devops, parallel, non-blocking for buildable tests):** W2 TEI provisioning + W11 keys.

- **Cluster B (AiResponseCache + Redis) — can run ∥ A ∥ C:**
  - `db-migration` (if OQ-1=full): W4 `ai.AiResponseCache` table + migration. *(gate: migration applies on pg17)*
  - `backend-feature`: W7 `IGlobalSettingsProvider` (prereq) → W5 cache read-through + handler wiring (R5 gate) → W6 Redis rate limiter. Optional W9 (BE-13) / W10 (BE-14) per OQ-2.
  - `api-tester`: cache HIT-no-gateway-call (AC-B1), miss→store→hit (AC-B2), R5 gate (AC-B3), Redis limiter across instances (AC-B4), degrade (AC-B6).

- **Cluster C (grounding flip + config) — small, can run ∥; partly depends on A for a *live* end-to-end:**
  - `backend-feature`: W12 config/DI wiring + W8 prompt-cache completion (BE-12). Verify the `AiHelper:ContextProvider="Rag"` flip resolves `RagContextProvider` (AC-C1).
  - `api-tester`: end-to-end help loop with mocked gateway + RAG (AC-C2), out-of-scope redirect (AC-C3), graceful-degrade matrix (AC-D2).

- **Gate 1 — `security-auditor` (MANDATORY):** AI prompts + child-adjacent data + secrets + cache-as-bypass (R2) + key leakage (R3) + parity (R1). Critical/High block.

- **Gate 2 — `reviewer`:** all ACs (A–E), CONVENTIONS, arch tests (AC-E1) still green, HANDOFF.md updated (live-grounding now active path, re-embed runbook, cache keys/TTL, Redis limiter, config matrix, what's still external/devops).

- **`committer`:** per-story branches (e.g. `feat/P3-07-BE-0-reembed`, `feat/ai-response-cache`, `feat/ai-redis-ratelimiter`), conventional messages, PRs — never on `main`, never merge.

---

### Key file references (absolute)
- Gateway / safety / handlers: `e:\Wrokspace\Learnexia\backend\src\Modules\Ai\Learnexia.Modules.Ai.Infrastructure\Gateway\AiGateway.cs`; `...Ai.Application\Safety\SafetyLayer.cs`; `...Ai.Application\Features\Explain\Commands\ExplainConceptCommandHandler.cs` (deferred cache seams at the "DEFERRED P3-04-BE-9" comments)
- Rate limiter (in-process, to move to Redis): `...Ai.Application\Services\AiTutorRateLimiter.cs`
- Grounding stub + flip: `...Ai.Application\PromptBuilder\Stubs\EmptyLearningContextProvider.cs`; `...Ai.Application\DependencyInjection.cs`
- RAG: `...Curriculum.Infrastructure\Services\RagContextProvider.cs`; `...\BgeM3EmbeddingProvider.cs`; `...\DeterministicEmbedding.cs`; `...\EmbeddingSettings.cs`; `...\Features\Retrieval\Queries\RetrieveChunks\RetrieveChunksQueryHandler.cs`; `...\Persistence\Seed\CurriculumChunkSeeder.cs`; `...Curriculum.Infrastructure\DependencyInjection.cs`
- Prompt cache field/serialize: `e:\Wrokspace\Learnexia\backend\src\Shared\Learnexia.Shared.Contracts\Ai\AiRequest.cs`; `...Ai.Infrastructure\Providers\ClaudeProvider.cs`
- Redis infra to reuse: `e:\Wrokspace\Learnexia\backend\src\Host\Learnexia.Host\Program.cs` (lines 56-78); `...Gamification.Infrastructure\Caching\RedisGamificationCache.cs` + `...Gamification.Infrastructure\DependencyInjection.cs` (Redis/Null selector); `e:\Wrokspace\Learnexia\docker\docker-compose.yaml` (`redis` service)
- Arch invariant tests: `e:\Wrokspace\Learnexia\backend\tests\Modules.Ai.UnitTests\AiModuleArchTests.cs`
- Source briefs/plans/tasks: `docs\briefs\ai-cost-routing.md`, `docs\briefs\ai-helper-mvp.md`, `docs\plans\P3-01.md`, `docs\plans\P3-07.md`, `tasks\Backend\Phase-4-AI-Tutor\P3-01-BE.md`, `tasks\Backend\Phase-4-AI-Tutor\P3-07-BE.md`
