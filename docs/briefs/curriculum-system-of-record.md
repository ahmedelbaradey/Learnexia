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

pgvector fixes the vector dimension per column at creation time. If the embedding model is later changed (e.g. BGE-M3 1024-dim → OpenAI text-embedding-3-large 3072-dim), a new column or table is required. To enable:
- Parallel model coexistence during migration (dual-index: BGE-M3 index + new-model index both live simultaneously while traffic shifts)
- Model versioning without disruptive column migrations on the chunks table

### Schema

```sql
-- chunk_embeddings table (curriculum schema)
Id           bigserial PK
ChunkId      int FK → CurriculumChunks.Id (ON DELETE CASCADE)
Model        varchar(64)   -- e.g. 'bge-m3', 'openai-text-embedding-3-large'
Version      varchar(32)   -- e.g. '1.0', '2024-11'
Dimension    int           -- e.g. 1024, 3072
Vector       vector(1024)  -- default dimension; separate rows/table per new dimension
CreatedAt    timestamptz
```

**Note on the dimension constraint:** pgvector still fixes dimension per column. The migration story for a model with a different dimension (e.g. 3072) is: add a new table `chunk_embeddings_3072` with `vector(3072)` and populate it in parallel; once traffic is switched, drop the old table. The `chunk_embeddings` table (1024-dim) is the default for BGE-M3 and Cohere embed-multilingual-v3 (both output 1024-dim — compatible). A row in `chunk_embeddings` identifies its model + version so dual-model periods are unambiguous.

### Impact on BL-04

BL-04-BE-3/BE-4: remove the `EmbeddingVectorRef vector(1024)` column from `CurriculumChunk`. Add `chunk_embeddings` as a new entity with the schema above. BL-04 delivers the `chunk_embeddings` table (new task BL-04-BE-7).

### Impact on BL-05 / P3-07

BL-05 writes `CurriculumChunk` rows with no vector column (the column no longer exists on the chunk). Embeddings are written to `chunk_embeddings` by BL-05-PY-4b (if in scope) or by P3-07. P3-07 reads `chunk_embeddings` via the Shared.Contracts seam. HNSW/IVFFlat ANN indexes are on `chunk_embeddings.Vector`, not on `CurriculumChunk`.

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

## 6. Summary — New Entities Introduced by Decisions A–E

| Entity | Module | Decision | Status |
|---|---|---|---|
| `ContentSource` | `curriculum` | B — provenance tree root | New |
| `Chapter` | `curriculum` | B — provenance chapter | New |
| `ProvenanceMapping` | `curriculum` | B — chunk↔pedagogical mapping | New |
| `CurriculumVersion` | `curriculum` (links to P7-05) | C — immutable versioning | New |
| `SkillKey` column on `KnowledgeNode` | `learning` | C — stable semantic identity | Add column |
| `chunk_embeddings` table | `curriculum` | D — separate embedding table | New (replaces inline vector) |
| `KGSuggestion` | `curriculum` | E — suggestion queue | New |

**Removed:** `EmbeddingVectorRef vector(1024)` inline on `CurriculumChunk` (replaced by `chunk_embeddings`).

---

## 7. Open Questions Remaining for the Lead

1. **`chunk_embeddings` dimension constraint for future models:** when a non-1024-dim model is needed, confirm the parallel-table migration approach is acceptable (not a `ALTER COLUMN` — pgvector doesn't allow dimension changes). The `chunk_embeddings` schema above can accommodate multiple rows per chunk with different `Model`/`Version`/`Dimension` values, but the `Vector vector(1024)` column is still dimension-fixed. The recommended migration path is a separate table per dimension, not a shared table with a variable-dimension column.
2. **`CurriculumVersion` granularity:** P7-05 versions at the `(SubjectCode, Language)` tree level. BL-04/BL-05 add versioning at the ingestion pipeline level. Confirm these are the same `CurriculumVersion` entity or complementary layers (recommended: one entity, P7-05's publish action transitions the version status for the entire tree).
3. **`SkillKey` retroactive population for P2-11 seeds:** the hand-authored seeds from P2-11 were created before `SkillKey` existed. A migration must backfill `SkillKey` values for existing `KnowledgeNode` rows. Confirm the slug format and whether this migration is part of BL-04 or a separate story.
4. **`KGSuggestion` module home:** if BL-04 option A (separate `curriculum` module) is chosen, `KGSuggestion` lives there. If option B (fold into `learning`), it lives in `learning`. The approval action that writes to `KnowledgeEdge` is then an in-module write (simpler). Confirm placement as part of BL-04's module decision.
