# P7-02 — Lessons & Content Blocks admin — Coverage report

## Summary
- Story: P7-02 Manage lessons & content (lesson CRUD + metadata + ordered content-block editor + atomic delete + language inheritance).
- Backend cases catalogued: **32** (BE-TC-01..32). Covered by existing tests: **23**. **GAP (to implement): 9.**
- Frontend reference cases: 10 (FE lead).
- Existing test source: `P7_02_LessonsContentBlocks_Tests.cs` (52 facts) + `P2_01_CurriculumHierarchy_Extended_Tests.cs` (Create-404, difficulty enum).

## Acceptance criteria → test cases → status

| Acceptance criterion (story) | Test case(s) | Status |
|------------------------------|--------------|--------|
| Lesson CRUD with metadata scoped to unit | BE-TC-15, BE-TC-16, BE-TC-25, BE-TC-26 | Covered + **GAP** (Update non-existent regression) |
| Lesson/content `Language` inherited from Subject, not editable | BE-TC-31, BE-TC-32 | **GAP** (placement/resolved-language guard under-tested) |
| Add content blocks with type/payload/order | BE-TC-01, BE-TC-07, BE-TC-08, BE-TC-10 | Covered + **GAP** (non-existent lesson, malformed payload) |
| Reorder/remove block persists | BE-TC-03, BE-TC-04, BE-TC-05, BE-TC-06 | Covered |
| Reorder lessons within unit | BE-TC-21, BE-TC-22 | Covered |
| Delete lesson handles blocks atomically | BE-TC-19, BE-TC-20 | Covered |
| Admin-only access; non-admin → 403 | BE-TC-28, BE-TC-29, BE-TC-30 | Covered + **GAP** (Lessons/List lockdown) |

**No acceptance criterion is fully uncovered.** Every criterion has at least one Covered case; GAPs harden
non-existent-entity error paths, malformed payloads, the language-inheritance guard, and the `List` lockdown.

## Prioritized GAP list for `api-tester`

**P0:**
1. BE-TC-25 Lesson Update non-existent → 404 not 500, no `ex.Message` leak (PR #183-style)

**P1:**
2. BE-TC-10 Add block to non-existent lessonId → `Successed=false` (not 500)
3. BE-TC-11 Edit non-existent blockId → `Successed=false`, no leak
4. BE-TC-30 Lessons `List` anonymous→401 / non-admin→403

**P2:**
5. BE-TC-12 Delete non-existent blockId → `Successed=false`
6. BE-TC-13 Malformed (non-JSON) payload → 422 not 500
7. BE-TC-14 Oversized payload handled gracefully (never 500)
8. BE-TC-31 Lesson admin DTO surfaces resolved language
9. BE-TC-32 Lesson placement resolves to single language tree

## Risk notes
- The block-editor and atomic-delete paths are the richest and **already well-covered** — low residual risk.
- Highest residual risk is the **non-existent-entity error paths** (add/edit/delete against a wrong id): these are the
  exact "500-not-graceful" shapes that PR #183 fixed elsewhere; weighted P0/P1 even though P7-02 wasn't named in #183,
  because the same handler pattern is reused.
- Language inheritance (P7-02-BE-6) is asserted only indirectly via student reads; a direct placement-guard test is a
  P2 hardening item, not a blocker.
