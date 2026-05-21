namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Raised when a student completes a lesson. Consumed cross-module by gamification/analytics handlers
/// (XP, badge, streak, analytics) via MediatR fan-out (FR-GM-7, ADR 0002).
///
/// Payload carries opaque int IDs only — NO PII (no name, email, DOB, or child-data fields).
/// Mirrors the shape of <see cref="Identity.UserRegisteredIntegrationEvent"/>.
/// </summary>
public sealed record LessonCompletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int LessonId,
    int SkillId,
    int AccuracyPercentage,
    int CorrectAnswerCount) : IIntegrationEvent;
