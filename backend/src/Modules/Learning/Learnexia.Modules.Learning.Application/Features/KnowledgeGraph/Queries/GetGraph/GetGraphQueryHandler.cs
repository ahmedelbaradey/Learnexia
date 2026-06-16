using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetGraph;

/// <summary>
/// Handles <see cref="GetGraphQuery"/>.
/// Returns all non-deleted nodes and edges for the requested subject.
/// Admin-only: includes nodes wrapping inactive skills (flagged via <see cref="KnowledgeNodeDto.IsSkillActive"/>).
///
/// Option-C refactor: GetGraphNodesAsync + GetGraphEdgesAsync + Skill.Select().ToListAsync()
/// moved into IKnowledgeGraphService (GetGraphNodesAsync + GetGraphEdgesAsync + GetSkillActiveMapAsync).
/// </summary>
public class GetGraphQueryHandler
    : BaseResponseHandler, IQueryHandler<GetGraphQuery, BaseResponse<SkillGraphDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetGraphQueryHandler(
        ILearningServiceManager service,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SkillGraphDto>> Handle(
        GetGraphQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.SubjectId <= 0)
                return BadRequest<SkillGraphDto>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            // Load all non-deleted nodes in the subject.
            var nodes = await _service.KnowledgeGraphService.GetGraphNodesAsync(request.SubjectId, cancellationToken);

            // Load all non-deleted edges between subject nodes.
            var edges = await _service.KnowledgeGraphService.GetGraphEdgesAsync(request.SubjectId, cancellationToken);

            // Collect referenced SkillIds to fetch IsActive flags.
            var skillIds = nodes
                .Where(n => n.SkillId.HasValue)
                .Select(n => n.SkillId!.Value)
                .Distinct()
                .ToList();

            // Fetch IsActive flags for all referenced skills (ToListAsync stays in Infrastructure).
            var skillActiveMap = await _service.KnowledgeGraphService.GetSkillActiveMapAsync(skillIds, cancellationToken);

            // Map nodes → DTOs (add IsSkillActive flag).
            var nodeDtos = nodes.Select(n => new KnowledgeNodeDto
            {
                Id         = n.Id,
                Name       = n.Name,
                NodeType   = n.NodeType,
                SubjectId  = n.SubjectId,
                GradeId    = n.GradeId,
                Difficulty = n.Difficulty,
                SkillId    = n.SkillId,
                // Non-skill nodes are always "active"; skill nodes: look up the map (default true if missing).
                IsSkillActive = n.SkillId.HasValue
                    ? skillActiveMap.GetValueOrDefault(n.SkillId.Value, true)
                    : true
            }).ToList();

            var edgeDtos = _mapper.Map<List<KnowledgeEdgeDto>>(edges);

            var graph = new SkillGraphDto
            {
                SubjectId = request.SubjectId,
                Nodes     = nodeDtos,
                Edges     = edgeDtos
            };

            return Success(graph);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetGraphQuery");
            return ServerError<SkillGraphDto>();
        }
    }
}
