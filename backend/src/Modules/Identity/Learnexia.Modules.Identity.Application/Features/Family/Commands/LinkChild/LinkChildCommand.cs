using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;

// Link-an-existing-child (AC-2). The acting parent is resolved server-side from the JWT
// (ICurrentUserService) — there is intentionally NO ParentId on this command, so a caller
// cannot link a child on behalf of another parent (AC-7 / IDOR protection). The child is
// identified by email and must already be a provisioned Student.
public record LinkChildCommand : ICommand<BaseResponse<LinkedChildResponse>>
{
    public string ChildEmail { get; set; } = null!;
}
