# Cross-Cutting Brief — AI Helper MVP (not AI Teacher)

> **Status: APPROVED.** This brief encodes the lead-approved "AI Helper, not AI Teacher" model. All P3-* stories (P3-03 through P3-07, P3-12) must read this alongside their story brief and the cost-routing brief (`docs/briefs/ai-cost-routing.md`). It supersedes any prior framing that described the tutor as a general conversational AI.

---

## 1. The Core Constraint: Helper, not Teacher

The MVP tutor is a **closed-loop, intent-constrained helper** over **seeded verified skills**. It is not a general chatbot, not a homework solver, and not a "ask anything" AI.

### 1.1 Allowed intents (the only four)

| Intent (Arabic) | Intent (English) | Maps to | Scope |
|---|---|---|---|
| "اشرح السؤال" | Explain this concept/question | P3-04 `ExplainConceptCommand` | Scoped to the ACTIVE skill/question only |
| "اديني تلميح" | Give me a hint | P3-05 `GetHintCommand` | Scoped to the current wrong answer |
| "ليه إجابتي غلط" | Why is my answer wrong | P3-05 `GetHintCommand` — distinct intent `WhyWrong` | Dynamic, uses the student's actual wrong answer as input; always runtime (no cache key) |
| "اديني مثال مشابه" | Give me a similar example | P3-06 `SimilarExampleCommand` (runtime-grounded variant) | Grounded in current skill context only |

### 1.2 Blocked behaviors

The following are explicitly out of scope and must be enforced as hard policy — not just documented:

- "Explain anything in the world" (questions outside the active skill context)
- "Solve the whole homework" (give the final answer)
- "Ask any general question" (open-ended chatbot mode)
- Any request not matching one of the four allowed intents above

---

## 2. Scope Guard — Cross-Cutting Enforcement

The scope constraint is enforced at **two layers**:

**Layer 1 — P3-02 Safety/Scope Guard:** An intent classifier (cheap model — Haiku) maps every incoming request to one of the four allowed intents or rejects it. Any request that doesn't map is rejected before touching the AI gateway. This is the primary enforcement point. The classifier runs before any LLM call.

**Layer 2 — P3-03 Prompt Builder:** The system prompt and all four subject templates are scoped to "the active skill/question context." No template asks the model to explain anything outside the provided curriculum context. The anti-injection frame reinforces this.

Both layers must be present. P3-02 is the gate; P3-03 is the wall.

---

## 3. `ILearningContextProvider` — The Grounding Seam

All four helper intents fetch grounding through a single interface:

```
interface ILearningContextProvider {
    Task<LearningContext> GetContextAsync(
        int studentId,
        int skillId,       // the active skill
        int questionId?,   // the active question (when in quiz mode)
        string wrongAnswer?, // the student's specific wrong answer (for "why wrong" intent)
        CancellationToken ct
    );
}
```

`LearningContext` carries: approved skill content chunks, the question text + type, the student's wrong answer (when applicable), grade/subject/language metadata.

### 3.1 Two implementations — ship in order

**`SeededCorpusContextProvider` (ships now, MVP):**
- Pulls from the P2-11 `KnowledgeNode`/`KnowledgeEdge` graph + the hand-seeded verified skill corpus (the seeded chunk corpus from P3-07's `CurriculumChunk` table).
- Returns only approved/QA-passed content. Never returns unapproved or draft content to the AI helper.
- This implementation ships **now**, in parallel with the curriculum pipeline.

**`RagContextProvider` (ships later, via P3-07):**
- Wraps the full P3-07 pgvector RAG retrieval over the `chunk_embeddings` corpus.
- Owned by the Curriculum module / BL-04 pipeline. Do NOT redefine `chunk_embeddings` schema here — reference it via the `IChunkRetrievalContract` seam only.
- Swaps in via config (DI registration) — no code change required in the Helper when this implementation is ready.

The Helper depends only on `ILearningContextProvider`. The seam is the contract; the implementation is a config decision. This is the key architectural choice that makes the parallel build possible.

### 3.2 Module placement

`ILearningContextProvider` is declared in `Shared.Contracts/AiTutor/`. Both implementations live in their respective module's Infrastructure layer (`Ai.Infrastructure` for the seeded variant, `Curriculum.Infrastructure` for the RAG variant). The DI container selects the active implementation via config.

---

## 4. Refuse-and-Redirect Rule (Hard Acceptance Criterion)

If `ILearningContextProvider.GetContextAsync` returns no approved content (empty chunks and no relevant skill grounding), the Helper **does not answer**. It returns a redirect response:

> Arabic: "خلينا نكمل تحدي [skill name] الحالي، أو اختار درس العلوم."
> English: "Let's keep working on your current [skill name] challenge, or pick a Science lesson."

The redirect text is templated (EN+AR), localizable, and includes the active skill name. It is not a raw error — it is a friendly, on-brand redirect.

**This is a hard acceptance criterion for P3-04, P3-05, P3-06, and P3-12 (the UI state).** It must be tested explicitly: given a student asking about something outside their current skill context, the system redirects rather than answering.

This rule aligns with P3-07's "decline rather than hallucinate" principle and P3-06's `Declined_NoContext` result. All three are manifestations of the same invariant: **no grounding = no answer**.

---

## 5. Closed-Loop Completion Metric (North Star)

The success metric for the AI Helper is NOT "how many questions the AI answered." It is:

**Wrong answer → AI help → retry → success**

Specifically: after a student receives AI help (hint, explanation, why-wrong), does the child complete the question / retry and succeed, and does their frustration pattern drop?

### 5.1 Instrumentation events (required)

Every backend handler for the four intents must emit these events (fire-and-forget, via the integration event bus):

| Event | When emitted | Key fields |
|---|---|---|
| `HelpRequested { StudentId, Intent, SkillId, QuestionId?, AttemptId? }` | When a valid intent is received and context is available | StudentId, intent type, skill/question ids |
| `HelpDelivered { StudentId, Intent, SkillId, QuestionId?, ModelUsed, ContextSource }` | When the helper response is successfully delivered | ContextSource: `SeededCorpus` or `Rag`; ModelUsed: model id |
| `HelpDeclined { StudentId, Intent, SkillId, Reason }` | When the helper refuses (no context) | Reason: `NoContext` or `OutOfScope` |
| `PostHelpRetry { StudentId, QuestionId, AttemptId, PreviousHelpIntent }` | When a student retries a question after receiving help | Links to the preceding HelpDelivered |
| `PostHelpSuccess { StudentId, QuestionId, AttemptId, HelpIntent }` | When a student answers correctly after receiving help | The resolved loop |

These events are consumed by:
- **P5-03 product analytics** — funnel analysis of the help loop
- **P6-02 AI quality evaluation** — correlation between help delivery and outcomes
- The data from these events also informs the decision of when seeded→RAG upgrade is worth the complexity cost

### 5.2 Instrumentation placement

The `HelpRequested`, `HelpDelivered`, and `HelpDeclined` events are emitted by the AI helper handlers (P3-04/P3-05/P3-06 handlers). `PostHelpRetry` and `PostHelpSuccess` are emitted by the quiz submission handler (P2-08 / `SubmitAnswerCommand`) when it detects a retry on a question that had prior help. The quiz handler already knows the attempt state — it adds the help-context check.

---

## 6. Build Order — Parallel, Not Gated

The seeded AI Helper is an MVP deliverable that builds **in parallel** with the Gamification-FE and Parent work. It does NOT gate on BL-* or the full Curriculum ingestion pipeline.

```
Timeline view (parallel tracks):

Track A — AI Helper (ships at MVP):
  P3-01 (Gateway) + P3-02 (Safety/Scope Guard) + P3-03 (Prompt Builder)
      ↓
  [SeededCorpusContextProvider — ILearningContextProvider impl #1]
      ↓
  P3-04 (Explain) + P3-05 (Hint + WhyWrong) + P3-06 (SimilarExample)
      ↓
  P3-12 (UI — 4 helper actions + redirect state)

Track B — Curriculum Pipeline (runs in parallel, swaps in later):
  BL-01 (upload) → BL-02 (OCR/parse/chunk) → BL-03 (KG) → BL-04 (chunk schema) → BL-05 (ingest)
      ↓ (when ready)
  P3-07 (RAG retrieval — RagContextProvider impl #2)
      ↓
  Config swap: ILearningContextProvider → RagContextProvider (no code change in Helper)
```

The Helper runs on the existing `Ai` module + cost-routing gateway (Haiku-default/escalate, caching) per `docs/briefs/ai-cost-routing.md`. The cost-routing decisions are unchanged.

---

## 7. Intent → Story Mapping

| Intent | Primary story | Secondary involvement | Runtime vs Cached |
|---|---|---|---|
| "اشرح السؤال" (Explain) | P3-04 `ExplainConceptCommand` | P3-03 (prompt), P3-02 (scope guard) | Cached for known concept+grade+language; runtime for custom questions |
| "اديني تلميح" (Hint) | P3-05 `GetHintCommand` (intent=`Hint`) | P3-03 (prompt), P3-02 (scope guard) | Cached per `(QuestionId, HintLevel, Language)`; runtime on cache miss |
| "ليه إجابتي غلط" (Why Wrong) | P3-05 `GetHintCommand` (intent=`WhyWrong`) | P3-03 (prompt), P3-02 (scope guard) | Always runtime — student's specific wrong answer is the unique key |
| "اديني مثال مشابه" (Similar Example) | P3-06 `SimilarExampleCommand` | P3-03 (prompt), P3-02 (scope guard) | Runtime-grounded; may be cached per skill if examples are stable |

P3-12 UI surfaces exactly these four helper actions as distinct affordances, plus the redirect state (no-context response).

---

## 7b. Runtime-configurable thresholds (Global Settings)

The cache/review thresholds that govern the AI Helper's cost-control and content-quality gates — auto-approval confidence threshold, WhyWrong variant cap, and practice pool size — are read at runtime via **`IGlobalSettingsProvider`** (contract in `Shared.Contracts`). The decided values (0.85 / 50 / 5) are **bootstrap defaults** used when a key is absent from the DB. The full DB-backed, Redis-cached, audited, admin-editable implementation lands in **P10-12**. Until then the bootstrap defaults apply transparently; no calling code in P3-04/05/06 needs to change when P10-12 ships.

See `docs/briefs/ai-cost-routing.md §8b` for the full key inventory and implementation rules.

## 8. Links

This brief governs:

- [P3-03 — Prompt Builder](P3-03.md) — scope guard integration; `ILearningContextProvider` seam in PromptContext
- [P3-04 — Explain Concept](P3-04.md) — intent #1; refuse-and-redirect enforcement; instrumentation
- [P3-05 — Hints & Re-explanation](P3-05.md) — intents #2 and #3 (Hint + WhyWrong); instrumentation
- [P3-06 — Similar Example / Question Generation](P3-06.md) — intent #4; runtime-grounded variant
- [P3-07 — RAG Retrieval](P3-07.md) — provides `RagContextProvider`; the seam + `SeededCorpusContextProvider` ship first
- [P3-12 — AI Tutor UI](P3-12.md) — surfaces the four intents + redirect state; does not expose free-text chatbot

Also read:
- [ai-cost-routing.md](ai-cost-routing.md) — model routing, cache, quota (unchanged by this brief)
- [CLAUDE.md](../../CLAUDE.md) — product rules, module isolation, design-patterns-ask-first
