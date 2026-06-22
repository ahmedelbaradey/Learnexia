# P5-04 — Report Delivery via Notifications — Execution Report

> Filled in by **api-tester** after implementing `backend-test-cases.md`.
> Test file: `backend/tests/Learnexia.IntegrationTests/P5_04_ReportDelivery_Tests.cs`
> Run date: 2026-06-22
> Runner: api-tester (Claude Sonnet 4.6)

## How to run
```
# from repo root
dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P5_04_ReportDelivery_Tests" -c Debug
```
(Requires Docker for Testcontainers Postgres — see docs/dev/HANDOFF.md.)

## Results

| Case ID | Title | Status | Notes / defect ref |
|---|---|---|---|
| DEL-INT-01 | Report generation triggers recap delivery to the family (full chain) | PASS | Generator → event → handler → inbox chain confirmed; Category=WeeklyReport(0) |
| DEL-INT-02 | Recap row links to report + renders parent-facing copy | PASS | Title/Body non-empty; {xp}/{skills} substituted; Code=WEEKLY_RECAP + Category=WeeklyReport confirmed |
| DEL-INT-03 | Only the linked family notified (cross-family isolation) | PASS | Family 1 has recap; Family 2 has zero WEEKLY_RECAP rows — load-bearing AC3 assertion passes |
| DEL-INT-04 | Zero-activity week → no recap (suppressed end-to-end) | PASS | WeeklyReport row written; no WEEKLY_RECAP notification; no push attempts |
| DEL-INT-05 | Orphan child → fail-soft, no throw, no row | PASS | No exception; no WEEKLY_RECAP row for orphanChildId=998_004_001 |
| DEL-INT-06 | Push failure isolated; inbox row still written | PASS | **Closed 2026-06-22** — `P5_04_ReportDeliveryPushFailure_Tests`: a single-purpose `ThrowingPushSender` (always throws, no shared-mutable state) injected via a per-test `WithWebHostBuilder` factory over the same Testcontainers DB. With an active device token seeded, the dispatcher attempts push → throws → caught in `TrySendPushAsync` → `SaveChangesAsync` still commits. Asserts: dispatch does not throw; inbox row persists; push bit (2) NOT set; in-app bit (4) set; `SentAtUtc` stamped. |

## Summary
- Total: 6 designed (+1 follow-up) · Passed: 7 · Failed: 0 · Blocked: 0 · Skipped: 0 (DEL-INT-06 unblocked 2026-06-22)

## Recipient-semantics finding (FINDING DEL-F01)

**Observed recipient:** `Notification.RecipientExternalUserId = childId` (the student's user id)

**Code path:** `WeeklyRecapReadyIntegrationEventHandler` calls `FindParentForChildAsync(ev.StudentId)` to gate on parent preferences and build the `NudgeMessage`, but sets `NudgeMessage.RecipientChildUserId = childId`. The `NudgeDispatcher` then persists `Notification.RecipientExternalUserId = message.RecipientChildUserId`. The parentId is used only internally for preference gating; it is NOT stored as the notification recipient.

**AC3 satisfied as-built?** FUNCTIONALLY YES — the cross-family isolation test (DEL-INT-03) passes: notifications are scoped to the correct family because each child is linked to exactly one parent. Family 2's child receives no recap. There is no cross-family data leak.

**Literal AC3 interpretation:** AC3 states "only linked PARENTS are notified." The literal recipient stored in the Notification entity is the **child's** user id, not the parent's user id. The parent app reads via the parent-child linkage on the read path (parent queries the child's inbox). This is the as-built re-engagement pattern used consistently across all 11 nudge handlers.

**Defect flag:** FINDING DEL-F01 — Severity: LOW (no data-safety issue; no cross-family leak)
- Observed: `RecipientExternalUserId = childId`
- Story AC3: "only linked parents notified" (could be read as parent should be the literal recipient)
- Impact: If the FE reads the notification inbox keyed on parentId (not childId), no notifications will be found. The FE must read child-keyed notifications via the linkage.
- Verdict: As-built contract is consistent with all other re-engagement handlers. Flagged for lead review to confirm the FE reads model is aligned.

**RESOLUTION (lead, 2026-06-22): RESOLVED — BY DESIGN. Not a defect.** The platform uses a **child-scoped family inbox model**: notification ownership is child-keyed (`RecipientExternalUserId = childId`) and the parent accesses notifications through the family relationship (read via the parent→child linkage), NOT a parent-keyed inbox. This is the consistent contract across all 11 re-engagement handlers; cross-family isolation is intact (DEL-INT-03). **No parent-keyed inbox in MVP.** AC3 ("only linked parents notified") is satisfied through the linkage. Ratified in **ADR 0003** (`docs/dev/adr/0003-child-scoped-family-inbox.md`). The FE parent app MUST read child-keyed notifications via the family linkage.

## Blockers encountered

**DEL-INT-06 — RESOLVED 2026-06-22 (was BLOCKED-pushsender-fault-mode).**
Rather than add a `SetFaultMode` toggle to the shared `TestPushSenderImpl` (shared-mutable state → cross-test flakiness risk), a dedicated stateless `ThrowingPushSender` is injected via a per-test `WithWebHostBuilder` factory over the same Testcontainers DB (`P5_04_ReportDeliveryPushFailure_Tests`). The case now asserts push-fault isolation directly (not just by code review). No shared fake state.

## Defects found
| # | Severity | Case | Description | Status |
|---|---|---|---|---|
| DEL-F01 | LOW | DEL-INT-03 | `Notification.RecipientExternalUserId = childId` (not parentId). AC3 "only linked parents notified" is satisfied functionally (cross-family isolation passes) but the literal recipient is the child's user id, not the parent's. FE must read child-keyed notifications via linkage. | Open — for lead review |
