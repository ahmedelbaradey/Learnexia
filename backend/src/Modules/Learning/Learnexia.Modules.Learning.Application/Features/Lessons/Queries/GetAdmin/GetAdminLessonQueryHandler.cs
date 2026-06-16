using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.GetAdmin;

/// <summary>
/// Returns the full admin lesson detail including all non-deleted content blocks (incl. inactive).
/// This query is admin-only — the controller gate enforces <c>AdminOnly</c> policy.
/// No language guard is applied here (admin accesses any lesson directly by ID).
///
/// Option-C: all EF calls moved into ILessonService.GetAdminLessonWithContentBlocksAsync (Infrastructure).
/// Handler is now thin.
/// </summary>
public class GetAdminLessonQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdminLessonQuery, BaseResponse<AdminLessonDetailDto>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetAdminLessonQueryHandler(
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

    public async Task<BaseResponse<AdminLessonDetailDto>> Handle(
        GetAdminLessonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the lesson with its content blocks.
            // Global IsDeleted filter applies — deleted lessons return NotFound.
            // IsActive filter is intentionally NOT applied — admin sees inactive lessons.
            // Include(ContentBlocks) and ordering by SequenceOrder happen inside Infrastructure.
            var lesson = await _service.LessonService.GetAdminLessonWithContentBlocksAsync(request.Id, cancellationToken);

            if (lesson is null)
                return NotFound<AdminLessonDetailDto>(_localizer[SharedResourcesKey.LessonNotFound]);

            // LessonsProfile maps ContentBlocks filtered (non-deleted, ordered by SequenceOrder).
            var dto = _mapper.Map<AdminLessonDetailDto>(lesson);
            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetAdminLessonQuery");
            return ServerError<AdminLessonDetailDto>();
        }
    }
}
