# P5-02 — Weak-Area Detection — Execution Report

> Filled in by **api-tester** after implementing `backend-test-cases.md`.
> Test file: `backend/tests/Learnexia.IntegrationTests/P5_02_WeakAreaDetection_Tests.cs`
> Run date: 2026-06-22
> Runner: api-tester (Claude Sonnet 4.6)

## How to run
```
# from repo root
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P5_02_WeakAreaDetection_Tests" -c Debug
```
(Requires Docker for Testcontainers Postgres — see docs/dev/HANDOFF.md.)

## Results

| Case ID | Title | Status | Notes / defect ref |
|---|---|---|---|
| WA-INT-01 | Mixed-severity child → correct severity bands over Postgres | PASS | High(<30%), Medium(30-50%), Low(≥50%+bad accuracy) bands confirmed over Npgsql; strong skill and implicit NotStarted excluded |
| WA-INT-02 | Ranking High → Medium → Low, then deficit desc | PASS | High(10%) before High(25%) before Medium(40%) before Low(60%) confirmed; OrderByDescending(severity).ThenByDescending(deficit) translates correctly under Npgsql |
| WA-INT-03 | Strong-skills-only child → empty list (not error) | PASS | Empty list returned; no throw; sentinel contract holds |
| WA-INT-04 | Resolved area drops off after mastery rises | PASS | Skill at 25% detected; after update to 85% + good accuracy attempt, drops off on re-query (AC4 auto-drop-off confirmed) |
| WA-INT-05 | maxResults cap enforced over the real query | PASS | 7 high-severity skills seeded; maxResults=5 returns ≤5 from our set; highest-deficit skills included |
| WA-INT-06 | Cross-subject detection with correct SubjectCode | PASS | MATH=0, SCIENCE=1, ENGLISH=3 confirmed in returned entries; no code outside 0-3 |
| WA-INT-07 | Parent endpoint surfaces detected weak areas (smoke) | PASS | GET /api/Parent/Children/{id}/WeakAreas with seeded child returns 200 + areas array containing the seeded weak skill |

## Summary
- Total: 7 · Passed: 7 · Failed: 0 · Blocked: 0 · Skipped: 0

## Implementation notes

### Npgsql LINQ translation confirmed
The group-by + `OrderByDescending(sa => sa.Attempt.CompletedAt).Select(sa => sa.Attempt.AccuracyPercentage).FirstOrDefault()` chain (the recent-accuracy lookup for the Low tier) translates correctly under the real Npgsql provider. WA-INT-01 confirms the Low-tier detection works end-to-end.

### Grade.Number uniqueness
Tests use a hash-derived grade number per test tag to avoid the unique-index collision on `(GradeId, SubjectCode, Language)` that was documented in P6-04. Each test creates a fresh Grade with a pseudo-unique Number.

### WA-INT-07 de-dup note
WA-INT-07 exercises a child with actual seeded weak areas (differs from P5-08 E5-HAPPY which asserts envelope shape against a fresh empty child). Both tests are complementary.

## Defects found
None.
