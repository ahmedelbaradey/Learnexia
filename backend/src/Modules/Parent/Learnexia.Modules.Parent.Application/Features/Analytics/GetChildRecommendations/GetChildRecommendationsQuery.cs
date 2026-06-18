using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildRecommendations;

// E10 — GET /Parent/Children/{id}/Recommendations
// Consumes the IStudentRecommendationsQuery seam (P5-09-BE-5).
// No-data (cold-start) → well-formed empty response, never an error.
public record GetChildRecommendationsQuery(int ChildId)
    : IQuery<BaseResponse<RecommendationsDto>>;
