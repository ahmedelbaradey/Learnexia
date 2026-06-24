# Security Audit — BL-03 Build & query the knowledge graph

- **Auditor:** security-auditor (defensive review only — no code edits)
- **Date:** 2026-06-24
- **Branch:** `feat/BL-03-knowledge-graph` (work uncommitted in the worktree at audit time)
- **Verdict:** **PASS** (0 Critical, 0 High). Medium/Low/Info notes below; one platform-wide rate-limiting gap reaffirmed (pre-existing, not introduced by BL-03).

## Scope reviewed (files / endpoints)

**.NET — Decision-E chokepoint + cross-module seams**
- `Shared.Contracts/Learning/IKnowledgeEdgeWriter.cs`, `IKnowledgeNodeReader.cs` (+ DTOs)
- `Learning.Infrastructure/Contracts/KnowledgeEdgeWriterAdapter.cs`, `KnowledgeNodeReaderAdapter.cs`
- `Curriculum.Infrastructure/Jobs/EdgeInferenceAdvanceService.cs` (untrusted ResultJson consumer)
- `Curriculum.Infrastructure/Features/KGSuggestions/Commands/{Approve,Reject,BuildKnowledgeGraphSuggestions}…Handler.cs`
- `Curriculum.Infrastructure/Features/KGSuggestions/Queries/ListKGSuggestionsQueryHandler.cs`
- `Curriculum.Api/Controllers/KGSuggestionsController.cs`
- `Learning.Api/Controllers/KnowledgeGraphController.cs`
- `Learning.Application/.../Queries/GetRelatedConcepts/*`, `GetRemediationPath/*` + `KnowledgeGraphOptions.cs`
- `Learning.Infrastructure/Service/KnowledgeGraphService.cs`
- DI registrations (`Curriculum.Infrastructure/DependencyInjection.cs`), `appsettings.json`, csproj refs

**Python — `python/curriculum_intelligence/`**
- `inference/lightrag_inferer.py`, `mock_inferer.py`, `factory.py`, `postprocessor.py`, `pipeline.py`, `models.py`, `scoring.py`
- `workers/infer_poller.py`, `app/db.py` (claim/mark SQL), `app/config.py`, `.env.example`, `pyproject.toml`

**Endpoints:**
- `GET  /api/Learning/KnowledgeGraph/RelatedConcepts/{nodeId}` — `[Authorize]` student
- `GET  /api/Learning/KnowledgeGraph/RemediationPath/{nodeId}` — `[Authorize]` student
- `GET/POST /api/curriculum/kg-suggestions` + `/{id}/approve` + `/{id}/reject` + `/build` — all `[Authorize(AdminOnly)]`

## Decision-E invariant verification (PRIMARY)

**PASS — the single-write-path holds.** Traced every code path that could touch `learning.KnowledgeEdge`:
- `EdgeInferenceAdvanceService` writes ONLY `KGSuggestion{Pending}` (`db.KGSuggestions.Add`); it never references `KnowledgeEdges`. Explicit comment + verified code.
- `BuildKnowledgeGraphSuggestionsCommandHandler` writes ONLY a `PipelineJob{infer_edges}` row — no suggestion, no edge.
- `RejectKGSuggestionCommandHandler` stamps `Rejected` only — no edge.
- The Python lane writes NO DB entity rows (only the `PipelineJobs` job row via parameterized `mark_done`/`mark_failed`); pipeline is pure w.r.t. entities.
- The ONLY `KnowledgeEdges.Add` outside the learning module's own `AddKnowledgeEdgeCommandHandler` is `KnowledgeEdgeWriterAdapter.PublishApprovedEdgeAsync`, reachable solely from `ApproveKGSuggestionCommandHandler` (admin-only) → the seam.
- The adapter runs the full guard chain (node existence → cross-language → duplicate → acyclic via `SkillGraphValidator.AssertAcyclic`) BEFORE insert; cycle/cross-language return typed flags, the approve handler maps them to 422 and rolls back the transaction. No bypass.

**No back-door found.** Inference/auto-approval is impossible: Python emits suggestions, .NET re-clamps and queues them Pending, only an admin approve publishes.

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Info | Prompt-injection mitigation on the live LightRAG inferer is strong (system/user split, fenced `<curriculum_nodes>`, `<`/`>` neutralized, closed SkillKey vocabulary, hard drop of out-of-set keys + invalid relationship types, advisory strengths). Live path is devops-gated (mock default; live needs `INFERER_BACKEND=lightrag` AND `ANTHROPIC_API_KEY`). Residual risk is bounded by .NET re-clamp + postprocessor 4-subject drop + admin approval. | `inference/lightrag_inferer.py:_SYSTEM_PROMPT`, `_fence_nodes`, `_map_response` | None required now. Keep prompt-injection re-test on the **live-flip checklist** (same gate as Azure-DI / BGE-M3) before `INFERER_BACKEND=lightrag` ships. |
| 2 | Medium | No rate limiting / throttling on `POST /api/curriculum/kg-suggestions/build`. Each call enqueues an `infer_edges` job (LLM work). An authenticated admin (or compromised admin token) could flood the queue. **Platform-wide gap** — no `AddRateLimiter` anywhere in the codebase; not introduced by BL-03. The brief/plan flagged "rate-limited / system-gated" as a requirement. | `KGSuggestionsController.cs:128` (`Build`) | Add request rate limiting on `/build` (and the wider sensitive-endpoint set) when the platform adopts a rate-limiter. Until then accept as a known risk gated by AdminOnly. Document in HANDOFF. |
| 3 | Low | `KnowledgeEdgeWriterAdapter` collapses three distinct domain errors (missing node, unresolvable subject, cross-language) into the single `CrossLanguage=true` flag (self-documented at lines 65–70). Not a security defect — fails closed (no edge written) — but a missing-node approve returns a misleading 422 "cross-language" message. | `KnowledgeEdgeWriterAdapter.cs:65-70, 95-101` | Optional: add a distinct `NodeMissing` flag to `EdgePublishResult` for a clearer envelope. Behavior is safe as-is. |
| 4 | Low | `GetRemediationPathQueryHandler.LoadNodesAsync` issues one `GetNodeForRemediationAsync` round-trip per result node (N+1). Bounded by `RemediationMaxDepth` (default 3 → ≤ a handful of nodes), so not a practical DoS, but a deeper config or a wide fan-out node multiplies DB round-trips on a student-facing endpoint. | `.../GetRemediationPath/GetRemediationPathQueryHandler.cs` (`LoadNodesAsync`) | Optional: batch-load the result node ids in a single `WHERE Id IN (...)` query. Keep `RemediationMaxDepth` small. |
| 5 | Info | Graph-traversal DoS defended: remediation BFS is depth-capped (`RemediationMaxDepth`, appsettings, default 3) AND cycle-guarded (`visited` HashSet seeded with the start node; back-edges skipped). Prerequisite edges are loaded once and walked in-memory — no per-hop DB hammering. `nodeId` is a route `int`; non-existent → 404; student-safe inactive-skill filter applied to results (no hidden-skill disclosure). | `GetRemediationPathQueryHandler.BfsUpstream` | None. |
| 6 | Info | Untrusted `ResultJson` handled defensively: `JsonDocument.Parse` in try/catch → typed `InferResultValidationException`; `GetStringSafe`/`GetDecimalSafe` tolerate missing/wrong-typed fields; strength + confidence re-clamped to [0,1] server-side (never trust the Python/LLM value); `inference_model` truncated to 128 chars, SkillKeys length-aware; self-loops dropped; malformed edges skipped; bad ResultJson → non-retryable `PermanentlyFailed`; any exception → `TerminateJobOnExceptionAsync` (no-stranding terminal write). No raw `ex.Message` persisted to a client-facing field. | `EdgeInferenceAdvanceService.cs` | None. Matches the BL-02/BL-05 hardening pattern. |
| 7 | Info | Authz matrix correct. Suggestion mgmt + `/build` all `[Authorize(Policy = AdminOnly)]` (controller-level). Query endpoints `[Authorize]` (any authenticated user) with the inactive-skill filter on `RelatedConcepts` + `RemediationPath`. No IDOR: approve/reject load a global suggestion by id but are admin-only (no per-user object ownership applies); KG suggestions carry no child/user PII. | `KGSuggestionsController.cs:32`, `KnowledgeGraphController.cs:43-95` | None. |
| 8 | Info | Module isolation intact — `Curriculum.Infrastructure.csproj` references only Curriculum projects + `Shared.{Kernel,Contracts,Resources}`; NO `Learning` project reference. Cross-module reach is via `IKnowledgeEdgeWriter` / `IKnowledgeNodeReader` (Shared.Contracts) only; all node refs are plain `int` (no cross-module FK/navigation). | `Curriculum.Infrastructure.csproj` | None. |
| 9 | Info | Secrets posture clean. `.env.example` ships empty `ANTHROPIC_API_KEY` / `AZURE_DI_*` placeholders; live deps (`anthropic`, `lightrag-hku`) isolated in the `[live]` extra (mock default needs none); key read env-only (`config.py`), never logged (only the backend *kind* and model name are logged). No JWT/`appsettings` secret changes in this story (auth config untouched; no `CHANGE_ME` introduced). | `.env.example`, `app/config.py`, `factory.py`, `appsettings.json` diff | None. |
| 10 | Info | Python outbox SQL fully parameterized (`%(name)s` placeholders); the only f-string interpolation is `self._table` (an internal `_qualified(schema, table)` constant, not user input). The Python worker never increments `RetryCount` (owned by .NET per ADR-0004 §3); `error_message` truncated to 2048. The .NET claim CTE in `EdgeInferenceAdvanceService` is a static string passed to `SqlQueryRaw` with no interpolation. | `app/db.py`, `EdgeInferenceAdvanceService.cs:125-147` | None. |
| 11 | Info | Postprocessor 4-subject domain restriction + self-loop/dedup cannot be trivially bypassed: `enforce_subject_domain` checks BOTH endpoints' leading SkillKey segment against `{math,science,arabic,english}` and rejects invalid relationship types; `drop_self_loops` removes source==target; both fail-soft (drop, never raise). Crafted input that smuggles a 5th subject is dropped here AND the .NET side resolves SkillKeys against real `KnowledgeNode` rows (unresolved keys dropped). | `inference/postprocessor.py` | None. |

## Dependency scan

- **.NET:** `dotnet list backend/Learnexia.Modular.sln package --vulnerable` → **no vulnerable packages** across all projects (Curriculum, Learning, Shared, and the rest).
- **Python:** `pip-audit` not installed in this environment (offline). Live LLM/OCR deps are confined to the `[live]` extra and not installed in the mock build; base runtime deps (psycopg, etc.) are not network-reachable on this story's surface. Recommend running `pip-audit` in CI for `python/curriculum_intelligence` when the live extra is provisioned.

## Notes / accepted risks

- **Live LightRAG is mocked / devops-gated** (mock default; live requires `INFERER_BACKEND=lightrag` + `ANTHROPIC_API_KEY`, else it degrades to the mock with a warning — CI can never call out). This lowers the prompt-injection finding to **Info** for the shipped build, but the mitigation MUST be re-validated on the **live-flip checklist** before the live inferer is enabled — same posture as Azure-DI (BL-02) and BGE-M3 (P3-07).
- **Defense-in-depth is layered and correct:** untrusted LLM output passes (1) Python postprocessor (4-subject + self-loop + dedup + vocabulary), (2) .NET re-clamp + SkillKey resolution against real nodes, (3) the full edge guard chain inside `learning` on approve, (4) human admin approval. No single layer is the sole gate.
- **Finding #2 (no `/build` rate limiting)** is a pre-existing platform gap, not a BL-03 regression — left as a Medium note for the lead to risk-accept (AdminOnly already restricts the blast radius) and to fold into the platform-wide rate-limiter follow-up.

## Verdict: PASS

No Critical or High findings. The Decision-E no-auto-publish invariant is enforced with a single auditable chokepoint; cross-module isolation, authz, untrusted-input hardening, traversal DoS bounds, prompt-injection isolation, and secrets posture are all sound. Medium #2 and Low #3/#4 are non-blocking; #2 to be tracked, #3/#4 optional polish.
