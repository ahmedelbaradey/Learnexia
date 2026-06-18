# Lexi recommendation narration (kid-style AI voice over the recommendations)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor
- **Epic:** AI Tutor (Lexi)
- **Issue type:** Story
- **Story Points:** 3 — a new AI intent reusing the existing Explain orchestration, grounded on persisted content.
- **Labels:** `backend`, `ai`, `parent`, `energy`
- **Requirements:** FR-AI-*, FR-PA-2

## Description
As a child/parent, I want **Lexi** to explain the day's recommendations in a warm, kid-friendly voice tuned to the child's grade — so the guidance feels personal and motivating. Lexi only **narrates** the recommendation content that the deterministic engine (**P5-09**) already produced; it never recomputes recommendations itself.

This is a **new AI intent** that reuses the full existing AI path (safety + cache + energy debit) and therefore **costs energy** like the other helpers.

## Acceptance Criteria
- A new `HelperIntent.Recommendation` AI intent narrates the child's **persisted** recommendation set (from `IStudentRecommendationsQuery`) in kid-style language tuned to the child's **Grade** (tone/scope) and motivational **gamification level** framing — it does NOT call the recommendation engine live or invent new recommendations.
- The intent runs through the **same orchestration as Explain**: rate-limit → child-access gate → energy **pre-authorization** → cache → safety layer → **debit on successful delivery**. Server-resolved cost `ai_cost.recommendation = 5` (Practice tier), client-blind via `CreditCostResolver`.
- **Charge-per-delivery** (consistent with the locked energy model — a delivered narration, including a cache hit, debits energy). No delivery (safety-block / refuse-and-redirect / failure) → no debit.
- Insufficient energy / paused-or-locked child → the same friendly blocked responses as the other AI intents (no narration delivered, no debit).
- Output is localized/kid-appropriate (EN + AR), grounded strictly on the recommendation content (no hallucinated skills), and passes the safety layer.

## Notes
- Brief: [../../docs/briefs/recommendations-engine.md](../../docs/briefs/recommendations-engine.md). **Depends on P5-09** (the persisted recommendations are the grounding) — fast-follow after the engine ships.
- **Lead-approved (rule #8) 2026-06-18:** adding the 5th `HelperIntent` (`Recommendation`) is approved; energy cost = **5**; **charge-per-delivery**.
- Reuses: the Explain command/handler shape, `ISafetyLayer`, the prompt builder/`TemplateSelector` (add a Recommendation template), the AI cache, `ICreditSpendService` pre-auth + `TryDebitAsync`, and the child-access gate. New: `ai_cost.recommendation` GlobalSetting key + a `CreditReasonCode` for the debit.
- Trigger: on-demand "Ask Lexi" (parent surface for v1; child surface optional later). FE is the other lead's — contract only (mirrors the Explain SSE/response shape).
- **Ask before any further new design pattern** beyond this approved intent (CLAUDE.md rule #8).
