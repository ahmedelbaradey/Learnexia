# Per-child recommendation engine ("Areas to focus" → next actions)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics
- **Epic:** Parent Dashboard / Adaptive Guidance
- **Issue type:** Story
- **Story Points:** 5 — deterministic cross-signal aggregation + a daily job + a read seam/endpoint.
- **Labels:** `backend`, `learning`, `analytics`, `adaptive`
- **Requirements:** FR-PA-1, FR-PA-2

## Description
As a parent (and the child's app), I want each child to get a small set of **specific, explainable "what to do next" recommendations** — grounded in their real weaknesses and current level — so the "Areas to focus on" / "Recommendations from Lexi" cards show genuine guidance instead of stubs.

The recommendation **content is computed deterministically and for FREE** (rule-based, explainable, reproducible) — it is NOT a live LLM call. The optional kid-style AI *voice* over this content is a separate story (**P3-14 Lexi narration**, energy-costed).

## Acceptance Criteria
- For a child, the engine produces a small ranked set (cap **3–5**) of `RecommendationItem`s, each tied to a real skill/subject with a severity, a suggested **action type** (e.g. review concept / practice / quiz), and a **target difficulty**, plus localized (EN+AR) title/body/CTA **keys** (no rendered free-text persisted).
- The recommendation is computed from **three un-conflated signals** ("at his level"): (1) school **Grade** (Identity/JWT) → curriculum scope; (2) per-skill **mastery %** (Learning/P5-02 weak areas) → which areas + (3) **AdaptivityEngine** target difficulty per weak skill; gamification level is NOT used for content (motivational framing only, applied by the narration layer).
- Recommendations are **recomputed DAILY, once per child** by a scheduled job (after the P3-13 profile recompute) and **persisted** (one row per child per day); the read path does **not** recompute on every load.
- A parent can read **only their own child's** recommendations (IDOR via `IParentChildQuery`); a child with no weak areas / first-week → a well-formed empty-or-encouraging set, never an error.
- Cross-module access is via `Shared.Contracts` seams only — no cross-module FK / project reference. **No new module** (Learning computes).
- Deterministic + reproducible: the same inputs produce the same recommendations (no randomness, no LLM in the core).

## Notes
- Brief: [../../docs/briefs/recommendations-engine.md](../../docs/briefs/recommendations-engine.md). Locked design: Learning computes (free, deterministic); Lexi (Ai) narrates on-demand + costs energy (**P3-14**).
- **Foundations (all built):** P5-02 weak areas (`IStudentAllSubjectsWeakAreasQuery`), P3-08 AdaptivityEngine (`IAdaptivityService.GetTargetDifficulty`), P3-13 profile (`IStudentProfileService` + the nightly `StudentProfileRecomputeJob` to mirror).
- New seam: `Shared.Contracts/Learning/IStudentRecommendationsQuery → RecommendationItem[]` (consumed by the parent dashboard AND the P3-14 Lexi narration as its grounding).
- Surfacing: a dedicated `GET /Parent/Children/{id}/Recommendations` (the FE has a waiting `RecommendationsCard.tsx`/`FocusAreasCard.tsx`). FE is the other lead's — contract only.
- **Ask before any new design pattern** (CLAUDE.md rule #8).
