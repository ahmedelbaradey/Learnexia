using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetUnlockedBy;

/// <summary>
/// Handles <see cref="GetUnlockedByQuery"/>.
/// Returns nodes that become accessible once the requested node is mastered.
/// NodeId not found → 404. Found but no unlocked nodes → 200 empty collection.
/// </summary>
public class GetUnlockedByQueryHandler
    : BaseResponseHandler, IQueryHandler<GetUnlockedByQuery, BaseResponse<List<KnowledgeNodeDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetUnlockedByQueryHandler(
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
        GetUnlockedByQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var nodeExists = await _repository.Learning.KnowledgeNodeExistsAsync(request.NodeId, cancellationToken);
            if (!nodeExists)
                return NotFound<List<KnowledgeNodeDto>>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            var nodes = await _repository.Learning.GetUnlockedByNodeAsync(request.NodeId, cancellationToken);

            if (!nodes.Any())
                return EmptyCollection(new List<KnowledgeNodeDto>());

            var dtos = _mapper.Map<List<KnowledgeNodeDto>>(nodes);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetUnlockedByQuery");
            return ServerError<List<KnowledgeNodeDto>>(ex.Message);
        }
    }
}
