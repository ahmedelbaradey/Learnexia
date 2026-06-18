using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetFamilySummary;

// E2 — GET /Parent/Family/Summary
// Family-scoped: the parent resolves their own children from the JWT; no childId input.
public record GetFamilySummaryQuery : IQuery<BaseResponse<FamilySummaryDto>>;
