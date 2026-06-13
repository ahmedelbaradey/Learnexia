# Phase 4 (AI Tutor) — Wave 1: AI Gateway + Student Mastery Engine

Backend-only. First wave of the Phase 4 (AI Tutor) backend build. Bundles the two **independent root stories** of the phase, each built through the full agent pipeline (analyzer → planner → implementers → api-tester/security-auditor → reviewer → committer) and merged into this wave branch with `--no-ff`.

> **Naming note:** "Phase 4 = AI Tutor" but its story IDs are `P3-xx` (the IDs were swapped with Phase 3 Gamification — see `user-stories/README.md`). This is intentional and project-wide.

## Stories in this wave

### P3-01 — Route AI requests through an AI Gateway (new `Ai` module)
The foundational seam every AI feature (P3-02..P3-13) calls through. No LLM is ever called directly from a feature handler.

- **New 4-layer `Ai` module** (`.Domain` / `.Application` / `.Infrastructure` / `.Api`) — lead-approved.
- **Frozen contract** in `Shared.Contracts/Ai/`: `IAiGateway`, `AiRequest`, `AiResult` (with `Successed`), `AiError`, `AiUsage`, `AiChunk`, `AiTaskKind`, `AiModelTier`. P3-02/P3-03 can now wire against this.
- **`AiModelRouter`** — task→provider+model routing, config-overridable (cheap `claude-haiku-4-5` for hints/short tasks, mid `claude-sonnet-4-6` for explain/hint/question-gen, premium `claude-opus-4-8` for hard reasoning).
- **`ClaudeProvider` + `OpenAiProvider`** behind `IAiProvider` — thin typed `HttpClient` wrappers (no vendor SDK), provider HTTP/JSON types confined to `Ai.Infrastructure`.
- **`AiGateway` facade** — bounded retry (count + backoff on 429/5xx/timeout), hard per-call timeout (linked CTS), translates every provider failure to a typed `AiError` (never echoes provider body/stack, never throws to caller), captures `AiUsage` (tokens/latency/cost) logged at **Debug** via `ILoggerManager`.
- **Architecture test** asserts provider adapters live in `Ai.Infrastructure` and `IAiGateway` lives in `Shared.Contracts`.
- **Log-only** (no DB table / migration — usage persistence deferred to P7-11). **Endpoint-less** (SSE deferred to P3-04).
- Tests: **29 unit + architecture** green. Security-auditor: **PASS** (0 Critical/High).

### P3-09 — Track per-skill mastery (Student Modeling Engine)
The foundation of the adaptivity cluster — P3-08, P3-10, P3-11, P3-13 depend on it.

- **`StudentSkillMastery`** entity + `MasteryStatus` enum + migration `AddStudentSkillMasteryTable` (unique `(StudentId, SkillId)` index; the three spaced-repetition columns `ReviewIntervalDays`/`NextReviewDueAt`/`RepetitionNumber` **reserved for P3-10** so it needs no second migration on this table).
- **`MasteryEngine`** — pure static cumulative-accuracy engine (same formula as P2-04, no divergence): NotStarted / NeedsReview (<50%) / InProgress (50%–threshold) / Mastered (≥ per-skill `MasteryThreshold`).
- **Write path** — `CompleteAttemptCommandHandler` upserts mastery per distinct skill after attempt completion, **atomically** via the Learning `UnitOfWorkBehavior` ambient transaction (ADR-0001 §2).
- **Read path** — `GET /api/Learning/Mastery` + `GET /api/Learning/Mastery/Skill/{skillId}` (`[Authorize(Roles="Student")]`, IDOR-safe — student id from JWT only, never route/query; unknown skill → NotStarted default, never 500).
- **`IMasteryService`** in-process seam for P3-08/10/11/13.
- Tests: **246 unit (10 MasteryEngine) + 11 integration** green. Security-auditor: **PASS** (0 Critical/High/Medium).

## Integrated verification (this wave branch)
- `dotnet build backend/Learnexia.Modular.sln` → **0 errors** (27 pre-existing warnings).
- `Modules.Ai.UnitTests` → **29/29**. `Modules.Learning.UnitTests` → **246/246**.
- Per-story integration + security gates passed before merge.

## Conventions / rules honored
- Module isolation (`Ai.Infrastructure` → `Shared.Contracts` only; `StudentId` loose `int`, no cross-module FK).
- `BaseResponse<T>` / `Successed` / `NewResult`; `ILoggerManager` (not `ILogger<T>`).
- No Unit of Work added — Learning uses the existing `UnitOfWorkBehavior` per ADR-0001 (note: CLAUDE.md rule #3 wording is stale for new modules; tracked as a non-blocking doc cleanup).
- Only the lead-approved patterns (`IAiProvider` adapter + `AiModelRouter`).

## Deferred / follow-ups (non-blocking)
- **Real provider API keys** (`Ai__Providers__Claude__ApiKey`, `Ai__Providers__OpenAi__ApiKey`) must be supplied via env/secret store **before P3-04 runtime**. None are in the repo.
- **Per-task runtime timeout / retry budget** (NFR-1 <4s) is owned by P3-04 (the tutor endpoint), not P3-01 (endpoint-less).
- Harden the AI architecture test's SDK-namespace scan (currently forward-looking since we use raw `HttpClient`).
- P5-02 cross-module mastery read needs a `Shared.Contracts` seam (not a direct call).

## Unblocks
Merging this wave to `main` unblocks **Wave 2**: P3-02 (Safety Layer) + P3-03 (Prompt Builder) + P3-07 (RAG/Curriculum) off P3-01; P3-08 (Adaptivity) + P3-10 (Spaced Repetition) + P3-13 (Student Profile) off P3-09.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
