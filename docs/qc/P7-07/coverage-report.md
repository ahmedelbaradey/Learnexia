# P7-07 — Coverage Report (Suspend / Reactivate / Delete)

**Story:** `user-stories/Phase-7-Admin-Console/P7-07-suspend-reactivate-delete-accounts.md`
**Controller:** `AdminUsersController` (suspend/reactivate/delete) — `AdminOnly`.
**Baseline:** `P7_06_07_08_UserAccountAdmin_Tests.cs` + `P7_12_AuditLog_Tests.cs`.

## Acceptance-criterion → test-case matrix

| AC | Criterion (abridged) | Covering cases | Verdict |
|----|----------------------|----------------|---------|
| AC-1 | Suspend flips status, **revokes sessions/token**, requires typed confirm + reason | 07-20, 07-21, 07-24, 07-25 (covered) + **07-22/23/26/27 (GAP)** | Core covered; DB-state + reason persistence + boundary gaps |
| AC-2 | Reactivate restores sign-in; prior reason/history visible | 07-40 (covered) + **07-41 (DB), 07-27 (reason visible after) (GAP)** | Sign-in restore covered; "history remains visible" gap |
| AC-3 | Suspend/delete parent → warn + cascade to linked children | 07-58 (cascade=true covered) + **07-59 (cascade=false), 07-60 (atomic) (GAP)** | Cascade-true covered; non-cascade branch is a gap |
| AC-4 | Delete two-step confirm + reason; other modules notified via integration events | 07-50, 07-52, 07-53, 07-54 (covered) + **07-51, 07-55, 07-56, 07-70, 07-71 (GAP)** | Gate + soft-delete covered; **event fan-out + no-mutation gaps** |
| AC-5 | Every action records actor/timestamp/target/reason, **audited (P7-12)**; already-deleted rejected | 07-30, 07-31, 07-42, 07-57 (rejections covered) + **07-80..07-84 (audit GAP — none covered)** | Rejections covered; **AUDIT entirely uncovered — top gap** |
| AC-6 | Only admin; non-admin → 403 | 07-01, 07-02, 07-04, 07-07 (covered) + **07-03/05/06/08/09/10 (GAP)** | Anonymous covered for all 3; **reactivate/delete 403 + basic-role gaps** |
| (self/superadmin) | Self-protection + SuperAdmin protection | 07-32, 07-33, 07-62, 07-63 (covered) | Covered |

## Prioritized backend gap list for `api-tester`

**P0 (do first):**
1. **BE-TC-07-80 / 81 / 82 / 84 — audit rows for `Account.Suspended` / `Account.Reactivated` / `Account.Deleted`, and gate-before-audit (84)** (AC-5). Highest value; nothing verifies the audit trail for lifecycle actions today.
2. **BE-TC-07-05 / 07-08 — reactivate-parent-403 and delete-parent-403** (AC-6). Only suspend has the parent-403 case; reactivate+delete only test anonymous.
3. **BE-TC-07-29 / 44 / 61 — not-found (99999999) on suspend / reactivate / delete.**

**P1:**
4. BE-TC-07-83 — audit Details PII-safety for lifecycle rows.
5. BE-TC-07-59 — delete parent with cascadeChildren=false leaves children active.
6. BE-TC-07-26 / 27 — suspend persists IsActive=false + AccountStatus + LastStatusReason + StatusChangedAtUtc.
7. BE-TC-07-41 — reactivate persists Active + IsActive=true.
8. BE-TC-07-55 / 56 — delete sets AccountStatus=Deleted + IsActive=false; deleted user cannot sign in.
9. BE-TC-07-51 — confirm=false delete leaves account intact (no mutation).
10. BE-TC-07-43 — reactivate an already-active account (no error/500).
11. BE-TC-07-22 — missing/null reason → 422.
12. BE-TC-07-70 / 71 — integration-event fan-out observable (or at least no 500 + consumer effect).
13. BE-TC-07-03 / 06 / 09 — basic-role 403 on all three actions.
14. BE-TC-07-10 — expired admin JWT on write paths → 401.

**P2:** 07-23 (reason 500-char boundary accept), 07-60 (multi-child transactional cascade).

## Headline counts
- Total backend cases: **45** — Covered **18**, GAP **27**.
- By priority: P0 = 18, P1 = 21, P2 = 6.
- Frontend reference cases: **11** (admin-dashboard; likely Blocked if UI not built).
