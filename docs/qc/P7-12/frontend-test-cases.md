# P7-12 Admin Audit Log — Frontend (Web E2E) Test Cases

> **Lighter reference for the frontend lead.** Surface = Next.js `admin-dashboard`. The log is **view-only** — the UI must expose no edit/delete. RTL + LTR.

| ID | Title | Type | Pri | Precondition | Steps | Expected |
|---|---|---|---|---|---|---|
| FE-TC-12-01 | Signed-out → redirect | auth | P0 | no session | open `/audit` | redirect to admin sign-in |
| FE-TC-12-02 | Non-admin → blocked | auth-authz | P0 | non-admin | open `/audit` | 403 / redirect |
| FE-TC-12-03 | Log renders newest-first | functional | P0 | admin + rows | open Audit | rows by actor/action/target/timestamp, newest first |
| FE-TC-12-04 | View-only — no edit/delete affordances | state | P0 | admin | inspect each row | no edit/delete buttons anywhere (immutability is UI-enforced too) |
| FE-TC-12-05 | Filter by actor / action / target / date | functional | P1 | admin + mixed rows | apply each filter | list narrows; params reflected |
| FE-TC-12-06 | Empty / no-match state | state | P1 | admin + no-match filter | filter to nothing | friendly empty state (i18n), not error |
| FE-TC-12-07 | Export filtered log (CSV/JSON) | functional | P1 | admin | click export with filters active | download honors active filters (confirm BE vs client-side export) |
| FE-TC-12-08 | Details show ids/states only (no PII) | privacy | P0 | admin | inspect Details column | ids + enum/before→after only; no names/emails/child content |
| FE-TC-12-09 | RTL (Arabic) layout | RTL-i18n | P1 | admin, locale=ar | open Audit | table/filters mirror correctly |
| FE-TC-12-10 | Pagination works | functional | P2 | admin + >1 page | page through | server-side paging; no full-table load |
