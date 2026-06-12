# Decisions needed — AI phase + Curriculum Intelligence

> Fill in the **Decision:** line under each item. These are the open gates surfaced by the
> AI-phase + Curriculum Intelligence task breakdown (briefs in `docs/briefs/`, plans in
> `docs/plans/`, tasks in `tasks/Backend/Phase-4-AI-Tutor/` + `tasks/Backend/Backlog-Phase-2-Plus/`).
> Nothing in that breakdown is built yet (all tasks 🔲) — these are blockers to **dispatching**
> implementers, not to the planning. Governing briefs:
> [ai-helper-mvp.md](briefs/ai-helper-mvp.md) ·
> [ai-cost-routing.md](briefs/ai-cost-routing.md) ·
> [curriculum-system-of-record.md](briefs/curriculum-system-of-record.md).

Owner: lead · Status: **RESOLVED** (answers inline; credit economy + monetization carved to **Phase 9 — Payment, Billing & Credits**) · Created: 2026-06-12 · Resolved: 2026-06-12

> ⚠️ **Credit economy v2 — revised 2026-06-13** (supersedes the figures recorded in Section 1 below): **Free 100/month + 10/day · Premium 5000/month + 250/day · pack 1000 credits / $5.** Per-action costs unchanged (hint=1, explain-mistake=3, deep-explanation=5, practice-generation=5). Charge policy: delivered = charge, **cache-hit = charge**, refuse = free, error = free. Primary cost lever is the **app-level Redis response cache** (hit = $0 AI tokens), with provider prompt-cache as an optional secondary. Phase-9 stories/tasks carry the authoritative numbers.

---

## 1. Per-plan AI quota numbers (pricing decision)

**Why it's needed:** the `IAiUsageBudget` quota enforcement (P3-01-BE-14) is config-driven, but the
actual caps are a product/pricing call. A 199 EGP (~$4) plan only works if runtime AI is bounded and
cache-served (see [ai-cost-routing.md](briefs/ai-cost-routing.md)).

**Decide the caps per tier:**
- **Free:** AI Helper requests/day = ``  (recommend a small daily cap, e.g. 5–10 hints/day)  
- **Premium (199 EGP):** AI Helper requests/day = ``  (recommend ~30/day) · monthly ceiling = ``
- On quota exhaustion → serve the cached canned explanation (no error). Confirm: `yes / no` YES 

**Decision:** AI Credit Economy Decision:

Free:
- 300 credits/month
- 20 credits/day cap

Premium:
- 3000 credits/month
- 150 credits/day soft cap

Extra:
- 500 credits purchasable pack


Costs:

Hint:
1 credit

Explain Mistake:
3 credits

Deep Explanation:
5 credits

Practice Generation:
5 credits

 

Quota exhausted:
YES

Serve cached explanation when possible.
Never block learning.  

بس في الـ UI للأطفال ما تسميهاش Credits.

للطفل خليها حاجة زي:

⚡ طاقة المساعد

---

## 2. Batch/cache job host + cache schema placement (infra)

**Why it's needed:** the offline pre-generation jobs (P3-04-BE-10 concept explanations,
P3-05-BE-12 hints, P3-06-BE-8 question gen) and the BL ingestion jobs need a host; and the
`ConceptExplanationCache` / `HintCache` / `generation_batches` tables need a schema home.

- **Job host:** Hangfire (already in the stack per P1-07) ☐ · dedicated `IHostedService` worker ☐ · separate worker process ☐ — *recommend Hangfire*
- **Cache table schema:** `ai` schema ☐ (recommended) · `curriculum` ☐ · `learning` ☐

**Decision:** Hangfire , `ai` schema
ai.ConceptExplanationCache

ai.HintCache

ai.GenerationBatch

ai.AiUsageLedger

ai.AiCreditTransaction
---

## 3. .NET ↔ Python boundary for the curriculum pipeline (architecture)

**Why it's needed:** BL-02 (Azure DI / RAG-Anything parsing), BL-05 (RAG-Anything ingestion + BGE-M3
embeddings), and BL-03 (LightRAG KG) have `-PY` task tables. Where does that Python service live, and
is it in scope for *this* repo's build?

- **Location:** same-repo `python/curriculum_intelligence/` (FastAPI) ☐ · separate service repo ☐
- **Scope now:** build the Python service in this cycle ☐ · only the .NET orchestration slice now, Python tracked separately ☐
- **Seam:** integration event / message bus ☐ · background poller on upload status ☐ · synchronous HTTP ☐

**Decision:** same-repo python/ folder
Location:
YES
same repo:
python/curriculum_intelligence


Scope:
.NET orchestration now
Python pipeline separate implementation stream


Seam:
Integration Events / Message Bus

Book Uploaded

↓

Event

↓

Python Worker

↓

Process

↓

Event Completed

↓

.NET updates status

---

## 4. Runtime + provisioning prerequisites (confirm before dispatch)

**Why it's needed:** several first-of-their-kind patterns and external dependencies must be green-lit.

- **Streaming wire format:** SSE (recommended; buffer→safety-filter→stream so safety is never bypassed) — confirm `yes / no`. Pin the exact event shape (`data:`, `event: redirect`, `[DONE]`, `event: error`) in HANDOFF before P3-12 FE.
- **Provider API keys:** Claude (default), provisioned in dev/CI secret store (never committed) — confirm `yes / no`
- **Azure Document Intelligence:** resource + key provisioned for Arabic OCR — confirm `yes / no` · region/account = `____`
- **License verification (before adopting):** RAG-Anything (MIT?) ☐ · LightRAG (MIT?) ☐ · BGE-M3 (MIT-like?) ☐
- **Embedding model + dimension lock:** BGE-M3 / `vector(1024)` (recommended) — confirm before the BL-04/P3-07 migration: `____`
- **Arabic OCR benchmark gate (BL-02-PY-7):** assemble the 20–30 page test pack and review `benchmark_report.md` before committing Azure DI as primary — confirm owner: `____`

**Decision:** Decision — Runtime + Provisioning Prerequisites

1) Streaming wire format (AI Tutor)
===================================

Decision:
YES — Use SSE (Server Sent Events)
Claude streaming

↓

Small buffer window

↓

Safety check chunk

↓

Send SSE chunk

Reason:
Provide ChatGPT-like streaming experience while keeping child safety.

Architecture:

Claude / AI Provider
        |
        |
Buffer response chunks
        |
        |
Safety Filter
        |
        |
SSE Stream to Client


Rules:
- Never stream raw provider output directly to children.
- Safety layer must run before client delivery.
- Pin SSE contract before P3-12 FE.


Event contract:

Message:
event: message
data:
{
  "content": "..."
}


Redirect:
event: redirect
data:
{
  "type": "lesson",
  "targetId": "..."
}


Error:
event: error
data:
{
  "code": "...",
  "message": "..."
}


Completion:
event: done
data: [DONE]


Status:
APPROVED


==================================================


2) AI Provider + API Keys
==========================

Decision:
YES — Claude as default provider.

Rules:
- API keys must be stored only in:
  - developer secret store
  - CI/CD secrets
  - production secret manager

Never commit keys into repository.

Architecture:

           IAiProvider
               |
               |
        ClaudeProvider
               |
         IAiModelRouter
               |
      _________|_____________
     |         |             |

     Haiku     Sonnet      Opus

Tutor request

↓

AiRouter

↓

Simple:
Haiku

Explain:
Sonnet

Offline review:
Opus

Future providers possible:
- OpenAI
- Gemini
- Local models


Status:
APPROVED


==================================================


3) Azure Document Intelligence (Arabic OCR)
============================================

Decision:
YES — provision for benchmark first.

Purpose:
Extract Arabic educational content from:

- textbooks
- worksheets
- scanned pages
- diagrams
- tables


Architecture:

PDF/Image

      |

Azure Document Intelligence

      |

Structured blocks

      |

Claude Vision validation

      |

Curriculum draft

      |

Human approval

      |

Publish


Important:
Azure DI is NOT automatically trusted.

It becomes the primary OCR only after passing the Arabic benchmark.


Region/account:
TBD during provisioning.


Status:
APPROVED FOR BENCHMARK


==================================================


4) Open Source License Verification
====================================

Decision:
YES — required before adoption.

Verified:

------------------------------------

RAG-Anything

License:
MIT

Commercial usage:
YES

Approved for:
- RAG orchestration
- multimodal document processing
- retrieval workflows

Status:
APPROVED


------------------------------------

LightRAG

License:
MIT

Commercial usage:
YES

Approved for:
- Knowledge Graph assisted retrieval
- entity relationships
- graph-based context

Status:
APPROVED


------------------------------------

BGE-M3

Provider:
BAAI

License:
MIT

Commercial usage:
YES

Approved for:
- multilingual embeddings
- Arabic semantic search
- RAG retrieval

Embedding size:
1024 dimensions

Status:
APPROVED


------------------------------------


Important dependency rule:

Approval applies to these libraries/models only.

Do NOT automatically approve bundled:

- OCR engines
- parsers
- extra models
- third-party tools


Every runtime dependency requires its own license check.


==================================================


5) Embedding Model Decision (BL-04 / P3-07)
============================================


Decision:

Initial model:
BGE-M3

Dimension:
1024


DO NOT store vectors directly in ContentChunk.


Wrong:

ContentChunk
-------------
Id
Text
Vector(1024)


Rejected because:
pgvector dimensions are fixed.

Future migration:
BGE-M3 (1024)

to

another model (1536+)

would require painful migration.


------------------------------------


Approved design:


ContentChunk
-------------

Id

Text

Language

SkillKey

ContentVersion

Status



ChunkEmbedding
--------------

Id

ChunkId

Provider

Model

ModelVersion

Dimension

Vector

CreatedAt

IsActive



Example:

Same Chunk:

"الكسر يتكون من بسط ومقام"


Embedding 1:

Model:
BGE-M3

Dimension:
1024


Future:

Embedding 2:

Model:
New model

Dimension:
1536


Both can coexist.


Benefits:

- model migration without downtime
- dual indexing
- A/B retrieval testing
- provider independence


Logical model:
ChunkEmbedding

Physical storage:
dimension-specific vector indexes allowed

Status:
APPROVED


==================================================


6) Embedding Provider Abstraction
==================================


Decision:
Required.


Do NOT couple application directly to BGE.


Architecture:


IEmbeddingProvider

        |

        |

BgeM3EmbeddingProvider


Future:

OpenAIEmbeddingProvider

CohereEmbeddingProvider

ArabicSpecializedProvider


Reason:

Embedding technology changes quickly.

Learnexia curriculum data must survive model changes.


Status:
APPROVED


==================================================


7) Arabic OCR Benchmark Gate (BL-02-PY-7)
==========================================


Decision:
YES — mandatory before production ingestion.


Create:

Arabic Curriculum Benchmark Pack


Size:

20–30 pages


Must include:


Arabic:

- tashkeel / harakat
- early grade text


Math:

- equations
- fractions
- geometry


Science:

- diagrams
- labels


Mixed content:

- Arabic + English
- RTL + LTR
- tables


Low quality:

- scanned worksheets
- phone photos



Test:

OCR accuracy

Layout accuracy

Diacritics preservation

Diagram understanding

Skill mapping accuracy



Deliverable:

benchmark_report.md


Rule:

No automatic publishing of AI-extracted curriculum.


Pipeline:


Extract

   |

Review

   |

Approve

   |

Publish


Owner:
AI / Curriculum pipeline owner


Status:
REQUIRED


==================================================


FINAL APPROVAL SUMMARY
=======================


SSE Streaming:
APPROVED


Claude Provider:
APPROVED


Azure Document Intelligence:
APPROVED AFTER BENCHMARK


RAG-Anything:
APPROVED (MIT)


LightRAG:
APPROVED (MIT)


BGE-M3:
APPROVED (MIT)


Embedding storage:
Separate ChunkEmbedding table


Embedding dimension:
BGE-M3 1024 initial


Provider abstraction:
Required


Arabic benchmark:
Mandatory before production


Decision:
CLEARED FOR AI + CURRICULUM ARCHITECTURE IMPLEMENTATION

---
5.
Add:
AI evaluation dataset gate.

## Already-settled (for reference — do not re-open)

- New **`Ai`** module + new **`Curriculum`** module approved; **`learning`** extended for adaptivity/mastery/profile.
- `KnowledgeNode`/`KnowledgeEdge` stay **physically in `learning`** (P2-11), Curriculum is the *logical* owner via `Shared.Contracts`.
- Default provider **Claude**, task-routed: Haiku (classify) / **Sonnet (tutoring floor)** / Opus (offline only).
- **AI Helper (not Teacher):** 4 intents, refuse-and-redirect off-curriculum, ships on the **seeded corpus in parallel** with the BL pipeline (not gated behind it).
- Curriculum **system of record**: provenance layer (`ContentSource`/`Chapter`) separate from the pedagogical tree; immutable **versioning** + stable `SkillKey`; separate versioned `chunk_embeddings` table; auto-KG → `KGSuggestion` review queue.
