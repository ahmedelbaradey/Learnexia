namespace Learnexia.Shared.Contracts.Identity;

/// <summary>
/// Published by <c>IdentityChildAccountService.ChangeLearningLanguageAsync</c> (after the User
/// row is committed) when a parent changes a child's <c>LearningLanguage</c>.
///
/// Mirrors <see cref="UserRegisteredIntegrationEvent"/> — carries only what consumers need:
/// the student id plus old and new language codes. No PII beyond the student id.
///
/// Consumers:
///   Learning module — deletes the child's Math/Science <c>Attempt</c> rows so derived
///   mastery resets; Arabic/English progress and all gamification are untouched.
/// </summary>
public sealed record LearningLanguageChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    string OldLanguage,
    string NewLanguage) : IIntegrationEvent;
