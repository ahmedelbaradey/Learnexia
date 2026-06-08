using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateChildProfile;

/// <summary>
/// Admin command to update the harmless profile fields of a child account (P7-08 BE-1).
///
/// Updates only <c>PreferredLanguage</c> (UI/UX language, full culture code) and
/// <c>Nationality</c> (country). Deliberately does NOT touch:
///   - <c>Grade</c>  — use <see cref="OverrideChildGrade.OverrideChildGradeCommand"/> (emits event)
///   - <c>LearningLanguage</c> — use <see cref="AdminChangeLearningLanguage.AdminChangeLearningLanguageCommand"/> (confirm-gated)
///   - <c>FullName</c>, email, password, roles — outside P7-08 scope
///
/// Security:
///   - Authorized via class-level <c>AdminOnly</c> policy on <c>AdminUsersController</c>.
///   - No parent-link IDOR check — admin acts on any child.
///   - Validates that the target user holds the Student role (not a parent/admin).
///   - No event emitted (harmless field update per brief AC-1).
///   - Audit: <c>AdminActionPerformedEvent(ChildProfileUpdated)</c> published post-mutation.
/// </summary>
public record UpdateChildProfileCommand : ICommand<BaseResponse<string>>
{
    public int ChildId { get; init; }

    /// <summary>
    /// UI/UX preferred language for the child. Accepted values: "ar" | "en" | "ar-EG" | "en-US".
    /// Normalized to full culture code ("ar-EG" / "en-US") in the handler.
    /// Null = no change to this field.
    /// </summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>Country / nationality string (free-text, max 100 chars). Null = no change.</summary>
    public string? Country { get; init; }
}
