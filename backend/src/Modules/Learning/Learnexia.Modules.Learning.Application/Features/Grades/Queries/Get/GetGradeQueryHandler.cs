using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Queries.Get;

public class GetGradeQueryHandler : BaseResponseHandler, IQueryHandler<GetGradeQuery, BaseResponse<SingleGradeResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public GetGradeQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<SingleGradeResponse>> Handle(GetGradeQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _service.GradeService.GetByIdAsync<SingleGradeResponse>(request.Id, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetGradeQuery");
            return ServerError<SingleGradeResponse>(ex.Message);
        }
    }
}
