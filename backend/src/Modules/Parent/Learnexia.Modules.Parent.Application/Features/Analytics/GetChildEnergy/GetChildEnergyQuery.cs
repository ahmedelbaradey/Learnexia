using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildEnergy;

// E7 — GET /Parent/Children/{id}/Energy
public record GetChildEnergyQuery(int ChildId) : IQuery<BaseResponse<ChildEnergyDto>>;
