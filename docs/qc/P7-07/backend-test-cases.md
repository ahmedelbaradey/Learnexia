# P7-07 — Suspend / Reactivate / Delete accounts — Backend (API) test cases

**Target agent:** `api-tester`
**Surface:** `AdminUsersController` write endpoints, class-level `AdminOnly`.
- `POST   api/Admin/Users/{id}/suspend`     — `SuspendAccountCommand` (ICommand → validated → 422)
- `POST   api/Admin/Users/{id}/reactivate`  — `ReactivateAccountCommand`
- `DELETE api/Admin/Users/{id}`             — `DeleteAccountCommand` (confirm-gate → 424)
**Baseline:** `P7_06_07_08_UserAccountAdmin_Tests.cs`. Audit infra: `P7_12_AuditLog_Tests.cs` (only `Subject.Created`).
**State machine:** `AccountStatus` 0=Active, 1=Suspended, 2=Deleted; Deleted is terminal. Suspend also flips `IsActive=false`, revokes the Redis refresh token, and emits cross-module integration events.

Legend: **Covered** (cite method) / **GAP** / **PARTIAL**.

---

## A. Auth / authz matrix

| ID | Title | Type | Pri | Steps | Expected | Status / existing test |
|----|-------|------|-----|-------|----------|------------------------|
| BE-TC-07-01 | POST suspend anonymous → 401 | auth | P0 | POST `…/1/suspend` no bearer | 401 | **Covered** — `P707_Suspend_Anonymous_Returns401` |
| BE-TC-07-02 | POST suspend parent → 403 | authz | P0 | parent bearer | 403 | **Covered** — `P707_Suspend_Parent_Returns403` |
| BE-TC-07-03 | POST suspend basic-role → 403 | authz | P1 | basic bearer | 403 | **GAP** — basic variant missing |
| BE-TC-07-04 | POST reactivate anonymous → 401 | auth | P0 | POST `…/1/reactivate` no bearer | 401 | **Covered** — `P707_Reactivate_Anonymous_Returns401` |
| BE-TC-07-05 | POST reactivate parent → 403 | authz | P0 | parent bearer | 403 | **GAP** — reactivate parent-403 never tested |
| BE-TC-07-06 | POST reactivate basic-role → 403 | authz | P1 | basic bearer | 403 | **GAP** |
| BE-TC-07-07 | DELETE anonymous → 401 | auth | P0 | DELETE `…/1` no bearer | 401 | **Covered** — `P707_Delete_Anonymous_Returns401` |
| BE-TC-07-08 | DELETE parent → 403 | authz | P0 | parent bearer | 403 | **GAP** — delete parent-403 never tested |
| BE-TC-07-09 | DELETE basic-role → 403 | authz | P1 | basic bearer | 403 | **GAP** |
| BE-TC-07-10 | Suspend/Delete with expired admin JWT → 401 | auth | P1 | expired admin token | 401, not 500 | **GAP** — invalid-token on write paths untested |

---

## B. Suspend — validation, happy path, session revocation

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-07-20 | Empty reason → 422 | validation | P0 | admin | POST `…/{id}/suspend` `{reason:""}` | 422; successed=false | **Covered** — `P707_Suspend_EmptyReason_Returns422` |
| BE-TC-07-21 | Reason > 500 chars → 422 | boundary | P0 | admin | reason = 501 chars | 422; successed=false | **Covered** — `P707_Suspend_ReasonTooLong_Returns422` |
| BE-TC-07-22 | Missing reason field (null) → 422 | validation | P1 | admin | POST `{}` (no reason) | 422 | **GAP** — null/omitted reason (vs empty string) untested |
| BE-TC-07-23 | Reason at boundary (500 chars) → 200 | boundary | P2 | suspendable user | reason = exactly 500 chars | 200; suspended | **GAP** — upper boundary accept untested |
| BE-TC-07-24 | Happy path: AccountStatus→Suspended, sign-in blocked | functional | P0 | parent w/ known pw | suspend → attempt sign-in | suspend 200; sign-in ≠ 200, successed=false | **Covered** — `P707_Suspend_HappyPath_AccountBecomesSuspended_SignInBlocked` |
| BE-TC-07-25 | Suspend revokes refresh token (cannot refresh after) | auth/persistence | P0 | parent w/ tokens | suspend → POST Refresh-Token | refresh ≠ 200, successed=false | **Covered** — `P707_Suspend_RefreshTokenInvalidated` |
| BE-TC-07-26 | Suspend persists IsActive=false + AccountStatus=Suspended in DB | persistence | P1 | parent | suspend → query DB | `IsActive=false`, `AccountStatus=Suspended(1)` | **GAP** — DB-state assertion (only sign-in behaviour tested) |
| BE-TC-07-27 | Suspend persists LastStatusReason + StatusChangedAtUtc | persistence | P1 | parent | suspend w/ reason "X" → GET profile | `lastStatusReason="X"`, `statusChangedAtUtc` non-null | **GAP** — reason/timestamp persistence untested (AC: "prior reason/history remains visible") |
| BE-TC-07-28 | Envelope shape on suspend success | functional | P1 | parent | suspend | envelope keys present | **Covered** — `Envelope_SuspendResponse_HasAllBaseResponseKeys` |
| BE-TC-07-29 | Suspend non-existent id → 404 | negative | P0 | admin | POST `…/99999999/suspend` | 404 or successed=false | **GAP** — not-found path on suspend untested |

---

## C. Suspend — illegal-transition + self/SuperAdmin protection

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-07-30 | Suspend already-Suspended → rejected | state | P0 | suspend once | suspend again | rejected (4xx or 200 successed=false) | **Covered** — `P707_Suspend_AlreadySuspended_Rejected` |
| BE-TC-07-31 | Suspend a Deleted account → rejected (terminal) | state | P0 | delete user | suspend it | rejected | **Covered** — `P707_Suspend_DeletedAccount_Rejected` |
| BE-TC-07-32 | Admin cannot suspend self | auth-authz | P0 | admin id from Me | suspend own id | rejected | **Covered** — `P707_Suspend_OwnAccount_Rejected` |
| BE-TC-07-33 | Admin cannot suspend SuperAdmin | auth-authz | P0 | superadmin id | suspend it | rejected | **Covered** — `P707_Suspend_SuperAdmin_Rejected` (skips if id unfindable) |

---

## D. Reactivate

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-07-40 | Reactivate restores sign-in | functional | P0 | suspend user | reactivate → sign-in | reactivate 200; sign-in 200 successed=true | **Covered** — `P707_Reactivate_RestoresSignIn` |
| BE-TC-07-41 | Reactivate sets AccountStatus=Active, IsActive=true (DB) | persistence | P1 | suspended user | reactivate → query DB | `AccountStatus=Active(0)`, `IsActive=true` | **GAP** — DB-state assertion missing |
| BE-TC-07-42 | Reactivate a Deleted account → rejected (terminal) | state | P0 | delete user | reactivate it | rejected | **Covered** — `P707_Reactivate_DeletedAccount_Rejected` |
| BE-TC-07-43 | Reactivate an already-Active account → rejected / no-op | state | P1 | active user | reactivate it | rejected or 200 no-op (assert no error/500) | **GAP** — reactivate-active path untested |
| BE-TC-07-44 | Reactivate non-existent id → 404 | negative | P1 | admin | POST `…/99999999/reactivate` | 404 / successed=false | **GAP** |

---

## E. Delete — confirm-gate, validation, soft-delete, cascade

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-07-50 | confirm=false → 424 FailedDependency (no mutation) | negative/gate | P0 | parent | DELETE `{confirm:false}` | 424; successed=false | **Covered** — `P707_Delete_ConfirmFalse_Returns424` |
| BE-TC-07-51 | confirm=false leaves account intact (no DB mutation) | persistence | P1 | parent | DELETE `{confirm:false}` → query DB | `IsDeleted=false`, `AccountStatus=Active` | **GAP** — the gate test asserts 424 but not "no mutation" |
| BE-TC-07-52 | Empty reason → 422 | validation | P0 | admin | DELETE `{reason:"",confirm:true}` | 422; successed=false | **Covered** — `P707_Delete_EmptyReason_Returns422` |
| BE-TC-07-53 | Soft-delete hides user from default search | persistence | P0 | parent | DELETE confirm=true → search | user absent from search | **Covered** — `P707_Delete_SoftDeletes_HiddenFromDefaultSearch` |
| BE-TC-07-54 | Soft-delete NOT physical (row + IsDeleted=true remain) | persistence | P0 | parent | DELETE confirm=true → DB IgnoreQueryFilters | row exists, `IsDeleted=true` | **Covered** — `P707_Delete_SoftDelete_NotPhysicallyRemoved` |
| BE-TC-07-55 | Delete sets AccountStatus=Deleted, IsActive=false | persistence | P1 | parent | DELETE confirm=true → DB | `AccountStatus=Deleted(2)`, `IsActive=false` | **GAP** — only IsDeleted asserted; status/IsActive not |
| BE-TC-07-56 | Deleted user cannot sign in | auth | P1 | parent w/ pw | DELETE → attempt sign-in | sign-in ≠ 200 | **GAP** — sign-in block after delete untested |
| BE-TC-07-57 | Delete already-Deleted → rejected (terminal) | state | P0 | delete user | delete again | rejected | **Covered** — `P707_Delete_AlreadyDeleted_Rejected` |
| BE-TC-07-58 | Cascade: delete parent with cascadeChildren=true → children soft-deleted | persistence | P0 | parent + child | DELETE `{cascadeChildren:true}` → DB | child `IsDeleted=true` | **Covered** — `P707_Delete_CascadeChildren_ChildrenAlsoDeleted` |
| BE-TC-07-59 | Cascade=false: delete parent leaves children active | persistence | P1 | parent + child | DELETE `{cascadeChildren:false}` → DB | child `IsDeleted=false`/active | **GAP** — the non-cascade branch is untested (asserts cascade flag is honoured both ways) |
| BE-TC-07-60 | Cascade atomicity: child delete in same transaction (no orphan half-state) | persistence | P2 | parent + 2 children | DELETE cascade=true → DB both children | both children IsDeleted=true (all-or-nothing) | **GAP** — multi-child transactional cascade untested |
| BE-TC-07-61 | Delete non-existent id → 404 | negative | P1 | admin | DELETE `…/99999999` confirm=true | 404 / successed=false | **GAP** |
| BE-TC-07-62 | Admin cannot delete self | auth-authz | P0 | admin id | DELETE own id | rejected | **Covered** — `P707_Delete_OwnAccount_Rejected` |
| BE-TC-07-63 | Admin cannot delete SuperAdmin | auth-authz | P0 | superadmin id | DELETE it | rejected | **Covered** — `P707_Delete_SuperAdmin_Rejected` (skips if unfindable) |

---

## F. Cross-module integration events (AC-4) — DESTRUCTIVE notification fan-out

> AC-4: "other modules are notified via integration events to clean up their data (no direct cross-module writes)." The account-lifecycle events are consumed by Gamification + Parent + Learning. Existing tests assert sign-in/refresh revocation but **not** that downstream consumers received the events.

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-07-70 | Suspend emits AccountSuspendedIntegrationEvent (observable side-effect) | persistence/event | P1 | parent w/ child gamification state | suspend → assert downstream consumer effect (e.g. session/state flagged) or audit row | event consumed; no 500; consumer state reflects suspension | **GAP** — event fan-out not verified beyond token revoke |
| BE-TC-07-71 | Delete emits AccountDeletedIntegrationEvent | persistence/event | P1 | parent + child | delete cascade → assert consumer cleanup | downstream cleanup observable; no orphaned cross-module rows error | **GAP** — best-effort scope; confirm at least no 500 + audit row |

---

## G. Audit trail (P7-12) — AC-5 "actor, timestamp, target, reason" — CRITICAL

> P7-07 AC-5: "Every lifecycle action records actor, timestamp, target, and reason and is audited (P7-12)." Handlers emit `Account.Suspended/Reactivated/Deleted`. **No test verifies these audit rows.** Requires Moderation migration applied + poll on `GET api/Admin/Audit/Log`.

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-07-80 | Suspend emits `Account.Suspended` audit row | audit | P0 | parent | suspend reason="abuse" → poll `?actionType=Account.Suspended&adminUserId={adminId}` | row: action=`Account.Suspended`, targetEntityType=`User`, targetEntityId={id}, correct adminUserId | **GAP** |
| BE-TC-07-81 | Reactivate emits `Account.Reactivated` audit row | audit | P0 | suspended user | reactivate → poll `?actionType=Account.Reactivated` | matching row | **GAP** |
| BE-TC-07-82 | Delete emits `Account.Deleted` audit row | audit | P0 | parent | delete confirm=true → poll `?actionType=Account.Deleted` | matching row | **GAP** |
| BE-TC-07-83 | Audit Details for lifecycle actions carries no PII (ids/reason-meta only, no email) | PII/audit | P1 | as above | inspect Details of the 3 rows | no `@`/email/password; ids + enum/reason metadata only | **GAP** |
| BE-TC-07-84 | confirm=false delete (424) emits NO `Account.Deleted` audit row | audit/gate | P1 | parent | DELETE confirm=false → poll | no `Account.Deleted` row for that target (gate fired before mutation) | **GAP** — verifies the gate runs before audit emission |

---

## Summary — P7-07 backend

- **Covered:** 18 cases (auth anon/parent for suspend, full suspend validation+happy+revoke+illegal-transition+self/superadmin, reactivate restore+deleted-reject, delete 424-gate+empty-reason+soft-delete×2+already-deleted+cascade-true+self/superadmin).
- **GAP:** 27 cases — most valuable: **audit-trail rows for suspend/reactivate/delete (80–84), reactivate/delete parent+basic 403 (05/06/08/09), non-cascade branch (59), DB-state persistence after suspend/reactivate/delete (26/27/41/55), not-found paths (29/44/61), confirm=false no-mutation (51), deleted-cannot-sign-in (56), integration-event fan-out (70/71).**
- **Headline gap count: 27.**
