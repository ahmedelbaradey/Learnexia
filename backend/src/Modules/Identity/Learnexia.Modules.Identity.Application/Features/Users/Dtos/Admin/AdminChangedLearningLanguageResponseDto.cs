namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;

/// <summary>
/// Response returned by <c>AdminChangeLearningLanguageCommand</c> on success.
/// Contains only opaque child id and language codes — no PII (P7-08 security rule).
/// </summary>
public sealed record AdminChangedLearningLanguageResponseDto(
    int ChildId,
    string NewLearningLanguage);
