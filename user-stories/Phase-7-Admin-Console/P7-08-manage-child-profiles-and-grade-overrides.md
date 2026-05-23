# Manage child profiles & grade overrides

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — User & Account Management
- **Issue type:** Story
- **Story Points:** 5 — admin edits to a child's grade/language/country; grade override must re-scope curriculum while preserving history (per P5-06) via integration events.
- **Labels:** `admin`, `identity`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ID-2, FR-ID-3, FR-LR-1

## Description
As an admin, I want to edit a child's profile and override their grade, so that I can correct mistakes and help with support cases without forcing the parent to redo onboarding.

## Acceptance Criteria
- Given a child account, when I edit **language** or **country**, then the change is saved and reflected on the child's profile.
- Given a child account, when I **override the grade** and confirm, then the grade updates and the curriculum/skill tree **re-scopes to the new grade** (same behavior as the parent-driven transition, P5-06).
- **History is preserved:** XP, level, badges, streaks, and past mastery records carry over and are not deleted; re-scoping is signaled to learning/gamification via integration events (no cross-module FK).
- Given an invalid grade (outside 1–6) or an unsupported language/country, then the edit is rejected with a clear validation message.
- Every override/edit records **actor, timestamp, old → new values, and reason**, and is **audited** (P7-12).
- Only an admin can perform these edits; non-admin → 403/redirect.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P1-01/P1-03/P1-04 (Identity, parent/child), P7-06 (inspect/search), P7-12 (audit log); grade behavior mirrors P5-06.
- Reuses the **Identity** module. Grade override **preserves XP/badges/streaks/mastery history** (per P5-06) and re-scopes curriculum via `Shared.Contracts` integration events only. No teacher role.
