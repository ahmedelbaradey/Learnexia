# AI offline pre-generation jobs (cache-prewarm + bulk question-gen) — BACKLOG

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor *(BACKLOG — deferred follow-up of P3-15)*
- **Epic:** AI Tutor cost & scale
- **Issue type:** Story (Technical Enabler)
- **Story Points:** ~8 (estimate)
- **Labels:** `backend`, `ai`, `cost`, `backlog`
- **Requirements:** FR-AI-1 (extends), NFR-2 (cost), per `docs/briefs/ai-cost-routing.md`

## Status: BACKLOG (recorded 2026-06-30, per rule #9)
Split out of **P3-15** during its build. P3-15 shipped the reusable **batch gateway** (`IAiBatchGateway` + `ClaudeBatchProvider` + deterministic fake — merged separately). The two **offline jobs** that consume it were deferred here because a correct implementation needs prerequisites that aren't in place yet (see Blockers). **Do not build until the lead schedules it** (run analyzer → planner first). Not MVP-blocking — a cost optimization.

## Description
Build the offline/batch AI generation jobs on top of the existing `IAiBatchGateway`:
1. **Cache pre-warm job** — batch-generate common AI responses (Explain / why-wrong / practice) and write them to `AiResponseCache` as `PendingReview` (R5 gate; admin approves before students are served), so runtime requests get cache HITs.
2. **Bulk grounded-question generation job** — batch-generate questions (the P3-06 path) and persist them as `Draft` → **P7-09 moderation** (never auto-published), grounded only on retrieved context.

Both fail-soft, idempotent, off by default (config + no schedule), key-less-testable via the `DeterministicFakeBatchGateway`.

## Blockers (why this was deferred from P3-15)
- **Cross-module enumeration seam (no FK):** both jobs must enumerate the right identities from the **Learning** module (questions/concepts per subject+grade) via a NEW `Shared.Contracts` read seam — no cross-module FK (module isolation). The bulk-question-gen job additionally needs a **Moderation Draft writer** seam.
- **EXACT runtime cache-key alignment (the P3-15 FAIL root cause):** the prewarm MUST build the SAME cache key the runtime produces — keyed on the **real `questionId`/`conceptId`** the runtime uses (`ExplainConceptCommandHandler` uses `conceptId: questionId ?? 0`, `difficulty: 0`, + the JWT-grade age-band + grounding + model + lang + version). The discarded P3-15 attempt fabricated a `conceptId` from `string.GetHashCode()` (process-randomized → non-deterministic across restarts) with `difficulty: 1` → prewarmed rows could never be hit and idempotency broke on restart. The job effectively has to replicate the runtime request build (incl. grounding retrieval) per identity, then submit those as a batch.

## Acceptance Criteria (to refine at scheduling)
- Prewarm writes cache rows whose key **provably equals** a real runtime Explain request's key (a test asserts `prewarmKey == AiCacheKeyBuilder.ForExplain(<runtime inputs>)`), all `PendingReview`, idempotent across restarts (deterministic identity, not a randomized hash).
- Bulk question-gen persists `Draft` → moderation, grounded-only, fail-soft, idempotent.
- Both off by default; key-less-testable via the fake; runtime path unchanged.

## Notes
- Depends on (built): **P3-15** batch gateway (`IAiBatchGateway`/`ClaudeBatchProvider`/fake), `AiResponseCache` + its serve-gate, P3-06 generation path, P7-09 moderation.
- Activation is devops (keys + `Ai:Batch:Enabled` + a schedule). Related: [[P3-15]], `docs/briefs/ai-cost-routing.md`.
