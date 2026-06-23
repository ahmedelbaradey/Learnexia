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
├── workers/      pipeline (parse→artifact→result) + poller (claim loop, PY-9)
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
