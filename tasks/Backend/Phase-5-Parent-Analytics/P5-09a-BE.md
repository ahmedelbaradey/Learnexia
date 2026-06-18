# P5-09a (backend) — Profile-aware recommendation selection

> Story: [../../../user-stories/Phase-5-Parent-Analytics/P5-09a-profile-aware-recommendation-selection.md](../../../user-stories/Phase-5-Parent-Analytics/P5-09a-profile-aware-recommendation-selection.md)
> Brief: [../../../docs/briefs/recommendations-as-his-level-enrichment.md](../../../docs/briefs/recommendations-as-his-level-enrichment.md)
> Phase 5 · Module: **Learning** (enrich the merged P5-09 engine). PR 1 of 2 (engine first; Lexi framing = P3-14a, stacked).

## Design rules (binding)
- Keep `RecommendationEngine.Compute` **pure / static / deterministic / one class** — no new design pattern (rule #8), no LLM, no scoring black box. Every rule traces to a named `DerivedProfile` dimension (explainability AC).
- **Un-conflation:** gamification level stays OUT of the engine; grade stays out of ranking. The mastery signal still chooses WHICH areas; the profile only modulates action-type/quantity/difficulty-nudge/ordering within that set.
- **Moderate** rule set (lead-approved 2026-06-18). Thresholds in a new config-bound `RecommendationOptions` (Domain, mirror `StudentProfileOptions`/`AdaptivityOptions`).

## Tasks
| ID | Task | Module / target | Deps | Est (h) |
|---|---|---|---|---|
| P5-09a-BE-1 | **`RecommendationOptions`** (Domain) + bind in DI + `appsettings.json`: attention-span fatigue threshold, ColdStart/confidence threshold (reuse if present), quantity caps (min 3 / max 5), difficulty-nudge enable. Mirror `AdaptivityOptions`. | Learning.Domain + Infrastructure DI + Host appsettings | — | 2 |
| P5-09a-BE-2 | **Enrich `RecommendationEngine.Compute`** with the Moderate rules over `DerivedProfile` dims 1-4, gated by `DataPointCount`: (a) `RecurringErrorSkillIds` ∋ area → force `Review`; (b) low `AttentionSpanMinutes` → cap nearer 3; (c) bounded difficulty nudge to the low edge of the adaptivity band on `Review` items only (never crosses the band — `GetTargetDifficulty` stays source of truth); (d) ordering: recurring-error first within equal severity; (e) confidence gate: `0 < DataPointCount < threshold` → only rule (a), else grade+mastery; `==0` → unchanged cold-start. `PreferredExplanationStyle` = soft passthrough only (provisional signal — never a hard driver). | Learning.Application `RecommendationEngine` | BE-1 | 5 |
| P5-09a-BE-3 | **Optional `PreferredExplanationStyle` field** on `RecommendationItem` (`Shared.Contracts/Learning`) — nullable, additive; serialize into `ItemsJson` (additive jsonb — **no migration**; the daily job rewrites rows). Carried for P3-14a. | Shared.Contracts/Learning + RecommendationService serialization | BE-2 | 2 |
| P5-09a-BE-4 | **Enrich `RecommendationEngineTests`** — per-dimension determinism (recurring-error→Review, fatigue→smaller set, difficulty-nudge bounded, ordering), confidence-gate fallback at each `DataPointCount` band, cold-start unchanged, reproducibility. | Modules.Learning.UnitTests | BE-2 | 4 |
| P5-09a-BE-5 | **reviewer + mandatory security-auditor** (child behavioral data — confirm no raw error lists leak; explainability preserved). | gates | BE-1..4 | 3 |

## Acceptance-criteria coverage
- Moderate profile rules + un-conflation → BE-2; config-tunable → BE-1; cold-start/confidence fallback → BE-2 + BE-4; optional style field (additive, no migration) → BE-3; determinism/explainability → BE-4 + BE-5.

## Notes
- **No new endpoint, no migration** (additive jsonb). Parent endpoint contract backward-compatible. FE = other lead's (no change needed).
- Do NOT add gamification level here (P3-14a). New richer profile dimensions = backlog **P3-13a**.
