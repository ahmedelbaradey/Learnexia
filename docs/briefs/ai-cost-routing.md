# Cross-Cutting Strategy — AI Cost & Model Routing

> **Status: APPROVED (revised 2026-06-13 — R1–R6 applied).** The lead has approved this strategy. All AI-tutor stories (P3-01, P3-04, P3-05, P3-06 and downstream P3-07..P3-13) must conform to this document. Implementers: read this before touching any gateway, cache, or LLM call.
>
> Always verify current Anthropic pricing and model IDs before launch — do not treat any cost figure here as a contractual guarantee. Measure real prompts with `count_tokens` (model: `claude-haiku-4-5`) before locking any pricing estimate.

## 1. The Problem: Why This Matters

The Learnexia student plan targets **199 EGP/month (~$4 USD)**. The AI tutor is the product's most differentiating feature. If the AI runtime cost exceeds the plan price, the business is non-viable. The naive path — defaulting all calls to Opus — is 10-50x too expensive for a product at this price point.

### Worked economics (illustrative — verify pricing at launch; measure with `count_tokens` before locking)

| Scenario | Calls/day | Calls/month | Avg tokens (in/out) | Model | Approx cost/student/month |
|---|---|---|---|---|---|
| Worst case (Opus everything) | 20 | 600 | 2 000 in / 300 out | claude-opus-4-8 | ~$5-15 (exceeds plan) |
| Target mix + caching | 20 | 600 | ~200 in (uncached miss) / 300 out | 60% Haiku / 35% Sonnet / 5% Opus | ~$0.10-0.40 |
| Redis cache HIT | 20 | 600 | 0 in / 0 out (served from Redis) | None (cache) | **~$0.00 AI tokens** |

**The primary cost lever is the application-level Redis semantic/content cache.** A cache HIT costs $0 AI tokens — the stored response is served directly; no model call is made. The effective cost per action = miss-rate × uncached token cost. This is the most powerful cost control available: at a 90% cache-hit rate on pre-generated content, the per-student AI token cost approaches zero.

All cost numbers: **verify current Anthropic pricing/model IDs before launch. Measure real prompts with `count_tokens` (model: `claude-haiku-4-5`) before locking any pricing estimate.**

### Cold-start behaviour

When the cache is empty (early deployment, new curriculum content), every request is a cache miss. During this cold-start window the COGS is temporarily higher — approximate uncached cost per action (Haiku 4.5, uncached):

| Action | Approx in tokens | Approx out tokens | Approx uncached cost |
|---|---|---|---|
| Hint | ~1,500 | ~100 | ~0.2¢ |
| Explain mistake | ~1,500–2,000 | ~200 | ~0.33¢ |
| Practice gen (per item) | ~2,000 | ~300 | ~0.5¢ |
| Deep explanation (Sonnet) | ~2,000 | ~500 | ~0.8¢ |

Note: **output tokens dominate** on Haiku (output priced ~5× input). The offline batch pre-generation job fills the cache at content-publish time to minimise the cold-start window. Monitor miss-rate for the first 48 h after each content release.

## 2. Two Cost Levers

### Primary lever — Application-level Redis semantic/content cache

The `ai.AiResponseCache` table (§4) and a Redis read-through layer (keyed by the same cache key) form the primary cost control. On a Redis HIT: the stored `Response` is returned immediately. **No model call is made. No AI tokens consumed.** Cost = $0 AI tokens (pure margin).

Invalidation discipline (see §6) keeps the cache coherent. ReviewStatus gating (§5 / R5) prevents amplifying unreviewed content.

### Secondary lever — Anthropic provider prompt-caching (`cache_control`)

Apply `cache_control` breakpoints on the repeated, static portions of every system prompt **on the miss path** (after a Redis miss has forced a model call):

- The child-safe system prompt prefix (100-200 tokens, identical across all requests).
- The curriculum chunk context for a given lesson (identical for all students studying the same lesson).
- The safety/guardrail prefix.

**Caveats (Haiku):** Haiku's cacheable-prefix floor is approximately 4,096 tokens. For short prompts (hint, classify, short task) the cached prefix will be under this floor and prompt-caching will not fire. Prompt-caching is most effective on multi-turn or large-context prompts. Estimated input-token reduction on cached portions when the floor is met: ~70-90% (verify with Anthropic caching docs at launch).

This applies to runtime calls. Offline Batch API calls benefit from Batch API pricing instead.

## 3. Two Cost Lanes

### Lane A — OFFLINE (batch, amortized, spend-freely)

Content is generated **once**, amortized across all students. Cost per student approaches zero with scale.

- Runs via the **Anthropic Batch API** (~50% cheaper than synchronous calls — verify at launch).
- Models: **Sonnet** (`claude-sonnet-4-6`) for bulk generation; **Opus** (`claude-opus-4-8`) for QA/validation passes.
- Includes: concept-explanation pre-generation, 3-level hint pre-generation, bulk question generation, curriculum QA.
- Output: stored in `ai.AiResponseCache` rows (see §4), tagged `Type` = `Explain` or `Hint`, or `QuizQuestion` rows tagged `GeneratedBy.AI`.
- Pre-generated rows enter with `ReviewStatus = PendingReview`; the Opus QA pass sets them to `Approved`. Only `Approved` rows are served as cache hits (see §5).

### Lane B — RUNTIME (per-student daily, ruthlessly cheap)

Every call in this lane is multiplied by the number of active students. Cost discipline is non-negotiable.

- **Default model for runtime is cheap — NOT Opus.** Opus is the exception (hard reasoning, offline QA only).
- A **Redis + AiResponseCache lookup** precedes every gateway call. On cache hit (`Approved` entry): no LLM call, zero cost.
- On cache miss: route to the cheapest model capable of the task (see §3 ModelRouter table).
- Per-student quota enforced server-side (see §6). On exhaustion: serve cached canned response, never an error.

## 4. ModelRouter — Deterministic Task-Kind Routing

The `AiModelRouter` is a **deterministic, FREE switch on task kind** — it is NOT an LLM call and not a classifier. It is a pure in-memory mapping resolved from `AiGatewayOptions` config. Any change to the routing table requires a config change, not a code change.

### Routing table (default config — override per environment)

| Task kind (`AiTaskKind`) | Default model | Tier | Notes |
|---|---|---|---|
| `CheckAnswer` | `claude-haiku-4-5` | Cheap | Binary/structured; cheap is sufficient |
| `Classify` | `claude-haiku-4-5` | Cheap | Taxonomy/label task |
| `ShortTask` | `claude-haiku-4-5` | Cheap | Any bounded, structured output |
| `Explain` (runtime cache miss) | `claude-sonnet-4-6` | Mid | Arabic pedagogical quality floor; do NOT drop below Sonnet without eval evidence |
| `Hint` (runtime cache miss) | `claude-sonnet-4-6` | Mid | Same floor as Explain; hints must be pedagogically sound |
| `WhyWrong` (always runtime) | `claude-sonnet-4-6` | Mid | Dynamic; never cached per-student; can be cached by compound key (R3) |
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

## 5. Pre-Generation is the Primary Path (Not a Fallback)

Concept explanations and the 3 hint levels per question are **FINITE per grade**. Every combination of `(ConceptId, Grade, Language, Difficulty)` is enumerable offline. Every `(QuestionId, HintLevel)` combination is enumerable offline.

**Architecture consequence:** pre-generate all of these OFFLINE in BATCH, store in `ai.AiResponseCache`, and serve from cache at runtime. The LLM is called at runtime only for the genuinely dynamic remainder — specifically, "why is THIS student's specific wrong answer wrong" (student-answer-specific) and similar-example (runtime-grounded).

### Unified cache table: `ai.AiResponseCache` (replaces former ConceptExplanationCache + HintCache)

A single table in the `ai` schema, `ai.AiResponseCache`, holds all pre-generated and runtime-cached AI responses. The `Type` column discriminates between content categories.

| Column | Type | Notes |
|---|---|---|
| `Id` | bigserial PK | |
| `CacheKey` | varchar(512) UNIQUE | Hash of the canonical key tuple (see per-type key below) |
| `Response` | text | The cached AI-generated content |
| `Type` | smallint | Enum: `Explain=1`, `Hint=2`, `WhyWrong=3`, `Practice=4` |
| `SkillKey` | varchar(256) | Stable semantic skill identifier (from curriculum versioning) |
| `CurriculumVersion` | varchar(64) | The `CurriculumVersion` at generation time |
| `PromptVersion` | varchar(32) | The prompt-template version used; bump invalidates row |
| `ModelVersion` | varchar(100) | Which model generated the response |
| `ReviewStatus` | smallint | Enum: `PendingReview=0`, `Approved=1`, `Rejected=2` |
| `Confidence` | decimal(5,4) nullable | Model-reported confidence (0.0–1.0); drives auto-approval threshold |
| `CreatedAt` | timestamptz | |
| `ApprovedBy` | int nullable | Admin userId (plain int, no cross-module FK) who approved; null = auto-approved |
| `ApprovedAt` | timestamptz nullable | |
| `InvalidatedAt` | timestamptz nullable | Set on invalidation; invalidated rows not served |

**Indexes:** UNIQUE on `CacheKey`; index on `(Type, ReviewStatus)` for admin queue filtering; index on `(SkillKey, CurriculumVersion)` for bulk invalidation.

### Cache key per Type

| Type | Canonical key tuple | Notes |
|---|---|---|
| `Explain` | `(ConceptId, GradeId, Language, Difficulty, PromptVersion, CurriculumVersion)` | Fully enumerable offline |
| `Hint` | `(QuestionId, HintLevel, Language, PromptVersion, CurriculumVersion)` | Fully enumerable offline |
| `WhyWrong` | `(QuestionId, SHA256(NormalizedWrongAnswer), Language, AgeBand, PromptVersion, CurriculumVersion)` | `NormalizedWrongAnswer` = trim/lowercase/canonical; `AgeBand` derived from grade for cross-student reuse |
| `Practice` | `(SkillKey, VariationIndex, Language, PromptVersion, CurriculumVersion)` | Pool of N variations; never 1:1 per student (see §4 Practice Pool) |

The `CacheKey` column stores `SHA256(canonical_key_tuple_as_json)` — a stable, compact unique key. The canonical tuple components are stored separately only for debug/admin visibility; the lookup is always by `CacheKey`.

### Practice generation cache — rotating pool, never 1:1 (R4)

Practice/similar-example entries use `Type=Practice`, keyed by `(SkillKey, VariationIndex, Language, PromptVersion, CurriculumVersion)`. A pool of N variations per skill is pre-generated (not one fixed response per skill). At runtime the handler selects a random variation index from the pool, cycling to keep responses fresh.

**Why not 1:1 per skill:** serving the same fixed example to every child who studies a skill:
- Defeats adaptive practice (all students see an identical worked example).
- Hands every child the same **answer key** — an exam-integrity / academic-dishonesty risk.

**Security-auditor note (R4):** the api-tester and security-auditor must verify that (a) no student receives the same fixed example on every request, (b) the variation pool is rotated rather than always returning index 0, and (c) the cache key cannot be predicted by a student to enumerate the full example pool.

**AC-coverage note:** the acceptance criteria for P3-06 (similar-example, Part B) must include: (a) at least N=3 variations per skill pre-generated, (b) sequential requests from the same student return different variation indices with non-trivial probability, (c) the same variation may be served to different students (pool design, not per-student uniqueness).

### Runtime review gate (R5) — only `Approved` entries amplified

A Redis cache **freezes one AI answer and amplifies it to thousands of children**. A wrong cached hint propagates to every student who hits that cache entry. Rule:

**Only `ReviewStatus = Approved` entries are served as cache hits to other children. `PendingReview` entries are NOT served as cache hits — they are stored as candidates and await approval.**

On a runtime cache miss:
1. Generate → Safety Layer → compute `Confidence`.
2. If `Confidence ≥ threshold` AND safety passed → store with `ReviewStatus = Approved` (auto-approved; the auto-threshold is set in `AiGatewayOptions.AutoApprovalConfidenceThreshold`).
3. Otherwise → store with `ReviewStatus = PendingReview` for human review; the **current student** receives the response (it passed safety), but it is NOT served as a cache hit to subsequent students until approved.

Offline pre-generated entries follow the same gate: they enter as `PendingReview`; the Opus QA pass sets them to `Approved`. Only then do they enter the cache-hit path.

This transforms `ai.AiResponseCache` into a **curated, auditable content bank** — analogous to the `curriculum-system-of-record.md` review-queue pattern. Cached pedagogy is reviewed pedagogy.

See also: `docs/briefs/ai-eval-gate.md` (Gate B — the safety eval must pass before any AI content reaches real students) and `docs/briefs/curriculum-system-of-record.md` (review-queue pattern for AI-generated curriculum artifacts).

## 6. Cache Invalidation and Versioning (R6)

Invalidation triggers — set `InvalidatedAt = NOW()` (soft-invalidation) on rows matching the affected key predicates:

| Trigger | Predicate | Action |
|---|---|---|
| Correct answer changes on a QuizQuestion | `Type IN (Hint, WhyWrong) AND <QuestionId match in CacheKey>` | Invalidate affected rows; re-generate via offline job |
| PromptVersion bumped | `PromptVersion = old_version` | Invalidate all rows with the old prompt version |
| CurriculumVersion switch (new Active version) | `CurriculumVersion = archived_version` | Invalidate all rows for the archived version |
| Manual "reported-bad" purge | Row-level or `SkillKey`-scoped admin action | Set `ReviewStatus = Rejected` + `InvalidatedAt` |

`PromptVersion`, `CurriculumVersion`, and `ModelVersion` on each row make staleness detectable without a full cache flush. Invalidated rows are not served (the lookup queries `WHERE InvalidatedAt IS NULL AND ReviewStatus = Approved`). Invalidated rows are retained for audit; a background cleanup job archives or hard-deletes rows older than a configured retention window.

## 7. Per-Plan Daily Request Cap (MVP Cost-Safety Guardrail)

> **Scope:** This section describes a **minimal cost-safety guardrail only** — a daily request count cap, not a credit economy. The full credit economy (ledger, per-action pricing, purchasable credit packs, monthly grants/reset, billing) is deferred to **Phase 9 (`P9-*` — Payment, Billing & Credits)**. Phase 9 supersedes this guardrail and promotes the concept to a full `ai.AiUsageLedger` / `ai.AiCreditTransaction` system. For reference: the Phase 9 credit amounts are Free 100/mo + 10/day, Premium 5000/mo + 250/day, pack 1000 credits/$5 — cost-routing only references these; enforcement is P9's responsibility.

Enforce server-side in the gateway via `IAiUsageBudget` (see P3-01-BE-14):

| Plan | Daily runtime LLM request cap (illustrative) | On exhaustion |
|---|---|---|
| Free | ~20 runtime LLM calls/day (cached responses unlimited) | Serve cached/canned response — never a hard error |
| Premium AI | ~100 runtime LLM calls/day | Graceful degradation to lower tier model, then cached |

Cap values are illustrative and config-driven — product must set exact numbers (confirm in `AiGatewayOptions`). The mechanism: each runtime gateway call checks the student's remaining **daily request count**; if exhausted, skip the LLM and serve the best available cached response. Never return a hard error to the student — degrade gracefully to the cached canned explanation.

Track consumed requests in the `AiUsageLogs` table (planned for P7-11) or an interim Redis/DB counter. This cap is a temporary guardrail; Phase 9 builds the full billing and credit-accounting system on top of it.

## 8. Eval-Driven Tier Mix Validation

**Do NOT assume** Haiku is adequate for Arabic pedagogical explanation quality without measurement. The explanation floor is Sonnet (see routing table above). The illustrative mix (~60% Haiku / 35% Sonnet / 5% Opus) is a target that must be validated by an eval set before any cost optimization changes the routing table.

**The tier-mix decision is gated by the AI evaluation dataset** — do not trust the 80% Haiku split for Arabic pedagogy until it is measured. See **[docs/briefs/ai-eval-gate.md](ai-eval-gate.md)** for the standing eval dataset that gates (a) this tier-mix decision, (b) safety/age-appropriateness, and (c) Arabic explanation/hint quality. The cost-routing tier split must not be promoted to production until the eval gate passes.

This validation is the responsibility of **P6-02** (AI quality evaluation story). The eval set must cover:
- Arabic explanation quality at Haiku vs Sonnet (acceptability threshold to be defined by the content team).
- Hint quality for common wrong-answer patterns.
- Question generation quality and diversity.

Do not lower the Sonnet floor on `Explain`, `Hint`, or `WhyWrong` task kinds without passing the P6-02 eval gate (see `docs/briefs/ai-eval-gate.md`).

## 9. Open Questions for the Lead

| # | Question | Recommendation |
|---|---|---|
| OQ-A | **Cache module placement:** `ai.AiResponseCache` lives in the `Ai` module schema — confirmed. | Resolved: `ai` module schema. |
| OQ-B | **Batch job host:** Hangfire vs hosted `IHostedService` vs separate worker? | Confirm Hangfire presence/config before the offline batch pre-generation task is dispatched. |
| OQ-C | **`IAiBatchGateway` vs extending `IAiGateway`:** should the Batch API path be a separate interface? | Separate `IAiBatchGateway` keeps the runtime and batch seams independent; confirm before P3-01 backend-feature is dispatched. |
| OQ-D | **Quota store:** Redis counter (fast, ephemeral) vs DB column on the student/plan record vs the P7-11 `AiUsageLogs` table? | Interim: DB column on the plan/subscription record. P7-11 adds the analytics table on top. |
| OQ-E | **Exact quota numbers per plan** (Free vs Premium AI monthly runtime call caps). | Product decision — block the quota-enforcement task until defined. |
| OQ-F | **Auto-approval confidence threshold:** what is the minimum `Confidence` score for a runtime-generated response to be auto-approved? | Recommend 0.85 as a starting value; content team to review and calibrate from the `PendingReview` queue data. |
| OQ-G | **WhyWrong cache pool or single entry?** Per R3, WhyWrong is cacheable by compound key — but it is student-answer-specific. Should there be a per-question hard cap on the number of distinct WhyWrong entries stored? | Recommend a cap of ~50 distinct wrong-answer variants per question to prevent unbounded cache growth; entries evicted LRU beyond the cap. |
| OQ-H | **Practice pool size (N):** how many variation indices per skill should the offline job pre-generate? | Recommend N=5 as MVP; bump to N=10 when the question bank is larger. Confirm with product. |

## 10. Links

This strategy governs the following stories. Each story brief links back here:

- [P3-01 — AI Gateway](P3-01.md) — `ModelRouter`, `IAiGateway`, prompt caching wiring, Batch API path, quota enforcement, graceful degradation.
- [P3-02 — Safety Layer](P3-02.md) — runtime review gate; every generated response must pass safety before `ReviewStatus` transitions to `Approved`.
- [P3-04 — Explain Concept](P3-04.md) — `AiResponseCache` (Type=Explain) primary path, offline pre-generation, runtime cache-miss Sonnet call.
- [P3-05 — Hints & Re-explanation](P3-05.md) — `AiResponseCache` (Type=Hint) primary path; `AiResponseCache` (Type=WhyWrong) compound-key cache (R3).
- [P3-06 — Question Generation](P3-06.md) — Batch API offline generation, Opus QA pass, `AiResponseCache` (Type=Practice) rotating pool (R4).
