# Route AI requests through an AI Gateway

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** AI Gateway
- **Issue type:** Technical Enabler
- **Story Points:** 5 — new `Ai` module (.NET in-process gateway) + provider abstraction + model routing; foundation for all AI features.
- **Labels:** `ai`, `gateway`, `backend`
- **Requirements:** NFR-2, NFR-9

## Description
As an AI engineer, I want an in-process **`IAiGateway`** (a new `Ai` module in the .NET modular monolith) sitting between feature handlers and the LLM providers, so that all AI calls share one abstraction with provider routing and cost control.

## Acceptance Criteria
- The .NET backend calls AI features only through the gateway, never an LLM directly.
- The gateway abstracts LLM providers behind one interface (**Claude default**; GPT/Gemini swappable) and routes by task (cheap model for hints/simple explanations, premium for hard reasoning).
- A provider outage or error returns a graceful, typed error to the backend (no raw stack traces).
- Model/provider choice is configurable without code changes.

## Notes
- Covers A1.1. Provider abstraction mitigates single-provider dependency (BUSINESS_PLAN §7).
