# Stay signed in (token refresh & sign-out)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Identity & Onboarding
- **Issue type:** Story
- **Story Points:** 2 — refresh/sign-out largely exist in the Identity module; mainly wiring + Redis blacklist.
- **Labels:** `auth`, `backend`
- **Requirements:** FR-ID-4

## Description
As a logged-in user, I want my session to refresh automatically and to be able to sign out, so that I stay logged in across visits without re-entering my password and can end my session securely on a shared device.

## Acceptance Criteria
- Given a valid refresh token, when the access token expires, then `POST /auth/refresh` returns a new access token.
- Given I sign out, when I call `POST /auth/logout`, then my refresh token is invalidated (Redis blacklist) and can no longer be exchanged.
- Given an expired or revoked refresh token, then refresh fails with 401 and I'm prompted to log in again.

## Notes
- Blocked by "Register as a student or parent" (P1-01).
- Reuses JWT auth/refresh/session already in the Identity module (B1.4).
