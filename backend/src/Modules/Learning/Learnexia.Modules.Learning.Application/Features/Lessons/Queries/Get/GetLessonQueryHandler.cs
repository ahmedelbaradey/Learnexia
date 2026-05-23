using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;

public class GetLessonQueryHandler : BaseResponseHandler, IQueryHandler<GetLessonQuery, BaseResponse<SingleLessonResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public GetLessonQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<SingleLessonResponse>> Handle(GetLessonQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _service.LessonService.GetByIdAsync<SingleLessonResponse>(request.Id, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetLessonQuery");
            return ServerError<SingleLessonResponse>(ex.Message);
        }
    }
}
