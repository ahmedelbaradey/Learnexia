# Cross-Cutting Brief — Curriculum as System of Record

> **Version:** 2026-06-12 — encodes decisions A–E approved by the lead.
> **Status:** Authoritative — all BL-01..05 briefs and task files reference this document.
> Referenced from: `docs/briefs/BL-01.md`, `BL-02.md`, `BL-03.md`, `BL-04.md`, `BL-05.md`.

---

## 1. The Two Trees — Provenance vs Pedagogical (Decision B)

The curriculum pipeline operates on **two distinct tree structures**. These MUST NOT be merged or confused.

### 1a. Pedagogical tree (educational hierarchy) — "what students learn"

```
Grade → Subject → Unit → Lesson → Concept → Skill
```

- **Physical home:** `learning` module (P2-01, already built).
- **Logical owner:** `Curriculum` module authors/validates; `learning` module stores/serves.
- **Purpose:** defines the teaching structure — the sequence in which content is taught, the granularity at which progress is tracked (XP, badges, mastery).
- **Cross-module access:** the `curriculum` module writes to this hierarchy only through a `Shared.Contracts` seam (integration events or a published Learning command) — never via a direct DbContext reference.

### 1b. Provenance tree (source attribution) — "where content came from"

```
ContentSource (Book|Image|Worksheet|VideoTranscript)
  └── Chapter
        └── Page / Block
```

- **Physical home:** `curriculum` module (new entities introduced by this decision set).
- **Logical owner:** `curriculum` module exclusively.
- **Purpose:** traceability — records the physical source of every extracted content block so that, when a chunk appears in a student's explanation, we know it came from "Chapter 5, Page 47 of the Grade-4 Mathematics Textbook."
- **Aggregate:** `ContentSource` (types: Book, Image, Worksheet, VideoTranscript) → has many `Chapters` → each Chapter has many `Pages`/`Blocks`.

### 1c. The mapping between the two trees

A Chapter is NOT a Unit. A concept can be sourced from multiple books; one chapter can feed several lessons. The relationship is many-to-many and is modelled explicitly:

```
ProvenanceMapping (ChunkId FK, PageOrBlockRef, PedagogicalNodeId, NodeType)
```

Every `CurriculumChunk` carries a `ProvenanceRef` (a reference to its source `Page`/`Block` within a `ContentSource`) for end-to-end traceability. This is separate from the chunk's `ConceptId` link to the pedagogical tree.

---

## 2. Knowledge Graph — Logical Ownership vs Physical Location (Decision A)

### Ownership-vs-location split (the single most important fact in this system)

| Dimension | Owner | Physical location |
|---|---|---|
| **Authoring / validation** of the prerequisite graph | `Curriculum` module | n/a — logical only |
| **Persistence** of `KnowledgeNode` / `KnowledgeEdge` | `learning` module | `learning` schema, Postgres |
| **Query API** (`GetPrerequisitesOf`, `GetUnlockedBy`, `GetRelatedConcepts`, remediation traversal) | `learning` module | `KnowledgeGraphController` |
| **Consumption** (unlock rules, adaptivity, learning paths) | `learning` module | `LearningPathEngine`, P2-04, P3-08 |

**Why no relocation:** `KnowledgeNode`/`KnowledgeEdge` were built in P2-11, are actively consumed by `LearningPathEngine` and the unlock/remediation system (P2-04, P3-08, P3-10), and are tightly coupled to `Skill` via a filtered unique index. Relocating a live, consumed table into a new module is a high-risk migration and is explicitly rejected. The `curriculum` module instead authors and validates the graph and writes to it via the `Shared.Contracts` seam.

### Cross-module write path

When `curriculum` needs to persist an inferred edge:

```
Curriculum module
  → publishes CurriculumEdgeInferred integration event (Shared.Contracts)
  → Learning module handles it → validates acyclicity (SkillGraphValidator) → persists
```

Or alternatively: call the `AddKnowledgeEdgeCommand` via a published Learning command seam. Never a cross-module DbContext write.

---

## 3. Versioning Model + Stable SkillKey (Decision C)

### CurriculumVersion aggregate

`CurriculumVersion` has status: `{ Draft, Active, Archived }`.

Rules:
- Published content is **immutable**. Never mutate an `Active` version.
- Corrections create a new `Draft` version. Review proceeds, then an atomic switch: v1 becomes `Archived`, v2 becomes `Active` (single transaction, no window where no Active version exists).
- At any point: exactly one `Active` version per `(SubjectCode, Language)` tree; any number of `Draft` or `Archived` versions.
- Connect to P7-05 (publish/version/preview story in Phase 7). BL-04/BL-05 model the version entity and status; P7-05 owns the admin UI surface and the publish-act that switches versions. Do not duplicate P7-05's `Draft`/`Published` lifecycle — extend it.

### Stable SkillKey — the critical link across versions (semantic identity)

When content is re-published (v1 → v2), `KnowledgeNode` identity must survive so that student mastery, XP, streaks, and badge unlocks are NOT orphaned. This honors the product rule: *grade transition preserves history.*

`SkillKey` is a human-readable, stable semantic identifier that persists across versions:

```
Format: {SubjectCode}.{GradeLevel}.{UnitSlug}.{SkillSlug}
Example: math.grade4.fractions.add-unlike-denominators
```

Rules for `SkillKey`:
- Set at creation time; never mutated even if the display name changes.
- All `KnowledgeNode` rows carry a `SkillKey` (a stable indexed column).
- When a new version is built, nodes are matched by `SkillKey` — existing mastery/XP/streak rows remain anchored to the same node identity.
- `KnowledgeNode.SkillKey` is a unique index (within subject + grade scope).
- New skills introduced in v2 get a new `SkillKey`; retired skills are soft-deleted (not hard-deleted) so history is preserved.

### Version-aware retrieval and AI explanation/hint cache

- **RAG retrieval (P3-07):** all chunk queries filter `CurriculumVersion.Status = Active`. A chunk belonging to an `Archived` version is never returned in retrieval results. The pgvector index on `chunk_embeddings` is built only over `Active`-version chunks (or filtered at query time).
- **AI explanation/hint cache:** invalidated on every version switch (Active → new Active). Cache keys include the `VersionId` or a content hash. Stale cache entries from the prior Active version are not served after the atomic switch.
- These are implementation notes for `backend-feature` and `api-tester` agents — not optional.

---

## 4. Separate Embedding Table (Decision D)

### Why a separate table

pgvector fixes the vector dimension **per column** at creation time — a single `vector(N)` column can only store vectors of exactly N floats. This means:
- A `vector(1024)` column **cannot** hold a 1536-dim or 3072-dim embedding row. Different-dimension models must live in different physical columns or different tables.
- `ALTER COLUMN` to change the dimension is not supported by pgvector.
- Storing `Dimension int` as a metadata column alongside a fixed `vector(1024)` column is a **documentation fiction** — the column dimension is still 1024 regardless of what the int field says. A 1536-dim vector inserted into a `vector(1024)` column raises an error.

To enable parallel model coexistence during migration (dual-index: BGE-M3 index + new-model index both live simultaneously while traffic shifts) and model versioning without disruptive chunk-table migrations, the physical design is:

**One physical embedding table per (Model, Dimension).** The logical entity (`ChunkEmbedding`) is shared across all such tables; only the typed vector column and its pgvector index differ.

### Physical table design — one table per embedding dimension

```sql
-- chunk_embeddings_bge_m3 (curriculum schema) — current, BGE-M3, 1024-dim
Id           bigserial PK
ChunkId      int FK → CurriculumChunks.Id (ON DELETE CASCADE)
Provider     varchar(64)    -- e.g. 'huggingface'
Model        varchar(64)    -- e.g. 'bge-m3'
ModelVersion varchar(32)    -- e.g. '1.0'
IsActive     bool           -- true = currently served for retrieval; exactly one active model at a time
Vector       vector(1024)   -- FIXED at 1024; BGE-M3 + Cohere embed-multilingual-v3 output 1024-dim
CreatedAt    timestamptz

-- ANN index (deferred to BL-05/P3-07 when chunk volume is known)
-- CREATE INDEX ON chunk_embeddings_bge_m3 USING hnsw (Vector vector_cosine_ops)

-- chunk_embeddings_<future_model> — added later WITHOUT migrating the above table
-- Example: chunk_embeddings_openai_3072 with vector(3072)
-- Drop the old table only after the new one is fully backfilled + IsActive flipped.
```

### IsActive — one model served at a time

- **`IsActive = true`** marks the embedding table (model) that retrieval (P3-07) queries. Exactly one model is active at a time per dimension context.
- RAG retrieval (P3-07) queries only the currently active embedding table (or filters `IsActive = true` if the active-model metadata is stored in a registry row rather than per embedding row). Implement as a configurable pointer to the active table name, not a scan of all tables.
- During a model migration: build the new table, backfill all chunk embeddings into it, then **atomically** flip `IsActive` (old table `IsActive = false`, new table `IsActive = true`). Only then retire the old table.

### Dual-index migration path

```
Step 1: Create chunk_embeddings_<new_model> table with vector(<new_dim>).
Step 2: Backfill — embed all existing CurriculumChunk rows into the new table.
        (Old table still IsActive; retrieval is unaffected during backfill.)
Step 3: Build HNSW/IVFFlat index on new table.
Step 4: Atomic flip — new table IsActive = true, old table IsActive = false.
Step 5: Drop old table (or archive) once traffic is stable on the new model.
```

This path requires no changes to `CurriculumChunk` at any point, and no downtime for retrieval.

**Note:** The `Dimension int` metadata field is retained on the logical model for documentation purposes (humans can see what dimension a given table's vector column has) but it plays no role in pgvector operations — the physical column type is the authority.

### Impact on BL-04

BL-04-BE-3/BE-4: remove the `EmbeddingVectorRef vector(1024)` column from `CurriculumChunk`. Add a `ChunkEmbeddingBgeM3` entity mapping to `chunk_embeddings_bge_m3` (the initial per-dimension physical table) with the schema above. BL-04 delivers this table (task BL-04-BE-7, revised). Future models add new tables without touching this one.

### Impact on BL-05 / P3-07

BL-05 writes `CurriculumChunk` rows with no vector column (the column no longer exists on the chunk). Embeddings are written to `chunk_embeddings_bge_m3` by BL-05-PY-4b (if in scope) or by P3-07. P3-07 reads the active embedding table (identified by `IsActive`) via the Shared.Contracts seam. HNSW/IVFFlat ANN indexes are on the active table's `Vector` column, not on `CurriculumChunk`.

---

## 4b. Cross-Process Transport — DB-Outbox + Python Poller (Pipeline Seam)

### The problem with in-process MediatR

The .NET backend uses **in-process MediatR** for integration events between modules within the same process. MediatR event handlers run in the same AppDomain as the publisher. A separate Python FastAPI process **cannot be reached by an in-process MediatR event** — there is no shared memory bus between .NET and Python. Any design that assumes the Python worker "subscribes" to MediatR integration events is incorrect and will produce a non-functional pipeline.

### Approved MVP transport: DB-Outbox + Python poller

No external broker (RabbitMQ, Kafka, Azure Service Bus) is in the current stack. The simplest cross-process seam that avoids operating a new piece of infrastructure is:

```
.NET side (enqueue):
  On upload / on stage-complete → write a row to curriculum.PipelineJobs
  (Status = Pending, JobType, Payload JSON, CreatedAt)

Python side (poll):
  Python worker polls curriculum.PipelineJobs WHERE Status = 'Pending'
  → claims a row (UPDATE ... SET Status = 'Processing' WHERE Id = ? AND Status = 'Pending')
  → processes (OCR / ingest / embed)
  → writes results back + sets Status = 'Done' or 'Failed' + result payload

.NET side (advance):
  A background hosted service (or a periodic check in the handler) polls
  curriculum.PipelineJobs WHERE Status IN ('Done','Failed')
  → reads result → advances CurriculumDocument.ParseStatus / IngestionStatus
  → deletes or archives the completed job row
```

### `curriculum.PipelineJobs` schema

```sql
Id           bigserial PK
JobType      varchar(64)    -- 'parse' | 'ingest' | 'embed'
Status       varchar(16)    -- 'Pending' | 'Processing' | 'Done' | 'Failed'
DocumentId   int            -- FK → CurriculumDocuments.Id
PayloadJson  text           -- input for the Python worker (e.g. { object_key, content_type })
ResultJson   text NULL      -- output written by the Python worker (e.g. artifact key, chapters)
ErrorMessage text NULL
CreatedAt    timestamptz
ClaimedAt    timestamptz NULL
CompletedAt  timestamptz NULL
RetryCount   int DEFAULT 0
```

Indexes: on `(Status, JobType)` for polling; on `DocumentId`.

### Postgres LISTEN/NOTIFY (optional optimisation)

To avoid busy-polling, the Python worker can listen on a Postgres channel:

```sql
-- .NET issues on insert:
NOTIFY pipeline_jobs, '<job_id>';

-- Python receives the notification instantly; falls back to polling
-- on reconnect. This is an optimisation, not a requirement for MVP.
```

### Upgrade path

When a real broker is warranted (high volume, cross-datacenter), the job-row contract stays the same — the .NET "enqueue" side emits to the broker instead of writing a DB row, and the Python side consumes from the broker queue instead of polling. The contract (job type + payload JSON shape + result JSON shape) is the seam; swapping the transport does not change the business logic on either side.

### Impact on BL-01 (`.NET enqueue`)

- BL-01-BE-5 is **revised**: on upload success, instead of publishing a MediatR integration event, write a `PipelineJobs` row with `JobType='parse'`, `Status='Pending'`, `DocumentId`, `PayloadJson = { object_key, content_type }`. New task **BL-01-BE-9** covers the `PipelineJobs` entity + migration.

### Impact on BL-02 (`.NET advance` + `Python poll`)

- BL-02-BE-3 is **revised**: the `ParseCurriculumDocumentCommandHandler` is no longer triggered by a MediatR event — it is triggered by the .NET job-advance poller detecting a `Done` `parse` job row. New task **BL-02-BE-7** covers the .NET poller (background `IHostedService` that polls `PipelineJobs WHERE Status='Done' AND JobType='parse'`).
- BL-02-PY-1 is **revised**: the Python service polls `PipelineJobs WHERE Status='Pending' AND JobType='parse'` rather than receiving an event. New Python task **BL-02-PY-9** covers the polling loop + atomic claim.

### Impact on BL-05 (`.NET advance` + `Python poll`)

- BL-05-BE-4 is **revised**: the `IngestCurriculumDocumentCommandHandler` is triggered by the .NET job-advance poller detecting a `Done` `parse` job (written by BL-02) which creates a new `ingest` job row. New task **BL-05-BE-13** covers the .NET poller for ingest-complete jobs.
- BL-05-PY-1 is **revised**: the Python ingestion service polls `PipelineJobs WHERE Status='Pending' AND JobType='ingest'`. New Python task **BL-05-PY-6** covers the polling loop + atomic claim for ingestion.

---

## 5. KG as Product Moat — Suggestions vs Published Truth (Decision E)

### The principle

The published prerequisite graph (`Fractions → requires → Division`) is the product moat. It is **human/Claude-approved** — every edge that a student's learning path depends on has been reviewed and validated before being marked `published`.

LightRAG's auto-extracted graph is a **noisy suggestion source**. It feeds a **human review queue** (the `KGSuggestion` queue) — never the live graph directly. Auto-extracted edges are never auto-published.

### The two-phase KG build (BL-03)

```
Phase 1 — Auto-suggest (Python / LightRAG):
  → infer candidate edges from BL-05 structured curriculum
  → write to KGSuggestion queue (status: Pending)
  → NOT written to KnowledgeEdge table

Phase 2 — Human/admin approval (BL-03 review endpoints):
  → admin reviews each suggested edge in the queue
  → approves: the edge is written to KnowledgeEdge (via the Shared.Contracts seam)
            + SkillGraphValidator acyclic check before persist
  → rejects: suggestion discarded

Published KnowledgeEdge rows: ONLY approved edges.
```

### KGSuggestion entity (new, BL-03)

```
KGSuggestion:
  Id, SourceNodeId FK, TargetNodeId FK,
  RelationshipType (Prerequisite/Related), Strength (decimal 0–1),
  SourceCurriculumDocumentId FK (provenance),
  InferenceModel (string — 'lightrag-v1', etc.),
  Status (Pending / Approved / Rejected),
  ReviewedAt (nullable), ReviewedByUserId (int, nullable),
  CreatedAt
```

This entity lives in the `curriculum` module (or `learning` — wherever BL-04 lands) and is separate from `KnowledgeEdge`. The approval action writes from `KGSuggestion` → `KnowledgeEdge` (via the seam).

### Implication for BL-03-PY tasks

PY-2 (LightRAG inference) writes to the `KGSuggestion` queue, never to `KnowledgeEdge`. PY-3 (post-processing/dedup) operates on suggestions. The `.NET` side (BE-3) reads approved suggestions and persists them via the seam. The review endpoints (BE-NEW) expose the suggestion queue to admins.

---

## 6. Summary — New Entities Introduced by Decisions A–E + Pipeline Seam

| Entity | Module | Decision / Seam | Status |
|---|---|---|---|
| `ContentSource` | `curriculum` | B — provenance tree root | New |
| `Chapter` | `curriculum` | B — provenance chapter | New |
| `ProvenanceMapping` | `curriculum` | B — chunk↔pedagogical mapping | New |
| `CurriculumVersion` | `curriculum` (links to P7-05) | C — immutable versioning | New |
| `SkillKey` column on `KnowledgeNode` | `learning` | C — stable semantic identity | Add column |
| `chunk_embeddings_bge_m3` table | `curriculum` | D — per-dimension embedding table (1024-dim, BGE-M3) | New (replaces inline vector) |
| `KGSuggestion` | `curriculum` | E — suggestion queue | New |
| `PipelineJobs` table | `curriculum` | Pipeline seam — DB-outbox + Python poller | New (BL-01-BE-9) |

**Removed:** `EmbeddingVectorRef vector(1024)` inline on `CurriculumChunk` (replaced by `chunk_embeddings_bge_m3`).
**Renamed:** `chunk_embeddings` → `chunk_embeddings_bge_m3` to make the physical dimension explicit. Future tables follow the pattern `chunk_embeddings_<model_slug>` (e.g. `chunk_embeddings_openai_3072`).

---

## 7. Open Questions Remaining for the Lead

1. **`chunk_embeddings_bge_m3` dimension + naming convention:** the physical table name `chunk_embeddings_bge_m3` encodes the model. Confirm this naming convention for future tables (e.g. `chunk_embeddings_openai_3072`). The parallel-table migration path is decided — a new model gets a new table, not an `ALTER COLUMN`.
2. **`CurriculumVersion` granularity:** P7-05 versions at the `(SubjectCode, Language)` tree level. BL-04/BL-05 add versioning at the ingestion pipeline level. Confirm these are the same `CurriculumVersion` entity or complementary layers (recommended: one entity, P7-05's publish action transitions the version status for the entire tree).
3. **`SkillKey` retroactive population for P2-11 seeds:** the hand-authored seeds from P2-11 were created before `SkillKey` existed. A migration must backfill `SkillKey` values for existing `KnowledgeNode` rows. Confirm the slug format and whether this migration is part of BL-04 or a separate story.
4. **`KGSuggestion` module home:** if BL-04 option A (separate `curriculum` module) is chosen, `KGSuggestion` lives there. If option B (fold into `learning`), it lives in `learning`. The approval action that writes to `KnowledgeEdge` is then an in-module write (simpler). Confirm placement as part of BL-04's module decision.
5. **`PipelineJobs` poller interval + retry policy:** the Python worker polls the DB. Confirm: poll interval (recommended: 5s for MVP, LISTEN/NOTIFY optimisation optional), max retry count before marking a job as `PermanentlyFailed`, and whether a dead-letter admin endpoint is in scope for BL-01/BL-02 or deferred. Also confirm: is the .NET job-advance poller a `BackgroundService` or a Hangfire job (Hangfire is not in the current stack — `BackgroundService` is the zero-dependency path).
6. **Postgres LISTEN/NOTIFY opt-in:** the Python worker can use `LISTEN pipeline_jobs` to avoid busy-polling. Confirm whether this is in scope for MVP or deferred (polling is acceptable at low volume).
