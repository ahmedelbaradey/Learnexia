# P7-11 AI-Safety Dashboard — Frontend (Web E2E) Test Cases

> **Lighter reference for the frontend lead.** Surface = Next.js `admin-dashboard`. Backend lives on PR #184 — FE work proceeds against the contract in `tasks/Backend/Phase-7-Admin-Console/P7-11-BE.md`. Child-safety sensitive: drill-in shows only what's needed to assess safety. RTL + LTR.

| ID | Title | Type | Pri | Precondition | Steps | Expected |
|---|---|---|---|---|---|---|
| FE-TC-11-01 | Signed-out → redirect | auth | P0 | no session | open `/ai-safety` | redirect to sign-in |
| FE-TC-11-02 | Non-admin → blocked | auth-authz | P0 | non-admin | open `/ai-safety` | 403 / redirect |
| FE-TC-11-03 | Safety-signal cards render | functional | P0 | admin | open AI Safety | total outputs, blocked/flagged count + rate cards |
| FE-TC-11-04 | Breakdown by reason / subject / language | functional | P1 | admin | switch breakdown | charts split by reason/category, subject, ar/en |
| FE-TC-11-05 | Eval results facet (degrade state) | state | P0 | admin (P6-02 absent) | open evals section | honest "available after eval set (P6-02)" state, not error/zero-as-real |
| FE-TC-11-06 | Usage & cost facet (degrade state) | state | P0 | admin (P3-01 absent) | open usage section | honest N/A for request volume/cost until AI Gateway, not error |
| FE-TC-11-07 | Threshold-breach indicator | functional | P1 | admin (eval data present) | inspect eval trend | clear breach indicator when run < threshold |
| FE-TC-11-08 | Drill-in flagged outputs list | functional | P1 | admin | open flagged drill-in | content ref, verdict, reason, timestamp; paginated |
| FE-TC-11-09 | No unnecessary child PII in drill-in | privacy | P0 | admin | inspect flagged rows | no prompt/response text, no child personal data |
| FE-TC-11-10 | Date-range filter re-queries | functional | P0 | admin | change range | all facets update; from/to in requests |
| FE-TC-11-11 | RTL (Arabic) charts/layout | RTL-i18n | P1 | admin, locale=ar | open AI Safety | charts/cards mirror correctly |
| FE-TC-11-12 | Empty window state | state | P2 | admin, empty window | far-future range | zeros render cleanly for real facets; no spinner/error |
