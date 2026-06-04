using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.AddChild;

// Parent-driven child provisioning. The acting parent is resolved server-side from the JWT
// (ICurrentUserService) — there is intentionally NO ParentId field, so a parent cannot create a child on
// behalf of another family (IDOR). There is also NO Role field: the child's role is hard-coded to Student
// inside the IChildAccountService seam, so this path cannot mint a Parent/Admin.
public record AddChildCommand : ICommand<BaseResponse<AddedChildResponse>>
{
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public int Grade { get; set; }
    public string Language { get; set; } = null!; // "ar" | "en" — UI/UX preference (maps to PreferredLanguage)
    public string Country { get; set; } = null!;
    /// <summary>
    /// Medium-of-instruction language for curriculum delivery ("ar" or "en").
    /// Required; distinct from <see cref="Language"/> (the UI preference).
    /// Stored on the child's User record; emitted as the <c>learning_language</c> JWT claim.
    /// Immutable by the student — parent-only change path is P8-04.
    /// </summary>
    public string LearningLanguage { get; set; } = null!; // "ar" | "en"
}
