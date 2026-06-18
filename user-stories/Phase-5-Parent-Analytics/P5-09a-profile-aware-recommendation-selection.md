# Profile-aware recommendation selection (P5-09 enrichment)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics
- **Epic:** Adaptive Guidance
- **Issue type:** Story (enrichment of P5-09)
- **Story Points:** 3 — deterministic rules over the existing P3-13 profile dimensions + config.
- **Labels:** `backend`, `learning`, `adaptive`
- **Requirements:** FR-PA-1, FR-PA-2, FR-AD-5 (consumes the P3-13 profile)

## Description
The merged P5-09 engine personalizes by mastery + adaptivity but treats the rich P3-13 behavioral profile as a single cold/rich boolean — 4 of its 5 dimensions are dead inputs. This story makes the **deterministic** engine genuinely fit *how each child learns* by feeding the existing `DerivedProfile` dimensions into recommendation **action-type, quantity, difficulty-nudge, and ordering** — keeping it free, explainable, and reproducible, and keeping the "at his level" signals **un-conflated** (gamification level stays OUT of the engine).

## Acceptance Criteria
- The engine uses these existing `DerivedProfile` dimensions (gated by `DataPointCount` confidence): **Moderate** rule set —
  - a weak area whose `SkillId` is in `RecurringErrorSkillIds` → forced `Review` action-type (repeated errors mean practice alone hasn't worked), even at Medium severity;
  - a fatigue-prone child (low `AttentionSpanMinutes`, below a configured threshold) → smaller item set (nearer 3, not 5);
  - a **bounded** difficulty nudge toward the low edge of the AdaptivityEngine band on `Review` items only — **never** crossing the band (`IAdaptivityService.GetTargetDifficulty` stays the source of truth);
  - ordering: surface a recurring-error item first within equal severity.
- **Cold-start / low-confidence:** `DataPointCount == 0` → current behaviour (grade + mastery only, single Celebrate item); `0 < DataPointCount < ColdStartDataPointThreshold` → apply only the most conservative rule (RecurringError→Review), else grade + mastery.
- **Determinism + explainability preserved:** same inputs → same output; every rule traces to a named profile dimension. No LLM, no scoring black box, no new design pattern (rule #8).
- **Un-conflation:** gamification level is NOT used in the engine (it is Lexi's framing signal — P3-14a). Grade stays out of ranking (scope/tone only).
- Thresholds live in a new `RecommendationOptions` (config-bound, mirroring `StudentProfileOptions`/`AdaptivityOptions`) so product can tune without a deploy.
- Backward-compatible output: the parent endpoint contract is unchanged except an **optional, nullable** `PreferredExplanationStyle` field on `RecommendationItem` (additive jsonb — no migration), carried for P3-14a to honour.

## Notes
- Brief: [../../docs/briefs/recommendations-as-his-level-enrichment.md](../../docs/briefs/recommendations-as-his-level-enrichment.md). Enriches **P5-09**; the `DerivedProfile` (P3-13) exposes only 5 fields today — this uses them. New richer derivations (grit/time-of-day) are **backlog P3-13a**.
- **Risk:** `PreferredExplanationStyle` is provisional in P3-13 — use it only as a passthrough/soft tiebreaker, never a hard action/difficulty driver.
- BE-only (FE contract backward-compatible). Mandatory security-auditor (child behavioral data).
