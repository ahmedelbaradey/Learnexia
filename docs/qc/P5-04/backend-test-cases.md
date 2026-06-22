# P5-04 — Report Delivery via Notifications — Backend Test Cases

> Story: [P5-04 Deliver reports via notifications](../../../user-stories/Phase-5-Parent-Analytics/P5-04-report-delivery.md)
> Task: [P5-04-BE](../../../tasks/Backend/Phase-5-Parent-Analytics/P5-04-BE.md) (traceability task — no new backend work; verifies the P5-01 + P9-06 delivery path)
> Surface under test: **event-driven delivery** — `WeeklyRecapReadyIntegrationEvent` → `Notifications/.../Reengagement/WeeklyRecapReadyIntegrationEventHandler` → `NudgeDispatcher` → parent inbox row (+ push). **No HTTP endpoint.** Assertion is on the persisted `Notification` row for the parent recipient.
> Target agent: **api-tester**
> File type: integration tests in `backend/tests/Learnexia.IntegrationTests/` (new file `P5_04_ReportDelivery_Tests.cs`), Testcontainers Postgres, `[Collection("IntegrationTests")]`. Mirror `P9_06_HabitLoop_Tests` exactly (it already drives this same handler).

## Scope and de-duplication (read before implementing)

`P9_06_HabitLoop_Tests` **already covers** the core handler behavior:
- TC-04 — `WeeklyRecapReadyIntegrationEvent` (xp=150, skills=4) writes a `WEEKLY_RECAP` row with `Category == WeeklyReport(0)`.
- TC-06 — the row's `Title`/`Body` are rendered with `{xp}`/`{skills}` substituted.
- TC-09 — orphan child (no parent link) → fail-soft: no throw, no row.

`WeeklyRecapPublishTests` (Parent.UnitTests, RC-01..06) **already covers** the producer side: publish-on-active, suppress-on-pure-zero, event field correctness, publish-throws fail-soft, row-persisted-despite-publish-failure.

**Do NOT re-implement TC-04/06/09 or RC-01..06.** The P5-04 gap these cases close is the **report-delivery acceptance criteria specifically** — the end-to-end "report generated → recap reaches the **linked parent** and only the linked parent, zero-activity → no nudge, delivery is fail-soft + logged" chain, asserted from the **report generator** as the trigger (not a hand-published event), and the **parent-targeting / cross-family isolation** that P9-06 does not assert.

## Important targeting clarification (verify during implementation)

The current `WeeklyRecapReadyIntegrationEventHandler` resolves `parentId` via `IParentChildQuery.FindParentForChildAsync(ev.StudentId)` but then builds/dispatches a nudge whose recipient is keyed on `ev.StudentId` (the existing re-engagement rows query `RecipientExternalUserId == childId` in P9-06). **The api-tester must confirm against the running system who the persisted `Notification.RecipientExternalUserId` actually is** (child vs parent) and assert P5-04 AC3 ("only linked parents notified") against the **observed** recipient semantics. If the recipient is the child id (recap is a child-keyed re-engagement row that the parent app reads via linkage), document that as the as-built contract in the execution report; if AC3 intends the parent to be the literal recipient, flag a **possible defect** for the lead rather than asserting a behavior the code does not implement. Either way, the cross-family isolation case (DEL-INT-03) is the load-bearing AC3 assertion.

## Seeding notes (binding for the implementer)

- Mirror `P9_06_HabitLoop_Tests`: `ApplyMigrationsAndSeedAsync`, `LearningSeeder.SeedAsync`, `BadgeSeeder.SeedAsync`, `_factory.PushSender.Reset()`, `_factory.TestClock.Reset()` in `InitializeAsync`.
- Use `CreateParentChildPairAsync` to get `(parentToken, childToken, parentId, childId)`.
- Use `GetNotificationsAsync(childId, code: "WEEKLY_RECAP")` (P9-06 helper) plus, if recipient is the parent, an equivalent lookup keyed on `parentId`.
- Allow `Task.Delay(~400ms)` after publishing for the in-process MediatR dispatch (P9-06 convention).

---

## Test cases

### DEL-INT-01 — Report generation triggers a recap delivery to the linked family (end-to-end from the generator)
- **Type:** functional / integration (full chain)
- **Priority:** P0
- **Traces to:** AC1 (when a weekly report is generated, the parent receives a notification via the Notifications module)
- **Preconditions / seed:** Parent P + child C with real activity (xp > 0 or skills > 0). Reset push sender.
- **Steps:**
  1. Resolve `IWeeklyReportGeneratorService`; call `GenerateAsync(C, lastMonday)` (the generator publishes `WeeklyRecapReadyIntegrationEvent`).
  2. `Task.Delay(~400ms)`.
  3. Query `Notifications` for the recap row of the family (code `WEEKLY_RECAP`).
- **Expected result:** Exactly ≥1 `WEEKLY_RECAP` row written, `Category == NotificationCategory.WeeklyReport`. Confirms the **generator → event → handler → inbox** chain works end-to-end (P9-06 starts from a hand-published event; this starts from the real producer, closing the P5-04 "when a report is generated" wording).

### DEL-INT-02 — Recap row links the family to the report and renders parent-facing copy
- **Type:** functional / RTL-i18n
- **Priority:** P1
- **Traces to:** AC2 (notification links to / opens the report); AC1 (delivered via Notifications)
- **Preconditions / seed:** Parent P + child C with xp=200, skills=5; child locale `ar` (default) — and a second pair with locale `en` if the harness supports per-child locale.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)`; delay.
  2. Read the `WEEKLY_RECAP` row.
- **Expected result:** Row `Title` and `Body` non-empty, `{xp}`/`{skills}` substituted (200 / 5 present). Body is locale-appropriate (Arabic copy for `ar`). The row carries the metadata the FE deep-link uses to open the report (assert whatever link/payload field exists — `Code == "WEEKLY_RECAP"` + `Category == WeeklyReport` at minimum; if a deep-link/route field exists on the entity, assert it is populated).
- **De-dup note:** Overlaps P9-06 TC-06 on placeholder substitution. Keep this case to the **delivery-link** angle (the report-opening contract). If TC-06 already covers everything observable, mark DEL-INT-02 covered-by-TC-06 and add only the link-field assertion.

### DEL-INT-03 — Only the linked family is notified; a second unrelated family receives nothing (cross-family isolation)
- **Type:** auth-authz / negative (IDOR-equivalent for delivery)
- **Priority:** P0
- **Traces to:** AC3 (delivery respects parent-child linkage; only notifies linked parents)
- **Preconditions / seed:** Family 1 = Parent P1 + child C1 (active). Family 2 = Parent P2 + child C2 (active but no report generated). Reset push sender.
- **Steps:**
  1. `GenerateAsync(C1, lastMonday)` only.
  2. Delay.
  3. Query recap rows for **both** families.
- **Expected result:** Family 1 has a `WEEKLY_RECAP` row; Family 2 has **none**. No recap row references C2 or P2. Confirms linkage scoping — the load-bearing AC3 assertion that P9-06 does not make.

### DEL-INT-04 — Zero-activity week generates NO recap (suppressed end-to-end)
- **Type:** negative / boundary
- **Priority:** P0
- **Traces to:** AC1 (notification only when a report is meaningfully generated); FR-GM-8 never-shaming; P5-01 GEN-INT-03
- **Preconditions / seed:** Parent P + freshly-linked child C, **no** activity. Reset push sender.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)` (producer suppresses the event for pure-zero weeks).
  2. Delay.
  3. Query recap rows for the family.
- **Expected result:** A `WeeklyReport` row exists (week processed) but **no** `WEEKLY_RECAP` notification row and **no** push attempt recorded by `_factory.PushSender`. Confirms suppression survives the full chain (not just the unit-level producer check RC-03).

### DEL-INT-05 — Delivery is fail-soft: an orphan child (no parent link) is logged and skipped without throwing
- **Type:** negative / fail-soft
- **Priority:** P1
- **Traces to:** AC4 (failed delivery retried and logged); ADR 0002 fail-soft
- **Preconditions / seed:** None (orphan child id with no `ParentStudent` linkage).
- **Steps:**
  1. Publish `WeeklyRecapReadyIntegrationEvent(StudentId: orphanId, xp>0, skills>0)` via `IPublisher`.
  2. `Record.ExceptionAsync` around the publish; delay.
  3. Query recap rows for `orphanId`.
- **Expected result:** No exception propagates; no recap row written. (Mirror of P9-06 TC-09; included here so P5-04's AC4 has an explicit owned trace. If the api-tester prefers, mark it covered-by-P9-06-TC-09 in the matrix rather than re-implementing.)

### DEL-INT-06 — Push delivery isolated: a failing push sender does not block the inbox row (fail-soft + logged)
- **Type:** negative / fail-soft
- **Priority:** P2
- **Traces to:** AC4 (failed delivery retried and logged; push isolated)
- **Preconditions / seed:** Parent P + child C with activity; configure `_factory.PushSender` to fault on send (if the test harness supports a failure mode).
- **Steps:**
  1. `GenerateAsync(C, lastMonday)`; delay.
  2. Query the `WEEKLY_RECAP` inbox row.
- **Expected result:** The inbox row is still written (in-app delivery succeeds) even though push failed; no exception propagates. Confirms `NudgeDispatcher` isolates push from inbox.
- **Blocker check:** If the test `PushSender` cannot be put into a failure mode, mark **BLOCKED — pushsender-fault-mode** and note that push-failure isolation is asserted indirectly by the dispatcher's existing fail-soft design.

---

## Priority summary
- **P0:** DEL-INT-01, DEL-INT-03, DEL-INT-04
- **P1:** DEL-INT-02, DEL-INT-05
- **P2:** DEL-INT-06

Total new integration cases: **6** (with DEL-INT-05 optionally folded into existing P9-06 TC-09). P9-06 TC-04/06/09 and Parent unit RC-01..06 remain and are NOT duplicated.

## Open question for the lead
The recap recipient semantics (child-keyed re-engagement row read via linkage vs. literal parent recipient) determine whether AC3 "only linked parents notified" is satisfied by the as-built code or needs a fix. See the "Important targeting clarification" section. The api-tester should record the observed behavior; the lead decides if it is the intended contract.
