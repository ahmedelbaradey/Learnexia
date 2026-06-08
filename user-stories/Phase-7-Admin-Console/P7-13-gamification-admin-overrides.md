# Gamification admin overrides

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — Gamification Management
- **Issue type:** Story
- **Story Points:** 8 — an admin write surface over the shipped gamification engine spanning four override areas (league tiers, catalog CRUD, timed-event writes, streak-freeze grants), each AdminOnly and audited, layered on top of read-only endpoints.
- **Labels:** `admin`, `gamification`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-8

## Description
As an admin, I want write controls over the gamification engine — league tiers, the badge & mission catalogs, timed events, and streak freezes — so that I can correct mistakes, run promotions, and resolve support cases without editing seed data or the database by hand.

## Acceptance Criteria
- Given a student's current weekly league standing, when I **override their league tier** and confirm, then the new tier is persisted, applied to the live standings, and the action is audited.
- Given the **badge catalog**, when I create/edit/deactivate a **`BadgeDefinition`** (name, description, criteria, icon, active state), then it persists and is reflected in the catalog students earn against; existing earned badges are not retroactively removed by a deactivation.
- Given the **mission catalog**, when I create/edit/deactivate a **`MissionDefinition`** (type — daily / weekly / weekly-challenge, target, reward, active state), then it persists and is reflected in the missions generated for students.
- Given **timed events**, when I create/update/end an event via the new write endpoints, then it persists and drives the engine — complementing the existing read-only `GET api/admin/timed-events` (which was read-only before this story).
- Given a student, when I **grant a streak-freeze**, then their available streak-freeze count increases (or one is applied), the change is reflected in the engine, and the grant is audited with actor + reason.
- Every override/edit/grant records **actor, timestamp, target, old → new values, and reason** and is **audited** (P7-12); before/after snapshots avoid leaking child PII.
- All endpoints are **AdminOnly**; non-admin → 403/redirect.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P7-12 (audit log), and the shipped **Phase 3 gamification** engine (XP/levels, streaks + streak-freeze, hearts/practice, badge catalog, daily/weekly missions + weekly challenges, weekly leagues + tiers, timed events).
- Phase 3 gamification shipped with all current endpoints **read-only**; admin write/overrides were deferred to P7. This story is that write surface, living in the **Gamification** module. Cross-module effects (e.g. targeting a child) go through `Shared.Contracts` integration seams, never direct cross-module FK/writes. No teacher role.
- **Seed-vs-admin-edit precedence (design consideration):** `BadgeSeeder` / `MissionSeeder` are **idempotent startup seeders**. Admin edits to `BadgeDefinition` / `MissionDefinition` must not be silently overwritten on the next boot — the seeders should seed-if-absent (and not clobber admin-modified rows). Settle the precedence rule (seed creates, admin owns thereafter) before implementing; **ask the lead before introducing any new pattern** to reconcile seed vs admin authorship.
