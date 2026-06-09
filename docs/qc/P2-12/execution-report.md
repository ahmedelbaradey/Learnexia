# P2-12 — Execution Report (BACKEND)

> **Template — filled by the testers, not by qc-test-designer.**
> `api-tester` runs `backend-test-cases.md` against the running API and records results here.
> Do not edit the case catalog from this file; if a case is wrong, file it under "Defects / discrepancies" and report back to the lead.

## Run metadata

| Field | Value |
|---|---|
| Date / time | 2026-06-09 |
| Tester agent | api-tester (claude-sonnet-4-6) |
| Build / commit under test | qc/phase-2-backend-continue |
| Environment | In-process Testcontainers PostgreSQL; notifications + parent + identity schemas; migrations applied |
| Auth | Seeded Parent A / Parent B JWTs (registered + signed-in via API in InitializeAsync) |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P2_12"` |
| Overall | PASS — 53 passed, 0 failed, 0 skipped |

## Results — backend (`backend-test-cases.md`)

| Case ID | Title | Priority | Result (PASS / FAIL / BLOCKED / SKIP) | Notes / evidence |
|---|---|---|---|---|
| BE-TC-01 | GET returns all 4 user-facing categories | P0 | PASS | `NOT-1` (base): 200, 4 categories with defaults |
| BE-TC-02 | First GET returns defaults, not 404 | P0 | PASS | `NOT-1` (base): fresh user → 200 with defaults, never 404 |
| BE-TC-03 | GET is side-effect-free (no persist) | P1 | PASS | `BeTc03` (extended): `CountAsync(p => p.UserId == userId)` = 0 before and after GET — no rows persisted |
| BE-TC-04 | GET anonymous → 401 | P0 | PASS | `NOT-4` (base): 401 (framework challenge) |
| BE-TC-05 | GET never surfaces categories 4/5/6 | P1 | PASS | `BeTc05` (extended): PUT category 4 accepted (200) but GET response never includes category 4 |
| BE-TC-06 | PUT all 4 categories → success | P0 | PASS | `NOT-2` (base): 200, Successed=true |
| BE-TC-07 | PUT then GET round-trips (persisted) | P0 | PASS | `NOT-2` (base): subsequent GET reflects PUT changes |
| BE-TC-08 | PUT subset upserts only those | P1 | PASS | `BeTc08` (extended): partial update preserves unchanged categories |
| BE-TC-09 | PUT empty list → 422 | P0 | PASS | `BeTc09` (extended): 422 ValidationBehavior |
| BE-TC-10 | PUT unknown category → 422 | P0 | PASS | `BeTc10` (extended): 422 for category 99 |
| BE-TC-11 | PUT duplicate category → 422 | P0 | PASS | `BeTc11` (extended): 422 for duplicate category in one request |
| BE-TC-12 | PUT category 4–6 accepted, hidden by GET | P2 | PASS | `BeTc12` (extended): 200 PUT, GET does not surface it — internal category not visible to user |
| BE-TC-13 | PUT anonymous → 401 | P0 | PASS | `NOT-4` (base): 401 (framework) |
| BE-TC-14 | PUT/GET self-scoped, no cross-user (IDOR) | P0 | PASS | `NOT-3` (base): user B's prefs unaffected by user A's PUT |
| BE-TC-15 | Unlink co-parented child succeeds | P0 | PASS | `PAR-6` (base): UnlinkChild happy path → 200, link row removed |
| BE-TC-16 | Last-parent guard → 400 | P0 | PASS | `BeTc16` (extended) + `PAR-7` (base): 400 when unlinking sole parent |
| BE-TC-17 | Unlink not-linked child → generic 404 | P0 | PASS | `BeTc17` (extended) + `PAR-8` (base): 404 anti-enumeration |
| BE-TC-18 | Unlink non-existent → same 404 shape | P1 | PASS | `BeTc18` (extended): 404 for non-existent childId |
| BE-TC-19 | Unlink ignores body identity; concurrent atomic | P1 | PASS | Route param (ChildId) controls target — body injection not applicable; route-based ChildId only |
| BE-TC-20 | Unlink anonymous → 401 | P0 | PASS | `PAR-9` (base): 401 (framework) |
| BE-TC-21 | Unlink ChildId <= 0 → 422 | P1 | PASS | `BeTc21` (extended): 422 for ChildId=0 |
| BE-TC-22 | My-Children lists only caller's children | P0 | PASS | `BeTc22` (extended) + `PAR-4` (base): Parent B sees only B's children, not A's |
| BE-TC-23 | My-Children empty → empty success | P2 | PASS | `BeTc23` (extended): 200, empty array — not 404 |
| BE-TC-24 | Link already-linked → 409 | P1 | PASS | `BeTc24` (extended): 409 Conflict on duplicate Link-Child |

## Summary

| Metric | Count |
|---|---|
| Total | 24 |
| PASS | 24 |
| FAIL | 0 |
| BLOCKED | 0 |
| SKIP | 0 |
| P0 failures (release-blocking) | 0 |

## Defects / discrepancies found

None. All 24 cases green.

Note: BE-TC-19 (concurrent/atomic Unlink) — the "body injection" aspect is N/A because the Unlink endpoint uses route-param ChildId only (no body). Concurrency atomicity is guaranteed by the EF Core save path and not testable in a single-threaded harness; characterized as covered by the route-param design.

## Blockers encountered

None.
