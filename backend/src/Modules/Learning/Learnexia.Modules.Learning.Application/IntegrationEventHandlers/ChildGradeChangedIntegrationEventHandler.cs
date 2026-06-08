using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Learning.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="ChildGradeChangedIntegrationEvent"/> in the Learning
/// module (P7-08 BE-4). Produced by <c>OverrideChildGradeCommandHandler</c> in Identity.
///
/// Q-C2 FINDING (verified at implementation time):
///   Learning's curriculum reads do NOT key off <c>User.Grade</c> at query time from a
///   persisted materialization. Grade is passed as a query parameter by the student
///   (e.g. <c>GetSubjectsForGradeQuery.Grade</c>). There is NO persisted per-student
///   grade-scoped state in the Learning module that needs refreshing on grade change —
///   <c>Attempt</c> rows, mastery data, and skill progress are all student-scoped
///   (by student id), NOT grade-scoped. Subjects/Units/Lessons are curriculum-scoped
///   (by grade in the content tree), and the student's reads automatically pick up
///   the new grade's content on the next query.
///
///   CONCLUSION: This consumer is a no-op / informational stub.
///   The grade change takes effect automatically on the next read/token.
///   No Learning state mutation is required here.
///   P5-06 may enrich this consumer when grade-transition enrichment is built.
///
/// NON-DESTRUCTIVE GUARANTEE: this handler does NOT delete any XP, badges, streaks,
/// mastery rows, or Attempt rows. All learning history is preserved (locked decision).
///
/// Fail-soft: mirrors <see cref="AccountDeletedIntegrationEventHandler"/> pattern.
/// </summary>
public sealed class ChildGradeChangedIntegrationEventHandler
    : INotificationHandler<ChildGradeChangedIntegrationEvent>
{
    private readonly ILoggerManager _logger;

    public ChildGradeChangedIntegrationEventHandler(ILoggerManager logger)
    {
        _logger = logger;
    }

    public Task Handle(
        ChildGradeChangedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // No-op stub (Q-C2 finding): Learning reads key off grade at query time via the
            // student's request parameter — no persisted grade-scoped state needs refreshing.
            // All learning history (Attempt rows, mastery, XP, badges, streaks) is preserved.
            // This log entry serves as an audit trail and a hook for P5-06 enrichment.
            _logger.LogInfo(
                $"P7-08: ChildGradeChangedIntegrationEvent received in Learning module " +
                $"(eventId={notification.EventId}, childUserId={notification.ChildUserId}, " +
                $"{notification.OldGrade} → {notification.NewGrade}, " +
                $"changedByAdminUserId={notification.ChangedByAdminUserId}). " +
                $"No-op: Learning reads scope by grade at query time; no persisted state needs updating. " +
                $"Learning history preserved (non-destructive grade override). " +
                $"P5-06 may enrich this consumer when grade-transition logic is built.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-08: Error handling ChildGradeChangedIntegrationEvent in Learning module " +
                $"(eventId={notification.EventId}, childUserId={notification.ChildUserId}) — ignored.");
        }

        return Task.CompletedTask;
    }
}
