# Capture AI tutor usage for streamed (SSE) responses

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (follow-up to the P7-11 tutor-cost slice)
- **Epic:** Admin — Analytics & AI Oversight
- **Issue type:** Story
- **Story Points:** 2 — gateway instrumentation, reuses the existing usage recorder.
- **Labels:** `ai`, `backend`, `observability`
- **Requirements:** FR-ADM-10, FR-AI-4. NFR-1.

## Description
As an admin, I want AI tutor usage/cost to also include **streamed** responses, so that the usage/cost dashboard (P7-11) and the platform AI-request-volume KPI (P7-10) reflect *all* tutor calls — not just non-streaming completions.

## Background
The P7-11 tutor-cost slice persists `ai.AiUsageLogs` from `AiGateway.CompleteAsync` (non-streaming). The streaming path `AiGateway.StreamAsync` (used by Hint / Explain / SimilarExample SSE) does **not** call `EnrichWithCost` / the usage recorder — a documented v1 capture gap. This story closes it.

## Acceptance Criteria
- A successful `StreamAsync` call captures one `AiUsageLog` row via the existing fire-and-forget `IAiUsageRecorder` (token counts taken from the provider's end-of-stream usage when available; cost enriched the same way as `CompleteAsync`).
- Capture is **fail-soft** and **does not block or fail** the SSE response or add measurable latency (NFR-1) — same guarantees as the non-streaming path.
- The P7-11 `GET /api/Admin/AiSafety/usage` totals and the P7-10 `aiRequestVolume` KPI now include streamed calls; the "non-streaming completions only" caveat is removed from the docs/contract.
- No PII: the captured row stays PII-light (no prompt/response text); `StudentId` remains nullable/unpopulated unless a provider-neutral path supplies it.

## Notes
- If a provider does not emit usage in the stream, record what is available (e.g. zeroed/estimated tokens) and note it — do not fabricate token counts.
- Pairs with P5-03 in the same backend wave; independent of the Analytics module.
