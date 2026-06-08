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

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetPrerequisites;

/// <summary>
/// Handles <see cref="GetPrerequisitesQuery"/>.
/// Returns nodes that must be mastered before the requested node (its prerequisites).
/// NodeId not found → 404. Found but no prerequisites → 200 empty collection.
///
/// Security (P7-03): This endpoint is student-reachable ([Authorize]).
/// - Returns <see cref="StudentKnowledgeNodeDto"/> which does NOT carry <c>IsSkillActive</c>,
///   so inactive-skill existence is not disclosed to students.
/// - Nodes wrapping an inactive skill are filtered out of the result, consistent with the
///   <c>IsActive</c> filter applied on the student SkillTree endpoint.
/// </summary>
public class GetPrerequisitesQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPrerequisitesQuery, BaseResponse<List<StudentKnowledgeNodeDto>>>
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

    public async Task<BaseResponse<List<StudentKnowledgeNodeDto>>> Handle(
        GetPrerequisitesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var nodeExists = await _repository.Learning.KnowledgeNodeExistsAsync(request.NodeId, cancellationToken);
            if (!nodeExists)
                return NotFound<List<StudentKnowledgeNodeDto>>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            var nodes = await _repository.Learning.GetPrerequisiteNodesAsync(request.NodeId, cancellationToken);

            if (!nodes.Any())
                return EmptyCollection(new List<StudentKnowledgeNodeDto>());

            // Security: filter out nodes that wrap an inactive skill so students cannot
            // discover hidden skills via prerequisite graph traversal.
            var skillIds = nodes
                .Where(n => n.SkillId.HasValue)
                .Select(n => n.SkillId!.Value)
                .Distinct()
                .ToHashSet();

            var activeSkillIds = new HashSet<int>();
            if (skillIds.Count > 0)
            {
                activeSkillIds = (await _repository.Learning
                    .GetByCondition<Skill>(s => skillIds.Contains(s.Id) && s.IsActive, trackChanges: false)
                    .Select(s => s.Id)
                    .ToListAsync(cancellationToken))
                    .ToHashSet();
            }

            var visibleNodes = nodes
                .Where(n => !n.SkillId.HasValue || activeSkillIds.Contains(n.SkillId.Value))
                .ToList();

            var dtos = _mapper.Map<List<StudentKnowledgeNodeDto>>(visibleNodes);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetPrerequisitesQuery");
            return ServerError<List<StudentKnowledgeNodeDto>>();
        }
    }
}
