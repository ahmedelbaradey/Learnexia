# Pause / unpause a child's access (parent control)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 3 — immediate-effect parent toggle + IDOR authz + localized blocked UX for the child.
- **Labels:** `billing`, `child-data`, `parent-control`, `security-sensitive`, `backend`, `frontend`
- **Requirements:** FR-PAY-12 *(new — Phase 10)*

## Description
As a parent, I want to immediately pause one of my children's access to AI features — and unpause it later — without changing my subscription, seats, or the child's energy balance, so that I have a simple parental control that takes effect right now rather than waiting for a billing cycle boundary.

This story is a **parent-control feature**, entirely separate from the billing-driven seat lifecycle:
- It is **not** a seat cancellation (P10-14).
- It is **not** the NoSeat/Locked billing state (P10-15).
- It is **not** energy forfeiture, redistribution (P10-16), or refunds (P10-17).
- The Paused state only blocks the child's real-time usage of AI features; it has zero effect on billing, seat counts, or energy balances.

## Locked mid-cycle seat model — authoritative context (2026-06-17)

The following rules from the approved mid-cycle model are **load-bearing background** for this story (they determine what P10-18 must NOT do):

- **ADD/REACTIVATE a seat mid-cycle:** prorate MONEY only; NO energy is minted mid-cycle — the child's mid-cycle energy comes ONLY from the parent allocating family-wallet credit (P10-16). Renewal grant = `ActiveSeats × PlanEnergyPerSeat`.
- **REMOVE/cancel a seat mid-cycle:** effective at CYCLE END; seat stays Active until next renewal; child keeps remaining allowance; NO prorated refund; NO energy reclaim/forfeit/convert. At renewal, the seat is removed and the grant recalculates with fewer seats; over-seat children → NoSeat/Locked, never deleted, keep history/XP/progress.
- **7-DAY GRACE = payment-failure retry window** at the renewal boundary only — it is NOT a mid-cycle energy strip.
- **Purchased packs:** family-owned, never expire, not prorated, not seat-linked — entirely unaffected by this story.

**P10-18 (Pause) touches NONE of these mechanics.** Pause is a real-time access gate only.

## Acceptance Criteria

### Pause (immediate effect)
- A parent can **pause a child's access** with a single action (with a confirmation step). The pause takes effect **immediately**.
- A paused child **cannot** invoke any AI Tutor feature (Explain, Hint, SimilarExample, Simplify) — the system returns a localized, friendly "your access is paused by your parent" message. **No raw error is exposed.**
- A paused child's **energy balance is unchanged**: no energy is minted, forfeited, or converted on pause. The child's subscription allowance and the family's purchased reserve are both untouched.
- A paused child's **seat is unchanged**: the seat remains Active; billing, seat count, and the renewal grant are all unaffected.
- A paused child **keeps all learning progress, XP, streaks, history, mastery, and badges** — no data is touched by pause or unpause. Pause only gates AI feature access.
- The parent can only pause **their own children** (IDOR — owning-parent scope; family-scoped authz). Attempting to pause another family's child is rejected.

### Unpause (immediate effect)
- A parent can **unpause a previously paused child** at any time. The child regains AI access immediately.
- Unpause **does not mint new energy** — the child's energy balances are exactly what they were before the pause (they were untouched during the pause).
- Unpause **does not change the seat state** — if the seat was Active before the pause, it is still Active after unpause.

### State model
- A child has a **`ParentPauseState`** that is either `Active` (not paused) or `Paused` (parent-paused).
- `ParentPauseState` is **entirely separate** from the billing-driven `SeatState {Active, NoSeatLocked}` (P10-15). A child can be in any combination: Active/Active, Active/Paused, NoSeatLocked/Active, NoSeatLocked/Paused. The AI spend gate must check **both** — either locked seat OR parent-paused blocks AI spend. **The two states must not be conflated, merged, or inferred from each other.**
- State changes (Pause / Unpause) are **immutable ledger events** — written to the per-child audit log (P10-01) as `ParentPause` / `ParentUnpause` entries. No silent state change.
- `ParentPauseState` is stored on the child's Billing seat/profile record (Billing module owns this field) — not in Identity, not in Learning/Gamification.

### Parent visibility
- The parent's child list shows each child's `ParentPauseState` clearly — e.g. a "Paused" badge on the child card.
- The parent can see **why** the child's AI access is blocked: if it is due to a parent pause, the parent UI says "you paused this child"; if it is due to a billing lock (P10-15 NoSeat/Locked), the parent UI uses the P10-15 locked copy. The two states have distinct visual treatments and copy.

### Compliance / security
- **IDOR (child data + parent control) — `security-auditor` stage is mandatory.** Family-scope authz on all commands/queries; parent can only act on their own children; child JWT cannot trigger pause/unpause. No cross-family reads.
- **Localized strings only** — no free-text literals. All parent-facing and child-facing messages are `SharedResourcesKey` entries (EN+AR resx). States are enums, not magic strings.
- Pause / unpause are **idempotent**: pausing an already-paused child is a no-op (no duplicate ledger entries, no error); unpausing an Active child is a no-op.

## Cross-reference: contrast with P10-15 (NoSeat/Locked)

| Dimension | P10-18 ParentPause | P10-15 NoSeat/Locked |
|---|---|---|
| Trigger | Parent-initiated, immediate | Billing event (downgrade / seat-cancel / payment-failure) + grace expiry |
| Effect on seat | None (seat stays Active) | Seat removed / NoSeat |
| Effect on energy | None (balance untouched) | Unspent entitlement allocation forfeited |
| Effect on billing | None | Seat removed at renewal; renewal grant recalculates |
| Duration | Until parent unpauses | Until parent re-assigns a seat (or re-subscribes) |
| Reversal | Unpause (immediate, no energy change) | Re-activate (P10-15 `ReactivateChildSeatCommand`) |
| Ledger entry | `ParentPause` / `ParentUnpause` | `SeatLock` / `AllocationForfeit` |

## Backend vs Frontend scope

### BACKEND (P10-18-BE)
- `ParentPauseState` enum `{Active, Paused}` + storage on the Billing child seat/profile record; EF config + migration (schema `billing`).
- `IPauseChildService` in `Billing.Application/Abstractions`; impl in `Billing.Infrastructure/Services`. Two methods: `PauseChildAsync(parentUserId, childId)` + `UnpauseChildAsync(parentUserId, childId)`. Family-scope ownership guard in the service; idempotent.
- CQRS commands `PauseChildCommand` / `UnpauseChildCommand` + handlers (Option C — handlers inject the service only, no EF in Application) + FluentValidation validators.
- `GetChildAccessStatusQuery(parentUserId, childId)` → per-child `{ seatState, parentPauseState }` DTO.
- **Extend the AI spend gate** (currently checks `SeatState` per P10-15-BE-4): additionally check `ParentPauseState == Paused` and return a friendly localized decline (`SharedResourcesKey.ChildAccessPausedByParent`) — no raw error. The spend gate checks BOTH states independently.
- **`Shared.Contracts` seam**: extend `ISeatStateQuery` (or add `IChildAccessStateQuery`) in `Shared.Contracts/Billing` to expose `IsChildAccessAllowedAsync(int childId)` — combines SeatState Active AND ParentPauseState Active — so the AI module can check without referencing Billing internals.
- Ledger: append `ParentPause` / `ParentUnpause` entries on each state change; idempotency guard (no duplicate entries for no-op calls).
- REST controller: `POST /api/Billing/Children/{childId}/Pause`, `POST /api/Billing/Children/{childId}/Unpause`, `GET /api/Billing/Children/{childId}/AccessStatus`. Parent-JWT-only `[Authorize]`; child JWTs rejected; family-scope authz.
- Option C service-only (Application EF-free); no free-text literals; module isolation (childId is a loose `int`, no cross-module FK).

### FRONTEND (P10-18-FE) — parent area only, other (frontend) lead
- **Pause/unpause toggle** on the parent child card / per-child detail (parent area): a clearly labeled control ("Pause AI access" / "Resume AI access") with a **confirmation step** before pause (confirm copy: "Your child will lose AI access immediately; billing is unchanged. Resume any time."). EN+AR, RTL.
- **Paused badge** on the parent child list / child card: visible "AI access paused" badge with distinct visual treatment from the P10-15 NoSeat/Locked badge.
- **Blocked child experience** (child-facing): when a child attempts to use an AI feature while paused, they see a localized friendly message ("Your parent has paused your AI access. Ask your parent to resume it.") — never a raw error.
- Parent-only controls (pause/unpause toggle, Paused badge) are **never rendered on a child route**. The blocked-child message is shown only when the child app calls the AI endpoint and receives the paused decline.
- EN+AR, RTL.

## Dependencies
- **P10-15** (SeatState + AI spend gate) — **upstream**; this story extends the spend gate with a second independent check. Coordinate the spend-gate logic and the `Shared.Contracts` seam.
- **P10-01** (immutable per-child ledger) — `ParentPause` / `ParentUnpause` entries are written here.
- **P10-13** (FamilyEnergyAccount + per-child allocation) — the allocation/energy balances are **untouched** by this story; this dependency is a "do not touch" boundary.
- **P10-14** (seat model) — seat state is **untouched** by this story; same "do not touch" boundary.
- **Ai module** (spend path / AI Tutor feature handlers) — the Shared.Contracts seam (`IChildAccessStateQuery`) is the integration point; the Ai module must call the seam before invoking AI cost logic.

## Notes
- **Locked 2026-06-17.** Pause is a real-time access gate; it has zero effect on billing, seats, or energy. This is a non-negotiable invariant — do not add any energy/billing side-effects to the pause or unpause path.
- **No duplicate-grant guard needed** — because pause never mints or forfeits energy, there is nothing to guard against at unpause.
- **Idempotency is simple**: `PauseChildAsync` on an already-Paused child is a no-op (return success, write no ledger entry). Same for Unpause on an Active child.
- **The spend gate checks BOTH states independently** — `SeatState.NoSeatLocked` OR `ParentPauseState.Paused` each alone blocks AI spend. The gate must not short-circuit one check if the other passes. This ensures Paused children with Active seats are correctly blocked, and NoSeat/Locked children with Active pause state are also correctly blocked.
- **Module isolation (HARD):** Billing owns `ParentPauseState`; Identity owns child profiles. Child is referenced by a loose `int` childId — no cross-module FK. If parent→child family ownership needs cross-module verification, go through `Shared.Contracts` only.
- **No Unit of Work (ADR 0001):** pause/unpause + ledger entry are two writes; wrap in an explicit transaction inside the service (Infrastructure), never in a handler.
- **`ValidationBehavior` runs for `ICommand<>` only** — `PauseChildCommand` and `UnpauseChildCommand` get FluentValidation validators; the access-status query does not.
- **No free-text literals** — child-facing blocked message and all parent-facing messages are `SharedResourcesKey` keys (EN+AR resx); `ParentPauseState` is an enum.
- **Security-auditor is mandatory** (child data + parent control + IDOR risk). Critical/High findings block.
- Cross-reference: the P10-15 Locked copy and the P10-18 Paused copy must be visually and textually distinct in both parent and child surfaces so parents and children can tell them apart.
