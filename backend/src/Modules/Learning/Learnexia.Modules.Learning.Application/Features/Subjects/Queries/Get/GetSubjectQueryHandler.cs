using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.Get;

public class GetSubjectQueryHandler : BaseResponseHandler, IQueryHandler<GetSubjectQuery, BaseResponse<SingleSubjectResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public GetSubjectQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<SingleSubjectResponse>> Handle(GetSubjectQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _service.SubjectService.GetByIdAsync<SingleSubjectResponse>(request.Id, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectQuery");
            return ServerError<SingleSubjectResponse>(ex.Message);
        }
    }
}
