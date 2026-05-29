# Security Audit — P2-07 Instant Answer Feedback

Branch: `feat/P2-07-instant-answer-feedback` · Auditor: security-auditor · Date: 2026-05-29
Story sensitivity: low-medium. Extends existing student-answer submission handlers (P2-08 PASS) with per-type correctness logic, integration-event publication, and a new thin read-only repo method.

---

## 1. Scope

### Files audited (P2-07 changes only)

| Layer | File |
|---|---|
| Domain / Services | `Learnexia.Modules.Learning.Domain/Services/AnswerComparator.cs` (new) |
| Application / Commands | `Features/Attempts/Commands/SubmitAnswer/SubmitAnswerCommandHandler.cs` (edited) |
| Application / Commands | `Features/Attempts/Commands/CompleteAttempt/CompleteAttemptCommandHandler.cs` (edited) |
| Application / Abstractions | `Learnexia.Modules.Learning.Application/Abstractions/ILearningRepository.cs` (added `GetLessonSkillIdAsync`) |
| Infrastructure / Repository | `Learnexia.Modules.Learning.Infrastructure/Repository/LearningRepository.cs` (impl) |
| Shared.Contracts / Learning | `Learnexia.Shared.Contracts/Learning/AnswerSubmittedIntegrationEvent.cs` (read-only) |
| Shared.Contracts / Learning | `Learnexia.Shared.Contracts/Learning/LessonCompletedIntegrationEvent.cs` (read-only) |
| Tests — unit | `backend/tests/Modules.Learning.UnitTests/AnswerComparatorTests.cs` (new, read-only audit) |
| Tests — integration | `backend/tests/Learnexia.IntegrationTests/P2_07_InstantAnswerFeedback_Tests.cs` (new, read-only audit) |

### Cross-referenced (load-bearing, not changed in P2-07)

- `Shared.Kernel/Messaging/IsolatedNotificationPublisher.cs` — per-handler exception isolation (FA-5)
- `Host/Extensions/MediatRExtensions.cs:22` — `IsolatedNotificationPublisher` registration (FA-5)
- `docs/dev/adr/0002-domain-events-and-dispatch.md` — ghost-event-on-rollback accepted trade-off (FA-4)
- `BaseResponseHandler.ServerError<T>()` — zero-argument signature (FA-2)

### Out of scope

- Pre-existing P2-08 surfaces (already PASS-audited): `SubmitAnswerValidation`, `CompleteAttemptValidation`, ownership guards, IDOR on read queries, `TimeSpentSeconds` upper bound, mass-assignment in mapping — none changed in P2-07.
- `AbandonAttemptCommandHandler` — confirmed no `// TODO P2-07` present; not touched.
- Frontend (no FE surface for P2-07).

---

## 2. Methodology

Eight focus areas defined in the P2-07 Batch 5 plan were inspected:

1. Event payload data minimization — both event record definitions and the publish call sites
2. `ex.Message` leak — both new `try/catch` blocks and the outer `catch (Exception ex)` fallback
3. Log line content — all new `_logger.LogError` / `_logger.LogWarn` calls
4. Ghost-event-on-rollback risk — comment presence + ADR 0002 reference
5. Handler isolation — `IsolatedNotificationPublisher` registration and the integration test case 11
6. `AnswerComparator` input safety — null/empty guards, no eval/regex/deserialization
7. Authorization unchanged — `[Authorize(Roles="Student")]` presence, ownership guards
8. New repo method `GetLessonSkillIdAsync` — scope, injection risk, read-only status

Additional standard checks:

- Dependency vulnerability scan (`dotnet list backend/Learnexia.Modular.sln package --vulnerable`)
- `StudentId` source (JWT-only, not client-supplied)
- No new cross-module project references or raw SQL introduced

---

## 3. Findings

### F-01 — Low — Log lines use string interpolation instead of structured logging placeholders

**Location:**
- `SubmitAnswerCommandHandler.cs:134` — `_logger.LogError(publishEx, $"P2-07: AnswerSubmittedIntegrationEvent publish failed for AttemptId={attempt.Id}, QuestionId={question.Id}, StudentId={studentId.Value}")`
- `SubmitAnswerCommandHandler.cs:139` — `_logger.LogWarn($"P2-07: AnswerSubmittedIntegrationEvent skipped — QuestionId={question.Id} has no SkillId...")`
- `CompleteAttemptCommandHandler.cs:139` — `_logger.LogError(publishEx, $"P2-07: LessonCompletedIntegrationEvent publish failed for AttemptId={attempt.Id}, LessonId={attempt.LessonId}, StudentId={studentId.Value}")`
- `CompleteAttemptCommandHandler.cs:144` — `_logger.LogWarn($"P2-07: LessonCompletedIntegrationEvent skipped — LessonId={attempt.LessonId} has no SkillId...")`

**Description:** The log messages use C# string interpolation (`$"..."`) rather than the structured logging placeholder syntax (`"... {AttemptId}", attempt.Id`). The fields logged are exclusively opaque integer IDs (`AttemptId`, `QuestionId`, `LessonId`, `StudentId`) — no PII, no answer payload, no `CorrectAnswer` string. The content itself passes the PII check. However, string interpolation loses structured-log queryability: log aggregation tools (Seq, ELK, App Insights) cannot group by `StudentId` or `AttemptId` because the values are baked into the message string rather than emitted as indexed fields. On a children's platform where per-child analytics and incident investigation are needed, this is a maintenance/observability gap. The values are non-sensitive IDs so this is Low, not Medium.

**Recommendation:** Migrate the four log lines to structured-logging syntax, for example:
```csharp
_logger.LogError(publishEx, "P2-07: AnswerSubmittedIntegrationEvent publish failed for AttemptId={AttemptId}, QuestionId={QuestionId}, StudentId={StudentId}", attempt.Id, question.Id, studentId.Value);
_logger.LogWarn("P2-07: AnswerSubmittedIntegrationEvent skipped — QuestionId={QuestionId} has no SkillId", question.Id);
```
This is a non-blocking cleanup consistent with the structured-logging standard used in other handlers in the codebase.

---

### F-02 — Info — Ghost-event-on-rollback risk (accepted Phase-2 trade-off per ADR 0002)

**Location:** `SubmitAnswerCommandHandler.cs:113–135` and `CompleteAttemptCommandHandler.cs:116–145`

**Description:** Both handlers publish their respective integration events inside the `UnitOfWorkBehavior`'s `next()` scope — before `CommitAsync` completes. If `CommitAsync` fails after a successful `_publisher.Publish(...)` call, downstream subscribers have received an event for state that was never persisted. This is the well-understood Phase-2 MVP trade-off documented as Risk R1 in the P2-07 brief.

**Verification:** Both publish blocks carry the comment:
> "Fail-soft: log + continue. We do NOT fail the user request because of a publisher failure. Ghost-event-on-rollback risk is accepted per ADR 0002; outbox is a future hardening story."

This citation directly references ADR 0002 §3, which authorizes in-process best-effort dispatch for MVP. The comment is present in both handlers as required by the plan template.

**Categorized as Info** — documented + accepted. Outbox is a future story (ADR 0002 §5).

---

### F-03 — Info — `IsolatedNotificationPublisher` provides correct defense-in-depth

**Location:** `Shared.Kernel/Messaging/IsolatedNotificationPublisher.cs` · `Host/Extensions/MediatRExtensions.cs:37`

**Description:** Both publishing handlers wrap `_publisher.Publish(...)` in a `try/catch` (handler-side belt). The underlying `IsolatedNotificationPublisher` catches per-handler exceptions independently so one failing subscriber does not abort the publish loop or the originating request (publisher-side suspenders). The two layers are correctly layered.

Integration test case 11 (`HandlerIsolation_ThrowingSubscriber_DoesNotFailApiRequest`) registers a deliberately-throwing `INotificationHandler<AnswerSubmittedIntegrationEvent>` alongside the capturing handler and asserts: HTTP 200 returned, capturing handler received the event. This proves both isolation layers work end-to-end.

**Categorized as Info** — correct defense-in-depth; no action required.

---

### F-04 — Info — Dependency vulnerability scan

**Command:** `dotnet list backend/Learnexia.Modular.sln package --vulnerable`

**Result:** No vulnerable packages found across all 29 projects in the solution (all Host, module, shared, and test projects).

---

## 4. Verified Correct (No Finding)

### FA-1 — Event payload data minimization: PASS

**`AnswerSubmittedIntegrationEvent`** (record definition at `Shared.Contracts/Learning/AnswerSubmittedIntegrationEvent.cs`):
Fields: `Guid EventId`, `DateTime OccurredOnUtc`, `int StudentId`, `int LessonId`, `int SkillId`, `int CorrectAnswerCount`.
No `CorrectAnswer` string, no `AnswerPayload` string, no name, email, DOB, or child-data fields.
The record XML doc explicitly states: "Payload carries opaque int IDs only — NO PII."

**Publish site** (`SubmitAnswerCommandHandler.cs:120–126`) constructs the event using named positional arguments:
- `StudentId: studentId.Value` — JWT-derived
- `LessonId: attempt.LessonId` — opaque int
- `SkillId: question.SkillId.Value` — opaque int
- `CorrectAnswerCount: isCorrect ? 1 : 0` — binary int derived from server-side correctness result

`request.AnswerPayload` and `question.CorrectAnswer` are NOT included in the event. Confirmed: no reflection, no `with` expression, no spread that could accidentally include them.

**`LessonCompletedIntegrationEvent`** (record at `Shared.Contracts/Learning/LessonCompletedIntegrationEvent.cs`):
Fields: `Guid EventId`, `DateTime OccurredOnUtc`, `int StudentId`, `int LessonId`, `int SkillId`, `int AccuracyPercentage`, `int CorrectAnswerCount`.
Same PII check: no correctness strings, no child data. Record doc: "Payload carries opaque int IDs only — NO PII."

**Publish site** (`CompleteAttemptCommandHandler.cs:124–131`):
- `AccuracyPercentage: (int)Math.Round(attempt.AccuracyPercentage)` — aggregate percentage, no raw answer data
- `CorrectAnswerCount: answers.Count(a => a.IsCorrect)` — integer count, no individual answer content

Both events PASS the data-minimization check.

---

### FA-2 — `ex.Message` not leaked to the client: PASS

**`SubmitAnswerCommandHandler.cs`:**
- Inner `catch (Exception publishEx)` at line 130: calls `_logger.LogError(publishEx, "...")` only. Does NOT return to the client; execution continues past the `if/else` block. The user response is assembled at line 143 and returned regardless of publisher success/failure.
- Outer `catch (Exception ex)` at line 155: calls `_logger.LogError(ex, "Error in SubmitAnswerCommand")` and returns `ServerError<SubmitAnswerResponse>()` with zero arguments. `BaseResponseHandler.ServerError<T>(string? message = null)` defaults to `"Internal Server Error."` — no exception text surfaces to the client.

**`CompleteAttemptCommandHandler.cs`:**
- Inner `catch (Exception publishEx)` at line 135: same pattern — log only, execution continues.
- Outer `catch (Exception ex)` at line 159: `ServerError<AttemptSummaryDto>()` zero-argument call.

Both handlers confirmed: `ex.Message` never propagated to the client response.

---

### FA-3 — Log line content (no answer payload or CorrectAnswer): PASS

All four new log lines logged in the P2-07 publish blocks contain:
- `SubmitAnswerCommandHandler.cs:134`: `AttemptId`, `QuestionId`, `StudentId` — opaque integer IDs only.
- `SubmitAnswerCommandHandler.cs:139`: `QuestionId` — opaque integer ID only.
- `CompleteAttemptCommandHandler.cs:139`: `AttemptId`, `LessonId`, `StudentId` — opaque integer IDs only.
- `CompleteAttemptCommandHandler.cs:144`: `LessonId` — opaque integer ID only.

None of the log lines include `request.AnswerPayload`, `question.CorrectAnswer`, student name, email, or any other PII. These fields are not referenced anywhere near the log calls. The "skipped" warning pattern (`QuestionId={question.Id} has no SkillId`) is correct and contains no correctness oracle.

---

### FA-4 — ADR pointer present in both handlers: PASS

Both publish blocks carry a comment explicitly citing "accepted per ADR 0002" for the ghost-event-on-rollback trade-off. See F-02 above for the exact text. The rationale comment was present as specified in the Batch 2/3 plan template.

---

### FA-5 — Handler isolation (`IsolatedNotificationPublisher`): PASS

`IsolatedNotificationPublisher` is registered at `MediatRExtensions.cs:37` via `cfg.NotificationPublisherType = typeof(IsolatedNotificationPublisher)`. It iterates handlers independently, catching each exception and logging it without re-throwing, ensuring remaining handlers and the originating call are unaffected.

Integration test case 11 (`HandlerIsolation_ThrowingSubscriber_DoesNotFailApiRequest`) registers `ThrowingAnswerSubmittedHandler` alongside `CapturingAnswerSubmittedHandler`. The test asserts HTTP 200 is returned and the capturing handler received the event. This end-to-end proof passes.

Both the publisher-level (IsolatedNotificationPublisher) and handler-level (try/catch around `_publisher.Publish`) isolation layers are present and correctly layered (belt and suspenders).

---

### FA-6 — `AnswerComparator` input safety: PASS

`AnswerComparator.AreEqual(QuestionType, string?, string?)` at `Domain/Services/AnswerComparator.cs`:

- **Null/empty guard at line 25:** `if (string.IsNullOrWhiteSpace(studentPayload) || string.IsNullOrWhiteSpace(correctAnswer)) return false;` — any null, empty, or whitespace-only input returns `false` without throwing.
- **No regex, no `Eval`, no deserialization, no reflection** — the method body is a pure `switch` over `string.Equals` and `bool.TryParse`. No injection vectors.
- **`bool.TryParse` on unrecognized input (`"yes"`)** returns `false` from `TryParse`, causing the `&&` chain to short-circuit and return `false` — no exception.
- **Very large strings:** both `string.Equals` (MCQ/FillInBlank/Matching) and `bool.TryParse` (TrueFalse) are O(n) on string length. This inherits the F-01 finding from P2-08 (the `AnswerPayload` maximum-length validator is still absent). No new injection vector is introduced; the existing P2-08 F-01 remediation recommendation covers this.
- **Unknown `QuestionType`:** the `_ => false` default arm returns `false` without throwing.

Unit test coverage (`AnswerComparatorTests.cs`): 12 cases covering MCQ (2), TrueFalse (3), FillInBlank (4), Matching (1), and null/empty guards (3 — null payload, empty payload, null correctAnswer). All branch combinations in the guard and switch are covered. No test gap found.

---

### FA-7 — Authorization unchanged: PASS

Neither `SubmitAnswerCommandHandler.cs` nor `CompleteAttemptCommandHandler.cs` includes a controller; P2-07 adds no new endpoints. The controller actions `POST /api/Learning/Quizzes/{attemptId}/Answers` and `POST /api/Learning/Quizzes/{attemptId}/Complete` (both in `QuizzesController`) carry `[Authorize(Roles = "Student")]` from P2-08 and were not modified in this PR. Confirmed by reading the handlers: no `[AllowAnonymous]` decorators introduced, no controller changes.

Ownership guards are unchanged:
- `SubmitAnswerCommandHandler.cs:72`: `if (attempt.StudentId != studentId.Value) return Unauthorized(...)`
- `CompleteAttemptCommandHandler.cs:78`: `if (attempt.StudentId != studentId.Value) return Unauthorized(...)`

`studentId` is resolved from `_currentUser.UserId` (JWT-derived) on line 60/66 of the respective handlers and never from the request body.

---

### FA-8 — New repo method `GetLessonSkillIdAsync`: PASS

`LearningRepository.GetLessonSkillIdAsync(int lessonId, CancellationToken)`:
- Projects a single `int?` column (`l.SkillId`) via EF Core LINQ — no raw SQL, no string interpolation, no `FromSqlRaw`, no `System.Linq.Dynamic`.
- Uses `AsNoTracking()` — read-only, no state mutation.
- Filtered by `l.Id == lessonId` where `lessonId` comes from `attempt.LessonId` (server-side entity field, not a raw user input). No IDOR vector: the caller already verified ownership of the attempt before calling this method.
- Returns `int?` only — no PII, no answer content, no cross-module data.

---

## 5. Verdict

**PASS**

No Critical or High findings. All eight focus areas verified as correctly implemented. One Low finding (F-01 — string-interpolated log lines instead of structured-logging placeholders) is documented but does not block the reviewer gate. Three Info findings (F-02 ghost-event accepted trade-off; F-03 correct defense-in-depth; F-04 dependency scan clean) are documented for completeness.

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 1 |
| Info | 3 |

### Top follow-up items (non-blocking)

1. **(F-01)** Migrate the four new `LogError`/`LogWarn` lines in both publish blocks to structured-logging placeholder syntax (`"... {AttemptId}", attempt.Id`) so log aggregators can index and query by entity ID. This is a code-quality / observability improvement, not a security blocker.
2. **(Inherited from P2-08 F-01)** `AnswerPayload` still has no maximum-length validator. `AnswerComparator.AreEqual` is now the consumer of this value — both `string.Equals` and `bool.TryParse` are O(n). Adding `MaximumLength(4096)` to `SubmitAnswerValidation` remains the recommended fix for the storage and compute risk.
