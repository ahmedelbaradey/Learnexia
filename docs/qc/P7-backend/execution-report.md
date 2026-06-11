# Phase-7 Backend — Integration Test Execution Report

**Run date:** 2026-06-12
**Branch:** `qc/phase-7-backend` (off latest `main`)
**Harness:** `Learnexia.IntegrationTests` (WebApplicationFactory + Testcontainers PostgreSQL), local Docker 28.4.0
**Filter:** `FullyQualifiedName~P7_`

## Headline result

| Metric | Count |
|---|---|
| **Passed** | **365** |
| **Failed** | **28** |
| Skipped | 0 |
| **Total** | **393** |
| Duration | 4m53s |

The earlier "~127 RED P7 tests" estimate was **wrong** — the suite is 93% green. The 28 failures are **NOT stale fixtures**: they surface **4 real product defects** (3 of them shipped to `main`) plus 2 test-data/interaction issues. No production code was changed in this pass (validation-only).

## Failure buckets (root-cause grouped)

### Bucket A — P7-04 Questions admin: `CorrectAnswer` jsonb mismatch → **REAL DEFECT (C), High** (14 tests)
**Symptom:** `POST /api/Learning/Questions` 500s with `22P02: invalid input syntax for type json` for MCQ and FillInBlank questions. Only TrueFalse succeeds.
**Root cause:** `QuizQuestionConfig.cs:48-50` maps `CorrectAnswer` to a **`jsonb`** column, but the contract + `QuizQuestionTypeValidation` treat `CorrectAnswer` as a **plain scalar string** (MCQ = one of the option strings e.g. `"Paris"`; FillInBlank = `"Cairo"`). Bare strings are invalid JSON → Postgres rejects on insert. TrueFalse only works by accident (`"true"`/`"false"` are valid JSON literals). **This is a real production bug — admin creation of MCQ/FillInBlank questions is broken on `main`, not just in tests.**
**Fix (backend-feature):** either change `CorrectAnswer` column type `jsonb`→`text` (migration; `Options` can stay jsonb since it's always a JSON array/object), or JSON-encode the scalar in the add/edit handler. Then re-run.
**Cascade:** the create-500 makes every downstream P7-04 test fail (ByLesson empty, reorder/edit/delete/activate/StartAttempt all have no question to operate on). Fixing the create unblocks ~13 of the 14.
Affected: `AddMcq_RoundTrip…`, `AddFillInBlank_RoundTrip…`, `AddQuestion_ResponseEnvelope…`, `AddQuestion_AppendSemantics…`, `EditMcq…`, `GetById…`, `DeleteQuestion…`, `DeletedQuestion_ExcludedFromStudentStartAttempt`, `DeactivateQuestion…` (×2), `ReactivateQuestion…`, `ReorderQuestions…` (×2), `StartAttempt_CorrectAnswer_NeverLeaksToStudent`.

### Bucket B — P7-07 Account delete: 500 on delete/cascade → **REAL DEFECT (C), High** (5 tests)
**Symptom:** `DELETE` account (soft-delete + cascade-children) returns 500 `حدث خطأ أثناء تنفيذ عملية دورة حياة الحساب` ("error executing account-lifecycle operation"). Suspend/Reactivate work fine, so `AccountStatus` column + migration are present. The fault is specific to the **delete path** (the `IIdentityDbTransaction` cascade / soft-delete write).
**Knock-on:** `P707_Suspend_DeletedAccount_Rejected` fails because the account never actually reaches `Deleted` state (delete 500'd), so a later suspend wrongly succeeds.
**Fix (backend-feature):** inspect the inner exception in the delete-account command handler / `IIdentityDbTransaction` cascade. Then re-run.
Affected: `P707_Delete_CascadeChildren…`, `P707_Delete_SoftDelete_NotPhysicallyRemoved`, `P707_Delete_SoftDeletes_HiddenFromDefaultSearch`, `P707_Delete_AlreadyDeleted_Rejected`, `P707_Suspend_DeletedAccount_Rejected`.

### Bucket C — P7-12 Audit: curriculum **CREATE** not audited → **REAL DEFECT (C), Medium** (4 tests)
**Symptom:** Creating a Subject via admin produces **no `Subject.Created` audit row**, while `Subject.Activated`, `Content.Published`, `Account.Suspended`, `Child.GradeOverridden` etc. **do** appear (confirmed in the FILTER-4 dump — the relay works). So the audit relay is wired correctly; the **curriculum create handlers don't raise `AdminActionPerformedDomainEvent`** (only the lifecycle/activate/identity paths do).
**Fix (backend-feature):** raise the admin-action domain event in the Subject/Unit/Lesson/etc. **Create** handlers (post-commit, per ADR 0002). Then re-run.
Affected: `E2E1_SubjectCreate_ProducesAuditRow`, `E2E2_AuditRow_HasCorrectFields…`, `Idem1_OneAction_ExactlyOneAuditRow`, `Pii1_AuditLogDto_HasNoNameOrEmailFields`.

### Bucket D — P7-12 Audit: date filter + unpopulated `CreatedAt` → **REAL DEFECT (C), Low** (1 test)
**Symptom:** `Filter4_ByDateRange` returns rows whose `occurredAtUtc` is **after** `dateTo`. The dump also shows **every** row has `"createdAt":"0001-01-01T00:00:00"` — `AuditLog.CreatedAt` (from `CreationAuditedEntity`) is never stamped. The `dateFrom/dateTo` filter almost certainly keys off the unpopulated `CreatedAt` instead of `OccurredAtUtc`, so the range is effectively ignored.
**Fix (backend-feature):** filter on `OccurredAtUtc` (and/or stamp `CreatedAt`). Then re-run.
Affected: `Filter4_ByDateRange_ReturnsOnlyRowsInRange`.

### Bucket E — P7-01 duplicate Subject code → **TEST-DATA (A/B)** (1 test)
**Symptom:** `AC5_SubjectDeactivate_HidesFromStudentForGrade` fails at setup — subject create returns 400 `توجد بالفعل شجرة مادة بهذا الرمز واللغة لهذه المرحلة الدراسية` ("a subject tree with this code+language already exists for this grade"). The P8-02 UNIQUE `(SubjectCode, Language, Grade)` constraint rejects the test's non-unique code.
**Fix (test):** give the test a unique `SubjectCode`/grade per run. **api-tester/test fix — no product change.**

### Bucket F — P7-05 publish→visibility + P7-03 skill round-trip → **NEEDS CONFIRMATION** (2 tests)
- `P7_05 PublishedSubject_VisibleToStudentForGrade` (AC-LEAK-2): after publish, the subject is **not** visible on student ForGrade (`found False`). Likely the **P8-03 learning-language filter** — the test's created subject's `SubjectCode`/`Language` doesn't match the student's learning language (`ar`), so it's filtered out. Probably **(B) stale test** (predates P8-02/03 bilingual filtering) but could be a real interaction gap — confirm by checking the created subject's language vs the student claim.
- `P7_03 Skill_CrudRoundTrip`: `GetById` after update/delete returns **500** (`Internal Server Error`). Needs inner-exception inspection — real-defect candidate.

## Triage summary

| Bucket | Tests | Category | Owner | Blocks `main` quality? |
|---|---|---|---|---|
| A — Questions `CorrectAnswer` jsonb | 14 | **(C) Real defect, High** | backend-feature | Yes — MCQ/FIB question authoring broken in prod |
| B — Account delete 500 | 5 | **(C) Real defect, High** | backend-feature | Yes — admin delete broken in prod |
| C — Curriculum create not audited | 4 | **(C) Real defect, Med** | backend-feature | Partial — audit completeness gap |
| D — Audit date filter / CreatedAt | 1 | **(C) Real defect, Low** | backend-feature | Minor |
| E — Duplicate subject code | 1 | (A/B) Test-data | api-tester | No |
| F — Publish visibility / skill 500 | 2 | Needs confirmation | api-tester→backend-feature | TBD |

## P7-09/10/11

No test failures attributable to the unbuilt P7-09 (moderation queue) / P7-10 (analytics) / P7-11 (AI-safety) stories — they have no integration tests in the suite (correct; they're blocked on upstream phases). Nothing quarantined.

## Recommended next steps
1. **backend-feature** (real defects, in priority order): A (Questions jsonb) → B (account delete 500) → C (curriculum-create audit) → D (audit date filter). A + B are the priority — they are **shipped production bugs**, not test artifacts.
2. **api-tester / test fix**: E (unique subject code), then confirm F.
3. Re-run `FullyQualifiedName~P7_`, then the **full** integration suite to confirm no P1/P2/P4/P8 regressions.

## Note
This pass ran tests only and changed **no** code (production or test). The buckets above are the validated triage; fixes are deferred to the appropriate agents per the workflow.
