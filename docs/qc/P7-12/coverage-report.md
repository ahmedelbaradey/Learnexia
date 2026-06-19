# P7-12 Admin Audit Log — Coverage Report

**Story:** `user-stories/Phase-7-Admin-Console/P7-12-admin-action-audit-log.md`
**Brief:** `docs/briefs/P7-12.md`
**Controller:** `backend/src/Modules/Moderation/.../Controllers/AuditController.cs` (`api/Admin/Audit/Log`, AdminOnly, read-only)
**Existing suite:** `backend/tests/Learnexia.IntegrationTests/P7_12_AuditLog_Tests.cs`

## Counts

| Bucket | Total | Covered | GAP |
|---|---|---|---|
| Backend | 31 | 18 | **13** (2 of which are cross-referenced as covered in sibling suites) |
| Frontend (reference) | 10 | n/a | n/a |

## Acceptance-criteria → coverage matrix

| AC (story) | Backend case IDs | Verdict |
|---|---|---|
| AC-1 Every admin action (moderation, curriculum, role/config, P7-13 gamification, P7-08 lang change) writes actor/action/target/timestamp/before-after | 12-21..24 (curriculum); 12-25 (gamification, via P7-13 suite); **12-26 account (P7-07) GAP**, **12-27 lang-change (P7-08) GAP**, **12-28 moderation (P7-09) GAP** | **Gap — only the curriculum + gamification producers are proven; account, language-change, and moderation producers are unverified** |
| AC-2 Append-only / immutable — no edit/delete path | 12-05, 12-06, 12-07 | Covered |
| AC-3 Paginated, searchable/filterable by actor/action/target/date | 12-13..18; **12-19 combined GAP**, **12-20 inverted-range GAP** | Covered (individual filters); composition uncovered |
| AC-4 Export filtered log (CSV/JSON) | **12-31 GAP/spec-confirm** | **Gap — no export route visible; scope unconfirmed** |
| AC-5 Admin-only read; non-admin → 403; view-only | 12-01..04 | Covered |
| AC-6 Written by domain/integration events, not direct writes; FullAuditedEntity | 12-21, 12-23 | Covered |
| AC-7 Before/after snapshot is accountability-only, no child PII | 12-22, 12-24; **12-27 (lang old/new snapshot) GAP** | Partial — PII-safety proven for curriculum; child-targeting snapshots (P7-08) untested here |

## Risk notes

1. **The audit log is a fan-in from every P7 producer, but the P7-12 suite only proves ONE producer (Learning curriculum Subject.Created).** The whole value of the log is breadth of capture. The prior P7-backend execution report already caught a real defect class here: *curriculum create handlers didn't raise the domain event* (Bucket C). The same failure mode is live for any producer that forgets to raise `AdminActionPerformedDomainEvent`. Account actions (P7-07, 12-26), the learning-language change (P7-08, 12-27), and moderation review (P7-09, 12-28) are all named in the AC and **none has an audit-focused test in this suite** — gamification (12-25) is covered only because the P7-13 suite tests it.
2. **`createdAt` stamping regression (12-30).** The prior execution report (Bucket D) found `AuditLog.CreatedAt` always `0001-01-01` and the date filter keying off the unstamped column. `E2E2` checks `occurredAtUtc` but not `createdAt`; without an explicit assertion the fix can silently regress. Medium risk.
3. **Export (AC-4) may be unbuilt.** No export route on `AuditController`. Either the BE doesn't implement it (real AC gap) or it's intended client-side in the admin app. Needs a lead decision before 12-31 can be scoped.
4. **Filter composition** (12-19) and inverted-range degradation (12-20) are low risk but cheap.

## Prioritized backend GAP list for api-tester

**P1:**
- 12-26 Account suspend/reactivate/delete (P7-07) → audit row (confirm action strings)
- 12-27 Learning-language change (P7-08) → audit row w/ old/new snapshot, no PII
- 12-28 Moderation review (P7-09) → audit row (cross-ref 09-33)
- 12-30 createdAt stamped (not 0001-01-01) — regression guard for the prior Bucket-D defect

**P2:**
- 12-19 combined actor+action+date filters AND-compose
- 12-20 inverted date range → 200/empty not 500
- 12-29 multiple action types coexist & filter independently
- 12-31 export filtered log (CSV/JSON) — **only after lead confirms scope**

## Open questions / assumptions for the lead

- **Export scope (AC-4):** is CSV/JSON export a backend route or a client-side admin-app feature? No route exists on `AuditController` today. Decision needed before 12-31.
- **Exact action strings** for the Identity (P7-07) account actions and the P7-08 learning-language change must be read from those handlers' `AdminActionPerformedDomainEvent` raise sites before 12-26/12-27 can assert literals. If a producer does not raise the event, the test surfaces a real AC-1 capture gap (same defect class as the historical curriculum-create miss).
- Producer coverage overlaps with sibling suites by design (P7-13 already proves the gamification producer end-to-end). Recommend api-tester keeps the cross-module producer assertions in **one** place per producer to avoid duplication — the P7-12 cases 12-26/12-27/12-28 should assert the *consumer* side (row lands + shape) without re-testing each producer's own validation.
