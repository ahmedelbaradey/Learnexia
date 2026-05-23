using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Queries.Get;

public class GetConceptQueryHandler : BaseResponseHandler, IQueryHandler<GetConceptQuery, BaseResponse<SingleConceptResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public GetConceptQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<SingleConceptResponse>> Handle(GetConceptQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _service.ConceptService.GetByIdAsync<SingleConceptResponse>(request.Id, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetConceptQuery");
            return ServerError<SingleConceptResponse>(ex.Message);
        }
    }
}
