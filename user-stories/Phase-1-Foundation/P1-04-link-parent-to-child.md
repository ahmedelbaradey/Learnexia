# Link a parent to a child account

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Identity & Onboarding
- **Issue type:** Story
- **Story Points:** 3 — new `ParentStudent` linkage + parent registration flow + authorization scoping.
- **Labels:** `auth`, `parent`, `backend`
- **Requirements:** FR-ID-3

## Description
As a parent, I want my account linked to each child I add, so that I can view their progress and weekly reports and manage their accounts.

## Acceptance Criteria
- Given a parent adds a child during onboarding (P1-03), then a `ParentStudent` relationship is created automatically for that child.
- An existing parent can also link to an additional child account they already provisioned.
- Given a parent linked to a child, then I can access only that child's data — never other families' (FR-PA scoping).
- A parent can be linked to multiple children; a child can be linked by more than one parent.
- Given an attempt to link to a non-existent child, then I see a clear error.

## Notes
- Adds Parent role + `ParentStudent` linkage (B1.3). **Primary linkage path is the add-child step in P1-03** (parent-driven onboarding), not a student-initiated invite.
- Enables Phase 5 parent reports (FR-PA-1/2) and per-child grade transition (P5-05). Out of scope here: report content itself.
- Open question: COPPA-style parental consent for under-13 (BRD §10) — track separately.
