# AI Batch API & offline pre-generation (cost optimization) — BACKLOG

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor *(BACKLOG — deferred, not scheduled)*
- **Epic:** AI Tutor cost & scale
- **Issue type:** Story (Technical Enabler / enrichment of P3-01)
- **Story Points:** ~8 (estimate) — batch gateway seam + provider + offline jobs + tests + security review.
- **Labels:** `backend`, `ai`, `cost`, `backlog`
- **Requirements:** FR-AI-1 (extends), NFR-2 (cost), per `docs/briefs/ai-cost-routing.md`

## Status: BACKLOG (recorded 2026-06-23, per rule #9)
Captures the **deferred** parts of the P3-01 AI Gateway story so they are tracked rather than dangling as `🔲` sub-tasks. **Do not build until the lead schedules it** (run analyzer → planner first). **Not MVP-blocking** — the runtime path is complete: streaming SSE (P3-04/05/06), on-demand generation, and the `AiResponseCache` (auto-approve serve, OQ-7 resolved) already deliver the AI tutor end-to-end. This story is a **cost optimization** (the Anthropic Batch API is ~50% cheaper for bulk/offline work) that pays off at scale, not at launch.

## Description
As the platform, I want an **offline / batch** AI generation path so that high-volume, non-interactive AI work (cache pre-warming, bulk grounded-question generation) runs through the cheaper Anthropic Batch API instead of per-request runtime calls — reducing AI spend at scale without changing the student-facing runtime path.

Two pieces, both currently deferred:
1. **Batch gateway seam (was P3-01-BE-13):** `IAiBatchGateway` (`SubmitBatchAsync` / `PollBatchAsync`) in `Shared.Contracts/Ai/` + a `ClaudeBatchProvider` in `Ai.Infrastructure` over the Anthropic Batch API, registered in `AddAiModule`. Mirror the existing `IAiGateway`/provider shape (one interface, one impl, config-selected — rule #8, no Strategy/Factory).
2. **Offline pre-generation jobs (consumers):** scheduled/triggered jobs that use the batch gateway to (a) pre-warm `AiResponseCache` for common explain / why-wrong / practice prompts per grade+subject+language, and (b) bulk-generate grounded questions (P3-06) as `Draft → moderation` (P7-09). Fail-soft, idempotent, deterministic-fake-testable (no live keys in CI).

## Acceptance Criteria (to refine at scheduling)
- `IAiBatchGateway` seam + `ClaudeBatchProvider` exist behind config; a deterministic fake exercises submit/poll/result-mapping in tests (no keys needed).
- An offline job can submit a batch of prompts, poll to completion, and persist results (warmed cache rows and/or `Draft` questions) — fail-soft, idempotent on re-run (no duplicate cache rows / questions).
- Bulk-generated questions land as `Draft` into the P7-09 moderation queue (never auto-published), grounded only on retrieved curriculum context (no hallucinated skills).
- Runtime student-facing behavior is unchanged; this path is purely additive and off by default until keys + a schedule exist.
- Cost/quota guardrails per `docs/briefs/ai-cost-routing.md`.

## Notes
- **Superseded sibling (NOT part of this story):** the interim per-plan daily request cap (`IAiUsageBudget`, was P3-01-BE-14) is **superseded by the Phase 10 energy economy** (charge-on-delivery, built + QC'd) — do NOT build the interim guardrail; P10 owns cost control now.
- **Activation is devops:** even when built, the batch path is dormant until provider keys + a job schedule exist (see `docs/dev/AI-ACTIVATION-RUNBOOK.md`).
- Deps (all built): P3-01 gateway, P3-04/05/06, `AiResponseCache`, P7-09 moderation. Related: [[P3-13a]] (the other Phase-4 backlog story), `docs/briefs/ai-cost-routing.md`.
