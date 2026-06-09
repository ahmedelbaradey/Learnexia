# Execution Plan — P7-06 / P7-07 / P7-08 (User/Account Wave, Backend Only)

> Written by `planner` (2026-06-08). Backend-only plan for the Phase-7 Admin Console **User/Account wave**.
> Branch: `feat/phase-7-backend` (ongoing wave PR — commits fold into the existing PR, no new PR).
> No design stage (backend-only wave). No FE batch. Designer/FE deferred to the separate P7-0x-FE stories.

---

## Source

| Artifact | Path |
|---|---|
| Pipeline Brief P7-06 | `docs/briefs/P7-06.md` |
| Pipeline Brief P7-07 | `docs/briefs/P7-07.md` |
| Pipeline Brief P7-08 | `docs/briefs/P7-08.md` |
| Gap Analysis | `docs/briefs/phase-7-admin-gap-analysis.md` |
| BE task file P7-06 | `tasks/Backend/Phase-7-Admin-Console/P7-06-BE.md` |
| BE task file P7-07 | `tasks/Backend/Phase-7-Admin-Console/P7-07-BE.md` |
| BE task file P7-08 | `tasks/Backend/Phase-7-Admin-Console/P7-08-BE.md` |
| Identity module source | `backend/src/Modules/Identity/**` (verified on disk) |
| Shared.Contracts source | `backend/src/Shared/Learnexia.Shared.Contracts/**` (verified on disk) |
| CONVENTIONS.md | `docs/dev/CONVENTIONS.md` |

---

## Lead decisions baked in (do not re-litigate)

| Decision | Detail |
|---|---|
| P7-07 delete = SOFT-DELETE/disable | Retain row + linked children + learning history; reversible by admin. `AccountStatus = Deleted`. |
| P7-07 status model | New `AccountStatus` enum `{ Active=0, Suspended=1, Deleted=2 }` on `User`. Distinct from `IsActive` (mechanical gate) and `LockoutEnd` (auto-lockout). |
| P7-07 session revocation (MVP) | Suspend/delete sets `AccountStatus`, sets `IsActive = false`, removes `userrefreshtoken-{userId}` from Redis, and terminates tracked sessions via `SessionManagementService`. Existing short-lived access tokens expire naturally. `OnTokenValidated` revocation hook is explicitly deferred (G2 follow-up). |
| P7-07 integration events | `AccountSuspendedIntegrationEvent`, `AccountReactivatedIntegrationEvent`, `AccountDeletedIntegrationEvent` in `Shared.Contracts/Identity/` (publish-only; consumers in Learning, Gamification, Parent). |
| P7-07 confirm-gate status code | Unconfirmed `DeleteAccountCommand` returns `BusinessValidation` → HTTP 424. Consistent with P8-04. |
| P7-08 grade override = non-destructive | PRESERVE all learning history/progress; re-scope curriculum only via `ChildGradeChanged` event. NO fresh-start, NO 424 gate (soft confirm flag only). |
| P7-08 `ChildGradeChanged` ownership | P7-08 introduces `ChildGradeChangedIntegrationEvent` in `Shared.Contracts/Identity/`; P5-06 reuses it. |
| P7-06 minimal child PII in list | `AdminUserListItemDto` carries only name/email/role/status/created; child grade/languages/country only in the single-profile detail. |
| P7-06 route shape | New `AdminUsersController` (attribute route `api/Admin/Users`) — cleanly separates the REST shape the FE contract expects from the legacy `api/Users/UserManagement/[Action]` controller. P7-07 and P7-08 hang their write endpoints on this same controller. |
| P7-06 profile DTO | New admin-specific `AdminUserProfileDto` — keeps self-service `/Me`-style and admin-inspect projections independent. |
| P7-06 status pre-P7-07 | Search/filter uses `IsActive` until P7-07 lands; P7-07 migration upgrades the filter to `AccountStatus`. |
| P7-12 audit log | Out of scope this wave (deferred). `AdminActionPerformedEvent` is publish-only. |
| Parent seam gap | `IParentChildQuery` today only has `ParentHasAnyChildAsync`, `IsParentOfChildAsync`, `FindParentForChildAsync` (returns ONE parent). Two new methods must be added: `GetChildIdsForParentAsync(parentId)` and `GetParentIdsForChildAsync(childId)`. These are **serialized shared-file edits** (per PARALLELISM.md) and are the long-pole prerequisite for P7-06-BE-3 and P7-07-BE-4. |
| `LastSignInAtUtc` column | Lead decision deferred / not required for this wave. Activity summary sources last-active from Gamification/Learning seams and session store only; sign-in timestamp is labeled "not tracked" in the DTO. No migration for P7-06. |
| P7-08 confirm-gate for grade override | Soft UX confirm flag (HTTP 200, not 424). Grade override is non-destructive so a hard 424 gate is not warranted. |
| `ChildGradeChanged` event placement | `Shared.Contracts/Identity/ChildGradeChangedIntegrationEvent.cs`, implementing `IIntegrationEvent` with `EventId`/`OccurredOnUtc`. |

---

## Task inventory

| ID | Story | Stack | Summary | Est (h) | Depends on |
|---|---|---|---|---|---|
| **SHARED-CONTRACTS-P7-06** | P7-06 | Shared.Contracts | Add `GetChildIdsForParentAsync` + `GetParentIdsForChildAsync` to `IParentChildQuery`; implement in `Parent.Infrastructure`. Serialized shared-file edit. | 2 | — |
| **P7-06-BE-1** | P7-06 | Identity | `SearchUsersQuery` + handler — paginated, role/status/free-text-over-name+email filter; excludes `IsDeleted`; returns `PaginatedResult<AdminUserListItemDto>` (minimal PII). Status filter on `IsActive` (upgraded to `AccountStatus` by P7-07). Validate inputs in-handler (query, not auto-validated). | 4 | — |
| **P7-06-BE-2** | P7-06 | Identity | `GetAdminUserProfileQuery` + handler — admin-specific `AdminUserProfileDto` with both `PreferredLanguage` + `LearningLanguage` shown distinctly; grade/country for children. Emit `AdminActionPerformedEvent` (`AdminActions.UserViewed`) post-read (best-effort). | 3 | P7-06-BE-1 |
| **P7-06-BE-3** | P7-06 | Identity | `GetUserFamilyQuery` + handler — children[] for parent, parents[] for child, via `IParentChildQuery.GetChildIdsForParentAsync` / `GetParentIdsForChildAsync` + `IChildAccountService.GetChildrenAsync`. | 3 | SHARED-CONTRACTS-P7-06 |
| **P7-06-BE-4** | P7-06 | Identity | `GetUserActivitySummaryQuery` + handler — compose from `IStudentXpQuery`, `IStudentStreakQuery`, `IStudentBadgesQuery`, `IStudentMissionsQuery`, `IStudentLeagueQuery` (Gamification seams); each is best-effort (missing/throwing = no data, never 500). No `LastSignInAtUtc` column (label as "not tracked"). | 4 | P7-06-BE-2 |
| **P7-06-BE-5** | P7-06 | Identity | New `AdminUsersController` (route `api/Admin/Users`, already `AdminOnly` via policy) with four `[HttpGet]` actions: search, profile, family, activity. Add `AdminActions.UserViewed` + `AdminActions.UserSearched` constants to `Shared.Contracts/Admin/AdminActions.cs`. Class-level `[Authorize(AdminOnly)]` on controller. | 2 | P7-06-BE-1..4 |
| **SHARED-CONTRACTS-P7-07** | P7-07 | Shared.Contracts | Add `AccountSuspendedIntegrationEvent`, `AccountReactivatedIntegrationEvent`, `AccountDeletedIntegrationEvent` in `Shared.Contracts/Identity/`. Serialized shared-file edit. | 2 | — |
| **P7-07-BE-1** | P7-07 | Identity (db-migration) | Add `AccountStatus` (int NOT NULL DEFAULT 0), `LastStatusReason` (varchar nullable, max 500), `StatusChangedBy` (int nullable), `StatusChangedAtUtc` (timestamptz nullable) to `User`. Backfill existing rows → `Active`. Configure in `UserEntityConfig`. Migration name `P7_07_AddAccountStatus`. Use `DateTime.UtcNow` and `.ToUniversalTime()` per Npgsql gotcha. | 3 | — |
| **P7-07-BE-2** | P7-07 | Identity | `SuspendAccountCommand(int UserId, string Reason)` + handler + validator. Sets `AccountStatus = Suspended` + `IsActive = false` + stamps reason/actor/time. Revokes sessions: (a) `IsActive=false` blocks next sign-in; (b) Redis `IDistributedCache.RemoveAsync("userrefreshtoken-{userId}")` invalidates refresh token; (c) `SessionManagementService.TerminateSessionAsync` for each tracked session. Validator: reason required + bounded (max 500 chars); reject if `AccountStatus == Deleted`. | 3 | P7-07-BE-1 |
| **P7-07-BE-3** | P7-07 | Identity | `ReactivateAccountCommand(int UserId, string? Reason)` + handler + validator. `Suspended → Active`; restore `IsActive = true`; stamp actor/time. Validator: reject if `AccountStatus == Deleted` (deleted is terminal). | 2 | P7-07-BE-1 |
| **P7-07-BE-4** | P7-07 | Identity | `DeleteAccountCommand(int UserId, string Reason, bool Confirm, bool? CascadeChildren)` + handler + validator. Unconfirmed → `BusinessValidation` (424). Soft-delete: `AccountStatus = Deleted`, `IsActive = false`, stamp reason/actor/time, revoke sessions/refresh-token. Parent with `CascadeChildren = true`: cascade to linked children via `IParentChildQuery.GetChildIdsForParentAsync` — multi-row write on parent + children in an **explicit transaction** (no UoW). | 4 | P7-07-BE-1, SHARED-CONTRACTS-P7-06 |
| **P7-07-BE-5** | P7-07 | Identity + consumers | Publish `AccountSuspended/Reactivated/Deleted` events **post-commit, best-effort** (mirror `PublishLearningLanguageChangedEventAsync` pattern). Wire `INotificationHandler` consumers: Gamification (freeze/clean XP/streak/league on suspend/delete); Parent (update family/linkage view on delete); Learning (thin lock/no-op stub for now — Q-B1 notes delete semantics deferred for learning-data purge). Each consumer in its own try/catch per ADR 0002 §3, idempotent. | 3 | P7-07-BE-2..4, SHARED-CONTRACTS-P7-07 |
| **P7-07-BE-6** | P7-07 | Identity | Add three `[HttpPost/Delete]` write actions to `AdminUsersController` (suspend, reactivate, delete). Add `AdminActions.AccountSuspended`, `AccountReactivated`, `AccountDeleted` constants to `AdminActions.cs`. Emit `AdminActionPerformedEvent` (actor/target/reason) post-commit for each lifecycle action. Upgrade `SearchUsersQuery` status filter to `AccountStatus` (remove the `IsActive` proxy). | 2 | P7-07-BE-2..5 |
| **SHARED-CONTRACTS-P7-08** | P7-08 | Shared.Contracts | Add `ChildGradeChangedIntegrationEvent(Guid EventId, DateTime OccurredOnUtc, int ChildId, int FromGrade, int ToGrade, DateTime ChangedAtUtc)` in `Shared.Contracts/Identity/`. Serialized shared-file edit. | 2 | — |
| **P7-08-BE-1** | P7-08 | Identity | `UpdateChildProfileCommand(int ChildId, string? PreferredLanguage, string? Country)` + handler + validator. Plain field update of UI language (normalized to culture code `ar-EG`/`en-US`) + Nationality. No event, no progress impact. Admin gated by `AdminOnly`; no family-link restriction. | 3 | — |
| **P7-08-BE-3** | P7-08 | Identity | `OverrideChildGradeCommand(int ChildId, int Grade, string Reason, bool Confirm)` + handler + validator. Validate grade ∈ 1..6. Set `User.Grade`; commit; post-commit best-effort publish `ChildGradeChangedIntegrationEvent(ChildId, oldGrade, newGrade, UtcNow)`. Confirm flag is a soft UX guard (HTTP 200 — not 424). History preserved: consumers must NOT delete XP/badges/streaks. | 3 | P7-08-BE-1, SHARED-CONTRACTS-P7-08 |
| **P7-08-BE-4** | P7-08 | Learning | `ChildGradeChangedIntegrationEvent` consumer in Learning — check whether any persisted state needs grade-scoped rewriting. If learning reads key off `User.Grade` live at query time (verify), handler is a no-op stub with audit log; otherwise recompute skill-tree availability for new grade without deleting mastery/attempts. Wire in Learning.Application `INotificationHandler`. | 2 | P7-08-BE-3, SHARED-CONTRACTS-P7-08 |
| **P7-08-BE-5** | P7-08 | Identity | Endpoint wiring in `AdminUsersController` (profile PATCH, grade POST); emit `AdminActionPerformedEvent` (`AdminActions.ChildGradeOverridden`, `AdminActions.ChildProfileUpdated`) post-commit. Add constants to `AdminActions.cs`. | 2 | P7-08-BE-1..4 |
| **P7-08-BE-6** | P7-08 | Identity | `AdminChangeLearningLanguageCommand(int ChildId, string LearningLanguage, bool Confirm)` + handler + validator. Guard order: (1) `Confirm == false` → `BusinessValidation` (424) FIRST; (2) validate `LearningLanguage` ∈ {ar,en}, reject no-op same-lang; (3) call `IChildAccountService.ChangeLearningLanguageAsync(childId, lang)` — seam handles commit + `LearningLanguageChangedIntegrationEvent` publish; (4) map seam error codes to `NotFound`/`BadRequest`/`Success`; (5) emit `AdminActionPerformedEvent` (`AdminActions.ChildLearningLanguageChanged`). | 4 | P7-08-BE-5 |
| **P7-08-BE-7** | P7-08 | Learning + Gamification | Reuse existing `LearningLanguageChangedIntegrationEventHandler` (already hard-deletes Math/Science attempts). No new code required — verify the handler fires for admin-path publishes. Confirm Gamification handler is already wired for re-scope; if not, add a stub. | 2 | P7-08-BE-6 |

**Totals by story:** P7-06 ≈ 16h (incl. 2h shared-contracts); P7-07 ≈ 19h (incl. 2h shared-contracts); P7-08 ≈ 18h (incl. 2h shared-contracts). Wave total ≈ 53h.

---

## Dependency order (topological)

```
SHARED-CONTRACTS-P7-06  ──→  P7-06-BE-3
                         ──→  P7-07-BE-4

P7-06-BE-1  ──→  P7-06-BE-2  ──→  P7-06-BE-4
P7-06-BE-1..4  ──→  P7-06-BE-5

SHARED-CONTRACTS-P7-07  ──→  P7-07-BE-5

P7-07-BE-1  ──→  P7-07-BE-2
            ──→  P7-07-BE-3
            ──→  P7-07-BE-4  (also needs SHARED-CONTRACTS-P7-06)

P7-07-BE-2..5  ──→  P7-07-BE-6  (also upgrades P7-06 status filter)

SHARED-CONTRACTS-P7-08  ──→  P7-08-BE-3
                         ──→  P7-08-BE-4

P7-08-BE-1  ──→  P7-08-BE-3  ──→  P7-08-BE-5  ──→  P7-08-BE-6
P7-08-BE-3 + SHARED-CONTRACTS-P7-08  ──→  P7-08-BE-4
P7-08-BE-6  ──→  P7-08-BE-7  (verify, not re-build)
```

**Critical path:** SHARED-CONTRACTS-P7-06 → P7-07-BE-4 (cascade delete) is the primary cross-story dependency; implement the seam extension as the very first step of the wave.

---

## Execution batches

### Batch 0 — Serialized shared-file edits (sequential, MUST land first)

**Agent:** `backend-feature` (shared-contracts author; one agent, one commit per contract edit to avoid merge conflicts)

These three tasks touch `Shared.Contracts` (a shared project) and `Parent.Infrastructure`/`IParentChildQuery`. Per PARALLELISM.md, shared-file edits serialize.

**Step 0-A (do first):**
- `SHARED-CONTRACTS-P7-06` — extend `IParentChildQuery` with `GetChildIdsForParentAsync(int parentId)` + `GetParentIdsForChildAsync(int childId)` in `Shared.Contracts/Parent/IParentChildQuery.cs`; implement in `Parent.Infrastructure`.

**Step 0-B (after 0-A, or in parallel with 0-A if they touch different files — these touch different files so CAN run in parallel):**
- `SHARED-CONTRACTS-P7-07` — add three `Account*IntegrationEvent` records in `Shared.Contracts/Identity/`.
- `SHARED-CONTRACTS-P7-08` — add `ChildGradeChangedIntegrationEvent` record in `Shared.Contracts/Identity/`.

In practice: do all three in one `backend-feature` pass if the agent is careful about file boundaries (0-A touches `Parent/IParentChildQuery.cs` + `Parent.Infrastructure`; 0-B touches `Identity/Account*.cs` + `Identity/ChildGradeChanged*.cs`). Commit as one atomic shared-contracts commit.

**Gating note:** Nothing in Batch 1 or later may start until Batch 0 is committed and the build is green.

---

### Batch 1 — P7-07 migration (sequential, before P7-07 feature work)

**Agent:** `db-migration`

- `P7-07-BE-1` — generate and apply `P7_07_AddAccountStatus` migration in `Identity.Infrastructure/Migrations/`. Columns: `AccountStatus int NOT NULL DEFAULT 0`, `LastStatusReason varchar(500) nullable`, `StatusChangedBy int nullable`, `StatusChangedAtUtc timestamptz nullable`. Backfill existing rows → `Active (0)`. Configure in `UserEntityConfig`. Do NOT alter the existing `IsActive` column — it stays as the sign-in mechanical gate (synced by suspend/reactivate/delete handlers).

**Review gate:** reviewer checks migration SQL (backfill, column types, Npgsql timestamptz, no FK on StatusChangedBy), build still green, snapshot updated.

---

### Batch 2 — P7-06 backend feature (largely parallel-internal, sequential on the seam)

**Agent:** `backend-feature`

All four query handlers + DTOs + controller + audit constants for P7-06. Internal ordering:

- **2-A (parallel):** `P7-06-BE-1` (SearchUsersQuery) + `P7-06-BE-3` (GetUserFamilyQuery — now unblocked by Batch 0).
- **2-B (after 2-A):** `P7-06-BE-2` (GetAdminUserProfileQuery — depends on BE-1 for the DTO pattern established).
- **2-C (after 2-B):** `P7-06-BE-4` (GetUserActivitySummaryQuery — depends on BE-2's DTO pattern + must degrade gracefully).
- **2-D (after 2-A..C):** `P7-06-BE-5` — create `AdminUsersController` with all four actions + `AdminActions.UserViewed`/`UserSearched` constants.

**Key implementation notes for backend-feature agent:**
- Return type for search: `PaginatedResult<AdminUserListItemDto>` (the house type via `ToPaginatedListAsync` — NOT a bespoke `PagedResult<T>`).
- Free-text filter: `FullName.Contains(q) || Email.Contains(q)` — EF-translatable; `Email` is on `IdentityUser<int>` base.
- Status filter at this point maps to `IsActive` (before P7-07 lands `AccountStatus`). Note this in a TODO comment: "Upgrade to AccountStatus after P7-07-BE-6 lands."
- Family query: `IParentChildQuery.GetChildIdsForParentAsync` for parent→children; `GetParentIdsForChildAsync` for child→parents. Then enrich child names/emails via `IChildAccountService.GetChildrenAsync(childIds)`. For parent lookups (child→parent), enrich via `IUserLookup` or a direct UserManager call — NOT a cross-module FK.
- Activity summary: each Gamification seam call in its own `try/catch`; null/empty on failure. `LastSignInAtUtc` field does NOT exist on `User`; return `null` for sign-in and document it.
- Audit emit: `AdminActionPerformedEvent` published **after** the read succeeds (no commit — this is a query; publish best-effort in the handler, consistent with wave pattern). Carry only `AdminUserId`, `Action = AdminActions.UserViewed`, `TargetEntityType = "User"`, `TargetEntityId = userId`, no PII in `Details`.
- Route attribute on controller: `[Route("api/Admin/Users")]`, each action uses `[HttpGet]` / `[HttpGet("{id}")]` / `[HttpGet("{id}/family")]` / `[HttpGet("{id}/activity")]`.

---

### Batch 3 — P7-07 backend feature (internal parallel on suspend/reactivate, sequential on delete+events)

**Agent:** `backend-feature`

- **3-A (parallel):** `P7-07-BE-2` (SuspendAccountCommand) + `P7-07-BE-3` (ReactivateAccountCommand) — both depend only on Batch 1 migration.
- **3-B (after 3-A + Batch 0 complete):** `P7-07-BE-4` (DeleteAccountCommand — needs `GetChildIdsForParentAsync` from Batch 0-A).
- **3-C (after 3-A..B):** `P7-07-BE-5` — publish integration events + wire module consumers (Gamification, Parent, Learning stub).
- **3-D (after 3-C + Batch 2):** `P7-07-BE-6` — add three write actions to `AdminUsersController` + `AdminActions` constants + upgrade `SearchUsersQuery` status filter from `IsActive` to `AccountStatus`.

**Key implementation notes for backend-feature agent:**
- Session revocation in `SuspendAccountCommand` and `DeleteAccountCommand`: call `ISessionManagementService.GetUserSessionsAsync(userId)`, then `TerminateSessionAsync(sessionId, SessionTerminationReason.AdminAction)` for each; AND call `IDistributedCache.RemoveAsync($"userrefreshtoken-{userId}")` directly (this is the Redis refresh-token key — confirmed from `SignOutCommandHandler` and `AuthenticationIdentityService`). Wrap both in try/catch (Redis failure must not abort the status change).
- `AccountStatus` check in `SignInCommandHandler`: modify the `!user.IsActive` check to also check `user.AccountStatus != AccountStatus.Active` — OR keep the IsActive sync and rely on the existing `!user.IsActive` gate (recommended: keep IsActive sync so the sign-in check requires zero change).
- Delete cascade: `GetChildIdsForParentAsync(userId)` to find children; loop over each child and apply the same AccountStatus/IsActive/session/token revocation; wrap the entire parent + all children writes in an explicit `IDbContextTransaction` (no UoW per ADR 0001). Each child's `StatusChangedBy = adminUserId`, `StatusChangedAtUtc = UtcNow`.
- Integration events: publish post-commit, best-effort (mirror `PublishLearningLanguageChangedEventAsync`). `AccountSuspendedIntegrationEvent(EventId, OccurredOnUtc, UserId, Reason)` etc.
- Learning consumer for `AccountDeleted`: thin stub (log + no-op) is acceptable now; Q-B1 data purge semantics are deferred.
- Prefer post-commit publish for P7-07 lifecycle events (a rolled-back lifecycle change must not emit a phantom audit/event). This diverges from the pre-commit pattern in the curriculum wave; flag the inconsistency to lead as a tracked wave-wide follow-up.
- P7-07-BE-6 route additions: `[HttpPost("{id}/suspend")]`, `[HttpPost("{id}/reactivate")]`, `[HttpDelete("{id}")]` on `AdminUsersController`.
- After P7-07-BE-6 upgrades the `SearchUsersQuery` status filter: change `IsActive`-based filter to `AccountStatus`-based (Active=0 for active filter, Suspended=1 for suspended). Also add `Deleted` exclusion to the base query (exclude `AccountStatus == Deleted` in addition to `IsDeleted == true`).

---

### Batch 4 — API testing for P7-06 + P7-07 (sequential, after Batches 2+3)

**Agent:** `api-tester`

Run against the real running API. Minimum test scenarios:

**P7-06:**
- Anonymous request → 401; non-admin authenticated → 403; admin → 200.
- Search with no filters → first page of non-deleted users.
- Search by role=parent, role=child.
- Search by free-text q (matches name; matches email; no match → empty).
- Profile detail: both `preferredLanguage` and `learningLanguage` present and labeled distinctly for a child; profile accessible for parent.
- Family for a parent → children[]; for a child → parents[]; invalid userId → 404.
- Activity summary: at least returns HTTP 200 without 500 even when Gamification seams return no data.
- Audit event emitted on profile read (verify via log or event count if observable).

**P7-07:**
- Suspend: account status → Suspended; subsequent sign-in with that account → blocked; refresh token invalidated (attempt refresh → 401); non-admin → 403.
- Reactivate: account status → Active; sign-in restored; reactivating an already-Active account → sensible response (idempotent or 400).
- Delete unconfirmed → 424. Delete confirmed → AccountStatus=Deleted; further suspend/reactivate of the deleted account → 400/rejected.
- Delete parent with CascadeChildren=true → children also Deleted.
- Already-deleted account suspend attempt → rejected.
- Search by status=active excludes Deleted and Suspended; status=suspended shows only Suspended.

---

### Batch 5 — P7-08 backend feature (parallel-internal on profile vs grade vs language)

**Agent:** `backend-feature`

- **5-A (parallel):**
  - `P7-08-BE-1` (UpdateChildProfileCommand — harmless, no deps beyond Batch 0 complete).
  - `P7-08-BE-3` (OverrideChildGradeCommand — needs SHARED-CONTRACTS-P7-08 from Batch 0).
- **5-B (after 5-A):**
  - `P7-08-BE-4` (ChildGradeChanged consumer in Learning — verify existing reads key off `User.Grade` live; if yes, wire a no-op stub consumer + document; if not, add recompute logic without deleting mastery/XP).
  - `P7-08-BE-5` (AdminUsersController wiring for profile PATCH + grade POST + audit emit + `AdminActions` constants).
- **5-C (after 5-B):**
  - `P7-08-BE-6` (AdminChangeLearningLanguageCommand — depends on controller being wired + Batch 0 complete + `IChildAccountService` seam already built).
- **5-D (after 5-C):**
  - `P7-08-BE-7` (verify `LearningLanguageChangedIntegrationEventHandler` fires for admin-path publishes; add Gamification re-scope stub if needed).

**Key implementation notes for backend-feature agent:**
- `UpdateChildProfileCommand`: use `IChildAccountService.UpdateChildAsync` as the write path OR a direct `UserManager.UpdateAsync` — prefer the seam if it covers `PreferredLanguage`+`Country` without grade side-effects. Note: `UpdateChildRequest` currently includes `FullName` + `Grade` + `Language` + `Country`; build an admin-specific path that only updates `PreferredLanguage` + `Nationality` to avoid accidental grade side-effect through the parent seam. Alternatively issue a direct `UserManager.UpdateAsync` on just those two fields (this is the Identity module's own DbContext — no cross-module FK).
- `OverrideChildGradeCommand`: confirm flag is a soft UX guard (check `Confirm == false` → return a `BusinessValidation`-style warning/confirmation-required response — or simply require `Confirm=true` always and reject with a 400 if false; do NOT use 424). Publish `ChildGradeChangedIntegrationEvent` post-commit best-effort.
- `AdminChangeLearningLanguageCommand`: confirm guard MUST run FIRST before any DB/seam call (identical to parent `ChangeLearningLanguageCommandHandler`). Admin path: no `IsParentOfChildAsync` check (admin acts on any child — `AdminOnly` policy is the gate). Call `IChildAccountService.ChangeLearningLanguageAsync(childId, lang)` unchanged.
- `AdminActions` constants to add: `ChildProfileUpdated`, `ChildGradeOverridden`, `ChildLearningLanguageChanged`.
- New routes on `AdminUsersController`: `[HttpPatch("{childId}/profile")]`, `[HttpPost("{childId}/grade")]`, `[HttpPost("{childId}/learning-language")]`.

---

### Batch 6 — API testing for P7-08 (sequential, after Batch 5)

**Agent:** `api-tester`

**P7-08:**
- Profile edit (preferredLanguage + country): saved correctly; no progress/grade affected; invalid language rejected; non-admin 403.
- Grade override with confirm=true: grade updated; `ChildGradeChangedIntegrationEvent` emitted; XP/badges/streaks NOT deleted; invalid grade (0, 7) → 422.
- Grade override with confirm=false: rejected (400 or warn-response — per implementation choice above).
- Learning-language change unconfirmed (confirm=false) → 424 (BEFORE any DB mutation).
- Learning-language change confirmed (confirm=true): same-language → no-op success; different language → `LearningLanguageChangedIntegrationEvent` emitted; Math/Science attempts deleted; Arabic/English + gamification retained.
- Learning-language change unsupported value → 422.
- All endpoints non-admin → 403; anonymous → 401.
- Audit event emitted for each override type.

---

### Batch 7 — Security audit (REQUIRED, cross-wave)

**Agent:** `security-auditor`

**Scope — all three stories (user + child data + auth state machine):**

Priority areas:
1. **IDOR on family/profile/activity endpoints** — can admin-A read admin-B's account? Can a non-admin reach `api/Admin/Users/{id}` if the policy attribute is missing? Verify `AdminOnly` is on every new action.
2. **Child PII exposure in list DTO** — confirm `AdminUserListItemDto` contains only minimal fields (no child grade/languages/country in the paginated list).
3. **Session revocation completeness** — verify suspend/delete wipes both the Redis refresh token (`userrefreshtoken-{userId}`) AND all tracked sessions, and that this is tested in Batch 4. Document the residual access-token window explicitly (bounded by JWT TTL).
4. **Cascade delete IDOR** — can an admin cascade-delete children they didn't create? Confirm `AdminOnly` is sufficient and there is no parent-link IDOR bypass.
5. **`AccountStatus` state machine** — verify the handlers enforce legal transitions: Active→Suspended, Active→Deleted, Suspended→Active (reactivate), Suspended→Deleted; Deleted is terminal (no reactivate, no re-suspend). Edge cases tested in Batch 4 but auditor checks the handler logic directly.
6. **Learning-language confirm gate** — confirm 424 fires BEFORE `IChildAccountService.ChangeLearningLanguageAsync` is called (a failed confirm must never trigger the Math/Science wipe).
7. **Grade override non-destructive** — confirm the `ChildGradeChangedIntegrationEvent` consumer in Learning does NOT delete any attempts/mastery. Auditor checks the consumer logic.
8. **Audit emit PII** — verify `AdminActionPerformedEvent.Details` for all three stories carries no PII (only opaque ids, action strings, old→new grade/language codes). Child email/name must not appear in `Details`.
9. **Exception leakage** — verify no `ex.Message` leaks to the API response in any new handler.
10. **`AdminActions` constants don't collide** — check no string collision with existing P7-01..P7-05 constants.

**Critical/High findings block the reviewer gate.** Medium/Low are advisory.

---

### Batch 8 — Reviewer gate (final, after Batches 4 + 6 + 7)

**Agent:** `reviewer`

Review all three stories together (they share the same controller and `Shared.Contracts` edits). Checklist:

- All acceptance criteria from P7-06/07/08 brief documents covered.
- `AdminUsersController` route shape matches FE contract (`api/Admin/Users`, `{id}`, `{id}/family`, `{id}/activity`, `{id}/suspend`, `{id}/reactivate`, `{id}/learning-language`, `{id}/grade`, `{childId}/profile`).
- No cross-module FK; all cross-module reads via `Shared.Contracts` seams.
- `PaginatedResult<T>` (not bespoke `PagedResult<T>`) used for search.
- `Successed` spelling preserved in all `BaseResponse<T>` usages.
- `ILoggerManager` injected (not `ILogger<T>`).
- No `ex.Message` in response bodies.
- `AccountStatus` and `IsActive` are kept in sync (suspend/delete always sets `IsActive=false`; reactivate sets `IsActive=true`).
- `DeleteAccountCommand` wraps multi-row writes in an explicit transaction.
- All commands are `ICommand<>` (validation fires); all queries validate inputs in-handler.
- Integration events publish post-commit, best-effort (try/catch, publish failure does not abort the primary mutation).
- Security auditor findings addressed or formally deferred with lead sign-off.
- Build passes (all projects), no new compiler warnings.

---

### Batch 9 — Commit (after Batch 8 PASSES)

**Agent:** `committer`

Incremental commits on `feat/phase-7-backend`, one commit per story group (or logical unit):
1. `feat(identity): P7-06 admin user search & inspect + parent-seam extension`
2. `feat(identity): P7-07 account lifecycle (suspend/reactivate/delete) + AccountStatus migration`
3. `feat(identity): P7-08 admin child profile & grade override + ChildGradeChanged contract`

Push to `feat/phase-7-backend`. Fold into the existing wave PR (PR #106 or whichever is open). No new PR. No merge. Never amend/force-push.

---

## Review gates summary

| After batch | Gate | Condition |
|---|---|---|
| Batch 0 (shared contracts) | Inline reviewer check (build pass) | Compile-clean; IParentChildQuery extension is backward-compatible; IIntegrationEvent shape matches existing events |
| Batch 1 (migration) | `reviewer` | SQL correct; backfill correct; snapshot updated; build passes |
| Batch 2 (P7-06 feature) | `reviewer` | AC-1..7 from P7-06 brief covered; DTO shape correct; route correct |
| Batch 3 (P7-07 feature) | `reviewer` | AC-1..7 from P7-07 brief covered; session revocation wired; cascade delete in explicit transaction |
| Batch 4 (api-tester P7-06+07) | `reviewer` reviews test results | All listed scenarios pass; no regressions |
| Batch 5 (P7-08 feature) | `reviewer` | AC-1..8 from P7-08 brief covered; confirm-gate fires BEFORE seam call; consumer verified non-destructive |
| Batch 6 (api-tester P7-08) | `reviewer` reviews test results | All listed scenarios pass; 424 for unconfirmed lang-change; non-destructive grade override confirmed |
| Batch 7 (security-auditor) | **REQUIRED gate** | Critical/High findings resolved; Medium/Low advisory noted |
| Batch 8 (reviewer) | Final gate before commit | Full checklist pass |

---

## Blockers and prerequisites

### Cleared (no action needed)
- `AdminActionPerformedEvent` + `AdminActions.cs` exist in `Shared.Contracts/Admin/` (verified on disk — P7-01..05 shipped them).
- `IChildAccountService.ChangeLearningLanguageAsync` is built and live (verified in `IChildAccountService.cs`).
- `LearningLanguageChangedIntegrationEvent` + its Learning consumer are built (verified in `Shared.Contracts/Identity/`).
- `AdminUsersController` does NOT yet exist — Batch 2 creates it from scratch (correct; the legacy `UserManagementController` is at a different route and is not modified by this wave).
- `UserManagementController` is already `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` at class level (P1-13) — the BE-5 "add Authorize" tasks in the task files are already satisfied for the legacy controller.
- Gamification seam interfaces (`IStudentXpQuery`, `IStudentStreakQuery`, `IStudentBadgesQuery`, `IStudentMissionsQuery`, `IStudentLeagueQuery`) all exist in `Shared.Contracts/Gamification/` (verified on disk).

### Must be cleared before Batch 0 starts
- None — Batch 0 is self-contained.

### Open questions that MUST be answered before specific batches

| Question | Blocking | Recommendation (bake in unless lead overrides) |
|---|---|---|
| **Q-A8 (route shape)** — confirmed `api/Admin/Users` for all three stories? | Batch 2-D, 3-D, 5-B | YES — baked in. New `AdminUsersController` at `api/Admin/Users`. |
| **Q-B2 (session revocation MVP scope)** — confirmed that "block future sign-in + kill refresh + terminate sessions, but allow existing access tokens to expire naturally" is acceptable for MVP? | Batch 3-A (suspend), Batch 4 (api-tester AC-1) | BAKED IN per lead decision. Document residual window in code comment and HANDOFF. |
| **Q-C1 (grade override confirm gate)** — confirmed non-destructive grade override does NOT use 424, just a soft confirm flag? | Batch 5-A | BAKED IN per lead decision. |
| **Q-C2 (ChildGradeChanged consumer scope)** — does Learning need a real re-scope or is a no-op stub sufficient for this wave (since reads already scope by `User.Grade` live)? | Batch 5-B (P7-08-BE-4) | Backend-feature agent to verify at implementation time: if `LessonQueryHandler`/skill-tree queries key off `User.Grade` at query time → consumer is a no-op stub; if any persisted grade-scoped state exists → add recompute without deleting mastery. |
| **Q-A6 (`LastSignInAtUtc` column)** — no migration for P7-06; activity summary returns `null` for sign-in. Confirmed? | Batch 2 (P7-06-BE-4) | BAKED IN per plan. Label as "not tracked" in DTO. |

### Informational (not blocking — document in HANDOFF)
- **P7-12 audit log is deferred.** `AdminActionPerformedEvent` is publish-only this wave. The wave-wide pre-commit vs post-commit publish inconsistency is a tracked follow-up (P7-06 read-handlers publish post-read/best-effort; P7-07 lifecycle handlers should publish post-commit; curriculum wave P7-01..05 publishes pre-commit). P7-07 should set the new standard (post-commit) and the curriculum wave inconsistency is a separate cleanup ticket.
- **Route overlap risk:** the new `AdminUsersController` (`api/Admin/Users`) must not conflict with any existing route. Verified: no existing controller uses that prefix.
- **P7-12 ordering (gap analysis §5 §6 item #6):** the gap analysis recommended P7-12 land BEFORE P7-06/07/08 so audit events are consumable. This wave keeps P7-12 deferred (consumer side) — the event is published now, consumed in the P7-12 wave. No blocker.

---

## Definition of done

### Per-batch
| Batch | Done when |
|---|---|
| 0 (shared contracts) | `IParentChildQuery` has both new methods; `Parent.Infrastructure` implements them; three new `Account*Event` records + `ChildGradeChangedIntegrationEvent` exist; build green. |
| 1 (migration) | Migration file generated; `dotnet ef database update` applied to local dev DB; `AccountStatus` column present; backfill SQL correct; snapshot updated. |
| 2 (P7-06 feature) | `AdminUsersController` at `api/Admin/Users` with four GET actions; all four query handlers pass; `AdminUserListItemDto` has no child PII beyond name/email/role/status; `AdminUserProfileDto` has both language fields; build green. |
| 3 (P7-07 feature) | Suspend/reactivate/delete commands + validators + handlers present; cascade delete uses explicit transaction; session+refresh revocation wired; three integration events published post-commit; module consumers present; `AccountStatus` filter active in SearchUsers; build green. |
| 4 (api-tester P7-06+07) | All Batch 4 test scenarios documented and pass; no regressions on existing auth endpoints. |
| 5 (P7-08 feature) | Profile PATCH, grade POST, learning-language POST handlers present; confirm-gate on learning-language fires 424 before seam call; grade override publishes `ChildGradeChangedIntegrationEvent`; Learning consumer wired; build green. |
| 6 (api-tester P7-08) | All Batch 6 test scenarios documented and pass; 424 verified before seam call for learning-language unconfirmed. |
| 7 (security-auditor) | Security report produced; no Critical/High unresolved; PII-in-audit finding addressed. |
| 8 (reviewer) | Full reviewer checklist passes; all P7-06/07/08 acceptance criteria met. |
| 9 (committer) | Three commits on `feat/phase-7-backend`; pushed; PR #106 updated with description of user/account wave. |

### Overall wave DoD (tied to acceptance criteria)
1. `GET api/Admin/Users?role=&status=&q=&page=&pageSize=` returns paginated minimal-PII list; excludes Deleted accounts.
2. `GET api/Admin/Users/{id}` returns full profile with both `preferredLanguage` and `learningLanguage` labeled distinctly; non-admin → 403; anonymous → 401.
3. `GET api/Admin/Users/{id}/family` returns family linkage both directions (children[] for parent, parents[] for child).
4. `GET api/Admin/Users/{id}/activity` returns activity summary (Gamification seams); degrades gracefully; never 500.
5. `POST api/Admin/Users/{id}/suspend` blocks sign-in and kills refresh token + sessions; reason required; already-Deleted rejected.
6. `POST api/Admin/Users/{id}/reactivate` restores sign-in; Deleted rejected.
7. `DELETE api/Admin/Users/{id}` (with confirm+reason) soft-deletes; cascade option for parents; multi-child write is transactional.
8. `PATCH api/Admin/Users/{childId}/profile` updates preferredLanguage + country; no progress impact.
9. `POST api/Admin/Users/{childId}/grade` overrides grade non-destructively; preserves XP/badges/streaks; emits `ChildGradeChangedIntegrationEvent`.
10. `POST api/Admin/Users/{childId}/learning-language` with `confirm=false` → 424 (no mutation); with `confirm=true` → hard-deletes Math/Science attempts via existing seam; same-language → no-op; invalid → 422.
11. All write endpoints emit `AdminActionPerformedEvent` (opaque ids, no PII in Details).
12. All endpoints non-admin → 403; anonymous → 401.
13. Security audit passed (no Critical/High).
14. All three commits on `feat/phase-7-backend`, pushed, folded into wave PR.

---

Plan ready — dispatch Batch 0.
