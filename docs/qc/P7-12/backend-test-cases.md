# P7-12 Admin Audit Log — Backend Test Cases (api-tester)

> Surface: `AuditController` @ `GET api/Admin/Audit/Log` (AdminOnly, read-only, immutable). Fed by `AdminActionPerformedEvent` (Shared.Contracts) → `AuditLogEventHandler` (Moderation) via the post-commit `AdminActionPerformedDomainEvent` relay.
>
> Existing suite: `backend/tests/Learnexia.IntegrationTests/P7_12_AuditLog_Tests.cs` — read first. Each case is **Covered** (cite `[Fact]`) or **GAP**.
>
> Filter param names (`GetAuditLogQuery`): `AdminUserId`, `ActionType`, `TargetEntityType`, `DateFrom`, `DateTo`, `PageNumber`, `PageSize`. DTO fields: id/eventId/adminUserId/action/targetEntityType/targetEntityId/details/occurredAtUtc/createdAt (no PII).

## Auth / immutability

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-12-01 | Log anonymous → 401 | auth | P0 | none | GET Log no bearer | 401 | **Covered** — `Auth1_AuditLog_Anonymous_Returns401` |
| BE-TC-12-02 | Log parent → 403 | auth | P0 | parent JWT | GET Log parent | 403 | **Covered** — `Auth2_AuditLog_Parent_Returns403` |
| BE-TC-12-03 | Log basicuser → 403 | auth | P0 | basicuser JWT | GET Log basicuser | 403 | **Covered** — `Auth3_AuditLog_BasicUser_Returns403` |
| BE-TC-12-04 | Log admin → 200 | auth | P0 | admin JWT | GET Log admin | 200 | **Covered** — `Auth4_AuditLog_Admin_Returns200` |
| BE-TC-12-05 | POST Log → 404/405 (no create route) | negative | P0 | admin | POST Log | 404 or 405 | **Covered** — `Immut1_PostAuditLog_NoRouteExists` |
| BE-TC-12-06 | PUT Log → 404/405 | negative | P0 | admin | PUT Log | 404 or 405 | **Covered** — `Immut2_PutAuditLog_NoRouteExists` |
| BE-TC-12-07 | DELETE Log → 404/405 | negative | P0 | admin | DELETE Log | 404 or 405 | **Covered** — `Immut3_DeleteAuditLog_NoRouteExists` |

## Envelope / pagination

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-12-08 | BaseResponse envelope keys | functional | P1 | admin | GET Log | statusCode/successed/message/data/errors | **Covered** — `Env1_ResponseHasBaseResponseEnvelopeKeys` |
| BE-TC-12-09 | Paged data has paging keys | functional | P1 | admin | GET Log | currentPage/totalCount/totalPages/pageSize | **Covered** — `Env2_PaginatedResultHasPagingKeys` |
| BE-TC-12-10 | PageSize=9999 clamped ≤100 | boundary | P1 | ≥1 row | GET Log?pageSize=9999 | pageSize ≤ 100 | **Covered** — `Env3_PageSizeClamped_To100` |
| BE-TC-12-11 | Empty log → Successed=true (EmptyCollection) | state | P1 | far-future date filter | GET Log?dateFrom=2099-01-01&dateTo=2099-12-31 | 200; successed=true | **Covered** — `Env4_EmptyLog_ReturnsSucceededTrue` |
| BE-TC-12-12 | Smoke: endpoint reachable (not 404) | functional | P2 | admin | GET Log | not 404; 200 | **Covered** — `Smoke_AuditLogEndpoint_IsReachable` |

## Filters / ordering

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-12-13 | Filter by actionType | functional | P1 | Subject.Created row | GET Log?actionType=Subject.Created | all rows action=Subject.Created | **Covered** — `Filter1_ByActionType_ReturnsOnlyMatchingRows` |
| BE-TC-12-14 | Filter by targetEntityType | functional | P1 | Subject row | GET Log?targetEntityType=Subject | all rows targetEntityType=Subject | **Covered** — `Filter2_ByTargetEntityType_ReturnsOnlyMatchingRows` |
| BE-TC-12-15 | Filter by adminUserId | functional | P1 | row by admin | GET Log?adminUserId={id} | all rows adminUserId={id} | **Covered** — `Filter3_ByAdminUserId_ReturnsOnlyMatchingRows` |
| BE-TC-12-16 | Filter by dateFrom/dateTo | functional | P1 | recent row | GET Log?dateFrom=&dateTo= | all rows within range | **Covered** — `Filter4_ByDateRange_ReturnsOnlyRowsInRange` |
| BE-TC-12-17 | Unknown actionType → empty, Successed=true | negative | P1 | admin | GET Log?actionType=Nonexistent | 200; empty; successed=true | **Covered** — `Filter5_UnknownActionType_ReturnsEmptySucceeded` |
| BE-TC-12-18 | Newest-first ordering | functional | P1 | 2 rows | GET Log | occurredAtUtc descending | **Covered** — `Order1_ResultsAreNewestFirst` |
| BE-TC-12-19 | **Combined actor + action + date filters AND-compose** | functional | P2 | mixed rows | GET Log?adminUserId=&actionType=&dateFrom=&dateTo= | rows satisfy all filters | **GAP** — filters tested only individually |
| BE-TC-12-20 | **DateFrom > DateTo (inverted range) → empty / 200 (not 500)** | boundary | P2 | admin | GET Log?dateFrom=2030&dateTo=2020 | 200, empty result, no 500 | **GAP** — inverted date range untested (audit query is not validated; must degrade) |

## End-to-end capture (cross-module producers)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-12-21 | Subject.Create → audit row appears | functional | P0 | admin | POST Subjects/Create; poll Log | row w/ action=Subject.Created | **Covered** — `E2E1_SubjectCreate_ProducesAuditRow` |
| BE-TC-12-22 | Audit row correct fields + no PII in Details | persistence | P0 | admin | POST Subjects/Create; read row | adminUserId=actor, action=Subject.Created, targetEntityType=Subject, occurredAtUtc real, Details no name/@/password | **Covered** — `E2E2_AuditRow_HasCorrectFields_NoPiiInDetails` |
| BE-TC-12-23 | One action → exactly one audit row (no double-write) | persistence | P0 | admin | POST Subjects/Create | exactly 1 matching row | **Covered** — `Idem1_OneAction_ExactlyOneAuditRow` |
| BE-TC-12-24 | DTO has no name/email/password/userName/phoneNumber | persistence/privacy | P0 | row | read row | PII fields absent; expected fields present | **Covered** — `Pii1_AuditLogDto_HasNoNameOrEmailFields` |
| BE-TC-12-25 | **Gamification override (P7-13) lands in audit log** | functional | P0 | admin + child profile | POST league-tier override; GET Log?actionType=Gamification.LeagueTierOverridden | row appears | **Covered (in P7-13 suite)** — `Audit_OverrideLeagueTier_ProducesAuditRow`. *From P7-12's own perspective, cross-module producer coverage relies on P7-13/P7-09 tests — note the dependency.* |
| BE-TC-12-26 | **Account suspend/reactivate/delete (P7-07) lands in audit log** | functional | P1 | admin + target account | POST suspend; GET Log?actionType=Account.Suspended (confirm exact string) | row appears with targetEntityType for account | **GAP** — the AC names "role/config changes" / account actions as audited; P7-12 suite only proves the **Learning curriculum** producer. Identity-side producers (P7-07) are unverified in any audit-focused test |
| BE-TC-12-27 | **Learning-language change (P7-08) lands in audit log** | functional | P1 | admin + child | change child learning-language; GET Log?actionType=Child.LearningLanguageChanged (confirm string) | row records old/new LearningLanguage only, no PII | **GAP** — AC explicitly calls out the P7-08 learning-language change as audited with old/new snapshot; no audit-focused test asserts it |
| BE-TC-12-28 | **Moderation review (P7-09) lands in audit log** | functional | P1 | admin + Pending item | POST Moderation Review; GET Log?actionType={moderation review action} | row appears | **GAP** — cross-references P7-09 09-33; from P7-12's side this confirms the new Moderation producer reaches the consumer |
| BE-TC-12-29 | **Distinct action types coexist & filter independently** | functional | P2 | rows of ≥2 action types | create Subject + grant streak-freeze; filter each actionType | each filter returns only its own action; no cross-contamination | **GAP** — multi-producer coexistence in the log is untested |
| BE-TC-12-30 | occurredAtUtc / createdAt are real (not epoch/MinValue) | persistence | P1 | any row | read row | occurredAtUtc after 2020; createdAt stamped (not 0001-01-01) | **GAP / regression-guard** — the prior P7-backend execution report (Bucket D) found `createdAt` always `0001-01-01` and date filter keying off the unstamped column. E2E2 checks occurredAtUtc but **not** createdAt; add an explicit createdAt-stamped assertion to lock the fix |

## Export

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-12-31 | **Export filtered log (CSV/JSON)** | functional | P2 | admin | GET export endpoint (if implemented) | CSV/JSON download honoring active filters; AdminOnly | **GAP / spec-confirm** — AC-4 "admins can export the filtered log (CSV/JSON)". No export route is visible on `AuditController`. Confirm whether export is in scope for the BE or deferred to FE client-side; if BE, it is entirely untested (and possibly unbuilt) |
