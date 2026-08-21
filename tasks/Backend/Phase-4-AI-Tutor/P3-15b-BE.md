# P3-15b (backend) — AI offline pre-generation jobs (cache-prewarm + bulk question-gen) — BACKLOG

> Story: [../../../user-stories/Phase-4-AI-Tutor/P3-15b-offline-pregeneration-jobs.md](../../../user-stories/Phase-4-AI-Tutor/P3-15b-offline-pregeneration-jobs.md)
> Brief: [../../../docs/briefs/ai-cost-routing.md](../../../docs/briefs/ai-cost-routing.md)
> Phase 4 · Module: **Ai** (jobs) + a new **Learning** `Shared.Contracts` read seam (+ Moderation Draft writer for BE-2).

## Status: BACKLOG — deferred. DO NOT build until scheduled.
Split out of **P3-15** (which shipped the `IAiBatchGateway` batch gateway). These two offline jobs were deferred because they need prerequisites not yet in place — see Blockers. **Decompose into full tasks when the lead schedules it** (analyzer → planner first). Not MVP-blocking (cost optimization).

## Candidate scope (to refine at scheduling)
| ID (provisional) | Task | Origin |
|---|---|---|
| P3-15b-BE-1 | **Cache pre-warm job** (Hangfire, off by default) — enumerate the **real runtime identities** (per subject+grade+language) via a new Learning `Shared.Contracts` seam; for each, build the SAME `AiRequest` the runtime builds (incl. grounding retrieval) and the SAME cache key (`AiCacheKeyBuilder.ForExplain` with the real `conceptId`/`questionId`, `difficulty:0`, JWT-grade age-band, grounding, model, lang, version); submit via `IAiBatchGateway`; write results `PendingReview` (R5 gate). Fail-soft, **idempotent across restarts** (deterministic identity — NOT `string.GetHashCode()`). | was P3-15-BE-4 |
| P3-15b-BE-2 | **Bulk grounded-question generation job** — batch-generate via the P3-06 path; persist `Draft` → **P7-09 moderation** (never auto-publish), grounded-only. Needs a Moderation Draft-writer seam. Fail-soft, idempotent. | was P3-15-BE-5 |
| P3-15b-BE-3 | **Learning enumeration seam** in `Shared.Contracts` — list the question/concept identities (per subject+grade) the jobs iterate; no cross-module FK. | enabler |
| P3-15b-BE-4 | Cost/quota guardrails + scheduling config (off by default). | brief |
| P3-15b-BE-5 | **Tests** — fake-gateway driven (no keys). MUST include a test that the prewarm cache key **equals** a real runtime Explain key (`AiCacheKeyBuilder.ForExplain(<runtime inputs>)`), and an idempotency test that survives a **simulated process restart** (the gap the original in-process test missed). + security-auditor (AI prompts + generated child content). | gate |

## Hard prerequisites / lessons (from the P3-15 review FAIL — do NOT repeat)
- **Cache-key MUST match the runtime exactly.** The discarded attempt fabricated `conceptId = Math.Abs(skillKey.GetHashCode()) % 100_000` with `difficulty:1`. `string.GetHashCode()` is **process-randomized** in .NET → non-deterministic across restarts (breaks idempotency) AND it never equals the runtime's `conceptId: questionId ?? 0` / `difficulty:0` → prewarmed rows are unhittable dead weight. Enumerate the real identities; replicate the runtime request/key precisely.
- **R5 gate:** writes are `PendingReview` only; the runtime serve-gate serves only `Approved`. Never auto-approve.
- Module isolation: jobs live in Ai; cross-module reads via `Shared.Contracts` only (no FK).

## Notes
- Depends on (built + merged): **P3-15** (`IAiBatchGateway`/`ClaudeBatchProvider`/`DeterministicFakeBatchGateway`), `AiResponseCache` + serve-gate, P3-06 generation, P7-09 moderation.
- Activation is devops (provider keys + `Ai:Batch:Enabled` + a schedule).
