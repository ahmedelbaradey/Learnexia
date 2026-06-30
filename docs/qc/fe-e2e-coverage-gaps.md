# Frontend E2E Coverage — Gap Backlog

**Date:** 2026-06-24 · **Scope:** the Playwright E2E suite (`tests/e2e/`) over the **student-app (Expo web PWA)**, **admin-dashboard (Next.js)**, and **marketing-site**. Produced from a read-only route→spec→assertion audit of every navigable route vs the specs that exercise it.

**Headline:** coverage is **broadly healthy** — roughly **70–80% of routes have real, assertion-based specs** (auth, session/IDOR, learning dashboards, parent dashboard shell, admin users/analytics/audit/AI-safety/gamification/curriculum-CRUD + the knowledge-graph editor). The gaps cluster in a few concrete places below. This doc is the backlog for a future coverage wave — nothing here blocks the current build.

> **How to read "coverage type":** **REAL** = meaningful assertions (flows, validation, data, auth, error paths). **SHALLOW** = loads-page / screenshot-only / smoke. **NONE** = no spec. **BLOCKED** = a spec exists but is skipped behind a backend/seed/build blocker.

---

## P0 — highest impact (some are backend/seed blockers, not FE)

1. **Quiz correct-answer path is entirely blocked — `DEF-P205FE-01` (backend).**
   The backend grades submitted answers as wrong, so every "correct path" assertion is skipped across `P2-05/06/07-FE.spec.ts` + `carryover-d1.spec.ts`. Untestable until fixed: correct feedback, 100% score, auto-advance, XP-on-correct. **This single backend bug gates the core learning-flow E2E.** Fix it first — it unblocks the most coverage per unit of effort.

2. **Only MCQ questions are seeded.** TrueFalse / FillInBlank / Matching have stub UI but **zero E2E** on both student (`P2-06/07`) and admin (`P7-04` question authoring) because no seed data exists for them. Add seed fixtures for all four question types → unblocks both apps at once.

3. **Account recovery: `/forgot-password` + `/reset-password` — NONE.** Two critical auth routes with **zero E2E** (email submit, validation, token handling, error states). Cheap to add, high value (these are real user-recovery paths).

4. **Admin question CRUD (`P7-04`) — incomplete/truncated spec.** `P7-admin-curriculum-lessons.spec.ts` cuts off mid-questions: only partial MCQ create; FIB/short-answer + **edit/delete untested**. Recover/finish the spec.

---

## P1 — major gaps (screens exist, flow untested)

**Student-app**
- **Hearts / practice-mode / streak-freeze mechanics** — screens render (`P4-gamification-xp-streak-hearts`) but the *spend heart → practice mode* and *streak-freeze (heart cost)* flows are not asserted. (Confirm which of these mechanics actually ship before writing specs — some sub-features the audit listed may not exist yet.)
- **`/attempts`** — list + states covered (`carryover-d1`), but tap-to-detail and retry-from-history are untested.
- **Skill tree unlock rules** — tree renders 4 states (`P2-03-FE`), but the prerequisite-unlock *logic* (what unlocks what, XP cost) isn't verified, only that nodes/locked-reasons render.
- **Parent settings sub-flows** — `settings` shell + 4 panels render (`P5-05`), but plan/notifications/security panel *interactions* aren't E2E'd. Learning-language change IS covered, but via the dedicated `P8-learning-language` spec, not from the settings UI itself.

**Admin-dashboard**
- **Curriculum publish / unpublish / rollback (`P7-05`)** — `curriculum/preview/*` renders a read-only snapshot, but the publish mutations are skipped (`if (!hasPublish)` guards). The core curriculum-deployment workflow has no E2E.
- **Moderation review actions (`P7-09`)** — list/filter/detail are REAL, but approve/reject/flag run only against **mocked/intercepted** data (no live seed — moderation items require the AI-safety pipeline). Document as "mocked workflow" + plan a staging integration test, or add an offline seed path.
- **User-edit + subject-edit forms** — only the auth guard (deep-link redirect) and create/delete are tested; the actual `users/[id]/edit` and subject-edit forms aren't exercised.

---

## P2 — completeness / lower value

- **Parent `/activity` + `/energy`** — screenshot-only (`parent-final-capture`); no functional assertions on chart data / metrics.
- **Gamification detail depth** — badges/missions/league/events catalogs + counters render, but detail modals, completion flows, and unlock animations aren't asserted (mostly nav + render today).
- **Onboarding `/complete`** and **`/role-select` direct UI** — used as redirect targets; the screens' own content/CTAs aren't asserted.
- **Marketing site** — locale routing + RTL (`marketing-components-ar`) are REAL, but hero/feature/CTA-to-register flows aren't tested.

---

## Cross-cutting notes

- **Screenshot-only specs give NO regression protection.** `p12-screenshots`, `parent-final-capture`, `parent-lang-check`, `rtl-alignment-polish`, `rtl-reverify-fresh` capture images / check `dir`+`lang` only. They are visual-capture aids, not functional coverage — don't count them toward a route's coverage.
- **Admin RTL/Arabic is static-only.** Admin RTL cases are blocked by an `ADMIN_LOCALE='en'` build-time constant (`AUD-TC-19`, `GAM-TC-34`, `CUR-TC-91` skipped). Runtime ar/RTL on admin needs a separate `ar` build or a runtime locale switch (architecture change) to test.
- **Backend `Search` ignored on `ListSubjects` (`DEF-04`)** — curriculum subjects search filter is a no-op server-side; `CUR-TC-03` is skipped. Small backend fix unblocks it.

---

## The Curriculum Intelligence pipeline review UI — absent by design (not a test gap)

Confirmed (find + grep, both audits): there are **no routes, no `api-client` methods, and no specs** for the new pipeline's admin surfaces — **document upload**, the **`IngestionReviewItem`** review queue, and the **`KGSuggestion`** approve/reject queue. These backend APIs ship (BL-01/05/03) but have no frontend. The human-in-the-loop approval gate (Decision-E) therefore has no UI and cannot be E2E'd until the screens are built. This is the largest *functional* gap in operating the pipeline, but it is a **build-the-FE** item (needs stories → designer → frontend → e2e), not a coverage fix. Tracked in `docs/dev/HANDOFF.md`.

---

## Suggested order for a coverage wave

1. Fix `DEF-P205FE-01` (backend grading) — unblocks the most E2E.
2. Add multi-type question seed fixtures — unblocks 3 question types on both apps.
3. Add `/forgot-password` + `/reset-password` specs — cheap, high value.
4. Finish the truncated admin questions spec + add publish/unpublish/rollback.
5. Then work down P1 (hearts/streak-freeze, attempts detail, moderation seed, edit forms).

> Per CLAUDE.md, anything that becomes a build task should be written up as story/task files first (with lead sign-off) before implementation. This doc is the raw gap inventory feeding that.
