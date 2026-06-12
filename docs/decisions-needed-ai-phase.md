# Decisions needed — AI phase + Curriculum Intelligence

> Fill in the **Decision:** line under each item. These are the open gates surfaced by the
> AI-phase + Curriculum Intelligence task breakdown (briefs in `docs/briefs/`, plans in
> `docs/plans/`, tasks in `tasks/Backend/Phase-4-AI-Tutor/` + `tasks/Backend/Backlog-Phase-2-Plus/`).
> Nothing in that breakdown is built yet (all tasks 🔲) — these are blockers to **dispatching**
> implementers, not to the planning. Governing briefs:
> [ai-helper-mvp.md](briefs/ai-helper-mvp.md) ·
> [ai-cost-routing.md](briefs/ai-cost-routing.md) ·
> [curriculum-system-of-record.md](briefs/curriculum-system-of-record.md).

Owner: lead · Status: **OPEN** · Created: 2026-06-12

---

## 1. Per-plan AI quota numbers (pricing decision)

**Why it's needed:** the `IAiUsageBudget` quota enforcement (P3-01-BE-14) is config-driven, but the
actual caps are a product/pricing call. A 199 EGP (~$4) plan only works if runtime AI is bounded and
cache-served (see [ai-cost-routing.md](briefs/ai-cost-routing.md)).

**Decide the caps per tier:**
- **Free:** AI Helper requests/day = `____`  (recommend a small daily cap, e.g. 5–10 hints/day)
- **Premium (199 EGP):** AI Helper requests/day = `____`  (recommend ~30/day) · monthly ceiling = `____`
- On quota exhaustion → serve the cached canned explanation (no error). Confirm: `yes / no`

**Decision:** _______________________________________________

---

## 2. Batch/cache job host + cache schema placement (infra)

**Why it's needed:** the offline pre-generation jobs (P3-04-BE-10 concept explanations,
P3-05-BE-12 hints, P3-06-BE-8 question gen) and the BL ingestion jobs need a host; and the
`ConceptExplanationCache` / `HintCache` / `generation_batches` tables need a schema home.

- **Job host:** Hangfire (already in the stack per P1-07) ☐ · dedicated `IHostedService` worker ☐ · separate worker process ☐ — *recommend Hangfire*
- **Cache table schema:** `ai` schema ☐ (recommended) · `curriculum` ☐ · `learning` ☐

**Decision:** _______________________________________________

---

## 3. .NET ↔ Python boundary for the curriculum pipeline (architecture)

**Why it's needed:** BL-02 (Azure DI / RAG-Anything parsing), BL-05 (RAG-Anything ingestion + BGE-M3
embeddings), and BL-03 (LightRAG KG) have `-PY` task tables. Where does that Python service live, and
is it in scope for *this* repo's build?

- **Location:** same-repo `python/curriculum_intelligence/` (FastAPI) ☐ · separate service repo ☐
- **Scope now:** build the Python service in this cycle ☐ · only the .NET orchestration slice now, Python tracked separately ☐
- **Seam:** integration event / message bus ☐ · background poller on upload status ☐ · synchronous HTTP ☐

**Decision:** _______________________________________________

---

## 4. Runtime + provisioning prerequisites (confirm before dispatch)

**Why it's needed:** several first-of-their-kind patterns and external dependencies must be green-lit.

- **Streaming wire format:** SSE (recommended; buffer→safety-filter→stream so safety is never bypassed) — confirm `yes / no`. Pin the exact event shape (`data:`, `event: redirect`, `[DONE]`, `event: error`) in HANDOFF before P3-12 FE.
- **Provider API keys:** Claude (default), provisioned in dev/CI secret store (never committed) — confirm `yes / no`
- **Azure Document Intelligence:** resource + key provisioned for Arabic OCR — confirm `yes / no` · region/account = `____`
- **License verification (before adopting):** RAG-Anything (MIT?) ☐ · LightRAG (MIT?) ☐ · BGE-M3 (MIT-like?) ☐
- **Embedding model + dimension lock:** BGE-M3 / `vector(1024)` (recommended) — confirm before the BL-04/P3-07 migration: `____`
- **Arabic OCR benchmark gate (BL-02-PY-7):** assemble the 20–30 page test pack and review `benchmark_report.md` before committing Azure DI as primary — confirm owner: `____`

**Decision:** _______________________________________________

---

## Already-settled (for reference — do not re-open)

- New **`Ai`** module + new **`Curriculum`** module approved; **`learning`** extended for adaptivity/mastery/profile.
- `KnowledgeNode`/`KnowledgeEdge` stay **physically in `learning`** (P2-11), Curriculum is the *logical* owner via `Shared.Contracts`.
- Default provider **Claude**, task-routed: Haiku (classify) / **Sonnet (tutoring floor)** / Opus (offline only).
- **AI Helper (not Teacher):** 4 intents, refuse-and-redirect off-curriculum, ships on the **seeded corpus in parallel** with the BL pipeline (not gated behind it).
- Curriculum **system of record**: provenance layer (`ContentSource`/`Chapter`) separate from the pedagogical tree; immutable **versioning** + stable `SkillKey`; separate versioned `chunk_embeddings` table; auto-KG → `KGSuggestion` review queue.
