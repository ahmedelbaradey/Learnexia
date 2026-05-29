# Security Audit — P2-08 Record Granular Per-Question Answers

Branch: `feat/P2-08-record-granular-answers` · Auditor: security-auditor · Date: 2026-05-29
Story sensitivity: student/child data — per-question answers, per-student progress, ownership-scoped reads.

---

## 1. Scope

### Files audited (P2-08 Batches 2–4 only)

| Layer | File |
|---|---|
| Application / Commands | `Features/Attempts/Commands/SubmitAnswer/SubmitAnswerCommand.cs` |
| Application / Commands | `Features/Attempts/Commands/SubmitAnswer/SubmitAnswerCommandHandler.cs` |
| Application / Commands | `Features/Attempts/Commands/CompleteAttempt/CompleteAttemptCommand.cs` |
| Application / Commands | `Features/Attempts/Commands/CompleteAttempt/CompleteAttemptCommandHandler.cs` |
| Application / Commands | `Features/Attempts/Commands/AbandonAttempt/AbandonAttemptCommand.cs` |
| Application / Commands | `Features/Attempts/Commands/AbandonAttempt/AbandonAttemptCommandHandler.cs` |
| Application / Queries | `Features/Attempts/Queries/GetStudentAttempts/GetStudentAttemptsQuery.cs` |
| Application / Queries | `Features/Attempts/Queries/GetStudentAttempts/GetStudentAttemptsQueryHandler.cs` |
| Application / Queries | `Features/Attempts/Queries/GetSkillStats/GetSkillStatsQuery.cs` |
| Application / Queries | `Features/Attempts/Queries/GetSkillStats/GetSkillStatsQueryHandler.cs` |
| Application / DTOs | `Features/Attempts/Dtos/SubmitAnswerDto.cs` |
| Application / DTOs | `Features/Attempts/Dtos/SubmitAnswerResponse.cs` |
| Application / DTOs | `Features/Attempts/Dtos/AttemptSummaryDto.cs` |
| Application / DTOs | `Features/Attempts/Dtos/AttemptListItemDto.cs` |
| Application / DTOs | `Features/Attempts/Dtos/SkillStatsDto.cs` |
| Application / Validation | `Features/Attempts/Validation/SubmitAnswerValidation.cs` |
| Application / Validation | `Features/Attempts/Validation/CompleteAttemptValidation.cs` |
| Application / Validation | `Features/Attempts/Validation/AbandonAttemptValidation.cs` |
| Application / Mapping | `Mapping/QuizProfile.cs` |
| Api / Controllers | `Api/Controllers/QuizzesController.cs` (3 new actions) |
| Api / Controllers | `Api/Controllers/StudentsController.cs` (new) |
| Api / Controllers | `Api/Controllers/SkillsController.cs` (new `Stats` action) |

### Cross-referenced (load-bearing, not changed in P2-08 but directly invoked)

- `Shared.Kernel/Responses/BaseResponseHandler.cs` — `ServerError<T>` signature
- `Shared.Kernel/Abstractions/ICurrentUserService.cs` — `UserId` type (`int?`)
- `Infrastructure/Persistence/LearningDbContext.cs` — `SaveChangesAsync(userId)` timestamp convention
- `Infrastructure/Service/AttemptService.cs` — `StartNewAsync` uses `DateTime.UtcNow` for `StartedAt`
- `Infrastructure/Persistence/Configurations/StudentAnswerConfig.cs` — `AnswerPayload` column definition

### Out of scope

- Pre-existing P2-06 `StartAttemptCommandHandler` (not changed; not in the batch)
- Frontend (no FE surface for P2-08)
- Pre-existing `SkillsController` CRUD actions (List/GetById/Create/Update/Delete) — missing `[Authorize]` on those is a pre-existing gap, not introduced by P2-08; noted in finding F-07 for awareness

---

## 2. Methodology

Seven focus areas from the P2-08 Batch 6 plan were inspected:

1. JWT-derived `StudentId` — never client-supplied across all three write commands
2. Ownership enforcement on writes (`SubmitAnswer`, `CompleteAttempt`, `AbandonAttempt`)
3. IDOR on read endpoints (`GetStudentAttempts`, `GetSkillStats`)
4. `CorrectAnswer` field not in `AttemptListItemDto`, `SkillStatsDto`, `AttemptSummaryDto`; `QuizProfile` mapping audit
5. `ex.Message` not leaked — `ServerError<T>()` pattern (no message argument)
6. `TimeSpentSeconds` upper-bound validator (≤ 3600)
7. Cross-lesson answer injection guard in `SubmitAnswerCommandHandler`

Additional checks performed per the standard checklist:
- Controller authorization decorators
- Mass-assignment / over-posting in `SubmitAnswerCommand → StudentAnswer` mapping
- `AnswerPayload` input size — unbounded string passed to DB and used in a correctness comparison
- `DateTime.Now` vs `DateTime.UtcNow` consistency for `DurationSeconds` correctness
- Dependency vulnerability scan (`dotnet list … --vulnerable`)

---

## 3. Findings

### F-01 — Low — `AnswerPayload` has no maximum-length validator

**Location:** `SubmitAnswerValidation.cs` · `StudentAnswerConfig.cs:21` (no `HasMaxLength` on `AnswerPayload`)

**Description:** The `AnswerPayload` field is validated as `NotEmpty` but has no upper-length bound. The EF column type is `text` (unlimited). A malicious or buggy client can send a multi-megabyte string which: (a) is persisted to the DB verbatim, inflating the `StudentAnswers` table; (b) is passed into a case-insensitive string comparison against `question.CorrectAnswer` — a very large string against a short `CorrectAnswer` triggers an O(n) string operation at the application tier. For a children's platform that will aggregate millions of answer rows this is a storage and compute risk.

**Recommendation:** Add `.MaximumLength(4096)` (or an appropriate per-`QuestionType` maximum, such as 500 for text answers) to `SubmitAnswerValidation.RuleFor(x => x.AnswerPayload)`, and add `.HasMaxLength(4096)` to `StudentAnswerConfig.AnswerPayload` (backed by a migration column type change from `text` to `varchar(4096)`). The validator fires before the handler touches the DB.

---

### F-02 — Low — `DateTime.Now` / `DateTime.UtcNow` inconsistency produces potentially incorrect `DurationSeconds` on non-UTC hosts

**Location:** `CompleteAttemptCommandHandler.cs:150` · `AbandonAttemptCommandHandler.cs:149` (both `RecomputeAggregates` private methods)

**Description:** `AttemptService.StartNewAsync` sets `attempt.StartedAt = DateTime.UtcNow` (UTC). Both `RecomputeAggregates` methods compute duration as `(DateTime.Now - attempt.StartedAt).TotalSeconds`. When the host's local timezone is not UTC (e.g., Egypt, UTC+2), `DateTime.Now` exceeds `DateTime.UtcNow` by the offset, producing a `DurationSeconds` value that is inflated by the offset in seconds (7200 for UTC+2). The comment in the code attributes this to matching the `LearningDbContext` audit-stamp convention (`DateTime.Now`), but the `LearningDbContext` stamps `CreatedAt` (an audit field), while `StartedAt` was set with `UtcNow` — the mismatch is therefore a bug, not an intentional alignment. While this does not directly violate a security control, it causes incorrect adaptive-learning signals (inflated duration statistics for all completed/abandoned attempts) and is a data-integrity defect on a children's learning platform where timing drives adaptivity.

**Recommendation:** Change both `RecomputeAggregates` methods to use `DateTime.UtcNow` for `now`, consistent with `StartedAt`'s origin. If `LearningDbContext` is ever moved to UTC audit stamps, this will already be correct. The comment referencing the audit-stamp justification should be removed.

---

### F-03 — Info — `CorrectAnswer` present in `SubmitAnswerResponse` (by design — conditional disclosure)

**Location:** `SubmitAnswerResponse.cs:17` · `SubmitAnswerCommandHandler.cs:113`

**Description:** `SubmitAnswerResponse` contains a `CorrectAnswer` field. The handler sets it to `null` when `IsCorrect == true` and populates it only when `IsCorrect == false`. The plan explicitly states this is the P2-07 feedback contract ("null when correct so client never gets it for free"). This is an intentional design decision documented in the plan. Confirmed: the field is `null` on correct answers; the client learns the correct answer only after giving a wrong one. No regression against earlier `QuizQuestionDto` exclusion.

**No action required.** Documented here for the reviewer's awareness that the field is present but conditionally populated.

---

### F-04 — Info — Pre-existing `SkillsController` CRUD actions (List/GetById/Create/Update/Delete) have no `[Authorize]`

**Location:** `SkillsController.cs:17-35` (pre-existing actions, not added by P2-08)

**Description:** The five CRUD actions on `SkillsController` that predate P2-08 have no `[Authorize]` attribute. The new `GetSkillStats` action added by P2-08 at line 44 correctly carries `[Authorize]`. The existing gap is a known codebase pattern (CONVENTIONS.md §13: "Permission policies exist but aren't enforced") and was not introduced by this PR. However, because P2-08 modified this file to add the new action, the reviewer's attention should be directed to it.

**No blocking action for P2-08.** Flag for a separate hardening pass (curriculum-authoring access control) when that story ships, consistent with the note in `GradesController.cs`.

---

### F-05 — Info — Generic `[Authorize]` on read endpoints (no role or policy constraint)

**Location:** `StudentsController.cs:23` · `SkillsController.cs:45`

**Description:** Both new read endpoints use `[Authorize]` without a role or permission policy. This means any authenticated user (Parent, Admin, or a future role) who possesses a valid JWT can reach these endpoints; the handler's IDOR guard (`studentId == currentUser.UserId`) is the only access control enforced. For Phase 2 this is the documented scope — "Parent/admin scoping is deferred to Phase 5/7" — and the IDOR guard is present and correct. The risk is that an admin or parent user who happens to share a `UserId` integer with a student (impossible in ASP.NET Identity's single user table, since IDs are unique, but worth verifying) could call these endpoints. In practice, since all users are in the same Identity table and IDs are unique, the IDOR guard is effective.

**No blocking action.** Acknowledged as a deferred Phase 5/7 concern per the plan. Recommend adding `[Authorize(Roles = "Student")]` as a defence-in-depth improvement for Phase 5 when parent/admin scoping is implemented.

---

### F-06 — Info — Dependency vulnerability scan

**Command:** `dotnet list backend/Learnexia.Modular.sln package --vulnerable`

**Result:** No vulnerable packages found across all 30 projects in the solution (Host, all module layers, all test projects).

---

## 4. Verified Correct (No Finding)

### FA-1 — JWT-derived StudentId (Focus Area 1): PASS

All three write-command handlers (`SubmitAnswer`, `CompleteAttempt`, `Abandon`) resolve `studentId` exclusively from `_currentUser.UserId` on the first line of the handler body. `StudentId` is absent from `SubmitAnswerDto`, `CompleteAttemptCommand`, and `AbandonAttemptCommand`. The `AttemptId` route value is injected by the controller (`command with { AttemptId = attemptId }`) and identifies the attempt — not the student. Confirmed compliant with AC-6.

### FA-2 — Ownership enforcement on writes (Focus Area 2): PASS

All three handlers perform the ownership check at step 3 (before any state mutation or DB write is staged):
- `SubmitAnswer`: line 66 — `attempt.StudentId != studentId.Value → Unauthorized`
- `CompleteAttempt`: line 73 — `attempt.StudentId != studentId.Value → Unauthorized`
- `AbandonAttempt`: line 72 — `attempt.StudentId != studentId.Value → Unauthorized`

The check happens after the attempt is loaded (step 2) and before any answer row, status update, or aggregate recompute is staged (steps 5+). Ordering is correct.

### FA-3 — IDOR on read endpoints (Focus Area 3): PASS

- `GetStudentAttemptsQueryHandler` line 60: `currentUserId is null || request.StudentId != currentUserId.Value → Unauthorized`
- `GetSkillStatsQueryHandler` line 59: `currentUserId is null || request.StudentId != currentUserId.Value → Unauthorized`

Both checks gate on the JWT-resolved identity against the route/query-parameter supplied ID. Student A cannot read Student B's attempts or skill stats.

### FA-4 — CorrectAnswer not in client-facing list/stats DTOs (Focus Area 4): PASS

- `AttemptListItemDto`: no `CorrectAnswer` field. Comment: "SECURITY: CorrectAnswer is intentionally absent."
- `SkillStatsDto`: no `CorrectAnswer` field. Comment: "SECURITY: CorrectAnswer is intentionally absent."
- `AttemptSummaryDto`: no `CorrectAnswer` field.
- `QuizProfile`: `CreateMap<Attempt, AttemptListItemDto>()` and `CreateMap<Attempt, AttemptSummaryDto>()` — `Attempt` has no `CorrectAnswer` field, so no exclusion needed there. `CreateMap<QuizQuestion, QuizQuestionDto>()` retains the pre-existing `ForSourceMember(src => src.CorrectAnswer, opt => opt.DoNotValidate())` exclusion. `CreateMap<SubmitAnswerCommand, StudentAnswer>()` ignores `IsCorrect` (handler-computed). No new mapping path introduces `CorrectAnswer` into a list/stats DTO.

### FA-5 — ex.Message not leaked (Focus Area 5): PASS

All five handlers follow the identical pattern:

```
catch (Exception ex)
{
    _logger.LogError(ex, "Error in <HandlerName>");
    return ServerError<T>();
}
```

`ServerError<T>()` is called with zero arguments. `BaseResponseHandler.ServerError<T>(string? message = null)` defaults to `"Internal Server Error."` — no exception text surfaces to the client. The full exception (including stack trace) is passed to `_logger.LogError(ex, ...)` server-side only. Confirmed in all five handlers.

### FA-6 — TimeSpentSeconds upper bound (Focus Area 6): PASS

`SubmitAnswerValidation` line 30–33:

```csharp
RuleFor(x => x.TimeSpentSeconds)
    .GreaterThanOrEqualTo(0)
    .LessThanOrEqualTo(3600)
```

The 3600-second (1-hour) ceiling is present and will reject inflated values at the `ValidationBehavior` pipeline stage before the handler runs.

### FA-7 — Cross-lesson answer injection guard (Focus Area 7): PASS

`SubmitAnswerCommandHandler` step 5 (line 75–81):

```csharp
var question = _repository.Learning
    .GetByCondition<QuizQuestion>(
        q => q.Id == request.QuestionId && q.LessonId == attempt.LessonId,
        trackChanges: false)
    .FirstOrDefault();
if (question is null)
    return NotFound<SubmitAnswerResponse>(_localizer[SharedResourcesKey.QuestionNotFound]);
```

The predicate includes `q.LessonId == attempt.LessonId`, so a question from a different lesson returns null → `NotFound`. A student cannot inject an answer from a different lesson into the current attempt.

### FA-8 — Mass-assignment / over-posting: PASS

`CreateMap<SubmitAnswerCommand, StudentAnswer>().ForMember(dest => dest.IsCorrect, opt => opt.Ignore())`: `IsCorrect` is computed server-side and set explicitly after mapping. `AnswerPayload`, `TimeSpentSeconds`, `HintUsed`, `AttemptId`, `QuestionId` are the only fields that map from the command. There is no path for a client to supply `StudentId`, `CreatedBy`, audit timestamps, or `IsCorrect` through the mapping layer.

### FA-9 — Dependency scan: PASS

No vulnerable packages found (see F-06).

---

## 5. Verdict

**PASS-WITH-NOTES**

No Critical or High findings. All seven Batch 6 focus areas verified as correctly implemented. Two Low findings (F-01 `AnswerPayload` unbounded; F-02 `DateTime.Now`/`UtcNow` mismatch producing incorrect duration statistics) and three Info findings (F-03 conditional `CorrectAnswer` in response — by design; F-04 pre-existing missing auth on SkillsController CRUD; F-05 generic `[Authorize]` on read endpoints) are documented but do not block the reviewer gate.

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 2 |
| Info | 4 |

### Top follow-up items (non-blocking)

1. **(F-01)** Add `MaximumLength(4096)` to `SubmitAnswerValidation.RuleFor(x => x.AnswerPayload)` and a matching `HasMaxLength` in `StudentAnswerConfig` to prevent unbounded storage/comparison cost.
2. **(F-02)** Change `RecomputeAggregates` in both `CompleteAttemptCommandHandler` and `AbandonAttemptCommandHandler` to use `DateTime.UtcNow` instead of `DateTime.Now` — `StartedAt` is `UtcNow`; the delta must be computed in the same timezone to avoid inflated duration signals by the server's local UTC offset.
3. **(F-04/F-05)** Add `[Authorize(Roles = "Student")]` to the two new read endpoints when Phase 5 parent/admin scoping ships; add `[Authorize]` to the pre-existing SkillsController CRUD actions when curriculum-authoring access control ships.
