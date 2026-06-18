using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildSubjectMastery;

// E4 — GET /Parent/Children/{id}/SubjectMastery
public record GetChildSubjectMasteryQuery(int ChildId) : IQuery<BaseResponse<SubjectMasteryResponseDto>>;
