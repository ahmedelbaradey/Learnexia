# Phase 4 (AI Tutor) — Wave 4: Hints/WhyWrong/Simplify + SimilarExample (completes the AI Helper)

Backend-only. **Final wave of the Phase-4 (AI Tutor) backend build.** Two stories on the P3-04 explain pipeline; each through the full agent pipeline (implementers → api-tester → mandatory security-auditor → reviewer → committer) and merged into this wave branch with `--no-ff`.

> Branch `feat/phase4-wave-4` (the historical `feat/wave-3`/`wave-2` names are unrelated old project waves). Story IDs `P3-xx` = the AI-Tutor phase.

## Stories in this wave

### P3-05 — Progressive hints + WhyWrong + Simplify (AI Helper intents #2 + #3)
- `GetHintCommand` (Hint | WhyWrong) + `SimplifyExplanationCommand`; handlers via **`ISafetyLayer`** (buffer→safety→emit, never raw tokens); refuse-and-redirect on empty context.
- **No-reveal guard** (normalization-aware: NFC + tashkeel strip + Arabic-Indic digit fold + whitespace/case) so a Hint never contains the `CorrectAnswer`. **IDOR-scoped** `IQuestionAnswerContract` (attempt lookup scoped to the JWT student — refuses cross-student). Server-derived hint level (`MaxHintLevels=3`, config). Usage recorded via `HintUsedIntegrationEvent` → Learning handler (fresh-scope publish + Learning.Infrastructure added to cross-module MediatR scan).
- SSE `POST /api/AiTutor/Hint` (+ Hint `hintLevel` preamble) + `POST /api/AiTutor/Simplify`. 214 unit + 21 SSE integration green. **Mandatory security gate: PASS** (the High IDOR + no-reveal/fail-closed findings were fixed).

### P3-06 Part B — SimilarExample (AI Helper intent #4)
- `SimilarExampleCommand {SkillId, QuestionId?}` + handler via `ISafetyLayer`; refuse-and-redirect; instrumentation; rate-limit.
- SSE `POST /api/AiTutor/SimilarExample` — same wire contract as P3-04, **no hint-level preamble** (the P3-12 FE delta). 219 unit + 13 SSE integration green. **Mandatory security gate: PASS.**
- **Completes the closed-set 4-intent AI Helper:** Explain (P3-04) · Hint + WhyWrong (P3-05) · SimilarExample (P3-06).

## Integrated verification (this wave branch)
- `dotnet build backend/Learnexia.Modular.sln` → **0 errors**. `Modules.Ai.UnitTests` → **219/219**. Per-story integration + mandatory security + reviewer gates all passed before merge (P3-05 21/21 SSE incl. IDOR + no-reveal + usage-recording; P3-06 13/13 SSE).

## Conventions / rules honored
- Module isolation (AiTutor folded into the `Ai` module; cross-module reads only via `Shared.Contracts` seams — `IQuestionAnswerContract`/`ILessonContextContract`/`ILearningContextProvider`; usage write via `HintUsedIntegrationEvent`; NO cross-module FK).
- Handlers obtain AI content ONLY via `ISafetyLayer` (never `IAiGateway` — enforced by the architecture test). SSE is the only new pattern (lead-approved rule-8 exception).
- `BaseResponse<T>`/`Successed`/`NewResult`; `ILoggerManager`; typed SSE errors (no `ex.Message`/stack-trace leak); no UoW misuse.
- **Mandatory child-safety security gate passed for both stories.**

## Deferred / follow-ups (non-blocking)
- **AI cost-economy across P3-04/05/06** — `AiResponseCache` (cache-first + offline Batch pre-generation + Opus QA), `WrongAnswerNormalizer`/compound-key WhyWrong cache, Practice-pool cache, and `IAiUsageBudget` quota — all DEFERRED pending **P3-01-BE-12/13/14**; commented seams are in the handlers.
- **P3-06 Part A** (offline grounded question-generation for the item bank) — deferred (Batch-API primary path + P7-09 moderation destination not ready).
- **Live grounding dormant** — `ILearningContextProvider` defaults to `EmptyLearningContextProvider` (always redirects) until `SeededCorpusContextProvider`/`RagContextProvider` is wired; real Claude keys (env/secret) needed before runtime.
- In-process `AiTutorRateLimiter` → Redis before multi-instance scale-out.
- `PostHelpRetry`/`PostHelpSuccess` closed-loop events (need a `SubmitAnswerCommandHandler` change) deferred.

## Phase-4 backend status after this wave
**Complete: 12/12 backend stories** — P3-01 (Gateway), P3-02 (Safety), P3-03 (Prompt Builder), P3-04 (Explain), P3-05 (Hints/WhyWrong/Simplify), P3-06 (SimilarExample; Part A offline-gen deferred), P3-07 (RAG/Curriculum), P3-08 (Adaptivity), P3-09 (Mastery), P3-10 (Spaced Repetition), P3-11 (Adaptive quiz), P3-13 (Student Profile). (P3-12 is the UI/frontend, out of backend scope.) The **4-intent AI Helper** backend is end-to-end (behind the safety layer, on the seeded-context seam), with the cost-economy + live grounding/keys + offline question-gen as the documented follow-ups.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
