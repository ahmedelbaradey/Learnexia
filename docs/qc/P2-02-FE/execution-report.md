# P2-02-FE — Execution Report (EMPTY TEMPLATE)

> **Owner:** `frontend-e2e-tester` (fill after running). `qc-test-designer` scaffolds this file but never fills results.
> **Spec under test:** [`frontend-test-cases.md`](./frontend-test-cases.md) → implemented in `tests/e2e/specs/P2-02-FE.spec.ts`.
> **Run recipe:** HANDOFF "⚠️ Sandbox/WSL e2e run recipe" (Node 20, `EXPO_OFFLINE=1`, backend on `:5080` fresh-seeded, reuse the running Expo server — no `CI` env).

---

## 1. Run summary

| Field | Value |
|---|---|
| Date / runner | _TBD_ |
| Commit / branch | _TBD_ |
| Backend `:5080` build + seed | _TBD_ |
| Expo web `:8081` | _TBD_ |
| Playwright project(s) (chromium / mobile) | _TBD_ |
| Total cases | 28 |
| Pass | _TBD_ |
| Fail | _TBD_ |
| Blocked | _TBD_ (designed: FE-TC-06, 13, 16, 23, 28) |

---

## 2. Per-case results

| Case ID | Title | Priority | Result (PASS / FAIL / BLOCKED) | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Child lands on home + sees embedded subjects list | P0 | | |
| FE-TC-02 | Exactly 4 subject rows, no Social Studies | P0 | | |
| FE-TC-03 | Grade context — grade caption + grade-scoped subjects | P0 | | |
| FE-TC-04 | Tap subject → subject-detail Lessons tab | P0 | | |
| FE-TC-05 | Lessons grouped by unit, units in sequence order | P0 | | |
| FE-TC-06 | Detail header shows opened subject | P1 | | BLOCKED — header renders generic title, no testID (OQ5) |
| FE-TC-07 | Lessons within a unit in sequence order | P1 | | |
| FE-TC-08 | Subject with no lessons → empty state | P0 | | |
| FE-TC-09 | Empty unit → dashed "Coming soon" tile | P2 | | likely needs route stub |
| FE-TC-10 | Subjects list shimmer while loading | P1 | | |
| FE-TC-11 | Lessons tab shimmer while loading | P2 | | |
| FE-TC-12 | Subjects list error + retry recovers | P1 | | |
| FE-TC-13 | Unknown subject id → "Subject not found" + Back | P1 | | BLOCKED — no testID; backend 404 contract unconfirmed |
| FE-TC-14 | Lessons tab error + retry recovers | P1 | | |
| FE-TC-15 | Detail shell — back + SegmentedTabs (Lessons default) | P0 | | |
| FE-TC-16 | No raw keys inside Lessons tab | P1 | | BLOCKED — needs deterministic subject row testID (OQ1) |
| FE-TC-17 | Back from subject-detail → child home | P1 | | |
| FE-TC-18 | Subject rows meet kid-UX tap-target floor | P1 | | |
| FE-TC-19 | Social Studies never renders (defensive filter) | P0 | | |
| FE-TC-20 | Duplicate / unknown subjects deduped + dropped | P2 | | |
| FE-TC-21 | Canonical order Math→Science→Arabic→English | P2 | | |
| FE-TC-22 | Arabic child — RTL + Arabic content | P0 | | |
| FE-TC-23 | English child — LTR + English content | P1 | | BLOCKED — needs en-child seed (OQ3) |
| FE-TC-24 | Subject-row RTL mirroring (chevron + flex) | P2 | | |
| FE-TC-25 | Different grade → grade-scoped content | P2 | | may downgrade if 2nd grade unseedable |
| FE-TC-26 | English login UI, ar child → RTL from Me (cross-check) | P2 | | |
| FE-TC-27 | No raw i18n keys on the browse chain (ar + en) | P1 | | |
| FE-TC-28 | Signed-out deep-link to subject → Login | P1 | | BLOCKED — redundant w/ P1 route-guard pass |

---

## 3. Defects found

> One row per defect. Severity: Critical / High / Medium / Low. File browse-surface bugs back to `frontend`.

| ID | Case(s) | Severity | Description | Status |
|---|---|---|---|---|
| _none yet_ | | | | |

---

## 4. testIDs / seams requested back to `frontend`

> Populate from the BLOCKED cases + any new hooks the run needed.

- [ ] **OQ1** — per-`SubjectRow` `testID` (e.g. `subject-row-{id}` / `subject-row-{key}`) in `SubjectsListSection`.
- [ ] **OQ2** — `testID`s on `LessonCard` (`lesson-card-{id}`), unit header (`unit-{id}`), and state blocks (`subjects-empty`, `subjects-error`, `subject-not-found`, `empty-unit-{id}`).
- [ ] **OQ3** — an API-based seed helper in `tests/e2e/` (register parent + add child(grade, lang) → child creds) to cut runtime/flake and enable en-child + multi-grade cases.
- [ ] **OQ5** — render the real subject name in the subject-detail header + add `testID="subject-detail-header"`.

---

## 5. Notes / environment gotchas

- _TBD by the tester (seeder content used, route stubs needed, flake observed, Eastern-numeral handling, etc.)._
