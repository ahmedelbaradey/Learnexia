using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.GetAdmin;

/// <summary>
/// Returns the full admin lesson detail including all non-deleted content blocks (incl. inactive).
/// This query is admin-only — the controller gate enforces <c>AdminOnly</c> policy.
/// No language guard is applied here (admin accesses any lesson directly by ID).
/// </summary>
public class GetAdminLessonQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdminLessonQuery, BaseResponse<AdminLessonDetailDto>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetAdminLessonQueryHandler(
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

    public async Task<BaseResponse<AdminLessonDetailDto>> Handle(
        GetAdminLessonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the lesson with its content blocks.
            // Global IsDeleted filter applies — deleted lessons return NotFound.
            // IsActive filter is intentionally NOT applied — admin sees inactive lessons.
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.Id, false)
                .Include(l => l.ContentBlocks)
                .FirstOrDefaultAsync(cancellationToken);

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
