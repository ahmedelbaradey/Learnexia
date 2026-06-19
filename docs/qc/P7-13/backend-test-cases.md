# P7-13 Gamification Admin Overrides — Backend Test Cases (api-tester)

> Surface: `AdminGamificationController` @ `api/Admin/Gamification` (AdminOnly). Endpoints confirmed from code:
> - `POST children/{childId}/league-tier` · `POST children/{childId}/streak-freeze`
> - `GET Badges` · `POST Badges` · `PUT Badges/{id}` · `PATCH Badges/{id}/active`
> - `GET Missions` · `POST Missions` · `PUT Missions/{id}` · `PATCH Missions/{id}/active`
> - `POST TimedEvents` · `PUT TimedEvents/{id}` · `POST TimedEvents/{id}/activate` · `POST TimedEvents/{id}/expire`
> - (read-only, pre-existing) `GET api/admin/timed-events` — used by the suite's id-resolver
>
> Existing suite: `backend/tests/Learnexia.IntegrationTests/P7_13_GamificationAdmin_Tests.cs` — very thorough (AC-1..AC-9 + audit E2E + seeder). Each case is **Covered** (cite `[Fact]`) or **GAP**.
>
> Validator facts: commands are `ICommand` → ValidationBehavior → **422** on invalid bodies. `Confirm=true` required (Equal(true)); `Reason` NotEmpty; streak `Count` in (0, MaxFreezes=2]; timed-event `Multiplier` in [1,5], `Start<End`; enum `IsInEnum`. Domain rejections (no-op, already-active, not-found) → `Successed=false` (200 or 400/404/424). Audit via post-commit `AdminActionPerformedDomainEvent` relay.

## AC-7 Auth matrix

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-01 | league-tier anonymous → 401 | auth | P0 | POST league-tier no bearer | 401 | **Covered** — `Auth_LeagueTierOverride_Anonymous_Returns401` |
| BE-TC-13-02 | league-tier parent → 403 | auth | P0 | POST parent | 403 | **Covered** — `Auth_LeagueTierOverride_Parent_Returns403` |
| BE-TC-13-03 | league-tier basicuser → 403 | auth | P0 | POST basicuser | 403 | **Covered** — `Auth_LeagueTierOverride_BasicUser_Returns403` |
| BE-TC-13-04 | streak-freeze anonymous → 401 | auth | P0 | POST no bearer | 401 | **Covered** — `Auth_StreakFreeze_Anonymous_Returns401` |
| BE-TC-13-05 | streak-freeze parent → 403 | auth | P0 | POST parent | 403 | **Covered** — `Auth_StreakFreeze_Parent_Returns403` |
| BE-TC-13-06 | GET Badges anonymous → 401 | auth | P0 | GET no bearer | 401 | **Covered** — `Auth_ListBadges_Anonymous_Returns401` |
| BE-TC-13-07 | GET Badges basicuser → 403 | auth | P0 | GET basicuser | 403 | **Covered** — `Auth_ListBadges_BasicUser_Returns403` |
| BE-TC-13-08 | POST Badges anonymous → 401 | auth | P0 | POST no bearer | 401 | **Covered** — `Auth_CreateBadge_Anonymous_Returns401` |
| BE-TC-13-09 | POST Missions anonymous → 401 | auth | P0 | POST no bearer | 401 | **Covered** — `Auth_CreateMission_Anonymous_Returns401` |
| BE-TC-13-10 | POST TimedEvents anonymous → 401 | auth | P0 | POST no bearer | 401 | **Covered** — `Auth_CreateTimedEvent_Anonymous_Returns401` |
| BE-TC-13-11 | GET Missions parent → 403 | auth | P0 | GET parent | 403 | **Covered** — `Auth_ListMissions_Parent_Returns403` |
| BE-TC-13-12 | **PUT Badges/{id} parent → 403** | auth | P1 | PUT parent | 403 | **GAP** — only POST/GET auth tested; the PUT/PATCH mutation routes are not asserted against a non-admin |
| BE-TC-13-13 | **PATCH Badges/{id}/active anonymous → 401** | auth | P1 | PATCH no bearer | 401 | **GAP** — PATCH active route auth untested |
| BE-TC-13-14 | **POST TimedEvents/{id}/activate parent → 403** | auth | P1 | POST parent | 403 | **GAP** — the activate/expire write routes' auth untested for non-admin |

## AC-8 Envelope

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-15 | GET Badges envelope keys | functional | P1 | GET Badges admin | statusCode/successed/message/data/errors | **Covered** — `Envelope_GetBadges_HasBaseResponseKeys` |
| BE-TC-13-16 | GET Missions envelope keys | functional | P1 | GET Missions admin | statusCode/successed/data | **Covered** — `Envelope_GetMissions_HasBaseResponseKeys` |
| BE-TC-13-17 | **GET api/admin/timed-events envelope keys** | functional | P2 | GET timed-events admin | BaseResponse envelope present | **GAP** — the read-only timed-events list is used as an id-resolver but its envelope is never asserted directly |

## AC-2 Badge CRUD

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-18 | Create badge → appears in admin list | functional | P0 | POST then GET | 200/201; badge in list by code | **Covered** — `Badge_Create_ReturnsCreatedAndAppearsInList` |
| BE-TC-13-19 | Duplicate code → rejected (424) | negative | P0 | POST same code ×2 | second rejected, Successed=false | **Covered** — `Badge_Create_DuplicateCode_Rejected` |
| BE-TC-13-20 | Update mutable fields persists | persistence | P1 | PUT then DB read | Name updated | **Covered** — `Badge_Update_MutableFields_Persists` |
| BE-TC-13-21 | Deactivate → still in admin list, isActive=false | state | P0 | PATCH active=false then GET | present, isActive=false | **Covered** — `Badge_Deactivate_AdminListStillShowsIt` |
| BE-TC-13-22 | Reactivate → isActive=true | state | P1 | PATCH active=true then DB read | isActive=true | **Covered** — `Badge_Reactivate_IsActiveRestored` |
| BE-TC-13-23 | **Deactivation does not strip earned StudentBadges** | persistence | P0 | seed a StudentBadge for a deactivated badge def; PATCH active=false | the existing StudentBadge row survives (AC-2: "existing earned badges not retroactively removed") | **GAP** — AC-2's earned-badge-preservation invariant is documented in the suite header but has **no test**; this is a P0 data-safety assertion |
| BE-TC-13-24 | **PUT Badges/{id} unknown id → Successed=false (NotFound)** | negative | P1 | PUT to 99999999 | Successed=false / 404; not 500 | **GAP** — update-on-missing-badge untested |
| BE-TC-13-25 | **PATCH Badges/{id}/active unknown id → Successed=false** | negative | P2 | PATCH to 99999999 | Successed=false / 404; not 500 | **GAP** |

## AC-9 Badge/Mission validation (422)

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-26 | Badge empty Code → 422 | validation | P0 | POST code="" | 422 | **Covered** — `Badge_Create_EmptyCode_Returns422` |
| BE-TC-13-27 | Badge RewardXp=0 → 422 | validation | P1 | POST rewardXp=0 | 422 | **Covered** — `Badge_Create_RewardXpZero_Returns422` |
| BE-TC-13-28 | Mission empty Code → 422 | validation | P0 | POST code="" | 422 | **Covered** — `Mission_Create_EmptyCode_Returns422` |
| BE-TC-13-29 | **Badge invalid Rarity / TriggerType enum → 422** | validation/boundary | P2 | POST rarity=99 | 422 (IsInEnum) | **GAP** — enum validation only tested for league tier, not badge enums |
| BE-TC-13-30 | **Mission invalid Cadence / TargetType enum → 422** | validation/boundary | P2 | POST cadence=99 | 422 | **GAP** |
| BE-TC-13-31 | **Mission Target=0 / RewardXp=0 → 422** | validation | P2 | POST target=0 | 422 (GreaterThan(0)) | **GAP** — mission numeric-bound validators untested |

## AC-3 Mission CRUD

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-32 | Create mission → in admin list | functional | P0 | POST then GET | in list by code | **Covered** — `Mission_Create_AppearsInList` |
| BE-TC-13-33 | Update mutable fields persists | persistence | P1 | PUT then DB read | IconKey updated | **Covered** — `Mission_Update_MutableFields_Persists` |
| BE-TC-13-34 | Deactivate → still in list, isActive=false | state | P0 | PATCH then GET | present, isActive=false | **Covered** — `Mission_Deactivate_AdminListStillShowsIt` |
| BE-TC-13-35 | Reactivate → isActive=true | state | P1 | PATCH then DB read | isActive=true | **Covered** — `Mission_Reactivate_IsActiveRestored` |
| BE-TC-13-36 | **Duplicate mission Code → rejected** | negative | P1 | POST same code ×2 | second Successed=false | **GAP** — badge dup-code is tested; mission dup-code is not (symmetry gap; confirm mission has a unique-code rule) |

## AC-4 Timed events

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-37 | Create valid → Successed=true | functional | P0 | POST valid | 200/201, Successed=true | **Covered** — `TimedEvent_Create_ValidParams_Succeeds` |
| BE-TC-13-38 | Start ≥ End → 422 | validation | P0 | POST bad window | 422 | **Covered** — `TimedEvent_Create_InvalidWindow_Returns422` |
| BE-TC-13-39 | Multiplier > 5 → 422 | boundary | P0 | POST mult=6 | 422 | **Covered** — `TimedEvent_Create_MultiplierTooHigh_Returns422` |
| BE-TC-13-40 | Multiplier < 1 → 422 | boundary | P0 | POST mult=0.5 | 422 | **Covered** — `TimedEvent_Create_MultiplierTooLow_Returns422` |
| BE-TC-13-41 | Create then activate → IsActive=true | state | P0 | POST then activate | IsActive=true | **Covered** — `TimedEvent_CreateThenActivate_Succeeds` |
| BE-TC-13-42 | Activate already-active → Successed=false | negative | P1 | activate ×2 | rejected | **Covered** — `TimedEvent_Activate_AlreadyActive_Rejected` |
| BE-TC-13-43 | Activate then expire → IsActive=false | state | P0 | activate then expire | IsActive=false | **Covered** — `TimedEvent_ActivateThenExpire_Succeeds` |
| BE-TC-13-44 | Expire already-inactive → Successed=false | negative | P1 | expire inactive | rejected | **Covered** — `TimedEvent_Expire_AlreadyInactive_Rejected` |
| BE-TC-13-45 | Update window/multiplier persists | persistence | P1 | PUT then DB read | Multiplier+NameEn updated | **Covered** — `TimedEvent_Update_WindowAndMultiplier_Persists` |
| BE-TC-13-46 | **Activate unknown id → Successed=false / 404 (not 500)** | negative | P1 | POST 99999999/activate | rejected, not 500 | **GAP** — activate/expire on a missing event untested |
| BE-TC-13-47 | **Multiplier == 1 and == 5 (inclusive boundaries) → success** | boundary | P2 | POST mult=1, mult=5 | both accepted | **GAP** — only out-of-range (0.5, 6) is tested; the inclusive [1,5] boundaries are not |
| BE-TC-13-48 | **PUT TimedEvents/{id} with Start ≥ End → 422** | validation | P2 | PUT bad window | 422 | **GAP** — window validation tested only on create, not update |

## AC-5 Streak freeze

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-49 | Count=1 → FreezeBalance increases (≤MaxFreezes) | functional | P0 | grant 1 | balance +1, ≤2 | **Covered** — `StreakFreeze_Grant_Count1_IncreasesFreezeBalance` |
| BE-TC-13-50 | Count=5 (>MaxFreezes) → 422 | validation | P0 | grant 5 | 422 | **Covered** — `StreakFreeze_Grant_CountExceedsMaxFreezes_Returns422` |
| BE-TC-13-51 | Count=0 → 422 | validation | P1 | grant 0 | 422 | **Covered** — `StreakFreeze_Grant_Count0_Returns422` |
| BE-TC-13-52 | Empty Reason → 422 | validation | P0 | grant reason="" | 422 | **Covered** — `StreakFreeze_Grant_EmptyReason_Returns422` |
| BE-TC-13-53 | Confirm=false → 422 | validation | P0 | grant confirm=false | 422 | **Covered** — `StreakFreeze_Grant_ConfirmFalse_Returns422` |
| BE-TC-13-54 | Non-existent child → Successed=false | negative | P1 | grant to 99999999 | rejected | **Covered** — `StreakFreeze_Grant_NonExistentChild_Rejected` |
| BE-TC-13-55 | **Grant to a child already at MaxFreezes (Count=2 then more) → balance capped, Successed=false** | boundary | P1 | grant up to 2, then grant again | balance never exceeds MaxFreezes=2; over-cap grant rejected | **GAP** — the suite header documents the "balance-at-max → Successed=false" handler path but no test exercises a child *already at the cap* (validator-cap is tested, handler-cap is not) |

## AC-1 League tier

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-56 | Empty Reason → 422 | validation | P0 | override reason="" | 422 | **Covered** — `LeagueTierOverride_EmptyReason_Returns422` |
| BE-TC-13-57 | Confirm=false → 422 | validation | P0 | override confirm=false | 422 | **Covered** — `LeagueTierOverride_ConfirmFalse_Returns422` |
| BE-TC-13-58 | Invalid tier enum (99) → 422 | validation/boundary | P0 | override newTier=99 | 422 | **Covered** — `LeagueTierOverride_InvalidEnumValue_Returns422` |
| BE-TC-13-59 | Non-existent child → Successed=false | negative | P1 | override 99999999 | rejected | **Covered** — `LeagueTierOverride_NonExistentChild_Rejected` |
| BE-TC-13-60 | Override persists new CurrentTier | persistence | P0 | override Bronze→Silver | DB CurrentTier=Silver | **Covered** — `LeagueTierOverride_PersistsNewTier` |
| BE-TC-13-61 | No-op same tier → Successed=false | negative | P1 | override to current tier | rejected | **Covered** — `LeagueTierOverride_NoOp_SameTier_Rejected` |

## AC-6 Audit E2E (relay)

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-62 | GrantStreakFreeze → audit row (action+adminUserId+target+PII-safe) | persistence | P0 | grant; poll Log | row action=Gamification.StreakFreezeGranted, target=StudentXpProfile, no PII | **Covered** — `Audit_GrantStreakFreeze_ProducesAuditRow` |
| BE-TC-13-63 | OverrideLeagueTier → audit row | persistence | P0 | override; poll Log | action=Gamification.LeagueTierOverridden | **Covered** — `Audit_OverrideLeagueTier_ProducesAuditRow` |
| BE-TC-13-64 | CreateBadge → audit row | persistence | P1 | create; poll Log | action=Badge.Created | **Covered** — `Audit_CreateBadge_ProducesAuditRow` |
| BE-TC-13-65 | ActivateTimedEvent → audit row | persistence | P1 | activate; poll Log | action=TimedEvent.Activated | **Covered** — `Audit_ActivateTimedEvent_ProducesAuditRow` |
| BE-TC-13-66 | **Deactivate/Update badge & mission → audit rows** | persistence | P2 | PATCH/PUT badge & mission; poll Log | rows for Badge.Deactivated / Mission.Updated etc. (confirm exact strings) | **GAP** — only Create+Activate audit paths tested; the deactivate/update producers are unverified (these are AC-1 "badge/mission catalog edits" audited actions) |
| BE-TC-13-67 | **CreateMission / CreateTimedEvent → audit rows** | persistence | P2 | create; poll Log | Mission.Created / TimedEvent.Created rows | **GAP** — badge create is audit-tested but mission/timed-event create are not |
| BE-TC-13-68 | **One override → exactly one audit row (no double-write)** | persistence/idempotency | P1 | single override; count rows | exactly 1 matching audit row | **GAP** — P7-12 proves no-double-write for Subject.Created; the gamification producers are not asserted for idempotency |

## Seeder precedence

| ID | Title | Type | Pri | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|
| BE-TC-13-69 | Admin-edited badge survives re-seed (seed-if-absent) | regression | P1 | edit then re-run BadgeSeeder | admin Name preserved | **Covered** — `Seeder_AdminEditedBadge_SurvivesReseed` |
| BE-TC-13-70 | **Admin-edited mission survives MissionSeeder re-run** | regression | P2 | edit then re-run MissionSeeder | admin field preserved | **GAP** — seed-if-absent precedence is proven for badges only; missions (same seeder concern per the story Notes) untested |
