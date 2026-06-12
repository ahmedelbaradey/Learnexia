# Cross-Cutting Strategy — AI Cost & Model Routing

> **Status: APPROVED.** The lead has approved this strategy. All AI-tutor stories (P3-01, P3-04, P3-05, P3-06 and downstream P3-07..P3-13) must conform to this document. Implementers: read this before touching any gateway, cache, or LLM call.
>
> Always verify current Anthropic pricing and model IDs before launch — do not treat any cost figure here as a contractual guarantee.

## 1. The Problem: Why This Matters

The Learnexia student plan targets **199 EGP/month (~$4 USD)**. The AI tutor is the product's most differentiating feature. If the AI runtime cost exceeds the plan price, the business is non-viable. The naive path — defaulting all calls to Opus — is 10-50x too expensive for a product at this price point.

### Worked economics (illustrative — verify pricing at launch)

| Scenario | Calls/day | Calls/month | Avg tokens (in/out) | Model | Approx cost/student/month |
|---|---|---|---|---|---|
| Worst case (Opus everything) | 20 | 600 | 2 000 in / 300 out | claude-opus-4-8 | ~$5-15 (exceeds plan) |
| Target mix + caching | 20 | 600 | ~200 in (cached) / 300 out | 60% Haiku / 35% Sonnet / 5% Opus | ~$0.10-0.40 |
| Pre-generated + cache hit | 20 | 600 | 0 in / 0 out (served from DB) | None (cache) | ~$0.00 |

The target mix and the "0 cost on cache hit" path together make the 199 EGP plan economically viable. Opus-everything does not.

All cost numbers: **verify current Anthropic pricing/model IDs before launch.**

## 2. Two Cost Lanes

### Lane A — OFFLINE (batch, amortized, spend-freely)

Content is generated **once**, amortized across all students. Cost per student approaches zero with scale.

- Runs via the **Anthropic Batch API** (~50% cheaper than synchronous calls — verify at launch).
- Models: **Sonnet** (`claude-sonnet-4-6`) for bulk generation; **Opus** (`claude-opus-4-8`) for QA/validation passes.
- Includes: concept-explanation pre-generation, 3-level hint pre-generation, bulk question generation, curriculum QA.
- Output: stored in the `ConceptExplanationCache` and `HintCache` tables (see §4), or `QuizQuestion` rows tagged `GeneratedBy.AI`.

### Lane B — RUNTIME (per-student daily, ruthlessly cheap)

Every call in this lane is multiplied by the number of active students. Cost discipline is non-negotiable.

- **Default model for runtime is cheap — NOT Opus.** Opus is the exception (hard reasoning, offline QA only).
- A cache lookup precedes every gateway call. On cache hit: no LLM call, zero cost.
- On cache miss: route to the cheapest model capable of the task (see §3 ModelRouter table).
- Per-student quota enforced server-side (see §5). On exhaustion: serve cached canned response, never an error.

## 3. ModelRouter — Deterministic Task-Kind Routing

The `AiModelRouter` is a **deterministic, FREE switch on task kind** — it is NOT an LLM call and not a classifier. It is a pure in-memory mapping resolved from `AiGatewayOptions` config. Any change to the routing table requires a config change, not a code change.

### Routing table (default config — override per environment)

| Task kind (`AiTaskKind`) | Default model | Tier | Notes |
|---|---|---|---|
| `CheckAnswer` | `claude-haiku-4-5` | Cheap | Binary/structured; cheap is sufficient |
| `Classify` | `claude-haiku-4-5` | Cheap | Taxonomy/label task |
| `ShortTask` | `claude-haiku-4-5` | Cheap | Any bounded, structured output |
| `Explain` (runtime cache miss) | `claude-sonnet-4-6` | Mid | Arabic pedagogical quality floor; do NOT drop below Sonnet without eval evidence |
| `Hint` (runtime cache miss) | `claude-sonnet-4-6` | Mid | Same floor as Explain; hints must be pedagogically sound |
| `Simplify` | `claude-sonnet-4-6` | Mid | Re-explanation for struggling student |
| `QuestionGeneration` (runtime) | `claude-sonnet-4-6` | Mid | Exceptional path only (see §4 for offline-primary rule) |
| `AnalyzeDiagram` | `claude-opus-4-8` | Premium | Vision + hard reasoning |
| `HardReasoning` | `claude-opus-4-8` | Premium | Complex multi-step |
| `ContentQA` | `claude-opus-4-8` | Premium | OFFLINE batch QA only |

**Model IDs per CLAUDE.md hard facts:**
- Haiku: `claude-haiku-4-5`
- Sonnet: `claude-sonnet-4-6`
- Opus: `claude-opus-4-8`

All model IDs: **verify current Anthropic model IDs before launch.**

### Escalate-on-failure

When the cheap-tier model returns a low-confidence or pedagogically insufficient response (detected by a structured confidence field in the response, or when the student signals confusion after a hint), the gateway escalates one tier:

- Haiku → Sonnet on low confidence or repeated student confusion.
- Sonnet → Opus on hard-reasoning tasks where Sonnet confidence is low (rare at runtime).

Escalation is bounded: maximum one escalation per call chain; never escalate a runtime hint to Opus without explicit config override.

**Correcting the earlier P3-01 brief:** the prior wording listed `claude-opus-4-8` as the default (`Q4`). The approved strategy is: **cheap by default at runtime, escalate only on failure/hard-task.** Opus is reserved for offline QA and genuinely hard reasoning. The task-file decisions table for P3-01 is updated accordingly.

## 4. Pre-Generation is the Primary Path (Not a Fallback)

Concept explanations and the 3 hint levels per question are **FINITE per grade**. Every combination of `(ConceptId, Grade, Language, Difficulty)` is enumerable offline. Every `(QuestionId, HintLevel)` combination is enumerable offline.

**Architecture consequence:** pre-generate all of these OFFLINE in BATCH, store in cache tables, and serve from cache at runtime. The LLM is called at runtime only for the genuinely dynamic ~10-20% — specifically, "why is THIS student's specific wrong answer wrong" (student-answer-specific explain/hint).

### Cache tables (new, db-migration required — see §6 for module placement)

**`ConceptExplanationCache`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `ConceptId` | int | Not a FK across modules — plain int reference |
| `GradeId` | int | |
| `Language` | varchar(5) | `ar` or `en` |
| `Difficulty` | smallint | 1-3 |
| `Content` | text | Pre-generated explanation |
| `ModelId` | varchar(100) | Which model generated it |
| `GeneratedAtUtc` | timestamptz | |
| `BatchJobId` | varchar(200) | Anthropic Batch API job reference |
| `QaPassedAt` | timestamptz? | Null until Opus QA pass |

**`HintCache`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `QuestionId` | int | Not a FK across modules — plain int |
| `HintLevel` | smallint | 1, 2, or 3 |
| `Language` | varchar(5) | `ar` or `en` |
| `Content` | text | Pre-generated hint text |
| `ModelId` | varchar(100) | |
| `GeneratedAtUtc` | timestamptz | |
| `BatchJobId` | varchar(200) | |
| `QaPassedAt` | timestamptz? | |

Cache invalidation: on `QuizQuestion` update (correct answer changes), invalidate `HintCache` rows for that `QuestionId`. On curriculum content update, mark affected `ConceptExplanationCache` rows stale (add a `StaleAt` column).

### Offline batch pre-generation job

- A background job (Hangfire or equivalent, confirm with lead — see Open Questions §7) enumerates all `(ConceptId, Grade, Language, Difficulty)` and `(QuestionId, HintLevel)` combinations not yet cached.
- Submits to the **Anthropic Batch API** (Sonnet for generation; Opus for QA validation).
- On job completion, inserts/upserts cache rows.
- This job runs once at content-load time and again whenever curriculum content changes.

### Runtime cache lookup (before any gateway call)

For `Explain` and `Hint` calls:
1. Check `ConceptExplanationCache` / `HintCache` by key. On hit: return cached content immediately. Zero LLM cost.
2. On miss: call the gateway (runtime lane, Sonnet default). Cache the result async (fire-and-forget) so subsequent students hit cache.
3. For student-answer-specific explains ("why is my answer wrong"): always runtime (no cache key; the student's specific wrong answer is the unique input).

## 5. Anthropic Cost Features

### Prompt caching (`cache_control`)

Apply `cache_control` breakpoints on the repeated, static portions of every system prompt:
- The child-safe system prompt prefix (100-200 tokens, identical across all requests).
- The curriculum chunk context for a given lesson (identical for all students studying the same lesson).
- The safety/guardrail prefix.

Estimated input-token reduction on cached portions: ~90% (verify with Anthropic caching docs at launch). This applies to runtime calls; offline Batch API calls benefit from Batch API pricing instead.

### Batch API

All OFFLINE generation goes through the Anthropic Batch API:
- Submit a batch of prompts → get results asynchronously.
- Approximate cost saving: ~50% vs synchronous (verify at launch).
- Required for: concept-explanation pre-generation, hint pre-generation, bulk question generation.
- The gateway must expose a `SubmitBatchAsync(IReadOnlyList<AiRequest>) → BatchJobId` path (add to `IAiGateway` or a separate `IAiBatchGateway`).

## 6. Per-Plan Daily Request Cap (MVP Cost-Safety Guardrail)

> **Scope:** This section describes a **minimal cost-safety guardrail only** — a daily request count cap, not a credit economy. The full credit economy (ledger, per-action pricing, purchasable credit packs, monthly grants/reset, billing) is deferred to **Phase 9 (`P9-*` — Payment, Billing & Credits)**. Phase 9 supersedes this guardrail and promotes the concept to a full `ai.AiUsageLedger` / `ai.AiCreditTransaction` system.

Enforce server-side in the gateway via `IAiUsageBudget` (see P3-01-BE-14):

| Plan | Daily runtime LLM request cap (illustrative) | On exhaustion |
|---|---|---|
| Free | ~20 runtime LLM calls/day (cached responses unlimited) | Serve cached/canned response — never a hard error |
| Premium AI | ~100 runtime LLM calls/day | Graceful degradation to lower tier model, then cached |

Cap values are illustrative and config-driven — product must set exact numbers (confirm in `AiGatewayOptions`). The mechanism: each runtime gateway call checks the student's remaining **daily request count**; if exhausted, skip the LLM and serve the best available cached response. Never return a hard error to the student — degrade gracefully to the cached canned explanation.

Track consumed requests in the `AiUsageLogs` table (planned for P7-11) or an interim Redis/DB counter. This cap is a temporary guardrail; Phase 9 builds the full billing and credit-accounting system on top of it.

## 7. Eval-Driven Tier Mix Validation

**Do NOT assume** Haiku is adequate for Arabic pedagogical explanation quality without measurement. The explanation floor is Sonnet (see routing table above). The illustrative mix (~60% Haiku / 35% Sonnet / 5% Opus) is a target that must be validated by an eval set before any cost optimization changes the routing table.

**The tier-mix decision is gated by the AI evaluation dataset** — do not trust the 80% Haiku split for Arabic pedagogy until it is measured. See **[docs/briefs/ai-eval-gate.md](ai-eval-gate.md)** for the standing eval dataset that gates (a) this tier-mix decision, (b) safety/age-appropriateness, and (c) Arabic explanation/hint quality. The cost-routing tier split must not be promoted to production until the eval gate passes.

This validation is the responsibility of **P6-02** (AI quality evaluation story). The eval set must cover:
- Arabic explanation quality at Haiku vs Sonnet (acceptability threshold to be defined by the content team).
- Hint quality for common wrong-answer patterns.
- Question generation quality and diversity.

Do not lower the Sonnet floor on `Explain` or `Hint` task kinds without passing the P6-02 eval gate (see `docs/briefs/ai-eval-gate.md`).

## 8. Open Questions for the Lead

| # | Question | Recommendation |
|---|---|---|
| OQ-A | **Cache module placement:** should `ConceptExplanationCache` and `HintCache` live in the `Ai` module schema (`ai.concept_explanation_cache`) or the `Curriculum` module schema (`curriculum.concept_explanation_cache`)? | `Ai` module schema — these are AI-generated artifacts, not curriculum source-of-truth data. Curriculum owns the concept; Ai owns the generated explanation. |
| OQ-B | **Batch job host:** Hangfire (already referenced in backend) vs a simple hosted `IHostedService` vs a separate worker? | Confirm Hangfire presence/config before the offline batch pre-generation task is dispatched. |
| OQ-C | **`IAiBatchGateway` vs extending `IAiGateway`:** should the Batch API path be a separate interface or a method on the existing `IAiGateway`? | Separate `IAiBatchGateway` keeps the runtime and batch seams independent; confirm before P3-01 backend-feature is dispatched. |
| OQ-D | **Quota store:** Redis counter (fast, ephemeral) vs DB column on the student/plan record vs the P7-11 `AiUsageLogs` table? | Interim: DB column on the plan/subscription record (avoids new infra). P7-11 adds the analytics table on top. |
| OQ-E | **Exact quota numbers per plan** (Free vs Premium AI monthly runtime call caps). | Product decision — not a technical one. Block the quota-enforcement task until defined. |

## 9. Links

This strategy governs the following stories. Each story brief links back here:

- [P3-01 — AI Gateway](P3-01.md) — `ModelRouter`, `IAiGateway`, prompt caching wiring, Batch API path, quota enforcement, graceful degradation.
- [P3-04 — Explain Concept](P3-04.md) — `ConceptExplanationCache` primary path, offline pre-generation, runtime cache-miss Sonnet call.
- [P3-05 — Hints & Re-explanation](P3-05.md) — `HintCache` primary path, offline pre-generation, runtime cache-miss Sonnet call.
- [P3-06 — Question Generation](P3-06.md) — Batch API offline generation, Opus QA pass, runtime exception-only.
