# Level- and profile-aware Lexi framing (P3-14 enrichment)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor
- **Epic:** AI Tutor (Lexi)
- **Issue type:** Story (enrichment of P3-14)
- **Story Points:** 2 — prompt-template + context enrichment; no new orchestration, no new energy cost.
- **Labels:** `backend`, `ai`, `adaptive`
- **Requirements:** FR-AI-*, FR-PA-2

## Description
The merged P3-14 Lexi narration tunes its tone only on the child's grade — even though its own AC promised **motivational gamification-level framing**, and the child's behavioral profile could shape encouragement style. This story closes that AC gap and makes Lexi's voice fit the child's level: grade → vocabulary/scope, gamification level → motivational framing, profile → encouragement style — three **un-conflated** prompt fragments. Still grounded only on the persisted recommendations; no new energy cost.

## Acceptance Criteria
- The narration prompt now includes the child's **gamification level** (motivational "level-up" framing) — fetched via the existing `IStudentXpQuery` seam (`CurrentLevel`, default 1 on null). Framing ONLY: it must never change which areas or the difficulty (existing template guardrails preserved — no level-assessment, no lesson-unlocking).
- The narration also uses a **coarse, anonymous** profile-derived **encouragement-style** hint (e.g. fatigue-prone → shorter/warmer; `PreferredExplanationStyle` → explanation tone), sourced from the persisted `RecommendationItem` style field (P5-09a) — so the Ai handler needs **no cross-module profile call** and no raw behavioral data enters the prompt.
- Grade-based vocabulary/scope tuning is preserved; the three signals stay un-conflated in the prompt (distinct fragments, never merged).
- **PII-minimisation:** only derived, anonymous hints (a level number, a coarse style word) reach the prompt — never `StudentId`, `RecurringErrorSkillIds`, or per-skill error data.
- **Cache correctness (required):** the narration cache key (`AiCacheKeyBuilder.ForRecommendation`) now also includes `CurrentLevel` (and the style hint) so a level-up / changed profile yields a fresh narration rather than a stale cached one.
- **No economy change:** still `HelperIntent.Recommendation`, cost `ai_cost.recommendation = 5`, charge-per-delivery, cache-hit-charges — the enrichment adds quality to the same call, not a new billable action.

## Notes
- Brief: [../../docs/briefs/recommendations-as-his-level-enrichment.md](../../docs/briefs/recommendations-as-his-level-enrichment.md). Enriches **P3-14**; **stacked on P5-09a** (consumes the persisted style field).
- Reuses the existing `IStudentXpQuery` seam (Gamification implements it; Learning already consumes it) — Ai just adds a DI registration + handler injection. No new contract.
- BE-only. Mandatory security-auditor (AI prompt + child data — confirm only anonymous derived hints reach the prompt).
