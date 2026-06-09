# P2-02-FE — QC Test Plan + Coverage Report (Frontend / student-app web PWA)

> **Story:** [Browse subjects & lessons](../../../user-stories/Phase-2-Learning-Core/P2-02-browse-subjects-and-lessons.md) · **Surface:** child learning surface (student-app web PWA)
> **Design Spec:** [W11 Browse Subjects + Skill Tree](../../../design-system/ui_kits/student-mobile/W11-subjects-tree.md)
> **Tasks:** [P2-02-FE](../../../tasks/Frontend/student-app/Phase-2-Learning-Core/P2-02-FE.md)
> **Run owner of this catalog:** `qc-test-designer` (design only). **Implemented by** `frontend-e2e-tester` into `tests/e2e/specs/P2-02-FE.spec.ts`; results into [`execution-report.md`](./execution-report.md).
> **Scope:** FRONTEND web E2E only. No backend test-case file is produced for this run.

---

## 1. Summary

P2-02-FE is the **child browse surface**: a signed-in child sees their grade's 4 MVP subjects (Math / Science / Arabic / English — **no Social Studies**), taps a subject, and sees its units + lessons in sequence order, plus an empty state when a subject has no lessons.

**Important reality vs. the story/design-spec wording (read before implementing):** the W11 *standalone* "Subjects" screen described in the Design Spec §1 Surface 1 was **superseded by the W13 home dashboard** (P2-09-FE). On `main` today:

- The subjects list lives **inside the child home** (`apps/student-app/app/(child)/index.tsx`) as the embedded `SubjectsListSection` (`_components/SubjectsListSection.tsx`) under a **"Your subjects" eyebrow** — the W11 H1 "Subjects" was demoted. There is **no `/subjects` standalone route**; the browse entry point is the child home (`/(child)/`).
- Subject detail (units + lessons) is unchanged and lives at `(child)/subjects/[subjectId]/index.tsx` (Lessons tab) inside the `[subjectId]/_layout.tsx` shell (back chevron + `SegmentedTabs` Lessons | Skill Tree).
- The Skill-Tree tab (`tree.tsx`) is **P2-03-FE**, out of scope for this catalog except where browse navigation touches it.

So "open Subject Selection" in AC1 = **land on the child home and see the embedded subjects list**.

**Surfaces under test (this run):**

| # | Surface | Route / file |
|---|---|---|
| S1 | Child home → embedded subjects list (4 subjects + states) | `(child)/index.tsx` → `_components/SubjectsListSection.tsx` |
| S2 | Subject-detail shell (header, back, SegmentedTabs) | `(child)/subjects/[subjectId]/_layout.tsx` |
| S3 | Lessons tab (units + lessons in order, empty/error/404, empty-unit tile) | `(child)/subjects/[subjectId]/index.tsx` |
| — | `SubjectRow` / `LessonCard` primitives (rendered by S1/S3) | `packages/ui/src/components/{SubjectRow,LessonCard}` |

**Case counts (all frontend, target agent `frontend-e2e-tester`):**

| Bucket | Count |
|---|---|
| **Total designed** | **28** |
| P0 | 11 |
| P1 | 11 |
| P2 | 6 |
| BLOCKED (testID / harness gap) | 5 (FE-TC-06, 13, 16, 23, 28) — see §4 |

By type: functional 9 · state (loading/empty/error/404) 6 · RTL-i18n 5 · product-override 3 · a11y/kid-UX 3 · regression/negative 2.

**Precondition reality (load-bearing — see §5):** there is no API seeding helper in `tests/e2e/`. The established pattern (`P1-09-FE.spec.ts`) seeds a child **through the UI**: register a parent → add a child with a grade + learning language via `AddChildForm` → sign out → sign in as the child. The browse surface needs a **child** session AND **seeded curriculum** (the Development `dotnet run` recipe migrates + seeds a fresh DB on first boot — see HANDOFF "⚠️ Sandbox/WSL e2e run recipe"). The grade picked at add-child time drives which subjects/lessons the child sees.

---

## 2. Coverage matrix — every acceptance criterion → case(s)

| Acceptance criterion (story) | Covered by | Verdict |
|---|---|---|
| AC1 — Signed in, open Subject Selection → see 4 MVP subjects for my grade (Math/Science/Arabic/English) | FE-TC-01, FE-TC-02, FE-TC-03, FE-TC-19 (product override) | ✅ Covered |
| AC2 — Selecting a subject shows its units + lessons in **sequence order** | FE-TC-04, FE-TC-05, FE-TC-07 | ✅ Covered |
| AC3 — Query endpoints return subjects/lessons filtered by the **student's grade** | FE-TC-03 (grade caption + grade-driven content), FE-TC-25 (different grade → different content, P2) | ✅ Covered (FE asserts the grade-scoped content the child actually sees; the server-side filter itself is backend QC) |
| AC4 — Subject with **no lessons for my grade** → appropriate empty state | FE-TC-08 (subject empty), FE-TC-09 (empty unit tile) | ✅ Covered |
| Product decision — **4 subjects, no Social Studies** | FE-TC-19, FE-TC-20 (defensive filter), FE-TC-21 (canonical order) | ✅ Covered |
| Learning-language filter — child sees **only own-language** content (Phase-2 backend: wrong-language access silently redirects, no 403) | FE-TC-22 (ar child content), FE-TC-23 (en child content — BLOCKED on seeding a 2nd child reliably) | ⚠️ Partial — FE can only assert "the content shown matches the child's language"; the silent-redirect itself is backend-observable, not FE-observable. Logged as assumption A3. |

**Non-AC but in-scope (design spec + NFR):**

| Concern | Covered by |
|---|---|
| Loading shimmer states (subjects + lessons) | FE-TC-10, FE-TC-11 |
| Error + retry (subjects list, lessons tab) | FE-TC-12, FE-TC-14 |
| 404 unknown subject id → "Subject not found" + Back | FE-TC-13 (BLOCKED — see §4) |
| Subject-detail header + back navigation | FE-TC-06 (BLOCKED on header testID), FE-TC-15 |
| SegmentedTabs Lessons \| Skill Tree present + default = Lessons | FE-TC-15 |
| RTL ar (default) vs LTR en layout | FE-TC-22, FE-TC-24, FE-TC-26 |
| i18n integrity — no raw keys on the browse chain (ar + en) | FE-TC-27 |
| Kid-UX NFR-6 — tap targets ≥ 44–48px, no scary error chrome | FE-TC-17, FE-TC-18 |
| Auth gate — signed-out cannot reach `/subjects/{id}` (auth-required lessons endpoint) | FE-TC-28 (BLOCKED — covered by P1-09 route-guard pass, cross-ref) |

**Coverage verdict:** every story acceptance criterion has at least one P0/P1 case. The only partial is the learning-language **silent-redirect** (AC-adjacent, backend behavior) — the FE can only positively assert the child sees their own-language content; it cannot observe the redirect. Documented as assumption A3 + open question OQ4.

---

## 3. Risk notes (where cases are weighted)

1. **Surface drift (highest design risk).** The story + Design Spec describe a standalone "Subjects" screen; the shipped surface is the **embedded** `SubjectsListSection` on the home dashboard with the H1 demoted to a "Your subjects" eyebrow. A naive implementer following the spec verbatim would look for a `/subjects` route that doesn't exist. P0 cases (FE-TC-01/02) explicitly anchor on the child-home entry + the section, not a standalone route. **This is the #1 thing the implementer must internalize.**
2. **Selector fragility — few testIDs on the browse path.** `SubjectsListSection` exposes `testID="subjects-list-section"` on the wrapper only; individual `SubjectRow`s, `LessonCard`s, unit headers, state blocks, the detail header, and `SegmentedTabs` carry **no testID** — rows are addressable only by `aria-label` (= subject name) / role. Arabic is the default locale, so copy-based selectors are brittle. Several cases lean on `getByRole('button', { name: <subject-name-regex ar|en> })` as the least-bad fallback; the cleaner fix is testIDs (OQ1). 5 cases are BLOCKED on missing hooks.
3. **No mastery percent wired.** `SubjectsListSection` does **not** pass `masteryPercent` to `SubjectRow`, so the mastery caption + bar are never rendered today. Design Spec §3.1 treats mastery as optional (`omit to hide`). Cases assert subject **name + tap target**, not a mastery value — and FE-TC-21 explicitly notes mastery is absent (not a defect; logged as OQ2 / assumption A2).
4. **Seeding cost + flakiness.** Every browse test needs a full register→add-child→sign-in chain (multiple real API round-trips, ~120s timeout per the P1-09 pattern). Curriculum must be seeded server-side. This makes the suite slow and sensitive to seeder content drift (e.g. whether grade 1 has empty units). Cases that need *specific* states (empty subject, 404, locked lesson) are weighted lower priority / marked BLOCKED where the seeded data can't be guaranteed.
5. **Lesson state variety is seeder-dependent.** Locked/Available/Completed `LessonCard` chrome + the WhyLockedSheet are P2-03/P2-05 territory and depend on attempt history a fresh child doesn't have. Browse-scope cases assert lessons **render in order** and are **pressable**, not their lock state. WhyLockedSheet behavior is explicitly out of this catalog.

---

## 4. BLOCKED cases — what unblocks them

| Case | Blocker | Unblock |
|---|---|---|
| FE-TC-06 — assert subject name in the detail header | Header title currently renders the i18n key `child.subjects.title` ("Subjects"/"المواد"), **not the real subject name** (`_layout.tsx` line 119), and has **no testID**. Cannot assert which subject you're on. | (a) Add `testID="subject-detail-header"` + render the real subject name; OR (b) accept the generic title and downgrade to "header is present" (FE-TC-15 covers presence). |
| FE-TC-13 — 404 "Subject not found" on unknown subject id | Needs to hit `/subjects/999999` and observe the 404 branch. No testID on the 404 block (only the i18n string). Also the lessons endpoint may return empty-200 rather than 404 for an unknown-but-valid-shape id depending on backend. | Add `testID="subject-not-found"` to the 404 block; confirm backend returns 404 (not empty 200) for unknown subject id. |
| FE-TC-16 — assert no raw key inside the **lessons tab** specifically | Reaching the lessons tab needs a subject id with seeded lessons; the i18n sweep is reliable but the *navigation* into it depends on a deterministic subject. | Add `testID` to a subject row (OQ1) so the test can deterministically open one; or expose seeded subject ids. |
| FE-TC-23 — English-child own-language content | Needs a **second** child seeded with `learningLanguage: 'en'` under the same parent, then sign-in as that child. The UI add-child chain is heavy and the parent-onboarding "add second child" path is not exercised by existing helpers. | Provide an API seed helper (OQ3) OR confirm the add-2nd-child UI path is stable; then unblock. |
| FE-TC-28 — signed-out cannot reach `/subjects/{id}` | Route-group guards already covered + fixed in the P1 frontend QC pass (`useGroupGuard` on `(child)` layout). Re-testing here is redundant and the guard redirect is global, not browse-specific. | Cross-reference P1-09-FE route-guard cases; or keep as a thin smoke assertion (deep-link `/subjects/1` while signed-out → bounced to `/login`). |

None are dropped — each is listed in `frontend-test-cases.md` with its blocker + the proposed unblock.

---

## 5. Open questions / assumptions (lead must resolve before/with implementation)

**Open questions (need a frontend or lead decision):**

- **OQ1 — Per-`SubjectRow` testID.** `SubjectsListSection` does not pass a `testID` to each `SubjectRow` (the prop exists on the component). Without it, rows are addressable only by `aria-label` = raw subject name (locale-dependent). **Ask:** add `testID={`subject-row-${dto.id}`}` (and/or a `subjectKey`-based testID) so e2e can deterministically tap "the Math row" regardless of locale. Highest-value hook for this story.
- **OQ2 — Per-`LessonCard` + unit-header + state-block testIDs.** Lessons-tab cards, unit eyebrow/name, and the empty/error/404/empty-unit blocks have no testIDs. **Ask:** add `testID="unit-{unitId}"`, `testID="lesson-card-{lessonId}"`, `testID="subjects-empty"`, `testID="subjects-error"`, `testID="subject-not-found"`, `testID="empty-unit-{unitId}"`. Needed for sequence-order + state assertions without copy selectors.
- **OQ3 — A test seed seam.** Every browse test pays the full UI register→add-child→sign-in cost. **Ask:** is there appetite for a small API-based seed helper in `tests/e2e/` (register parent + add child + return child creds) to cut runtime and flake? (Mirrors what `api-tester` does on the backend.)
- **OQ4 — Learning-language silent-redirect is FE-unobservable.** Per Phase-2 backend QC, wrong-language browse access silently redirects (no 403); the FE only ever shows own-language content. **Confirm:** the only FE-observable assertion is "the subjects/lessons shown are in the child's learning language" — there's no FE path to *trigger* a wrong-language request. Accepted as A3.
- **OQ5 — Subject-detail header should show the real subject name.** `_layout.tsx` renders `t('child.subjects.title')` ("Subjects") as the header title, not the subject's own name (the spec §1 Surface 2 calls for the subject name). Is the generic title intentional for W11/W13, or a gap? Blocks FE-TC-06.

**Assumptions baked into the cases:**

- **A1** — Curriculum is seeded server-side (fresh-DB-on-boot recipe). Grade 1 (the value the add-child helper picks) has ≥1 subject with ≥1 unit + lessons. If the seeder leaves grade 1 sparse, FE-TC-04/05/07 may need a different grade — flagged in each case.
- **A2** — Mastery percent is intentionally not wired into `SubjectRow` on this surface (no `masteryPercent` passed). Cases assert name + tap, not a percentage. Not a defect.
- **A3** — FE can only positively assert own-language content (OQ4).
- **A4** — Arabic is the default locale (`html[dir=rtl][lang=ar]` on fresh context); child locale then comes from `Me.preferredLanguage` (the `ar-EG`/`en-US`→2-letter normalization was fixed in the P1 frontend QC pass). LTR cases either switch the login UI to English first or seed an English-learning child.
- **A5** — The browse entry is the **child home** (`/(child)/`), not a `/subjects` route. AC1 "open Subject Selection" = land on home + see the embedded list.

---

## 6. Handoff

- **`frontend-e2e-tester`** implements [`frontend-test-cases.md`](./frontend-test-cases.md) → `tests/e2e/specs/P2-02-FE.spec.ts`, reusing the `registerParent` / `addChildViaForm` / `signInViaUI` / `signOutAndWait` helper pattern from `tests/e2e/specs/P1-09-FE.spec.ts`. Run per the HANDOFF "⚠️ Sandbox/WSL e2e run recipe" (Node 20, `EXPO_OFFLINE=1`, backend on `:5080`, reuse the running Expo server). Where a case is BLOCKED on a missing testID, **file the needed hook back to `frontend`** (do not reach into CSS) and mark the case BLOCKED in the execution report.
- **No backend test-case file** is produced for this run (frontend-only scope).
- After the run, fill [`execution-report.md`](./execution-report.md) (pass/fail per case + defects + the testIDs requested). `qc-test-designer` never fills results.

**Test cases ready** — `frontend-e2e-tester` to implement `frontend-test-cases.md` and write results into `execution-report.md`. (No `backend-test-cases.md` for this run.)
