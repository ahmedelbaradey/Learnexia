# Register as a student or parent

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Identity & Onboarding
- **Issue type:** Story
- **Story Points:** 3 — reuses existing Identity module/JWT; cost is parent self-registration plus parent-provisioned child accounts.
- **Labels:** `auth`, `backend`, `frontend`
- **Requirements:** FR-ID-1

## Description
As a parent, I want to register myself with email and password, so that I can then create accounts for my children and manage their learning.

## Acceptance Criteria
- Given the registration screen, when a parent enters a valid email and password, then a parent account is created and a JWT access token is returned.
- A **child account is not self-registered** — it is provisioned by the parent during onboarding (P1-03) using a login email the parent assigns.
- Given an email that's already registered (parent or child), then a clear error is shown and no duplicate account is created.
- Given a password that fails the strength rules, then registration is blocked with a specific message.
- Passwords are stored hashed (never in plain text); the response never returns the password.

## Notes
- **Product decision:** sign-up is parent-led. Students do not self-register; the parent creates child accounts in P1-03 and the child logs in with the assigned email (P1-09).
- Reuses the `identity` module (`AspNetUsers`, roles, JWT) from backend (SRS §7).
- There is **no teacher role** in the product (SRS §3).
