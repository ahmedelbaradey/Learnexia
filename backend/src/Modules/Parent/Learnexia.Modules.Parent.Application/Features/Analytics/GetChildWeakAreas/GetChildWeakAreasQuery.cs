using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeakAreas;

// E5 — GET /Parent/Children/{id}/WeakAreas
// Consumes the all-subjects weak-area seam (IStudentAllSubjectsWeakAreasQuery).
public record GetChildWeakAreasQuery(int ChildId) : IQuery<BaseResponse<WeakAreasResponseDto>>;
