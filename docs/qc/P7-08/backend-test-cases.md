# P7-08 — Manage child profiles & grade overrides — Backend (API) test cases

**Target agent:** `api-tester`
**Surface:** `AdminUsersController` child-management endpoints, class-level `AdminOnly`.
- `PATCH api/Admin/Users/{childId}/profile`           — `UpdateChildProfileCommand` (harmless field write)
- `POST  api/Admin/Users/{childId}/grade`             — `OverrideChildGradeCommand` (NON-destructive; confirm=false → **400**)
- `POST  api/Admin/Users/{childId}/learning-language` — `AdminChangeLearningLanguageCommand` (DESTRUCTIVE; confirmFreshStart=false → **424**)
**Baseline:** `P7_06_07_08_UserAccountAdmin_Tests.cs` + `P7_12_AuditLog_Tests.cs`.
**Key distinction:** grade override is **non-destructive** → soft 400 gate, preserves XP/attempts. Learning-language change is **destructive** (hard-deletes Math/Science attempts) → hard 424 gate, mirrors parent P8-04. All three require the target to hold the **Student** role.

Legend: **Covered** (cite method) / **GAP** / **PARTIAL**.

---

## A. Auth / authz matrix

| ID | Title | Type | Pri | Steps | Expected | Status / existing test |
|----|-------|------|-----|-------|----------|------------------------|
| BE-TC-08-01 | PATCH profile anonymous → 401 | auth | P0 | no bearer | 401 | **Covered** — `P708_UpdateProfile_Anonymous_Returns401` |
| BE-TC-08-02 | PATCH profile parent → 403 | authz | P0 | parent bearer | 403 | **Covered** — `P708_UpdateProfile_Parent_Returns403` |
| BE-TC-08-03 | PATCH profile basic-role → 403 | authz | P1 | basic bearer | 403 | **GAP** |
| BE-TC-08-04 | POST grade anonymous → 401 | auth | P0 | no bearer | 401 | **Covered** — `P708_OverrideGrade_Anonymous_Returns401` |
| BE-TC-08-05 | POST grade parent → 403 | authz | P0 | parent bearer | 403 | **GAP** — grade parent-403 untested |
| BE-TC-08-06 | POST grade basic-role → 403 | authz | P1 | basic bearer | 403 | **GAP** |
| BE-TC-08-07 | POST learning-language anonymous → 401 | auth | P0 | no bearer | 401 | **Covered** — `P708_ChangeLearningLanguage_Anonymous_Returns401` |
| BE-TC-08-08 | POST learning-language parent → 403 | authz | P0 | parent bearer | 403 | **Covered** — `P708_ChangeLearningLanguage_Parent_Returns403` |
| BE-TC-08-09 | POST learning-language basic-role → 403 | authz | P1 | basic bearer | 403 | **GAP** |
| BE-TC-08-10 | Expired admin JWT on any P7-08 write → 401 | auth | P1 | expired admin token | 401, not 500 | **GAP** |

---

## B. Update child profile (PATCH …/profile) — harmless field write (AC-1)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-08-20 | Happy path: preferredLanguage + country update, reflected on profile | functional | P0 | child grade=2 EG | PATCH `{preferredLanguage:"en",country:"SA"}` → GET profile | 200; profile `nationality=SA` | **Covered** — `P708_UpdateProfile_HappyPath_NoPorgressImpact` |
| BE-TC-08-21 | preferredLanguage updated (verify on profile) | functional | P1 | child | PATCH `{preferredLanguage:"en"}` → GET profile | `preferredLanguage="en"` | **PARTIAL** — happy-path asserts nationality only, not preferredLanguage value |
| BE-TC-08-22 | No progress impact: XP/attempts unchanged after profile edit | persistence | P1 | child w/ some attempts | PATCH profile → count attempts/XP | counts unchanged (harmless write) | **GAP** — "no progress affected" not directly asserted for profile edit |
| BE-TC-08-23 | Null fields = no change (partial update) | functional | P2 | child country=EG | PATCH `{preferredLanguage:"en"}` (country omitted) | country still EG; preferredLanguage updated | **GAP** — partial/null-means-no-change semantics untested |
| BE-TC-08-24 | Update profile on a non-child (parent) → rejected | validation | P0 | parent | PATCH `…/{parentId}/profile` | rejected (4xx/successed=false) | **Covered** — `P708_UpdateProfile_NonChildUser_Rejected` |
| BE-TC-08-25 | Unsupported preferredLanguage (e.g. "fr") → 422 | validation | P1 | child | PATCH `{preferredLanguage:"fr"}` | 422 (AC: language outside ar/en rejected) | **GAP** — preferredLanguage value validation untested |
| BE-TC-08-26 | Non-existent childId → 404 | negative | P1 | admin | PATCH `…/99999999/profile` | 404/successed=false | **GAP** |

---

## C. Grade override (POST …/grade) — NON-destructive, soft 400 gate (AC-4/5)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-08-30 | grade < 1 → 422 | boundary | P0 | admin | POST `{grade:0,confirm:true}` | 422; successed=false | **Covered** — `P708_OverrideGrade_GradeLessThan1_Returns422` |
| BE-TC-08-31 | grade > 6 → 422 | boundary | P0 | admin | POST `{grade:7,confirm:true}` | 422; successed=false | **Covered** — `P708_OverrideGrade_GradeGreaterThan6_Returns422` |
| BE-TC-08-32 | grade=1 and grade=6 boundaries → accepted | boundary | P2 | child grade=3 | POST grade=1 then grade=6 | both 200 | **GAP** — inclusive boundary accept (1 and 6) untested |
| BE-TC-08-33 | confirm=false → **400** (soft gate, NOT 424) | gate/negative | P0 | child | POST `{grade:3,confirm:false}` | 400; successed=false | **Covered** — `P708_OverrideGrade_ConfirmFalse_Returns400` |
| BE-TC-08-34 | confirm=false leaves grade unchanged (no mutation) | persistence | P1 | child grade=2 | POST `{grade:3,confirm:false}` → DB | grade still 2 | **GAP** — gate test asserts 400 but not "grade unchanged" |
| BE-TC-08-35 | confirm=true happy path → 200, grade updated, DTO has newGrade/oldGrade | functional | P0 | child grade=2 | POST `{grade:5,confirm:true}` → GET profile | 200; `newGrade=5`; profile grade=5 | **Covered** — `P708_OverrideGrade_HappyPath_GradeUpdated` |
| BE-TC-08-36 | Same grade as current → 400 (no-op, no phantom event) | negative/state | P0 | child grade=3 | POST `{grade:3,confirm:true}` | 400; successed=false | **GAP** — the no-op guard (`oldGrade==Grade → BadRequest`) is untested |
| BE-TC-08-37 | NON-DESTRUCTIVE: XP awards + attempts preserved after override | persistence | P0 | child | record counts → override → recount | attempt count + XP-award count unchanged; grade changed | **Covered** — `P708_OverrideGrade_LearningHistoryPreserved` |
| BE-TC-08-38 | Override on a non-child (parent) → rejected | validation | P0 | parent | POST `…/{parentId}/grade` | rejected | **Covered** — `P708_OverrideGrade_NonChildUser_Rejected` |
| BE-TC-08-39 | Non-existent childId → 404 | negative | P1 | admin | POST `…/99999999/grade` confirm=true | 404/successed=false | **GAP** |
| BE-TC-08-40 | Override emits ChildGradeChangedIntegrationEvent (no 500; consumer no-op) | event | P1 | child | override confirm=true | success; no error from the best-effort publish path | **GAP** — event publish path untested (informational for P5-06) |

---

## D. Change learning-language (POST …/learning-language) — DESTRUCTIVE, hard 424 gate (AC-2/3)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-08-50 | confirmFreshStart=false → **424** FailedDependency | gate/negative | P0 | child | POST `{learningLanguage:"en",confirmFreshStart:false}` | 424; successed=false | **Covered** — `P708_ChangeLearningLanguage_NotConfirmed_Returns424` |
| BE-TC-08-51 | Not-confirmed → NO DB mutation (language unchanged) | persistence | P0 | child lang=ar | POST confirmFreshStart=false → DB | `LearningLanguage` still "ar" | **Covered** — `P708_ChangeLearningLanguage_NotConfirmed_NoMutation` |
| BE-TC-08-52 | Not-confirmed → Math/Science attempts NOT deleted | persistence | P0 | child w/ Math/Science attempts | POST confirmFreshStart=false → count attempts | attempt count unchanged (destructive path never ran) | **GAP** — the no-confirm test checks the language field but NOT that attempts survive |
| BE-TC-08-53 | Unsupported language ("fr") → 422 | validation | P0 | admin | POST `{learningLanguage:"fr",confirmFreshStart:true}` | 422; successed=false | **Covered** — `P708_ChangeLearningLanguage_UnsupportedCode_Returns422` |
| BE-TC-08-54 | confirmFreshStart=true → 200, language updated in DB | functional | P0 | child lang=ar | POST `{learningLanguage:"en",confirmFreshStart:true}` → DB | 200; `LearningLanguage="en"` | **Covered** — `P708_ChangeLearningLanguage_Confirmed_UpdatesLanguage` |
| BE-TC-08-55 | Confirmed change → Math/Science attempts hard-deleted (fresh start) | persistence | P0 | child lang=ar w/ Math+Science+Arabic attempts | confirmFreshStart=true → count attempts by subject | Math/Science attempts gone; **Arabic/English attempts retained**; gamification retained | **GAP** — the destructive fresh-start (the core AC-2 behaviour) is entirely unverified |
| BE-TC-08-56 | Confirmed change emits LearningLanguageChangedIntegrationEvent | event | P1 | child | confirmFreshStart=true | event consumed by Learning (cleanup); no 500 | **GAP** |
| BE-TC-08-57 | Same language → no-op success (no event, no reset) | state | P1 | child lang=en | POST `{learningLanguage:"en",confirmFreshStart:true}` | 200 success; attempts NOT deleted; no event | **GAP** — same-language no-op path untested |
| BE-TC-08-58 | Change on a non-child (parent) → rejected | validation | P0 | parent | POST `…/{parentId}/learning-language` | rejected | **Covered** — `P708_ChangeLearningLanguage_NonChildUser_Rejected` |
| BE-TC-08-59 | Non-existent childId → 404 | negative | P1 | admin | POST `…/99999999/learning-language` confirmFreshStart=true | 404/successed=false | **GAP** |
| BE-TC-08-60 | Learning-language claim staleness (Q-C3): old token keeps old value until refresh | auth | P2 | child token | change lang → reuse old access token | old token still has old lang; new value lands on next refresh/sign-in | **GAP** — bounded-staleness behaviour untested (documented in controller XML) |

---

## E. Audit trail (P7-12) — AC: "actor, timestamp, old→new values, reason; audited" — CRITICAL

> Handlers emit `Child.ProfileUpdated`, `Child.GradeOverridden` (Details `oldGrade=…;newGrade=…`), `Child.LearningLanguageChanged`. **No test verifies these audit rows.** Requires Moderation migration + poll on `GET api/Admin/Audit/Log`.

| ID | Title | Type | Pri | Precondition | Steps | Expected | Status / existing test |
|----|-------|------|-----|--------------|-------|----------|------------------------|
| BE-TC-08-80 | Grade override emits `Child.GradeOverridden` audit row with old→new | audit | P0 | child grade=2 | override to 5 → poll `?actionType=Child.GradeOverridden&adminUserId={adminId}` | row: targetEntityType=`User`, targetEntityId={childId}, Details contains `oldGrade=2;newGrade=5`, correct adminUserId | **GAP** |
| BE-TC-08-81 | Learning-language change emits `Child.LearningLanguageChanged` audit row | audit | P0 | child | confirmFreshStart=true → poll `?actionType=Child.LearningLanguageChanged` | matching row with old→new lang metadata | **GAP** |
| BE-TC-08-82 | Profile update emits `Child.ProfileUpdated` audit row | audit | P1 | child | PATCH profile → poll `?actionType=Child.ProfileUpdated` | matching row | **GAP** |
| BE-TC-08-83 | Audit Details for P7-08 actions carries no PII (ids/codes only, no email/name) | PII/audit | P1 | as above | inspect Details of the rows | no `@`/email/password/name; ids + grade/lang codes only | **GAP** |
| BE-TC-08-84 | Rejected actions emit NO audit row (gate-before-audit) | audit/gate | P1 | child grade=2 | grade confirm=false (400) AND lang confirmFreshStart=false (424) → poll | no `Child.GradeOverridden` / `Child.LearningLanguageChanged` row for that target | **GAP** — verifies audit fires only after a successful mutation |

---

## Summary — P7-08 backend

- **Covered:** 14 cases (auth anon for all 3 + parent for profile & language; profile happy + non-child reject; grade 422×2 + confirm=false-400 + happy + non-destructive-history + non-child reject; language 424-gate + no-mutation + unsupported-422 + confirmed-update + non-child reject).
- **GAP:** 27 cases — most valuable: **destructive fresh-start verification (55) + not-confirmed-attempts-survive (52), same-grade no-op (36), audit rows for all three actions (80–84), grade parent-403 (05), unsupported preferredLanguage (25), confirm=false grade-unchanged (34), same-language no-op (57), not-found paths (26/39/59), integration events (40/56).**
- **Headline gap count: 27.**
