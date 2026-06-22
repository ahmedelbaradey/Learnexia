# P10-18 — Pause / unpause a child's access — Backend QC test cases

**Story:** `user-stories/Phase-10-Payments-Billing/P10-18-pause-child-access.md`
**Task:** `tasks/Backend/Phase-10-Payments-Billing/P10-18-BE.md`
**Surface under test:** `ParentPauseState{Active,Paused}` on the Billing child seat record, `IPauseChildService` (`PauseChildAsync`/`UnpauseChildAsync` — family-scope guard, idempotency, ledger `ParentPause`/`ParentUnpause`, zero billing/energy/seat side-effects), the AI spend gate's independent `ParentPauseState` check, the `IChildAccessStateQuery` seam (`SeatState==Active AND ParentPauseState==Active`), `GetChildAccessStatusQuery`, and routes `POST /Children/{childId}/Pause|Unpause`, `GET /Children/{childId}/AccessStatus`.
**Existing suite:** `P10_18_PauseChild_IntegrationTests` (11 tests). **Already QC'd by integration tests** — this doc traces ACs to existing tests and flags gaps. Design-only.

## Money / correctness lenses applied
- **Zero side-effects** — pause/unpause write ONLY `ParentPauseState` + a `ParentPause`/`ParentUnpause` ledger entry; no seat, energy balance, or billing-date write.
- **Independent gate checks** — `SeatState.NoSeatLocked` OR `ParentPauseState.Paused` each alone blocks AI spend; no short-circuit.
- **Idempotency** — re-pause a paused child / re-unpause an active child = no-op (no duplicate ledger).
- **IDOR / authz** — parent can act only on own children; child JWT rejected; cross-family rejected, no mutation.
- **No energy charged** on a paused-blocked AI request (pre-LLM gate).

## Test cases

| ID | Title | Type | Pri | Seed / preconditions | Action | Expected (assertions) | Traces to AC | Existing test |
|----|-------|------|-----|----------------------|--------|-----------------------|--------------|---------------|
| QC-18-01 | Pause happy path — state + ledger, seat/energy unchanged | functional | P0 | parent + active child | `POST /Children/{id}/Pause` | 200; `AccessStatus=Paused`; `ParentPause` ledger row; seat + energy balances unchanged | Pause immediate; no side-effects | `PAUSE_01_Pause_HappyPath` |
| QC-18-02 | Unpause happy path — state + ledger, seat/energy unchanged | functional | P0 | paused child | `POST /Children/{id}/Unpause` | 200; `AccessStatus=Active`; `ParentUnpause` ledger; balances unchanged | Unpause immediate; no side-effects | `PAUSE_02_Unpause_HappyPath` |
| QC-18-03 | Same-day pause→unpause→re-pause — distinct ledger rows | persistence | P1 | active child | pause, unpause, pause | all 200; distinct ledger rows; no 500/collision | Ledger every change; idempotency-key per-day boundary | `PAUSE_08_SameDay_Pause_Unpause_Repause_NoCollision` |
| QC-18-04 | Re-pause a paused child → 200 no-op, no duplicate ledger | negative | P0 | already-paused child | pause again | 200 no-op; no duplicate `ParentPause` row | Idempotent pause | `PAUSE_03a_RePause_Idempotent_NoDuplicateLedger` |
| QC-18-05 | Re-unpause an active child → 200 no-op, no ledger | negative | P0 | already-active child | unpause | 200 no-op; no ledger written | Idempotent unpause | `PAUSE_03b_ReUnpause_Idempotent_NoLedger` |
| QC-18-06 | IDOR — Parent A can't pause/unpause/read B's child | auth-authz | P0 | A's child, B's JWT | B pause/unpause/AccessStatus on A's child | 403; no mutation | Owning-parent scope (IDOR) | `PAUSE_04_IDOR_CrossFamily_Rejected_NoMutation` |
| QC-18-07 | Authz — anon 401; child (Student) JWT 401/403 on all 3 routes | auth-authz | P0 | child JWT | anon + child call 3 routes | anon→401; child→401/403 | Parent-JWT-only; child rejected | `PAUSE_05_AuthZ_AnonymousAndChildRejected` |
| QC-18-08 | Paused child denied AI (pre-LLM gate); no energy charged | functional | P0 | active-seat child, then paused, has energy | child invokes AI feature | declined `ChildAccessPausedByParent`; no energy debited | Spend gate independent pause check | `PAUSE_06_PausedChild_DeniedAI_PreLLMGate_NoEnergyCharged` |
| QC-18-09 | AccessStatus DTO — active seat + not paused → allowed | functional | P1 | active seat, active pause | `GET /AccessStatus` | `isAccessAllowed=true`; `seatState=Active`,`parentPauseState=Active` | Combined access seam | `PAUSE_07a_AccessStatus_ActiveSeat_NotPaused_IsAllowed` |
| QC-18-10 | AccessStatus DTO — active seat + paused → not allowed | functional | P0 | active seat, paused | `GET /AccessStatus` | `isAccessAllowed=false` (paused alone blocks) | Independent pause check | `PAUSE_07b_AccessStatus_ActiveSeat_Paused_NotAllowed` |
| QC-18-11 | AccessStatus DTO — NoSeatLocked + not paused → not allowed | functional | P0 | locked seat, active pause | `GET /AccessStatus` | `isAccessAllowed=false` (locked alone blocks) | Independent seat check | `PAUSE_07c_AccessStatus_NoSeatLocked_NotPaused_NotAllowed` |

## Gaps flagged for `api-tester` (no existing covering test)

- **GAP-18-A (both conditions true — locked AND paused):** AccessStatus is tested for active+paused (07b) and locked+not-paused (07c), but **not the combined `NoSeatLocked AND Paused`** case. The story explicitly calls out all four combinations and warns the gate must not conflate them. **Add a P1** asserting `isAccessAllowed=false` and that unpausing a locked child still yields `false` (seat still blocks). Quick to add.
- **GAP-18-B (unpause does not mint energy / change seat — explicit):** PAUSE-02 asserts balances unchanged generally; **add a P2** explicit assertion that after pause→unpause the energy balance is *exactly* the pre-pause value and the seat state is *exactly* the pre-pause state (the "do not touch" boundary against P10-13/P10-14).
- **GAP-18-C (validation):** `PauseChildCommand`/`UnpauseChildCommand` have validators (`childId > 0`). No test asserts `childId <= 0` → 422. **Add a P2** validation case.
- **Better suited to unit tests:** the `IChildAccessStateQuery.IsChildAccessAllowedAsync` boolean truth-table (Active/Active=true; every other combination=false) — pin all four combinations cheaply as a unit test of the seam predicate.
