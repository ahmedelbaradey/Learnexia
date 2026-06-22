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
| DEL-INT-06 | Push failure isolated; inbox row still written | BLOCKED | BLOCKED-pushsender-fault-mode: TestPushSenderImpl always succeeds; cannot inject fault mode. Isolation is verified by code review (NudgeDispatcher persists inbox row BEFORE push attempt). Test passes trivially as a documented blocker. |

## Summary
- Total: 6 · Passed: 5 · Failed: 0 · Blocked: 1 · Skipped: 0

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

## Blockers encountered

**DEL-INT-06 — BLOCKED-pushsender-fault-mode**
`TestPushSenderImpl.SendAsync` always returns success. There is no fault-mode injection. To unblock: add `SetFaultMode(bool throws)` to `TestPushSenderImpl` that causes the next `SendAsync` call to throw. Mitigation: NudgeDispatcher source code confirms the inbox row is persisted in Step 1, before the push attempt in Step 3 — push isolation by design. The existing P9-07 arbitration tests also exercise the dispatcher's fail-soft path.

## Defects found
| # | Severity | Case | Description | Status |
|---|---|---|---|---|
| DEL-F01 | LOW | DEL-INT-03 | `Notification.RecipientExternalUserId = childId` (not parentId). AC3 "only linked parents notified" is satisfied functionally (cross-family isolation passes) but the literal recipient is the child's user id, not the parent's. FE must read child-keyed notifications via linkage. | Open — for lead review |
