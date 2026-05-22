using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.AddChild;

// Parent-driven child provisioning (AC-1/AC-3/AC-4). The acting parent is resolved server-side
// from the JWT (ICurrentUserService) — there is intentionally NO ParentId field, so a parent
// cannot create a child on behalf of another family (IDOR). There is also NO Role field: the
// child's role is hard-coded to Student in the handler, so this path cannot mint a Parent/Admin.
// Single child per call (the frontend loops for siblings); duplicate email is rejected with a
// specific message and creates no account.
public record AddChildCommand : ICommand<BaseResponse<AddedChildResponse>>
{
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public int Grade { get; set; }
    public string Language { get; set; } = null!; // "ar" | "en"
    public string Country { get; set; } = null!;
}
