using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.UnlinkChild;

// Removes the (parent, child) link. The acting parent is resolved server-side from the JWT — there is
// intentionally NO ParentId field (IDOR). The last-parent guard blocks an unlink that would leave the
// child with zero parents; a non-linked / other-parent child returns a generic 404 (anti-enumeration).
public record UnlinkChildCommand : ICommand<BaseResponse<bool>>
{
    public int ChildId { get; set; }
}
