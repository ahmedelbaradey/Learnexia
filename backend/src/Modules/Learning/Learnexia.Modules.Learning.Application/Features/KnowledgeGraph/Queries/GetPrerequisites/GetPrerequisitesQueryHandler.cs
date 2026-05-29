using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetPrerequisites;

/// <summary>
/// Handles <see cref="GetPrerequisitesQuery"/>.
/// Returns nodes that must be mastered before the requested node (its prerequisites).
/// NodeId not found → 404. Found but no prerequisites → 200 empty collection.
/// </summary>
public class GetPrerequisitesQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPrerequisitesQuery, BaseResponse<List<KnowledgeNodeDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetPrerequisitesQueryHandler(
        ILearningRepositoryManager repository,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<KnowledgeNodeDto>>> Handle(
        GetPrerequisitesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var nodeExists = await _repository.Learning.KnowledgeNodeExistsAsync(request.NodeId, cancellationToken);
            if (!nodeExists)
                return NotFound<List<KnowledgeNodeDto>>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            var nodes = await _repository.Learning.GetPrerequisiteNodesAsync(request.NodeId, cancellationToken);

            if (!nodes.Any())
                return EmptyCollection(new List<KnowledgeNodeDto>());

            var dtos = _mapper.Map<List<KnowledgeNodeDto>>(nodes);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetPrerequisitesQuery");
            return ServerError<List<KnowledgeNodeDto>>(ex.Message);
        }
    }
}
