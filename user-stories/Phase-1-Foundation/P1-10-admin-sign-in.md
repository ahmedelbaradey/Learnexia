# Sign in to the admin dashboard

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Identity & Onboarding
- **Issue type:** Story
- **Story Points:** 3 — admin login reuses Identity; cost is the seeded admin, admin-only guard, and the Next.js dashboard shell.
- **Labels:** `admin`, `auth`, `frontend`, `backend`
- **Requirements:** SRS §3 (Admin role), FR-ID-1, FR-ID-4, NFR-4

## Description
As an admin, I want to sign in to a secure admin dashboard, so that I can manage curriculum, subjects, and content moderation (those features arrive in later phases — this story establishes admin access and the shell).

## Acceptance Criteria
- Given the admin web app, when an admin signs in with a valid email and password, then a JWT is issued and they land on the dashboard shell.
- Admin accounts are **provisioned by seeding/invite — never public self-registration** (consistent with SRS §3 and the no-self-register product decision).
- Given non-admin credentials, when they attempt to reach admin routes, then access is denied (redirect to login / 403).
- Session refresh and sign-out work for admins (reuse Identity, P1-02).
- The dashboard shell renders authenticated navigation with placeholders for later admin features (curriculum upload, content management) — no feature logic yet.

## Notes
- The admin surface is the **Next.js `admin-dashboard`** app (not the Expo student app) per [../../docs/dev/FRONTEND_ARCHITECTURE.md](../../docs/dev/FRONTEND_ARCHITECTURE.md).
- Builds on RBAC (P1-05) for the Admin role + admin-only policies, and on the Identity module/JWT (architecture.md §4.1).
- **Admin feature stories** (curriculum upload/structuring, content moderation) live in the Backlog (BL-01..BL-05) and Phase 2+. This story is auth + shell only.
- There is **no teacher role** in the product (SRS §3).
