# Feed learning data back into the system (calibration loop)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 5 — Parent + Analytics (Week 8)
- **Epic:** Analytics & Insights
- **Issue type:** Story
- **Story Points:** 5 — aggregation jobs + difficulty recalibration + AI-question quality flags + threshold tuning hooks.
- **Labels:** `analytics`, `adaptivity`, `backend`, `data`, `network-effect`
- **Requirements:** FR-PA-4 (data feedback / calibration — SRS §4.7)

## Description
As the platform, I want aggregate learning outcomes to continuously improve question difficulty, content quality, and adaptivity thresholds, so that every student's data makes the product better for the next student.

> **Why this story exists:** the barrier-to-entry strategy's BE7 "data network effect" is the moat that's *"hard for a newcomer to replicate."* Today raw signals are captured (`P2-08`, `P5-03`) and AI questions are tagged with provenance (`P3-06` `GeneratedBy`), but **nothing consumes the aggregate to improve the system** — the data is dormant. Closes gap 3a-7.

## Acceptance Criteria
- A scheduled aggregation computes per-question **empirical difficulty** (p-value: % correct, avg time, hint usage) across students and **recalibrates the stored difficulty band** when it diverges from the authored label.
- **AI-generated questions** (`GeneratedBy`) with poor empirical signals (very high/low success, frequent reports) are **flagged for review** rather than auto-served, protecting question quality.
- Adaptivity thresholds used by `P3-08` can be **tuned from real outcome data** (config-driven, not hard-coded), with changes auditable.
- All calibration outputs are explainable and reversible (no silent destructive overwrite of authored content); a human can review before promotion where appropriate.
- Aggregation operates on **de-identified** aggregates — no per-child PII in the calibration pipeline.

## Notes
- **Security/privacy:** aggregates over minors' data — route through `security-auditor` (de-identification, no PII leakage).
- Depends on: `P2-08` (answers), `P3-06` (provenance), `P3-08` (adaptivity thresholds), `P5-03` (analytics). Synergizes with `P3-13` (richer signals → better calibration).
- Closes gap **3a-7**; turns BE7 from collected-but-dormant data into a compounding advantage.
