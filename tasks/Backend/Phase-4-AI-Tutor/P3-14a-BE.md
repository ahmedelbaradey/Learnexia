# P3-14a (backend) — Level- and profile-aware Lexi framing

> Story: [../../../user-stories/Phase-4-AI-Tutor/P3-14a-level-profile-aware-lexi-framing.md](../../../user-stories/Phase-4-AI-Tutor/P3-14a-level-profile-aware-lexi-framing.md)
> Brief: [../../../docs/briefs/recommendations-as-his-level-enrichment.md](../../../docs/briefs/recommendations-as-his-level-enrichment.md)
> Phase 4 · Module: **Ai** (enrich the merged P3-14 narration). **PR 2 of 2 — stacked on P5-09a** (consumes the persisted style field).

## Design rules (binding)
- **Prompt-template + context enrichment only** — NO new orchestration, NO new energy cost (still `ai_cost.recommendation = 5`, charge-per-delivery, cache-hit-charges). Grounding stays strictly the persisted `RecommendationItem[]` (no skill invention).
- **Un-conflation in the prompt:** grade → vocabulary/scope; gamification level → motivational framing; profile → encouragement style — three distinct fragments. Framing must never change which areas or difficulty (keep the existing template guardrails: no level-assessment, no lesson-unlocking).
- **PII-minimisation:** only derived, anonymous hints reach the prompt (a level number, a coarse style word) — never `StudentId`, `RecurringErrorSkillIds`, or per-skill error data.

## Tasks
| ID | Task | Module / target | Deps | Est (h) |
|---|---|---|---|---|
| P3-14a-BE-1 | **Extend `PromptContext`** with `CurrentLevel` (int) + an optional anonymous `EncouragementStyle`/style hint. | Ai.Application `PromptContext` | — | 1 |
| P3-14a-BE-2 | **`PromptBuilder` fragments** — a motivational level line (e.g. "Level {n} — frame the next step as levelling up") + an encouragement-style line; injected only for `HelperIntent.Recommendation`. | Ai.Application `PromptBuilder` | BE-1 | 2 |
| P3-14a-BE-3 | **Templates (EN+AR)** — update all four subject `…Recommendation` constants to consume the new fragments, with the existing framing guardrails intact. | Math/Science/Arabic/English templates | BE-2 | 2 |
| P3-14a-BE-4 | **Handler wiring** — `RecommendationNarrationCommandHandler` injects `IStudentXpQuery` (Ai adds the DI registration of the EXISTING seam; default `CurrentLevel=1` on null), reads the persisted `PreferredExplanationStyle` from the grounding it already fetches (no cross-module profile call), populates the new `PromptContext` fields. | Ai.Application handler + Ai DI | BE-1, P5-09a-BE-3 | 3 |
| P3-14a-BE-5 | **Cache-key fix (REQUIRED correctness)** — `AiCacheKeyBuilder.ForRecommendation` must include `CurrentLevel` (+ the style hint) so a level-up / changed profile yields a fresh narration, not a stale cache hit. | Ai.Application `AiCacheKeyBuilder` | BE-1 | 1 |
| P3-14a-BE-6 | **api-tester + mandatory security-auditor** — Lexi SSE still correct (charge-per-delivery, no-delivery=no-debit); **cache freshness on level change** (a higher level → fresh narration, still debits); prompt carries only anonymous derived hints (no raw behavioral data / StudentId); grounding unchanged. | tests | BE-1..5 | 4 |

## Acceptance-criteria coverage
- Level → motivational framing (closes the P3-14 AC) → BE-2/BE-3/BE-4; profile → encouragement style (anonymous) → BE-1/BE-4; un-conflation + guardrails → BE-3; cache freshness → BE-5 + BE-6; no economy change → BE-4 (cost untouched); PII-minimisation → BE-6.

## Notes
- **No new energy cost, no new intent, no new endpoint** — reuses the existing Lexi recommendation SSE route + `HelperIntent.Recommendation`.
- Reuses the existing `IStudentXpQuery` seam (no new contract). Stacked on P5-09a for the persisted style field.
- **Mandatory security-auditor** (AI prompt + child data) before reviewer — Critical/High block.
