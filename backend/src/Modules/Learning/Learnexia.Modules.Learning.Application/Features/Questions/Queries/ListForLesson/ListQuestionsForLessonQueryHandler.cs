using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Queries.ListForLesson;

/// <summary>
/// Returns all non-deleted quiz questions for a lesson ordered by SequenceOrder.
/// Admin-gated route — returns all questions including inactive ones.
/// Returns <see cref="AdminQuestionDto"/> which includes CorrectAnswer (admin authoring only).
///
/// SECURITY: This handler is wired to AdminOnly-gated endpoints only.
/// Student reads use StartAttemptCommandHandler which returns QuizQuestionDto (no CorrectAnswer).
/// </summary>
public class ListQuestionsForLessonQueryHandler
    : BaseResponseHandler, IQueryHandler<ListQuestionsForLessonQuery, BaseResponse<List<AdminQuestionDto>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListQuestionsForLessonQueryHandler(
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

    public async Task<BaseResponse<List<AdminQuestionDto>>> Handle(
        ListQuestionsForLessonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify the lesson exists (global IsDeleted filter active).
            var lessonExists = await _repository.Learning
                .AnyAsync<Lesson>(l => l.Id == request.LessonId);

            if (!lessonExists)
                return NotFound<List<AdminQuestionDto>>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Return all non-deleted questions (global filter excludes IsDeleted=true).
            // IsActive is NOT filtered here — admin sees all, including inactive.
            // Cap at 500 as a DoS backstop (mirrors the reorder bound; a lesson realistically has <50 questions).
            var questions = await _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId, false)
                .OrderBy(q => q.SequenceOrder)
                .Take(500)
                .ToListAsync(cancellationToken);

            var dtos = _mapper.Map<List<AdminQuestionDto>>(questions);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListQuestionsForLessonQuery");
            return ServerError<List<AdminQuestionDto>>();
        }
    }
}
