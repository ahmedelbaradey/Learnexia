using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetStudentAttempts;

/// <summary>
/// Handles GetStudentAttemptsQuery.
///
/// Returns all attempts for the requesting student, ordered by StartedAt descending.
/// Authorization scope: a student may only read their own attempts; the handler compares
/// the route-supplied StudentId against the JWT-resolved UserId (IDOR guard).
/// Parent / admin scoping is deferred to Phase 5 / Phase 7.
///
/// Empty list is a valid response (200 + EmptyCollection) — do NOT return 404.
/// CorrectAnswer is NEVER in AttemptListItemDto.
/// </summary>
public class GetStudentAttemptsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetStudentAttemptsQuery, BaseResponse<List<AttemptListItemDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetStudentAttemptsQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<AttemptListItemDto>>> Handle(
        GetStudentAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Inline validation: queries are NOT auto-validated by ValidationBehavior.
            if (request.StudentId <= 0)
                return BadRequest<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.StudentIdMustBePositive]);

            // Step 2 — Authorization scope: student may only read their own attempts (IDOR guard).
            var currentUserId = _currentUser.UserId;
            if (currentUserId is null || request.StudentId != currentUserId.Value)
                return Unauthorized<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 3 — Query: fetch all attempts for this student, most recent first.
            var attempts = await _repository.Learning
                .GetByCondition<Attempt>(a => a.StudentId == request.StudentId, trackChanges: false)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync(cancellationToken);

            // Step 4 — Empty list is valid; return EmptyCollection (200) rather than 404.
            if (!attempts.Any())
                return EmptyCollection(new List<AttemptListItemDto>());

            // Step 5 — Map via AutoMapper (Status enum → string conversion is handled in QuizProfile).
            var dtos = _mapper.Map<List<AttemptListItemDto>>(attempts);

            // Step 6 — Return.
            var result = Success(dtos);
            result.Message = _localizer[SharedResourcesKey.AttemptsRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetStudentAttemptsQuery");
            return ServerError<List<AttemptListItemDto>>();
        }
    }
}
