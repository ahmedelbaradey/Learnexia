using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Queries.List;

/// <summary>
/// Returns all non-deleted content blocks for a lesson ordered by SequenceOrder.
/// Admin-gated route — returns all blocks including inactive ones.
/// </summary>
public class ListContentBlocksQueryHandler
    : BaseResponseHandler, IQueryHandler<ListContentBlocksQuery, BaseResponse<List<AdminContentBlockDto>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListContentBlocksQueryHandler(
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

    public async Task<BaseResponse<List<AdminContentBlockDto>>> Handle(
        ListContentBlocksQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify the lesson exists (global IsDeleted filter active).
            var lessonExists = await _repository.Learning
                .AnyAsync<Lesson>(l => l.Id == request.LessonId);

            if (!lessonExists)
                return NotFound<List<AdminContentBlockDto>>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Return all non-deleted blocks (global filter excludes IsDeleted=true).
            // IsActive is NOT filtered here — admin sees all, including inactive.
            var blocks = await _repository.Learning
                .GetByCondition<ContentBlock>(cb => cb.LessonId == request.LessonId, false)
                .OrderBy(cb => cb.SequenceOrder)
                .ToListAsync(cancellationToken);

            var dtos = _mapper.Map<List<AdminContentBlockDto>>(blocks);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListContentBlocksQuery");
            return ServerError<List<AdminContentBlockDto>>();
        }
    }
}
