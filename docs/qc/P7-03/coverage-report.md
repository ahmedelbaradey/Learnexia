# P7-03 — Skills & Knowledge Graph admin — Coverage report

## Summary
- Story: P7-03 Author skills & skill dependency graph (skill CRUD + edge authoring + cycle/cross-language/duplicate guards + per-language reads).
- Backend cases catalogued: **34** (BE-TC-01..34). Covered by existing tests: **24**. **GAP (to implement): 10.**
- Frontend reference cases: 9 (FE lead).
- Existing test source: `P7_03_SkillsGraph_Tests.cs` (~50 facts) + `P2_01_CurriculumHierarchy_Extended_Tests.cs` (skill Create-404, threshold bounds).

## Acceptance criteria → test cases → status

| Acceptance criterion (story) | Test case(s) | Status |
|------------------------------|--------------|--------|
| Skill CRUD with threshold/time/owner | BE-TC-15, BE-TC-18, BE-TC-19, BE-TC-21, BE-TC-22, BE-TC-23 | Covered + **GAP** (Update/Delete non-existent regression) |
| Add prerequisite edge (`KnowledgeEdge` + Strength) | BE-TC-08, BE-TC-11, BE-TC-12, BE-TC-14 | Covered + **GAP** (Strength bounds, relType enum) |
| Reject cross-language edge | BE-TC-03 | Covered |
| Reject cycle | BE-TC-01, BE-TC-02, BE-TC-05, BE-TC-07 | Covered + **GAP** (self-loop, cycle×cross-lang interaction) |
| Reject duplicate edge | BE-TC-04 | Covered |
| List prerequisites-of / unlocked-by, per-language | BE-TC-28, BE-TC-33, BE-TC-34 | Covered + **GAP** (non-existent node, single-language read scoping) |
| Remove edge | BE-TC-10, BE-TC-13 | Covered + **GAP** (edgeId=0 validator) |
| Admin-only access; non-admin → 403 | BE-TC-29, BE-TC-30, BE-TC-31 | Covered |
| Skill deletion as a live prerequisite | BE-TC-24 | **GAP** (cascade-vs-guard behavior unasserted) |
| Cross-subject same-language edge behavior | BE-TC-06 | **GAP** |

**No acceptance criterion is fully uncovered.** The three headline guards (cycle / cross-language / duplicate) are all
Covered; GAPs are edge-of-edge cases (self-loop, bounds, enum, interaction, deletion-as-prerequisite, read scoping).

## Prioritized GAP list for `api-tester`

**P0:**
1. BE-TC-05 Self-loop edge (Source==Target) → rejected, no persist
2. BE-TC-21 Skill Update non-existent → 404 not 500, no `ex.Message` leak (PR #183-style)

**P1:**
3. BE-TC-06 Cross-subject same-language edge → documented behavior, not 500
4. BE-TC-23 Skill Delete non-existent → 404 not 500, no leak
5. BE-TC-24 Skill delete while it is a live prerequisite → graceful behavior

**P2:**
6. BE-TC-07 Cycle × cross-language interaction → one clear reject
7. BE-TC-12 Strength inclusive bounds 0.0 / 1.0 accepted
8. BE-TC-13 RemoveEdge edgeId=0 → 422
9. BE-TC-14 Invalid RelationshipType enum → 422
10. BE-TC-33 Prerequisites/UnlockedBy non-existent node → graceful (not 500)
11. BE-TC-34 GetGraph single-language read scoping

## Risk notes
- The skill graph is described in the story as "the most important asset in the company" — but the three headline
  guards are **already well-covered**. Residual risk is concentrated in **degenerate graph inputs**: the **self-loop**
  (BE-TC-05) is the single most likely uncaught cycle (the existing cycle tests only use ≥2 distinct nodes), so it is P0.
- The Skill Update/Delete non-existent paths (BE-TC-21/23) reuse the same handler pattern that regressed in PR #183
  elsewhere — P0/P1 even though P7-03 wasn't named in #183.
- Deletion of a skill that is an active prerequisite (BE-TC-24) is a data-integrity risk for student learning paths;
  assert the behavior is deliberate (cascade or guard), not an accidental 500 / orphan.
