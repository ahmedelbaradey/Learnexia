# curriculum-intelligence

The **first Python service** in the Learnexia repo: an offline parsing worker that
turns uploaded curriculum files (Arabic PDFs/DOCX/images) into normalized,
machine-readable JSON artifacts for the Curriculum Intelligence pipeline.

It is built and owned per **[ADR-0004](../../docs/dev/adr/0004-python-curriculum-pipeline-service.md)**.
Story: **BL-02** (`docs/briefs/BL-02.md`, `tasks/Backend/Backlog-Phase-2-Plus/BL-02-BE.md`).

## How it integrates with .NET — DB-outbox ONLY

There is **no service-to-service HTTP** between .NET and Python. The two meet only
at the `curriculum."PipelineJobs"` table and the `curriculum` MinIO bucket:

```
.NET upload  ──INSERT PipelineJobs(JobType='parse', Status='Pending')──►  outbox
                                                                            │
curriculum-intelligence (this service):                                    │ polls
  1. ATOMICALLY CLAIM a Pending parse job (SELECT … FOR UPDATE SKIP LOCKED  │
     → UPDATE … SET Status='Processing', ClaimedAt=now() … RETURNING *)     │
  2. download object_key from the `curriculum` bucket (MinIO)               │
  3. parse → normalized artifact JSON (text/images/tables/equations/layout) │
  4. PUT artifact JSON to the `curriculum` bucket                           │
  5. UPDATE … SET Status='Done', ResultJson={artifact_key, chapters[], …}   │
     (or Status='Failed' + ErrorMessage on terminal failure)               │
                                                                            ▼
.NET ParseJobAdvanceService  ──polls Done/Failed (FOR UPDATE SKIP LOCKED)──► advances doc
```

**`JobType`/`Status` are STRINGS** — a deliberate cross-process contract; never
convert to int enums on either side. **Retry policy is owned by .NET** — this
worker only reports terminal `Failed` + diagnostics; it never re-enqueues.

## Layout

```
curriculum_intelligence/
├── app/          config, db (atomic claim), storage (MinIO), logging, health (FastAPI)
├── parsers/      ParserBackend protocol, mock (default), Azure DI (live), MinerU fallback,
│                 artifact model + JSON serializer
├── ingestion/    Extractor protocol, mock (default) + Claude (live), Arabic chunker,
│                 confidence/flags, SkillKey derivation, ResultJson model + pipeline (BL-05)
├── workers/      parse pipeline+poller (PY-9) + ingest poller (BL-05)
├── benchmarks/   offline Arabic OCR accuracy gate (samples/, results/)
├── tests/        pytest: normalization, serialization, diacritics, + Testcontainers contract test
├── main.py       entrypoint — FastAPI /health + poll loop in one process
├── pyproject.toml / Dockerfile / .env.example / LICENSE (MIT)
```

## Parser backend: mock (default) vs Azure DI (live)

The OCR/parse call sits behind the `ParserBackend` protocol. `PARSER_BACKEND`
selects the implementation:

- **`mock` (default — dev + CI):** deterministic, fixture-driven, no network. Emits
  diacritized Arabic so the smoke/diacritics tests pass without binary samples.
- **`azure_di` (live — devops-gated):** real `prebuilt-layout` `ar` OCR. Selected
  only when `PARSER_BACKEND=azure_di` **and** `AZURE_DI_ENDPOINT`+`AZURE_DI_KEY`
  are set; otherwise it logs a warning and degrades to the mock.

The **Arabic OCR benchmark** (`benchmarks/benchmark_runner.py`) gates the LIVE
adoption — run it offline once the corpus + Azure DI resource exist. It is NOT
part of the default test run.

## Ingest lane (BL-05) — the SECOND poller, same process

The worker now runs **two** poll loops in one process (separate daemon threads,
one psycopg connection each). The ingest lane mirrors the parse lane exactly,
parameterized on `JobType='ingest'`:

```
.NET (on parse-Done) ──INSERT PipelineJobs(JobType='ingest', Status='Pending',
                         PayloadJson={artifact_key, document_id})──►  outbox
                                                                       │ IngestPoller polls
curriculum-intelligence ingest lane:                                   │
  1. CLAIM a Pending ingest job (same FOR UPDATE SKIP LOCKED claim)    │
  2. download the BL-02 artifact JSON from the `curriculum` bucket     │
  3. extractor (mock default / Claude live) → raw hierarchy            │
  4. derive stable SkillKeys → grade-scoped Arabic-boundary chunking   │
  5. confidence scoring + advisory low-confidence flags                │
  6. UPDATE … Status='Done', ResultJson={nodes, chunks, flags, …}      │
                                                                       ▼
.NET IngestJobAdvanceService  ──polls Done/Failed──► writes ALL DB rows
```

**The ingest lane writes NO DB rows and creates NO entities** (lead decision Q6):
it returns a structured `ResultJson` the .NET `IngestJobAdvanceService` persists
(chunks, learning-tree nodes, review-queue). It touches only `PipelineJobs` +
the `curriculum` bucket.

### ResultJson contract (the .NET side deserializes this — keep field names exact)

```json
{
  "schema_version": "1.0",
  "nodes":  [ { "node_type": "Unit|Lesson|Concept|Skill",
                "skill_key": "math.grade5.fractions.add-fractions",
                "parent_skill_key": "math.grade5.fractions.fractions",
                "title": "...", "grade": 5, "subject_code": "math",
                "language": "ar", "difficulty": 3, "confidence": 0.86 } ],
  "chunks": [ { "content": "...", "content_type": "text", "source_page": 3,
                "chapter_number": 1, "node_skill_key": "math.grade5.fractions.add-fractions",
                "confidence": 0.9 } ],
  "flags":  [ { "kind": "low_confidence_node|low_confidence_chunk|unmapped_chunk",
                "ref": "<skill_key or chunk index>", "confidence": 0.55,
                "reason": "..." } ],
  "diagnostics": { "extractor": "mock", "node_count": 2, "chunk_count": 3, … }
}
```

- `confidence` is a 0..1 float on **every** node + chunk. The worker only EMITS
  it; the **.NET side applies the 0.7 threshold** and routes sub-threshold items
  to the review queue. `flags[]` is advisory (a hint), never a gate.
- `skill_key` format is `{subjectCode}.grade{N}.{unitSlug}.{leafSlug}` (the BL-04
  format), **derived deterministically** so re-ingest yields byte-identical keys —
  the anchor that makes the .NET natural-key upsert idempotent.
- The 4-subject set (`math`/`science`/`arabic`/`english`) is enforced; any other
  `subject_code` is a terminal extraction failure (no Social Studies).
- Diacritics (U+064B–U+065F) are preserved through extraction + chunking +
  serialization (`ensure_ascii=False`).

### Extractor backend: mock (default) vs Claude (live, devops-gated)

The hierarchy-extraction call sits behind the `Extractor` protocol (mirrors
`ParserBackend`). `EXTRACTOR_BACKEND` selects the implementation:

- **`mock` (default — dev + CI):** deterministic, no network. Echoes an
  `__ingest_fixture__` block from the artifact, or builds a synthetic Arabic
  Unit→Lesson→Concept→Skill chain.
- **`claude` (live — devops-gated):** real Anthropic Messages API. Selected only
  when `EXTRACTOR_BACKEND=claude` **and** `ANTHROPIC_API_KEY` is set; otherwise it
  logs a warning and degrades to the mock (so CI can never call out to Claude).
  Model from `ANTHROPIC_MODEL` (default `claude-sonnet-4-6`). The `anthropic` SDK
  is in the `[live]` extra and imported lazily — the mock default needs none of it.

**Embeddings are deferred (Q4):** the ingest lane does NOT generate embeddings;
P3-07 owns vectors. No `embeddings` key in `ResultJson`.

## Running

```bash
# install (mock default — no heavy OCR/AI deps)
pip install ".[test]"

# tests (Azure DI/Claude mocked; contract test needs Docker)
pytest                      # all
pytest -m "not contract"    # skip the Testcontainers Postgres test

# run the worker locally (reads .env)
python -m curriculum_intelligence.main
```

In docker-compose the `curriculum-intelligence` service runs the poll loop +
`/health` on `learnexia-network`, alongside `postgres` and `minio`.

## Secrets

All credentials (Postgres DSN, MinIO creds, Azure DI endpoint/key, Anthropic key)
come from **env only** — see `.env.example`. Nothing is hardcoded or committed.
