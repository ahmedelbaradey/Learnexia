using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildProgress;

// E1 — GET /Parent/Children/{id}/Progress
// Query: NOT auto-validated (queries are excluded from ValidationBehavior per CLAUDE.md rule 4).
// childId must be a positive integer — validated inline in the handler.
public record GetChildProgressQuery(int ChildId) : IQuery<BaseResponse<ChildProgressDto>>;
