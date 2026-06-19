# P7-10 Platform Analytics — Frontend (Web E2E) Test Cases

> **Lighter reference for the frontend lead.** Surface = Next.js `admin-dashboard`. RTL (Arabic-default) + LTR. The honest-v1 contract is load-bearing for the FE: real cards render numbers; N/A facets must render an explicit "available after P5-03 / Fake provider / P7-11" state — not a zero, not an error.

| ID | Title | Type | Pri | Precondition | Steps | Expected |
|---|---|---|---|---|---|---|
| FE-TC-10-01 | Signed-out → redirect | auth | P0 | no session | open `/analytics` | redirect to admin sign-in |
| FE-TC-10-02 | Non-admin → blocked | auth-authz | P0 | non-admin | open `/analytics` | 403 / redirect; no KPI data |
| FE-TC-10-03 | KPI cards render | functional | P0 | admin | open Analytics | cards for lessons/attempts/active learners/missions/XP/subscriptions/AI safety render with numbers |
| FE-TC-10-04 | N/A facets show honest state | state | P0 | admin | inspect retention/session-duration/revenue/AI-request-volume cards | each shows its N/A reason as i18n text (e.g. "available after analytics events"), not 0 and not an error |
| FE-TC-10-05 | DAU/WAU/MAU labelled as activity proxy | functional | P1 | admin | inspect active-users card | labelled "active learners (activity)" until P5-03, not "sessions" |
| FE-TC-10-06 | Date-range filter re-queries | functional | P0 | admin | change range | request carries from/to; cards update |
| FE-TC-10-07 | Invalid range surfaced gracefully | validation/error | P1 | admin | pick from ≥ to (if UI allows) or >365d | 400 from API surfaced as friendly message, not raw 500/envelope |
| FE-TC-10-08 | Breakdown by subject / grade / language | functional | P1 | admin | switch breakdown dimension | charts/tables split by Math/Science/Arabic/English, grade, and ar/en |
| FE-TC-10-09 | No Social Studies subject appears | functional | P1 | admin | inspect subject breakdown | only the 4 product subjects; no Social Studies |
| FE-TC-10-10 | No child PII on dashboard | privacy | P1 | admin | inspect all cards | aggregates only; no individual child name/id surfaced |
| FE-TC-10-11 | RTL (Arabic) charts/layout | RTL-i18n | P1 | admin, locale=ar | open Analytics | charts/axes/cards mirror correctly |
| FE-TC-10-12 | Empty-platform / empty-window state | state | P2 | admin, empty window | pick far-future range | zeros render cleanly (real-data cards), not spinners/errors |
