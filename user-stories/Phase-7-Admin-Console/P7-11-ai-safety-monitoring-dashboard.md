# AI-safety & quality monitoring dashboard

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Analytics & AI Oversight
- **Issue type:** Story
- **Story Points:** 5 — read-only aggregate dashboard over AI-safety signals (P3-02) + eval results (P6-02) plus tutor usage/cost; child-safety sensitive, admin-only.
- **Labels:** `admin`, `analytics`, `ai`, `safety`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-AI-4 (AI safety), NFR-1

## Description
As an admin, I want an AI-safety & quality monitoring dashboard, so that I can confirm the AI Safety Layer is working — see blocked/flagged AI outputs, eval pass/fail rates, and tutor usage & cost — and catch regressions before they reach children.

## Acceptance Criteria
- Given the admin dashboard, when I open AI Safety, then I see **safety signal aggregates** over a selectable time range: total AI tutor outputs, count/rate of **blocked** and **flagged** outputs (from the P3-02 Safety Layer verdicts), broken down by reason/category, subject, and language.
- The dashboard surfaces the latest **AI-safety eval results** (P6-02): pass/fail rate per run against the eval set, with the trend across runs and a clear indicator when a run breaches the safety threshold.
- The dashboard shows **AI tutor usage & cost**: request volume, token/cost trends, and average latency over the time range, with subject/grade breakdown.
- An admin can drill into a list of recent blocked/flagged outputs (content reference, verdict, reason, timestamp); only the minimum needed to assess safety is shown — no unnecessary child PII.
- All figures are **aggregates** sourced from P3-02 safety signals and P6-02 eval results via a reporting read-model; cross-module data comes through integration contracts, not cross-module FK joins. Aggregates may be cached and must not degrade live request latency (NFR-1).
- Only an admin can reach these views and endpoints; non-admin → 403/redirect.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P3-02 (AI Safety Layer), P6-02 (AI-safety eval set).
- P7-11 reads AI-safety signals from P3-02 (safety layer) and eval results from P6-02 (eval set), plus tutor usage/cost from the AI Gateway. Read/aggregate-only — no new write entities beyond optional cached aggregates. Charts are admin-facing (web). Child data is sensitive — surface only what the admin needs to assess safety. RTL/Arabic + English.
