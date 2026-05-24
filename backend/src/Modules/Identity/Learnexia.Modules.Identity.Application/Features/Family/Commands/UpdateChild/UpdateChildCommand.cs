using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.UpdateChild;

// Parent edits an existing child's profile (BE-8). Family scope is enforced server-side: the acting
// parent is resolved from the JWT (ICurrentUserService) and a ParentStudent link to ChildId must
// exist before any write — there is intentionally NO ParentId field, so a parent cannot edit a child
// in another family (IDOR). Login/email and role are OUT of scope here (no mass-assignment): only
// fullName, grade, language and country are mutable.
public record UpdateChildCommand : ICommand<BaseResponse<UpdatedChildResponse>>
{
    public int ChildId { get; set; }
    public string FullName { get; set; } = null!;
    public int Grade { get; set; }
    public string Language { get; set; } = null!; // "ar" | "en"
    public string Country { get; set; } = null!;
}
