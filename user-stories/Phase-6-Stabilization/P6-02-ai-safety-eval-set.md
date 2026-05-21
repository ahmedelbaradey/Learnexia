# Validate AI safety with an eval set

- **Project:** Learnexia
- **Sprint / Phase:** Phase 6 — Stabilization (Week 9)
- **Epic:** QA & Stabilization
- **Issue type:** Story
- **Story Points:** 5 — curated eval set + harness for age-appropriateness and hallucination; child-safety critical.
- **Labels:** `qa`, `ai`, `safety`
- **Requirements:** FR-AI-4

## Description
As a parent, I want the AI safety filtering proven against a test set before launch, so that I can trust child-safety isn't just best-effort.

## Acceptance Criteria
- An AI safety eval set covers age-appropriateness and hallucination spot-checks across subjects and both languages.
- The Safety Layer is run against the eval set with pass/fail thresholds recorded.
- Failures are triaged; safety-critical failures block launch until resolved.
- The eval set is re-runnable for future prompt/model changes.

## Notes
- Covers Q1.3. Validates the mandatory Safety Layer (P3-02 / FR-AI-4).
