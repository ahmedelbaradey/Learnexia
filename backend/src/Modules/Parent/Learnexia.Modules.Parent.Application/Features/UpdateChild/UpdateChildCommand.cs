using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.UpdateChild;

// Parent edits an existing child's profile. Family scope is enforced server-side: the acting parent is
// resolved from the JWT (ICurrentUserService) and a ParentStudent link to ChildId must exist before any
// write — there is intentionally NO ParentId field (IDOR). Login/email and role are OUT of scope here.
public record UpdateChildCommand : ICommand<BaseResponse<UpdatedChildResponse>>
{
    public int ChildId { get; set; }
    public string FullName { get; set; } = null!;
    public int Grade { get; set; }
    public string Language { get; set; } = null!; // "ar" | "en"
    public string Country { get; set; } = null!;
}
