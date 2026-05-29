using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;

/// <summary>
/// Handles <see cref="GetLessonQuery"/>.
/// Hand-rolls the load of <see cref="Lesson"/> + first <see cref="QuizQuestion"/> (by Id ASC)
/// and maps to <see cref="SingleLessonResponse"/> with <c>QuickCheck</c> filled manually.
///
/// SECURITY: <c>CorrectAnswer</c> is excluded via <c>QuizProfile</c>'s
/// <c>ForSourceMember(...DoNotValidate())</c> — never echoed to the client.
/// ex.Message is NOT propagated to the response (Q12 fix).
/// </summary>
public class GetLessonQueryHandler
    : BaseResponseHandler, IQueryHandler<GetLessonQuery, BaseResponse<SingleLessonResponse>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetLessonQueryHandler(
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

    public async Task<BaseResponse<SingleLessonResponse>> Handle(
        GetLessonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the lesson by id. AsNoTracking — query handler; no writes.
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.Id, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return NotFound<SingleLessonResponse>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Load the first QuizQuestion for the lesson by Id ASC (Q2 rule — first-by-Id).
            // Null when no questions exist; QuickCheck is null in that case (valid per AC1).
            var firstQuestion = await _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.LessonId == request.Id, false)
                .OrderBy(q => q.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Map Lesson → SingleLessonResponse (Explanation + Visual auto-map by name).
            var dto = _mapper.Map<SingleLessonResponse>(lesson);

            // Fill QuickCheck manually — CorrectAnswer excluded via QuizProfile (QuizProfile.cs:21-26).
            dto.QuickCheck = firstQuestion is not null
                ? _mapper.Map<QuizQuestionDto>(firstQuestion)
                : null;

            return Success(dto);
        }
        catch (Exception ex)
        {
            // [F3] Log full exception server-side; do NOT echo ex.Message to the client (Q12 fix).
            _logger.LogError(ex, "Error: in GetLessonQuery");
            return ServerError<SingleLessonResponse>();
        }
    }
}
