# Publish, version & preview curriculum content

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Curriculum Management
- **Issue type:** Story
- **Story Points:** 8 — a draft→published lifecycle with versioning and a student-eye preview spanning the whole hierarchy.
- **Labels:** `admin`, `curriculum`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-4

## Description
As an admin, I want curriculum content to move through a draft→published lifecycle with versions and a preview, so that I can edit safely and only expose vetted content to students.

## Acceptance Criteria
- Given a curriculum item (subject/unit/lesson/quiz), when the admin edits it, then changes accumulate in a **draft** state and are not served to students until published.
- Given a draft, when the admin clicks Publish, then a new published **version** is recorded with a timestamp and author, and students begin receiving it.
- Given a published item with newer draft edits, when the admin views it, then both the live version and the pending draft are distinguishable.
- Given any draft, when the admin opens Preview, then they see the content rendered as a student would, without it being published.
- Given a published item, when the admin reverts/rolls back, then the previous published version is restored.
- (Admin-only access; non-admin → 403/redirect.)

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P7-01..P7-04 (the content being versioned), P2-01 (hierarchy), P1-10 (admin shell), P1-05 (Admin policy).
- Lifecycle state (`Draft`/`Published`) + version rows live in the `learning`/`assessment` modules; publish is an atomic multi-write done in an explicit transaction (no Unit of Work). Student-facing reads filter to the latest published version. Admin-only per SRS §3.
