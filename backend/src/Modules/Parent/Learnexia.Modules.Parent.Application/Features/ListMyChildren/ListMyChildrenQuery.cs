using Learnexia.Modules.Parent.Application.Features.LinkChild;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.ListMyChildren;

// Lists the children linked to the CURRENT parent (resolved server-side from the JWT in the handler).
// Queries are not auto-validated; there is no input to validate and the controller's role gate plus the
// handler's UserId guard enforce access.
public record ListMyChildrenQuery : IQuery<BaseResponse<IEnumerable<LinkedChildResponse>>>;
