# Enforce role-based access control

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Identity & Onboarding
- **Issue type:** Story
- **Story Points:** 3 — policy setup across Student/Parent/Admin plus securing secrets out of appsettings.
- **Labels:** `auth`, `security`, `backend`
- **Requirements:** FR-ID, NFR-4

## Description
As a platform owner, I want every endpoint protected by role-based authorization, so that students, parents, and admins can only access what their role permits and child data stays protected.

## Acceptance Criteria
- Given an endpoint restricted to a role, when a user without that role calls it, then the request is rejected with 403.
- Students cannot access parent reports or other students' data; parents cannot access learning content as a learner; admin-only curriculum endpoints reject non-admins (per SRS §3 permission matrix).
- JWT secret and provider keys are read from secret storage / environment, not committed in `appsettings` (architecture.md §14).
- Unauthenticated requests to protected endpoints return 401.

## Notes
- Reuses the Identity module's role/permission model (B1.4); includes secrets management (O1.4).
