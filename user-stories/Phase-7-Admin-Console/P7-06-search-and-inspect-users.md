# Search & inspect users

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — User & Account Management
- **Issue type:** Story
- **Story Points:** 5 — paginated/filterable search over parents + children plus a read-only profile/family/activity view; reuses Identity, but aggregates cross-module activity via contracts.
- **Labels:** `admin`, `identity`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ID-1, FR-ID-3

## Description
As an admin, I want to search and inspect parent and child accounts, so that I can resolve support requests and investigate issues without touching another family's data unnecessarily.

## Acceptance Criteria
- Given the admin dashboard, when I open Users, then I see a **paginated list** of accounts filterable by role (parent/child), status (active/suspended), and a free-text query over name/email; results are server-paginated.
- Given a result, when I open it, then I see a **read-only profile**: name, email, role, status, created/last-active dates, and (for children) grade, language, and country.
- Given a parent, then I can see their **linked children** (family); given a child, then I can see their linked parent(s) — per the P1-04 `ParentStudent` linkage.
- Given a selected user, then I can view a **recent activity summary** (e.g. last sign-in, recent learning/gamification activity) sourced via integration contracts, not direct cross-module FKs.
- Only an admin can reach these views and endpoints; non-admin → 403/redirect.
- Read-only inspection is **audited** (who viewed which account, when) per the admin audit log (P7-12).

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P1-01/P1-03/P1-04 (Identity, parent/child).
- Reuses the **Identity** module. Activity summary crosses modules → consumed via `Shared.Contracts` integration seams only (no cross-module FK). No teacher role.
