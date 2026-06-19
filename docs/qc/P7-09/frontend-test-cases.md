# P7-09 Moderation Queue — Frontend (Web E2E) Test Cases

> **Lighter reference for the frontend lead.** Surface = Next.js `admin-dashboard` (not the student Expo app). The backend lead owns the API cases; this doc exists so the FE/admin lead has a starting catalog. Routes/components are illustrative — confirm against the admin-dashboard implementation when it lands. RTL (Arabic-default) + LTR (English).

| ID | Title | Type | Pri | Precondition | Steps | Expected |
|---|---|---|---|---|---|---|
| FE-TC-09-01 | Signed-out → redirect | auth | P0 | no session | navigate to `/moderation` | redirected to admin sign-in |
| FE-TC-09-02 | Non-admin → blocked | auth-authz | P0 | non-admin session | navigate to `/moderation` | 403 page / redirect; no queue data |
| FE-TC-09-03 | Queue renders with columns | functional | P0 | admin + ≥1 item | open Moderation | table shows source, contentRef, status, detectedAt; pagination control present |
| FE-TC-09-04 | Empty queue state | state | P1 | admin + no items | open Moderation | friendly empty state (i18n text), not an error/spinner |
| FE-TC-09-05 | Filter by status / source / subject / grade / date | functional | P1 | admin + mixed items | apply each filter | list narrows; query params reflected in request |
| FE-TC-09-06 | Search by content reference | functional | P1 | admin | type a content-ref fragment | results filter to matches |
| FE-TC-09-07 | Open item detail | functional | P0 | admin + item | click a row | detail panel shows safety verdict (failed checks / reason codes), source, status, timestamp |
| FE-TC-09-08 | Approve action | functional | P0 | admin + Pending item | open detail → Approve | success toast; row status → Approved; list refreshes |
| FE-TC-09-09 | Reject requires reason | validation | P0 | admin + Pending item | open Reject modal → submit with empty reason | inline validation blocks submit; reason required (i18n message, not raw key) |
| FE-TC-09-10 | Reject with reason succeeds | functional | P0 | admin + Pending item | Reject with reason | success; status → Rejected |
| FE-TC-09-11 | Flag action | functional | P1 | admin + Pending item | open detail → Flag | success; status → Flagged |
| FE-TC-09-12 | Re-review terminal item disabled/handled | state | P1 | admin + Approved item | open Approved item | review actions disabled OR server 400 surfaced as friendly message (not a raw 500) |
| FE-TC-09-13 | Server error surfaced from BaseResponse | state/error | P1 | admin | force an API failure | `BaseResponse` message shown as i18n text, not raw envelope/keys |
| FE-TC-09-14 | RTL (Arabic) layout | RTL-i18n | P1 | admin, locale=ar | open Moderation | table + modals mirror correctly; no clipped/LTR-stuck controls |
| FE-TC-09-15 | LTR (English) layout | RTL-i18n | P2 | admin, locale=en | open Moderation | layout correct in LTR |
| FE-TC-09-16 | No child PII leaked in UI | a11y/privacy | P1 | admin + Ai-output item | inspect detail | only reason codes / content ref shown; no prompt/response text, no child personal data |
