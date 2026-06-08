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

namespace Learnexia.Modules.Learning.Application.Features.Questions.Queries.GetById;

/// <summary>
/// Returns a single quiz question for admin authoring.
/// Returns <see cref="AdminQuestionDto"/> which includes CorrectAnswer.
///
/// SECURITY: This handler is wired to AdminOnly-gated endpoints only.
/// The global soft-delete filter means deleted questions are never visible here.
/// </summary>
public class GetQuestionByIdQueryHandler
    : BaseResponseHandler, IQueryHandler<GetQuestionByIdQuery, BaseResponse<AdminQuestionDto>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetQuestionByIdQueryHandler(
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

    public async Task<BaseResponse<AdminQuestionDto>> Handle(
        GetQuestionByIdQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var question = await _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.Id == request.Id, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (question is null)
                return NotFound<AdminQuestionDto>(_localizer[SharedResourcesKey.QuizQuestionNotFound]);

            var dto = _mapper.Map<AdminQuestionDto>(question);
            return Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetQuestionByIdQuery");
            return ServerError<AdminQuestionDto>();
        }
    }
}
