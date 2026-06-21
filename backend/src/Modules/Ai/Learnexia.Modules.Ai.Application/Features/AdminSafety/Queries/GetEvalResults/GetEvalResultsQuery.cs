using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetEvalResults;

/// <summary>
/// Returns the latest offline AI-safety eval run result (P6-02 / P7-11-BE-3).
///
/// <para>Queries are NOT auto-validated (ValidationBehavior runs for ICommand only).
/// No parameters required — the endpoint always returns the single latest committed artifact.</para>
/// </summary>
public record GetEvalResultsQuery : IQuery<BaseResponse<EvalResultsDto>>;
