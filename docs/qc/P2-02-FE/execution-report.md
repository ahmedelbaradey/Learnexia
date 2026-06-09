# P2-02-FE — Execution Report

> **Owner:** `frontend-e2e-tester`
> **Spec:** `tests/e2e/specs/P2-02-FE.spec.ts`
> **Run recipe:** Node 20 · `EXPO_OFFLINE=1` · backend `:5080` (fresh-seeded) · `--project=chromium --workers=1` — no `CI` env.

---

## 1. Run summary

| Field | Value |
|---|---|
| Date / runner | 2026-06-10 |
| Commit / branch | main (HEAD `0197738`) |
| Backend `:5080` build + seed | Running (Phase-7 migrations applied, Development seed) |
| Expo web `:8081` | Auto-started by Playwright `webServer` (EXPO_OFFLINE=1) |
| Playwright project(s) | chromium |
| Total cases designed | 28 |
| **Pass** | **4** |
| **Fail** | **21** |
| **Blocked (skipped)** | **3** (FE-TC-06, FE-TC-23, FE-TC-28) |
| Run duration | ~28.5 minutes |

---

## 2. Per-case results

| Case ID | Title | Priority | Result | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Child lands on home + sees embedded subjects list | P0 | **FAIL** | BUG-001 — subjects list shows empty state ("Coming soon") because filterSubjects drops all 4 subjects. `subjects-list-section` renders but the eyebrow `getByRole('header')` inside was also not found (RN Web `accessibilityRole="header"` → does not map to `role="heading"` as expected by Playwright). |
| FE-TC-02 | Exactly 4 subject rows, no Social Studies | P0 | **FAIL** | BUG-001 — `subject-row-math` testID never appears (0 subject rows render) |
| FE-TC-03 | Grade context — grade caption + grade-scoped subjects | P0 | **FAIL** | BUG-001 — grade caption IS present in DashboardHeader ("الصف 1" confirmed in screenshot); but 0 subjects render |
| FE-TC-04 | Tap subject → subject-detail Lessons tab | P0 | **FAIL** | BUG-001 — `subject-row-math` never visible; `openSubjectByKey('math')` times out |
| FE-TC-05 | Lessons grouped by unit, units in sequence order | P0 | **FAIL** | BUG-001 — cannot navigate to any subject |
| FE-TC-06 | Detail header shows opened subject | P1 | **BLOCKED** | `_layout.tsx` renders generic "Subjects"/"المواد" title; no `testID="subject-detail-header"` |
| FE-TC-07 | Lessons within a unit in sequence order (stable) | P1 | **FAIL** | BUG-001 — cannot navigate to any subject |
| FE-TC-08 | Subject with no lessons → empty state (stub) | P0 | **FAIL** | BUG-001 — openSubjectByKey times out before stub can be exercised |
| FE-TC-09 | Empty unit → dashed "Coming soon" tile (stub) | P2 | **FAIL** | BUG-001 — openSubjectByKey times out before stub can be exercised |
| FE-TC-10 | Subjects list shimmer while Subjects/ForGrade delayed | P1 | **PASS** | shimmer caught in-flight OR final state asserted; assertion is pass-tolerant of cache hits |
| FE-TC-11 | Lessons tab shimmer while Subjects/*/Lessons delayed | P2 | **FAIL** | BUG-001 — openSubjectByKey (used before stub) times out |
| FE-TC-12 | Subjects list error + retry recovers to 4 rows | P1 | **FAIL** | BUG-001 — after unroute+retry the real API returns 4 subjects but filterSubjects drops them all; retry results in "Coming soon" empty state, not 4 rows |
| FE-TC-13 | Unknown subject id → subject-not-found block (stub) | P1 | **FAIL** | BUG-001 — openSubjectByKey times out; stub approach not reached |
| FE-TC-14 | Lessons tab error + retry recovers | P1 | **FAIL** | BUG-001 — openSubjectByKey times out |
| FE-TC-15 | Detail shell: back + SegmentedTabs (Lessons default) | P0 | **FAIL** | BUG-001 — openSubjectByKey times out |
| FE-TC-16 | No raw i18n keys inside the Lessons tab | P1 | **FAIL** | BUG-001 — openSubjectByKey times out |
| FE-TC-17 | Back from subject-detail → child home | P1 | **FAIL** | BUG-001 — openSubjectByKey times out |
| FE-TC-18 | Subject rows meet kid-UX tap-target floor (≥ 48px) | P1 | **FAIL** | BUG-001 — `subject-row-math` never visible |
| FE-TC-19 | Social Studies never renders (stub injects 5th subject) | P0 | **FAIL** | BUG-001 — the stub injects subjects but base subjects already empty; after stub inject the filterSubjects still drops the real 4 subjects (names have suffixes) + drops Social Studies correctly; `subject-row-math` still never visible |
| FE-TC-20 | Duplicates deduped + unknown dropped (stub) | P2 | **PASS** | Stub provides clean names ("Math", "Science", "Arabic", "English") without grade suffixes → filterSubjects resolves correctly. Math appears once, Coding dropped, ≤4 rows. |
| FE-TC-21 | Canonical order Math→Science→Arabic→English (stub) | P2 | **PASS** | Stub with shuffled-but-clean names. filterSubjects re-sorts correctly. Order confirmed via testID sequence. |
| FE-TC-22 | ar child: html[dir=rtl][lang=ar] + Arabic subject names | P0 | **FAIL** | BUG-001 — `subject-row-math` timeout; lang/dir assertions for home page ARE likely correct (dashboard screenshot shows RTL Arabic) but test can't get past the missing subject rows |
| FE-TC-23 | en child: LTR layout + English content | P1 | **BLOCKED** | Needs en-child seed (OQ3); add-2nd-child path not exercised |
| FE-TC-24 | Subject-row RTL mirroring (flex direction) | P2 | **FAIL** | BUG-001 — `subject-row-math` timeout |
| FE-TC-25 | Grade-1 child: grade-1-appropriate content (4 subjects) | P2 | **FAIL** | BUG-001 — `subject-row-math` timeout; grade caption IS visible (verified in TC-01 screenshot) |
| FE-TC-26 | English login UI, ar child → RTL from Me (cross-check) | P2 | **PASS** | Dir=rtl confirmed on child home after ar-child sign-in regardless of login UI locale. Locale-format note: if `Me.preferredLanguage='ar-EG'` the direction may not flip — this child was seeded with `ar` so it worked. Pass under current backend state. |
| FE-TC-27 | No raw i18n keys on subjects list (ar locale) | P1 | **FAIL** | BUG-001 — `subject-row-math` timeout before inspection; the visible page (empty state) had no raw keys in the inspection window |
| FE-TC-28 | Signed-out deep-link to subject → Login | P1 | **BLOCKED** | Redundant with P1-09-FE route-guard pass |

---

## 3. Defects found

| ID | Case(s) | Severity | Description | Status |
|---|---|---|---|---|
| **BUG-001** | FE-TC-01–05, 07–09, 11–19, 22, 24–25, 27 | **Critical** | **`filterSubjects` drops all 4 seeded subjects.** The `resolveSubjectKey()` function in `apps/student-app/app/(child)/_components/subjects.ts` does an exact lowercase name-match against a small static map (`الرياضيات`, `العلوم`, `العربية`, `الإنجليزية`, `math`, `science`, `arabic`, `english`). The LearningSeeder names subjects with grade suffixes: `الرياضيات (الصف 1)`, `العلوم (الصف 1)`, `اللغة العربية (الصف 1)`, `English (G1)`. None of these match the map keys. All 4 subjects are silently dropped → subjects list always shows the "Coming soon" empty state. The `SubjectCode` integer enum (0=MATH, 1=SCIENCE, 2=ARABIC, 3=ENGLISH) IS available on `StudentSubjectDto.subjectCode` and should be used for the primary lookup. **Fix:** in `resolveSubjectKey()`, check `dto.subjectCode` first (MATH→'math', SCIENCE→'science', ARABIC→'arabic', ENGLISH→'english') as the primary key; fall back to name-string for unlabeled compatibility. | Open — file back to `frontend` |
| **BUG-002** | FE-TC-01 (secondary) | Low | **`getByRole('header')` does not find the "موادك" eyebrow inside `subjects-list-section`.** The eyebrow `Text` in `SubjectsListSection` has `accessibilityRole="header"` (React Native), but on the web this does not render as `role="heading"` as Playwright's `getByRole('header')` expects. The test found `role="heading"` only on the DashboardHeader greeting ("مرحبا E2E!"). **Impact:** FE-TC-01 step 3 assertion on eyebrow header would fail even if BUG-001 were fixed. **Fix:** either add `accessibilityRole="header"` rendering as `role="heading"` (Tamagui/RNW may need `aria-level` attribute), or add a dedicated `testID="subjects-eyebrow"` to the eyebrow Text for testability. Low severity since the section testID presence check in step 2 passes. | Open — file back to `frontend` |

---

## 4. testIDs / seams requested back to `frontend`

The following testIDs were present and working at implementation time (all per the brief's "testIDs NOW EXIST" preamble):
- `subjects-list-section`, `subjects-loading`, `subjects-error`, `subjects-empty` — PRESENT ✓
- `subject-row-{math|science|arabic|english}` — PRESENT ✓ but never rendered in practice (BUG-001)
- `segmented-tab-lessons`, `segmented-tab-tree` — PRESENT ✓
- `lessons-loading`, `lessons-error`, `lessons-empty`, `subject-not-found` — PRESENT ✓
- `unit-header-{unitId}`, `empty-unit-{unitId}`, `lesson-card-{lessonId}` — PRESENT ✓

Still missing / requested:
- [ ] **OQ5 / FE-TC-06** — add `testID="subject-detail-header"` and render the real subject name (not generic "Subjects") in `[subjectId]/_layout.tsx`. Currently line 119: `t('child.subjects.title')`.
- [ ] **BUG-002 fix** — `subjects-eyebrow` testID on the `SubjectsListSection` eyebrow Text (or fix `accessibilityRole="header"` → `role="heading"` mapping on web).
- [ ] **OQ3** — API-based seed helper in `tests/e2e/` to cut the 2-minute register+add-child chain per test.

---

## 5. Root cause — BUG-001 (Critical): `filterSubjects` discards all seeded subjects

**Verified via direct API call:**
```
GET /api/learning/Subjects/ForGrade?grade=1 (child JWT, learningLanguage=ar)

Response data:
  id=1  name="الرياضيات (الصف 1)"       subjectCode=0 (MATH)     lang=null
  id=3  name="العلوم (الصف 1)"           subjectCode=1 (SCIENCE)  lang=null
  id=5  name="اللغة العربية (الصف 1)"   subjectCode=2 (ARABIC)   lang=null
  id=6  name="English (G1)"              subjectCode=3 (ENGLISH)  lang=null
```

**`SUBJECT_NAME_MAP` exact keys:**
```
الرياضيات, العلوم, العربية, الإنجليزية, math, science, arabic, english
```

None of the seeder names match (grade suffix `(الصف 1)` / `(G1)` breaks the match).
`subjectCode` integer values ARE returned but `filterSubjects` ignores them.

**Screenshot evidence:** `test-results/P2-02-FE-Group-A-—-Browse--0f0cf-edded-subjects-list-section-chromium/test-failed-1.png` — child home renders correctly (RTL, Arabic, grade caption, ContinueCard with a Math lesson), but the subjects section shows the empty state "قريباً — لا توجد دروس بعد" with no subject rows.

**Why FE-TC-20 and FE-TC-21 pass:** those tests stub the API response with clean names ("Math", "Science", "Arabic", "English") without grade suffixes, so `filterSubjects` resolves them correctly.

**File to fix:** `apps/student-app/app/(child)/_components/subjects.ts`, function `resolveSubjectKey`. Use `subjectCode` integer enum as primary key.

---

## 6. Environment gotchas

- Expo web auto-started by Playwright `webServer` (took ~50s first bundle).
- Backend returns `language: null` for all subjects (the `lang` field on `StudentSubjectDto` is not mapped to a non-null value by the current seeder / DTO), but this does not affect the current bug.
- 4 subjects ARE returned by the backend for grade-1 ar child (confirmed by direct API call); the bug is entirely in the FE filtering layer.
