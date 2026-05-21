# Auth & onboarding screens

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Frontend Foundation
- **Issue type:** Story
- **Story Points:** 3 — parent sign-up + add-child onboarding screens, plus a child login screen.
- **Labels:** `frontend`, `auth`, `onboarding`
- **Requirements:** FR-ID-1, FR-ID-2

## Description
As a parent, I want clear screens to register, add my children, and set each child's details, and a simple login screen my child uses, so that setup is quick and my child can sign in on their own afterward.

## Acceptance Criteria
- Parent flow: Splash → Login/Register (parent) → Add Child(ren) → per-child Grade/Language/Country setup, navigable end to end.
- The Add-Child screen lets the parent add multiple children and assign each a login email (P1-03).
- Register and login call the auth API and handle success/error states (invalid credentials, duplicate email).
- Child login screen: a child signs in with the email the parent assigned and lands on their own home dashboard in their chosen language (RTL for Arabic).
- All screens render correctly in Arabic (RTL) and English using the design system.

## Notes
- Blocked by P1-01 (parent register), P1-03 (parent-driven onboarding/add-child), P1-08 (design system). Covers F1.4.
- **Product decision:** no student self-registration screen; the only student-facing auth screen is login.
