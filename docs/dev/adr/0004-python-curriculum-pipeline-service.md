# ADR 0004 — Python curriculum-pipeline service (parsing & ingestion worker)

- **Status:** Accepted (2026-06-23)
- **Deciders:** Lead (with the curriculum-pipeline build)
- **Context source:** BL-02 scoping pass (`docs/briefs/BL-02.md`); the Curriculum Intelligence backlog (BL-01..05) and `docs/briefs/curriculum-system-of-record.md` §4b.
- **Related:** [[ADR 0001 — unit of work]] (no UoW; explicit transactions), BL-01 (upload front door + `PipelineJobs` DB-outbox), BL-02 (multimodal parsing), BL-05 (curriculum ingestion), P3-07 (BGE-M3 TEI — the prior devops-gated inference precedent).

## Context

The Curriculum Intelligence pipeline turns uploaded curriculum files into a structured, retrievable learning system. The heavy lifting — OCR (incl. scanned **Arabic** PDFs via Azure Document Intelligence), multimodal extraction (text/images/tables/equations/layout), LLM-driven structuring, semantic chunking, and embedding prep — is **Python work**, not .NET. The backend is a .NET 10 modular monolith with no Python anywhere in the repo, no Python service home, and no Python implementer agent.

`curriculum-system-of-record.md` §4b already decided the **integration boundary**: a **DB-outbox + Python poller**, NOT MediatR or a synchronous .NET→Python HTTP call. BL-01 shipped that outbox: `curriculum.PipelineJobs` (`JobType`/`Status` as **strings** — a deliberate cross-process contract) plus `CurriculumDocument` rows and the `curriculum` MinIO bucket holding the uploaded files. BL-02 is the first story that needs the Python side to exist.

Azure Document Intelligence is **devops-gated** — no resource/key is provisioned yet — exactly the situation BGE-M3 was in for P3-07, where the .NET side shipped against a mocked/seeded path and devops flipped it live later.

This ADR records how the Python service is structured, run, owned, and integrated, so the decision lives outside chat (CLAUDE.md: a new service needs explicit sign-off; agreed decisions are recorded).

## Decision

1. **Repo home — `python/curriculum_intelligence/`.** A top-level `python/` tree, separate from `backend/`, with subpackages:
   `app/` (config, db, storage, logging), `parsers/` (Azure DI client, MinerU/PaddleOCR fallback, normalization), `workers/` (the outbox poller/claim loop), `benchmarks/` (Arabic OCR accuracy gate), `tests/` (pytest). The task files' `services/parsing/...` path drift is reconciled to this layout.

2. **Integration is the DB-outbox only — no service-to-service calls.** The Python worker and the .NET `ParseJobAdvanceService` never call each other; they meet solely at `curriculum.PipelineJobs` rows. The worker **claims** a `JobType='parse', Status='Pending'` row atomically (`UPDATE ... WHERE Status='Pending' ... FOR UPDATE SKIP LOCKED` semantics), reads the source file from the `curriculum` bucket via `ObjectKey`, writes a per-document structured JSON artifact back to MinIO, records the artifact key in the job/result, and transitions the row to `Done`/`Failed`. `Status`/`JobType` strings are the contract — do not convert to int enums on either side.

3. **Retry policy is owned by .NET** (reconciles the BL-02 Q7 conflict). The Python worker reports terminal `Failed` with diagnostics; the .NET advance service owns attempt-counting / re-enqueue / dead-letter decisions. The Python side does not implement its own retry backoff against the queue.

4. **Runtime — its own container, polling loop.** A `curriculum-intelligence` service is added to `docker/docker-compose.yaml` on `learnexia-network`: a long-running poll loop (+ a FastAPI health endpoint). The **`curriculum` bucket is added to the existing `minio-setup`** init (today it only creates `avatars`). Secrets (Azure DI key/endpoint, Postgres connection, MinIO creds) come from **env / the secret store**, never committed — same posture as the rest of the stack and the BGE-M3 token.

5. **Azure DI is devops-gated; build mocked now.** The OCR call sits behind an interface with a deterministic **mock/fixture implementation** used in dev + CI. The full .NET orchestration slice and the Python worker scaffold/poller are built and tested against the mock now; live Azure DI is a later devops activation (provision resource + key + Arabic benchmark samples → flip config). The **Arabic OCR accuracy benchmark gates the live Azure DI adoption (PY-2)**, not the mocked build.

6. **Ownership — `general-purpose` agent, no new subagent.** The existing `-PY` tasks in `tasks/Backend/Backlog-Phase-2-Plus/BL-02-BE.md` already record the Python scope, so no new story is needed. The Python worker is built by the **general-purpose** agent (the specialized roster is .NET/TS-only). We do **not** introduce a dedicated python-worker subagent type for now; revisit if BL-05 proves Python volume warrants it.

7. **Testing — pytest + a Testcontainers contract test.** Python unit tests mock Azure DI/Claude; a contract test exercises the atomic claim against a Testcontainers Postgres (+ MinIO) to prove the outbox handshake. The .NET side adds an integration test for the `ParseJobAdvanceService` claim/advance path. The Arabic benchmark runs offline (not in the default CI gate).

## Consequences

- **First Python in the repo.** CI must learn to build/test `python/` (a Python job — added when the first worker code lands, per BL-02 Q4). HANDOFF records the new compose service, the minio-setup bucket addition, and the env/secret keys (Azure DI endpoint/key) as load-bearing config.
- **BL-02 splits into two largely-parallel batches** once the .NET `CurriculumDocument` parse-fields migration lands: the .NET orchestration slice and the Python worker, synchronized only through `PipelineJobs`.
- **Schema (BL-02 Q9):** reuse the existing `DocumentStatus`/`StatusReason` on `CurriculumDocument`; add only `ParsedArtifactObjectKey` + `ParsedAt` — no separate parallel parse-status lifecycle.
- **Devops follow-up (gated, not blocking the build):** provision an Azure Document Intelligence resource + key, supply Arabic benchmark sample files, then flip the parser config from mock to live and clear the benchmark gate. Mirrors the BGE-M3 / AI-activation runbook precedent.
- **BL-05 reuses this service.** Ingestion (LLM extraction, semantic chunking, BGE-M3 embedding prep) lands in the same `python/curriculum_intelligence/` tree on the same outbox pattern (`JobType='ingest'`), so this ADR is the home for that stage too.

## Alternatives considered

- **Embed Python under `backend/`** — rejected: mixes runtimes/build tooling under a tree that is otherwise a single .NET solution.
- **Synchronous .NET → Python HTTP (or MediatR) trigger** — rejected by `curriculum-system-of-record.md` §4b: the .NET process cannot reach the Python process reliably, and a long OCR job must not block a request. The durable DB-outbox decouples them and survives restarts.
- **Wait for Azure DI provisioning before any BL-02 work** — rejected: needlessly stalls the entire .NET slice + worker scaffold, which are fully buildable and testable against a mock (the P3-07/BGE-M3 precedent).
- **Author a dedicated python-worker subagent now** — deferred: not enough Python surface yet to justify it; the general-purpose agent + this ADR + the `-PY` tasks are sufficient. Revisit at BL-05.
