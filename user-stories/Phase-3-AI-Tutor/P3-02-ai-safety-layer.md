# Filter AI output through a Safety Layer

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — AI Tutor (Week 5–6)
- **Epic:** AI Gateway
- **Issue type:** Story
- **Story Points:** 5 — mandatory, child-safety-critical filtering with multiple checks; high importance and care.
- **Labels:** `ai`, `safety`, `security`
- **Requirements:** FR-AI-4 (mandatory)

## Description
As a parent, I want every AI response checked for safety before my child sees it, so that I can trust the tutor never shows toxic, age-inappropriate, or fabricated content.

## Acceptance Criteria
- Every AI-generated response passes through the Safety Layer before display — no bypass path exists.
- The layer filters for toxicity, age-appropriateness, and likely hallucination.
- Given content fails a check, then it is blocked/regenerated and never returned to the student; the event is logged.
- The filter behavior is covered by a test/eval set (expanded in Phase 6, P6-02).

## Notes
- Covers A1.2. Non-negotiable per BRD §7 and a key risk mitigation (BRD §9).
