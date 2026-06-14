# Phase 4 (AI Tutor) — Wave 2: Adaptivity cluster + AI Safety/Prompt + RAG

Backend-only. Second wave of the Phase 4 (AI Tutor) backend build — the 6 stories that depend on the Wave 1 roots (P3-01 AI Gateway, P3-09 Mastery, merged via #126). Each was built through the full agent pipeline (analyzer/planner briefs pre-existed → implementers → api-tester/security-auditor → reviewer → committer) and merged into this wave branch with `--no-ff`. Latest `main` (#127/#128/#129) is integrated.

> **Naming:** "Phase 4 = AI Tutor", story IDs are `P3-xx` (swapped with Phase-3 Gamification). Intentional, project-wide. (This is distinct from the historical "Wave 2 = P1-02/P1-04" — see `docs/pr/wave-2.md`.)

## Stories in this wave

### Adaptivity cluster (extends the Learning module; off P3-09)
- **P3-08 — Adaptivity Engine.** Pure static weighted-score engine (accuracy/time/hints/retries → difficulty), config-bound `AdaptivityOptions`, `IAdaptivityService` seam (for P3-11), inspection endpoint. No migration. 273 unit + 8 integration green. Security: PASS (inline).
- **P3-10 — Spaced Repetition.** `SpacedRepetitionEngine` (IsDue + expanding ladder 1/3/7/14/30), Hangfire `SR-Sweep` job (idempotent), interval-progression hook in `CompleteAttempt` (rides P3-09's txn), `GET /Reviews/Due`. No new migration (SR columns reserved by P3-09). 286 unit + 8 integration green. Security: PASS (inline). SM-2 deferred.
- **P3-13 — Behavioral Student Profile.** `StudentLearningProfile` (jsonb) + migration; pure static `StudentProfileEngine` (4 rule-based derivations: question-type affinity / recurring-error clusters / attention-span / explanation-style); `SP-Recompute` job + completion hook; data-minimized `GET /Profile`. 302 unit + 8 integration green. **Mandatory child-privacy security gate: PASS** (data minimization, IDOR, grade-transition preservation). `ExplanationStyle` provisional pending P3-03.

### AI cluster (extends the `Ai` module; off P3-01)
- **P3-02 — AI Safety Layer.** `ISafetyLayer` facade (the ONLY AI-content exit — arch test forbids direct `IAiGateway` use elsewhere) + 3 composable fail-closed checks (toxicity/age/hallucination, injection-fenced judge prompts) + `ai.SafetyEvents` PII-light table + new `AiDbContext` + eval harness (`Category=Eval`, needs live keys). 37 unit+arch green. **Mandatory safety gate: PASS** (no-bypass, fail-closed, no-leak, PII-light).
- **P3-03 — Prompt Builder.** `IPromptBuilder` + 4-subject ar/en templates (no Social Studies) + unconditional anti-injection tone frame + graceful degradation + `HelperIntent`; `Shared.Contracts` seams (`IStudentWeakAreasQuery`→P3-09, `ICurriculumContextQuery`/`ILearningContextProvider`→P3-07, `IChildLearningProfileQuery`→P3-04) with empty stubs. 203 unit green. **Mandatory gate: PASS** (PII-minimal prompt, FR-AI-6). `SeededCorpusContextProvider` deferred to P3-07.

### RAG (new `Curriculum` module; off P3-01)
- **P3-07 — RAG Retrieval.** New 4-layer `Curriculum` module + pgvector 3-table schema (`CurriculumChunk` no inline vector / `CurriculumVersion` / separate `chunk_embeddings_bge_m3` `vector(1024)` + HNSW cosine) + `RetrieveChunksQuery` (server-side `<=>`, Active-version + grade/subject/skill filter + similarity floor) + dev `GET /api/Curriculum/Retrieve` + seeder + `RagContextProvider` (implements `ILearningContextProvider` + `ICurriculumContextQuery`; `IChunkRetrievalContract` dropped). 11/11 integration green vs real pgvector. Reviewer PASS; light security PASS.
  - ⚠️ **Embeddings are deterministic PLACEHOLDERS (`seed-placeholder-v0`) — semantic RAG is NOT live until P3-07-BE-0 (BGE-M3 TEI endpoint on Hetzner) is provisioned and rows are re-embedded.** Activate with `AiHelper:ContextProvider="Rag"` + `Curriculum:Embedding:*` + re-tuned `Curriculum:Retrieval:SimilarityDistanceFloor`. RAG *mechanics* are complete + tested; *semantics* are dormant pending BE-0.

## Integrated verification (this wave branch)
- `dotnet build backend/Learnexia.Modular.sln` → **0 errors** (clean rebuild; 28 pre-existing warnings).
- `Curriculum.IntegrationTests` → **11/11** vs real pgvector. Per-story unit/integration + security gates all passed before merge.
- Latest `main` merged in (#127 onboarding, #128 RTL polish + Identity language endpoint, #129 AI-spec docs) — no code conflicts (HANDOFF resolved).
- Integration fix on this branch: added the missing `Curriculum.Application → Curriculum.Domain` ProjectReference (the scaffold omitted it; earlier per-story builds passed on stale `obj/`). Clean rebuild green.

## Conventions / rules honored
- Module isolation (new `Curriculum` module → `Shared.Contracts` only; loose-int hierarchy refs, no cross-module FK; cross-module seams in `Shared.Contracts`).
- `BaseResponse<T>`/`Successed`/`NewResult`; `ILoggerManager`; ADR-0001 (no UoW misuse); deterministic engines (rule 8 — all four engines are plain static classes, no Strategy/Visitor).
- Mandatory security gates passed for P3-02, P3-03, P3-13; light pass for P3-07/P3-08/P3-10.

## Deferred / follow-ups (non-blocking)
- **P3-07-BE-0 (devops):** stand up the BGE-M3 TEI endpoint + re-embed placeholder rows with real BGE-M3 (parity stamp makes the swap detectable). Until then RAG is mechanically wired but semantically dormant.
- Real provider API keys (Claude/OpenAI + TEI) needed via env/secret before P3-04 runtime + the `Category=Eval` safety harness.
- P3-07 retrieval handler lives in `Curriculum.Infrastructure` (needs DbContext+pgvector) — registered via the Host scanning both Curriculum assemblies; optional future refactor to an Application-layer repository abstraction.

## Unblocks
Merging this wave unblocks **Wave 3**: P3-04 (Explain — wired through `ISafetyLayer` + `IPromptBuilder` + `SeededCorpusContextProvider`/RAG) and P3-11 (Adaptive quiz selection — consumes `IAdaptivityService`).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
