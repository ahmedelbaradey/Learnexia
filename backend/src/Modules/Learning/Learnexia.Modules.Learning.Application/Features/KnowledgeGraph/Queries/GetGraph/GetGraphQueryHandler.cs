using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetGraph;

/// <summary>
/// Handles <see cref="GetGraphQuery"/>.
/// Returns all non-deleted nodes and edges for the requested subject.
/// Admin-only: includes nodes wrapping inactive skills (flagged via <see cref="KnowledgeNodeDto.IsSkillActive"/>).
/// </summary>
public class GetGraphQueryHandler
    : BaseResponseHandler, IQueryHandler<GetGraphQuery, BaseResponse<SkillGraphDto>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetGraphQueryHandler(
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

    public async Task<BaseResponse<SkillGraphDto>> Handle(
        GetGraphQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.SubjectId <= 0)
                return BadRequest<SkillGraphDto>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            // Load all non-deleted nodes in the subject.
            var nodes = await _repository.Learning.GetGraphNodesAsync(request.SubjectId, cancellationToken);

            // Load all non-deleted edges between subject nodes.
            var edges = await _repository.Learning.GetGraphEdgesAsync(request.SubjectId, cancellationToken);

            // Build a lookup of SkillId → IsActive so we can flag inactive-skill nodes.
            // We load skills by their node SkillId references (not all skills in the subject).
            var skillIds = nodes
                .Where(n => n.SkillId.HasValue)
                .Select(n => n.SkillId!.Value)
                .Distinct()
                .ToHashSet();

            // Fetch IsActive flags for all referenced skills.
            // Use IgnoreQueryFilters so we can see inactive (but not soft-deleted) skills.
            var skillActiveMap = new Dictionary<int, bool>();
            if (skillIds.Count > 0)
            {
                var skillActiveData = await _repository.Learning
                    .GetByCondition<Skill>(s => skillIds.Contains(s.Id), trackChanges: false)
                    .Select(s => new { s.Id, s.IsActive })
                    .ToListAsync(cancellationToken);

                foreach (var s in skillActiveData)
                    skillActiveMap[s.Id] = s.IsActive;
            }

            // Map nodes → DTOs (add IsSkillActive flag).
            var nodeDtos = nodes.Select(n => new KnowledgeNodeDto
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType,
                SubjectId = n.SubjectId,
                GradeId = n.GradeId,
                Difficulty = n.Difficulty,
                SkillId = n.SkillId,
                // Non-skill nodes are always "active"; skill nodes: look up the map (default true if missing).
                IsSkillActive = n.SkillId.HasValue
                    ? skillActiveMap.GetValueOrDefault(n.SkillId.Value, true)
                    : true
            }).ToList();

            var edgeDtos = _mapper.Map<List<KnowledgeEdgeDto>>(edges);

            var graph = new SkillGraphDto
            {
                SubjectId = request.SubjectId,
                Nodes = nodeDtos,
                Edges = edgeDtos
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
