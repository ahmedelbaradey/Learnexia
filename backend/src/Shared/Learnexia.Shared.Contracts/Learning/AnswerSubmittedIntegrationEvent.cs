namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Raised when a student submits an answer. Consumed cross-module by gamification/analytics handlers
/// via MediatR fan-out (FR-GM-7, ADR 0002).
///
/// Contract-only this cycle (P4-01): defined now, wired later. Shape mirrors
/// <see cref="LessonCompletedIntegrationEvent"/> minus <c>AccuracyPercentage</c>.
///
/// Payload carries opaque int IDs only — NO PII (no name, email, DOB, or child-data fields).
/// </summary>
public sealed record AnswerSubmittedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int LessonId,
    int SkillId,
    int CorrectAnswerCount) : IIntegrationEvent;
