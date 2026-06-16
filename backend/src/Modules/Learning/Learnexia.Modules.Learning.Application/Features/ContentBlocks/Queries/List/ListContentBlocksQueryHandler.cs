using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Queries.List;

/// <summary>
/// Returns all non-deleted content blocks for a lesson ordered by SequenceOrder.
/// Admin-gated route — returns all blocks including inactive ones.
///
/// Option-C: all EF calls moved into IContentBlockService (Infrastructure). Handler is now thin.
/// </summary>
public class ListContentBlocksQueryHandler
    : BaseResponseHandler, IQueryHandler<ListContentBlocksQuery, BaseResponse<List<AdminContentBlockDto>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListContentBlocksQueryHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
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
            var lessonExists = await _service.ContentBlockService.LessonExistsAsync(request.LessonId, cancellationToken);

            if (!lessonExists)
                return NotFound<List<AdminContentBlockDto>>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Return all non-deleted blocks (global filter excludes IsDeleted=true).
            // IsActive is NOT filtered here — admin sees all, including inactive.
            // Ordering by SequenceOrder happens inside Infrastructure.
            var dtos = await _service.ContentBlockService.GetContentBlocksOrderedAsync(request.LessonId, cancellationToken);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListContentBlocksQuery");
            return ServerError<List<AdminContentBlockDto>>();
        }
    }
}
