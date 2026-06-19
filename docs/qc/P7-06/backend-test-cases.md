# P7-06 — Admin User Search & Inspect — Backend (API) test cases

**Target agent:** `api-tester`
**Surface:** `AdminUsersController` (`api/Admin/Users`), class-level `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`.
**Existing test file (gap-analysis baseline):** `backend/tests/Learnexia.IntegrationTests/P7_06_07_08_UserAccountAdmin_Tests.cs` (the "Existing test" column cites the exact method).
**Envelope:** `BaseResponse<T>` with keys `statusCode` / `successed` (sic) / `message` / `data` / `errors`. List endpoint returns `PaginatedResult<AdminUserListItemDto>` (nested `data.data` array + `currentPage` / `totalCount` / `totalPages` / `pageSize`).

Endpoints in scope (all `AdminOnly`):
- `GET  api/Admin/Users` — search / list
- `GET  api/Admin/Users/{id}` — profile inspect
- `GET  api/Admin/Users/{id}/family` — family linkage
- `GET  api/Admin/Users/{id}/activity` — activity summary

Legend: **Covered** = an existing test asserts it (cite method); **GAP** = no existing test, implement new; **PARTIAL** = touched but a sub-assertion is missing.

---

## A. Auth / authz matrix (all four read endpoints)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-01 | GET /Admin/Users anonymous → 401 | auth | P0 | no token | GET `api/Admin/Users` no bearer | 401 | **Covered** — `P706_SearchUsers_Anonymous_Returns401` |
| BE-TC-06-02 | GET /Admin/Users parent → 403 | authz | P0 | parent token | GET with parent bearer | 403 | **Covered** — `P706_SearchUsers_Parent_Returns403` |
| BE-TC-06-03 | GET /Admin/Users basic-role → 403 | authz | P0 | basicuser token | GET with basic bearer | 403 | **Covered** — `P706_SearchUsers_BasicUser_Returns403` |
| BE-TC-06-04 | GET /Admin/Users/{id} anonymous → 401 | auth | P0 | — | GET `…/1` no bearer | 401 | **Covered** — `P706_GetProfile_Anonymous_Returns401` |
| BE-TC-06-05 | GET /Admin/Users/{id} parent → 403 | authz | P0 | parent token | GET `…/1` parent bearer | 403 | **Covered** — `P706_GetProfile_Parent_Returns403` |
| BE-TC-06-06 | GET /Admin/Users/{id} basic-role → 403 | authz | P1 | basic token | GET `…/1` basic bearer | 403 | **GAP** — only parent variant exists for profile detail |
| BE-TC-06-07 | GET /Admin/Users/{id}/family anonymous → 401 | auth | P0 | — | GET `…/1/family` no bearer | 401 | **Covered** — `P706_GetFamily_Anonymous_Returns401` |
| BE-TC-06-08 | GET /Admin/Users/{id}/family parent → 403 | authz | P0 | parent token | GET `…/1/family` parent bearer | 403 | **Covered** — `P706_GetFamily_Parent_Returns403` |
| BE-TC-06-09 | GET /Admin/Users/{id}/family basic-role → 403 | authz | P1 | basic token | GET `…/1/family` basic bearer | 403 | **GAP** — basic variant missing |
| BE-TC-06-10 | GET /Admin/Users/{id}/activity anonymous → 401 | auth | P0 | — | GET `…/1/activity` no bearer | 401 | **Covered** — `P706_GetActivity_Anonymous_Returns401` |
| BE-TC-06-11 | GET /Admin/Users/{id}/activity parent → 403 | authz | P0 | parent token | GET `…/1/activity` parent bearer | 403 | **Covered** — `P706_GetActivity_Parent_Returns403` |
| BE-TC-06-12 | GET /Admin/Users/{id}/activity basic-role → 403 | authz | P1 | basic token | GET `…/1/activity` basic bearer | 403 | **GAP** — basic variant missing |
| BE-TC-06-13 | Malformed/expired JWT on /Admin/Users → 401 | auth | P1 | tampered or expired admin token | GET with garbage/expired bearer | 401 (not 403/500) | **GAP** — no invalid-token case for read endpoints |

---

## B. Search — happy path, envelope, pagination

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-20 | Admin search → 200 + envelope, successed=true | functional | P0 | admin token | GET `api/Admin/Users` | 200; envelope keys present; `successed=true` | **Covered** — `P706_SearchUsers_Admin_Returns200WithEnvelope` |
| BE-TC-06-21 | Paginated shape (currentPage/totalCount/totalPages/pageSize) | functional | P0 | admin | GET `?PageNumber=1&PageSize=10` | pagination metadata present | **Covered** — `P706_SearchUsers_ReturnsPaginatedShape` |
| BE-TC-06-22 | PageSize cap at 100 (PageSize=200 clamped, not rejected) | boundary | P1 | admin | GET `?PageNumber=1&PageSize=200` | 200; `pageSize ≤ 100`; no 400/500 | **Covered** — `P706_SearchUsers_PageSizeCappedAt100` |
| BE-TC-06-23 | PageNumber<1 coerced to 1 (no 500) | boundary | P2 | admin | GET `?PageNumber=0&PageSize=10` | 200; `currentPage=1` | **GAP** — handler coerces `<1 → 1` but untested |
| BE-TC-06-24 | PageSize<1 coerced to default 20 | boundary | P2 | admin | GET `?PageSize=0` | 200; `pageSize=20` | **GAP** — handler coerces `<1 → 20` but untested |
| BE-TC-06-25 | Empty result set → 200 successed=true (EmptyCollection, not 404) | state | P1 | admin | GET `?q=<random-guid-no-match>` | 200; `successed=true`; empty `data.data` | **GAP** — `PaginatedResult.EmptyCollection` path not asserted here |
| BE-TC-06-26 | OrderBy=desc does not error / no LINQ injection | negative | P2 | admin | GET `?OrderBy=FullName%20desc` and `?OrderBy=1;DROP%20TABLE` | both 200; no 500; results still paginated | **GAP** — OrderBy is whitelisted to FullName asc/desc; untested |

---

## C. Search — filters

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-30 | Free-text q by exact email finds user | functional | P0 | register parent w/ unique email | GET `?q={email}` | result includes that email | **Covered** — `P706_SearchUsers_EmailFilter_FindsUser` |
| BE-TC-06-31 | Free-text q by (partial) FullName finds user | functional | P1 | child created with known FullName ("Test Child") | GET `?q=Test%20Child&role=Student` | at least one row with that name | **GAP** — only email-q tested; name-q path (`u.FullName.Contains`) untested |
| BE-TC-06-32 | role=Parent returns only Parent-role rows | validation | P0 | mixed users | GET `?role=Parent&PageSize=50` | every row with role = "Parent" | **Covered** — `P706_SearchUsers_RoleFilter_ReturnsOnlyParents` |
| BE-TC-06-33 | role=Student returns only Student-role rows | validation | P1 | a child exists | GET `?role=Student&PageSize=50` | every row role = "Student" | **GAP** — only Parent role filter tested |
| BE-TC-06-34 | status=1 (Suspended) returns only suspended | validation | P0 | suspend one user (via P7-07) | GET `?status=1&PageSize=50` | every row `accountStatus=1`; the suspended user present | **GAP** — the `status` AccountStatus filter is entirely untested |
| BE-TC-06-35 | status=0 (Active) excludes the suspended user | validation | P1 | one active + one suspended user | GET `?status=0&PageSize=50` | suspended user absent; rows `accountStatus=0` | **GAP** — active-status filter untested |
| BE-TC-06-36 | Default search (no status) excludes Deleted accounts | persistence | P0 | soft-delete one user (P7-07) | GET `?q={deletedEmail}` | deleted user absent | **Covered** — `P707_Delete_SoftDeletes_HiddenFromDefaultSearch` (verifies the base `AccountStatus != Deleted` filter) |
| BE-TC-06-37 | Combined filters role+status+q intersect correctly | functional | P2 | child created | GET `?role=Student&status=0&q={childEmail}` | exactly the child returned | **GAP** — combined-filter intersection untested |
| BE-TC-06-38 | Unknown role value → empty result, not 500 | negative | P2 | admin | GET `?role=Teacher` (no teacher role exists) | 200; empty `data.data`; no rows | **GAP** — asserts product rule "no teacher role" + graceful empty |

---

## D. Search — DTO PII minimization (CRITICAL)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-40 | List DTO omits grade / nationality / learningLanguage | a11y/PII | P0 | child created with grade+country+lang | GET `?role=Student&q={childEmail}`; inspect item | item has NO `grade`, `nationality`, `learningLanguage` (camel + Pascal) | **Covered** — `P706_SearchUsers_ListItem_DoesNotExposeSensitiveChildPii` |
| BE-TC-06-41 | List DTO exposes only id/fullName/email/role/accountStatus/createdAt | PII | P1 | child created | inspect a list item's full key set | exactly the minimal allow-list; NO `preferredLanguage`, `avatarUrl`, `isActive`, `lastStatusReason` | **GAP** — existing test only checks 3 forbidden fields; a positive allow-list assertion is stronger |

---

## E. Profile inspect — `GET /Admin/Users/{id}`

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-50 | Existing user → 200, both language fields distinct & non-empty | functional | P0 | parent registered | GET `…/{id}` | `preferredLanguage` + `learningLanguage` present & non-empty | **Covered** — `P706_GetProfile_ExistingUser_Returns200WithBothLanguageFields` |
| BE-TC-06-51 | Child profile includes grade + nationality | functional | P0 | child grade=4 country=EG | GET `…/{childId}` | `grade=4`, `nationality=EG` | **Covered** — `P706_GetProfile_Child_IncludesGradeAndCountry` |
| BE-TC-06-52 | Profile exposes status governance fields | functional | P1 | suspended user (P7-07) | GET `…/{id}` | `accountStatus=1`, `lastStatusReason` non-null, `statusChangedAtUtc` non-null, `isActive=false` | **GAP** — status/reason/timestamp fields on profile DTO untested |
| BE-TC-06-53 | lastSignInAtUtc is null (not tracked — Q-A6) | functional | P2 | any user | GET `…/{id}` | `lastSignInAtUtc` null if present | **Covered** — assertion inside `P706_GetProfile_ExistingUser_…` |
| BE-TC-06-54 | Non-existent id (99999999) → 404 (or 200 successed=false) | negative | P0 | admin | GET `…/99999999` | 404; or 200 `successed=false` | **Covered** — `P706_GetProfile_NonExistentUser_Returns404` |
| BE-TC-06-55 | id ≤ 0 (e.g. 0 or -1) → BadRequest / NotFound, not 500 | boundary | P1 | admin | GET `…/0` and `…/-1` | 400/404; `successed=false`; never 500 | **GAP** — handler guards `UserId <= 0 → BadRequest`; route `{id:int}` may also 404 negatives — untested |

---

## F. Family linkage — `GET /Admin/Users/{id}/family`

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-60 | Parent → children[] non-empty | functional | P0 | parent + 1 child | GET `…/{parentId}/family` | `data.children` array length>0 | **Covered** — `P706_GetFamily_ForParent_ReturnsChildren` |
| BE-TC-06-61 | Child → parents[] non-empty | functional | P0 | parent + 1 child | GET `…/{childId}/family` | `data.parents` array length>0 | **Covered** — `P706_GetFamily_ForChild_ReturnsParents` |
| BE-TC-06-62 | Parent with no children → empty children[], 200 successed=true | state | P1 | parent, no child added | GET `…/{parentId}/family` | 200; `children` empty array; no 500 | **GAP** — empty-family path untested |
| BE-TC-06-63 | Non-existent id → 404 (graceful), not 500 | negative | P1 | admin | GET `…/99999999/family` | 404 or 200 successed=false; never 500 | **GAP** — not-found path on family untested |
| BE-TC-06-64 | Family DTO carries minimal child PII (no grade/lang in linkage rows) | PII | P1 | parent+child | inspect each child item in `children[]` | linkage item has name/id but NOT learningLanguage | **GAP** — PII shape of family DTO untested |

---

## G. Activity summary — `GET /Admin/Users/{id}/activity`

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-70 | Activity never returns 500 (degrades gracefully) | state | P0 | admin | GET `…/1/activity` | status in {200,404}; never 500 | **Covered** — `P706_GetActivity_Admin_NeverReturns500` |
| BE-TC-06-71 | lastSignInAtUtc labelled null (not tracked) | functional | P1 | known user | GET `…/{id}/activity` | `lastSignInAtUtc` null if present | **Covered** — `P706_GetActivity_LastSignInIsNull` |
| BE-TC-06-72 | Activity for a child with no learning data → 200, fields null/zero not 500 | state | P1 | freshly created child | GET `…/{childId}/activity` | 200; gamification fields null/0; no 500 | **GAP** — graceful-empty for a real child (cross-module seam returns nothing) untested |
| BE-TC-06-73 | Activity non-existent id → 404, not 500 | negative | P1 | admin | GET `…/99999999/activity` | 404 or graceful 200; never 500 | **PARTIAL** — `…NeverReturns500` uses id=1 only; explicit non-existent id missing |

---

## H. Audit trail (P7-12 integration) — CRITICAL, AC-6 of P7-06

> The P7-12 audit tests only exercise `Subject.Created`. **No test verifies that read-inspect actions emit audit rows.** Per P7-06 AC-6 "read-only inspection is audited (who viewed which account, when)". These require the Moderation `ModerationDbContext` migration applied (see `P7_12_AuditLog_Tests.InitializeAsync`) and a short poll on `GET api/Admin/Audit/Log`.

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-06-80 | Profile view emits `User.Viewed` audit row | persistence/audit | P0 | admin; target user id | GET `…/{id}`; poll `GET api/Admin/Audit/Log?actionType=User.Viewed&adminUserId={adminId}` | row appears: `action=User.Viewed`, `targetEntityType=User`, `targetEntityId={id}`, correct `adminUserId` | **GAP** — handler emits `AdminActions.UserViewed`; never verified end-to-end |
| BE-TC-06-81 | Search emits `User.Searched` audit row with NO raw query term (PII-safe) | persistence/audit | P0 | admin | GET `?q={childFullName}`; poll `?actionType=User.Searched` | row appears: `action=User.Searched`; `details` contains `q-length=` + `role=` + `status=` but NOT the raw query string / child name | **GAP** — handler emits `UserSearched` with `q-length` only; never verified (and the PII-safe Details assertion is the high-value part) |
| BE-TC-06-82 | Audit row for read actions carries no PII in Details/DTO | PII/audit | P1 | as above | inspect the `User.Viewed`/`User.Searched` rows | no `email` / `name` / `password`; `details` carries ids/lengths/enums only | **GAP** — read-action PII-safety not asserted (Subject.Created variant exists in P7-12 but not for user reads) |

---

## Summary — P7-06 backend

- **Covered:** 16 cases (auth matrix for all 4 endpoints anonymous+parent, search happy/pagination/cap/email-filter/role-Parent/PII-omission, profile happy/child/not-found/lastSignIn, family parent+child, activity-never-500/lastSignIn).
- **GAP (new to implement):** 22 cases — most valuable: **status filter (34/35), search-by-name (31), Student role filter (33), audit-trail emission for view+search (80/81/82), profile status fields (52), basic-role 403 on detail/family/activity (06/09/12), graceful not-found on family/activity (63/73), empty-result EmptyCollection (25).**
- **Headline gap count: 22.**
