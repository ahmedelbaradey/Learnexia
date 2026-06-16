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
///
/// Security (P7-03): This endpoint is student-reachable ([Authorize]).
/// - Returns <see cref="StudentKnowledgeNodeDto"/> which does NOT carry <c>IsSkillActive</c>,
///   so inactive-skill existence is not disclosed to students.
/// - Nodes wrapping an inactive skill are filtered out of the result, consistent with the
///   <c>IsActive</c> filter applied on the student SkillTree endpoint.
///
/// Option-C refactor: KnowledgeNodeExistsAsync + GetPrerequisiteNodesAsync + Skill.Select().ToListAsync()
/// moved into IKnowledgeGraphService (NodeExistsAsync + GetPrerequisiteNodesAsync + GetActiveSkillIdsAsync).
/// </summary>
public class GetPrerequisitesQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPrerequisitesQuery, BaseResponse<List<StudentKnowledgeNodeDto>>>
{
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetPrerequisitesQueryHandler(
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

    public async Task<BaseResponse<List<StudentKnowledgeNodeDto>>> Handle(
        GetPrerequisitesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var nodeExists = await _service.KnowledgeGraphService.NodeExistsAsync(request.NodeId, cancellationToken);
            if (!nodeExists)
                return NotFound<List<StudentKnowledgeNodeDto>>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            var nodes = await _service.KnowledgeGraphService.GetPrerequisiteNodesAsync(request.NodeId, cancellationToken);

            if (!nodes.Any())
                return EmptyCollection(new List<StudentKnowledgeNodeDto>());

            // Security: filter out nodes that wrap an inactive skill so students cannot
            // discover hidden skills via prerequisite graph traversal.
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
            _logger.LogError(ex, "Error: in GetPrerequisitesQuery");
            return ServerError<List<StudentKnowledgeNodeDto>>();
        }
    }
}
