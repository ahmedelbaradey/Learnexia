# Phase 4 (AI Tutor) — Wave 3: Explain endpoint + Adaptive quiz selection

Backend-only. Third wave of the Phase 4 (AI Tutor) backend build — the two stories unblocked by Wave 2 (P3-04 needs P3-02/03; P3-11 needs P3-08). Each built through the full agent pipeline (analyzer/planner briefs pre-existed → implementers → api-tester → security-auditor → reviewer → committer) and merged into this wave branch with `--no-ff`.

> **Naming:** "Phase 4 = AI Tutor", story IDs `P3-xx`. This branch is `feat/phase4-wave-3` (distinct from the historical `feat/wave-3` = old P1-03/P1-05, already in main).

## Stories in this wave

### P3-11 — Serve adaptive quizzes (adaptive question selection) — Learning module
- Pure static `QuizSelectionEngine` (70/30 weighted difficulty mix, deterministic sort-by-Id), config-bound `QuizSelectionOptions`.
- `StartAttemptCommandHandler` wires `IAdaptivityService.GetTargetDifficulty` (P3-08) + selection; persists `Attempt.ServedDifficultyMix` (jsonb) + `Attempt.TargetDifficulty` (int) — both nullable/backfill-safe (migration `AddAttemptServedDifficultyMix`).
- Existing StartAttempt guards (lifecycle/IsActive/language/resume) preserved; resume reproduces the same set deterministically (mix persisted on start only).
- Tests: **311 unit (9 QuizSelectionEngine) + 8 integration** green. Reviewer PASS; security PASS (inline, low-sensitivity).

### P3-04 — Explain a concept on demand (AI tutor SSE endpoint, MVP slice) — Ai module
The first AI tutor endpoint. **Lead decisions:** folded into the existing `Ai` module (no new module); **SSE from day 1** (approved rule-8 exception); **MVP slice** (cache economy deferred).
- `ExplainConceptCommand` + validator; handler orchestrates `ILearningContextProvider` → `IPromptBuilder` (HelperIntent.Explain) → **`ISafetyLayer`** (buffer→safety→emit — never raw LLM tokens; handler never calls `IAiGateway`, enforced by the arch test), with **refuse-and-redirect** on empty context (no LLM call).
- SSE `POST /api/AiTutor/Explain` (`[Authorize(Roles="Student")]`) with the **pinned wire contract** (`event: message`/`redirect`/`error`/`done` + `[DONE]`) — published in HANDOFF for the P3-12 FE batch. Typed error codes (`ValidationError`/`UnhandledError` with a generic safe message; exceptions logged server-side — no info leak).
- `ILessonContextContract` cross-module seam (Learning→Ai via `Shared.Contracts`); `RedirectResponseBuilder` (ar/en); `Help*` instrumentation events; in-process `AiTutorRateLimiter` (per-student, JWT-keyed).
- Tests: **208 unit (5 handler-branch + arch) + 13 SSE integration** green. **Mandatory child-safety security gate: PASS** (no safety bypass, prompt-injection-resistant, child-data minimized, rate-limited, IDOR-safe; the Medium error-leak + D-1 typed-validation findings were fixed).

## Integrated verification (this wave branch)
- `dotnet build backend/Learnexia.Modular.sln` → **0 errors** (clean; pre-existing warnings only).
- `Modules.Ai.UnitTests` → **208/208**. `Modules.Learning.UnitTests` → **311/311**. Per-story integration + security/reviewer gates passed before merge (P3-04 13/13 SSE, P3-11 8/8) on this exact code (merges were conflict-free).

## Conventions / rules honored
- Module isolation (P3-04 reaches Learning only via `ILessonContextContract` in `Shared.Contracts`; no cross-module FK; P3-11 Learning-only).
- `BaseResponse<T>`/`Successed`/`NewResult` (SSE deliberately bypasses the envelope — the only new pattern, lead-approved); `ILoggerManager`; no UoW misuse; deterministic engines are plain static (rule 8).
- Mandatory security gate passed for P3-04; light/inline pass for P3-11.

## Deferred / follow-ups (non-blocking)
- **P3-04 cache economy + quota** (`AiResponseCache` table/repo, offline Batch pre-generation, `IAiUsageBudget`) — needs unbuilt **P3-01-BE-12/13/14**; commented seams are in the handler at the right insertion points.
- **P3-04 live grounding is dormant** — `ILearningContextProvider` defaults to `EmptyLearningContextProvider` (always redirects) until `SeededCorpusContextProvider`/`RagContextProvider` is wired; happy path proven with stubs. Real Claude keys (env/secret) needed before live runtime.
- **Rate limiter is in-process** — swap to Redis before multi-instance deploy.
- Platform backlog (auditor-flagged, pre-existing): CORS `*`+credentials, `RequireHttpsMetadata=false`.

## Unblocks
Merging this wave leaves **Wave 4** as the last of Phase-4 backend: **P3-05** (Hints + WhyWrong — shares the P3-04 explain pipeline) and **P3-06** (Generate curriculum-grounded questions). After Wave 4, Phase-4 backend is complete (12/12; P3-12 is UI).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
