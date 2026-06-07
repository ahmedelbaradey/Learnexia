# Suspend, reactivate & delete accounts

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — User & Account Management
- **Issue type:** Story
- **Story Points:** 5 — lifecycle state machine on Identity accounts with confirmation + reason, cascade to linked children, and integration-event notification of other modules.
- **Labels:** `admin`, `identity`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-6

## Description
As an admin, I want to suspend, reactivate, and delete parent or child accounts, so that I can enforce policy and handle abuse, support, or data-removal requests safely.

## Acceptance Criteria
- Given an account, when I **suspend** it, then its `AccountStatus` flips to suspended, **any active sessions are revoked** (it can no longer sign in or hold a valid token), and the action requires a typed **confirmation** and a **reason**. (`AccountStatus`/Suspend is a deliberate admin governance state — **distinct from the P1-13 failed-login lockout**, which is automatic and temporary.)
- Given a suspended account, when I **reactivate** it, then sign-in is restored; the prior reason/history remains visible.
- Given a parent account, when I suspend or delete it, then I am warned about and can cascade the effect to its **linked children** (per P1-04 family scope).
- Given a **delete** request, when I confirm (two-step, typed confirmation + reason), then the account is removed/anonymized and other modules are notified via integration events to clean up their data (no direct cross-module writes).
- Every lifecycle action records **actor, timestamp, target, and reason** and is **audited** (P7-12); suspending/deleting an already-deleted account is rejected with a clear message.
- Only an admin can perform these actions; non-admin → 403/redirect.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P1-01/P1-03/P1-04 (Identity, parent/child), P7-06 (inspect/search), P7-12 (audit log).
- Reuses the **Identity** module. `AccountStatus`/Suspend is a distinct governance state from the **P1-13 lockout** (automatic, temporary, failed-login throttling); suspend additionally **revokes existing sessions/tokens**. Cross-module cleanup happens via `Shared.Contracts` integration events, never direct FK/writes — the account-lifecycle events (suspend/reactivate/delete) are consumed by **Gamification** and **Parent** (which hold per-student state) as well as Learning, not only by Learning. Child data is sensitive — confirmation + reason + audit are mandatory. No teacher role.
