using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetPlanComparison;

/// <summary>
/// Returns Free vs Premium plan benefit/pricing comparison.
/// All values are sourced from <c>IGlobalSettingsProvider</c> — never hard-coded.
/// <para>Query — NOT auto-validated.</para>
/// </summary>
public record GetPlanComparisonQuery : IQuery<BaseResponse<PlanComparisonDto>>;
