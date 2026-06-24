using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetRelatedConcepts;

/// <summary>
/// Handles <see cref="GetRelatedConceptsQuery"/> (BL-03-BE-4).
/// Returns nodes connected via Related edges (either direction) for the given node.
/// NodeId not found → 404. Found but no related nodes → 200 empty collection.
///
/// <para>Security (mirrors GetPrerequisitesQueryHandler):
/// - Returns <see cref="StudentKnowledgeNodeDto"/> which does NOT carry <c>IsSkillActive</c>.
/// - Nodes wrapping an inactive skill are filtered out so students cannot discover hidden skills.</para>
/// </summary>
public class GetRelatedConceptsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetRelatedConceptsQuery, BaseResponse<List<StudentKnowledgeNodeDto>>>
{
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetRelatedConceptsQueryHandler(
        ILearningServiceManager service,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service   = service;
        _mapper    = mapper;
        _logger    = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<StudentKnowledgeNodeDto>>> Handle(
        GetRelatedConceptsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var nodeExists = await _service.KnowledgeGraphService.NodeExistsAsync(request.NodeId, cancellationToken);
            if (!nodeExists)
                return NotFound<List<StudentKnowledgeNodeDto>>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            var nodes = await _service.KnowledgeGraphService.GetRelatedNodesAsync(request.NodeId, cancellationToken);

            if (!nodes.Any())
                return EmptyCollection(new List<StudentKnowledgeNodeDto>());

            // Security: filter inactive-skill nodes — mirrors GetPrerequisitesQueryHandler.
            var skillIds = nodes
                .Where(n => n.SkillId.HasValue)
                .Select(n => n.SkillId!.Value)
                .Distinct()
                .ToList();

            var activeSkillIds = await _service.KnowledgeGraphService.GetActiveSkillIdsAsync(skillIds, cancellationToken);

            var visibleNodes = nodes
                .Where(n => !n.SkillId.HasValue || activeSkillIds.Contains(n.SkillId.Value))
                .ToList();

            var dtos = _mapper.Map<List<StudentKnowledgeNodeDto>>(visibleNodes);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetRelatedConceptsQuery");
            return ServerError<List<StudentKnowledgeNodeDto>>();
        }
    }
}
