# Curriculum Intelligence Pipeline — Live-Activation Runbook (devops)

**Audience:** the lead / devops engineer flipping the Curriculum Intelligence pipeline from its shipped **mocked** state to **live** external models. **No code change is required to ship the pipeline** — it is fully built, tested, and merged against deterministic mocks. Going live is a **config + secret + image-rebuild** operation, stage by stage.

**Scope:** the three devops-gated AI stages of `python/curriculum_intelligence/` — **parse (BL-02, Azure Document Intelligence)**, **ingest (BL-05, Claude extraction)**, **infer-edges (BL-03, LightRAG/Claude)**. For the **runtime embedding endpoint (BGE-M3 / TEI)** used by retrieval and the AI Tutor, see the separate [AI-ACTIVATION-RUNBOOK.md](AI-ACTIVATION-RUNBOOK.md) — that is a different service (Hetzner TEI) and is **not** part of this pipeline. Architecture of record: [adr/0004-python-curriculum-pipeline-service.md](adr/0004-python-curriculum-pipeline-service.md).

> **Golden rule — these activations DO NOT change the human-review gates.** Auto-extracted content and inferred edges *always* land in the `IngestionReviewItem` / `KGSuggestion` review queues and are published to live curriculum / `KnowledgeEdge` **only** by an admin approval. Going live makes the *proposals* real; it never makes them auto-publish. (Decision E / Q5.)

---

## 0. How the gating works (read first)

Every external-model call sits behind an interface with a deterministic **mock as the default**. The backend is selected by an env var; the mock is chosen unless the backend is explicitly set **AND** the required key is present — otherwise the code **degrades to mock with a warning** (it never silently calls out, and CI can never call a live model).

| Stage | Lane / service | Backend selector env | Live value | Default |
|---|---|---|---|---|
| Parse (BL-02) | `parse` lane → `parsers/factory.py` | `PARSER_BACKEND` | `azure_di` | `mock` |
| Ingest (BL-05) | `ingest` lane → `ingestion/factory.py` | `EXTRACTOR_BACKEND` | `claude` | `mock` |
| Infer edges (BL-03) | `infer_edges` lane → `inference/factory.py` | `INFERER_BACKEND` | `lightrag` | `mock` |

The **.NET side never calls a model** — `IParsingServiceClient` / `IIngestionServiceClient` / `IGraphInferenceClient` are `NoOp*` test seams only. All model work is in the Python worker; the .NET advance services (`ParseJobAdvanceService`, `IngestJobAdvanceService`, `EdgeInferenceAdvanceService`) consume whatever the Python lane writes to `PipelineJobs.ResultJson`, re-clamp/validate it, and write to the review queues. **So a live flip is entirely a `python/curriculum_intelligence/` + compose concern; the .NET deploy is unchanged.**

### Shared prerequisites for ANY live stage

1. **Rebuild the worker image with the `[live]` extra.** The shipped image installs only the base deps; the live SDKs (`azure-ai-documentintelligence`, `anthropic`, `lightrag-hku`) live in the `[live]` extra in `pyproject.toml` and are **not** in the running image. Build with the extra (e.g. `pip install ".[live]"` in the Dockerfile / a build arg) before flipping any backend, or the lane will fail to import the SDK and degrade back to mock.
2. **Secrets via the secret store / env — NEVER committed.** All keys below are injected as `${VAR}` in `docker/docker-compose.yaml` and resolved from the deploy environment. `.env.example` carries placeholders only. Confirm no key is echoed in logs (the code does not log keys).
3. **Compose env passthrough.** `docker/docker-compose.yaml` (service `curriculum-intelligence`) currently wires `PARSER_BACKEND`, `INFERER_BACKEND`, `AZURE_DI_ENDPOINT`, `AZURE_DI_KEY`, `ANTHROPIC_API_KEY`. ⚠️ **`EXTRACTOR_BACKEND` is NOT yet passed through** — see Stage 2; you must add it before the ingest flip.
4. **Activate in pipeline order** — Stage 1 (parse) → Stage 2 (ingest) → Stage 3 (infer). Each stage's live output is the next stage's input; flipping ingest live while parse is still mock just means ingest runs on mock-parsed artifacts.
5. **A live-flip security re-audit is MANDATORY per stage** before that stage processes real uploads — see §4. These items were explicitly deferred at build time because the live code paths don't execute under the mock.

---

## 1. Stage 1 — Parse (BL-02): Azure Document Intelligence

Turns uploaded PDFs/DOCX/images (incl. scanned **Arabic**) into the structured parse artifact.

**Provision**
- An **Azure Document Intelligence** resource (formerly Form Recognizer). Capture its **endpoint** + **key**.
- Confirm the model id + locale: defaults `AZURE_DI_MODEL_ID=prebuilt-layout`, `AZURE_DI_LOCALE=ar` (override only if you have a reason).

**Arabic OCR benchmark gate (PY-1b/PY-7) — clear BEFORE processing real curriculum**
- Drop 20–30 representative Arabic sample pages into `python/curriculum_intelligence/benchmarks/samples/` (lead-supplied; not committed).
- Run the offline benchmark runner (`benchmarks/benchmark_runner.py`) against the live Azure DI endpoint; it currently emits a **PENDING** report until samples + a key exist. Confirm accuracy meets the bar before flipping production traffic. This gates **AC1/AC10** ("mock-satisfied; live-pending").

**Flip (env — already wired in compose)**
```
PARSER_BACKEND=azure_di
AZURE_DI_ENDPOINT=<your endpoint>
AZURE_DI_KEY=<secret>
# optional overrides:
AZURE_DI_MODEL_ID=prebuilt-layout
AZURE_DI_LOCALE=ar
```
Rebuild the image with `[live]`, redeploy the `curriculum-intelligence` service.

**Verify**
- `/health` on :8091 healthy; worker logs show the Azure backend selected (no "degraded to mock" warning).
- Upload a real document via `POST /api/curriculum/documents` (admin); watch a `PipelineJobs{JobType='parse'}` row go `Pending → Processing → Done`; the document's `Status` advances and `ParsedArtifactObjectKey` is set; a parse artifact appears in the `curriculum` MinIO bucket.
- A failed parse → document `Failed` + diagnostics (bounded), not a stranded `Processing` row.

**Rollback:** set `PARSER_BACKEND=mock`, redeploy. In-flight jobs finish on whatever backend claimed them; new jobs use the mock.

**⚠️ Re-audit before production (deferred BL-02 items):** download/decompression **size bound** (zip-bomb on DOCX) and **XXE** on DOCX/XML parsing — these code paths only run live. See §4.

---

## 2. Stage 2 — Ingest (BL-05): Claude hierarchy extraction

Turns the parse artifact into the pedagogical hierarchy (Unit→Lesson→Concept→Skill) + semantic chunks, with per-item confidence.

**Provision**
- An **Anthropic API key**. Model default `ANTHROPIC_MODEL=claude-sonnet-4-6` (the cost-routing default; confirm the tier + a cost ceiling for full-textbook ingestion before opening the firehose).

**⚠️ Compose passthrough gap — do this first**
`EXTRACTOR_BACKEND` is **not** currently in the `curriculum-intelligence` env block. Add it (mirroring the `PARSER_BACKEND` line) so it can be flipped:
```yaml
      - EXTRACTOR_BACKEND=${EXTRACTOR_BACKEND:-mock}
      - ANTHROPIC_MODEL=${ANTHROPIC_MODEL:-claude-sonnet-4-6}
```
(`ANTHROPIC_API_KEY` is already wired.)

**Flip (env)**
```
EXTRACTOR_BACKEND=claude
ANTHROPIC_API_KEY=<secret>           # already wired in compose
ANTHROPIC_MODEL=claude-sonnet-4-6
```
Rebuild with `[live]` (for `anthropic`), redeploy.

**Verify**
- A `parse`-Done document enqueues an `ingest` job (the parse→ingest hand-off); the `ingest` lane runs Claude, returns `ResultJson` `{nodes, chunks, flags}`; `IngestJobAdvanceService` writes `CurriculumChunk` rows + calls `IPedagogicalTreeWriter` for above-threshold nodes and routes **below-`IngestionConfidenceThreshold` (default 0.7)** items to `IngestionReviewItem{Pending}`.
- Sanity-check the **review queue is populated** (low-confidence items are withheld, not auto-published) via the admin list endpoint. The .NET side **fail-closes + clamps** confidence, so a divergent/over-range value routes to review — the model's confidence is advisory only.
- Re-running ingest for a document is idempotent (stable SkillKey identity) — no duplicate nodes/chunks.

**Tuning:** `IngestionConfidenceThreshold` (.NET `appsettings` → `CurriculumPipeline`) controls the auto-publish-vs-review cut; raise it to push more content to human review. `INGEST_CONFIDENCE_THRESHOLD` on the Python side is advisory (populates `flags[]`) — the .NET threshold is authoritative.

**Rollback:** `EXTRACTOR_BACKEND=mock`, redeploy.

**⚠️ Re-audit before production (deferred BL-05 item):** LLM **prompt-injection** defense-in-depth on `ingestion/claude_extractor.py` — cap injected node/chunk count, consider a randomized fence nonce, verify partial-JSON-under-truncation behavior under live load. The system/user fence + escaping already shipped; this is the live hardening pass. See §4.

---

## 3. Stage 3 — Infer edges (BL-03): LightRAG / Claude

Proposes prerequisite/related edges between skills → `KGSuggestion{Pending}` (never auto-published to `KnowledgeEdge`).

**Provision**
- An **Anthropic API key** (same as Stage 2) and the **LightRAG** dependency: pin `lightrag-hku` in the `[live]` extra at provisioning time and wire its graph store per the LightRAG docs. Model selector: `INFER_ANTHROPIC_MODEL` (falls back to `ANTHROPIC_MODEL`); inference-model label `INFERENCE_MODEL` is recorded on each `KGSuggestion.InferenceModel`.

**Flip (env — `INFERER_BACKEND` already wired in compose)**
```
INFERER_BACKEND=lightrag
ANTHROPIC_API_KEY=<secret>
INFER_ANTHROPIC_MODEL=claude-sonnet-4-6   # optional; else ANTHROPIC_MODEL
```
Rebuild with `[live]`, redeploy.

**Verify**
- Admin triggers `POST /api/curriculum/kg-suggestions/build?subjectCode&gradeId` (rate-limited 5/min in prod) → enqueues an `infer_edges` job carrying the subject's nodes-by-SkillKey in `PayloadJson`.
- The lane returns suggested edges; `EdgeInferenceAdvanceService` resolves SkillKeys→node ids (fail-soft drop+log unresolved), **re-clamps strength/confidence server-side**, and writes `KGSuggestion{Pending}` — **verify NO `KnowledgeEdge` is written by the lane** (Decision E).
- Admin approve a suggestion → `IKnowledgeEdgeWriter` publishes to `KnowledgeEdge` with the cross-language / duplicate / **acyclic** guards (cycle/cross-language → 422, suggestion stays Pending). Reject → nothing published.

**Rollback:** `INFERER_BACKEND=mock`, redeploy.

**⚠️ Re-audit before production (deferred BL-03 item):** LLM **prompt-injection** on `inference/lightrag_inferer.py` (same defense-in-depth as ingest). The fenced `<curriculum_nodes>` prompt + allowed-SkillKey whitelist + .NET re-clamp already ship; this is the live hardening pass. See §4.

---

## 4. Mandatory per-stage live-flip security re-audit checklist

These were deferred at build time because the live code paths don't run under the mock. **Each must be cleared before that stage processes real input.** (Sources: `docs/security/BL-02-audit.md`, `BL-05-audit.md`, `BL-03-audit.md`.)

- **Stage 1 (Azure DI / parse):**
  - [ ] Download/decompression **size bound** on fetched objects (OOM / zip-bomb), incl. DOCX (zip) handling.
  - [ ] **XXE** hardening on any DOCX/XML parsing on the live path.
  - [ ] Re-run `pip-audit` with the `[live]` extra installed (live SDKs now in the image).
- **Stage 2 (Claude / ingest):**
  - [ ] Prompt-injection defense-in-depth on `claude_extractor.py`: cap injected node/chunk count; randomized fence nonce; truncation-under-load behavior.
  - [ ] Confirm `.NET` confidence **fail-closed + clamp** holds end-to-end on live output (no auto-publish of low-confidence content).
- **Stage 3 (LightRAG / infer):**
  - [ ] Prompt-injection defense-in-depth on `lightrag_inferer.py` (as above).
  - [ ] Confirm the **Decision-E invariant** under live load: only admin approve writes `KnowledgeEdge`; the lane + advance service write only suggestions.
- **All stages:**
  - [ ] No secret in logs / build args / image layers; keys come from the secret store only.
  - [ ] `curriculum-intelligence` service exposes only the internal `/health` port (no extra published ports).

---

## 5. Operate & monitor

- **Health:** `curriculum-intelligence` `/health` on :8091 (compose healthcheck already wired); the .NET API `/health`.
- **Pipeline state:** query `curriculum.PipelineJobs` by `Status`/`JobType` — `Pending` backlog, `Processing` (claimed), `Done`/`Failed`/`Archived`/`PermanentlyFailed`. A row stuck at `Processing` with no progress = investigate the worker (the advance services have a no-stranding terminal write, so this should only happen on a DB outage).
- **Poller cadence:** Python `POLL_INTERVAL_SECONDS` / `INGEST_POLL_INTERVAL_SECONDS` / `INFER_POLL_INTERVAL_SECONDS`; .NET `CurriculumPipeline:{PollerIntervalSeconds, IngestPollerIntervalSeconds, InferEdgesPollerIntervalSeconds}` (+ `*MaxRetries`).
- **Review backlog:** the admin `IngestionReviewItem` + `KGSuggestion` queues — these grow as the pipeline runs and require human/admin approval to publish. Staffing the review queue is the real operational cost of going live, not the model spend alone.
- **Cost:** Claude (ingest + infer) and Azure DI are per-use. Confirm a cost ceiling + alerting before bulk-processing a full curriculum.

## 6. One-glance flip summary

| Stage | Rebuild `[live]`? | Env to set | Compose edit needed | Pre-prod gate |
|---|---|---|---|---|
| Parse | yes (`azure-ai-documentintelligence`) | `PARSER_BACKEND=azure_di`, `AZURE_DI_ENDPOINT`, `AZURE_DI_KEY` | none (wired) | Arabic OCR benchmark + §4 Stage 1 |
| Ingest | yes (`anthropic`) | `EXTRACTOR_BACKEND=claude`, `ANTHROPIC_API_KEY`, `ANTHROPIC_MODEL` | **add `EXTRACTOR_BACKEND` (+`ANTHROPIC_MODEL`) passthrough** | §4 Stage 2 |
| Infer | yes (`anthropic` + `lightrag-hku`) | `INFERER_BACKEND=lightrag`, `ANTHROPIC_API_KEY`, `INFER_ANTHROPIC_MODEL` | none (wired) | §4 Stage 3 |

Rollback any stage = set its backend env back to `mock` and redeploy. The .NET deploy never changes.
