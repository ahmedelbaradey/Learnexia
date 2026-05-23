# Manage lessons & lesson content

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Curriculum Management
- **Issue type:** Story
- **Story Points:** 8 — CRUD plus an ordered content-block editor and lesson metadata, the richest editing surface in the area.
- **Labels:** `admin`, `curriculum`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-1

## Description
As an admin, I want to create and edit lessons within a unit and author their content as ordered blocks, so that students see structured lesson material curated through the dashboard rather than seeded by hand.

## Acceptance Criteria
- Given a unit, when the admin creates/edits a lesson, then it persists with title, difficulty, estimated time, sequence order, and lock state scoped to the unit.
- Given a lesson, when the admin adds content blocks (e.g. text, image, video, callout), then blocks persist with a type, payload, and ordering within the lesson.
- Given a lesson's content blocks, when the admin reorders or removes a block, then the new order/removal is saved and reflected on reload.
- Given a lesson, when the admin reorders lessons within the unit, then each lesson's `SequenceOrder` updates accordingly.
- Given a delete on a lesson, then it is removed (or soft-deactivated) and no longer served to students, with its content blocks handled atomically.
- (Admin-only access; non-admin → 403/redirect.)

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P7-01 (subjects/units), P2-01 (`Lesson`), P1-10 (admin shell), P1-05 (Admin policy).
- Content blocks are an additive child of `Lesson` in the `learning` module; multi-write block reordering uses an explicit transaction (no Unit of Work). Admin-only per SRS §3.
