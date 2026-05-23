# Manage subjects & units

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Curriculum Management
- **Issue type:** Story
- **Story Points:** 5 — CRUD over two hierarchy levels plus ordering and activate/deactivate, on top of the existing `learning` entities.
- **Labels:** `admin`, `curriculum`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-LR-1

## Description
As an admin, I want to create, edit, reorder and activate/deactivate subjects and their units, so that I can curate the top of the curriculum hierarchy without a developer running seed scripts.

## Acceptance Criteria
- Given the admin dashboard, when an admin opens Curriculum → Subjects, then all subjects (the 4: Math, Science, Arabic, English) list with their grade, sequence order, and active state.
- Given a subject, when the admin creates/edits a unit under it, then the unit persists with title, description, and sequence order scoped to that subject.
- Given a subject or unit, when the admin drags to reorder, then the new `SequenceOrder` is saved and the list reflects it on reload.
- Given a subject or unit, when the admin toggles active/inactive, then inactive items are hidden from the student curriculum but preserved (soft state, not deleted).
- Given a delete on a unit that still contains lessons, then the API rejects it with a clear "unit not empty" error.
- (Admin-only access; non-admin → 403/redirect.)

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P2-01 (curriculum hierarchy — `Subject`/`Unit`), P1-10 (admin shell), P1-05 (Admin policy).
- Admin-only per SRS §3; no teacher role. Subjects are fixed to the 4-subject product decision — admins curate, not invent new subjects beyond grade scoping.
