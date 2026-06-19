# P7-07 — Suspend / Reactivate / Delete — Frontend (web E2E) test cases (reference)

**Target agent:** `frontend-e2e-tester` (FE lead owns the Next.js `admin-dashboard`).
**Status:** REFERENCE. If the admin dashboard UI is not built, mark all **Blocked (UI not implemented)**.

These are governance actions on PII — the UI MUST enforce typed confirmation + reason before any destructive call, mirroring the backend gates.

| ID | Title | Type | Pri | Steps | Expected |
|----|-------|------|-----|-------|----------|
| FE-TC-07-01 | Non-admin cannot reach lifecycle actions | authz | P0 | Parent visits an account page | No suspend/delete controls; route blocked |
| FE-TC-07-02 | Suspend requires reason + typed confirmation | functional | P0 | Click Suspend | Modal demands a reason; submit disabled until typed confirmation entered |
| FE-TC-07-03 | Suspend success updates status badge to Suspended | functional | P0 | Confirm suspend | Row/profile shows Suspended; success toast (i18n) |
| FE-TC-07-04 | Reactivate restores Active status | functional | P0 | Reactivate a suspended account | Status → Active; prior reason/history still visible |
| FE-TC-07-05 | Delete = two-step typed confirmation + reason | functional | P0 | Click Delete | Two-step confirm; cannot submit without exact confirmation phrase + reason |
| FE-TC-07-06 | Delete a parent → cascade-children warning | functional | P0 | Delete a parent with children | Warning lists linked children; explicit cascade choice required |
| FE-TC-07-07 | Cancelling the confirm dialog performs no action | state | P1 | Open delete dialog, cancel | No request sent; account unchanged |
| FE-TC-07-08 | Already-deleted/illegal action shows clear error | error | P1 | Attempt to suspend a deleted account | Localized "already deleted" message, not a raw 4xx |
| FE-TC-07-09 | Admin cannot suspend/delete self or SuperAdmin | authz | P1 | Open own / superadmin account | Destructive controls disabled/hidden |
| FE-TC-07-10 | Error surfacing uses i18n copy, not raw envelope keys | error | P1 | Force a 4xx | Friendly localized message |
| FE-TC-07-11 | RTL layout for dialogs when locale = ar | RTL-i18n | P2 | Switch to Arabic | Confirm dialogs mirror RTL; Arabic copy |
