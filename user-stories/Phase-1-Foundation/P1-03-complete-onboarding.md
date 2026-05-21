# Parent completes onboarding and adds children

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Identity & Onboarding
- **Issue type:** Story
- **Story Points:** 5 — parent-driven multi-child onboarding + child-account provisioning with assigned login email + per-child profile setup.
- **Labels:** `auth`, `onboarding`, `frontend`, `backend`
- **Requirements:** FR-ID-2 (modified — see Notes)

## Description
As a parent, I want to add one or more of my children and complete each child's onboarding myself, so that my children get accounts set up for their grade and language and can simply log in and start learning.

## Acceptance Criteria
- Given a registered parent in onboarding, when I add a child, then I enter the child's details and set grade (1–6), language (Arabic/English), and country for that child.
- I can add more than one child in the same onboarding flow; each child gets a separate profile and account.
- Adding a child provisions a child account with a **login email I assign**; the child later logs in with that email (see P1-01 / P1-09).
- Onboarding completion is a **parent action** — a child cannot self-register or self-onboard.
- Given an invalid grade (outside 1–6) or an email already in use, then that child entry is rejected with a specific message and no account is created.
- Each child's chosen language sets that child's app locale (including RTL for Arabic) on first login.

## Notes
- **Product decision (overrides SRS FR-ID-2):** the source SRS implies the *student* completes onboarding (grade/language/country). Per current direction, **the parent** completes onboarding and creates child accounts. FR-ID-2's captured fields are unchanged; only the actor changes.
- Creates the `ParentStudent` linkage as children are added — reconcile with P1-04 (linkage) and P1-01 (registration/child-account provisioning).
- Grade/language/country feed the Prompt Builder in Phase 3 (FR-AI-5). Grade can later be changed via the parent dashboard's grade-transition feature (P5-05 / P5-06).
- Open question: COPPA-style parental consent for under-13 (BRD §10) — track separately; parent-driven onboarding partly addresses it.
