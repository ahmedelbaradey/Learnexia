using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Queries.Get;

public class GetSkillQueryHandler : BaseResponseHandler, IQueryHandler<GetSkillQuery, BaseResponse<SingleSkillResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public GetSkillQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<SingleSkillResponse>> Handle(GetSkillQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _service.SkillService.GetByIdAsync<SingleSkillResponse>(request.Id, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSkillQuery");
            return ServerError<SingleSkillResponse>(ex.Message);
        }
    }
}
