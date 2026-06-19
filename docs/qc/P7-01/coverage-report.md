# P7-01 — Subjects & Units admin — Coverage report

## Summary
- Story: P7-01 Manage subjects & units (CRUD + reorder + activate/deactivate + bilingual trees + coverage).
- Backend cases catalogued: **35** (BE-TC-01..35). Covered by existing tests: **18**. **GAP (to implement): 17.**
- Frontend reference cases: 10 (FE lead).
- Existing test sources: `P7_01_SubjectsUnitsAdmin_Tests.cs` (P7-01 management endpoints) and
  `P2_01_CurriculumHierarchy_Tests.cs` + `P2_01_CurriculumHierarchy_Extended_Tests.cs` (base CRUD, Create-path 404/422 guards, auth-on-6-controllers, unique-key, SubjectCode product rule).

## Acceptance criteria → test cases → status

| Acceptance criterion (story) | Test case(s) | Status |
|------------------------------|--------------|--------|
| Subject roots list with code/language/grade/order/active | BE-TC-12 (GetById), BE-TC-34 (list pagination) | Covered (List: AC1) + **GAP** (GetById, pagination) |
| Per-grade language-coverage view flags gaps | BE-TC-30, BE-TC-33 | Covered + **GAP** (gradeId=0 boundary) |
| Create/edit unit scoped to `(code,language)` tree | BE-TC-11 (Update), BE-TC-26 | **GAP** (Update) + Covered (base CRUD via P2-01 BE-TC-02) |
| Drag-reorder persists `SequenceOrder` within language tree | BE-TC-18, BE-TC-19, BE-TC-35 | Covered + **GAP** (duplicate-id edge) |
| Activate/deactivate hides from student, preserves row, scoped to tree | BE-TC-22, BE-TC-23, BE-TC-24, BE-TC-25 | Covered + **GAP** (Unit SetActive 0-id 422) |
| Reject duplicate `(GradeId,SubjectCode,Language)`; no 5th SubjectCode | BE-TC-28, BE-TC-32, BE-TC-06 | Covered (Create) + **GAP** (Update collision) |
| Reject delete of non-empty unit | BE-TC-26 | Covered |
| (implicit) Reject delete of grade with subjects, no FK-500, no leak | BE-TC-04, BE-TC-05 | **GAP** ★ PR #183 regression |
| (implicit) Edit non-existent → 404 not 500, no `ex.Message` leak | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-07 | **GAP** ★ PR #183 regression |
| Admin-only access; non-admin → 403 | BE-TC-14, BE-TC-15, BE-TC-16, BE-TC-17 | Covered (writes) + **GAP** (Subjects/Units `List`/`GetById` lockdown; Grades read breadth) |
| BaseResponse envelope shape | BE-TC-31 | Covered |

**No acceptance criterion is left fully uncovered.** Every criterion has at least one Covered case; the GAPs harden
the implicit/edge/regression paths.

## Prioritized GAP list for `api-tester` (implement these)

**P0 — must (regression + auth lockdown):**
1. BE-TC-01 Subject Update non-existent → 404, no leak ★
2. BE-TC-02 Unit Update non-existent → 404, no leak ★
3. BE-TC-03 Grade Update non-existent → 404, no leak ★
4. BE-TC-04 Grade Delete with subjects → 400 "not empty", no FK-500, no leak ★
5. BE-TC-06 Subject Update → duplicate tree → 400/422, no unique-index-500, no leak ★
6. BE-TC-15 Subjects/Units `List` + `GetById` anonymous→401 / non-admin→403

**P1 — should:**
7. BE-TC-05 Grade Delete (empty) succeeds
8. BE-TC-07 Unit Update → non-existent SubjectId → 404, no leak
9. BE-TC-08 Subject Delete non-existent → 404, no leak
10. BE-TC-09 Unit Delete non-existent → 404, no leak
11. BE-TC-10 Subject Update happy-path persists
12. BE-TC-11 Unit Update happy-path persists

**P2 — nice:**
13. BE-TC-12 Subject GetById admin round-trip fields
14. BE-TC-13 Subject Update empty Name → 422
15. BE-TC-17 Grades read reachable by non-admin authed user (200)
16. BE-TC-25 Unit SetActive 0-id → 422
17. BE-TC-33 Coverage gradeId=0 boundary
18. BE-TC-34 Subjects/List pagination metadata
19. BE-TC-35 Reorder duplicate-id edge

## Risk notes
- **Highest risk = the PR #183 surface (Group A).** These bugs (500-not-404, `ex.Message` leak, FK-500 on grade delete)
  were live in production-shaped code and only the Create paths got regression tests. Update + Grade-Delete are the
  thinnest-tested mutating paths and the exact shapes that regressed — weighted P0.
- **Info-leak assertion is the load-bearing part** of Group A: a handler can "pass" a 404 check while still returning a
  stack trace on a different exception. Each regression case must assert the body is leak-free, not just the status.
- Admin DTO lockdown on `List`/`GetById` (BE-TC-15) leaks `IsActive`/`SequenceOrder`/`UnitId` if it regresses to
  anonymous — security-adjacent, so P0.
