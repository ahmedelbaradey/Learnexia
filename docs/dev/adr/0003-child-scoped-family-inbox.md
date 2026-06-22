# ADR 0003 — Child-scoped family inbox model for notifications

- **Status:** Accepted (2026-06-22)
- **Deciders:** Backend lead
- **Context source:** finding **DEL-F01** from the P5-04 backend E2E (PR #230); ratified here.
- **Related:** [[ADR 0002 — domain events and dispatch]], P4-09 notification foundation, P5-04 report delivery, P9-05..12 re-engagement nudges.

## Context

The notification / re-engagement system (P4-09 foundation + the P9 nudge handlers) writes every inbox row keyed to the **child** (student) user id:

```
Notification.RecipientExternalUserId = childId   // never the parentId
```

This holds for **all 11 re-engagement handlers** — including notifications conceptually "for the parent", such as the **weekly recap** (P5-04). The `WeeklyRecapReadyIntegrationEventHandler` resolves the parent only to gate on the parent's preferences and build the message; the persisted recipient is still the child. The parent id is never stored as the notification owner.

The P5-04 E2E (DEL-F01) surfaced the question: the story AC reads "**only linked parents are notified**", which could be interpreted as the parent being the literal recipient. The as-built behaviour delivers to the child's inbox, which the parent reads via the family relationship.

Cross-family isolation is intact and asserted (DEL-INT-03): each child is linked to exactly one parent, so no notification leaks across families.

## Decision

Adopt the **child-scoped family inbox model** as the platform standard for MVP:

1. **Notification ownership is child-keyed.** `RecipientExternalUserId = childId` for every notification, including parent-facing ones (weekly recap, etc.). This is the single consistent contract across all handlers.
2. **Parent access is through the family relationship.** A parent reads a child's notifications via the parent→child linkage on the read path — there is no separate parent-keyed inbox.
3. **No parent-keyed inbox in MVP.** We will NOT introduce a parent-owned notification store. AC "only linked parents notified" is satisfied through the linkage + cross-family isolation, not by changing the literal recipient.

## Consequences

- **Backend:** no change required — this ratifies the existing, consistent behaviour. DEL-F01 is **resolved as by-design, not a defect**.
- **Frontend (FE lead):** the parent app MUST read notifications **child-keyed via the family linkage** (e.g. fetch per linked child), NOT by querying a parent-id inbox — which would return nothing. This constraint is recorded on the P5-04 / P9-02 FE tasks.
- **Isolation:** unchanged — cross-family isolation is guaranteed by the one-parent-per-child link and verified by `P5_04_ReportDelivery_Tests` (DEL-INT-03).
- **Future option (post-MVP, needs its own story):** if a genuine parent-owned inbox is ever required (e.g. account-level notices not tied to a child), it would be a new feature + data model — explicitly out of scope here.

## Alternatives considered

- **Parent-keyed inbox / dual recipient:** rejected for MVP — a feature change + data-model addition touching all 11 handlers + the read API, for no functional gain (the linkage already delivers parent-facing notifications correctly and isolated).
