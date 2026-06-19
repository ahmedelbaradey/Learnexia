# P7-11b (backend) — Streaming (SSE) AI usage capture

> Story: [../../../user-stories/Phase-7-Admin-Console/P7-11b-streaming-ai-usage-capture.md](../../../user-stories/Phase-7-Admin-Console/P7-11b-streaming-ai-usage-capture.md)
> Phase 7 · Module: **Ai** · completes the P7-11 tutor-cost slice (closes the `StreamAsync` capture gap)

## Design rules (binding)
- **Reuse the existing fire-and-forget recorder** `IAiUsageRecorder` (Singleton, own DI scope, fail-soft) — do NOT add a new write pattern (the fire-and-forget pattern is already lead-approved).
- Capture MUST be fail-soft and MUST NOT block, fail, or measurably slow the SSE response (NFR-1). Mirror the `CompleteAsync` guarantees.
- PII-light (`AiUsageLog` shape unchanged); module-internal (no cross-module refs).

## Tasks
| ID | Task | Module / target | Deps | Est (h) |
|---|---|---|---|---|
| P7-11b-BE-1 | **Capture usage in `StreamAsync`** — accumulate/collect the provider's end-of-stream `AiUsage` (token counts from the final/usage chunk where the provider supplies them), run it through the same `EnrichWithCost` path, then `_usageRecorder.Record(usage, request.Task)` after the stream completes successfully. Guard against partial/aborted streams (no record on caller-cancel/error, or record what's known — analyzer confirms provider stream-usage availability). | Ai.Infrastructure (`AiGateway.StreamAsync`) | — | 4 |
| P7-11b-BE-2 | **Drop the "non-streaming only" caveat** — update the XML docs/comments on `AiGateway`, `IPlatformAiSafetyStatsQuery.AiRequestVolume`, `PlatformKpiSummaryDto.AiRequestVolume`, and the P7-11 `TutorUsageDto` to reflect that streamed calls are now counted. No contract shape change. | Ai + Shared.Contracts + Identity.Application docs | BE-1 | 1 |
| P7-11b-BE-3 | **api-tester** — a streamed call (fake provider that streams + emits usage) results in an `ai.AiUsageLogs` row (poll, fire-and-forget); a caller-cancelled/errored stream behaves per the BE-1 decision; `/AiSafety/usage` + P7-10 `aiRequestVolume` include the streamed row. | tests | BE-1 | 3 |

## Acceptance-criteria coverage
- Streamed call → one `AiUsageLog` row via the recorder → BE-1; totals include streamed calls → BE-2/BE-3.
- Fail-soft, no SSE latency/blocking (NFR-1) → BE-1 (reuse recorder, post-stream, try/catch).
- PII-light unchanged → BE-1.

## Notes
- **security-auditor** recommended (AI gateway path) but the surface is small (reuses the audited recorder + entity); reviewer decides if a full re-audit is needed or the prior P7-11 audit covers it.
- If the configured fake/test provider does not stream usage, BE-3 may assert "row present with available fields" rather than exact token counts — note it, do not fabricate.
