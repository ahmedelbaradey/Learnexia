# P7-13 Gamification Admin Overrides — Frontend (Web E2E) Test Cases

> **Lighter reference for the frontend lead.** Surface = Next.js `admin-dashboard`. Override actions are sensitive (target a child) — require explicit confirm + reason. RTL + LTR.

| ID | Title | Type | Pri | Precondition | Steps | Expected |
|---|---|---|---|---|---|---|
| FE-TC-13-01 | Signed-out → redirect | auth | P0 | no session | open `/gamification` admin | redirect to sign-in |
| FE-TC-13-02 | Non-admin → blocked | auth-authz | P0 | non-admin | open admin gamification | 403 / redirect |
| FE-TC-13-03 | Badge catalog list (active + inactive) | functional | P0 | admin | open Badges | both active and inactive badges shown with status |
| FE-TC-13-04 | Create badge | functional | P0 | admin | create with valid fields | success; appears in list |
| FE-TC-13-05 | Duplicate code blocked | validation | P1 | admin | create with existing code | error surfaced (i18n), not raw 424 |
| FE-TC-13-06 | Edit badge | functional | P1 | admin | edit fields, save | persisted; list reflects change |
| FE-TC-13-07 | Deactivate/reactivate badge | state | P1 | admin | toggle active | status flips; stays in admin list |
| FE-TC-13-08 | Mission catalog CRUD | functional | P1 | admin | create/edit/deactivate mission | parity with badge flows |
| FE-TC-13-09 | Timed-event create/activate/expire | functional | P1 | admin | full lifecycle | transitions reflected; double-activate handled gracefully |
| FE-TC-13-10 | Timed-event validation (window/multiplier) | validation | P1 | admin | bad window or multiplier | inline validation; 422 surfaced as friendly message |
| FE-TC-13-11 | League-tier override needs confirm + reason | validation | P0 | admin + child | submit without confirm/reason | blocked; confirm + reason required |
| FE-TC-13-12 | Streak-freeze grant needs confirm + reason | validation | P0 | admin + child | grant without confirm/reason | blocked |
| FE-TC-13-13 | Streak-freeze cap surfaced | state | P1 | admin + child at cap | grant beyond cap | server cap message shown, not raw error |
| FE-TC-13-14 | RTL (Arabic) layout | RTL-i18n | P1 | admin, locale=ar | open each screen | mirrors correctly |
| FE-TC-13-15 | No child PII beyond identifiers | privacy | P1 | admin | inspect child-targeting screens | only id/tier/balance shown; no progress dumps |
