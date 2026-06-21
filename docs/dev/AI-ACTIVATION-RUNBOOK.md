# DevOps Runbook — Activating Live AI (Phase 4 / P3-xx)

**Audience:** operators / devops performing the AI "flip-to-live."
**Status of this doc:** verified against backend code on 2026-06-21 (branch `main`). Where a fact could not be confirmed in code it is marked **UNVERIFIED**.

> **This runbook activates real, paid AI.** It does not change application code. Every step below is a configuration / infrastructure action (env vars, an external embedding service, an admin HTTP call). The backend ships **dormant**: it boots, serves all non-AI features, and returns a graceful "AI unavailable / redirect" fallback while AI is off. Nothing here requires a deployment.

---

## 1. Overview — dormant vs live, and the activation model

The AI Tutor (Explain, Hint, Why-Wrong / Simplify, Similar-Example, Recommendation narration) is fully built and wired, but **gated by configuration**. With the committed defaults (all the relevant keys empty) the system is **dormant**:

| Layer | Dormant default | What it does when dormant |
|---|---|---|
| Provider gateway | `Ai:Providers:Claude:ApiKey` / `Ai:Providers:OpenAi:ApiKey` empty | Provider sends no auth header → upstream 401/403 → gateway returns `AiError.Unavailable` after retries → `SafetyLayer` returns a localized fallback. **No crash.** (`ClaudeProvider.cs:129`, `AiGateway` fail path, `SafetyLayer.cs:123-128`) |
| RAG context | `AiHelper:ContextProvider` empty | `EmptyLearningContextProvider` is wired — always returns zero curriculum chunks → the tutor refuses-and-redirects (graceful degradation AC-4). (`Curriculum/.../DependencyInjection.cs`, `AiModule.cs`) |
| Embedding service | `Curriculum:Embedding:BaseUrl` empty | `BgeM3EmbeddingProvider` logs `[RAG DORMANT]` and returns null on every call. (`BgeM3EmbeddingProvider.cs`) |
| Response cache serving | `AiHelper:Cache:autoApproveEnabled` (default `true`) | See §8 — serving IS reachable once a provider key is present; the kill-switch can freeze it. |

**The config-gated activation model.** Each capability turns on independently by setting a config key (env var). No code path is conditionally compiled; the same binary runs dormant or live. Gates mirror the codebase's "feature-present-when-configured" idiom (same as Redis-when-present and OTLP-when-set).

**What flipping live does (one paragraph).** You give the gateway a real provider key (so `IAiGateway.CompleteAsync` makes real paid calls to Anthropic/OpenAI), you stand up a BGE-M3 embedding service and point the backend at it, you run a one-time admin job to embed the curriculum corpus into pgvector, then you set `AiHelper:ContextProvider=Rag` so the tutor grounds its answers in retrieved curriculum chunks instead of refusing. After that, every student AI request flows: prompt build → (RAG retrieval) → real provider → safety checks → response (and, on a safety-pass, an optional cached approval). You then verify safety quality with a live Gate-B eval and watch `/health`’s `ai-gateway` entry plus OTel telemetry.

---

## 2. Required environment variables

Env-var form uses double underscores (`__`) for config-section nesting (ASP.NET Core convention). All keys verified against the code cited in the right column.

### Provider gateway (required to make any AI call)

| Env var (`__` form) | Colon path | Purpose | Example | Req? | Verified at |
|---|---|---|---|---|---|
| `Ai__Providers__Claude__ApiKey` | `Ai:Providers:Claude:ApiKey` | Anthropic API key. Sent as `x-api-key`. Empty → header omitted → dormant. | `sk-ant-...` | **Required**¹ | `ClaudeProvider.cs:129` |
| `Ai__Providers__OpenAi__ApiKey` | `Ai:Providers:OpenAi:ApiKey` | OpenAI API key. Sent as `Authorization: Bearer`. Empty → header omitted → dormant. | `sk-...` | Optional¹ | `OpenAiProvider.cs:107` |
| `Ai__Gateway__DefaultProvider` | `Ai:Gateway:DefaultProvider` | Provider the router falls back to. Valid: `Claude`, `OpenAi`. | `Claude` | Optional (default `Claude`) | `AiGatewayOptions.cs` |
| `Ai__Gateway__TimeoutSeconds` | `Ai:Gateway:TimeoutSeconds` | Per-call timeout. | `30` | Optional (default 30) | `AiGatewayOptions.cs` |
| `Ai__Gateway__RetryCount` | `Ai:Gateway:RetryCount` | Transient-error retries. | `3` | Optional (default 3) | `AiGatewayOptions.cs` |
| `Ai__Gateway__RetryBackoffSeconds` | `Ai:Gateway:RetryBackoffSeconds` | Retry backoff base. | `1.0` | Optional (default 1.0) | `AiGatewayOptions.cs` |

¹ At least one of Claude / OpenAI must be set. `DefaultProvider` decides which is used when the model router has no explicit override; the committed model routing table targets Claude models (`claude-haiku-4-5` / `claude-sonnet-4-6` / `claude-opus-4-8`, see `AiModelRouter.cs`), so **a Claude key is the practical requirement** unless you reconfigure routing.

> Provider base URLs are **not** env-configurable — they are hardcoded in DI: `https://api.anthropic.com/` and `https://api.openai.com/` (`Ai.Infrastructure/DependencyInjection.cs`). Endpoints: `v1/messages` (Claude), `v1/chat/completions` (OpenAI). Anthropic version header is `anthropic-version: 2023-06-01`.

### RAG / embedding (BGE-M3 TEI) — required for grounded answers

| Env var (`__` form) | Colon path | Purpose | Example | Req? | Verified at |
|---|---|---|---|---|---|
| `Curriculum__Embedding__BaseUrl` | `Curriculum:Embedding:BaseUrl` | Base URL of the BGE-M3 TEI service. Empty → RAG dormant. | `http://tei-host:8080` | Required for RAG | `EmbeddingSettings.cs`, `appsettings.json:120` |
| `Curriculum__Embedding__ModelVersion` | `Curriculum:Embedding:ModelVersion` | Pinned model version. **Required when BaseUrl is set** (parity guard blocks embedding if absent). | `1.0` | Required for RAG | `appsettings.json:126-129`, `BgeM3EmbeddingProvider.cs` |
| `Curriculum__Embedding__AuthToken` | `Curriculum:Embedding:AuthToken` | Bearer token for the TEI endpoint. Never commit. | `(secret)` | Optional (if TEI requires auth) | `appsettings.json:121-123` |
| `Curriculum__Embedding__Model` | `Curriculum:Embedding:Model` | Model slug; must match seeded rows. | `bge-m3` | Optional (default `bge-m3`) | `appsettings.json:124-125` |
| `Curriculum__Retrieval__SimilarityDistanceFloor` | `Curriculum:Retrieval:SimilarityDistanceFloor` | Cosine-distance cutoff. Chunks with distance > floor are dropped. **Re-calibrate after real re-embed** (committed `0.4` was tuned to placeholder vectors; BGE-M3 typical 0.1–0.35). | `0.3` | Optional (default 0.4) | `appsettings.json:131-141`, `RetrieveChunksQueryHandler.cs` |

### Context-provider flip — the master RAG switch

| Env var (`__` form) | Colon path | Purpose | Example | Req? | Verified at |
|---|---|---|---|---|---|
| `AiHelper__ContextProvider` | `AiHelper:ContextProvider` | `Rag` → live pgvector retrieval (`RagContextProvider`). Empty / any other value → `EmptyLearningContextProvider` (always redirects). Case-insensitive. | `Rag` | Required for grounded answers | `appsettings.json:76-84`, `Curriculum/.../DependencyInjection.cs`, `AiReadinessProbe.cs:47-53` |

### Redis (response cache hot layer + cross-instance rate limiter) — optional but recommended in prod

| Env var (`__` form) | Colon path | Purpose | Example | Req? | Verified at |
|---|---|---|---|---|---|
| `ConnectionStrings__Redis` | `ConnectionStrings:Redis` | Enables Redis-backed distributed cache, the AI response-cache hot layer (`ai:rc:*`), and the cross-instance AI rate limiter (`ai:rl:*`). Empty → in-memory cache + in-process rate limiter (single-instance only). | `redis:6379` | Optional (recommended for multi-instance) | `Program.cs:121-135`, `Ai.Infrastructure/DependencyInjection.cs` |

### Response-cache tunables (admin/runtime; default values work) — see §8

| Env var (`__` form) | Colon path | Default | Purpose |
|---|---|---|---|
| `AiHelper__Cache__autoApproveEnabled` | `AiHelper:Cache:autoApproveEnabled` | `true` | Master kill-switch for serving cached responses. `false` freezes everything at PendingReview (dormant). |
| `AiHelper__Cache__safetyPassConfidence` | `AiHelper:Cache:safetyPassConfidence` | `0.90` | Confidence assigned when all safety checks pass. Must be ≥ `autoApprovalConfidence` for auto-approval to fire. |
| `AiHelper__Cache__autoApprovalConfidence` | `AiHelper:Cache:autoApprovalConfidence` | `0.85` | Min confidence to auto-approve a generated response into the served cache. |

### Safety (defaults are correct; do not weaken)

`Ai:Safety:EnableToxicityCheck` / `EnableAgeCheck` / `EnableHallucinationCheck` (all default `true`), `Ai:Safety:MaxRegenerationAttempts` (default 2), `Ai:Safety:ModerationProvider` (default `Claude`). Env form e.g. `Ai__Safety__EnableToxicityCheck`. **Keep all checks enabled in production** (`SafetyOptions.cs`, `appsettings.json:159-167`).

### Observability (for monitoring; not an AI gate) — see §7 step 8

`OpenTelemetry__Otlp__Endpoint` (OTLP collector URL; empty = no export, app runs unchanged), `OpenTelemetry__ServiceName` (default `Learnexia`), `OpenTelemetry__SamplingRatio` (default 1.0). Verified `Program.cs:64-115`.

> **Secrets rule:** never commit non-empty values for `Ai__Providers__*__ApiKey` or `Curriculum__Embedding__AuthToken`. Inject via the environment / secret store only. `appsettings.json` ships these empty by design.

---

## 3. BGE-M3 TEI setup

**What it is.** The backend does not run an embedding model in-process. It calls an external **HuggingFace Text Embeddings Inference (TEI)** service serving **BGE-M3** (1024-dimensional embeddings). Provisioning that service (e.g. on a GPU host) is **BE-0 devops work** and is **not** part of `docker/docker-compose.yaml` (the `8080` port there is the API host, not TEI — verified). The committed appsettings comment shows the intended shape (`http://hetzner-host:8080`).

**How the backend talks to it.** `BgeM3EmbeddingProvider` (`Curriculum.Infrastructure/Services/BgeM3EmbeddingProvider.cs`) is a typed `HttpClient` whose base address is `Curriculum:Embedding:BaseUrl`, timeout 10s.

- **Request:** `POST {BaseUrl}/embed` with JSON `{"inputs": "<text>", "normalize": true}`.
- **Auth:** if `Curriculum:Embedding:AuthToken` is set, sent per-request as `Authorization: Bearer <token>`.
- **Response:** TEI returns either a batch shape `[[...1024 floats...]]` or a flat `[...1024 floats...]`; the provider handles both.
- **Dimension / model:** **1024** dims, model slug `bge-m3` (default). The DB vector column is fixed at `vector(1024)` (§4), so the served model MUST be 1024-dim BGE-M3.
- **Parity guard:** `IsConfigured` requires BOTH `BaseUrl` AND `ModelVersion` non-empty. With `BaseUrl` set but `ModelVersion` empty the provider logs `[PARITY GUARD]` and blocks embedding. Set `ModelVersion` to the exact version your TEI serves so re-embedded rows are stamped correctly.

**Startup signal.** Look for one of: `[RAG DORMANT]` (no BaseUrl), `[PARITY GUARD]` (BaseUrl but no ModelVersion), or `[CONFIGURED]` (both set).

---

## 4. Corpus re-embedding

Curriculum content is chunked and embedded into pgvector. The mechanism is a **Hangfire background job** triggered by an **admin HTTP endpoint**.

**Storage (verified):**
- Table `curriculum.chunk_embeddings_bge_m3` (schema `curriculum`).
- Column `Vector` of type `vector(1024)` (pgvector), HNSW index `ix_chunk_embeddings_bge_m3_vector_hnsw_cosine` with `vector_cosine_ops`. Rows also carry `Provider`, `Model`, `ModelVersion`, `IsActive`.
- Created by migration `20260613213836_InitialCurriculum` (it also emits `CREATE EXTENSION IF NOT EXISTS vector`).
- Seeded rows ship with placeholder vectors stamped `ModelVersion = 'seed-placeholder-v0'` — these MUST be replaced before live RAG.

**Trigger (verified):**
- Endpoint: **`POST /api/Admin/Curriculum/ReEmbed`** — admin-only (`AuthorizationPolicies.AdminOnly`). (`ReEmbedController.cs`)
- Precondition: `Curriculum:Embedding:BaseUrl` + `ModelVersion` configured, else HTTP 400 with a clear message.
- It enqueues `ReEmbedCurriculumJob` (Hangfire) and returns the job ID immediately; embedding runs in the background.
- The job is **idempotent**: it only re-embeds rows whose `ModelVersion` differs from the configured active version (batches of 50, fail-soft per chunk). Re-running when everything is current is a safe no-op. On success it stamps each row `Provider=bge-m3-tei`, `Model`, `ModelVersion`, and the new `Vector`.

How to call it:

```bash
curl -X POST "$API/api/Admin/Curriculum/ReEmbed" \
  -H "Authorization: Bearer $ADMIN_JWT"
```

**Verify completion (verified):** the placeholder count must reach zero. Run against the DB:

```sql
-- Must be 0 after a successful re-embed:
SELECT COUNT(*) FROM curriculum.chunk_embeddings_bge_m3
WHERE "ModelVersion" = 'seed-placeholder-v0';

-- Sanity: active embeddings exist and carry the live model version:
SELECT "ModelVersion", COUNT(*)
FROM curriculum.chunk_embeddings_bge_m3
WHERE "IsActive" = true
GROUP BY "ModelVersion";
```

You can also watch the Hangfire dashboard (Development only) or the job log for completion. **UNVERIFIED:** there is no dedicated "re-embed status" admin GET endpoint — verification is via the SQL count above (and Hangfire), per the controller's own documented post-steps.

---

## 5. `ContextProvider=Rag` flip

The single switch that turns the tutor from "refuse / redirect" to "answer grounded in retrieved curriculum."

- **Key:** `AiHelper:ContextProvider` (env `AiHelper__ContextProvider`).
- **Accepted values:** `Rag` (case-insensitive) → `RagContextProvider` does live pgvector cosine-distance retrieval via `RetrieveChunksQuery` (server-side `<=>` over the HNSW index). Any other value or empty → `EmptyLearningContextProvider`, which always returns zero chunks (the refuse-and-redirect default). (`Curriculum/.../DependencyInjection.cs`, `RagContextProvider.cs`)
- **DI mechanic:** when set to `Rag`, Curriculum registers `RagContextProvider` last for `ILearningContextProvider` / `ICurriculumContextQuery`, overriding the Ai-module stubs (last registration wins).
- **Startup signal:** `AiModule` logs whether `ContextProvider=Rag` (RagContextProvider active) or the empty provider is active.

**Order of operations (HARD requirement).** Embeddings must exist **before** you flip:
1. TEI up + `Curriculum:Embedding:BaseUrl` + `ModelVersion` set.
2. `POST /api/Admin/Curriculum/ReEmbed` run and verified (placeholder count = 0).
3. Re-calibrate `Curriculum:Retrieval:SimilarityDistanceFloor` for the real corpus (start ~0.3).
4. **Then** set `AiHelper:ContextProvider=Rag` and restart so DI picks up `RagContextProvider`.

Flipping to `Rag` before the corpus is embedded means retrieval returns nothing → the tutor over-redirects (no crash, but useless). The committed `appsettings.json` comment (lines 76-84) encodes exactly this precondition list.

---

## 6. Gate-B live AI-safety eval procedure

**What exists (verified, P6-02):** an **offline** eval harness under `backend/tests/Ai.EvalTests/`:
- `SafetyEvalHarnessTests.cs` runs the 62 cases in `Data/safety-eval-set.json` against the **real** safety checks (Toxicity / Age / Hallucination), but feeds them through `Fakes/DeterministicFakeAiGateway.cs` which returns **canned judge verdicts** — no provider, no key, no network.
- The eval set covers **both Arabic and English** (31 `ar` + 31 `en`) across all 4 subjects and all 3 checks.
- Results are written to `Data/safety-eval-results.json` (run id, pass/fail counts, pass-rate, threshold, and `byCheck` / `bySubject` / `byLanguage` breakdowns). The offline pass threshold is **100%** (asserted in `SafetyEvalHarnessTests.cs`; any miss is a parse/map bug, not a model-quality signal).
- The committed artifact is embedded into `Ai.Infrastructure` and surfaced by **`GET /api/Admin/AiSafety/evals`** (admin-only) via `IAiSafetyEvalResultsQuery` → `AiSafetyEvalResultsQueryAdapter` → `AdminAiSafetyController`. It returns aggregate metrics only (no prompts/PII) and never 404/500 (returns a bootstrap sentinel if no run exists).

Run the offline tier any time:
```bash
dotnet test backend/tests/Ai.EvalTests --filter Category=EvalOffline
```

### ⚠️ GAP — a LIVE Gate-B eval is NOT runnable as-is (needs new code)

**This is the single most important honesty flag in this runbook.** The harness **cannot** be pointed at a real provider by config or env var:
- `DeterministicFakeAiGateway` is hardcoded to canned responses; its constructor takes only a canned verdict string — there is **no flag, env var, or DI seam** to swap in the real `ClaudeProvider`/`OpenAiProvider`. (`Fakes/DeterministicFakeAiGateway.cs`)
- The live tier is **documented but not implemented**: the `.csproj` and the P6-02 brief reserve a `[Trait("Category","EvalLive")]` tier "for devops before prod," but **no `EvalLive` test class exists**.

**Therefore, to run a true live Gate-B eval, new code must be added** (this runbook does NOT build it — flag it to the backend team):
- A new test class `SafetyEvalHarnessLiveTests.cs` tagged `[Trait("Category","EvalLive")]` that wires the **real** `AiGateway` + provider(s) from real keys, runs the same `safety-eval-set.json` cases through the real moderation path (ignoring `expectedJudgeVerdict`), in **both `ar` and `en`**, and writes a `safety-eval-results.json` with `tier: "EvalLive"`.
- A looser, deliberate pass threshold (model judgment is non-deterministic — e.g. 0 unsafe outputs allowed; ≥ some % alignment on borderline cases) decided by the team. Until that class exists, **the only "live" safety validation available is manual** (see the smoke test in §7 step 6): run a representative set of unsafe + age-inappropriate + hallucination-bait prompts in Arabic and English against the live tutor and confirm each is blocked/redirected, then read `GET /api/Admin/AiSafety/evals` for the (offline) baseline.

**Pass criteria intent (for whoever builds the live tier):** zero unsafe/age-inappropriate outputs surfaced to a child in either language; hallucination-bait grounded or refused; results recorded to `safety-eval-results.json` and visible at `GET /api/Admin/AiSafety/evals`.

---

## 7. Activation order of operations (the safe sequence)

Do these in order. Steps 1–2 can run in parallel; do not flip `Rag` (step 5) before step 4 verifies.

1. **Set provider key(s).** `Ai__Providers__Claude__ApiKey` (and/or OpenAI). Optionally set `ConnectionStrings__Redis` for the cache hot layer + cross-instance rate limiting. Restart. Confirm startup log shows the key as present (not `[DORMANT]`).
2. **Stand up BGE-M3 TEI** (1024-dim) and set `Curriculum__Embedding__BaseUrl`, `Curriculum__Embedding__ModelVersion` (and `AuthToken` if needed). Restart. Confirm `BgeM3EmbeddingProvider [CONFIGURED]` log.
3. **Re-embed the corpus.** `POST /api/Admin/Curriculum/ReEmbed` (admin JWT). Wait for the Hangfire job.
4. **Verify embeddings.** Placeholder count = 0 and active rows carry the live `ModelVersion` (SQL in §4). Re-calibrate `Curriculum__Retrieval__SimilarityDistanceFloor` (~0.3) for the real corpus.
5. **Flip RAG.** Set `AiHelper__ContextProvider=Rag`. Restart so DI binds `RagContextProvider`. Confirm the `AiModule` `ContextProvider=Rag` startup log.
6. **Smoke test.** Hit a tutor endpoint (e.g. `POST` Explain / Hint) in both Arabic and English with in-corpus questions → confirm grounded answers; with unsafe/age-inappropriate/hallucination-bait prompts → confirm block/redirect. (This is the interim live-safety check until the §6 live tier exists.)
7. **Gate-B eval.** Offline tier (`dotnet test --filter Category=EvalOffline`) must be green; perform the live safety validation per §6 (currently manual + new-code gap).
8. **Monitor.** Confirm `GET /health` → 200 with the `ai-gateway` entry `Healthy` (and RAG note); watch OTel traces/metrics if `OpenTelemetry__Otlp__Endpoint` is set; review `GET /api/Admin/AiSafety/evals` and the admin AI-safety dashboard / `GET /api/Admin/Analytics/kpis` for safety-flag and usage trends.

---

## 8. Backend readiness checklist (verified)

| Capability | State | Evidence |
|---|---|---|
| Provider gateway (Claude + OpenAI, real HTTP) | **READY-BUT-DORMANT** — real `POST` to `api.anthropic.com` / `api.openai.com`; dormant until a key is set; fail-closed via `AiError.Unavailable`. | `ClaudeProvider.cs:129,164-176`, `OpenAiProvider.cs:107`, `AiGateway`, `SafetyLayer.cs:123-128` |
| Model routing | **READY** — deterministic router with built-in Claude model table + config overrides (`Ai:Gateway:Models:*`). | `AiModelRouter.cs`, `ModelConfig.cs` |
| RAG retrieval seam (pgvector cosine over HNSW) | **READY-BUT-DORMANT** — `RagContextProvider` does real server-side retrieval; `EmptyLearningContextProvider` wired until `AiHelper:ContextProvider=Rag`. | `RagContextProvider.cs`, `RetrieveChunksQueryHandler.cs`, `Curriculum/.../DependencyInjection.cs` |
| BGE-M3 embedding client | **READY-BUT-DORMANT** — TEI HTTP client; dormant (`[RAG DORMANT]`) until `BaseUrl`+`ModelVersion` set; parity guard enforces version stamp. | `BgeM3EmbeddingProvider.cs`, `EmbeddingSettings.cs` |
| pgvector storage + re-embed job | **READY** — `curriculum.chunk_embeddings_bge_m3` `vector(1024)` + HNSW; admin `POST /api/Admin/Curriculum/ReEmbed` → idempotent Hangfire job. Corpus still on placeholder vectors until run. | migration `20260613213836_InitialCurriculum`, `ReEmbedController.cs`, `ReEmbedCurriculumJob.cs` |
| AiResponseCache (serving) | **READY-BUT-DORMANT — but NOT for the reason the HANDOFF claims.** The stale note says "serving dormant until a Confidence signal is wired." **Code now wires Confidence:** `SafetyLayer` assigns `safetyPassConfidence` (default 0.90) on the genuine safety-pass path, which exceeds `autoApprovalConfidence` (0.85) → entries auto-approve and become servable. Serving is therefore reachable **once a provider key exists**; it is gated only by the runtime kill-switch `AiHelper:Cache:autoApproveEnabled` (default `true` = active). Set that to `false` to freeze serving without a deploy. | `SafetyLayer.cs:140-158`, `ExplainConceptCommandHandler.cs` (approval gate), `AiResponseCacheRepository.cs:105-156`, `AiResponseCache.cs:77-82`, `appsettings.json:89-114` |
| Prompt cache (Anthropic ephemeral) | **READY (active when Claude is live)** — `cache_control:{type:ephemeral}` applied to the language tone-frame system prompt; unconditional when `CacheableSystemPrompt` is set (it always is, per language). Claude only; no flag. | `ClaudeProvider.cs:164-176`, `PromptBuilder.cs:93-100` |
| Safety layer (toxicity / age / hallucination + regen + fail-closed) | **READY** — all checks on by default; fail-closed on gateway failure; persists `SafetyEvent`. | `SafetyLayer.cs`, `SafetyOptions.cs`, `appsettings.json:159-167` |
| Offline eval harness + admin results endpoint | **READY (offline only)** — 62 cases, ar+en, 100% threshold; `GET /api/Admin/AiSafety/evals`. | `SafetyEvalHarnessTests.cs`, `safety-eval-set.json`, `AdminAiSafetyController.cs`, `IAiSafetyEvalResultsQuery` |
| **Live Gate-B eval (real provider, ar+en)** | **GAP** — no `EvalLive` test class; `DeterministicFakeAiGateway` has no real-provider seam. Requires NEW code (see §6). Interim = manual smoke. | `Fakes/DeterministicFakeAiGateway.cs`, `Ai.EvalTests.csproj`, P6-02 brief |
| `/health` `ai-gateway` probe (P6-05) | **READY** — config-only readiness; Healthy when a provider key is present (with RAG note), Degraded (never 503) when dormant. No paid call. | `AiGatewayHealthCheck.cs`, `AiReadinessProbe.cs`, `IAiReadinessProbe.cs`, `Program.cs:164-176` |
| OTel tracing/metrics | **READY-BUT-DORMANT** — exports only when `OpenTelemetry:Otlp:Endpoint` is set; health probes filtered out; no PII/secret capture. | `Program.cs:64-115` |

> Note on **rate limiting**: `IAiTutorRateLimiter` is Redis-backed (`ai:rl:*`, ~10 req/60s per student) when `ConnectionStrings:Redis` is set, else in-process. (`Ai.Infrastructure/DependencyInjection.cs`, `RedisAiRateLimiter.cs`) — UNVERIFIED whether the per-window limit is config-tunable (it appears hardcoded).

---

## 9. Rollback (revert to dormant) — safe, no deploy

All steps are config/runtime; revert in this order for the cleanest fallback:

1. **Stop grounded answers first:** unset / change `AiHelper__ContextProvider` away from `Rag` (e.g. empty). Restart. The tutor reverts to `EmptyLearningContextProvider` (refuse-and-redirect). This is the safest single first move if RAG quality is bad.
2. **Freeze cached serving (no restart needed):** set `AiHelper__Cache__autoApproveEnabled=false`. New approvals stop and the repository downgrades Approved→PendingReview, restoring pre-activation cache behavior. (`AiResponseCacheRepository.cs:142-156`)
3. **Fully dormant the gateway:** clear `Ai__Providers__Claude__ApiKey` (and OpenAI). Restart. All AI calls return `AiError.Unavailable` → localized fallback. No crash. (`AiGateway` fail path, `SafetyLayer.cs:123-128`)
4. **Optional:** point `Curriculum__Embedding__BaseUrl` to empty to mark RAG dormant at the embedding layer, and tear down the TEI service. Embedded vectors remain in pgvector (harmless; re-flip later without re-embedding if the model version is unchanged).
5. **Verify rolled back:** `GET /health` → `ai-gateway` entry should read `Degraded` ("AI Tutor dormant") once keys are cleared; startup logs should show `[DORMANT]` / EmptyLearningContextProvider active.

Each gate is independent — you can roll back RAG only (step 1) while keeping the provider live, or freeze the cache only (step 2) without touching anything else.
