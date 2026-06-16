# Seat enforcement, grace period & NoSeat/Locked child lifecycle

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 8 — grace-window state machine + enforcement job + child NoSeat/Locked lifecycle + parent "choose active children" flow.
- **Labels:** `billing`, `subscription`, `seats`, `child-data`, `backend`, `frontend`
- **Requirements:** FR-PAY-11 *(new — Phase 10)*

## Description
As a parent whose paid seats are reduced (downgrade, seat cancellation, or a failed payment), I want a grace period during which my children keep learning, and then a clear way to choose which children keep the remaining active seats — so that a reduction never silently deletes a child or destroys their progress, and over-limit children are only locked out of AI help, not removed.

This story is the **enforcement + lifecycle half** of the LOCKED family energy model. It builds directly on the seat model (P10-14) and the family wallet + entitlement grant + per-child allocation (P10-13), the redistribution/reset rules (P10-16), the subscription/downgrade state machine (P10-05), dunning/payment-failure (P10-09), and the immutable per-child ledger (P10-01). It does **not** define how energy is granted or split — that is P10-13/P10-16. It defines **what happens to seats and children when the number of paid active seats goes down**.

## Locked rules this story implements (authoritative — do not reinterpret)
- **Seats are not reduced instantly.** A downgrade, an explicit seat cancellation, or a payment failure starts a **grace period**. During grace, the previously-active seats stay **temporarily active** and the affected children keep full access (allocations stay live, AI spend continues). On grace expiry, limits are **enforced**.
- **Only paid ACTIVE seats** ever generate entitlement energy (per P10-13). Grace does not mint new energy beyond the seats already active.
- **Enforcement NEVER deletes children.** When active paid seats < children currently holding a seat, the **parent chooses** which children keep the remaining active seats (up to the limit). Children not chosen become **NoSeat/Locked**.
- A **NoSeat/Locked** child: has **no subscription allowance row** (no entitlement allocation), **cannot spend AI energy**, but is **never deleted** and **keeps all learning progress, XP, streaks, history, mastery, and achievements** (Identity + Learning/Gamification data untouched). A locked child may still be re-activated later (parent re-assigns a seat when seats become available).
- **Already-spent energy is never reclaimed** on enforcement (consistent with the P10-16 redistribution rule). Unspent entitlement allocation of a now-locked child is forfeited/returned to the family wallet per the P10-13 allocation/reset rules — it is **not** converted to purchased.
- **Purchased (pack) energy is untouched** by seat enforcement — it is a permanent shared family reserve (P10-13/P10-07). A NoSeat/Locked child still cannot spend (the spend gate is seat-based), but the family's purchased reserve is never reduced by enforcement.
- All state movements (entitlement allocation removed on lock, allowance forfeited) are written to the **immutable per-child ledger** (P10-01) — never silent.

## Acceptance Criteria

### Grace period (downgrade / seat-cancel / payment-failure)
- Any event that reduces paid active seats — a **downgrade** (P10-05 `Downgrading`), an explicit **seat cancellation** (P10-14), or a **payment failure / dunning** (P10-09 `PastDue`/`Dunning`) — starts a **grace window** instead of enforcing immediately.
- During grace, the affected seats remain **active**: the children keep their allocations and can still spend AI energy; entitlement grants for the current cycle are **not** revoked mid-grace.
- The parent is shown a **grace banner** stating that seats are being reduced, how many seats will remain, the **grace end date**, and a call to action to choose which children keep seats (resolve early to skip enforcement surprises).
- Grace is **idempotent**: repeated triggering events (e.g. several failed-charge webhooks) do not stack or shorten the window beyond the single active grace window.
- **OPEN NUMBER (LEAD FLAG):** the **grace-period length** and exact **behavior during grace** are not finalized. Recommend aligning seat-grace with the existing P10-09 dunning grace (grace ends at `CurrentCycleEnd`) and making the length **config-driven** via P10-12 GlobalSettings (e.g. `seats.grace_days`). Confirm with lead before implementation; do not hard-code a number.

### Enforcement on grace expiry
- When the grace window ends, the platform **enforces** the new seat limit: the number of children allowed an active seat = current **paid active seats**.
- If children-holding-a-seat ≤ paid active seats, **no child is locked** (nothing to enforce).
- If children-holding-a-seat > paid active seats, the **over-limit** children are moved to **NoSeat/Locked** state. If the parent has already chosen which children keep seats (see below), that choice is honored; otherwise a **deterministic default** selection is applied (recommend: keep the earliest-activated seats; flag tie-break rule to lead) and the parent can still re-choose afterward.
- Enforcement **never deletes a child** and never touches Identity/Learning/Gamification records.
- Each lock writes an **immutable ledger** entry; unspent entitlement allocation of the locked child is forfeited per P10-13 allocation/reset rules (never converted to purchased; never reclaimed if already spent).

### NoSeat/Locked child state
- A NoSeat/Locked child has **no active entitlement allowance row** and **cannot spend AI energy** — the seat-based spend gate (P10-13) denies AI Helper requests for that child with a localized, friendly message (no raw error).
- The locked child **keeps everything else**: profile, learning progress, XP, streaks, history, mastery, badges/achievements — all intact and still viewable.
- A locked child can be **re-activated** by the parent assigning it an available active seat (when seats free up or the parent buys/keeps more), at which point it rejoins the entitlement allocation (P10-13).

### Parent chooses which children keep active seats
- The parent sees the list of their children with current seat state and can **choose which children keep the active seats**, constrained to the **current paid active-seat limit** (cannot select more than the limit).
- The choice can be made **during grace** (pre-empts the default) or **after enforcement** (re-shuffle who is active vs locked).
- Submitting a choice that selects more children than the limit is **rejected** with a clear validation message; selecting fewer is allowed (remaining seats simply stay unassigned).
- The action is **parent-gated and family-scoped** — a parent can only set seat assignments for their own children. Applying the choice updates seat assignments and child Locked/Active state and is **ledgered** (no silent state change).

## Web-checkout-first / money — COMPLIANCE FLAG
> This story does **not** itself take payment, but it reacts to payment-failure (P10-09) and changes paid-seat entitlement. Because it touches **money-derived entitlement and child data**, the **`security-auditor` stage is mandatory** for the backend batch (family-scope authz on seat re-assignment, no cross-family seat moves, no child deletion, idempotent enforcement).

## Backend vs Frontend scope

**Backend (P10-15-BE):**
- Grace-window state on the seat/subscription model (start on downgrade/seat-cancel/payment-failure; config-driven length via P10-12; idempotent).
- Enforcement job (Hangfire) that runs on grace expiry: compute over-limit children, apply parent choice or deterministic default, move over-limit children to NoSeat/Locked, forfeit unspent allocation per P10-13 allocation/reset rules, write immutable ledger entries — never delete a child.
- NoSeat/Locked child state in the Billing model + the **seat-based spend gate** so a locked child's AI spend is denied (graceful, localized).
- Parent commands/queries: view seat state per child, choose which children keep active seats (validated against the paid-active-seat limit, family-scoped), re-activate a child when a seat is free.
- `Shared.Contracts` seam so the AI/spend path and the Parent module can ask "does this child currently hold an active seat?" without crossing module boundaries (no FK). Add-child seat reservation seam is owned by P10-14; this story consumes the same seat-state seam for the locked check.
- Option C service-only (Application EF-free); no free-text string literals (localized keys + enums).

**Frontend (P10-15-FE) — parent area only, other (frontend) lead:**
- **Grace banner** on the parent plan/billing surface: "seats are being reduced", seats-remaining, grace-end date, CTA to choose active children.
- **Choose-active-children** screen/modal: list children with seat state, select up to the limit, submit; validation when over the limit; confirmation copy.
- **Locked-child state** in the parent's child list / child cards: a clear "no active seat — learning history kept, AI help paused" badge + a "give this child a seat" affordance when seats are available.
- Never rendered on a child route; parent-gated. EN + AR, RTL.

## Dependencies
- **P10-14** (seats model + active-seat count + add-child seat reservation seam) — **hard upstream**; seat state lives here.
- **P10-13** (family energy account + entitlement grant + per-child allocation + allocation/cycle-reset rules) — **hard upstream**; enforcement forfeits unspent allocation per its reset rules.
- **P10-16** (sibling redistribution) — shares the "already-spent energy is never reclaimed" rule; keep allocation-row contracts consistent.
- **P10-05** (subscription / downgrade state machine) — downgrade is one grace trigger.
- **P10-09** (dunning / payment-failure + grace at cycle end) — payment-failure is one grace trigger; reuse/align the grace window.
- **P10-01** (immutable per-child ledger) — all lock/forfeit movements are ledgered.
- Relates to **P10-17** (purchased-energy refunds) — keep seat-state + ledger contracts consistent.

## Notes
- **Supersedes the per-child `CreditAccount` ownership model** (LOCKED 2026-06-16) — the family-account migration is owned by P10-13 and the seat model by P10-14; this story assumes the family/seat model is already in place and only adds grace + enforcement + locked lifecycle.
- **LEAD FLAG (open number):** grace-period length + behavior during grace — recommend config-driven (P10-12) and aligned with P10-09's cycle-end grace; confirm before build.
- Children are **never deleted** and **never lose progress/XP/history/achievements** — this is a hard invariant; the security-auditor must verify enforcement cannot cascade into Identity/Learning data.
- **No energy proration for seats in MVP** (per the locked model) — enforcement is a binary keep/lock per child, not a partial-seat calculation.
