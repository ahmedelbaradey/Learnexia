# Manage subjects & units

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Curriculum Management
- **Issue type:** Story
- **Story Points:** 5 — CRUD over two hierarchy levels plus ordering and activate/deactivate, on top of the existing `learning` entities, now spanning the bilingual parallel subject trees.
- **Labels:** `admin`, `curriculum`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-1

## Description
As an admin, I want to create, edit, reorder and activate/deactivate subjects and their units across the bilingual subject trees, so that I can curate the top of the curriculum hierarchy — per `(SubjectCode, Language)` — without a developer running seed scripts.

## Acceptance Criteria
- Given the admin dashboard, when an admin opens Curriculum → Subjects, then each subject root lists with its **`SubjectCode`** (MATH / SCIENCE / ARABIC / ENGLISH), its **`Language`** (ar / en), grade, sequence order, and active state — so the **6 roots per grade** (Math×2, Science×2, Arabic-ar, English-en) are all visible and distinguishable.
- Given a grade, when the admin opens its curriculum, then a **language-coverage view** shows which `(SubjectCode, Language)` trees exist for that grade and **flags any gaps** (e.g. an `en` Science tree missing while `ar` Science exists).
- Given a subject tree, when the admin creates/edits a unit under it, then the unit persists with title, description, and sequence order **scoped to that `(SubjectCode, Language)` tree** (units and ordering are not shared across the two language trees of the same code).
- Given a subject or unit, when the admin drags to reorder, then the new `SequenceOrder` is saved **within that language tree only** and the list reflects it on reload.
- Given a subject or unit, when the admin toggles active/inactive, then inactive items are hidden from the student curriculum but preserved (soft state, not deleted); activation state is scoped to the language tree.
- Given a Create/Update that would duplicate an existing `(GradeId, SubjectCode, Language)` tree, or introduce a 5th `SubjectCode`, then the API rejects it (the `(GradeId, SubjectCode, Language)` **unique key** is honored — no duplicate tree, no new subject code).
- Given a delete on a unit that still contains lessons, then the API rejects it with a clear "unit not empty" error.
- (Admin-only access; non-admin → 403/redirect.)

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P2-01 (curriculum hierarchy — `Subject`/`Unit`), P1-10 (admin shell), P1-05 (Admin policy).
- Admin-only per SRS §3; no teacher role. Curriculum is now **bilingual parallel trees**: `Subject` carries `SubjectCode` (one of the 4 fixed codes: MATH/SCIENCE/ARABIC/ENGLISH) + `Language` (ar/en), with a UNIQUE `(GradeId, SubjectCode, Language)` constraint and **6 Subject roots per grade** (Math×2, Science×2, Arabic-ar, English-en). Arabic/English subjects are pinned by subject; Math/Science follow the student's `LearningLanguage`. Admins curate each tree — they do **not** invent new subject codes beyond the 4, and the unique key prevents duplicate trees per grade.
