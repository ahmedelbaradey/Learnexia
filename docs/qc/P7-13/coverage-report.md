# P7-13 Gamification Admin Overrides — Coverage Report

**Story:** `user-stories/Phase-7-Admin-Console/P7-13-gamification-admin-overrides.md`
**Brief:** `docs/briefs/P7-13.md`
**Controller:** `backend/src/Modules/Gamification/.../Controllers/AdminGamificationController.cs` (`api/Admin/Gamification`, AdminOnly)
**Existing suite:** `backend/tests/Learnexia.IntegrationTests/P7_13_GamificationAdmin_Tests.cs` (the most complete of the four — AC-1..AC-9 + audit E2E + seeder)

## Counts

| Bucket | Total | Covered | GAP |
|---|---|---|---|
| Backend | 70 | 51 | **19** |
| Frontend (reference) | 15 | n/a | n/a |

## Acceptance-criteria → coverage matrix

| AC (story) | Backend case IDs | Verdict |
|---|---|---|
| AC-1 Override league tier persists + audited | 13-56..61, 13-63 | Covered |
| AC-2 Badge CRUD; deactivate hides from engine, keeps earned badges; reactivate | 13-18..22; **13-23 earned-badge preservation GAP**, **13-24/25 unknown-id GAP** | **Gap — earned-badge preservation invariant untested** |
| AC-3 Mission CRUD; same activate/deactivate semantics | 13-32..35; **13-36 dup-code GAP** | Covered (minor symmetry gap) |
| AC-4 Timed-event create/update/activate/expire; window/multiplier validation; double-transition | 13-37..45; **13-46 unknown-id, 13-47 inclusive boundary, 13-48 update-window GAP** | Covered (boundary + unknown-id gaps) |
| AC-5 Streak-freeze grant; cap; audited | 13-49..54, 13-62; **13-55 handler-cap (already-at-max) GAP** | Covered (handler-cap path gap) |
| AC-6 Every override/edit/grant audited w/ actor/time/target/old→new/reason, PII-safe | 13-62..65; **13-66 deactivate/update, 13-67 mission/timed create, 13-68 no-double-write GAP** | Partial — only create+activate+grant+tier producers audit-tested |
| AC-7 AdminOnly; non-admin → 403 | 13-01..11; **13-12/13/14 PUT/PATCH/activate-route auth GAP** | Covered (mutation-route role-matrix gap) |
| AC-8 BaseResponse envelope | 13-15, 13-16; **13-17 timed-events list GAP** | Covered |
| AC-9 422 on invalid command bodies | 13-26..28, 13-50..53, 13-56..58; **13-29/30/31 badge/mission enum+numeric GAP** | Covered (enum/numeric breadth gap) |
| Seed-vs-admin precedence (Notes) | 13-69; **13-70 mission seeder GAP** | Covered for badges only |

## Risk notes

1. **AC-2 earned-badge preservation is the only P0 functional gap (13-23).** The story explicitly requires that deactivating a `BadgeDefinition` must NOT retroactively strip `StudentBadge` rows already earned. The suite *documents* this in its header comment but never tests it. A deactivate cascade/soft-delete bug here would silently revoke kids' earned badges — high-impact, data-destructive, and untested. **Top priority.**
2. **Audit producer breadth (13-66/67/68).** Like P7-12, the audit relay is only proven for a subset of producers (badge **create**, timed-event **activate**, streak grant, tier override). The deactivate/update producers and mission/timed-event **create** are unverified. The historical defect class (a handler forgetting to raise `AdminActionPerformedDomainEvent`) applies to each. Medium-high.
3. **Boundary completeness.** Multiplier is tested at 0.5 and 6 (both fail) but never at the inclusive [1,5] edges (13-47). Streak-freeze validator-cap (Count>2) is tested but the *handler* cap (child already at MaxFreezes) is not (13-55). Low-medium.
4. **Mutation-route auth matrix (13-12/13/14).** Auth is proven for GET/POST routes but PUT/PATCH/activate/expire are not asserted against non-admins. Since the class-level `[Authorize(AdminOnly)]` covers all, risk is low, but a regression that moved an attribute would slip through.

## Prioritized backend GAP list for api-tester

**P0:**
- 13-23 Deactivating a badge def does NOT strip earned StudentBadges

**P1:**
- 13-12, 13-13, 13-14 PUT/PATCH/activate routes → 403/401 for non-admin
- 13-24 PUT badge unknown id → Successed=false/404
- 13-36 duplicate mission code → rejected (confirm rule exists)
- 13-46 activate/expire unknown timed-event id → Successed=false/404 not 500
- 13-55 grant to child already at MaxFreezes → balance capped, Successed=false
- 13-68 one override → exactly one audit row (no double-write)

**P2:**
- 13-17 timed-events list envelope
- 13-25 PATCH badge active unknown id
- 13-29, 13-30, 13-31 badge/mission enum + numeric validators → 422
- 13-47 multiplier inclusive boundaries (1 and 5) accepted
- 13-48 PUT timed-event with Start≥End → 422
- 13-66, 13-67 deactivate/update + mission/timed create audit rows
- 13-70 admin-edited mission survives MissionSeeder re-run

## Open questions / assumptions for the lead

- **13-23 setup:** seeding a `StudentBadge` for the test requires either the engine's earn path or a direct `GamificationDbContext` insert (the suite already uses direct DbContext inserts for `StudentXpProfile`, so this is consistent). Confirm `StudentBadge` entity name/shape for the api-tester.
- **Mission dup-code (13-36):** confirm `CreateMissionDefinitionCommandValidator` / handler enforces unique Code (badge does, via 424). If missions have no such rule, 13-36 becomes a spec note rather than a test.
- **Audit action strings** for deactivate/update/mission-create/timed-create producers must be read from each handler's `AdminActionPerformedDomainEvent` raise site before 13-66/67 can assert literals — and if a producer doesn't raise the event, the test surfaces a real AC-6 capture gap.
- This suite is already the strongest of the four; the gaps are completeness/edge-case, not foundational. Recommend the api-tester treat 13-23 and the audit-breadth cases as the meaningful adds and the rest as backlog.
