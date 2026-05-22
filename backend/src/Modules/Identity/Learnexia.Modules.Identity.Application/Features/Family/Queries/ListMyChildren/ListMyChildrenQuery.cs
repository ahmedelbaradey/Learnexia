using Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Family.Queries.ListMyChildren;

// Lists the children linked to the CURRENT parent (resolved server-side from the JWT in the handler).
// Queries are not auto-validated (CONVENTIONS §4); there is no input to validate and the controller's
// role gate plus the handler's UserId guard enforce access.
public record ListMyChildrenQuery : IQuery<BaseResponse<IEnumerable<LinkedChildResponse>>>;
