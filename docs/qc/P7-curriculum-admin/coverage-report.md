# P7 Curriculum (Admin) — QC Coverage Report

**Scope:** Frontend E2E (Playwright, `admin` project, port 3001) for the merged Curriculum Wave-2 surfaces in `apps/admin-dashboard` — P7-01 subjects/units, P7-02 lessons/content-blocks, P7-03 skills/graph, P7-04 questions, P7-05 lifecycle/publish.
**Backends:** merged + already integration-tested → treated as **pre-existing**; this run validates the FE behaviour against what SHIPPED (not the imagined contract).

## Summary

| Metric | Count |
|--------|------|
| Total FE cases | 94 (CUR-TC-01 … CUR-TC-94) |
| P0 | 38 |
| P1 | 41 |
| P2 | 15 |
| Backend contract-smoke | pre-existing (not in this catalog; see note below) |

**By area:** Subjects 20 · Subject-detail+Units 10 · Lessons+detail 8 · Content blocks 10 · Questions 15 · Skills+graph 14 · Lifecycle/preview/coverage 12 · Cross-cutting (RTL/a11y/auth/no-optimistic) 5.

**Backend note:** backends are merged and covered by their own integration tests (P7-01..05). No new BE cases are authored here. If a contract-smoke is wanted, the minimum set is: `GET Subjects/List` envelope `{Successed,data,totalCount,totalPages}`; `GET ContentLifecycle/Preview` returns `currentState`; `GET ContentVersionHistory`; `POST KnowledgeGraph/Edges` cycle→400; question CorrectAnswer/Options jsonb round-trip per type. **Mark BE coverage pre-existing.**

## Coverage matrix (acceptance criterion → case IDs)

| Story / AC | Cases | Status |
|------------|-------|--------|
| P7-01 subjects list: grade filter | CUR-TC-05 | covered |
| P7-01 subjects list: language filter (client) | CUR-TC-06 | covered |
| P7-01 subjects list: search | CUR-TC-07, 08 | covered |
| P7-01 subjects: pagination | CUR-TC-09 | covered |
| P7-01 subjects: four states | CUR-TC-01, 02, 03, 04 | covered |
| P7-01 subjects CRUD | CUR-TC-11, 12, 14, 16 | covered |
| P7-01 pinned-language rule | CUR-TC-13 | covered |
| P7-01 IsActive toggle | CUR-TC-15, 27 | covered |
| P7-01 single-tree keyboard reorder | CUR-TC-17, 18 | covered |
| P7-01 subject detail + nav | CUR-TC-10, 21, 22 | covered |
| P7-01 units CRUD + reorder | CUR-TC-24, 25, 26, 28, 29 | covered |
| Product rule: 4 subjects, no Social Studies | CUR-TC-20, 89 | covered |
| P7-02 lessons list four states + nav | CUR-TC-31, 32, 33 | covered |
| P7-02 lessons CRUD | CUR-TC-34, 35, 37 | covered |
| P7-02 lessons active toggle | CUR-TC-36 | covered |
| P7-02 lessons reorder | CUR-TC-38 | covered |
| P7-02 content-block editor four states | CUR-TC-39 | covered |
| P7-02 add/edit/reorder/delete 4 block types | CUR-TC-40, 41, 42, 43, 44, 45, 46 | covered |
| P7-02 sanitized markdown preview (XSS) | CUR-TC-40, 43 | covered |
| P7-02 URL guard (SEC-3) | CUR-TC-41, 42 | covered |
| P7-02 payload cap (SEC-2) | CUR-TC-47 | covered |
| P7-02 preview parse-error guard | CUR-TC-48 | covered (BLOCKED if no malformed seed) |
| P7-04 questions list four states | CUR-TC-49 | covered |
| P7-04 4 types CorrectAnswer/Options round-trip | CUR-TC-50, 51, 52, 53 | covered |
| P7-04 Matching grades-correctly e2e (must-have) | CUR-TC-53 | covered |
| P7-04 per-type validation | CUR-TC-54, 55, 56 | covered |
| P7-04 type locked on edit | CUR-TC-57 | covered |
| P7-04 edit/delete/activate-deactivate | CUR-TC-58, 59, 60 | covered |
| P7-04 reorder | CUR-TC-61, 62 | covered |
| P7-04 matching parse-error guard | CUR-TC-63 | covered (BLOCKED if no malformed seed) |
| P7-03 skills list/filter/states | CUR-TC-64, 65, 66 | covered |
| P7-03 skills CRUD | CUR-TC-67, 68, 69 | covered |
| P7-03 accessible graph nav | CUR-TC-70, 71 | covered |
| P7-03 add/remove prerequisite edges | CUR-TC-72, 73 | covered |
| P7-03 cycle/duplicate/cross-language rejection | CUR-TC-74, 75, 76 | covered |
| P7-03 picker excludes self/existing | CUR-TC-77 | covered |
| P7-05 publish/unpublish/archive/restore | CUR-TC-78, 79, 80, 81 | covered |
| P7-05 version history + Latest | CUR-TC-82 | covered |
| P7-05 rollback | CUR-TC-83 | covered |
| P7-05 preview (valid + 404) | CUR-TC-84, 85 | covered |
| P7-05 publication coverage matrix | CUR-TC-86, 87, 88, 89 | covered |
| P7-05 lifecycle badge in lists | CUR-TC-19, 23, 30 | covered |
| Cross: auth routing | CUR-TC-90 | covered |
| Cross: RTL | CUR-TC-91 | covered (runtime RTL BLOCKED — admin is build-time `en`) |
| Cross: a11y (roles/caption/axe/focus-trap) | CUR-TC-92, 93 | covered |
| Cross: no-optimistic | CUR-TC-94 (+ asserted in every mutation case) | covered |

### Gaps / known limitations (not defects unless reproduced)
- **Lesson/Question lifecycle slots are placeholders** — `lesson-{id}-lifecycle-slot` and `question-{id}-lifecycle-slot` are empty by design (DTOs lack `lifecycleState`). No lifecycle-badge assertion for lessons/questions (only subjects + units fetch via Preview). Covered as "slot exists, empty".
- **Subject detail resolves from page-1 of the list hook** — a real subject living on list page ≥2 can show the not-found state. CUR-TC-22 records this as a known limitation; flag as a defect only if a page-1 subject mis-resolves.
- **Runtime RTL unreachable** — `ADMIN_LOCALE='en'` build-time; CUR-TC-91 is a static `dir`-attribute check; full ar-layout RTL is **BLOCKED**.
- **Coverage all-published (CUR-TC-88)** and **malformed-payload guards (CUR-TC-48, 63)** depend on data states that may need API seeding; mark BLOCKED if unreachable.

## Test Data / Seeding

**Admin login (all cases):** `superadmin` / `123Pa$$word!` at `/login` (fields `name="userName"`, `name="password"`). Prefer a Playwright `storageState` auth fixture.

**What the curriculum seed provides (`LearningSeeder`):** subjects, units, lessons, questions across grades/languages — enough for read/list/filter/state/reorder cases. Use seeded entities for read-only and reorder cases.

**What each flow must create (do NOT mutate shared seed rows destructively):**
- Create/edit/delete cases (subjects/units/lessons/blocks/questions/skills) → create a **throwaway** entity with a unique name (e.g. `qc-<area>-<timestamp>`), act on it, delete it. Keeps re-runs idempotent.
- Pagination (CUR-TC-09): if seed has `totalPages<=1`, bulk-create subjects via API (`POST Subjects`) to exceed one page.
- Reorder cases: need ≥2 siblings in the same (grade, language) tree / unit / lesson — create extras if seed is thin.
- Lifecycle (CUR-TC-78..83): a freshly-created subject defaults to **Draft** → drive Publish→Unpublish→Archive→Restore→Rollback through the UI. Rollback needs ≥2 published versions (publish, unpublish, edit, publish again).
- Coverage all-published (CUR-TC-88): publish all 4 subjects × AR/EN for one grade via API if not seeded.
- Malformed-payload guards (CUR-TC-48 block, CUR-TC-63 matching): require seeding a row with invalid `payload`/`options`/`correctAnswer` JSON via direct API — if the API rejects malformed input (likely), these are **BLOCKED**; document and skip.
- Negative graph rejections (CUR-TC-74/75/76): prefer Playwright route-interception returning the documented 400 error codes (`KnowledgeEdgeWouldCreateCycle`, `KnowledgeEdgeDuplicate`, `KnowledgeEdgeCrossLanguageForbidden`) rather than constructing real cycles, since the picker already excludes existing prerequisites.

**Enum values on the wire (INT):** Subject MATH=0 SCIENCE=1 ARABIC=2 ENGLISH=3 · Language Ar=0 En=1 · Lifecycle Draft=1 Published=2 Archived=3 · BlockType Text=1 Image=2 Video=3 Callout=4 · QuestionType MCQ=1 TrueFalse=2 Matching=3 FillInBlank=4. Use these when seeding via API or asserting payloads.

## Handoff note

### MISSING / weak `data-testid`s the E2E will need (for `frontend` to add)
1. **Login inputs** — `/login` Tamagui `TextField`s expose only `name="userName"` / `name="password"` (no `data-testid`, no submit-button testid). E2E will select by `name=` + the accessible button label. *Nice-to-have:* add `data-testid="login-username|login-password|login-submit"` for robustness.
2. **Form modal roots** — `SubjectForm`, `UnitForm`, `LessonForm`, `BlockForm`, `QuestionEditor` dialog containers: only `QuestionEditor` (`question-editor-dialog`) and `SkillForm` (`skill-form-dialog`) have a root testid. SubjectForm/UnitForm/LessonForm/BlockForm rely on `role="dialog"`. E2E will scope by `role=dialog`; *nice-to-have* `subject-form-dialog|unit-form-dialog|lesson-form-dialog|block-form-dialog`.
3. **AdminErrorBanner messages** — most error/success banners have no testid (asserted by visible text via `strings.*`). Brittle across copy edits. *Nice-to-have:* `data-testid` on banner variants (e.g. `toggle-error-banner`, `reorder-error-banner`).
4. **Lifecycle action dialogs** — `PublishCurriculumDialog`/`RollbackCurriculumDialog` confirms have testids (`publish-confirm-btn`, `rollback-confirm-btn`), but **Unpublish/Archive/Restore confirm buttons** were not confirmed to have dedicated testids — E2E should scope the confirm inside the open dialog. *Nice-to-have:* `unpublish-confirm-btn|archive-confirm-btn|restore-confirm-btn`.
5. **Coverage matrix rows** use `coverage-row-{subject-label-lowercase}` (math/science/arabic/english) — confirmed present; individual cells have `coverage-slot-not-created`/`coverage-warning-chip` but no per-cell `(subject,lang)` testid → assert via row + column position.

### Backend / seed prerequisites
- Backend `:5080` running (Development, CORS `:3001`) with `LearningSeeder` applied; admin app `:3001` with `NEXT_PUBLIC_API_URL=http://localhost:5080`.
- For lifecycle/version/rollback cases, the `ContentLifecycle` + `ContentVersionHistory` endpoints must be live (they back the per-row badges and panels).
- If seeding malformed payloads is impossible through the API, CUR-TC-48 and CUR-TC-63 are BLOCKED — record in `execution-report.md`.
