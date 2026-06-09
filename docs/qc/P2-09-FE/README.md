# QC Test Plan + Coverage Report — P2-09-FE (Home Dashboard, student-app web PWA, child surface)

> On-demand `qc-test-designer` pass. **Frontend (student-app web E2E) only** — no backend HTTP surface in scope (the `GET /api/Learning/Dashboard` contract is covered by the Phase-2 backend QC pass). Design-only: this folder defines cases; `frontend-e2e-tester` implements `frontend-test-cases.md` into `tests/e2e/specs/P2-09-FE.spec.ts` and fills `execution-report.md`.

## 1. Summary

- **Story:** [P2-09](../../../user-stories/Phase-2-Learning-Core/P2-09-home-dashboard.md) — "See the home dashboard." The child's home/landing screen after sign-in.
- **Surface under test:** `apps/student-app/app/(child)/index.tsx` (the child home route) + its widgets — `DashboardHeader`, `ContinueCard` (both `@learnexia/ui`), `LeaguePreviewRow` + `SubjectsListSection` (inline `_components`).
- **Data source:** `useDashboard()` → `GET /api/Learning/Dashboard` returning `DashboardDto`, plus `useMe()` (greeting + grade) and `useSubjectsForGrade()` (the 4-subject section).
- **Batch scope:** the whole story (header + continue + states + RTL/i18n + the Phase-4 widgets that have since been wired live).

**Counts**

| | Count |
|---|---|
| **Total FE cases** | **29** |
| By surface | Frontend (student-app web) — 29 |
| P0 | 12 |
| P1 | 12 |
| P2 | 5 |
| BLOCKED (not testable in headless web E2E now) | 4 (FE-TC-26, 27, 28, 29) |

No `backend-test-cases.md` — frontend-only run by design.

## 2. Critical context the testers MUST honour (reconciled against live code)

The dashboard captures and the **original** W13 Design Spec are **behind the merged code**. The Phase-4 gamification waves (P4-02/03/04/07) flipped several "Phase-2 stub" fields to **live** data. Assert against the **live `DashboardDto`**, not the W13 spec table:

- **`DashboardDto` has 13 fields** (`packages/api-client/src/generated/nswag-client.ts:6985`): `xp`, `streak`, `leaguePreview`, `continue`, `level`, `hearts`, `inPracticeMode`, `badgesCount`, `recentBadges`, `dailyMissions`, `weeklyMission`, `freezeBalance`, `activeTimedEvents`.
- The **screen** consumes: `continue`, `hearts` (default **5**, not the spec's 3 — see `index.tsx:266`), `streak`, `xp`, `level`, `inPracticeMode`, `leaguePreview`. `weeklyXpTarget` is still the hard-coded `100` stub. `dailyMissions`/`weeklyMission` are NOT rendered (MissionBanner never mounts).
- **Continue target is REAL in Phase 2** — BE resolves the most-recent-attempt subject → first Available lesson, with a Grade-1 Math fallback for net-new children (`useDashboard.ts:7-10`). So a *brand-new* child still gets a non-null `continue` (Grade-1 Math) — the "no continue card" empty branch only fires when BE returns `continue === null`, which is **rare** in practice. Tester must treat a present ContinueCard as the expected default and the null branch as a defensive/degenerate path.
- **Subjects:** the dashboard captures show mock "Reading"/"Art" tiles — **MOCK**. The live product has exactly **4 subjects: Math / Science / Arabic / English** (`_components/subjects.ts` `ALLOWED_KEYS`). Social Studies and any unknown subject are silently dropped by `filterSubjects()`. Assert only the 4 product subjects; assert **no** Reading/Art ever appears.
- **Arabic is the default locale** (`html[dir=rtl]`, `lang=ar`). Prefer `getByTestId`; never assert on Arabic copy as a primary selector.
- **Auth/seed path:** there is **no API-level seeding helper** in the harness. Every child is created the long way — register parent → add-child → sign out → sign in as child (see the helpers reused verbatim in `tests/e2e/specs/P1-09-FE.spec.ts`). A freshly-created child is the only "fresh child" the harness can produce; a "progressed child" (non-fallback continue, XP>0, streak>0) is **not seedable through the UI** today → those assertions are BLOCKED (see §4 + the BLOCKED cases).

## 3. Coverage matrix — every acceptance criterion → case IDs

The story has 4 acceptance criteria (`P2-09-home-dashboard.md` lines 15-19). The implementation tracks finer-grained AC1–AC13 (referenced inline in `index.tsx`); both are mapped.

| # | Acceptance criterion (story) | Case IDs | Verdict |
|---|---|---|---|
| AC-S1 | Dashboard shows XP, streak, daily mission, a "continue" entry point, and a league preview | FE-TC-01, 02, 03, 04, 05, 06, 19, 20 | Covered |
| AC-S2 | Tapping "continue" opens the next unlocked lesson from the learning path | FE-TC-07, 08, 09 | Covered (resume-correctness partly BLOCKED → FE-TC-09) |
| AC-S3 | Phase-4-dependent widgets (mission, league) render gracefully as placeholders until gamification is live | FE-TC-10, 11, 12 | Covered |
| AC-S4 | Renders in Arabic (RTL) and English | FE-TC-13, 14, 15, 16, 17 | Covered |

**Implementation-level AC1–AC13 mapping (from `index.tsx`):**

| Impl AC | Meaning | Case IDs |
|---|---|---|
| AC1 / AC9 | Header renders; loading skeleton | FE-TC-01, 02, 18 |
| AC2 / AC3 | ContinueCard conditional + navigation | FE-TC-06, 07, 08, 09 |
| AC4 / AC13 | SubjectsListSection — 4 subjects, no Social Studies, routing | FE-TC-21, 22, 23, 24 |
| AC5 | ContinueCard Boss / available-vs-completed chrome | FE-TC-25 (BLOCKED-partial), FE-TC-06 |
| AC6 | MissionBanner never rendered in Phase 2 | FE-TC-10 |
| AC7 | LeaguePreview hidden when null | FE-TC-11, 12 |
| AC8 | Greeting = first name from `useMe` | FE-TC-04 |
| AC10 | Dashboard error strip + Retry | FE-TC-19, 20 |
| AC11 | Stats a11y label | FE-TC-17 |
| AC12 | Sign-out preserved (TopBar) | FE-TC-15 |
| Fresh/zero-state | Level 1, no XP/streak, fallback continue | FE-TC-03, FE-TC-26 (BLOCKED for progressed) |

**Gap check:** no acceptance criterion is left without a case. The only partial gaps are *resume-correctness against a known-progressed child* (FE-TC-09 asserts the navigation **target shape**, not that it is the "right" lesson for a progressed child — BLOCKED for the deeper assertion) and the *available-vs-completed / Boss chrome* (FE-TC-25 — not deterministically reproducible via the UI seed path). Both are flagged BLOCKED with the seed reason, not dropped.

## 4. Risk notes (where cases are weighted, and why)

1. **Continue navigation (AC-S2) is the load-bearing user value** — "jump straight back into learning." Weighted P0 (FE-TC-07, 08). The handler (`index.tsx:196`) guards on `lessonId && subjectId` and pushes `/(child)/lessons/{lessonId}?subjectId={subjectId}`. Risk: a malformed/partial `ContinueTargetDto` (missing `subjectId`) silently no-ops the tap — FE-TC-08 asserts the URL actually changes.
2. **Fresh-child zero-state must not break the screen** — level 1, XP 0, streak 0, hearts 5, fallback Grade-1-Math continue. A `0/100` XPBar, `0`-day StreakFlame, and a present-but-fallback ContinueCard must all render without `undefined`/`[object Object]` (FE-TC-03, FE-TC-19-adjacent kid-UX). This is the realistic state of every freshly-seeded child in the harness.
3. **The 4-subject product constraint** is a recurring regression magnet (mock captures show Reading/Art). Dedicated negative case FE-TC-23 asserts no 5th subject and no Social Studies; FE-TC-22 asserts exactly the 4 expected.
4. **RTL is the default, not the exception.** Arabic-first means the *English* path is the one more likely to regress. FE-TC-13/14 assert both `html[dir]` values around the dashboard; FE-TC-16 asserts no raw i18n keys leak in either locale.
5. **Error/loading composition is scoped** — the dashboard error strip must render between header and continue while SubjectsListSection beneath still renders (AC10). A naive implementation blanks the whole screen on `dashboardQuery.isError`. FE-TC-19/20 assert the scoped behaviour via route-interception.
6. **Widget-graceful-degradation** (mission/league null) is explicitly in scope per AC-S3 — FE-TC-10/11 assert *absence* (no MissionBanner mount, no league row, no "Coming soon" placeholder) rather than presence.

## 5. Open questions / assumptions (lead must resolve before/with implementation)

1. **Missing widget testIDs (selector blockers).** Only `dashboard-header`, `continue-card`, `subjects-list-section`, and `sign-out-button` carry stable `testID`s today. The following have **no testID** and the tester will need them (or must fall back to role/aria-label, which is brittle in Arabic-default):
   - **No testID** on the dashboard **error strip** (it uses `accessibilityRole="alert"` only) — needed for FE-TC-19/20. Request `testID="dashboard-error"` + `testID="dashboard-error-retry"` on the retry button.
   - **No testID** on the **LeaguePreviewRow** — needed for FE-TC-11/12. Request `testID="league-preview"`.
   - **No testID** on individual **SubjectRow**s nor a stable per-subject hook — needed for FE-TC-22/23/24. Request `testID="subject-row-{key}"` (math/science/arabic/english) or at least a `subjects-list-section`-scoped count hook.
   - **No testID** on the **welcome empty-state tile** (the `continue===null && subjects empty` branch) — needed for FE-TC-12-adjacent empty path. Request `testID="dashboard-empty"`.
   - **No testID on the lesson player screen** (`(child)/lessons/[lessonId].tsx`) — FE-TC-07/08 currently assert the **URL** (`/lessons/{id}?subjectId=`) as the landing signal. A `testID="lesson-screen"` (or reuse a W12 lesson-intro testID) would make the resume assertion robust. **Lead decision requested.**
2. **Seed: fresh vs progressed child.** The harness has **no API seeding seam** — only the UI register→add-child→login path, which produces a *fresh* child (BE fallback = Grade-1 Math continue, XP 0, streak 0, level 1). A **progressed child** (real continue target ≠ fallback, XP>0, streak>0, league populated, a Completed/Boss continue node) is **not producible through the student-app UI** in one e2e run (it requires completing lessons / Phase-4 gamification accrual). **Decision needed:** do we (a) add a backend seed/fixture endpoint for a progressed child, (b) drive a full lesson completion in-flow (slow, flaky), or (c) accept the progressed-child cases as BLOCKED for this pass? Default assumption taken here: **(c)** — FE-TC-09 (resume-correctness for progressed), FE-TC-25 (Boss/Completed chrome), FE-TC-26 (widgets reflect real data after progress) are marked BLOCKED with this reason.
3. **`hearts` default is 5, not 3.** The W13 Design Spec pins `hearts=3`; the merged code (`index.tsx:266`, post-P4-04) defaults to **5**. Cases assert presence/non-crash of the Hearts widget, not a specific count, to avoid coupling to a stale spec value. Confirm 5 is the intended fresh-child cap.
4. **Practice Mode pill** (`inPracticeMode`) is a P4-04 addition not in the story AC. For a fresh child it is `false` (hidden). Covered as an absence assertion only (FE-TC-10-adjacent); no positive case (not seedable). Confirm this is acceptable scope.
5. **MissionBanner** is built but never mounted; `dailyMissions` IS now populated by BE in some environments (P4-06). The screen still does not render a mission banner (the wire-up is deferred). FE-TC-10 asserts no mission banner UI regardless of `dailyMissions` content. Confirm the dashboard intentionally still suppresses missions in this story's scope.

## 6. Handoff

- **`frontend-test-cases.md`** → **`frontend-e2e-tester`**: implement each FE-TC-* 1:1 into `tests/e2e/specs/P2-09-FE.spec.ts` (reuse the parent-register / add-child / sign-in helpers from `P1-09-FE.spec.ts`). Run against the live stack per `tests/e2e/README.md` + the HANDOFF "Sandbox/WSL e2e run recipe." Report any missing testID from §5 back to `frontend` rather than reaching into CSS.
- **`execution-report.md`** → filled by `frontend-e2e-tester` **after** the run: pass/fail per FE-TC, defects, and confirmation of which BLOCKED cases stayed blocked. The template is scaffolded (empty results) in this folder; `qc-test-designer` never fills results.
- No `api-tester` involvement — no backend surface in this run.

**Test cases ready — `frontend-e2e-tester` to implement `frontend-test-cases.md`; results into `execution-report.md`.** (No `backend-test-cases.md` — frontend-only run.)
