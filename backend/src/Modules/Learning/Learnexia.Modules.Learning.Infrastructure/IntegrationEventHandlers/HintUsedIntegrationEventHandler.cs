using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="HintUsedIntegrationEvent"/> in the Learning module (P3-05 BE-4).
/// Produced by the Ai module's <c>GetHintCommandHandler</c> fire-and-forget after hint delivery.
///
/// <para>On receipt, this handler increments <c>StudentAnswer.HintUsed = true</c> for the answer
/// to the given question in the given attempt, and increments <c>Attempt.HintsUsedCount++</c>
/// on the parent attempt. Mirrors the <c>AnswerSubmittedIntegrationEvent</c> ADR 0002 pattern.</para>
///
/// <para>Module isolation: the Ai module publishes <see cref="HintUsedIntegrationEvent"/> via MediatR
/// fire-and-forget — no cross-module FK or project reference exists.</para>
///
/// <para>UoW note: this handler is an <c>INotificationHandler</c> (not an <c>ICommand</c>) so the
/// <c>UnitOfWorkBehavior</c> does NOT wrap it. This handler must commit its own changes via the
/// <c>LearningDbContext</c> directly. Placed in Infrastructure so it can inject the DbContext.</para>
///
/// <para>Fail-soft: a failed hint-usage recording must NOT crash the student's UI session.
/// Exceptions are caught and logged; the event is NOT re-published (best-effort).</para>
/// </summary>
public sealed class HintUsedIntegrationEventHandler
    : INotificationHandler<HintUsedIntegrationEvent>
{
    private readonly LearningDbContext _db;
    private readonly ILoggerManager _logger;

    public HintUsedIntegrationEventHandler(
        LearningDbContext db,
        ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(
        HintUsedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Load the Attempt with tracking so we can mutate HintsUsedCount.
            var attempt = await _db.Set<Attempt>()
                .FirstOrDefaultAsync(a => a.Id == notification.AttemptId, cancellationToken);

            if (attempt is null)
            {
                _logger.LogWarn(
                    $"P3-05: HintUsedIntegrationEvent: Attempt {notification.AttemptId} not found — skipping hint count increment.");
                return;
            }

            // Ownership guard: only update if the event's StudentId matches the attempt.
            if (attempt.StudentId != notification.StudentId)
            {
                _logger.LogWarn(
                    $"P3-05: HintUsedIntegrationEvent: StudentId mismatch (event={notification.StudentId}, attempt={attempt.StudentId}) — skipping.");
                return;
            }

            // Step 2 — Increment Attempt.HintsUsedCount.
            attempt.HintsUsedCount++;

            // Step 3 — Mark StudentAnswer.HintUsed = true for the given question in this attempt.
            // The re-answer guard (SubmitAnswerCommandHandler) means there should be at most one
            // StudentAnswer per (AttemptId, QuestionId), but we handle multiple gracefully.
            var studentAnswers = await _db.Set<StudentAnswer>()
                .Where(sa => sa.AttemptId == notification.AttemptId
                          && sa.QuestionId == notification.QuestionId)
                .ToListAsync(cancellationToken);

            foreach (var answer in studentAnswers.Where(a => !a.HintUsed))
                answer.HintUsed = true;

            // Step 4 — Commit. This handler is outside UoW; must commit directly.
            // Pass StudentId as the audit userId (the student is the actor for hint usage).
            await _db.SaveChangesAsync(notification.StudentId);

            _logger.LogInfo(
                $"P3-05: HintUsedIntegrationEvent handled: AttemptId={notification.AttemptId}, " +
                $"QuestionId={notification.QuestionId}, HintLevel={notification.HintLevel}, " +
                $"StudentId={notification.StudentId}. HintsUsedCount now={attempt.HintsUsedCount}.");
        }
        catch (Exception ex)
        {
            // Fail-soft: hint-usage recording failure must not disrupt the student session.
            _logger.LogError(ex,
                $"P3-05: HintUsedIntegrationEventHandler: error recording hint usage " +
                $"(AttemptId={notification.AttemptId}, QuestionId={notification.QuestionId}) — ignored.");
        }
    }
}
