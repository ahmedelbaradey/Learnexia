using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.ChangeChildLearningLanguage;

/// <summary>
/// Parent requests a change to a child's <c>LearningLanguage</c> (medium of instruction).
/// This is a destructive operation: after the Identity commit the Learning module resets the
/// child's Math/Science <c>Attempt</c> rows to start fresh in the new language.
///
/// Security:
///   - <c>ChildId</c> is in the body; the acting parent id is ALWAYS resolved from the JWT
///     (ICurrentUserService) — never from the request body (IDOR rule mirrors UpdateChildCommand).
///   - Family-scope link check is performed in the handler before any mutation.
///   - <c>ConfirmFreshStart</c> must be <c>true</c> for the change to proceed; <c>false</c>
///     returns <c>BusinessValidation</c> (HTTP 424) and no state is changed.
/// </summary>
public record ChangeLearningLanguageCommand : ICommand<BaseResponse<ChangedLearningLanguageResponse>>
{
    public int ChildId { get; set; }

    /// <summary>Target learning language. Accepted values: "ar" | "en".</summary>
    public string NewLearningLanguage { get; set; } = null!;

    /// <summary>
    /// Explicit consent flag. The client MUST send <c>true</c> to confirm the parent has
    /// acknowledged the destructive Math/Science progress reset. Omitting this field or sending
    /// <c>false</c> triggers a <c>BusinessValidation</c> (424 FailedDependency) response before
    /// any link check or mutation is attempted (locked decision §4).
    /// </summary>
    public bool ConfirmFreshStart { get; set; }
}
