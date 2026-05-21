using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.CheckRoleAvailability;

public record CheckRoleAvailabilityQuery : IQuery<BaseResponse<SingleHolderRoleAvailabilityResponse>>
{
}
